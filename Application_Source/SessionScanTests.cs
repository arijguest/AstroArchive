using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void SessionScanTests(){
   Test("Session inventory requires a durable import, physical volume and telescope identity",()=>{
    var stamp=new FileStamp{Identity="ABCD1234:0001",Size=100,Created=1,Modified=2};var frame=new Frame{Hash="hash",Telescope="Renamed scope",TelescopeIdentity="stable-scope",Model="Dwarf 3",ClassificationVersion=Assets.ClassificationVersion};var old=new SourceManifest{Status="Complete",Hash=frame.Hash,Source=stamp,Metadata=frame};var entry=new ScanEntry{Enumerated=stamp.Clone()};entry.Enumerated.Identity="";
    Func<bool> matches=()=>SessionScanCache.Matches(old,frame,entry,null,"ABCD1234","stable-scope","Auto");Check(matches(),"FAT inventory did not match imported session");
    Check(!SessionScanCache.Matches(old,frame,entry,null,"1234ABCD","stable-scope","Auto")&&!SessionScanCache.Matches(old,frame,entry,null,"ABCD1234","another-scope","Auto")&&!SessionScanCache.Matches(old,frame,entry,null,null,"stable-scope","Auto"),"Different or unknown device reused inventory");
    foreach(string status in new[]{"Copying","Failed","Deleted"}){old.Status=status;Check(!matches(),"Incomplete/deleted import skipped");}old.Status="Complete";
    old.Source=null;Check(!matches(),"Missing source stamp skipped");old.Source=stamp;
    Check(!SessionScanCache.Matches(old,null,entry,null,"ABCD1234","stable-scope","Auto"),"Missing archive index skipped");
    frame.ClassificationVersion="old";Check(!matches(),"Obsolete classification skipped");frame.ClassificationVersion=Assets.ClassificationVersion;
    Check(!SessionScanCache.Matches(old,frame,entry,null,"ABCD1234","stable-scope","Seestar S50"),"Changed model override skipped");
    entry.Enumerated.Modified++;Check(!matches(),"Modified source skipped");entry.Enumerated.Modified--;
    entry.Enumerated.Size++;Check(!matches(),"Changed size skipped");entry.Enumerated.Size--;
    entry.Enumerated.Attributes=0x400000;Check(!matches(),"Cloud placeholder skipped");
   });
   Test("Reliable session inventory catches same-size rewrites with restored timestamps",()=>{
    var stamp=new FileStamp{Identity="ABCD1234:0001",Size=100,Created=1,Modified=2,Changed=3,Reliable=true};var frame=new Frame{Hash="hash",TelescopeIdentity="scope",ClassificationVersion=Assets.ClassificationVersion};var old=new SourceManifest{Status="Complete",Hash=frame.Hash,Source=stamp,Metadata=frame};var entry=new ScanEntry{Enumerated=stamp.Clone()};entry.Enumerated.Identity="";entry.Enumerated.Changed=0;entry.Enumerated.Reliable=false;var current=stamp.Clone();
    Check(SessionScanCache.Matches(old,frame,entry,current,"ABCD1234","scope","Auto"),"Trusted unchanged source missed");current.Changed++;Check(!SessionScanCache.Matches(old,frame,entry,current,"ABCD1234","scope","Auto"),"Restored mtime concealed rewrite");
   });
   Test("Session discovery skips filesystem bookkeeping and keeps unknown session names",()=>{
    foreach(string name in new[]{"$RECYCLE.BIN","System Volume Information",".Spotlight-V100",".Trashes",".ASTROARCHIVE"})Check(SessionScanCache.SystemFolder(name),"System folder traversed");Check(!SessionScanCache.SystemFolder("DWARF_RAW_20261008")&&!SessionScanCache.SystemFolder("MyWorks"),"Capture folder excluded");
   });
   WindowsTest("Incremental scan reads only new files in existing and nested session folders",()=>{
    string source=Path.Combine(root,"incremental-source"),session=Path.Combine(source,"DWARF_RAW_M33_20261007");Directory.CreateDirectory(session);
    for(int i=0;i<100;i++){int pixel=1200+i;Write(Path.Combine(session,"Light_"+i.ToString("000")+".fit"),64,48,(x,y)=>pixel,new Dictionary<string,string>());}
    using(var repo=new Repository(Path.Combine(root,"incremental-repo"))){var initial=repo.Scan(source,"Scope","Auto",ct,NoProgress);Check(repo.Import(initial.Frames,ct,NoProgress).Imported==100,"Fixture imports failed");
     Write(Path.Combine(session,"Light_100.fit"),64,48,(x,y)=>2200,new Dictionary<string,string>());string nested=Path.Combine(session,"later-subframes");Directory.CreateDirectory(nested);Write(Path.Combine(nested,"Light_101.fit"),64,48,(x,y)=>2300,new Dictionary<string,string>());
     int emitted=0;var scan=repo.Scan(source,"Scope","Auto",ct,NoProgress,onFrame:f=>emitted++,deferHash:true,quickScan:true);Check(scan.FastSkippedFiles==100&&scan.FastSkippedFolders==1&&scan.Frames.Count==2&&emitted==2&&scan.Errors.Count==0,"Old rows rebuilt or new nested frames missed");
     Check(scan.Metrics.Snapshot().Single(s=>s.Stage=="Header open/read").Bytes==2*2880,"Old headers were reread");Check(scan.Metrics.Progress().Done==102&&scan.Metrics.Progress().Total==102,"Skipped inventory missing from progress");
     var full=repo.Scan(source,"Scope","Auto",ct,NoProgress,deferHash:true,quickScan:true,fullScan:true);Check(full.FastSkippedFiles==0&&full.MetadataCacheHits==0&&full.Frames.Count==102&&full.Frames.Count(f=>f.Status.StartsWith("Duplicate"))==100,"Full scan did not read and hash all sources");Check(full.Metrics.Snapshot().Single(s=>s.Stage=="Header open/read").Bytes==102*2880,"Full rescan reused old headers");
     var imported=repo.Import(scan.Frames,ct,NoProgress,new ImportOptions{SourceRoot=source,DeleteOriginals=true});Check(imported.Imported==2&&imported.OriginalsDeleted==2&&Directory.GetFiles(session,"*.fit").Length==100,"Skipped originals entered cleanup selection");
    }
   });
   WindowsTest("Session inventory rechecks changed sidecars, missing copies and source edits",()=>{
    string source=Path.Combine(root,"incremental-metadata"),session=Path.Combine(source,"MyWorks","M33_20261008");Directory.CreateDirectory(session);string path=Path.Combine(session,"Light_0001.fit"),shots=Path.Combine(source,"shotsInfo.json");Write(path,64,48,(x,y)=>1500,new Dictionary<string,string>());File.WriteAllText(shots,"{\"targetName\":\"M33\"}");
    using(var repo=new Repository(Path.Combine(root,"incremental-metadata-repo"))){repo.Import(repo.Scan(source,"Scope","Auto",ct,NoProgress).Frames,ct,NoProgress);Func<ImportPlan> scan=()=>repo.Scan(source,"Scope","Auto",ct,NoProgress,quickScan:true);
     Check(scan().FastSkippedFiles==1,"Unchanged session not skipped");File.WriteAllText(Path.Combine(session,"shotsInfo.json"),"{\"targetName\":\"M45\"}");Check(scan().FastSkippedFiles==0,"New nearer session metadata ignored");File.Delete(Path.Combine(session,"shotsInfo.json"));
     File.WriteAllText(Path.Combine(session,"session.json"),"{\"target\":\"M33\"}");Check(scan().FastSkippedFiles==0,"New associated sidecar ignored");File.Delete(Path.Combine(session,"session.json"));
     DateTime modified=File.GetLastWriteTimeUtc(shots);System.Threading.Thread.Sleep(40);File.WriteAllText(shots,"{\"targetName\":\"M45\"}");File.SetLastWriteTimeUtc(shots,modified);Check(scan().FastSkippedFiles==0,"Same-size restored-mtime sidecar edit ignored");File.Delete(shots);Check(scan().FastSkippedFiles==0,"Deleted session metadata ignored");
     var saved=repo.All().Single();File.Delete(repo.FilePath(saved));var missing=scan();Check(missing.FastSkippedFiles==0&&missing.Frames.Single().Status=="Restore","Missing archive copy skipped");repo.Import(missing.Frames,ct,NoProgress);
     modified=File.GetLastWriteTimeUtc(path);System.Threading.Thread.Sleep(40);Write(path,64,48,(x,y)=>2500,new Dictionary<string,string>());File.SetLastWriteTimeUtc(path,modified);var changed=scan();Check(changed.FastSkippedFiles==0&&changed.Frames.Single().Status=="New","Changed source skipped");
    }
   });
   WindowsTest("USB repeat upload reports skipped imports without transferring them",()=>{
    string source=Path.Combine(root,"incremental-usb"),session=Path.Combine(source,"DWARF_RAW_20261008");Directory.CreateDirectory(session);Write(Path.Combine(session,"Light_M33.fit"),64,48,(x,y)=>1700,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"incremental-usb-repo"))){var profile=new TelescopeProfile{Id="USB Scope",Model="Dwarf 3"};repo.Import(repo.Scan(source,profile.Id,profile.Model,ct,NoProgress).Frames,ct,NoProgress);var result=UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress);Check(result.Plan.FastSkippedFiles==1&&result.Plan.Frames.Count==0&&result.Import.Imported==0&&result.Import.Duplicates==1&&result.Summary.Contains("1 skipped by session inventory")&&Directory.GetFiles(session,"*.fit").Length==1,"Repeated USB upload did unnecessary work or lost accounting");}
   });
  }
 }
}
