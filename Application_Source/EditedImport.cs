using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class EditedImportCandidate {
  public string Path{get;set;} public string RelativePath{get;set;} public long Bytes{get;set;} public string Hash{get;set;} public FileStamp Stamp{get;set;} public EditedMetadata Metadata{get;set;} public bool Include{get;set;} public string Problem{get;set;}
  public string Filename{get{return RelativePath;}}public string ImageClass{get{return Metadata==null?"Unknown":Metadata.ImageClass;}} public string Object{get{return Metadata==null?"Unknown":Metadata.ObjectLabel;}}public string TotalExposure{get{return Metadata==null?"Unknown":Metadata.TotalExposureText;}}
  public string FileType{get{return MediaFiles.FileType(Path??RelativePath);}}
  public string DuplicateReason{get;set;}public string NameConflict{get;set;}
  public bool CanInclude{get{return string.IsNullOrEmpty(Problem)&&string.IsNullOrEmpty(DuplicateReason);}}
  public string Status{get{return !string.IsNullOrEmpty(Problem)?"Unreadable":!string.IsNullOrEmpty(DuplicateReason)?"Duplicate":!string.IsNullOrEmpty(NameConflict)?"New version (same name)":"Ready";}}
  public string ReviewNote{get{return Problem??DuplicateReason??(!string.IsNullOrEmpty(NameConflict)?"Different content from "+NameConflict+". Import keeps both versions.":"");}}
 }
 public sealed class EditedImportPlan {
  public string Folder;public List<EditedImportCandidate> Images=new List<EditedImportCandidate>();public List<string> Errors=new List<string>();public List<string> SkippedFolders=new List<string>();public int SkippedArchived;
 }
 public sealed partial class Repository {
  public EditedImportPlan ScanEditedFolder(string folder,bool recursive,CancellationToken ct,Action<ProgressInfo> progress){
   folder=System.IO.Path.GetFullPath(folder);if(!Directory.Exists(folder))throw new DirectoryNotFoundException(folder);if(Util.Within(folder,Root))throw new IOException("Choose a folder outside the repository.");
   var captures=All();var matcher=new EditedTargetMatcher(captures.Select(f=>f.Target));var archived=new HashSet<string>(captures.Select(f=>f.Hash).Where(h=>!string.IsNullOrEmpty(h)),StringComparer.OrdinalIgnoreCase);
   var plan=new EditedImportPlan{Folder=folder};var duplicates=EditedDuplicates(ct,progress,plan.Errors);var directories=new Stack<string>();directories.Push(folder);
   while(directories.Count>0){ct.ThrowIfCancellationRequested();string directory=directories.Pop();
    try{if(!FileStamp.CanTraverse(new DirectoryInfo(directory))){plan.Errors.Add("Linked folder skipped: "+directory);continue;}
     if(recursive)foreach(string child in Directory.EnumerateDirectories(directory)){if(Util.Within(child,Root)||System.IO.Path.GetFileName(child).Equals(".astroarchive",StringComparison.OrdinalIgnoreCase))plan.SkippedFolders.Add(child);else directories.Push(child);}
     foreach(string path in Directory.EnumerateFiles(directory).Where(Util.IsImageAsset)){ct.ThrowIfCancellationRequested();var row=new EditedImportCandidate{Path=path,RelativePath=path.Substring(folder.TrimEnd('\\','/').Length+1)};
      try{if(((File.GetAttributes(path))&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked image skipped.");row.Stamp=FileStamp.Read(path);row.Bytes=row.Stamp.Size;
       if(progress!=null)progress(new ProgressInfo{Stage="Reviewing edited images",Text=row.RelativePath});row.Hash=Util.Hash(path,ct);
       if(!row.Stamp.ContentSame(FileStamp.Read(path)))throw new IOException("Image changed during review. Scan again.");
       if(archived.Contains(row.Hash)){plan.SkippedArchived++;continue;}
       var header=Assets.Inspect(path).Header;row.Metadata=EditedMetadata.Read(row.RelativePath,header,null,matcher);
       if(!row.Stamp.ContentSame(FileStamp.Read(path)))throw new IOException("Image changed during review. Scan again.");
       string duplicate=duplicates.Duplicate(row.Hash);if(duplicate!=null){row.DuplicateReason="Same content as "+duplicate;duplicates.Add(System.IO.Path.GetFileName(path),row.Hash,duplicate);}
       else{row.NameConflict=duplicates.NameConflict(System.IO.Path.GetFileName(path),row.Hash);duplicates.Add(System.IO.Path.GetFileName(path),row.Hash,row.RelativePath);row.Include=true;}
      }catch(OperationCanceledException){throw;}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is NotSupportedException||e is ArgumentException||e is OverflowException))throw;row.Problem=e.Message;row.Include=false;}plan.Images.Add(row);
     }
    }catch(OperationCanceledException){throw;}catch(IOException e){plan.Errors.Add(directory+": "+e.Message);}catch(UnauthorizedAccessException e){plan.Errors.Add(directory+": "+e.Message);}
   }
   foreach(var row in plan.Images.Where(i=>i.Include))row.NameConflict=duplicates.NameConflict(System.IO.Path.GetFileName(row.Path),row.Hash);
   foreach(var gif in plan.Images.Where(i=>i.Include&&MediaFiles.Gif(i.Path))){string match=MediaFiles.MatchingImage(gif.RelativePath,plan.Images.Where(i=>i.Include).Select(i=>i.RelativePath));if(match!=null){var still=plan.Images.Single(i=>i.RelativePath==match);gif.Metadata=EditedMetadata.Read(gif.RelativePath,Assets.Inspect(gif.Path).Header,still.Metadata,matcher);gif.Metadata.Evidence+="\nRelated edited image: "+still.RelativePath;}}
   return plan;
  }
  public EditedProject ImportEditedFolder(EditedImportPlan plan,string name,CancellationToken ct,Action<ProgressInfo> progress,EditedImportResult result=null){
   ct.ThrowIfCancellationRequested();result=result??new EditedImportResult();var archived=new HashSet<string>(All().Select(f=>f.Hash).Where(h=>!string.IsNullOrEmpty(h)),StringComparer.OrdinalIgnoreCase);var duplicates=EditedDuplicates(ct,progress,result.Warnings);var rows=new List<EditedImportCandidate>();
   result.SkippedArchived=plan.SkippedArchived;result.SkippedDuplicates=plan.Images.Count(i=>!i.Include&&!string.IsNullOrEmpty(i.DuplicateReason));
   foreach(var row in plan.Images.Where(i=>i.Include)){
    if(!string.IsNullOrEmpty(row.Problem)||row.Stamp==null||string.IsNullOrEmpty(row.Hash)||!row.Stamp.ContentSame(FileStamp.Read(row.Path))||!Util.Hash(row.Path,ct).Equals(row.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException("Image changed or is unreadable: "+row.RelativePath+". Scan again.");
    if(archived.Contains(row.Hash)){result.SkippedArchived++;continue;}
    if(duplicates.Duplicate(row.Hash)!=null){result.SkippedDuplicates++;continue;}
    rows.Add(row);if(!string.IsNullOrEmpty(row.NameConflict)||!string.IsNullOrEmpty(duplicates.NameConflict(System.IO.Path.GetFileName(row.Path),row.Hash)))result.NameConflicts++;
    duplicates.Add(System.IO.Path.GetFileName(row.Path),row.Hash,row.RelativePath);
   }
   if(rows.Count==0)return null;var project=NewEditedProject(name,"","");Directory.CreateDirectory(EditedProjectFolder(project));
   try{foreach(var row in rows){ct.ThrowIfCancellationRequested();if(!string.IsNullOrEmpty(row.Problem)||row.Stamp==null||string.IsNullOrEmpty(row.Hash)||!row.Stamp.ContentSame(FileStamp.Read(row.Path)))throw new IOException("Image changed or is unreadable: "+row.RelativePath+". Scan again.");
     AddEditedFile(project,row.Path,System.IO.Path.GetFileName(row.Path),row.Hash,null,ct,progress,row.RelativePath,row.Metadata);
     result.Imported++;
    }result.Project=project;return project;
   }catch{RemoveNewEditedProject(project);throw;}
  }
 }
}
