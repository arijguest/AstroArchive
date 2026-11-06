using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace AstroArchive {
 public partial class MainUi {
  static bool ShortSub(Frame frame){return SessionStacking.Eligible(frame);}
  MenuItem ImportStackingMenu(){
   var selected=SelectedImportRows();var rows=plan==null?new List<Frame>():plan.Frames.ToList();return Branch("Stack short exposures",MenuAction("Selected imports…",()=>StackSessions(selected,true),repo!=null&&selected.Any(ShortSub)&&!SearchBlocked("ImportSearchBox")),MenuAction("All imports in this list…",()=>StackSessions(rows,true),repo!=null&&rows.Any(ShortSub)&&!SearchBlocked("ImportSearchBox")));
  }
  MenuItem LibraryStackingMenu(List<Frame> selected){
   var snapshot=selected.ToList();return Branch("Stack short exposures",MenuAction("Selected images…",()=>StackSessions(snapshot,false),repo!=null&&snapshot.Any(ShortSub)&&!SearchBlocked("SearchBox")),MenuAction("All images in repository…",()=>StackSessions(all.ToList(),false),repo!=null&&all.Any(ShortSub)),new Separator(),MenuAction("Restore replaced subs…",RestoreStackedOriginals,repo!=null));
  }
  void RestoreStackedOriginals(){
   if(repo==null||RepositoryOperationBlocked)return;string folder=Folder("Choose a stacking job containing recovery.json",Path.Combine(repo.Meta,"stacking"));if(folder==null)return;
   var repository=repo;Run(ct=>{var result=SessionStacking.RestoreOriginals(repository,folder,ct,Progress);return result.Imported+" originals restored; "+result.Duplicates+" already archived."+(result.Failed>0?" "+result.Failed+" failed: "+string.Join("; ",result.Errors):"");},message=>{L("StatusLabel").Text=message;if(completionActivity!=null){completionActivity.OutputPath=folder;completionActivity.ReportTitle="Stacking recovery";completionActivity.Report=message;}},"Restoring stacked original subs");
  }
  void StackSessions(List<Frame> selection,bool imports){
   if(repo==null||RepositoryOperationBlocked)return;var inputs=selection.Where(ShortSub).ToList();if(inputs.Count==0)return;
   var dialog=new FormWindow(Window,"Stack short exposures by session",700,700);dialog.Text(inputs.Count+" eligible subs of 10 seconds or less",true);
   dialog.Text("Each session and compatible capture setup needs at least two qualifying subs and is stacked separately. Quality is measured first; only yellow and green subs are used. Red, rejected, damaged, unknown-exposure and longer exposures are retained and skipped.");
   var weighting=dialog.Select("Weighting",new[]{"Quality weighting (recommended)","Equal weighting"},"Quality weighting (recommended)");
   dialog.Text("Matching library calibration frames are copied into the working folder and retained in the library. The nearest compatible dark temperature is used; a large difference is reported when there is no closer alternative. Already calibrated subs receive no additional calibration.");
   string detected="";try{detected=SessionStacking.ResolveExecutable(settings.SirilExecutable);}catch{}
   var executable=dialog.Input("Siril executable",detected);dialog.Button("Choose Siril…",()=>{var picker=new OpenFileDialog{Filter="Siril executable|siril-cli.exe;siril.exe|Executable|*.exe",FileName=executable.Text};if(picker.ShowDialog(dialog.Window)==true)executable.Text=picker.FileName;});
   var cleanup=dialog.Check("Replace stacked archive subs with the verified stack",false);UiHelp.Tip(cleanup,"After the stack is imported, checkpointed and verified, retain verified recovery copies before deleting contributing archive subs. Calibration frames and source mirrors are retained.");
   if(imports)dialog.Text("Selected pending subs are copied and verified into the archive first. Source originals are retained. All imports means every row remaining in this list, including rows hidden by filters.");else dialog.Text("All images means the entire repository, including files hidden by filters. Selected images affects only the explicit selection.");
   dialog.Text("Recovery copies and processing reports remain in the job folder. Cancellation or a failed stack retains the originals; any completed stack stays archived. You can continue browsing while this runs.");
   string command=null;dialog.Accept("Start background stacking",()=>{try{command=SessionStacking.ResolveExecutable(executable.Text);return true;}catch(Exception error){LocalizedMessage.Show(dialog.Window,error.Message,"Siril required");return false;}});if(!dialog.Show())return;
   string jobFolder=null;ActivityEntry jobActivity=null;
   var options=new SessionStackOptions{SirilExecutable=command,QualityWeighting=Convert.ToString(weighting.SelectedItem)=="Quality weighting (recommended)",DeleteOriginals=cleanup.IsChecked==true,JobCreated=folder=>{jobFolder=folder;Window.Dispatcher.Invoke(new Action(()=>{jobActivity=currentActivity;if(jobActivity!=null){jobActivity.OutputPath=folder;jobActivity.ReportTitle="Session stacking report";}}));}};
   var repository=repo;var importPlan=plan;var snapshots=inputs.Select(f=>f.Clone()).ToList();SessionStackResult result=null;var importWarnings=new List<string>();
   Run(ct=>{
    try{
     if(imports){var pending=snapshots.Where(f=>Repository.PendingMetadata(f,true)).ToList();foreach(var row in pending.Where(f=>f.Status=="Duplicate in source"))row.Status="New";if(pending.Count>0){var copied=repository.Import(pending,ct,Progress,new ImportOptions{SourceRoot=importPlan.Source,DeleteOriginals=false,Workers=1});importWarnings.AddRange(copied.Errors);importWarnings.AddRange(copied.Warnings);}}
     var indexed=repository.All().ToDictionary(f=>f.Hash);var archived=new List<Frame>();foreach(var row in snapshots){Frame current;if(!string.IsNullOrEmpty(row.Hash)&&indexed.TryGetValue(row.Hash,out current))archived.Add(current);else importWarnings.Add(row.OriginalName+": not archived; skipped and source retained.");}
     if(archived.Count==0)throw new InvalidOperationException("No selected subs could be verified in the archive. Source files retained.");
     result=SessionStacking.Run(repository,archived,options,ct,Progress);return result.Stacked+" session stacks saved; "+result.Deleted+" archive subs replaced."+((result.Errors.Count+result.Warnings.Count+importWarnings.Count)>0?" Review the stacking report.":"");
    }finally{string retainedReport=null;if(jobFolder!=null)try{retainedReport=File.ReadAllText(Path.Combine(jobFolder,"report.txt"));}catch{}Window.Dispatcher.Invoke(new Action(()=>{if(jobActivity!=null&&retainedReport!=null){jobActivity.Report=retainedReport;if(result==null)jobActivity.NeedsReview=true;}if(imports&&plan==importPlan&&importPlan!=null){foreach(var row in snapshots){int index=importPlan.Frames.FindIndex(f=>string.Equals(f.SourcePath,row.SourcePath,StringComparison.OrdinalIgnoreCase));if(index>=0)importPlan.Frames[index]=row;}FilterImports();}}));}
   },message=>{L("StatusLabel").Text=message;if(completionActivity!=null&&result!=null){completionActivity.OutputPath=result.RecoveryFolder;completionActivity.ReportTitle="Session stacking report";completionActivity.Report=result.Report+(importWarnings.Count>0?"\r\nImport warnings\r\n"+string.Join("\r\n",importWarnings):"");completionActivity.NeedsReview=result.Errors.Count+result.Warnings.Count+importWarnings.Count>0;}},"Stacking short exposures by session");
  }
 }
}
