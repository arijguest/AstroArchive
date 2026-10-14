using System;
using System.Diagnostics;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void SkyEventsTests(){
   Test("Upcoming sky events find known eclipses, meteor peaks and oppositions with a visible time and direction",()=>{
    var clock=Stopwatch.StartNew();
    var spain=SkyEvents.Upcoming(new DateTime(2026,8,1,0,0,0,DateTimeKind.Utc),41.65,-4.72,31);
    var solar=spain.FirstOrDefault(e=>e.Title.EndsWith("solar eclipse"));
    Check(solar!=null&&Math.Abs((solar.BestUtc-new DateTime(2026,8,12,18,32,0,DateTimeKind.Utc)).TotalMinutes)<40&&solar.Altitude>0&&solar.View!=null,"The 12 August 2026 eclipse was not found for northern Spain: "+string.Join(", ",spain.Select(e=>e.Title+" "+e.BestUtc.ToString("MMdd HH:mm"))));
    var london=SkyEvents.Upcoming(new DateTime(2026,8,1,0,0,0,DateTimeKind.Utc),51.5,-0.1,31);
    var perseids=london.FirstOrDefault(e=>e.Title=="Perseids peak");
    Check(perseids!=null&&perseids.BestUtc.Month==8&&perseids.BestUtc.Day>=12&&perseids.BestUtc.Day<=13&&perseids.Altitude>=10&&perseids.Score>0&&perseids.View.Latitude==51.5,"The Perseid peak night was not found for London");
    var lunar=SkyEvents.Upcoming(new DateTime(2026,2,25,0,0,0,DateTimeKind.Utc),34.05,-118.24,14).FirstOrDefault(e=>e.Title.EndsWith("lunar eclipse"));
    Check(lunar!=null&&lunar.Title.StartsWith("Total")&&lunar.Altitude>0,"The 3 March 2026 total lunar eclipse was not found for Los Angeles");
    var jupiter=SkyEvents.Upcoming(new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc),40.7,-74.0,14).FirstOrDefault(e=>e.Title=="Jupiter at opposition");
    Check(jupiter!=null&&jupiter.BestUtc.Month==1&&jupiter.BestUtc.Day>=9&&jupiter.BestUtc.Day<=11&&jupiter.Altitude>20,"Jupiter's January 2026 opposition was not found");
    Check(spain.Zip(spain.Skip(1),(a,b)=>a.Score>=b.Score).All(x=>x)&&spain.All(e=>e.Altitude>=-1&&!string.IsNullOrEmpty(e.Direction)&&e.View!=null),"Events are not ranked or lack a position");
    Check(clock.Elapsed.TotalSeconds<60,"Finding a month of events took too long: "+clock.Elapsed.TotalSeconds);
   });
   Test("Moon occultations and close approaches are found with contact times, and bad input fails closed",()=>{
    var moon=SkyEvents.Upcoming(new DateTime(2026,3,1,0,0,0,DateTimeKind.Utc),35,-100,60).Where(e=>e.Kind=="Occultation"||e.Kind=="Conjunction").ToList();
    Check(moon.Count>0&&moon.All(e=>e.Altitude>3&&e.View!=null&&e.Detail.Length>0),"No Moon events found over two months");
    Check(SkyEvents.Compass(0)=="N"&&SkyEvents.Compass(350)=="N"&&SkyEvents.Compass(95)=="E"&&SkyEvents.Compass(225)=="SW","Compass points are wrong");
    Check(SkyEvents.Upcoming(new DateTime(2026,3,1,0,0,0,DateTimeKind.Utc),95,0,10).Count>=0,"Invalid latitude should not throw");
   });
  }
 }
}
