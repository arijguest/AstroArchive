// Named mosaic collections, explicit panel assignments and inexpensive metadata review.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace AstroArchive {
 public sealed class MosaicPanelChoice {
  public string Id,Name,Kind;public int Count;
  public string Label{get{return Name+" · "+Count;}}
 }
 public sealed class MosaicRow {
  public Frame Frame;public MosaicMember Member;public string Panel{get;set;}
  public string Filename{get{return Frame==null?"Missing capture "+Member.Hash.Substring(0,12):Frame.OriginalName;}}
  public string Target{get{return Frame==null?"-":Frame.Target;}}public string Night{get{return Frame==null?"-":Frame.Night;}}public string Filter{get{return Frame==null?"-":Frame.Filter;}}
  public string State{get{return Frame==null?"Missing":Member.State;}}
 }
 public partial class MainUi {
  bool mosaicUpdating;List<MosaicRow> mosaicRows=new List<MosaicRow>();List<MosaicMember> mosaicCollectionMembers=new List<MosaicMember>();
  ListBox MosaicCollections{get{return (ListBox)Window.FindName("MosaicCollectionsList");}}
  ListBox MosaicPanels{get{return (ListBox)Window.FindName("MosaicPanelsList");}}
  MosaicProject ActiveMosaic{get{return MosaicCollections.SelectedItem as MosaicProject;}}
  MosaicPanelChoice ActiveMosaicPanel{get{return MosaicPanels.SelectedItem as MosaicPanelChoice;}}
  List<MosaicRow> SelectedMosaicRows(){return G("MosaicGrid").SelectedItems.Cast<MosaicRow>().ToList();}
  void InitializeMosaics(){
   B("NewMosaicButton").Click+=(s,e)=>NewMosaic();B("DetectMosaicsButton").Click+=(s,e)=>DiscoverMosaics(Context());B("AssignMosaicButton").Click+=(s,e)=>AssignToMosaic(SelectedFiles());
   B("DetectMosaicsButton").ToolTip="Use selected library captures, or the visible library captures when none are selected. Reuses indexed metadata.";B("AssignMosaicButton").ToolTip="Select captures in Library, then assign them to a mosaic panel or completed output.";B("MosaicExportButton").Click+=(s,e)=>ExportMosaicCollection();B("MosaicToolsButton").Click+=(s,e)=>MosaicTools();B("MosaicConfirmButton").Click+=(s,e)=>ConfirmMosaicSelection();B("MosaicRemoveButton").Click+=(s,e)=>RemoveMosaicSelection();
   MosaicCollections.SelectionChanged+=(s,e)=>{if(!mosaicUpdating)RefreshMosaicPanels();};MosaicPanels.SelectionChanged+=(s,e)=>{if(!mosaicUpdating)RefreshMosaicRows();};
   G("MosaicGrid").SelectionChanged+=(s,e)=>MosaicSelectionChanged();
   G("MosaicGrid").MouseDoubleClick+=(s,e)=>{var row=G("MosaicGrid").SelectedItem as MosaicRow;if(row!=null&&row.Frame!=null&&cancel==null)PreviewImage(row.Frame);};
   bool onRow=false;var grid=G("MosaicGrid");grid.PreviewMouseRightButtonDown+=(s,e)=>{var row=ItemsControl.ContainerFromElement(grid,e.OriginalSource as DependencyObject) as DataGridRow;onRow=row!=null;if(row==null){grid.ContextMenu.IsOpen=false;e.Handled=true;return;}if(!grid.SelectedItems.Contains(row.Item)){grid.SelectedItems.Clear();grid.SelectedItems.Add(row.Item);}row.Focus();e.Handled=true;};
   var menu=ThemedMenu();G("MosaicGrid").ContextMenu=menu;G("MosaicGrid").ContextMenuOpening+=(s,e)=>{var selected=SelectedMosaicRows();if(cancel!=null||selected.Count==0||e.CursorLeft>=0&&!onRow){e.Handled=true;return;}menu.Items.Clear();menu.Items.Add(FileAction("Assign to panel or completed output…",()=>AssignToMosaic(selected.Where(r=>r.Frame!=null).Select(r=>r.Frame).ToList())));menu.Items.Add(FileAction("Confirm selected membership",ConfirmMosaicSelection,selected.All(r=>r.Frame!=null&&(r.Member.PanelId!=null||r.Member.Role=="Output"))));menu.Items.Add(FileAction("Remove from this collection",RemoveMosaicSelection));menu.Items.Add(FileAction("Preview image…",()=>PreviewImage(selected[0].Frame),selected.Count==1&&selected[0].Frame!=null));};
  }
  void SetMosaicBusy(bool busy){
   foreach(string name in new[]{"NewMosaicButton","DetectMosaicsButton","AssignMosaicButton","MosaicExportButton","MosaicToolsButton","MosaicConfirmButton","MosaicRemoveButton"})B(name).IsEnabled=!busy&&repo!=null;
   if(!busy)MosaicSelectionChanged();
  }
  void RefreshMosaics(){
   string id=ActiveMosaic==null?null:ActiveMosaic.Id;mosaicUpdating=true;try{var projects=repo==null?new List<MosaicProject>():repo.Mosaics().Where(p=>!p.IgnoreDiscovery).ToList();MosaicCollections.ItemsSource=projects;MosaicCollections.SelectedItem=projects.FirstOrDefault(p=>p.Id==id)??projects.FirstOrDefault();}finally{mosaicUpdating=false;}RefreshMosaicPanels();
  }
  void RefreshMosaicPanels(){
   var p=ActiveMosaic;string id=ActiveMosaicPanel==null?null:ActiveMosaicPanel.Id,kind=ActiveMosaicPanel==null?"All":ActiveMosaicPanel.Kind;
   var choices=new List<MosaicPanelChoice>();var members=p==null||repo==null?new List<MosaicMember>():repo.MosaicMembers(p.Id).Where(m=>m.State!="Ignored").ToList();mosaicCollectionMembers=members;
   if(p!=null){choices.Add(new MosaicPanelChoice{Name="All panels",Kind="All",Count=members.Count});choices.AddRange(p.Panels.OrderBy(panel=>panel.Name,StringComparer.OrdinalIgnoreCase).Select(panel=>new MosaicPanelChoice{Id=panel.Id,Name=panel.Name,Kind="Panel",Count=members.Count(m=>m.PanelId==panel.Id)}));choices.Add(new MosaicPanelChoice{Name="Unassigned",Kind="Unassigned",Count=members.Count(m=>m.PanelId==null&&m.Role=="Input")});choices.Add(new MosaicPanelChoice{Name="Completed outputs",Kind="Output",Count=members.Count(m=>m.Role=="Output")});}
   mosaicUpdating=true;try{MosaicPanels.ItemsSource=choices;MosaicPanels.SelectedItem=choices.FirstOrDefault(x=>x.Id==id&&x.Kind==kind)??choices.FirstOrDefault();}finally{mosaicUpdating=false;}RefreshMosaicRows();DrawMosaicMap(p);
  }
  void RefreshMosaicRows(){
   var p=ActiveMosaic;var choice=ActiveMosaicPanel;mosaicRows.Clear();var frames=all.Where(f=>f.Hash!=null).ToDictionary(f=>f.Hash);
   if(p!=null&&repo!=null)foreach(var m in mosaicCollectionMembers){
    if(choice!=null&&!(choice.Kind=="All"||choice.Kind=="Panel"&&choice.Id==m.PanelId||choice.Kind=="Output"&&m.Role=="Output"||choice.Kind=="Unassigned"&&m.Role=="Input"&&m.PanelId==null))continue;
    Frame f;frames.TryGetValue(m.Hash,out f);var panel=p.Panels.FirstOrDefault(x=>x.Id==m.PanelId);mosaicRows.Add(new MosaicRow{Frame=f,Member=m,Panel=m.Role=="Output"?"Completed output":panel==null?"Unassigned":panel.Name});
   }
   G("MosaicGrid").ItemsSource=mosaicRows.OrderBy(r=>r.Panel).ThenBy(r=>r.Night).ThenBy(r=>r.Filename).ToList();
   L("MosaicSummaryLabel").Text=p==null?"Collections link captures across targets and nights. Detection reuses metadata; plate solving is optional.":p.Label+" · "+mosaicRows.Count+" shown · "+mosaicRows.Count(r=>r.Member.State=="Suggested"||r.Member.PanelId==null&&r.Member.Role=="Input")+" need review · "+mosaicRows.Count(r=>r.Frame==null)+" missing archive records";
   if(repo!=null&&!string.IsNullOrEmpty(repo.MosaicWarning))L("MosaicSummaryLabel").Text=repo.MosaicWarning;MosaicSelectionChanged();
  }
  void MosaicSelectionChanged(){
   var selected=SelectedMosaicRows();bool ready=repo!=null&&cancel==null,present=selected.Count>0;
   B("MosaicConfirmButton").IsEnabled=ready&&present&&selected.All(r=>r.Frame!=null&&(r.Member.PanelId!=null||r.Member.Role=="Output"));
   B("MosaicRemoveButton").IsEnabled=ready&&present;B("MosaicExportButton").IsEnabled=ready&&ActiveMosaic!=null&&mosaicCollectionMembers.Any(m=>repositoryRows.ContainsKey(m.Hash));B("AssignMosaicButton").IsEnabled=ready&&SelectedFiles().Any(Repository.MosaicScience);
   B("MosaicToolsButton").IsEnabled=ready;L("MosaicDetailsLabel").Text=selected.Count==1?selected[0].Member.State+" · "+selected[0].Member.Evidence:selected.Count>1?selected.Count+" selected captures.":"Select captures to confirm, reassign or remove their membership.";if(selected.Count==1&&selected[0].Frame!=null&&selected[0].Frame.Sky!=null){var sky=selected[0].Frame.Sky;L("MosaicDetailsLabel").Text+=" · Centre RA "+sky.RA.ToString("0.0000",CultureInfo.InvariantCulture)+"°, Dec "+sky.Dec.ToString("0.0000",CultureInfo.InvariantCulture)+"° · "+sky.Evidence;}L("MosaicDetailsLabel").ToolTip=L("MosaicDetailsLabel").Text;
  }
  void MosaicMutation(Func<string> change,Action completed=null){if(repo==null||cancel!=null)return;Run(ct=>{string message=change();repo.Checkpoint(ct);return message;},message=>{L("StatusLabel").Text=message;if(completed!=null)completed();});}
  void SelectMosaic(string id){MosaicCollections.SelectedItem=MosaicCollections.Items.Cast<MosaicProject>().FirstOrDefault(p=>p.Id==id);((TabControl)Window.FindName("MainTabs")).SelectedIndex=2;}
  bool ValidMosaicName(FormWindow d,string name){if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>160){MessageBox.Show(d.Window,"Enter a name with 1–160 characters.");return false;}return true;}
  void NewMosaic(){
   if(repo==null||cancel!=null)return;var d=new FormWindow(Window,"New mosaic",530,370);d.Text("Create a collection",true);d.Text("Add panels and link captures from any target, telescope or night.");var name=d.Input("Mosaic name","");d.Accept("Create mosaic",()=>ValidMosaicName(d,name.Text));if(!d.Show())return;string id=null;MosaicMutation(()=>{var p=repo.CreateMosaic(name.Text);id=p.Id;return "Created "+p.Name+".";},()=>SelectMosaic(id));
  }
  void AssignToMosaic(List<Frame> selection){
   if(repo==null||cancel!=null)return;var rows=selection.Where(Repository.MosaicScience).ToList();if(rows.Count==0)return;
   var d=new FormWindow(Window,"Assign mosaic captures",620,650);d.Text(rows.Count+" selected captures",true);d.Text("Choose their panel, or attach a completed stitched image as an output. Existing targets, sessions and archived files are retained.");
   var projects=repo.Mosaics().Where(p=>!p.IgnoreDiscovery).ToList();var create=new MosaicProject{Name="Create a new mosaic…"};projects.Add(create);d.Text("Mosaic collection");var projectBox=new ComboBox{ItemsSource=projects,DisplayMemberPath="Name",SelectedItem=projects.FirstOrDefault(p=>ActiveMosaic!=null&&p.Id==ActiveMosaic.Id)??projects.First()};d.Add(projectBox);var name=d.Input("New mosaic name","");
   d.Text("Panel");var panelBox=new ComboBox{DisplayMemberPath="Name"};d.Add(panelBox);var panelName=d.Input("New panel name","Panel 01");var output=d.Check("Attach as completed stitched output",false);
   Action refresh=()=>{var project=(MosaicProject)projectBox.SelectedItem;name.IsEnabled=project.Id==null;panelName.Text="Panel "+(project.Panels.Count+1).ToString("00");var panels=project.Panels.ToList();panels.Add(new MosaicPanel{Name="Create a new panel…"});panelBox.ItemsSource=panels;panelBox.SelectedItem=panels.FirstOrDefault(p=>ActiveMosaicPanel!=null&&p.Id==ActiveMosaicPanel.Id&&p.Id!=null)??panels.First();};projectBox.SelectionChanged+=(s,e)=>refresh();panelBox.SelectionChanged+=(s,e)=>panelName.IsEnabled=output.IsChecked!=true&&panelBox.SelectedItem!=null&&((MosaicPanel)panelBox.SelectedItem).Id==null;output.Checked+=(s,e)=>{panelBox.IsEnabled=false;panelName.IsEnabled=false;};output.Unchecked+=(s,e)=>{panelBox.IsEnabled=true;panelName.IsEnabled=panelBox.SelectedItem!=null&&((MosaicPanel)panelBox.SelectedItem).Id==null;};refresh();
   d.Accept("Assign captures",()=>{var p=(MosaicProject)projectBox.SelectedItem;var panel=panelBox.SelectedItem as MosaicPanel;return (p.Id!=null||ValidMosaicName(d,name.Text))&&(output.IsChecked==true||panel!=null&&(panel.Id!=null||ValidMosaicName(d,panelName.Text)));});if(!d.Show())return;
   string projectId=((MosaicProject)projectBox.SelectedItem).Id,panelId=panelBox.SelectedItem==null?null:((MosaicPanel)panelBox.SelectedItem).Id;bool completedOutput=output.IsChecked==true;
   MosaicMutation(()=>{if(projectId==null)projectId=repo.CreateMosaic(name.Text).Id;if(!completedOutput&&panelId==null)panelId=repo.CreateMosaicPanel(projectId,panelName.Text).Id;repo.AssignMosaic(projectId,panelId,rows,completedOutput);return rows.Count+" captures assigned to the mosaic.";},()=>SelectMosaic(projectId));
  }
  void DiscoverMosaics(List<Frame> selection,bool reread=false){
   if(repo==null||cancel!=null)return;var rows=selection.Where(Repository.MosaicScience).ToList();if(rows.Count==0)return;MosaicDetectionResult result=null;
   Run(ct=>{if(reread)repo.ReadMosaicMetadata(rows,ct,Progress);result=repo.DetectMosaics(rows,ct,true);repo.Checkpoint(ct);return result.ToString();},message=>{((TabControl)Window.FindName("MainTabs")).SelectedIndex=2;L("StatusLabel").Text=message;if(result.Warnings.Count>0)ShowReport("Mosaic metadata review",string.Join("\r\n",result.Warnings));});
  }
  void ConfirmMosaicSelection(){var p=ActiveMosaic;var rows=SelectedMosaicRows();if(p==null||rows.Count==0)return;MosaicMutation(()=>{repo.ConfirmMosaic(p.Id,rows.Select(r=>r.Member.Hash));return rows.Count+" mosaic memberships confirmed.";});}
  void RemoveMosaicSelection(){var p=ActiveMosaic;var rows=SelectedMosaicRows();if(p==null||rows.Count==0)return;MosaicMutation(()=>{repo.RemoveMosaicMembers(p.Id,rows.Select(r=>r.Member.Hash));return rows.Count+" captures removed from the collection; archived files retained.";});}
  void NewMosaicPanel(){var p=ActiveMosaic;if(p==null)return;var d=new FormWindow(Window,"New mosaic panel",530,370);var name=d.Input("Panel name","Panel "+(p.Panels.Count+1).ToString("00"));d.Accept("Create panel",()=>ValidMosaicName(d,name.Text));if(d.Show())MosaicMutation(()=>{repo.CreateMosaicPanel(p.Id,name.Text);return "Mosaic panel created.";});}
  void EditMosaic(){
   var p=ActiveMosaic;if(p==null)return;var d=new FormWindow(Window,"Edit mosaic collection",540,430);var name=d.Input("Mosaic name",p.Name);var expected=d.Input("Planned panels (optional)",p.ExpectedPanels.HasValue?p.ExpectedPanels.ToString():"");int? count=null;
   d.Accept("Save collection",()=>{int n;if(expected.Text.Trim().Length>0&&(!int.TryParse(expected.Text,out n)||n<1||n>10000)){MessageBox.Show(d.Window,"Planned panels must be 1–10000, or blank.");return false;}count=expected.Text.Trim().Length==0?(int?)null:int.Parse(expected.Text);return ValidMosaicName(d,name.Text);});if(d.Show())MosaicMutation(()=>{repo.RenameMosaic(p.Id,name.Text,count);return "Mosaic collection updated.";});
  }
  void EditMosaicPanel(){
   var p=ActiveMosaic;var choice=ActiveMosaicPanel;if(p==null||choice==null||choice.Id==null)return;var d=new FormWindow(Window,"Rename mosaic panel",530,370);var name=d.Input("Panel name",choice.Name);d.Accept("Save panel",()=>ValidMosaicName(d,name.Text));if(d.Show())MosaicMutation(()=>{repo.RenameMosaicPanel(p.Id,choice.Id,name.Text);return "Mosaic panel renamed.";});
  }
  void MosaicTools(){
   if(repo==null||cancel!=null)return;var p=ActiveMosaic;var selection=SelectedMosaicRows().Where(r=>r.Frame!=null).Select(r=>r.Frame).ToList();var source=selection.Count>0?selection:Context();var menu=ThemedMenu();
   menu.Items.Add(FileAction("Read headers/session metadata…",()=>DiscoverMosaics(source,true),source.Any(Repository.MosaicScience)));menu.Items.Add(FileAction("Solve selected representatives…",()=>SolveMosaicRepresentatives(selection),selection.Count>0));menu.Items.Add(new Separator());menu.Items.Add(FileAction("New panel…",NewMosaicPanel,p!=null));menu.Items.Add(FileAction("Edit collection…",EditMosaic,p!=null));menu.Items.Add(FileAction("Rename selected panel…",EditMosaicPanel,p!=null&&ActiveMosaicPanel!=null&&ActiveMosaicPanel.Id!=null));menu.Items.Add(FileAction("Dismiss collection…",()=>DismissMosaic(p),p!=null));menu.PlacementTarget=B("MosaicToolsButton");menu.IsOpen=true;
  }
  void DismissMosaic(MosaicProject p){if(p==null)return;var d=new FormWindow(Window,"Dismiss mosaic collection",560,400);d.Text(p.Name,true);d.Text("Remove this collection from the mosaic view and retain that choice during later detection. Its captures stay in the archive.");d.Accept("Dismiss collection",()=>true);if(d.Show())MosaicMutation(()=>{repo.IgnoreMosaic(p.Id);return "Collection dismissed; captures retained.";});}
  void ExportMosaicCollection(){
   var p=ActiveMosaic;if(p==null||repo==null||cancel!=null)return;var hashes=new HashSet<string>(repo.MosaicMembers(p.Id).Where(m=>m.State!="Ignored").Select(m=>m.Hash));ExportProject(all.Where(f=>hashes.Contains(f.Hash)).ToList(),true,p.Id);
  }
  void SolveMosaicRepresentatives(List<Frame> selection){
   if(repo==null||cancel!=null||selection.Count==0)return;if(!PlateSolve.Configured(settings)){Configure();if(!PlateSolve.Configured(settings))return;}
   var members=repo.MosaicMembers(ActiveMosaic==null?null:ActiveMosaic.Id).Where(m=>m.State!="Ignored"&&m.PanelId!=null).GroupBy(m=>m.Hash).ToDictionary(g=>g.Key,g=>g.First());
   var representatives=selection.Where(Repository.MosaicScience).GroupBy(f=>members.ContainsKey(f.Hash)?members[f.Hash].ProjectId+"|"+members[f.Hash].PanelId:f.Mosaic!=null&&f.Mosaic.PanelKey!=null&&f.Mosaic.Conflict==null?f.Mosaic.ProjectKey+"|"+f.Mosaic.PanelKey:f.Hash).Select(g=>g.FirstOrDefault(f=>f.Kind=="Stack")??g.First()).ToList();if(representatives.Count==0)return;
   var d=new FormWindow(Window,"Solve mosaic representatives",610,450);d.Text(representatives.Count+" representative images",true);d.Text("Run the configured plate solver for these selected pointing groups. Existing cached solutions are reused. Unassigned images are solved individually; their results are kept for review.");d.Text(settings.UseOnline?"Astrometry.net will receive detected star coordinates.":"ASTAP will solve locally.");d.Accept("Solve representatives",()=>true);if(!d.Show())return;
   Run(ct=>{var errors=new List<string>();int done=0;foreach(var f in representatives){ct.ThrowIfCancellationRequested();try{var solution=repo.CachedSolve(f,settings,ct,message=>Progress(new ProgressInfo{Stage="Mosaic solving",Text=message,Done=done,Total=representatives.Count}));f.Sky=solution.Sky??(settings.FieldHeight.HasValue?MosaicGeometry.Approximate(solution.RA,solution.Dec,settings.FieldHeight.Value,f.Width,f.Height):null);if(f.Sky==null)f.Sky=new SkyGeometry{RA=solution.RA,Dec=solution.Dec,Evidence="Plate-solved centre; footprint unavailable"};repo.RecordMosaicGeometry(f,f.Sky);}catch(OperationCanceledException){throw;}catch(Exception e){errors.Add(f.OriginalName+": "+e.Message);}done++;}var result=repo.DetectMosaics(selection,ct,true);repo.Checkpoint(ct);return done+" representatives checked. "+result+"\r\n"+string.Join("\r\n",errors);},message=>{L("StatusLabel").Text=message.Split('\n')[0];if(message.Trim().Contains("\r\n")&&message.Trim().Split('\n').Length>1)ShowReport("Mosaic solving",message);});
  }
  void DrawMosaicMap(MosaicProject project){
   var canvas=(Canvas)Window.FindName("MosaicMap");canvas.Children.Clear();var panels=project==null?new List<MosaicPanel>():project.Panels.Where(p=>p.Sky!=null&&p.Sky.HasFootprint).ToList();
   if(panels.Count==0){var text=new TextBlock{Text="Panel outlines appear when sky mapping is available in the metadata.",Width=195,TextWrapping=TextWrapping.Wrap,FontSize=11};Theme.Bind(text,TextBlock.ForegroundProperty,"Muted");Canvas.SetLeft(text,15);Canvas.SetTop(text,65);canvas.Children.Add(text);return;}
   var origin=panels[0].Sky;var footprints=panels.Select(p=>p.Sky.Corners.Select(c=>MosaicGeometry.Project(c,origin.RA,origin.Dec)).ToArray()).ToList();var points=footprints.Where(g=>g.All(x=>x!=null)).SelectMany(g=>g).ToList();if(points.Count==0)return;double left=points.Min(p=>p[0]),right=points.Max(p=>p[0]),bottom=points.Min(p=>p[1]),top=points.Max(p=>p[1]),scale=Math.Min(195/Math.Max(.00001,right-left),135/Math.Max(.00001,top-bottom));
   for(int i=0;i<panels.Count;i++){var outline=footprints[i];if(outline.Any(x=>x==null))continue;var polygon=new Polygon{StrokeThickness=1.5,Fill=new SolidColorBrush(Color.FromArgb(35,117,109,239)),ToolTip=panels[i].Name+"\n"+panels[i].Sky.Evidence};Theme.Bind(polygon,Shape.StrokeProperty,"Accent");foreach(var point in outline)polygon.Points.Add(new Point(15+(point[0]-left)*scale,20+(top-point[1])*scale));canvas.Children.Add(polygon);var label=new TextBlock{Text=panels[i].Name,FontSize=10,MaxWidth=90,TextTrimming=TextTrimming.CharacterEllipsis};Theme.Bind(label,TextBlock.ForegroundProperty,"Text");Canvas.SetLeft(label,polygon.Points.Average(p=>p.X)-20);Canvas.SetTop(label,polygon.Points.Average(p=>p.Y)-7);canvas.Children.Add(label);}
   var north=new TextBlock{Text="North ↑ · East →"+(panels.Any(p=>p.Sky.Approximate)?" · Approximate":""),FontSize=10};Theme.Bind(north,TextBlock.ForegroundProperty,"Muted");Canvas.SetLeft(north,12);Canvas.SetTop(north,161);canvas.Children.Add(north);
  }
 }
}
