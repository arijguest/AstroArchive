using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public sealed class CometOrbit {public string Class;public double Q,E,PeriodYears;public DateTime Perihelion;}
 // Comet pages. The information pack holds only name stubs for a few comets, so
 // the overview, orbit and imaging notes come from here and TargetDescriptions.txt.
 public static class CometInfo {
  public const string SourceTitle="NASA/JPL Small-Body Database";
  static CometOrbit O(string cls,double q,double e,double years,int y,int m,int d){return new CometOrbit{Class=cls,Q=q,E=e,PeriodYears=years,Perihelion=new DateTime(y,m,d)};}
  // JPL SBDB orbit solutions, read 12 October 2026: class, perihelion distance (AU),
  // eccentricity, period (years; 0 when the orbit is open) and that solution's perihelion.
  static readonly Dictionary<string,CometOrbit> orbits=new Dictionary<string,CometOrbit>(StringComparer.OrdinalIgnoreCase){
   {"1P",O("HTC",0.575,0.9679,75.9,1986,2,8)},
   {"2P",O("ETc",0.340,0.8469,3.3,2023,10,22)},
   {"9P",O("JFc",1.543,0.5097,5.58,2016,8,2)},
   {"12P",O("HTC",0.781,0.9546,71.2,2024,4,21)},
   {"13P",O("HTC",1.173,0.9297,68.2,2024,6,30)},
   {"17P",O("JFc",2.066,0.4302,6.91,2014,3,26)},
   {"21P",O("JFc",1.013,0.7105,6.55,2018,9,10)},
   {"29P",O("JFc",5.775,0.0448,14.9,2019,4,16)},
   {"46P",O("JFc",1.055,0.6588,5.44,2018,12,12)},
   {"55P",O("HTC",0.976,0.9056,33.2,1998,2,28)},
   {"67P",O("JFc",1.243,0.6409,6.44,2015,8,13)},
   {"73P",O("JFc",0.972,0.6857,5.44,2022,8,25)},
   {"103P",O("JFc",1.065,0.6936,6.48,2017,4,20)},
   {"109P",O("HTC",0.960,0.9632,133,1992,12,11)},
   {"1I",O("HYP",0.256,1.2011,0,2017,9,9)},
   {"2I",O("HYP",2.007,3.3565,0,2019,12,8)},
   {"3I",O("HYP",1.356,6.1414,0,2025,10,29)},
   {"C/1995 O1",O("COM",0.891,0.9950,2360,1997,3,29)},
   {"C/1996 B2",O("COM",0.230,0.9999,97900,1996,5,1)},
   {"C/2006 P1",O("HYP",0.171,1.0000,0,2007,1,12)},
   {"C/2011 L4",O("HYP",0.302,1.0000,0,2013,3,10)},
   {"C/2013 US10",O("HYP",0.823,1.0003,0,2015,11,15)},
   {"C/2014 Q2",O("COM",1.290,0.9978,14000,2015,1,30)},
   {"C/2017 K2",O("HYP",1.797,1.0006,0,2022,12,19)},
   {"C/2019 Y4",O("COM",0.253,0.9992,5890,2020,5,31)},
   {"C/2020 F3",O("COM",0.295,0.9992,6790,2020,7,3)},
   {"C/2021 A1",O("HYP",0.615,1.0001,0,2022,1,3)},
   {"C/2022 E3",O("HYP",1.112,1.0003,0,2023,1,12)},
   {"C/2023 A3",O("HYP",0.391,1.0001,0,2024,9,27)},
   {"C/2023 P1",O("COM",0.225,0.9961,439,2023,9,17)},
   {"C/2024 G3",O("HYP",0.093,1.0000,0,2025,1,13)},
   {"C/2025 A6",O("COM",0.530,0.9955,1290,2025,11,8)},
   {"C/2025 R2",O("COM",0.504,0.9942,809,2025,9,12)}
  };
  // The information pack files 2I/Borisov under its interstellar number.
  static readonly Dictionary<string,string> sameObject=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"C/2019 Q4","2I"},{"C/2025 N1","3I"}};
  public static string Canonical(string designation){string id;return designation!=null&&sameObject.TryGetValue(designation,out id)?id:designation;}
  public static CometOrbit Orbit(string id){CometOrbit orbit;return id!=null&&orbits.TryGetValue(Canonical(id.Trim()),out orbit)?orbit:null;}
  public static bool IsComet(TargetInfoCard card){
   if(card==null||card.domain!="solar_system")return false;
   return Regex.IsMatch(card.id??"",@"^(?:[CPDXAI]/\d{4} [A-Z]{1,2}\d{1,3}|\d{1,4}[PDI])$")||(card.type_label??"").IndexOf("comet",StringComparison.OrdinalIgnoreCase)>=0;
  }
  public static string TypeLabel(string designation){
   string kind=Regex.Match(designation??"",@"^(?:\d+)?([CPDXAI])").Groups[1].Value;
   switch(kind){case "C":return "Long-period comet";case "P":return "Periodic comet";case "D":return "Defunct comet";case "X":return "Comet";case "I":return "Interstellar object";case "A":return "Asteroid on a comet-like orbit";default:return "Comet";}
  }
  // A page for a comet the information pack does not hold, built from its designation.
  public static TargetInfoCard Card(string target){
   string label=CometTargets.FromLabel(target)??Regex.Replace((target??"").Trim(),@"^Comet\s+","",RegexOptions.IgnoreCase);
   string designation=Canonical(CometTargets.Designation(target));
   return new TargetInfoCard{id=designation??label,name=label,domain="solar_system",type_label=designation==null?"Comet":TypeLabel(designation),parent_body="Sun",review_status="derived",content_tier="basic",aliases=new string[0],measurements=new TargetInfoMeasurement[0],citations=new TargetInfoCitation[0],source_urls=new string[0]};
  }
  // Explains what the designation says when no written overview exists.
  public static string Overview(TargetInfoCard card){
   string id=card.id??"",name=string.IsNullOrWhiteSpace(card.name)?id:card.name;
   var dated=Regex.Match(id,@"^([CPDXA])/(\d{4}) ([A-Z])([A-Z])?(\d{1,3})$");var numbered=Regex.Match(id,@"^(\d{1,4})([PDI])$");
   if(numbered.Success&&numbered.Groups[2].Value=="I")return name+" is an interstellar object, the "+Ordinal(int.Parse(numbered.Groups[1].Value))+" known visitor from outside the Solar System. It passes through once on an open orbit and will not return.";
   if(numbered.Success&&numbered.Groups[2].Value=="P")return name+" is a periodic comet, one that returns to the inner Solar System within about 200 years. Its number, "+numbered.Groups[1].Value+", records the order in which periodic comets were confirmed to return.";
   if(numbered.Success)return name+" is a defunct comet: it has broken up, faded away or not been seen at its recent expected returns.";
   if(!dated.Success)return "Comets are named after their discoverers, and many share a name such as ATLAS, PANSTARRS or ZTF. Add the comet's designation to the target name, for example C/2022 E3 (ZTF), to see its orbit.";
   string kind=dated.Groups[1].Value=="C"?"a long-period comet, one that takes more than 200 years to orbit the Sun or is passing through only once":dated.Groups[1].Value=="P"?"a periodic comet, one that returns within about 200 years":dated.Groups[1].Value=="D"?"a defunct comet, one that has broken up or been lost":dated.Groups[1].Value=="X"?"a comet whose orbit could not be determined reliably":"an object on a comet-like orbit that showed no comet activity when it was found";
   int half="ABCDEFGHJKLMNOPQRSTUVWXY".IndexOf(dated.Groups[3].Value,StringComparison.Ordinal);
   string when=(half%2==0?"first":"second")+" half of "+CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(half/2+1)+" "+dated.Groups[2].Value;
   string found=dated.Groups[4].Success?"its designation shows it was first catalogued in the "+when+".":"its designation shows it was the "+Ordinal(int.Parse(dated.Groups[5].Value))+" comet reported in the "+when+".";
   return name+" is "+kind+". The letter and number in "+id+" date its discovery: "+found;
  }
  public const string Imaging="Comets move against the stars, so keep sub-exposures short enough that the head stays sharp. Stack twice, once aligned on the comet and once on the stars, then blend the two. Siril, DeepSkyStacker and PixInsight offer comet alignment.";
  public static string ClassLabel(CometOrbit orbit){
   switch(orbit.Class){case "HTC":return "Halley-type";case "JFc":return "Jupiter-family";case "ETc":return "Encke-type";case "COM":return "Long-period";default:return orbit.E>=1.1?"Hyperbolic (interstellar)":"Near-parabolic";}
  }
  public static string Perihelion(CometOrbit orbit){return Localization.F("{0} AU from the Sun",orbit.Q.ToString("0.0##",CultureInfo.InvariantCulture));}
  public static string Period(CometOrbit orbit){return Localization.F("{0} years per orbit",orbit.PeriodYears.ToString(orbit.PeriodYears<100?"0.##":"#,##0",CultureInfo.InvariantCulture));}
  public static string Eccentricity(CometOrbit orbit){return orbit.E.ToString("0.0###",CultureInfo.InvariantCulture);}
  // Short-period orbits are solved near an older perihelion, so only one-off and
  // long-period passages show a date.
  public static bool ShowsPerihelionDate(CometOrbit orbit){return orbit.PeriodYears==0||orbit.PeriodYears>20;}
  public static string PerihelionDate(CometOrbit orbit){return TargetCards.Day(orbit.Perihelion,true);}
  public static string LookupUrl(string id){return "https://ssd.jpl.nasa.gov/tools/sbdb_lookup.html#/?sstr="+Uri.EscapeDataString(id);}
  static string Ordinal(int n){string suffix=n%100>=11&&n%100<=13?"th":n%10==1?"st":n%10==2?"nd":n%10==3?"rd":"th";return n.ToString(CultureInfo.InvariantCulture)+suffix;}
 }
}
