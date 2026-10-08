using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public sealed class SkyPoint {public double RA,Dec;public SkyPoint(){}public SkyPoint(double ra,double dec){RA=ra;Dec=dec;}}
 public sealed class SkyGeometry {
  public double RA,Dec,WidthDegrees,HeightDegrees;public string Evidence;public bool Approximate;
  public List<SkyPoint> Corners=new List<SkyPoint>();
  public SkyGeometry Clone(){var c=(SkyGeometry)MemberwiseClone();c.Corners=Corners.Select(p=>new SkyPoint(p.RA,p.Dec)).ToList();return c;}
  public bool HasFootprint {get{return Corners!=null&&Corners.Count==4&&WidthDegrees>0&&HeightDegrees>0;}}
 }
 public static class SkyWcs {
  const double R=Math.PI/180;
  static bool Finite(double n){return !double.IsNaN(n)&&!double.IsInfinity(n);}
  public static bool ValidPosition(double ra,double dec){return Finite(ra)&&Finite(dec)&&ra>=0&&ra<360&&dec>=-90&&dec<=90;}
  public static SkyPoint Inverse(double x,double y,double ra,double dec){
   double a=ra*R,d=dec*R,xi=x*R,eta=y*R,t=Math.Cos(d)-eta*Math.Sin(d);
   return new SkyPoint(((a+Math.Atan2(xi,t))/R+360)%360,Math.Atan2(Math.Sin(d)+eta*Math.Cos(d),Math.Sqrt(t*t+xi*xi))/R);
  }
  public static double[] Project(SkyPoint p,double ra,double dec){
   double a=(p.RA-ra)*R,d=p.Dec*R,c=dec*R,t=Math.Sin(c)*Math.Sin(d)+Math.Cos(c)*Math.Cos(d)*Math.Cos(a);
   if(t<=0)return null;return new[]{Math.Cos(d)*Math.Sin(a)/t/R,(Math.Cos(c)*Math.Sin(d)-Math.Sin(c)*Math.Cos(d)*Math.Cos(a))/t/R};
  }
  public static FitsHeader WcsCards(Stream input){
   var header=new FitsHeader();var card=new byte[80];
   for(int count=0;count<2048;count++){
    int read=0,n;while(read<80&&(n=input.Read(card,read,80-read))>0)read+=n;if(read!=80)break;
    string text=System.Text.Encoding.ASCII.GetString(card),key=text.Substring(0,8).Trim();if(key=="END")break;if(text[8]!='=')continue;
    string value=text.Substring(10).Trim();bool quoted=false;int slash=-1;for(int i=0;i<value.Length;i++){if(value[i]=='\'')quoted=!quoted;else if(value[i]=='/'&&!quoted){slash=i;break;}}if(slash>=0)value=value.Substring(0,slash).Trim();if(value.StartsWith("'")&&value.EndsWith("'"))value=value.Substring(1,value.Length-2).Trim();header.Values[key]=value;
   }
   return header;
  }
  public static SkyGeometry FromHeader(FitsHeader h,int width,int height,string evidence="Existing FITS WCS"){
   if(width<=0||height<=0||h.Get("CTYPE1")!="RA---TAN"||h.Get("CTYPE2")!="DEC--TAN")return null;
   if(h.Values.Keys.Any(k=>k.StartsWith("A_")||k.StartsWith("B_")||k.StartsWith("AP_")||k.StartsWith("BP_")||k.StartsWith("PV1_")||k.StartsWith("PV2_")))return null;
   if(new[]{"CUNIT1","CUNIT2"}.Any(k=>h.Get(k)!=""&&h.Get(k).ToLowerInvariant()!="deg"))return null;
   if(h.Get("RADESYS","RADECSYS")!=""&&!new[]{"ICRS","FK5"}.Contains(h.Get("RADESYS","RADECSYS").ToUpperInvariant()))return null;
   if(h.Number("EQUINOX").HasValue&&h.Number("EQUINOX")!=2000)return null;
   double? ra=h.Number("CRVAL1"),dec=h.Number("CRVAL2"),px=h.Number("CRPIX1"),py=h.Number("CRPIX2");if(!ra.HasValue||!dec.HasValue||!px.HasValue||!py.HasValue||!ValidPosition(ra.Value,dec.Value))return null;
   double a,b,c,d;
   if(new[]{"CD1_1","CD1_2","CD2_1","CD2_2"}.Any(h.Values.ContainsKey)){
    var n=new[]{"CD1_1","CD1_2","CD2_1","CD2_2"}.Select(k=>h.Number(k)).ToArray();if(n.Any(v=>!v.HasValue))return null;a=n[0].Value;b=n[1].Value;c=n[2].Value;d=n[3].Value;
   }else{
    var sx=h.Number("CDELT1");var sy=h.Number("CDELT2");if(!sx.HasValue||!sy.HasValue)return null;
    if(new[]{"PC1_1","PC1_2","PC2_1","PC2_2"}.Any(h.Values.ContainsKey)){if(new[]{"PC1_1","PC1_2","PC2_1","PC2_2"}.Any(k=>h.Values.ContainsKey(k)&&!h.Number(k).HasValue))return null;a=sx.Value*(h.Number("PC1_1")??1);b=sx.Value*(h.Number("PC1_2")??0);c=sy.Value*(h.Number("PC2_1")??0);d=sy.Value*(h.Number("PC2_2")??1);}
    else{double t=(h.Number("CROTA2","CROTA1")??0)*R;a=sx.Value*Math.Cos(t);b=-sy.Value*Math.Sin(t);c=sx.Value*Math.Sin(t);d=sy.Value*Math.Cos(t);}
   }
   if(!Finite(a*b*c*d)||Math.Abs(a*d-b*c)<1e-16)return null;
   Func<double,double,SkyPoint> sky=(x,y)=>Inverse(a*(x-px.Value)+b*(y-py.Value),c*(x-px.Value)+d*(y-py.Value),ra.Value,dec.Value);
   var centre=sky((width+1)/2.0,(height+1)/2.0);var corners=new List<SkyPoint>{sky(.5,.5),sky(width+.5,.5),sky(width+.5,height+.5),sky(.5,height+.5)};
   double w=Catalog.Distance(corners[0].RA,corners[0].Dec,corners[1].RA,corners[1].Dec),z=Catalog.Distance(corners[1].RA,corners[1].Dec,corners[2].RA,corners[2].Dec);
   if(w<=0||z<=0||w>90||z>90||corners.Any(p=>!ValidPosition(p.RA,p.Dec)))return null;
   return new SkyGeometry{RA=centre.RA,Dec=centre.Dec,WidthDegrees=w,HeightDegrees=z,Corners=corners,Evidence=evidence};
  }
 }
}
