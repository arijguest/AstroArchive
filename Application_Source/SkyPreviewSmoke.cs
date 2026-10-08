using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 public partial class MainUi {
  void SmokeCaptureSky(string output){
   double width=Window.Width,height=Window.Height;int scale=settings.TextScalePercent;string theme=settings.ThemeMode;var original=((Image)Window.FindName("PreviewImage")).Source as BitmapSource;
   var stage=(Grid)Window.FindName("PreviewStage");var host=(Grid)Window.FindName("PreviewHost");var footer=(FrameworkElement)Window.FindName("PreviewFooter");var globe=(SkyGlobeView)Window.FindName("PreviewSky");var details=(Expander)Window.FindName("PreviewDetails");
   var frame=new Frame{Target="M45",ObservedUtc="2026-10-07T23:00:00Z",Latitude=51.5,Longitude=0};var rgb=new byte[720*1280*3];var colours=new[]{new byte[]{220,40,40},new byte[]{40,220,40},new byte[]{40,40,220},new byte[]{220,220,40}};
   for(int y=0;y<1280;y++)for(int x=0;x<720;x++)Array.Copy(colours[(y<640?0:2)+(x<360?0:1)],0,rgb,(y*720+x)*3,3);
   var image=BitmapSource.Create(720,1280,96,96,PixelFormats.Rgb24,null,rgb,720*3);image.Freeze();int cases=0;
   try{
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();Window.Width=1180;Window.Height=textScale==150?1000:780;UpdateCaptureSky("",frame);L("PreviewMessage").Visibility=Visibility.Collapsed;previewViewport.SetImage(image,true);PumpPopupLayout();previewViewport.Resize();PumpPopupLayout();
     var imageArea=stage.TransformToAncestor(host).TransformBounds(new Rect(stage.RenderSize));var lower=footer.TransformToAncestor(host).TransformBounds(new Rect(footer.RenderSize));
     if(imageArea.Width<50||imageArea.Height<80||lower.Top<imageArea.Bottom+PreviewViewport.ToolbarSpace-0.5||lower.Top-imageArea.Bottom-PreviewViewport.ToolbarSpace>6||lower.Bottom>host.ActualHeight+1)throw new Exception("Compact preview footer overlaps/clips image or leaves a gap: "+imageArea+", "+lower+", host "+host.RenderSize);
     // Verify the bitmap through the real sidebar with the globe present.
     var full=new RenderTargetBitmap((int)Math.Ceiling(host.ActualWidth),(int)Math.Ceiling(host.ActualHeight),96,96,PixelFormats.Pbgra32);full.Render(host);var pixels=new byte[full.PixelWidth*full.PixelHeight*4];full.CopyPixels(pixels,full.PixelWidth*4,0);
     for(int quadrant=0;quadrant<4;quadrant++){int x=(int)(imageArea.X+imageArea.Width*(quadrant%2==0?0.2:0.8)),y=(int)(imageArea.Y+imageArea.Height*(quadrant<2?0.2:0.8)),offset=(y*full.PixelWidth+x)*4;var expected=colours[quadrant];if(Math.Abs(pixels[offset+2]-expected[0])>12||Math.Abs(pixels[offset+1]-expected[1])>12||Math.Abs(pixels[offset]-expected[2])>12||pixels[offset+3]<250)throw new Exception("Sidebar sky layout clipped preview quadrant "+quadrant);}
     if(!globe.Context.HasHorizon||globe.ActualHeight<90||globe.ActualWidth<100)throw new Exception("Capture sky did not render with frame time and site");
     SavePopup(host,Path.Combine(output,"AstroArchive_Capture_Sky_"+mode+"_"+textScale+".png"));int builds=globe.DrawingBuilds;globe.InvalidateVisual();PumpPopupLayout();if(globe.DrawingBuilds!=builds)throw new Exception("Unchanged sky rebuilt its cached drawing");
     details.IsExpanded=true;PumpPopupLayout();previewViewport.Resize();PumpPopupLayout();lower=footer.TransformToAncestor(host).TransformBounds(new Rect(footer.RenderSize));if(lower.Bottom>host.ActualHeight+1)throw new Exception("Expanded capture details pushed sky outside preview");details.IsExpanded=false;cases++;
    }
    frame.Dec=-60;frame.RA=160;frame.Latitude=-33.9;frame.Longitude=151.2;UpdateCaptureSky("",frame);PumpPopupLayout();if(!globe.Context.Evidence.Contains("Southern celestial sky"))throw new Exception("Southern capture hemisphere lost");SavePopup(host,Path.Combine(output,"AstroArchive_Capture_Sky_South.png"));
    frame.ObservedUtc=null;UpdateCaptureSky("",frame);PumpPopupLayout();if(globe.Context.HasHorizon||!globe.Context.Summary.Contains("capture time unknown"))throw new Exception("Missing capture clock produced a compass horizon");
   }finally{details.IsExpanded=false;settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();Window.Width=width;Window.Height=height;previewViewport.SetImage(original,true);UpdateCaptureSky("",previewFrame);PumpPopupLayout();}
   File.WriteAllText(Path.Combine(output,"capture-sky-smoke.txt"),"PASS: "+cases+" real sidebar image/sky layouts in light/dark and 100/150% text; compact footer bounds, expanded details, full-image quadrant pixels, cached redraws, northern/southern captures and unknown-clock fallback.");
  }
 }
}
