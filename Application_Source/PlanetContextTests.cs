using System;
using System.Collections.Generic;
using System.IO;

namespace AstroArchive {
 public partial class Tests {
  static void PlanetContextTests(){
   Test("Offline planets match independent JPL Horizons geocentric references",()=>{
    // Horizons API, centre 500@399, quantity 1 (astrometric ICRF RA/Dec),
    // degrees at 00:00 UTC. Retrieved 2026-10-12; see docs/PLANETARY_SKY.md.
    var bodies=new[]{"Mercury","Venus","Mars","Jupiter","Saturn","Uranus","Neptune"};
    var dates=new[]{new DateTime(2024,10,10,0,0,0,DateTimeKind.Utc),new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc)};
    double[,] references={
     {201.85129,-8.76835,227.55234,-18.49208,110.75012,22.82596,80.24644,22.41411,345.59690,-8.51915,54.07170,19.06763,358.37190,-2.15090},
     {219.95550,-18.56809,211.64240,-20.86597,130.33367,19.53798,143.69073,15.05857,10.56246,1.59499,63.00007,20.96271,2.56646,-0.43535}
    };
    for(int d=0;d<dates.Length;d++)for(int b=0;b<bodies.Length;b++){
     SkyVector vector;double distance;Check(PlanetEphemeris.TryAt(bodies[b],dates[d],out vector,out distance),"Planet missing: "+bodies[b]);
     double error=MoonEphemeris.Separation(vector,SkyVector.Equatorial(references[d,b*2],references[d,b*2+1]));
     Check(error<(bodies[b]=="Jupiter"||bodies[b]=="Saturn"?0.25:0.06),bodies[b]+" disagrees with Horizons on "+dates[d]+": "+error+" degrees");
     Check(Math.Abs(vector.Dot(vector)-1)<1e-10&&distance>0,"Invalid planetary direction/distance");
    }
   });
   Test("Moon target uses the same topocentric of-date position as its phase overlay",()=>{
    foreach(string body in new[]{"Moon","Lunar"})foreach(bool site in new[]{false,true}){
     var frame=new Frame{Target=body,ObservedUtc="2026-10-12T00:00:00Z",Latitude=site?(double?)51.5:null,Longitude=site?(double?)-0.1:null};var sky=CaptureSky.Resolve(frame,null);
     Check(sky.HasPosition&&sky.PositionOfDate&&sky.Moon!=null&&sky.Moon.HasTarget&&sky.ApproximatePosition,"Timed Moon target lacks its position");
     Check(MoonEphemeris.Separation(sky.TargetOfDate,sky.Moon.Topocentric)<0.00001&&sky.Moon.TargetSeparation<0.00001,"Moon target was precessed twice or used geocentric instead of observer position");
     if(site)Check(Math.Abs(sky.Altitude-sky.Moon.Altitude)<1e-8&&Math.Abs(sky.Azimuth-sky.Moon.Azimuth)<1e-8,"Moon target and overlay have different elevations");
     else Check(MoonEphemeris.Separation(sky.TargetOfDate,SkyVector.Equatorial(210.67964,-17.43228))<0.04,"Moon target disagrees with Horizons apparent coordinates");
    }
   });
   Test("Every supported solar target resolves from capture time without frame pointing",()=>{
    foreach(string name in new[]{"Sun","Solar","Moon","Lunar","Mercury","Venus","Mars","Jupiter","Saturn","Uranus","Neptune"}){
     var f=new Frame{Target=name,ObservedUtc="2026-10-12T00:00:00Z"};var sky=CaptureSky.Resolve(f,null);
     Check(sky.HasPosition&&sky.PositionOfDate&&!sky.HasHorizon&&sky.Evidence.Contains("capture time"),"Unpositioned solar target: "+name);
     f.Latitude=-33.9;f.Longitude=151.2;var south=CaptureSky.Resolve(f,null);Check(south.HasHorizon&&CaptureSky.Finite(south.Altitude)&&CaptureSky.Finite(south.Azimuth),"Southern horizon failed: "+name);
    }
    var jupiter=CaptureSky.Resolve(new Frame{Target="Jupiter",ObservedUtc="2026-10-12T00:00:00Z"},null);
    Check(MoonEphemeris.Separation(jupiter.TargetOfDate,SkyVector.Equatorial(144.05705,14.93970))<0.25,"Jupiter coordinates have the wrong epoch");
    var later=CaptureSky.Resolve(new Frame{Target="Jupiter",ObservedUtc="2026-11-12T00:00:00Z"},null);Check(later.Key!=jupiter.Key&&MoonEphemeris.Separation(later.TargetOfDate,jupiter.TargetOfDate)>1,"Moving planet reused an old catalogue direction");
   });
   Test("Planetary sky respects solved/recorded pointing and never guesses unsupported targets or clocks",()=>{
    var f=new Frame{Target="Jupiter",RA=12,Dec=-30,ObservedUtc="2026-10-12T00:00:00Z"};var sky=CaptureSky.Resolve(f,null);Check(sky.RA==12&&sky.Dec==-30&&!sky.PositionOfDate,"Ephemeris replaced measured frame pointing");
    f.Sky=new SkyGeometry{RA=15,Dec=20,Evidence="Solved"};Check(CaptureSky.Resolve(f,null).RA==15,"Ephemeris replaced plate solution");
    foreach(string target in new[]{"Pluto","Earth","Europa","Planetary","C/2023 A3","Unknown"})Check(!CaptureSky.Resolve(new Frame{Target=target,ObservedUtc=f.ObservedUtc},null).HasPosition,"Unsupported body received invented coordinates: "+target);
    foreach(string clock in new[]{null,"2026-10-12","2026-10-12T00:00:00"})Check(!CaptureSky.Resolve(new Frame{Target="Jupiter",Observed=clock,TimeSource="Filename (timezone unknown)"},null).HasPosition,"Unknown timezone/date invented planetary position");
    SkyVector vector;double distance;Check(!PlanetEphemeris.TryAt("Jupiter",new DateTime(2100,1,1),out vector,out distance),"Planet model extrapolated beyond its validity");
    var calibration=new Frame{Target="Jupiter",Kind="Dark",ObservedUtc=f.ObservedUtc};Check(!CaptureSky.Resolve(calibration,null).HasPosition,"Calibration received a planet position");
   });
   Test("Planetary timestamps work across video and still formats with explicit UTC or offsets",()=>{
    foreach(string format in new[]{"AVI","MP4","MOV","M4V","WMV","MKV","SER","FITS","XISF","TIFF","PNG","JPEG","DNG"}){
     var header=new FitsHeader();header.Values["OBJECT"]="Jupiter";header.Values["DATE-OBS"]="2026-10-12T01:00:00+01:00";
     var frame=CaptureSky.FromHeader(header,format,"Jupiter."+format.ToLowerInvariant());var sky=CaptureSky.Resolve(frame,null);Check(sky.HasPosition&&sky.Utc==new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc),"Offset timestamp lost: "+format);
     header.Values["DATE-OBS"]="2026-10-12T00:00:00";header.Values["TIMESYS"]="UTC";frame=CaptureSky.FromHeader(header,format,"Jupiter."+format.ToLowerInvariant());Check(CaptureSky.Resolve(frame,null).HasPosition,"Explicit UTC standard lost: "+format);
     MetadataProfiles.Apply(frame,header,new AssetInfo{Format=format,Header=header});Check(frame.ObservedUtc!=null&&CaptureSky.Resolve(frame,null).HasPosition,"Scanner discarded explicit UTC metadata: "+format);
     header.Values["TIMESYS"]="TAI";Check(!CaptureSky.Resolve(CaptureSky.FromHeader(header,format,"Jupiter."+format.ToLowerInvariant()),null).HasPosition,"Unsupported clock standard treated as UTC: "+format);
    }
   });
   Test("Existing indexed header clocks recover planetary positions without re-reading files",()=>{
    var header=new Dictionary<string,string>{{"DATE-OBS","2026-10-12T00:00:00"},{"TIMESYS","UTC"}};
    var frame=new Frame{Target="Jupiter",Format="XISF",Observed="2026-10-12T00:00:00",TimeSource="Capture time (timezone unknown)",ImageKey="image:0",Images=new List<ImageDescriptor>{new ImageDescriptor{Key="image:0",Headers=header}}};
    var sky=CaptureSky.Resolve(frame,null);Check(sky.HasPosition&&sky.Utc==new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc),"Retained UTC header was ignored in an existing index");
    Check(frame.ObservedUtc==null&&frame.TimeSource=="Capture time (timezone unknown)","Read-only sky recovery rewrote metadata");
    frame.Facts=new Dictionary<string,MetadataFact>();frame.Facts["Observed"]=new MetadataFact{Source="User",Value=""};Check(!CaptureSky.Resolve(frame,null).HasPosition,"Retained header overrode user clock edits");frame.Facts.Clear();
    header["TIMESYS"]="TAI";Check(!CaptureSky.Resolve(frame,null).HasPosition,"Legacy unsupported time standard guessed UTC");header["TIMESYS"]="UTC";header["DATE-OBS"]="2026-10-12";Check(!CaptureSky.Resolve(frame,null).HasPosition,"Legacy date-only header fabricated a capture clock");
   });
   Test("Observing city fills an absent capture timezone using date-specific DST rules",()=>{
    var london=new Settings{ObservingCityId=2643743,Latitude=51.50853,Longitude=-0.12574};
    var frame=new Frame{Target="Jupiter",Format="AVI",Kind="Video",Observed="2026-07-12T01:30:00",TimeSource="Filename (timezone unknown)"};var sky=CaptureSky.Resolve(frame,london);
    Check(sky.Utc==new DateTime(2026,7,12,0,30,0,DateTimeKind.Utc)&&sky.HasPosition&&sky.HasHorizon,"London summer timezone/DST was not used");
    Check(frame.ObservedUtc==null&&frame.TimeZoneId==null,"City fallback rewrote saved metadata");
    frame.Observed="2026-01-12T01:30:00";Check(CaptureSky.CaptureUtc(frame,london)==new DateTime(2026,1,12,1,30,0,DateTimeKind.Utc),"City fallback used today's offset instead of capture-date offset");
    var sydney=new Settings{ObservingCityId=2147714,Latitude=-33.86785,Longitude=151.20732};frame.Observed="2026-01-12T10:00:00";Check(CaptureSky.CaptureUtc(frame,sydney)==new DateTime(2026,1,11,23,0,0,DateTimeKind.Utc),"Sydney summer timezone/DST or midnight rollover failed");
    var kathmandu=new Settings{ObservingCityId=1283240};frame.Observed="2026-07-12T06:00:00";Check(CaptureSky.CaptureUtc(frame,kathmandu)==new DateTime(2026,7,12,0,15,0,DateTimeKind.Utc),"Fractional timezone offset was rounded");
    frame.ObservedUtc="2026-07-12T03:00:00Z";Check(CaptureSky.CaptureUtc(frame,london).Value.Hour==3,"City replaced explicit UTC");frame.ObservedUtc=null;frame.Observed="2026-07-12T06:00:00+02:00";Check(CaptureSky.CaptureUtc(frame,london).Value.Hour==4,"City replaced explicit clock offset");
    frame.Observed="2026-07-12T06:00:00";frame.TimeZoneId="UTC";Check(CaptureSky.CaptureUtc(frame,london).Value.Hour==6,"City replaced explicit capture timezone");frame.TimeZoneId=null;
    frame.TimeSource="Session folder (not frame time)";Check(!CaptureSky.CaptureUtc(frame,london).HasValue,"City turned session-folder time into a capture clock");frame.TimeSource="Filename (timezone unknown)";
    frame.Observed="2026-03-29T01:30:00";Check(!CaptureSky.CaptureUtc(frame,london).HasValue,"Invalid daylight-saving clock invented a capture time");frame.Observed="2026-10-25T01:30:00";Check(!CaptureSky.CaptureUtc(frame,london).HasValue,"Ambiguous daylight-saving clock selected an arbitrary occurrence");
    frame.Observed="2026-07-12";Check(!CaptureSky.CaptureUtc(frame,london).HasValue,"City turned date-only metadata into a capture clock");
    Check(CityTimeZones.ForCity(-1)==null,"Unknown city received arbitrary timezone rules");
   });
   Test("Scanned AVI and SER recordings use their retained capture clock for planetary sky",()=>{
    byte[] main=new byte[56];Array.Copy(VideoNumber(100000,4,false),0,main,0,4);Array.Copy(VideoNumber(10,4,false),0,main,16,4);
    byte[] avi=VideoJoin(VideoText("AVI "),VideoChunk("LIST",VideoText("hdrl"),VideoChunk("avih",main)));
    string path=VideoWrite("Jupiter_2026-10-12_00-00-00.avi",VideoJoin(VideoText("RIFF"),VideoNumber((ulong)avi.Length,4,false),avi));var frame=Classifier.Read(path,root,"Scope","Auto");
    Check(frame.Kind=="Video"&&frame.Target=="Jupiter"&&frame.Observed.StartsWith("2026-10-12"),"AVI filename capture time lost");Check(!CaptureSky.Resolve(frame,null).HasPosition,"Unzoned AVI filename guessed UTC");
    frame=new MetadataPatch(new Dictionary<string,string>{{"TimeZoneId","UTC"}}).Apply(frame);var sky=CaptureSky.Resolve(frame,null);Check(sky.HasPosition&&sky.Utc.Value.Hour==0,"AVI with recorded capture timezone still lacks a planetary position");
    frame=Util.Deserialize<Frame>(Util.Serialize(frame));Check(CaptureSky.Resolve(frame,null).Key==sky.Key,"Saved AVI lost its capture-time planetary position");
    string ser=MakeSer("Moon-clock.ser",0,0,8,1,new byte[4]);using(var file=new FileStream(ser,FileMode.Open,FileAccess.Write))using(var writer=new BinaryWriter(file)){file.Position=170;writer.Write(new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc).Ticks);}
    frame=Classifier.Read(ser,root,"Scope","Auto");Check(frame.Target=="Moon"&&frame.Format=="SER"&&CaptureSky.Resolve(frame,null).HasPosition,"SER UTC header did not locate Moon");
   });
  }
 }
}
