// Discovers completed backups of one archive in a backup folder, applies retention
// without breaking incremental ZIP chains, and restores any backup with verification.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace AstroArchive {
 public sealed class BackupSet {
  public string Name,Path;public bool Compressed;public BackupManifest Manifest;public DateTime CreatedUtc;
  public Dictionary<string,BackupFile> Files;public List<string> Chain;
  public bool Incremental{get{return Manifest.Kind=="Incremental";}}
 }
 public static class BackupSchedule {
  public static readonly string[] Frequencies={"Off","Daily","Weekly","Monthly"};
  public static TimeSpan Interval(string frequency){return frequency=="Daily"?TimeSpan.FromDays(1):frequency=="Weekly"?TimeSpan.FromDays(7):frequency=="Monthly"?TimeSpan.FromDays(30):TimeSpan.MaxValue;}
  // A little slack keeps a daily backup from drifting later each day; a clock moved back never blocks backups.
  public static bool Due(string frequency,string lastUtc,DateTime utcNow){
   if(frequency==null||frequency=="Off"||!Frequencies.Contains(frequency))return false;DateTime last;
   if(!DateTime.TryParse(lastUtc,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out last))return true;
   return utcNow-last>=Interval(frequency)-TimeSpan.FromMinutes(30)||last>utcNow.AddDays(1);
  }
 }
 public sealed class BackupRestoreResult {public string Repository;public int Files;public long Bytes;public int Sets;}
 public static class BackupSets {
  public const string Prefix="AstroArchive-backup-";
  static bool Candidate(string name){return name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase)&&name.IndexOf(".partial",StringComparison.OrdinalIgnoreCase)<0;}
  public static BackupManifest ReadManifest(string path){
   if(File.Exists(path)&&path.EndsWith(".zip",StringComparison.OrdinalIgnoreCase)){
    using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var zip=new ZipArchive(input,ZipArchiveMode.Read)){var entry=zip.GetEntry("backup.json");if(entry==null)throw new InvalidDataException("This ZIP is not an AstroArchive backup: "+System.IO.Path.GetFileName(path));using(var stream=entry.Open())using(var reader=new StreamReader(stream))return Util.Deserialize<BackupManifest>(reader.ReadToEnd());}
   }
   string folder=File.Exists(path)&&System.IO.Path.GetFileName(path).Equals("backup.json",StringComparison.OrdinalIgnoreCase)?System.IO.Path.GetDirectoryName(path):path;
   string manifest=System.IO.Path.Combine(folder,"backup.json");if(!File.Exists(manifest))throw new InvalidDataException("This folder is not an AstroArchive backup: "+folder);
   return Util.Deserialize<BackupManifest>(File.ReadAllText(manifest));
  }
  // Completed backups of this archive, oldest first. Unreadable or foreign backups are ignored, never touched.
  public static List<BackupSet> List(string destination,string repositoryId,CancellationToken ct){
   var result=new List<BackupSet>();if(string.IsNullOrEmpty(repositoryId)||!Directory.Exists(destination))return result;
   var paths=Directory.EnumerateDirectories(destination).Concat(Directory.EnumerateFiles(destination,"*.zip"));
   foreach(string path in paths){
    ct.ThrowIfCancellationRequested();string name=System.IO.Path.GetFileName(path);if(!Candidate(name))continue;
    try{
     var manifest=ReadManifest(path);DateTime created;
     if(manifest==null||manifest.Files==null||manifest.RepositoryId!=repositoryId||!DateTime.TryParse(manifest.CreatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out created))continue;
     bool zip=File.Exists(path);var files=new Dictionary<string,BackupFile>(StringComparer.OrdinalIgnoreCase);foreach(var file in manifest.Files)if(file!=null&&file.Path!=null)files[file.Path]=file;
     result.Add(new BackupSet{Name=name,Path=path,Compressed=zip,Manifest=manifest,CreatedUtc=created,Files=files,Chain=zip?(manifest.Chain!=null&&manifest.Chain.Count>0?manifest.Chain.ToList():new List<string>{name}):new List<string>()});
    }catch(Exception e){if(e is OperationCanceledException)throw;if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException))throw;}
   }
   return result.OrderBy(s=>s.CreatedUtc).ThenBy(s=>s.Name,StringComparer.OrdinalIgnoreCase).ToList();
  }
  // The newest complete backup of the same format; a ZIP chain with a missing member cannot be a base.
  public static BackupSet Latest(string destination,string repositoryId,bool compressed,CancellationToken ct){
   var sets=List(destination,repositoryId,ct).Where(s=>s.Compressed==compressed).ToList();if(sets.Count==0)return null;var latest=sets[sets.Count-1];
   if(compressed&&latest.Chain.Any(name=>!File.Exists(System.IO.Path.Combine(destination,name))))return null;
   return latest;
  }
  public static bool Unchanged(BackupFile prior,FileStamp stamp){
   return prior!=null&&stamp!=null&&!string.IsNullOrEmpty(prior.Hash)&&prior.Bytes==stamp.Size&&prior.ModifiedUtcTicks==stamp.Modified&&prior.CreatedUtcTicks==stamp.Created&&(prior.ChangedUtcTicks==0||!stamp.Reliable||prior.ChangedUtcTicks==stamp.Changed);
  }
  // Keeps the newest backups plus every earlier ZIP they still need; deletes only this archive's backups.
  public static List<string> Prune(string destination,string repositoryId,int keep,CancellationToken ct){
   var deleted=new List<string>();if(keep<=0)return deleted;var sets=List(destination,repositoryId,ct);if(sets.Count<=keep)return deleted;
   var kept=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var set in sets.Skip(sets.Count-keep)){kept.Add(set.Name);foreach(string member in set.Chain)kept.Add(member);}
   foreach(var set in sets){
    ct.ThrowIfCancellationRequested();if(kept.Contains(set.Name))continue;
    try{if(set.Compressed)File.Delete(set.Path);else Directory.Delete(set.Path,true);deleted.Add(set.Name);}catch(IOException){}catch(UnauthorizedAccessException){}
   }
   return deleted;
  }
  static string Inside(string root,string relative){
   if(string.IsNullOrEmpty(relative)||!relative.StartsWith("Repository/",StringComparison.Ordinal))throw new InvalidDataException("Unexpected path in backup: "+relative);
   string part=relative.Substring("Repository/".Length);if(part.Length==0||System.IO.Path.IsPathRooted(part)||part.Split('/').Any(p=>p==".."||p=="."||p.Length==0))throw new InvalidDataException("Unsafe path in backup: "+relative);
   string full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,part.Replace('/',System.IO.Path.DirectorySeparatorChar)));if(!Util.Within(full,root))throw new InvalidDataException("Unsafe path in backup: "+relative);return full;
  }
  // Rebuilds the Repository folder from a folder backup or a ZIP (with any earlier ZIPs it
  // builds on), checking every file's size and SHA-256 before it is kept.
  public static BackupRestoreResult Restore(string backup,string target,CancellationToken ct,Action<ProgressInfo> progress){
   var manifest=ReadManifest(backup);if(manifest==null||manifest.Files==null||manifest.Files.Count==0)throw new InvalidDataException("This backup lists no files.");
   bool zip=File.Exists(backup)&&backup.EndsWith(".zip",StringComparison.OrdinalIgnoreCase);string folder=zip?System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(backup)):File.Exists(backup)?System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(backup)):System.IO.Path.GetFullPath(backup);
   target=System.IO.Path.GetFullPath(target);
   // A folder backup is a snapshot: never restore inside it. Next to ZIP backups is fine.
   if((zip?string.Equals(target.TrimEnd('\\','/'),folder.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase):Util.Within(target,folder))||Util.Within(folder,target))throw new IOException("Restore into a new folder outside the backup.");
   if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any())throw new IOException("Choose an empty folder for the restored archive.");
   if(zip)foreach(string set in manifest.Files.Select(f=>f.Set).Where(s=>s!=null).Distinct(StringComparer.OrdinalIgnoreCase))if(!File.Exists(System.IO.Path.Combine(folder,set)))throw new IOException("This backup builds on "+set+", which is not in the same folder. Put the earlier backups next to it and try again.");
   bool created=!Directory.Exists(target);Directory.CreateDirectory(target);
   var archives=new Dictionary<string,ZipArchive>(StringComparer.OrdinalIgnoreCase);var streams=new List<Stream>();long total=manifest.Files.Sum(f=>f.Bytes),done=0;int count=0;DateTime last=DateTime.MinValue;bool complete=false;
   try{
    foreach(var file in manifest.Files){
     ct.ThrowIfCancellationRequested();string destination=Inside(target,file.Path);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));string temp=destination+".partial";
     Stream input;
     if(zip){
      string set=file.Set??System.IO.Path.GetFileName(backup);ZipArchive archive;
      if(!archives.TryGetValue(set,out archive)){var stream=new FileStream(System.IO.Path.Combine(folder,set),FileMode.Open,FileAccess.Read,FileShare.Read);streams.Add(stream);archive=new ZipArchive(stream,ZipArchiveMode.Read);archives[set]=archive;}
      var entry=archive.GetEntry(file.Path);if(entry==null)throw new InvalidDataException(set+" does not contain "+file.Path+".");input=entry.Open();
     }else input=new FileStream(System.IO.Path.Combine(folder,file.Path.Replace('/',System.IO.Path.DirectorySeparatorChar)),FileMode.Open,FileAccess.Read,FileShare.Read);
     string hash;long length=0;
     using(input)using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var sha=System.Security.Cryptography.SHA256.Create())using(var lease=FileTransfer.Rent()){
      byte[] buffer=lease.Buffer;int n;while((n=input.Read(buffer,0,buffer.Length))>0){ct.ThrowIfCancellationRequested();output.Write(buffer,0,n);sha.TransformBlock(buffer,0,n,buffer,0);length+=n;done+=n;
       if(progress!=null&&(DateTime.UtcNow-last).TotalMilliseconds>=150){last=DateTime.UtcNow;progress(new ProgressInfo{Stage="Restoring backup",Text=file.Path,Done=count,Total=manifest.Files.Count,BytesDone=done,BytesTotal=total,TotalKnown=true,ProgressFraction=total==0?1:done/(double)total});}}
      sha.TransformFinalBlock(new byte[0],0,0);hash=BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();output.Flush(true);
     }
     if(length!=file.Bytes||hash!=file.Hash){Repository.TryRemove(temp);throw new InvalidDataException("Restore verification failed for "+file.Path+". The backup may be damaged.");}
     File.Move(temp,destination);
     try{if(file.ModifiedUtcTicks>0)File.SetLastWriteTimeUtc(destination,new DateTime(file.ModifiedUtcTicks,DateTimeKind.Utc));if(file.CreatedUtcTicks>0)File.SetCreationTimeUtc(destination,new DateTime(file.CreatedUtcTicks,DateTimeKind.Utc));}catch(ArgumentException){}
     count++;
    }
    complete=true;
    return new BackupRestoreResult{Repository=target,Files=count,Bytes=done,Sets=zip?Math.Max(1,archives.Count):1};
   }finally{
    foreach(var archive in archives.Values)archive.Dispose();foreach(var stream in streams)stream.Dispose();
    // The target was new or empty, so nothing of the user's is removed.
    if(!complete)try{if(created)Directory.Delete(target,true);else foreach(string entry in Directory.EnumerateFileSystemEntries(target).ToList()){if(Directory.Exists(entry))Directory.Delete(entry,true);else File.Delete(entry);}}catch(IOException){}catch(UnauthorizedAccessException){}
   }
  }
 }
}
