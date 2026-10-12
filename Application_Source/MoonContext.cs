// Display-only Moon context at a recorded capture time. Main periodic terms from
// Meeus, Astronomical Algorithms ch. 25/47/48: about 0.01° in position, which is
// far finer than the drawn disc, elevations or rounded separations need.
using System;
using System.Globalization;

namespace AstroArchive {
 public sealed class MoonSnapshot {
  public DateTime Utc;
  // Unit vectors in the true equator/equinox of date (not J2000).
  public SkyVector Geocentric,Topocentric,Sun;
  public double DistanceKm,Illumination,PhaseAngle,Elongation;
  public bool Waxing,HasHorizon,HasTarget,HasTopocentric;
  public double Altitude,Azimuth,TargetSeparation;
  public string PhaseName{get{return MoonEphemeris.PhaseName(Illumination,Waxing);}}
  public string IlluminationText{get{return MoonEphemeris.Percent(Illumination);}}
  // Southern observers see the familiar waxing crescent mirrored.
  public bool SouthernView;
 }
 public static class MoonEphemeris {
  const double Degrees=Math.PI/180,EarthRadiusKm=6378.14,AuKm=149597870.7;
  // Rows: D, M, M', F, longitude (1e-6 °), distance (1e-3 km).
  static readonly int[,] LongitudeDistance={
   {0,0,1,0,6288774,-20905355},{2,0,-1,0,1274027,-3699111},{2,0,0,0,658314,-2955968},{0,0,2,0,213618,-569925},
   {0,1,0,0,-185116,48888},{0,0,0,2,-114332,-3149},{2,0,-2,0,58793,246158},{2,-1,-1,0,57066,-152138},
   {2,0,1,0,53322,-170733},{2,-1,0,0,45758,-204586},{0,1,-1,0,-40923,-129620},{1,0,0,0,-34720,108743},
   {0,1,1,0,-30383,104755},{2,0,0,-2,15327,10321},{0,0,1,2,-12528,0},{0,0,1,-2,10980,79661},
   {4,0,-1,0,10675,-34782},{0,0,3,0,10034,-23210},{4,0,-2,0,8548,-21636},{2,1,-1,0,-7888,24208},
   {2,1,0,0,-6766,30824},{1,0,-1,0,-5163,-8379},{1,1,0,0,4987,-16675},{2,-1,1,0,4036,-12831},
   {2,0,2,0,3994,-10445},{4,0,0,0,3861,-11650},{2,0,-3,0,3665,14403},{0,1,-2,0,-2689,-7003},
   {2,0,-1,2,-2602,0},{2,-1,-2,0,2390,10056},{1,0,1,0,-2348,6322},{2,-2,0,0,2236,-9884}
  };
  // Rows: D, M, M', F, latitude (1e-6 °).
  static readonly int[,] Latitude={
   {0,0,0,1,5128122},{0,0,1,1,280602},{0,0,1,-1,277693},{2,0,0,-1,173237},{2,0,-1,1,55413},
   {2,0,-1,-1,46271},{2,0,0,1,32573},{0,0,2,1,17198},{2,0,1,-1,9266},{0,0,2,-1,8822},
   {2,-1,0,-1,8216},{2,0,-2,-1,4324},{2,0,1,1,4200},{2,1,0,-1,-3359},{2,-1,-1,1,2463},
   {2,-1,0,1,2211},{2,-1,-1,-1,2065},{0,1,-1,-1,-1870},{4,0,-1,-1,1828},{0,1,0,1,-1794},
   {0,0,0,3,-1749},{0,1,-1,1,-1565},{1,0,0,1,-1491},{0,1,1,1,-1475},{0,1,1,-1,-1410},
   {0,1,0,-1,-1344},{1,0,0,-1,-1335},{0,0,3,1,1107},{4,0,0,-1,1021},{4,0,-1,1,833}
  };
  static double Wrap(double degrees){return (degrees%360+360)%360;}
  static double Sin(double degrees){return Math.Sin(degrees*Degrees);}
  static double Cos(double degrees){return Math.Cos(degrees*Degrees);}
  // Terrestrial time differs from UTC by about a minute; the Moon moves ~0.01° in that time.
  static double DeltaT(DateTime utc){double y=utc.Year+(utc.DayOfYear-0.5)/365.25-2000;return y>=-20&&y<=50?62.92+0.32217*y+0.005589*y*y:69;}
  static double Centuries(DateTime utc){return (utc.AddSeconds(DeltaT(utc))-new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)).TotalDays/36525;}
  // Nutation in longitude and true obliquity, degrees.
  static void Nutation(double t,out double longitude,out double obliquity){
   double omega=125.04452-1934.136261*t,sun=280.4665+36000.7698*t,moon=218.3165+481267.8813*t;
   longitude=(-17.20*Sin(omega)-1.32*Sin(2*sun)-0.23*Sin(2*moon)+0.21*Sin(2*omega))/3600;
   double mean=23.0+26.0/60+21.448/3600-(46.8150*t+0.00059*t*t-0.001813*t*t*t)/3600;
   obliquity=mean+(9.20*Cos(omega)+0.57*Cos(2*sun)+0.10*Cos(2*moon)-0.09*Cos(2*omega))/3600;
  }
  // Apparent geocentric ecliptic longitude/latitude (degrees) and distance (km).
  public static void Moon(DateTime utc,out double longitude,out double latitude,out double distanceKm){
   double t=Centuries(utc),t2=t*t,t3=t2*t,t4=t3*t;
   double l=Wrap(218.3164477+481267.88123421*t-0.0015786*t2+t3/538841-t4/65194000);
   double d=Wrap(297.8501921+445267.1114034*t-0.0018819*t2+t3/545868-t4/113065000);
   double m=Wrap(357.5291092+35999.0502909*t-0.0001536*t2+t3/24490000);
   double mp=Wrap(134.9633964+477198.8675055*t+0.0087414*t2+t3/69699-t4/14712000);
   double f=Wrap(93.2720950+483202.0175233*t-0.0036539*t2-t3/3526000+t4/863310000);
   double e=1-0.002516*t-0.0000074*t2,a1=Wrap(119.75+131.849*t),a2=Wrap(53.09+479264.290*t),a3=Wrap(313.45+481266.484*t);
   double sl=0,sr=0,sb=0;
   for(int i=0;i<LongitudeDistance.GetLength(0);i++){
    double argument=LongitudeDistance[i,0]*d+LongitudeDistance[i,1]*m+LongitudeDistance[i,2]*mp+LongitudeDistance[i,3]*f,scale=Math.Pow(e,Math.Abs(LongitudeDistance[i,1]));
    sl+=LongitudeDistance[i,4]*scale*Sin(argument);sr+=LongitudeDistance[i,5]*scale*Cos(argument);
   }
   for(int i=0;i<Latitude.GetLength(0);i++){double argument=Latitude[i,0]*d+Latitude[i,1]*m+Latitude[i,2]*mp+Latitude[i,3]*f;sb+=Latitude[i,4]*Math.Pow(e,Math.Abs(Latitude[i,1]))*Sin(argument);}
   sl+=3958*Sin(a1)+1962*Sin(l-f)+318*Sin(a2);
   sb+=-2235*Sin(l)+382*Sin(a3)+175*Sin(a1-f)+175*Sin(a1+f)+127*Sin(l-mp)-115*Sin(l+mp);
   double nutation,obliquity;Nutation(t,out nutation,out obliquity);
   longitude=Wrap(l+sl/1000000+nutation);latitude=sb/1000000;distanceKm=385000.56+sr/1000;
  }
  // Apparent geocentric ecliptic longitude (degrees) and distance (km) of the Sun.
  public static void Sun(DateTime utc,out double longitude,out double distanceKm){
   double t=Centuries(utc),l0=Wrap(280.46646+36000.76983*t+0.0003032*t*t),m=Wrap(357.52911+35999.05029*t-0.0001537*t*t),e=0.016708634-0.000042037*t-0.0000001267*t*t;
   double c=(1.914602-0.004817*t-0.000014*t*t)*Sin(m)+(0.019993-0.000101*t)*Sin(2*m)+0.000289*Sin(3*m),v=m+c,omega=125.04-1934.136*t;
   longitude=Wrap(l0+c-0.00569-0.00478*Sin(omega));distanceKm=1.000001018*(1-e*e)/(1+e*Cos(v))*AuKm;
  }
  static SkyVector Equatorial(double longitude,double latitude,double obliquity){
   double cb=Cos(latitude),x=cb*Cos(longitude),y=cb*Sin(longitude),z=Sin(latitude),ce=Cos(obliquity),se=Sin(obliquity);
   return new SkyVector(x,y*ce-z*se,y*se+z*ce);
  }
  static SkyVector Unit(double x,double y,double z){double n=Math.Sqrt(x*x+y*y+z*z);return n<=0?new SkyVector(0,0,1):new SkyVector(x/n,y/n,z/n);}
  public static SkyVector Toward(SkyVector from,SkyVector to,double step){
   // A short step along the great circle from one direction toward another.
   double along=from.Dot(to);double x=to.X-along*from.X,y=to.Y-along*from.Y,z=to.Z-along*from.Z,n=Math.Sqrt(x*x+y*y+z*z);
   if(n<1e-12)return from;return Unit(from.X+step*x/n,from.Y+step*y/n,from.Z+step*z/n);
  }
  public static double Separation(SkyVector a,SkyVector b){return Math.Acos(Math.Max(-1,Math.Min(1,a.Dot(b))))/Degrees;}
  // Site is optional: without it the Moon is geocentric and has no elevation.
  public static MoonSnapshot At(DateTime utc,bool site,double latitude,double longitude){
   if(utc.Kind!=DateTimeKind.Utc)utc=DateTime.SpecifyKind(utc,DateTimeKind.Utc);
   double moonLongitude,moonLatitude,distance,sunLongitude,sunDistance,nutation,obliquity;
   Moon(utc,out moonLongitude,out moonLatitude,out distance);Sun(utc,out sunLongitude,out sunDistance);Nutation(Centuries(utc),out nutation,out obliquity);
   var snapshot=new MoonSnapshot{Utc=utc,DistanceKm=distance,SouthernView=site&&latitude<0};
   snapshot.Geocentric=Equatorial(moonLongitude,moonLatitude,obliquity);snapshot.Sun=Equatorial(sunLongitude,0,obliquity);snapshot.Topocentric=snapshot.Geocentric;
   double elongation=Math.Acos(Math.Max(-1,Math.Min(1,Cos(moonLatitude)*Cos(moonLongitude-sunLongitude))));
   double phase=Math.Atan2(sunDistance*Math.Sin(elongation),distance-sunDistance*Math.Cos(elongation));
   snapshot.Elongation=elongation/Degrees;snapshot.PhaseAngle=phase/Degrees;snapshot.Illumination=(1+Math.Cos(phase))/2;snapshot.Waxing=Wrap(moonLongitude-sunLongitude)<180;
   if(site){
    // Geocentric parallax shifts the Moon by up to a degree for a real observer.
    double lst=SkyOrientation.Sidereal(utc,longitude),u=Math.Atan(0.99664719*Math.Tan(latitude*Degrees)),rs=0.99664719*Math.Sin(u),rc=Math.Cos(u),r=distance/EarthRadiusKm;
    snapshot.Topocentric=Unit(r*snapshot.Geocentric.X-rc*Cos(lst),r*snapshot.Geocentric.Y-rc*Sin(lst),r*snapshot.Geocentric.Z-rs);snapshot.HasTopocentric=true;
   }
   return snapshot;
  }
  public static string Percent(double illumination){
   double percent=Math.Max(0,Math.Min(100,illumination*100));
   // Never round a sliver to 0% or a nearly full disc to 100%.
   int rounded=(int)Math.Round(percent,MidpointRounding.AwayFromZero);if(rounded==0&&percent>0.05)rounded=1;if(rounded==100&&percent<99.95)rounded=99;
   return rounded.ToString(CultureInfo.InvariantCulture)+"%";
  }
  public static string PhaseName(double illumination,bool waxing){
   if(illumination<0.03)return "New Moon";if(illumination>0.97)return "Full Moon";
   if(Math.Abs(illumination-0.5)<=0.05)return waxing?"First quarter":"Last quarter";
   return (waxing?"Waxing ":"Waning ")+(illumination<0.5?"crescent":"gibbous");
  }
 }
}
