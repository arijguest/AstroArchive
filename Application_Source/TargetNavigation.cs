using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
namespace AstroArchive {
 public static class TargetNavigation {
  public static readonly string[] Groups={"Solar system","Nebulae","Galaxies","Star clusters","Stars","Meteors","Other targets","Calibration","Unidentified"};
  // Every comet is listed under one Comets entry; its captures stay grouped by comet inside it.
  public const string CometsSection="Comets";
  // Called for every capture while filtering, so each distinct target name is classified once.
  static readonly System.Collections.Concurrent.ConcurrentDictionary<string,bool> comets=new System.Collections.Concurrent.ConcurrentDictionary<string,bool>(StringComparer.Ordinal);
  public static string Section(string target){return target!=null&&target!=CometsSection&&comets.GetOrAdd(target,CometTargets.IsComet)?CometsSection:target;}
  // Derived entries that hold several stored targets (Comets) or captures of one body (eclipses).
  public static bool IsSection(string name){return name==CometsSection||Eclipses.IsSection(name);}
  // Keeps each target's rows together, in the order its first row appears, without reordering within a target.
  public static List<T> GroupByTarget<T>(IEnumerable<T> rows,Func<T,string> target){return rows.GroupBy(target,StringComparer.OrdinalIgnoreCase).SelectMany(g=>g).ToList();}
  static readonly Lazy<Dictionary<string,string>> types=new Lazy<Dictionary<string,string>>(()=>Catalog.Objects.GroupBy(o=>o.Name).ToDictionary(g=>g.Key,g=>g.First().Type,StringComparer.OrdinalIgnoreCase));
  public static string Group(string name){
   if(name=="All targets")return "";string target=Catalog.CanonicalTarget(name);string type;
   if(target.Equals("Unknown",StringComparison.OrdinalIgnoreCase))return "Unidentified";
   if(target.Equals("Meteor",StringComparison.OrdinalIgnoreCase))return "Meteors";
   if(target=="Calibration")return "Calibration";
   if(IsSection(name))return "Solar system";
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
   if(CometTargets.IsComet(target))return "Solar system";
   if(Regex.IsMatch(label,@"\b(nebula|nebulae|supernova remnant)\b",RegexOptions.IgnoreCase))hints.Add("Nebulae");
   if(Regex.IsMatch(label,@"\b(galaxy|galaxies)\b",RegexOptions.IgnoreCase))hints.Add("Galaxies");
   if(Regex.IsMatch(label,@"\b(cluster|clusters)\b",RegexOptions.IgnoreCase))hints.Add("Star clusters");
   return hints.Count==1?hints.First():"Other targets";
  }
  public static List<TargetSummary> Build(IEnumerable<Frame> frames,CancellationToken token=default(CancellationToken)){return Build(frames,null,token);}
  // Edited images and other lists without derived sections list every comet under Comets.
  public static List<TargetSummary> BuildSections(IEnumerable<Frame> frames,CancellationToken token=default(CancellationToken)){return Build(frames,f=>Section(f.Target),token);}
  // The section key lets derived groups (eclipses) list captures apart from their stored target.
  public static List<TargetSummary> Build(IEnumerable<Frame> frames,Func<Frame,string> section,CancellationToken token=default(CancellationToken)){
   var rows=frames.ToList();var result=new List<TargetSummary>{Summarize("All targets",rows,token)};
   result.AddRange(rows.GroupBy(section??(f=>f.Target)).Select(g=>Summarize(g.Key,g,token)).OrderBy(t=>Array.IndexOf(Groups,t.Group)).ThenBy(t=>t.Name==CometsSection||CometTargets.IsComet(t.Name)?1:0).ThenBy(t=>t.DisplayName,StringComparer.OrdinalIgnoreCase).ThenBy(t=>t.Name,StringComparer.OrdinalIgnoreCase));token.ThrowIfCancellationRequested();return result;
  }
  static Match CometName(string name){return Regex.Match(name??"",@"^([CPDXAI]\s*/?\s*\d{4}\s*[A-Z]{1,2}\d{1,3}\b|\d{1,4}\s*[PDI]\b)\s*[/ -]?\s*(.*)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
  public static string ShortName(string name){
   if(name=="All targets")return Localization.T("All Targets");string common=Catalog.CommonName(name);
   if(common.Length>0){
    string id=PreferredId(name),suffix=" ("+id+")";
    // The row already starts with this ID; keep qualified names in the resolver.
    if(common.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))common=common.Substring(0,common.Length-suffix.Length);
    return Catalog.DisplayId(id)+" - "+Catalog.DisplayIds(common);
   }
   // Comets keep IAU styling (169P/NEAT, C/2023 A3 (Tsuchinshan-ATLAS)) rather than the catalogue "ID - name" layout.
   if(CometTargets.IsComet(name)){var match=CometName(name);if(match.Success&&match.Groups[2].Value.Length>0){string id=match.Groups[1].Value.Trim(),discoverer=match.Groups[2].Value.Trim('(',')',' ');return char.IsDigit(id[0])?id+"/"+discoverer:id+" ("+discoverer+")";}if(name.StartsWith("Comet ",StringComparison.OrdinalIgnoreCase))return name.Substring(6).Trim();}return name;
  }
  public static string Identifier(string name){if(name=="All targets")return "";var ids=Catalog.CatalogueIds(name);if(ids.Length>0)return string.Join(" · ",ids.Where(id=>id!=PreferredId(name)).Select(x=>Catalog.DisplayId(x)));return "";}
  public static string TargetId(string name){return name=="All targets"?Localization.T("All Targets"):Catalog.DisplayId(RecordedId(name));}
  // Compact ID for evidence that is saved with a capture.
  public static string RecordedId(string name){string id=PreferredId(name);if(id.Length>0)return id;var comet=CometName(name);return comet.Success?comet.Groups[1].Value.Trim():name;}
  public static string WithoutObjectType(string title){return Regex.Replace(title??"",@"\s+(?:(?:planetary|emission|reflection|dark)\s+nebula|(?:open|globular|star)\s+cluster|supernova\s+remnant|nebula|galaxy|cluster)$","",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
  public static string PreferredId(string name){return Catalog.CatalogueIds(name).OrderBy(id=>id.StartsWith("M",StringComparison.Ordinal)?0:id.StartsWith("NGC",StringComparison.Ordinal)?1:id.StartsWith("IC",StringComparison.Ordinal)?2:id.StartsWith("C",StringComparison.Ordinal)?3:4).FirstOrDefault()??"";}
  static TargetSummary Summarize(string name,IEnumerable<Frame> frames,CancellationToken token){var summary=CaptureGroups.Summarize(frames,token);return new TargetSummary{Name=name,Files=summary.Captures,Subs=summary.Subs,Videos=summary.Videos,Stacks=summary.Stacks,Sessions=summary.Sessions,ExposureSeconds=summary.ExposureSeconds,UnknownExposure=summary.UnknownExposure};}
  public static string Exposure(double seconds){return seconds>=3600?((int)(seconds/3600))+"h"+(seconds%3600>=60?" "+((int)(seconds%3600/60))+"m":""):seconds>=60?((int)(seconds/60))+"m"+(seconds%60>=1?" "+((int)(seconds%60))+"s":""):seconds.ToString("0.#",CultureInfo.InvariantCulture)+"s";}
 }
}
