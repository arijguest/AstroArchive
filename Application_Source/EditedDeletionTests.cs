using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void EditedDeletionTests(){
   Test("Edited default order groups RAW formats first and GIFs last despite timestamps",()=>{
    var names=new[]{"new.gif","old.fit.gz","newer.png","old.xisf","camera.NEF","camera.cr2","new.fits","still.jpg","capture.ser","still.tiff","compressed.fz","old.gif"};
    var images=names.Select((name,index)=>new EditedImage{Filename=name,RelativePath=name,Modified=new DateTime(2026,10,9).AddMinutes(-index)}).ToArray();
    var ordered=EditedGallery.Order(images);Check(ordered.Take(7).Select(i=>i.FileType).SequenceEqual(new[]{"CR2","FITS","FITS","FZ","NEF","SER","XISF"}),"RAW types were not grouped at the top");
    Check(ordered[1].Filename=="old.fit.gz"&&ordered.Skip(7).Select(i=>i.FileType).SequenceEqual(new[]{"JPEG","PNG","TIFF","GIF","GIF"})&&ordered[10].Filename=="new.gif","Group ordering or newest-first ordering within a type failed");
    Check(EditedGallery.Order(images.Reverse()).Select(i=>i.Filename).SequenceEqual(ordered.Select(i=>i.Filename)),"Default ordering depends on enumeration order");
   });
   Test("Edited deletion spans projects, deduplicates selection and preserves originals and output metadata",()=>{
    string source=Path.Combine(root,"edited-delete-source"),one=Path.Combine(source,"M31_10x60s_Ha.fit"),two=Path.Combine(source,"M51_20x90s.fit");Write(one,64,48,(x,y)=>2000,new Dictionary<string,string>());Write(two,64,48,(x,y)=>3000,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"edited-delete-repo"))){
     repo.Import(repo.Scan(source,"Scope-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var capture=repo.All().Single(f=>f.OriginalName==Path.GetFileName(one));string original=repo.FilePath(capture),hash=Util.Hash(original,ct);
     var first=repo.CreateEditedWorkingCopy(capture,"Working",null,ct,NoProgress);var second=repo.AddEditedImages(new[]{two},null,"Added",ct,NoProgress);
     var working=repo.EditedImages(first).Single();string output=Path.Combine(repo.EditedProjectFolder(first),"finished.fit");File.Copy(repo.EditedPath(first,working.RelativePath),output);string notes=Path.Combine(repo.EditedProjectFolder(first),"notes.txt");File.WriteAllText(notes,"keep");
     repo.SaveEditedMetadata(first,new[]{working},new EditedMetadata{RA="12:00:00"},ct);var gallery=EditedGallery.Read(repo,new string[0],ct);var selection=gallery.Images.Where(i=>i.Kind!="Editor output").ToList();selection.Add(selection[0]);
     var result=repo.DeleteEditedImages(selection,ct,NoProgress);Check(result.Deleted==2&&result.Errors.Count==0,"Deletion counts or duplicate selection handling failed");
     Check(selection.All(i=>!File.Exists(repo.EditedPath(i.Project,i.RelativePath)))&&File.Exists(one)&&File.Exists(two)&&File.Exists(original)&&Util.Hash(original,ct)==hash&&repo.All().Count==2,"Deletion changed a source original or capture index");
     var remaining=EditedGallery.Read(repo,new string[0],ct);Check(remaining.Images.Count==1&&remaining.Images[0].Filename=="finished.fit"&&remaining.Images[0].Metadata.TotalExposure==600&&File.Exists(notes),"Unselected output, metadata or unrelated file was lost");
     List<string> errors;var projects=repo.EditedProjects(out errors);Check(projects.Count==2&&errors.Count==0&&projects.Single(p=>p.Id==first.Id).Sources.Count==1&&!projects.Single(p=>p.Id==first.Id).MetadataEdits.Any(),"Source provenance or project manifest was lost; deleted metadata patch retained");
     Check(!Directory.Exists(Path.Combine(repo.Meta,"staging"))||!Directory.EnumerateDirectories(Path.Combine(repo.Meta,"staging"),"delete-edited-*").Any(),"Completed deletion left staged files");
     Check(repo.AddEditedImages(new[]{two},second,null,ct,NoProgress)!=null&&repo.EditedImages(second).Count==1,"Deleted bytes were still excluded as duplicates");
    }
   });
   Test("Edited deletion validates the entire selection before deleting any file",()=>{
    string file=Path.Combine(root,"edited-delete-invalid-source","image.fit");Write(file,64,48,(x,y)=>2000,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"edited-delete-invalid-repo"))){var project=repo.AddEditedImages(new[]{file},null,"Validation",ct,NoProgress);var row=EditedGallery.Read(repo,new string[0],ct).Images.Single();string path=repo.EditedPath(project,row.RelativePath),manifest=Path.Combine(repo.EditedProjectFolder(project),"edited-project.json"),before=File.ReadAllText(manifest);
     foreach(string invalid in new[]{"../escape.fit","edited-project.json","images"})Expect(()=>repo.DeleteEditedImages(new[]{row,new EditedImage{Project=project,RelativePath=invalid}},ct,NoProgress),"Invalid deletion path accepted");
     Check(File.Exists(path)&&File.ReadAllText(manifest)==before,"Invalid batch deleted an earlier valid file or changed its manifest");
     using(var stop=new CancellationTokenSource()){stop.Cancel();Expect(()=>repo.DeleteEditedImages(new[]{row},stop.Token,NoProgress),"Canceled deletion ran");}Check(File.Exists(path),"Pre-canceled deletion removed a file");
    }
   });
   Test("Edited deletion rolls back moved files when project metadata cannot be saved",()=>{
    string file=Path.Combine(root,"edited-delete-rollback-source","image.fit");Write(file,64,48,(x,y)=>2000,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"edited-delete-rollback-repo"))){var project=repo.AddEditedImages(new[]{file},null,"Rollback",ct,NoProgress);var row=EditedGallery.Read(repo,new string[0],ct).Images.Single();string path=repo.EditedPath(project,row.RelativePath),hash=Util.Hash(path,ct),manifest=Path.Combine(repo.EditedProjectFolder(project),"edited-project.json"),before=File.ReadAllText(manifest);FileDeletionResult result;
     // Preflight reads the manifest; an obstructing directory then forces the
     // metadata commit to fail after the image moves, on both operating systems.
     string saved=manifest+".fixture-original";
     try{result=repo.DeleteEditedImages(new[]{row},ct,p=>{if(p.Done==0){File.Move(manifest,saved);Directory.CreateDirectory(manifest);}});}
     finally{if(Directory.Exists(manifest))Directory.Delete(manifest);if(File.Exists(saved))File.Move(saved,manifest);}
     Check(result.Deleted==0&&result.Errors.Count==1&&File.Exists(path)&&Util.Hash(path,ct)==hash&&File.ReadAllText(manifest)==before,"Manifest failure lost image bytes or reported success");
     Check(!Directory.EnumerateDirectories(Path.Combine(repo.Meta,"staging"),"delete-edited-*").Any(),"Rollback left staged files");
    }
   });
   Test("Edited batch cancellation retains completed deletions and leaves pending files",()=>{
    string source=Path.Combine(root,"edited-delete-cancel-source"),one=Path.Combine(source,"one.fit"),two=Path.Combine(source,"two.fit");Write(one,64,48,(x,y)=>2000,new Dictionary<string,string>());Write(two,64,48,(x,y)=>3000,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"edited-delete-cancel-repo"))){repo.AddEditedImages(new[]{one,two},null,"Cancel",ct,NoProgress);var rows=EditedGallery.Read(repo,new string[0],ct).Images;using(var stop=new CancellationTokenSource()){Expect(()=>repo.DeleteEditedImages(rows,stop.Token,p=>{if(p.Done==1)stop.Cancel();}),"Mid-batch cancellation was ignored");}
     Check(!File.Exists(repo.EditedPath(rows[0].Project,rows[0].RelativePath))&&File.Exists(repo.EditedPath(rows[1].Project,rows[1].RelativePath))&&EditedGallery.Read(repo,new string[0],ct).Images.Count==1&&File.Exists(one)&&File.Exists(two),"Cancellation removed pending files or restored a committed deletion");
    }
   });
   Test("Edited deletion reports stale selections without deleting other files",()=>{
    string file=Path.Combine(root,"edited-delete-stale-source","image.fit");Write(file,64,48,(x,y)=>2000,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"edited-delete-stale-repo"))){repo.AddEditedImages(new[]{file},null,"Stale",ct,NoProgress);var row=EditedGallery.Read(repo,new string[0],ct).Images.Single();File.Delete(repo.EditedPath(row.Project,row.RelativePath));var result=repo.DeleteEditedImages(new[]{row},ct,NoProgress);Check(result.Deleted==0&&result.Errors.Count==1&&File.Exists(file),"Missing Edited file was counted as a successful deletion or source was removed");}
   });
   WindowsTest("Edited deletion rejects redirected project folders and staging without external changes",()=>{
    string file=Path.Combine(root,"edited-delete-links-source","image.fit");Write(file,64,48,(x,y)=>2000,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"edited-delete-links-repo"))){repo.AddEditedImages(new[]{file},null,"Links",ct,NoProgress);var row=EditedGallery.Read(repo,new string[0],ct).Images.Single();string outside=Path.Combine(root,"edited-delete-links-outside");Directory.CreateDirectory(outside);string external=Path.Combine(outside,"image.fit");File.Copy(file,external);string hash=Util.Hash(external,ct),link=Path.Combine(repo.EditedProjectFolder(row.Project),"redirect");SecurityJunction(link,outside);
     try{Expect(()=>repo.DeleteEditedImages(new[]{row,new EditedImage{Project=row.Project,RelativePath="redirect/image.fit"}},ct,NoProgress),"Linked Edited file was deleted");Check(File.Exists(repo.EditedPath(row.Project,row.RelativePath))&&Util.Hash(external,ct)==hash,"Linked deletion changed an earlier selected image or external bytes");}finally{Directory.Delete(link);}
     link=Path.Combine(repo.Meta,"staging");SecurityJunction(link,outside);try{Expect(()=>repo.DeleteEditedImages(new[]{row},ct,NoProgress),"Linked staging accepted");Check(File.Exists(repo.EditedPath(row.Project,row.RelativePath))&&Directory.EnumerateFileSystemEntries(outside).Count()==1&&Util.Hash(external,ct)==hash,"Linked staging deleted images or changed external data");}finally{Directory.Delete(link);}
    }
   });
  }
 }
}
