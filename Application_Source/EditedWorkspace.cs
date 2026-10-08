// Portable, mutable editor projects live outside the capture index.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class EditedSource {
  public string RelativePath{get;set;} public string OriginalName{get;set;} public string Hash{get;set;} public string ArchiveHash{get;set;} public EditedMetadata Metadata{get;set;}
 }
 public sealed class EditedProject {
  public int Schema{get;set;} public string Id{get;set;} public string Name{get;set;} public string Processor{get;set;} public string Target{get;set;} public DateTime CreatedUtc{get;set;}
  public Dictionary<string,EditedMetadata> MetadataEdits{get;set;}
  public List<EditedSource> Sources{get;set;} public override string ToString(){return Name;}
 }
 public sealed class EditedImage {
  [System.Web.Script.Serialization.ScriptIgnore]public EditedProject Project{get;set;}
  public string RelatedImage{get;set;} public string Filename{get;set;} public string RelativePath{get;set;} public string Kind{get;set;} public long Bytes{get;set;} public DateTime Modified{get;set;} public string Source{get;set;} public EditedMetadata Metadata{get;set;} public string MetadataProblem{get;set;}
 }
 public sealed partial class Repository {
  public string EditedFolder{get{return Path.Combine(Meta,"edited");}}
  public string EditedProjectFolder(EditedProject project){Guid id;if(project==null||!Guid.TryParseExact(project.Id,"N",out id))throw new InvalidDataException("Invalid edited project identity.");string path=Path.Combine(EditedFolder,project.Id);CheckManagedPath(path,Root);return path;}
  public List<EditedProject> EditedProjects(out List<string> errors){
   errors=new List<string>();var projects=new List<EditedProject>();CheckManagedPath(Path.Combine(EditedFolder,"edited-project.json"),Root);if(!Directory.Exists(EditedFolder))return projects;
   foreach(string directory in Directory.EnumerateDirectories(EditedFolder)){
    Guid id;if(!Guid.TryParseExact(Path.GetFileName(directory),"N",out id))continue;
    try{CheckManagedPath(Path.Combine(directory,"edited-project.json"),Root);var project=Util.Deserialize<EditedProject>(File.ReadAllText(Path.Combine(directory,"edited-project.json")));
     if(project==null||project.Schema!=1||project.Id!=Path.GetFileName(directory)||string.IsNullOrWhiteSpace(project.Name)||project.Sources==null)throw new InvalidDataException("Invalid edited project record.");
     foreach(var source in project.Sources)EditedPath(project,source.RelativePath);if(project.MetadataEdits!=null)foreach(string key in project.MetadataEdits.Keys)EditedPath(project,key);projects.Add(project);
    }catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException))throw;errors.Add(Path.GetFileName(directory)+": "+e.Message);}
   }return projects.OrderByDescending(p=>p.CreatedUtc).ToList();
  }
  public string EditedPath(EditedProject project,string relative){
   string folder=EditedProjectFolder(project);if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))throw new InvalidDataException("Invalid edited image path.");
   string path=Path.GetFullPath(Path.Combine(folder,relative));if(!Util.Within(path,folder)||path.Equals(folder,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Edited image path escapes its project.");CheckManagedPath(path,Root);return path;
  }
  void SaveEditedProject(EditedProject project,CancellationToken ct=default(CancellationToken)){string folder=EditedProjectFolder(project),path=Path.Combine(folder,"edited-project.json"),temp=path+"."+Guid.NewGuid().ToString("N")+".partial";
   CheckManagedPath(path,Root);ct.ThrowIfCancellationRequested();try{File.WriteAllText(temp,Util.Serialize(project));CommitTemporary(temp,path,ct);}finally{TryRemove(temp);}
  }
  EditedProject NewEditedProject(string name,string processor,string target){
   if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Enter a project name.");CheckManagedPath(Path.Combine(EditedFolder,"edited-project.json"),Root);Directory.CreateDirectory(EditedFolder);
   return new EditedProject{Schema=1,Id=Guid.NewGuid().ToString("N"),Name=name.Trim(),Processor=processor??"",Target=target??"",CreatedUtc=DateTime.UtcNow,Sources=new List<EditedSource>()};
  }
  public EditedProject CreateEditedWorkingCopy(Frame capture,string name,string processor,CancellationToken ct,Action<ProgressInfo> progress){
   return CreateEditedWorkingCopies(new[]{capture},name,processor,ct,progress);
  }
  public EditedProject CreateEditedWorkingCopies(IEnumerable<Frame> captures,string name,string processor,CancellationToken ct,Action<ProgressInfo> progress){
   ct.ThrowIfCancellationRequested();var selected=captures.ToList();if(selected.Count==0)throw new ArgumentException("Choose archived images.");var indexed=new List<Frame>();
   foreach(var capture in selected){var stored=capture==null?null:Find(capture.Hash);if(stored==null)throw new IOException("The archived capture is unavailable.");ValidateCapture(stored,ct);indexed.Add(stored);}
   var project=NewEditedProject(name,processor,indexed.Select(f=>f.TargetLabel).Distinct().Count()==1?indexed[0].TargetLabel:"Multiple objects");Directory.CreateDirectory(EditedProjectFolder(project));
   try{foreach(var capture in indexed){ct.ThrowIfCancellationRequested();var metadata=EditedMetadata.Read(capture.OriginalName,Assets.Inspect(FilePath(capture)).Header);if(metadata.ImageClass!="Meteor"&&string.IsNullOrEmpty(metadata.Object)&&!Catalog.IsAmbiguous(capture.Target))metadata.Object=capture.Target;if(string.IsNullOrEmpty(metadata.Filters)&&capture.Filter!="Unknown")metadata.Filters=capture.Filter;
    AddEditedFile(project,FilePath(capture),capture.OriginalName,capture.Hash,capture.Hash,ct,progress,null,metadata);}return project;}
   catch{RemoveNewEditedProject(project);throw;}
  }
  EditedProject ReadEditedProject(EditedProject selected){
   string id=selected.Id,path=Path.Combine(EditedProjectFolder(selected),"edited-project.json");CheckManagedPath(path,Root);var project=Util.Deserialize<EditedProject>(File.ReadAllText(path));
   if(project==null||project.Id!=id||project.Schema!=1||string.IsNullOrWhiteSpace(project.Name)||project.Sources==null)throw new InvalidDataException("Invalid edited project record.");foreach(var record in project.Sources){if(record==null)throw new InvalidDataException("Invalid edited image record.");EditedPath(project,record.RelativePath);}if(project.MetadataEdits!=null)foreach(string key in project.MetadataEdits.Keys)EditedPath(project,key);return project;
  }
  public EditedProject AddEditedImages(IEnumerable<string> files,EditedProject project,string name,CancellationToken ct,Action<ProgressInfo> progress){
   ct.ThrowIfCancellationRequested();var inputs=files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(inputs.Count==0)throw new ArgumentException("Choose images to add.");
   bool created=project==null;if(created){project=NewEditedProject(name,"","");Directory.CreateDirectory(EditedProjectFolder(project));}
   else project=ReadEditedProject(project);
   try{foreach(string file in inputs){ct.ThrowIfCancellationRequested();if(!Util.IsImageAsset(file))throw new NotSupportedException("Choose a supported image: "+Path.GetFileName(file));
     var metadata=EditedMetadata.Read(Path.GetFileName(file),Assets.Inspect(file).Header);string hash=Util.Hash(file,ct);AddEditedFile(project,file,Path.GetFileName(file),hash,null,ct,progress,null,metadata);
    }return project;
   }catch{if(created)RemoveNewEditedProject(project);throw;}
  }
  void AddEditedFile(EditedProject project,string source,string name,string hash,string archiveHash,CancellationToken ct,Action<ProgressInfo> progress,string importedRelative=null,EditedMetadata metadata=null){
   string basename=Util.SafeFile(name),relative=Path.Combine("images",importedRelative??basename);EditedPath(project,relative);int suffix=1;string directory=Path.GetDirectoryName(relative);
   while(File.Exists(EditedPath(project,relative))||Directory.Exists(EditedPath(project,relative))||project.Sources.Any(record=>record.RelativePath.Equals(relative,StringComparison.OrdinalIgnoreCase)))relative=Path.Combine(directory,Path.GetFileNameWithoutExtension(basename)+"_"+(suffix++)+Path.GetExtension(basename));
   string destination=EditedPath(project,relative),temp=destination+"."+Guid.NewGuid().ToString("N")+".partial";Directory.CreateDirectory(Path.GetDirectoryName(destination));
   var metrics=new PipelineMetrics(progress);metrics.Stage="Creating edited working copy";metrics.Current=name;metrics.Pulse(true);
   bool published=false;try{CopyVerified(source,temp,hash,ct,metrics);ct.ThrowIfCancellationRequested();File.Move(temp,destination);published=true;
    var record=new EditedSource{RelativePath=relative,OriginalName=name,Hash=hash,ArchiveHash=archiveHash,Metadata=metadata};project.Sources.Add(record);
    try{SaveEditedProject(project);}catch{project.Sources.Remove(record);throw;}published=false;
   }finally{TryRemove(temp);if(published)TryRemove(destination);}
  }
  void RemoveNewEditedProject(EditedProject project){string path=EditedProjectFolder(project);var errors=new List<string>();if(Directory.Exists(path))RemoveOwnedTree(path,Root,errors,CancellationToken.None);}
  public List<EditedImage> EditedImages(EditedProject project){
   var images=new List<EditedImage>();string folder=EditedProjectFolder(project);if(!Directory.Exists(folder))return images;var directories=new Stack<string>();directories.Push(folder);
   while(directories.Count>0){string directory=directories.Pop();CheckManagedPath(Path.Combine(directory,"edited-project.json"),Root);
    foreach(string child in Directory.EnumerateDirectories(directory))if(FileStamp.CanTraverse(new DirectoryInfo(child)))directories.Push(child);
    foreach(string path in Directory.EnumerateFiles(directory).Where(Util.IsImageAsset)){CheckManagedPath(path,Root);string relative=path.Substring(folder.Length+1);var source=project.Sources.FirstOrDefault(s=>s.RelativePath.Equals(relative,StringComparison.OrdinalIgnoreCase));var file=new FileInfo(path);
     var original=source==null?(project.Sources.Count==1?project.Sources[0].Metadata:null):source.Metadata;EditedMetadata metadata;string problem=null;
     try{metadata=EditedMetadata.Read(relative,Assets.Inspect(path).Header,original);}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is NotSupportedException||e is ArgumentException||e is OverflowException))throw;metadata=EditedMetadata.Read(relative,null,original);problem=e.Message;}
     images.Add(new EditedImage{Filename=file.Name,RelativePath=relative,Bytes=file.Length,Modified=file.LastWriteTime,Kind=source==null?"Editor output":string.IsNullOrEmpty(source.ArchiveHash)?"Added image":"Working copy",Source=source==null?"":source.OriginalName,Metadata=UserEditedMetadata(project,relative,metadata),MetadataProblem=problem});
    }
   }
   foreach(var gif in images.Where(i=>MediaFiles.Gif(i.RelativePath))){string match=MediaFiles.MatchingImage(gif.RelativePath,images.Where(i=>i.MetadataProblem==null).Select(i=>i.RelativePath));if(match==null)continue;var still=images.Single(i=>i.RelativePath==match);gif.RelatedImage=match;
    try{gif.Metadata=EditedMetadata.Read(gif.RelativePath,Assets.Inspect(EditedPath(project,gif.RelativePath)).Header,still.Metadata);gif.Metadata.Evidence+="\nRelated edited image: "+still.Filename;gif.Metadata=UserEditedMetadata(project,gif.RelativePath,gif.Metadata);}catch(IOException){}
   }
   return images.OrderByDescending(i=>i.Modified).ThenBy(i=>i.Filename).ToList();
  }
 }
}
