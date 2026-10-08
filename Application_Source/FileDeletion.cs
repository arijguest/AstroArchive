// Delete only canonical indexed captures, retaining shared metadata and source mirrors.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class FileDeletionResult {public int Deleted;public List<string> Errors=new List<string>();}
 public sealed partial class Repository {
  public List<Frame> FailedFiles(){return All().Where(Util.FailedFilename).ToList();}
  public List<Frame> NonRawFiles(){return All().Where(f=>(ImportPolicy.RasterFilename(f.OriginalName)||ImportPolicy.RasterFilename(f.RelativePath))&&!Util.Within(FilePath(f),EditedFolder)&&!Util.Within(FilePath(f),Path.Combine(Root,"Edited"))).ToList();}
  public FileDeletionResult DeleteFrames(IEnumerable<Frame> selection,CancellationToken ct,Action<ProgressInfo> progress){
   var indexed=All().ToDictionary(f=>f.Hash);var frames=new List<Frame>();
   foreach(var requested in selection.GroupBy(f=>f.Hash).Select(g=>g.First())){Frame actual;if(string.IsNullOrEmpty(requested.Hash)||!indexed.TryGetValue(requested.Hash,out actual))throw new IOException("A selected file is no longer in the repository. Refresh and select it again.");frames.Add(actual);}
   if(frames.Count==0)throw new InvalidOperationException("Select files to delete.");
   // Validate the complete selection before touching any capture.
   foreach(var frame in frames){string path=FilePath(frame);CheckManagedPath(path,Root);if(Util.Within(path,Meta))throw new IOException("Repository metadata cannot be deleted as a capture.");}
   var result=new FileDeletionResult();string staging=Path.Combine(Meta,"staging","delete-"+Guid.NewGuid().ToString("N"));CheckManagedPath(Path.Combine(staging,"capture"),Root);
   try{for(int i=0;i<frames.Count;i++){
    ct.ThrowIfCancellationRequested();Frame frame=frames[i];if(progress!=null)progress(new ProgressInfo{Done=i,Total=frames.Count,Stage="Deleting files",Text=frame.OriginalName});
    string path=FilePath(frame),staged=Path.Combine(staging,i.ToString("D8")+".capture");bool moved=false,committed=false;
    try{
     CheckManagedPath(path,Root);
     if(File.Exists(path)){Directory.CreateDirectory(staging);CheckManagedPath(staged,Root);FileRetry.Run(()=>{File.Move(path,staged);return true;},ct,null);moved=true;}
     try{db.Transaction(()=>{RememberDeletion(frame);db.Exec("DELETE FROM files WHERE hash=?",frame.Hash);});committed=true;}catch{if(moved)File.Move(staged,path);throw;}
     result.Deleted++;if(moved)FileRetry.Run(()=>{File.Delete(staged);return true;},CancellationToken.None,null);
    }catch(OperationCanceledException){throw;}catch(Exception e){result.Errors.Add(FileRetry.Detail(committed?staged:path,e));}
   }}finally{
    if(Directory.Exists(staging)&&!Directory.EnumerateFileSystemEntries(staging).Any())Directory.Delete(staging);
    // Preserve deletions in the portable snapshot even when a later file is canceled.
    if(result.Deleted>0)try{Checkpoint(CancellationToken.None);}catch(Exception e){result.Errors.Add("Deleted records are saved locally; archive snapshot could not be updated: "+e.Message);}
   }
   if(progress!=null)progress(new ProgressInfo{Done=frames.Count,Total=frames.Count,Stage="Deleting files",Text=result.Deleted+" files deleted"});return result;
  }
 }
}
