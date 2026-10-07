using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public static class TargetNavigation {
  public static readonly string[] Groups={"Solar system","Comets","Nebulae","Galaxies","Star clusters","Stars","Other targets","Calibration","Unidentified"};
  static readonly Lazy<Dictionary<string,string>> types=new Lazy<Dictionary<string,string>>(()=>Catalog.Objects.GroupBy(o=>o.Name).ToDictionary(g=>g.Key,g=>g.First().Type,StringComparer.OrdinalIgnoreCase));
  public static string Group(string name){
   if(name=="All targets")return "";string target=Catalog.CanonicalTarget(name);string type;
   if(target.Equals("Unknown",StringComparison.OrdinalIgnoreCase))return "Unidentified";
   if(target=="Calibration")return "Calibration";
   if(Regex.IsMatch(target,@"^(Sun|Solar|Moon|Lunar|Mercury|Venus|Mars|Jupiter|Saturn|Uranus|Neptune|Pluto|Planetary)$",RegexOptions.IgnoreCase))return "Solar system";
   if(types.Value.TryGetValue(target,out type)){
    switch(type){
     case "G":case "GPair":case "GTrpl":case "GGroup":return "Galaxies";
     case "Neb":case "HII":case "PN":case "RfN":case "EmN":case "DrkN":case "SNR":case "Cl+N":return "Nebulae";
     case "OCl":case "GCl":case "*Ass":return "Star clusters";
     case "Nova":case "*":case "**":return "Stars";
    }
   }
   string label=target+" "+Catalog.CommonName(target);var hints=new HashSet<string>();
   if(Regex.IsMatch(label,@"\bcomet\b|^[CPDXAI]\s*/?\s*\d{4}\s*[A-Z]{1,2}\d{1,3}\b|^\d{1,4}\s*[PDI](?:\s*/|\s+|$)|\b(Halley|Hale[- ]Bopp|Hyakutake|NEOWISE)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))hints.Add("Comets");
   if(Regex.IsMatch(label,@"\b(nebula|nebulae|supernova remnant)\b",RegexOptions.IgnoreCase))hints.Add("Nebulae");
   if(Regex.IsMatch(label,@"\b(galaxy|galaxies)\b",RegexOptions.IgnoreCase))hints.Add("Galaxies");
   if(Regex.IsMatch(label,@"\b(cluster|clusters)\b",RegexOptions.IgnoreCase))hints.Add("Star clusters");
   return hints.Count==1?hints.First():"Other targets";
  }
  public static List<TargetSummary> Build(IEnumerable<Frame> frames){
   var rows=frames.ToList();var result=new List<TargetSummary>{Summarize("All targets",rows)};
   result.AddRange(rows.GroupBy(f=>f.Target).Select(g=>Summarize(g.Key,g)).OrderBy(t=>Array.IndexOf(Groups,t.Group)).ThenBy(t=>t.DisplayName,StringComparer.OrdinalIgnoreCase).ThenBy(t=>t.Name,StringComparer.OrdinalIgnoreCase));return result;
  }
  static Match CometName(string name){return Regex.Match(name??"",@"^([CPDXAI]\s*/?\s*\d{4}\s*[A-Z]{1,2}\d{1,3}\b|\d{1,4}\s*[PDI]\b)\s*[/ -]?\s*(.*)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
  public static string ShortName(string name){if(name=="All targets")return name;string common=Catalog.CommonName(name);if(common.Length>0)return common;if(Group(name)=="Comets"){var match=CometName(name);if(match.Success&&match.Groups[2].Value.Length>0)return match.Groups[2].Value.Trim('(',')',' ');if(name.StartsWith("Comet ",StringComparison.OrdinalIgnoreCase))return name.Substring(6).Trim();}return name;}
  public static string Identifier(string name){if(name=="All targets")return "";if(Catalog.CommonName(name).Length>0)return Catalog.ObjectId(name);var match=CometName(name);return match.Success&&match.Groups[2].Value.Length>0?match.Groups[1].Value.Trim():"";}
  static TargetSummary Summarize(string name,IEnumerable<Frame> frames){var summary=CaptureGroups.Summarize(frames);return new TargetSummary{Name=name,Files=summary.Captures,Subs=summary.Subs,Stacks=summary.Stacks,Sessions=summary.Sessions,ExposureSeconds=summary.ExposureSeconds,UnknownExposure=summary.UnknownExposure};}
  public static string Exposure(double seconds){return seconds>=3600?((int)(seconds/3600))+"h"+(seconds%3600>=60?" "+((int)(seconds%3600/60))+"m":""):seconds>=60?((int)(seconds/60))+"m"+(seconds%60>=1?" "+((int)(seconds%60))+"s":""):seconds.ToString("0.#",CultureInfo.InvariantCulture)+"s";}
 }
}
