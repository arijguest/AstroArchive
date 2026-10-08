using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  Frame previewSkyFrame,editedSkyFrame;
  void InitializeSkyPreview(string prefix){
   InitializePreviewHeader(prefix);
   var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");var reset=B(prefix+"PreviewSkyResetButton");reset.Click+=(s,e)=>globe.ResetView();UiHelp.Tip(reset,"Restore the capture sky view and zoom. Drag the globe to rotate; scroll or pinch to zoom.");
   UpdateCaptureSky(prefix,null);
  }
  void UpdateCaptureSky(string prefix,Frame frame){
   if(prefix.Length==0)previewSkyFrame=frame;else editedSkyFrame=frame;
   var globe=Window.FindName(prefix+"PreviewSky") as SkyGlobeView;if(globe==null)return;var sky=CaptureSky.Resolve(frame,settings);globe.SetContext(sky);
  }
  void RefreshSkyPreviews(){UpdateCaptureSky("",previewSkyFrame);UpdateCaptureSky("Edited",editedSkyFrame);}
  static Frame ReadSkyFrame(string path,string fallbackTarget=null){
   try{var info=Assets.Inspect(path);return CaptureSky.FromHeader(info.Header,info.Format,Path.GetFileName(path),fallbackTarget);}
   catch(Exception error){if(!(error is IOException||error is UnauthorizedAccessException||error is NotSupportedException||error is ArgumentException||error is OverflowException))throw;
    return new Frame{OriginalName=Path.GetFileName(path),Target=fallbackTarget??Catalog.TargetFromFilename(path)};
   }
  }
 }
}
