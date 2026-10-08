using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  Frame previewSkyFrame,editedSkyFrame;
  void InitializeSkyPreview(string prefix){
   var host=(Grid)Window.FindName(prefix+"PreviewHost");var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");
   host.SizeChanged+=(s,e)=>{double height=Math.Max(96,Math.Min(180,host.ActualHeight*0.23));if(Math.Abs(globe.Height-height)>0.5)globe.Height=height;};
   UpdateCaptureSky(prefix,null);
  }
  void UpdateCaptureSky(string prefix,Frame frame){
   if(prefix.Length==0)previewSkyFrame=frame;else editedSkyFrame=frame;
   var globe=Window.FindName(prefix+"PreviewSky") as SkyGlobeView;if(globe==null)return;var sky=CaptureSky.Resolve(frame,settings);globe.SetContext(sky);
   L(prefix+"PreviewSkyTitle").Text=frame==null?"CAPTURE SKY":"SKY · "+sky.TargetLabel;L(prefix+"PreviewSkyTime").Text=sky.TimeLabel;L(prefix+"PreviewSkySummary").Text=sky.Summary;
   foreach(string suffix in new[]{"Title","Time","Summary"})L(prefix+"PreviewSky"+suffix).ToolTip=sky.Evidence;
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
