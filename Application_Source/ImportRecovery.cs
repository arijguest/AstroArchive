using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AstroArchive.Remote;

namespace AstroArchive {
 public sealed class ImportResumeRecord {
  public string Id=Guid.NewGuid().ToString("N"),Kind,Title,Repository,Source,State="Running",ProtectedPassword;
  public DateTime UpdatedUtc=DateTime.UtcNow;
  public Connection Connection;public TelescopeProfile Profile;public List<Entry> Files;
  public List<Frame> Frames;public ImportSelection Selection;public RemoteLiveState LiveState;public string[] EditedFiles;public EditedImportPlan EditedPlan;
  public int Workers;public bool IgnoreFailed,IgnoreRaster,DeleteOriginals;public string Solve="Off",Rotation="Off";
 }
 public sealed class ImportResumeStore {
  readonly string root;readonly object sync=new object();
  public readonly List<string> Warnings=new List<string>();
  public ImportResumeStore(string root){this.root=Path.GetFullPath(root);Directory.CreateDirectory(this.root);Paths.CheckLinks(this.root,Path.GetDirectoryName(this.root));}
  string PathFor(string id,string extension){Guid value;if(!Guid.TryParseExact(id,"N",out value))throw new IOException("Invalid import recovery identifier.");string path=Path.Combine(root,id+extension);Paths.CheckLinks(path,root);return path;}
  public void Save(ImportResumeRecord record){lock(sync){if(record.Connection!=null&&!string.IsNullOrEmpty(record.Connection.Password))throw new IOException("Import recovery credentials must be encrypted before saving.");record.UpdatedUtc=DateTime.UtcNow;string path=PathFor(record.Id,".json"),temporary=PathFor(record.Id,"."+Guid.NewGuid().ToString("N")+".tmp");try{byte[] bytes=Encoding.UTF8.GetBytes(Util.Serialize(record));using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{Repository.TryRemove(temporary);}}}
  public List<ImportResumeRecord> Load(){lock(sync){Warnings.Clear();var result=new List<ImportResumeRecord>();foreach(string path in Directory.EnumerateFiles(root,"*.json")){try{Paths.CheckLinks(path,root);var record=Util.Deserialize<ImportResumeRecord>(File.ReadAllText(path));if(record==null||PathFor(record.Id,".json")!=path||string.IsNullOrEmpty(record.Repository)||!Path.IsPathRooted(record.Repository)||!new[]{"Files","Usb","Dump","RemoteFiles","RemoteLive","EditedFiles","EditedFolder"}.Contains(record.Kind))throw new IOException("Invalid recovery record.");if(record.State=="Running"||record.State=="Pausing"){record.State="Interrupted";Save(record);}result.Add(record);}catch(Exception e){Warnings.Add(Path.GetFileName(path)+": "+e.Message);}}return result.OrderByDescending(r=>r.UpdatedUtc).ToList();}}
  public void Remove(ImportResumeRecord record){lock(sync){File.Delete(PathFor(record.Id,".json"));File.Delete(PathFor(record.Id,".json.bak"));File.Delete(PathFor(record.Id,".completed"));}}
  static string Key(Frame frame){return frame.SourcePath+"|"+frame.ImageKey+"|"+frame.ImageIndex;}
  sealed class Completion {public string Key,Hash;}
  public void Completed(ImportResumeRecord record,Frame frame){
   if(frame.Status!="Imported"&&(frame.Status==null||!frame.Status.StartsWith("Duplicate")))return;
   lock(sync){byte[] bytes=Encoding.UTF8.GetBytes(Util.Serialize(new Completion{Key=Key(frame),Hash=frame.Hash})+"\n");using(var output=new FileStream(PathFor(record.Id,".completed"),FileMode.Append,FileAccess.Write,FileShare.Read)){output.Write(bytes,0,bytes.Length);output.Flush(true);}}
  }
  public List<Frame> Remaining(ImportResumeRecord record,Repository repository,CancellationToken ct){
   var completed=new Dictionary<string,string>(Util.PathComparer);lock(sync){string path=PathFor(record.Id,".completed");if(File.Exists(path))foreach(string line in File.ReadLines(path)){try{var item=Util.Deserialize<Completion>(line);if(item!=null&&!string.IsNullOrEmpty(item.Key)&&!string.IsNullOrEmpty(item.Hash))completed[item.Key]=item.Hash;}catch{ /* A crash may truncate the final acknowledgement; archive verification still deduplicates it. */ }}}
   var archived=repository.All().GroupBy(Key,Util.PathComparer).ToDictionary(g=>g.Key,g=>g.ToList(),Util.PathComparer);var result=new List<Frame>();foreach(var frame in record.Frames??new List<Frame>()){
    ct.ThrowIfCancellationRequested();string hash;Frame retained=null;
    if(completed.TryGetValue(Key(frame),out hash))retained=repository.Find(hash);
    else if(!string.IsNullOrEmpty(frame.Hash))retained=repository.Find(frame.Hash);
    else{List<Frame> matches;if(archived.TryGetValue(Key(frame),out matches))retained=matches.FirstOrDefault(f=>f.SourceStamp!=null&&frame.SourceStamp!=null&&f.SourceStamp.ContentSame(frame.SourceStamp));}
    if(retained!=null)try{repository.ValidateCapture(retained,ct);continue;}catch(OperationCanceledException){throw;}catch(IOException){}catch(InvalidDataException){}
    var pending=frame.Clone();pending.Status="New";pending.TransferIssue=null;result.Add(pending);
   }return result;
  }
 }
}
