using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 // The annual meteor showers of the International Meteor Organization's working list, with the
 // dates, peak rates and radiants the Meteors information page and Capture details use.
 // Dates are the usual calendar window and peak day; the peak moves by about a day between
 // years. Rates are the zenithal hourly rate (ZHR) under ideal dark skies.
 public sealed class MeteorShower {
  public string Code,Name,Parent,Note;
  public int StartMonth,StartDay,PeakMonth,PeakDay,EndMonth,EndDay;
  public double Zhr,Speed,RadiantRA,RadiantDec;
  // Day of the year (1-366, non-leap calendar) the activity starts, peaks and ends; the end can be before the start when the shower spans New Year.
  public int StartDayOfYear{get{return MeteorShowers.DayOfYear(StartMonth,StartDay);}}
  public int PeakDayOfYear{get{return MeteorShowers.DayOfYear(PeakMonth,PeakDay);}}
  public int EndDayOfYear{get{return MeteorShowers.DayOfYear(EndMonth,EndDay);}}
  public bool WrapsYear{get{return EndDayOfYear<StartDayOfYear;}}
  public string Peak{get{return PeakDay.ToString(CultureInfo.InvariantCulture)+" "+CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(PeakMonth);}}
  public string Window{get{var months=CultureInfo.InvariantCulture.DateTimeFormat;return StartDay+" "+months.GetAbbreviatedMonthName(StartMonth)+" to "+EndDay+" "+months.GetAbbreviatedMonthName(EndMonth);}}
 }
 // How a shower looks from a latitude: its radiant's greatest height, a word for it and the hourly rate that height allows.
 public sealed class MeteorVisibility {
  public MeteorShower Shower;public double MaxAltitude,ExpectedRate;public string Quality;
  public bool Visible{get{return MaxAltitude>=MeteorShowers.MinimumAltitude;}}
 }
 public static class MeteorShowers {
  public const double MinimumAltitude=10;
  static readonly int[] cumulative={0,31,59,90,120,151,181,212,243,273,304,334};
  public static int DayOfYear(int month,int day){return cumulative[month-1]+day;}
  static MeteorShower S(string code,string name,int sm,int sd,int pm,int pd,int em,int ed,double zhr,double speed,double ra,double dec,string parent,string note){
   return new MeteorShower{Code=code,Name=name,StartMonth=sm,StartDay=sd,PeakMonth=pm,PeakDay=pd,EndMonth=em,EndDay=ed,Zhr=zhr,Speed=speed,RadiantRA=ra,RadiantDec=dec,Parent=parent,Note=note};
  }
  // Ordered through the year by peak date.
  public static readonly MeteorShower[] All={
   S("QUA","Quadrantids",12,28,1,3,1,12,80,41,230,49,"asteroid 2003 EH1","A sharp peak that lasts only a few hours; blue meteors are common."),
   S("ACE","Alpha Centaurids",1,31,2,8,2,20,6,56,210,-59,"unknown","A southern shower with fast, bright meteors and occasional fireballs."),
   S("GNO","Gamma Normids",2,25,3,14,3,28,6,56,249,-51,"unknown","A southern shower; the radiant is only visible well from the southern hemisphere."),
   S("LYR","Lyrids",4,14,4,22,4,30,18,49,271,34,"comet C/1861 G1 (Thatcher)","Often leaves glowing trains; occasional outbursts reach about 100 an hour."),
   S("ETA","Eta Aquariids",4,19,5,6,5,28,50,66,338,-1,"comet 1P/Halley","Very fast meteors with long trains; the rate is much better from the tropics and southern hemisphere."),
   S("PAU","Piscis Austrinids",7,15,7,28,8,10,5,35,341,-30,"unknown","A southern shower that peaks with the Southern Delta Aquariids."),
   S("SDA","Southern Delta Aquariids",7,12,7,30,8,23,25,41,340,-16,"comet 96P/Machholz","Slow to medium, faint meteors; it overlaps the Perseid season."),
   S("CAP","Alpha Capricornids",7,3,7,30,8,15,5,23,307,-10,"comet 169P/NEAT","Slow, bright fireballs make up for the low rate."),
   S("PER","Perseids",7,17,8,12,8,24,100,59,48,58,"comet 109P/Swift-Tuttle","The best-known shower: fast, bright meteors, many with persistent trains, on warm summer nights."),
   S("KCG","Kappa Cygnids",8,3,8,17,8,25,3,25,286,59,"asteroid 2008 ED69","A weak shower of slow meteors and an occasional bright one."),
   S("AUR","Aurigids",8,28,9,1,9,5,6,66,91,39,"comet C/1911 N1 (Kiess)","Usually weak, with rare outbursts of a few hundred an hour."),
   S("DRA","Draconids",10,6,10,8,10,10,10,20,262,54,"comet 21P/Giacobini-Zinner","Very slow meteors, best seen in the evening; the rate changes greatly between years."),
   S("STA","Southern Taurids",9,23,10,10,11,12,5,27,40,11,"comet 2P/Encke","Slow, bright fireballs and a very long season."),
   S("ORI","Orionids",10,2,10,21,11,7,20,66,95,16,"comet 1P/Halley","Fast meteors that often leave glowing trains."),
   S("NTA","Northern Taurids",10,13,11,12,12,2,5,29,58,22,"comet 2P/Encke","Slow, bright fireballs and a very long season."),
   S("LEO","Leonids",11,6,11,17,11,30,15,71,152,22,"comet 55P/Tempel-Tuttle","The fastest of the showers; rich in bright meteors with trains, with storms every 33 years or so."),
   S("PUP","Puppid-Velids",12,1,12,7,12,15,10,40,123,-45,"unknown","A southern shower of medium-speed meteors."),
   S("GEM","Geminids",12,4,12,14,12,20,150,35,112,33,"asteroid 3200 Phaethon","The strongest shower of the year: slow, bright meteors in many colours."),
   S("URS","Ursids",12,17,12,22,12,26,10,33,217,76,"comet 8P/Tuttle","A modest shower near the Little Dipper, seen from the northern hemisphere.")
  };
  public static MeteorShower ByCode(string code){return All.FirstOrDefault(s=>string.Equals(s.Code,code,StringComparison.OrdinalIgnoreCase));}
  // True on any date inside the shower's usual window, including the days at both ends.
  public static bool IsActive(MeteorShower shower,DateTime date){
   int day=DayOfYear(date.Month,Math.Min(date.Day,DateTime.DaysInMonth(2001,date.Month)));
   return shower.WrapsYear?day>=shower.StartDayOfYear||day<=shower.EndDayOfYear:day>=shower.StartDayOfYear&&day<=shower.EndDayOfYear;
  }
  // Days from the peak (negative before it), wrapping across New Year to the nearest peak.
  public static int DaysFromPeak(MeteorShower shower,DateTime date){
   int day=DayOfYear(date.Month,Math.Min(date.Day,DateTime.DaysInMonth(2001,date.Month))),delta=day-shower.PeakDayOfYear;
   if(delta>182)delta-=365;else if(delta<-182)delta+=365;return delta;
  }
  // Relative activity from 0 to 1: full at the peak, easing to a small fraction at the ends of the window.
  public static double Activity(MeteorShower shower,int dayOfYear){
   if(!IsActiveDay(shower,dayOfYear))return 0;
   int delta=dayOfYear-shower.PeakDayOfYear;if(delta>182)delta-=365;else if(delta<-182)delta+=365;
   int span=delta<0?SpanBefore(shower):SpanAfter(shower);if(span<=0)return 1;
   double t=1-Math.Min(1,Math.Abs(delta)/(double)span);return 0.06+0.94*t*t;
  }
  static bool IsActiveDay(MeteorShower shower,int day){return shower.WrapsYear?day>=shower.StartDayOfYear||day<=shower.EndDayOfYear:day>=shower.StartDayOfYear&&day<=shower.EndDayOfYear;}
  static int SpanBefore(MeteorShower shower){int span=shower.PeakDayOfYear-shower.StartDayOfYear;return span<0?span+365:span;}
  static int SpanAfter(MeteorShower shower){int span=shower.EndDayOfYear-shower.PeakDayOfYear;return span<0?span+365:span;}
  // The greatest height the radiant reaches in the sky at a latitude.
  public static double MaxAltitude(double latitude,double radiantDec){return 90-Math.Abs(latitude-radiantDec);}
  public static MeteorVisibility Visibility(MeteorShower shower,double latitude){
   double altitude=MaxAltitude(latitude,shower.RadiantDec);double rate=altitude<MinimumAltitude?0:shower.Zhr*Math.Sin(altitude*Math.PI/180);
   string quality=altitude<MinimumAltitude?"Not visible":altitude>=60?"Excellent":altitude>=40?"Good":altitude>=25?"Fair":"Low";
   return new MeteorVisibility{Shower=shower,MaxAltitude=altitude,ExpectedRate=rate,Quality=quality};
  }
  // The showers active on a date, best first. With a latitude, showers whose radiant never clears 10 degrees are left out.
  public static List<MeteorShower> ActiveOn(DateTime date,double? latitude){
   return All.Where(s=>IsActive(s,date)&&(!latitude.HasValue||Visibility(s,latitude.Value).Visible))
    .OrderByDescending(s=>Activity(s,DayOfYear(date.Month,Math.Min(date.Day,DateTime.DaysInMonth(2001,date.Month))))*(latitude.HasValue?Visibility(s,latitude.Value).ExpectedRate:s.Zhr)).ToList();
  }
  // "Perseids (peak 12 Aug)" for each active shower, up to three, for the Capture details row.
  public static string Describe(DateTime date,double? latitude){
   var active=ActiveOn(date,latitude);if(active.Count==0)return null;
   var parts=active.Take(3).Select(s=>{int days=DaysFromPeak(s,date);return s.Name+" ("+(days==0?"peak night":"peak "+s.Peak)+")";}).ToList();
   if(active.Count>3)parts.Add("+"+(active.Count-3).ToString(CultureInfo.InvariantCulture)+" more");
   return string.Join(" · ",parts);
  }
 }
 // One line of the "what colours can mean" guide: the swatch, the usual cause and what it looks like.
 public sealed class MeteorColour {
  public string Name,Cause,Hex;
 }
 public static class MeteorColours {
  // The colour of a meteor comes mainly from the elements the vaporised grain releases, plus the air it heats; speed sets how hot it glows.
  public static readonly MeteorColour[] All={
   new MeteorColour{Name="Orange-yellow",Hex="#E8A33D",Cause="Sodium in the grain. Common in slow meteors and in the Perseids' bright trails."},
   new MeteorColour{Name="Yellow",Hex="#E8D44D",Cause="Iron. The usual colour of a typical meteor, from the iron in most meteoroids."},
   new MeteorColour{Name="Blue-green",Hex="#3FC1B0",Cause="Magnesium. Often seen in bright meteors and fireballs."},
   new MeteorColour{Name="Violet",Hex="#9B6BDB",Cause="Ionised calcium, in the fastest meteors. Faint to the eye, but a camera records it well."},
   new MeteorColour{Name="Red",Hex="#D9453D",Cause="Nitrogen and oxygen in the air around the meteor, seen at the start and end of the trail of fast, bright meteors."},
   new MeteorColour{Name="Green",Hex="#5FD068",Cause="Oxygen atoms at 557.7 nm. The green glow of the persistent trains left behind by bright meteors, which can last for seconds or minutes."}
  };
  public const string Speed="Speed matters too: fast meteors such as the Leonids and Perseids glow bluer and whiter, and slow ones such as the Geminids and Taurids look yellower or orange. Your camera's white balance and sensor shift these colours, so compare frames taken with the same settings.";
 }
}
