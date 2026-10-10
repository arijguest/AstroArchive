using System;
using System.IO;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  Frame previewSkyFrame;
  void InitializeSkyPreview(string prefix){
   InitializePreviewHeader(prefix);
   var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");var reset=B(prefix+"PreviewSkyResetButton");reset.Click+=(s,e)=>globe.ResetView();globe.SizeChanged+=(s,e)=>PositionSkyReset(prefix);UiHelp.Hint(reset,"Reset sky view.");
   UpdateCaptureSky(prefix,null);
  }
  void UpdateCaptureSky(string prefix,Frame frame){
   if(prefix.Length==0)previewSkyFrame=frame;
   var globe=Window.FindName(prefix+"PreviewSky") as SkyGlobeView;if(globe==null)return;var sky=CaptureSky.Resolve(frame,settings);globe.SetContext(sky);
   var viewport=prefix.Length==0?previewViewport:editedPreviewViewport;if(viewport!=null)viewport.SetRemainderEnabled(!CaptureSky.IsCalibration(frame)&&!sky.BelowHorizon);PositionSkyReset(prefix);
  }
  void RefreshSkyPreviews(){UpdateCaptureSky("",previewSkyFrame);}
  void PositionSkyReset(string prefix){
   var panel=(Grid)Window.FindName(prefix+"PreviewSkyPanel");var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");var button=B(prefix+"PreviewSkyResetButton");
   if(globe.ActualWidth<=0||globe.ActualHeight<=0)return;
   var home=globe.HomeBounds;var anchor=globe.TranslatePoint(home.BottomLeft,panel);
   double size=Math.Max(22,Math.Min(40,home.Width*0.11));
   // Scale the whole control, including its vector icon and hit area, without
   // affecting the space allocated to the image or globe. Camera zoom leaves
   // this lower-left anchor unchanged so reset is always easy to find.
   var transform=new TransformGroup();transform.Children.Add(new ScaleTransform(size/button.Width,size/button.Height));
   transform.Children.Add(new TranslateTransform(anchor.X,anchor.Y-size));button.RenderTransform=transform;
  }
  static Frame ReadSkyFrame(string path,string fallbackTarget=null){
   try{var info=Assets.Inspect(path);return CaptureSky.FromHeader(info.Header,info.Format,Path.GetFileName(path),fallbackTarget);}
   catch(Exception error){if(!(error is IOException||error is UnauthorizedAccessException||error is NotSupportedException||error is ArgumentException||error is OverflowException))throw;
    return new Frame{OriginalName=Path.GetFileName(path),Target=fallbackTarget??Catalog.TargetFromFilename(path)};
   }
  }
 }
}
