// Mosaic metadata uses existing header/session reads. No pixel decoding or solving.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public sealed class MosaicHint {
  public string ProjectKey,Name,PanelKey,Evidence,Conflict;
  public int? ExpectedPanels,Row,Column; public bool Declared,Output;
  public MosaicHint Clone(){return (MosaicHint)MemberwiseClone();}
 }
 public sealed class SkyPoint {public double RA,Dec;public SkyPoint(){}public SkyPoint(double ra,double dec){RA=ra;Dec=dec;}}
 public sealed class SkyGeometry {
  public double RA,Dec,WidthDegrees,HeightDegrees;public string Evidence;public bool Approximate;
  public List<SkyPoint> Corners=new List<SkyPoint>();
  public SkyGeometry Clone(){var c=(SkyGeometry)MemberwiseClone();c.Corners=Corners.Select(p=>new SkyPoint(p.RA,p.Dec)).ToList();return c;}
  public bool HasFootprint {get{return Corners!=null&&Corners.Count==4&&WidthDegrees>0&&HeightDegrees>0;}}
 }
 public static class MosaicMetadata {
  static string Text(Dictionary<string,object> data,params string[] keys){if(data==null)return null;foreach(string key in keys){var pair=data.FirstOrDefault(p=>string.Equals(p.Key,key,StringComparison.OrdinalIgnoreCase));if(pair.Value is string||pair.Value is int||pair.Value is long||pair.Value is double||pair.Value is bool){string value=Convert.ToString(pair.Value,CultureInfo.InvariantCulture).Trim();if(value.Length>0&&value.Length<=512)return value;}}return null;}
  static int? Integer(string value){int n;return int.TryParse(value,out n)&&n>=0&&n<=10000?(int?)n:null;}
  static bool Flag(string value){return value!=null&&new[]{"T","TRUE","1","YES","MOSAIC"}.Contains(value.ToUpperInvariant());}
  static string Value(string value){return string.IsNullOrWhiteSpace(value)?null:value.Trim();}
  static string PanelKey(string value){int n;return value!=null&&int.TryParse(value,out n)&&n>=0?n.ToString(CultureInfo.InvariantCulture):value;}
  static string Combine(string a,string b,string field,MosaicHint hint){if(a!=null&&b!=null&&!string.Equals(a,b,StringComparison.OrdinalIgnoreCase))hint.Conflict="Conflicting "+field+" metadata.";return a??b;}
  static MosaicHint Fields(Dictionary<string,object> root,bool nested){
   string id=Text(root,"mosaicId","mosaic_id"),name=Text(root,"mosaicName","mosaic_name");
   if(nested){id=id??Text(root,"id","projectId");name=name??Text(root,"name","projectName");}
   string panel=PanelKey(Text(root,"panelId","panel_id","tileId","tile_id"));
   int? row=Integer(Text(root,"panelRow","tileRow")),column=Integer(Text(root,"panelColumn","tileColumn"));
   if(panel==null&&row.HasValue&&column.HasValue)panel="row "+row+" column "+column;
   bool declared=id!=null||name!=null||Flag(Text(root,"isMosaic","is_mosaic"));
   if(!declared&&panel==null)return null;
   return new MosaicHint{ProjectKey=id,Name=name,PanelKey=panel,ExpectedPanels=Integer(Text(root,"panelCount","plannedPanels","panel_count")),Row=row,Column=column,Declared=declared,Output=new[]{"output","completed","stitched"}.Contains((Text(root,"mosaicRole")??"").ToLowerInvariant()),Evidence="Session mosaic metadata"};
  }
  static bool FileMatches(object value,string name){var s=value as string;if(s!=null)return string.Equals(Path.GetFileName(s.Replace('\\','/')),name,StringComparison.OrdinalIgnoreCase);var list=value as IEnumerable;if(list!=null)foreach(var item in list)if(FileMatches(item,name))return true;return false;}
  public static MosaicHint Session(Dictionary<string,object> root,string filename){
   if(root==null)return null;
   var hint=Fields(root,false);object nested;
   var mosaic=root.FirstOrDefault(p=>string.Equals(p.Key,"mosaic",StringComparison.OrdinalIgnoreCase)).Value as Dictionary<string,object>;
   if(mosaic!=null)hint=Merge(hint,Fields(mosaic,true));
   // Arrays describe several panels. Only a unique exact filename association applies.
   var matches=new List<KeyValuePair<string,Dictionary<string,object>>>();
   foreach(var container in new[]{root,mosaic}.Where(x=>x!=null))foreach(string key in new[]{"panels","frames","images"}){
    if(!container.TryGetValue(key,out nested)||nested is string)continue;var items=nested as IEnumerable;if(items==null)continue;
    foreach(var item in items){var fields=item as Dictionary<string,object>;if(fields==null)continue;
     if(fields.Any(p=>new[]{"file","filename","fileName","path","files"}.Contains(p.Key)&&FileMatches(p.Value,filename)))matches.Add(new KeyValuePair<string,Dictionary<string,object>>(key,fields));
    }
   }
   if(matches.Count>1){if(hint==null)hint=new MosaicHint{Evidence="Session mosaic metadata"};hint.Conflict="Several session entries claim this filename.";}
   if(matches.Count==1){var panel=Fields(matches[0].Value,false)??new MosaicHint{Evidence="Per-file session panel metadata"};if(matches[0].Key=="panels")panel.PanelKey=panel.PanelKey??PanelKey(Text(matches[0].Value,"id"));hint=Merge(hint,panel);}
   return hint;
  }
  public static MosaicHint Merge(MosaicHint a,MosaicHint b){
   if(a==null)return b==null?null:b.Clone();if(b==null)return a.Clone();var h=a.Clone();
   h.ProjectKey=Combine(a.ProjectKey,b.ProjectKey,"mosaic identifier",h);h.Name=Combine(a.Name,b.Name,"mosaic name",h);h.PanelKey=Combine(a.PanelKey,b.PanelKey,"panel identifier",h);
   h.ExpectedPanels=a.ExpectedPanels??b.ExpectedPanels;h.Row=a.Row??b.Row;h.Column=a.Column??b.Column;h.Declared=a.Declared||b.Declared;h.Output=a.Output||b.Output;h.Conflict=h.Conflict??b.Conflict;h.Evidence=a.Evidence+"; "+b.Evidence;return h;
  }
  public static MosaicHint Read(Frame frame,FitsHeader header,string path,string root,MosaicHint session){
   string id=Value(header.Get("MOSAICID")),name=Value(header.Get("MOSNAME","MOSAICNM")),panel=PanelKey(Value(header.Get("PANELID","TILEID")));
   int? row=Integer(header.Get("PANELROW")),column=Integer(header.Get("PANELCOL"));if(panel==null&&row.HasValue&&column.HasValue)panel="row "+row+" column "+column;
   MosaicHint fits=id!=null||name!=null||Flag(header.Get("MOSAIC"))||panel!=null?new MosaicHint{ProjectKey=id,Name=name,PanelKey=panel,Row=row,Column=column,ExpectedPanels=Integer(header.Get("NPANELS")),Declared=id!=null||name!=null||Flag(header.Get("MOSAIC")),Output=new[]{"OUTPUT","COMPLETED","STITCHED"}.Contains(header.Get("MOSROLE").ToUpperInvariant()),Evidence="FITS mosaic metadata"}:null;
   var hint=Merge(fits,session);
   path=Path.GetFullPath(path);root=Path.GetFullPath(root);string relative=Util.Within(path,root)?path.Substring(root.TrimEnd('\\','/').Length).TrimStart('\\','/').Replace('\\','/'):frame.OriginalName??Path.GetFileName(path);
   string markerPath=Path.GetFileName(root.TrimEnd('\\','/'))+"/"+relative;
   bool marker=Regex.IsMatch(markerPath,@"(?:^|[/_ -])MOSAIC(?:$|[/_ .-])",RegexOptions.IgnoreCase);
   if(hint==null&&marker)hint=new MosaicHint{Name=Catalog.IsAmbiguous(frame.Target)?"Mosaic candidate":frame.Target+" mosaic",Evidence="MOSAIC folder/filename marker"};
   if(hint==null)return null;
   var tokens=Regex.Matches(markerPath,@"(?:^|[/_ -])(?:panel|tile)[_ -]*(\d{1,4})(?=$|[/_ .-])",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>int.Parse(m.Groups[1].Value).ToString()).Distinct().ToList();
   if(tokens.Count>1)hint.Conflict="Conflicting panel numbers in folder and filename.";
   if(tokens.Count==1){hint.PanelKey=Combine(hint.PanelKey,tokens[0],"panel identifier",hint);hint.Evidence+="; numbered panel/tile marker";}
   if(hint.ProjectKey==null){
    // A named mosaic folder is shared by its panel subdirectories and targets.
    string directory=Path.GetDirectoryName(path),anchor=null;
    while(directory!=null){if(Regex.IsMatch(Path.GetFileName(directory),@"(?:^|[_ -])MOSAIC(?:$|[_ .-])",RegexOptions.IgnoreCase)){anchor=directory;break;}if(directory.Equals(root,StringComparison.OrdinalIgnoreCase))break;directory=Path.GetDirectoryName(directory);}
    hint.ProjectKey=anchor==null?"session:"+frame.Session:"folder:"+anchor.Substring(Path.GetPathRoot(anchor).Length).Replace('\\','/');
   }
   else hint.ProjectKey="declared:"+hint.ProjectKey;
   hint.ProjectKey=Util.HashText((frame.TelescopeIdentity??frame.Telescope)+"|"+hint.ProjectKey);
   if(hint.Name==null)hint.Name=Catalog.IsAmbiguous(frame.Target)?"Mosaic "+hint.ProjectKey.Substring(0,8):frame.Target+" mosaic";
   if(hint.Name.Length>160)hint.Name=hint.Name.Substring(0,160);if(hint.ExpectedPanels==0)hint.ExpectedPanels=null;
   return hint;
  }
 }
 public static class MosaicGeometry {
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
  public static SkyGeometry Approximate(double ra,double dec,double height,int widthPixels,int heightPixels){
   if(!ValidPosition(ra,dec)||height<=0||height>30||widthPixels<=0||heightPixels<=0)return null;
   double w=height*widthPixels/heightPixels;if(w>30)return null;
   return new SkyGeometry{RA=ra,Dec=dec,WidthDegrees=w,HeightDegrees=height,Approximate=true,Evidence="Solved centre with configured field height; orientation assumed",Corners=new List<SkyPoint>{Inverse(-w/2,-height/2,ra,dec),Inverse(w/2,-height/2,ra,dec),Inverse(w/2,height/2,ra,dec),Inverse(-w/2,height/2,ra,dec)}};
  }
  static double Area(List<double[]> p){double a=0;for(int i=0;i<p.Count;i++){var b=p[(i+1)%p.Count];a+=p[i][0]*b[1]-b[0]*p[i][1];}return Math.Abs(a)/2;}
  static double Cross(double[] a,double[] b,double[] p){return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0]);}
  public static double Overlap(SkyGeometry a,SkyGeometry b){
   if(a==null||b==null||!a.HasFootprint||!b.HasFootprint)return 0;
   var p=a.Corners.Select(x=>Project(x,a.RA,a.Dec)).ToList();var q=b.Corners.Select(x=>Project(x,a.RA,a.Dec)).ToList();if(p.Any(x=>x==null)||q.Any(x=>x==null))return 0;
   double size=Math.Min(Area(p),Area(q));if(size<=0)return 0;
   double winding=0;for(int i=0;i<q.Count;i++)winding+=q[i][0]*q[(i+1)%q.Count][1]-q[(i+1)%q.Count][0]*q[i][1];double sign=winding>=0?1:-1;
   for(int i=0;i<q.Count&&p.Count>0;i++){var x=q[i];var y=q[(i+1)%q.Count];var clipped=new List<double[]>();var previous=p[p.Count-1];double dp=sign*Cross(x,y,previous);
    foreach(var current in p){double dc=sign*Cross(x,y,current);if((dc>=0)!=(dp>=0)){double t=dp/(dp-dc);clipped.Add(new[]{previous[0]+t*(current[0]-previous[0]),previous[1]+t*(current[1]-previous[1])});}if(dc>=0)clipped.Add(current);previous=current;dp=dc;}p=clipped;
   }return Math.Min(1,Area(p)/size);
  }
  public static List<List<Frame>> PointingGroups(IEnumerable<Frame> frames){
   var groups=new List<List<Frame>>();var radii=new List<double>();
   foreach(var f in frames.Where(f=>f.Sky!=null&&f.Sky.HasFootprint).OrderBy(f=>f.Hash??f.SourcePath,StringComparer.Ordinal)){
    double size=Math.Min(f.Sky.WidthDegrees,f.Sky.HeightDegrees);
    int match=-1;double distance=0;for(int i=0;i<groups.Count;i++){var first=groups[i][0].Sky;double value=Catalog.Distance(first.RA,first.Dec,f.Sky.RA,f.Sky.Dec);if(value+radii[i]<=.08*Math.Min(size,Math.Min(first.WidthDegrees,first.HeightDegrees))){match=i;distance=value;break;}}
    if(match<0){groups.Add(new List<Frame>{f});radii.Add(0);}else{groups[match].Add(f);radii[match]=Math.Max(radii[match],distance);}
   }return groups;
  }
 }
}
