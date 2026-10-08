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
  void SmokeEdited(string output){
   var original=repo;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string directory=Path.Combine(Path.GetTempPath(),"AstroArchive-edited-ui-"+Guid.NewGuid().ToString("N")),source=directory+"-source";Repository temporary=null;
   try{
    Directory.CreateDirectory(source);string filename="M31_Ha_120x60s_starless.png",path=Path.Combine(source,filename);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Gray8,null,new byte[]{50,100,150,200},2)));using(var stream=File.Create(path))encoder.Save(stream);
    temporary=new Repository(directory);repo=temporary;var project=repo.AddEditedImages(new[]{path},null,"Andromeda edits",CancellationToken.None,null);RefreshEdited(project.Id);GoToPage(3);PumpPopupLayout();
    if(EditedProjectsList.Items.Count!=1||G("EditedGrid").Items.Count!=1||!B("EditedAddButton").IsEnabled||!B("EditedImportFolderButton").IsEnabled)throw new Exception("Edited project navigation or import controls did not render.");
    var image=G("EditedGrid").Items.Cast<EditedImage>().Single();G("EditedGrid").SelectedItem=image;PumpPopupLayout();if(!B("EditedPreviewButton").IsEnabled||!B("EditedDetailsButton").IsEnabled||image.Metadata.ImageClass!="Starless"||image.Metadata.TotalExposure!=7200)throw new Exception("Edited selection/acquisition details missing.");
    T("EditedSearchBox").Text="stars only";if(G("EditedGrid").Items.Count!=0)throw new Exception("Edited class search did not filter.");T("EditedSearchBox").Text="M31 starless";if(G("EditedGrid").Items.Count!=1)throw new Exception("Edited object/class search lost a match.");T("EditedSearchBox").Clear();
    foreach(string mode in new[]{"Light","Dark"}){Theme.Apply(Window,mode);PumpPopupLayout();Readable(L("EditedSummary").Foreground,Window.Background,mode+" edited summary");Capture(Path.Combine(output,"AstroArchive_Edited_"+mode+".png"));}
    // New outputs appear on refresh without entering the capture index.
    File.Copy(repo.EditedPath(project,project.Sources[0].RelativePath),Path.Combine(repo.EditedProjectFolder(project),"M31_stars.png"));RefreshEdited(project.Id);if(G("EditedGrid").Items.Count!=2||repo.All().Count!=0)throw new Exception("Editor output discovery altered the capture index.");
    cancel=new CancellationTokenSource();SetBusy(true);PumpPopupLayout();if(!((Popup)Window.FindName("OperationPopup")).IsOpen||!B("CancelButton").IsVisible||B("EditedAddButton").IsEnabled)throw new Exception("Operation progress/cancel panel did not replace the bottom bar.");B("CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!cancel.IsCancellationRequested)throw new Exception("Popup Cancel did not cancel the operation.");cancel.Dispose();cancel=null;SetBusy(false);PumpPopupLayout();if(((Popup)Window.FindName("OperationPopup")).IsOpen)throw new Exception("Operation panel retained idle space.");
    File.WriteAllText(Path.Combine(output,"edited-smoke.txt"),"PASS Edited projects, image/object/class search, acquisition metadata, editor output refresh, light/dark display and active-only operation panel with Cancel.");
   }finally{if(cancel!=null){cancel.Dispose();cancel=null;}((Popup)Window.FindName("OperationPopup")).IsOpen=false;repo=original;if(temporary!=null)temporary.Dispose();RefreshEdited();GoToPage(page);SetBusy(false);if(Directory.Exists(directory))Directory.Delete(directory,true);if(Directory.Exists(source))Directory.Delete(source,true);}
  }
 }
}
