using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace AstroArchive {
 public struct OrbitPoint {
  public double X,Y,Z;public OrbitPoint(double x,double y,double z){X=x;Y=y;Z=z;}
  public double Length {get{return Math.Sqrt(X*X+Y*Y+Z*Z);}}
 }
 public sealed class OrbitRecord {
  public int schema=1;public string object_id,provider_id,central_body,reference_plane,coordinate_frame,time_standard,solution_id,solution_date,source;
  public double epoch,e,q,i,ascending_node,argument_of_perihelion;
  public double? mean_anomaly,perihelion_time,valid_from,valid_to,gravitational_parameter;public string validity_limits;public string orbit_class;
  public Dictionary<string,string> uncertainties;public InformationRights rights;
  public bool IsClosed {get{return e<1;}}
 }
 public sealed class OrbitPath {
  public OrbitPoint[] Points;public bool Closed,Clipped;public double Extent;public string Name;
 }
 public sealed class OrbitScene {
  public OrbitRecord Record;public OrbitPath Full,Inner;public List<OrbitPath> Context=new List<OrbitPath>();
 }
 // Two-body geometry in AU, degrees and Julian days. No ephemeris/pointing promise.
 public static class OrbitalModel {
  public const double R=Math.PI/180,SunMu=0.0002959122082855911,EarthMu=8.8876924467066e-10;
  public static bool Finite(double x){return !double.IsNaN(x)&&!double.IsInfinity(x);}
  public static bool Valid(OrbitRecord o){
   return o!=null&&o.schema==1&&!string.IsNullOrWhiteSpace(o.object_id)&&o.object_id.Length<=100&&
    (o.central_body=="Sun"||o.central_body=="Earth")&&o.reference_plane=="ecliptic"&&o.coordinate_frame=="J2000"&&
    (o.time_standard=="TDB"||o.time_standard=="TT")&&Finite(o.epoch)&&o.epoch>=2000000&&o.epoch<=3000000&&
    Finite(o.e)&&o.e>=0&&o.e<=100&&Finite(o.q)&&o.q>=1e-8&&o.q<=1e6&&
    Finite(o.i)&&o.i>=0&&o.i<=180&&Finite(o.ascending_node)&&Finite(o.argument_of_perihelion)&&
    Math.Abs(o.ascending_node)<=3600&&Math.Abs(o.argument_of_perihelion)<=3600&&
    (!o.gravitational_parameter.HasValue||Finite(o.gravitational_parameter.Value)&&o.gravitational_parameter>0&&o.gravitational_parameter<1)&&(!o.mean_anomaly.HasValue||Finite(o.mean_anomaly.Value)&&Math.Abs(o.mean_anomaly.Value)<=36000)&&
    (!o.perihelion_time.HasValue||Finite(o.perihelion_time.Value)&&o.perihelion_time>=1000000&&o.perihelion_time<=4000000)&&
    (o.mean_anomaly.HasValue||o.perihelion_time.HasValue)&&(o.e!=1||o.perihelion_time.HasValue)&&
    (!o.valid_from.HasValue||Finite(o.valid_from.Value))&&(!o.valid_to.HasValue||Finite(o.valid_to.Value))&&
    (!o.valid_from.HasValue||!o.valid_to.HasValue||o.valid_from<=o.valid_to)&&InformationRights.Permitted(o.rights,false)&&(o.uncertainties==null||o.uncertainties.Count<=40&&o.uncertainties.All(p=>p.Key.Length<=20&&(p.Value??"").Length<=120));
  }
  public static OrbitPoint Orient(OrbitRecord o,double x,double y){
   double w=o.argument_of_perihelion*R,n=o.ascending_node*R,j=o.i*R,cw=Math.Cos(w),sw=Math.Sin(w),cn=Math.Cos(n),sn=Math.Sin(n),ci=Math.Cos(j),si=Math.Sin(j);
   return new OrbitPoint((cw*cn-sw*sn*ci)*x+(-sw*cn-cw*sn*ci)*y,(cw*sn+sw*cn*ci)*x+(-sw*sn+cw*cn*ci)*y,sw*si*x+cw*si*y);
  }
  public static OrbitPoint AtAnomaly(OrbitRecord o,double anomaly){double radius=o.q*(1+o.e)/(1+o.e*Math.Cos(anomaly));return Orient(o,radius*Math.Cos(anomaly),radius*Math.Sin(anomaly));}
  public static OrbitPath Path(OrbitRecord o,bool inner,CancellationToken token){
   if(!Valid(o))return null;var points=new List<OrbitPoint>();double extent=o.IsClosed?o.q*(1+o.e)/(1-o.e):Math.Max(40,o.q*8);
   double bound=inner?Math.Max(o.q*1.5,Math.Max(o.central_body=="Earth"?0.004:6,Math.Min(40,o.q*4))):extent;
   bool clipped=!o.IsClosed||inner&&extent>bound;double end=clipped?Math.Acos(Math.Max(-1,Math.Min(1,(o.q*(1+o.e)/bound-1)/o.e))):Math.PI;
   // Adaptive subdivision is bounded. A second inner path retains perihelion detail
   // even when the complete ellipse extends billions of AU near e=1.
   Func<double,OrbitPoint> point=v=>AtAnomaly(o,v);double start=-end;
   points.Add(point(start));for(int k=0;k<64;k++){token.ThrowIfCancellationRequested();double a=start+(2*end)*k/64,b=start+(2*end)*(k+1)/64;Sample(point,a,b,point(a),point(b),points,Math.Max(1e-10,bound*0.0003),0,token);}
   if(points.Count>1)points[points.Count-1]=point(end);
   return new OrbitPath{Points=points.ToArray(),Closed=!clipped,Clipped=clipped,Extent=Math.Max(o.q,clipped?bound:extent)};
  }
  static void Sample(Func<double,OrbitPoint> f,double a,double b,OrbitPoint p,OrbitPoint q,List<OrbitPoint> points,double tolerance,int depth,CancellationToken token){
   token.ThrowIfCancellationRequested();double m=(a+b)/2;var v=f(m);double dx=v.X-(p.X+q.X)/2,dy=v.Y-(p.Y+q.Y)/2,dz=v.Z-(p.Z+q.Z)/2;
   if(depth<6&&points.Count<8100&&(dx*dx+dy*dy+dz*dz)>tolerance*tolerance){Sample(f,a,m,p,v,points,tolerance,depth+1,token);Sample(f,m,b,v,q,points,tolerance,depth+1,token);}else if(points.Count<8192)points.Add(q);
  }
  public static double Julian(DateTime utc){return 2451545+(utc.ToUniversalTime()-new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)).TotalDays;}
  public static DateTime? Utc(double julian){try{return new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc).AddDays(julian-2451545);}catch{return null;}}
  // Post-1972 UTC -> TT; TDB-TT is under 2 ms, explicitly approximate here.
  // Future leap seconds are unknown, so dated markers are limited to 1972-2026.
  public static double? Dynamical(DateTime utc){
   utc=utc.ToUniversalTime();if(utc.Year<1972||utc.Year>2026)return null;int leap=10;
   var dates=new[]{"1972-07-01","1973-01-01","1974-01-01","1975-01-01","1976-01-01","1977-01-01","1978-01-01","1979-01-01","1980-01-01","1981-07-01","1982-07-01","1983-07-01","1985-07-01","1988-01-01","1990-01-01","1991-01-01","1992-07-01","1993-07-01","1994-07-01","1996-01-01","1997-07-01","1999-01-01","2006-01-01","2009-01-01","2012-07-01","2015-07-01","2017-01-01"};
   foreach(var d in dates)if(utc>=DateTime.Parse(d,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal))leap++;
   return Julian(utc)+(leap+32.184)/86400;
  }
  public static bool Position(OrbitRecord o,double day,out OrbitPoint point,bool epochOnly=false){
   point=new OrbitPoint();if(!Valid(o)||!Finite(day))return false;
   if(!epochOnly&&(!o.valid_from.HasValue||!o.valid_to.HasValue||day<o.valid_from||day>o.valid_to))return false;
   double mu=o.gravitational_parameter??(o.central_body=="Earth"?EarthMu:SunMu),dt=day-(o.perihelion_time??o.epoch),v;
   if(o.e==1){if(!o.perihelion_time.HasValue)return false;double b=dt*Math.Sqrt(mu/(2*o.q*o.q*o.q)),s=1.5*b;
    double d=2*Math.Sinh(Math.Log(s+Math.Sqrt(s*s+1))/3);if(s<0)d=-2*Math.Sinh(Math.Log(-s+Math.Sqrt(s*s+1))/3);v=2*Math.Atan(d);
   }else if(o.e<1){double a=o.q/(1-o.e),mean=o.mean_anomaly.HasValue?o.mean_anomaly.Value*R+(day-o.epoch)*Math.Sqrt(mu/(a*a*a)):dt*Math.Sqrt(mu/(a*a*a));
    mean=Math.IEEERemainder(mean,2*Math.PI);double lo=-Math.PI,hi=Math.PI,angle=mean;
    for(int k=0;k<64;k++){double value=(1-o.e)*angle+o.e*SinDifference(angle)-mean;if(Math.Abs(value)<1e-14*Math.Max(1e-30,Math.Abs(mean)))break;if(value>0)hi=angle;else lo=angle;double next=angle-value/((1-o.e)+2*o.e*Math.Pow(Math.Sin(angle/2),2));angle=next>lo&&next<hi?next:(lo+hi)/2;}
    v=2*Math.Atan2(Math.Sqrt(1+o.e)*Math.Sin(angle/2),Math.Sqrt(1-o.e)*Math.Cos(angle/2));
   }else{double a=o.q/(o.e-1),mean=o.mean_anomaly.HasValue?o.mean_anomaly.Value*R+(day-o.epoch)*Math.Sqrt(mu/(a*a*a)):dt*Math.Sqrt(mu/(a*a*a));
    double sign=mean<0?-1:1,lo=0,hi=1;mean=Math.Abs(mean);while(hi<64&&(o.e-1)*hi+o.e*SinhDifference(hi)<mean)hi*=2;
    for(int k=0;k<80;k++){double mid=(lo+hi)/2;if((o.e-1)*mid+o.e*SinhDifference(mid)<mean)lo=mid;else hi=mid;}double h=sign*(lo+hi)/2;v=2*Math.Atan(Math.Sqrt((o.e+1)/(o.e-1))*Math.Tanh(h/2));
   }
   if(1+o.e*Math.Cos(v)<=0)return false;point=AtAnomaly(o,v);return Finite(point.X)&&Finite(point.Y)&&Finite(point.Z);
  }
  static double SinDifference(double x){if(Math.Abs(x)>=0.1)return x-Math.Sin(x);double x2=x*x;return x*x2*(1.0/6-x2/120+x2*x2/5040-x2*x2*x2/362880+x2*x2*x2*x2/39916800);}
  static double SinhDifference(double x){if(Math.Abs(x)>=0.1)return Math.Sinh(x)-x;double x2=x*x;return x*x2*(1.0/6+x2/120+x2*x2/5040+x2*x2*x2/362880+x2*x2*x2*x2/39916800);}
 }
}
