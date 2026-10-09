using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public static partial class Exporter {
  public static string CreateEdited(Repository repo,IEnumerable<EditedImage> selection,ExportOptions options,CancellationToken ct,Action<ProgressInfo> progress){
   ct.ThrowIfCancellationRequested();string destination=Destination(repo,options);
   var inputs=new List<Tuple<string,string>>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var image in selection){
    ct.ThrowIfCancellationRequested();if(image==null||image.Project==null)throw new InvalidDataException("Select an Edited image from the gallery.");
    repo.ReadEditedProject(image.Project);string path=repo.EditedPath(image.Project,image.RelativePath);
    if(!Util.IsImageAsset(path)||!File.Exists(path))throw new IOException("Edited image is missing or unsupported: "+image.RelativePath);
    if(seen.Add(path))inputs.Add(Tuple.Create(path,Util.Hash(path,ct)));
   }
   if(inputs.Count==0)throw new InvalidOperationException("Select Edited files to export.");
   ct.ThrowIfCancellationRequested();CheckDestinationPath(destination);Directory.CreateDirectory(destination);
   var folders=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);int done=0;
   foreach(var input in inputs){
    ct.ThrowIfCancellationRequested();CheckDestinationPath(destination);
    string path=UniquePath(Path.Combine(destination,OutputName(input.Item1)),folders);
    string temporary=Path.Combine(destination,".astroarchive-export-"+Guid.NewGuid().ToString("N")+".partial");
    if(progress!=null)progress(new ProgressInfo{Done=done,Total=inputs.Count,Stage="Exporting Edited files",Text=Path.GetFileName(input.Item1)});
    try{Repository.CheckManagedPath(input.Item1,repo.Root);Repository.CopyVerified(input.Item1,temporary,input.Item2,ct);ct.ThrowIfCancellationRequested();File.Move(temporary,path);done++;}
    finally{if(File.Exists(temporary))File.Delete(temporary);}
   }
   if(progress!=null)progress(new ProgressInfo{Done=done,Total=inputs.Count,Stage="Exporting Edited files",Text=done+" files exported"});return destination;
  }
 }
}
