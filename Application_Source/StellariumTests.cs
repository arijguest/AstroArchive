using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void StellariumTests(){
   Test("Stellarium angles, Julian day and field of view are exact and culture independent",()=>{
    Check(Stellarium.Hours(83.822083)=="5h35m17.30s"&&Stellarium.Hours(0)=="0h0m0.00s"&&Stellarium.Hours(359.9999999)=="0h0m0.00s"&&Stellarium.Hours(-15)=="23h0m0.00s","RA text wrong: "+Stellarium.Hours(83.822083));
    Check(Stellarium.Degrees(-5.391111)=="-5d23m28.0s"&&Stellarium.Degrees(0)=="+0d0m0.0s"&&Stellarium.Degrees(89.99999)=="+90d0m0.0s"&&Stellarium.Degrees(-120)=="-90d0m0.0s","Dec text wrong: "+Stellarium.Degrees(-5.391111));
    Check(Math.Abs(Stellarium.JulianDay(new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc))-2451545.0)<1e-9,"J2000 epoch wrong");
    Check(Stellarium.Field(null)==null&&Stellarium.Field(new Frame())==null,"Missing footprint gave a field");
    var frame=new Frame{Sky=new SkyGeometry{RA=10,Dec=20,WidthDegrees=2,HeightDegrees=3}};
    Check(Math.Abs(Stellarium.Field(frame).Value-3.6)<1e-9,"Field did not use the larger side plus margin");
    frame.Sky.WidthDegrees=0.001;frame.Sky.HeightDegrees=0.001;Check(Stellarium.Field(frame).Value==0.05,"Tiny field not clamped");
    frame.Sky.WidthDegrees=500;Check(Stellarium.Field(frame).Value==180,"Huge field not clamped");
    var old=Thread.CurrentThread.CurrentCulture;
    try{Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");Check(Stellarium.Hours(83.822083)=="5h35m17.30s"&&Stellarium.Script(new StellariumView{RA=10.5,Dec=-5.25,Latitude=-33.5,Longitude=151.25,FieldDegrees=1.5}).Contains("151.25, -33.5"),"Decimal comma leaked into the script");}
    finally{Thread.CurrentThread.CurrentCulture=old;}
   });
   Test("Stellarium script carries only what the capture knows",()=>{
    var settings=new Settings();
    var full=Stellarium.From(CaptureSky.Resolve(SkyFrame(83.822083,-5.391111,-33.8688,151.2093),settings),SkyFrame(83.822083,-5.391111,-33.8688,151.2093));
    Check(full!=null&&full.Utc.HasValue&&full.Latitude.HasValue&&!full.Approximate,"Full capture lost time or place");
    string script=Stellarium.Script(full);
    Check(script.Contains("core.setObserverLocation(151.2093, -33.8688, 0, 0, \"\", \"Earth\");")&&script.Contains("core.setTimeRate(0);")&&script.Contains("core.setDate(\"2000-01-01T12:00:00\", \"utc\");")&&script.Contains("core.moveToRaDecJ2000(\"5h35m17.30s\", \"-5d23m28.0s\", 0);")&&script.Contains("StelMovementMgr.zoomTo(5, 0);"),"Full script wrong:\n"+script);
    var noTime=SkyFrame(83.822083,-5.391111,-33.8688,151.2093);noTime.ObservedUtc=null;noTime.Observed=null;
    var partial=Stellarium.From(CaptureSky.Resolve(noTime,settings),noTime);
    Check(partial!=null&&!partial.Utc.HasValue&&!partial.Latitude.HasValue,"Unknown time still sent time or place");
    script=Stellarium.Script(partial);Check(!script.Contains("setDate")&&!script.Contains("setTimeRate")&&!script.Contains("setObserverLocation")&&script.Contains("moveToRaDecJ2000"),"Position-only script changed time or place:\n"+script);
    Check(Stellarium.Tooltip(partial).Contains("position only")&&Stellarium.Tooltip(full).Contains("time and place")&&Stellarium.Tooltip(null).Contains("needs"),"Tooltip does not say what is sent");
    var calibration=SkyFrame(10,20,0,0);calibration.Kind="Dark";Check(Stellarium.From(CaptureSky.Resolve(calibration,settings),calibration)==null,"Calibration frame can open Stellarium");
    Check(Stellarium.From(CaptureSky.Resolve(null,settings),null)==null&&Stellarium.From(CaptureSky.Resolve(new Frame{Target="Unknown"},settings),new Frame{Target="Unknown"})==null,"Frame without a position can open Stellarium");
   });
   Test("Stellarium executable is validated, remembered and launched with a start-up script",()=>{
    string root=Path.Combine(Path.GetTempPath(),"aa-stellarium-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
    try{
     string exe=Path.Combine(root,"stellarium.exe"),other=Path.Combine(root,"stellarium-console.exe");File.WriteAllText(exe,"");File.WriteAllText(other,"");
     Check(Stellarium.IsExecutable(exe)&&!Stellarium.IsExecutable(other)&&!Stellarium.IsExecutable(Path.Combine(root,"missing.exe"))&&!Stellarium.IsExecutable(null),"Executable check wrong");
     var settings=new Settings();Stellarium.Configure(settings,exe);var restored=Util.Deserialize<Settings>(Util.Serialize(settings));
     Check(Stellarium.Configured(restored)==exe&&Stellarium.Resolve(restored)==Path.GetFullPath(exe),"Chosen path not kept");
     string folder=Path.Combine(root,"scripts");Directory.CreateDirectory(folder);string stale=Path.Combine(folder,"open-stale.ssc");File.WriteAllText(stale,"x");File.SetLastWriteTimeUtc(stale,DateTime.UtcNow.AddDays(-3));
     string path=Stellarium.WriteScript("core.setTimeRate(0);\n",folder);
     Check(File.ReadAllText(path)=="core.setTimeRate(0);\n"&&!File.Exists(stale)&&path.EndsWith(".ssc"),"Script file or clean-up wrong");
     var info=Stellarium.LaunchInfo(exe,path);Check(info.FileName==Path.GetFullPath(exe)&&info.Arguments=="--startup-script \""+path+"\""&&!info.UseShellExecute&&info.WorkingDirectory==root,"Launch command wrong: "+info.Arguments);
     Expect(()=>Stellarium.LaunchInfo(other,path),"Wrong executable launched");
    }finally{try{Directory.Delete(root,true);}catch(IOException){}}
   });
   Test("Stellarium Remote Control is used only when it answers on this PC",()=>{
    var closed=new TcpListener(IPAddress.Loopback,0);closed.Start();int unused=((IPEndPoint)closed.LocalEndpoint).Port;closed.Stop();
    Check(!Stellarium.TrySend("core.setTimeRate(0);",unused,500),"Nothing listening was reported as sent");
    var server=new TcpListener(IPAddress.Loopback,0);server.Start();int port=((IPEndPoint)server.LocalEndpoint).Port;string request=null;
    var worker=new Thread(()=>{try{using(var client=server.AcceptTcpClient())using(var stream=client.GetStream()){var buffer=new byte[8192];var text=new StringBuilder();stream.ReadTimeout=2000;
     while(true){int read=stream.Read(buffer,0,buffer.Length);if(read<=0)break;text.Append(Encoding.UTF8.GetString(buffer,0,read));string all=text.ToString();int split=all.IndexOf("\r\n\r\n",StringComparison.Ordinal);if(split>=0&&all.Length-split-4>=Encoding.UTF8.GetByteCount("code="+Uri.EscapeDataString("core.setTimeRate(0);\n")))break;}
     request=text.ToString();var reply=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");stream.Write(reply,0,reply.Length);}}catch(Exception){}});
    worker.Start();
    try{Check(Stellarium.TrySend("core.setTimeRate(0);\n",port,2000),"Answering Stellarium was not used");}finally{worker.Join(3000);server.Stop();}
    int bodyStart=request==null?-1:request.IndexOf("\r\n\r\n",StringComparison.Ordinal);
    Check(bodyStart>=0&&request.StartsWith("POST /api/scripts/direct ")&&Uri.UnescapeDataString(request.Substring(bodyStart+4))=="code=core.setTimeRate(0);\n","Remote Control request wrong: "+request);
   });
  }
 }
}
