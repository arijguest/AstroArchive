// Repeat-import regressions use disposable archives and generated captures.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static ImportPlan FastScan(Repository repo,string source,string id="Scope"){return repo.Scan(source,id,"Auto",ct,NoProgress,deferHash:true,filenameMatching:true);}
  static void FastImportTests(){
   Test("Robust import matching is opt-in and survives saved settings",()=>{
    Check(!new Settings().RobustImportMatching&&!Util.Deserialize<Settings>("{}").RobustImportMatching,"Legacy settings enabled robust mode");
    Check(Util.Deserialize<Settings>(Util.Serialize(new Settings{RobustImportMatching=true})).RobustImportMatching,"Robust setting not saved");
   });
   Test("Fast DWARF repeat scan omits a known session and leaves its later files for robust review",()=>{
    string source=Path.Combine(root,"fast-dwarf"),session=Path.Combine(source,"DWARF_RAW_TELE_M45_2026-10-08-22-00-00");Directory.CreateDirectory(session);
    string file=Path.Combine(session,"raw_001.fit");Write(file,64,48,(x,y)=>1400,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"fast-dwarf-repo"))){
     repo.Import(repo.Scan(source,"Scope","Dwarf 3",ct,NoProgress).Frames,ct,NoProgress);
     File.WriteAllText(file,"edited bytes");string later=Path.Combine(session,"new_later.fit");Write(later,64,48,(x,y)=>1700,new Dictionary<string,string>());
     string nested=Path.Combine(session,"nested");Directory.CreateDirectory(nested);File.WriteAllText(Path.Combine(nested,"bad.fit"),"do not open");
     var scan=FastScan(repo,source);Check(scan.FastSessionFolders==1&&scan.Frames.Count==0&&scan.Errors.Count==0&&scan.Metrics.Snapshot().Sum(s=>s.Bytes)==0,"Known session contents were opened or walked");
     var full=repo.Scan(source,"Scope","Dwarf 3",ct,NoProgress,fullScan:true,filenameMatching:true);Check(full.FastSessionFolders==0&&full.Frames.Any(f=>f.OriginalName=="new_later.fit")&&full.Errors.Count==2,"Full rescan failed to revisit omitted folder");
     Check(File.Exists(file)&&File.Exists(later),"Fast scan removed originals");
    }
   });
   Test("New DWARF sessions with repeated raw_001 names remain candidates",()=>{
    string source=Path.Combine(root,"fast-new-dwarf"),first=Path.Combine(source,"DWARF_RAW_TELE_20261008"),second=Path.Combine(source,"DWARF_RAW_TELE_20261009");Directory.CreateDirectory(first);Write(Path.Combine(first,"raw_001.fit"),64,48,(x,y)=>1300,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"fast-new-dwarf-repo"))){repo.Import(repo.Scan(source,"Scope","Dwarf 3",ct,NoProgress).Frames,ct,NoProgress);Directory.CreateDirectory(second);Write(Path.Combine(second,"raw_001.fit"),64,48,(x,y)=>1500,new Dictionary<string,string>());var scan=FastScan(repo,source);Check(scan.FastSessionFolders==1&&scan.Frames.Count==1&&scan.Frames.Single().SourcePath.StartsWith(second),"A fresh session reused an old sequential filename");}
   });
   Test("Fast Seestar scan skips unchanged or edited filenames while finding new exposures",()=>{
    string source=Path.Combine(root,"fast-seestar","MyWorks","M45_sub");Directory.CreateDirectory(source);string old=Path.Combine(source,"Light_M45_20261008-220000.fit");Write(old,64,48,(x,y)=>1300,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"fast-seestar-repo"))){repo.Import(repo.Scan(source,"Scope","Seestar S50",ct,NoProgress).Frames,ct,NoProgress);File.WriteAllText(old,"edited content is deliberately ignored");Write(Path.Combine(source,"Light_M45_20261008-220100.fit"),64,48,(x,y)=>1500,new Dictionary<string,string>());var scan=FastScan(repo,source);Check(scan.FastSkippedFiles==1&&scan.FastSessionFolders==0&&scan.Frames.Count==1&&scan.Errors.Count==0&&scan.Metrics.Snapshot().Single(s=>s.Stage=="Header open/read").Bytes==2880,"Known payload reread or new Seestar exposure hidden");}
   });
   Test("Filename inventory follows mirrors moved roots and renamed generic folders",()=>{
    string source=Path.Combine(root,"fast-old-mirror");Directory.CreateDirectory(source);string name="Light_M45_20261008-220000.fit";Write(Path.Combine(source,name),64,48,(x,y)=>1300,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"fast-move-repo"))){repo.Import(repo.Scan(source,"Scope","Auto",ct,NoProgress).Frames,ct,NoProgress);string moved=Path.Combine(root,"fast-moved-card","another-target");Directory.CreateDirectory(moved);File.WriteAllText(Path.Combine(moved,name.ToUpperInvariant()),"changed bytes");var scan=FastScan(repo,moved);Check(scan.FastSkippedFiles==1&&scan.Frames.Count==0&&scan.Metrics.Snapshot().Sum(s=>s.Bytes)==0,"Moved names required headers or hashes");Check(FastScan(repo,moved,"Other telescope").FastSkippedFiles==0,"A different physical telescope reused names");}
   });
   Test("Legacy archives backfill filename aliases once without opening their captures",()=>{
    string source=Path.Combine(root,"fast-legacy");Directory.CreateDirectory(source);string path=Path.Combine(source,"unique.capture.fit");Write(path,64,48,(x,y)=>1300,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"fast-legacy-repo"))){repo.Import(repo.Scan(source,"Scope","Auto",ct,NoProgress).Frames,ct,NoProgress);using(var db=new Database(repo.WorkingIndex)){db.Exec("DELETE FROM import_names");db.Exec("DELETE FROM sessions WHERE id=?","import-name-inventory:1");}File.WriteAllText(path,"changed");var scan=FastScan(repo,source);Check(scan.FastSkippedFiles==1&&scan.Errors.Count==0,"Legacy inventory failed");Check(FastScan(repo,source).FastSkippedFiles==1,"Warm inventory lost its match");}
   });
   Test("Deletion exclusion and explicit reimport permission remain authoritative for filename matching",()=>{
    string source=Path.Combine(root,"fast-deleted");Directory.CreateDirectory(source);Write(Path.Combine(source,"unique.fit"),64,48,(x,y)=>1300,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"fast-deleted-repo"))){repo.Import(repo.Scan(source,"Scope","Auto",ct,NoProgress).Frames,ct,NoProgress);var frame=repo.All().Single();repo.DeleteFrames(new[]{frame},ct,NoProgress);var scan=FastScan(repo,source);Check(scan.FastDeletedFiles==1&&scan.Frames.Count==0,"Deleted name was reread");repo.AllowReimport(new[]{frame.Hash},ct);scan=FastScan(repo,source);Check(scan.FastDeletedFiles==0&&scan.Frames.Count==1,"Explicit permission did not restore import");}
   });
   Test("Robust USB mode detects same-name edits that default imports skip",()=>{
    string source=Path.Combine(root,"fast-usb-edits");Directory.CreateDirectory(source);string file=Path.Combine(source,"capture.fit");Write(file,64,48,(x,y)=>1300,new Dictionary<string,string>());var profile=new TelescopeProfile{Id="Scope",Model="Seestar S50"};
    using(var repo=new Repository(Path.Combine(root,"fast-usb-edits-repo"))){Check(UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress).Import.Imported==1,"First upload failed");Write(file,64,48,(x,y)=>1500,new Dictionary<string,string>());var fast=UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress);Check(fast.Import.Imported==0&&fast.Plan.FastSkippedFiles==1&&fast.Plan.Metrics.Snapshot().Sum(s=>s.Bytes)==0,"Default upload examined edited bytes");var robust=UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress,robustMatching:true);Check(robust.Plan.FastSkippedFiles==0&&robust.Import.Imported==1&&repo.All().Count==2,"Robust mode missed same-name edits");}
   });
   Test("Robust USB imports restore missing copies while fast matching trusts archive inventory",()=>{
    string source=Path.Combine(root,"fast-usb-missing");Directory.CreateDirectory(source);Write(Path.Combine(source,"capture.fit"),64,48,(x,y)=>1300,new Dictionary<string,string>());var profile=new TelescopeProfile{Id="Scope",Model="Seestar S50"};
    using(var repo=new Repository(Path.Combine(root,"fast-usb-missing-repo"))){UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress);File.Delete(repo.FilePath(repo.All().Single()));Check(UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress).Import.Imported==0,"Fast matching verified physical archive copies");Check(UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress,robustMatching:true).Restored==1,"Robust mode did not restore missing copy");}
   });
   Test("Duplicate-heavy filename scans read only the new image headers",()=>{
    string source=Path.Combine(root,"fast-large-session","MyWorks","M45_sub");Directory.CreateDirectory(source);
    using(var repo=new Repository(Path.Combine(root,"fast-large-repo"))){
     for(int i=0;i<1000;i++){string name="Light_M45_20261008-"+i.ToString("000000")+".fit";repo.Save(new Frame{Hash=Util.HashText(name),OriginalName=name,SourcePath=Path.Combine(source,name),SourceRoot=source,Telescope="Scope",TelescopeIdentity="Scope",Bytes=1000000,RelativePath=Path.Combine("Targets",name)});File.WriteAllText(Path.Combine(source,name),"payload must never be read");}
     Write(Path.Combine(source,"Light_M45_20261009-000001.fit"),64,48,(x,y)=>1500,new Dictionary<string,string>());
     var scan=FastScan(repo,source);Check(scan.FastSkippedFiles==1000&&scan.Frames.Count==1&&scan.Errors.Count==0&&scan.Metrics.Snapshot().Single(s=>s.Stage=="Header open/read").Bytes==2880,"Repeat cost scaled with known image contents");
     Console.WriteLine("FAST IMPORT: 1000 archived names skipped; 1 new header read (2880 bytes).");
    }
   });
   Test("Known DWARF folder identifiers without a matching archived filename are still scanned",()=>{
    string source=Path.Combine(root,"fast-anchor","DWARF_RAW_TELE_20261008");Directory.CreateDirectory(source);string old=Path.Combine(source,"raw_001.fit");Write(old,64,48,(x,y)=>1300,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"fast-anchor-repo"))){repo.Import(repo.Scan(source,"Scope","Dwarf 3",ct,NoProgress).Frames,ct,NoProgress);File.Delete(old);Write(Path.Combine(source,"raw_099.fit"),64,48,(x,y)=>1600,new Dictionary<string,string>());var scan=FastScan(repo,source);Check(scan.FastSessionFolders==0&&scan.Frames.Count==1,"Folder name alone omitted new content");}
   });
   Test("Connected telescope selection prefers bindings then a matching selected or unique profile",()=>{
    TelescopeProfile a=new TelescopeProfile{Id="Seestar A",Model="Seestar S50"},b=new TelescopeProfile{Id="Seestar B",Model="Seestar S30"},d=new TelescopeProfile{Id="Dwarf",Model="Dwarf 3"};var device=new UsbTelescope{Make="Seestar"};
    Check(UsbTelescopeDiscovery.MatchProfile(device,new[]{a,d})==a,"Unique model match not selected");Check(UsbTelescopeDiscovery.MatchProfile(device,new[]{d})==null,"Incompatible telescope selected");Check(UsbTelescopeDiscovery.MatchProfile(device,new[]{a,b})==null,"Ambiguous physical telescope guessed");Check(UsbTelescopeDiscovery.MatchProfile(device,new[]{a,b,d},b.Id)==b,"Matching selected telescope ignored");device.ProfileId=a.Id;Check(UsbTelescopeDiscovery.MatchProfile(device,new[]{a,b,d},b.Id)==a,"Stored physical binding ignored");
   });
  }
 }
}
