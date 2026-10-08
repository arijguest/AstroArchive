// A saved nickname always belongs to one known astronomical identity.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
namespace AstroArchive {
 public sealed class TargetNameRule {
  public string Id{get;set;}public string CommonName{get;set;}public List<string> Aliases{get;set;}
 }
 public static partial class Catalog {
  sealed class SavedNameIndex {
   public readonly Dictionary<string,string> Aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase),Common=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase),Descriptions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   public readonly List<KeyValuePair<Regex,string>> Phrases=new List<KeyValuePair<Regex,string>>();
  }
  static volatile SavedNameIndex savedNames=new SavedNameIndex();static int namesRevision;
  public static int NamesRevision{get{return namesRevision;}}
  internal static string NameKey(string text){return FileSearch.Fold(CompactId(text)).Replace(" ","");}
  static readonly Regex CatalogueId=new Regex(@"^(?:M|NGC|IC|C|B|UGC|PGC|SH2)\d+[A-Z]?$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  static SavedNameIndex SavedNames(IEnumerable<TargetNameRule> rules){
   var index=new SavedNameIndex();var ids=new HashSet<string>();
   foreach(var rule in rules??Enumerable.Empty<TargetNameRule>()){
    if(rule==null)throw new ArgumentException("Each name entry needs an object ID.");
    string id;string key=NameKey(rule.Id);if(!aliases.TryGetValue(key,out id)||!Objects.Any(o=>o.Name==id))throw new ArgumentException("Choose a known catalogue ID: "+rule.Id);
    if(!ids.Add(id))throw new ArgumentException("Keep one name entry per object: "+id);
    var common=(rule.CommonName??"").Trim();var labels=(rule.Aliases??new List<string>()).Concat(common.Length==0?Enumerable.Empty<string>():new[]{common}).Where(s=>!string.IsNullOrWhiteSpace(s)).Select(s=>s.Trim()).Distinct().ToList();
    foreach(string label in labels){
     string aliasKey=NameKey(label),owner;if(aliasKey.Length<2||label.Length>160)throw new ArgumentException("Use a name between 2 and 160 characters.");
     if(IsAmbiguous(label))throw new ArgumentException("This name refers to more than one object; include its catalogue ID: "+label);
     if(aliases.TryGetValue(aliasKey,out owner)&&owner!=id)throw new ArgumentException(label+" belongs to "+owner+", not "+id+".");
     if(CatalogueId.IsMatch(aliasKey)&&(!aliases.TryGetValue(aliasKey,out owner)||owner!=id))throw new ArgumentException("Catalogue numbers cannot be reassigned: "+label);
     var mentioned=FilenameTargets(label,true);if(mentioned.Any(target=>target!=id))throw new ArgumentException("The name includes another object: "+label);
     if(index.Aliases.TryGetValue(aliasKey,out owner)&&owner!=id)throw new ArgumentException("The same name is assigned to two objects: "+label);
     index.Aliases[aliasKey]=id;
     string words=FileSearch.Fold(label);if(!CatalogueId.IsMatch(aliasKey))index.Phrases.Add(new KeyValuePair<Regex,string>(new Regex(@"\b"+string.Join(@"\s*",words.Split(' ').Select(Regex.Escape))+@"\b",RegexOptions.CultureInvariant),id));
    }
    if(common.Length>0)index.Common[id]=common;index.Descriptions[id]=string.Join(" ",labels);
   }return index;
  }
  public static void ValidateNames(IEnumerable<TargetNameRule> rules){SavedNames(rules);}
  public static void ConfigureNames(IEnumerable<TargetNameRule> rules){var index=SavedNames(rules);savedNames=index;Interlocked.Increment(ref namesRevision);}
  public static List<TargetNameRule> ChangeNames(IEnumerable<TargetNameRule> rules,TargetNameRule change){
   string id=KnownName(change.Id);var updated=(rules??Enumerable.Empty<TargetNameRule>()).Where(r=>KnownName(r.Id)!=id).Select(r=>new TargetNameRule{Id=r.Id,CommonName=r.CommonName,Aliases=r.Aliases==null?null:r.Aliases.ToList()}).ToList();
   updated.Add(new TargetNameRule{Id=change.Id,CommonName=change.CommonName,Aliases=change.Aliases==null?null:change.Aliases.ToList()});ValidateNames(updated);return updated;
  }
  static Dictionary<string,string[]> catalogueIds;
  static void IndexCatalogueIds(){
   catalogueIds=aliases.Where(p=>CatalogueId.IsMatch(p.Key)).GroupBy(p=>p.Value).ToDictionary(g=>g.Key,g=>g.Select(p=>p.Key).Distinct().OrderBy(IdRank).ThenBy(id=>id,StringComparer.OrdinalIgnoreCase).ToArray());
  }
  static int IdRank(string id){return id.StartsWith("M",StringComparison.Ordinal)?0:id.StartsWith("C",StringComparison.Ordinal)?1:id.StartsWith("NGC",StringComparison.Ordinal)?2:id.StartsWith("IC",StringComparison.Ordinal)?3:id.StartsWith("UGC",StringComparison.Ordinal)?6:id.StartsWith("PGC",StringComparison.Ordinal)?7:4;}
  public static string[] CatalogueIds(string target){string id=ObjectId(target);string[] others;if(!catalogueIds.TryGetValue(id,out others))return id.Length==0?new string[0]:new[]{id};return new[]{id}.Concat(others.Where(alias=>alias!=id)).ToArray();}
 }
}
