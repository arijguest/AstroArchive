using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
namespace AstroArchive {
 public partial class MainUi {
  bool editedReady,refreshingEdited;List<EditedImage> editedImages=new List<EditedImage>();
  ListBox EditedProjectsList{get{return (ListBox)Window.FindName("EditedProjectsList");}}
  EditedProject ActiveEditedProject{get{return EditedProjectsList.SelectedItem as EditedProject;}}
  EditedImage ActiveEditedImage{get{return G("EditedGrid").SelectedItem as EditedImage;}}
  void InitializeEdited(){
   EditedProjectsList.SelectionChanged+=(s,e)=>{if(!refreshingEdited)RefreshEditedImages();};
   C("EditedClassFilter").ItemsSource=new[]{"All images","Starless","Stars only","Meteor","Edited image","Unknown (conflicting labels)"};C("EditedClassFilter").SelectedIndex=0;C("EditedClassFilter").SelectionChanged+=(s,e)=>FilterEditedImages();
   T("EditedSearchBox").TextChanged+=(s,e)=>FilterEditedImages();G("EditedGrid").SelectionChanged+=(s,e)=>UpdateEditedActions();G("EditedGrid").MouseDoubleClick+=(s,e)=>PreviewEditedImage();
   B("EditedAddButton").Click+=(s,e)=>AddEditedImages();B("EditedImportFolderButton").Click+=(s,e)=>ImportEditedFolder();B("EditedRefreshButton").Click+=(s,e)=>RefreshEdited();
   B("EditedFolderButton").Click+=(s,e)=>OpenEditedFolder();B("EditedPreviewButton").Click+=(s,e)=>PreviewEditedImage();B("EditedEditorButton").Click+=(s,e)=>ShowEditedEditors();B("EditedDetailsButton").Click+=(s,e)=>ShowEditedDetails();
   UiHelp.Tip(B("EditedAddButton"),"Copy finished or in-progress images into an Edited project.");UiHelp.Tip(B("EditedRefreshButton"),"Find images saved by your editor in the selected project.");UiHelp.Tip(B("EditedFolderButton"),"Open this project's working files and editor outputs in File Explorer.");
   editedReady=true;RefreshEdited();Window.Activated+=(s,e)=>{if(editedReady&&cancel==null&&!closing)RefreshEdited();};
  }
  void RefreshEdited(string select=null){
   if(!editedReady)return;string id=select??(ActiveEditedProject==null?null:ActiveEditedProject.Id);var projects=new List<EditedProject>();var errors=new List<string>();
   try{if(repo!=null)projects=repo.EditedProjects(out errors);}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException))throw;errors.Add(e.Message);}
   refreshingEdited=true;try{EditedProjectsList.ItemsSource=projects;EditedProjectsList.SelectedItem=projects.FirstOrDefault(p=>p.Id==id)??projects.FirstOrDefault();}finally{refreshingEdited=false;}
   RefreshEditedImages();if(errors.Count>0){L("EditedSummary").Text=errors.Count+" project(s) could not be loaded";L("EditedSummary").ToolTip=string.Join("\n",errors);}else L("EditedSummary").ToolTip=null;
  }
  void RefreshEditedImages(){
   editedImages=new List<EditedImage>();try{if(repo!=null&&ActiveEditedProject!=null)editedImages=repo.EditedImages(ActiveEditedProject);}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException))throw;L("EditedProjectInfo").ToolTip=e.Message;}
   L("EditedProjectInfo").Text=ActiveEditedProject==null?"Choose or add a project":string.Join(" · ",new[]{ActiveEditedProject.Target,ActiveEditedProject.Processor}.Where(t=>!string.IsNullOrEmpty(t)));
   FilterEditedImages();
  }
  void FilterEditedImages(){
   if(!editedReady)return;string selected=ActiveEditedImage==null?null:ActiveEditedImage.RelativePath;var words=Util.Tokens(T("EditedSearchBox").Text);
   string imageClass=Convert.ToString(C("EditedClassFilter").SelectedItem);var rows=editedImages.Where(i=>(imageClass=="All images"||i.Metadata.ImageClass==imageClass)&&words.All(w=>(i.Filename+" "+i.Kind+" "+i.Source+" "+i.Metadata.ImageClass+" "+i.Metadata.ObjectLabel+" "+i.Metadata.Filters).IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0)).ToList();G("EditedGrid").ItemsSource=rows;G("EditedGrid").SelectedItem=rows.FirstOrDefault(i=>i.RelativePath==selected);
   L("EditedSummary").Text=rows.Count+" images";L("EditedEmptyState").Visibility=rows.Count==0?Visibility.Visible:Visibility.Collapsed;UpdateEditedActions();
  }
  void UpdateEditedActions(){
   if(!editedReady)return;bool ready=repo!=null&&cancel==null;B("EditedAddButton").IsEnabled=ready;B("EditedImportFolderButton").IsEnabled=ready;B("EditedRefreshButton").IsEnabled=ready;B("EditedFolderButton").IsEnabled=ready&&ActiveEditedProject!=null;
   B("EditedPreviewButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedEditorButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedDetailsButton").IsEnabled=ready&&ActiveEditedImage!=null;
  }
  void OpenEditedFolder(){if(repo!=null&&ActiveEditedProject!=null)OpenFolder(repo.EditedProjectFolder(ActiveEditedProject));}
  void ShowEditedDetails(){if(ActiveEditedImage!=null)ShowReport(ActiveEditedImage.Filename,ActiveEditedImage.Metadata.Details+(string.IsNullOrEmpty(ActiveEditedImage.MetadataProblem)?"":"\n\nMetadata could not be read: "+ActiveEditedImage.MetadataProblem));}
  void AddEditedImages(){
   if(repo==null||cancel!=null)return;var picker=new OpenFileDialog{Title="Add edited images",Multiselect=true,Filter="Supported images|*.fit;*.fits;*.fts;*.fit.gz;*.fits.gz;*.fts.gz;*.xisf;*.fz;*.ser;*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.dng;*.cr2;*.cr3;*.nef;*.arw;*.raf;*.orf;*.rw2|All files|*.*"};if(picker.ShowDialog(Window)!=true)return;
   var dialog=new FormWindow(Window,"Add edited images",600,400);dialog.Text(picker.FileNames.Length+" images selected",true);
   var choices=EditedProjectsList.Items.Cast<EditedProject>().ToList();var create=new EditedProject{Name="New project…"};choices.Insert(0,create);var projectBox=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedItem=ActiveEditedProject??create};dialog.Add(projectBox);
   var name=dialog.Input("New project name",Path.GetFileNameWithoutExtension(picker.FileNames[0]));Action update=()=>name.IsEnabled=ReferenceEquals(projectBox.SelectedItem,create);projectBox.SelectionChanged+=(s,e)=>update();update();
   dialog.Text("Images are copied into Edited. Save further edits and outputs in the project folder to keep them together.");dialog.Accept("Add images",()=>{if(name.IsEnabled&&string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show(dialog.Window,"Enter a project name.");return false;}if(!picker.FileNames.All(Util.IsImageAsset)){MessageBox.Show(dialog.Window,"Choose supported image files.");return false;}return true;});if(!dialog.Show())return;
   var selected=ReferenceEquals(projectBox.SelectedItem,create)?null:projectBox.SelectedItem as EditedProject;
   string projectName=name.Text;Run(ct=>repo.AddEditedImages(picker.FileNames,selected,projectName,ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(3);});
  }
  void ImportEditedFolder(){
   if(repo==null||cancel!=null)return;string folder=Folder("Choose a folder of existing edited images","");if(folder==null)return;
   var dialog=new FormWindow(Window,"Review an edited image folder",590,360);dialog.Text(folder,true);var recursive=dialog.Check("Include subfolders",true);dialog.Text("Scan images first, then review files and detected acquisition details before copying.");dialog.Accept("Scan folder",()=>true);if(!dialog.Show())return;
   bool includeSubfolders=recursive.IsChecked==true;EditedImportPlan import=null;Run(ct=>{import=repo.ScanEditedFolder(folder,includeSubfolders,ct,Progress);return "";},done=>ReviewEditedFolder(import));
  }
  void ReviewEditedFolder(EditedImportPlan import){
   var dialog=new FormWindow(Window,"Import existing edited images",1000,750);dialog.Text(import.Images.Count+" images found",true);var name=dialog.Input("Edited project name",new DirectoryInfo(import.Folder).Name);
   if(import.Errors.Count>0)dialog.Text(import.Errors.Count+" folders could not be scanned. Their images will not be imported.");
   dialog.Text("Select the images to copy. Unreadable files stay excluded; source folders are retained. Detected details can be inspected before import.");
   var table=new DataGrid{ItemsSource=import.Images,IsReadOnly=false,AutoGenerateColumns=false,Height=350};table.Columns.Add(new DataGridCheckBoxColumn{Header="IMPORT",Binding=new System.Windows.Data.Binding("Include"){Mode=System.Windows.Data.BindingMode.TwoWay}});
   foreach(var column in new[]{new[]{"FILE","Filename"},new[]{"CLASS","ImageClass"},new[]{"OBJECT","Object"},new[]{"TOTAL EXPOSURE","TotalExposure"},new[]{"PROBLEM","Problem"}})table.Columns.Add(new DataGridTextColumn{Header=column[0],Binding=new System.Windows.Data.Binding(column[1]),IsReadOnly=true,Width=column[0]=="FILE"?new DataGridLength(1,DataGridLengthUnitType.Star):DataGridLength.Auto});
   var rowStyle=new Style(typeof(DataGridRow),Window.TryFindResource(typeof(DataGridRow)) as Style);rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("Metadata.Evidence")));table.RowStyle=rowStyle;dialog.Add(table);
   if(import.Errors.Count>0){var problems=new TextBox{Text=string.Join("\n",import.Errors),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=90,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};dialog.Add(problems);}
   dialog.Accept("Import selected images",()=>{table.CommitEdit(DataGridEditingUnit.Cell,true);table.CommitEdit(DataGridEditingUnit.Row,true);if(string.IsNullOrWhiteSpace(name.Text)||!import.Images.Any(i=>i.Include)){MessageBox.Show(dialog.Window,"Enter a project name and select readable images.");return false;}if(import.Images.Any(i=>i.Include&&!string.IsNullOrEmpty(i.Problem))){MessageBox.Show(dialog.Window,"Exclude images with a reported problem before importing.");return false;}return true;});if(!dialog.Show())return;
   string projectName=name.Text;Run(ct=>repo.ImportEditedFolder(import,projectName,ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(3);});
  }
  void PreviewEditedImage(){
   if(repo==null||cancel!=null||ActiveEditedProject==null||ActiveEditedImage==null)return;string path=repo.EditedPath(ActiveEditedProject,ActiveEditedImage.RelativePath);PreviewData data=null;byte[] pixels=null;
   Run(ct=>{data=DecodePreview(path,ct);data.ApplyContext(null,path);pixels=data.Render(ScientificPreview(path)?settings.PreviewStretch??"Auto":"Linear",ct);return path;},image=>new ImagePreviewWindow(Window,Path.GetFileName(image),data.Width,data.Height,pixels).ShowDialog());
  }
  void ShowEditedEditors(){
   if(ActiveEditedImage==null)return;var menu=ThemedMenu();string name=ActiveEditedImage.Filename;
   menu.Items.Add(FileAction("Siril…",OpenEditedEditor,Util.IsFits(name)&&!name.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)));
   menu.Items.Add(FileAction("Default application",()=>Process.Start(new ProcessStartInfo(repo.EditedPath(ActiveEditedProject,ActiveEditedImage.RelativePath)){UseShellExecute=true})));
   menu.Items.Add(FileAction("Open folder for another editor",OpenEditedFolder));menu.PlacementTarget=B("EditedEditorButton");menu.IsOpen=true;
  }
  void OpenEditedEditor(){
   if(repo==null||cancel!=null||ActiveEditedProject==null||ActiveEditedImage==null)return;string executable=settings.SirilExecutable;
   if(string.IsNullOrEmpty(executable)||!File.Exists(executable)){var picker=new OpenFileDialog{Title="Locate Siril",Filter="Siril GUI|siril.exe"};if(picker.ShowDialog(Window)!=true)return;executable=picker.FileName;settings.SirilExecutable=executable;SaveSettings();}
   string path=repo.EditedPath(ActiveEditedProject,ActiveEditedImage.RelativePath);Run(ct=>{ct.ThrowIfCancellationRequested();using(var process=Process.Start(SirilHandoff.LaunchInfo(executable,path))){if(process==null)throw new IOException("The editor did not start.");}return path;},done=>{});
  }
  void CreateEditedCopies(List<Frame> selected){
   if(repo==null||cancel!=null||selected.Count==0)return;var dialog=new FormWindow(Window,"Create Edited working copies",610,390);dialog.Text("Create a project for your editor",true);var name=dialog.Input("Edited project name",selected[0].TargetLabel+" · "+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   dialog.Text("Copy and verify the selected archived images into Edited, then open the project folder. Load these copies in AstroWizard or your preferred editor, and save outputs alongside them.");dialog.Accept("Create working copies",()=>!string.IsNullOrWhiteSpace(name.Text));if(!dialog.Show())return;
   string projectName=name.Text;Run(ct=>repo.CreateEditedWorkingCopies(selected,projectName,"Other editor",ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(3);OpenEditedFolder();});
  }
  void BuildEditedNavigation(MenuItem menu){
   menu.Items.Add(MenuAction("Browse edited images",()=>{RefreshEdited();GoToPage(3);},true,false));menu.Items.Add(MenuAction("Add images…",AddEditedImages,repo!=null));menu.Items.Add(MenuAction("Import folder…",ImportEditedFolder,repo!=null));
   menu.Items.Add(MenuAction("Open project folder",OpenEditedFolder,repo!=null&&ActiveEditedProject!=null));menu.Items.Add(MenuAction("Preview selected image…",PreviewEditedImage,ActiveEditedImage!=null));menu.Items.Add(MenuAction("Refresh projects",()=>RefreshEdited(),repo!=null));
  }
  void ShowPerformanceTable(){
   var dialog=new FormWindow(Window,"Operation diagnostics",720,520);dialog.Text("Last operation",true);dialog.Text(L("StatusLabel").Text+"\n"+L("RateLabel").Text);
   var grid=new DataGrid{ItemsSource=G("MetricsGrid").ItemsSource,IsReadOnly=true,AutoGenerateColumns=false,MinHeight=160,MaxHeight=320};foreach(var column in G("MetricsGrid").Columns.OfType<DataGridTextColumn>())grid.Columns.Add(new DataGridTextColumn{Header=column.Header,Binding=column.Binding,Width=column.Width});dialog.Add(grid);dialog.CloseOnly();dialog.Show();
  }
 }
}
