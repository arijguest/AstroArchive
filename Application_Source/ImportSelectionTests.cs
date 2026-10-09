using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void ImportSelectionTests(){
   Test("USB selections normalise overlaps and reject paths outside the telescope",()=>{
    string source=Path.Combine(root,"selected-card"),folder=Path.Combine(source,"MyWorks","M45_sub"),nested=Path.Combine(folder,"nested");Directory.CreateDirectory(nested);
    string inside=Path.Combine(nested,"one.fit"),single=Path.Combine(source,"selected.fit"),outside=Path.Combine(root,"outside.fit");File.WriteAllText(inside,"");File.WriteAllText(single,"");File.WriteAllText(outside,"");
    var selection=ImportSelection.Create(source,new[]{inside,nested,folder,single,single});Check(selection.Folders.Count==1&&selection.Files.Count==1&&selection.Folders.Single()==folder&&!selection.WholeSource,"Overlapping selections would be scanned twice");
    var whole=ImportSelection.Create(source,new[]{single,folder,source});Check(whole.WholeSource&&whole.Paths.Count()==1,"Whole drive retained overlapping selections");
    Expect(()=>ImportSelection.Create(source,new[]{outside}),"Another drive/source accepted");Expect(()=>ImportSelection.Create(source,new[]{"selected.fit"}),"Relative path accepted");Expect(()=>ImportSelection.Create(source,new string[0]),"Empty selection accepted");Expect(()=>ImportSelection.Create(source,new[]{Path.Combine(source,"missing.fit")}),"Missing selection accepted");
    string system=Path.Combine(source,".astroarchive");Directory.CreateDirectory(system);Expect(()=>ImportSelection.Create(source,new[]{system}),"Application folder accepted");string sidecar=Path.Combine(source,"session.json");File.WriteAllText(sidecar,"{}");Expect(()=>ImportSelection.Create(source,new[]{sidecar}),"Sidecar accepted as a capture");
   });
   Test("Selected USB imports fully scan only selected folders and files with ancestor metadata",()=>{
    string source=Path.Combine(root,"selected-full-card"),folder=Path.Combine(source,"DWARF_RAW_M33_20261008"),nested=Path.Combine(folder,"sub"),other=Path.Combine(source,"unselected");Directory.CreateDirectory(nested);Directory.CreateDirectory(other);
    string first=Path.Combine(folder,"raw_001.fit"),second=Path.Combine(nested,"raw_002.fit"),single=Path.Combine(source,"single.fit"),shots=Path.Combine(source,"shotsInfo.json");Write(first,64,48,(x,y)=>3100,new Dictionary<string,string>());Write(second,64,48,(x,y)=>3200,new Dictionary<string,string>());Write(single,64,48,(x,y)=>3300,new Dictionary<string,string>());File.WriteAllText(shots,"{\"targetName\":\"M33\"}");File.WriteAllText(Path.Combine(other,"bad.fit"),"Do not open this unselected file");
    var selection=ImportSelection.Create(source,new[]{folder,second,single});var profile=new TelescopeProfile{Id="Selected DWARF",Model="Dwarf 3"};
    using(var repo=new Repository(Path.Combine(root,"selected-full-repo"))){
     var initial=UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress,selection:selection);Check(initial.Import.Imported==3&&initial.Plan.Errors.Count==0&&initial.Plan.Frames.Count==3,"Unselected capture was scanned or selected captures were omitted");
     Check(initial.Plan.Metrics.Snapshot().Single(s=>s.Stage=="Discovery").Files==3&&initial.Plan.FastSessionFolders==0&&!initial.Plan.FilenameMatching,"Selected import used a shortcut or traversed unrelated entries");Check(initial.Plan.Frames.All(f=>f.SourceRoot==source&&f.SourceMetadataPath==shots)&&initial.Summary.Contains("Full scan of 1 folder and 1 file"),"Selection lost ancestor metadata or scope reporting");
     string later=Path.Combine(folder,"raw_003.fit");Write(later,64,48,(x,y)=>3400,new Dictionary<string,string>());var repeat=UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress,selection:selection);Check(repeat.Import.Imported==1&&repeat.Import.Duplicates==3&&repeat.Plan.FastSkippedFiles==0&&repeat.Plan.Metrics.Snapshot().Single(s=>s.Stage=="Header open/read").Bytes==4*2880,"Known session omission or header reuse hid selected captures");
     var lost=repo.All().Single(f=>f.OriginalName=="single.fit");File.Delete(repo.FilePath(lost));var repair=UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress,selection:selection);Check(repair.Restored==1&&repo.Verify(ct,NoProgress)==0,"Full selected import did not restore a missing archive copy");Check(new[]{first,second,single,later}.All(File.Exists),"Selected USB import removed originals");
     Write(first,64,48,(x,y)=>3450,new Dictionary<string,string>());var edited=UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress,selection:selection);Check(edited.Import.Imported==1&&edited.Import.Duplicates==3,"Selected full scan trusted an edited existing filename");
    }
   });
   Test("Individual USB files retain import policy and mismatched selection cannot import",()=>{
    string source=Path.Combine(root,"selected-files-card");Directory.CreateDirectory(source);string good=Path.Combine(source,"good.fit"),ignored=Path.Combine(source,"failed.fit");Write(good,64,48,(x,y)=>3500,new Dictionary<string,string>());File.WriteAllText(ignored,"Ignored failed file must not be read");var selection=ImportSelection.Create(source,new[]{good,ignored});
    using(var repo=new Repository(Path.Combine(root,"selected-files-repo"))){var result=UsbAutoUpload.Run(repo,new TelescopeProfile{Id="Files",Model="Auto"},source,1,ct,NoProgress,ignoreFailed:true,selection:selection);Check(result.Import.Imported==1&&result.Import.IgnoredFailed==1&&result.Plan.Errors.Count==0,"Individual selection bypassed import exclusions");string other=Path.Combine(root,"selected-other-card");Directory.CreateDirectory(other);Expect(()=>repo.Scan(other,"Files","Auto",ct,NoProgress,selection:selection),"Mismatched selection source accepted");}
   });
   Test("USB selected import retains no copies on pre-import disconnect or cancellation",()=>{
    string source=Path.Combine(root,"selected-disconnect-card");Directory.CreateDirectory(source);string file=Path.Combine(source,"one.fit");Write(file,64,48,(x,y)=>3600,new Dictionary<string,string>());var selection=ImportSelection.Create(source,new[]{file});var profile=new TelescopeProfile{Id="Disconnected",Model="Auto"};
    using(var repo=new Repository(Path.Combine(root,"selected-disconnect-repo"))){int checks=0;Expect(()=>UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress,available:()=>++checks==1,selection:selection),"Disconnect after scan ignored");Check(repo.All().Count==0&&File.Exists(file),"Disconnected selection copied or removed data");using(var canceled=new CancellationTokenSource()){canceled.Cancel();Expect(()=>UsbAutoUpload.Run(repo,profile,source,1,canceled.Token,NoProgress,selection:selection),"Canceled selection ignored");}Check(repo.All().Count==0,"Canceled selection imported captures");}
   });
   Test("USB confirmation highlights whole-drive and large selections",()=>{
    string source=Path.Combine(root,"selected-warnings-card");Directory.CreateDirectory(source);var whole=ImportSelection.Create(source,new[]{source});Check(whole.ConfirmationText("Seestar").Contains("entire telescope drive")&&whole.ConfirmationText("Seestar").Contains("long time"),"Whole-drive time warning missing");var paths=new List<string>();for(int i=0;i<ImportSelection.LargeFileSelection;i++){string path=Path.Combine(source,"capture_"+i+".fit");File.WriteAllText(path,"");paths.Add(path);}var large=ImportSelection.Create(source,paths);Check(large.ConfirmationText("DWARFLAB").Contains("500 files")&&large.ConfirmationText("DWARFLAB").Contains("Dwarflab")&&large.ConfirmationText("DWARFLAB").Contains("full scan and import"),"Large-file or brand warning missing");var single=ImportSelection.Create(source,paths.Take(1));Check(single.ConfirmationText("Seestar").Contains("may take a while")&&single.ConfirmationText("Seestar").Contains("Originals will remain"),"Full-scan confirmation or retention policy missing");
   });
   Test("Saved USB selections and detected make survive settings and telescope renames",()=>{
    var settings=new Settings();var profile=TelescopeProfiles.Save(settings,"Auto Seestar","Auto","Auto",null,new UsbVolume[0]);profile.SourceMake="Seestar";profile.VolumeId="selected-volume";profile.LastImportSelection=new List<string>{"MyWorks/M45_sub","single.fit"};
    var saved=Util.Deserialize<Settings>(Util.Serialize(settings));TelescopeProfiles.Rename(saved,"Auto Seestar","Renamed Seestar");var recovered=saved.Telescopes.Single();var device=new UsbTelescope{Make="Seestar",Volume=new UsbVolume{Id="new-volume"}};
    Check(recovered.SessionIdentity=="Auto Seestar"&&recovered.LastImportSelection.SequenceEqual(profile.LastImportSelection)&&UsbTelescopeDiscovery.MatchProfile(device,saved.Telescopes)==recovered,"Saved selection, make or stable physical identity was lost");
   });
  }
 }
}
