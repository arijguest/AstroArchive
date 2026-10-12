using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public sealed class TargetInfoSource {public string Title,Url;}
 public static class TargetInfoPresentation {
  public static string Constellation(string value){
   var figure=SkyFigures.Figures.FirstOrDefault(f=>string.Equals(f.Id,value,StringComparison.OrdinalIgnoreCase)||string.Equals(f.Name,value,StringComparison.OrdinalIgnoreCase));
   return figure==null?null:figure.Name;
  }
  public static string Summary(TargetInfoCard card){
   string summary=card.summary??"";
   // Catalogue summaries use IAU abbreviations. Resolve the actual value rather
   // than showing the schema annotation or changing reviewed narrative prose.
   return Regex.Replace(summary,@"\b([A-Za-z]{3}) \(IAU constellation code\)",m=>Constellation(m.Groups[1].Value)??m.Groups[1].Value);
  }
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
  public static List<TargetInfoSource> Sources(TargetInfoCard card){
   var sources=new List<TargetInfoSource>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   Action<string,string> add=(title,url)=>{
    Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))return;
    string key=uri.GetLeftPart(UriPartial.Path).TrimEnd('/')+uri.Query;
    if(seen.Add(key))sources.Add(new TargetInfoSource{Title=string.IsNullOrWhiteSpace(title)?uri.Host:title,Url=url});
   };
   foreach(var citation in card.citations??new TargetInfoCitation[0])if(citation.review_status!="draft")add(citation.source_title,citation.source_url);
   foreach(string url in card.source_urls??new string[0])add(null,url);
   return sources;
  }
 }
}
