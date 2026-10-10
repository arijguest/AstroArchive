#nullable enable
using SkiaSharp;
using System.Diagnostics;
using System.Globalization;
namespace AstroArchive;

public sealed class LinuxAnimationOptions
{
    public double SecondsPerChart { get; set; }=4;
    public int FramesPerSecond { get; set; }=24;
    public int MaximumEdge { get; set; }=960;
    public string Transition { get; set; }="Glide";
}
public static class LinuxAnalyticsAnimation
{
    public static (int Width,int Height) Dimensions(AnalyticsPage page,int maximumEdge)
    {
        if(maximumEdge<32||maximumEdge>3840)throw new ArgumentException("Animation resolution must be between 32 and 3840 pixels.");
        int w=(int)page.CanvasWidth,h=(int)page.CanvasHeight,a=w,b=h;
        while(b!=0){int next=a%b;a=b;b=next;}
        int uw=w/a,uh=h/a,multiple=Math.Min(a,maximumEdge/Math.Max(uw,uh));
        if((uw%2!=0||uh%2!=0)&&multiple%2!=0)multiple--;
        if(multiple<2)throw new ArgumentException("Animation resolution is too small for this layout.");
        return (uw*multiple,uh*multiple);
    }
    private static double Ease(double t) { t=Math.Clamp(t,0,1);return t*t*(3-2*t); }
    public static SKBitmap Frame(IList<AnalyticsPage> pages,byte[] logo,LinuxAnimationOptions options,double seconds)
    {
        var size=Dimensions(pages[0],options.MaximumEdge);using var brand=SKBitmap.Decode(logo)??throw new InvalidDataException("Cannot decode analytics logo.");
        return Render(pages,brand,options,seconds,size.Width,size.Height);
    }
    private static SKBitmap Render(IList<AnalyticsPage> pages,SKBitmap logo,LinuxAnimationOptions options,double seconds,int width,int height)
    {
        var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);
        canvas.Scale((float)(width/pages[0].CanvasWidth),(float)(height/pages[0].CanvasHeight));canvas.Clear(SKColor.Parse(pages[0].Background));
        int scene=Math.Clamp((int)(seconds/options.SecondsPerChart),0,pages.Count-1);double local=seconds-scene*options.SecondsPerChart,mix=Ease(local/Math.Min(.65,options.SecondsPerChart*.23));
        void Draw(int index,double alpha,double reveal,double offset,double scale) {
            using var layer=new SKPaint{Color=SKColors.White.WithAlpha((byte)(255*alpha))};canvas.SaveLayer(layer);canvas.Translate((float)offset,0);
            canvas.Scale((float)scale,(float)scale,(float)(pages[0].CanvasWidth/2),(float)(pages[0].CanvasHeight/2));LinuxAnalyticsExport.Draw(canvas,pages[index],logo,reveal);canvas.Restore();
        }
        bool glide=options.Transition=="Glide",zoom=options.Transition=="Zoom";
        if(scene>0&&mix<1)Draw(scene-1,1,1,glide?-pages[0].CanvasWidth*.075*mix:0,zoom?1+.025*mix:1);
        Draw(scene,scene==0?1:mix,Ease(local/Math.Min(1.1,options.SecondsPerChart*.6)),glide&&scene>0?pages[0].CanvasWidth*.075*(1-mix):0,zoom?1.025-.025*mix:1);
        return bitmap;
    }
    public static void Save(string destination,IList<AnalyticsPage> pages,byte[] logo,string format,LinuxAnimationOptions options,CancellationToken ct,Action<int,string>? progress=null)
    {
        if(format is not ("GIF" or "MP4")||pages.Count==0||!double.IsFinite(options.SecondsPerChart)||options.SecondsPerChart<.1||options.SecondsPerChart>60||options.FramesPerSecond<1||options.FramesPerSecond>60||options.Transition is not ("Fade" or "Glide" or "Zoom"))throw new ArgumentException("Invalid animation settings.");
        if(pages.Any(p=>p.CanvasWidth!=pages[0].CanvasWidth||p.CanvasHeight!=pages[0].CanvasHeight))throw new ArgumentException("Animation scenes must share a layout.");
        var size=Dimensions(pages[0],options.MaximumEdge);int count=checked((int)Math.Ceiling(pages.Count*options.SecondsPerChart*options.FramesPerSecond));
        string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp."+format.ToLowerInvariant();using var brand=SKBitmap.Decode(logo)??throw new InvalidDataException("Cannot decode analytics logo.");
        Process? encoder=null;
        try {
            ct.ThrowIfCancellationRequested();
            if(format=="GIF") {
                IEnumerable<byte[]> Samples() { var small=Dimensions(pages[0],Math.Min(options.MaximumEdge,320));foreach(var page in pages) { ct.ThrowIfCancellationRequested();using var sample=Render([page],brand,options,options.SecondsPerChart*.9,small.Width,small.Height);yield return sample.Bytes; } }
                using var gif=new AnalyticsGif(temporary,size.Width,size.Height,AnalyticsGif.Palette(Samples()));
                for(int i=0;i<count;i++) { ct.ThrowIfCancellationRequested();using var frame=Render(pages,brand,options,i/(double)options.FramesPerSecond,size.Width,size.Height);int delay=(int)Math.Round((i+1)*100.0/options.FramesPerSecond)-(int)Math.Round(i*100.0/options.FramesPerSecond);gif.Add(frame.Bytes,delay);progress?.Invoke((i+1)*98/count,"Rendering GIF frame "+(i+1)+" / "+count); }
                gif.Complete();
            } else {
                const string ffmpeg="/usr/bin/ffmpeg";if(!File.Exists(ffmpeg))throw new IOException("Install ffmpeg to export H.264 MP4 stories. GIF and document exports need no external encoder.");
                var start=new ProcessStartInfo(ffmpeg){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardError=true};
                foreach(string arg in new[]{"-nostdin","-v","error","-f","rawvideo","-pix_fmt","bgra","-video_size",size.Width+"x"+size.Height,"-framerate",options.FramesPerSecond.ToString(CultureInfo.InvariantCulture),"-i","pipe:0","-an","-c:v","libx264","-threads","2","-preset","veryfast","-crf","20","-pix_fmt","yuv420p","-movflags","+faststart",temporary})start.ArgumentList.Add(arg);
                encoder=Process.Start(start)!;var error=encoder.StandardError.ReadToEndAsync();
                using var cancellation=ct.Register(()=>{try{if(!encoder.HasExited)encoder.Kill(true);}catch{} });
                for(int i=0;i<count;i++) { ct.ThrowIfCancellationRequested();using var frame=Render(pages,brand,options,i/(double)options.FramesPerSecond,size.Width,size.Height);encoder.StandardInput.BaseStream.WriteAsync(frame.Bytes,ct).AsTask().GetAwaiter().GetResult();progress?.Invoke((i+1)*98/count,"Rendering MP4 frame "+(i+1)+" / "+count); }
                encoder.StandardInput.Close();if(!encoder.WaitForExit(120000)){encoder.Kill(true);throw new IOException("MP4 finalization exceeded two minutes.");}ct.ThrowIfCancellationRequested();
                if(encoder.ExitCode!=0)throw new IOException("FFmpeg could not encode the MP4 story: "+error.GetAwaiter().GetResult());
            }
            ct.ThrowIfCancellationRequested();File.Move(temporary,destination);progress?.Invoke(100,"Animation export complete");
        } catch { ct.ThrowIfCancellationRequested();throw; }
        finally { if(encoder!=null){if(!encoder.HasExited)encoder.Kill(true);encoder.WaitForExit();encoder.Dispose();}if(File.Exists(temporary))File.Delete(temporary); }
    }
}
