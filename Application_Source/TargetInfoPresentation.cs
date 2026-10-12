using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public sealed class TargetInfoSource {public string Title,Url;}
 public static class TargetInfoPresentation {
  public static string Constellation(string value){
   if(new[]{"Ser","Se1","Se2","Serpens","Serpens Caput","Serpens Cauda"}.Any(v=>string.Equals(v,value,StringComparison.OrdinalIgnoreCase)))return "Serpens";
   if(new[]{"CrA","Corona Austrina","Corona Australis"}.Any(v=>string.Equals(v,value,StringComparison.OrdinalIgnoreCase)))return "Corona Australis";
   var figure=SkyFigures.Figures.FirstOrDefault(f=>string.Equals(f.Id,value,StringComparison.OrdinalIgnoreCase)||string.Equals(f.Name,value,StringComparison.OrdinalIgnoreCase));
   return figure==null?null:figure.Name;
  }
  public static string Summary(TargetInfoCard card){
   string summary=card.summary??"";
   // Catalogue summaries use IAU abbreviations. Resolve the actual value rather
   // than showing the schema annotation or changing reviewed narrative prose.
   return Regex.Replace(summary,@"\b([A-Za-z][A-Za-z0-9]{2}) \(IAU constellation code\)",m=>Constellation(m.Groups[1].Value)??m.Groups[1].Value);
  }
  // A curated overview, when one exists, replaces the pack's summary.
  // Comets without either explain their designation instead of showing nothing.
  public static string DisplaySummary(TargetInfoCard card){
   var curated=TargetDescriptions.For(card.id);if(curated!=null&&!string.IsNullOrWhiteSpace(curated.Overview))return curated.Overview;
   string summary=Summary(card);return string.IsNullOrWhiteSpace(summary)&&CometInfo.IsComet(card)?CometInfo.Overview(card):summary;
  }
  public static string Imaging(TargetInfoCard card){var curated=TargetDescriptions.For(card.id);return curated!=null&&!string.IsNullOrWhiteSpace(curated.Imaging)?curated.Imaging:CometInfo.IsComet(card)?CometInfo.Imaging:null;}
  public static string Caption(string value){
   string text=value??"";
   text=text.Replace("Astronomical observation; filter bands and colour processing unspecified.","").Replace("Optical astrophotography; processing/filter details not stated.","");
   return Regex.Replace(text,@"\s*Do not (?:label|call|describe)[^.]*\.","").Trim();
  }
  public static string Credit(TargetInfoImage image){
   string credit=image.attribution??image.creator??"";
   int title=credit.LastIndexOf(" — ",StringComparison.Ordinal);
   if(title>0&&credit.EndsWith(", via Wikimedia Commons",StringComparison.Ordinal))credit=credit.Substring(0,title);
   return credit.Trim();
  }
  public static string Tagline(TargetInfoCard card,string constellation){
   // "IC 434" or "NGC 2237 (Rosette A)" already show the ID in the heading.
   string id=(card.id??"").Trim();bool inHeading=id.Length>0&&Regex.IsMatch(card.name??"",@"(?<![A-Za-z0-9])"+string.Join(@"[\s-]*",Regex.Split(id,@"[\s-]+").Select(Regex.Escape))+@"(?![0-9])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
   bool named=!string.IsNullOrWhiteSpace(card.name)&&!inHeading;
   var parts=new List<string>();if(named)parts.Add(card.id);if(!string.IsNullOrWhiteSpace(card.type_label))parts.Add(card.type_label);if(constellation!=null)parts.Add(constellation);
   return string.Join(" · ",parts);
  }
  public static List<TargetInfoSource> Sources(TargetInfoCard card){return Sources(card,false);}
  // Curated overviews cite SIMBAD for the object, and NASA's Messier pages where relevant.
  // Comets cite JPL's Small-Body Database, the source of their orbits, instead.
  public static List<TargetInfoSource> Sources(TargetInfoCard card,bool curated){
   var sources=new List<TargetInfoSource>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   Action<string,string> add=(title,url)=>{
    Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))return;
    string key=uri.GetLeftPart(UriPartial.Path).TrimEnd('/')+uri.Query;
    if(seen.Add(key))sources.Add(new TargetInfoSource{Title=string.IsNullOrWhiteSpace(title)?uri.Host:title,Url=url});
   };
   foreach(var citation in card.citations??new TargetInfoCitation[0])if(citation.review_status!="draft")add(citation.source_title,citation.source_url);
   foreach(string url in card.source_urls??new string[0])add(null,url);
   if(CometInfo.IsComet(card)){if(CometInfo.Orbit(card.id)!=null||curated)add(CometInfo.SourceTitle+" · "+card.id,CometInfo.LookupUrl(card.id));}
   else if(curated){
    var messier=Regex.Match(card.id??"",@"^M(\d{1,3})$");if(messier.Success)add("NASA Hubble Messier Catalog · Messier "+messier.Groups[1].Value,"https://science.nasa.gov/mission/hubble/science/explore-the-night-sky/hubble-messier-catalog/messier-"+messier.Groups[1].Value+"/");
    add("SIMBAD · "+card.id,"https://simbad.cds.unistra.fr/simbad/sim-id?Ident="+Uri.EscapeDataString(Regex.Replace(card.id,@"^SH2-","SH 2-",RegexOptions.IgnoreCase)));
   }
   return sources;
  }
 }
}
namespace AstroArchive {
 public sealed class TargetDescription {public string Overview,Imaging,Fact;public string[] HiddenAliases=new string[0];}
 // Curated overviews for commonly imaged targets, embedded in the application.
 // They replace generated catalogue sentences ("NGC 7000 is a h ii region in Cyg").
 public static class TargetDescriptions {
  static readonly Lazy<Dictionary<string,TargetDescription>> entries=new Lazy<Dictionary<string,TargetDescription>>(()=>{
   using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TargetDescriptions.txt")){
    if(stream==null)return new Dictionary<string,TargetDescription>(StringComparer.OrdinalIgnoreCase);
    using(var reader=new System.IO.StreamReader(stream,System.Text.Encoding.UTF8))return Parse(reader.ReadToEnd());
   }
  });
  public static Dictionary<string,TargetDescription> Parse(string text){
   var result=new Dictionary<string,TargetDescription>(StringComparer.OrdinalIgnoreCase);TargetDescription current=null;var overview=new List<string>();
   Action close=()=>{if(current!=null&&overview.Count>0)current.Overview=string.Join(" ",overview);overview.Clear();};
   foreach(string raw in (text??"").Replace("\r","").Split('\n')){
    string line=raw.Trim();if(line.Length==0||line.StartsWith("# "))continue;
    if(line.StartsWith("## ")){close();current=new TargetDescription();result[line.Substring(3).Trim()]=current;continue;}
    if(current==null)continue;
    if(line.StartsWith("Imaging:"))current.Imaging=line.Substring(8).Trim();
    else if(line.StartsWith("Fact:"))current.Fact=line.Substring(5).Trim();
    else if(line.StartsWith("Not:"))current.HiddenAliases=line.Substring(4).Split(',').Select(a=>a.Trim()).Where(a=>a.Length>0).ToArray();
    else overview.Add(line);
   }
   close();return result;
  }
  public static TargetDescription For(string id){TargetDescription entry;return id!=null&&entries.Value.TryGetValue(id,out entry)?entry:null;}
  public static int Count{get{return entries.Value.Count;}}
 }
}
