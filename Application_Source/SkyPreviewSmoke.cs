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
  static bool SkyHasText(Drawing drawing){var group=drawing as DrawingGroup;return drawing is GlyphRunDrawing||group!=null&&group.Children.Any(SkyHasText);}
  void CheckSkyFit(string prefix,PreviewViewport preview){
   var host=(Grid)Window.FindName(prefix+"PreviewHost");var stage=(Grid)Window.FindName(prefix+"PreviewStage");var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");
   double width,height;new PreviewGeometry(720,1280).Frame(host.ActualWidth,Math.Max(0,host.ActualHeight-PreviewViewport.ToolbarSpace),out width,out height);
   if(Math.Abs(stage.ActualWidth-width)>1||Math.Abs(stage.ActualHeight-height)>1)throw new Exception("Sky reduced maximum image fit: "+prefix+", "+stage.RenderSize+" vs "+width+"x"+height);
   double top=height+PreviewViewport.ToolbarSpace,remaining=Math.Max(0,host.ActualHeight-top);
   if(remaining>=50){var bounds=globe.TransformToAncestor(host).TransformBounds(new Rect(globe.RenderSize));if(globe.Visibility!=Visibility.Visible||Math.Abs(bounds.Top-top)>1||Math.Abs(bounds.Height-remaining)>1||bounds.Bottom>host.ActualHeight+1)throw new Exception("Sky does not fill only the remaining space: "+bounds+", host "+host.RenderSize);}
   else if(globe.Visibility!=Visibility.Collapsed)throw new Exception("Sky stole space from a height-limited image");
   if(SkyHasText(VisualTreeHelper.GetDrawing(globe)))throw new Exception("Sky still draws labels");
   preview.SmokeGestures();
  }
  void SmokeCaptureSky(string output){
   double width=Window.Width,height=Window.Height;int scale=settings.TextScalePercent,page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string theme=settings.ThemeMode;int cases=0;
   var frame=new Frame{Target="M45",ObservedUtc="2026-10-07T23:00:00Z",Latitude=51.5,Longitude=0};var rgb=new byte[720*1280*3];var colours=new[]{new byte[]{220,40,40},new byte[]{40,220,40},new byte[]{40,40,220},new byte[]{220,220,40}};
   for(int y=0;y<1280;y++)for(int x=0;x<720;x++)Array.Copy(colours[(y<640?0:2)+(x<360?0:1)],0,rgb,(y*720+x)*3,3);
   var image=BitmapSource.Create(720,1280,96,96,PixelFormats.Rgb24,null,rgb,720*3);image.Freeze();
   try{
    foreach(string prefix in new[]{"","Edited"}){
     frame.ObservedUtc="2026-10-07T23:00:00Z";frame.RA=null;frame.Dec=null;frame.Latitude=51.5;frame.Longitude=0;
     var preview=prefix.Length==0?previewViewport:editedPreviewViewport;var host=(Grid)Window.FindName(prefix+"PreviewHost");var stage=(Grid)Window.FindName(prefix+"PreviewStage");var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");var popup=(Popup)Window.FindName(prefix+"PreviewDetailsPopup");var header=(Grid)Window.FindName(prefix+"PreviewHeader");var original=((Image)Window.FindName(prefix+"PreviewImage")).Source as BitmapSource;
     try{
      GoToPage(prefix.Length==0?0:3);
      foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
       settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();Window.Width=1180;Window.Height=1000;UpdateCaptureSky(prefix,frame);L(prefix+"PreviewMessage").Visibility=Visibility.Collapsed;preview.SetImage(image,true);PumpPopupLayout();preview.Resize();PumpPopupLayout();CheckSkyFit(prefix,preview);
       var imageArea=stage.TransformToAncestor(host).TransformBounds(new Rect(stage.RenderSize));var full=CaptureSidebar(host,Path.Combine(output,"AstroArchive_Capture_Sky_"+prefix+mode+"_"+textScale+".png"));var pixels=new byte[full.PixelWidth*full.PixelHeight*4];full.CopyPixels(pixels,full.PixelWidth*4,0);
       for(int quadrant=0;quadrant<4;quadrant++){int x=(int)(imageArea.X+imageArea.Width*(quadrant%2==0?0.2:0.8)),y=(int)(imageArea.Y+imageArea.Height*(quadrant<2?0.2:0.8)),offset=(y*full.PixelWidth+x)*4;var expected=colours[quadrant];if(Math.Abs(pixels[offset+2]-expected[0])>12||Math.Abs(pixels[offset+1]-expected[1])>12||Math.Abs(pixels[offset]-expected[2])>12||pixels[offset+3]<250)throw new Exception("Sidebar image quadrant clipped: "+prefix+mode+textScale+" quadrant "+quadrant);}
       if(!globe.Context.HasHorizon)throw new Exception("Capture sky did not use frame time/site: "+prefix+mode+textScale+"; "+globe.Context.Evidence);int builds=globe.DrawingBuilds;globe.InvalidateVisual();PumpPopupLayout();if(globe.DrawingBuilds!=builds)throw new Exception("Unchanged sky rebuilt cached drawing");
       foreach(string control in new[]{prefix+"PreviewDetailsButton",prefix+"OpenPreviewButton",prefix.Length==0?"StretchMode":"EditedStretchMode"}){var item=(FrameworkElement)Window.FindName(control);var bounds=item.TransformToAncestor(header).TransformBounds(new Rect(item.RenderSize));if(bounds.Right>header.ActualWidth+1||bounds.Left<0||string.IsNullOrEmpty(AutomationProperties.GetName(item)))throw new Exception("Preview header control clipped or unnamed: "+control);}
       if(object.Equals(B(prefix+"OpenPreviewButton").Content,"Open image…"))throw new Exception("Open image button is not an icon");
       L(prefix+"PreviewInfo").Text="720 × 1280 pixels";B(prefix+"PreviewDetailsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();if(!popup.IsOpen||!L(prefix+"PreviewInfo").IsVisible)throw new Exception("Details icon did not expose capture information");CheckSkyFit(prefix,preview);SavePopup((FrameworkElement)popup.Child,Path.Combine(output,"AstroArchive_Capture_Details_"+prefix+mode+textScale+".png"));
       popup.Child.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual((Visual)popup.Child),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});PumpPopupLayout();if(popup.IsOpen)throw new Exception("Escape did not close capture details");
       Window.Height=650;PumpPopupLayout();preview.Resize();PumpPopupLayout();CheckSkyFit(prefix,preview);if(globe.Visibility!=Visibility.Collapsed)throw new Exception("Height-limited image still reserves a sky map");cases+=2;
      }
      Window.Height=1000;PumpPopupLayout();preview.SetImage(image,true);PumpPopupLayout();double first=globe.ActualHeight;preview.SetImage(BitmapSource.Create(1200,1280,96,96,PixelFormats.Rgb24,null,new byte[1200*1280*3],1200*3),true);PumpPopupLayout();if(globe.ActualHeight<=first)throw new Exception("Sky did not grow when image aspect left more space");
      frame.Dec=-60;frame.RA=160;frame.Latitude=-33.9;frame.Longitude=151.2;UpdateCaptureSky(prefix,frame);PumpPopupLayout();if(!globe.Context.Evidence.Contains("Southern celestial sky"))throw new Exception("Southern capture hemisphere lost");frame.ObservedUtc=null;UpdateCaptureSky(prefix,frame);if(globe.Context.HasHorizon)throw new Exception("Missing capture clock produced a horizon");
     }finally{popup.IsOpen=false;preview.SetImage(original,true);UpdateCaptureSky(prefix,null);}
    }
   }finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();Window.Width=width;Window.Height=height;GoToPage(page);UpdateCaptureSky("",previewFrame);PumpPopupLayout();}
   File.WriteAllText(Path.Combine(output,"capture-sky-smoke.txt"),"PASS: "+cases+" Repository/Edited layouts in light/dark and 100/150% text; maximum image fit, adaptive/hidden sky, image quadrant pixels, text-free cached drawing, header icons, details popup/Escape, aspect changes and hemisphere/clock fallback.");
  }
 }
}
