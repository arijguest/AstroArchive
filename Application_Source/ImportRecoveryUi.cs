using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AstroArchive.Remote;

namespace AstroArchive {
 public partial class MainUi {
  ImportResumeStore importResumeStore;ImportResumeRecord pendingImportRecord,foregroundImportRecord;
  readonly Dictionary<string,ActivityEntry> recoverableImports=new Dictionary<string,ActivityEntry>();
  ImportResumeStore ResumeStore {get{return importResumeStore??(importResumeStore=new ImportResumeStore(Path.Combine(Path.GetDirectoryName(config),"ImportJobs")));}}
  void InitializeImportRecovery(){
   B("PauseImportButton").Click+=(s,e)=>PauseAllImports();
   try{foreach(var record in ResumeStore.Load())AttachImportRecovery(record,null);if(ResumeStore.Warnings.Count>0){var warning=AddActivity("Import recovery needs attention");warning.NeedsReview=true;warning.Unread=true;warning.Report=string.Join("\n",ResumeStore.Warnings);warning.Status="Some import records could not be read. The records were retained.";}if(recoverableImports.Count>0)L("StatusLabel").Text=recoverableImports.Count+" unfinished imports available in Activity.";}
   catch(Exception error){var warning=AddActivity("Import recovery could not load");warning.NeedsReview=true;warning.Unread=true;warning.Report=error.Message;warning.Status="Open Activity for recovery details.";}RenderActivity();
  }
  bool CanResumeImport(ImportResumeRecord record){return record.Kind.StartsWith("Remote")?!NetworkImportBlocked&&(repo==null||repo.Root.Equals(record.Repository,StringComparison.OrdinalIgnoreCase)||remoteSessions.Count==0):!RepositoryOperationBlocked;}
  void AttachImportRecovery(ImportResumeRecord record,ActivityEntry activity){
   var entry=activity??AddActivity(record.Title);entry.Running=false;entry.NeedsReview=true;entry.Canceled=false;entry.Status=(record.State=="Paused"?"Paused":"Interrupted")+". Resume verifies completed copies and continues this import.";entry.RepositoryRoot=record.Repository;entry.ImportKind=record.Kind;if(activity==null)entry.Unread=true;entry.Pause=null;entry.Cancel=null;
   entry.ResumeAvailable=()=>CanResumeImport(record);entry.Resume=()=>ResumeImport(record,entry);entry.Discard=()=>{ResumeStore.Remove(record);recoverableImports.Remove(record.Id);entry.Resume=null;entry.Discard=null;entry.NeedsReview=false;entry.Status="Canceled. Completed archive copies and original source files are retained.";RenderActivity();};recoverableImports[record.Id]=entry;RenderActivity();
  }
  ImportResumeRecord FolderImportRecord(List<Frame> frames,ImportOptions options,string solve,string rotation){return new ImportResumeRecord{Kind="Files",Title="File import · "+(frames.Count==0?"Source":frames[0].Telescope),Repository=repo.Root,Source=options.SourceRoot,Frames=frames.Select(f=>f.Clone()).ToList(),Workers=options.Workers,IgnoreFailed=options.IgnoreFailed,IgnoreRaster=options.IgnoreRaster,DeleteOriginals=options.DeleteOriginals,Solve=solve,Rotation=rotation};}
  ImportResumeRecord RemoteImportRecord(RemoteImportRequest request){
   var connection=Util.Deserialize<Connection>(Util.Serialize(request.Connection));string encrypted=string.IsNullOrEmpty(connection.Password)?null:PlateSolve.Protect(connection.Password);connection.Password="";
   return new ImportResumeRecord{Kind=request.Live?"RemoteLive":"RemoteFiles",Title=(request.Live?"Live import · ":"Telescope download · ")+request.Profile.Id+" · "+connection.Host,Repository=repo.Root,Connection=connection,ProtectedPassword=encrypted,Profile=Util.Deserialize<TelescopeProfile>(Util.Serialize(request.Profile)),Files=request.Files,Workers=settings.CopyWorkers,IgnoreFailed=settings.IgnoreFailed,IgnoreRaster=settings.IgnoreRasterImports};
  }
  void PersistPausing(ImportResumeRecord record,ActivityEntry entry){record.State="Pausing";try{ResumeStore.Save(record);}catch(Exception e){entry.NeedsReview=true;entry.Report="Could not save the pause state: "+e.Message+". The existing recovery record is retained.";}}
  void PauseForegroundImport(){if(foregroundImportRecord==null||cancel==null)return;PersistPausing(foregroundImportRecord,currentActivity);cancel.Cancel();if(currentActivity!=null)currentActivity.Status="Pausing safely; verified copies are retained…";}
  void PauseRemoteImport(RemoteImportSession session){PersistPausing(session.Record,session.Activity);session.Cancellation.Cancel();session.Activity.Status="Pausing safely; verified downloads are retained…";}
  void PauseAllImports(){PauseForegroundImport();foreach(var session in remoteSessions.ToList())PauseRemoteImport(session);RenderActivity();}
  async void ResumeImport(ImportResumeRecord record,ActivityEntry previous){await ResumeImportOperation(record,previous);}
  async Task ResumeImportOperation(ImportResumeRecord record,ActivityEntry previous,Func<Connection,ISource> factory=null){
   if(!CanResumeImport(record))return;
   try{
    if(repo==null||!repo.Root.Equals(record.Repository,StringComparison.OrdinalIgnoreCase))OpenRepository(record.Repository);
    if(record.Kind.StartsWith("Remote")){
     var connection=Util.Deserialize<Connection>(Util.Serialize(record.Connection));connection.Password=string.IsNullOrEmpty(record.ProtectedPassword)?"":PlateSolve.Unprotect(record.ProtectedPassword);
     var request=new RemoteImportRequest{Connection=connection,Profile=record.Profile,Files=record.Files,Live=record.Kind=="RemoteLive"};string issue=ValidateRemoteRequest(request);if(issue!=null)throw new IOException(issue);
     previous.NeedsReview=false;previous.Resume=null;previous.Discard=null;recoverableImports.Remove(record.Id);await StartRemoteImport(request,factory,record);
    }else{
     if(!Directory.Exists(record.Source))throw new IOException("Reconnect the original source at "+record.Source+", then resume this import.");
     if(record.Solve!="Off"&&!PlateSolve.Configured(settings))throw new IOException("Configure the saved import's plate solver in Settings before resuming.");
     previous.NeedsReview=false;previous.Resume=null;previous.Discard=null;recoverableImports.Remove(record.Id);pendingImportRecord=record;BeginLive(true);
     ImportResult result=null;EditedImportResult edited=null;await RunOperation(ct=>{
      if(record.Kind=="Dump"){var dump=repo.ProcessDump(ct,Progress,record.Workers,LiveFrame,record.IgnoreFailed,record.IgnoreRaster);plan=dump.Plan;if(dump.Import.Failed+dump.Plan.Errors.Count>0)record.State="Interrupted";return dump.Summary;}
      if(record.Kind.StartsWith("Edited")){edited=new EditedImportResult();var project=record.EditedPlan!=null?repo.ImportEditedFolder(record.EditedPlan,"Edited images",ct,Progress,edited):repo.AddEditedImages(record.EditedFiles,null,"Edited images",ct,Progress,edited);return project==null?null:project.Id;}
      if(record.Kind=="Usb"){var upload=UsbAutoUpload.Run(repo,record.Profile,record.Source,record.Workers,ct,Progress,LiveFrame,p=>plan=p,ignoreFailed:record.IgnoreFailed,ignoreRaster:record.IgnoreRaster,robustMatching:true,selection:record.Selection);result=upload.Import;if(result.Failed>0)record.State="Interrupted";return upload.Summary;}
      var frames=ResumeStore.Remaining(record,repo,ct);plan=new ImportPlan{Frames=frames,Source=record.Source};result=repo.Import(frames,ct,Progress,new ImportOptions{SourceRoot=record.Source,Workers=record.Workers,IgnoreFailed=record.IgnoreFailed,IgnoreRaster=record.IgnoreRaster,DeleteOriginals=record.DeleteOriginals,OnFrame=LiveFrame});if(result.Failed>0)record.State="Interrupted";
      var paths=new HashSet<string>((record.Frames??new List<Frame>()).Select(f=>f.SourcePath),StringComparer.OrdinalIgnoreCase);var retained=repo.All().Where(f=>paths.Contains(f.SourcePath)).ToList();
      if(record.Solve!="Off")AutoIdentify(retained.Where(f=>(f.Kind=="Light"||f.Kind=="Stack")&&(record.Solve=="All light/stack files"||TargetIdentification.NeedsPlateSolve(f))).ToList(),ct,result.Metrics);
      if(record.Rotation!="Off")foreach(var group in retained.Where(f=>f.Kind=="Light"&&(record.Rotation=="All light sessions"||f.Mount=="Unknown")).GroupBy(f=>f.Session+"|"+f.Group))AnalyzeGroup(group.ToList(),ct,null);
      return result.Imported+" imported; "+result.Duplicates+" duplicates; "+result.Failed+" failed. Verified completed copies retained.";
     },message=>{if(edited!=null)EditedImportComplete(message,edited);else L("StatusLabel").Text=message;},record.Title, "ResumeImport");
    }
   }catch(Exception error){previous.NeedsReview=true;previous.Resume=()=>ResumeImport(record,previous);previous.Discard=()=>{ResumeStore.Remove(record);recoverableImports.Remove(record.Id);previous.Resume=null;previous.Discard=null;previous.NeedsReview=false;RenderActivity();};recoverableImports[record.Id]=previous;previous.Status="Could not resume: "+error.Message;RenderActivity();}
  }
  void FinishImportRecord(ImportResumeRecord record,ActivityEntry entry,bool success,bool canceled){
   if(record==null)return;
   try{if(record.State=="Pausing"||record.State=="Interrupted"||closing||!success&&(!canceled||record.State!="Canceled")){record.State=record.State=="Pausing"||closing?"Paused":"Interrupted";ResumeStore.Save(record);AttachImportRecovery(record,entry);}
    else{ResumeStore.Remove(record);recoverableImports.Remove(record.Id);}}
   catch(Exception e){record.State="Interrupted";AttachImportRecovery(record,entry);entry.Report=(entry.Report??"")+"\nRecovery state could not be updated: "+e.Message;}
  }
 }
}
