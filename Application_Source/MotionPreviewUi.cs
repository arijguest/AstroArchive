using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace AstroArchive {
 // Original media remains read-only. One composed GIF/SER frame is held at a time.
 public sealed class MotionPreview:IDisposable {
  readonly PreviewViewport viewport;readonly Action<string> ready,error;readonly string path;readonly DispatcherTimer timer=new DispatcherTimer();readonly CancellationTokenSource cancel=new CancellationTokenSource();
  Stream stream;BitmapDecoder gif;byte[] canvas,restore;int width,height,index,disposal,left,top,frameWidth,frameHeight;Frame ser;MediaElement video;bool disposed,playing=true,decoding;
  public bool Playing{get{return playing;}}
  public MotionPreview(PreviewViewport viewport,string path,Action<string> ready,Action<string> error){this.viewport=viewport;this.path=path;this.ready=ready;this.error=error;timer.Tick+=(s,e)=>Next();}
  public void Start(){try{
   if(MediaFiles.Gif(path)){
    var info=RasterHeaders.Inspect(path);width=info.Header.Width;height=info.Header.Height;canvas=new byte[checked(width*height*4)];
    stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);gif=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand);
    if(gif.Frames.Count>4096)throw new NotSupportedException("GIF exceeds the 4,096-frame preview limit.");Next();
   }else if(Assets.Extension(path)==".ser"){
    var info=Assets.Inspect(path);ser=new Frame{Format=info.Format,Images=info.Images,ImageKey=info.Images[0].Key,Bayer=info.Header.Get("BAYERPAT")};timer.Interval=TimeSpan.FromMilliseconds(100);Next();
   }else{
    video=new MediaElement{LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Manual,ScrubbingEnabled=true,Volume=0};
    video.MediaOpened+=(s,e)=>{if(disposed)return;viewport.SetMedia(video,Math.Max(1,video.NaturalVideoWidth),Math.Max(1,video.NaturalVideoHeight));viewport.SetPlayback(Toggle,playing);ready(video.NaturalVideoWidth+" × "+video.NaturalVideoHeight+" · video");if(!playing)video.Pause();};
    video.MediaFailed+=(s,e)=>Fail("Windows could not play this video. Its codec may be unavailable. "+e.ErrorException.Message);
    video.MediaEnded+=(s,e)=>{if(disposed)return;playing=false;viewport.SetPlayback(Toggle,false);};
    // A visual parent is required before the Windows media pipeline can open.
    viewport.SetMedia(video,640,480);viewport.SetPlayback(Toggle,true);video.Source=new Uri(Path.GetFullPath(path));video.Play();
   }
  }catch(Exception e){Fail(e.Message);}}
  static int Query(BitmapFrame frame,string query,int fallback){try{var metadata=frame.Metadata as BitmapMetadata;object value=metadata==null?null:metadata.GetQuery(query);return value==null?fallback:Convert.ToInt32(value);}catch(NotSupportedException){return fallback;}catch(ArgumentException){return fallback;}catch(InvalidOperationException){return fallback;}}
  void DrawGif(){
   if(index==0)Array.Clear(canvas,0,canvas.Length);
   else if(disposal==2){for(int y=top;y<Math.Min(height,top+frameHeight);y++)Array.Clear(canvas,(y*width+left)*4,Math.Min(frameWidth,width-left)*4);}
   else if(disposal==3&&restore!=null)Buffer.BlockCopy(restore,0,canvas,0,canvas.Length);
   var frame=gif.Frames[index];left=Query(frame,"/imgdesc/Left",0);top=Query(frame,"/imgdesc/Top",0);frameWidth=frame.PixelWidth;frameHeight=frame.PixelHeight;disposal=Query(frame,"/grctlext/Disposal",0);
   if(left<0||top<0||left+frameWidth>width||top+frameHeight>height)throw new InvalidDataException("GIF frame lies outside its canvas.");
   restore=disposal==3?(byte[])canvas.Clone():null;var converted=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);byte[] pixels=new byte[checked(frameWidth*frameHeight*4)];converted.CopyPixels(pixels,frameWidth*4,0);
   for(int y=0;y<frameHeight;y++)for(int x=0;x<frameWidth;x++){int from=(y*frameWidth+x)*4,to=((top+y)*width+left+x)*4;if(pixels[from+3]==0)continue;Buffer.BlockCopy(pixels,from,canvas,to,4);}
   var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,canvas,width*4);bitmap.Freeze();viewport.SetImage(bitmap,index==0&&gif.Frames.Count==1);viewport.SetPlayback(gif.Frames.Count>1?(Action)Toggle:null,playing);
   ready(width+" × "+height+" · GIF · "+gif.Frames.Count+" frames");timer.Interval=TimeSpan.FromMilliseconds(Math.Max(20,Query(frame,"/grctlext/Delay",10)*10));index=(index+1)%gif.Frames.Count;
   if(playing&&gif.Frames.Count>1)timer.Start();
  }
  async void Next(){
   if(disposed||decoding)return;timer.Stop();try{
    if(gif!=null){DrawGif();return;}if(ser==null)return;decoding=true;int current=index;var token=cancel.Token;
    var bitmap=await Task.Run(()=>{var data=Assets.Display(ser,path,current,token);var pixels=data.Render("Linear",token);var image=BitmapSource.Create(data.Width,data.Height,96,96,PixelFormats.Rgb24,null,pixels,data.Width*3);image.Freeze();return image;},token);
    if(disposed)return;viewport.SetImage(bitmap,false);viewport.SetPlayback(Toggle,playing);ready(ser.Images[0].Width+" × "+ser.Images[0].Height+" · SER · frame "+(current+1)+" / "+ser.Images[0].Count+" · 10 fps preview");index=(current+1)%ser.Images[0].Count;if(playing)timer.Start();
   }catch(OperationCanceledException){}catch(Exception e){if(!disposed)Fail(e.Message);}finally{decoding=false;}
  }
  void Toggle(){if(disposed)return;playing=!playing;viewport.SetPlayback(Toggle,playing);if(video!=null){if(playing){if(video.NaturalDuration.HasTimeSpan&&video.Position>=video.NaturalDuration.TimeSpan)video.Position=TimeSpan.Zero;video.Play();}else video.Pause();}else if(playing)timer.Start();else timer.Stop();}
  void Fail(string message){if(disposed)return;Dispose();viewport.SetImage(null,true);error("Preview unavailable\n\n"+message);}
  public void Pause(){if(playing&&!disposed)Toggle();}
  public void Dispose(){if(disposed)return;disposed=true;timer.Stop();cancel.Cancel();if(video!=null){video.Close();video.Source=null;}if(stream!=null)stream.Dispose();viewport.SetPlayback(null,false);canvas=null;restore=null;gif=null;}
 }
}
