using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using Microsoft.Win32;
namespace AstroArchive {
 public partial class MainUi {
  bool editedReady,refreshingEdited;List<EditedImage> editedImages=new List<EditedImage>();
  ComboBox EditedProjectsList{get{return (ComboBox)Window.FindName("EditedProjectsList");}}
  ListBox EditedTargets{get{return (ListBox)Window.FindName("EditedTargetList");}}
  bool refreshingEditedTargets;
  EditedProject ActiveEditedProject{get{var project=EditedProjectsList.SelectedItem as EditedProject;return project==null||string.IsNullOrEmpty(project.Id)?null:project;}}
  EditedImage ActiveEditedImage{get{return G("EditedGrid").SelectedItem as EditedImage;}}
  EditedProject EditedImageProject{get{return ActiveEditedImage==null?ActiveEditedProject:ActiveEditedImage.Project;}}
  MotionPreview editedMotion;PreviewViewport editedPreviewViewport;CancellationTokenSource editedPreviewCancel;int editedPreviewGeneration;PreviewData editedPreviewData;bool editedChoosingStretch;
  void InitializeEditedPreview(){
   editedPreviewViewport=new PreviewViewport((Grid)Window.FindName("EditedPreviewHost"),(Grid)Window.FindName("EditedPreviewStage"),(Image)Window.FindName("EditedPreviewImage"),(FrameworkElement)Window.FindName("EditedPreviewSky"));InitializeSkyPreview("Edited");C("EditedStretchMode").ItemsSource=PreviewData.StretchModes;C("EditedStretchMode").SelectedItem=settings.PreviewStretch??"Auto per channel";
   C("EditedStretchMode").SelectionChanged+=(s,e)=>{if(!editedChoosingStretch)LoadEditedPreview(false);};B("EditedOpenPreviewButton").Click+=(s,e)=>PreviewEditedImage();
  }
  void CancelEditedPreview(){ClosePreviewDetails("Edited");if(editedMotion!=null){editedMotion.Dispose();editedMotion=null;}editedPreviewGeneration++;if(editedPreviewCancel!=null){editedPreviewCancel.Cancel();editedPreviewCancel.Dispose();editedPreviewCancel=null;}editedPreviewData=null;UpdateCaptureSky("Edited",null);if(editedPreviewViewport!=null)editedPreviewViewport.SetImage(null,true);}
  async void LoadEditedPreview(bool reload=true,Func<string,CancellationToken,Frame,PreviewData> decode=null){
   if(!editedReady)return;var image=ActiveEditedImage;if(image==null||repo==null){CancelEditedPreview();L("EditedPreviewName").Text="Select an edited image";L("EditedPreviewInfo").Text="";L("EditedDetailsLabel").Text="Select an edited image to view metadata.";L("EditedLibrarySummaryLabel").Text="";L("EditedPreviewMessage").Text="Select an edited image.";L("EditedPreviewMessage").Visibility=Visibility.Visible;return;}
   if(editedMotion!=null){editedMotion.Dispose();editedMotion=null;}if(editedPreviewCancel!=null){editedPreviewCancel.Cancel();editedPreviewCancel.Dispose();}editedPreviewCancel=new CancellationTokenSource();var token=editedPreviewCancel.Token;int generation=++editedPreviewGeneration;var previous=reload||editedPreviewData==null?null:editedPreviewData.Copy();var skyContext=reload?null:editedSkyFrame;
   string path;try{path=repo.EditedPath(image.Project,image.RelativePath);}catch(Exception error){L("EditedPreviewMessage").Text=error.Message;L("EditedPreviewMessage").Visibility=Visibility.Visible;return;}
   if(reload){editedPreviewData=null;editedPreviewViewport.BeginLoading();editedChoosingStretch=true;C("EditedStretchMode").SelectedItem=ScientificPreview(path)&&!ObservationTargets.Unstretched(image.Metadata.Object,null)?settings.PreviewStretch??"Auto per channel":"Linear";editedChoosingStretch=false;}
   UpdateCaptureSky("Edited",skyContext??new Frame{Target=image.Metadata.Object,OriginalName=image.Filename});L("EditedPreviewName").Text=image.Filename;L("EditedPreviewName").ToolTip=path;L("EditedDetailsLabel").Text=image.Metadata.Details;L("EditedLibrarySummaryLabel").Text=image.Project.Name;L("EditedPreviewMessage").Text=previous==null?"Loading image…":"Stretching…";L("EditedPreviewMessage").Visibility=Visibility.Visible;string mode=Convert.ToString(C("EditedStretchMode").SelectedItem);
   if(MediaFiles.Motion(path)){UpdateCaptureSky("Edited",skyContext??ReadSkyFrame(path,image.Metadata.Object));C("EditedStretchMode").IsEnabled=false;editedMotion=new MotionPreview(editedPreviewViewport,path,info=>{if(generation!=editedPreviewGeneration)return;L("EditedPreviewMessage").Visibility=Visibility.Collapsed;L("EditedPreviewInfo").Text=info;},message=>{if(generation!=editedPreviewGeneration)return;L("EditedPreviewMessage").Text=message;L("EditedPreviewMessage").Visibility=Visibility.Visible;});editedMotion.Start();return;}
   try{Func<bool> current=()=>generation==editedPreviewGeneration&&!Window.Dispatcher.HasShutdownStarted;
    var data=previous??await LoadPreviewSamples(path,null,skyContext,image.Metadata.Object,token,current,sky=>UpdateCaptureSky("Edited",sky),decode);data.ApplyContext(new Frame{Target=image.Metadata.Object},path);
    if(!current()||token.IsCancellationRequested)return;editedPreviewData=data;bool firstPaint=true;
    await RenderProgressivePreview(data,mode,previous==null,token,current,(bitmap,provisional)=>{
     editedPreviewViewport.SetImage(bitmap,reload&&firstPaint);firstPaint=false;L("EditedPreviewMessage").Visibility=Visibility.Collapsed;L("EditedPreviewInfo").Text=data.SourceWidth+" × "+data.SourceHeight+" pixels";L("EditedPreviewInfo").ToolTip=data.Description+" · "+data.DisplayMode+(provisional?" · applying selected stretch":"");C("EditedStretchMode").IsEnabled=!data.SkipStretch;
     if(data.SkipStretch){editedChoosingStretch=true;C("EditedStretchMode").SelectedItem="Linear";editedChoosingStretch=false;}
    });
   }catch(OperationCanceledException){}catch(Exception error){if(generation!=editedPreviewGeneration||Window.Dispatcher.HasShutdownStarted)return;if(((Image)Window.FindName("EditedPreviewImage")).Source==null)editedPreviewViewport.BeginLoading();L("EditedPreviewMessage").Text="Preview unavailable\n\n"+error.Message;L("EditedPreviewMessage").Visibility=Visibility.Visible;}
  }
  void InitializeEdited(){
   EditedProjectsList.SelectionChanged+=(s,e)=>{if(!refreshingEdited)RefreshEditedImages();};
   C("EditedClassFilter").ItemsSource=new[]{"All images","Starless","Stars only","GIF","Meteor","Edited image","Unknown (conflicting labels)"};C("EditedClassFilter").SelectedIndex=0;C("EditedClassFilter").SelectionChanged+=(s,e)=>FilterEditedImages();
   T("EditedSearchBox").TextChanged+=(s,e)=>FilterEditedImages();G("EditedGrid").SelectionChanged+=(s,e)=>{UpdateEditedActions();LoadEditedPreview();};G("EditedGrid").MouseDoubleClick+=(s,e)=>PreviewEditedImage();
   EditedTargets.SelectionChanged+=(s,e)=>{if(!refreshingEditedTargets)FilterEditedImages(false);};B("EditedClearButton").Click+=(s,e)=>{T("EditedSearchBox").Clear();C("EditedClassFilter").SelectedIndex=0;EditedTargets.SelectedIndex=0;};B("EditedFiltersButton").Click+=(s,e)=>OpenEditedFilters();InitializeEditedPreview();
   B("EditedAddButton").Click+=(s,e)=>AddEditedImages();B("EditedImportFolderButton").Click+=(s,e)=>ImportEditedFolder();B("EditedRefreshButton").Click+=(s,e)=>RefreshEdited();
   B("EditedFolderButton").Click+=(s,e)=>OpenEditedFolder();B("EditedPreviewButton").Click+=(s,e)=>PreviewEditedImage();B("EditedEditorButton").Click+=(s,e)=>ShowEditedEditors();B("EditedDetailsButton").Click+=(s,e)=>ShowEditedDetails();
   UiHelp.Tip(B("EditedAddButton"),"Copy finished or in-progress images into an Edited project.");UiHelp.Tip(B("EditedRefreshButton"),"Find images saved by your editor in the selected project.");UiHelp.Tip(B("EditedFolderButton"),"Open this project's working files and editor outputs in File Explorer.");
   editedReady=true;RefreshEdited();Window.Activated+=(s,e)=>{if(editedReady&&cancel==null&&!closing)RefreshEdited();};
  }
  void RefreshEdited(string select=null){
   if(!editedReady)return;string id=select??(ActiveEditedProject==null?"":ActiveEditedProject.Id);var projects=new List<EditedProject>();var errors=new List<string>();
   try{if(repo!=null)projects=repo.EditedProjects(out errors);}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException))throw;errors.Add(e.Message);}
   projects.Insert(0,new EditedProject{Id="",Name="All projects"});refreshingEdited=true;try{EditedProjectsList.ItemsSource=projects;EditedProjectsList.SelectedItem=projects.FirstOrDefault(p=>p.Id==id)??projects.First();}finally{refreshingEdited=false;}
   RefreshEditedImages();if(errors.Count>0){L("EditedSummary").Text=errors.Count+" project(s) could not be loaded";L("EditedSummary").ToolTip=string.Join("\n",errors);}else L("EditedSummary").ToolTip=null;
  }
  void RefreshEditedImages(){
   editedImages=new List<EditedImage>();if(repo!=null)foreach(var project in EditedProjectsList.Items.Cast<EditedProject>().Where(p=>!string.IsNullOrEmpty(p.Id)&&(ActiveEditedProject==null||p.Id==ActiveEditedProject.Id)))try{var images=repo.EditedImages(project);foreach(var image in images)image.Project=project;editedImages.AddRange(images);}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException))throw;L("EditedProjectInfo").ToolTip=e.Message;}
   L("EditedProjectInfo").Text=ActiveEditedProject==null?"All projects":string.Join(" · ",new[]{ActiveEditedProject.Target,ActiveEditedProject.Processor}.Where(t=>!string.IsNullOrEmpty(t)));
   FilterEditedImages();
  }
  void FilterEditedImages(bool rebuildTargets=true){
   if(!editedReady)return;var selected=ActiveEditedImage;var query=FileSearch.Parse(T("EditedSearchBox").Text);ShowSearchError("EditedSearchBox",query);L("EditedSearchHint").Visibility=query.IsEmpty?Visibility.Visible:Visibility.Collapsed;
   string imageClass=Convert.ToString(C("EditedClassFilter").SelectedItem);var rows=editedImages.Where(i=>(imageClass=="All images"||i.Metadata.ImageClass==imageClass)&&query.Matches(EditedSearchDocument(i))).ToList();
   if(rebuildTargets){string target=EditedTargets.SelectedItem is TargetSummary?((TargetSummary)EditedTargets.SelectedItem).Name:"All targets";var summaries=TargetNavigation.Build(rows.Select(i=>new Frame{Target=i.Metadata.ImageClass=="Meteor"?"Meteor":i.Metadata.Object,Kind="Edited image"})).Select(t=>new EditedTargetSummary{Name=t.Name,Files=t.Files}).ToList();var view=new ListCollectionView(summaries);view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));refreshingEditedTargets=true;try{EditedTargets.ItemsSource=view;EditedTargets.SelectedItem=summaries.FirstOrDefault(t=>t.Name==target)??summaries.First();}finally{refreshingEditedTargets=false;}}
   var active=EditedTargets.SelectedItem as TargetSummary;if(active!=null&&active.Name!="All targets")rows=rows.Where(i=>Catalog.CanonicalTarget(i.Metadata.ImageClass=="Meteor"?"Meteor":i.Metadata.Object)==active.Name).ToList();SetRows("EditedGrid",rows);G("EditedGrid").SelectedItem=rows.FirstOrDefault(i=>selected!=null&&i.Project.Id==selected.Project.Id&&i.RelativePath==selected.RelativePath);
   L("EditedSummary").Text=rows.Count+" images";L("EditedEmptyState").Visibility=rows.Count==0?Visibility.Visible:Visibility.Collapsed;UpdateEditedActions();
  }
  void UpdateEditedActions(){
   if(!editedReady)return;bool ready=repo!=null&&cancel==null;B("EditedAddButton").IsEnabled=ready;B("EditedImportFolderButton").IsEnabled=ready;B("EditedRefreshButton").IsEnabled=ready;B("EditedFolderButton").IsEnabled=ready&&EditedImageProject!=null;EditedProjectsList.IsEnabled=ready;B("EditedFiltersButton").IsEnabled=ready;B("EditedOpenPreviewButton").IsEnabled=ready&&ActiveEditedImage!=null;
   B("EditedPreviewButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedEditorButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedDetailsButton").IsEnabled=ready&&ActiveEditedImage!=null;
  }
  void OpenEditedFolder(){if(repo!=null&&EditedImageProject!=null)OpenFolder(repo.EditedProjectFolder(EditedImageProject));}
  void OpenEditedFilters(){var menu=ThemedMenu();foreach(string imageClass in C("EditedClassFilter").Items){string choice=imageClass;var item=new MenuItem{Header=choice,IsCheckable=true,IsChecked=choice==Convert.ToString(C("EditedClassFilter").SelectedItem)};item.Click+=(s,e)=>C("EditedClassFilter").SelectedItem=choice;menu.Items.Add(item);}menu.PlacementTarget=B("EditedFiltersButton");menu.IsOpen=true;}
  void ShowEditedDetails(){if(ActiveEditedImage!=null)ShowReport(ActiveEditedImage.Filename,ActiveEditedImage.Metadata.Details+(string.IsNullOrEmpty(ActiveEditedImage.MetadataProblem)?"":"\n\nMetadata could not be read: "+ActiveEditedImage.MetadataProblem));}
  void AddEditedImages(){
   if(repo==null||cancel!=null)return;var picker=new OpenFileDialog{Title="Add edited images",Multiselect=true,Filter="Supported images|*.fit;*.fits;*.fts;*.fit.gz;*.fits.gz;*.fts.gz;*.xisf;*.fz;*.ser;*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.avi;*.mp4;*.mov;*.m4v;*.wmv;*.mkv;*.dng;*.cr2;*.cr3;*.nef;*.arw;*.raf;*.orf;*.rw2|All files|*.*"};if(picker.ShowDialog(Window)!=true)return;
   var dialog=new FormWindow(Window,"Add edited images",600,400);dialog.Text(picker.FileNames.Length+" images selected",true);
   var choices=EditedProjectsList.Items.Cast<EditedProject>().Where(p=>!string.IsNullOrEmpty(p.Id)).ToList();var create=new EditedProject{Name="New project…"};choices.Insert(0,create);var projectBox=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedItem=ActiveEditedProject??create};dialog.Add(projectBox);
   var name=dialog.Input("New project name",Path.GetFileNameWithoutExtension(picker.FileNames[0]));Action update=()=>name.IsEnabled=ReferenceEquals(projectBox.SelectedItem,create);projectBox.SelectionChanged+=(s,e)=>update();update();
   dialog.Text("Images are copied into Edited. Save further edits and outputs in the project folder to keep them together.");dialog.Accept("Add images",()=>{if(name.IsEnabled&&string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show(dialog.Window,"Enter a project name.");return false;}if(!picker.FileNames.All(Util.IsImageAsset)){MessageBox.Show(dialog.Window,"Choose supported image files.");return false;}return true;});if(!dialog.Show())return;
   var selected=ReferenceEquals(projectBox.SelectedItem,create)?null:projectBox.SelectedItem as EditedProject;
   string projectName=name.Text;Run(ct=>repo.AddEditedImages(picker.FileNames,selected,projectName,ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(2);});
  }
  void ImportEditedFolder(){
   if(repo==null||cancel!=null)return;
   var dialog=new FormWindow(Window,"Import edited images",650,390);dialog.Text("Import files from a folder",true);dialog.Text("SOURCE FOLDER");var source=new TextBox{MinWidth=200};var browse=new Button{Content="Browse",Margin=new Thickness(8,0,0,0),Padding=new Thickness(12,8,12,8)};var controls=new DockPanel();DockPanel.SetDock(browse,Dock.Right);controls.Children.Add(browse);controls.Children.Add(source);dialog.Add(controls);
   browse.Click+=(s,e)=>{string selected=Folder("Choose a folder of existing edited images",source.Text);if(selected!=null)source.Text=selected;};var recursive=dialog.Check("Include subfolders",true);dialog.Text("Scan first, then review images before copying. Repository/database folders and files already archived are skipped. Source files are retained.");dialog.Accept("Scan folder",()=>{if(!Directory.Exists(source.Text.Trim())){MessageBox.Show(dialog.Window,"Choose an existing source folder.");return false;}return true;});if(!dialog.Show())return;
   string folder=source.Text.Trim();bool includeSubfolders=recursive.IsChecked==true;EditedImportPlan import=null;Run(ct=>{import=repo.ScanEditedFolder(folder,includeSubfolders,ct,Progress);return "";},done=>ReviewEditedFolder(import));
  }
  void ReviewEditedFolder(EditedImportPlan import){
   var dialog=new FormWindow(Window,"Import existing edited images",1000,750);dialog.Text(import.Images.Count+" images found",true);var name=dialog.Input("Edited project name",new DirectoryInfo(import.Folder).Name);
   if(import.SkippedArchived>0||import.SkippedFolders.Count>0)dialog.Text(import.SkippedArchived+" already archived images skipped · "+import.SkippedFolders.Count+" repository/database folders skipped");
   if(import.Errors.Count>0)dialog.Text(import.Errors.Count+" folders could not be scanned. Their images will not be imported.");
   dialog.Text("Select the images to copy. Unreadable files stay excluded; source folders are retained. Detected details can be inspected before import.");
   var table=new DataGrid{ItemsSource=import.Images,IsReadOnly=false,AutoGenerateColumns=false,Height=350};table.Columns.Add(new DataGridCheckBoxColumn{Header="IMPORT",Binding=new System.Windows.Data.Binding("Include"){Mode=System.Windows.Data.BindingMode.TwoWay}});
   foreach(var column in new[]{new[]{"FILE","Filename"},new[]{"FILE TYPE","FileType"},new[]{"CLASS","ImageClass"},new[]{"OBJECT","Object"},new[]{"TOTAL EXPOSURE","TotalExposure"},new[]{"PROBLEM","Problem"}})table.Columns.Add(new DataGridTextColumn{Header=column[0],Binding=new System.Windows.Data.Binding(column[1]),IsReadOnly=true,Width=column[0]=="FILE"?new DataGridLength(1,DataGridLengthUnitType.Star):DataGridLength.Auto});
   var rowStyle=new Style(typeof(DataGridRow),Window.TryFindResource(typeof(DataGridRow)) as Style);rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("Metadata.Evidence")));table.RowStyle=rowStyle;dialog.Add(table);
   if(import.Errors.Count>0){var problems=new TextBox{Text=string.Join("\n",import.Errors),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=90,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};dialog.Add(problems);}
   dialog.Accept("Import selected images",()=>{table.CommitEdit(DataGridEditingUnit.Cell,true);table.CommitEdit(DataGridEditingUnit.Row,true);if(string.IsNullOrWhiteSpace(name.Text)||!import.Images.Any(i=>i.Include)){MessageBox.Show(dialog.Window,"Enter a project name and select readable images.");return false;}if(import.Images.Any(i=>i.Include&&!string.IsNullOrEmpty(i.Problem))){MessageBox.Show(dialog.Window,"Exclude images with a reported problem before importing.");return false;}return true;});if(!dialog.Show())return;
   string projectName=name.Text;Run(ct=>repo.ImportEditedFolder(import,projectName,ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(2);});
  }
  void PreviewEditedImage(){
   if(repo==null||cancel!=null||EditedImageProject==null||ActiveEditedImage==null)return;string path=repo.EditedPath(EditedImageProject,ActiveEditedImage.RelativePath);if(MediaFiles.Motion(path)){new ImagePreviewWindow(Window,ActiveEditedImage.Filename,path).ShowDialog();return;}PreviewData data=null;byte[] pixels=null;
   Run(ct=>{data=DecodePreview(path,ct);data.ApplyContext(null,path);pixels=data.Render(ScientificPreview(path)?settings.PreviewStretch??"Auto per channel":"Linear",ct);return path;},image=>new ImagePreviewWindow(Window,Path.GetFileName(image),data.Width,data.Height,pixels).ShowDialog());
  }
  void ShowEditedEditors(){
   if(ActiveEditedImage==null)return;var menu=ThemedMenu();string name=ActiveEditedImage.Filename;
   menu.Items.Add(FileAction("Siril…",OpenEditedEditor,Util.IsFits(name)&&!name.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)));
   menu.Items.Add(FileAction("Default application",()=>Process.Start(new ProcessStartInfo(repo.EditedPath(EditedImageProject,ActiveEditedImage.RelativePath)){UseShellExecute=true})));
   menu.Items.Add(FileAction("Open folder for another editor",OpenEditedFolder));menu.PlacementTarget=B("EditedEditorButton");menu.IsOpen=true;
  }
  void OpenEditedEditor(){
   if(repo==null||cancel!=null||EditedImageProject==null||ActiveEditedImage==null)return;string executable=settings.SirilExecutable;
   if(string.IsNullOrEmpty(executable)||!File.Exists(executable)){var picker=new OpenFileDialog{Title="Locate Siril",Filter="Siril GUI|siril.exe"};if(picker.ShowDialog(Window)!=true)return;executable=picker.FileName;settings.SirilExecutable=executable;SaveSettings();}
   string path=repo.EditedPath(EditedImageProject,ActiveEditedImage.RelativePath);Run(ct=>{ct.ThrowIfCancellationRequested();using(var process=Process.Start(SirilHandoff.LaunchInfo(executable,path))){if(process==null)throw new IOException("The editor did not start.");}return path;},done=>{});
  }
  void CreateEditedCopies(List<Frame> selected){
   if(repo==null||cancel!=null||selected.Count==0)return;var dialog=new FormWindow(Window,"Create Edited working copies",610,390);dialog.Text("Create a project for your editor",true);var name=dialog.Input("Edited project name",selected[0].TargetLabel+" · "+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   dialog.Text("Copy and verify the selected archived images into Edited, then open the project folder. Load these copies in AstroWizard or your preferred editor, and save outputs alongside them.");dialog.Accept("Create working copies",()=>!string.IsNullOrWhiteSpace(name.Text));if(!dialog.Show())return;
   string projectName=name.Text;Run(ct=>repo.CreateEditedWorkingCopies(selected,projectName,"Other editor",ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(2);OpenEditedFolder();});
  }
  void BuildEditedNavigation(MenuItem menu){
   menu.Items.Add(ColumnsNavigation("EditedGrid"));menu.Items.Add(MenuAction("Browse edited images",()=>{RefreshEdited();GoToPage(2);},true,false));menu.Items.Add(MenuAction("Add images…",AddEditedImages,repo!=null));menu.Items.Add(MenuAction("Import folder…",ImportEditedFolder,repo!=null));
   menu.Items.Add(MenuAction("Open project folder",OpenEditedFolder,repo!=null&&EditedImageProject!=null));menu.Items.Add(MenuAction("Preview selected image…",PreviewEditedImage,ActiveEditedImage!=null));menu.Items.Add(MenuAction("Refresh projects",()=>RefreshEdited(),repo!=null));
  }
  void ShowPerformanceTable(){
   var dialog=new FormWindow(Window,"Operation diagnostics",720,520);dialog.Text("Last operation",true);dialog.Text(L("StatusLabel").Text+"\n"+L("RateLabel").Text);
   var grid=new DataGrid{ItemsSource=G("MetricsGrid").ItemsSource,IsReadOnly=true,AutoGenerateColumns=false,MinHeight=160,MaxHeight=320};foreach(var column in G("MetricsGrid").Columns.OfType<DataGridTextColumn>())grid.Columns.Add(new DataGridTextColumn{Header=column.Header,Binding=column.Binding,Width=column.Width});dialog.Add(grid);dialog.CloseOnly();dialog.Show();
  }
 }
 public sealed class EditedTargetSummary:TargetSummary {public new string Tooltip{get{return Label+"\n"+Files+" edited image"+(Files==1?"":"s");}}}
}
