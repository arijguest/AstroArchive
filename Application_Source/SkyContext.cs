// Small, display-only sky snapshots. No file reads, plate solving or clock polling.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public struct SkyVector {
  public readonly double X,Y,Z;
  public SkyVector(double x,double y,double z){X=x;Y=y;Z=z;}
  public static SkyVector Equatorial(double ra,double dec){double a=ra*Math.PI/180,d=dec*Math.PI/180,c=Math.Cos(d);return new SkyVector(c*Math.Cos(a),c*Math.Sin(a),Math.Sin(d));}
  public static SkyVector Horizontal(double azimuth,double altitude){double a=azimuth*Math.PI/180,d=altitude*Math.PI/180,c=Math.Cos(d);return new SkyVector(c*Math.Sin(a),c*Math.Cos(a),Math.Sin(d));}
  public double Dot(SkyVector other){return X*other.X+Y*other.Y+Z*other.Z;}
 }
 // Only the viewing camera moves; capture coordinates, epoch and horizon stay fixed.
 public sealed class SkyGlobeCamera {
  double homeYaw,homeTilt;
  public double Yaw{get;private set;}public double Tilt{get;private set;}public double Zoom{get;private set;}
  public SkyGlobeCamera(){SetHome(180,25);}
  public void SetHome(double yaw,double tilt){if(!CaptureSky.Finite(yaw)||!CaptureSky.Finite(tilt))return;homeYaw=SkyOrientation.Wrap(yaw);homeTilt=Math.Max(-89,Math.Min(89,tilt));Reset();}
  public void Reset(){Yaw=homeYaw;Tilt=homeTilt;Zoom=1;}
  public void Orbit(double yaw,double tilt){if(!CaptureSky.Finite(yaw)||!CaptureSky.Finite(tilt))return;Yaw=SkyOrientation.Wrap(Yaw+yaw%360);Tilt=Math.Max(-89,Math.Min(89,Tilt+tilt));}
  public void Magnify(double factor){if(!CaptureSky.Finite(factor)||factor<=0)return;Zoom=Math.Max(0.6,Math.Min(3,Zoom*factor));}
 }
 public sealed class SkyFigure {
  public string Name;public SkyVector Label;public int[][] Paths;
 }
 public static class SkyFigures {
  public static readonly SkyVector[] Stars;
  public static readonly SkyFigure[] Figures;
  static SkyFigures(){
   var stars=new List<SkyVector>();var figures=new List<SkyFigure>();var indices=new Dictionary<string,int>();
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("sky-constellations.txt"))using(var reader=new StreamReader(stream)){
    string line;while((line=reader.ReadLine())!=null){if(line.StartsWith("#")||line.Length==0)continue;var fields=line.Split('|');var paths=new List<int[]>();
     foreach(string path in fields[3].Split('/')){var points=new List<int>();foreach(string text in path.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)){int index;if(!indices.TryGetValue(text,out index)){index=stars.Count;indices[text]=index;stars.Add(Coordinate(text));}points.Add(index);}paths.Add(points.ToArray());}
     figures.Add(new SkyFigure{Name=fields[1],Label=Coordinate(fields[2]),Paths=paths.ToArray()});
    }
   }
   Stars=stars.ToArray();Figures=figures.ToArray();
  }
  static SkyVector Coordinate(string text){var parts=text.Trim().Split(',');return SkyVector.Equatorial(double.Parse(parts[0],CultureInfo.InvariantCulture),double.Parse(parts[1],CultureInfo.InvariantCulture));}
 }
 public sealed class CaptureSky {
  static readonly Lazy<Dictionary<string,CatalogObject>> positions=new Lazy<Dictionary<string,CatalogObject>>(()=>Catalog.Objects.GroupBy(o=>o.Name).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase));
  public bool HasPosition,HasHorizon,ApproximatePosition;public double RA,Dec,Latitude,Longitude,Altitude,Azimuth;
  public DateTime? Utc;public string TargetLabel,TimeLabel,Summary,Evidence,Key;
  public SkyOrientation Orientation;public double? StackSeconds;public DateTime? TrackStartUtc,TrackEndUtc;public string TrackEvidence;
  public bool BelowHorizon{get{return HasPosition&&HasHorizon&&Altitude<0;}}
  SkyVector[] stackTrack;
  public SkyVector[] StackTrack{get{
   if(stackTrack!=null)return stackTrack;if(!TrackStartUtc.HasValue||!TrackEndUtc.HasValue||TrackEndUtc<=TrackStartUtc||!HasPosition||!HasHorizon)return stackTrack=new SkyVector[0];
   // Fixed work per capture, never per frame in the stack. Great-circle chords
   // approximate the diurnal small-circle path, including long integrations.
   const int segments=32;stackTrack=new SkyVector[segments+1];var target=SkyVector.Equatorial(RA,Dec);
   for(int i=0;i<=segments;i++)stackTrack[i]=new SkyOrientation(TrackStartUtc.Value.AddSeconds((TrackEndUtc.Value-TrackStartUtc.Value).TotalSeconds*i/segments),true,Latitude,Longitude).Map(target);
   return stackTrack;
  }}
  public static bool IsCalibration(Frame frame){return frame!=null&&(frame.Target=="Calibration"||Regex.IsMatch(frame.Kind??"",@"^(?:Master[ _-]*)?(?:Dark(?:[ _-]*flat)?|Flat|Bias|Offset)$",RegexOptions.IgnoreCase));}
  static double? StackDuration(Frame frame){
   if(frame==null||frame.Kind!="Stack")return null;
   double? duration=frame.SkyStackDurationSeconds;
   if(!duration.HasValue){
    var header=new FitsHeader();var image=frame.Images==null?null:frame.Images.FirstOrDefault(i=>i.Key==frame.ImageKey)??frame.Images.FirstOrDefault();
    if(image!=null){if(image.Headers!=null)foreach(var value in image.Headers)header.Values[value.Key]=value.Value;if(image.Comments!=null)foreach(var value in image.Comments)header.Comments[value.Key]=value.Value;}
    MetadataFact exposure;if(frame.Facts!=null&&frame.Facts.TryGetValue("Exposure",out exposure)&&exposure!=null&&!string.IsNullOrEmpty(exposure.Raw))if(Regex.IsMatch(exposure.Raw,@"total|integrat|per[ _-]?(sub|frame)|individual|single",RegexOptions.IgnoreCase))header.Comments["EXPTIME"]=exposure.Raw;
    duration=TotalDuration(EditedMetadata.Read(string.IsNullOrWhiteSpace(frame.OriginalName)?"capture.fit":frame.OriginalName,header));
   }
   // Integration metadata is an estimate of elapsed time; never multiply an
   // ambiguous EXPTIME by the stack count or invent acquisition gaps.
   return duration.HasValue&&Finite(duration.Value)&&duration.Value>0?duration:null;
  }
  static double? TotalDuration(EditedMetadata metadata){
   if(metadata.TotalExposure.HasValue)return metadata.TotalExposure;
   // A stack's reported integration can differ from its labelled sub duration.
   // Never multiply an unqualified EXPTIME by a frame count.
   if(metadata.ReportedExposure.HasValue&&metadata.SubExposure.HasValue&&metadata.ReportedExposure>metadata.SubExposure&&(!metadata.Subs.HasValue||Math.Abs(metadata.ReportedExposure.Value-metadata.Subs.Value*metadata.SubExposure.Value)<0.01))return metadata.ReportedExposure;
   return null;
  }
  static DateTime? Offset(DateTime time,double seconds){return Finite(seconds)&&seconds<=(DateTime.MaxValue-time).TotalSeconds&&seconds>=-(time-DateTime.MinValue).TotalSeconds?(DateTime?)time.AddSeconds(seconds):null;}
  static void ObserveGroup(CaptureSky sky,IEnumerable<Frame> group){
   if(group==null)return;var frames=group.Where(f=>f.Kind=="Light").ToList();if(frames.Count<2)return;
   var recorded=frames.Select(f=>new{Frame=f,Time=CaptureUtc(f)}).Where(f=>f.Time.HasValue).OrderBy(f=>f.Time.Value).ToList();if(recorded.Count==0)return;
   var first=recorded[0];var last=recorded[recorded.Count-1];var end=last.Time;
   if(last.Frame.Exposure.HasValue&&Finite(last.Frame.Exposure.Value)&&last.Frame.Exposure>0)end=Offset(last.Time.Value,last.Frame.Exposure.Value);
   if(recorded.Count==1){if(frames.Any(f=>!f.Exposure.HasValue||!Finite(f.Exposure.Value)||f.Exposure<=0))return;end=Offset(first.Time.Value,frames.Sum(f=>f.Exposure.Value));}
   if(!end.HasValue||end<=first.Time)return;
   sky.TrackStartUtc=first.Time;sky.TrackEndUtc=end;sky.Utc=end;
   sky.TrackEvidence=recorded.Count==1?"Estimated group interval from integration; gaps unknown.":"Recorded group interval, including the final sub exposure."+(recorded.Count<frames.Count?" Some capture times are missing.":"");
  }
  public static bool Finite(double number){return !double.IsNaN(number)&&!double.IsInfinity(number);}
  static bool Position(double ra,double dec){return Finite(ra)&&Finite(dec)&&ra>=0&&ra<=360&&Math.Abs(dec)<=90;}
  public static Frame FromHeader(FitsHeader header,string format,string filename,string fallbackTarget=null){
   string target=header.Get("OBJECT","OBJNAME","TARGET");if(Catalog.IsAmbiguous(target))target=fallbackTarget??Catalog.TargetFromFilename(filename);
   var frame=new Frame{OriginalName=filename,Target=target,Observed=header.Get("DATE-OBS","DATEOBS","DATE_OBS"),Latitude=header.Number("SITELAT","OBSGEO-B"),Longitude=header.Number("SITELONG","SITELON","OBSGEO-L"),TimeSource="Capture time (timezone unknown)"};
   frame.RA=Catalog.Sex(header.Get("OBJCTRA"),true)??header.Number("RA_DEG","RADEG","RA_OBJ");frame.Dec=Catalog.Sex(header.Get("OBJCTDEC"),false)??header.Number("DEC_DEG","DECDEG","DEC_OBJ","DEC");
   string comment;if(!frame.RA.HasValue&&header.Comments.TryGetValue("RA",out comment)&&comment.IndexOf("deg",StringComparison.OrdinalIgnoreCase)>=0)frame.RA=header.Number("RA");
   if(header.Get("CTYPE1").StartsWith("RA")&&header.Get("CTYPE2").StartsWith("DEC")){frame.RA=header.Number("CRVAL1")??frame.RA;frame.Dec=header.Number("CRVAL2")??frame.Dec;}
   string system=header.Get("TIMESYS");if((system.Length==0||system.Equals("UTC",StringComparison.OrdinalIgnoreCase))&&(format=="FITS"||Regex.IsMatch(frame.Observed,@"(?:Z|[+-]\d\d:\d\d)$",RegexOptions.IgnoreCase)))frame.TimeSource="FITS UTC";
   // A non-UTC TIMESYS must not become UTC merely because DATE-OBS has a Z suffix.
   if(system.Length>0&&!system.Equals("UTC",StringComparison.OrdinalIgnoreCase)){frame.Observed="";frame.TimeSource="Capture clock "+system;}
   string kind=header.Get("IMAGETYP","IMAGETYPE","FRAME","FRAMETYP")+" "+Path.GetFileNameWithoutExtension(filename??"");
   if(Regex.IsMatch(kind,@"(?:^|[ _-])(?:master[ _-]*)?(?:dark(?:[ _-]*flat)?|flat|bias|offset)(?:[ _-]|$)",RegexOptions.IgnoreCase)||Regex.IsMatch(header.Get("IMAGETYP","IMAGETYPE","FRAME","FRAMETYP"),@"dark|flat|bias|offset",RegexOptions.IgnoreCase)){frame.Kind="Dark";frame.Target="Calibration";}
   else if((header.Number("NCOMBINE","STACKCNT","NSTACK","STACKNUM","NSUBS","SUBCOUNT")??0)>1||Regex.IsMatch(kind,@"(?:^|[ _-])(?:stack|stacked|restacked)(?:[ _-]|$)|\d+x\d+(?:\.\d+)?s",RegexOptions.IgnoreCase))frame.Kind="Stack";
   var metadata=EditedMetadata.Read(filename,header);double? duration=frame.Kind=="Stack"?TotalDuration(metadata):metadata.TotalExposure;if(duration.HasValue&&!IsCalibration(frame)){frame.Kind="Stack";frame.SkyStackDurationSeconds=duration;}
   frame.ObservedUtc=CaptureUtc(frame).HasValue?CaptureUtc(frame).Value.ToString("o",CultureInfo.InvariantCulture):null;return frame;
  }
  public static DateTime? CaptureUtc(Frame frame){
   if(frame==null||(frame.TimeSource??"").StartsWith("Session folder",StringComparison.OrdinalIgnoreCase))return null;
   foreach(string text in new[]{frame.ObservedUtc,frame.Observed}){
    // A date alone, unzoned filename timestamp or shifted night cannot place a horizon.
    if(string.IsNullOrEmpty(text)||!Regex.IsMatch(text,@"[T ]\d\d:\d\d"))continue;
    bool explicitZone=Regex.IsMatch(text,@"(?:Z|[+-]\d\d:\d\d)$",RegexOptions.IgnoreCase);
    bool knownUtc=text==frame.ObservedUtc||string.Equals(frame.TimeSource,"FITS UTC",StringComparison.OrdinalIgnoreCase)||string.Equals(frame.TimeZoneId,"UTC",StringComparison.OrdinalIgnoreCase);
    DateTimeOffset utc;if((explicitZone||knownUtc)&&DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out utc))return utc.UtcDateTime;
    DateTime local;if(!string.IsNullOrEmpty(frame.TimeZoneId)&&DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out local))try{
     var zone=TimeZoneInfo.FindSystemTimeZoneById(frame.TimeZoneId);local=DateTime.SpecifyKind(local,DateTimeKind.Unspecified);
     if(!zone.IsInvalidTime(local)&&!zone.IsAmbiguousTime(local))return TimeZoneInfo.ConvertTimeToUtc(local,zone);
    }catch(TimeZoneNotFoundException){}catch(InvalidTimeZoneException){}
   }return null;
  }
  public static CaptureSky Resolve(Frame frame,Settings settings,IEnumerable<Frame> group=null){
   var sky=new CaptureSky{TargetLabel=frame==null?"Select a capture":Catalog.IsAmbiguous(frame.Target)?"Capture position":frame.TargetLabel,Evidence=""};
   if(frame!=null){
    if(frame.Sky!=null&&Position(frame.Sky.RA,frame.Sky.Dec)){sky.RA=frame.Sky.RA;sky.Dec=frame.Sky.Dec;sky.HasPosition=true;sky.ApproximatePosition=frame.Sky.Approximate;sky.Evidence=frame.Sky.Evidence??"Frame pointing";}
    else if(frame.RA.HasValue&&frame.Dec.HasValue&&Position(frame.RA.Value,frame.Dec.Value)){sky.RA=frame.RA.Value;sky.Dec=frame.Dec.Value;sky.HasPosition=true;sky.Evidence="Frame pointing";}
    else{CatalogObject target;if(positions.Value.TryGetValue(Catalog.CanonicalTarget(frame.Target),out target)){sky.RA=target.RA;sky.Dec=target.Dec;sky.HasPosition=true;sky.ApproximatePosition=true;sky.Evidence="Approximate target centre from the catalogue";}}
   }
   sky.Utc=CaptureUtc(frame);ObserveGroup(sky,group);bool site=false;string place="";
   if(frame!=null&&frame.Latitude.HasValue&&frame.Longitude.HasValue&&ObservingCities.ValidCoordinates(frame.Latitude.Value,frame.Longitude.Value)){sky.Latitude=frame.Latitude.Value;sky.Longitude=frame.Longitude.Value;site=true;place="Capture location";}
   else if(settings!=null&&settings.Latitude.HasValue&&settings.Longitude.HasValue&&ObservingCities.ValidCoordinates(settings.Latitude.Value,settings.Longitude.Value)){sky.Latitude=settings.Latitude.Value;sky.Longitude=settings.Longitude.Value;site=true;place=settings.ObservingCity??"Saved observing location";}
   sky.HasHorizon=site&&sky.Utc.HasValue;sky.Orientation=new SkyOrientation(sky.Utc,sky.HasHorizon,sky.Latitude,sky.Longitude);
   if(sky.Utc.HasValue)sky.TimeLabel=sky.Utc.Value.ToString("dd/MM/yy HH:mm:ss 'UTC'",CultureInfo.InvariantCulture);
   else{var date=frame==null?null:CaptureSessions.Date(frame);sky.TimeLabel=date==null?"Capture time unknown":date.Text+" · time / timezone unknown";}
   string hemisphere=sky.Dec>0?"Northern celestial sky":sky.Dec<0?"Southern celestial sky":"Celestial equator";
   if(sky.HasPosition&&sky.HasHorizon){
    var target=sky.Orientation.Map(SkyVector.Equatorial(sky.RA,sky.Dec));sky.Altitude=Math.Asin(Math.Max(-1,Math.Min(1,target.Z)))*180/Math.PI;sky.Azimuth=SkyOrientation.Wrap(Math.Atan2(target.X,target.Y)*180/Math.PI);
    string[] compass={"N","NE","E","SE","S","SW","W","NW"};
    sky.Summary=(sky.ApproximatePosition?"≈ ":"")+"Alt "+sky.Altitude.ToString("0",CultureInfo.InvariantCulture)+"° · "+compass[(int)Math.Round(sky.Azimuth/45)%8]+" "+sky.Azimuth.ToString("0",CultureInfo.InvariantCulture)+"°"+(sky.Altitude<0?" · below horizon":"");
   }else sky.Summary=!sky.HasPosition?"Pointing unavailable":!site?"Celestial view · set an observing location":"Celestial view · capture time unknown";
   sky.Evidence+=(sky.Evidence.Length>0?"\n":"")+sky.TimeLabel+"\n"+(site?place+" · "+sky.Latitude.ToString("0.###",CultureInfo.InvariantCulture)+"°, "+sky.Longitude.ToString("0.###",CultureInfo.InvariantCulture)+"°":"Choose your observing town / city in Settings for the local horizon.");
   if(sky.HasPosition)sky.Evidence+="\n"+hemisphere+" · RA "+sky.RA.ToString("0.###",CultureInfo.InvariantCulture)+"°, Dec "+sky.Dec.ToString("0.###",CultureInfo.InvariantCulture)+"°.";
   sky.Evidence+="\n"+(sky.HasHorizon?"Sky at the recorded capture time. Solid horizon; faint figures lie below it. Geometric altitude excludes atmospheric refraction.":"Celestial coordinates only. No compass horizon is inferred without a known capture time and observing location.");
   sky.StackSeconds=StackDuration(frame);
   if(!sky.TrackStartUtc.HasValue&&sky.StackSeconds.HasValue&&sky.Utc.HasValue){
    var start=Offset(sky.Utc.Value,-sky.StackSeconds.Value);if(start.HasValue){sky.TrackStartUtc=start;sky.TrackEndUtc=sky.Utc;sky.TrackEvidence="Estimated stack interval from integration; gaps unknown.";}else sky.StackSeconds=null;
   }
   if(sky.TrackStartUtc.HasValue&&sky.HasHorizon&&sky.HasPosition){sky.Evidence+="\n"+sky.TrackEvidence;sky.TimeLabel=sky.TrackStartUtc.Value.ToString("dd/MM/yy HH:mm:ss",CultureInfo.InvariantCulture)+"–"+sky.TrackEndUtc.Value.ToString("dd/MM/yy HH:mm:ss 'UTC'",CultureInfo.InvariantCulture);}
   sky.Key=string.Join("|",new[]{sky.HasPosition.ToString(),sky.HasHorizon.ToString(),sky.TrackStartUtc.HasValue?sky.TrackStartUtc.Value.Ticks.ToString(CultureInfo.InvariantCulture):"",sky.TrackEndUtc.HasValue?sky.TrackEndUtc.Value.Ticks.ToString(CultureInfo.InvariantCulture):"",sky.StackSeconds.HasValue?sky.StackSeconds.Value.ToString("R",CultureInfo.InvariantCulture):"",sky.RA.ToString("R",CultureInfo.InvariantCulture),sky.Dec.ToString("R",CultureInfo.InvariantCulture),sky.Latitude.ToString("R",CultureInfo.InvariantCulture),sky.Longitude.ToString("R",CultureInfo.InvariantCulture),sky.Utc.HasValue?sky.Utc.Value.Ticks.ToString(CultureInfo.InvariantCulture):""});return sky;
  }
 }
 public sealed class SkyOrientation {
  readonly double cz,sz,ct,st,cq,sq,cl,sl,cp,sp;readonly bool horizon;
  public static double Wrap(double degrees){return (degrees%360+360)%360;}
  public static double Sidereal(DateTime utc,double longitude){double days=(utc-new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)).TotalDays,t=days/36525;return Wrap(280.46061837+360.98564736629*days+0.000387933*t*t-t*t*t/38710000+longitude);}
  public SkyOrientation(DateTime? utc,bool horizon,double latitude,double longitude){
   this.horizon=horizon;double t=utc.HasValue?(utc.Value-new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)).TotalDays/36525:0,k=Math.PI/(180*3600);
   // J2000 catalogue vectors precess to the recorded epoch once per snapshot.
   double zeta=(2306.2181*t+0.30188*t*t+0.017998*t*t*t)*k,z=(2306.2181*t+1.09468*t*t+0.018203*t*t*t)*k,theta=(2004.3109*t-0.42665*t*t-0.041833*t*t*t)*k;
   cq=Math.Cos(zeta);sq=Math.Sin(zeta);cz=Math.Cos(z);sz=Math.Sin(z);ct=Math.Cos(theta);st=Math.Sin(theta);
   double lst=utc.HasValue?Sidereal(utc.Value,longitude)*Math.PI/180:0,p=latitude*Math.PI/180;cl=Math.Cos(lst);sl=Math.Sin(lst);cp=Math.Cos(p);sp=Math.Sin(p);
  }
  public SkyVector Map(SkyVector vector){
   double a=cq*vector.X-sq*vector.Y,b=sq*vector.X+cq*vector.Y,c=ct*a-st*vector.Z;
   var v=new SkyVector(cz*c-sz*b,sz*c+cz*b,st*a+ct*vector.Z);
   if(!horizon)return v;double meridian=cl*v.X+sl*v.Y;
   return new SkyVector(-sl*v.X+cl*v.Y,-sp*meridian+cp*v.Z,cp*meridian+sp*v.Z);
  }
 }
}
