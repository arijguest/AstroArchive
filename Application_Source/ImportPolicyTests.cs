using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void ImportPolicyTests(){
   Test("Delete when imported retains every calibration type without attempting source cleanup",()=>{
    string source=Path.Combine(root,"keep-calibration-source");Directory.CreateDirectory(source);int index=0;
    foreach(string kind in new[]{"Dark","Flat","Bias","Dark flat","Master dark","Master flat","Master bias","Master dark flat","Offset"}){
     int value=2000+index++;Write(Path.Combine(source,"calibration_"+index+".fit"),64,48,(x,y)=>value,new Dictionary<string,string>{{"IMAGETYP","'"+kind+"'"}});
    }
    using(var repo=new Repository(Path.Combine(root,"keep-calibration-repo"))){var plan=repo.Scan(source,"Scope","Auto",ct,NoProgress);Check(plan.Frames.Count==9&&plan.Frames.All(CaptureSky.IsCalibration),"Fixture calibration types were not classified");
     var hashes=plan.Frames.ToDictionary(f=>f.SourcePath,f=>Util.Hash(f.SourcePath,ct));var result=repo.Import(plan.Frames,ct,NoProgress,new ImportOptions{SourceRoot=source,DeleteOriginals=true,Workers=2});
     Check(result.Imported==9&&result.OriginalsDeleted==0&&result.OriginalsKept==9&&result.Failed==0&&result.CleanupErrors.Count==0,"Calibration retention was reported as deletion or cleanup failure");
     Check(hashes.All(p=>File.Exists(p.Key)&&Util.Hash(p.Key,ct)==p.Value)&&repo.All().All(f=>Util.Hash(repo.FilePath(f),ct)==f.Hash&&f.SourceDisposition=="Original retained (calibration frame)"),"Calibration source or verified archive copy was changed");
     Check(!result.Metrics.Snapshot().Any(s=>s.Stage=="Source cleanup"),"Calibration originals entered source-removal work");
    }
   });
   Test("Delete when imported retains calibration markers after frame-type review",()=>{
    string source=Path.Combine(root,"keep-calibration-marker-source");Directory.CreateDirectory(source);Write(Path.Combine(source,"marker.fit"),64,48,(x,y)=>2100,new Dictionary<string,string>());Write(Path.Combine(source,"target.fit"),64,48,(x,y)=>2200,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"keep-calibration-marker-repo"))){var plan=repo.Scan(source,"Scope","Auto",ct,NoProgress);foreach(var frame in plan.Frames){frame.Kind="Light";frame.Target=frame.OriginalName=="target.fit"?"Calibration":"M45";frame.Calibration=frame.OriginalName=="marker.fit"?"Calibration frame":"Unknown";}
     var result=repo.Import(plan.Frames,ct,NoProgress,new ImportOptions{SourceRoot=source,DeleteOriginals=true});Check(result.Imported==2&&result.OriginalsKept==2&&result.OriginalsDeleted==0&&result.CleanupErrors.Count==0&&Directory.GetFiles(source).Length==2,"Reviewed calibration originals entered cleanup");
    }
   });
   Test("Dump imports and duplicate retries retain calibration originals",()=>{
    using(var repo=new Repository(Path.Combine(root,"keep-calibration-dump-repo"))){repo.EnsureDumpFolder();string path=Path.Combine(repo.DumpFolder,"dark.fit");Write(path,64,48,(x,y)=>2300,new Dictionary<string,string>{{"IMAGETYP","'DARK'"}});string hash=Util.Hash(path,ct);
     var first=repo.ProcessDump(ct,NoProgress);var repeat=repo.ProcessDump(ct,NoProgress);
     Check(first.Import.Imported==1&&repeat.Import.Imported==0&&repeat.Import.Duplicates==1&&new[]{first,repeat}.All(r=>r.Import.OriginalsDeleted==0&&r.Import.OriginalsKept==(Environment.OSVersion.Platform==PlatformID.Win32NT?1:0)&&r.Import.CleanupErrors.Count==0),"Dump removed a calibration or reported intentional retention as failure");
     Check(File.Exists(path)&&Util.Hash(path,ct)==hash&&repo.All().Count==1&&Util.Hash(repo.FilePath(repo.All().Single()),ct)==hash,"Dump calibration bytes or archive index changed");
    }
   });
   WindowsTest("Delete when imported removes science originals while retaining telescope calibrations",()=>{
    string source=Path.Combine(root,"mixed-calibration-cleanup-source");Directory.CreateDirectory(source);string light=Path.Combine(source,"light.fit"),dark=Path.Combine(source,"dark.fit");Write(light,64,48,(x,y)=>2400,new Dictionary<string,string>{{"IMAGETYP","'LIGHT'"}});Write(dark,64,48,(x,y)=>2500,new Dictionary<string,string>{{"IMAGETYP","'DARK'"}});
    using(var repo=new Repository(Path.Combine(root,"mixed-calibration-cleanup-repo"))){var result=repo.Import(repo.Scan(source,"Scope","Auto",ct,NoProgress).Frames,ct,NoProgress,new ImportOptions{SourceRoot=source,DeleteOriginals=true});Check(result.Imported==2&&result.OriginalsDeleted==1&&result.OriginalsKept==1&&result.CleanupErrors.Count==0&&!File.Exists(light)&&File.Exists(dark)&&repo.All().Count==2,"Calibration protection blocked science cleanup or deleted the telescope dark");}
   });
   Test("Legacy Mosaic metadata never blocks ordinary import browsing or stacking exports",()=>{
    string source=Path.Combine(root,"legacy-mosaic-source");Directory.CreateDirectory(source);string file=Path.Combine(source,"Light_M45.fit");var headers=LightHeaders(new DateTime(2026,10,6,21,0,0),"M45");headers["MOSAICID"]="'Old collection'";headers["PANELID"]="'1'";Write(file,64,48,(x,y)=>1800,headers);
    using(var repo=new Repository(Path.Combine(root,"legacy-mosaic-repo"))){repo.Import(repo.Scan(source,"Scope","Auto",ct,NoProgress).Frames,ct,NoProgress);var frame=repo.All().Single();string json=Util.Serialize(frame);json=json.Substring(0,json.Length-1)+",\"Mosaic\":{\"Declared\":true,\"Name\":\"Old collection\",\"PanelKey\":\"1\"}}";
     using(var database=new Database(repo.WorkingIndex))database.Exec("UPDATE files SET data=? WHERE hash=?",json,frame.Hash);
     var retained=repo.All().Single();Check(retained.Hash==frame.Hash&&retained.Target=="M45"&&File.Exists(repo.FilePath(retained)),"Opening legacy metadata damaged the capture");string exported=Exporter.Create(repo,repo.All(),new ExportOptions{CreateNewFolder=true,AddMetadata=true,Parent=root,Name="legacy-mosaic-export",IncludeCalibration=false},ct,NoProgress);Check(Directory.GetFiles(exported,"*.fit",SearchOption.AllDirectories).Length==1&&!File.ReadAllText(Path.Combine(exported,"manifest.json")).Contains("MosaicCollections"),"Old membership still controls export");
    }
   });
   Test("Non-raw import exclusion defaults on in old settings and preserves an explicit opt-out",()=>{
    Check(new Settings().IgnoreRasterImports&&Util.Deserialize<Settings>("{\"IgnoreFailed\":false}").IgnoreRasterImports,"New or legacy settings do not default to ignoring non-raw images");
    var selected=new Settings{IgnoreRasterImports=false};Check(!Util.Deserialize<Settings>(Util.Serialize(selected)).IgnoreRasterImports,"Explicit opt-out was lost");
   });
   Test("Repository non-raw purge selects only PNG/JPG/JPEG/MP4 and preserves Edited and scientific originals",()=>{
    using(var repo=new Repository(Path.Combine(root,"non-raw-purge-repo"))){
     var retained=new List<string>();int index=0;
     foreach(string relative in new[]{"captures/preview.PNG","captures/preview.JpG","captures/preview.jpeg","captures/preview.Mp4","captures/science.fit","captures/science.tiff","captures/science.xisf","captures/science.ser","captures/camera.dng","captures/recording.avi",".astroarchive/edited/project/output.jpg","Edited/legacy.png","Edited/recording.mp4"}){
      string path=Path.Combine(repo.Root,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,"fixture "+index++);repo.Save(new Frame{Hash=Util.Hash(path,ct),RelativePath=relative,OriginalName=Path.GetFileName(relative)});if(!relative.StartsWith("captures/preview",StringComparison.Ordinal))retained.Add(path);
     }
     var files=repo.NonRawFiles();Check(files.Count==4,"Purge included Edited or a scientific format");var result=repo.DeleteFrames(files,ct,NoProgress);Check(result.Deleted==4&&result.Errors.Count==0&&repo.All().Count==9&&repo.Deletions().Count==4&&retained.All(File.Exists),"Purge damaged retained files or skipped deletion history");
    }
   });
   Test("PNG/JPG/MP4 exclusion is optional and stops files before metadata reads",()=>{
    string source=Path.Combine(root,"raster-exclusion-source");Directory.CreateDirectory(source);foreach(string name in new[]{"preview.PNG","preview.JpG","preview.jpeg","preview.Mp4"})File.WriteAllText(Path.Combine(source,name),"invalid preview");Write(Path.Combine(source,"light.fit"),64,48,(x,y)=>1200,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"raster-exclusion-repo"))){var ignored=repo.Scan(source,"Scope","Auto",ct,NoProgress,ignoreRaster:true);Check(ignored.IgnoredRaster==4&&ignored.Frames.Count==1&&ignored.Errors.Count==0,"Raster bytes were read despite exclusion");var included=repo.Scan(source,"Scope","Auto",ct,NoProgress);Check(included.IgnoredRaster==0&&included.Errors.Count==3,"Default scan silently excludes raster originals");var result=repo.Import(ignored.Frames,ct,NoProgress);Check(result.Imported==1&&Directory.GetFiles(source).Length==5,"Ignored previews removed");}
   });
   Test("Import rechecks PNG/JPG/MP4 exclusion independently of the scan",()=>{
    string source=Path.Combine(root,"raster-enforcement-source");Directory.CreateDirectory(source);foreach(string name in new[]{"preview.PNG","preview.jpg","preview.JPEG","preview.MP4"})File.WriteAllText(Path.Combine(source,name),"original");
    using(var repo=new Repository(Path.Combine(root,"raster-enforcement-repo"))){var rows=Directory.GetFiles(source).Select(path=>new Frame{Status="New",Kind="Light",SourcePath=path,OriginalName=Path.GetFileName(path),Bytes=8});var result=repo.Import(rows,ct,NoProgress,new ImportOptions{IgnoreRaster=true,DeleteOriginals=true,SourceRoot=source});Check(result.Imported==0&&result.Failed==0&&result.IgnoredRaster==4&&Directory.GetFiles(source).Length==4&&repo.LastReport.Contains("4 PNG/JPG/MP4 files ignored"),"Import bypassed exclusion or removed originals");}
   });
   Test("Dump and USB respect raster exclusion and report skipped originals",()=>{
    using(var repo=new Repository(Path.Combine(root,"raster-dump-repo"))){repo.EnsureDumpFolder();File.WriteAllText(Path.Combine(repo.DumpFolder,"preview.Mp4"),"original");var dump=repo.ProcessDump(ct,NoProgress,ignoreRaster:true);Check(dump.Import.IgnoredRaster==1&&dump.Plan.Errors.Count==0&&File.Exists(Path.Combine(repo.DumpFolder,"preview.Mp4")),"Dump ignored policy or removed file");}
    string source=Path.Combine(root,"raster-usb-source");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"preview.MP4"),"original");using(var repo=new Repository(Path.Combine(root,"raster-usb-repo"))){var profile=new TelescopeProfile{Id="Scope",Model="Seestar S50"};var result=UsbAutoUpload.Run(repo,profile,source,1,ct,NoProgress,ignoreRaster:true);Check(result.Import.IgnoredRaster==1&&result.Plan.Errors.Count==0&&File.Exists(Path.Combine(source,"preview.MP4")),"USB ignored policy");}
   });
   Test("Unknown-target assignment preserves known targets calibrations meteors and archived rows",()=>{
    var rows=new[]{new Frame{Target="Unknown",Kind="Light",Status="New"},new Frame{Target="Unknown",Kind="Stack",Status="Failed"},new Frame{Target="M45",Kind="Light",Status="New"},new Frame{Target="Unknown",Kind="Dark",Status="New"},new Frame{Target="Unknown",Kind="Light",Status="Duplicate"},new Frame{Target="Unknown",Kind="Light",Status="New",OriginalName="meteor.fit"}};
    Check(ImportPolicy.AssignUnknown(rows,"Crescent Nebula")==2&&rows.Take(2).All(f=>f.Target=="NGC6888"&&f.TargetEvidence.StartsWith("User")&&f.Facts["Target"].Source=="User"),"Assignment lacked canonical target or user evidence");Check(rows[2].Target=="M45"&&rows[3].Target=="Unknown"&&rows[4].Target=="Unknown"&&rows[5].Target=="Meteor","Assignment overwrote protected rows");Expect(()=>ImportPolicy.AssignUnknown(rows,"Unknown"),"Unknown target accepted");Expect(()=>ImportPolicy.AssignUnknown(rows,"M45 M31"),"Conflicting target accepted");
   });
   Test("Import persists Unknown target assignment and reclassification retains the user choice",()=>{
    string source=Path.Combine(root,"unknown-target-source");Directory.CreateDirectory(source);string file=Path.Combine(source,"light_001.fit");Write(file,64,48,(x,y)=>1500,new Dictionary<string,string>{{"IMAGETYP","'LIGHT'"}});using(var repo=new Repository(Path.Combine(root,"unknown-target-repo"))){var plan=repo.Scan(source,"Scope","Auto",ct,NoProgress);Check(ImportPolicy.AssignUnknown(plan.Frames,"C27")==1,"Unknown light not assignable");repo.Import(plan.Frames,ct,NoProgress);var frame=repo.All().Single();Check(frame.Target=="NGC6888"&&frame.Facts["Target"].Source=="User"&&File.Exists(file),"Assigned target did not survive import");var classified=Assets.InspectExisting(frame,file,ct);Check(classified.Target==frame.Target,"Classification discarded user target");}
   });
  }
 }
}
