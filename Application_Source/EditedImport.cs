using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class EditedImportCandidate {
  public string Path{get;set;} public string RelativePath{get;set;} public long Bytes{get;set;} public string Hash{get;set;} public FileStamp Stamp{get;set;} public EditedMetadata Metadata{get;set;} public bool Include{get;set;} public string Problem{get;set;}
  public string Filename{get{return RelativePath;}}public string ImageClass{get{return Metadata==null?"Unknown":Metadata.ImageClass;}} public string Object{get{return Metadata==null?"Unknown":Metadata.ObjectLabel;}}public string TotalExposure{get{return Metadata==null?"Unknown":Metadata.TotalExposureText;}}
 }
 public sealed class EditedImportPlan {
  public string Folder;public List<EditedImportCandidate> Images=new List<EditedImportCandidate>();public List<string> Errors=new List<string>();
 }
 public sealed partial class Repository {
  public EditedImportPlan ScanEditedFolder(string folder,bool recursive,CancellationToken ct,Action<ProgressInfo> progress){
   folder=System.IO.Path.GetFullPath(folder);if(!Directory.Exists(folder))throw new DirectoryNotFoundException(folder);if(Util.Within(folder,Root)||Util.Within(Root,folder))throw new IOException("Choose a folder outside the repository.");
   var plan=new EditedImportPlan{Folder=folder};var directories=new Stack<string>();directories.Push(folder);
   while(directories.Count>0){ct.ThrowIfCancellationRequested();string directory=directories.Pop();
    try{if(!FileStamp.CanTraverse(new DirectoryInfo(directory))){plan.Errors.Add("Linked folder skipped: "+directory);continue;}
     if(recursive)foreach(string child in Directory.EnumerateDirectories(directory))if(System.IO.Path.GetFileName(child)!=".astroarchive")directories.Push(child);
     foreach(string path in Directory.EnumerateFiles(directory).Where(Util.IsImageAsset)){ct.ThrowIfCancellationRequested();var row=new EditedImportCandidate{Path=path,RelativePath=path.Substring(folder.TrimEnd('\\','/').Length+1)};plan.Images.Add(row);
      try{if(((File.GetAttributes(path))&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked image skipped.");row.Stamp=FileStamp.Read(path);row.Bytes=row.Stamp.Size;
       if(progress!=null)progress(new ProgressInfo{Stage="Reviewing edited images",Text=row.RelativePath});var header=Assets.Inspect(path).Header;row.Metadata=EditedMetadata.Read(row.RelativePath,header);row.Hash=Util.Hash(path,ct);
       if(!row.Stamp.ContentSame(FileStamp.Read(path)))throw new IOException("Image changed during review. Scan again.");row.Include=true;
      }catch(OperationCanceledException){throw;}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is NotSupportedException||e is ArgumentException||e is OverflowException))throw;row.Problem=e.Message;row.Include=false;}
     }
    }catch(OperationCanceledException){throw;}catch(IOException e){plan.Errors.Add(directory+": "+e.Message);}catch(UnauthorizedAccessException e){plan.Errors.Add(directory+": "+e.Message);}
   }return plan;
  }
  public EditedProject ImportEditedFolder(EditedImportPlan plan,string name,CancellationToken ct,Action<ProgressInfo> progress){
   ct.ThrowIfCancellationRequested();var rows=plan.Images.Where(i=>i.Include).ToList();if(rows.Count==0)throw new InvalidOperationException("Select images to import.");var project=NewEditedProject(name,"","");Directory.CreateDirectory(EditedProjectFolder(project));
   try{foreach(var row in rows){ct.ThrowIfCancellationRequested();if(!string.IsNullOrEmpty(row.Problem)||row.Stamp==null||string.IsNullOrEmpty(row.Hash)||!row.Stamp.ContentSame(FileStamp.Read(row.Path)))throw new IOException("Image changed or is unreadable: "+row.RelativePath+". Scan again.");
     AddEditedFile(project,row.Path,System.IO.Path.GetFileName(row.Path),row.Hash,null,ct,progress,row.RelativePath,row.Metadata);
    }return project;
   }catch{RemoveNewEditedProject(project);throw;}
  }
 }
}
