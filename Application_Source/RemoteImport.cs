// Network captures are staged locally, then use the normal verified archive importer.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using AstroArchive.Remote;
using RemoteConnection = AstroArchive.Remote.Connection;

namespace AstroArchive {
 public static class RemoteCapturePaths {
  public static string Relative(RemoteConnection c,string path){
   string root=c.Folder.TrimEnd('\\','/'),prefix=root+(c.Kind=="Seestar SMB"?"\\":"/");
   if(!path.StartsWith(prefix,c.Kind=="Seestar SMB"?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new IOException("The telescope returned a path outside its selected storage.");
   return Paths.SafeRelative(path.Substring(prefix.Length));
  }
  public static string Parent(RemoteConnection c,string path){string relative=Relative(c,path);string parent=Path.GetDirectoryName(relative);return string.IsNullOrEmpty(parent)?c.Folder:Join(c,c.Folder,parent);}
  public static string Join(RemoteConnection c,string folder,string name){return folder.TrimEnd('\\','/')+(c.Kind=="Seestar SMB"?"\\":"/")+name.Replace('\\',c.Kind=="Seestar SMB"?'\\':'/').Replace('/',c.Kind=="Seestar SMB"?'\\':'/');}
  public static string LocalRelative(RemoteConnection c,string path){string relative=Relative(c,path),context=c.Kind=="Seestar SMB"?SmbPath.Parse(c.Folder).Relative:c.Folder.Trim('/');return context.Length==0?relative:Paths.SafeRelative(context+"/"+relative);}
  public static bool SameVersion(Entry a,Entry b){return a!=null&&b!=null&&!a.Directory&&!b.Directory&&a.Size==b.Size&&a.Modified.ToUniversalTime()==b.Modified.ToUniversalTime();}
 }
 public sealed class RemoteCaptureCatalog {
  public const int MaxEntries=50000;
  public static List<Entry> Browse(ISource source,RemoteConnection connection,string folder,CancellationToken ct){
   if(folder!=connection.Folder)RemoteCapturePaths.Relative(connection,folder);
   var result=new List<Entry>();foreach(var item in source.List(folder)){ct.ThrowIfCancellationRequested();RemoteCapturePaths.Relative(connection,item.Path);
    if(RemoteCapturePaths.Parent(connection,item.Path)!=folder.TrimEnd('\\','/')&&RemoteCapturePaths.Parent(connection,item.Path).TrimEnd('\\','/')!=folder.TrimEnd('\\','/'))throw new IOException("The telescope returned an invalid folder listing.");
    if(SessionScanCache.SystemFolder(item.Name))continue;
    if(item.Directory||Util.IsImageAsset(item.Name))result.Add(item);
    if(result.Count>MaxEntries)throw new IOException("This folder has too many entries. Choose a smaller capture folder under Advanced.");
   }return result.OrderByDescending(e=>e.Directory).ThenBy(e=>e.Name,StringComparer.OrdinalIgnoreCase).ToList();
  }
  public static List<Entry> Search(ISource source,RemoteConnection connection,string folder,CancellationToken ct,Action<int> progress=null){
   var result=new List<Entry>();var queue=new Queue<string>();var visited=new HashSet<string>(connection.Kind=="Seestar SMB"?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);queue.Enqueue(folder);int entries=0;
   while(queue.Count>0){ct.ThrowIfCancellationRequested();string next=queue.Dequeue();if(!visited.Add(next))continue;if(visited.Count>4096)throw new IOException("Too many folders to search. Choose a smaller capture folder under Advanced.");
    foreach(var item in Browse(source,connection,next,ct)){if(++entries>MaxEntries)throw new IOException("Too many captures to search at once. Choose a smaller capture folder.");if(item.Directory)queue.Enqueue(item.Path);else result.Add(item);}
    if(progress!=null)progress(result.Count);
   }return result;
  }
  public static bool WarnForNetwork(IEnumerable<Entry> files){var list=files.ToList();return list.Count>=100||list.Sum(e=>Math.Max(0,e.Size))>=1073741824;}
 }
 public sealed class RemoteReceipt {public long Size;public long Modified;public string Hash;}
 public sealed class RemoteStagingResult {
  public string Root;public readonly List<string> Files=new List<string>();public readonly List<string> Errors=new List<string>();public readonly List<string> Warnings=new List<string>();public int Downloaded,Reused;
 }
 public static class RemoteCaptureStaging {
  public static string CacheRoot(string cache,RemoteConnection c){return Path.Combine(cache,Util.HashText(c.Kind+"|"+c.Host+"|"+c.Port+"|"+c.Folder));}
  static string Copy(ISource source,RemoteConnection c,Entry expected,string root,string receipts,CancellationToken ct,Action<long> pulse,out bool reused){
   ct.ThrowIfCancellationRequested();string relative=RemoteCapturePaths.LocalRelative(c,expected.Path),destination=Path.Combine(root,relative),receiptPath=Path.Combine(receipts,Util.HashText(relative)+".json");
   Paths.CheckLinks(destination,root);Paths.CheckLinks(receiptPath,receipts);Entry before=source.Stat(expected.Path);
   // FTP LIST often omits seconds; use fresh MDTM observations for transfer checks.
   if(expected.Size!=before.Size||(c.Kind!="DWARF FTP"&&!RemoteCapturePaths.SameVersion(expected,before)))throw new IOException("Still being written; try again when this capture is complete.");
   reused=false;
   if(File.Exists(destination)&&File.Exists(receiptPath))try{var saved=Util.Deserialize<RemoteReceipt>(File.ReadAllText(receiptPath));if(saved.Size==before.Size&&saved.Modified==before.Modified.ToUniversalTime().Ticks&&new FileInfo(destination).Length==before.Size&&Util.Hash(destination,ct)==saved.Hash){reused=true;return destination;}}catch(OperationCanceledException){throw;}catch(Exception){}
   if(c.Kind=="DWARF FTP"){if(ct.WaitHandle.WaitOne(1000))ct.ThrowIfCancellationRequested();if(!RemoteCapturePaths.SameVersion(before,source.Stat(expected.Path)))throw new IOException("Still being written; try again when this capture is complete.");}
   Directory.CreateDirectory(Path.GetDirectoryName(destination));Paths.CheckLinks(destination,root);string temp=destination+"."+Guid.NewGuid().ToString("N")+".partial";long bytes=0;string hash;
   try{
    using(var input=source.Open(expected.Path))using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,1048576))using(var sha=SHA256.Create()){
     byte[] buffer=new byte[262144];var clock=Stopwatch.StartNew();int count;
     while((count=input.Read(buffer,0,buffer.Length))>0){ct.ThrowIfCancellationRequested();bytes+=count;if(bytes>before.Size)throw new IOException("Capture grew while downloading; retry after writing finishes.");output.Write(buffer,0,count);sha.TransformBlock(buffer,0,count,buffer,0);if(pulse!=null)pulse(bytes);
      if(c.LimitMB>0){double target=bytes/(c.LimitMB*1048576);while(target-clock.Elapsed.TotalSeconds>0.02){if(ct.WaitHandle.WaitOne((int)Math.Min(100,target*1000-clock.Elapsed.TotalMilliseconds)))ct.ThrowIfCancellationRequested();}}
     }sha.TransformFinalBlock(new byte[0],0,0);hash=BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();output.Flush(true);
    }
    if(bytes!=before.Size||!RemoteCapturePaths.SameVersion(before,source.Stat(expected.Path)))throw new IOException("Capture changed while downloading; retry after writing finishes.");
    if(Util.IsFits(destination)&&!destination.EndsWith(".gz",StringComparison.OrdinalIgnoreCase))FitsCheck.Validate(temp);
    if(Util.Hash(temp,ct)!=hash)throw new IOException("The local download failed checksum verification.");
    ct.ThrowIfCancellationRequested();if(File.Exists(destination))File.Delete(destination);File.Move(temp,destination);File.SetLastWriteTimeUtc(destination,before.Modified.ToUniversalTime());
    Util.AtomicText(receiptPath,Util.Serialize(new RemoteReceipt{Size=before.Size,Modified=before.Modified.ToUniversalTime().Ticks,Hash=hash}));return destination;
   }finally{Repository.TryRemove(temp);}
  }
  public static RemoteStagingResult Download(ISource source,RemoteConnection c,IEnumerable<Entry> selection,string cache,CancellationToken ct,Action<ProgressInfo> progress){
   var files=selection.Where(e=>!e.Directory&&Util.IsImageAsset(e.Name)).GroupBy(e=>RemoteCapturePaths.Relative(c,e.Path),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();if(files.Count==0)throw new IOException("Select at least one capture file.");
   string scope=CacheRoot(cache,c),root=Path.Combine(scope,"captures"),receipts=Path.Combine(scope,"receipts");Paths.CheckLinks(scope,cache);Directory.CreateDirectory(root);Directory.CreateDirectory(receipts);Paths.CheckLinks(root,cache);Paths.CheckLinks(receipts,cache);
   var result=new RemoteStagingResult{Root=root};long total=files.Sum(e=>Math.Max(0,e.Size)),doneBytes=0;int done=0;
   Paths.CheckLinks(Path.Combine(scope,"download.lock"),scope);using(var gate=new FileStream(Path.Combine(scope,"download.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
    foreach(var file in files){ct.ThrowIfCancellationRequested();try{bool reused;string local=Copy(source,c,file,root,receipts,ct,n=>{if(progress!=null)progress(new ProgressInfo{Stage="Downloading from telescope",Text=file.Name,Done=done,Total=files.Count,TotalKnown=true,BytesDone=doneBytes+n,BytesTotal=total,ProgressFraction=total==0?0:(doneBytes+n)/(double)total});},out reused);result.Files.Add(local);if(reused)result.Reused++;else result.Downloaded++;}
     catch(OperationCanceledException){throw;}catch(Exception e){if(FileRetry.DiskFull(e))throw;result.Errors.Add(file.Name+": "+e.Message);}done++;doneBytes+=Math.Max(0,file.Size);
    }
    // Keep recognised adjacent sidecars and ancestor DWARF session metadata.
    var wanted=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    foreach(var file in files){string parent=RemoteCapturePaths.Parent(c,file.Path);HashSet<string> names;if(!wanted.TryGetValue(parent,out names))wanted[parent]=names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string name in AssociatedMetadata.Names(file.Name))names.Add(name);
     for(string folder=parent;;folder=RemoteCapturePaths.Parent(c,folder)){if(!wanted.TryGetValue(folder,out names))wanted[folder]=names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);names.Add("shotsInfo.json");if(folder.TrimEnd('\\','/')==c.Folder.TrimEnd('\\','/'))break;}
    }
    long metadataBytes=0;foreach(var folder in wanted){ct.ThrowIfCancellationRequested();try{foreach(var entry in source.List(folder.Key)){if(entry.Directory||!folder.Value.Contains(entry.Name))continue;if(entry.Size>16*1024*1024||(metadataBytes+=Math.Max(0,entry.Size))>128*1024*1024){result.Warnings.Add("Metadata too large to download: "+entry.Name);continue;}bool reused;Copy(source,c,entry,root,receipts,ct,null,out reused);}}
     catch(OperationCanceledException){throw;}catch(Exception e){result.Warnings.Add("Some session metadata was unavailable: "+e.Message);}
    }
   }return result;
  }
 }
 public sealed class RemoteArchiveResult {public RemoteStagingResult Downloads;public AutoUploadResult Archive;public string Summary{get{return Archive.Summary+" "+Downloads.Downloaded+" downloaded; "+Downloads.Reused+" verified local downloads reused; "+Downloads.Errors.Count+" network files need retry.";}}}
 public static class RemoteArchiveImport {
  public static RemoteArchiveResult Run(Repository repo,TelescopeProfile profile,RemoteConnection c,IEnumerable<Entry> files,string cache,int workers,CancellationToken ct,Action<ProgressInfo> progress,Action<Frame> frame=null,Action<ImportPlan> plan=null,bool ignoreFailed=false,bool ignoreRaster=false,Func<RemoteConnection,ISource> sourceFactory=null){
   RemoteStagingResult downloaded;using(var source=(sourceFactory??Downloader.Source)(c))downloaded=RemoteCaptureStaging.Download(source,c,files,cache,ct,progress);
   if(downloaded.Files.Count==0)throw new IOException("No complete captures could be downloaded. "+string.Join("\n",downloaded.Errors));
   var selected=ImportSelection.Create(downloaded.Root,downloaded.Files);var archived=UsbAutoUpload.Run(repo,profile,downloaded.Root,workers,ct,progress,frame,plan,ignoreFailed:ignoreFailed,ignoreRaster:ignoreRaster,robustMatching:true,selection:selected);
   archived.Import.Errors.AddRange(downloaded.Errors);archived.Import.Warnings.AddRange(downloaded.Warnings);repo.SaveImportReport(archived.Import);return new RemoteArchiveResult{Downloads=downloaded,Archive=archived};
  }
 }
 public sealed class RemoteLiveTracker {
  readonly HashSet<string> baseline=new HashSet<string>(StringComparer.OrdinalIgnoreCase),complete=new HashSet<string>(StringComparer.OrdinalIgnoreCase);readonly Dictionary<string,Entry> previous=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);bool initialized;
  public List<Entry> Observe(IEnumerable<Entry> entries,bool includeExisting){var current=entries.ToList();if(!initialized){initialized=true;if(!includeExisting)foreach(var e in current)baseline.Add(e.Path);}
   var present=new HashSet<string>(current.Select(e=>e.Path),StringComparer.OrdinalIgnoreCase);var ready=new List<Entry>();foreach(var e in current){Entry old;if(!baseline.Contains(e.Path)&&!complete.Contains(e.Path)&&previous.TryGetValue(e.Path,out old)&&RemoteCapturePaths.SameVersion(old,e))ready.Add(e);previous[e.Path]=e;}foreach(string missing in previous.Keys.Where(p=>!present.Contains(p)).ToList())previous.Remove(missing);return ready;
  }
  public void Imported(string path){complete.Add(path);}
 }
 public static class RemoteLiveImport {
  public static void Run(Repository repo,TelescopeProfile profile,RemoteConnection c,string cache,int workers,CancellationToken ct,Action<ProgressInfo> progress,Action<Frame> frame,Action<ImportPlan> plan,bool ignoreFailed,bool ignoreRaster,Func<RemoteConnection,ISource> sourceFactory=null){
   var tracker=new RemoteLiveTracker();int imported=0;sourceFactory=sourceFactory??Downloader.Source;
   while(true){ct.ThrowIfCancellationRequested();try{
    List<Entry> entries;using(var source=sourceFactory(c))entries=RemoteCaptureCatalog.Search(source,c,c.Folder,ct,n=>{if(progress!=null)progress(new ProgressInfo{Stage="Scanning telescope",Text=n.ToString("N0")+" capture names checked for new files",TotalKnown=false});});
    var ready=tracker.Observe(entries,c.IncludeExisting);if(ready.Count>0){var result=RemoteArchiveImport.Run(repo,profile,c,ready,cache,workers,ct,progress,frame,plan,ignoreFailed,ignoreRaster,sourceFactory);imported+=result.Archive.Import.Imported;
     var finished=new HashSet<string>(result.Archive.Plan.Frames.Where(f=>f.Status=="Imported"||f.Status.StartsWith("Duplicate")||f.Status=="Deleted"||f.Rejected).Select(f=>f.SourcePath),StringComparer.OrdinalIgnoreCase);
     foreach(var entry in ready)if(finished.Contains(Path.Combine(result.Downloads.Root,RemoteCapturePaths.LocalRelative(c,entry.Path))))tracker.Imported(entry.Path);
    }
    if(progress!=null)progress(new ProgressInfo{Stage="Live import",Text="Watching for completed captures · "+imported+" imported. Stop retains completed imports.",TotalKnown=false});
   }catch(OperationCanceledException){throw;}catch(Exception e){if(FileRetry.DiskFull(e))throw;if(progress!=null)progress(new ProgressInfo{Stage="Reconnecting to telescope",Text=e.Message+" · Retrying automatically; completed imports are retained.",TotalKnown=false});}
    if(ct.WaitHandle.WaitOne(c.PollSeconds*1000))ct.ThrowIfCancellationRequested();
   }
  }
 }
}
