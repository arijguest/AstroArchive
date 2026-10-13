// C# 5 / .NET Framework 4.8. Celestron Origin capture layout and metadata.
// Sources: Celestron's Origin manual §12 and "Downloading Raw Image Files from Celestron Origin via WiFi"
// (anonymous FTP at origin.local:21; one folder per object and date holding raw FITS from core 1.2.5059,
// TIFF before that, plus calibration frames); headers and info.json fields as read by the open-source
// OriginStack project: CAMERA = 'Origin178-<unit id>', CREATOR = 'Origin <core version>', DATE-OBS in
// local time with the UTC offset in a separate TIMEZONE keyword, Light0001.fits / bias0009.fits names
// and the onboard FinalStackedMaster.tiff.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public static class CelestronOrigin {
  public const string Make="Celestron",Model="Celestron Origin",SessionInfo="info.json",StackedMaster="FinalStackedMaster";
  // Session folders are "<Object_Name>_YYYY-MM-DD_HH-MM-SS", e.g. Orion_Nebula_2025-02-11_20-22-01.
  static readonly Regex SessionFolder=new Regex(@"^(?<name>.+?)_(?<date>\d{4}-\d{2}-\d{2})_(?<time>\d{2}-\d{2}-\d{2})$",RegexOptions.CultureInvariant);
  static readonly Regex FrameName=new Regex(@"^(?<kind>light|bias|dark|flat)(?<n>\d{1,6})\.(?:fits?|fts|tiff?)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  static readonly Regex Camera=new Regex(@"^\s*Origin\d*-(?<unit>[0-9a-f]{8,})\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

  public static bool CameraHeader(string camera){return Camera.IsMatch(camera??"");}
  public static string UnitId(string camera){var m=Camera.Match(camera??"");return m.Success?m.Groups["unit"].Value.ToLowerInvariant():null;}
  public static bool IsStackedMaster(string name){return Regex.IsMatch(name??"",@"^FinalStackedMaster\.(?:tiff?|fits?)$",RegexOptions.IgnoreCase);}
  // "Light", "Bias", "Dark" or "Flat" for Origin's numbered frame names; null for anything else.
  public static string FrameKind(string name){var m=FrameName.Match(name??"");if(!m.Success)return null;string kind=m.Groups["kind"].Value.ToLowerInvariant();return char.ToUpperInvariant(kind[0])+kind.Substring(1);}
  // Object name from a session folder, with underscores read as spaces; null when the folder is not Origin's.
  public static string SessionTarget(string folder){var m=SessionFolder.Match(folder??"");return m.Success?m.Groups["name"].Value.Replace('_',' ').Trim():null;}
  // Folder signature for files without Origin headers (TIFF raws from older cores, the stacked master):
  // an Origin-named file directly inside an Origin session folder.
  public static bool Layout(string context){
   string[] parts=(context??"").Replace('\\','/').Split(new[]{'/'},StringSplitOptions.RemoveEmptyEntries);if(parts.Length<2)return false;
   string file=parts[parts.Length-1],folder=parts[parts.Length-2];
   return (FrameKind(file)!=null||IsStackedMaster(file))&&SessionTarget(folder)!=null;
  }

  // Parses a TIMEZONE value such as -0700, -07:00 or +5:30; anything else (PDT, empty) is unusable.
  public static bool Offset(string text,out TimeSpan offset){
   offset=TimeSpan.Zero;var m=Regex.Match((text??"").Trim().Trim('\''),@"^(?<sign>[+-])(?<h>\d{1,2}):?(?<m>\d{2})$");if(!m.Success)return false;
   int hours=int.Parse(m.Groups["h"].Value,CultureInfo.InvariantCulture),minutes=int.Parse(m.Groups["m"].Value,CultureInfo.InvariantCulture);if(hours>14||minutes>59)return false;
   offset=new TimeSpan(hours,minutes,0);if(m.Groups["sign"].Value=="-")offset=-offset;return true;
  }
  // Reads an Origin capture time: DATE-OBS is local unless it carries its own offset or Z; TIMEZONE gives the offset.
  public static bool CaptureTime(string dateObs,string timezone,out DateTime utc,out DateTime local){
   utc=local=default(DateTime);string text=(dateObs??"").Trim().Trim('\'');if(text.Length<=10)return false;
   if(text.EndsWith("Z",StringComparison.OrdinalIgnoreCase))return false;
   var own=Regex.Match(text,@"(?<time>T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?)(?<offset>[+-]\d{2}:?\d{2})$");TimeSpan offset;
   if(own.Success){if(!Offset(own.Groups["offset"].Value,out offset))return false;text=text.Substring(0,own.Groups["offset"].Index);}
   else if(!Offset(timezone,out offset))return false;
   if(!DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out local))return false;
   local=DateTime.SpecifyKind(local,DateTimeKind.Unspecified);utc=DateTime.SpecifyKind(local-offset,DateTimeKind.Utc);return true;
  }

  // The session info.json beside a capture, flattened to the fields AstroArchive uses; empty when absent or unreadable.
  public sealed class Session {public string Object,DateTime;public double? Latitude,Longitude,RaDegrees,DecDegrees,TotalSeconds;public int? StackedDepth;public string Path;}
  public static Session ReadSession(string folder){
   var session=new Session();if(string.IsNullOrEmpty(folder))return session;string path=System.IO.Path.Combine(folder,SessionInfo);
   try{if(!File.Exists(path)||new FileInfo(path).Length>1024*1024)return session;var raw=Util.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));return Parse(raw,path);}
   catch(Exception e){if(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException)return session;throw;}
  }
  public static Session Parse(Dictionary<string,object> raw,string path=null){
   var session=new Session{Path=path};if(raw==null)return session;object nested;var d=raw.TryGetValue("StackedInfo",out nested)&&nested is Dictionary<string,object>?(Dictionary<string,object>)nested:raw;
   session.Object=Text(d,"objectName");session.DateTime=Text(d,"dateTime");
   double? depth=Number(d,"stackedDepth");if(depth.HasValue&&depth.Value>=1&&depth.Value<=1000000&&depth.Value==Math.Floor(depth.Value))session.StackedDepth=(int)depth.Value;
   double? ms=Number(d,"totalDurationMs");if(ms.HasValue&&ms.Value>0)session.TotalSeconds=ms.Value/1000;
   var gps=Child(d,"gps");if(gps!=null){double? lat=Number(gps,"latitude"),lon=Number(gps,"longitude");if(lat.HasValue&&lon.HasValue&&Math.Abs(lat.Value)<=90&&Math.Abs(lon.Value)<=180&&!(lat.Value==0&&lon.Value==0)){session.Latitude=lat;session.Longitude=lon;}}
   // celestial.first/second are the session's RA/Dec in radians (OriginStack session_info.py).
   var sky=Child(d,"celestial");if(sky!=null){double? ra=Number(sky,"first"),dec=Number(sky,"second");if(ra.HasValue&&dec.HasValue&&Math.Abs(dec.Value)<=Math.PI/2+1e-9){double deg=ra.Value*180/Math.PI;session.RaDegrees=((deg%360)+360)%360;session.DecDegrees=dec.Value*180/Math.PI;}}
   return session;
  }
  static Dictionary<string,object> Child(Dictionary<string,object> d,string key){object value;return d.TryGetValue(key,out value)?value as Dictionary<string,object>:null;}
  static string Text(Dictionary<string,object> d,string key){object value;return d.TryGetValue(key,out value)&&value is string?((string)value).Trim():null;}
  static double? Number(Dictionary<string,object> d,string key){object value;if(!d.TryGetValue(key,out value)||value==null||value is string||value is bool)return null;try{double n=Convert.ToDouble(value,CultureInfo.InvariantCulture);return double.IsNaN(n)||double.IsInfinity(n)?(double?)null:n;}catch(Exception e){if(e is FormatException||e is InvalidCastException||e is OverflowException)return null;throw;}}

  static void Fact(Frame f,string field,string value,string raw,string source,string unit=null){if(f.Facts==null)f.Facts=new Dictionary<string,MetadataFact>();f.Facts[field]=new MetadataFact{Value=value,Raw=raw,Source=source,Unit=unit};}
  static bool User(Frame f,string field){MetadataFact fact;return f.Facts!=null&&f.Facts.TryGetValue(field,out fact)&&fact!=null&&fact.Source=="User";}

  // Origin-specific readings applied after generic classification, only to frames already identified as Origin.
  public static void Apply(Frame f,FitsHeader h,string sourcePath,Session session=null){
   if(f==null||f.Make!=Make)return;f.DeviceProfile="celestron-origin-1";
   string name=f.OriginalName??Path.GetFileName(sourcePath??""),folder=Path.GetDirectoryName(sourcePath??"")??"";
   if(session==null)session=ReadSession(folder);
   string unit=UnitId(h==null?null:h.Get("CAMERA"));if(unit!=null&&string.IsNullOrEmpty(f.CameraId)){f.CameraId=unit;Fact(f,"CameraId",unit,h.Get("CAMERA"),"Celestron Origin CAMERA header (model-unit)");}
   // Frame type: Origin numbers its frames without a separator (Light0001, bias0009), which generic name rules miss.
   string kind=FrameKind(name);
   if(IsStackedMaster(name)){f.Kind="Stack";f.Calibration="Device stack";}
   else if(kind=="Light"&&(f.Kind=="Unknown"||f.Kind=="Light"))f.Kind="Light";
   else if(kind!=null&&kind!="Light"&&(f.Kind=="Unknown"||f.Kind=="Light")){f.Kind=kind;f.Calibration="Calibration frame";f.Target="Calibration";f.TargetEvidence="Calibration frame";}
   // Target: the session's object name, from info.json or else the session folder.
   if(f.Target!="Calibration"&&(Catalog.IsAmbiguous(f.Target)||(f.TargetEvidence??"").StartsWith("Unrecognised"))&&!User(f,"Target")){
    string label=!Catalog.IsAmbiguous(session.Object)?session.Object:SessionTarget(Path.GetFileName(folder));
    if(!Catalog.IsAmbiguous(label)){string known=Catalog.KnownName(label);f.Target=Catalog.Normalize(label);f.TargetEvidence=known!=null||CometTargets.IsComet(f.Target)?"Recognised Celestron Origin session target":"Unrecognised label; plate solving required";if(f.Notes!=null)f.Notes=f.Notes.Replace("Target needs identification. ","");}
   }
   // Time: Origin writes local DATE-OBS with a separate TIMEZONE; convert so downstream UTC handling is right.
   if(h!=null&&!User(f,"Observed")){DateTime utc,local;string raw=h.Get("DATE-OBS","DATEOBS","DATE_OBS");
    if(CaptureTime(raw,h.Get("TIMEZONE"),out utc,out local)){f.Observed=utc.ToString("yyyy-MM-ddTHH:mm:ss.fff",CultureInfo.InvariantCulture);f.TimeSource="FITS UTC";f.Night=local.AddHours(-12).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);f.AcquisitionDate=local.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);f.AcquisitionDateSource="Celestron Origin local time";Fact(f,"Observed",f.Observed,raw+" "+h.Get("TIMEZONE"),"Celestron Origin DATE-OBS (local) with TIMEZONE offset","UTC");}
   }
   if(!f.Latitude.HasValue&&!f.Longitude.HasValue&&session.Latitude.HasValue){f.Latitude=session.Latitude;f.Longitude=session.Longitude;Fact(f,"Latitude",session.Latitude.Value.ToString("R",CultureInfo.InvariantCulture),null,"Celestron Origin info.json GPS","deg");Fact(f,"Longitude",session.Longitude.Value.ToString("R",CultureInfo.InvariantCulture),null,"Celestron Origin info.json GPS","deg");}
   if(f.Target!="Calibration"&&!f.RA.HasValue&&!f.Dec.HasValue&&session.RaDegrees.HasValue){f.RA=session.RaDegrees;f.Dec=session.DecDegrees;}
   // The onboard stack records how many subs it combined and their total duration.
   if(f.Kind=="Stack"&&IsStackedMaster(name)){
    if(session.StackedDepth.HasValue&&f.StackCount<=0&&!User(f,"StackCount")){f.StackCount=session.StackedDepth.Value;Fact(f,"StackCount",f.StackCount.ToString(CultureInfo.InvariantCulture),null,"Celestron Origin info.json stackedDepth");}
    if(session.TotalSeconds.HasValue&&!f.Exposure.HasValue&&!User(f,"Exposure")){f.Exposure=session.TotalSeconds;Fact(f,"Exposure",session.TotalSeconds.Value.ToString("R",CultureInfo.InvariantCulture),null,"Celestron Origin info.json totalDurationMs","s");}
   }
   // Keep info.json with the archived capture, as other session sidecars are.
   if(f.AssociatedFiles!=null&&session.Path!=null&&File.Exists(session.Path)&&!f.AssociatedFiles.Any(a=>string.Equals(a.SourcePath,session.Path,StringComparison.OrdinalIgnoreCase))){var stamp=FileStamp.Read(session.Path);if(stamp.Size<=16*1024*1024)f.AssociatedFiles.Add(new AssociatedFile{SourcePath=session.Path,Stamp=stamp,Role="Capture / session sidecar"});}
  }
 }
}
