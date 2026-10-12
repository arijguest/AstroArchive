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
