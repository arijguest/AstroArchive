// Network captures are staged locally, then use the normal verified archive importer.
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Threading;
using AstroArchive.Remote;
using RemoteConnection = AstroArchive.Remote.Connection;

namespace AstroArchive {
 public static class RemoteCapturePaths {
  // Windows paths (Seestar shares and watched local folders) use backslashes and ignore case.
  public static bool WindowsPaths(RemoteConnection c){return c.Kind=="Seestar SMB"||c.Kind=="Local folder";}
  public static bool LocalFolder(RemoteConnection c){return c!=null&&c.Kind=="Local folder";}
  // Watching the archive itself, or a folder that contains it, would re-import archived copies.
  public static void CheckWatchedFolder(RemoteConnection c,string repositoryRoot){
   if(!LocalFolder(c)||string.IsNullOrEmpty(repositoryRoot))return;string folder=Path.GetFullPath(c.Folder);
   if(Util.Within(folder,repositoryRoot)||Util.Within(repositoryRoot,folder))throw new IOException("Choose a capture folder outside the repository. AstroArchive cannot watch its own archive or a folder that contains it.");
  }
  public static string SourceLabel(RemoteConnection c){return LocalFolder(c)?c.Folder:c.Host;}
  public static string Relative(RemoteConnection c,string path){
   string root=c.Folder.TrimEnd('\\','/'),prefix=root+(WindowsPaths(c)?"\\":"/");
   if(!path.StartsWith(prefix,WindowsPaths(c)?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new IOException(LocalFolder(c)?"A capture was found outside the watched folder.":"The telescope returned a path outside its selected storage.");
   return Paths.SafeRelative(path.Substring(prefix.Length));
  }
  public static string Parent(RemoteConnection c,string path){string relative=Relative(c,path);string parent=Path.GetDirectoryName(relative);return string.IsNullOrEmpty(parent)?c.Folder:Join(c,c.Folder,parent);}
  public static string Join(RemoteConnection c,string folder,string name){char separator=WindowsPaths(c)?'\\':'/';return folder.TrimEnd('\\','/')+separator+name.Replace('\\',separator).Replace('/',separator);}
  // Watched local folders are archived in place, so their local path is the original path.
  public static string LocalRelative(RemoteConnection c,string path){string relative=Relative(c,path),context=LocalFolder(c)?"":c.Kind=="Seestar SMB"?SmbPath.Parse(c.Folder).Relative:c.Folder.Trim('/');return context.Length==0?relative:Paths.SafeRelative(context+"/"+relative);}
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
   var result=new List<Entry>();var queue=new Queue<string>();var visited=new HashSet<string>(RemoteCapturePaths.WindowsPaths(connection)?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);queue.Enqueue(folder);int entries=0;
   while(queue.Count>0){ct.ThrowIfCancellationRequested();string next=queue.Dequeue();if(!visited.Add(next))continue;if(visited.Count>4096)throw new IOException("Too many folders to search. Choose a smaller capture folder under Advanced.");
    foreach(var item in Browse(source,connection,next,ct)){if(++entries>MaxEntries)throw new IOException("Too many captures to search at once. Choose a smaller capture folder.");if(item.Directory)queue.Enqueue(item.Path);else result.Add(item);}
    if(progress!=null)progress(result.Count);
   }return result;
  }
 }
 public sealed class RemoteReceipt {public long Size;public long Modified;public string Hash,RelativePath;public bool Committed;}
 public sealed class RemoteStagingResult {
  public string Root;public readonly List<string> Files=new List<string>();public readonly List<string> Errors=new List<string>();public readonly List<string> Warnings=new List<string>();public int Downloaded,Reused;
 }
 public sealed class RemoteTransferEstimate {
  readonly long total;readonly double limit;readonly EtaEstimate estimate=new EtaEstimate();double lastSeconds,rate;long lastBytes;
  public RemoteTransferEstimate(long totalBytes,double limitMB){total=Math.Max(0,totalBytes);limit=limitMB*1048576;}
  public ProgressInfo Sample(long completedBytes,int done,int files,string text,double seconds,bool reused=false){
   completedBytes=Math.Max(0,Math.Min(total,completedBytes));double span=seconds-lastSeconds;long delta=completedBytes-lastBytes;
   if(!reused&&delta>0){double observed=delta/Math.Max(0.001,span);if(limit>0)observed=Math.Min(limit,observed);double alpha=1-Math.Exp(-Math.Max(0.001,span)/3);rate=rate<=0?observed:rate+(observed-rate)*alpha;}
   lastBytes=completedBytes;lastSeconds=seconds;double effective=rate>0?rate:limit;double? remaining=completedBytes>=total?(double?)0:effective>0?(double?)((total-completedBytes)/effective):null;
   return new ProgressInfo{Stage="Downloading from telescope",Text=text,Done=done,Total=files,TotalKnown=true,BytesDone=completedBytes,BytesTotal=total,ElapsedSeconds=seconds,EffectiveBytesPerSecond=effective,CopyPhase=true,RemainingSeconds=completedBytes>=total?(double?)0:estimate.Update(remaining,seconds),ProgressFraction=total==0?1:completedBytes/(double)total};
  }
 }
 public static class RemoteCaptureStaging {
  public static string CacheRoot(string cache,RemoteConnection c){return Path.Combine(cache,Util.HashText(c.Kind+"|"+c.Host+"|"+c.Port+"|"+c.Folder));}
  static void ForgetMetadata(RemoteConnection c,string path,string root,string receipts){string relative=RemoteCapturePaths.LocalRelative(c,path),local=Path.Combine(root,relative),receipt=Path.Combine(receipts,Util.HashText(relative)+".json");Paths.CheckLinks(local,root);Paths.CheckLinks(receipt,receipts);if(File.Exists(local))File.Delete(local);if(File.Exists(receipt))File.Delete(receipt);}
  static string Copy(ISource source,RemoteConnection c,Entry expected,string root,string receipts,CancellationToken ct,Action<long> pulse,out bool reused){
   ct.ThrowIfCancellationRequested();string relative=RemoteCapturePaths.LocalRelative(c,expected.Path),destination=Path.Combine(root,relative),receiptPath=Path.Combine(receipts,Util.HashText(relative)+".json");
   Paths.CheckLinks(destination,root);Paths.CheckLinks(receiptPath,receipts);Entry before=source.Stat(expected.Path);
   // FTP LIST often omits seconds; use fresh MDTM observations for transfer checks.
   if(expected.Size!=before.Size||(!RemoteConnection.Ftp(c.Kind)&&!RemoteCapturePaths.SameVersion(expected,before)))throw new IOException("Still being written; try again when this capture is complete.");
   reused=false;
   if(File.Exists(destination)&&File.Exists(receiptPath))try{var saved=Util.Deserialize<RemoteReceipt>(File.ReadAllText(receiptPath));if(saved.Size==before.Size&&saved.Modified==before.Modified.ToUniversalTime().Ticks&&new FileInfo(destination).Length==before.Size&&Util.Hash(destination,ct)==saved.Hash){
     if(saved.Committed){saved.Committed=false;Util.AtomicText(receiptPath,Util.Serialize(saved));}
     reused=true;return destination;
    }}catch(OperationCanceledException){throw;}catch(Exception){}
   // No settle delay before FTP transfers: a capture that changes while downloading fails the size, MDTM and FITS checks below and is retried.
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
    Util.AtomicText(receiptPath,Util.Serialize(new RemoteReceipt{Size=before.Size,Modified=before.Modified.ToUniversalTime().Ticks,Hash=hash,RelativePath=relative}));return destination;
   }finally{Repository.TryRemove(temp);}
  }
  public static RemoteStagingResult Download(ISource source,RemoteConnection c,IEnumerable<Entry> selection,string cache,CancellationToken ct,Action<ProgressInfo> progress,Action<Entry,string,long> transfer=null){
   using(var lease=OpenLease(cache,c))return DownloadUnderLease(source,c,selection,cache,ct,progress,transfer);
  }
  internal static FileStream OpenLease(string cache,RemoteConnection connection){
   string scope=CacheRoot(cache,connection);Paths.CheckLinks(scope,cache);Directory.CreateDirectory(scope);
   string path=Path.Combine(scope,"download.lock");Paths.CheckLinks(path,scope);
   return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  }
  internal static RemoteStagingResult DownloadUnderLease(ISource source,RemoteConnection c,IEnumerable<Entry> selection,string cache,CancellationToken ct,Action<ProgressInfo> progress,Action<Entry,string,long> transfer){
   var files=selection.Where(e=>!e.Directory&&Util.IsImageAsset(e.Name)).GroupBy(e=>RemoteCapturePaths.Relative(c,e.Path),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();if(files.Count==0)throw new IOException("Select at least one capture file.");
   string scope=CacheRoot(cache,c),root=Path.Combine(scope,"captures"),receipts=Path.Combine(scope,"receipts");Paths.CheckLinks(scope,cache);Directory.CreateDirectory(root);Directory.CreateDirectory(receipts);Paths.CheckLinks(root,cache);Paths.CheckLinks(receipts,cache);
   var result=new RemoteStagingResult{Root=root};long total=files.Sum(e=>Math.Max(0,e.Size)),doneBytes=0;int done=0;var clock=Stopwatch.StartNew();var eta=new RemoteTransferEstimate(total,c.LimitMB);
    foreach(var file in files){ct.ThrowIfCancellationRequested();bool reused=false;if(transfer!=null)transfer(file,"Downloading",0);if(progress!=null)progress(eta.Sample(doneBytes,done,files.Count,file.Name,clock.Elapsed.TotalSeconds));try{string local=Copy(source,c,file,root,receipts,ct,n=>{if(transfer!=null)transfer(file,"Downloading",n);if(progress!=null)progress(eta.Sample(doneBytes+n,done,files.Count,file.Name,clock.Elapsed.TotalSeconds));},out reused);result.Files.Add(local);if(reused)result.Reused++;else result.Downloaded++;if(transfer!=null)transfer(file,"Downloaded",file.Size);}
     catch(OperationCanceledException){if(transfer!=null)transfer(file,"Download stopped",0);throw;}catch(Exception e){if(transfer!=null)transfer(file,"Download failed",0);if(FileRetry.DiskFull(e))throw;result.Errors.Add(file.Name+": "+e.Message);}done++;doneBytes+=Math.Max(0,file.Size);if(progress!=null)progress(eta.Sample(doneBytes,done,files.Count,file.Name,clock.Elapsed.TotalSeconds,true));
    }
    // Keep recognised adjacent sidecars and ancestor DWARF session metadata.
    var wanted=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    foreach(var file in files){string parent=RemoteCapturePaths.Parent(c,file.Path);HashSet<string> names;if(!wanted.TryGetValue(parent,out names))wanted[parent]=names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string name in AssociatedMetadata.Names(file.Name))names.Add(name);if(c.Kind=="Origin FTP")names.Add(CelestronOrigin.SessionInfo);
     for(string folder=parent;;folder=RemoteCapturePaths.Parent(c,folder)){if(!wanted.TryGetValue(folder,out names))wanted[folder]=names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);names.Add("shotsInfo.json");if(folder.TrimEnd('\\','/')==c.Folder.TrimEnd('\\','/'))break;}
    }
    long metadataBytes=0;foreach(var folder in wanted){ct.ThrowIfCancellationRequested();List<Entry> listed;
     try{listed=source.List(folder.Key);}catch(OperationCanceledException){throw;}catch(Exception e){foreach(string name in folder.Value)ForgetMetadata(c,RemoteCapturePaths.Join(c,folder.Key,name),root,receipts);result.Warnings.Add("Some session metadata was unavailable: "+e.Message);continue;}
     foreach(string missing in folder.Value.Where(name=>!listed.Any(e=>!e.Directory&&e.Name.Equals(name,StringComparison.OrdinalIgnoreCase))))ForgetMetadata(c,RemoteCapturePaths.Join(c,folder.Key,missing),root,receipts);
     foreach(var entry in listed){if(entry.Directory||!folder.Value.Contains(entry.Name))continue;
      if(entry.Size>16*1024*1024||(metadataBytes+=Math.Max(0,entry.Size))>128*1024*1024){ForgetMetadata(c,entry.Path,root,receipts);result.Warnings.Add("Metadata too large to download: "+entry.Name);continue;}
      try{bool reused;Copy(source,c,entry,root,receipts,ct,null,out reused);}catch(OperationCanceledException){throw;}catch(Exception e){ForgetMetadata(c,entry.Path,root,receipts);result.Warnings.Add("Session metadata will need retry: "+entry.Name+": "+e.Message);}
     }
    }
   return result;
  }

  internal const long CompletedCacheBytes=4L*1024*1024*1024;
  internal static void MarkCommitted(string root,IEnumerable<Frame> frames){
   string receipts=Path.Combine(Path.GetDirectoryName(root),"receipts");
   foreach(var frame in frames.Where(f=>f.Status=="Imported"||f.Status=="Duplicate"||f.Status=="Duplicate (cached)")){
    string local=Path.GetFullPath(frame.SourcePath);
    if(!Util.Within(local,root)||!File.Exists(local))continue;
    string relative=local.Substring(root.TrimEnd('\\','/').Length).TrimStart('\\','/');
    string receiptPath=Path.Combine(receipts,Util.HashText(relative)+".json");
    Paths.CheckLinks(receiptPath,receipts);
    if(!File.Exists(receiptPath))continue;
    var receipt=Util.Deserialize<RemoteReceipt>(File.ReadAllText(receiptPath));
    if(receipt==null||receipt.Hash!=frame.Hash)continue;
    receipt.RelativePath=relative;receipt.Committed=true;
    Util.AtomicText(receiptPath,Util.Serialize(receipt));
   }
  }

  internal static void TrimCompleted(string root,long limit){
   string receipts=Path.Combine(Path.GetDirectoryName(root),"receipts");
   Paths.CheckLinks(receipts,Path.GetDirectoryName(root));
   if(!Directory.Exists(receipts))return;
   var entries=new List<Tuple<FileInfo,FileInfo>>();
   foreach(var file in new DirectoryInfo(receipts).EnumerateFiles("*.json")){
    if((file.Attributes&FileAttributes.ReparsePoint)!=0)continue;
    RemoteReceipt receipt;
    try{receipt=Util.Deserialize<RemoteReceipt>(File.ReadAllText(file.FullName));}
    catch(ArgumentException){continue;}catch(InvalidOperationException){continue;}
    if(receipt==null||string.IsNullOrEmpty(receipt.RelativePath)||file.Name!=Util.HashText(receipt.RelativePath)+".json")continue;
    if(!receipt.Committed&&Util.IsImageAsset(receipt.RelativePath))continue;
    var staged=new FileInfo(Paths.Local(root,receipt.RelativePath));
    Paths.CheckLinks(staged.FullName,root);
    if(staged.Exists)entries.Add(Tuple.Create(file,staged));
   }
   long bytes=entries.Sum(e=>e.Item2.Length);
   foreach(var entry in entries.OrderBy(e=>e.Item1.LastWriteTimeUtc)){
    if(bytes<=limit)break;
    long size=entry.Item2.Length;
    entry.Item2.Delete();entry.Item1.Delete();bytes-=size;
   }
  }
 }
 // Captures that exist but are still being written are retried on the next poll.
 public sealed class CaptureNotReadyException:IOException {public readonly int Count;public CaptureNotReadyException(int count):base("Waiting for "+count+" capture"+(count==1?"":"s")+" to finish writing."){Count=count;}}
 public sealed class RemoteArchiveResult {public RemoteStagingResult Downloads;public AutoUploadResult Archive;public string Report;public bool LocalFolder;public string Summary{get{return Archive.Summary+(LocalFolder?(Downloads.Errors.Count>0?" "+Downloads.Errors.Count+" captures could not be read and will be retried.":""):" "+Downloads.Downloaded+" downloaded; "+Downloads.Reused+" verified local downloads reused; "+Downloads.Errors.Count+" network files need retry.");}}}
 public static class RemoteArchiveImport {
  // Hold a cache lease through archiving so another request cannot replace the
  // staged bytes/metadata being screened. Different telescope caches transfer in parallel.
  static readonly ConcurrentDictionary<string,SemaphoreSlim> caches=new ConcurrentDictionary<string,SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
  static readonly ConditionalWeakTable<Repository,SemaphoreSlim> archives=new ConditionalWeakTable<Repository,SemaphoreSlim>();
  public static bool Included(Entry file,bool ignoreFailed,bool ignoreRaster){return (!ignoreFailed||!Util.FailedFilename(file.Name))&&(!ignoreRaster||!ImportPolicy.RasterFilename(file.Name));}
  // A watched folder is already on this PC: confirm each capture is complete and
  // unchanged, then archive it directly with the verified connected-import pipeline.
  static RemoteArchiveResult ArchiveInPlace(Repository repo,TelescopeProfile profile,RemoteConnection c,List<Entry> requested,List<Entry> included,int workers,CancellationToken ct,Action<ProgressInfo> progress,Action<Frame> frame,Action<ImportPlan> plan,bool ignoreFailed,bool ignoreRaster,Func<RemoteConnection,ISource> sourceFactory){
   RemoteCapturePaths.CheckWatchedFolder(c,repo.Root);
   var checkedFiles=new RemoteStagingResult{Root=c.Folder.TrimEnd('\\','/')};int writing=0;
   using(var source=(sourceFactory??Downloader.Source)(c))foreach(var file in included.Where(e=>!e.Directory).GroupBy(e=>e.Path,StringComparer.OrdinalIgnoreCase).Select(g=>g.First())){
    ct.ThrowIfCancellationRequested();
    try{
     var now=source.Stat(file.Path);if(!RemoteCapturePaths.SameVersion(file,now)){writing++;continue;}
     // Capture programs can create a FITS header before the pixel data is complete.
     if(Util.IsFits(file.Path)&&!file.Path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase))try{FitsCheck.Validate(file.Path);}catch(InvalidDataException){writing++;continue;}
     checkedFiles.Files.Add(file.Path);
    }catch(OperationCanceledException){throw;}catch(Exception e){if(e is IOException||e is UnauthorizedAccessException)checkedFiles.Errors.Add(file.Name+": "+e.Message);else throw;}
   }
   // Unfinished captures are not errors: they stay unclaimed and are offered again next poll.
   if(checkedFiles.Files.Count==0){if(writing>0)throw new CaptureNotReadyException(writing);throw new IOException("No captures could be read. "+string.Join("\n",checkedFiles.Errors));}
   var archiveGate=archives.GetValue(repo,key=>new SemaphoreSlim(1,1));if(progress!=null)progress(new ProgressInfo{Stage="Waiting to archive",Text="New captures are ready. Waiting for the current archive batch to finish.",TotalKnown=false});archiveGate.Wait(ct);try{
    var selected=ImportSelection.Create(checkedFiles.Root,checkedFiles.Files);var archived=UsbAutoUpload.Run(repo,profile,checkedFiles.Root,workers,ct,progress,frame,plan,ignoreFailed:ignoreFailed,ignoreRaster:ignoreRaster,robustMatching:true,selection:selected);
    archived.Import.IgnoredFailed+=requested.Count(e=>ignoreFailed&&Util.FailedFilename(e.Name));archived.Import.IgnoredRaster+=requested.Count(e=>ignoreRaster&&ImportPolicy.RasterFilename(e.Name)&&(!ignoreFailed||!Util.FailedFilename(e.Name)));archived.Import.Warnings.AddRange(checkedFiles.Errors);repo.SaveImportReport(archived.Import);
    return new RemoteArchiveResult{Downloads=checkedFiles,Archive=archived,Report=repo.LastReport,LocalFolder=true};
   }finally{archiveGate.Release();}
  }
  // Bound work in progress by both file count and size. An individual large recording
  // can occupy a batch by itself.
  internal static IEnumerable<List<Entry>> Batches(IEnumerable<Entry> entries){
   var batch=new List<Entry>();long bytes=0;
   foreach(var entry in entries){
    long size=Math.Max(0,entry.Size);
    if(batch.Count>0&&(batch.Count>=24||size>128L*1024*1024-bytes)){
     yield return batch;batch=new List<Entry>();bytes=0;
    }
    batch.Add(entry);bytes+=size;
   }
   if(batch.Count>0)yield return batch;
  }

  static void AddBatch(AutoUploadResult total,AutoUploadResult batch){
   total.Restored+=batch.Restored;
   total.Plan.Frames.AddRange(batch.Plan.Frames);total.Plan.Errors.AddRange(batch.Plan.Errors);
   var a=total.Import;var b=batch.Import;
   a.Imported+=b.Imported;a.Duplicates+=b.Duplicates;a.Failed+=b.Failed;a.SkippedDeleted+=b.SkippedDeleted;
   a.IgnoredFailed+=b.IgnoredFailed;a.IgnoredRaster+=b.IgnoredRaster;a.OriginalsKept+=b.OriginalsKept;
   a.Workers=Math.Max(a.Workers,b.Workers);a.Trials.AddRange(b.Trials);
   a.Errors.AddRange(b.Errors);a.Warnings.AddRange(b.Warnings);a.CleanupErrors.AddRange(b.CleanupErrors);
   a.Metrics.Accumulate(b.Metrics.Snapshot());
  }

  public static RemoteArchiveResult Run(Repository repo,TelescopeProfile profile,RemoteConnection c,IEnumerable<Entry> files,string cache,int workers,CancellationToken ct,Action<ProgressInfo> progress,Action<Frame> frame=null,Action<ImportPlan> plan=null,bool ignoreFailed=false,bool ignoreRaster=false,Func<RemoteConnection,ISource> sourceFactory=null,Action<AutoUploadResult> batchCompleted=null){
   var requested=files.ToList();var included=requested.Where(f=>Included(f,ignoreFailed,ignoreRaster)).ToList();
   if(included.Count==0)throw new IOException("The selected files are excluded by import preferences. Adjust those preferences or choose other captures.");
   if(RemoteCapturePaths.LocalFolder(c))return ArchiveInPlace(repo,profile,c,requested,included,workers,ct,progress,frame,plan,ignoreFailed,ignoreRaster,sourceFactory);
   string scope=Path.GetFullPath(RemoteCaptureStaging.CacheRoot(cache,c));
   var cacheGate=caches.GetOrAdd(scope,key=>new SemaphoreSlim(1,1));
   if(progress!=null)progress(new ProgressInfo{Stage="Waiting for telescope download",Text="Waiting for the current selection on this telescope.",TotalKnown=false});
   cacheGate.Wait(ct);
   try{
    using(var lease=RemoteCaptureStaging.OpenLease(cache,c))
    using(var source=(sourceFactory??Downloader.Source)(c)){
     var captures=included.Where(e=>!e.Directory&&Util.IsImageAsset(e.Name))
      .GroupBy(e=>RemoteCapturePaths.Relative(c,e.Path),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
     if(captures.Count==0)throw new IOException("Select at least one capture file.");
     var batches=Batches(captures).ToList();string root=Path.Combine(scope,"captures");
     var downloads=new RemoteStagingResult{Root=root};
     var total=new AutoUploadResult{Plan=new ImportPlan{Source=root},Import=new ImportResult{Metrics=new PipelineMetrics(null)},ScopeSummary=captures.Count+" selected network captures. "};
     var archiveGate=archives.GetValue(repo,key=>new SemaphoreSlim(1,1));
     bool archiveStarted=false;
     try{
      for(int i=0;i<batches.Count;i++){
       ct.ThrowIfCancellationRequested();
       string suffix=" · batch "+(i+1)+" of "+batches.Count;
       Action<ProgressInfo> report=progress==null?(Action<ProgressInfo>)null:p=>{p.Stage=(p.Stage??"Importing")+suffix;progress(p);};
       Action<Entry,string,long> transfer=frame==null?(Action<Entry,string,long>)null:(entry,status,bytes)=>frame(new Frame{
        SourcePath=Path.Combine(root,RemoteCapturePaths.LocalRelative(c,entry.Path)),OriginalName=entry.Name,Telescope=profile.Id,Model=profile.Model,
        Kind="Unknown",Status=status,Bytes=entry.Size,SourceDisposition=ImportWorkflow.Size(bytes)+" / "+ImportWorkflow.Size(entry.Size)+" from telescope"
       });
       var batch=RemoteCaptureStaging.DownloadUnderLease(source,c,batches[i],cache,ct,report,transfer);
       downloads.Files.AddRange(batch.Files);downloads.Errors.AddRange(batch.Errors);downloads.Warnings.AddRange(batch.Warnings);
       downloads.Downloaded+=batch.Downloaded;downloads.Reused+=batch.Reused;
       if(batch.Files.Count==0)continue;
       if(report!=null)report(new ProgressInfo{Stage="Waiting to archive",Text="Downloads are verified. Waiting for the current archive batch.",TotalKnown=false});
       archiveGate.Wait(ct);
       try{
        var selected=ImportSelection.Create(root,batch.Files);archiveStarted=true;
        var archived=UsbAutoUpload.Run(repo,profile,root,workers,ct,report,frame,plan,ignoreFailed:ignoreFailed,
         ignoreRaster:ignoreRaster,selection:selected,newOrChanged:true,deferFinalCheckpoint:true);
        AddBatch(total,archived);
        if(batchCompleted!=null)batchCompleted(archived);
        try{RemoteCaptureStaging.MarkCommitted(root,archived.Plan.Frames);RemoteCaptureStaging.TrimCompleted(root,RemoteCaptureStaging.CompletedCacheBytes);}
        catch(IOException error){downloads.Warnings.Add("Could not trim the completed telescope cache. "+error.Message);}
        catch(UnauthorizedAccessException error){downloads.Warnings.Add("Could not trim the completed telescope cache. "+error.Message);}
       }finally{archiveGate.Release();}
      }
     }finally{
      if(archiveStarted){
       archiveGate.Wait();
       try{repo.Checkpoint(CancellationToken.None);}
       catch(IOException error){downloads.Warnings.Add("Portable index checkpoint failed. "+error.Message);}
       catch(UnauthorizedAccessException error){downloads.Warnings.Add("Portable index checkpoint failed. "+error.Message);}
       finally{archiveGate.Release();}
      }
     }
     if(downloads.Files.Count==0)throw new IOException("No complete captures could be downloaded. "+string.Join("\\n",downloads.Errors));
     total.Import.IgnoredFailed+=requested.Count(e=>ignoreFailed&&Util.FailedFilename(e.Name));
     total.Import.IgnoredRaster+=requested.Count(e=>ignoreRaster&&ImportPolicy.RasterFilename(e.Name)&&(!ignoreFailed||!Util.FailedFilename(e.Name)));
     total.Import.Errors.AddRange(downloads.Errors);total.Import.Warnings.AddRange(downloads.Warnings);
     repo.SaveImportReport(total.Import);
     return new RemoteArchiveResult{Downloads=downloads,Archive=total,Report=repo.LastReport};
    }
   }finally{cacheGate.Release();}
  }
 }
 public sealed class RemoteLiveState {public bool Initialized;public List<string> Baseline=new List<string>();public List<string> Complete=new List<string>();}
 public sealed class RemoteLiveTracker {
  readonly HashSet<string> baseline=new HashSet<string>(StringComparer.OrdinalIgnoreCase),complete=new HashSet<string>(StringComparer.OrdinalIgnoreCase);readonly Dictionary<string,Entry> previous=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);bool initialized;
  public RemoteLiveTracker(RemoteLiveState state=null){if(state!=null){initialized=state.Initialized;baseline.UnionWith(state.Baseline);complete.UnionWith(state.Complete);}}
  public RemoteLiveState Snapshot(){return new RemoteLiveState{Initialized=initialized,Baseline=baseline.ToList(),Complete=complete.ToList()};}
  public List<Entry> Observe(IEnumerable<Entry> entries,bool includeExisting){var current=entries.ToList();if(!initialized){initialized=true;if(!includeExisting)foreach(var e in current)baseline.Add(e.Path);}
   var present=new HashSet<string>(current.Select(e=>e.Path),StringComparer.OrdinalIgnoreCase);var ready=new List<Entry>();foreach(var e in current){Entry old;if(!baseline.Contains(e.Path)&&!complete.Contains(e.Path)&&previous.TryGetValue(e.Path,out old)&&RemoteCapturePaths.SameVersion(old,e))ready.Add(e);previous[e.Path]=e;}foreach(string missing in previous.Keys.Where(p=>!present.Contains(p)).ToList())previous.Remove(missing);return ready;
  }
  public void Imported(string path){complete.Add(path);}
 }
 public static class RemoteLiveImport {
  public static void Run(Repository repo,TelescopeProfile profile,RemoteConnection c,string cache,int workers,CancellationToken ct,Action<ProgressInfo> progress,Action<Frame> frame,Action<ImportPlan> plan,bool ignoreFailed,bool ignoreRaster,Func<RemoteConnection,ISource> sourceFactory=null,Action<RemoteArchiveResult> completed=null,RemoteLiveState state=null,Action<RemoteLiveState> checkpoint=null){
   RemoteCapturePaths.CheckWatchedFolder(c,repo.Root);var tracker=new RemoteLiveTracker(state);bool baselineSaved=state!=null&&state.Initialized;int imported=0,duplicates=0;sourceFactory=sourceFactory??Downloader.Source;
   while(true){ct.ThrowIfCancellationRequested();try{
    bool local=RemoteCapturePaths.LocalFolder(c);
    List<Entry> entries;using(var source=sourceFactory(c))entries=RemoteCaptureCatalog.Search(source,c,c.Folder,ct,n=>{if(progress!=null)progress(new ProgressInfo{Stage=local?"Scanning folder":"Scanning telescope",Text=n.ToString("N0")+" capture names checked for new files",TotalKnown=false});});
    var ready=tracker.Observe(entries.Where(e=>RemoteArchiveImport.Included(e,ignoreFailed,ignoreRaster)),c.IncludeExisting);if(!baselineSaved){if(checkpoint!=null)checkpoint(tracker.Snapshot());baselineSaved=true;}if(ready.Count>0){var result=RemoteArchiveImport.Run(repo,profile,c,ready,cache,workers,ct,progress,frame,plan,ignoreFailed,ignoreRaster,sourceFactory);imported+=result.Archive.Import.Imported;duplicates+=result.Archive.Import.Duplicates;if(completed!=null)completed(result);
     var finished=new HashSet<string>(result.Archive.Plan.Frames.Where(f=>f.Status=="Imported"||f.Status.StartsWith("Duplicate")||f.Status=="Deleted"||f.Rejected).Select(f=>f.SourcePath),StringComparer.OrdinalIgnoreCase);
     foreach(var entry in ready)if(finished.Contains(Path.Combine(result.Downloads.Root,RemoteCapturePaths.LocalRelative(c,entry.Path))))tracker.Imported(entry.Path);
     if(checkpoint!=null)checkpoint(tracker.Snapshot());
    }
    if(progress!=null)progress(new ProgressInfo{Stage="Live import",Text="Watching for completed captures · "+imported+" imported · "+duplicates+" already present. Stop retains completed imports.",TotalKnown=false});
   }catch(OperationCanceledException){throw;}catch(CaptureNotReadyException e){if(progress!=null)progress(new ProgressInfo{Stage="Live import",Text=e.Message+" · "+imported+" imported · "+duplicates+" already present.",TotalKnown=false});}catch(Exception e){if(FileRetry.DiskFull(e))throw;if(progress!=null)progress(new ProgressInfo{Stage=RemoteCapturePaths.LocalFolder(c)?"Waiting for folder":"Reconnecting to telescope",Text=e.Message+" · Retrying automatically; completed imports are retained.",TotalKnown=false});}
    if(ct.WaitHandle.WaitOne(c.PollSeconds*1000))ct.ThrowIfCancellationRequested();
   }
  }
 }
}
