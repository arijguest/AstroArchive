using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class EditedImportCandidate {
  public string Path{get;set;} public string RelativePath{get;set;} public long Bytes{get;set;} public string Hash{get;set;} public FileStamp Stamp{get;set;} public EditedMetadata Metadata{get;set;} public bool Include{get;set;} public bool AlreadyImported{get;set;} public string Problem{get;set;}
  public string Status{get{return !string.IsNullOrEmpty(Problem)?"Needs review":AlreadyImported?"Already in Edited":"New";}}
  public string Filename{get{return RelativePath;}}public string ImageClass{get{return Metadata==null?"Unknown":Metadata.ImageClass;}} public string Object{get{return Metadata==null?"Unknown":Metadata.ObjectLabel;}}public string TotalExposure{get{return Metadata==null?"Unknown":Metadata.TotalExposureText;}}
 }
 public sealed class EditedImportPlan {
  public string Folder;public List<EditedImportCandidate> Images=new List<EditedImportCandidate>();public List<string> Errors=new List<string>();public List<string> SkippedFolders=new List<string>();public int SkippedArchived;
 }
 public sealed partial class Repository {
  HashSet<string> EditedContentHashes(CancellationToken ct,Action<ProgressInfo> progress,List<string> errors){
   var hashes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);List<string> projectErrors;var projects=EditedProjects(out projectErrors);errors.AddRange(projectErrors.Select(e=>"Existing Edited project: "+e));
   foreach(var project in projects){var directories=new Stack<string>();directories.Push(EditedProjectFolder(project));while(directories.Count>0){ct.ThrowIfCancellationRequested();string directory=directories.Pop();
    try{CheckManagedPath(Path.Combine(directory,"edited-project.json"),Root);foreach(string child in Directory.EnumerateDirectories(directory))if(FileStamp.CanTraverse(new DirectoryInfo(child)))directories.Push(child);
     foreach(string path in Directory.EnumerateFiles(directory).Where(Util.IsImageAsset)){ct.ThrowIfCancellationRequested();try{CheckManagedPath(path,Root);if(progress!=null)progress(new ProgressInfo{Stage="Checking existing edited images",Text=Path.GetFileName(path)});var before=FileStamp.Read(path);string hash=Util.Hash(path,ct);if(!before.ContentSame(FileStamp.Read(path)))throw new IOException("Image changed during duplicate review.");hashes.Add(hash);}catch(IOException e){errors.Add(path+": "+e.Message);}catch(UnauthorizedAccessException e){errors.Add(path+": "+e.Message);}}
    }catch(IOException e){errors.Add(directory+": "+e.Message);}catch(UnauthorizedAccessException e){errors.Add(directory+": "+e.Message);}
   }}return hashes;
  }
  public EditedImportPlan ScanEditedFolder(string folder,bool recursive,CancellationToken ct,Action<ProgressInfo> progress){
   folder=System.IO.Path.GetFullPath(folder);if(!Directory.Exists(folder))throw new DirectoryNotFoundException(folder);if(Util.Within(folder,Root))throw new IOException("Choose a folder outside the repository.");
   var archived=new HashSet<string>(All().Select(f=>f.Hash).Where(h=>!string.IsNullOrEmpty(h)),StringComparer.OrdinalIgnoreCase);
   var plan=new EditedImportPlan{Folder=folder};var edited=EditedContentHashes(ct,progress,plan.Errors);var directories=new Stack<string>();directories.Push(folder);
   while(directories.Count>0){ct.ThrowIfCancellationRequested();string directory=directories.Pop();
    try{if(!FileStamp.CanTraverse(new DirectoryInfo(directory))){plan.Errors.Add("Linked folder skipped: "+directory);continue;}
     if(recursive)foreach(string child in Directory.EnumerateDirectories(directory)){if(Util.Within(child,Root)||System.IO.Path.GetFileName(child).Equals(".astroarchive",StringComparison.OrdinalIgnoreCase))plan.SkippedFolders.Add(child);else directories.Push(child);}
     foreach(string path in Directory.EnumerateFiles(directory).Where(Util.IsImageAsset)){ct.ThrowIfCancellationRequested();var row=new EditedImportCandidate{Path=path,RelativePath=path.Substring(folder.TrimEnd('\\','/').Length+1)};
      try{if(((File.GetAttributes(path))&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked image skipped.");row.Stamp=FileStamp.Read(path);row.Bytes=row.Stamp.Size;
       if(progress!=null)progress(new ProgressInfo{Stage="Reviewing edited images",Text=row.RelativePath});row.Hash=Util.Hash(path,ct);
       if(!row.Stamp.ContentSame(FileStamp.Read(path)))throw new IOException("Image changed during review. Scan again.");
       if(archived.Contains(row.Hash)){plan.SkippedArchived++;continue;}
       var header=Assets.Inspect(path).Header;row.Metadata=EditedMetadata.Read(row.RelativePath,header);
       if(!row.Stamp.ContentSame(FileStamp.Read(path)))throw new IOException("Image changed during review. Scan again.");row.AlreadyImported=edited.Contains(row.Hash);row.Include=!row.AlreadyImported;
      }catch(OperationCanceledException){throw;}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is NotSupportedException||e is ArgumentException||e is OverflowException))throw;row.Problem=e.Message;row.Include=false;}plan.Images.Add(row);
     }
    }catch(OperationCanceledException){throw;}catch(IOException e){plan.Errors.Add(directory+": "+e.Message);}catch(UnauthorizedAccessException e){plan.Errors.Add(directory+": "+e.Message);}
   }
   foreach(var gif in plan.Images.Where(i=>i.Metadata!=null&&MediaFiles.Gif(i.Path))){string match=MediaFiles.MatchingImage(gif.RelativePath,plan.Images.Where(i=>i.Metadata!=null).Select(i=>i.RelativePath));if(match!=null){var still=plan.Images.Single(i=>i.RelativePath==match);gif.Metadata=EditedMetadata.Read(gif.RelativePath,Assets.Inspect(gif.Path).Header,still.Metadata);gif.Metadata.Evidence+="\nRelated edited image: "+still.RelativePath;}}
   return plan;
  }
  public EditedProject ImportEditedFolder(EditedImportPlan plan,string name,CancellationToken ct,Action<ProgressInfo> progress){
   ct.ThrowIfCancellationRequested();var archived=new HashSet<string>(All().Select(f=>f.Hash).Where(h=>!string.IsNullOrEmpty(h)),StringComparer.OrdinalIgnoreCase);var rows=plan.Images.Where(i=>i.Include&&!archived.Contains(i.Hash)).ToList();if(rows.Count==0)throw new InvalidOperationException("Select new images to import; images already archived are skipped.");var project=NewEditedProject(name,"","");Directory.CreateDirectory(EditedProjectFolder(project));
   try{foreach(var row in rows){ct.ThrowIfCancellationRequested();if(!string.IsNullOrEmpty(row.Problem)||row.Stamp==null||string.IsNullOrEmpty(row.Hash)||!row.Stamp.ContentSame(FileStamp.Read(row.Path)))throw new IOException("Image changed or is unreadable: "+row.RelativePath+". Scan again.");
     AddEditedFile(project,row.Path,System.IO.Path.GetFileName(row.Path),row.Hash,null,ct,progress,row.RelativePath,row.Metadata);
    }return project;
   }catch{RemoveNewEditedProject(project);throw;}
  }
 }
}
