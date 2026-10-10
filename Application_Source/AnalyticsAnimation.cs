using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 public sealed class AnalyticsVideoOptions {
  public double SecondsPerChart=4;public int FramesPerSecond=24,MaximumEdge;public string Transition="Glide";
 }
 // Pre-build text and geometry once. Animation transforms that drawing, rather
 // than re-layouting labels each frame. Reports always retain continuation pages.
 public sealed class AnalyticsAnimation {
  sealed class Layer {public AnalyticsMark Mark;public Drawing Drawing;public int Index;public Point? RingCentre;}
  readonly List<List<Layer>> scenes=new List<List<Layer>>();readonly IList<AnalyticsPage> pages;readonly AnalyticsVideoOptions options;
  public double Duration {get{return pages.Count*options.SecondsPerChart;}}
  public AnalyticsAnimation(IList<AnalyticsPage> pages,AnalyticsVideoOptions options){
   if(pages==null||pages.Count==0)throw new ArgumentException("Choose at least one chart.");
   if(options==null||options.SecondsPerChart<=0||double.IsNaN(options.SecondsPerChart)||double.IsInfinity(options.SecondsPerChart)||options.FramesPerSecond<1||options.FramesPerSecond>60||options.MaximumEdge<0)throw new ArgumentException("Invalid animation settings.");
   if(pages.Any(p=>p.CanvasWidth!=pages[0].CanvasWidth||p.CanvasHeight!=pages[0].CanvasHeight))throw new ArgumentException("All video scenes must use the same layout.");
   this.pages=pages;this.options=options;var logo=AnalyticsExport.BrandLogo();
   foreach(var page in pages){var layers=new List<Layer>();int n=0;foreach(var mark in page.Marks){var drawing=new DrawingGroup();using(var context=drawing.Open())AnalyticsExport.DrawMark(context,mark,logo);drawing.Freeze();layers.Add(new Layer{Mark=mark,Drawing=drawing,Index=mark.Role=="chart"?n++:0,RingCentre=Centre(mark)});}scenes.Add(layers);}
  }
  static Point? Centre(AnalyticsMark mark){
   if(mark.Role!="chart"||mark.Kind!="polygon")return null;int steps=mark.Points.Length/4-1;var p=mark.Points;int middle=Math.Max(1,steps/3)*2,last=Math.Max(2,steps*2/3)*2;
   double x=p[0],y=p[1],a=p[middle],b=p[middle+1],c=p[last],d=p[last+1],denominator=2*(x*(b-d)+a*(d-y)+c*(y-b));if(Math.Abs(denominator)<.00001)return null;
   double e=x*x+y*y,f=a*a+b*b,g=c*c+d*d;return new Point((e*(b-d)+f*(d-y)+g*(y-b))/denominator,(e*(c-a)+f*(x-c)+g*(a-x))/denominator);
  }
  static Geometry Wipe(Point centre,double radius,double amount){
   var shape=new StreamGeometry();using(var context=shape.Open()){context.BeginFigure(centre,true,true);for(int i=0;i<=64;i++){double a=-Math.PI/2+Math.PI*2*amount*i/64;context.LineTo(new Point(centre.X+radius*Math.Cos(a),centre.Y+radius*Math.Sin(a)),true,false);}}shape.Freeze();return shape;
  }
  static double Ease(double t){t=Math.Max(0,Math.Min(1,t));return 1-Math.Pow(1-t,3);}
  void Scene(DrawingContext context,int index,double seconds,double opacity,double shift,double zoom){
   double width=pages[0].CanvasWidth,height=pages[0].CanvasHeight;context.PushOpacity(opacity);context.PushTransform(new TranslateTransform(shift,0));context.PushTransform(new ScaleTransform(zoom,zoom,width/2,height/2));
   foreach(var layer in scenes[index]){
    var mark=layer.Mark;if(mark.Role!="chart"){context.DrawDrawing(layer.Drawing);continue;}
    double reveal=Ease((seconds-Math.Min(.25,layer.Index*.008))/.85);
    // Axes and background tracks remain anchored throughout the reveal.
    bool grow=mark.FillEnd!=null&&(mark.Kind=="rect"||mark.Kind=="polygon");
    if(grow&&mark.Kind=="rect"){
     bool column=mark.VerticalGradient;context.PushTransform(new ScaleTransform(column?1:reveal,column?reveal:1,mark.X,mark.Y+mark.Height));context.DrawDrawing(layer.Drawing);context.Pop();
    }else if(grow&&mark.Kind=="polygon"){
     if(layer.RingCentre.HasValue&&reveal<.999){context.PushClip(Wipe(layer.RingCentre.Value,Math.Max(width,height)*2,reveal));context.DrawDrawing(layer.Drawing);context.Pop();}else{context.PushOpacity(reveal);context.DrawDrawing(layer.Drawing);context.Pop();}
    }else if(mark.Kind=="text"){
     context.PushOpacity(reveal);context.PushTransform(new TranslateTransform(0,12*(1-reveal)));context.DrawDrawing(layer.Drawing);context.Pop();context.Pop();
    }else context.DrawDrawing(layer.Drawing);
   }context.Pop();context.Pop();context.Pop();
  }
  public void Draw(DrawingContext context,double time){
   time=Math.Max(0,Math.Min(Duration-.000001,time));int scene=Math.Min(pages.Count-1,(int)(time/options.SecondsPerChart));double local=time-scene*options.SecondsPerChart;
   double transition=Math.Min(.65,options.SecondsPerChart*.23),mix=Ease(local/transition),width=pages[0].CanvasWidth;
   context.PushClip(new RectangleGeometry(new Rect(0,0,width,pages[0].CanvasHeight)));
   context.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(pages[0].Background)),null,new Rect(0,0,width,pages[0].CanvasHeight));
   bool glide=options.Transition=="Glide",zoom=options.Transition=="Zoom";
   if(scene>0&&local<transition)Scene(context,scene-1,options.SecondsPerChart,1,glide?-width*.075*mix:0,zoom?1+.025*mix:1);
   Scene(context,scene,local,scene==0?1:mix,glide&&scene>0?width*.075*(1-mix):0,zoom?1.025-.025*mix:1);context.Pop();
  }
  public DrawingImage Preview(double seconds){var group=new DrawingGroup();using(var context=group.Open())Draw(context,seconds);group.Freeze();var result=new DrawingImage(group);result.Freeze();return result;}
  public byte[] Frame(double seconds,int width,int height){
   var visual=new DrawingVisual();using(var context=visual.RenderOpen()){context.PushTransform(new ScaleTransform(width/pages[0].CanvasWidth,height/pages[0].CanvasHeight));Draw(context,seconds);context.Pop();}
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var bytes=new byte[width*height*4];bitmap.CopyPixels(bytes,width*4,0);return bytes;
  }
  public static void Dimensions(AnalyticsPage page,int maximumEdge,out int width,out int height){
   int originalWidth=(int)page.CanvasWidth,originalHeight=(int)page.CanvasHeight,a=originalWidth,b=originalHeight;while(b!=0){int next=a%b;a=b;b=next;}int unitWidth=originalWidth/a,unitHeight=originalHeight/a;
   int multiple=maximumEdge>0?Math.Min(a,maximumEdge/Math.Max(unitWidth,unitHeight)):a;if((unitWidth%2!=0||unitHeight%2!=0)&&multiple%2!=0)multiple--;multiple=Math.Max(2,multiple);width=unitWidth*multiple;height=unitHeight*multiple;
  }
  public static void Save(string destination,IList<AnalyticsPage> pages,string format,AnalyticsVideoOptions options,Action<int,string> progress,CancellationToken cancellation){
   if(format!="MP4"&&format!="GIF")throw new ArgumentException("Choose MP4 or GIF.");var animation=new AnalyticsAnimation(pages,options);int width,height;Dimensions(pages[0],options.MaximumEdge,out width,out height);
   int count=checked((int)Math.Ceiling(animation.Duration*options.FramesPerSecond));string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp."+format.ToLowerInvariant();
   try{
    cancellation.ThrowIfCancellationRequested();if(format=="MP4"){
     using(var encoder=new AnalyticsMp4(temporary,width,height,options.FramesPerSecond)){
      for(int i=0;i<count;i++){cancellation.ThrowIfCancellationRequested();encoder.Add(animation.Frame(i/(double)options.FramesPerSecond,width,height));if(progress!=null)progress((i+1)*98/count,"Rendering scene "+(Math.Min(pages.Count-1,(int)(i/(double)options.FramesPerSecond/options.SecondsPerChart))+1)+" of "+pages.Count);}
      cancellation.ThrowIfCancellationRequested();if(progress!=null)progress(99,"Finalising MP4");encoder.Complete();
     }
    }else{
     if(progress!=null)progress(0,"Preparing animation palette");var palette=AnalyticsGif.Palette(Enumerable.Range(0,pages.Count).Select(i=>{cancellation.ThrowIfCancellationRequested();return animation.Frame(i*options.SecondsPerChart+Math.Min(1.25,options.SecondsPerChart*.75),Math.Min(width,320),Math.Max(2,(int)(Math.Min(width,320)*height/(double)width)));}));
     using(var encoder=new AnalyticsGif(temporary,width,height,palette)){
      for(int i=0;i<count;i++){cancellation.ThrowIfCancellationRequested();int delay=(int)Math.Round((i+1)*100.0/options.FramesPerSecond)-(int)Math.Round(i*100.0/options.FramesPerSecond);encoder.Add(animation.Frame(i/(double)options.FramesPerSecond,width,height),delay);if(progress!=null)progress((i+1)*98/count,"Rendering scene "+(Math.Min(pages.Count-1,(int)(i/(double)options.FramesPerSecond/options.SecondsPerChart))+1)+" of "+pages.Count);}encoder.Complete();
     }
    }
    cancellation.ThrowIfCancellationRequested();if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);if(progress!=null)progress(100,"Export complete");
   }finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
}
