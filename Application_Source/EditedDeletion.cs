// Delete mutable Edited copies without touching capture records or source originals.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed partial class Repository {
  public FileDeletionResult DeleteEditedImages(IEnumerable<EditedImage> selection,CancellationToken ct,Action<ProgressInfo> progress){
   ct.ThrowIfCancellationRequested();var projects=new Dictionary<string,EditedProject>(StringComparer.OrdinalIgnoreCase);var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var images=new List<EditedImage>();
   // Validate the entire selection, including project records and links, before moving files.
   foreach(var image in selection){
    ct.ThrowIfCancellationRequested();if(image==null||image.Project==null)throw new InvalidDataException("Select an Edited image from the gallery.");
    string path=EditedPath(image.Project,image.RelativePath);if(!Util.IsImageAsset(path)||Directory.Exists(path))throw new InvalidDataException("Only Edited image files can be deleted.");
    if(!projects.ContainsKey(image.Project.Id))projects.Add(image.Project.Id,ReadEditedProject(image.Project));
    if(paths.Add(path))images.Add(image);
   }
   if(images.Count==0)throw new InvalidOperationException("Select Edited files to delete.");
   var result=new FileDeletionResult();string staging=Path.Combine(Meta,"staging","delete-edited-"+Guid.NewGuid().ToString("N"));CheckManagedPath(Path.Combine(staging,"image"),Root);
   try{for(int i=0;i<images.Count;i++){
    ct.ThrowIfCancellationRequested();var image=images[i];if(progress!=null)progress(new ProgressInfo{Done=i,Total=images.Count,Stage="Deleting Edited files",Text=image.Filename});
    string path=EditedPath(image.Project,image.RelativePath),staged=Path.Combine(staging,i.ToString("D8")+".image");bool moved=false,committed=false;
    try{
     ct.ThrowIfCancellationRequested();if(!File.Exists(path))throw new IOException("Edited file is missing. Refresh and select it again.");
     Directory.CreateDirectory(staging);CheckManagedPath(staged,Root);FileRetry.Run(()=>{MoveCapture(path,staged);return true;},ct,null);moved=true;
     var project=Util.Deserialize<EditedProject>(Util.Serialize(projects[image.Project.Id]));
     // Retain source provenance: remaining outputs can still inherit acquisition metadata.
     if(project.MetadataEdits!=null)foreach(string key in project.MetadataEdits.Keys.Where(k=>EditedPath(project,k).Equals(path,StringComparison.OrdinalIgnoreCase)).ToArray())project.MetadataEdits.Remove(key);
     SaveEditedProject(project,ct);committed=true;projects[project.Id]=project;result.Deleted++;
     FileRetry.Run(()=>{CheckManagedPath(staged,Root);File.Delete(staged);return true;},CancellationToken.None,null);
    }catch(Exception error){
     if(moved&&!committed)try{CheckManagedPath(path,Root);CheckManagedPath(staged,Root);MoveCapture(staged,path);}catch(Exception rollback){throw new IOException("Edited deletion could not restore "+path+". Recover the file from "+staged+": "+rollback.Message,error);}
     if(error is OperationCanceledException)throw;
     result.Errors.Add(FileRetry.Detail(committed?staged:path,error));
    }
   }}finally{CheckManagedPath(Path.Combine(staging,"image"),Root);if(Directory.Exists(staging)&&!Directory.EnumerateFileSystemEntries(staging).Any())Directory.Delete(staging);}
   if(progress!=null)progress(new ProgressInfo{Done=images.Count,Total=images.Count,Stage="Deleting Edited files",Text=result.Deleted+" files deleted"});return result;
  }
 }
}
