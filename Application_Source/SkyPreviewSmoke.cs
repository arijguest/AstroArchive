using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 public partial class MainUi {
  BitmapSource CaptureSidebar(FrameworkElement host,string path){
   // A nested visual retains its parent offset; capture the root, then crop.
   var full=PopupBitmap(Window);var origin=host.TranslatePoint(new Point(),Window);
   var bitmap=new CroppedBitmap(full,new Int32Rect((int)Math.Round(origin.X),(int)Math.Round(origin.Y),(int)Math.Ceiling(host.ActualWidth),(int)Math.Ceiling(host.ActualHeight)));
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);return bitmap;
  }
  void CheckSkyFit(string prefix,PreviewViewport preview,int imageWidth=720,int imageHeight=1280){
   var host=(Grid)Window.FindName(prefix+"PreviewHost");var stage=(Grid)Window.FindName(prefix+"PreviewStage");var panel=(Grid)Window.FindName(prefix+"PreviewSkyPanel");var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");
   double width,height;new PreviewGeometry(imageWidth,imageHeight).Frame(host.ActualWidth,Math.Max(0,host.ActualHeight-PreviewViewport.ToolbarSpace),out width,out height);
   if(Math.Abs(stage.ActualWidth-width)>1||Math.Abs(stage.ActualHeight-height)>1)throw new Exception("Sky reduced maximum image fit: "+prefix+", "+stage.RenderSize+" vs "+width+"x"+height);
   double top=height+PreviewViewport.ToolbarSpace,remaining=Math.Max(0,host.ActualHeight-top);
   if(remaining>=50){var bounds=panel.TransformToAncestor(host).TransformBounds(new Rect(panel.RenderSize));if(panel.Visibility!=Visibility.Visible||Math.Abs(bounds.Top-top)>1||Math.Abs(bounds.Height-remaining)>1||bounds.Bottom>host.ActualHeight+1)throw new Exception("Sky does not fill only the remaining space: "+bounds+", host "+host.RenderSize);}
   else if(panel.Visibility!=Visibility.Collapsed)throw new Exception("Sky stole space from a height-limited image");
   foreach(var label in globe.CardinalLabels){
    var bounds=label.Value;var sphere=globe.GlobeBounds;double x=sphere.X+sphere.Width/2,y=sphere.Y+sphere.Height/2,r=sphere.Width/2;
    double dx=Math.Max(bounds.Left-x,Math.Max(0,x-bounds.Right)),dy=Math.Max(bounds.Top-y,Math.Max(0,y-bounds.Bottom));
    if(!new Rect(globe.RenderSize).Contains(bounds)||dx*dx+dy*dy<(r+3)*(r+3))throw new Exception("Cardinal label overlaps the globe or is clipped: "+label.Key);
   }
   preview.SmokeGestures();
  }
  void SmokeSkyNavigation(string prefix,PreviewViewport preview,Frame frame){
   var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");var host=(Grid)Window.FindName(prefix+"PreviewHost");var original=((Image)Window.FindName(prefix+"PreviewImage")).Source as BitmapSource;
   try{
    // Runners can constrain window height to their desktop. A wider portrait
    // fixture guarantees genuine remaining sky space before exercising inputs.
    if(!globe.IsVisible){preview.SetImage(BitmapSource.Create(1200,1280,96,96,PixelFormats.Rgb24,null,new byte[1200*1280*3],1200*3),true);PumpPopupLayout();preview.Resize();PumpPopupLayout();}
    if(!globe.IsVisible||globe.ActualHeight<50)throw new Exception("Sky gesture fixture has no visible globe: "+prefix+", host "+host.RenderSize);
    int visibleLabels=0,hiddenLabels=0;
    foreach(double yawAngle in new[]{0.0,45,90,135,180,225,270,315})foreach(double tiltAngle in new[]{0.0,25,70}){
     globe.Camera.Orbit(yawAngle-globe.Camera.Yaw,tiltAngle-globe.Camera.Tilt);globe.ZoomView(1);PumpPopupLayout();
     visibleLabels+=globe.CardinalLabels.Count;hiddenLabels+=4-globe.CardinalLabels.Count;
     foreach(var label in globe.CardinalLabels){var b=label.Value;var sphere=globe.GlobeBounds;double x=sphere.X+sphere.Width/2,y=sphere.Y+sphere.Height/2,r=sphere.Width/2,dx=Math.Max(b.Left-x,Math.Max(0,x-b.Right)),dy=Math.Max(b.Top-y,Math.Max(0,y-b.Bottom));if(!new Rect(globe.RenderSize).Contains(b)||dx*dx+dy*dy<(r+3)*(r+3))throw new Exception("Rotated cardinal overlaps sky or clips");}
    }
    if(visibleLabels==0||hiddenLabels==0)throw new Exception("Cardinals did not adapt to camera orientation");
    globe.ZoomView(100);PumpPopupLayout();
    foreach(var label in globe.CardinalLabels){var b=label.Value;var sphere=globe.GlobeBounds;double x=sphere.X+sphere.Width/2,y=sphere.Y+sphere.Height/2,r=sphere.Width/2,dx=Math.Max(b.Left-x,Math.Max(0,x-b.Right)),dy=Math.Max(b.Top-y,Math.Max(0,y-b.Bottom));if(!new Rect(globe.RenderSize).Contains(b)||dx*dx+dy*dy<(r+3)*(r+3))throw new Exception("Zoomed globe obscures cardinal label");}globe.ResetView();PumpPopupLayout();
    int builds=globe.DrawingBuilds;
       var sky=globe.Context;double yaw=globe.Camera.Yaw,tilt=globe.Camera.Tilt;globe.RotateView(25,12);PumpPopupLayout();
       if(globe.Camera.Yaw==yaw||globe.Camera.Tilt==tilt||globe.DrawingBuilds<=builds||!ReferenceEquals(sky,globe.Context))throw new Exception("Sky drag did not rotate without changing capture data: "+prefix+", camera "+yaw+","+tilt+" -> "+globe.Camera.Yaw+","+globe.Camera.Tilt+", draws "+builds+" -> "+globe.DrawingBuilds);
       var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.MouseWheelEvent};globe.RaiseEvent(wheel);PumpPopupLayout();if(!wheel.Handled||globe.Camera.Zoom<=1)throw new Exception("Sky scroll did not zoom");
       UpdateCaptureSky(prefix,frame);PumpPopupLayout();if(globe.Camera.Zoom<=1)throw new Exception("Refreshing unchanged capture reset sky exploration");
       var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(globe),0,Key.Left){RoutedEvent=Keyboard.KeyDownEvent};double rotated=globe.Camera.Yaw;globe.RaiseEvent(key);if(!key.Handled||globe.Camera.Yaw==rotated)throw new Exception("Sky keyboard navigation did not rotate");
       B(prefix+"PreviewSkyResetButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();if(globe.Camera.Yaw!=yaw||globe.Camera.Tilt!=tilt||globe.Camera.Zoom!=1)throw new Exception("Sky reset button did not restore capture view");
       if(!globe.Focusable||!globe.IsManipulationEnabled)throw new Exception("Sky touch or keyboard navigation is disabled");
       var reset=B(prefix+"PreviewSkyResetButton");var resetBounds=reset.TransformToAncestor(host).TransformBounds(new Rect(reset.RenderSize));var sphereBounds=globe.TransformToAncestor(host).TransformBounds(globe.GlobeBounds);var panel=(Grid)Window.FindName(prefix+"PreviewSkyPanel");var panelBounds=panel.TransformToAncestor(host).TransformBounds(new Rect(panel.RenderSize));
       if(Math.Abs(sphereBounds.X+sphereBounds.Width/2-(panelBounds.X+panelBounds.Width/2))>1||Math.Abs(sphereBounds.Y+sphereBounds.Height/2-(panelBounds.Y+panelBounds.Height/2))>1||Math.Abs(sphereBounds.Width-(Math.Min(panel.ActualWidth,panel.ActualHeight)-36))>1)throw new Exception("Sky sphereBounds is not centred or maximised with slight padding");
       if(reset.BorderThickness!=new Thickness(0)||Math.Abs(resetBounds.Left-sphereBounds.Left)>1||Math.Abs(resetBounds.Bottom-sphereBounds.Bottom)>1||!(reset.Content is System.Windows.Shapes.Path))throw new Exception("Reset is not borderless with an oriented icon close to the sphereBounds edge");
   }finally{preview.SetImage(original,true);PumpPopupLayout();preview.Resize();PumpPopupLayout();}
  }
  void SmokeCaptureSky(string output){
   double width=Window.Width,height=Window.Height;int scale=settings.TextScalePercent,page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string theme=settings.ThemeMode;int cases=0;
   var frame=new Frame{Target="M45",ObservedUtc="2026-10-07T23:00:00Z",Latitude=51.5,Longitude=0};var rgb=new byte[720*1280*3];var colours=new[]{new byte[]{220,40,40},new byte[]{40,220,40},new byte[]{40,40,220},new byte[]{220,220,40}};
   for(int y=0;y<1280;y++)for(int x=0;x<720;x++)Array.Copy(colours[(y<640?0:2)+(x<360?0:1)],0,rgb,(y*720+x)*3,3);
   var image=BitmapSource.Create(720,1280,96,96,PixelFormats.Rgb24,null,rgb,720*3);image.Freeze();
   try{
    foreach(string prefix in new[]{"","Edited"}){
     frame.ObservedUtc="2026-10-07T23:00:00Z";frame.RA=null;frame.Dec=null;frame.Latitude=51.5;frame.Longitude=0;
     var preview=prefix.Length==0?previewViewport:editedPreviewViewport;var host=(Grid)Window.FindName(prefix+"PreviewHost");var stage=(Grid)Window.FindName(prefix+"PreviewStage");var panel=(Grid)Window.FindName(prefix+"PreviewSkyPanel");var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");var popup=(Popup)Window.FindName(prefix+"PreviewDetailsPopup");var header=(Grid)Window.FindName(prefix+"PreviewHeader");var original=((Image)Window.FindName(prefix+"PreviewImage")).Source as BitmapSource;
     try{
      GoToPage(prefix.Length==0?0:2);
      foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
       settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();Window.Width=1180;Window.Height=1000;UpdateCaptureSky(prefix,frame);L(prefix+"PreviewMessage").Visibility=Visibility.Collapsed;preview.SetImage(image,true);PumpPopupLayout();preview.Resize();PumpPopupLayout();CheckSkyFit(prefix,preview);
       var imageArea=stage.TransformToAncestor(host).TransformBounds(new Rect(stage.RenderSize));var full=CaptureSidebar(host,Path.Combine(output,"AstroArchive_Capture_Sky_"+prefix+mode+"_"+textScale+".png"));var pixels=new byte[full.PixelWidth*full.PixelHeight*4];full.CopyPixels(pixels,full.PixelWidth*4,0);
       for(int quadrant=0;quadrant<4;quadrant++){int x=(int)(imageArea.X+imageArea.Width*(quadrant%2==0?0.2:0.8)),y=(int)(imageArea.Y+imageArea.Height*(quadrant<2?0.2:0.8)),offset=(y*full.PixelWidth+x)*4;var expected=colours[quadrant];if(Math.Abs(pixels[offset+2]-expected[0])>12||Math.Abs(pixels[offset+1]-expected[1])>12||Math.Abs(pixels[offset]-expected[2])>12||pixels[offset+3]<250)throw new Exception("Sidebar image quadrant clipped: "+prefix+mode+textScale+" quadrant "+quadrant);}
       if(!globe.Context.HasHorizon)throw new Exception("Capture sky did not use frame time/site: "+prefix+mode+textScale+"; "+globe.Context.Evidence);int builds=globe.DrawingBuilds;globe.InvalidateVisual();PumpPopupLayout();if(globe.DrawingBuilds!=builds)throw new Exception("Unchanged sky rebuilt cached drawing");
       SmokeSkyNavigation(prefix,preview,frame);
       foreach(string control in new[]{prefix+"PreviewDetailsButton",prefix+"OpenPreviewButton",prefix.Length==0?"StretchMode":"EditedStretchMode"}){var item=(FrameworkElement)Window.FindName(control);var bounds=item.TransformToAncestor(header).TransformBounds(new Rect(item.RenderSize));if(bounds.Right>header.ActualWidth+1||bounds.Left<0||string.IsNullOrEmpty(AutomationProperties.GetName(item)))throw new Exception("Preview header control clipped or unnamed: "+control);}
       if(object.Equals(B(prefix+"OpenPreviewButton").Content,"Open image…"))throw new Exception("Open image button is not an icon");
       L(prefix+"PreviewInfo").Text="720 × 1280 pixels";B(prefix+"PreviewDetailsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();if(!popup.IsOpen||!L(prefix+"PreviewInfo").IsVisible)throw new Exception("Details icon did not expose capture information");CheckSkyFit(prefix,preview);SavePopup((FrameworkElement)popup.Child,Path.Combine(output,"AstroArchive_Capture_Details_"+prefix+mode+textScale+".png"));
       popup.Child.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual((Visual)popup.Child),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});PumpPopupLayout();if(popup.IsOpen)throw new Exception("Escape did not close capture details");
       Window.Height=650;PumpPopupLayout();preview.Resize();PumpPopupLayout();CheckSkyFit(prefix,preview);if(panel.Visibility!=Visibility.Collapsed)throw new Exception("Height-limited image still reserves a sky map");cases+=2;
      }
      Window.Height=1000;PumpPopupLayout();preview.SetImage(image,true);PumpPopupLayout();// Collapsed WPF elements can retain their previous RenderSize. Compare the
      // allocated height, and verify the newly visible drawing bounds separately.
      double first=panel.Height;preview.SetImage(BitmapSource.Create(1200,1280,96,96,PixelFormats.Rgb24,null,new byte[1200*1280*3],1200*3),true);PumpPopupLayout();preview.Resize();PumpPopupLayout();CheckSkyFit(prefix,preview,1200,1280);if(panel.Height<=first)throw new Exception("Sky did not grow when image aspect left more space: "+prefix+", old "+first+", new "+panel.Height+", host "+host.RenderSize+", stage "+stage.RenderSize);
      foreach(string kind in new[]{"Dark","Master dark","Dark flat","Master flat","Bias","Master bias"}){
       var calibration=frame.Clone();calibration.Kind=kind;var imageSize=stage.RenderSize;UpdateCaptureSky(prefix,calibration);PumpPopupLayout();preview.Resize();PumpPopupLayout();if(panel.IsVisible||stage.RenderSize!=imageSize)throw new Exception("Calibration sky is visible or changed image fit: "+kind);
      }
      frame.Kind="Stack";frame.Exposure=3600;frame.StackCount=120;UpdateCaptureSky(prefix,frame);PumpPopupLayout();if(!panel.IsVisible||!globe.TargetMarkerDrawn||globe.Context.Utc!=CaptureSky.CaptureUtc(frame))throw new Exception("Stack sky capture indicator did not render at its recorded time");
      CaptureSidebar(host,Path.Combine(output,"AstroArchive_Visible_Stack_Sky_"+prefix+settings.ThemeMode+".png"));
      var cachedSky=globe.Context;int skyBuilds=globe.DrawingBuilds;UpdateCaptureSky(prefix,frame);globe.InvalidateVisual();PumpPopupLayout();if(!ReferenceEquals(cachedSky,globe.Context)||globe.DrawingBuilds!=skyBuilds)throw new Exception("Unchanged stack indicator rebuilt");
      frame.Kind="Light";
      var group=Enumerable.Range(0,3).Select(i=>{var sub=frame.Clone();sub.Exposure=30;sub.ObservedUtc=new DateTime(2026,10,7,22,i*20,0,DateTimeKind.Utc).ToString("o");return sub;}).ToList();
      UpdateCaptureSky(prefix,group[0]);PumpPopupLayout();if(!globe.TargetMarkerDrawn||globe.Context.Utc!=CaptureSky.CaptureUtc(group[0]))throw new Exception("Group preview did not retain the representative image's capture indicator");
      CaptureSidebar(host,Path.Combine(output,"AstroArchive_Visible_Group_Sky_"+prefix+settings.ThemeMode+".png"));
      RefreshSkyPreviews();PumpPopupLayout();if(!globe.TargetMarkerDrawn||globe.Context.Utc!=CaptureSky.CaptureUtc(group[0]))throw new Exception("Refreshing preview lost its capture indicator");
      UpdateCaptureSky(prefix,frame);PumpPopupLayout();
      var below=frame.Clone();below.RA=SkyOrientation.Wrap(SkyOrientation.Sidereal(CaptureSky.CaptureUtc(below).Value,0)+180);below.Dec=0;below.Latitude=51.5;below.Longitude=0;
      globe.SetContext(CaptureSky.Resolve(below,settings));PumpPopupLayout();var blank=new RenderTargetBitmap((int)Math.Ceiling(globe.ActualWidth),(int)Math.Ceiling(globe.ActualHeight),96,96,PixelFormats.Pbgra32);blank.Render(globe);var blankPixels=new byte[blank.PixelWidth*blank.PixelHeight*4];blank.CopyPixels(blankPixels,blank.PixelWidth*4,0);
      if(!globe.Context.BelowHorizon||blankPixels.Any(b=>b!=0)||globe.CardinalLabels.Count!=0||globe.TargetMarkerDrawn)throw new Exception("Below-horizon sky is not completely blank");
      var fitted=stage.RenderSize;UpdateCaptureSky(prefix,below);PumpPopupLayout();if(panel.IsVisible||stage.RenderSize!=fitted)throw new Exception("Below-horizon map was not hidden or changed image fit");
      UpdateCaptureSky(prefix,frame);PumpPopupLayout();
      frame.Dec=-60;frame.RA=160;frame.Latitude=-33.9;frame.Longitude=151.2;UpdateCaptureSky(prefix,frame);PumpPopupLayout();if(!globe.Context.Evidence.Contains("Southern celestial sky"))throw new Exception("Southern capture hemisphere lost");frame.ObservedUtc=null;UpdateCaptureSky(prefix,frame);PumpPopupLayout();if(globe.Context.HasHorizon||globe.CardinalLabels.Count!=0)throw new Exception("Missing capture clock produced a horizon or compass directions");
     }finally{popup.IsOpen=false;preview.SetImage(original,true);UpdateCaptureSky(prefix,null);}
    }
   }finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();Window.Width=width;Window.Height=height;GoToPage(page);UpdateCaptureSky("",previewFrame);PumpPopupLayout();}
   File.WriteAllText(Path.Combine(output,"capture-sky-smoke.txt"),"PASS: "+cases+" Repository/Edited layouts in light/dark and 100/150% text; maximum image fit, adaptive/hidden sky, image quadrant pixels, adaptive unobscured compass labels and cached drawing, orbit without editing captures, routed scroll/keyboard gestures, view retention/reset, accessible bottom-left reset without added panel height, header icons, details popup/Escape, aspect changes and hemisphere/clock fallback.");
  }
 }
}
