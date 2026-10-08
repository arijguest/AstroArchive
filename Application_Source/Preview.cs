// FITS preview rendering and viewport geometry, independent of WPF.
using System;
using System.Linq;
namespace AstroArchive {
 public static class PreviewPixels {
  public static byte[] Render(FitsImage image){
   if(image==null||image.Width<=0||image.Height<=0||image.Pixels==null||image.Pixels.Length!=(long)image.Width*image.Height)throw new ArgumentException("Invalid preview image.");
   var finite=image.Pixels.Where(v=>!double.IsNaN(v)&&!double.IsInfinity(v)).OrderBy(v=>v).ToArray();var pixels=new byte[image.Pixels.Length];if(finite.Length==0)return pixels;
   double low=finite[(int)((finite.Length-1)*0.005)],high=finite[(int)((finite.Length-1)*0.995)];if(high<=low){low=finite[0];high=finite[finite.Length-1];}
   for(int i=0;i<pixels.Length;i++){double value=image.Pixels[i];if(double.IsNaN(value)||double.IsInfinity(value))continue;if(high<=low){pixels[i]=127;continue;}double normalized=Math.Max(0,Math.Min(1,(value-low)/(high-low))),scaled=12*normalized;pixels[i]=(byte)Math.Round(255*Math.Log(scaled+Math.Sqrt(scaled*scaled+1))/Math.Log(12+Math.Sqrt(145)));}
   return pixels;
  }
 }
 // Rotate only the display; fitting the frame to this aspect avoids letterboxing.
 public class PreviewGeometry {
  public readonly double Width,Height;public readonly bool Rotated;public readonly int QuarterTurns;
  public PreviewGeometry(double width,double height):this(width,height,width>height?1:0){}
  public PreviewGeometry(double width,double height,int quarterTurns){QuarterTurns=((quarterTurns%4)+4)%4;Rotated=QuarterTurns%2!=0;Width=Rotated?height:width;Height=Rotated?width:height;}
  public void Frame(double availableWidth,double availableHeight,out double width,out double height){width=Math.Max(0,Math.Min(availableWidth,availableHeight*Width/Height));height=width*Height/Width;}
 }
 public class PreviewZoom {
  public double Scale{get;private set;}public double X{get;private set;}public double Y{get;private set;}
  public PreviewZoom(){Scale=1;}
  static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
  public void Fit(double width,double height,double imageWidth,double imageHeight){if(width<=0||height<=0||imageWidth<=0||imageHeight<=0)return;Scale=Math.Min(32,Math.Max(0.01,Math.Min(width/imageWidth,height/imageHeight)));X=(width-imageWidth*Scale)/2;Y=(height-imageHeight*Scale)/2;}
  public void Zoom(double factor,double anchorX,double anchorY){if(!Finite(factor)||factor<=0||!Finite(anchorX)||!Finite(anchorY))return;double next=Math.Min(32,Math.Max(0.01,Scale*factor)),ratio=next/Scale;X=anchorX-(anchorX-X)*ratio;Y=anchorY-(anchorY-Y)*ratio;Scale=next;}
  public void Pan(double x,double y){if(Finite(x)&&Finite(y)){X+=x;Y+=y;}}
  public void Constrain(double width,double height,double imageWidth,double imageHeight){
   if(width<=0||height<=0||imageWidth<=0||imageHeight<=0)return;
   double minimum=Math.Min(width/imageWidth,height/imageHeight);if(Scale<minimum)Zoom(minimum/Scale,width/2,height/2);
   X=Math.Max(Math.Min(0,width-imageWidth*Scale),Math.Min(0,X));Y=Math.Max(Math.Min(0,height-imageHeight*Scale),Math.Min(0,Y));
  }
 }
 public static class TableText {
  public static string Display(string value,bool target=false){
   if(target)return string.IsNullOrWhiteSpace(value)?"Unknown":value;
   if(string.IsNullOrWhiteSpace(value)||value=="?"||string.Equals(value,"Unknown",StringComparison.OrdinalIgnoreCase)||string.Equals(value,"Unknown date",StringComparison.OrdinalIgnoreCase)||string.Equals(value,"Other / unknown",StringComparison.OrdinalIgnoreCase))return "-";
   return value.Replace(" (model unknown)"," / -");
  }
 }
}
