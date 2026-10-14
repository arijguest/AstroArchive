using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 // The corona drawn on the sky map for a total eclipse. Its shape follows the solar cycle (long equatorial streamers near
 // minimum, a rounder crown near maximum) and its axis follows the Sun's rotation axis, so the streamers lie along the
 // solar equator as drawn. Everything is computed; no imagery is bundled.
 public static class SolarCorona {
  // Sunspot cycle extremes (decimal years), minimum then maximum alternately, from the published smoothed sunspot numbers.
  // The last entries are the cycle 25 maximum and a projected minimum; later dates continue at the mean half-cycle.
  static readonly double[] Extremes={1954.3,1958.2,1964.8,1968.9,1976.5,1979.9,1986.7,1989.9,1996.4,2001.9,2008.9,2014.3,2019.9,2024.8,2030.9};
  const double HalfCycle=5.5;
  public static double DecimalYear(DateTime utc){var start=new DateTime(utc.Year,1,1,0,0,0,DateTimeKind.Utc);var end=start.AddYears(1);return utc.Year+(utc.ToUniversalTime()-start).TotalDays/(end-start).TotalDays;}
  // 0 at sunspot minimum, 1 at maximum, a smooth rise and fall between the dated extremes.
  public static double Activity(double year){
   var list=Extremes.ToList();int front=0;while(year<list[0]){list.Insert(0,list[0]-HalfCycle);front++;}while(year>list[list.Count-1])list.Add(list[list.Count-1]+HalfCycle);
   for(int i=0;i<list.Count-1;i++)if(year>=list[i]&&year<=list[i+1]){double f=(year-list[i])/(list[i+1]-list[i]),smooth=(1-Math.Cos(Math.PI*f))/2;bool startsAtMinimum=(i-front)%2==0;return startsAtMinimum?smooth:1-smooth;}
   return 0.5;
  }
  public static double Activity(DateTime utc){return Activity(DecimalYear(utc));}
  // Position angle of the Sun's north pole, degrees east of celestial north (Meeus, Astronomical Algorithms, ch. 29).
  public static double PositionAngle(double julianDay,double apparentLongitude){
   double rad=Math.PI/180,t=(julianDay-2451545)/36525,omega=(125.04-1934.136*t)*rad;
   double epsilon=(23.4392911-0.0130042*t+0.00256*Math.Cos(omega))*rad,lambda=apparentLongitude*rad;
   double k=(73.6667+1.3958333*(julianDay-2396758)/36525)*rad,inclination=7.25*rad;
   double x=Math.Atan(-Math.Cos(lambda)*Math.Tan(epsilon)),y=Math.Atan(-Math.Cos(lambda-k)*Math.Tan(inclination));
   return (x+y)/rad;
  }
  public static double PositionAngle(DateTime utc){double longitude,km;MoonEphemeris.Sun(utc,out longitude,out km);return PositionAngle(OrbitalModel.Julian(utc),longitude);}
  // Screen angle (radians, y down) of the Sun's north pole: celestial north at the Sun, turned east by the position angle.
  public static double ScreenAngle(SkyVector sun,SkyVector celestialPole,double positionAngleDegrees,SkyVector right,SkyVector up){
   double pole=celestialPole.Dot(sun);double nx=celestialPole.X-pole*sun.X,ny=celestialPole.Y-pole*sun.Y,nz=celestialPole.Z-pole*sun.Z;
   double n=Math.Sqrt(nx*nx+ny*ny+nz*nz);if(n<1e-9)return -Math.PI/2;nx/=n;ny/=n;nz/=n;
   // East is the direction of increasing right ascension: pole x sun.
   double ex=celestialPole.Y*sun.Z-celestialPole.Z*sun.Y,ey=celestialPole.Z*sun.X-celestialPole.X*sun.Z,ez=celestialPole.X*sun.Y-celestialPole.Y*sun.X;
   double e=Math.Sqrt(ex*ex+ey*ey+ez*ez);if(e<1e-9)return -Math.PI/2;ex/=e;ey/=e;ez/=e;
   double p=positionAngleDegrees*Math.PI/180,ax=Math.Cos(p)*nx+Math.Sin(p)*ex,ay=Math.Cos(p)*ny+Math.Sin(p)*ey,az=Math.Cos(p)*nz+Math.Sin(p)*ez;
   var a=new SkyVector(ax,ay,az);return Math.Atan2(-a.Dot(up),a.Dot(right));
  }
  // Outer extent in solar radii at an angle from the solar equator (radians). East and west differ a little, as real streamers do.
  public static double Reach(double activity,double fromEquator,bool west){
   double equator=3.7-1.3*activity,pole=1.35+0.85*activity,sharp=5.5-3.5*activity;
   double c=Math.Cos(fromEquator),reach=pole+(equator-pole)*Math.Pow(Math.Abs(c),sharp);
   // Short polar plumes near minimum, about 12 degrees either side of each pole.
   reach+=0.35*(1-activity)*Math.Exp(-Math.Pow((Math.Acos(Math.Min(1,Math.Abs(c)))-78*Math.PI/180)/(8*Math.PI/180),2));
   if((c<0)!=west)reach*=0.9;return reach*(1+0.05*Math.Sin(3*fromEquator+(west?1.3:0.4)));
  }
  public static readonly double[] LayerScale={1,0.8,0.62,0.47,0.34,0.23,0.14};
 }
}
