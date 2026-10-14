using System;
using System.Linq;

namespace AstroArchive {
 public partial class Tests {
  static void MeteorShowerTests(){
   Test("Meteor shower calendar covers the year with consistent windows",()=>{
    Check(MeteorShowers.All.Length>=15&&MeteorShowers.All.Select(s=>s.Code).Distinct().Count()==MeteorShowers.All.Length,"Shower list is too short or repeats a code");
    foreach(var shower in MeteorShowers.All){
     Check(shower.Zhr>0&&shower.Speed>0&&Math.Abs(shower.RadiantDec)<=90&&shower.RadiantRA>=0&&shower.RadiantRA<360,shower.Name+" has an impossible rate or radiant");
     Check(MeteorShowers.IsActive(shower,new DateTime(2026,shower.PeakMonth,shower.PeakDay)),shower.Name+" is not active on its own peak");
     Check(MeteorShowers.Activity(shower,shower.PeakDayOfYear)==1&&MeteorShowers.Activity(shower,shower.StartDayOfYear)<0.1&&MeteorShowers.Activity(shower,shower.EndDayOfYear)<0.1,shower.Name+" activity does not peak between faint ends");
    }
    var order=MeteorShowers.All.Select(s=>s.PeakDayOfYear).ToList();Check(order.SequenceEqual(order.OrderBy(x=>x)),"Showers are not ordered by peak date");
   });
   Test("Meteor shower dates handle New Year and daily activity",()=>{
    var quadrantids=MeteorShowers.ByCode("QUA");var perseids=MeteorShowers.ByCode("PER");
    Check(MeteorShowers.IsActive(quadrantids,new DateTime(2026,12,30))&&MeteorShowers.IsActive(quadrantids,new DateTime(2027,1,2))&&!MeteorShowers.IsActive(quadrantids,new DateTime(2026,2,1)),"Quadrantid window across New Year is wrong");
    Check(MeteorShowers.DaysFromPeak(quadrantids,new DateTime(2026,12,30))==-4&&MeteorShowers.DaysFromPeak(perseids,new DateTime(2026,8,14))==2,"Days from peak wrong");
    Check(MeteorShowers.Activity(quadrantids,MeteorShowers.DayOfYear(12,30))>0&&MeteorShowers.Activity(quadrantids,MeteorShowers.DayOfYear(6,1))==0&&MeteorShowers.Activity(perseids,MeteorShowers.DayOfYear(8,12))>MeteorShowers.Activity(perseids,MeteorShowers.DayOfYear(8,2)),"Activity does not rise to the peak");
    Check(MeteorShowers.IsActive(perseids,new DateTime(2024,8,12))&&MeteorShowers.IsActive(perseids,new DateTime(2028,8,24))&&!MeteorShowers.IsActive(perseids,new DateTime(2028,8,25)),"Leap-year dates shift the window");
   });
   Test("Meteor showers are filtered and ranked for the observing latitude",()=>{
    var north=MeteorShowers.ActiveOn(new DateTime(2026,8,12),51);var south=MeteorShowers.ActiveOn(new DateTime(2026,8,12),-35);var anywhere=MeteorShowers.ActiveOn(new DateTime(2026,8,12),null);
    Check(north.Count>0&&north[0].Code=="PER"&&anywhere[0].Code=="PER","Perseids should lead on their peak night");
    Check(!south.Any(s=>s.Code=="PER")&&south.Any(s=>s.Code=="SDA")&&anywhere.Any(s=>s.Code=="PER"),"A shower below the horizon was offered to the southern hemisphere");
    var geminids=MeteorShowers.Visibility(MeteorShowers.ByCode("GEM"),51);Check(geminids.Quality=="Excellent"&&Math.Abs(geminids.MaxAltitude-72)<0.01&&geminids.ExpectedRate>140&&geminids.ExpectedRate<150,"Geminid visibility from 51°N wrong");
    Check(!MeteorShowers.Visibility(MeteorShowers.ByCode("URS"),-40).Visible&&MeteorShowers.Visibility(MeteorShowers.ByCode("URS"),-40).Quality=="Not visible"&&MeteorShowers.Visibility(MeteorShowers.ByCode("URS"),-40).ExpectedRate==0,"Ursids should not be visible from 40°S");
    Check(MeteorShowers.ActiveOn(new DateTime(2026,6,1),51).Count==0,"A shower was active in a quiet month");
   });
   Test("Capture details name the showers active on a meteor's date",()=>{
    Check(MeteorShowers.Describe(new DateTime(2026,8,12),51).StartsWith("Perseids (peak night)"),"Perseid peak night not named");
    Check(MeteorShowers.Describe(new DateTime(2026,12,16),51).StartsWith("Geminids (peak 14 Dec)"),"Geminids a couple of days after peak not named with their peak");
    Check(MeteorShowers.Describe(new DateTime(2026,6,1),51)==null&&MeteorShowers.Describe(new DateTime(2026,10,25),51).Contains("Orionids"),"Quiet or active dates described wrongly");
    Check(MeteorShowers.Describe(new DateTime(2026,8,12),null).EndsWith("+1 more"),"Many overlapping showers were not summarised");
   });
  }
 }
}
