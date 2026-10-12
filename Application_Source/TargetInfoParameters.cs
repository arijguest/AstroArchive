using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 // Static physical parameters (NASA Planetary Fact Sheets and the IAU 2015 nominal solar values) for the planets,
 // the largest dwarf planets and the Sun. Orbits are mean semi-major axes and sidereal periods.
 public sealed class TargetParameterSet {
  public double AU,Days,DiameterKm,Mass;public int MassExponent;public string[] Moons;
  public bool HasOrbit{get{return AU>0;}}
 }
 public static class TargetParameters {
  static TargetParameterSet P(double au,double days,double km,double mass,int exponent,params string[] moons){return new TargetParameterSet{AU=au,Days=days,DiameterKm=km,Mass=mass,MassExponent=exponent,Moons=moons};}
  static readonly Dictionary<string,TargetParameterSet> table=new Dictionary<string,TargetParameterSet>(StringComparer.OrdinalIgnoreCase){
   {"SUN",P(0,0,1392700,1.989,30)},
   {"MERCURY",P(0.387,87.97,4879,3.30,23)},
   {"VENUS",P(0.723,224.7,12104,4.87,24)},
   {"EARTH",P(1.000,365.26,12756,5.97,24,"Moon")},
   {"MARS",P(1.524,687.0,6792,6.42,23,"Phobos","Deimos")},
   {"JUPITER",P(5.203,4332.6,142984,1.898,27,"Io","Europa","Ganymede","Callisto")},
   {"SATURN",P(9.537,10759,120536,5.68,26,"Titan","Rhea","Iapetus","Dione","Tethys","Enceladus","Mimas")},
   {"URANUS",P(19.19,30688,51118,8.68,25,"Titania","Oberon","Umbriel","Ariel","Miranda")},
   {"NEPTUNE",P(30.07,60182,49528,1.02,26,"Triton","Proteus","Nereid")},
   {"PLUTO",P(39.48,90560,2377,1.303,22,"Charon","Nix","Hydra","Kerberos","Styx")},
   {"CERES",P(2.767,1681,939,9.38,20)},
   {"ERIS",P(67.8,204199,2326,1.66,22,"Dysnomia")}
  };
  public static TargetParameterSet For(string id){TargetParameterSet set;return id!=null&&table.TryGetValue(id.Trim(),out set)?set:null;}
  public static string Orbit(TargetParameterSet set){
   var inv=CultureInfo.InvariantCulture;string au=set.AU.ToString(set.AU<10?"0.###":"0.#",inv);
   return set.Days<400?Localization.F("{0} AU from the Sun · {1} days",au,set.Days.ToString("0.#",inv)):Localization.F("{0} AU from the Sun · {1} years",au,(set.Days/365.25).ToString(set.Days/365.25<100?"0.##":"0",inv));
  }
  public static string Diameter(TargetParameterSet set){return set.DiameterKm.ToString("#,##0",CultureInfo.InvariantCulture)+" km";}
  public static string Mass(TargetParameterSet set){
   const string digits="⁰¹²³⁴⁵⁶⁷⁸⁹";string exponent=string.Concat(set.MassExponent.ToString(CultureInfo.InvariantCulture).Select(c=>digits[c-'0']));
   return set.Mass.ToString("0.###",CultureInfo.InvariantCulture)+" × 10"+exponent+" kg";
  }
  public static string Moons(TargetParameterSet set){return string.Join(", ",set.Moons.Select(m=>Localization.T(m)));}
 }
}
