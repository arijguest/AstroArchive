using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AstroArchive.Remote;

namespace AstroArchive {
 public partial class Tests {
  static void CelestronOriginTests(){
   Test("Celestron Origin frame names, session folders and camera headers are recognised",()=>{
    Check(CelestronOrigin.FrameKind("Light0001.fits")=="Light"&&CelestronOrigin.FrameKind("bias0009.fits")=="Bias"&&CelestronOrigin.FrameKind("dark0003.fit")=="Dark"&&CelestronOrigin.FrameKind("flat0012.tiff")=="Flat","Origin frame kinds wrong");
    Check(CelestronOrigin.FrameKind("Light_M42_10s.fits")==null&&CelestronOrigin.FrameKind("Light0001.json")==null,"Other names taken as Origin frames");
    Check(CelestronOrigin.IsStackedMaster("FinalStackedMaster.tiff")&&!CelestronOrigin.IsStackedMaster("Stacked_M42.fit"),"Onboard stack name wrong");
    Check(CelestronOrigin.SessionTarget("Orion_Nebula_2025-02-11_20-22-01")=="Orion Nebula"&&CelestronOrigin.SessionTarget("M42_sub")==null,"Session folder target wrong");
    Check(CelestronOrigin.Layout("RawData/Pinwheel_Galaxy_2025-01-30_19-51-43/Light0004.tiff")&&CelestronOrigin.Layout("Pinwheel_Galaxy_2025-01-30_19-51-43/FinalStackedMaster.tiff")&&!CelestronOrigin.Layout("Captures/M42/Light0004.fits"),"Origin folder signature wrong");
    Check(CelestronOrigin.UnitId("Origin178-932F562381E97CE70")=="932f562381e97ce70"&&CelestronOrigin.UnitId("ZWO ASI2600MC")==null,"Camera unit ID wrong");
    Check(InstrumentDetection.MakeOf("Origin178-932f562381e97ce70")=="Celestron"&&InstrumentDetection.MakeOf("Celestron Origin")=="Celestron"&&InstrumentDetection.MakeOf("Celestron EdgeHD 8")!="Celestron"&&InstrumentDetection.MakeOf("Origin")!="Celestron","Only the Origin is the Celestron smart-telescope make");
    Check(TelescopeProfiles.Models.Contains(CelestronOrigin.Model),"Celestron Origin missing from telescope models");
   });
   Test("Celestron Origin local DATE-OBS uses its TIMEZONE offset",()=>{
    DateTime utc,local;
    Check(CelestronOrigin.CaptureTime("2026-08-31T20:40:32","-0700",out utc,out local)&&utc==new DateTime(2026,9,1,3,40,32)&&local==new DateTime(2026,8,31,20,40,32),"Separate offset not applied");
    Check(CelestronOrigin.CaptureTime("'2026-08-31T20:40:32.250'","+05:30",out utc,out local)&&utc==new DateTime(2026,8,31,15,10,32,250),"Colon offset or fraction wrong");
    Check(CelestronOrigin.CaptureTime("2026-08-31T20:40:32-0700","-0700",out utc,out local)&&utc==new DateTime(2026,9,1,3,40,32),"Embedded offset applied twice");
    Check(!CelestronOrigin.CaptureTime("2026-08-31T20:40:32Z","-0700",out utc,out local)&&!CelestronOrigin.CaptureTime("2026-08-31T20:40:32","PDT",out utc,out local)&&!CelestronOrigin.CaptureTime("2026-08-31","-0700",out utc,out local),"UTC, named-zone or date-only times converted");
   });
   Test("Celestron Origin info.json supplies target, site, position and stack totals",()=>{
    var raw=Util.Deserialize<Dictionary<string,object>>("{\"StackedInfo\":{\"objectName\":\"Black Eye Galaxy\",\"dateTime\":\"2026-08-31T20:40:32-0700\",\"stackedDepth\":42,\"totalDurationMs\":420000,\"gps\":{\"latitude\":33.83,\"longitude\":-117.79},\"celestial\":{\"first\":3.3911,\"second\":0.3769}}}");
    var s=CelestronOrigin.Parse(raw);
    Check(s.Object=="Black Eye Galaxy"&&s.StackedDepth==42&&s.TotalSeconds==420&&s.Latitude==33.83&&s.Longitude==-117.79,"Session fields wrong");
    Check(Math.Abs(s.RaDegrees.Value-194.296)<0.01&&Math.Abs(s.DecDegrees.Value-21.595)<0.01,"Radian coordinates not converted");
    var empty=CelestronOrigin.Parse(Util.Deserialize<Dictionary<string,object>>("{\"gps\":{\"latitude\":0,\"longitude\":0},\"stackedDepth\":2.5}"));
    Check(!empty.Latitude.HasValue&&!empty.StackedDepth.HasValue,"Null island or fractional depth accepted");
   });
   Test("Celestron Origin captures classify with make, kind, target and UTC time",()=>{
    string session=Path.Combine(root,"origin-classify","RawData","Black_Eye_Galaxy_2026-08-31_20-40-30");Directory.CreateDirectory(session);
    var headers=new Dictionary<string,string>{{"CAMERA","'Origin178-932f562381e97ce70'"},{"CREATOR","'Origin 1.4.6084'"},{"DATE-OBS","'2026-08-31T20:40:32'"},{"TIMEZONE","'-0700'"},{"EXPTIME","10"},{"ISOSPEED","200"},{"BAYERPAT","'RGGB'"}};
    string light=Path.Combine(session,"Light0001.fits"),bias=Path.Combine(session,"bias0009.fits");Write(light,32,24,(x,y)=>1200,headers);Write(bias,32,24,(x,y)=>500,new Dictionary<string,string>{{"CAMERA","'Origin178-932f562381e97ce70'"},{"EXPTIME","0"}});
    File.WriteAllText(Path.Combine(session,"info.json"),"{\"StackedInfo\":{\"objectName\":\"Black Eye Galaxy\",\"gps\":{\"latitude\":33.83,\"longitude\":-117.79}}}");
    string rootFolder=Path.Combine(root,"origin-classify");
    var f=Classifier.Read(light,rootFolder,"Origin-01","Auto");
    Check(f.Make=="Celestron"&&f.Model=="Celestron Origin"&&f.DeviceProfile=="celestron-origin-1"&&f.CameraId=="932f562381e97ce70","Origin instrument not identified: "+f.Make+" / "+f.Model);
    Check(f.Kind=="Light","Light0001 not a light: "+f.Kind);
    Check(f.Target!="Unknown"&&(f.TargetEvidence??"").Contains("Celestron Origin"),"Session target not used: "+f.Target+" / "+f.TargetEvidence);
    Check(f.Observed=="2026-09-01T03:40:32.000"&&f.TimeSource=="FITS UTC"&&f.Night=="2026-08-31"&&f.AcquisitionDate=="2026-08-31","Local time not converted: "+f.Observed+" "+f.Night);
    Check(f.Latitude==33.83&&f.Longitude==-117.79,"info.json site not applied");
    Check(f.AssociatedFiles!=null&&f.AssociatedFiles.Any(a=>Path.GetFileName(a.SourcePath)=="info.json"),"info.json not kept with the capture");
    var b=Classifier.Read(bias,rootFolder,"Origin-01","Auto");
    Check(b.Make=="Celestron"&&b.Kind=="Bias"&&b.Target=="Calibration","bias0009 not a bias frame: "+b.Kind);
   });
   Test("Celestron Origin discovery reads its broadcast and mDNS answers",()=>{
    var origin=TelescopeDiscovery.ParseOrigin(Encoding.UTF8.GetBytes("Identity:Origin-1234 Origin IP Address: 192.168.1.195"),"192.168.1.195");
    Check(origin!=null&&origin.Kind=="Origin FTP"&&origin.Name=="Celestron Origin"&&origin.Host=="192.168.1.195","Origin broadcast not recognised");
    Check(TelescopeDiscovery.ParseOrigin(Encoding.UTF8.GetBytes("SkyPortal WiFi IP Address: 1.2.3.4"),"1.2.3.4")==null&&TelescopeDiscovery.ParseOrigin(new byte[]{0xff,0xfe},"1.2.3.4")==null,"Other broadcasts accepted");
    byte[] query=TelescopeDiscovery.MdnsQuery("origin.local",7);
    Check(query.Length==12+14+4&&query[5]==1&&query[query.Length-3]==1&&query[query.Length-2]==0x80,"mDNS query is not a unicast-response A query");
    // Response: header, echoed question, then one A answer whose name points back to the question.
    var answer=new List<byte>{0,7,0x84,0,0,1,0,1,0,0,0,0};answer.AddRange(query.Skip(12).Take(14));answer.AddRange(new byte[]{0,1,0,1,0xC0,12,0,1,0x80,1,0,0,0,120,0,4,192,168,1,195});
    var found=TelescopeDiscovery.ParseMdns(answer.ToArray(),"origin.local");Check(found.Count==1&&found[0]=="192.168.1.195","mDNS A record not read");
    Check(TelescopeDiscovery.ParseMdns(query,"origin.local").Count==0&&TelescopeDiscovery.ParseMdns(answer.Take(answer.Count-3).ToArray(),"origin.local").Count==0,"Queries or truncated answers accepted");
    var loop=new List<byte>(answer);loop[answer.Count-15]=(byte)(answer.Count-16);Check(TelescopeDiscovery.ParseMdns(loop.ToArray(),"origin.local").Count==0,"Compression loop not rejected");
   });
   Test("Origin FTP connections validate like DWARF and use the FTP source",()=>{
    var c=new Connection{Kind="Origin FTP",Host="192.168.1.195",Folder="/RawData",Port=21};c.Validate(false);
    Check(Connection.Ftp("Origin FTP")&&Connection.Ftp("DWARF FTP")&&!Connection.Ftp("Seestar SMB"),"FTP kinds wrong");
    using(var source=Downloader.Source(c))Check(source is FtpSource,"Origin does not use FTP");
    Expect(()=>new Connection{Kind="Origin FTP",Host="origin.local",Folder="/"}.Validate(false),"Hostname accepted where an IPv4 address is required");
   });
  }
 }
}
