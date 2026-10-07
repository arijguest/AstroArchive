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
  IntPtr db;readonly object sync=new object();readonly string databasePath;
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
  public void ManifestSchema(){Exec("CREATE TABLE IF NOT EXISTS source_manifest(root TEXT,path TEXT,identity TEXT,size INTEGER,mtime INTEGER,hash TEXT,destination TEXT,status TEXT,data TEXT,PRIMARY KEY(root,path))");Exec("CREATE INDEX IF NOT EXISTS manifest_identity ON source_manifest(identity)");Exec("CREATE INDEX IF NOT EXISTS manifest_hash ON source_manifest(hash,status)");}
  public void Exec(string sql,params string[] args){Query(sql,args);}
  public void Transaction(Action action){lock(sync){Exec("BEGIN IMMEDIATE");try{action();Exec("COMMIT");}catch{Exec("ROLLBACK");throw;}}}
  public void BackupTo(string path,CancellationToken ct){lock(sync){using(var destination=new Database(path,false)){IntPtr backup=sqlite3_backup_init(destination.db,UTF("main"),db,UTF("main"));if(backup==IntPtr.Zero)throw new IOException(destination.Error());int code=0;try{for(int i=0;i<40;i++){ct.ThrowIfCancellationRequested();code=sqlite3_backup_step(backup,-1);if(code==101)break;if(code!=5&&code!=6)throw new IOException("SQLite backup: "+destination.Error());if(ct.WaitHandle.WaitOne(50))ct.ThrowIfCancellationRequested();}if(code!=101)throw new IOException("SQLite snapshot remained busy.");}finally{if(sqlite3_backup_finish(backup)!=0)throw new IOException("SQLite snapshot could not commit: "+destination.Error());}destination.Exec("PRAGMA journal_mode=DELETE");}}}
  public void Dispose(){lock(sync){if(db!=IntPtr.Zero){int code=sqlite3_close(db);if(code!=0)throw new IOException("SQLite close failed: "+Error());db=IntPtr.Zero;}}}

 }
 public sealed partial class Repository:IDisposable {
  public string Root{get;private set;} public string Meta{get;private set;} Database db;Mutex mutex;bool held;
  public static string LocalIndexBase;
  public string WorkingIndex{get;private set;}public string LastReport="";DateTime checkpoint=DateTime.MinValue;
  public Repository(string root){Root=Path.GetFullPath(root);Directory.CreateDirectory(Root);Meta=Path.Combine(Root,".astroarchive");Directory.CreateDirectory(Meta);
   mutex=new Mutex(false,"Local\\AstroArchive_"+Util.HashText(Root.ToLowerInvariant()));try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held){mutex.Dispose();throw new IOException("This repository is already open in another AstroArchive window.");}
   string local=Path.Combine(LocalIndexBase??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","repositories"),Util.HashText(Root.ToLowerInvariant()));Directory.CreateDirectory(local);WorkingIndex=Path.Combine(local,"index.sqlite");
   try{string portable=Path.Combine(Meta,"index.sqlite");if(!File.Exists(WorkingIndex)&&File.Exists(portable)){using(var original=new Database(portable))original.BackupTo(WorkingIndex,CancellationToken.None);}db=new Database(WorkingIndex);db.ManifestSchema();}catch{Dispose();throw;}
  }
  public void Checkpoint(CancellationToken ct){string local=WorkingIndex+"."+Guid.NewGuid().ToString("N")+".snapshot",target=Path.Combine(Meta,"index.sqlite"),temp=target+"."+Guid.NewGuid().ToString("N")+".partial";try{db.BackupTo(local,ct);string hash=Util.Hash(local,ct);CopyVerified(local,temp,hash,ct);CommitTemporary(temp,target,ct);checkpoint=DateTime.UtcNow;}finally{TryRemove(local);TryRemove(local+"-wal");TryRemove(local+"-shm");TryRemove(temp);}}
  public void Dispose(){if(db!=null){try{Checkpoint(CancellationToken.None);}catch{}db.Dispose();db=null;}if(held){mutex.ReleaseMutex();held=false;}if(mutex!=null)mutex.Dispose();}
  public List<Frame> All(){return db.Query("SELECT data FROM files").Select(Util.Deserialize<Frame>).ToList();}
  public void Save(Frame f){db.Exec("INSERT OR REPLACE INTO files(hash,data) VALUES(?,?)",f.Hash,Util.Serialize(f));}
  public void Refile(Frame f,CancellationToken ct){string old=FilePath(f),rel=Destination(f),dest=Path.Combine(Root,rel);if(string.Equals(old,dest,StringComparison.OrdinalIgnoreCase)){Save(f);return;}if(!File.Exists(old))throw new IOException("Repository file missing: "+old);if(Util.Hash(old,ct)!=f.Hash)throw new IOException("Repository file has changed; metadata was not applied: "+old);Directory.CreateDirectory(Path.GetDirectoryName(dest));bool moved=false;string previous=f.RelativePath;
   if(File.Exists(dest)){if(Util.Hash(dest,ct)!=f.Hash)throw new IOException("Conflicting destination file.");}else{File.Move(old,dest);moved=true;}try{f.RelativePath=rel;f.RepositoryStamp=FileStamp.Read(dest);Save(f);}catch{f.RelativePath=previous;if(moved)File.Move(dest,old);throw;}
  }
  public void SaveRotation(RotationResult r){db.Exec("INSERT OR REPLACE INTO sessions(id,data) VALUES(?,?)",r.Session,Util.Serialize(r));}
  public string FilePath(Frame f){string p=Path.GetFullPath(Path.Combine(Root,f.RelativePath??""));if(!Util.Within(p,Root))throw new IOException("Repository path escapes the selected folder.");return p;}
  public ImportPlan Scan(string source,string telescope,string model,CancellationToken ct,Action<ProgressInfo> progress,bool reindex=false,Action<Frame> onFrame=null,bool deferHash=false,bool cloudSource=false){
   source=Path.GetFullPath(source);if(!Directory.Exists(source))throw new DirectoryNotFoundException(source);if(!reindex&&(Util.Within(source,Root)||Util.Within(Root,source)))throw new IOException("Source and repository must be separate folders, with neither inside the other.");
   var metrics=new PipelineMetrics(progress);var plan=new ImportPlan{Source=source,Metrics=metrics};var known=All().ToDictionary(f=>f.Hash);var seen=new HashSet<string>();var cached=db.Query("SELECT data FROM source_manifest WHERE root=?",source).Select(Util.Deserialize<SourceManifest>).ToDictionary(m=>m.Path,StringComparer.OrdinalIgnoreCase);var shotsCache=new Dictionary<string,Classifier.ShotsMetadata>(StringComparer.OrdinalIgnoreCase);var queue=new BlockingCollection<FileInfo>(128);int discovered=0;
   using(var linked=CancellationTokenSource.CreateLinkedTokenSource(ct)){var producer=Task.Run(()=>{try{var stack=new Stack<DirectoryInfo>();stack.Push(new DirectoryInfo(source));while(stack.Count>0){linked.Token.ThrowIfCancellationRequested();var d=stack.Pop();try{using(var entries=d.EnumerateFileSystemInfos().GetEnumerator()){while(true){FileSystemInfo item;using(var stage=metrics.Begin("Discovery",d.FullName)){if(!entries.MoveNext())break;item=entries.Current;if(item is FileInfo&&Util.IsFits(item.Name))stage.Complete();}if((item.Attributes&FileAttributes.Directory)!=0){if(item.Name!=".astroarchive"){if(FileStamp.CanTraverse((DirectoryInfo)item))stack.Push((DirectoryInfo)item);else lock(plan.Errors)plan.Errors.Add("Skipped linked or unresolvable directory: "+item.FullName);}}else if(Util.IsFits(item.Name)){queue.Add((FileInfo)item,linked.Token);Interlocked.Increment(ref discovered);}}}}catch(OperationCanceledException){throw;}catch(Exception e){lock(plan.Errors)plan.Errors.Add(FileRetry.Detail(d.FullName,e));}}}finally{metrics.Total=discovered;queue.CompleteAdding();}},linked.Token);
    try{foreach(var info in queue.GetConsumingEnumerable(ct)){ct.ThrowIfCancellationRequested();try{FileStamp stamp=FileStamp.Read(info);plan.Bytes+=stamp.Size;Frame f=null;SourceManifest old;
      using(var check=metrics.Begin("Duplicate checking",info.Name)){if(cached.TryGetValue(info.FullName,out old)&&old.Status=="Complete"&&stamp.VerifiedUnchanged(old.Source)&&known.ContainsKey(old.Hash)){var indexed=known[old.Hash];if(File.Exists(FilePath(indexed))&&FileStamp.Read(FilePath(indexed)).VerifiedUnchanged(indexed.RepositoryStamp??old.Copy)){f=indexed.Clone();f.SourcePath=info.FullName;f.SourceStamp=stamp;f.Status="Duplicate (cached)";plan.CacheHits++;}else{f=indexed.Clone();f.SourcePath=info.FullName;f.SourceStamp=stamp;f.Status="Restore";}}check.Complete();}
      if(f==null){PipelineMetrics.Scope waiting=(cloudSource||stamp.Cloud)?metrics.Begin("Cloud availability",info.Name):null;try{using(var stage=metrics.Begin("Metadata",info.Name)){f=FileRetry.Run(()=>Classifier.Read(info.FullName,source,telescope,model,stamp.Size,n=>stage.Bytes(n),shotsCache),ct,message=>{metrics.Current=message;metrics.Pulse(true);});if(!stamp.ContentSame(f.SourceStamp))throw new InvalidDataException("Source changed while reading metadata. Scan again.");stamp=f.SourceStamp;f.Hash="";f.Status="New";stage.Complete();}}finally{if(waiting!=null){waiting.Complete();waiting.Dispose();}}
       if(!deferHash||reindex){using(var stage=metrics.Begin("Duplicate checking",info.Name)){string hash=Util.Hash(info.FullName,ct);stage.Bytes(stamp.Size);stage.Complete();Frame existing;if(known.TryGetValue(hash,out existing)){f=existing.Clone();f.SourcePath=info.FullName;f.SourceStamp=stamp;f.Status=File.Exists(FilePath(existing))&&Util.Hash(FilePath(existing),ct)==hash?"Duplicate":"Restore";}else{f.Hash=hash;f.Status=seen.Contains(hash)?"Duplicate in source":"New";}seen.Add(hash);}}
      }
      if(reindex){f.RelativePath=info.FullName.Substring(Root.TrimEnd('\\','/').Length).TrimStart('\\','/');f.Status="Indexed";f.RepositoryStamp=FileStamp.Read(info);Save(f);}f.SourceRoot=source;plan.Frames.Add(f);if(onFrame!=null)onFrame(f.Clone());metrics.Complete(stamp.Size);
     }catch(OperationCanceledException){throw;}catch(Exception e){string detail=FileRetry.Detail(info.FullName,e);plan.Errors.Add(detail);var unreadable=new Frame{SourcePath=info.FullName,SourceRoot=source,OriginalName=info.Name,Telescope=telescope,Model=model,Make="Unknown",Target="Unknown",Camera="Unknown",Kind="Unknown",Mount="Unknown",Night="Unknown date",Notes=detail,Status="Unreadable",Hash=""};plan.Frames.Add(unreadable);if(onFrame!=null)onFrame(unreadable);metrics.Complete(0);}}
     producer.GetAwaiter().GetResult();metrics.Total=metrics.Done;metrics.TotalBytes=plan.Bytes;metrics.Current="Scan complete: "+plan.Frames.Count+" FITS; "+plan.CacheHits+" unchanged verified files cached";metrics.Pulse(true);LastReport=metrics.Report()+"\r\n"+string.Join("\r\n",plan.Errors);if(reindex)Checkpoint(ct);return plan;
    }finally{linked.Cancel();try{producer.GetAwaiter().GetResult();}catch(OperationCanceledException){}queue.Dispose();}
   }
  }
  public string Destination(Frame f){
   bool cal=f.Kind.StartsWith("Master")||new[]{"Dark","Flat","Bias"}.Contains(f.Kind);string settings=Util.Safe(Util.Num(f.Exposure)+"s_gain"+Util.Num(f.Gain)+"_bin"+f.BinX+"x"+f.BinY);
   string mount=f.Mount.StartsWith("EQ")?"EQ":f.Mount.StartsWith("Alt/Az")?"AltAz":"Unknown";
   string p=cal?Path.Combine("Calibration",Util.Safe(f.MakeText),Util.Safe(f.Telescope),Util.Safe(f.Camera),Util.Safe(f.Kind),Util.Safe(f.Night),settings):Path.Combine("Targets",Util.Safe(f.Target),Util.Safe(f.Night),Util.Safe(f.MakeText),Util.Safe(f.Telescope),mount,Util.Safe(f.Camera),Util.Safe(f.Kind),settings);
   string filename=f.Hash.Substring(0,12)+"_"+Util.SafeFile(f.OriginalName);return Path.Combine(p,filename);
  }
  public static void CopyVerified(string from,string to,string hash,CancellationToken ct){string copied=FileTransfer.CopyHash(from,to,ct,null,false);if(!string.IsNullOrEmpty(hash)&&copied!=hash)throw new IOException("Checksum mismatch; source may have changed during copying: "+from);if(Util.Hash(to,ct)!=copied)throw new IOException("Destination checksum mismatch: "+to);}
  public int Verify(CancellationToken ct,Action<ProgressInfo> progress){var all=All();int bad=0;for(int i=0;i<all.Count;i++){ct.ThrowIfCancellationRequested();var f=all[i];progress(new ProgressInfo{Done=i,Total=all.Count,Text="Verifying "+f.OriginalName});string p=FilePath(f);if(!File.Exists(p)){f.Status="Missing";bad++;}else if(Util.Hash(p,ct)!=f.Hash){f.Status="Changed";bad++;}else f.Status="Verified";Save(f);}return bad;}
  public void ExportIndex(string path){var all=All();StringBuilder b=new StringBuilder("Target,Make,Model,MakeEvidence,TargetEvidence,Telescope,Camera,Kind,Mount,MountEvidence,Observed,TimeSource,Exposure_s,Gain,Temperature_C,Filter,Calibration,Dimensions,Hash,RelativePath,SourceDisposition\r\n");foreach(var f in all){string[] a={f.Target,f.MakeText,f.Model,f.MakeEvidence,f.TargetEvidence,f.Telescope,f.Camera,f.Kind,f.Mount,f.MountEvidence,f.Observed,f.TimeSource,Util.Num(f.Exposure),Util.Num(f.Gain),Util.Num(f.Temperature),f.Filter,f.Calibration,f.SizeText,f.Hash,f.RelativePath,f.SourceDisposition};b.AppendLine(string.Join(",",a.Select(s=>"\""+(s??"").Replace("\"","\"\"")+"\"")));}File.WriteAllText(path,b.ToString(),new UTF8Encoding(true));}
 }
}
