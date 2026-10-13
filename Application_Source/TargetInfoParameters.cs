using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 // Static physical parameters (NASA Planetary Fact Sheets and the IAU 2015 nominal solar values) for the planets,
 // the largest dwarf planets and the Sun. Orbits are mean semi-major axes and sidereal periods.
 public sealed class TargetParameterSet {
  public double AU,Days,DiameterKm,Mass,Gravity,OrbitKm,PerigeeKm,ApogeeKm;public int MassExponent,MoonCount;public string[] Moons;
  public bool HasOrbit{get{return AU>0||OrbitKm>0;}}
 }
 public static class TargetParameters {
  static TargetParameterSet P(double au,double days,double km,double mass,int exponent,double gravity,int moonCount,params string[] moons){return new TargetParameterSet{AU=au,Days=days,DiameterKm=km,Mass=mass,MassExponent=exponent,Gravity=gravity,MoonCount=moonCount,Moons=moons};}
  static readonly Dictionary<string,TargetParameterSet> table=new Dictionary<string,TargetParameterSet>(StringComparer.OrdinalIgnoreCase){
   {"SUN",P(0,0,1392700,1.989,30,274,0)},
   {"MERCURY",P(0.387,87.97,4879,3.30,23,3.7,0)},
   {"VENUS",P(0.723,224.7,12104,4.87,24,8.87,0)},
   {"EARTH",P(1.000,365.26,12756,5.97,24,9.8,1,"Moon")},
   {"MARS",P(1.524,687.0,6792,6.42,23,3.71,2,"Phobos","Deimos")},
   {"JUPITER",P(5.203,4332.6,142984,1.898,27,24.79,97,"Io","Europa","Ganymede","Callisto")},
   {"SATURN",P(9.537,10759,120536,5.68,26,10.44,274,"Titan","Rhea","Iapetus","Dione","Tethys","Enceladus","Mimas")},
   {"URANUS",P(19.19,30688,51118,8.68,25,8.87,29,"Titania","Oberon","Umbriel","Ariel","Miranda")},
   {"NEPTUNE",P(30.07,60182,49528,1.02,26,11.15,16,"Triton","Proteus","Nereid")},
   {"PLUTO",P(39.48,90560,2377,1.303,22,0.62,5,"Charon","Nix","Hydra","Kerberos","Styx")},
   {"MOON",new TargetParameterSet{OrbitKm=384400,PerigeeKm=363300,ApogeeKm=405500,Days=27.32,DiameterKm=3475,Mass=7.346,MassExponent=22,Gravity=1.62,Moons=new string[0]}},
   {"CERES",P(2.767,1681,939,9.38,20,0.27,0)},
   {"ERIS",P(67.8,204199,2326,1.66,22,0.82,1,"Dysnomia")}
  };
  public static TargetParameterSet For(string id){TargetParameterSet set;return id!=null&&table.TryGetValue(id.Trim(),out set)?set:null;}
  public static string Orbit(TargetParameterSet set){
   var inv=CultureInfo.InvariantCulture;
   if(set.OrbitKm>0)return Localization.F("{0} km from Earth · {1} days",set.OrbitKm.ToString("#,##0",inv),set.Days.ToString("0.##",inv));
   string au=set.AU.ToString(set.AU<10?"0.###":"0.#",inv);
   return set.Days<400?Localization.F("{0} AU from the Sun · {1} days",au,set.Days.ToString("0.#",inv)):Localization.F("{0} AU from the Sun · {1} years",au,(set.Days/365.25).ToString(set.Days/365.25<100?"0.##":"0",inv));
  }
  public static string DistanceRange(TargetParameterSet set){var inv=CultureInfo.InvariantCulture;return set.PerigeeKm.ToString("#,##0",inv)+" to "+set.ApogeeKm.ToString("#,##0",inv)+" km";}
  public static string Gravity(TargetParameterSet set){return Localization.F("{0} m/s²",set.Gravity.ToString(set.Gravity<1?"0.00":set.Gravity<100?"0.0#":"0",CultureInfo.InvariantCulture));}
  public static bool ShowsMoonCount(TargetParameterSet set){return set.MoonCount>set.Moons.Length;}
  public static string Diameter(TargetParameterSet set){return set.DiameterKm.ToString("#,##0",CultureInfo.InvariantCulture)+" km";}
  public static string Mass(TargetParameterSet set){
   const string digits="⁰¹²³⁴⁵⁶⁷⁸⁹";string exponent=string.Concat(set.MassExponent.ToString(CultureInfo.InvariantCulture).Select(c=>digits[c-'0']));
   return set.Mass.ToString("0.###",CultureInfo.InvariantCulture)+" × 10"+exponent+" kg";
  }
  public static string Moons(TargetParameterSet set){return string.Join(", ",set.Moons.Select(m=>Localization.T(m)));}
 }
}
