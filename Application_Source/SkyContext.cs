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
  public string Id,Name;public SkyVector Label;public int[][] Paths;
 }
 public static class SkyFigures {
  public static readonly SkyVector[] Stars;
  public static readonly SkyFigure[] Figures;
  static SkyFigures(){
   var stars=new List<SkyVector>();var figures=new List<SkyFigure>();var indices=new Dictionary<string,int>();
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("sky-constellations.txt"))using(var reader=new StreamReader(stream)){
    string line;while((line=reader.ReadLine())!=null){if(line.StartsWith("#")||line.Length==0)continue;var fields=line.Split('|');var paths=new List<int[]>();
     foreach(string path in fields[3].Split('/')){var points=new List<int>();foreach(string text in path.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)){int index;if(!indices.TryGetValue(text,out index)){index=stars.Count;indices[text]=index;stars.Add(Coordinate(text));}points.Add(index);}paths.Add(points.ToArray());}
     figures.Add(new SkyFigure{Id=fields[0],Name=fields[1],Label=Coordinate(fields[2]),Paths=paths.ToArray()});
    }
   }
   Stars=stars.ToArray();Figures=figures.ToArray();
  }
  static SkyVector Coordinate(string text){var parts=text.Trim().Split(',');return SkyVector.Equatorial(double.Parse(parts[0],CultureInfo.InvariantCulture),double.Parse(parts[1],CultureInfo.InvariantCulture));}
 }
 public sealed class CaptureSky {
  static readonly Lazy<Dictionary<string,CatalogObject>> positions=new Lazy<Dictionary<string,CatalogObject>>(()=>Catalog.Objects.GroupBy(o=>o.Name).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase));
  public bool HasPosition,HasHorizon,ApproximatePosition,PositionOfDate;public double RA,Dec,Latitude,Longitude,Altitude,Azimuth;
  public DateTime? Utc;public string TargetLabel,TimeLabel,Summary,Evidence,Key;
  public SkyOrientation Orientation;
  // Null when the capture clock is unknown; the Moon needs a real time.
  public MoonSnapshot Moon;
  public SkyVector TargetOfDate{get{var vector=SkyVector.Equatorial(RA,Dec);return PositionOfDate?vector:Orientation.Precess(vector);}}
  public SkyVector TargetOnGlobe{get{return Orientation.MapOfDate(TargetOfDate);}}
  // A body the user photographed was above the horizon; a calculated position below
  // it means the recorded clock or timezone is off, so the map stays visible.
  public bool BelowHorizon{get{return HasPosition&&HasHorizon&&Altitude<0&&Body==null;}}
  // Sun, Moon or planet named by the target or by a solar/lunar observation mode.
  public string Body;public bool BodyBelowHorizon,Daytime;public double SunAltitude=double.NaN;
  // Set when the Moon covers part of the Sun from the site, or the Moon is in Earth's shadow.
  public EclipseInfo Eclipse;
  public static bool IsCalibration(Frame frame){return frame!=null&&(frame.Target=="Calibration"||Regex.IsMatch(frame.Kind??"",@"^(?:Master[ _-]*)?(?:Dark(?:[ _-]*flat)?|Flat|Bias|Offset)$",RegexOptions.IgnoreCase));}
  public static string Compass(double azimuth){string[] compass={"N","NE","E","SE","S","SW","W","NW"};return compass[(int)Math.Round(SkyOrientation.Wrap(azimuth)/45)%8];}
  public static string Elevation(double altitude,double azimuth){return altitude.ToString("0",CultureInfo.InvariantCulture)+"° · "+Compass(azimuth)+" "+azimuth.ToString("0",CultureInfo.InvariantCulture)+"°"+(altitude<0?" · below horizon":"");}
  public static bool Finite(double number){return !double.IsNaN(number)&&!double.IsInfinity(number);}
  static bool Position(double ra,double dec){return Finite(ra)&&Finite(dec)&&ra>=0&&ra<=360&&Math.Abs(dec)<=90;}
  public static Frame FromHeader(FitsHeader header,string format,string filename,string fallbackTarget=null){
   string target=header.Get("OBJECT","OBJNAME","TARGET");if(Catalog.IsAmbiguous(target))target=fallbackTarget??Catalog.TargetFromFilename(filename);
   var frame=new Frame{OriginalName=filename,Target=target,Observed=header.Get("DATE-OBS","DATEOBS","DATE_OBS"),Latitude=header.Number("SITELAT","OBSGEO-B"),Longitude=header.Number("SITELONG","SITELON","OBSGEO-L"),TimeSource="Capture time (timezone unknown)"};
   frame.RA=Catalog.Sex(header.Get("OBJCTRA"),true)??header.Number("RA_DEG","RADEG","RA_OBJ");frame.Dec=Catalog.Sex(header.Get("OBJCTDEC"),false)??header.Number("DEC_DEG","DECDEG","DEC_OBJ","DEC");
   string comment;if(!frame.RA.HasValue&&header.Comments.TryGetValue("RA",out comment)&&comment.IndexOf("deg",StringComparison.OrdinalIgnoreCase)>=0)frame.RA=header.Number("RA");
   if(header.Get("CTYPE1").StartsWith("RA")&&header.Get("CTYPE2").StartsWith("DEC")){frame.RA=header.Number("CRVAL1")??frame.RA;frame.Dec=header.Number("CRVAL2")??frame.Dec;}
   string system=header.Get("TIMESYS");if((system.Length==0||system.Equals("UTC",StringComparison.OrdinalIgnoreCase))&&(format=="FITS"||system.Equals("UTC",StringComparison.OrdinalIgnoreCase)||Regex.IsMatch(frame.Observed,@"(?:Z|[+-]\d\d:\d\d)$",RegexOptions.IgnoreCase)))frame.TimeSource="FITS UTC";
   // A non-UTC TIMESYS must not become UTC merely because DATE-OBS has a Z suffix.
   if(system.Length>0&&!system.Equals("UTC",StringComparison.OrdinalIgnoreCase)){frame.Observed="";frame.TimeSource="Capture clock "+system;}
   string kind=header.Get("IMAGETYP","IMAGETYPE","FRAME","FRAMETYP")+" "+Path.GetFileNameWithoutExtension(filename??"");
   if(Regex.IsMatch(kind,@"(?:^|[ _-])(?:master[ _-]*)?(?:dark(?:[ _-]*flat)?|flat|bias|offset)(?:[ _-]|$)",RegexOptions.IgnoreCase)||Regex.IsMatch(header.Get("IMAGETYP","IMAGETYPE","FRAME","FRAMETYP"),@"dark|flat|bias|offset",RegexOptions.IgnoreCase)){frame.Kind="Dark";frame.Target="Calibration";}
   else if((header.Number("NCOMBINE","STACKCNT","NSTACK","STACKNUM","NSUBS","SUBCOUNT")??0)>1||Regex.IsMatch(kind,@"(?:^|[ _-])(?:stack|stacked|restacked)(?:[ _-]|$)|\d+x\d+(?:\.\d+)?s",RegexOptions.IgnoreCase))frame.Kind="Stack";
   if(EditedMetadata.Read(filename,header).TotalExposure.HasValue&&!IsCalibration(frame))frame.Kind="Stack";
   frame.ObservedUtc=CaptureUtc(frame).HasValue?CaptureUtc(frame).Value.ToString("o",CultureInfo.InvariantCulture):null;return frame;
  }
  public static DateTime? CaptureUtc(Frame frame,Settings settings=null){
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
   }
   // Older non-FITS indexes can retain a valid header clock while ObservedUtc
   // is empty. Recover from indexed metadata only, without reopening a video.
   bool userClock=false;foreach(string key in new[]{"Observed","ObservedUtc","TimeZoneId"}){MetadataFact fact;if(frame.Facts!=null&&frame.Facts.TryGetValue(key,out fact)&&fact!=null&&fact.Source=="User")userClock=true;}
   var image=frame.Images==null?null:frame.Images.FirstOrDefault(i=>i.Key==frame.ImageKey)??frame.Images.FirstOrDefault();
   if(!userClock&&image!=null&&image.Headers!=null){
    var header=new FitsHeader{Values=image.Headers};string text=header.Get("DATE-OBS","DATEOBS","DATE_OBS"),system=header.Get("TIMESYS");
    bool utcStandard=system.Equals("UTC",StringComparison.OrdinalIgnoreCase)||frame.Format=="FITS";
    bool zoned=Regex.IsMatch(text,@"(?:Z|[+-]\d\d:\d\d)$",RegexOptions.IgnoreCase);
    DateTimeOffset utc;if((system.Length==0||system.Equals("UTC",StringComparison.OrdinalIgnoreCase))&&(utcStandard||zoned)&&Regex.IsMatch(text,@"[T ]\d\d:\d\d")&&DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out utc))return utc.UtcDateTime;
   }
   // Last resort: a selected observing town supplies historical timezone/DST
   // rules for a real but unzoned capture time. Explicit metadata always wins.
   if(settings!=null&&settings.ObservingCityId.HasValue&&string.IsNullOrEmpty(frame.TimeZoneId)&&!(frame.TimeSource??"").StartsWith("Capture clock ",StringComparison.OrdinalIgnoreCase)){
    string standard=image==null||image.Headers==null?"":new FitsHeader{Values=image.Headers}.Get("TIMESYS");
    if(standard.Length>0&&!standard.Equals("UTC",StringComparison.OrdinalIgnoreCase))return null;
    string text=frame.Observed;DateTime local;
    if(!string.IsNullOrEmpty(text)&&Regex.IsMatch(text,@"[T ]\d\d:\d\d")&&!Regex.IsMatch(text,@"(?:Z|[+-]\d\d:\d\d)$",RegexOptions.IgnoreCase)&&DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out local)){
     var zone=CityTimeZones.ForCity(settings.ObservingCityId.Value);local=DateTime.SpecifyKind(local,DateTimeKind.Unspecified);
     if(zone!=null&&!zone.IsInvalidTime(local)&&!zone.IsAmbiguousTime(local))return TimeZoneInfo.ConvertTimeToUtc(local,zone);
    }
   }
   return null;
  }
  static bool HasClock(Frame frame){return Regex.IsMatch(frame.Observed??"",@"[T ]\d\d:\d\d");}
  // UTC is assumed for an unzoned clock; a date alone is taken at noon. Session-folder dates still count as dates.
  static DateTime? RoughMoonTime(Frame frame){
   if(frame==null||IsCalibration(frame))return null;DateTime parsed;
   if(HasClock(frame)&&!(frame.TimeSource??"").StartsWith("Session folder",StringComparison.OrdinalIgnoreCase)&&DateTime.TryParse(frame.Observed,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out parsed))return DateTime.SpecifyKind(parsed,DateTimeKind.Utc);
   var date=CaptureSessions.Date(frame);return date==null?(DateTime?)null:DateTime.SpecifyKind(date.Date.Date.AddHours(12),DateTimeKind.Utc);
  }
  public static CaptureSky Resolve(Frame frame,Settings settings){
   var sky=new CaptureSky{TargetLabel=frame==null?"Select a capture":Catalog.IsAmbiguous(frame.Target)?"Capture position":frame.TargetLabel,Evidence=""};
   if(frame!=null){
    if(frame.Sky!=null&&Position(frame.Sky.RA,frame.Sky.Dec)){sky.RA=frame.Sky.RA;sky.Dec=frame.Sky.Dec;sky.HasPosition=true;sky.ApproximatePosition=frame.Sky.Approximate;sky.Evidence=frame.Sky.Evidence??"Frame pointing";}
    else if(frame.RA.HasValue&&frame.Dec.HasValue&&Position(frame.RA.Value,frame.Dec.Value)){sky.RA=frame.RA.Value;sky.Dec=frame.Dec.Value;sky.HasPosition=true;sky.Evidence="Frame pointing";}
    else{CatalogObject target;if(positions.Value.TryGetValue(Catalog.CanonicalTarget(frame.Target),out target)){sky.RA=target.RA;sky.Dec=target.Dec;sky.HasPosition=true;sky.ApproximatePosition=true;sky.Evidence="Approximate target centre from the catalogue";}}
   }
   string body=frame==null||IsCalibration(frame)?null:ObservationTargets.NamedSolar(frame.Target)??(Catalog.IsAmbiguous(frame.Target)?ObservationTargets.NamedSolar(ObservationTargets.ModeBody(frame.ObservationMode)):null);sky.Body=body;
   sky.Utc=CaptureUtc(frame,settings);bool site=false;string place="";
   if(frame!=null&&frame.Latitude.HasValue&&frame.Longitude.HasValue&&ObservingCities.ValidCoordinates(frame.Latitude.Value,frame.Longitude.Value)){sky.Latitude=frame.Latitude.Value;sky.Longitude=frame.Longitude.Value;site=true;place="Capture location";}
   else if(settings!=null&&settings.Latitude.HasValue&&settings.Longitude.HasValue&&ObservingCities.ValidCoordinates(settings.Latitude.Value,settings.Longitude.Value)){sky.Latitude=settings.Latitude.Value;sky.Longitude=settings.Longitude.Value;site=true;place=settings.ObservingCity??"Saved observing location";}
   sky.HasHorizon=site&&sky.Utc.HasValue;sky.Orientation=new SkyOrientation(sky.Utc,sky.HasHorizon,sky.Latitude,sky.Longitude);
   if(sky.Utc.HasValue)sky.TimeLabel=sky.Utc.Value.ToString("dd/MM/yy HH:mm:ss 'UTC'",CultureInfo.InvariantCulture);
   else{var date=frame==null?null:CaptureSessions.Date(frame);sky.TimeLabel=date==null?"Capture time unknown":date.Text+" · time / timezone unknown";}
   if(sky.Utc.HasValue){
    var moon=MoonEphemeris.At(sky.Utc.Value,site,sky.Latitude,sky.Longitude);moon.HasHorizon=sky.HasHorizon;
    if(sky.HasHorizon){var local=sky.Orientation.MapOfDate(moon.Topocentric);moon.Altitude=Math.Asin(Math.Max(-1,Math.Min(1,local.Z)))*180/Math.PI;moon.Azimuth=SkyOrientation.Wrap(Math.Atan2(local.X,local.Y)*180/Math.PI);}
    sky.Moon=moon;
   }
   // Lunar, solar and planetary modes often write a blank mount pointing (0°, 0°).
   // That is not a measurement, so the calculated body position replaces it;
   // any other recorded or solved pointing keeps priority.
   bool pointingReplaced=false;
   DateTime? roughTime=!sky.Utc.HasValue&&body=="Moon"?RoughMoonTime(frame):null;
   if(sky.HasPosition&&(sky.Utc.HasValue||roughTime.HasValue)&&body!=null&&Math.Abs(sky.RA)<1e-6&&Math.Abs(sky.Dec)<1e-6){sky.HasPosition=false;pointingReplaced=true;}
   // The Moon moves only about 0.5° an hour against the stars, so an unzoned clock or a bare
   // date still places it within a few degrees on the celestial map (never on a horizon).
   if(!sky.HasPosition&&roughTime.HasValue){
    var rough=MoonEphemeris.At(roughTime.Value,false,0,0).Geocentric;
    sky.RA=SkyOrientation.Wrap(Math.Atan2(rough.Y,rough.X)*180/Math.PI);sky.Dec=Math.Asin(Math.Max(-1,Math.Min(1,rough.Z)))*180/Math.PI;sky.HasPosition=true;sky.PositionOfDate=true;sky.ApproximatePosition=true;
    sky.Evidence="Approximate Moon position (geocentric) from the recorded capture "+(HasClock(frame)?"time, timezone unknown":"date")+" - good to a few degrees\nMeeus lunar/solar model"+(pointingReplaced?"\nThe file's recorded pointing was blank (0°, 0°) and is not used.":"");
   }
   if(!sky.HasPosition&&sky.Utc.HasValue&&body!=null){
    SkyVector direction=new SkyVector();bool found=false;double distance;
    if(body=="Moon"&&sky.Moon!=null){direction=sky.Moon.Topocentric;found=true;}
    else if(body=="Sun"&&sky.Moon!=null){direction=sky.Moon.Sun;double sunLongitude,sunDistance;MoonEphemeris.Sun(sky.Utc.Value,out sunLongitude,out sunDistance);if(site)direction=PlanetEphemeris.Topocentric(direction,sunDistance/149597870.7,sky.Utc.Value,sky.Latitude,sky.Longitude);found=true;}
    else if(PlanetEphemeris.TryAt(body,sky.Utc.Value,out direction,out distance)){direction=sky.Orientation.Precess(direction);if(site)direction=PlanetEphemeris.Topocentric(direction,distance,sky.Utc.Value,sky.Latitude,sky.Longitude);found=true;}
    if(found){sky.RA=SkyOrientation.Wrap(Math.Atan2(direction.Y,direction.X)*180/Math.PI);sky.Dec=Math.Asin(Math.Max(-1,Math.Min(1,direction.Z)))*180/Math.PI;sky.HasPosition=true;sky.PositionOfDate=true;sky.ApproximatePosition=true;sky.Evidence="Approximate "+body+" position calculated at the recorded capture time"+(site?" (observer location)":" (geocentric)")+"\n"+(body=="Moon"||body=="Sun"?"Meeus lunar/solar model":"JPL approximate Keplerian model · 1800–2050")+(pointingReplaced?"\nThe file's recorded pointing was blank (0°, 0°) and is not used.":"");}
   }
   if(sky.Utc.HasValue&&body!=null)sky.Eclipse=Eclipses.For(body,sky.Utc,site,sky.Latitude,sky.Longitude);
   if(sky.Moon!=null&&sky.HasHorizon){var sunLocal=sky.Orientation.MapOfDate(sky.Moon.Sun);sky.SunAltitude=Math.Asin(Math.Max(-1,Math.Min(1,sunLocal.Z)))*180/Math.PI;}
   sky.Daytime=body=="Sun"||sky.Eclipse!=null&&sky.Eclipse.Solar||sky.SunAltitude>-0.833;
   if(sky.Moon!=null&&sky.HasPosition){sky.Moon.HasTarget=true;sky.Moon.TargetSeparation=MoonEphemeris.Separation(sky.TargetOfDate,sky.Moon.Topocentric);}
   string hemisphere=sky.Dec>0?"Northern celestial sky":sky.Dec<0?"Southern celestial sky":"Celestial equator";
   if(sky.HasPosition&&sky.HasHorizon){
    var target=sky.TargetOnGlobe;sky.Altitude=Math.Asin(Math.Max(-1,Math.Min(1,target.Z)))*180/Math.PI;sky.Azimuth=SkyOrientation.Wrap(Math.Atan2(target.X,target.Y)*180/Math.PI);
    sky.Summary=(sky.ApproximatePosition?"≈ ":"")+"Alt "+sky.Altitude.ToString("0",CultureInfo.InvariantCulture)+"° · "+Compass(sky.Azimuth)+" "+sky.Azimuth.ToString("0",CultureInfo.InvariantCulture)+"°"+(sky.Altitude<0?" · below horizon":"");
    if(body!=null&&sky.Altitude<0){sky.BodyBelowHorizon=true;sky.Summary+=" · check the capture timezone";}
   }else sky.Summary=!sky.HasPosition?(body!=null&&!sky.Utc.HasValue?"Capture time / timezone required":"Pointing unavailable"):!site?"Celestial view · set an observing location":"Celestial view · capture time unknown";
   sky.Evidence+=(sky.Evidence.Length>0?"\n":"")+sky.TimeLabel+"\n"+(site?place+" · "+sky.Latitude.ToString("0.###",CultureInfo.InvariantCulture)+"°, "+sky.Longitude.ToString("0.###",CultureInfo.InvariantCulture)+"°":"Choose your observing town / city in Settings for the local horizon.");
   if(!sky.HasPosition&&body!=null)sky.Evidence+="\n"+(!sky.Utc.HasValue?"A moving target needs a capture time with a known timezone. Set the capture timezone in metadata if needed.":"No offline ephemeris is available for this body/date.");
   if(sky.BodyBelowHorizon)sky.Evidence+="\nThe "+body+" was below the horizon at this recorded time, so the capture clock or timezone is probably wrong. Set the capture timezone in metadata.";
   if(sky.Eclipse!=null)sky.Evidence+="\n"+sky.Eclipse.Title+": "+sky.Eclipse.Summary+(sky.Eclipse.Solar?" (from the observing location)":"")+".";
   if(sky.HasPosition)sky.Evidence+="\n"+hemisphere+" · RA "+sky.RA.ToString("0.###",CultureInfo.InvariantCulture)+"°, Dec "+sky.Dec.ToString("0.###",CultureInfo.InvariantCulture)+"° · "+(sky.PositionOfDate?"equatorial coordinates of capture date":"J2000 equatorial coordinates")+".";
   sky.Evidence+="\n"+(sky.HasHorizon?"Sky at the recorded capture time. Solid horizon; faint figures lie below it. Geometric altitude excludes atmospheric refraction.":"Celestial coordinates only. No compass horizon is inferred without a known capture time and observing location.");
   sky.Key=string.Join("|",new[]{sky.HasPosition.ToString(),sky.HasHorizon.ToString(),sky.PositionOfDate.ToString(),sky.RA.ToString("R",CultureInfo.InvariantCulture),sky.Dec.ToString("R",CultureInfo.InvariantCulture),sky.Latitude.ToString("R",CultureInfo.InvariantCulture),sky.Longitude.ToString("R",CultureInfo.InvariantCulture),sky.Utc.HasValue?sky.Utc.Value.Ticks.ToString(CultureInfo.InvariantCulture):""});return sky;
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
  public SkyVector Map(SkyVector vector){return MapOfDate(Precess(vector));}
  // J2000 catalogue direction to the equator and equinox of the capture date.
  public SkyVector Precess(SkyVector vector){
   double a=cq*vector.X-sq*vector.Y,b=sq*vector.X+cq*vector.Y,c=ct*a-st*vector.Z;
   return new SkyVector(cz*c-sz*b,sz*c+cz*b,st*a+ct*vector.Z);
  }
  // Directions already of date (Moon and Sun) skip precession.
  public SkyVector MapOfDate(SkyVector v){
   if(!horizon)return v;double meridian=cl*v.X+sl*v.Y;
   return new SkyVector(-sl*v.X+cl*v.Y,-sp*meridian+cp*v.Z,cp*meridian+sp*v.Z);
  }
 }
}
