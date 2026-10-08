using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static Repository SafetyFixture(string name){string source=Path.Combine(root,name+"-source");Directory.CreateDirectory(source);Write(Path.Combine(source,"Light_M45.fit"),64,48,(x,y)=>930,new Dictionary<string,string>());var repo=new Repository(Path.Combine(root,name+"-repo"));repo.Import(repo.Scan(source,"Safety scope","Auto",ct,NoProgress).Frames,ct,NoProgress);return repo;}
  static void SafetyCopyTree(string source,string destination){Directory.CreateDirectory(destination);foreach(string file in Directory.EnumerateFiles(source))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));foreach(string folder in Directory.EnumerateDirectories(source))SafetyCopyTree(folder,Path.Combine(destination,Path.GetFileName(folder)));}
  static string SafetyAcl(FileSystemSecurity acl){return acl.AreAccessRulesProtected+"|"+acl.GetOwner(typeof(SecurityIdentifier)).Value+"|"+string.Join(";",acl.GetAccessRules(true,true,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Select(r=>r.IdentityReference.Value+":"+r.AccessControlType+":"+(int)r.FileSystemRights+":"+r.InheritanceFlags+":"+r.PropagationFlags+":"+r.IsInherited).OrderBy(s=>s));}
  static void ArchiveSafetyTests(){
   Test("Folder and lossless ZIP backups restore the live index, original pixels and edited files",()=>{
    foreach(bool zip in new[]{false,true})using(var repo=SafetyFixture("backup-roundtrip-"+zip)){
     Frame frame=repo.All().Single();frame.Notes="Latest unsnapshotted metadata";repo.Save(frame);
     string edited=Path.Combine(repo.EditedFolder,"kept","M45_starless.gif");Directory.CreateDirectory(Path.GetDirectoryName(edited));byte[] bytes=Enumerable.Range(0,300000).Select(i=>(byte)(i*17)).ToArray();File.WriteAllBytes(edited,bytes);File.WriteAllText(Path.Combine(repo.Meta,"custom-metadata.json"),"preserve this metadata");Directory.CreateDirectory(Path.Combine(repo.Meta,"staging"));File.WriteAllText(Path.Combine(repo.Meta,"staging","unfinished.partial"),"not recovery data");
     var result=repo.CreateBackup(Path.Combine(root,"verified-backups"),zip,ct,NoProgress);Check(File.Exists(result.Path)||Directory.Exists(result.Path),"Verified backup was not published");string restored=zip?Path.Combine(root,"unzip-"+Guid.NewGuid().ToString("N")):result.Path;
     if(zip)ZipFile.ExtractToDirectory(result.Path,restored);var manifest=Util.Deserialize<BackupManifest>(File.ReadAllText(Path.Combine(restored,"backup.json")));Check(manifest.Files.Count==result.Files&&File.Exists(Path.Combine(restored,"Restore.txt")),"Recovery manifest/instructions missing");foreach(var file in manifest.Files){string path=Path.Combine(restored,file.Path.Replace('/',Path.DirectorySeparatorChar));Check(new FileInfo(path).Length==file.Bytes&&Util.Hash(path,ct)==file.Hash,"Backup bytes changed: "+file.Path);}
     Check(!Directory.Exists(Path.Combine(restored,"Repository",".astroarchive","staging")),"Incomplete staging files copied");
     using(var recovered=new Repository(Path.Combine(restored,"Repository"))){var actual=recovered.All().Single();Check(actual.Notes==frame.Notes&&actual.Hash==frame.Hash&&Util.Hash(recovered.FilePath(actual),ct)==frame.Hash,"Restored index or acquisition data is stale");Check(File.ReadAllBytes(Path.Combine(recovered.EditedFolder,"kept","M45_starless.gif")).SequenceEqual(bytes),"Edited GIF bytes changed");Check(!recovered.OriginalsProtected,"Restored backup claimed protection without ACLs");}
    }
   });
   Test("Canceled backups remove incomplete outputs and retain earlier backups",()=>{
    using(var repo=SafetyFixture("backup-cancel")){string destination=Path.Combine(root,"cancel-backups");var original=repo.CreateBackup(destination,false,ct,NoProgress);using(var stop=new CancellationTokenSource()){Expect(()=>repo.CreateBackup(destination,true,stop.Token,p=>{if(p.Stage=="Creating backup")stop.Cancel();}),"Canceled backup succeeded");}Check(Directory.GetDirectories(destination).SequenceEqual(new[]{original.Path})&&!Directory.EnumerateFiles(destination).Any(),"Cancellation damaged an existing backup or left temporary files");}
   });
   Test("Backups reject destinations inside the archive and source changes",()=>{
    using(var repo=SafetyFixture("backup-changes")){Expect(()=>repo.CreateBackup(Path.Combine(repo.Meta,"backup"),false,ct,NoProgress),"Backup recursed into its archive");string destination=Path.Combine(root,"changed-backups");bool changed=false;Expect(()=>repo.CreateBackup(destination,false,ct,p=>{if(!changed&&p.Stage=="Verifying backup"){changed=true;File.WriteAllText(Path.Combine(repo.Root,"new-file.txt"),"external change");}}),"Backup silently omitted files added during copy");Check(changed&&!Directory.EnumerateFileSystemEntries(destination).Any(),"Failed backup was marked complete or left partial files");}
   });
   Test("Restoring a backup to a previously opened path uses its database instead of a stale local index",()=>{
    using(var repo=SafetyFixture("backup-cache")){var result=repo.CreateBackup(Path.Combine(root,"cache-backups"),false,ct,NoProgress);string restored=Path.Combine(result.Path,"Repository");string identity=Path.Combine(restored,".astroarchive","backup-origin.txt"),token=File.ReadAllText(identity);File.Delete(identity);
     using(var old=new Repository(restored)){var frame=old.All().Single();frame.Notes="Stale local cached metadata";old.Save(frame);}
     File.Copy(Path.Combine(repo.Meta,"index.sqlite"),Path.Combine(restored,".astroarchive","index.sqlite"),true);File.WriteAllText(identity,token);
     using(var fresh=new Repository(restored))Check(fresh.All().Single().Notes!="Stale local cached metadata","Restore reused the previous archive index");
    }
   });
   WindowsTest("NTFS protection blocks Explorer-style deletion and rename, keeps working folders writable",()=>{
    using(var repo=SafetyFixture("protection-ntfs")){string path=repo.FilePath(repo.All().Single()),parent=Path.GetDirectoryName(path);var original=SafetyAcl(File.GetAccessControl(path));var originalParent=SafetyAcl(Directory.GetAccessControl(parent));
     try{repo.SetOriginalsProtection(true,ct,NoProgress);Check(repo.OriginalsProtected,"Protection not enabled");Expect(()=>File.Delete(path),"Protected file could be deleted through parent DELETE_CHILD");Expect(()=>File.Move(path,path+".renamed"),"Protected file could be renamed");Expect(()=>Directory.Delete(Path.Combine(repo.Root,"Targets"),true),"Protected originals could be deleted recursively");Check(Util.Hash(path,ct)==repo.All().Single().Hash,"Read access or original bytes changed");string copy=Path.Combine(root,"protection-copy.fit");File.Copy(path,copy);File.Delete(copy);
      foreach(string folder in new[]{repo.Meta,repo.EditedFolder,repo.DumpFolder}){Directory.CreateDirectory(folder);string working=Path.Combine(folder,"mutable.txt");File.WriteAllText(working,"working");File.Move(working,working+".renamed");File.Delete(working+".renamed");}
     }finally{repo.SetOriginalsProtection(false,ct,NoProgress);}Check(SafetyAcl(File.GetAccessControl(path))==original&&SafetyAcl(Directory.GetAccessControl(parent))==originalParent,"Disabling protection did not restore original rules: "+original+" => "+SafetyAcl(File.GetAccessControl(path))+" / "+originalParent+" => "+SafetyAcl(Directory.GetAccessControl(parent)));File.Move(path,path+".unprotected");File.Move(path+".unprotected",path);}
   });
   WindowsTest("Protected imports, metadata refiling, telescope renames and confirmed deletion succeed",()=>{
    using(var repo=SafetyFixture("protection-operations")){try{repo.SetOriginalsProtection(true,ct,NoProgress);var frame=repo.All().Single();frame.Target="M31";repo.Refile(frame,ct);Expect(()=>File.Delete(repo.FilePath(frame)),"Refiled capture lost protection");repo.RenameTelescope("Safety scope","Renamed scope",ct,NoProgress);frame=repo.All().Single();Expect(()=>File.Delete(repo.FilePath(frame)),"Renamed capture lost protection");
      string source=Path.Combine(root,"protected-second-source");Directory.CreateDirectory(source);Write(Path.Combine(source,"Light_M42.fit"),64,48,(x,y)=>1200,new Dictionary<string,string>());var imported=repo.Import(repo.Scan(source,"Renamed scope","Auto",ct,NoProgress).Frames,ct,NoProgress);Check(imported.Imported==1&&imported.Failed==0,"Import into protected archive failed");foreach(var f in repo.All())Expect(()=>File.Delete(repo.FilePath(f)),"New capture was not protected");var deleted=repo.DeleteFrames(repo.All(),ct,NoProgress);Check(deleted.Deleted==2&&deleted.Errors.Count==0&&repo.All().Count==0,"Confirmed deletion failed under protection");Check(repo.ResetArchive(ct,NoProgress).Count==0,"Empty protected folders prevented reset");
     }finally{repo.SetOriginalsProtection(false,ct,NoProgress);}}
   });
   WindowsTest("Canceling protection removes only managed rules and restores prior permissions",()=>{
    using(var repo=SafetyFixture("protection-cancel")){string path=repo.FilePath(repo.All().Single());string before=SafetyAcl(Directory.GetAccessControl(repo.Root));using(var stop=new CancellationTokenSource()){Expect(()=>repo.SetOriginalsProtection(true,stop.Token,p=>stop.Cancel()),"Canceled protection succeeded");}Check(!repo.OriginalsProtected&&SafetyAcl(Directory.GetAccessControl(repo.Root))==before,"Canceled protection retained managed rules");File.Move(path,path+".test");File.Move(path+".test",path);}
   });
   WindowsTest("Protection survives reopen, interrupted unlocks and lossless backup",()=>{
    string archive,path;using(var repo=SafetyFixture("protection-reopen")){archive=repo.Root;path=repo.FilePath(repo.All().Single());repo.SetOriginalsProtection(true,ct,NoProgress);var manager=new ArchiveProtection(repo.Root,repo.Meta);manager.Unlock(path,null);File.Move(path,path+".test");File.Move(path+".test",path);}
    using(var repo=new Repository(archive)){try{Check(repo.OriginalsProtected,"Protection setting lost on restart");Expect(()=>File.Delete(path),"Interrupted unlock was not repaired on reopen");var result=repo.CreateBackup(Path.Combine(root,"protected-backups"),true,ct,NoProgress);string restored=Path.Combine(root,"protected-restore");ZipFile.ExtractToDirectory(result.Path,restored);Check(!File.Exists(Path.Combine(restored,"Repository",".astroarchive","protection.json")),"Backup carried a stale ACL setting");Expect(()=>File.Delete(path),"Backup unlocked original capture");}finally{repo.SetOriginalsProtection(false,ct,NoProgress);}}
   });
   WindowsTest("Canceling a protected telescope rename restores filenames and protection",()=>{
    using(var repo=SafetyFixture("protection-rename-cancel")){string path=repo.FilePath(repo.All().Single());try{repo.SetOriginalsProtection(true,ct,NoProgress);using(var stop=new CancellationTokenSource()){Expect(()=>repo.RenameTelescope("Safety scope","Canceled name",stop.Token,p=>stop.Cancel()),"Canceled rename succeeded");}Check(repo.All().Single().Telescope=="Safety scope"&&File.Exists(path),"Canceled rename did not restore original metadata/path");Expect(()=>File.Delete(path),"Rename rollback lost deletion protection");}finally{repo.SetOriginalsProtection(false,ct,NoProgress);}}
   });
   WindowsTest("Copied archives reapply protection at their new location without absolute recovery paths",()=>{
    string original,copied=Path.Combine(root,"protection-copied-repo");using(var repo=SafetyFixture("protection-copy-root")){original=repo.Root;repo.SetOriginalsProtection(true,ct,NoProgress);}SafetyCopyTree(original,copied);
    try{using(var repo=new Repository(copied)){try{Check(repo.OriginalsProtected,"Copied archive lost its protection setting");Expect(()=>File.Delete(repo.FilePath(repo.All().Single())),"Copied originals were not protected");}finally{repo.SetOriginalsProtection(false,ct,NoProgress);}}}finally{using(var repo=new Repository(original))repo.SetOriginalsProtection(false,ct,NoProgress);}
   });
   WindowsTest("Unrelated explicit Windows rules are preserved when protection is removed",()=>{
    using(var repo=SafetyFixture("protection-existing")){string path=repo.FilePath(repo.All().Single());var acl=File.GetAccessControl(path);string sid;using(var user=WindowsIdentity.GetCurrent())sid=user.User.Value;var unrelated=new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.ExecuteFile,AccessControlType.Deny);acl.AddAccessRule(unrelated);File.SetAccessControl(path,acl);string before=SafetyAcl(File.GetAccessControl(path));try{repo.SetOriginalsProtection(true,ct,NoProgress);repo.SetOriginalsProtection(false,ct,NoProgress);Check(SafetyAcl(File.GetAccessControl(path))==before,"Unrelated deny rule was removed");}finally{repo.SetOriginalsProtection(false,ct,NoProgress);acl=File.GetAccessControl(path);acl.RemoveAccessRuleSpecific(unrelated);File.SetAccessControl(path,acl);}}
   });
  }
 }
}
