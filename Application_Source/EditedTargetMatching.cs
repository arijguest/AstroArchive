using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public static partial class Catalog {
  internal static KeyValuePair<string,string>[] EditedNameAliases(){return aliases.Where(p=>p.Key.Length>=6&&!p.Key.Any(char.IsDigit)).ToArray();}
 }
 // Names tolerate small spelling errors. Catalogue numbers never do: a changed
 // digit can identify a completely different object.
 public sealed class EditedTargetMatcher {
  static readonly KeyValuePair<string,string>[] CatalogAliases=Catalog.EditedNameAliases();
  public static readonly EditedTargetMatcher Default=new EditedTargetMatcher(null);
  readonly KeyValuePair<string,string>[] names;
  public EditedTargetMatcher(IEnumerable<string> targets){
   var entries=new List<KeyValuePair<string,string>>(CatalogAliases);
   foreach(string target in targets??Enumerable.Empty<string>()){if(Catalog.IsAmbiguous(target)||Catalog.HasFilenameConflict(target))continue;string key=Key(target);if(key.Length>=6&&!key.Any(char.IsDigit))entries.Add(new KeyValuePair<string,string>(key,Catalog.CanonicalTarget(target)));}
   names=entries.Distinct().ToArray();
  }
  static string Key(string text){var result=new StringBuilder();foreach(char c in (text??"").Normalize(NormalizationForm.FormD))if(char.IsLetterOrDigit(c))result.Append(char.ToUpperInvariant(c));return result.ToString();}
  static int Distance(string a,string b){
   var previous=new int[b.Length+1];var before=new int[b.Length+1];var current=new int[b.Length+1];for(int j=0;j<=b.Length;j++)previous[j]=j;
   for(int i=1;i<=a.Length;i++){current[0]=i;for(int j=1;j<=b.Length;j++){current[j]=Math.Min(Math.Min(previous[j]+1,current[j-1]+1),previous[j-1]+(a[i-1]==b[j-1]?0:1));if(i>1&&j>1&&a[i-1]==b[j-2]&&a[i-2]==b[j-1])current[j]=Math.Min(current[j],before[j-2]+1);}var swap=before;before=previous;previous=current;current=swap;}
   return previous[b.Length];
  }
  public string Resolve(string text,out string evidence){
   evidence=null;if(string.IsNullOrWhiteSpace(text)||Catalog.HasFilenameConflict(text)||Regex.IsMatch(text,@"\b(?:M|NGC|IC|C|CALDWELL|B|UGC|PGC|SH\s*2)\s*[_-]?\s*\d",RegexOptions.IgnoreCase))return null;
   string[] words=Regex.Split(text,@"[^\p{L}\p{N}]+").Where(w=>w.Length>0).Take(64).ToArray();var probes=new HashSet<string>();
   for(int start=0;start<words.Length;start++){string probe="";for(int count=0;count<6&&start+count<words.Length;count++){probe+=Key(words[start+count]);if(probe.Length>=6&&probe.Length<=64)probes.Add(probe);}}
   var scores=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
   foreach(var name in names)foreach(string probe in probes){int length=Math.Max(probe.Length,name.Key.Length),limit=Math.Min(3,(int)Math.Floor(length*0.16));if(Math.Abs(probe.Length-name.Key.Length)>limit)continue;int edits=Distance(probe,name.Key);if(edits>limit)continue;double score=1.0-(double)edits/length,previous;if(!scores.TryGetValue(name.Value,out previous)||score>previous)scores[name.Value]=score;}
   var ranked=scores.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).ToList();if(ranked.Count==0)return null;if(ranked.Count>1&&ranked[0].Value-ranked[1].Value<0.06){evidence="Target name resembles multiple objects; identity left unresolved";return null;}
   evidence="Object from "+(ranked[0].Value==1?"name matching":"fuzzy name matching")+": "+Catalog.Label(ranked[0].Key)+" ("+(ranked[0].Value*100).ToString("0",CultureInfo.InvariantCulture)+"% name similarity)";return ranked[0].Key;
  }
 }
}
