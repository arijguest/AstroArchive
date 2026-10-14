using System;
using System.Globalization;

namespace AstroArchive {
 // Eclipse geometry from the bundled Meeus Moon/Sun models; no download or table.
 // A solar eclipse is local: the Moon is shifted by up to a degree of parallax, so
 // it needs the observer's location. A lunar eclipse is the same for everyone who
 // can see the Moon, so it needs only the capture time.
 public sealed class EclipseInfo {
  public const double TotalityThreshold=0.99;
  public bool Solar;public string Kind;
  // Solar: fraction of the Sun's disc covered. Lunar: fraction of the Moon's disc inside the umbra.
  public double Obscuration;
  // Solar: fraction of the Sun's diameter covered. Lunar: umbral magnitude (penumbral when not umbral).
  public double Magnitude,PenumbralMagnitude;
  public double SeparationArcmin,SunRadiusArcmin,MoonRadiusArcmin,ShadowRadiusArcmin,PenumbraRadiusArcmin;
  // Lunar: fraction of the Moon's disc inside the penumbra (the umbra included).
  public double PenumbralObscuration;
  public string Title{get{return Solar?"Solar eclipse":"Lunar eclipse";}}
  public string Summary{get{
   string percent=(Obscuration*100).ToString(Obscuration>=0.995||Obscuration<0.1?"0.#":"0",CultureInfo.InvariantCulture)+"%";
   if(Solar)return Kind+" · "+percent+" of the Sun covered · magnitude "+Magnitude.ToString("0.000",CultureInfo.InvariantCulture);
   if(Kind=="Penumbral")return "Penumbral · Moon in Earth's outer shadow · penumbral magnitude "+PenumbralMagnitude.ToString("0.000",CultureInfo.InvariantCulture);
   return Kind+" · "+percent+" of the Moon in Earth's umbra · umbral magnitude "+Magnitude.ToString("0.000",CultureInfo.InvariantCulture);
  }}
 }
 // One eclipse from start to end as seen by the capture: solar contacts are local to the
 // site, lunar contacts are the same for everyone who can see the Moon.
 public sealed class EclipseEvent {
  public bool Solar;public string Kind;public EclipseInfo Peak;
  // Solar: first and last contact. Lunar: entry into and exit from the penumbra.
  public DateTime Start,End,Greatest;
  // Lunar only: the Moon touching Earth's umbra (the partial phase).
  public DateTime? PartialStart,PartialEnd;
  // Solar: totality or annularity at the site. Lunar: totality.
  public DateTime? CentralStart,CentralEnd;
  // Evenly spaced from Start to End: covered fraction (solar disc, or Moon in the umbra),
  // and for lunar events the fraction of the Moon in the penumbra.
  public double[] Curve,PenumbralCurve;
  public TimeSpan Duration{get{return End-Start;}}
  // Where a time sits between Start and End, from 0 to 1.
  public double Position(DateTime utc){double span=(End-Start).TotalSeconds;return span<=0?0:Math.Max(0,Math.Min(1,(utc-Start).TotalSeconds/span));}
  public string Title{get{return Kind+" "+(Solar?"solar":"lunar")+" eclipse";}}
 }
 public static class Eclipses {
  const double Degrees=Math.PI/180,EarthRadiusKm=6378.137,MoonRadiusKm=1737.4,SunRadiusKm=695700,AuKm=149597870.7;
  public const string SolarSection="Solar Eclipse",LunarSection="Lunar Eclipse";
  public const string TotalityTag="Totality";
  // A solar eclipse covering 99% or more of the Sun reads as total: the corona shows and the capture is tagged.
  public static bool IsTotal(EclipseInfo e){return e!=null&&e.Solar&&e.Obscuration>=EclipseInfo.TotalityThreshold;}
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
   // An overlap below the local horizon is not an observable solar eclipse.
   // Check both ephemeris bodies independently of the file's recorded pointing.
   if(!ObservingCities.ValidCoordinates(latitude,longitude))return null;
   var moon=MoonEphemeris.At(utc,true,latitude,longitude);var orientation=new SkyOrientation(utc,true,latitude,longitude);
   double sunLongitude,sunKm;MoonEphemeris.Sun(utc,out sunLongitude,out sunKm);
   var sun=PlanetEphemeris.Topocentric(moon.Sun,sunKm/AuKm,utc,latitude,longitude);
   if(orientation.MapOfDate(sun).Z<0||orientation.MapOfDate(moon.Topocentric).Z<0)return null;
   var info=SolarGeometry(utc,latitude,longitude);return OuterMargin(info)>0?info:null;
  }
  // Disc sizes and separation at any time, eclipsed or not, so contacts can be found.
  static EclipseInfo SolarGeometry(DateTime utc,double latitude,double longitude){
   SkyVector moon,sun;double moonKm,sunKm;Vectors(utc,out moon,out moonKm,out sun,out sunKm);
   double lst=SkyOrientation.Sidereal(utc,longitude)*Degrees,u=Math.Atan(0.99664719*Math.Tan(latitude*Degrees)),rs=0.99664719*Math.Sin(u)*EarthRadiusKm,rc=Math.Cos(u)*EarthRadiusKm;
   double ox=rc*Math.Cos(lst),oy=rc*Math.Sin(lst),oz=rs;
   double mx=moon.X*moonKm-ox,my=moon.Y*moonKm-oy,mz=moon.Z*moonKm-oz,md=Math.Sqrt(mx*mx+my*my+mz*mz);
   double sx=sun.X*sunKm-ox,sy=sun.Y*sunKm-oy,sz=sun.Z*sunKm-oz,sd=Math.Sqrt(sx*sx+sy*sy+sz*sz);
   double separation=Math.Acos(Math.Max(-1,Math.Min(1,(mx*sx+my*sy+mz*sz)/(md*sd))));
   double sunRadius=Math.Asin(SunRadiusKm/sd),moonRadius=Math.Asin(MoonRadiusKm/md);
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
  public static EclipseInfo Lunar(DateTime utc){var info=LunarGeometry(utc);return OuterMargin(info)>0?info:null;}
  static EclipseInfo LunarGeometry(DateTime utc){
   SkyVector moon,sun;double moonKm,sunKm;Vectors(utc,out moon,out moonKm,out sun,out sunKm);
   double separation=Math.Acos(Math.Max(-1,Math.Min(1,-moon.Dot(sun))));
   double moonParallax=Math.Asin(EarthRadiusKm/moonKm),sunParallax=Math.Asin(EarthRadiusKm/sunKm),sunRadius=Math.Asin(SunRadiusKm/sunKm),moonRadius=Math.Asin(MoonRadiusKm/moonKm);
   double umbra=1.01*moonParallax-sunRadius+sunParallax,penumbra=1.01*moonParallax+sunRadius+sunParallax;
   double penumbral=(penumbra+moonRadius-separation)/(2*moonRadius);
   var info=new EclipseInfo{Solar=false,SeparationArcmin=Arcmin(separation),MoonRadiusArcmin=Arcmin(moonRadius),ShadowRadiusArcmin=Arcmin(umbra),PenumbraRadiusArcmin=Arcmin(penumbra),PenumbralMagnitude=penumbral};
   info.Magnitude=(umbra+moonRadius-separation)/(2*moonRadius);
   info.Obscuration=Math.Min(1,Overlap(umbra,moonRadius,separation)/(Math.PI*moonRadius*moonRadius));
   info.PenumbralObscuration=Math.Min(1,Overlap(penumbra,moonRadius,separation)/(Math.PI*moonRadius*moonRadius));
   info.Kind=info.Magnitude>=1?"Total":info.Magnitude>0?"Partial":"Penumbral";
   return info;
  }
  // Arcminutes of overlap left before the discs part: positive while eclipsed.
  static double OuterMargin(EclipseInfo e){return (e.Solar?e.SunRadiusArcmin:e.PenumbraRadiusArcmin)+e.MoonRadiusArcmin-e.SeparationArcmin;}
  // Lunar: the Moon touches the umbra. Positive while it does.
  static double UmbralMargin(EclipseInfo e){return e.ShadowRadiusArcmin+e.MoonRadiusArcmin-e.SeparationArcmin;}
  // Solar: one disc lies wholly inside the other (total or annular). Lunar: the Moon is wholly inside the umbra.
  static double CentralMargin(EclipseInfo e){return e.Solar?Math.Abs(e.SunRadiusArcmin-e.MoonRadiusArcmin)-e.SeparationArcmin:e.ShadowRadiusArcmin-e.MoonRadiusArcmin-e.SeparationArcmin;}
  // Time where margin crosses zero between a (positive side) and b, to about a tenth of a second.
  static DateTime Contact(DateTime inside,DateTime outside,Func<DateTime,double> margin){
   for(int i=0;i<24&&Math.Abs((outside-inside).TotalSeconds)>0.1;i++){var middle=inside.AddTicks((outside-inside).Ticks/2);if(margin(middle)>0)inside=middle;else outside=middle;}
   return inside.AddTicks((outside-inside).Ticks/2);
  }
  // The whole event a capture belongs to: contacts, greatest eclipse and a coverage curve,
  // all from the same offline models. Solar times are for the observing site.
  public static EclipseEvent Event(EclipseInfo capture,DateTime utc,double latitude,double longitude){
   if(capture==null)return null;bool solar=capture.Solar;
   Func<DateTime,EclipseInfo> at=solar?(Func<DateTime,EclipseInfo>)(t=>SolarGeometry(t,latitude,longitude)):LunarGeometry;
   Func<DateTime,double> outer=t=>OuterMargin(at(t));if(outer(utc)<=0)return null;
   // A local solar eclipse lasts under three hours and a penumbral lunar one under six and a half.
   var step=TimeSpan.FromMinutes(4);int limit=solar?60:120;DateTime? start=null,end=null;
   for(int i=1;i<=limit&&start==null;i++){var t=utc-TimeSpan.FromTicks(step.Ticks*i);if(outer(t)<=0)start=Contact(t+step,t,outer);}
   for(int i=1;i<=limit&&end==null;i++){var t=utc+TimeSpan.FromTicks(step.Ticks*i);if(outer(t)<=0)end=Contact(t-step,t,outer);}
   if(start==null||end==null)return null;
   // Separation has a single minimum during the event: golden-section search for it.
   double a=0,b=(end.Value-start.Value).TotalSeconds,golden=(Math.Sqrt(5)-1)/2;Func<double,double> separation=s=>at(start.Value.AddSeconds(s)).SeparationArcmin;
   double c=b-golden*(b-a),d=a+golden*(b-a),fc=separation(c),fd=separation(d);
   while(b-a>0.5){if(fc<fd){b=d;d=c;fd=fc;c=b-golden*(b-a);fc=separation(c);}else{a=c;c=d;fc=fd;d=a+golden*(b-a);fd=separation(d);}}
   var greatest=start.Value.AddSeconds((a+b)/2);var peak=at(greatest);
   var result=new EclipseEvent{Solar=solar,Start=start.Value,End=end.Value,Greatest=greatest,Peak=peak,Kind=peak.Kind};
   Func<DateTime,double> central=t=>CentralMargin(at(t)),umbral=t=>UmbralMargin(at(t));
   if(!solar&&UmbralMargin(peak)>0){result.PartialStart=Contact(greatest,start.Value,umbral);result.PartialEnd=Contact(greatest,end.Value,umbral);}
   if(CentralMargin(peak)>0){result.CentralStart=Contact(greatest,start.Value,central);result.CentralEnd=Contact(greatest,end.Value,central);}
   const int samples=48;result.Curve=new double[samples+1];result.PenumbralCurve=new double[samples+1];
   for(int i=0;i<=samples;i++){var e=at(start.Value.AddTicks((end.Value-start.Value).Ticks*i/samples));result.Curve[i]=OuterMargin(e)>0?e.Obscuration:0;result.PenumbralCurve[i]=solar?0:OuterMargin(e)>0?e.PenumbralObscuration:0;}
   return result;
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
