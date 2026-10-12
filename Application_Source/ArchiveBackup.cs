// Portable, byte-preserving backups. Only a completely verified backup is published.
// Incremental folder backups are still complete snapshots: unchanged files are NTFS
// hard links to the previous verified backup. Incremental ZIPs hold only new and
// changed files and name the earlier ZIPs they build on; Restore reassembles them.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
namespace AstroArchive {
 public class BackupFile {public string Path,Hash,Set;public long Bytes,ModifiedUtcTicks,CreatedUtcTicks,ChangedUtcTicks;}
 public class BackupManifest {
  public int Version=1;public string CreatedUtc,Archive;public List<BackupFile> Files=new List<BackupFile>();
  // Version 2: archive identity and incremental lineage. Chain lists ZIP sets, oldest first, ending with this one.
  public string RepositoryId,Kind,Base;public List<string> Chain;
 }
 public class BackupResult {public string Path;public int Files;public long Bytes;public bool Compressed;public bool Incremental;public int Copied,Reused;public long CopiedBytes;public string Base;public bool LinksUnavailable;}
 public sealed partial class Repository {
  const string BackupIdentityName="backup-origin.txt",RepositoryIdName="repository-id.txt";
  public const int MaxBackupChain=10;
  string BackupIdentity(){string path=Path.Combine(Meta,BackupIdentityName);if(!File.Exists(path))return "";Guid id;string text=File.ReadAllText(path).Trim();if(!Guid.TryParseExact(text,"N",out id))throw new InvalidDataException("The backup restore identity is invalid: "+path);return "|backup:"+text;}
  // A stable identity lets later backups recognise earlier backups of the same archive.
  public string RepositoryId(){
   string path=Path.Combine(Meta,RepositoryIdName);Guid id;
   if(File.Exists(path)&&Guid.TryParseExact(File.ReadAllText(path).Trim(),"N",out id))return id.ToString("N");
   string value=Guid.NewGuid().ToString("N");Util.AtomicText(path,value);return value;
  }
  sealed class BackupSource {public string Source,Relative;public FileStamp Stamp;public BackupFile Prior;}
  public static void ValidateBackupDestination(string destination,string root){
   string full=Path.GetFullPath(destination);if(Util.Within(full,root))throw new IOException("Choose a backup folder outside the archive. A separate drive is best.");
   string parent=full;while(parent!=null){if(Directory.Exists(parent)&&!FileStamp.CanTraverse(new DirectoryInfo(parent)))throw new IOException("Choose a backup folder without linked directories: "+parent);parent=Path.GetDirectoryName(parent);}
  }
  bool BackupExcluded(string relative){string portable=relative.Replace('\\','/').ToLowerInvariant();return portable==".astroarchive/"+BackupIdentityName||portable==".astroarchive/index.sqlite"||portable.StartsWith(".astroarchive/index.sqlite.",StringComparison.OrdinalIgnoreCase)||portable.StartsWith(".astroarchive/index.sqlite-",StringComparison.OrdinalIgnoreCase)||portable.StartsWith(".astroarchive/protection.",StringComparison.OrdinalIgnoreCase)||portable.StartsWith(".astroarchive/protection-rules.",StringComparison.OrdinalIgnoreCase)||portable==".astroarchive/staging"||portable.StartsWith(".astroarchive/staging/",StringComparison.OrdinalIgnoreCase);}
  List<BackupSource> BackupSources(CancellationToken ct){var files=new List<BackupSource>();var folders=new Stack<string>();folders.Push(Root);while(folders.Count>0){ct.ThrowIfCancellationRequested();string folder=folders.Pop();if(!FileStamp.CanTraverse(new DirectoryInfo(folder)))throw new IOException("Backup cannot follow a linked archive folder: "+folder);foreach(string path in Directory.EnumerateFileSystemEntries(folder)){ct.ThrowIfCancellationRequested();string relative=path.Substring(Root.TrimEnd('\\','/').Length+1);if(BackupExcluded(relative))continue;var attrs=File.GetAttributes(path);if((attrs&FileAttributes.Directory)!=0)folders.Push(path);else{if((attrs&FileAttributes.ReparsePoint)!=0&&!FileStamp.Read(path).Cloud)throw new IOException("Backup cannot follow a linked archive file: "+path);files.Add(new BackupSource{Source=path,Relative=relative.Replace('\\','/'),Stamp=FileStamp.Read(path)});}}}return files.OrderBy(f=>f.Relative,StringComparer.OrdinalIgnoreCase).ToList();}
  static string BackupHash(Stream input,Stream output,CancellationToken ct,Action<int> pulse){using(var lease=FileTransfer.Rent()){byte[] buffer=lease.Buffer;using(var hash=SHA256.Create()){int n;while((n=input.Read(buffer,0,buffer.Length))>0){ct.ThrowIfCancellationRequested();if(output!=null)output.Write(buffer,0,n);hash.TransformBlock(buffer,0,n,buffer,0);if(pulse!=null)pulse(n);}ct.ThrowIfCancellationRequested();hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();}}}
  const string RestoreInstructions="AstroArchive backup\r\n\r\nIf this is a ZIP, extract it first. The Repository folder contains your images, edited files and archive database in their original structure. In AstroArchive, choose this Repository folder in Settings > General > Repository to open the restored archive. You can also copy that folder to another location before opening it.\r\n\r\nImage files are preserved byte for byte. ZIP compression is lossless and does not change resolution, quality or metadata. backup.json lists every archived file with its size and SHA-256 checksum.\r\n\r\nBackups use ordinary permissions. To protect restored originals, enable protection in Settings > Backups. Keep another backup on a separate drive to protect against drive failure.\r\n";
  const string IncrementalZipInstructions="AstroArchive incremental backup\r\n\r\nThis ZIP holds only the files that were new or changed since the earlier backups it builds on. backup.json lists every file in the archive at this point, and \"Chain\" names the ZIP backups, oldest first, that together contain them. Keep those ZIPs in the same folder.\r\n\r\nTo restore, open AstroArchive, choose Settings > Backups > Restore a backup…, and select this ZIP. AstroArchive extracts each file from the right backup, verifies its SHA-256 checksum and recreates the Repository folder. Then choose that Repository folder in Settings > General.\r\n\r\nWithout AstroArchive: extract the ZIPs in the order listed in Chain into one folder, letting later files replace earlier ones, then delete any file under Repository that backup.json in this ZIP does not list.\r\n";
  const string IncrementalFolderInstructions="\r\nThis is an incremental backup: unchanged files are shared with earlier backups in this folder as NTFS hard links, so each backup folder is complete on its own and uses space only for changed files. Do not edit files inside backup folders; a change would affect every backup that shares the file.\r\n";
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateHardLink(string lpFileName,string lpExistingFileName,IntPtr lpSecurityAttributes);
  static string LongPath(string path){string full=Path.GetFullPath(path);return full.Length<240?full:full.StartsWith(@"\\")?@"\\?\UNC\"+full.Substring(2):@"\\?\"+full;}
  // 0 when linked; otherwise the Windows error. 1142 is the per-file NTFS link limit.
  static int HardLink(string target,string existing){
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return 50;
   try{return CreateHardLink(LongPath(target),LongPath(existing),IntPtr.Zero)?0:Math.Max(1,Marshal.GetLastWin32Error());}catch(EntryPointNotFoundException){return 50;}catch(DllNotFoundException){return 50;}
  }
  public BackupResult CreateBackup(string destination,bool compressed,CancellationToken ct,Action<ProgressInfo> progress){return CreateBackup(destination,compressed,ct,progress,false);}
  public BackupResult CreateBackup(string destination,bool compressed,CancellationToken ct,Action<ProgressInfo> progress,bool incremental){
   ValidateBackupDestination(destination,Root);if(!FileStamp.CanTraverse(new DirectoryInfo(Root)))throw new IOException("Choose an archive without linked root directories before backing up.");Directory.CreateDirectory(destination);
   string repositoryId=RepositoryId();
   string name="AstroArchive-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6),final=Path.Combine(destination,name+(compressed?".zip":"")),work=final+".partial",snapshot=Path.Combine(destination,"."+name+".index.partial");string identity=snapshot+".origin";long bytesDone=0,total=0;int done=0,count=0;DateTime last=DateTime.MinValue;string stage="Preparing backup",current="Saving current archive database";
   Action<bool> pulse=force=>{if(progress!=null&&(force||(DateTime.UtcNow-last).TotalMilliseconds>=150)){last=DateTime.UtcNow;progress(new ProgressInfo{Stage=stage,Text=current,Done=done,Total=count*2,BytesDone=bytesDone,BytesTotal=total,TotalKnown=count>0});}};
   // Only a complete, verified backup of this same archive and format can be a base.
   BackupSet previous=incremental?BackupSets.Latest(destination,repositoryId,compressed,ct):null;
   if(previous!=null&&compressed&&previous.Chain.Count>=MaxBackupChain)previous=null;
   var result=new BackupResult{Compressed=compressed,Incremental=previous!=null,Base=previous==null?null:previous.Name};
   try{
    ct.ThrowIfCancellationRequested();pulse(true);db.BackupTo(snapshot,ct);File.WriteAllText(identity,Guid.NewGuid().ToString("N"),new UTF8Encoding(false));var sources=BackupSources(ct);sources.Add(new BackupSource{Source=snapshot,Relative=".astroarchive/index.sqlite",Stamp=FileStamp.Read(snapshot)});sources.Add(new BackupSource{Source=identity,Relative=".astroarchive/"+BackupIdentityName,Stamp=FileStamp.Read(identity)});
    if(previous!=null)foreach(var file in sources){if(file.Source==snapshot||file.Source==identity)continue;BackupFile prior;if(previous.Files.TryGetValue("Repository/"+file.Relative,out prior)&&BackupSets.Unchanged(prior,file.Stamp))file.Prior=prior;}
    count=sources.Count;total=sources.Where(f=>f.Prior==null).Sum(f=>f.Stamp.Size)*2;
    // Windows clocks can return the same instant for successive small backups.
    // Keep their manifest times increasing so discovery and retention do not
    // choose an older snapshot by its random filename suffix.
    DateTime createdUtc=DateTime.UtcNow;var newest=BackupSets.List(destination,repositoryId,ct).LastOrDefault();
    if(newest!=null&&createdUtc<=newest.CreatedUtc)createdUtc=newest.CreatedUtc.AddTicks(1);
    var manifest=new BackupManifest{Version=2,CreatedUtc=createdUtc.ToString("o"),Archive=Path.GetFileName(Root.TrimEnd('\\','/')),RepositoryId=repositoryId,Kind=previous==null?"Full":"Incremental",Base=previous==null?null:previous.Name};
    if(compressed)manifest.Chain=(previous==null?new List<string>():previous.Chain.ToList()).Concat(new[]{name+".zip"}).ToList();
    stage="Creating backup";bool linksAvailable=true;var linked=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    Func<BackupSource,BackupFile> entry=file=>new BackupFile{Path="Repository/"+file.Relative,Bytes=file.Stamp.Size,ModifiedUtcTicks=file.Stamp.Modified,CreatedUtcTicks=file.Stamp.Created,ChangedUtcTicks=file.Stamp.Reliable?file.Stamp.Changed:0};
    Action<BackupSource,Stream> copy=(file,output)=>{ct.ThrowIfCancellationRequested();current=file.Relative;pulse(true);FileStamp before=FileStamp.Read(file.Source);if(!before.ContentSame(file.Stamp))throw new IOException("A file changed while preparing the backup: "+file.Relative);string hash;using(var input=new FileStream(file.Source,FileMode.Open,FileAccess.Read,FileShare.Read))hash=BackupHash(input,output,ct,n=>{bytesDone+=n;pulse(false);});if(!before.ContentSame(FileStamp.Read(file.Source)))throw new IOException("A file changed while backing up: "+file.Relative);var record=entry(file);record.Hash=hash;manifest.Files.Add(record);result.Copied++;result.CopiedBytes+=before.Size;done++;};
    // An unchanged file keeps the previous verified checksum; ZIP sets record where its bytes live.
    Action<BackupSource> reuse=file=>{var record=entry(file);record.Hash=file.Prior.Hash;record.Set=compressed?file.Prior.Set??previous.Name:null;manifest.Files.Add(record);result.Reused++;done++;};
    if(compressed){string instructions=previous==null?RestoreInstructions:IncrementalZipInstructions;using(var output=new FileStream(work,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){foreach(var file in sources){if(file.Prior!=null){reuse(file);continue;}var zipEntry=zip.CreateEntry("Repository/"+file.Relative,CompressionLevel.Optimal);DateTime date=new DateTime(file.Stamp.Modified,DateTimeKind.Utc).ToLocalTime();zipEntry.LastWriteTime=new DateTimeOffset(date.Year<1980||date.Year>2107?new DateTime(1980,1,1):date);using(var entryStream=zipEntry.Open())copy(file,entryStream);}WriteBackupEntry(zip,"backup.json",Util.Serialize(manifest));WriteBackupEntry(zip,"Restore.txt",instructions);}output.Flush(true);}}
    else{
     Directory.CreateDirectory(work);
     foreach(var file in sources){
      string target=Path.Combine(work,"Repository",file.Relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(target));
      if(file.Prior!=null){
       ct.ThrowIfCancellationRequested();current=file.Relative;pulse(true);string existing=Path.Combine(previous.Path,file.Prior.Path.Replace('/',Path.DirectorySeparatorChar));
       // A damaged earlier backup, the NTFS link limit, or a drive without hard links (FAT, exFAT,
       // some shares) falls back to a normal verified copy of that file.
       if(linksAvailable&&File.Exists(existing)&&new FileInfo(existing).Length==file.Prior.Bytes){int error=HardLink(target,existing);if(error==0){reuse(file);linked.Add(file.Relative);continue;}if(error!=1142){linksAvailable=false;result.LinksUnavailable=true;}TryRemove(target);}
       total+=file.Stamp.Size*2;
      }
      using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){copy(file,output);output.Flush(true);}File.SetLastWriteTimeUtc(target,new DateTime(file.Stamp.Modified,DateTimeKind.Utc));
     }
     result.Incremental=result.Reused>0;manifest.Kind=result.Incremental?"Incremental":"Full";if(!result.Incremental)manifest.Base=null;
     Util.AtomicText(Path.Combine(work,"backup.json"),Util.Serialize(manifest));Util.AtomicText(Path.Combine(work,"Restore.txt"),RestoreInstructions+(result.Incremental?IncrementalFolderInstructions:""));
    }
    stage="Verifying backup";
    Action<BackupFile,Stream> verify=(file,input)=>{ct.ThrowIfCancellationRequested();current=file.Path;pulse(true);long length=0;string hash=BackupHash(input,null,ct,n=>{length+=n;bytesDone+=n;pulse(false);});if(hash!=file.Hash||length!=file.Bytes)throw new InvalidDataException("Backup verification failed: "+file.Path);done++;};
    if(compressed){
     using(var input=new FileStream(work,FileMode.Open,FileAccess.Read,FileShare.Read))using(var zip=new ZipArchive(input,ZipArchiveMode.Read)){int stored=manifest.Files.Count(f=>f.Set==null);if(zip.Entries.Count!=stored+2)throw new InvalidDataException("Backup contents are incomplete.");VerifyBackupText(zip,"backup.json",Util.Serialize(manifest));VerifyBackupText(zip,"Restore.txt",previous==null?RestoreInstructions:IncrementalZipInstructions);foreach(var file in manifest.Files.Where(f=>f.Set==null)){var zipEntry=zip.GetEntry(file.Path);if(zipEntry==null||zipEntry.Length!=file.Bytes)throw new InvalidDataException("Backup entry is missing: "+file.Path);using(var stream=zipEntry.Open())verify(file,stream);}}
     // Earlier sets must still hold every reused file before this one is published.
     foreach(var set in manifest.Files.Where(f=>f.Set!=null).GroupBy(f=>f.Set,StringComparer.OrdinalIgnoreCase)){ct.ThrowIfCancellationRequested();string path=Path.Combine(destination,set.Key);if(!File.Exists(path))throw new IOException("An earlier backup this one builds on is missing: "+set.Key);using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var zip=new ZipArchive(input,ZipArchiveMode.Read))foreach(var file in set){var zipEntry=zip.GetEntry(file.Path);if(zipEntry==null||zipEntry.Length!=file.Bytes)throw new InvalidDataException("An earlier backup no longer holds "+file.Path+": "+set.Key);done++;}}
    }
    else{
     if(File.ReadAllText(Path.Combine(work,"backup.json"))!=Util.Serialize(manifest)||File.ReadAllText(Path.Combine(work,"Restore.txt"))!=RestoreInstructions+(result.Incremental?IncrementalFolderInstructions:""))throw new InvalidDataException("Backup recovery files could not be verified.");
     // A hard-linked file is the previous backup's verified bytes; rereading it would double the work.
     foreach(var file in manifest.Files){string path=Path.Combine(work,file.Path.Replace('/',Path.DirectorySeparatorChar));if(linked.Contains(file.Path.Substring("Repository/".Length))){if(new FileInfo(path).Length!=file.Bytes)throw new InvalidDataException("Backup verification failed: "+file.Path);done++;continue;}using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))verify(file,input);}
    }
    // Refuse a success message if a source changed or a new file appeared during the copy.
    foreach(var file in sources){ct.ThrowIfCancellationRequested();if(!file.Stamp.ContentSame(FileStamp.Read(file.Source)))throw new IOException("The archive changed during backup: "+file.Relative);}
    var after=BackupSources(ct);if(!sources.Where(f=>f.Source!=snapshot&&f.Source!=identity).Select(f=>f.Relative).SequenceEqual(after.Select(f=>f.Relative),StringComparer.OrdinalIgnoreCase))throw new IOException("Files were added or removed while the backup was being created. Try again.");
    ct.ThrowIfCancellationRequested();if(compressed)File.Move(work,final);else Directory.Move(work,final);stage="Backup complete";current="Verified "+manifest.Files.Count+" files";pulse(true);
    result.Path=final;result.Files=manifest.Files.Count;result.Bytes=manifest.Files.Sum(f=>f.Bytes);return result;
   }finally{TryRemove(identity);TryRemove(snapshot);TryRemove(snapshot+"-journal");TryRemove(work);if(Directory.Exists(work))try{Directory.Delete(work,true);}catch{}}
  }
  static void WriteBackupEntry(ZipArchive zip,string name,string text){using(var stream=zip.CreateEntry(name,CompressionLevel.Optimal).Open())using(var writer=new StreamWriter(stream,new UTF8Encoding(false)))writer.Write(text);}
  static void VerifyBackupText(ZipArchive zip,string name,string expected){var entry=zip.GetEntry(name);if(entry==null)throw new InvalidDataException("Missing backup recovery file: "+name);using(var stream=entry.Open())using(var reader=new StreamReader(stream))if(reader.ReadToEnd()!=expected)throw new InvalidDataException("Backup recovery file failed verification: "+name);}
 }
}
