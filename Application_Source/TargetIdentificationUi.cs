using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace AstroArchive {
 public partial class MainUi {
  volatile IdentificationProgress identificationProgress;
  Action pendingTargetReview;bool targetReviewQueued;
  void ShowTargetReviewWhenReady(Action review){
   if(closing)return;
   if(Window.IsVisible&&Window.WindowState!=WindowState.Minimized&&cancel==null)review();else pendingTargetReview=review;
  }
  void ResumeTargetReview(){
   if(pendingTargetReview==null||closing||targetReviewQueued)return;
   // Native restore/visibility events can arrive before WPF finishes updating the owner.
   // Test readiness on the dispatcher after those events; activation is a second trigger.
   targetReviewQueued=true;Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{
    targetReviewQueued=false;
    if(pendingTargetReview==null||closing||cancel!=null||!Window.IsVisible||Window.WindowState==WindowState.Minimized)return;
    var review=pendingTargetReview;pendingTargetReview=null;review();
   }));
  }
  void IdentificationStage(IdentificationProgress value){identificationProgress=value;var metrics=activeMetrics;if(metrics!=null){metrics.Stage=value.Stage;metrics.UpdateLegacy(value.Completed,value.Total,value.Detail);}}
  SolveResult SolveIdentification(Frame sample,bool imported,CancellationToken ct,Action<string> progress){
   if(PendingImport(sample,imported)){
    var before=FileStamp.Read(sample.SourcePath);if(sample.SourceStamp!=null&&!sample.SourceStamp.ContentSame(before))throw new IOException("Source changed since scanning; scan again.");
    var result=PlateSolve.Solve(sample,sample.SourcePath,settings,ct,progress);if(!before.ContentSame(FileStamp.Read(sample.SourcePath)))throw new IOException("Source changed while solving; scan again.");return result;
   }
   return repo.CachedSolve(sample,settings,ct,progress);
  }
  void IdentifyByPlate(bool imported,List<Frame> selected,int named){
   if(!PlateSolve.Configured(settings)){Configure(2);if(!PlateSolve.Configured(settings)){L("StatusLabel").Text=named+" filename matches; "+selected.Count+" captures need a configured plate solver.";return;}}
   List<TargetSolveJob> jobs=null;var repository=repo;
   Run(ct=>{jobs=TargetSolving.Plan(selected);TargetSolving.Solve(jobs,(frame,token,stage)=>SolveIdentification(frame,imported,token,stage),ct,IdentificationStage);return "";},done=>ShowTargetReviewWhenReady(()=>{
    if(closing||repository!=repo)return;
    if(!jobs.Any(j=>j.Solved)){L("StatusLabel").Text="No fields solved. Capture metadata was retained.";ShowReport("Target identification",IdentificationReport(jobs));return;}
    if(jobs.Count!=1||!jobs[0].Include){var dialog=IdentificationDialog(jobs);if(!dialog.Show()){L("StatusLabel").Text="Solved "+jobs.Count(j=>j.Solved)+" fields; metadata changes were not applied.";return;}}
    ApplyIdentifications(jobs,imported,named);
   }));
  }
  void ApplyIdentifications(List<TargetSolveJob> jobs,bool imported,int named){
   var chosen=jobs.Where(j=>j.Solved&&j.Include).ToList();int updated=0;var errors=new List<string>();
   cancellationMessage="Identification canceled. Metadata already applied is retained.";
   Run(ct=>{int done=0,total=chosen.Sum(j=>j.Frames.Count);foreach(var job in chosen)foreach(var original in job.Frames){ct.ThrowIfCancellationRequested();IdentificationStage(new IdentificationProgress{Completed=done,Total=total,Metadata=true,Stage="Applying target metadata",Detail=original.OriginalName+"\n"+Catalog.Label(job.Target)+" · "+job.Scope});try{var frame=TargetSolving.Apply(job,original,job.Target);StoreIdentification(original,frame,imported,ct);updated++;}catch(OperationCanceledException){throw;}catch(Exception error){errors.Add(original.OriginalName+": "+error.Message);}done++;}IdentificationStage(new IdentificationProgress{Completed=done,Total=total,Metadata=true,Stage="Saving archive index",Detail=updated+" captures updated"});if(!imported||chosen.Any(j=>j.Frames.Any(f=>!PendingImport(f,imported))))repo.Checkpoint(ct);return "";},done=>{
    if(imported)FilterImports();L("StatusLabel").Text=(named+updated)+" captures identified · "+jobs.Count(j=>j.Solved)+" fields solved · "+jobs.Count(j=>!j.Solved)+" failed";
    if(errors.Count>0||jobs.Any(j=>!j.Solved))ShowReport("Target identification report",IdentificationReport(jobs)+(errors.Count==0?"":"\n\nMetadata not applied:\n"+string.Join("\n",errors)));
   });
  }
  static string IdentificationReport(IEnumerable<TargetSolveJob> jobs){return string.Join("\n\n",jobs.Select(j=>j.Filename+"\n"+j.Scope+"\n"+(j.Solved?j.Centre+"\n"+j.TargetLabel+" · "+j.Match:j.Error)));}
  FormWindow IdentificationDialog(List<TargetSolveJob> jobs){
   var dialog=new FormWindow(Window,"Review solved targets",1080,680);dialog.Text(jobs.Count(j=>j.Solved)+" fields solved · "+jobs.Sum(j=>j.Frames.Count)+" selected captures",true);
   dialog.Text("Nearest major targets are selected. Change a target or uncheck a row to keep its metadata. One Light row applies to its entire selected session group.");
   var table=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=false,Height=340,SelectionMode=DataGridSelectionMode.Single,ItemsSource=jobs};
   var apply=new FrameworkElementFactory(typeof(CheckBox));apply.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,new Binding("Include"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});apply.SetBinding(UIElement.IsEnabledProperty,new Binding("Solved"));apply.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
   table.Columns.Add(new DataGridTemplateColumn{Header="Apply",CellTemplate=new DataTemplate{VisualTree=apply},Width=58});
   table.Columns.Add(new DataGridTextColumn{Header="Representative / stack",Binding=new Binding("Filename"),IsReadOnly=true,Width=new DataGridLength(1.5,DataGridLengthUnitType.Star)});
   table.Columns.Add(new DataGridTextColumn{Header="Captures",Binding=new Binding("Scope"),IsReadOnly=true,Width=new DataGridLength(1.2,DataGridLengthUnitType.Star)});
   var combo=new FrameworkElementFactory(typeof(ComboBox));combo.SetBinding(ComboBox.ItemsSourceProperty,new Binding("Choices"));combo.SetValue(ComboBox.DisplayMemberPathProperty,"Label");combo.SetValue(ComboBox.SelectedValuePathProperty,"Name");combo.SetBinding(ComboBox.SelectedValueProperty,new Binding("Target"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});combo.SetBinding(UIElement.IsEnabledProperty,new Binding("Solved"));combo.SetValue(FrameworkElement.ToolTipProperty,"Choose a target; use Search for another name or ID.");
   table.Columns.Add(new DataGridTemplateColumn{Header="Target",CellTemplate=new DataTemplate{VisualTree=combo},Width=new DataGridLength(1.1,DataGridLengthUnitType.Star)});
   table.Columns.Add(new DataGridTextColumn{Header="Match / failure",Binding=new Binding("Match"),IsReadOnly=true,Width=new DataGridLength(1.4,DataGridLengthUnitType.Star)});
   var rowStyle=new Style(typeof(DataGridRow));rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Centre")));table.RowStyle=rowStyle;dialog.Add(table);
   dialog.Button("Search target for selected row…",()=>{table.CommitEdit(DataGridEditingUnit.Cell,true);table.CommitEdit(DataGridEditingUnit.Row,true);var job=table.SelectedItem as TargetSolveJob;if(job==null||!job.Solved)return;string target=ChooseTarget(job.Result,job.Target);if(target!=null){job.Target=target;job.Include=true;table.Items.Refresh();}});
   dialog.Accept("Apply matches",()=>{table.CommitEdit(DataGridEditingUnit.Cell,true);table.CommitEdit(DataGridEditingUnit.Row,true);var invalid=jobs.FirstOrDefault(j=>j.Include&&(!j.Solved||Catalog.IsAmbiguous(Catalog.Normalize(j.Target))));if(invalid!=null){MessageBox.Show(dialog.Window,"Choose a target for each checked, successfully solved row. Failed rows must remain unchecked.");return false;}return true;});return dialog;
  }
  SolveResult SolveAutomaticFrame(Frame frame,Settings config,CancellationToken token,Action<string> progress,PipelineMetrics metrics){
   using(var stage=metrics.Begin("Target identification",frame.OriginalName)){var result=repo.CachedSolve(frame,config,token,progress,n=>stage.Bytes(n));stage.Complete();return result;}
  }
  void AutoIdentify(List<Frame> frames,CancellationToken ct,PipelineMetrics metrics){
   var jobs=TargetSolving.Plan(frames);TargetSolving.Solve(jobs,(frame,token,stage)=>SolveAutomaticFrame(frame,settings,token,stage,metrics),ct,IdentificationStage);
   int count=0,total=jobs.Where(j=>j.Include).Sum(j=>j.Frames.Count);
   foreach(var job in jobs){ct.ThrowIfCancellationRequested();foreach(var original in job.Frames){ct.ThrowIfCancellationRequested();if(job.Include){IdentificationStage(new IdentificationProgress{Completed=count,Total=total,Metadata=true,Stage="Applying target metadata",Detail=original.OriginalName+"\n"+job.TargetLabel});repo.Refile(TargetSolving.Apply(job,original,job.Target),ct);count++;}
     else{var frame=job.Solved?TargetSolving.WithPointing(job,original):original.Clone();if(job.Solved){frame.Notes=(frame.Notes??"")+job.Match+". Use Identify target to review. ";}else frame.Notes=(frame.Notes??"")+"Plate solve unavailable: "+job.Error+". ";repo.Save(frame);}
    }if(job.Error!=null&&plan!=null)plan.Errors.Add(job.Filename+": "+job.Error);
   }
   identificationProgress=null;metrics.Phase(0,0,"Target identification complete");
  }
 }
}
