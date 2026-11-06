using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public sealed class TargetInfoSource {public string Title,Url;}
 public sealed class TargetInfoCredits {
  public string Creator,Licence,LicenceUrl,SourceUrl,Caption;
  public string[] Attributions,Modifications,RepresentationCredits;
 }
 public static class TargetInfoPresentation {
  // A distance in light-years, rounded to two significant figures. Redshift calculations and
  // collected literature estimates (Wikidata) are marked approximate; their tooltips say which.
  public static string Distance(TargetInfoMeasurement m){
   double ly=m.value;string approximate=m.band=="redshift"||m.band=="wikidata"?"≈ ":"";
   if(ly>=1e9)return approximate+Localization.F("{0} billion light-years",Significant(ly/1e9));
   if(ly>=1e6)return approximate+Localization.F("{0} million light-years",Significant(ly/1e6));
   return approximate+Localization.F("{0} light-years",Significant(ly));
  }
  static string Significant(double value){
   int decades=(int)Math.Floor(Math.Log10(Math.Abs(value)));double rounded=Math.Round(value/Math.Pow(10,decades-1))*Math.Pow(10,decades-1);
   return rounded.ToString(decades>=2?"#,##0":decades==1?"0":"0.0",System.Globalization.CultureInfo.InvariantCulture);
  }
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
  public static string Imaging(TargetInfoCard card){if(card==null)return null;var curated=TargetDescriptions.For(card.id);string text=curated!=null&&!string.IsNullOrWhiteSpace(curated.Imaging)?curated.Imaging:CometInfo.IsComet(card)||card.id=="COMETS"?CometInfo.Imaging:null;return string.IsNullOrWhiteSpace(text)?null:text.Trim();}
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
  // Credits are data, not UI labels. Keep required names, notices and links while
  // presenting HTML metadata as ordinary wrapped text rather than page markup.
  public static string CreditText(string value){return DisplayText(value,true);}
  static string DisplayText(string value,bool links){
   string text=value??"";
   for(int pass=0;pass<3;pass++){
    string before=text;text=WebUtility.HtmlDecode(text);
    text=Regex.Replace(text,@"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>","",RegexOptions.IgnoreCase);
    text=Regex.Replace(text,@"<a\b(?<attributes>[^>]{0,8000})>(?<label>[\s\S]*?)</a\s*>",match=>{
     string label=Regex.Replace(match.Groups["label"].Value,@"<[^>]{0,8000}>","").Trim();string url=AnchorUrl(match.Groups["attributes"].Value);
     return !links||url==null||label==url?label:label.Length==0?url:label+" ("+url+")";
    },RegexOptions.IgnoreCase);
    text=Regex.Replace(text,@"<\s*br\s*/?\s*>|</\s*(?:p|div|li|tr)\s*>","\n",RegexOptions.IgnoreCase);
    text=Regex.Replace(text,@"<[^>]{0,8000}>","");if(text==before)break;
   }
   text=Regex.Replace(text.Replace("\r\n","\n").Replace('\r','\n'),@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]","");
   var lines=text.Split('\n').Select(line=>Regex.Replace(line,@"[\s\u00a0]+"," ").Trim()).Where(line=>line.Length>0);
   return Regex.Replace(string.Join("\n",lines),@",\s*;",";");
  }
  static string AnchorUrl(string attributes){
   var match=Regex.Match(attributes??"",@"\bhref\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<bare>[^\s>]+))",RegexOptions.IgnoreCase);
   return !match.Success?null:CreditUrl(match.Groups["double"].Success?match.Groups["double"].Value:match.Groups["single"].Success?match.Groups["single"].Value:match.Groups["bare"].Value);
  }
  static string CreditUrl(string value){
   string text=value??"";for(int pass=0;pass<3;pass++){string decoded=WebUtility.HtmlDecode(text);if(decoded==text)break;text=decoded;}Uri uri;
   return Uri.TryCreate(text.Trim(),UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https")&&uri.UserInfo.Length==0?uri.AbsoluteUri:null;
  }
  static string CreatorText(string value){
   string text=DisplayText(value,false);
   text=Regex.Replace(text,@"^(?:credits?\s*:\s*|credit line and copyright\s*:?\s*)","",RegexOptions.IgnoreCase);
   text=Regex.Replace(text,@"^(?:[a-z-]{2,12}:)?user:\s*","",RegexOptions.IgnoreCase).Trim();
   return Regex.IsMatch(text,@"^(?:own work|unknown(?: author| creator)?|not (?:recorded|provided)|anonymous)[\s,.]*$",RegexOptions.IgnoreCase)?"":text;
  }
  static string LicenceText(string value){
   string text=DisplayText(value,false);if(text.Equals("public-domain",StringComparison.OrdinalIgnoreCase))return "Public domain";
   text=Regex.Replace(text,@"^CC[- ]BY(?:[- ]SA)?",match=>Regex.IsMatch(match.Value,@"SA$",RegexOptions.IgnoreCase)?"CC BY-SA":"CC BY",RegexOptions.IgnoreCase);
   text=Regex.Replace(text,@"(?<=\bBY|\bSA|\bCC0)-(?=\d)"," ",RegexOptions.IgnoreCase);
   return Regex.Replace(text,@"(?<=\d\.\d)-(?=[A-Z]{2}(?:$|\s))"," ").Trim();
  }
  static string[] CreditLines(params string[] values){return values.Select(CreditText).Where(value=>value.Length>0).Distinct(StringComparer.Ordinal).ToArray();}
  public static string Caption(TargetInfoImage image){return Credits(image).Caption;}
  public static TargetInfoCredits Credits(TargetInfoImage image){
   if(image==null)return new TargetInfoCredits{Creator="",Licence="",Caption="",Attributions=new string[0],Modifications=new string[0],RepresentationCredits=new string[0]};
   var rights=image.rights;string creator=CreatorText(image.creator),licence=image.licence;if(creator.Length==0&&rights!=null)creator=CreatorText(rights.creator);
   if(string.IsNullOrWhiteSpace(licence)&&rights!=null)licence=(rights.licence_id??"")+(string.IsNullOrWhiteSpace(rights.licence_version)||rights.licence_version=="not applicable"?"":" "+rights.licence_version);
   var attributions=CreditLines(image.attribution,rights==null?null:rights.full_attribution).ToList();
   foreach(string rawCreator in new[]{image.creator,rights==null?null:rights.creator}){string requiredCreator=CreditText(rawCreator),otherCreator=CreatorText(rawCreator);bool other=otherCreator.Length>0&&otherCreator!=creator&&!attributions.Any(value=>value.Contains(otherCreator));if((Regex.IsMatch(rawCreator??"",@"copyright|<a\b",RegexOptions.IgnoreCase)||other||creator.Length==0&&attributions.Count==0)&&requiredCreator.Length>0&&!attributions.Any(value=>value.Contains(requiredCreator)))attributions.Add(requiredCreator);}
   string representation=DisplayText(image.representation,false),caption=representation;bool provenance=false;
   var explicitCredit=Regex.Match(caption,@"(?:^|\s)(?:(?:image\s+)?credits?\s*:|copyright\b|©|\(c\)|acknowledg(?:e)?ments?\s*:|courtesy(?:\s+of)?\s*:|courtesy\s+of\b|photo(?:graph)?\s+by\b|image\s+by\b|image processing\s*:|(?:image\s+)?source\s*:)",RegexOptions.IgnoreCase);
   if(explicitCredit.Success){caption=caption.Substring(0,explicitCredit.Index).Trim().TrimEnd(';',',');provenance=true;}
   string previous=caption;caption=Regex.Replace(caption,@";?\s*NASA prepared version\.?","",RegexOptions.IgnoreCase);
   caption=Regex.Replace(caption,@"\s+released as ESA/Hubble Picture of the Week\b[^;.]*","",RegexOptions.IgnoreCase);
   caption=Regex.Replace(caption,@"(?:[,;]?\s*)via Wikimedia Commons\.?$","",RegexOptions.IgnoreCase);provenance|=caption!=previous;
   caption=Caption(caption).Trim().TrimEnd(';',',');
   if(provenance&&caption.Length>0&&representation.EndsWith(".",StringComparison.Ordinal)&&!Regex.IsMatch(caption,@"[.!?]$"))caption+=".";
   return new TargetInfoCredits{Creator=creator,Licence=LicenceText(licence),SourceUrl=CreditUrl(image.file_page_url)??CreditUrl(rights==null?null:rights.source_url),LicenceUrl=CreditUrl(image.licence_url)??CreditUrl(rights==null?null:rights.licence_url),Caption=caption,
    Attributions=attributions.ToArray(),Modifications=CreditLines(image.modifications,rights==null?null:rights.modifications),RepresentationCredits=provenance?CreditLines(image.representation):new string[0]};
  }
  // Other catalogue designations of the object (NGC, IC, Messier...), without the survey identifiers that fill the full alias list.
  // One alternative joins the tagline; several go on a line of their own.
  public static List<string> AlternativeIds(TargetInfoCard card){
   var result=new List<string>();if(card==null||card.aliases==null)return result;
   Func<string,string> key=t=>Regex.Replace(t??"","[^A-Za-z0-9]","").ToUpperInvariant();var seen=new HashSet<string>{key(card.id),key(Catalog.DisplayId(card.id))};string heading=key(card.name);
   foreach(string alias in card.aliases){
    var m=Regex.Match((alias??"").Trim(),@"^(M|NGC|IC|C|Caldwell|Arp|B|Mel|Cr|Collinder|Melotte|Abell|UGC|Sh\s*2)[\s-]*0*(\d+.*)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);if(!m.Success)continue;
    string text=Regex.Replace(m.Groups[1].Value,@"\s+","")+" "+m.Groups[2].Value;string id=key(text);if(!seen.Add(id)||heading.Contains(id))continue;result.Add(text);
   }
   return result.Take(8).ToList();
  }
  public static string AlternativeLine(TargetInfoCard card){var ids=AlternativeIds(card);return ids.Count>1?string.Join(" · ",ids):"";}
  public static string Tagline(TargetInfoCard card,string constellation){
   // "IC 434" or "NGC 2237 (Rosette A)" already show the ID in the heading.
   string id=(card.id??"").Trim();bool inHeading=id.Length>0&&Regex.IsMatch(card.name??"",@"(?<![A-Za-z0-9])"+string.Join(@"[\s-]*",Regex.Split(id,@"[\s-]+").Select(Regex.Escape))+@"(?![0-9])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
   bool named=!string.IsNullOrWhiteSpace(card.name)&&!inHeading;
   var parts=new List<string>();if(named)parts.Add(Catalog.DisplayId(card.id));var other=AlternativeIds(card);if(other.Count==1)parts.Add(other[0]);if(!string.IsNullOrWhiteSpace(card.type_label))parts.Add(card.type_label);if(constellation!=null)parts.Add(constellation);
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
   if(CometInfo.IsComet(card)){if(CometInfo.Orbit(card.id)!=null||curated)add(CometInfo.SourceTitle+" · "+card.id,CometInfo.LookupUrl(card.id));if(CometCatalog.ShowersOf(card.id).Count>0)add(CometCatalog.ShowerSource,CometCatalog.ShowerUrl);}
   // Eclipse and section pages are not catalogue objects, so SIMBAD has nothing for them.
   else if(curated&&card.domain!="event"&&card.domain!="group"){
    var messier=Regex.Match(card.id??"",@"^M(\d{1,3})$");if(messier.Success)add("NASA Hubble Messier Catalog · Messier "+messier.Groups[1].Value,"https://science.nasa.gov/mission/hubble/science/explore-the-night-sky/hubble-messier-catalog/messier-"+messier.Groups[1].Value+"/");
    add("SIMBAD · "+Catalog.DisplayId(card.id),"https://simbad.cds.unistra.fr/simbad/sim-id?Ident="+Uri.EscapeDataString(Regex.Replace(card.id,@"^SH2-","SH 2-",RegexOptions.IgnoreCase)));
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
