using System;
namespace AstroArchive {
 public partial class Tests {
  static double RightAscension(SkyVector v){return SkyOrientation.Wrap(Math.Atan2(v.Y,v.X)*180/Math.PI);}
  static double Declination(SkyVector v){return Math.Asin(Math.Max(-1,Math.Min(1,v.Z)))*180/Math.PI;}
  // Meeus examples are in dynamical time; subtract the same ΔT the engine adds.
  static DateTime Dynamical(int year,int month,int day){var td=new DateTime(year,month,day,0,0,0,DateTimeKind.Utc);double y=year+(td.DayOfYear-0.5)/365.25-2000;return td.AddSeconds(-(62.92+0.32217*y+0.005589*y*y));}
  static void MoonContextTests(){
   Test("Moon position, distance and phase match Meeus worked examples",()=>{
    double longitude,latitude,distance;MoonEphemeris.Moon(Dynamical(1992,4,12),out longitude,out latitude,out distance);
    Check(Math.Abs(longitude-133.167265)<0.02&&Math.Abs(latitude+3.229126)<0.01&&Math.Abs(distance-368409.7)<30,"Moon ecliptic position drifted: "+longitude+", "+latitude+", "+distance);
    var moon=MoonEphemeris.At(Dynamical(1992,4,12),false,0,0);
    Check(Math.Abs(RightAscension(moon.Geocentric)-134.688470)<0.02&&Math.Abs(Declination(moon.Geocentric)-13.768368)<0.02,"Moon equatorial position drifted");
    Check(Math.Abs(moon.Illumination-0.6786)<0.005&&moon.Waxing&&moon.PhaseName=="Waxing gibbous"&&moon.IlluminationText=="68%","Moon phase drifted: "+moon.Illumination);
    double sun,sunDistance;MoonEphemeris.Sun(Dynamical(1992,10,13),out sun,out sunDistance);
    Check(Math.Abs(sun-199.90895)<0.01&&Math.Abs(sunDistance/149597870.7-0.99766)<0.0001,"Sun position drifted: "+sun);
   });
   Test("Moon phases follow recent new, quarter and full Moons",()=>{
    var full=MoonEphemeris.At(new DateTime(2024,9,18,2,34,0,DateTimeKind.Utc),false,0,0);Check(full.Illumination>0.99&&full.PhaseName=="Full Moon","Full Moon not full: "+full.Illumination);
    var fresh=MoonEphemeris.At(new DateTime(2024,10,2,18,49,0,DateTimeKind.Utc),false,0,0);Check(fresh.Illumination<0.01&&MoonEphemeris.Separation(fresh.Geocentric,fresh.Sun)<1&&fresh.PhaseName=="New Moon","Eclipse new Moon misplaced: "+fresh.Illumination);
    var quarter=MoonEphemeris.At(new DateTime(2024,10,10,18,55,0,DateTimeKind.Utc),false,0,0);Check(Math.Abs(quarter.Illumination-0.5)<0.02&&quarter.Waxing&&quarter.PhaseName=="First quarter","First quarter wrong: "+quarter.Illumination);
    var last=MoonEphemeris.At(new DateTime(2024,10,24,8,3,0,DateTimeKind.Utc),false,0,0);Check(Math.Abs(last.Illumination-0.5)<0.02&&!last.Waxing&&last.PhaseName=="Last quarter","Last quarter wrong: "+last.Illumination);
    var crescent=MoonEphemeris.At(new DateTime(2024,10,5,18,0,0,DateTimeKind.Utc),false,0,0);Check(crescent.Waxing&&crescent.Illumination<0.2&&crescent.PhaseName=="Waxing crescent","Waxing crescent misnamed");
   });
   Test("Moon percentages never hide a sliver or overstate a nearly full disc",()=>{
    Check(MoonEphemeris.Percent(0.001)=="1%"&&MoonEphemeris.Percent(0.0001)=="0%"&&MoonEphemeris.Percent(0.9993)=="99%"&&MoonEphemeris.Percent(1)=="100%"&&MoonEphemeris.Percent(0.625)=="63%","Moon percentage rounding misleading");
    Check(MoonEphemeris.PhaseName(0.5,true)=="First quarter"&&MoonEphemeris.PhaseName(0.4,false)=="Waning crescent"&&MoonEphemeris.PhaseName(0.8,false)=="Waning gibbous","Phase names inconsistent");
   });
   Test("Moon parallax lowers its elevation and site context stays optional",()=>{
    var frame=new Frame{Target="M42",ObservedUtc="2024-10-10T21:00:00Z",Latitude=51.5,Longitude=-0.1};var sky=CaptureSky.Resolve(frame,null);
    Check(sky.Moon!=null&&sky.Moon.HasHorizon&&sky.Moon.HasTopocentric&&sky.Moon.HasTarget,"Capture with time and site lacks Moon context");
    var geocentric=sky.Orientation.MapOfDate(sky.Moon.Geocentric);double geocentricAltitude=Math.Asin(geocentric.Z)*180/Math.PI,parallax=Math.Asin(6378.14/sky.Moon.DistanceKm)*180/Math.PI;
    Check(Math.Abs(geocentricAltitude-sky.Moon.Altitude-parallax*Math.Cos(geocentricAltitude*Math.PI/180))<0.1,"Topocentric parallax wrong: "+geocentricAltitude+" vs "+sky.Moon.Altitude);
    Check(sky.Moon.TargetSeparation>0&&sky.Moon.TargetSeparation<180,"Target separation invalid");
    frame.Latitude=null;frame.Longitude=null;var celestial=CaptureSky.Resolve(frame,null);Check(celestial.Moon!=null&&!celestial.Moon.HasHorizon&&!celestial.Moon.HasTopocentric&&celestial.Moon.HasTarget,"Missing site invented a Moon elevation");
    frame.ObservedUtc=null;Check(CaptureSky.Resolve(frame,null).Moon==null,"Missing capture clock invented a Moon");
    frame.ObservedUtc="2024-10-10T21:00:00Z";frame.Latitude=-33.9;frame.Longitude=151.2;Check(CaptureSky.Resolve(frame,null).Moon.SouthernView,"Southern observer view not flagged");
   });
   Test("Moon separation compares the target precessed to the same date",()=>{
    // 1992-04-12 Moon (of date) is ~0.1° from these J2000 coordinates after 8 years of precession.
    var frame=new Frame{Target="Unknown",RA=134.688,Dec=13.768,ObservedUtc=Dynamical(1992,4,12).ToString("o")};var sky=CaptureSky.Resolve(frame,null);
    Check(sky.Moon!=null&&sky.Moon.TargetSeparation<0.3,"Separation ignored precession or epoch: "+(sky.Moon==null?-1:sky.Moon.TargetSeparation));
    var away=new Frame{Target="Unknown",RA=314.688,Dec=-13.768,ObservedUtc=frame.ObservedUtc};Check(CaptureSky.Resolve(away,null).Moon.TargetSeparation>179.5,"Opposite target separation wrong");
   });
   Test("Moon step toward the Sun stays on the unit sphere",()=>{
    var moon=MoonEphemeris.At(new DateTime(2024,10,10,18,55,0,DateTimeKind.Utc),false,0,0);var step=MoonEphemeris.Toward(moon.Geocentric,moon.Sun,0.02);
    Check(Math.Abs(step.X*step.X+step.Y*step.Y+step.Z*step.Z-1)<1e-9&&MoonEphemeris.Separation(step,moon.Sun)<MoonEphemeris.Separation(moon.Geocentric,moon.Sun),"Bright-limb direction does not point toward the Sun");
   });
  }
 }
}
