using System;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static Frame SkyFrame(double ra,double dec,double latitude,double longitude){return new Frame{Target="Unknown",RA=SkyOrientation.Wrap(ra),Dec=dec,Latitude=latitude,Longitude=longitude,ObservedUtc="2000-01-01T12:00:00Z"};}
  static void SkyContextTests(){
   Test("Sky camera wraps rotation clamps poles and zoom and resets to capture",()=>{
    var camera=new SkyGlobeCamera();camera.SetHome(355,-25);camera.Orbit(20,30);Check(camera.Yaw==15&&camera.Tilt==5,"Orbit did not cross longitude seam");
    camera.Orbit(0,1000);Check(camera.Tilt==89,"North pole was not bounded");camera.Orbit(0,-1);Check(camera.Tilt==88,"Pole overshoot delayed reverse drag");camera.Orbit(-720,-1000);Check(camera.Yaw==15&&camera.Tilt==-89,"South pole or multiple orbit wrap failed");
    camera.Magnify(1000);Check(camera.Zoom==3,"Zoom overflow");camera.Magnify(0.00001);Check(camera.Zoom==0.6,"Zoom underflow");camera.Reset();Check(camera.Yaw==355&&camera.Tilt==-25&&camera.Zoom==1,"Reset lost original capture view");
    camera.Orbit(double.NaN,20);camera.Magnify(double.PositiveInfinity);Check(camera.Yaw==355&&camera.Tilt==-25&&camera.Zoom==1,"Invalid gesture corrupted camera");
    camera.SetHome(120,25);Check(camera.Yaw==120&&camera.Tilt==25&&camera.Zoom==1,"New capture retained previous camera");
   });
   Test("Sky horizon uses east-positive longitude and known sidereal time",()=>{
    var utc=new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc);Check(Math.Abs(SkyOrientation.Sidereal(utc,0)-280.46061837)<0.000001,"J2000 sidereal reference changed");
    Check(Math.Abs(SkyOrientation.Sidereal(utc,30)-310.46061837)<0.000001,"Eastern longitude reversed");
    var zenith=CaptureSky.Resolve(SkyFrame(280.46061837,30,30,0),new Settings());Check(zenith.HasHorizon&&Math.Abs(zenith.Altitude-90)<0.001,"Meridian star did not reach zenith");
    var east=CaptureSky.Resolve(SkyFrame(370.46061837,0,0,0),null);var west=CaptureSky.Resolve(SkyFrame(190.46061837,0,0,0),null);
    Check(Math.Abs(east.Altitude)<0.001&&Math.Abs(east.Azimuth-90)<0.001&&Math.Abs(west.Azimuth-270)<0.001,"East/west compass directions reversed");
   });
   Test("Sky hemisphere and horizon work for southern and polar observers",()=>{
    var south=CaptureSky.Resolve(SkyFrame(280.46061837,-90,-35,0),null);Check(Math.Abs(south.Altitude-35)<0.001&&Math.Abs(south.Azimuth-180)<0.001&&south.Evidence.Contains("Southern celestial sky"),"South celestial pole shown in wrong hemisphere");
    var north=CaptureSky.Resolve(SkyFrame(280.46061837,90,51,0),null);Check(Math.Abs(north.Altitude-51)<0.001&&north.Azimuth<0.001,"North celestial pole misplaced");
    foreach(double latitude in new[]{-90.0,0,90}){var result=CaptureSky.Resolve(SkyFrame(15,20,latitude,180),null);Check(CaptureSky.Finite(result.Altitude)&&CaptureSky.Finite(result.Azimuth),"Polar site produced invalid coordinates");}
   });
   Test("Sky capture time changes position and below-horizon fields stay explicit",()=>{
    var frame=SkyFrame(325.46061837,0,0,0);var first=CaptureSky.Resolve(frame,null);frame.ObservedUtc="2000-01-01T13:00:00Z";var later=CaptureSky.Resolve(frame,null);
    Check(later.Altitude>first.Altitude+14&&later.Key!=first.Key,"Different acquisition times reused the same sky");
    var below=CaptureSky.Resolve(SkyFrame(100.46061837,0,51,0),null);Check(below.Altitude<0&&below.Summary.Contains("below horizon"),"Inaccessible target presented above horizon");
   });
   Test("Sky never invents a capture clock from filenames folders dates or machine time",()=>{
    var frame=SkyFrame(280,30,51,0);frame.ObservedUtc=null;frame.Observed="2026-10-07T22:00:00";frame.TimeSource="Filename (timezone unknown)";
    Check(!CaptureSky.Resolve(frame,null).HasHorizon,"Unzoned filename was treated as UTC");frame.Observed="2026-10-07";frame.TimeSource="FITS date only";Check(!CaptureSky.CaptureUtc(frame).HasValue,"Date alone became a clock");
    frame.ObservedUtc="2026-10-07T22:00:00Z";frame.TimeSource="Session folder (not frame time)";Check(!CaptureSky.CaptureUtc(frame).HasValue,"Session-folder time used for a frame");
    frame.ObservedUtc=null;frame.Observed="2026-10-07T22:00:00+02:00";frame.TimeSource="Capture timestamp";Check(CaptureSky.CaptureUtc(frame).Value.Hour==20,"Explicit clock offset ignored");
    frame.Observed="2026-10-07T22:00:00";frame.TimeSource="FITS UTC";Check(CaptureSky.CaptureUtc(frame).Value.Hour==22,"Legacy FITS UTC clock lost");
   });
   Test("Sky capture location takes precedence without mixing partial sites",()=>{
    var frame=SkyFrame(280,30,-33,151);var settings=new Settings{Latitude=51,Longitude=0,ObservingCity="London"};var capture=CaptureSky.Resolve(frame,settings);
    Check(capture.Latitude==-33&&capture.Longitude==151,"Saved city replaced capture metadata");frame.Longitude=null;var fallback=CaptureSky.Resolve(frame,settings);Check(fallback.Latitude==51&&fallback.Longitude==0,"Partial capture location mixed with saved city");
    frame.Latitude=double.NaN;settings.Latitude=100;Check(!CaptureSky.Resolve(frame,settings).HasHorizon,"Invalid observing location created a horizon");
   });
   Test("Sky prefers frame pointing and identifies catalogue approximations",()=>{
    var frame=new Frame{Target="Whirlpool Galaxy"};var catalogue=CaptureSky.Resolve(frame,null);Check(catalogue.HasPosition&&catalogue.ApproximatePosition&&catalogue.RA>200&&catalogue.Dec>40,"Common target name not resolved");
    frame.RA=12;frame.Dec=-44;var pointing=CaptureSky.Resolve(frame,null);Check(pointing.RA==12&&pointing.Dec==-44&&!pointing.ApproximatePosition,"Catalogue replaced frame pointing");
    frame.Sky=new SkyGeometry{RA=15,Dec=-45,Evidence="Plate solved"};Check(CaptureSky.Resolve(frame,null).RA==15,"Solved centre ignored");
    frame.Sky.RA=double.NaN;frame.RA=double.PositiveInfinity;frame.Target="169P NEAT";Check(!CaptureSky.Resolve(frame,null).HasPosition,"Unknown moving target received invented coordinates");
   });
   Test("Sky header snapshots preserve coordinate units and time standards",()=>{
    var header=new FitsHeader();header.Values["OBJECT"]="M51";header.Values["OBJCTRA"]="13:30:00";header.Values["OBJCTDEC"]="-20:30:00";header.Values["DATE-OBS"]="2026-10-07T22:30:00";header.Values["SITELAT"]="-35";header.Values["SITELONG"]="149";
    var frame=CaptureSky.FromHeader(header,"FITS","capture.fit");Check(frame.RA==202.5&&frame.Dec==-20.5&&CaptureSky.Resolve(frame,null).HasHorizon,"FITS coordinates or UTC missing");
    header.Values["TIMESYS"]="TAI";header.Values["DATE-OBS"]+="Z";Check(!CaptureSky.CaptureUtc(CaptureSky.FromHeader(header,"FITS","capture.fit")).HasValue,"Non-UTC clock accepted");
    header.Values.Remove("TIMESYS");header.Values["DATE-OBS"]="2026-10-07";Check(!CaptureSky.CaptureUtc(CaptureSky.FromHeader(header,"FITS","capture.fit")).HasValue,"Header date alone accepted");
   });
   Test("Sky catalogue covers both hemispheres and cached precession preserves unit vectors",()=>{
    Check(SkyFigures.Figures.Length==89&&SkyFigures.Figures.Any(f=>f.Name=="Crux")&&SkyFigures.Figures.Any(f=>f.Name=="Ursa Major"),"Constellation hemisphere coverage incomplete");
    Check(SkyFigures.Stars.Length>600&&SkyFigures.Stars.Length<1000,"Unexpected catalogue size");var orientation=new SkyOrientation(new DateTime(2026,10,7,22,0,0,DateTimeKind.Utc),true,-33,151);
    foreach(var star in SkyFigures.Stars){var mapped=orientation.Map(star);Check(Math.Abs(mapped.Dot(mapped)-1)<0.000001,"Precession or horizon transform changed vector length");}
   });
  }
 }
}
