// .NET Framework 4.8. Reset only indexed archive files and owned metadata.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed partial class Repository {
  public static void CheckManagedPath(string path,string root){
   string full=Path.GetFullPath(path),boundary=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   if(!Util.Within(full,root)||full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Equals(boundary,StringComparison.OrdinalIgnoreCase))throw new IOException("Path is outside the archive.");
   // Inspect the leaf as well as its parents, including dangling links. Cloud
   // placeholders remain supported; name-surrogate reparse tags redirect paths.
   for(string item=full;item!=null;item=Path.GetDirectoryName(item)){
    FileAttributes attributes=0;bool exists=true;
    try{attributes=File.GetAttributes(item);}catch(FileNotFoundException){exists=false;}catch(DirectoryNotFoundException){exists=false;}
    if(exists){FileSystemInfo entry=(attributes&FileAttributes.Directory)!=0?(FileSystemInfo)new DirectoryInfo(item):new FileInfo(item);if(!FileStamp.CanAccess(entry))throw new IOException("Archive paths cannot follow a linked file or directory: "+item);}
    if(item.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Equals(boundary,StringComparison.OrdinalIgnoreCase))return;
   }
   throw new IOException("Path is outside the archive.");
  }
  public List<string> ResetArchive(CancellationToken ct,Action<ProgressInfo> progress){
   ct.ThrowIfCancellationRequested();var frames=All();var errors=frames.Count>0?DeleteFrames(frames,ct,progress).Errors:new List<string>();
   if(errors.Count==0){db.Transaction(()=>{db.Exec("DELETE FROM source_manifest WHERE status<>?","Deleted");db.Exec("DELETE FROM sessions");});foreach(string folder in new[]{"session-metadata","reports","staging"}){string path=Path.Combine(Meta,folder);if(Directory.Exists(path))RemoveOwnedTree(path,Root,errors,ct);}foreach(string folder in new[]{"Targets","Calibration"}){string path=Path.Combine(Root,folder);if(Directory.Exists(path))RemoveEmptyTree(path,Root,ct);}}
   if(errors.Count==0){LastReport="";TryRemove(Path.Combine(Path.GetDirectoryName(WorkingIndex),"last-import.txt"));}Checkpoint(ct);return errors;
  }
  static void RemoveOwnedTree(string folder,string root,List<string> errors,CancellationToken ct){CheckManagedPath(folder,root);if(!FileStamp.CanTraverse(new DirectoryInfo(folder))){errors.Add("Linked metadata directory retained: "+folder);return;}foreach(string file in Directory.EnumerateFiles(folder)){ct.ThrowIfCancellationRequested();try{CheckManagedPath(file,root);File.Delete(file);}catch(Exception e){errors.Add(FileRetry.Detail(file,e));}}foreach(string child in Directory.EnumerateDirectories(folder))RemoveOwnedTree(child,root,errors,ct);if(!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);}
  void RemoveEmptyTree(string folder,string root,CancellationToken ct){ct.ThrowIfCancellationRequested();CheckManagedPath(folder,root);if(!FileStamp.CanTraverse(new DirectoryInfo(folder)))return;foreach(string child in Directory.EnumerateDirectories(folder))RemoveEmptyTree(child,root,ct);if(!Directory.EnumerateFileSystemEntries(folder).Any())DeleteEmptyCaptureFolder(folder);}
 }
}
