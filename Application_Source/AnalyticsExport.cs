using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 public static class AnalyticsExport {
  static byte[] logoBytes;
  public static byte[] LogoBytes {
   get{if(logoBytes==null){using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("AstroArchive_Logo.png"))using(var buffer=new MemoryStream()){input.CopyTo(buffer);logoBytes=buffer.ToArray();}}return logoBytes;}
  }
  static BitmapSource Logo(){using(var input=new MemoryStream(LogoBytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=input;image.EndInit();image.Freeze();return image;}}
  static Brush Brush(string colour){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour));brush.Freeze();return brush;}
  static Brush Fill(AnalyticsMark mark){if(mark.FillEnd==null)return Brush(mark.Fill);var brush=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(mark.Fill),(Color)ColorConverter.ConvertFromString(mark.FillEnd),mark.VerticalGradient?90:0);brush.Freeze();return brush;}
  static FormattedText Text(AnalyticsMark mark){return new FormattedText(mark.Text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Arial"),FontStyles.Normal,mark.Bold?FontWeights.Bold:FontWeights.Normal,FontStretches.Normal),mark.Size,Brush(mark.Fill),1.0);}
  static Geometry Polygon(double[] points){var geometry=new StreamGeometry();using(var context=geometry.Open()){context.BeginFigure(new Point(points[0],points[1]),true,true);context.PolyLineTo(Enumerable.Range(1,points.Length/2-1).Select(i=>new Point(points[i*2],points[i*2+1])).ToList(),true,false);}geometry.Freeze();return geometry;}
  internal static void DrawMark(DrawingContext context,AnalyticsMark mark,BitmapSource logo){
   if(mark.Kind=="rect")context.DrawRoundedRectangle(Fill(mark),null,new Rect(mark.X,mark.Y,mark.Width,mark.Height),mark.Radius,mark.Radius);
   else if(mark.Kind=="polygon")context.DrawGeometry(Fill(mark),null,Polygon(mark.Points));
   else if(mark.Kind=="text")context.DrawText(Text(mark),new Point(mark.X,mark.Y));
   else if(mark.Kind=="logo")context.DrawImage(logo,new Rect(mark.X,mark.Y,mark.Width,mark.Height));
  }
  internal static BitmapSource BrandLogo(){return Logo();}
  static void Draw(DrawingContext context,IList<AnalyticsPage> pages){
   var logo=Logo();int columns=AnalyticsGraphics.SheetColumns(pages);double width,height;AnalyticsGraphics.SheetSize(pages,out width,out height);
   context.DrawRectangle(Brush(pages[0].Background),null,new Rect(0,0,width,height));
   for(int i=0;i<pages.Count;i++){
    context.PushTransform(new TranslateTransform(i%columns*pages[i].CanvasWidth,i/columns*pages[i].CanvasHeight));
    foreach(var mark in pages[i].Marks){
     DrawMark(context,mark,logo);
    }context.Pop();
   }
  }
  public static DrawingImage Preview(IList<AnalyticsPage> pages){var drawing=new DrawingGroup();using(var context=drawing.Open())Draw(context,pages);drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();return image;}
  static BitmapSource Raster(IList<AnalyticsPage> pages,int dpi){
   double width,height;AnalyticsGraphics.SheetSize(pages,out width,out height);
   double pixelWidth=Math.Ceiling(width*dpi/96.0),pixelHeight=Math.Ceiling(height*dpi/96.0);
   if(pixelWidth*pixelHeight>80000000||pixelWidth>32767||pixelHeight>32767)throw new InvalidOperationException("This collection is too large for one image at this resolution. Choose a lower image resolution, or export as PDF or SVG.");
   var visual=new DrawingVisual();using(var context=visual.RenderOpen())Draw(context,pages);
   var bitmap=new RenderTargetBitmap((int)pixelWidth,(int)pixelHeight,dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  }
  static string OutlinedText(AnalyticsMark mark){
   var geometry=Text(mark).BuildGeometry(new Point(mark.X,mark.Y)).GetFlattenedPathGeometry(.05,ToleranceType.Absolute);var b=new StringBuilder();
   foreach(var figure in geometry.Figures){
    b.Append(AnalyticsGraphics.N(figure.StartPoint.X)+" "+AnalyticsGraphics.N(figure.StartPoint.Y)+" m\n");
    foreach(var segment in figure.Segments){
     var line=segment as LineSegment;var poly=segment as PolyLineSegment;
     if(line!=null)b.Append(AnalyticsGraphics.N(line.Point.X)+" "+AnalyticsGraphics.N(line.Point.Y)+" l\n");
     if(poly!=null)foreach(var point in poly.Points)b.Append(AnalyticsGraphics.N(point.X)+" "+AnalyticsGraphics.N(point.Y)+" l\n");
    }if(figure.IsClosed)b.Append("h\n");
   }b.Append(geometry.FillRule==FillRule.EvenOdd?"f*\n":"f\n");return b.ToString();
  }
  static byte[] LogoRgb(string background){
   var visual=new DrawingVisual();using(var context=visual.RenderOpen()){context.DrawRectangle(Brush(background),null,new Rect(0,0,256,256));context.DrawImage(Logo(),new Rect(0,0,256,256));}
   var bitmap=new RenderTargetBitmap(256,256,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var rgb=new FormatConvertedBitmap(bitmap,PixelFormats.Rgb24,null,0);byte[] bytes=new byte[256*256*3];rgb.CopyPixels(bytes,256*3,0);return bytes;
  }
  public static void Save(string destination,IList<AnalyticsPage> pages,string format,int dpi){
   if(pages.Count==0)throw new ArgumentException("Choose at least one chart.");
   string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
    using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     if(format=="SVG"){byte[] bytes=new UTF8Encoding(false).GetBytes(AnalyticsGraphics.Svg(pages,Convert.ToBase64String(LogoBytes)));output.Write(bytes,0,bytes.Length);}
     else if(format=="PDF")AnalyticsPdf.Write(output,pages,LogoRgb(pages[0].Background),256,256,OutlinedText);
     else if(format=="PNG"||format=="JPEG"){
      BitmapEncoder encoder=format=="PNG"?(BitmapEncoder)new PngBitmapEncoder():new JpegBitmapEncoder{QualityLevel=96};encoder.Frames.Add(BitmapFrame.Create(Raster(pages,dpi)));encoder.Save(output);
     }else throw new ArgumentException("Choose PNG, JPEG, PDF or SVG.");
    }
    if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);
   }finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
}
