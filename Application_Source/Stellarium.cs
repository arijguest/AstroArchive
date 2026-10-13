// "Open in Stellarium": builds a small Stellarium script from a capture's sky context and hands it to a running copy
// (Remote Control on this PC) or to a newly started copy. Only when Stellarium is not installed does the view go to
// Stellarium Web, as URL parameters.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Win32;

namespace AstroArchive {
 public sealed class StellariumView {
  public double RA,Dec;public double? FieldDegrees,Latitude,Longitude,Altitude,Azimuth;public DateTime? Utc;public bool Approximate;public string Name;
  // Solar System bodies move, so Stellarium finds them by name at its current time rather than at a fixed position.
  public bool ByName;
 }
 public static class Stellarium {
  public const string Id="stellarium";
  public const int RemotePort=8090;
  const double DefaultFieldDegrees=5,FieldMargin=1.2,MinField=0.05,MaxField=180;
  static readonly DateTime JulianEpoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);

  // Null when there is nothing meaningful to show: no frame, a calibration frame, or no known position.
  public static StellariumView From(CaptureSky sky,Frame frame){
   if(sky==null||frame==null||CaptureSky.IsCalibration(frame)||!sky.HasPosition||!CaptureSky.Finite(sky.RA)||!CaptureSky.Finite(sky.Dec))return null;
   var view=new StellariumView{RA=sky.RA,Dec=sky.Dec,Approximate=sky.ApproximatePosition,FieldDegrees=Field(frame)};
   if(sky.Utc.HasValue)view.Utc=sky.Utc.Value.Kind==DateTimeKind.Utc?sky.Utc.Value:sky.Utc.Value.ToUniversalTime();
   if(sky.HasHorizon&&ObservingCities.ValidCoordinates(sky.Latitude,sky.Longitude)){view.Latitude=sky.Latitude;view.Longitude=sky.Longitude;view.Altitude=sky.Altitude;view.Azimuth=sky.Azimuth;}
   if(!Catalog.IsAmbiguous(frame.Target))view.Name=Catalog.CanonicalTarget(frame.Target);
   return view;
  }
  // The larger side of the solved or WCS footprint, with a little margin; null when the frame has no field of view.
  public static double? Field(Frame frame){
   var sky=frame==null?null:frame.Sky;if(sky==null)return null;
   double side=Math.Max(sky.WidthDegrees,sky.HeightDegrees);if(!CaptureSky.Finite(side)||side<=0)return null;
   return Math.Max(MinField,Math.Min(MaxField,side*FieldMargin));
  }
  // A target information page: deep-sky objects open at their J2000 catalogue position, framed to their size;
  // the Sun, Moon, planets and comets open by name. Eclipse pages show the eclipsed body. Groups have no single target.
  public static StellariumView ForCard(TargetInfoCard card){
   if(card==null||string.IsNullOrWhiteSpace(card.id))return null;
   var measurements=card.measurements??new TargetInfoMeasurement[0];
   if(card.domain=="deep_sky"){
    var ra=measurements.FirstOrDefault(m=>m.field=="RA");var dec=measurements.FirstOrDefault(m=>m.field=="Dec");if(ra==null||dec==null||!CaptureSky.Finite(ra.value)||!CaptureSky.Finite(dec.value))return null;
    var major=measurements.FirstOrDefault(m=>m.field=="MajAx");double? field=major!=null&&CaptureSky.Finite(major.value)&&major.value>0?Math.Max(0.5,Math.Min(MaxField,major.value/60*3)):(double?)null;
    return new StellariumView{RA=ra.value,Dec=dec.value,FieldDegrees=field??DefaultFieldDegrees,Name=card.id.Trim()};
   }
   string id=card.id.Trim().ToUpperInvariant();
   if(id==EclipseCard(true))return Body("Sun",2);
   if(id==EclipseCard(false))return Body("Moon",2);
   if(card.domain!="solar_system")return null;
   if(CometInfo.IsComet(card))return Body(CometName(card),5);
   return Body(card.name??card.id,id=="SUN"||id=="MOON"?2:0.5);
  }
  // Stellarium lists comets as "12P/Pons-Brooks" or "C/2023 A3 (Tsuchinshan-ATLAS)"; the pack holds the designation and name apart.
  static string CometName(TargetInfoCard card){
   string id=card.id.Trim(),name=System.Text.RegularExpressions.Regex.Replace((card.name??"").Trim(),@"^Comet\s+","");
   if(name.Length==0||name.IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0)return name.Length==0?id:name;
   if(char.IsDigit(id[0])){var alias=(card.aliases??new string[0]).FirstOrDefault(a=>a.StartsWith(id+"/",StringComparison.OrdinalIgnoreCase));return alias??id+"/"+name;}
   return id+" ("+name+")";
  }
  public static string EclipseCard(bool solar){return solar?"SOLAR ECLIPSE":"LUNAR ECLIPSE";}
  static StellariumView Body(string name,double field){return new StellariumView{ByName=true,Name=name.Trim(),FieldDegrees=field};}
  // Same glyph as PreviewStellariumIcon in MainWindow.xaml.
  public const string IconGeometry="M13,2 A8,8 0 1 0 18,13 A6.5,6.5 0 0 1 13,2 Z M4,2 L4,6 M2,4 L6,4";
  public static string CardTooltip(StellariumView view){return view.ByName?"Open in Stellarium, centred on this object now. Uses Stellarium Web when Stellarium is not installed.":"Open in Stellarium at the catalogue position. Uses Stellarium Web when Stellarium is not installed.";}
  public static string Tooltip(StellariumView view){
   if(view==null)return "Open in Stellarium (needs a light frame with a known position)";
   var text=new StringBuilder("Open in Stellarium");
   text.Append(view.Approximate?" at the approximate target centre":" at this capture's position");
   if(view.Utc.HasValue&&view.Latitude.HasValue)text.Append(", capture time and place");
   else if(view.Utc.HasValue)text.Append(" and capture time");
   else text.Append("; capture time unknown, so position only");
   return text.ToString();
  }
  public static double JulianDay(DateTime utc){
   if(utc.Kind!=DateTimeKind.Utc)utc=utc.ToUniversalTime();
   return 2440587.5+(utc-JulianEpoch).TotalDays;
  }
  // Stellarium's script parser reads angle strings like 5h35m17.30s and -5d23m28.0s.
  public static string Hours(double degrees){
   long hundredths=(long)Math.Round(((degrees%360+360)%360)/15*360000)%8640000;
   long h=hundredths/360000,m=hundredths/6000%60,s=hundredths%6000;
   return h.ToString(CultureInfo.InvariantCulture)+"h"+m.ToString(CultureInfo.InvariantCulture)+"m"+(s/100.0).ToString("0.00",CultureInfo.InvariantCulture)+"s";
  }
  public static string Degrees(double degrees){
   long tenths=(long)Math.Round(Math.Abs(Math.Max(-90,Math.Min(90,degrees)))*36000);
   long d=tenths/36000,m=tenths/600%60,s=tenths%600;
   return (degrees<0?"-":"+")+d.ToString(CultureInfo.InvariantCulture)+"d"+m.ToString(CultureInfo.InvariantCulture)+"m"+(s/10.0).ToString("0.0",CultureInfo.InvariantCulture)+"s";
  }
  // Names go inside a double-quoted script string.
  static string Quoted(string value){return (value??"").Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r"," ").Replace("\n"," ");}
  static string Number(double value){return value.ToString("0.######",CultureInfo.InvariantCulture);}
  // Only touches location, time and view. Time is frozen so the sky stays as it was at capture.
  public static string Script(StellariumView view){
   var text=new StringBuilder();
   if(view.Latitude.HasValue&&view.Longitude.HasValue)text.Append("core.setObserverLocation(").Append(Number(view.Longitude.Value)).Append(", ").Append(Number(view.Latitude.Value)).Append(", 0, 0, \"\", \"Earth\");\n");
   if(view.Utc.HasValue)text.Append("core.setTimeRate(0);\ncore.setDate(\"").Append(view.Utc.Value.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture)).Append("\", \"utc\");\n");
   if(view.ByName)text.Append("core.selectObjectByName(\"").Append(Quoted(view.Name)).Append("\", true);\nStelMovementMgr.setFlagTracking(true);\n");
   else text.Append("core.moveToRaDecJ2000(\"").Append(Hours(view.RA)).Append("\", \"").Append(Degrees(view.Dec)).Append("\", 0);\n");
   text.Append("StelMovementMgr.zoomTo(").Append(Number(view.FieldDegrees ?? DefaultFieldDegrees)).Append(", 0);\n");
   return text.ToString();
  }
  public const string WebAddress="https://stellarium-web.org/";
  // Stellarium Web reads date, lat/lng, az/alt (view direction, degrees) and fov from the query, and selects the object
  // named in a /skysource/ path. It has no RA/Dec parameter, so pointing needs the capture horizon or a catalogue name.
  public static string WebUrl(StellariumView view){
   var query=new List<string>{"fov="+Number(view.FieldDegrees ?? DefaultFieldDegrees)};
   if(view.Utc.HasValue)query.Add("date="+view.Utc.Value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture));
   if(view.Latitude.HasValue&&view.Longitude.HasValue){query.Add("lat="+Number(view.Latitude.Value));query.Add("lng="+Number(view.Longitude.Value));}
   if(view.Altitude.HasValue&&view.Azimuth.HasValue){query.Add("az="+Number(view.Azimuth.Value));query.Add("alt="+Number(view.Altitude.Value));}
   return WebAddress+(string.IsNullOrWhiteSpace(view.Name)?"":"skysource/"+Uri.EscapeDataString(view.Name.Trim()))+"?"+string.Join("&",query);
  }
  // Remote Control is off by default, so a refused connection is the normal "not running" answer.
  public static bool TrySend(string script,int port,int timeoutMs){
   try{
    var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port.ToString(CultureInfo.InvariantCulture)+"/api/scripts/direct");
    request.Method="POST";request.ContentType="application/x-www-form-urlencoded";request.Timeout=timeoutMs;request.ReadWriteTimeout=timeoutMs;request.Proxy=null;request.ServicePoint.Expect100Continue=false;
    byte[] body=Encoding.UTF8.GetBytes("code="+Uri.EscapeDataString(script));request.ContentLength=body.Length;
    using(var stream=request.GetRequestStream())stream.Write(body,0,body.Length);
    using(var response=(HttpWebResponse)request.GetResponse())return (int)response.StatusCode>=200&&(int)response.StatusCode<300;
   }catch(WebException){return false;}catch(IOException){return false;}catch(InvalidOperationException){return false;}
  }
  public static string Configured(Settings settings){
   string path;return settings!=null&&settings.ExternalApplicationPaths!=null&&settings.ExternalApplicationPaths.TryGetValue(Id,out path)?path:null;
  }
  public static void Configure(Settings settings,string path){
   if(settings.ExternalApplicationPaths==null)settings.ExternalApplicationPaths=new Dictionary<string,string>();settings.ExternalApplicationPaths[Id]=path??"";
  }
  public static bool IsExecutable(string path){
   try{return !string.IsNullOrWhiteSpace(path)&&File.Exists(path)&&Path.GetFileName(path).Equals("stellarium.exe",StringComparison.OrdinalIgnoreCase);}catch(ArgumentException){return false;}
  }
  public static string Detect(){
   var candidates=new List<string>();
   foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{using(var root=RegistryKey.OpenBaseKey(hive,view)){
    using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\stellarium.exe")){if(key!=null)candidates.Add(Convert.ToString(key.GetValue(null)).Trim('"'));}
    using(var uninstall=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall")){if(uninstall!=null)foreach(string name in uninstall.GetSubKeyNames())using(var key=uninstall.OpenSubKey(name)){
     if(key==null||!Convert.ToString(key.GetValue("DisplayName")).StartsWith("Stellarium",StringComparison.OrdinalIgnoreCase))continue;
     string location=Convert.ToString(key.GetValue("InstallLocation")).Trim('"');if(location.Length>0)candidates.Add(Path.Combine(location,"stellarium.exe"));
    }}
   }}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}catch(ArgumentException){}
   foreach(var folder in new[]{Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.LocalApplicationData})try{
    string root=Environment.GetFolderPath(folder);if(folder==Environment.SpecialFolder.LocalApplicationData)root=Path.Combine(root,"Programs");
    if(root.Length>0)candidates.Add(Path.Combine(root,"Stellarium","stellarium.exe"));
   }catch(ArgumentException){}
   return candidates.FirstOrDefault(IsExecutable);
  }
  public static string Resolve(Settings settings){string configured=Configured(settings);return IsExecutable(configured)?Path.GetFullPath(configured):Detect();}
  public static string ScriptFolder{get{return Path.Combine(Path.GetTempPath(),"AstroArchive","stellarium");}}
  // Scripts are tiny and only read at start-up; remove day-old ones so the folder does not grow.
  public static string WriteScript(string script,string folder){
   Directory.CreateDirectory(folder);
   try{foreach(string old in Directory.GetFiles(folder,"open-*.ssc"))if(File.GetLastWriteTimeUtc(old)<DateTime.UtcNow.AddDays(-1))File.Delete(old);}catch(IOException){}catch(UnauthorizedAccessException){}
   string path=Path.Combine(folder,"open-"+Guid.NewGuid().ToString("N").Substring(0,12)+".ssc");
   File.WriteAllText(path,script,new UTF8Encoding(false));return path;
  }
  public static ProcessStartInfo LaunchInfo(string executable,string scriptPath){
   if(!IsExecutable(executable))throw new IOException("Locate the Stellarium application (stellarium.exe).");
   return new ProcessStartInfo(Path.GetFullPath(executable),"--startup-script \""+scriptPath+"\""){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(executable))};
  }
  public static void Launch(string executable,string script){
   using(var process=Process.Start(LaunchInfo(executable,WriteScript(script,ScriptFolder))))if(process==null)throw new IOException("Stellarium did not start.");
  }
 }
}
