using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public static class ObservationTargets {
  static readonly string[] Bodies={"Sun","Solar","Moon","Lunar","Mercury","Venus","Mars","Jupiter","Saturn","Uranus","Neptune","Pluto","Planetary"};
  public static string CanonicalSolar(string target){string text=(target??"").Trim();return text.Equals("Solar",StringComparison.OrdinalIgnoreCase)||text.Equals("Sun",StringComparison.OrdinalIgnoreCase)?"Sun":text.Equals("Lunar",StringComparison.OrdinalIgnoreCase)||text.Equals("Moon",StringComparison.OrdinalIgnoreCase)?"Moon":target;}
  public static bool Unstretched(string target,string observationMode){
   return Bodies.Any(body=>string.Equals((target??"").Trim(),body,StringComparison.OrdinalIgnoreCase))||Regex.IsMatch(observationMode??"",@"^\s*(solar|sun|planetary|planet|lunar|moon)(?:[ _-]+(?:mode|capture|imaging|light))?\s*$",RegexOptions.IgnoreCase);
  }
  public static string ModeFromPath(string path){
   var parts=(path??"").Replace('\\','/').Split('/').Reverse().Take(3).ToArray();
   for(int index=0;index<parts.Length;index++){
    string name=Regex.Replace(parts[index],@"\.(fit|fits|fts)(\.gz)?$|\.(xisf|tiff?|png|jpe?g|bmp)$","",RegexOptions.IgnoreCase);
    string words=Regex.Replace(name,@"[_-]+"," ");
    if(Regex.IsMatch(words,@"\b(nebula|galaxy|cluster)\b",RegexOptions.IgnoreCase)){if(index==0)return "";continue;}
    if(index>0&&!Regex.IsMatch(words,@"^("+string.Join("|",Bodies)+@")(?: (?:sub|raw|captures?|images?|mode|20\d{2}.*))?$",RegexOptions.IgnoreCase))continue;
    foreach(string body in Bodies)if(Regex.IsMatch(words,@"\b"+Regex.Escape(body)+@"\b",RegexOptions.IgnoreCase))return body;
   }return "";
  }
 }
 public sealed partial class Repository {
  // Repair stored metadata without moving images or reading capture files.
  void NormalizeStoredMetadata(){
   db.Transaction(()=>{
    foreach(var frame in All()){bool changed=Classifier.ApplyStackMetadata(frame);string target=ObservationTargets.CanonicalSolar(frame.Target);if(target!=frame.Target||changed){frame.Target=target;Save(frame);}}
    foreach(string data in db.Query("SELECT data FROM source_manifest")){var manifest=Util.Deserialize<SourceManifest>(data);if(manifest.Metadata==null)continue;bool changed=Classifier.ApplyStackMetadata(manifest.Metadata);string target=ObservationTargets.CanonicalSolar(manifest.Metadata.Target);if(target!=manifest.Metadata.Target||changed){manifest.Metadata.Target=target;Manifest(manifest);}}
    foreach(var deletion in Deletions()){if(deletion.Metadata==null)continue;bool changed=Classifier.ApplyStackMetadata(deletion.Metadata);string target=ObservationTargets.CanonicalSolar(deletion.Metadata.Target);if(target!=deletion.Metadata.Target||changed){deletion.Metadata.Target=target;db.Exec("INSERT OR REPLACE INTO deleted_files(hash,data) VALUES(?,?)",deletion.Hash,Util.Serialize(deletion));}}
   });
  }
 }
}
