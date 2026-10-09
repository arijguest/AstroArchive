using System;
using System.IO;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public static class CometTargets {
  static readonly Regex designation=new Regex(@"(?<![A-Z0-9])(?:(?<class>[CPDX])\s*[/_ -]?\s*(?<year>\d{4})\s*[_ -]?\s*(?<code>[A-Z]{1,2})\s*(?<number>\d{1,3})(?![A-Z0-9])|(?<period>\d{1,4})\s*(?<kind>[PD])(?=$|[/_ -])|(?<interstellar>[23]I)\s*/\s*(?<visitor>Borisov|ATLAS)\b)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  const string Names=@"Hale[- ]Bopp|Pons[- ]Brooks|Tsuchinshan[- ]ATLAS|Halley|Hyakutake|NEOWISE|NEAT|LINEAR|PAN[- ]?STARRS|SWAN|SOHO|Catalina|Lovejoy|ZTF|Lemmon";
  public static string FromFilename(string filename){return FromLabel(Path.GetFileName(filename??""));}
  public static string FromLabel(string label){
   string text=Regex.Replace(label??"",@"\.(?:fit|fits|fts)(?:\.gz)?$|\.[a-z0-9]+$","",RegexOptions.IgnoreCase).Replace('_',' ');
   var match=designation.Match(text);var name=Regex.Match(text,@"\b("+Names+@")\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
   if(match.Success){string id=match.Groups["class"].Success?match.Groups["class"].Value.ToUpperInvariant()+"/"+match.Groups["year"].Value+" "+match.Groups["code"].Value.ToUpperInvariant()+match.Groups["number"].Value:match.Groups["period"].Success?match.Groups["period"].Value+match.Groups["kind"].Value.ToUpperInvariant():match.Groups["interstellar"].Value.ToUpperInvariant();
    if(match.Groups["visitor"].Success)return id+"/"+match.Groups["visitor"].Value;
    if(!name.Success)name=Regex.Match(text.Substring(match.Index+match.Length),@"^\s*[/ -]?\s*\(([^)]+)\)");
    if(!name.Success)name=Regex.Match(text.Substring(match.Index+match.Length),@"^\s*[/ -]?\s*(ATLAS)\b",RegexOptions.IgnoreCase);
    if(!name.Success&&match.Groups["period"].Success)name=Regex.Match(text.Substring(match.Index+match.Length),@"^\s*/\s*([a-z][a-z'-]*(?:[ -][a-z][a-z'-]*)*)",RegexOptions.IgnoreCase);
    string discovered=name.Success?Regex.Split(name.Groups[1].Value,@"[ -]+(?:stack(?:ed)?|edit(?:ed)?|final|starless|stars|light|raw|exp|gain)\b",RegexOptions.IgnoreCase)[0].Trim():"";
    return id+(discovered.Length>0?(match.Groups["period"].Success?"/"+discovered:" ("+discovered+")"):"");
   }
   if(name.Success)return name.Value;
   return Regex.IsMatch(text,@"^(?:Comet\s+)?ATLAS$",RegexOptions.IgnoreCase)?"ATLAS":null;
  }
  public static bool IsComet(string name){return FromLabel(name)!=null||Regex.IsMatch(name??"",@"\bcomet\b",RegexOptions.IgnoreCase);}
 }
}
