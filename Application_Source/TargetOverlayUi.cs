using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace AstroArchive {
 public partial class MainUi {
  // One setting drives every preview: switching the overlay off anywhere switches it off everywhere until it is switched back on.
  string targetOverlayKey;TargetOverlayResult targetOverlay;
  readonly Dictionary<string,SkyGeometry> editedSkies=new Dictionary<string,SkyGeometry>(StringComparer.OrdinalIgnoreCase);string editedSkyPending;
  readonly List<ImagePreviewWindow> targetWindows=new List<ImagePreviewWindow>();
  void InitializeTargetOverlay(){
   B("TargetOverlayButton").Click+=(s,e)=>SetTargetOverlay(!settings.ShowTargetOverlay);
   var edited=B("EditedTargetOverlayButton");if(edited!=null)edited.Click+=(s,e)=>SetTargetOverlay(!settings.ShowTargetOverlay);
   RefreshTargetOverlays();
  }
  void SetTargetOverlay(bool on){if(settings.ShowTargetOverlay==on)return;settings.ShowTargetOverlay=on;SaveSettings();RefreshTargetOverlays();}
  void RefreshTargetOverlays(bool reloadSky=false){
   if(reloadSky&&repo!=null&&previewFrame!=null&&!string.IsNullOrEmpty(previewFrame.Hash)){try{var stored=repo.Find(previewFrame.Hash);if(stored!=null){previewFrame.Sky=stored.Sky;previewFrame.Target=stored.Target;targetOverlayKey=null;}}catch(Exception){}}
   UpdateTargetOverlay();UpdateEditedTargetOverlay();foreach(var window in targetWindows.ToList())window.RefreshTargets();
  }
  void StyleTargetButton(string name,string icon){
   var button=B(name);if(button==null)return;bool on=settings.ShowTargetOverlay;
   button.ToolTip=Localization.T(TargetOverlay.Tooltip(on));AutomationProperties.SetName(button,Localization.T("Show targets in frame"));AutomationProperties.SetItemStatus(button,Localization.T(on?"On":"Off"));
   var path=(Path)Window.FindName(icon);if(path!=null)path.SetResourceReference(Shape.StrokeProperty,on?"Accent":"Text");
   if(on)button.SetResourceReference(Control.BackgroundProperty,"Selection");else button.ClearValue(Control.BackgroundProperty);
  }
  void UpdateTargetOverlay(){
   StyleTargetButton("TargetOverlayButton","TargetOverlayIcon");if(previewViewport==null)return;
   if(!settings.ShowTargetOverlay||previewFrame==null||previewData==null){previewViewport.SetTargets(null,null);return;}
   bool flip=TargetOverlay.DisplayFlip(previewData.FlipY,previewFrame.Sky,previewFrame.Format);string key=previewFrame.Hash+"|"+flip+"|"+previewFrame.Target+"|"+(previewFrame.Sky==null?"":Util.Serialize(previewFrame.Sky));
   if(targetOverlay==null||key!=targetOverlayKey){targetOverlayKey=key;targetOverlay=TargetOverlay.For(previewFrame,flip);}
   var frame=previewFrame;
   if(targetOverlay.CanSolve&&repo!=null)previewViewport.SetTargets(targetOverlay.Marks,targetOverlay.Note,()=>SolveForTargets(frame),"Plate solve…");else previewViewport.SetTargets(targetOverlay.Marks,targetOverlay.Note);
  }
  // Uses the normal Identify target flow, so the solution is reviewed and stored like any other; the overlay refreshes when it is applied.
  void SolveForTargets(Frame frame){
   if(repo==null||RepositoryOperationBlocked||frame==null)return;
   var live=all.FirstOrDefault(f=>f.Hash==frame.Hash)??frame;
   if(!(live.Kind=="Light"||live.Kind=="Stack"||live.Kind=="Unknown")){LocalizedMessage.Show(Window,"Select a light exposure or telescope stack to identify.","Select a capture");return;}
   IdentifyByPlate(false,new List<Frame>{live},0);
  }
  // Edited images carry no stored solution; a WCS in the file's own FITS or XISF header is read once per file in the background.
  void UpdateEditedTargetOverlay(){
   StyleTargetButton("EditedTargetOverlayButton","EditedTargetOverlayIcon");if(editedPreviewViewport==null)return;
   var image=ActiveEditedImage;if(!settings.ShowTargetOverlay||image==null||repo==null||editedPreviewData==null){editedPreviewViewport.SetTargets(null,null);return;}
   string path;try{path=repo.EditedPath(image.Project,image.RelativePath);}catch(Exception){editedPreviewViewport.SetTargets(null,null);return;}
   SkyGeometry sky;
   if(!editedSkies.TryGetValue(path,out sky)){
    if(editedSkyPending!=path){editedSkyPending=path;Task.Run(()=>HeaderSky(path)).ContinueWith(t=>{editedSkies[path]=t.Status==TaskStatus.RanToCompletion?t.Result:null;if(editedSkyPending==path)editedSkyPending=null;UpdateEditedTargetOverlay();},TaskScheduler.FromCurrentSynchronizationContext());}
    editedPreviewViewport.SetTargets(new List<TargetMark>(),"Reading the image header…");return;
   }
   var result=TargetOverlay.For(sky,image.Metadata==null?null:image.Metadata.Object,TargetOverlay.DisplayFlip(editedPreviewData.FlipY,sky,path.EndsWith(".xisf",StringComparison.OrdinalIgnoreCase)?"XISF":null));
   editedPreviewViewport.SetTargets(result.Marks,sky==null?"This image has no plate solution in its header":result.Note);
  }
  static SkyGeometry HeaderSky(string path){
   if(!ScientificPreview(path))return null;
   var info=Assets.Inspect(path);var header=info==null?null:info.Header;if(header==null)return null;
   var image=info.Images.FirstOrDefault();int width=header.Width>0?header.Width:image==null?0:image.Width,height=header.Height>0?header.Height:image==null?0:image.Height;
   return SkyWcs.FromHeader(header,width,height);
  }
  // Large preview windows share the setting and the overlay of the capture or image they show.
  void AttachTargets(ImagePreviewWindow window,Func<TargetOverlayResult> overlay){
   targetWindows.Add(window);window.Closed+=(s,e)=>targetWindows.Remove(window);
   window.EnableTargets(()=>settings.ShowTargetOverlay?overlay():null,()=>settings.ShowTargetOverlay,()=>SetTargetOverlay(!settings.ShowTargetOverlay));
  }
 }
}
