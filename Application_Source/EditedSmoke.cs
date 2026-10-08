using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  static bool EditedSmokeHasProjectControl(DependencyObject parent){if(parent is ComboBox)return true;if(parent==null)return false;foreach(object child in LogicalTreeHelper.GetChildren(parent)){var node=child as DependencyObject;if(node!=null&&EditedSmokeHasProjectControl(node))return true;}return false;}
  void SmokeEdited(string output){
   var original=repo;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string directory=Path.Combine(Path.GetTempPath(),"AstroArchive-edited-ui-"+Guid.NewGuid().ToString("N")),source=directory+"-source";Repository temporary=null;
   try{
    Directory.CreateDirectory(source);string filename="M31_Ha_120x60s_starless.png",path=Path.Combine(source,filename);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Gray8,null,new byte[]{50,100,150,200},2)));using(var stream=File.Create(path))encoder.Save(stream);
    temporary=new Repository(directory);repo=temporary;var project=repo.AddEditedImages(new[]{path},null,"Andromeda edits",CancellationToken.None,null);RefreshEdited(project.Id);GoToPage(3);PumpPopupLayout();
    if(Window.FindName("EditedProjectsList")!=null||G("EditedGrid").Items.Count!=1||!B("EditedAddButton").IsEnabled||!B("EditedImportFolderButton").IsEnabled)throw new Exception("Edited gallery or import controls did not render.");
    var image=G("EditedGrid").Items.Cast<EditedImage>().Single();G("EditedGrid").SelectedItem=image;PumpPopupLayout();if(!B("EditedPreviewButton").IsEnabled||!B("EditedDetailsButton").IsEnabled||image.Metadata.ImageClass!="Starless"||image.Metadata.TotalExposure!=7200)throw new Exception("Edited selection/acquisition details missing.");
    if(EditedTargets.Items.Count!=2||Window.FindName("EditedPreviewPane")==null||Window.FindName("EditedBody")==null)throw new Exception("Edited targets/table/preview layout missing.");
    T("EditedSearchBox").Text="stars only";if(G("EditedGrid").Items.Count!=0)throw new Exception("Edited class search did not filter.");T("EditedSearchBox").Text="M31 starless";if(G("EditedGrid").Items.Count!=1)throw new Exception("Edited object/class search lost a match.");T("EditedSearchBox").Clear();
    foreach(string mode in new[]{"Light","Dark"}){Theme.Apply(Window,mode);PumpPopupLayout();Readable(L("EditedSummary").Foreground,Window.Background,mode+" edited summary");Capture(Path.Combine(output,"AstroArchive_Edited_"+mode+".png"));}
    G("EditedGrid").SelectedIndex=0;WaitPreview(()=>((Image)Window.FindName("EditedPreviewImage")).Source!=null,"Edited inline preview did not decode.");
    RefreshEdited();PumpPopupLayout();if(G("EditedGrid").Items.Count!=1)throw new Exception("Refresh lost the edited image.");
    G("EditedGrid").SelectedIndex=0;if(!B("EditedFolderButton").IsEnabled||EditedImageProject==null)throw new Exception("Gallery cannot open the selected image folder.");
    // New outputs appear on refresh without entering the capture index.
    File.Copy(repo.EditedPath(project,project.Sources[0].RelativePath),Path.Combine(repo.EditedProjectFolder(project),"M31_10x60s_stars.png"));RefreshEdited(project.Id);if(G("EditedGrid").Items.Count!=2||repo.All().Count!=0)throw new Exception("Editor output discovery altered the capture index.");
    var exposureColumn=G("EditedGrid").Columns.First(c=>c.SortMemberPath=="Metadata.TotalExposure");SortTable("EditedGrid",exposureColumn,false);if(G("EditedGrid").Items.Cast<EditedImage>().First().Metadata.TotalExposure!=600)throw new Exception("Edited exposure sorting was textual rather than numeric.");
    if(G("EditedGrid").Columns.Count(c=>c.Visibility==Visibility.Visible)!=5)throw new Exception("Edited default headings are not compact.");
    if(G("EditedGrid").Columns.Any(c=>c.SortMemberPath=="Metadata.ImageClass"||c.SortMemberPath=="Project.Name"))throw new Exception("Edited still offers class/project headings.");
    var plan=repo.ScanEditedFolder(source,true,CancellationToken.None,null);if(plan.Images.Single().Include||!plan.Images.Single().AlreadyImported)throw new Exception("Existing import was selected again.");DataGrid review;var reviewDialog=EditedImportDialog(plan,out review);reviewDialog.Window.Show();PumpPopupLayout();if(EditedSmokeHasProjectControl(reviewDialog.Window.Content as DependencyObject))throw new Exception("Edited import offers project selection.");SavePopup((FrameworkElement)reviewDialog.Window.Content,Path.Combine(output,"AstroArchive_Edited_Import.png"));reviewDialog.Window.Close();
    string second=Path.Combine(source,"M51_20x60s_Ha.png");File.Copy(path,second);var another=repo.AddEditedImages(new[]{second},null,"Old second project",CancellationToken.None,null);RefreshEdited(another.Id);if(G("EditedGrid").Items.Count!=3||ActiveEditedImage.Project.Id!=another.Id)throw new Exception("New import hid existing Edited images.");
    G("EditedGrid").SelectedItem=G("EditedGrid").Items.Cast<EditedImage>().First(i=>i.Project.Id==project.Id&&i.Kind=="Added image");EditedMetadataFields fields;var metadataDialog=EditedMetadataDialog(SelectedEditedImages(),out fields);if(fields.Values().TotalExposure!=null||fields.TotalExposure.Text!="7200")throw new Exception("Metadata editor did not preserve unchanged fields.");metadataDialog.Window.Show();PumpPopupLayout();fields.TotalExposure.Text="3600";fields.Filters.Text="L, Ha";repo.SaveEditedMetadata(project,SelectedEditedImages(),fields.Values(),CancellationToken.None);SavePopup((FrameworkElement)metadataDialog.Window.Content,Path.Combine(output,"AstroArchive_Edited_Metadata.png"));metadataDialog.Window.Close();RefreshEdited();if(G("EditedGrid").Items.Count!=3||G("EditedGrid").Items.Cast<EditedImage>().Single(i=>i.Project.Id==project.Id&&i.Kind=="Added image").Metadata.TotalExposure!=3600||!B("EditedEditButton").IsEnabled)throw new Exception("Saved metadata or combined gallery did not survive refresh.");
    cancel=new CancellationTokenSource();SetBusy(true);PumpPopupLayout();if(!((Popup)Window.FindName("OperationPopup")).IsOpen||!B("CancelButton").IsVisible||B("EditedAddButton").IsEnabled)throw new Exception("Operation progress/cancel panel did not replace the bottom bar.");B("CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!cancel.IsCancellationRequested)throw new Exception("Popup Cancel did not cancel the operation.");cancel.Dispose();cancel=null;SetBusy(false);PumpPopupLayout();if(((Popup)Window.FindName("OperationPopup")).IsOpen)throw new Exception("Operation panel retained idle space.");
    ProcessDumpUi();if(cancel!=null||dumpProgressWindow!=null)throw new Exception("Empty Dump folder opened a progress window.");
    OpenDumpProgress();cancel=new CancellationTokenSource();SetBusy(true);latestProgress=new ProgressInfo{Stage="Copying",Text="Dump progress fixture",TotalKnown=true,Total=2,Done=1,ProgressFraction=0.5};LiveTick();PumpPopupLayout();dumpProgressWindow.UpdateLayout();
    if(!dumpProgressWindow.IsVisible||((Popup)Window.FindName("OperationPopup")).IsOpen||dumpProgressBar.Value!=0.5||!dumpProgressStatus.Text.Contains("Dump progress fixture"))throw new Exception("Dump progress window did not show live progress or duplicated the operation panel.");
    SavePopup((FrameworkElement)dumpProgressWindow.Content,Path.Combine(output,"AstroArchive_Dump_Progress.png"));var dumpWindow=dumpProgressWindow;dumpWindow.Close();if(!cancel.IsCancellationRequested||!dumpWindow.IsVisible)throw new Exception("Closing Dump progress did not wait for safe cancellation.");cancel.Dispose();cancel=null;latestProgress=null;SetBusy(false);if(dumpWindow.IsVisible||dumpProgressWindow!=null)throw new Exception("Dump progress window remained open after finishing.");
    File.WriteAllText(Path.Combine(output,"edited-smoke.txt"),"PASS Combined Edited gallery, additive duplicate import review, saved metadata, image/object/class search, acquisition metadata, editor output refresh, light/dark display and active-only operation panel with Cancel.");
   }finally{CancelEditedPreview();if(cancel!=null){cancel.Dispose();cancel=null;}CloseDumpProgress();latestProgress=null;((Popup)Window.FindName("OperationPopup")).IsOpen=false;repo=original;if(temporary!=null)temporary.Dispose();RefreshEdited();GoToPage(page);SetBusy(false);if(Directory.Exists(directory))Directory.Delete(directory,true);if(Directory.Exists(source))Directory.Delete(source,true);}
  }
 }
}
