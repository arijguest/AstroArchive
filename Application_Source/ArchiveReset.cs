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
   if(!Util.Within(full,boundary)||full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Equals(boundary,StringComparison.OrdinalIgnoreCase))throw new IOException("Refusing to delete outside the archive.");
   string parent=Path.GetDirectoryName(full);while(parent!=null){if(Directory.Exists(parent)&&!FileStamp.CanTraverse(new DirectoryInfo(parent)))throw new IOException("Refusing deletion through a linked directory: "+parent);if(parent.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Equals(boundary,StringComparison.OrdinalIgnoreCase))return;parent=Path.GetDirectoryName(parent);}
   throw new IOException("Refusing to delete outside the archive.");
  }
  public List<string> ResetArchive(CancellationToken ct,Action<ProgressInfo> progress){var errors=new List<string>();var frames=All();int i=0;foreach(var f in frames){ct.ThrowIfCancellationRequested();progress(new ProgressInfo{Done=i++,Total=frames.Count,Stage="Deleting archive",Text="Deleting "+f.OriginalName});try{string path=FilePath(f);if(File.Exists(path)){CheckManagedPath(path,Root);FileRetry.Run(()=>{File.Delete(path);return true;},ct,null);}db.Exec("DELETE FROM files WHERE hash=?",f.Hash);}catch(OperationCanceledException){throw;}catch(Exception e){errors.Add(FileRetry.Detail(f.RelativePath,e));}}
   if(errors.Count==0){db.Transaction(()=>{db.Exec("DELETE FROM source_manifest");db.Exec("DELETE FROM sessions");});foreach(string folder in new[]{"session-metadata","reports","staging"}){string path=Path.Combine(Meta,folder);if(Directory.Exists(path))RemoveOwnedTree(path,Root,errors,ct);}foreach(string folder in new[]{"Targets","Calibration"}){string path=Path.Combine(Root,folder);if(Directory.Exists(path))RemoveEmptyTree(path,Root,ct);}}
   if(errors.Count==0){LastReport="";TryRemove(Path.Combine(Path.GetDirectoryName(WorkingIndex),"last-import.txt"));}Checkpoint(ct);return errors;
  }
  static void RemoveOwnedTree(string folder,string root,List<string> errors,CancellationToken ct){CheckManagedPath(folder,root);if(!FileStamp.CanTraverse(new DirectoryInfo(folder))){errors.Add("Linked metadata directory retained: "+folder);return;}foreach(string file in Directory.EnumerateFiles(folder)){ct.ThrowIfCancellationRequested();try{CheckManagedPath(file,root);File.Delete(file);}catch(Exception e){errors.Add(FileRetry.Detail(file,e));}}foreach(string child in Directory.EnumerateDirectories(folder))RemoveOwnedTree(child,root,errors,ct);if(!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);}
  static void RemoveEmptyTree(string folder,string root,CancellationToken ct){ct.ThrowIfCancellationRequested();CheckManagedPath(folder,root);if(!FileStamp.CanTraverse(new DirectoryInfo(folder)))return;foreach(string child in Directory.EnumerateDirectories(folder))RemoveEmptyTree(child,root,ct);if(!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);}
 }
}
