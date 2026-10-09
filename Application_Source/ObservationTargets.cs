using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public static class ObservationTargets {
  static readonly string[] Bodies={"Sun","Solar","Moon","Lunar","Mercury","Venus","Mars","Jupiter","Saturn","Uranus","Neptune","Pluto","Planetary"};
  static readonly Regex solarWords=new Regex(@"(?<!\p{L})(?:"+string.Join("|",Bodies.Where(b=>b!="Planetary"))+@")(?!\p{L})",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Compiled);
  public static string NamedSolar(string target){string body=Bodies.FirstOrDefault(b=>b!="Planetary"&&b.Equals((target??"").Trim(),StringComparison.OrdinalIgnoreCase));return body=="Solar"?"Sun":body=="Lunar"?"Moon":body;}
  public static string CanonicalSolar(string target){return NamedSolar(target)??target;}
  public static string SolarFromFilename(string filename){
   string name=Path.GetFileName(filename??"");if(Regex.IsMatch(name,@"(?<![a-z])(nebula|galaxy|cluster)(?![a-z])",RegexOptions.IgnoreCase))return null;
   var found=solarWords.Matches(name).Cast<Match>().Select(m=>NamedSolar(m.Value)).Distinct().ToArray();return found.Length==1?found[0]:null;
  }
  public static bool Unstretched(string target,string observationMode){
   return Bodies.Any(body=>string.Equals((target??"").Trim(),body,StringComparison.OrdinalIgnoreCase))||Regex.IsMatch(observationMode??"",@"^\s*(solar|sun|planetary|planet|lunar|moon)(?:[ _-]+(?:mode|capture|imaging|light))?\s*$",RegexOptions.IgnoreCase);
  }
  public static string ModeFromPath(string path){
   var parts=(path??"").Replace('\\','/').Split('/').Reverse().Take(3).ToArray();
   for(int index=0;index<parts.Length;index++){
    string name=Regex.Replace(parts[index],@"\.(fit|fits|fts)(\.gz)?$|\.(xisf|tiff?|png|jpe?g|bmp)$","",RegexOptions.IgnoreCase);
    string words=Regex.Replace(name,@"[_-]+"," ");
    if(Regex.IsMatch(words,@"\b(nebula|galaxy|cluster)\b",RegexOptions.IgnoreCase)){if(index==0)return "";continue;}
    if(index==0){string body=SolarFromFilename(name);if(body!=null)return body;if(Regex.IsMatch(words,@"\bPlanetary\b",RegexOptions.IgnoreCase))return "Planetary";continue;}
    if(index>0&&!Regex.IsMatch(words,@"^("+string.Join("|",Bodies)+@")(?: (?:sub|raw|captures?|images?|mode|20\d{2}.*))?$",RegexOptions.IgnoreCase))continue;
    foreach(string body in Bodies)if(Regex.IsMatch(words,@"\b"+Regex.Escape(body)+@"\b",RegexOptions.IgnoreCase))return body;
   }return "";
  }
 }
 public sealed partial class Repository {
  // Still-image repairs use saved metadata. Legacy recordings read bounded headers
  // once to recover duration; their video payloads are never decoded or hashed here.
  void NormalizeStoredMetadata(){
   db.Transaction(()=>{
    var frames=All();foreach(var frame in frames){bool changed=MediaFiles.ApplyVideoType(frame)|Classifier.ApplyStackMetadata(frame);
     if(frame.Kind=="Video"&&frame.ClassificationVersion!=Assets.ClassificationVersion){
      AssetInfo asset=null;try{string path=FilePath(frame);var before=FileStamp.Read(path);asset=Assets.Inspect(path);if(!before.ContentSame(FileStamp.Read(path)))asset=null;}
      catch(IOException){}catch(UnauthorizedAccessException){}catch(NotSupportedException){}
      MediaFiles.ApplyVideoDuration(frame,asset==null?null:asset.DurationSeconds,asset==null?null:asset.DurationSource);frame.ClassificationVersion=Assets.ClassificationVersion;changed=true;
     }
     string target=ObservationTargets.CanonicalSolar(frame.Target);if(target!=frame.Target||changed){frame.Target=target;Save(frame);}
    }
    var archived=frames.Where(f=>!string.IsNullOrEmpty(f.Hash)).ToDictionary(f=>f.Hash);
    foreach(string data in db.Query("SELECT data FROM source_manifest")){var manifest=Util.Deserialize<SourceManifest>(data);if(manifest.Metadata==null)continue;bool changed=NormalizeVideoSnapshot(manifest.Metadata,archived)|Classifier.ApplyStackMetadata(manifest.Metadata);string target=ObservationTargets.CanonicalSolar(manifest.Metadata.Target);if(target!=manifest.Metadata.Target||changed){manifest.Metadata.Target=target;Manifest(manifest);}}
    foreach(var deletion in Deletions()){if(deletion.Metadata==null)continue;bool changed=NormalizeVideoSnapshot(deletion.Metadata,archived)|Classifier.ApplyStackMetadata(deletion.Metadata);string target=ObservationTargets.CanonicalSolar(deletion.Metadata.Target);if(target!=deletion.Metadata.Target||changed){deletion.Metadata.Target=target;db.Exec("INSERT OR REPLACE INTO deleted_files(hash,data) VALUES(?,?)",deletion.Hash,Util.Serialize(deletion));}}
   });
  }
  static bool NormalizeVideoSnapshot(Frame frame,System.Collections.Generic.Dictionary<string,Frame> archived){
   bool changed=MediaFiles.ApplyVideoType(frame);if(frame.Kind!="Video"||frame.ClassificationVersion==Assets.ClassificationVersion)return changed;
   Frame copy=null;if(frame.Hash!=null)archived.TryGetValue(frame.Hash,out copy);
   MediaFiles.ApplyVideoDuration(frame,copy==null?frame.VideoDurationSeconds:copy.VideoDurationSeconds,copy==null?frame.VideoDurationSource:copy.VideoDurationSource);frame.ClassificationVersion=Assets.ClassificationVersion;return true;
  }
 }
}
