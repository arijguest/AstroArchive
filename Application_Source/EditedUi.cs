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
  bool editedReady;List<EditedImage> editedImages=new List<EditedImage>();
  ListBox EditedTargets{get{return (ListBox)Window.FindName("EditedTargetList");}}
  bool refreshingEditedTargets;
  EditedImage ActiveEditedImage{get{return G("EditedGrid").SelectedItem as EditedImage;}}
  EditedProject EditedImageProject{get{return ActiveEditedImage==null?null:ActiveEditedImage.Project;}}
  MotionPreview editedMotion;PreviewViewport editedPreviewViewport;CancellationTokenSource editedPreviewCancel;int editedPreviewGeneration;PreviewData editedPreviewData;bool editedChoosingStretch;
  void InitializeEditedPreview(){
   editedPreviewViewport=new PreviewViewport((Grid)Window.FindName("EditedPreviewHost"),(Grid)Window.FindName("EditedPreviewStage"),(Image)Window.FindName("EditedPreviewImage"));C("EditedStretchMode").ItemsSource=PreviewData.StretchModes;C("EditedStretchMode").SelectedItem=settings.PreviewStretch??"Auto per channel";
   C("EditedStretchMode").SelectionChanged+=(s,e)=>{if(!editedChoosingStretch)LoadEditedPreview(false);};B("EditedOpenPreviewButton").Click+=(s,e)=>PreviewEditedImage();
  }
  void CancelEditedPreview(){if(editedMotion!=null){editedMotion.Dispose();editedMotion=null;}editedPreviewGeneration++;if(editedPreviewCancel!=null){editedPreviewCancel.Cancel();editedPreviewCancel.Dispose();editedPreviewCancel=null;}editedPreviewData=null;if(editedPreviewViewport!=null)editedPreviewViewport.SetImage(null,true);}
  async void LoadEditedPreview(bool reload=true){
   if(!editedReady)return;var image=ActiveEditedImage;if(image==null||repo==null){CancelEditedPreview();L("EditedPreviewName").Text="Select an edited image";L("EditedPreviewInfo").Text="";L("EditedDetailsLabel").Text="Select an edited image to view metadata.";L("EditedLibrarySummaryLabel").Text="";L("EditedPreviewMessage").Text="Select an edited image.";L("EditedPreviewMessage").Visibility=Visibility.Visible;return;}
   if(editedMotion!=null){editedMotion.Dispose();editedMotion=null;}if(editedPreviewCancel!=null){editedPreviewCancel.Cancel();editedPreviewCancel.Dispose();}editedPreviewCancel=new CancellationTokenSource();var token=editedPreviewCancel.Token;int generation=++editedPreviewGeneration;var previous=reload?null:editedPreviewData;
   string path;try{path=repo.EditedPath(image.Project,image.RelativePath);}catch(Exception error){L("EditedPreviewMessage").Text=error.Message;L("EditedPreviewMessage").Visibility=Visibility.Visible;return;}
   if(reload){editedPreviewData=null;editedPreviewViewport.SetImage(null,true);editedChoosingStretch=true;C("EditedStretchMode").SelectedItem=ScientificPreview(path)&&!ObservationTargets.Unstretched(image.Metadata.Object,null)?settings.PreviewStretch??"Auto per channel":"Linear";editedChoosingStretch=false;}
   L("EditedPreviewName").Text=image.Filename;L("EditedPreviewName").ToolTip=path;L("EditedDetailsLabel").Text=image.Metadata.Details;L("EditedLibrarySummaryLabel").Text="Edited image";L("EditedPreviewMessage").Text=previous==null?"Loading image…":"Stretching…";L("EditedPreviewMessage").Visibility=Visibility.Visible;string mode=Convert.ToString(C("EditedStretchMode").SelectedItem);
   if(MediaFiles.Motion(path)){C("EditedStretchMode").IsEnabled=false;editedMotion=new MotionPreview(editedPreviewViewport,path,info=>{if(generation!=editedPreviewGeneration)return;L("EditedPreviewMessage").Visibility=Visibility.Collapsed;L("EditedPreviewInfo").Text=info;},message=>{if(generation!=editedPreviewGeneration)return;L("EditedPreviewMessage").Text=message;L("EditedPreviewMessage").Visibility=Visibility.Visible;});editedMotion.Start();return;}
   try{var result=await Task.Run(()=>{var data=previous??DecodePreview(path,token);data.ApplyContext(new Frame{Target=image.Metadata.Object},path);var pixels=data.Render(mode,token);var bitmap=BitmapSource.Create(data.Width,data.Height,96,96,PixelFormats.Rgb24,null,pixels,data.Width*3);bitmap.Freeze();return Tuple.Create(data,bitmap);},token);
    if(generation!=editedPreviewGeneration||token.IsCancellationRequested)return;editedPreviewData=result.Item1;editedPreviewViewport.SetImage(result.Item2,reload);L("EditedPreviewMessage").Visibility=Visibility.Collapsed;L("EditedPreviewInfo").Text=result.Item1.SourceWidth+" × "+result.Item1.SourceHeight+" pixels";C("EditedStretchMode").IsEnabled=!result.Item1.SkipStretch;
    if(result.Item1.SkipStretch){editedChoosingStretch=true;C("EditedStretchMode").SelectedItem="Linear";editedChoosingStretch=false;}
   }catch(OperationCanceledException){}catch(Exception error){if(generation!=editedPreviewGeneration||Window.Dispatcher.HasShutdownStarted)return;editedPreviewViewport.SetImage(null,true);L("EditedPreviewMessage").Text="Preview unavailable\n\n"+error.Message;L("EditedPreviewMessage").Visibility=Visibility.Visible;}
  }
  void InitializeEdited(){
   C("EditedClassFilter").ItemsSource=new[]{"All images","Starless","Stars only","Meteor","Edited image","Unknown (conflicting labels)"};C("EditedClassFilter").SelectedIndex=0;C("EditedClassFilter").SelectionChanged+=(s,e)=>FilterEditedImages();
   T("EditedSearchBox").TextChanged+=(s,e)=>FilterEditedImages();G("EditedGrid").SelectionChanged+=(s,e)=>{UpdateEditedActions();LoadEditedPreview();};G("EditedGrid").MouseDoubleClick+=(s,e)=>PreviewEditedImage();
   EditedTargets.SelectionChanged+=(s,e)=>{if(!refreshingEditedTargets)FilterEditedImages(false);};B("EditedClearButton").Click+=(s,e)=>{T("EditedSearchBox").Clear();C("EditedClassFilter").SelectedIndex=0;EditedTargets.SelectedIndex=0;};B("EditedFiltersButton").Click+=(s,e)=>OpenEditedFilters();InitializeEditedPreview();
   InitializeEditedFileMenu();B("EditedEditButton").Click+=(s,e)=>EditEditedMetadata();
   B("EditedAddButton").Click+=(s,e)=>AddEditedImages();B("EditedImportFolderButton").Click+=(s,e)=>ImportEditedFolder();B("EditedRefreshButton").Click+=(s,e)=>RefreshEdited();
   B("EditedFolderButton").Click+=(s,e)=>OpenEditedFolder();B("EditedPreviewButton").Click+=(s,e)=>PreviewEditedImage();B("EditedEditorButton").Click+=(s,e)=>ShowEditedEditors();B("EditedDetailsButton").Click+=(s,e)=>ShowEditedDetails();
   UiHelp.Tip(B("EditedAddButton"),"Copy finished or in-progress images to Edited.");UiHelp.Tip(B("EditedRefreshButton"),"Find images saved by your editor in Edited.");UiHelp.Tip(B("EditedFolderButton"),"Open the selected image's folder in File Explorer.");
   editedReady=true;RefreshEdited();Window.Activated+=(s,e)=>{if(editedReady&&cancel==null&&!closing)RefreshEdited();};
  }
  void RefreshEdited(string select=null){
   if(!editedReady)return;var projects=new List<EditedProject>();var errors=new List<string>();editedImages=new List<EditedImage>();
   try{if(repo!=null)projects=repo.EditedProjects(out errors);}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException))throw;errors.Add(e.Message);}
   foreach(var project in projects)try{var images=repo.EditedImages(project);foreach(var image in images)image.Project=project;editedImages.AddRange(images);}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException))throw;errors.Add(e.Message);}
   editedImages=editedImages.OrderByDescending(i=>i.Modified).ThenBy(i=>i.Filename).ToList();
   if(select!=null){T("EditedSearchBox").Clear();C("EditedClassFilter").SelectedIndex=0;refreshingEditedTargets=true;try{EditedTargets.SelectedIndex=0;}finally{refreshingEditedTargets=false;}}
   FilterEditedImages();if(select!=null)G("EditedGrid").SelectedItem=G("EditedGrid").Items.Cast<EditedImage>().FirstOrDefault(i=>i.Project.Id==select);
   L("EditedSummary").ToolTip=errors.Count==0?null:string.Join("\n",errors);
  }
  void FilterEditedImages(bool rebuildTargets=true){
   if(!editedReady)return;var selected=ActiveEditedImage;var words=Util.Tokens(T("EditedSearchBox").Text);L("EditedSearchHint").Visibility=words.Length==0?Visibility.Visible:Visibility.Collapsed;
   string imageClass=Convert.ToString(C("EditedClassFilter").SelectedItem);var rows=editedImages.Where(i=>(imageClass=="All images"||i.Metadata.ImageClass==imageClass)&&words.All(w=>(i.Filename+" "+i.Kind+" "+i.Source+" "+i.Metadata.ImageClass+" "+i.Metadata.ObjectLabel+" "+Catalog.Aliases(i.Metadata.Object)+" "+i.Metadata.Filters).IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
   if(rebuildTargets){string target=EditedTargets.SelectedItem is TargetSummary?((TargetSummary)EditedTargets.SelectedItem).Name:"All targets";var summaries=TargetNavigation.Build(rows.Select(i=>new Frame{Target=i.Metadata.ImageClass=="Meteor"?"Meteor":i.Metadata.Object,Kind="Edited image"})).Select(t=>new EditedTargetSummary{Name=t.Name,Files=t.Files}).ToList();var view=new ListCollectionView(summaries);view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));refreshingEditedTargets=true;try{EditedTargets.ItemsSource=view;EditedTargets.SelectedItem=summaries.FirstOrDefault(t=>t.Name==target)??summaries.First();}finally{refreshingEditedTargets=false;}}
   var active=EditedTargets.SelectedItem as TargetSummary;if(active!=null&&active.Name!="All targets")rows=rows.Where(i=>Catalog.CanonicalTarget(i.Metadata.ImageClass=="Meteor"?"Meteor":i.Metadata.Object)==active.Name).ToList();SetRows("EditedGrid",rows);G("EditedGrid").SelectedItem=rows.FirstOrDefault(i=>selected!=null&&i.Project.Id==selected.Project.Id&&i.RelativePath==selected.RelativePath);
   L("EditedSummary").Text=rows.Count+" images";L("EditedEmptyState").Visibility=rows.Count==0?Visibility.Visible:Visibility.Collapsed;UpdateEditedActions();
  }
  void UpdateEditedActions(){
   if(!editedReady)return;bool ready=repo!=null&&cancel==null;B("EditedAddButton").IsEnabled=ready;B("EditedImportFolderButton").IsEnabled=ready;B("EditedRefreshButton").IsEnabled=ready;B("EditedFolderButton").IsEnabled=ready&&EditedImageProject!=null;B("EditedFiltersButton").IsEnabled=ready;B("EditedOpenPreviewButton").IsEnabled=ready&&ActiveEditedImage!=null;
   B("EditedPreviewButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedEditorButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedDetailsButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedEditButton").IsEnabled=ready&&SelectedEditedImages().Count>0;
  }
  void OpenEditedFolder(){if(repo!=null&&EditedImageProject!=null)OpenFolder(Path.GetDirectoryName(repo.EditedPath(EditedImageProject,ActiveEditedImage.RelativePath)));}
  void OpenEditedFilters(){var menu=ThemedMenu();foreach(string imageClass in C("EditedClassFilter").Items){string choice=imageClass;var item=new MenuItem{Header=choice,IsCheckable=true,IsChecked=choice==Convert.ToString(C("EditedClassFilter").SelectedItem)};item.Click+=(s,e)=>C("EditedClassFilter").SelectedItem=choice;menu.Items.Add(item);}menu.PlacementTarget=B("EditedFiltersButton");menu.IsOpen=true;}
  void ShowEditedDetails(){if(ActiveEditedImage!=null)ShowReport(ActiveEditedImage.Filename,ActiveEditedImage.Metadata.Details+(string.IsNullOrEmpty(ActiveEditedImage.MetadataProblem)?"":"\n\nMetadata could not be read: "+ActiveEditedImage.MetadataProblem));}
  void AddEditedImages(){
   if(repo==null||cancel!=null)return;var picker=new OpenFileDialog{Title="Add edited images",Multiselect=true,Filter="Supported images|*.fit;*.fits;*.fts;*.fit.gz;*.fits.gz;*.fts.gz;*.xisf;*.fz;*.ser;*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.avi;*.mp4;*.mov;*.m4v;*.wmv;*.mkv;*.dng;*.cr2;*.cr3;*.nef;*.arw;*.raf;*.orf;*.rw2|All files|*.*"};if(picker.ShowDialog(Window)!=true)return;
   Run(ct=>repo.AddEditedImages(picker.FileNames,null,"Edited images",ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(3);});
  }

  void ImportEditedFolder(){
   if(repo==null||cancel!=null)return;
   var dialog=new FormWindow(Window,"Import edited images",650,390);dialog.Text("Import files from a folder",true);dialog.Text("SOURCE FOLDER");var source=new TextBox{MinWidth=200};var browse=new Button{Content="Browse",Margin=new Thickness(8,0,0,0),Padding=new Thickness(12,8,12,8)};var controls=new DockPanel();DockPanel.SetDock(browse,Dock.Right);controls.Children.Add(browse);controls.Children.Add(source);dialog.Add(controls);
   browse.Click+=(s,e)=>{string selected=Folder("Choose a folder of existing edited images",source.Text);if(selected!=null)source.Text=selected;};var recursive=dialog.Check("Include subfolders",true);dialog.Text("Scan first, then review images before copying. Repository/database folders and files already archived are skipped. Source files are retained.");dialog.Accept("Scan folder",()=>{if(!Directory.Exists(source.Text.Trim())){MessageBox.Show(dialog.Window,"Choose an existing source folder.");return false;}return true;});if(!dialog.Show())return;
   string folder=source.Text.Trim();bool includeSubfolders=recursive.IsChecked==true;EditedImportPlan import=null;Run(ct=>{import=repo.ScanEditedFolder(folder,includeSubfolders,ct,Progress);return "";},done=>ReviewEditedFolder(import));
  }
  FormWindow EditedImportDialog(EditedImportPlan import,out DataGrid table){
   var dialog=new FormWindow(Window,"Import existing edited images",1000,650);dialog.Text(import.Images.Count+" images found",true);
   if(import.SkippedArchived>0||import.SkippedFolders.Count>0)dialog.Text(import.SkippedArchived+" already archived images skipped · "+import.SkippedFolders.Count+" repository/database folders skipped");
   if(import.Errors.Count>0)dialog.Text(import.Errors.Count+" review issues. See details below before importing.");
   dialog.Text("Selected files are added to Edited. Unchecked entries leave existing images untouched. Identical images already in Edited start unchecked. Source files are kept.");
   table=new DataGrid{ItemsSource=import.Images,IsReadOnly=false,AutoGenerateColumns=false,Height=350};table.Columns.Add(new DataGridCheckBoxColumn{Header="IMPORT",Binding=new System.Windows.Data.Binding("Include"){Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged}});
   foreach(var column in new[]{new[]{"FILE","Filename"},new[]{"STATUS","Status"},new[]{"CLASS","ImageClass"},new[]{"OBJECT","Object"},new[]{"TOTAL EXPOSURE","TotalExposure"},new[]{"PROBLEM","Problem"}})table.Columns.Add(new DataGridTextColumn{Header=column[0],Binding=new System.Windows.Data.Binding(column[1]),IsReadOnly=true,Width=column[0]=="FILE"?new DataGridLength(1,DataGridLengthUnitType.Star):DataGridLength.Auto});
   var rowStyle=new Style(typeof(DataGridRow),Window.TryFindResource(typeof(DataGridRow)) as Style);rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("Metadata.Evidence")));table.RowStyle=rowStyle;dialog.Add(table);
   if(import.Errors.Count>0){var problems=new TextBox{Text=string.Join("\n",import.Errors),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=90,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};dialog.Add(problems);}
   var reviewTable=table;dialog.Accept("Import selected images",()=>{reviewTable.CommitEdit(DataGridEditingUnit.Cell,true);reviewTable.CommitEdit(DataGridEditingUnit.Row,true);if(!import.Images.Any(i=>i.Include)){MessageBox.Show(dialog.Window,"Select readable images to add.");return false;}if(import.Images.Any(i=>i.Include&&!string.IsNullOrEmpty(i.Problem))){MessageBox.Show(dialog.Window,"Exclude images with a reported problem before importing.");return false;}return true;});return dialog;
  }
  void ReviewEditedFolder(EditedImportPlan import){
   DataGrid table;var dialog=EditedImportDialog(import,out table);if(!dialog.Show())return;
   Run(ct=>repo.ImportEditedFolder(import,"Edited images",ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(3);});
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
   if(repo==null||cancel!=null||selected.Count==0)return;var dialog=new FormWindow(Window,"Create Edited working copies",610,390);dialog.Text("Create working copies for your editor",true);
   dialog.Text("Copy and verify the selected archived images into Edited, then open their folder. Load these copies in your preferred editor and save outputs alongside them.");dialog.Accept("Create working copies",()=>true);if(!dialog.Show())return;
   Run(ct=>repo.CreateEditedWorkingCopies(selected,"Edited working copies","Other editor",ct,Progress).Id,id=>{RefreshEdited(id);GoToPage(3);OpenEditedFolder();});
  }

  void BuildEditedNavigation(MenuItem menu){
   menu.Items.Add(ColumnsNavigation("EditedGrid"));menu.Items.Add(MenuAction("Browse edited images",()=>{RefreshEdited();GoToPage(3);},true,false));menu.Items.Add(MenuAction("Add images…",AddEditedImages,repo!=null));menu.Items.Add(MenuAction("Import folder…",ImportEditedFolder,repo!=null));
   menu.Items.Add(MenuAction("Edit metadata…",EditEditedMetadata,repo!=null&&SelectedEditedImages().Count>0));
   menu.Items.Add(MenuAction("Open image folder",OpenEditedFolder,repo!=null&&EditedImageProject!=null));menu.Items.Add(MenuAction("Preview selected image…",PreviewEditedImage,ActiveEditedImage!=null));menu.Items.Add(MenuAction("Refresh edited images",()=>RefreshEdited(),repo!=null));
  }
  void ShowPerformanceTable(){
   var dialog=new FormWindow(Window,"Operation diagnostics",720,520);dialog.Text("Last operation",true);dialog.Text(L("StatusLabel").Text+"\n"+L("RateLabel").Text);
   var grid=new DataGrid{ItemsSource=G("MetricsGrid").ItemsSource,IsReadOnly=true,AutoGenerateColumns=false,MinHeight=160,MaxHeight=320};foreach(var column in G("MetricsGrid").Columns.OfType<DataGridTextColumn>())grid.Columns.Add(new DataGridTextColumn{Header=column.Header,Binding=column.Binding,Width=column.Width});dialog.Add(grid);dialog.CloseOnly();dialog.Show();
  }
 }
 public sealed class EditedTargetSummary:TargetSummary {public new string Tooltip{get{return Label+"\n"+Files+" edited image"+(Files==1?"":"s");}}}
}
