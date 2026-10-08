using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class EditedImportResult {
  public EditedProject Project;public int Imported,SkippedDuplicates,SkippedArchived,NameConflicts;
  public List<string> Warnings=new List<string>();
  public string Summary{get{return "Edited: "+Imported+" imported; "+SkippedDuplicates+" duplicates skipped; "+SkippedArchived+" already archived skipped; "+NameConflicts+" same-name versions kept.";}}
 }
 sealed class EditedDuplicateIndex {
  readonly Dictionary<string,string> hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,List<KeyValuePair<string,string>>> names=new Dictionary<string,List<KeyValuePair<string,string>>>(StringComparer.OrdinalIgnoreCase);
  public string Duplicate(string hash){string location;return hashes.TryGetValue(hash,out location)?location:null;}
  public string NameConflict(string name,string hash){List<KeyValuePair<string,string>> matches;if(!names.TryGetValue(Path.GetFileName(name),out matches))return null;return string.Join("; ",matches.Where(m=>!m.Key.Equals(hash,StringComparison.OrdinalIgnoreCase)).Select(m=>m.Value).Distinct());}
  public void Add(string name,string hash,string location){if(!hashes.ContainsKey(hash))hashes.Add(hash,location);name=Path.GetFileName(name);List<KeyValuePair<string,string>> matches;if(!names.TryGetValue(name,out matches)){matches=new List<KeyValuePair<string,string>>();names.Add(name,matches);}matches.Add(new KeyValuePair<string,string>(hash,location));}
 }
 public sealed partial class Repository {
  EditedDuplicateIndex EditedDuplicates(CancellationToken ct,Action<ProgressInfo> progress,List<string> warnings){
   var index=new EditedDuplicateIndex();List<string> errors;var projects=EditedProjects(out errors);warnings.AddRange(errors);
   foreach(var project in projects){
    var folders=new Stack<string>();folders.Push(EditedProjectFolder(project));
    while(folders.Count>0){ct.ThrowIfCancellationRequested();string folder=folders.Pop();
     try{foreach(string child in Directory.EnumerateDirectories(folder))if(FileStamp.CanTraverse(new DirectoryInfo(child))){CheckManagedPath(Path.Combine(child,"edited-project.json"),Root);folders.Push(child);}
      foreach(string path in Directory.EnumerateFiles(folder).Where(Util.IsImageAsset)){
       ct.ThrowIfCancellationRequested();try{CheckManagedPath(path,Root);var before=FileStamp.Read(path);if(progress!=null)progress(new ProgressInfo{Stage="Checking Edited duplicates",Text=project.Name+" / "+Path.GetFileName(path)});
        string hash=Util.Hash(path,ct);if(!before.ContentSame(FileStamp.Read(path)))throw new IOException("Image changed during duplicate check.");
        string relative=path.Substring(EditedProjectFolder(project).Length+1),location=project.Name+" / "+relative;
        index.Add(Path.GetFileName(path),hash,location);var source=project.Sources.FirstOrDefault(s=>s.RelativePath.Equals(relative,StringComparison.OrdinalIgnoreCase));
        if(source!=null&&!string.IsNullOrEmpty(source.OriginalName))index.Add(source.OriginalName,hash,location);
       }catch(OperationCanceledException){throw;}catch(IOException e){warnings.Add(path+": "+e.Message);}catch(UnauthorizedAccessException e){warnings.Add(path+": "+e.Message);}
      }
     }catch(IOException e){warnings.Add(folder+": "+e.Message);}catch(UnauthorizedAccessException e){warnings.Add(folder+": "+e.Message);}
    }
   }
   return index;
  }
 }
}
