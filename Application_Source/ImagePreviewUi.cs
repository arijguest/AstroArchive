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
  CancellationTokenSource detachedPreviewCancel;int detachedPreviewGeneration;
  void CancelDetachedPreview(){detachedPreviewGeneration++;if(detachedPreviewCancel!=null){detachedPreviewCancel.Cancel();detachedPreviewCancel=null;}}
  async void PreviewImage(Frame frame){
   if(repo==null||closing||metadataPreviewSuspended||frame==null)return;CancelDetachedPreview();var repository=repo;var capture=frame.Clone();string path=repository.FilePath(capture),mode=ScientificPreview(path)?settings.PreviewStretch??"Auto per channel":"Linear";bool motion=MediaFiles.Motion(path);
   var cancellation=detachedPreviewCancel=new CancellationTokenSource();var token=cancellation.Token;int generation=detachedPreviewGeneration;var entry=AddActivity("Loading image preview",true);entry.Status=capture.OriginalName;entry.Cancel=()=>cancellation.Cancel();PreviewData pixels=null;byte[] rendered=null;
   try{
    await previewDecodeGate.WaitAsync(token);
    try{await Task.Run(()=>{
     // Preview verification must not save cached stamps while an edit or
     // deletion owns the capture. Changed or moving files fail safely here.
     token.ThrowIfCancellationRequested();var before=FileStamp.Read(path);
     if(!before.VerifiedUnchanged(capture.RepositoryStamp)&&Util.Hash(path,token)!=capture.Hash)throw new InvalidDataException("The archived file changed. Refresh before previewing it.");
     if(!motion){pixels=DecodeFullPreview(path,token,capture);pixels.ApplyContext(capture,path);rendered=pixels.Render(mode,token);}
     token.ThrowIfCancellationRequested();if(!before.ContentSame(FileStamp.Read(path)))throw new IOException("The file changed while its preview was loading. Select it again after the current operation.");
    },token);}finally{previewDecodeGate.Release();}
    if(generation!=detachedPreviewGeneration||closing||repo!=repository||Window.Dispatcher.HasShutdownStarted)return;
    var popup=motion?new ImagePreviewWindow(Window,capture.OriginalName,path):new ImagePreviewWindow(Window,capture.OriginalName,pixels.Width,pixels.Height,rendered);
    if(!motion){bool flip=TargetOverlay.DisplayFlip(pixels.FlipY,capture.Sky,capture.Format);AttachTargets(popup,()=>TargetOverlay.For(capture,flip));}popup.Show();activities.Remove(entry);
   }catch(OperationCanceledException){entry.Canceled=true;entry.Status="Preview canceled.";}
   catch(Exception error){entry.Failed=true;entry.NeedsReview=true;entry.Status="Preview unavailable.";entry.Report=error.Message;}
   finally{entry.Running=false;entry.Cancel=null;if(ReferenceEquals(detachedPreviewCancel,cancellation))detachedPreviewCancel=null;cancellation.Dispose();if(!closing&&!Window.Dispatcher.HasShutdownStarted){if(entry.Failed)NotifyActivity(entry);else{activities.Remove(entry);RenderActivity();}}}
  }
 }
 public class ImagePreviewWindow:Window {
  readonly PreviewViewport preview;MotionPreview motion;
  public ImagePreviewWindow(Window owner,string filename,string path):this(owner,filename,480,640,new byte[480*640*3]){
   var host=(Grid)Content;var message=new TextBlock{Text="Loading preview…",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12)};host.Children.Add(message);
   Loaded+=(s,e)=>{motion=new MotionPreview(preview,path,info=>message.Visibility=Visibility.Collapsed,error=>{message.Text=error;message.Visibility=Visibility.Visible;},true);motion.Start();};Closed+=(s,e)=>{if(motion!=null)motion.Dispose();};
  }
  public ImagePreviewWindow(Window owner,string filename,int width,int height,byte[] pixels){
   Owner=owner;Icon=ApplicationIcon.Image;Title="Preview - "+filename;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;FontFamily=owner.FontFamily;FontSize=13;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,Control.BackgroundProperty,"Canvas");Theme.Bind(this,Control.ForegroundProperty,"Text");
   // Reserve the native window chrome, then size the client area to the portrait image.
   var geometry=new PreviewGeometry(width,height,0);var work=SystemParameters.WorkArea;double clientWidth,clientHeight;geometry.Frame(Math.Min(1100,work.Width-64),Math.Max(1,Math.Min(760,work.Height-100)-PreviewViewport.ToolbarSpace),out clientWidth,out clientHeight);
   Width=clientWidth+2*SystemParameters.ResizeFrameVerticalBorderWidth+16;Height=clientHeight+PreviewViewport.ToolbarSpace+SystemParameters.CaptionHeight+2*SystemParameters.ResizeFrameHorizontalBorderHeight+16;MinWidth=Math.Min(Width,260);MinHeight=Math.Min(Height,360);
   var host=new Grid{Margin=new Thickness(8)};var viewport=new Grid();var image=new Image();viewport.Children.Add(image);host.Children.Add(viewport);Content=host;preview=new PreviewViewport(host,viewport,image,null,false,true);
   var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,pixels,width*3);bitmap.Freeze();preview.SetImage(bitmap,true);
   Loaded+=(s,e)=>preview.Resize();PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.F){preview.Fit();e.Handled=true;}};
  }
  Func<TargetOverlayResult> targetOverlay;Func<bool> targetsOn;
  // The overlay is computed by the owner, which also owns the shared on/off setting.
  public void EnableTargets(Func<TargetOverlayResult> overlay,Func<bool> on,Action toggle){targetOverlay=overlay;targetsOn=on;preview.ShowTargetToggle(()=>{toggle();RefreshTargets();});RefreshTargets();}
  public void RefreshTargets(){if(targetOverlay==null)return;bool on=targetsOn();preview.SetTargetToggle(on);var result=on?targetOverlay():null;if(result==null)preview.SetTargets(null,null);else preview.SetTargets(result.Marks,result.Note);}
  public PreviewViewport Viewport{get{return preview;}}
  public void SmokeGestures(){preview.SmokeGestures();preview.SmokeRotation();}
 }
}
