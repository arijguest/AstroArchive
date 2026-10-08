using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  static PreviewData ProgressiveFixture(){
   var data=new PreviewData{Width=256,Height=256,SourceWidth=256,SourceHeight=256,Channels=1,Pixels=new double[256*256],Description="Progressive fixture"};
   for(int i=0;i<data.Pixels.Length;i++)data.Pixels[i]=0.01+0.03*i/data.Pixels.Length;return data;
  }
  void SmokeProgressivePreview(string output){
   string directory=Path.Combine(Path.GetTempPath(),"AstroArchive-progressive-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
   int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex,scale=settings.TextScalePercent;string theme=settings.ThemeMode,stretch=settings.PreviewStretch;double width=Window.Width,height=Window.Height;
   var oldFinished=new ManualResetEventSlim(false);var oldStarted=new ManualResetEventSlim(false);
   try{
    settings.ShowPreview=true;SetPreviewVisibility();Window.Width=1180;Window.Height=1000;
    foreach(string prefix in new[]{"","Edited"}){
     GoToPage(prefix.Length==0?0:2);var preview=prefix.Length==0?previewViewport:editedPreviewViewport;var host=(Grid)Window.FindName(prefix+"PreviewHost");var stage=(Grid)Window.FindName(prefix+"PreviewStage");var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");var image=(Image)Window.FindName(prefix+"PreviewImage");
     foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
      settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();preview.SetImage(BitmapSource.Create(256,256,96,96,PixelFormats.Rgb24,null,new byte[256*256*3],256*3),true);UpdateCaptureSky(prefix,new Frame{Target="M45"});PumpPopupLayout();preview.Resize();PumpPopupLayout();
      var bounds=stage.TransformToAncestor(host).TransformBounds(new Rect(stage.RenderSize));var skyBounds=globe.TransformToAncestor(host).TransformBounds(new Rect(globe.RenderSize));var visibility=globe.Visibility;
      preview.BeginLoading(720,1280);UpdateCaptureSky(prefix,new Frame{Target="M51",ObservedUtc="2026-10-08T07:00:00Z",Latitude=51.5,Longitude=0});L(prefix+"PreviewMessage").Text="Loading image…";L(prefix+"PreviewMessage").Visibility=Visibility.Visible;PumpPopupLayout();
      var pending=stage.TransformToAncestor(host).TransformBounds(new Rect(stage.RenderSize));var pendingSky=globe.TransformToAncestor(host).TransformBounds(new Rect(globe.RenderSize));
      if(image.Source!=null||bounds!=pending||skyBounds!=pendingSky||globe.Visibility!=visibility||!globe.Context.HasHorizon||!globe.Context.TargetLabel.Contains("M51"))throw new Exception("Loading erased/reflowed the frame or delayed sky metadata: "+prefix+mode+textScale);
      var buttons=PopupChildren<Button>(host);foreach(var button in buttons)if(Convert.ToString(button.Content)=="Fit"&&button.IsEnabled)throw new Exception("Blank image kept image navigation enabled");
      CaptureSidebar(host,Path.Combine(output,"AstroArchive_Loading_"+prefix+mode+textScale+".png"));
      var data=ProgressiveFixture();byte[] linear=data.Copy().Render("Linear",CancellationToken.None),final=data.Copy().Render("Auto",CancellationToken.None);int paints=0;
      Matrix initialView=new Matrix();
      var rendering=RenderProgressivePreview(data,"Auto",true,CancellationToken.None,()=>true,(bitmap,provisional)=>{
       var pixels=new byte[linear.Length];bitmap.CopyPixels(pixels,data.Width*3,0);if(provisional!=(paints==0)||!System.Linq.Enumerable.SequenceEqual(pixels,paints==0?linear:final))throw new Exception("Progressive image did not paint Linear before Auto");preview.SetImage(bitmap,paints==0);L(prefix+"PreviewMessage").Visibility=Visibility.Collapsed;
       if(paints==0){var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.PreviewMouseWheelEvent};stage.RaiseEvent(wheel);if(!wheel.Handled)throw new Exception("Initial image could not be zoomed");initialView=((MatrixTransform)image.RenderTransform).Matrix;}
       else if(((MatrixTransform)image.RenderTransform).Matrix!=initialView)throw new Exception("Background stretch reset the user's image view");paints++;
      });
      WaitPreview(()=>rendering.IsCompleted,"Progressive rendering did not finish");rendering.GetAwaiter().GetResult();if(paints!=2)throw new Exception("Progressive rendering missed a display pass");
      using(var cancellation=new CancellationTokenSource()){
       int stalePaints=0;var cancelled=RenderProgressivePreview(data,"Strong",true,cancellation.Token,()=>true,(bitmap,provisional)=>{stalePaints++;cancellation.Cancel();});WaitPreview(()=>cancelled.IsCompleted,"Cancelled progressive stretch kept running");if(!cancelled.IsCanceled||stalePaints!=1)throw new Exception("Old stretch repainted after cancellation");
      }
     }
    }
    GoToPage(0);settings.PreviewStretch="Auto";previewCache.Clear();string old=Path.Combine(directory,"old.fit"),skipped=Path.Combine(directory,"skipped.fit"),latest=Path.Combine(directory,"latest.fit");File.WriteAllText(old,"old");File.WriteAllText(skipped,"skipped");File.WriteAllText(latest,"latest");int oldReads=0,skippedReads=0,latestReads=0;
    Func<string,CancellationToken,Frame,PreviewData> decode=(path,token,frame)=>{if(path==old){Interlocked.Increment(ref oldReads);oldStarted.Set();if(!oldFinished.Wait(5000))throw new Exception("Slow codec fixture was not released");}else if(path==skipped)Interlocked.Increment(ref skippedReads);else Interlocked.Increment(ref latestReads);return ProgressiveFixture();};
    LoadPreview(old,true,new Frame{Target="M45",Width=256,Height=256,OriginalName="Old capture"},decode);WaitPreview(()=>oldStarted.IsSet,"Slow decode did not start");
    LoadPreview(skipped,true,new Frame{Target="M31",Width=256,Height=256},decode);LoadPreview(latest,true,new Frame{Target="M51",Width=256,Height=256,OriginalName="Latest capture"},decode);
    if(((Image)Window.FindName("PreviewImage")).Source!=null||!((SkyGlobeView)Window.FindName("PreviewSky")).Context.TargetLabel.Contains("M51"))throw new Exception("Rapid selection did not update the sky immediately");oldFinished.Set();
    WaitPreview(()=>previewData!=null&&previewData.Target=="M51"&&previewData.DisplayMode=="Auto"&&((Image)Window.FindName("PreviewImage")).Source!=null&&!Convert.ToString(L("PreviewInfo").ToolTip).Contains("applying selected stretch"),"Latest selection did not finish rendering");
    if(oldReads!=1||skippedReads!=0||latestReads!=1||L("PreviewName").Text!="Latest capture")throw new Exception("Passed-over selection decoded or old result replaced latest capture");
    LoadPreview(latest,true,new Frame{Target="M51",OriginalName="Latest capture"},decode);WaitPreview(()=>previewData!=null&&previewData.DisplayMode=="Auto"&&((Image)Window.FindName("PreviewImage")).Source!=null&&!Convert.ToString(L("PreviewInfo").ToolTip).Contains("applying selected stretch"),"Cached preview did not render");if(latestReads!=1)throw new Exception("Revisiting a frame repeated its decode");
    var group=new System.Collections.Generic.List<Frame>();for(int i=0;i<3;i++)group.Add(new Frame{Target="M51",Kind="Light",Width=256,Height=256,Exposure=30,ObservedUtc=new DateTime(2026,10,8,7,i*20,0,DateTimeKind.Utc).ToString("o"),Latitude=51.5,Longitude=0});
    LoadPreview(latest,true,group[0],decode,group);var groupGlobe=(SkyGlobeView)Window.FindName("PreviewSky");if(groupGlobe.Context.StackTrack.Length!=33)throw new Exception("Group preview reload discarded observing interval before image paint");
    WaitPreview(()=>previewData!=null&&L("PreviewMessage").Visibility==Visibility.Collapsed,"Group preview did not finish");PumpPopupLayout();if(groupGlobe.TrackSegmentsDrawn==0)throw new Exception("Loaded group preview did not draw its trail");
    LoadPreview(latest,false);WaitPreview(()=>L("PreviewMessage").Visibility==Visibility.Collapsed,"Group stretch did not finish");PumpPopupLayout();if(groupGlobe.Context.StackTrack.Length!=33||groupGlobe.TrackSegmentsDrawn==0)throw new Exception("Stretching group preview discarded its observing interval");
    LoadPreview(skipped,true,new Frame{Target="M31"},(path,token,frame)=>{throw new IOException("Unreadable preview fixture");});WaitPreview(()=>L("PreviewMessage").Text.Contains("Unreadable preview fixture"),"Decode failure not reported");if(((Image)Window.FindName("PreviewImage")).Source!=null||!((SkyGlobeView)Window.FindName("PreviewSky")).Context.TargetLabel.Contains("M31"))throw new Exception("Decode failure erased the current sky or kept old pixels");
    File.WriteAllText(Path.Combine(output,"progressive-preview-smoke.txt"),"PASS Repository/Edited retained frames during loading in light/dark at 100/150%; immediate sky metadata; Linear-before-stretch pixels; cancelled stretch; slow codec/rapid latest selection; coalesced passed-over frames; cached revisit and decode failure isolation.");
   }finally{oldFinished.Set();CancelPreview();CancelEditedPreview();previewCache.Clear();settings.PreviewStretch=stretch;settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();Window.Width=width;Window.Height=height;GoToPage(page);if(Directory.Exists(directory))Directory.Delete(directory,true);}
  }
 }
}
