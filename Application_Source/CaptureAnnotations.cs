// Personal stars, tags and notes. They belong to the repository
// (.astroarchive/annotations.json), keyed by capture checksum, so they follow
// backups, moved archives and metadata edits without rewriting capture files.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstroArchive {
 // Archived captures (keyed by checksum) and Edited working copies (keyed by project
 // and path, so re-saving a working copy keeps its notes) share one store.
 public interface IAnnotated {string AnnotationKey{get;}bool IsStarred{get;set;}List<string> Tags{get;set;}string UserNote{get;set;}}
 public sealed class CaptureAnnotation {
  public bool Starred{get;set;}public List<string> Tags{get;set;}public string Note{get;set;}public string Updated{get;set;}
  // Tags AstroArchive added by itself, once. Removing one stays removed.
  public List<string> AutoTags{get;set;}
  public bool Empty{get{return !Starred&&(Tags==null||Tags.Count==0)&&string.IsNullOrWhiteSpace(Note)&&(AutoTags==null||AutoTags.Count==0);}}
 }
 // One edit applied to a batch: null leaves that part of each capture unchanged.
 public sealed class AnnotationEdit {
  public bool? Starred;public string Note;public List<string> AddTags=new List<string>(),RemoveTags=new List<string>(),AutoTags=new List<string>();
 }
 public static class CaptureAnnotations {
  public const string FileName="annotations.json";public const int MaxTags=30,MaxTagLength=40,MaxNoteLength=4000;
  public static string PathFor(string meta){return Path.Combine(meta,FileName);}
  public static Dictionary<string,CaptureAnnotation> Load(string meta){
   var result=new Dictionary<string,CaptureAnnotation>(StringComparer.OrdinalIgnoreCase);string path=PathFor(meta);if(!File.Exists(path))return result;
   var saved=Util.Deserialize<Dictionary<string,CaptureAnnotation>>(File.ReadAllText(path));if(saved==null)return result;
   foreach(var pair in saved){if(string.IsNullOrWhiteSpace(pair.Key)||pair.Value==null)continue;var value=pair.Value;value.Tags=Tags(string.Join(",",value.Tags??new List<string>()));value.Note=Note(value.Note);value.AutoTags=Tags(string.Join(",",value.AutoTags??new List<string>()));if(!value.Empty)result[pair.Key]=value;}
   return result;
  }
  public static void Save(string meta,Dictionary<string,CaptureAnnotation> annotations){
   Directory.CreateDirectory(meta);var saved=annotations.Where(p=>!p.Value.Empty).OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value);
   Util.AtomicText(PathFor(meta),Util.Serialize(saved));
  }
  // Tags split on commas, semicolons or new lines; a leading # is optional.
  public static List<string> Tags(string text){
   var result=new List<string>();
   foreach(string raw in (text??"").Split(new[]{',',';','\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
    string tag=Regex.Replace(raw.Trim().TrimStart('#').Trim(),@"\s+"," ");if(tag.Length==0)continue;if(tag.Length>MaxTagLength)tag=tag.Substring(0,MaxTagLength).Trim();
    if(!result.Any(t=>string.Equals(t,tag,StringComparison.OrdinalIgnoreCase)))result.Add(tag);if(result.Count==MaxTags)break;
   }return result;
  }
  public static string Note(string text){string note=(text??"").Replace("\r\n","\n").Trim();if(note.Length>MaxNoteLength)note=note.Substring(0,MaxNoteLength);return note.Length==0?null:note;}
  public static string EditedKey(string projectId,string relativePath){return "edited:"+(projectId??"")+"/"+(relativePath??"").Replace('\\','/').ToLowerInvariant();}
  public static void Apply(IAnnotated item,Dictionary<string,CaptureAnnotation> annotations){
   CaptureAnnotation value;if(item==null)return;string key=item.AnnotationKey;
   if(string.IsNullOrEmpty(key)||annotations==null||!annotations.TryGetValue(key,out value)){item.IsStarred=false;item.Tags=null;item.UserNote=null;return;}
   item.IsStarred=value.Starred;item.Tags=value.Tags==null||value.Tags.Count==0?null:new List<string>(value.Tags);item.UserNote=value.Note;
  }
  public static void Apply<T>(IEnumerable<T> items,Dictionary<string,CaptureAnnotation> annotations) where T:IAnnotated{foreach(var item in items)Apply(item,annotations);}
  // Updates the store and the frames together; returns the number of captures changed.
  public static int Edit<T>(Dictionary<string,CaptureAnnotation> annotations,IEnumerable<T> items,AnnotationEdit edit) where T:IAnnotated{
   int changed=0;string now=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);var add=Tags(string.Join(",",edit.AddTags));var remove=Tags(string.Join(",",edit.RemoveTags));
   foreach(var group in items.Where(f=>!string.IsNullOrEmpty(f.AnnotationKey)).GroupBy(f=>f.AnnotationKey,StringComparer.OrdinalIgnoreCase)){
    CaptureAnnotation current;annotations.TryGetValue(group.Key,out current);var next=new CaptureAnnotation{Starred=current!=null&&current.Starred,Tags=current==null||current.Tags==null?new List<string>():new List<string>(current.Tags),Note=current==null?null:current.Note,AutoTags=current==null||current.AutoTags==null?new List<string>():new List<string>(current.AutoTags)};
    if(edit.Starred.HasValue)next.Starred=edit.Starred.Value;
    next.Tags.RemoveAll(t=>remove.Any(r=>string.Equals(r,t,StringComparison.OrdinalIgnoreCase)));foreach(string tag in add)if(next.Tags.Count<MaxTags&&!next.Tags.Any(t=>string.Equals(t,tag,StringComparison.OrdinalIgnoreCase)))next.Tags.Add(tag);
    if(edit.Note!=null)next.Note=Note(edit.Note);
    foreach(string tag in Tags(string.Join(",",edit.AutoTags)))if(!next.AutoTags.Any(t=>string.Equals(t,tag,StringComparison.OrdinalIgnoreCase))){next.AutoTags.Add(tag);if(next.Tags.Count<MaxTags&&!next.Tags.Any(t=>string.Equals(t,tag,StringComparison.OrdinalIgnoreCase)))next.Tags.Add(tag);}
    bool same=current!=null&&current.Starred==next.Starred&&current.Note==next.Note&&(current.Tags??new List<string>()).SequenceEqual(next.Tags)&&(current.AutoTags??new List<string>()).SequenceEqual(next.AutoTags);if(same||(current==null&&next.Empty))continue;
    changed++;if(next.Empty)annotations.Remove(group.Key);else{next.Updated=now;annotations[group.Key]=next;}
    foreach(var item in group)Apply(item,annotations);
   }
   return changed;
  }
  // Tags already used anywhere in the repository, most used first, for suggestions.
  public static List<string> KnownTags(Dictionary<string,CaptureAnnotation> annotations){
   return annotations.Values.Where(a=>a.Tags!=null).SelectMany(a=>a.Tags).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
  }
  // Tags every frame shares; a batch editor shows these and leaves the rest alone.
  public static List<string> CommonTags<T>(IList<T> items) where T:IAnnotated{
   if(items.Count==0)return new List<string>();var common=(items[0].Tags??new List<string>()).ToList();
   foreach(var item in items.Skip(1)){var tags=item.Tags??new List<string>();common.RemoveAll(t=>!tags.Any(o=>string.Equals(o,t,StringComparison.OrdinalIgnoreCase)));if(common.Count==0)break;}
   return common;
  }
 }
}
