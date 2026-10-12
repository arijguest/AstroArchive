using System;
using System.Globalization;

namespace AstroArchive {
 // Eclipse geometry from the bundled Meeus Moon/Sun models; no download or table.
 // A solar eclipse is local: the Moon is shifted by up to a degree of parallax, so
 // it needs the observer's location. A lunar eclipse is the same for everyone who
 // can see the Moon, so it needs only the capture time.
 public sealed class EclipseInfo {
  public bool Solar;public string Kind;
  // Solar: fraction of the Sun's disc covered. Lunar: fraction of the Moon's disc inside the umbra.
  public double Obscuration;
  // Solar: fraction of the Sun's diameter covered. Lunar: umbral magnitude (penumbral when not umbral).
  public double Magnitude,PenumbralMagnitude;
  public double SeparationArcmin,SunRadiusArcmin,MoonRadiusArcmin,ShadowRadiusArcmin,PenumbraRadiusArcmin;
  public string Title{get{return Solar?"Solar eclipse":"Lunar eclipse";}}
  public string Summary{get{
   string percent=(Obscuration*100).ToString(Obscuration>=0.995||Obscuration<0.1?"0.#":"0",CultureInfo.InvariantCulture)+"%";
   if(Solar)return Kind+" · "+percent+" of the Sun covered · magnitude "+Magnitude.ToString("0.000",CultureInfo.InvariantCulture);
   if(Kind=="Penumbral")return "Penumbral · Moon in Earth's outer shadow · penumbral magnitude "+PenumbralMagnitude.ToString("0.000",CultureInfo.InvariantCulture);
   return Kind+" · "+percent+" of the Moon in Earth's umbra · umbral magnitude "+Magnitude.ToString("0.000",CultureInfo.InvariantCulture);
  }}
 }
 public static class Eclipses {
  const double Degrees=Math.PI/180,EarthRadiusKm=6378.137,MoonRadiusKm=1737.4,SunRadiusKm=695700,AuKm=149597870.7;
  public const string SolarSection="Solar Eclipse",LunarSection="Lunar Eclipse";
  public static bool IsSection(string name){return name==SolarSection||name==LunarSection;}
  // Area shared by two discs of radius a and b whose centres are d apart.
  public static double Overlap(double a,double b,double d){
   if(a<=0||b<=0)return 0;if(d>=a+b)return 0;if(d<=Math.Abs(a-b)){double r=Math.Min(a,b);return Math.PI*r*r;}
   double alpha=Math.Acos(Math.Max(-1,Math.Min(1,(d*d+a*a-b*b)/(2*d*a)))),beta=Math.Acos(Math.Max(-1,Math.Min(1,(d*d+b*b-a*a)/(2*d*b))));
   return a*a*alpha+b*b*beta-0.5*Math.Sqrt(Math.Max(0,(-d+a+b)*(d+a-b)*(d-a+b)*(d+a+b)));
  }
  static double Arcmin(double radians){return radians/Degrees*60;}
  static void Vectors(DateTime utc,out SkyVector moon,out double moonKm,out SkyVector sun,out double sunKm){
   var snapshot=MoonEphemeris.At(utc,false,0,0);double longitude;MoonEphemeris.Sun(utc,out longitude,out sunKm);
   moon=snapshot.Geocentric;moonKm=snapshot.DistanceKm;sun=snapshot.Sun;
  }
  // Null unless the Moon covers part of the Sun as seen from this site.
  public static EclipseInfo Solar(DateTime utc,double latitude,double longitude){
   SkyVector moon,sun;double moonKm,sunKm;Vectors(utc,out moon,out moonKm,out sun,out sunKm);
   double lst=SkyOrientation.Sidereal(utc,longitude)*Degrees,u=Math.Atan(0.99664719*Math.Tan(latitude*Degrees)),rs=0.99664719*Math.Sin(u)*EarthRadiusKm,rc=Math.Cos(u)*EarthRadiusKm;
   double ox=rc*Math.Cos(lst),oy=rc*Math.Sin(lst),oz=rs;
   double mx=moon.X*moonKm-ox,my=moon.Y*moonKm-oy,mz=moon.Z*moonKm-oz,md=Math.Sqrt(mx*mx+my*my+mz*mz);
   double sx=sun.X*sunKm-ox,sy=sun.Y*sunKm-oy,sz=sun.Z*sunKm-oz,sd=Math.Sqrt(sx*sx+sy*sy+sz*sz);
   double separation=Math.Acos(Math.Max(-1,Math.Min(1,(mx*sx+my*sy+mz*sz)/(md*sd))));
   double sunRadius=Math.Asin(SunRadiusKm/sd),moonRadius=Math.Asin(MoonRadiusKm/md);
   if(separation>=sunRadius+moonRadius)return null;
   var info=new EclipseInfo{Solar=true,SeparationArcmin=Arcmin(separation),SunRadiusArcmin=Arcmin(sunRadius),MoonRadiusArcmin=Arcmin(moonRadius)};
   info.Obscuration=Math.Min(1,Overlap(sunRadius,moonRadius,separation)/(Math.PI*sunRadius*sunRadius));
   // Fraction of the Sun's diameter covered; once the Moon is wholly inside (annular)
   // or covers the Sun (total), the published convention is the ratio of the diameters.
   bool central=separation<=Math.Abs(sunRadius-moonRadius);
   info.Magnitude=central?moonRadius/sunRadius:(sunRadius+moonRadius-separation)/(2*sunRadius);
   info.Kind=central?(moonRadius>=sunRadius?"Total":"Annular"):"Partial";
   return info;
  }
  // Null unless the Moon is at least partly inside Earth's penumbra (Danjon's shadow
  // enlargement, as used for published magnitudes).
  public static EclipseInfo Lunar(DateTime utc){
   SkyVector moon,sun;double moonKm,sunKm;Vectors(utc,out moon,out moonKm,out sun,out sunKm);
   double separation=Math.Acos(Math.Max(-1,Math.Min(1,-moon.Dot(sun))));
   double moonParallax=Math.Asin(EarthRadiusKm/moonKm),sunParallax=Math.Asin(EarthRadiusKm/sunKm),sunRadius=Math.Asin(SunRadiusKm/sunKm),moonRadius=Math.Asin(MoonRadiusKm/moonKm);
   double umbra=1.01*moonParallax-sunRadius+sunParallax,penumbra=1.01*moonParallax+sunRadius+sunParallax;
   double penumbral=(penumbra+moonRadius-separation)/(2*moonRadius);if(penumbral<=0)return null;
   var info=new EclipseInfo{Solar=false,SeparationArcmin=Arcmin(separation),MoonRadiusArcmin=Arcmin(moonRadius),ShadowRadiusArcmin=Arcmin(umbra),PenumbraRadiusArcmin=Arcmin(penumbra),PenumbralMagnitude=penumbral};
   info.Magnitude=(umbra+moonRadius-separation)/(2*moonRadius);
   info.Obscuration=Math.Min(1,Overlap(umbra,moonRadius,separation)/(Math.PI*moonRadius*moonRadius));
   info.Kind=info.Magnitude>=1?"Total":info.Magnitude>0?"Partial":"Penumbral";
   return info;
  }
  // The eclipse a capture of the Sun or Moon shows, if any. Solar eclipses need a site.
  public static EclipseInfo For(string body,DateTime? utc,bool site,double latitude,double longitude){
   if(!utc.HasValue||body==null)return null;
   if(body=="Sun")return site?Solar(utc.Value,latitude,longitude):null;
   if(body=="Moon"){var solar=site?Solar(utc.Value,latitude,longitude):null;return solar??Lunar(utc.Value);}
   return null;
  }
 }
}
