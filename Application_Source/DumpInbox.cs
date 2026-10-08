// The only nested import source: the archive's dedicated Dump inbox.
using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public class DumpResult {
  public ImportPlan Plan;public ImportResult Import;
  public bool NeedsReview {get{return Plan.Errors.Count>0||Import.Failed>0||Import.OriginalsKept>0||Import.Warnings.Count>0;}}
  public string Summary {get{return "Dump: "+Import.Imported+" imported; "+Import.Duplicates+" duplicates; "+Import.OriginalsDeleted+" processed files removed; "+(Plan.Errors.Count+Import.Failed)+" errors; "+Import.OriginalsKept+" files retained after cleanup; "+Import.IgnoredFailed+" failed filenames ignored; "+Import.IgnoredRaster+" PNG/JPG files ignored.";}}
 }
 public sealed partial class Repository {
  public string DumpFolder {get{return Path.Combine(Root,"Dump");}}
  internal void ValidateDumpFolder(){
   Directory.CreateDirectory(DumpFolder);
   if(!FileStamp.CanTraverse(new DirectoryInfo(DumpFolder)))throw new IOException("Dump must be an ordinary folder inside the archive; linked folders are not supported.");
  }
  public void EnsureDumpFolder(){ValidateDumpFolder();}
  public DumpResult ProcessDump(CancellationToken ct,Action<ProgressInfo> progress,int workers=0,Action<Frame> onFrame=null,bool ignoreFailed=false,bool ignoreRaster=false){
   ct.ThrowIfCancellationRequested();ValidateDumpFolder();
   // Auto detection stays per file; no remembered model/camera override applies to mixed drops.
   var plan=ScanCore(DumpFolder,"Unknown","Auto",ct,progress,false,onFrame,true,false,true,ignoreFailed:ignoreFailed,ignoreRaster:ignoreRaster);
   foreach(var frame in plan.Frames.Where(f=>f.Status=="Duplicate"||f.Status=="Duplicate (cached)"||f.Status=="Duplicate in source"))frame.Status="New";
   foreach(var frame in plan.Frames.Where(f=>f.Status=="New"))frame.Notes=(frame.Notes??"")+"Imported from Dump. Assign the physical telescope ID using Edit metadata. ";
   var result=Import(plan.Frames,ct,progress,new ImportOptions{SourceRoot=DumpFolder,IgnoreFailed=ignoreFailed,IgnoreRaster=ignoreRaster,DeleteOriginals=true,DumpInbox=true,Workers=workers,OnFrame=onFrame,ScanMetrics=plan.Metrics});
   result.IgnoredFailed+=plan.IgnoredFailed;result.IgnoredRaster+=plan.IgnoredRaster;SaveImportReport(result);var report=new DumpResult{Plan=plan,Import=result};
   LastReport=report.Summary+"\r\n\r\n"+LastReport+"\r\n\r\nDump scan errors:\r\n"+string.Join("\r\n",plan.Errors);
   Directory.CreateDirectory(Path.Combine(Meta,"reports"));
   File.WriteAllText(Path.Combine(Meta,"reports","last-dump.txt"),LastReport);
   return report;
  }
 }
}
