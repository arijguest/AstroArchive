using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace AstroArchive {
 // One thing worth going outside for, with the best time to see it and where to look.
 public sealed class SkyEvent {
  public string Kind,Title,Detail;
  public DateTime BestUtc;public DateTime? StartUtc,EndUtc;
  public double Altitude,Azimuth,Score;
  public StellariumView View;
  public string Direction{get{return SkyEvents.Compass(Azimuth);}}
 }
 // Upcoming eclipses, lunar occultations and close approaches, meteor shower peaks and planet events for an observing site.
 // Everything is computed here from the bundled Meeus Moon/Sun series, the planetary elements, the meteor shower table and a
 // short list of bright stars on the Moon's path; nothing is downloaded and no third-party list is bundled.
 public static class SkyEvents {
  const double Degrees=Math.PI/180;
  struct Star{public string Name;public double RA,Dec,Magnitude;public Star(string name,double ra,double dec,double magnitude){Name=name;RA=ra;Dec=dec;Magnitude=magnitude;}}
  // Bright stars the Moon can cover, J2000 positions in degrees (factual catalogue values).
  static readonly Star[] Stars={
   new Star("Aldebaran",68.9802,16.5093,0.86),new Star("Alcyone",56.8712,24.1051,2.87),new Star("Pollux",116.3289,28.0262,1.14),
   new Star("Regulus",152.0930,11.9672,1.35),new Star("Spica",201.2983,-11.1613,0.97),new Star("Graffias",241.3593,-19.8054,2.62),
   new Star("Antares",247.3519,-26.4320,1.06),new Star("Nunki",283.8164,-26.2967,2.05)};
  static readonly string[] Planets={"Mercury","Venus","Mars","Jupiter","Saturn","Uranus","Neptune"};
  public static string Compass(double azimuth){string[] names={"N","NE","E","SE","S","SW","W","NW"};return names[(int)Math.Round(((azimuth%360)+360)%360/45)%8];}
  public static string CompassName(double azimuth){string[] names={"north","north-east","east","south-east","south","south-west","west","north-west"};return names[(int)Math.Round(((azimuth%360)+360)%360/45)%8];}
  static DateTime Utc(DateTime t){return t.Kind==DateTimeKind.Utc?t:DateTime.SpecifyKind(t,DateTimeKind.Utc);}
  // Altitude and azimuth in degrees of a direction of date.
  public static void AltAz(SkyVector ofDate,DateTime utc,double latitude,double longitude,out double altitude,out double azimuth){
   var local=new SkyOrientation(utc,true,latitude,longitude).MapOfDate(ofDate);
   altitude=Math.Asin(Math.Max(-1,Math.Min(1,local.Z)))/Degrees;azimuth=SkyOrientation.Wrap(Math.Atan2(local.X,local.Y)/Degrees);
  }
  public static double SunAltitude(DateTime utc,double latitude,double longitude){
   double altitude,azimuth;AltAz(MoonEphemeris.At(utc,false,0,0).Sun,utc,latitude,longitude,out altitude,out azimuth);return altitude;
  }
  // 0 in daylight, 1 once the sky is astronomically dark.
  static double Darkness(double sunAltitude){return sunAltitude>=-6?0:sunAltitude<=-18?1:(-6-sunAltitude)/12;}
  static SkyVector Fixed(double ra,double dec){return SkyVector.Equatorial(ra,dec);}
  static StellariumView View(string name,bool byName,SkyVector j2000,DateTime utc,double latitude,double longitude,double field,double altitude,double azimuth){
   double ra=SkyOrientation.Wrap(Math.Atan2(j2000.Y,j2000.X)/Degrees),dec=Math.Asin(Math.Max(-1,Math.Min(1,j2000.Z)))/Degrees;
   return new StellariumView{Name=name,ByName=byName,RA=ra,Dec=dec,FieldDegrees=field,Utc=utc,Latitude=latitude,Longitude=longitude,Altitude=altitude,Azimuth=azimuth};
  }
  public static List<SkyEvent> Upcoming(DateTime fromUtc,double latitude,double longitude,int days){
   fromUtc=Utc(fromUtc);var events=new List<SkyEvent>();
   Action<Action> guard=a=>{try{a();}catch(ArgumentException){}catch(InvalidOperationException){}};
   guard(()=>events.AddRange(EclipseEvents(fromUtc,latitude,longitude,days)));
   guard(()=>events.AddRange(Meteors(fromUtc,latitude,longitude,days)));
   guard(()=>events.AddRange(MoonEvents(fromUtc,latitude,longitude,days)));
   guard(()=>events.AddRange(PlanetEvents(fromUtc,latitude,longitude,days)));
   return events.Where(e=>e.Score>0).OrderByDescending(e=>e.Score).ThenBy(e=>e.BestUtc).ToList();
  }
  // Time of the most favourable moment in a window, and the direction there. Quality combines height and darkness.
  static bool BestMoment(Func<DateTime,SkyVector> ofDate,DateTime from,DateTime to,TimeSpan step,double latitude,double longitude,double minAltitude,out DateTime best,out double altitude,out double azimuth,bool needDark){
   best=from;altitude=-90;azimuth=0;double top=0;bool found=false;
   for(var t=from;t<=to;t+=step){
    double a,z;AltAz(ofDate(t),t,latitude,longitude,out a,out z);if(a<minAltitude)continue;
    double dark=needDark?Darkness(SunAltitude(t,latitude,longitude)):1;if(dark<=0)continue;
    double q=dark*Math.Sin(a*Degrees);if(q>top){top=q;best=t;altitude=a;azimuth=z;found=true;}
   }
   return found;
  }
  // ---- Eclipses -------------------------------------------------------------------------------------------------------
  static IEnumerable<SkyEvent> EclipseEvents(DateTime from,double latitude,double longitude,int days){
   var result=new List<SkyEvent>();var end=from.AddDays(days);var step=TimeSpan.FromMinutes(10);
   DateTime skipSolar=DateTime.MinValue,skipLunar=DateTime.MinValue;
   for(var t=from;t<=end;t+=step){
    if(t>skipSolar){
     var solar=Eclipses.Solar(t,latitude,longitude);
     if(solar!=null&&solar.Obscuration>=0.03){
      var ev=Eclipses.Event(solar,t,latitude,longitude);
      if(ev!=null){skipSolar=ev.End.AddHours(6);
       double a,z;AltAz(MoonEphemeris.At(ev.Greatest,true,latitude,longitude).Topocentric,ev.Greatest,latitude,longitude,out a,out z);
       var peak=ev.Peak;double share=Math.Min(1,Math.Max(0,peak.Obscuration));
       double score=peak.Kind=="Total"?100:peak.Kind=="Annular"?85:25+60*share;score*=Math.Min(1,Math.Max(0.3,a/20));
       string kind=peak.Kind;
       result.Add(new SkyEvent{Kind="Eclipse",Title=kind+" solar eclipse",BestUtc=ev.Greatest,StartUtc=ev.Start,EndUtc=ev.End,Altitude=a,Azimuth=z,Score=score,
        Detail=(kind=="Total"?"Totality for "+Minutes(ev.CentralStart,ev.CentralEnd)+". ":kind=="Annular"?"A ring of sunlight for "+Minutes(ev.CentralStart,ev.CentralEnd)+". ":"")+(peak.Obscuration*100).ToString(peak.Obscuration>=0.995?"0.#":"0",CultureInfo.InvariantCulture)+"% of the Sun covered at the greatest. Use certified eclipse glasses; never look at the Sun directly.",
        View=View("Sun",true,SkyVector.Equatorial(0,0),ev.Greatest,latitude,longitude,4,a,z)});
      }
     }
    }
    if(t>skipLunar){
     var lunar=Eclipses.Lunar(t);
     if(lunar!=null){
      var ev=Eclipses.Event(lunar,t,latitude,longitude);
      if(ev!=null){skipLunar=ev.End.AddHours(6);
       double a,z;AltAz(MoonEphemeris.At(ev.Greatest,true,latitude,longitude).Topocentric,ev.Greatest,latitude,longitude,out a,out z);
       double up=0;for(int i=0;i<=8;i++){var s=ev.Start.AddTicks((ev.End-ev.Start).Ticks*i/8);double sa,sz;AltAz(MoonEphemeris.At(s,true,latitude,longitude).Topocentric,s,latitude,longitude,out sa,out sz);if(sa>5)up++;}
       if(up>=2){
        var peak=ev.Peak;double score=peak.Kind=="Total"?90:peak.Kind=="Partial"?55:20;score*=Math.Min(1,Math.Max(0.3,a/25))*(up/9.0*0.5+0.5);
        result.Add(new SkyEvent{Kind="Eclipse",Title=peak.Kind+" lunar eclipse",BestUtc=ev.Greatest,StartUtc=ev.Start,EndUtc=ev.End,Altitude=a,Azimuth=z,Score=score,
         Detail=(peak.Kind=="Total"?"The Moon turns copper-red"+(ev.CentralStart.HasValue?" for "+Minutes(ev.CentralStart,ev.CentralEnd):"")+". ":peak.Kind=="Partial"?"Earth's shadow takes a bite from the Moon. ":"A faint darkening in Earth's outer shadow; subtle by eye. ")+"Safe to watch with the naked eye"+(a<0?"; the Moon is below the horizon at the greatest, so watch the part after moonrise or before moonset.":"."),
         View=View("Moon",true,SkyVector.Equatorial(0,0),ev.Greatest,latitude,longitude,6,a,z)});
       }
      }
     }
    }
   }
   return result;
  }
  static string Minutes(DateTime? start,DateTime? end){if(!start.HasValue||!end.HasValue)return "a short time";double m=(end.Value-start.Value).TotalMinutes;return m<1.5?Math.Round(m*60)+" seconds":m.ToString("0.#",CultureInfo.InvariantCulture)+" minutes";}
  // ---- Meteor showers ------------------------------------------------------------------------------------------------
  static IEnumerable<SkyEvent> Meteors(DateTime from,double latitude,double longitude,int days){
   var result=new List<SkyEvent>();var end=from.AddDays(days);double zone=Math.Round(longitude/15);
   foreach(var shower in MeteorShowers.All){
    if(shower.Zhr<10)continue;
    foreach(int year in new[]{from.Year,from.Year+1}){
     if(shower.PeakMonth==2&&shower.PeakDay>28&&!DateTime.IsLeapYear(year))continue;
     // The night that begins on the peak date, local evening to local morning.
     var evening=new DateTime(year,shower.PeakMonth,shower.PeakDay,18,0,0,DateTimeKind.Utc).AddHours(-zone);
     if(evening.AddHours(12)<from||evening>end)continue;
     var radiant=Fixed(shower.RadiantRA,shower.RadiantDec);DateTime best=evening;double altitude=0,azimuth=0,quality=0,moonShare=0,moonAlt=0,moonLit=0;bool any=false;
     for(var t=evening;t<=evening.AddHours(14);t+=TimeSpan.FromMinutes(20)){
      if(t<from)continue;
      var orientation=new SkyOrientation(t,true,latitude,longitude);double a,z;var of=orientation.Precess(radiant);AltAz(of,t,latitude,longitude,out a,out z);if(a<10)continue;
      double dark=Darkness(SunAltitude(t,latitude,longitude));if(dark<=0)continue;
      var moon=MoonEphemeris.At(t,true,latitude,longitude);double ma,mz;AltAz(moon.Topocentric,t,latitude,longitude,out ma,out mz);
      double wash=ma>0?moon.Illumination*Math.Sin(ma*Degrees):0,q=dark*Math.Sin(a*Degrees)*(1-0.75*wash);
      if(q>quality){quality=q;best=t;altitude=a;azimuth=z;moonShare=wash;moonAlt=ma;moonLit=moon.Illumination;any=true;}
     }
     if(!any||quality<0.12)continue;
     double score=12+Math.Min(55,shower.Zhr/2.2)*quality;
     string moonText=moonAlt<=0?"The Moon is down at the best time.":moonShare>0.35?"The Moon is "+(moonLit*100).ToString("0",CultureInfo.InvariantCulture)+"% lit and high, so it will wash out the fainter meteors.":"The Moon is "+(moonLit*100).ToString("0",CultureInfo.InvariantCulture)+"% lit but "+(moonAlt<15?"low":"not a big problem")+" then.";
     result.Add(new SkyEvent{Kind="Meteor shower",Title=shower.Name+" peak",BestUtc=best,Altitude=altitude,Azimuth=azimuth,Score=score,
      Detail="Up to about "+shower.Zhr.ToString("0",CultureInfo.InvariantCulture)+" meteors an hour under dark skies. "+moonText+" The radiant is "+altitude.ToString("0",CultureInfo.InvariantCulture)+"° up in the "+CompassName(azimuth)+" then; look about 40° away from it with a wide view of the sky.",
      View=View(shower.Name+" radiant",false,radiant,best,latitude,longitude,90,altitude,azimuth)});
    }
   }
   return result;
  }
  // ---- The Moon: occultations and close approaches --------------------------------------------------------------------
  struct Target{public string Name;public bool Planet;public SkyVector J2000;public double Magnitude;}
  static SkyVector OfDate(Target target,DateTime t,double latitude,double longitude,out string name){
   name=target.Name;var orientation=new SkyOrientation(t,true,latitude,longitude);
   if(target.Planet){SkyVector direction;double distance;if(PlanetEphemeris.TryAt(target.Name,t,out direction,out distance))return orientation.Precess(direction);return new SkyVector(0,0,0);}
   return orientation.Precess(target.J2000);
  }
  static IEnumerable<SkyEvent> MoonEvents(DateTime from,double latitude,double longitude,int days){
   var result=new List<SkyEvent>();var end=from.AddDays(days);var targets=new List<Target>();
   foreach(var star in Stars)targets.Add(new Target{Name=star.Name,J2000=Fixed(star.RA,star.Dec),Magnitude=star.Magnitude});
   foreach(string planet in new[]{"Venus","Jupiter","Saturn","Mars","Mercury"})targets.Add(new Target{Name=planet,Planet=true,Magnitude=planet=="Venus"?-4:planet=="Jupiter"?-2.2:planet=="Mars"?0.5:planet=="Saturn"?0.8:0});
   var coarse=TimeSpan.FromMinutes(30);
   foreach(var target in targets){
    Func<DateTime,double> separation=t=>{string n;var of=OfDate(target,t,latitude,longitude,out n);if(of.Dot(of)<0.5)return 180;return MoonEphemeris.Separation(MoonEphemeris.At(t,true,latitude,longitude).Topocentric,of);};
    double previous=separation(from-coarse),current=separation(from);
    for(var t=from;t<=end;t+=coarse){
     double next=separation(t+coarse);
     if(current<previous+1e-9&&current<=next&&current<2.8){
      // Refine to the minute around this minimum.
      DateTime bestT=t;double bestSep=current;for(var r=t-coarse;r<=t+coarse;r+=TimeSpan.FromMinutes(1)){double s=separation(r);if(s<bestSep){bestSep=s;bestT=r;}}
      var snap=MoonEphemeris.At(bestT,true,latitude,longitude);double radius=Math.Asin(1737.4/snap.DistanceKm)/Degrees;
      double ma,mz;AltAz(snap.Topocentric,bestT,latitude,longitude,out ma,out mz);double sun=SunAltitude(bestT,latitude,longitude);
      bool dark=sun<-3;bool occults=bestSep<radius;
      string unused;var star=OfDate(target,bestT,latitude,longitude,out unused);
      if(ma>3&&dark){
       double brightness=Math.Max(0.2,Math.Min(1,(3-target.Magnitude)/3.5));
       if(occults){
        // Contact times: the Moon's edge crossing the object.
        DateTime? first=null,last=null;for(var r=bestT.AddMinutes(-90);r<=bestT.AddMinutes(90);r+=TimeSpan.FromMinutes(1)){if(separation(r)<Math.Asin(1737.4/MoonEphemeris.At(r,true,latitude,longitude).DistanceKm)/Degrees){if(!first.HasValue)first=r;last=r;}}
        result.Add(new SkyEvent{Kind="Occultation",Title="Moon covers "+target.Name,BestUtc=bestT,StartUtc=first,EndUtc=last,Altitude=ma,Azimuth=mz,Score=(55+35*brightness)*Math.Min(1,ma/20),
         Detail=target.Name+" vanishes behind the Moon"+(first.HasValue?" and returns about "+(last.Value-first.Value).TotalMinutes.ToString("0",CultureInfo.InvariantCulture)+" minutes later":"")+". The exact moment depends on where you stand, so watch a few minutes either side. A small telescope or binoculars helps; the bright limb is the leading edge when the Moon is waxing.",
         View=View("Moon",true,SkyVector.Equatorial(0,0),bestT,latitude,longitude,2,ma,mz)});
       }else{
        result.Add(new SkyEvent{Kind="Conjunction",Title="Moon near "+target.Name,BestUtc=bestT,Altitude=ma,Azimuth=mz,Score=(14+22*brightness*(1-bestSep/3))*Math.Min(1,ma/25),
         Detail="The Moon passes "+bestSep.ToString("0.0",CultureInfo.InvariantCulture)+"° from "+target.Name+" ("+(snap.Illumination*100).ToString("0",CultureInfo.InvariantCulture)+"% lit). A fine sight to the naked eye and good for photographs with a wide lens.",
         View=View("Moon",true,SkyVector.Equatorial(0,0),bestT,latitude,longitude,10,ma,mz)});
       }
      }
     }
     previous=current;current=next;
    }
   }
   return result;
  }
  // ---- Planets ------------------------------------------------------------------------------------------------------
  static double SignedElongation(string planet,DateTime t){
   SkyVector d;double distance;if(!PlanetEphemeris.TryAt(planet,t,out d,out distance))return double.NaN;
   var of=new SkyOrientation(t,false,0,0).Precess(d);var sun=MoonEphemeris.At(t,false,0,0).Sun;
   // Positive when the planet is east of the Sun (an evening object).
   double planetLon=Math.Atan2(of.Y,of.X)/Degrees,sunLon=Math.Atan2(sun.Y,sun.X)/Degrees,diff=SkyOrientation.Wrap(planetLon-sunLon);return diff>180?diff-360:diff;
  }
  static IEnumerable<SkyEvent> PlanetEvents(DateTime from,double latitude,double longitude,int days){
   var result=new List<SkyEvent>();var end=from.AddDays(days);
   Func<string,Func<DateTime,SkyVector>> track=name=>t=>{SkyVector d;double dist;return PlanetEphemeris.TryAt(name,t,out d,out dist)?new SkyOrientation(t,false,0,0).Precess(d):new SkyVector(0,0,1);};
   // Oppositions: the planet is opposite the Sun and visible all night.
   foreach(string planet in new[]{"Mars","Jupiter","Saturn","Uranus","Neptune"}){
    // Daily scan of the planet-minus-Sun longitude difference for the crossing of 180 degrees.
    double lastDiff=double.NaN;DateTime lastT=from;
    for(var t=from;t<=end;t=t.AddDays(1)){
     double diff=OppositionOffset(planet,t);
     if(!double.IsNaN(lastDiff)&&lastDiff>0&&diff<=0&&Math.Abs(diff-lastDiff)<30){
      // The Sun gains on an outer planet, so the longitude difference falls through 180 degrees. Refine to the hour.
      DateTime hit=lastT;for(var r=lastT;r<=t;r=r.AddHours(1)){if(OppositionOffset(planet,r)<=0){hit=r;break;}}
      DateTime bestT;double a,z;var of=track(planet);
      if(BestMoment(of,hit.Date.AddHours(12),hit.Date.AddHours(36),TimeSpan.FromMinutes(20),latitude,longitude,5,out bestT,out a,out z,true)){
       bool naked=planet!="Uranus"&&planet!="Neptune";
       result.Add(new SkyEvent{Kind="Opposition",Title=planet+" at opposition",BestUtc=bestT,Altitude=a,Azimuth=z,Score=(naked?58:34)*Math.Min(1,a/35),
        Detail=planet+" is opposite the Sun: closest and brightest of the year, up all night. "+(naked?"Bright to the naked eye; a telescope shows the disc.":"Needs binoculars or a telescope and a finder chart."),
        View=View(planet,true,SkyVector.Equatorial(0,0),bestT,latitude,longitude,naked?3:1,a,z)});
      }
     }
     lastDiff=diff;lastT=t;
    }
   }
   // Greatest elongations of Mercury and Venus.
   foreach(string planet in new[]{"Mercury","Venus"}){
    double min=planet=="Mercury"?17:35;
    double p2=double.NaN,p1=double.NaN;
    for(var t=from.AddDays(-1);t<=end.AddDays(1);t=t.AddDays(1)){
     double e=Math.Abs(SignedElongation(planet,t));
     if(!double.IsNaN(p2)&&!double.IsNaN(p1)&&p1>=p2&&p1>=e&&p1>=min){
      var peak=t.AddDays(-1);if(peak>=from&&peak<=end){
       bool evening=SignedElongation(planet,peak)>0;
       DateTime window=peak.Date,bestT;double a,z;
       // Evening objects after sunset, morning objects before sunrise.
       var span=evening?window.AddHours(12):window.AddHours(-2);var spanEnd=evening?window.AddHours(30):window.AddHours(14);
       if(BestMoment(track(planet),span,spanEnd,TimeSpan.FromMinutes(10),latitude,longitude,4,out bestT,out a,out z,false)){
        double sun=SunAltitude(bestT,latitude,longitude);
        if(sun<-1&&sun>-14&&((evening&&SignedElongation(planet,bestT)>0)||(!evening&&SignedElongation(planet,bestT)<0)))
         result.Add(new SkyEvent{Kind="Elongation",Title=planet+" at greatest "+(evening?"eastern":"western")+" elongation",BestUtc=bestT,Altitude=a,Azimuth=z,Score=(planet=="Venus"?42:36)*Math.Min(1,a/20),
          Detail=planet+" is as far from the Sun as it gets, "+p1.ToString("0",CultureInfo.InvariantCulture)+"°, the best time to spot it "+(evening?"low in the evening twilight":"low before dawn")+".",
          View=View(planet,true,SkyVector.Equatorial(0,0),bestT,latitude,longitude,10,a,z)});
       }
      }
     }
     p2=p1;p1=e;
    }
   }
   // Close pairs of planets.
   var names=new[]{"Mercury","Venus","Mars","Jupiter","Saturn"};
   for(int i=0;i<names.Length;i++)for(int j=i+1;j<names.Length;j++){
    string first=names[i],second=names[j];
    Func<DateTime,double> gap=t=>{SkyVector a,b;double da,db;if(!PlanetEphemeris.TryAt(first,t,out a,out da)||!PlanetEphemeris.TryAt(second,t,out b,out db))return 180;return MoonEphemeris.Separation(a,b);};
    double g0=gap(from.AddDays(-1)),g1=gap(from);
    for(var t=from;t<=end;t=t.AddDays(1)){
     double g2=gap(t.AddDays(1));
     if(g1<=g0&&g1<=g2&&g1<2.5){
      DateTime best=t;double min=g1;for(var r=t.AddDays(-1);r<=t.AddDays(1);r=r.AddHours(1)){double s=gap(r);if(s<min){min=s;best=r;}}
      DateTime bestT;double a,z;
      Func<DateTime,SkyVector> mid=u=>track(first)(u);
      if(BestMoment(mid,best.Date.AddHours(-3),best.Date.AddHours(27),TimeSpan.FromMinutes(15),latitude,longitude,4,out bestT,out a,out z,false)){
       double sun=SunAltitude(bestT,latitude,longitude);double e1=Math.Abs(SignedElongation(first,bestT)),e2=Math.Abs(SignedElongation(second,bestT));
       if(sun<-3&&e1>12&&e2>12)
        result.Add(new SkyEvent{Kind="Conjunction",Title=first+" and "+second+" close together",BestUtc=bestT,Altitude=a,Azimuth=z,Score=(20+22*(1-min/2.5))*Math.Min(1,a/25),
         Detail="The two planets pass "+min.ToString("0.0",CultureInfo.InvariantCulture)+"° apart; they fit together in a telescope's low-power view and make a good photograph.",
         View=View(first,true,SkyVector.Equatorial(0,0),bestT,latitude,longitude,6,a,z)});
      }
     }
     g0=g1;g1=g2;
    }
   }
   return result;
  }
  // Ecliptic longitude of the planet minus the Sun's, minus 180 degrees, in -180..180; zero is opposition.
  static double OppositionOffset(string planet,DateTime t){
   SkyVector d;double dist;if(!PlanetEphemeris.TryAt(planet,t,out d,out dist))return double.NaN;
   var of=new SkyOrientation(t,false,0,0).Precess(d);double obliquity=(23.4393-0.01301*(t.Year-2000+t.DayOfYear/365.25)/100)*Degrees;
   double lon=Math.Atan2(of.Y*Math.Cos(obliquity)+of.Z*Math.Sin(obliquity),of.X)/Degrees,sunLon,sunKm;MoonEphemeris.Sun(t,out sunLon,out sunKm);
   double diff=SkyOrientation.Wrap(lon-sunLon)-180;return diff;
  }
 }
}
