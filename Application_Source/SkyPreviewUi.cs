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
   double diameter=Math.Max(0,Math.Min(globe.ActualWidth,globe.ActualHeight)-28);
   button.RenderTransform=new TranslateTransform(Math.Max(4,(panel.ActualWidth-diameter)/2),Math.Max(4,(panel.ActualHeight+diameter)/2-button.Height));
  }
  static Frame ReadSkyFrame(string path,string fallbackTarget=null){
   try{var info=Assets.Inspect(path);return CaptureSky.FromHeader(info.Header,info.Format,Path.GetFileName(path),fallbackTarget);}
   catch(Exception error){if(!(error is IOException||error is UnauthorizedAccessException||error is NotSupportedException||error is ArgumentException||error is OverflowException))throw;
    return new Frame{OriginalName=Path.GetFileName(path),Target=fallbackTarget??Catalog.TargetFromFilename(path)};
   }
  }
 }
}
