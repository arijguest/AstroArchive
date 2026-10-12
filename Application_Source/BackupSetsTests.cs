using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void VerifyRestored(string repository,BackupManifest manifest){
   foreach(var file in manifest.Files){string path=Path.Combine(repository,file.Path.Substring("Repository/".Length).Replace('/',Path.DirectorySeparatorChar));Check(File.Exists(path)&&new FileInfo(path).Length==file.Bytes&&Util.Hash(path,ct)==file.Hash,"Restored bytes differ: "+file.Path);}
   using(var restored=new Repository(repository))Check(restored.All().Count==1,"Restored archive does not open");
  }
  static void BackupSetsTests(){
   Test("Repository size counts captures, Edited, Dump and archive data separately",()=>{
    using(var repo=SafetyFixture("repository-size")){
     string edited=Path.Combine(repo.EditedFolder,"p","final.tif");Directory.CreateDirectory(Path.GetDirectoryName(edited));File.WriteAllBytes(edited,new byte[3000]);
     repo.EnsureDumpFolder();File.WriteAllBytes(Path.Combine(repo.DumpFolder,"waiting.fit"),new byte[2880]);
     var size=RepositorySize.Measure(repo.Root,repo.Meta,repo.EditedFolder,repo.DumpFolder,ct);long all=Directory.EnumerateFiles(repo.Root,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length);
     Check(size.Total==all&&size.Edited==3000&&size.Dump==2880&&size.Captures==new FileInfo(repo.FilePath(repo.All().Single())).Length&&size.ArchiveData>0&&size.Total==size.Captures+size.Edited+size.Dump+size.ArchiveData&&!size.Partial,"Repository size categories wrong: "+size.Breakdown);
     Check(RepositorySize.Format(512)=="512 bytes"&&RepositorySize.Format(1536).StartsWith("1")&&RepositorySize.Format(5L*1024*1024*1024*1024).EndsWith("TB")&&size.Breakdown.Contains("Edited"),"Size formatting wrong");
    }
   });
   Test("Backup schedules are due by frequency, with slack, and never when off",()=>{
    var now=new DateTime(2026,10,12,20,0,0,DateTimeKind.Utc);Func<double,string> ago=hours=>now.AddHours(-hours).ToString("o");
    Check(!BackupSchedule.Due("Off",null,now)&&!BackupSchedule.Due("Hourly",null,now)&&BackupSchedule.Due("Daily",null,now),"Off, unknown or never-backed-up schedules wrong");
    Check(!BackupSchedule.Due("Daily",ago(20),now)&&BackupSchedule.Due("Daily",ago(23.6),now)&&!BackupSchedule.Due("Weekly",ago(24*6),now)&&BackupSchedule.Due("Weekly",ago(24*7),now)&&BackupSchedule.Due("Monthly",ago(24*30),now),"Frequency intervals wrong");
    Check(BackupSchedule.Due("Daily",now.AddDays(3).ToString("o"),now)&&BackupSchedule.Due("Daily","not a date",now),"Future or corrupt last-backup times blocked backups");
   });
   Test("Incremental folder backups copy only changes yet stay complete, independent snapshots",()=>{
    using(var repo=SafetyFixture("incremental-folder")){
     string destination=Path.Combine(root,"incremental-folder-backups");var full=repo.CreateBackup(destination,false,ct,NoProgress,true);
     Check(!full.Incremental&&full.Copied==full.Files&&full.Reused==0,"First backup in a folder was not complete");
     string edited=Path.Combine(repo.EditedFolder,"kept","M45_final.tif");Directory.CreateDirectory(Path.GetDirectoryName(edited));File.WriteAllBytes(edited,Enumerable.Range(0,50000).Select(i=>(byte)i).ToArray());
     var second=repo.CreateBackup(destination,false,ct,NoProgress,true);var manifest=BackupSets.ReadManifest(second.Path);
     Check(manifest.Version==2&&manifest.RepositoryId==repo.RepositoryId()&&manifest.Files.Count==second.Files&&manifest.Files.Any(f=>f.Path.EndsWith("M45_final.tif")),"Incremental manifest incomplete");
     if(Environment.OSVersion.Platform==PlatformID.Win32NT&&!second.LinksUnavailable)Check(second.Incremental&&second.Reused>0&&second.Copied<second.Files&&manifest.Kind=="Incremental"&&manifest.Base==Path.GetFileName(full.Path),"Unchanged files were copied again: "+second.Copied+" of "+second.Files);
     Directory.Delete(full.Path,true);VerifyRestored(Path.Combine(second.Path,"Repository"),manifest);
     var third=repo.CreateBackup(destination,false,ct,NoProgress,true);Check(BackupSets.List(destination,repo.RepositoryId(),ct).Count==2&&third.Files==second.Files,"Snapshot after deleting an earlier backup is incomplete");
     Check(BackupSets.Prune(destination,repo.RepositoryId(),1,ct).SequenceEqual(new[]{Path.GetFileName(second.Path)})&&Directory.Exists(third.Path)&&!Directory.Exists(second.Path),"Retention removed the wrong folder backup");
     VerifyRestored(Path.Combine(third.Path,"Repository"),BackupSets.ReadManifest(third.Path));
    }
   });
   Test("Incremental ZIP backups store only changes and restore the whole chain with verification",()=>{
    using(var repo=SafetyFixture("incremental-zip")){
     string destination=Path.Combine(root,"incremental-zip-backups");var full=repo.CreateBackup(destination,true,ct,NoProgress,true);
     string edited=Path.Combine(repo.EditedFolder,"kept","M45_final.tif");Directory.CreateDirectory(Path.GetDirectoryName(edited));File.WriteAllBytes(edited,Enumerable.Range(0,50000).Select(i=>(byte)(i*7)).ToArray());
     var second=repo.CreateBackup(destination,true,ct,NoProgress,true);var manifest=BackupSets.ReadManifest(second.Path);
     Check(second.Incremental&&second.Reused>0&&manifest.Kind=="Incremental"&&manifest.Chain.SequenceEqual(new[]{Path.GetFileName(full.Path),Path.GetFileName(second.Path)})&&manifest.Files.Where(f=>f.Set!=null).All(f=>f.Set==Path.GetFileName(full.Path)),"Incremental ZIP lineage wrong");
     using(var zip=ZipFile.OpenRead(second.Path)){Check(zip.Entries.Count==manifest.Files.Count(f=>f.Set==null)+2&&zip.GetEntry(manifest.Files.First(f=>f.Path.EndsWith("M45_final.tif")).Path)!=null&&!zip.Entries.Any(e=>e.FullName.EndsWith("Light_M45.fit")),"Incremental ZIP stored unchanged files or missed the new one");}
     string restored=Path.Combine(root,"incremental-zip-restored");var result=BackupSets.Restore(second.Path,restored,ct,NoProgress);Check(result.Files==manifest.Files.Count&&result.Sets==2,"Chain restore incomplete");VerifyRestored(restored,manifest);
     Expect(()=>BackupSets.Restore(second.Path,restored,ct,NoProgress),"Restore overwrote a non-empty folder");
     File.Delete(edited);var third=repo.CreateBackup(destination,true,ct,NoProgress,true);var latest=BackupSets.ReadManifest(third.Path);Check(!latest.Files.Any(f=>f.Path.EndsWith("M45_final.tif"))&&latest.Chain.Count==3,"Deleted file still listed or chain not extended");
     string afterDelete=Path.Combine(root,"incremental-zip-restored-after-delete");BackupSets.Restore(third.Path,afterDelete,ct,NoProgress);Check(!File.Exists(Path.Combine(afterDelete,"Edited","kept","M45_final.tif"))&&!Directory.EnumerateFiles(afterDelete,"*M45_final.tif",SearchOption.AllDirectories).Any(),"Restore resurrected a deleted file");
     Check(BackupSets.Prune(destination,repo.RepositoryId(),1,ct).Count==0&&File.Exists(full.Path),"Retention broke the chain the newest backup needs");
     File.Delete(full.Path);string broken=Path.Combine(root,"incremental-zip-broken");Expect(()=>BackupSets.Restore(third.Path,broken,ct,NoProgress),"Restore succeeded without an earlier chain member");Check(!Directory.Exists(broken),"Failed restore left files behind");
     var fresh=repo.CreateBackup(destination,true,ct,NoProgress,true);Check(!fresh.Incremental&&BackupSets.ReadManifest(fresh.Path).Kind=="Full","A broken chain was used as a base");
    }
   });
   Test("Incremental ZIP chains restart with a full backup at the chain limit and ignore other archives",()=>{
    using(var repo=SafetyFixture("incremental-chain"))using(var other=SafetyFixture("incremental-other")){
     string destination=Path.Combine(root,"incremental-chain-backups");BackupResult last=null;DateTime created=DateTime.MinValue;
     for(int i=0;i<Repository.MaxBackupChain;i++){
      last=repo.CreateBackup(destination,true,ct,NoProgress,true);var step=BackupSets.ReadManifest(last.Path);var time=DateTime.Parse(step.CreatedUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind);
      Check(time>created&&step.Chain.Count==i+1,"Successive backups were not ordered by creation: "+i);created=time;
     }
     Check(BackupSets.ReadManifest(last.Path).Chain.Count==Repository.MaxBackupChain,"Chain did not grow to its limit");
     var restart=repo.CreateBackup(destination,true,ct,NoProgress,true);Check(!restart.Incremental&&BackupSets.ReadManifest(restart.Path).Chain.Count==1,"Chain did not restart with a full backup");
     var foreign=other.CreateBackup(destination,true,ct,NoProgress,true);Check(!foreign.Incremental&&BackupSets.List(destination,repo.RepositoryId(),ct).Count==Repository.MaxBackupChain+1,"Another archive's backups were treated as this archive's");
     var pruned=BackupSets.Prune(destination,repo.RepositoryId(),1,ct);Check(pruned.Count==Repository.MaxBackupChain&&File.Exists(restart.Path)&&File.Exists(foreign.Path),"Retention deleted the wrong backups");
    }
   });
  }
 }
}
