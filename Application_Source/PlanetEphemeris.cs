// Display-only planetary positions from JPL's approximate Keplerian model,
// Table 1 (1800-2050). https://ssd.jpl.nasa.gov/planets/approx_pos.html
// This small offline model is for the sky globe, not telescope pointing/solving.
using System;

namespace AstroArchive {
 public static class PlanetEphemeris {
  const double R=Math.PI/180,AuKm=149597870.7;
  static readonly string[] Names={"Mercury","Venus",null,"Mars","Jupiter","Saturn","Uranus","Neptune"};
  // a, e, inclination, mean longitude, longitude of perihelion, ascending node;
  // followed by their rates per Julian century. Earth is the Earth/Moon barycentre.
  static readonly double[,] Elements={
   {0.38709927,0.20563593,7.00497902,252.25032350,77.45779628,48.33076593,0.00000037,0.00001906,-0.00594749,149472.67411175,0.16047689,-0.12534081},
   {0.72333566,0.00677672,3.39467605,181.97909950,131.60246718,76.67984255,0.00000390,-0.00004107,-0.00078890,58517.81538729,0.00268329,-0.27769418},
   {1.00000261,0.01671123,-0.00001531,100.46457166,102.93768193,0,0.00000562,-0.00004392,-0.01294668,35999.37244981,0.32327364,0},
   {1.52371034,0.09339410,1.84969142,-4.55343205,-23.94362959,49.55953891,0.00001847,0.00007882,-0.00813131,19140.30268499,0.44441088,-0.29257343},
   {5.20288700,0.04838624,1.30439695,34.39644051,14.72847983,100.47390909,-0.00011607,-0.00013253,-0.00183714,3034.74612775,0.21252668,0.20469106},
   {9.53667594,0.05386179,2.48599187,49.95424423,92.59887831,113.66242448,-0.00125060,-0.00050991,0.00193609,1222.49362201,-0.41897216,-0.28867794},
   {19.18916464,0.04725744,0.77263783,313.23810451,170.95427630,74.01692503,-0.00196176,-0.00004397,-0.00242939,428.48202785,0.40805281,0.04240589},
   {30.06992276,0.00859048,1.77004347,-55.12002969,44.96476227,131.78422574,0.00026291,0.00005105,0.00035372,218.45945325,-0.32241464,-0.00508664}
  };
  // Orbital display uses the same reviewed JPL Table 1 as the sky globe.
  public static OrbitRecord Orbit(string id,DateTime utc){
   string[] names={"MERCURY","VENUS","EARTH","MARS","JUPITER","SATURN","URANUS","NEPTUNE"};int body=Array.IndexOf(names,(id??"").ToUpperInvariant());
   if(body<0||utc.Year<1800||utc.Year>2050)return null;double day=OrbitalModel.Julian(utc),t=(day-2451545)/36525;var v=new double[6];for(int k=0;k<6;k++)v[k]=Elements[body,k]+t*Elements[body,k+6];
   return new OrbitRecord{object_id=names[body],central_body="Sun",reference_plane="ecliptic",coordinate_frame="J2000",time_standard="TDB",epoch=day,e=v[1],q=v[0]*(1-v[1]),i=Math.Abs(v[2]),ascending_node=v[2]<0?v[5]+180:v[5],argument_of_perihelion=v[4]-v[5]-(v[2]<0?180:0),mean_anomaly=(v[3]-v[4])%360,valid_from=OrbitalModel.Julian(new DateTime(1800,1,1,0,0,0,DateTimeKind.Utc)),valid_to=OrbitalModel.Julian(new DateTime(2050,12,31,0,0,0,DateTimeKind.Utc)),solution_id="JPL Table 1 (1800–2050)",source="https://ssd.jpl.nasa.gov/planets/approx_pos.html",rights=OrbitalCatalog.Rights("https://ssd.jpl.nasa.gov/planets/approx_pos.html","Table 1",new DateTime(2026,10,13,0,0,0,DateTimeKind.Utc),"planetary-approximation")};
  }
  static SkyVector Heliocentric(int body,double t){
   var v=new double[6];for(int i=0;i<6;i++)v[i]=Elements[body,i]+t*Elements[body,i+6];
   double a=v[0],e=v[1],inclination=v[2]*R,node=v[5]*R,perihelion=(v[4]-v[5])*R;
   double mean=(SkyOrientation.Wrap(v[3]-v[4]+180)-180)*R,eccentric=mean;
   for(int i=0;i<12;i++){double step=(eccentric-e*Math.Sin(eccentric)-mean)/(1-e*Math.Cos(eccentric));eccentric-=step;if(Math.Abs(step)<1e-12)break;}
   double x=a*(Math.Cos(eccentric)-e),y=a*Math.Sqrt(1-e*e)*Math.Sin(eccentric);
   double cw=Math.Cos(perihelion),sw=Math.Sin(perihelion),cn=Math.Cos(node),sn=Math.Sin(node),ci=Math.Cos(inclination),si=Math.Sin(inclination);
   return new SkyVector((cw*cn-sw*sn*ci)*x+(-sw*cn-cw*sn*ci)*y,(cw*sn+sw*cn*ci)*x+(-sw*sn+cw*cn*ci)*y,sw*si*x+cw*si*y);
  }
  // Geocentric astrometric unit vector in J2000 equatorial coordinates.
  // Light-time is applied to the planet; Earth's position stays at reception time.
  public static bool TryAt(string name,DateTime utc,out SkyVector direction,out double distanceAu){
   direction=new SkyVector();distanceAu=0;int body=Array.IndexOf(Names,name);if(name==null||body<0||utc.Year<1800||utc.Year>2050)return false;
   double days=(utc-new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)).TotalDays,t=days/36525;var earth=Heliocentric(2,t);double lightDays=0;SkyVector delta=new SkyVector();
   for(int i=0;i<3;i++){var planet=Heliocentric(body,(days-lightDays)/36525);delta=new SkyVector(planet.X-earth.X,planet.Y-earth.Y,planet.Z-earth.Z);distanceAu=Math.Sqrt(delta.Dot(delta));lightDays=distanceAu*0.0057755183;}
   double obliquity=23.43928*R;direction=Unit(new SkyVector(delta.X,delta.Y*Math.Cos(obliquity)-delta.Z*Math.Sin(obliquity),delta.Y*Math.Sin(obliquity)+delta.Z*Math.Cos(obliquity)));return true;
  }
  static SkyVector Unit(SkyVector v){double length=Math.Sqrt(v.Dot(v));return new SkyVector(v.X/length,v.Y/length,v.Z/length);}
  // Observer parallax after precessing the geocentric direction to the date.
  public static SkyVector Topocentric(SkyVector ofDate,double distanceAu,DateTime utc,double latitude,double longitude){
   double lst=SkyOrientation.Sidereal(utc,longitude)*R,u=Math.Atan(0.99664719*Math.Tan(latitude*R)),rs=0.99664719*Math.Sin(u),rc=Math.Cos(u),ratio=distanceAu*AuKm/6378.14;
   return Unit(new SkyVector(ratio*ofDate.X-rc*Math.Cos(lst),ratio*ofDate.Y-rc*Math.Sin(lst),ratio*ofDate.Z-rs));
  }
 }
}
