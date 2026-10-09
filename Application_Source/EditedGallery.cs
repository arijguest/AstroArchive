// Read-only gallery snapshots can be built without accessing the live capture database.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class EditedGallery {
  public List<EditedImage> Images=new List<EditedImage>();
  public List<string> Errors=new List<string>();
  public static EditedGallery Read(Repository repository,string[] targets,CancellationToken token){
   token.ThrowIfCancellationRequested();var result=new EditedGallery();if(repository==null)return result;var projects=new List<EditedProject>();
   try{projects=repository.EditedProjects(out result.Errors,token);}catch(Exception error){if(!ReadError(error))throw;result.Errors.Add(error.Message);}
   foreach(var project in projects){token.ThrowIfCancellationRequested();try{
    var images=repository.ReadEditedImages(project,targets,token,false);foreach(var image in images)image.Project=project;result.Images.AddRange(images);
   }catch(Exception error){if(!ReadError(error))throw;result.Errors.Add(error.Message);}}
   token.ThrowIfCancellationRequested();result.Images=Order(result.Images);return result;
  }
  static int TypeGroup(EditedImage image){
   string type=image.FileType;if(type=="GIF")return 2;
   return new[]{"FITS","FZ","XISF","SER","CR2","CR3","NEF","NRW","ARW","DNG","RAF","ORF","RW2","PEF","SRW"}.Contains(type)?0:1;
  }
  public static List<EditedImage> Order(IEnumerable<EditedImage> images){
   return images.OrderBy(TypeGroup).ThenBy(i=>i.FileType,StringComparer.OrdinalIgnoreCase).ThenByDescending(i=>i.Modified).ThenBy(i=>i.Filename,StringComparer.OrdinalIgnoreCase).ThenBy(i=>i.Project==null?"":i.Project.Id,StringComparer.OrdinalIgnoreCase).ThenBy(i=>i.RelativePath,StringComparer.OrdinalIgnoreCase).ToList();
  }
  static bool ReadError(Exception error){return error is IOException||error is InvalidDataException||error is UnauthorizedAccessException;}
  // Compare metadata as well as file stamps: manual assignments do not change image bytes.
  public bool SameImages(IList<EditedImage> previous){
   if(Images.Count!=previous.Count)return false;
   for(int i=0;i<Images.Count;i++){
    var a=Images[i];var b=previous[i];var p=a.Project;var q=b.Project;
    if(a.RelativePath!=b.RelativePath||a.Filename!=b.Filename||a.Bytes!=b.Bytes||a.Modified!=b.Modified||a.Kind!=b.Kind||a.Source!=b.Source||a.RelatedImage!=b.RelatedImage||a.MetadataProblem!=b.MetadataProblem)return false;
    if(p.Id!=q.Id||p.Name!=q.Name||p.Target!=q.Target||p.Processor!=q.Processor||p.CreatedUtc!=q.CreatedUtc)return false;
    var x=a.Metadata;var y=b.Metadata;
    if(x.ImageClass!=y.ImageClass||x.Object!=y.Object||x.Filters!=y.Filters||x.RA!=y.RA||x.Dec!=y.Dec||x.Subs!=y.Subs||x.SubExposure!=y.SubExposure||x.TotalExposure!=y.TotalExposure||x.ReportedExposure!=y.ReportedExposure||x.Evidence!=y.Evidence)return false;
   }return true;
  }
 }
}
