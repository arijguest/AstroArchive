// SQLite is supplied by Windows 10/11 (winsqlite3.dll). No third-party runtime.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;

namespace AstroArchive {
 public sealed class Database:IDisposable {
  IntPtr db;readonly object sync=new object();readonly string databasePath;long generation;public long Generation{get{lock(sync)return generation;}}
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2(byte[] name,out IntPtr db,int flags,IntPtr vfs);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr db);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int n,out IntPtr stmt,IntPtr tail);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr stmt);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr stmt);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_errmsg(IntPtr db);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_extended_errcode(IntPtr db);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_text(IntPtr stmt,int col);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_bytes(IntPtr stmt,int col);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_text(IntPtr stmt,int col,byte[] val,int n,IntPtr free);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_backup_init(IntPtr destination,byte[] target,IntPtr source,byte[] origin);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_backup_step(IntPtr backup,int pages);
  [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_backup_finish(IntPtr backup);
  static byte[] UTF(string s){return Encoding.UTF8.GetBytes(s+"\0");}
  string Error(){IntPtr p=sqlite3_errmsg(db);return Marshal.PtrToStringAnsi(p)+" (SQLite "+sqlite3_extended_errcode(db)+") at "+databasePath;}
  IOException Failure(){var error=new IOException(Error());error.Data["SQLiteCode"]=sqlite3_extended_errcode(db);return error;}
  public Database(string path,bool wal=false){databasePath=path;if(sqlite3_open_v2(UTF(path),out db,0x10006,IntPtr.Zero)!=0){string e=Error();Dispose();throw new IOException(e);}try{Exec("PRAGMA busy_timeout=5000");Exec(wal?"PRAGMA journal_mode=WAL":"PRAGMA journal_mode=DELETE");Exec("PRAGMA synchronous=FULL");Exec("CREATE TABLE IF NOT EXISTS files(hash TEXT PRIMARY KEY, data TEXT NOT NULL)");Exec("CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,data TEXT NOT NULL)");ManifestSchema();}catch{Dispose();throw;}}
  public List<string> Query(string sql,params string[] args){lock(sync){IntPtr s;if(sqlite3_prepare_v2(db,UTF(sql),-1,out s,IntPtr.Zero)!=0)throw Failure();try{for(int i=0;i<args.Length;i++){byte[] b=UTF(args[i]??"");if(sqlite3_bind_text(s,i+1,b,b.Length-1,new IntPtr(-1))!=0)throw Failure();}var result=new List<string>();int n;while((n=sqlite3_step(s))==100){int len=sqlite3_column_bytes(s,0);byte[] b=new byte[len];if(len>0)Marshal.Copy(sqlite3_column_text(s,0),b,0,len);result.Add(Encoding.UTF8.GetString(b));}if(n!=101)throw Failure();return result;}finally{sqlite3_finalize(s);}}}
  public void ManifestSchema(){Exec("CREATE TABLE IF NOT EXISTS source_manifest(root TEXT,path TEXT,identity TEXT,size INTEGER,mtime INTEGER,hash TEXT,destination TEXT,status TEXT,data TEXT,PRIMARY KEY(root,path))");Exec("CREATE INDEX IF NOT EXISTS manifest_identity ON source_manifest(identity)");Exec("CREATE INDEX IF NOT EXISTS manifest_hash ON source_manifest(hash,status)");Exec("CREATE TABLE IF NOT EXISTS deleted_files(hash TEXT PRIMARY KEY,data TEXT NOT NULL)");}
  public void Exec(string sql,params string[] args){lock(sync){Query(sql,args);if(sql.StartsWith("INSERT",StringComparison.OrdinalIgnoreCase)||sql.StartsWith("UPDATE",StringComparison.OrdinalIgnoreCase)||sql.StartsWith("DELETE",StringComparison.OrdinalIgnoreCase))generation++;}}
  public void Transaction(Action action){lock(sync){Exec("BEGIN IMMEDIATE");try{action();Exec("COMMIT");}catch{Exec("ROLLBACK");throw;}}}
  public void BackupTo(string path,CancellationToken ct){lock(sync){using(var destination=new Database(path,false)){IntPtr backup=sqlite3_backup_init(destination.db,UTF("main"),db,UTF("main"));if(backup==IntPtr.Zero)throw new IOException(destination.Error());int code=0;try{for(int i=0;i<40;i++){ct.ThrowIfCancellationRequested();code=sqlite3_backup_step(backup,-1);if(code==101)break;if(code!=5&&code!=6)throw new IOException("SQLite backup: "+destination.Error());if(ct.WaitHandle.WaitOne(50))ct.ThrowIfCancellationRequested();}if(code!=101)throw new IOException("SQLite snapshot remained busy.");}finally{if(sqlite3_backup_finish(backup)!=0)throw new IOException("SQLite snapshot could not commit: "+destination.Error());}destination.Exec("PRAGMA journal_mode=DELETE");}}}
  public void Dispose(){lock(sync){if(db!=IntPtr.Zero){int code=sqlite3_close(db);if(code!=0)throw new IOException("SQLite close failed: "+Error());db=IntPtr.Zero;}}}

 }
 public sealed partial class Repository:IDisposable {
  public string Root{get;private set;} public string Meta{get;private set;} Database db;Mutex mutex;bool held;
  public static string LocalIndexBase;
  public string WorkingIndex{get;private set;}public string LastReport="";DateTime checkpoint=DateTime.MinValue;long checkpointGeneration=-1;FileStamp checkpointStamp;
  public Repository(string root){Root=Path.GetFullPath(root);Directory.CreateDirectory(Root);Meta=Path.Combine(Root,".astroarchive");Directory.CreateDirectory(Meta);
   mutex=new Mutex(false,"Local\\AstroArchive_"+Util.HashText(Root.ToLowerInvariant()));try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held){mutex.Dispose();throw new IOException("This repository is already open in another AstroArchive window.");}
   string local=Path.Combine(LocalIndexBase??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","repositories"),Util.HashText(Root.ToLowerInvariant()));Directory.CreateDirectory(local);WorkingIndex=Path.Combine(local,"index.sqlite");
   try{string portable=Path.Combine(Meta,"index.sqlite");if(!File.Exists(WorkingIndex)&&File.Exists(portable)){using(var original=new Database(portable))original.BackupTo(WorkingIndex,CancellationToken.None);}db=new Database(WorkingIndex);db.ManifestSchema();RecoverTelescopeRename();NormalizeSolarTargets();}catch{Dispose();throw;}
  }
  public void Checkpoint(CancellationToken ct){ct.ThrowIfCancellationRequested();if(db.Generation==checkpointGeneration&&File.Exists(Path.Combine(Meta,"index.sqlite"))&&FileStamp.Read(Path.Combine(Meta,"index.sqlite")).VerifiedUnchanged(checkpointStamp)){return;}long snapshotGeneration=db.Generation;string local=WorkingIndex+"."+Guid.NewGuid().ToString("N")+".snapshot",target=Path.Combine(Meta,"index.sqlite"),temp=target+"."+Guid.NewGuid().ToString("N")+".partial";try{db.BackupTo(local,ct);string hash=Util.Hash(local,ct);CopyVerified(local,temp,hash,ct);CommitTemporary(temp,target,ct);checkpoint=DateTime.UtcNow;checkpointGeneration=snapshotGeneration;checkpointStamp=FileStamp.Read(target);}finally{TryRemove(local);TryRemove(local+"-wal");TryRemove(local+"-shm");TryRemove(temp);}}
  public void Dispose(){if(db!=null){try{Checkpoint(CancellationToken.None);}catch{}db.Dispose();db=null;}if(held){mutex.ReleaseMutex();held=false;}if(mutex!=null)mutex.Dispose();}
  public List<Frame> All(){var frames=db.Query("SELECT data FROM files").Select(Util.Deserialize<Frame>).ToList();return frames;}
  public Frame Find(string hash){string data=db.Query("SELECT data FROM files WHERE hash=?",hash).FirstOrDefault();return data==null?null:Util.Deserialize<Frame>(data);}
  public void Save(Frame f){f.Target=ObservationTargets.CanonicalSolar(f.Target);db.Exec("INSERT OR REPLACE INTO files(hash,data) VALUES(?,?)",f.Hash,Util.Serialize(f));}
  public void Refile(Frame f,CancellationToken ct){string old=FilePath(f),rel=Destination(f),dest=Path.Combine(Root,rel);if(string.Equals(old,dest,StringComparison.OrdinalIgnoreCase)){Save(f);return;}if(!File.Exists(old))throw new IOException("Repository file missing: "+old);if(Util.Hash(old,ct)!=f.Hash)throw new IOException("Repository file has changed; metadata was not applied: "+old);Directory.CreateDirectory(Path.GetDirectoryName(dest));bool moved=false;string previous=f.RelativePath;
   if(File.Exists(dest)){if(Util.Hash(dest,ct)!=f.Hash)throw new IOException("Conflicting destination file.");}else{File.Move(old,dest);moved=true;}try{f.RelativePath=rel;f.RepositoryStamp=FileStamp.Read(dest);Save(f);}catch{f.RelativePath=previous;if(moved)File.Move(dest,old);throw;}
  }
  public void SaveRotation(RotationResult r){db.Exec("INSERT OR REPLACE INTO sessions(id,data) VALUES(?,?)",r.Session,Util.Serialize(r));}
  public string FilePath(Frame f){string p=Path.GetFullPath(Path.Combine(Root,f.RelativePath??""));if(!Util.Within(p,Root))throw new IOException("Repository path escapes the selected folder.");return p;}
  sealed class Scanned {public Frame Frame;public long Bytes;public bool CacheHit,HeaderHit;public string Error;}
  Scanned ScanOne(ScanEntry entry,string source,string telescope,string model,bool reindex,bool deferHash,bool cloudSource,string telescopeIdentity,Dictionary<string,SourceManifest> cached,HashSet<string> deleted,Dictionary<string,Classifier.ShotsMetadata> shots,MetadataHeaderCache headers,PipelineMetrics metrics,CancellationToken ct){
   var item=new Scanned();string name=Path.GetFileName(entry.Path);
   try{
    ct.ThrowIfCancellationRequested();FileStamp stamp=FileStamp.Read(new FileInfo(entry.Path),entry.Enumerated);item.Bytes=stamp.Size;Frame f=null,indexed=null;SourceManifest old;
    using(var check=metrics.Begin("Duplicate checking",name)){
     if(cached.TryGetValue(entry.Path,out old)&&old.Status=="Complete"&&stamp.VerifiedUnchanged(old.Source)&&(indexed=Find(old.Hash))!=null){
      f=indexed.Clone();f.SourcePath=entry.Path;f.SourceStamp=stamp;
      if(File.Exists(FilePath(indexed))&&FileStamp.Read(FilePath(indexed)).VerifiedUnchanged(indexed.RepositoryStamp??old.Copy)){f.Status="Duplicate (cached)";item.CacheHit=true;}else f.Status="Restore";
     }if(f==null&&old!=null&&old.Status=="Deleted"&&deleted.Contains(old.Hash)&&old.Metadata!=null&&stamp.VerifiedUnchanged(old.Source)){f=old.Metadata.Clone();f.SourcePath=entry.Path;f.SourceStamp=stamp;MarkDeleted(f);}check.Complete();
    }
    if(f==null){
     using(var metadata=metrics.Begin("Metadata",name)){
      f=FileRetry.Run(()=>{
       AssetInfo asset=headers.GetAsset(entry.Path,stamp);FitsHeader header=asset==null?null:asset.Header;FileStamp captured=stamp;bool hit=asset!=null;
       if(!hit){using(var availability=(cloudSource||stamp.Cloud)?metrics.Begin("Cloud availability",name):null)using(var read=metrics.Begin("Header open/read",name)){
        using(var locked=new FileStream(entry.Path,FileMode.Open,FileAccess.Read,FileShare.Read)){captured=FileStamp.Read(entry.Path);asset=Assets.Inspect(entry.Path,n=>{ct.ThrowIfCancellationRequested();read.Bytes(n);metadata.Bytes(n);});header=asset.Header;}read.Complete();if(availability!=null)availability.Complete();
       }}
       Frame parsed=Classifier.Read(entry.Path,source,telescopeIdentity??telescope,model,stamp.Size,n=>metadata.Bytes(n),shots,header,captured,ct,metrics,asset);
       parsed.Telescope=telescope;
       if(!stamp.ContentSame(parsed.SourceStamp)||(hit&&!stamp.ContentSame(FileStamp.Read(entry.Path))))throw new InvalidDataException("Source changed while reading metadata. Scan again.");
       if(!hit)headers.PutAsset(entry.Path,parsed.SourceStamp,asset);item.HeaderHit=hit;return parsed;
      },ct,message=>{metrics.Current=message;metrics.Pulse(true);});stamp=f.SourceStamp;f.Hash="";f.Status="New";metadata.Complete();
     }
     if(!deferHash||reindex||deleted.Count>0){using(var check=metrics.Begin("Duplicate checking",name)){
      string hash=Util.Hash(entry.Path,ct,n=>check.Bytes(n));if(!stamp.ContentSame(FileStamp.Read(entry.Path)))throw new InvalidDataException("Source changed during scanning.");
      Frame existing=Find(hash);if(deleted.Contains(hash)){f.Hash=hash;MarkDeleted(f);}else if(existing!=null){f=existing.Clone();f.SourcePath=entry.Path;f.SourceStamp=stamp;f.Status=File.Exists(FilePath(existing))&&Util.Hash(FilePath(existing),ct)==hash?"Duplicate":"Restore";}else f.Hash=hash;check.Complete();
     }}
    }
    f.SourceRoot=source;item.Frame=f;
   }catch(OperationCanceledException){throw;}catch(Exception e){item.Error=FileRetry.Detail(entry.Path,e);item.Frame=new Frame{SourcePath=entry.Path,SourceRoot=source,OriginalName=name,Telescope=telescope,TelescopeIdentity=telescopeIdentity??telescope,Model=model,Make="Unknown",Target="Unknown",Camera="Unknown",Kind="Unknown",Mount="Unknown",Night="Unknown date",Notes=item.Error,Status="Unreadable",Hash=""};}
   return item;
  }
  public ImportPlan Scan(string source,string telescope,string model,CancellationToken ct,Action<ProgressInfo> progress,bool reindex=false,Action<Frame> onFrame=null,bool deferHash=false,bool cloudSource=false,int metadataWorkers=0,string telescopeIdentity=null,bool deferFinish=false,bool ignoreFailed=false,bool ignoreRaster=false){
   return ScanCore(source,telescope,model,ct,progress,reindex,onFrame,deferHash,cloudSource,false,metadataWorkers,telescopeIdentity,deferFinish,ignoreFailed,ignoreRaster);
  }
  ImportPlan ScanCore(string source,string telescope,string model,CancellationToken ct,Action<ProgressInfo> progress,bool reindex,Action<Frame> onFrame,bool deferHash,bool cloudSource,bool dump,int metadataWorkers=0,string telescopeIdentity=null,bool deferFinish=false,bool ignoreFailed=false,bool ignoreRaster=false){
   var metrics=new PipelineMetrics(progress);metrics.Phase(0,0,"Scanning",false,false);
   source=Path.GetFullPath(source);if(!Directory.Exists(source))throw new DirectoryNotFoundException(source);if(dump)ValidateDumpFolder();else if(!reindex&&(Util.Within(source,Root)||Util.Within(Root,source)))throw new IOException("Source and repository must be separate folders, with neither inside the other.");
   var plan=new ImportPlan{Source=source,Metrics=metrics};Dictionary<string,SourceManifest> cached;
   using(var index=metrics.Begin("Index loading","Loading source manifest")){cached=db.Query("SELECT data FROM source_manifest WHERE root=?",source).Select(Util.Deserialize<SourceManifest>).ToDictionary(m=>m.Path,StringComparer.OrdinalIgnoreCase);index.Complete();}
   if(dump)cached.Clear();
   var deleted=DeletedHashes();var shots=new Dictionary<string,Classifier.ShotsMetadata>(StringComparer.OrdinalIgnoreCase);var seen=new HashSet<string>();
   var headers=new MetadataHeaderCache(Path.GetDirectoryName(WorkingIndex),source);int workers=cloudSource?1:metadataWorkers>0?Math.Min(4,metadataWorkers):Math.Min(2,Math.Max(1,Environment.ProcessorCount));
   using(var queue=new BlockingCollection<ScanEntry>(128))using(var overflow=new ScanOverflow(Path.GetDirectoryName(WorkingIndex)))using(var linked=CancellationTokenSource.CreateLinkedTokenSource(ct)){
    var producer=Task.Run(()=>{
     try{var stack=new Stack<DirectoryInfo>();stack.Push(new DirectoryInfo(source));while(stack.Count>0){linked.Token.ThrowIfCancellationRequested();var directory=stack.Pop();try{
      using(var discovery=metrics.Begin("Discovery",directory.FullName)){long files=0;foreach(var info in directory.EnumerateFileSystemInfos()){
       linked.Token.ThrowIfCancellationRequested();if((info.Attributes&FileAttributes.Directory)!=0){if(info.Name!=".astroarchive"&&!(reindex&&info.FullName.Equals(DumpFolder,StringComparison.OrdinalIgnoreCase))){if(FileStamp.CanTraverse((DirectoryInfo)info))stack.Push((DirectoryInfo)info);else lock(plan.Errors)plan.Errors.Add("Skipped linked or unresolvable directory: "+info.FullName);}}
       else if(Util.IsImageAsset(info.Name)){if(ignoreFailed&&Util.FailedFilename(info.Name)){plan.IgnoredFailed++;continue;}if(ignoreRaster&&ImportPolicy.RasterFilename(info.Name)){plan.IgnoredRaster++;continue;}var entry=ScanEntry.From((FileInfo)info);if(!queue.TryAdd(entry))overflow.Add(entry);metrics.Discover(entry.Enumerated.Size);files++;}
      }discovery.Complete(files);}
     }catch(OperationCanceledException){throw;}catch(Exception e){lock(plan.Errors)plan.Errors.Add(FileRetry.Detail(directory.FullName,e));}}}
     finally{try{overflow.Seal();}finally{metrics.InventoryComplete();queue.CompleteAdding();}}
    },linked.Token);
    var pending=new List<Task<Scanned>>();
    Action<Task<Scanned>> collect=task=>{var item=task.GetAwaiter().GetResult();Frame frame=item.Frame;plan.Bytes+=item.Bytes;if(item.Error!=null){lock(plan.Errors)plan.Errors.Add(item.Error);}if(item.CacheHit)plan.CacheHits++;if(item.HeaderHit)plan.MetadataCacheHits++;
     if(!string.IsNullOrEmpty(frame.Hash)&&frame.Status=="New"&&!seen.Add(frame.Hash))frame.Status="Duplicate in source";
     if(reindex&&item.Error==null&&frame.Status!="Deleted"){frame.RelativePath=frame.SourcePath.Substring(Root.TrimEnd('\\','/').Length).TrimStart('\\','/');frame.Status="Indexed";frame.RepositoryStamp=FileStamp.Read(frame.SourcePath);Save(frame);}
     plan.Frames.Add(frame);if(onFrame!=null)onFrame(frame.Clone());metrics.Complete(item.Error==null?item.Bytes:0);
    };
    try{
     foreach(var entry in queue.GetConsumingEnumerable(ct).Concat(overflow.Read(ct))){ct.ThrowIfCancellationRequested();var captured=entry;pending.Add(Task.Run(()=>ScanOne(captured,source,telescope,model,reindex,deferHash,cloudSource,telescopeIdentity,cached,deleted,shots,headers,metrics,linked.Token),linked.Token));
      if(pending.Count>=workers){var ready=Task.WhenAny(pending).GetAwaiter().GetResult();pending.Remove(ready);collect(ready);}
     }
     while(pending.Count>0){ct.ThrowIfCancellationRequested();var ready=Task.WhenAny(pending).GetAwaiter().GetResult();pending.Remove(ready);collect(ready);}producer.GetAwaiter().GetResult();
     metrics.Finalise("Saving scan metadata cache");try{headers.Flush();}catch(Exception e){plan.Errors.Add("Metadata cache could not be saved: "+e.Message);}if(reindex)Checkpoint(ct);
     if(!deferFinish)metrics.Finish("Scan complete: "+plan.Frames.Count+" FITS; "+plan.CacheHits+" verified duplicates; "+plan.MetadataCacheHits+" cached headers; "+plan.IgnoredFailed+" failed filenames ignored; "+plan.IgnoredRaster+" PNG/JPG files ignored");LastReport=metrics.Report()+"\r\n"+string.Join("\r\n",plan.Errors);return plan;
    }finally{linked.Cancel();try{producer.GetAwaiter().GetResult();}catch(OperationCanceledException){}foreach(var task in pending){try{task.GetAwaiter().GetResult();}catch(OperationCanceledException){}}}
   }
  }
  public string Destination(Frame f){
   bool cal=Assets.IsCalibration(f.Kind);string settings=Util.Safe(Util.Num(f.Exposure)+"s_gain"+Util.Num(f.Gain)+"_bin"+f.BinX+"x"+f.BinY);
   string type=MountLabels.Type(f.Mount);string mount=type=="EQ"?"EQ":type=="Alt-Az"?"AltAz":"Unknown";
   string p=cal?Path.Combine("Calibration",Util.Safe(f.MakeText),Util.Safe(f.Telescope),Util.Safe(f.Camera),Util.Safe(f.Kind),Util.Safe(f.Night),settings):Path.Combine("Targets",Util.Safe(ObservationTargets.CanonicalSolar(f.Target)),Util.Safe(f.Night),Util.Safe(f.MakeText),Util.Safe(f.Telescope),mount,Util.Safe(f.Camera),Util.Safe(f.Kind),settings);
   string filename=f.Hash.Substring(0,12)+"_"+Util.SafeFile(f.OriginalName);return Path.Combine(p,filename);
  }
  public static void CopyVerified(string from,string to,string hash,CancellationToken ct,PipelineMetrics metrics=null,PipelineMetrics.Transfer transfer=null){string copied=FileTransfer.CopyHash(from,to,ct,metrics,FileStamp.Read(from).Cloud,transfer);if(!string.IsNullOrEmpty(hash)&&copied!=hash)throw new IOException("Checksum mismatch; source may have changed during copying: "+from);using(var scope=metrics==null?null:metrics.Begin("Verification",Path.GetFileName(to))){if(transfer!=null)transfer.BeginVerification();if(Util.Hash(to,ct,n=>{if(scope!=null)scope.Bytes(n);if(transfer!=null)transfer.Verified(n);})!=copied)throw new IOException("Destination checksum mismatch: "+to);if(transfer!=null)transfer.EndVerification();if(scope!=null)scope.Complete();}}
  public int Verify(CancellationToken ct,Action<ProgressInfo> progress){var all=All();int bad=0;for(int i=0;i<all.Count;i++){ct.ThrowIfCancellationRequested();var f=all[i];progress(new ProgressInfo{Done=i,Total=all.Count,Text="Verifying "+f.OriginalName});string p=FilePath(f);if(!File.Exists(p)){f.Status="Missing";bad++;}else if(Util.Hash(p,ct)!=f.Hash){f.Status="Changed";bad++;}else f.Status="Verified";Save(f);}return bad;}
  public void ExportIndex(string path,IEnumerable<Frame> selection=null){var all=selection??All();StringBuilder b=new StringBuilder("Target,ObjectID,CommonName,Make,Model,MakeEvidence,TargetEvidence,Telescope,Camera,Kind,Mount,MountEvidence,Observed,TimeSource,AcquisitionDate,AcquisitionDateSource,Exposure_s,Gain,Temperature_C,Filter,Calibration,Dimensions,Hash,RelativePath,SourceDisposition,Format,Capabilities,CameraModel,CameraId,TelescopeModel,Offset,GainUnit,ElectronsPerADU,ReadoutMode,ROI,OpticalConfiguration,ObservedUTC,Timezone,LinearData,ImageKey,ImageIndex\r\n");foreach(var f in all){string[] a={f.Target,f.ObjectId,f.CommonName,f.MakeText,f.Model,f.MakeEvidence,f.TargetEvidence,f.Telescope,f.Camera,f.Kind,f.MountText,f.MountEvidenceText,f.Observed,f.TimeSource,f.AcquisitionDate,f.AcquisitionDateSource,Util.Num(f.Exposure),Util.Num(f.Gain),Util.Num(f.Temperature),f.Filter,f.Calibration,f.SizeText,f.Hash,f.RelativePath,f.SourceDisposition,f.Format,f.CapabilityText,f.CameraModel,f.CameraId,f.TelescopeModel,Util.Num(f.Offset),f.GainUnit,Util.Num(f.ElectronsPerAdu),f.ReadoutMode,f.Roi,f.OpticalConfiguration,f.ObservedUtc,f.TimeZoneId,f.LinearData.HasValue?f.LinearData.ToString():"",f.ImageKey,f.ImageIndex.HasValue?f.ImageIndex.ToString():""};b.AppendLine(string.Join(",",a.Select(s=>"\""+(s??"").Replace("\"","\"\"")+"\"")));}File.WriteAllText(path,b.ToString(),new UTF8Encoding(true));}
 }
}
