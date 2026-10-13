using System;
using System.Linq;

namespace AstroArchive {
 public partial class Tests {
  // Greatest-eclipse references from NASA's Five Millennium Canons (F. Espenak, NASA GSFC).
  static EclipseInfo PeakSolar(DateTime utc,double latitude,double longitude){
   return Enumerable.Range(-300,601).Select(s=>Eclipses.Solar(utc.AddSeconds(s),latitude,longitude)).Where(e=>e!=null).OrderByDescending(e=>e.Magnitude).FirstOrDefault();
  }
  static EclipseInfo PeakLunar(DateTime utc){
   return Enumerable.Range(-20,41).Select(m=>Eclipses.Lunar(utc.AddMinutes(m))).Where(e=>e!=null).OrderByDescending(e=>e.PenumbralMagnitude).FirstOrDefault();
  }
  static DateTime Utc(int y,int mo,int d,int h,int mi,int s){return new DateTime(y,mo,d,h,mi,s,DateTimeKind.Utc);}
  static void EclipseTests(){
   Test("Santa Ponsa eclipse uses the chosen town's summer clock and refreshes after a location edit",()=>{
    var town=new ObservingCity{Id=6324533,Name="Santa Ponsa",Latitude=39.50868,Longitude=2.4766};
    var frame=new Frame{Target="Sun",Kind="Stack",Format="FITS",RA=0,Dec=0,Observed="2026-08-12T20:32:54.797",ObservedUtc="2026-08-12T20:32:54.797Z",TimeZoneId="UTC",TimeSource="FITS UTC"};
    var wrong=CaptureSky.Resolve(frame,new Settings{Latitude=town.Latitude,Longitude=town.Longitude});Check(wrong.BodyBelowHorizon&&wrong.Eclipse==null,"Incorrect UTC interpretation did not reproduce the reported failure");
    var changed=new MetadataPatch(new System.Collections.Generic.Dictionary<string,string>(),true,town).Apply(frame);
    var expected=new DateTime(2026,8,12,18,32,54,797,DateTimeKind.Utc);var sky=CaptureSky.Resolve(changed,null);
    Check(sky.Utc==expected&&Util.Time(changed.ObservedUtc)==expected&&changed.Observed==frame.Observed,"Town did not convert CEST to UTC or changed the original clock");
    Check(sky.HasHorizon&&!sky.BodyBelowHorizon&&sky.Altitude>1&&sky.Altitude<4&&sky.Eclipse!=null&&sky.Eclipse.Solar&&sky.Eclipse.Obscuration>0.995,"Corrected capture did not show the near-total eclipse above the western horizon: "+sky.Summary);
    Check(sky.Key!=wrong.Key&&sky.PositionOfDate&&changed.Facts["TimeZoneId"].Source=="Capture town / city","Map was not invalidated or timezone provenance lost");
    // Existing records already carrying a chosen town also repair stale, assumed UTC when resolved.
    CaptureSite.Apply(frame,town);Check(CaptureSky.Resolve(frame,null).Key==sky.Key,"Previously chosen city still used stale imported UTC");
    var london=new ObservingCity{Id=2643743,Name="London",Latitude=51.50853,Longitude=-0.12574};
    var moved=new MetadataPatch(new System.Collections.Generic.Dictionary<string,string>(),true,london).Apply(changed);var later=CaptureSky.Resolve(moved,null);
    Check(later.Utc==expected.AddHours(1)&&later.Key!=sky.Key&&later.BodyBelowHorizon&&later.Eclipse==null,"Changing the town did not recalculate UTC, horizon and eclipse");
    var returned=new MetadataPatch(new System.Collections.Generic.Dictionary<string,string>(),true,town).Apply(moved);
    Check(CaptureSky.Resolve(returned,null).Key==sky.Key&&CaptureSky.Resolve(returned,null).Utc==expected,"Repeated location changes accumulated timezone offsets");
   });
   Test("Changing a capture town refreshes eclipse, horizon and review evidence",()=>{
    var frame=new Frame{Target="Sun",Kind="Light",Status="Imported",ObservedUtc="2024-04-08T18:17:16Z",Latitude=25.29,Longitude=-104.14};
    var previous=CaptureScreening.Site;CaptureScreening.Site=()=>new Settings();
    try{
     var before=CaptureSky.Resolve(frame,null);Check(before.Eclipse!=null&&before.Eclipse.Kind=="Total"&&!before.BodyBelowHorizon,"Visible total eclipse fixture missing");
     // London has no local eclipse at this instant. Tokyo is on the night side.
     CaptureSite.Apply(frame,new ObservingCity{Id=2,Name="London",Latitude=51.5,Longitude=0});var london=CaptureSky.Resolve(frame,null);
     Check(london.Eclipse==null&&london.Key!=before.Key&&Math.Abs(london.Altitude-before.Altitude)>1,"Changed town kept the old sky or local eclipse");
     CaptureSite.Apply(frame,new ObservingCity{Id=3,Name="Tokyo",Latitude=35.7,Longitude=139.7});var night=CaptureSky.Resolve(frame,null);
     Check(night.BodyBelowHorizon&&night.SunAltitude<0&&night.Eclipse==null&&CaptureScreening.HorizonIssue(frame)!=null,"Night-side Sun was treated as an observable eclipse or left unflagged");
     // A recorded pointing above the horizon cannot override the physical Sun check.
     frame.RA=SkyOrientation.Sidereal(night.Utc.Value,139.7);frame.Dec=35.7;var pointing=CaptureSky.Resolve(frame,null);
     Check(pointing.BodyBelowHorizon&&pointing.Eclipse==null&&pointing.Altitude<0,"Stored pointing hid an impossible Sun capture");
     frame.RA=null;frame.Dec=null;CaptureSite.Apply(frame,null);var restored=CaptureSky.Resolve(frame,null);
     Check(restored.Eclipse!=null&&!restored.BodyBelowHorizon&&CaptureScreening.HorizonIssue(frame)==null,"Clearing the town kept stale eclipse/review results");
     foreach(double latitude in new[]{-70.0,-30,0,30,70})foreach(double longitude in new[]{-180.0,-120,-60,0,60,120,180}){
      var probe=CaptureSky.Resolve(new Frame{Target="Moon",Kind="Light",ObservedUtc=frame.ObservedUtc,Latitude=latitude,Longitude=longitude},null);
      if(probe.SunAltitude<0||probe.Moon.Altitude<0)Check(probe.Eclipse==null||!probe.Eclipse.Solar,"A Sun or Moon below the horizon acquired a solar eclipse");
     }
    }finally{CaptureScreening.Site=previous;}
   });
   Test("Solar eclipse magnitude and kind match NASA greatest-eclipse values",()=>{
    var cases=new[]{
     new{Utc=Utc(2024,4,8,18,17,16),Lat=25.29,Lon=-104.14,Magnitude=1.0566,Kind="Total"},
     new{Utc=Utc(2017,8,21,18,25,32),Lat=36.97,Lon=-87.67,Magnitude=1.0306,Kind="Total"},
     new{Utc=Utc(2023,10,14,17,59,29),Lat=11.37,Lon=-83.10,Magnitude=0.9520,Kind="Annular"},
     new{Utc=Utc(2022,10,25,11,0,13),Lat=61.6,Lon=77.4,Magnitude=0.8619,Kind="Partial"}};
    foreach(var c in cases){
     var e=PeakSolar(c.Utc,c.Lat,c.Lon);Check(e!=null&&e.Solar,"No solar eclipse found on "+c.Utc.ToString("u"));
     Check(e.Kind==c.Kind&&Math.Abs(e.Magnitude-c.Magnitude)<0.005,"Solar eclipse "+c.Utc.ToString("u")+" gave "+e.Kind+" "+e.Magnitude.ToString("0.0000"));
     Check(c.Kind=="Total"?e.Obscuration>0.999:c.Kind=="Annular"?Math.Abs(e.Obscuration-c.Magnitude*c.Magnitude)<0.04:e.Obscuration>0.75&&e.Obscuration<0.9,"Solar obscuration is implausible: "+e.Obscuration);
    }
    Check(Eclipses.Solar(Utc(2024,4,15,18,0,0),25.29,-104.14)==null&&Eclipses.Solar(Utc(2024,4,8,18,17,16),51.5,-0.1)==null,"An eclipse was reported where the Moon does not cover the Sun");
   });
   Test("Lunar eclipse umbral and penumbral magnitudes match NASA values",()=>{
    var cases=new[]{
     new{Utc=Utc(2025,3,14,6,58,43),Umbral=1.1784,Kind="Total"},
     new{Utc=Utc(2022,11,8,10,59,11),Umbral=1.3589,Kind="Total"},
     new{Utc=Utc(2023,10,28,20,14,5),Umbral=0.1224,Kind="Partial"},
     new{Utc=Utc(2023,5,5,17,22,47),Umbral=-0.0464,Kind="Penumbral"}};
    foreach(var c in cases){
     var e=PeakLunar(c.Utc);Check(e!=null&&!e.Solar,"No lunar eclipse found on "+c.Utc.ToString("u"));
     Check(e.Kind==c.Kind&&Math.Abs(e.Magnitude-c.Umbral)<0.01,"Lunar eclipse "+c.Utc.ToString("u")+" gave "+e.Kind+" "+e.Magnitude.ToString("0.0000"));
     Check(c.Kind=="Total"?e.Obscuration>0.999:c.Kind=="Penumbral"?e.Obscuration==0:e.Obscuration>0&&e.Obscuration<0.12,"Lunar umbral coverage is implausible: "+e.Obscuration);
    }
    Check(Eclipses.Lunar(Utc(2025,3,1,12,0,0))==null&&Eclipses.Lunar(Utc(2025,3,14,0,0,0))==null,"A lunar eclipse was reported outside an eclipse");
   });
   Test("Eclipse events find NASA contact times, greatest eclipse and totality",()=>{
    Func<DateTime?,DateTime,bool> near=(found,expected)=>found.HasValue&&Math.Abs((found.Value-expected).TotalSeconds)<90;
    // 2025 March 14 total lunar eclipse: P1 03:57:28, U1 05:09:40, U2 06:26:06, greatest 06:58:43, U3 07:31:26, U4 08:47:52, P4 10:00:09.
    var capture=Utc(2025,3,14,5,40,0);var total=Eclipses.Event(Eclipses.Lunar(capture),capture,0,0);Check(total!=null&&!total.Solar&&total.Kind=="Total","Total lunar eclipse event missing");
    Check(near(total.Start,Utc(2025,3,14,3,57,28))&&near(total.End,Utc(2025,3,14,10,0,9))&&near(total.Greatest,Utc(2025,3,14,6,58,43)),"Lunar penumbral contacts or greatest eclipse wrong: "+total.Start.ToString("u")+" "+total.Greatest.ToString("u")+" "+total.End.ToString("u"));
    Check(near(total.PartialStart,Utc(2025,3,14,5,9,40))&&near(total.PartialEnd,Utc(2025,3,14,8,47,52))&&near(total.CentralStart,Utc(2025,3,14,6,26,6))&&near(total.CentralEnd,Utc(2025,3,14,7,31,26)),"Lunar umbral or total contacts wrong: "+total.PartialStart+" "+total.CentralStart+" "+total.CentralEnd+" "+total.PartialEnd);
    Check(total.Peak.Obscuration>0.999&&total.Curve.Length>10&&total.Curve.Max()>0.999&&total.Curve[0]==0&&total.PenumbralCurve.Max()>0.999&&total.Position(capture)>0.2&&total.Position(capture)<0.35,"Lunar coverage curve or capture position wrong");
    // The same event is found from any capture inside it.
    var later=Eclipses.Event(Eclipses.Lunar(Utc(2025,3,14,9,30,0)),Utc(2025,3,14,9,30,0),0,0);Check(later!=null&&Math.Abs((later.Greatest-total.Greatest).TotalSeconds)<5&&Math.Abs((later.Start-total.Start).TotalSeconds)<5,"A penumbral capture found a different event");
    // 2023 October 28 partial lunar eclipse: U1 19:35:23, U4 20:52:35, no totality.
    var partial=Eclipses.Event(Eclipses.Lunar(Utc(2023,10,28,20,0,0)),Utc(2023,10,28,20,0,0),0,0);
    Check(partial!=null&&partial.Kind=="Partial"&&near(partial.PartialStart,Utc(2023,10,28,19,35,23))&&near(partial.PartialEnd,Utc(2023,10,28,20,52,35))&&!partial.CentralStart.HasValue&&partial.Peak.Obscuration>0&&partial.Peak.Obscuration<0.12,"Partial lunar eclipse event wrong");
    // 2024 April 8 at the point of greatest eclipse: 4 min 28 s of totality inside a partial eclipse of about two and three-quarter hours.
    var solarCapture=Utc(2024,4,8,17,30,0);var solar=Eclipses.Event(Eclipses.Solar(solarCapture,25.29,-104.14),solarCapture,25.29,-104.14);
    Check(solar!=null&&solar.Solar&&solar.Kind=="Total"&&solar.CentralStart.HasValue&&!solar.PartialStart.HasValue,"Total solar eclipse event missing its totality");
    double totality=(solar.CentralEnd.Value-solar.CentralStart.Value).TotalSeconds;Check(Math.Abs(totality-268)<15&&Math.Abs((solar.Greatest-Utc(2024,4,8,18,17,16)).TotalSeconds)<60,"Local totality or greatest eclipse wrong: "+totality+" s at "+solar.Greatest.ToString("u"));
    Check(solar.Duration.TotalHours>2.4&&solar.Duration.TotalHours<3&&solar.Start<solarCapture&&solar.End>solar.Greatest,"Local solar contacts implausible: "+solar.Start.ToString("u")+" – "+solar.End.ToString("u"));
    Check(Eclipses.Event(null,solarCapture,0,0)==null,"An event was built without an eclipse");
   });
   Test("Sun, Moon and eclipse captures resolve their body, map and section",()=>{
    var sun=new Frame{Target="Sun",Kind="Light",Observed="2024-04-08T18:17:16Z",TimeSource="FITS UTC",Latitude=25.29,Longitude=-104.14};
    var sky=CaptureSky.Resolve(sun,null);Check(sky.Body=="Sun"&&sky.Daytime&&sky.Eclipse!=null&&sky.Eclipse.Solar&&sky.Eclipse.Kind=="Total"&&sky.HasPosition,"Total solar eclipse capture lacks its eclipse or daytime context");
    // A blank mount pointing (0°, 0°) must not override the calculated Moon.
    var moon=new Frame{Target="Moon",Kind="Stack",Observed="2025-03-14T06:58:43Z",TimeSource="FITS UTC",RA=0,Dec=0};
    var lunar=CaptureSky.Resolve(moon,null);Check(lunar.HasPosition&&MoonEphemeris.Separation(lunar.TargetOfDate,lunar.Moon.Geocentric)<1.5&&lunar.Eclipse!=null&&lunar.Eclipse.Kind=="Total","Blank pointing replaced the Moon or the lunar eclipse was missed");
    var mode=new Frame{Target="Unknown",ObservationMode="Lunar mode",Kind="Stack",Observed="2025-03-14T06:58:43Z",TimeSource="FITS UTC"};Check(CaptureSky.Resolve(mode,null).Body=="Moon","A lunar capture mode did not name the Moon");
    // A Moon computed below the horizon (a wrong clock) keeps its map.
    var wrong=new Frame{Target="Moon",Kind="Light",Observed="2025-03-14T12:58:43Z",TimeSource="FITS UTC",Latitude=51.5,Longitude=0};var hidden=CaptureSky.Resolve(wrong,null);
    Check(hidden.HasHorizon&&hidden.Altitude<0&&hidden.BodyBelowHorizon&&!hidden.BelowHorizon&&hidden.Summary.Contains("timezone"),"A below-horizon Moon hid its map or gave no clock hint");
    // An unzoned clock or a bare date still places the Moon on the celestial map, within a few degrees.
    var exact=CaptureSky.Resolve(new Frame{Target="Moon",Kind="Light",Observed="2025-03-14T06:58:43Z",TimeSource="FITS UTC"},null);
    var unzoned=CaptureSky.Resolve(new Frame{Target="Moon",Kind="Light",Observed="2025-03-14T08:58:43",TimeSource="Capture timestamp",RA=0,Dec=0},null);
    Check(unzoned.HasPosition&&!unzoned.HasHorizon&&unzoned.Body=="Moon"&&MoonEphemeris.Separation(unzoned.TargetOfDate,exact.TargetOfDate)<1.5,"An unzoned lunar clock lost its Moon position");
    var dated=CaptureSky.Resolve(new Frame{Target="Moon",Kind="Light",Observed="2025-03-14",TimeSource="FITS date only"},null);
    Check(dated.HasPosition&&!dated.HasHorizon&&MoonEphemeris.Separation(dated.TargetOfDate,exact.TargetOfDate)<8,"A date-only lunar capture lost its Moon position");
    Check(!CaptureSky.Resolve(new Frame{Target="Mars",Kind="Light",Observed="2025-03-14T08:58:43"},null).HasPosition,"A planet was placed without a clock");
    // A stored pointing far from the Moon at the capture time is stale and must not place the target marker; a nearby one or a real solve is kept.
    var stale=CaptureSky.Resolve(new Frame{Target="Moon",Kind="Stack",Observed="2025-03-14T06:58:43Z",TimeSource="FITS UTC",RA=SkyOrientation.Wrap(exact.RA+90),Dec=-exact.Dec},null);
    Check(stale.HasPosition&&stale.PositionOfDate&&MoonEphemeris.Separation(stale.TargetOfDate,exact.TargetOfDate)<0.01&&stale.Evidence.Contains("is not used"),"A stale lunar pointing placed the Moon target far from the Moon");
    var staleDated=CaptureSky.Resolve(new Frame{Target="Moon",Kind="Stack",Observed="2025-03-14T08:58:43",TimeSource="Capture timestamp",RA=SkyOrientation.Wrap(exact.RA+90),Dec=-exact.Dec},null);
    Check(staleDated.HasPosition&&MoonEphemeris.Separation(staleDated.TargetOfDate,exact.TargetOfDate)<1.5,"A stale lunar pointing survived an unzoned clock");
    var near=CaptureSky.Resolve(new Frame{Target="Moon",Kind="Stack",Observed="2025-03-14T06:58:43Z",TimeSource="FITS UTC",RA=SkyOrientation.Wrap(exact.RA+1.5),Dec=exact.Dec},null);
    Check(near.HasPosition&&!near.PositionOfDate&&Math.Abs(near.RA-SkyOrientation.Wrap(exact.RA+1.5))<1e-9,"A pointing near the Moon was replaced");
    var solved=CaptureSky.Resolve(new Frame{Target="Moon",Kind="Stack",Observed="2025-03-14T06:58:43Z",TimeSource="FITS UTC",Sky=new SkyGeometry{RA=SkyOrientation.Wrap(exact.RA+90),Dec=-exact.Dec,Evidence="Plate solved"}},null);
    Check(solved.HasPosition&&!solved.PositionOfDate&&Math.Abs(solved.RA-SkyOrientation.Wrap(exact.RA+90))<1e-9,"A plate-solved lunar position was replaced by the calculated Moon");
    var deepSky=new Frame{Target="M31",Kind="Light",Observed="2024-04-08T18:17:16Z",TimeSource="FITS UTC",Latitude=25.29,Longitude=-104.14};Check(CaptureSky.Resolve(deepSky,null).Eclipse==null,"A deep-sky capture was given an eclipse");
    var sections=TargetNavigation.Build(new[]{sun,new Frame{Target="Sun",Kind="Light"}},f=>f==sun?Eclipses.SolarSection:f.Target);
    Check(sections.Any(t=>t.Name==Eclipses.SolarSection&&t.Files==1&&t.Group=="Solar system")&&sections.Any(t=>t.Name=="Sun"&&t.Files==1),"Eclipse captures did not list in their own Solar system section");
   });
   Test("Disc overlap covers none, partial, contained and equal discs",()=>{
    Check(Eclipses.Overlap(1,1,2.5)==0&&Math.Abs(Eclipses.Overlap(1,0.5,0.2)-Math.PI*0.25)<1e-12&&Math.Abs(Eclipses.Overlap(1,1,0)-Math.PI)<1e-12,"Overlap limits are wrong");
    double half=Eclipses.Overlap(1,1,1);Check(Math.Abs(half-(2*Math.PI/3-Math.Sqrt(3)/2))<1e-9,"Lens area is wrong: "+half);
   });
   Test("Eclipse sections need a Sun or Moon capture time, and a site for the Sun",()=>{
    var total=Utc(2024,4,8,18,17,16);
    Check(Eclipses.For("Sun",total,true,25.29,-104.14).Kind=="Total"&&Eclipses.For("Sun",total,false,0,0)==null&&Eclipses.For("Sun",null,true,25.29,-104.14)==null,"Solar eclipse section did not require a time and site");
    Check(Eclipses.For("Moon",Utc(2025,3,14,6,58,43),false,0,0).Kind=="Total"&&Eclipses.For("Jupiter",total,true,25.29,-104.14)==null,"Lunar or non-eclipse bodies were misclassified");
   });
  }
 }
}
