using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  readonly PreviewCache previewCache=new PreviewCache();readonly SemaphoreSlim previewDecodeGate=new SemaphoreSlim(1,1);
  async Task<PreviewData> LoadPreviewSamples(string path,Frame frame,Frame sky,string fallbackTarget,CancellationToken token,Func<bool> current,Action<Frame> showSky,Func<string,CancellationToken,Frame,PreviewData> decode=null){
   // Coalesce rapid selections before starting expensive I/O. Sky/geometry are
   // already updated synchronously, and cancellation removes queued requests.
   await Task.Delay(35,token);await previewDecodeGate.WaitAsync(token);
   try{return await Task.Run(()=>{
    token.ThrowIfCancellationRequested();if(sky==null){var metadata=ReadSkyFrame(path,fallbackTarget);Window.Dispatcher.BeginInvoke(DispatcherPriority.Normal,new Action(()=>{if(current()&&!token.IsCancellationRequested)showSky(metadata);}));}
    var data=previewCache.Get(path,frame,()=> (decode??DecodePreview)(path,token,frame),token);data.ApplyContext(frame,path);return data;
   },token);}finally{previewDecodeGate.Release();}
  }
  static BitmapSource RenderPreviewBitmap(PreviewData data,string mode,CancellationToken token){
   var rgb=data.Render(mode,token);token.ThrowIfCancellationRequested();var bitmap=BitmapSource.Create(data.Width,data.Height,96,96,PixelFormats.Rgb24,null,rgb,data.Width*3);bitmap.Freeze();return bitmap;
  }
  async Task RenderProgressivePreview(PreviewData data,string mode,bool first,CancellationToken token,Func<bool> current,Action<BitmapSource,bool> show){
   bool progressive=first&&mode!="Linear"&&!data.SkipStretch;
   var bitmap=await Task.Run(()=>RenderPreviewBitmap(data,progressive?"Linear":mode,token),token);
   if(!current()||token.IsCancellationRequested)return;show(bitmap,progressive);
   if(!progressive)return;
   // Give WPF a chance to paint the unstretched image before processing it.
   await Window.Dispatcher.InvokeAsync(new Action(()=>{}),DispatcherPriority.ApplicationIdle,token);
   bitmap=await Task.Run(()=>RenderPreviewBitmap(data,mode,token),token);
   if(current()&&!token.IsCancellationRequested)show(bitmap,false);
  }
 }
}
