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
  MotionPreview editedMotion;PreviewViewport editedPreviewViewport;CancellationTokenSource editedPreviewCancel;int editedPreviewGeneration;PreviewData editedPreviewData;
  void InitializeEditedPreview(){
   editedPreviewViewport=new PreviewViewport((Grid)Window.FindName("EditedPreviewHost"),(Grid)Window.FindName("EditedPreviewStage"),(Image)Window.FindName("EditedPreviewImage"));InitializeEditedPreviewLayout();
   B("EditedOpenPreviewButton").Click+=(s,e)=>PreviewEditedImage();
  }
  void CancelEditedPreview(){if(editedMotion!=null){editedMotion.Dispose();editedMotion=null;}editedPreviewGeneration++;if(editedPreviewCancel!=null){editedPreviewCancel.Cancel();editedPreviewCancel.Dispose();editedPreviewCancel=null;}editedPreviewData=null;if(editedPreviewViewport!=null)editedPreviewViewport.SetImage(null,true);}
  async void LoadEditedPreview(bool reload=true,Func<string,CancellationToken,Frame,PreviewData> decode=null){
   if(metadataPreviewSuspended)return;
   if(!editedReady)return;var image=ActiveEditedImage;if(image==null||repo==null){CancelEditedPreview();L("EditedPreviewName").Text="Select an edited image";L("EditedPreviewMessage").Text="Select an edited image.";L("EditedPreviewMessage").Visibility=Visibility.Visible;return;}
   if(editedMotion!=null){editedMotion.Dispose();editedMotion=null;}if(editedPreviewCancel!=null){editedPreviewCancel.Cancel();editedPreviewCancel.Dispose();}editedPreviewCancel=new CancellationTokenSource();var token=editedPreviewCancel.Token;int generation=++editedPreviewGeneration;var previous=reload||editedPreviewData==null?null:editedPreviewData.Copy();
   string path;try{path=repo.EditedPath(image.Project,image.RelativePath);}catch(Exception error){L("EditedPreviewMessage").Text=error.Message;L("EditedPreviewMessage").Visibility=Visibility.Visible;return;}
   if(reload){editedPreviewData=null;editedPreviewViewport.BeginLoading();}
   L("EditedPreviewName").Text=image.Filename;L("EditedPreviewName").ToolTip=path;L("EditedPreviewMessage").Text="Loading image…";L("EditedPreviewMessage").Visibility=Visibility.Visible;string mode=ScientificPreview(path)&&!ObservationTargets.Unstretched(image.Metadata.Object,null)?settings.PreviewStretch??"Auto per channel":"Linear";
   if(MediaFiles.Motion(path)){editedMotion=new MotionPreview(editedPreviewViewport,path,info=>{if(generation!=editedPreviewGeneration)return;L("EditedPreviewMessage").Visibility=Visibility.Collapsed;},message=>{if(generation!=editedPreviewGeneration)return;L("EditedPreviewMessage").Text=message;L("EditedPreviewMessage").Visibility=Visibility.Visible;});editedMotion.Start();return;}
   try{Func<bool> current=()=>generation==editedPreviewGeneration&&!Window.Dispatcher.HasShutdownStarted;
    var data=previous??await LoadPreviewSamples(path,null,null,image.Metadata.Object,token,current,null,decode);data.ApplyContext(new Frame{Target=image.Metadata.Object},path);
    if(!current()||token.IsCancellationRequested)return;editedPreviewData=data;bool firstPaint=true;
    await RenderProgressivePreview(data,mode,previous==null,token,current,(bitmap,provisional)=>{
     editedPreviewViewport.SetImage(bitmap,reload&&firstPaint);firstPaint=false;L("EditedPreviewMessage").Visibility=Visibility.Collapsed;
    });
   }catch(OperationCanceledException){}catch(Exception error){if(generation!=editedPreviewGeneration||Window.Dispatcher.HasShutdownStarted)return;if(((Image)Window.FindName("EditedPreviewImage")).Source==null)editedPreviewViewport.BeginLoading();L("EditedPreviewMessage").Text="Preview unavailable\n\n"+error.Message;L("EditedPreviewMessage").Visibility=Visibility.Visible;}
  }
  void InitializeEdited(){
   C("EditedClassFilter").ItemsSource=new[]{"All images","Starless","Stars only","GIF","Meteor","Edited image","Unknown (conflicting labels)"};C("EditedClassFilter").SelectedIndex=0;C("EditedClassFilter").SelectionChanged+=(s,e)=>FilterEditedImages();
   T("EditedSearchBox").TextChanged+=(s,e)=>ScheduleSearch("EditedSearchBox");G("EditedGrid").SelectionChanged+=(s,e)=>{if(restoringTargetSelection)return;CaptureTargetSelection("EditedGrid");UpdateEditedActions();LoadEditedPreview();};G("EditedGrid").MouseDoubleClick+=(s,e)=>PreviewEditedImage();
   EditedTargets.SelectionChanged+=(s,e)=>{if(!refreshingEditedTargets)FilterEditedImages(false);};B("EditedClearButton").Click+=(s,e)=>{T("EditedSearchBox").Clear();C("EditedClassFilter").SelectedIndex=0;EditedTargets.SelectedIndex=0;FilterEditedImages();};B("EditedFiltersButton").Click+=(s,e)=>OpenEditedFilters();InitializeEditedPreview();
   InitializeEditedFileMenu();B("EditedEditButton").Click+=(s,e)=>EditEditedMetadata();
   B("EditedAddButton").Click+=(s,e)=>AddEditedImages();B("EditedImportFolderButton").Click+=(s,e)=>ImportEditedFolder();B("EditedRefreshButton").Click+=(s,e)=>RefreshEdited();B("DismissEditedImportNotice").Click+=(s,e)=>((FrameworkElement)Window.FindName("EditedImportNotice")).Visibility=Visibility.Collapsed;
   B("EditedFolderButton").Click+=(s,e)=>OpenEditedFolder();B("EditedPreviewButton").Click+=(s,e)=>PreviewEditedImage();B("EditedEditorButton").Click+=(s,e)=>ShowEditedEditors();B("EditedDetailsButton").Click+=(s,e)=>ShowEditedDetails();
   UiHelp.Describe(B("EditedAddButton"),"Add images to Edited.");UiHelp.Hint(B("EditedRefreshButton"),"Find new editor outputs.");UiHelp.Describe(B("EditedFolderButton"),"Open image folder.");UiHelp.Hint(B("EditedClearButton"),"Clear search, image-class filter and target selection.");
   editedReady=true;RefreshEdited();InitializeEditedRefresh();
  }
  void RefreshEdited(string select=null,string focusPath=null){
   if(!editedReady)return;CancelAutomaticEditedRefresh();editedCaptureTargets=all.Select(f=>f.Target).Distinct().ToArray();var gallery=EditedGallery.Read(repo,editedCaptureTargets,CancellationToken.None);editedImages=gallery.Images;
   if(select!=null){T("EditedSearchBox").Clear();C("EditedClassFilter").SelectedIndex=0;refreshingEditedTargets=true;try{EditedTargets.SelectedIndex=0;}finally{refreshingEditedTargets=false;}}
   editedFocusProject=select;editedFocusPath=focusPath;FilterEditedImages();
   L("EditedSummary").ToolTip=gallery.Errors.Count==0?null:string.Join("\n",gallery.Errors);
  }
  string editedFocusProject,editedFocusPath;
  void RestoreEditedSelection(IEnumerable<EditedImage> rows,EditedImage previous){
   var focus=rows.FirstOrDefault(i=>editedFocusProject!=null?i.Project.Id==editedFocusProject&&(editedFocusPath==null||i.RelativePath==editedFocusPath):previous!=null&&i.Project.Id==previous.Project.Id&&i.RelativePath==previous.RelativePath);
   if(editedFocusProject!=null&&focus!=null)editedSelection.Add(focus);RestoreTargetSelection("EditedGrid",focus);editedFocusProject=null;editedFocusPath=null;LoadEditedPreview();
  }
  void FilterEditedImages(bool rebuildTargets=true){
   if(!editedReady)return;if(Window.IsLoaded&&editedImages.Count>2000){ScheduleSearch("EditedSearchBox",true,true);return;}CancelSearch("EditedSearchBox");var selected=ActiveEditedImage;var query=FileSearch.Parse(T("EditedSearchBox").Text);ShowSearchError("EditedSearchBox",query);L("EditedSearchHint").Visibility=query.IsEmpty?Visibility.Visible:Visibility.Collapsed;
   string imageClass=Convert.ToString(C("EditedClassFilter").SelectedItem);var rows=editedImages.Where(i=>(imageClass=="All images"||i.Metadata.ImageClass==imageClass)&&query.Matches(EditedSearchDocument(i))).ToList();
   if(rebuildTargets){string target=EditedTargets.SelectedItem is TargetSummary?((TargetSummary)EditedTargets.SelectedItem).Name:"All targets";var summaries=TargetNavigation.Build(rows.Select(i=>new Frame{Target=i.Metadata.ImageClass=="Meteor"?"Meteor":i.Metadata.Object,Kind="Edited image"})).Select(t=>new EditedTargetSummary{Name=t.Name,Files=t.Files}).ToList();var view=new ListCollectionView(summaries);view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));refreshingEditedTargets=true;try{EditedTargets.ItemsSource=view;EditedTargets.SelectedItem=summaries.FirstOrDefault(t=>t.Name==target)??summaries.First();}finally{refreshingEditedTargets=false;}}
   var active=EditedTargets.SelectedItem as TargetSummary;if(active!=null&&active.Name!="All targets")rows=rows.Where(i=>Catalog.CanonicalTarget(i.Metadata.ImageClass=="Meteor"?"Meteor":i.Metadata.Object)==active.Name).ToList();SetRows("EditedGrid",rows);RestoreEditedSelection(rows,selected);
   L("EditedSummary").Text=rows.Count+" images";L("EditedEmptyState").Visibility=rows.Count==0?Visibility.Visible:Visibility.Collapsed;UpdateEditedActions();
  }
  void UpdateEditedActions(){
   if(!editedReady)return;bool ready=repo!=null&&!RepositoryOperationBlocked&&!SearchBlocked("EditedSearchBox");B("EditedAddButton").IsEnabled=ready;B("EditedImportFolderButton").IsEnabled=ready;B("EditedRefreshButton").IsEnabled=ready;B("EditedFolderButton").IsEnabled=ready&&EditedImageProject!=null;B("EditedFiltersButton").IsEnabled=ready;B("EditedOpenPreviewButton").IsEnabled=ready&&ActiveEditedImage!=null;
   B("EditedPreviewButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedEditorButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedDetailsButton").IsEnabled=ready&&ActiveEditedImage!=null;B("EditedEditButton").IsEnabled=ready&&SelectedEditedImages().Count>0;
  }
  void OpenEditedFolder(){if(repo!=null&&EditedImageProject!=null)OpenFolder(Path.GetDirectoryName(repo.EditedPath(EditedImageProject,ActiveEditedImage.RelativePath)));}
  void OpenEditedFilters(){var menu=ThemedMenu();foreach(string imageClass in C("EditedClassFilter").Items){string choice=imageClass;var item=new MenuItem{Header=choice,IsCheckable=true,IsChecked=choice==Convert.ToString(C("EditedClassFilter").SelectedItem)};item.Click+=(s,e)=>C("EditedClassFilter").SelectedItem=choice;menu.Items.Add(item);}menu.PlacementTarget=B("EditedFiltersButton");menu.IsOpen=true;}
  void ShowEditedDetails(){if(ActiveEditedImage!=null)ShowReport(ActiveEditedImage.Filename,ActiveEditedImage.Metadata.Details+(string.IsNullOrEmpty(ActiveEditedImage.MetadataProblem)?"":"\n\nMetadata could not be read: "+ActiveEditedImage.MetadataProblem));}
  void AddEditedImages(){
   if(repo==null||RepositoryOperationBlocked)return;var picker=new OpenFileDialog{Title="Add edited images",Multiselect=true,Filter="Supported images|*.fit;*.fits;*.fts;*.fit.gz;*.fits.gz;*.fts.gz;*.xisf;*.fz;*.ser;*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.avi;*.mp4;*.mov;*.m4v;*.wmv;*.mkv;*.dng;*.cr2;*.cr3;*.nef;*.arw;*.raf;*.orf;*.rw2|All files|*.*"};if(picker.ShowDialog(Window)!=true)return;
   var result=new EditedImportResult();Run(ct=>{var project=repo.AddEditedImages(picker.FileNames,null,"Edited images",ct,Progress,result);return project==null?null:project.Id;},id=>EditedImportComplete(id,result));
  }
  void ImportEditedFolder(){
   if(repo==null||RepositoryOperationBlocked)return;
   var dialog=new FormWindow(Window,"Import edited images",650,390);AddImportPreferencesButton(dialog,true);dialog.Text("Import files from a folder",true);dialog.Text("SOURCE FOLDER");var source=new TextBox{MinWidth=200};var browse=new Button{Content="Browse",Margin=new Thickness(8,0,0,0),Padding=new Thickness(12,8,12,8)};var controls=new DockPanel();DockPanel.SetDock(browse,Dock.Right);controls.Children.Add(browse);controls.Children.Add(source);dialog.Add(controls);
   browse.Click+=(s,e)=>{string selected=Folder("Choose a folder of existing edited images",source.Text);if(selected!=null)source.Text=selected;};var recursive=dialog.Check("Include subfolders",true);dialog.Text("Scan first, then review images before copying. Repository/database folders and files already archived are skipped. Source files are retained.");dialog.Accept("Scan folder",()=>{if(!Directory.Exists(source.Text.Trim())){MessageBox.Show(dialog.Window,"Choose an existing source folder.");return false;}return true;});if(!dialog.Show())return;
   string folder=source.Text.Trim();bool includeSubfolders=recursive.IsChecked==true;EditedImportPlan import=null;Run(ct=>{import=repo.ScanEditedFolder(folder,includeSubfolders,ct,Progress);return "";},done=>{var archive=repo;QueueActivityReview("Edited images ready to review",()=>{if(repo!=archive)throw new InvalidOperationException("The repository changed. Scan the edited folder again.");ReviewEditedFolder(import);});});
  }
  FormWindow EditedImportDialog(EditedImportPlan import,out DataGrid table){
   var dialog=new FormWindow(Window,"Import existing edited images",1000,650);AddImportPreferencesButton(dialog,true);dialog.Text(import.Images.Count+" images found",true);
   if(import.SkippedArchived>0||import.SkippedFolders.Count>0)dialog.Text(import.SkippedArchived+" already archived images skipped · "+import.SkippedFolders.Count+" repository/database folders skipped");
   if(import.Errors.Count>0)dialog.Text(import.Errors.Count+" review issues. See details below.");
   dialog.Text(import.Images.Count(i=>!string.IsNullOrEmpty(i.DuplicateReason))+" duplicates excluded · "+import.Images.Count(i=>!string.IsNullOrEmpty(i.NameConflict))+" matching names with different content. Selected files are added to Edited. Unchecked entries leave existing images untouched. Source files are retained.");
   table=EditedImportReviewTable(import);dialog.Add(table);
   if(import.Errors.Count>0)dialog.Add(new TextBox{Text=string.Join("\n",import.Errors),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=90,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   if(!import.Images.Any(i=>i.CanInclude)){dialog.Text("No new readable images to import. Existing copies are retained.");dialog.CloseOnly();return dialog;}
   var review=table;dialog.Accept("Import selected images",()=>{review.CommitEdit(DataGridEditingUnit.Cell,true);review.CommitEdit(DataGridEditingUnit.Row,true);if(!import.Images.Any(i=>i.Include)){MessageBox.Show(dialog.Window,"Select readable images to add.");return false;}if(import.Images.Any(i=>i.Include&&!i.CanInclude)){MessageBox.Show(dialog.Window,"Exclude duplicates and images with a reported problem before importing.");return false;}return true;});return dialog;
  }
  void ReviewEditedFolder(EditedImportPlan import){
   DataGrid table;var dialog=EditedImportDialog(import,out table);if(!dialog.Show()){if(reviewingActivity!=null){reviewingActivity.NeedsReview=true;reviewingActivity.Status="Results kept for later review.";}return;}
   var result=new EditedImportResult();Run(ct=>{var project=repo.ImportEditedFolder(import,"Edited images",ct,Progress,result);return project==null?null:project.Id;},id=>EditedImportComplete(id,result));
  }
  DataGrid EditedImportReviewTable(EditedImportPlan import){
   var checkStyle=new Style(typeof(CheckBox),Window.TryFindResource(typeof(CheckBox)) as Style);checkStyle.Setters.Add(new Setter(UIElement.IsEnabledProperty,new System.Windows.Data.Binding("CanInclude")));
   var table=new DataGrid{ItemsSource=import.Images,IsReadOnly=false,AutoGenerateColumns=false,Height=350};table.Columns.Add(new DataGridCheckBoxColumn{Header="IMPORT",ElementStyle=checkStyle,EditingElementStyle=checkStyle,Binding=new System.Windows.Data.Binding("Include"){Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged}});
   var wrap=new Style(typeof(TextBlock));wrap.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
   foreach(var column in new[]{new[]{"FILE","Filename"},new[]{"STATUS","Status"},new[]{"FILE TYPE","FileType"},new[]{"CLASS","ImageClass"},new[]{"OBJECT","Object"},new[]{"TOTAL EXPOSURE","TotalExposure"},new[]{"DETAILS","ReviewNote"}})table.Columns.Add(new DataGridTextColumn{Header=column[0],Binding=new System.Windows.Data.Binding(column[1]),IsReadOnly=true,MinWidth=column[0]=="FILE"?160:0,Width=column[0]=="DETAILS"?new DataGridLength(300):column[0]=="FILE"?new DataGridLength(1,DataGridLengthUnitType.Star):DataGridLength.Auto,ElementStyle=column[0]=="DETAILS"?wrap:null});
   var rowStyle=new Style(typeof(DataGridRow),Window.TryFindResource(typeof(DataGridRow)) as Style);rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("ReviewNote")));table.RowStyle=rowStyle;return table;
  }
  void EditedImportComplete(string id,EditedImportResult result){RefreshEdited();L("StatusLabel").Text=result.Summary;L("EditedImportNoticeText").Text=result.Summary;((FrameworkElement)Window.FindName("EditedImportNotice")).Visibility=Visibility.Visible;if(result.Warnings.Count>0)ShowReport("Edited duplicate checks",result.Summary+"\n\nExisting files that could not be checked:\n"+string.Join("\n",result.Warnings));}
  void PreviewEditedImage(){
   if(repo==null||RepositoryOperationBlocked||EditedImageProject==null||ActiveEditedImage==null)return;string path=repo.EditedPath(EditedImageProject,ActiveEditedImage.RelativePath);if(MediaFiles.Motion(path)){new ImagePreviewWindow(Window,ActiveEditedImage.Filename,path).ShowDialog();return;}PreviewData data=null;byte[] pixels=null;
   Run(ct=>{data=DecodeFullPreview(path,ct);data.ApplyContext(null,path);pixels=data.Render(ScientificPreview(path)?settings.PreviewStretch??"Auto per channel":"Linear",ct);return path;},image=>new ImagePreviewWindow(Window,Path.GetFileName(image),data.Width,data.Height,pixels).ShowDialog());
  }
  void ShowEditedEditors(){
   ExportEditedTo();
  }
  void CreateEditedCopies(List<Frame> selected){
   if(repo==null||RepositoryOperationBlocked||selected.Count==0)return;var dialog=new FormWindow(Window,"Create Edited working copies",610,390);dialog.Text("Create working copies for your editor",true);
   dialog.Text("Copy and verify the selected archived images into Edited, then open their folder. Load these copies in your preferred editor and save outputs alongside them.");dialog.Accept("Create working copies",()=>true);if(!dialog.Show())return;
   Run(ct=>repo.CreateEditedWorkingCopies(selected,"Edited working copies","Other editor",ct,Progress).Id,id=>{RefreshEdited();if(completionActivity!=null){completionActivity.Status="Edited working copies are ready.";completionActivity.OutputPath=repo.EditedFolder;}});
  }
  void ShowPerformanceTable(){
   var dialog=new FormWindow(Window,"Operation diagnostics",720,520);dialog.Text("Last operation",true);dialog.Text(L("StatusLabel").Text+"\n"+L("RateLabel").Text);
   var grid=new DataGrid{ItemsSource=G("MetricsGrid").ItemsSource,IsReadOnly=true,AutoGenerateColumns=false,MinHeight=160,MaxHeight=320};foreach(var column in G("MetricsGrid").Columns.OfType<DataGridTextColumn>())grid.Columns.Add(new DataGridTextColumn{Header=column.Header,Binding=column.Binding,Width=column.Width});dialog.Add(grid);dialog.CloseOnly();dialog.Show();
  }
 }
 public sealed class EditedTargetSummary:TargetSummary {public new string Tooltip{get{return Label+"\n"+Files+" edited image"+(Files==1?"":"s");}}}
}
