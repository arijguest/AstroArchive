// Screen content before copying and reuse the verified import pipeline.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public class AutoUploadResult {
  public ImportPlan Plan;public ImportResult Import;public int Restored;
  public string Summary{get{return Import.Imported-Restored+" imported; "+Restored+" restored; "+Import.Duplicates+" already present ("+Plan.FastSkippedFiles+(Plan.FilenameMatching?" skipped by filename/session matching":" skipped by session inventory")+"); "+Import.Failed+" failed; "+Import.SkippedDeleted+" previously deleted skipped; "+Import.IgnoredFailed+" failed filenames ignored; "+Import.IgnoredRaster+" PNG/JPG files ignored; "+Plan.Frames.Count(f=>f.Rejected)+" flagged captures retained for review. Originals retained.";}}
 }
 public static class UsbAutoUpload {
  public static AutoUploadResult Run(Repository repository,TelescopeProfile profile,string source,int workers,CancellationToken ct,Action<ProgressInfo> progress,Action<Frame> onFrame=null,Action<ImportPlan> onPlan=null,Func<bool> available=null,bool ignoreFailed=false,bool ignoreRaster=false,bool robustMatching=false){
   if(profile==null||string.IsNullOrWhiteSpace(profile.Id))throw new ArgumentException("Save or select this physical telescope first.");ct.ThrowIfCancellationRequested();if(available!=null&&!available())throw new IOException("The USB telescope is no longer connected. Reconnect it and try again.");
   var plan=repository.Scan(source,profile.Id,TelescopeProfiles.Model(profile.Model),ct,progress,false,f=>{if(f.Status=="New"&&(profile.Camera=="Wide"||profile.Camera=="Telephoto")){f.Camera=profile.Camera;f.CameraEvidence="Saved telescope camera override";Assets.UserFact(f,"Camera");}if(onFrame!=null)onFrame(f);},!robustMatching,false,telescopeIdentity:profile.SessionIdentity??profile.Id,deferFinish:true,ignoreFailed:ignoreFailed,ignoreRaster:ignoreRaster,quickScan:!robustMatching,fullScan:robustMatching,filenameMatching:!robustMatching);
   // Scan emits clones. Apply the override to its actual candidates as well.
   foreach(var f in plan.Frames.Where(f=>f.Status=="New"))if(profile.Camera=="Wide"||profile.Camera=="Telephoto"){f.Camera=profile.Camera;f.CameraEvidence="Saved telescope camera override";Assets.UserFact(f,"Camera");}
   if(onPlan!=null)onPlan(plan);ct.ThrowIfCancellationRequested();if(available!=null&&!available())throw new IOException("The USB telescope disconnected during screening. Reconnect it and try again.");
   int duplicates=plan.Frames.Count(f=>f.Status.StartsWith("Duplicate"));var restores=new HashSet<string>(plan.Frames.Where(f=>f.Status=="Restore").Select(f=>f.SourcePath),StringComparer.OrdinalIgnoreCase);
   var candidates=plan.Frames.Where(CaptureScreening.Importable).ToList();plan.Metrics.Phase(candidates.Count,0,"Screening files");
   var screening=repository.Screen(candidates,true,ct,p=>{plan.Metrics.UpdateLegacy(p.Done,p.Total,p.Text);plan.Metrics.Pulse(true);});foreach(var frame in plan.Frames)if(onFrame!=null)onFrame(frame.Clone());
   var ready=plan.Frames.Where(f=>CaptureScreening.Importable(f)&&!f.Rejected&&string.IsNullOrEmpty(f.ScreeningIssue)).ToList();
   var imported=repository.Import(ready,ct,progress,new ImportOptions{SourceRoot=plan.Source,IgnoreFailed=ignoreFailed,IgnoreRaster=ignoreRaster,Workers=workers,DeleteOriginals=false,CloudSource=false,OnFrame=onFrame,ScanMetrics=plan.Metrics,DeferFinish=true});imported.IgnoredFailed+=plan.IgnoredFailed;imported.IgnoredRaster+=plan.IgnoredRaster;imported.Duplicates+=duplicates+plan.FastSkippedFiles;imported.FastSkipped=plan.FastSkippedFiles;imported.FilenameMatching=plan.FilenameMatching;imported.SkippedDeleted+=plan.FastDeletedFiles;imported.Failed+=plan.Frames.Count(f=>f.Status=="Unreadable");imported.SkippedDeleted+=plan.Frames.Count(f=>f.Status=="Deleted");imported.Warnings.AddRange(plan.Errors);imported.Warnings.AddRange(screening.Errors);
   var result=new AutoUploadResult{Plan=plan,Import=imported,Restored=plan.Frames.Count(f=>f.Status=="Imported"&&restores.Contains(f.SourcePath))};repository.SaveImportReport(imported);return result;
  }
 }
}
