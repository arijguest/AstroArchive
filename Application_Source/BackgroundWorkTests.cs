// Background-process tests: which archive changes may run beside image quality and plate solving, and that the repository
// stays consistent when imports, edits and checkpoints land while a process is reading and storing results.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace AstroArchive {
 public partial class Tests {
  // Writes count sharp light frames named after their position so every file has its own content.
  static void BackgroundFixture(string source,int count,int first){
   Directory.CreateDirectory(source);int w=640,h=480;
   for(int i=first;i<first+count;i++){var plane=QualityEngineTests.Field(w,h,2.4+0.15*(i%5),70,12,100+i);Write(Path.Combine(source,"Light_M51_"+i+".fit"),w,h,(x,y)=>plane[y*w+x],LightHeaders(new DateTime(2026,10,5,21,i,0),"M51"));}
  }
  static bool Busy(Action action){try{action();return false;}catch(BackgroundBusyException){return true;}}

  static void BackgroundWorkTests(){
   Test("Background: one process per kind, different kinds side by side, nothing starts once the archive closes",()=>{
    var jobs=new BackgroundJobs();string refusal;
    var quality=jobs.Begin(BackgroundJob.Quality,"Measuring image quality",new[]{"a","b"},out refusal);Check(quality!=null&&refusal==null&&jobs.Any&&jobs.Find(BackgroundJob.Quality)==quality,"A process did not start");
    Check(jobs.Begin(BackgroundJob.Quality,"Measuring image quality",new[]{"c"},out refusal)==null&&refusal.Contains("already running"),"A second process of the same kind started: "+refusal);
    var solving=jobs.Begin(BackgroundJob.Identification,"Solving plates",new[]{"a"},out refusal);Check(solving!=null&&jobs.Running.Count==2,"Two different processes could not run together");
    quality.End();Check(jobs.Find(BackgroundJob.Quality)==null&&jobs.Running.Count==1&&quality.Finished,"Ending a process did not release it");
    quality.End();var again=jobs.Begin(BackgroundJob.Quality,"Measuring image quality",new[]{"c"},out refusal);Check(again!=null,"A kind could not restart after it ended");
    jobs.Close();again.End();solving.End();Check(jobs.Begin(BackgroundJob.Quality,"Measuring image quality",null,out refusal)==null&&refusal.Contains("closing")&&!jobs.Any,"A process started after the archive closed");
   });
   Test("Background: archive changes wait for processes that read the same captures, in both directions",()=>{
    var jobs=new BackgroundJobs();string refusal;
    var reading=jobs.Begin(BackgroundJob.Quality,"Measuring image quality",new[]{"a","B"},out refusal);
    using(jobs.Claim("Deleting files",new[]{"c"})){}
    Check(Busy(()=>jobs.Claim("Deleting files",new[]{"b"})),"A claim over a capture being read was allowed (hashes compare without case)");
    Check(Busy(()=>jobs.Claim("Resetting the archive",null)),"An archive-wide change ran beside a process");
    Check(jobs.Conflict(new[]{"A"})!=null&&jobs.Conflict(new[]{"z"})==null&&jobs.Conflict(null)!=null,"Conflict() disagrees with Claim()");
    try{jobs.Claim("Deleting files",new[]{"a"});}catch(BackgroundBusyException e){Check(e.Message.Contains("Measuring image quality")&&e.Message.Contains("Activity"),"The refusal does not say what to do: "+e.Message);}
    reading.End();using(jobs.Claim("Deleting files",new[]{"a"}))using(jobs.Claim("Deleting files",new[]{"b"})){}
    var held=jobs.Claim("Deleting files",new[]{"a","b"});
    Check(jobs.Begin(BackgroundJob.Quality,"Measuring image quality",new[]{"a"},out refusal)==null&&refusal.Contains("Deleting files"),"A process started over captures being deleted: "+refusal);
    Check(jobs.Begin(BackgroundJob.Quality,"Measuring image quality",null,out refusal)==null,"A process over the whole archive started during a deletion");
    var other=jobs.Begin(BackgroundJob.Quality,"Measuring image quality",new[]{"z"},out refusal);Check(other!=null,"A process over other captures was blocked: "+refusal);other.End();
    Check(Busy(()=>jobs.Claim("Renaming the telescope",null)),"Two archive changes overlapped");
    held.Dispose();held.Dispose();var whole=jobs.Claim("Resetting the archive",null);
    Check(jobs.Begin(BackgroundJob.Identification,"Solving plates",new[]{"q"},out refusal)==null,"A process started during an archive-wide change");whole.Dispose();
    Check(jobs.Begin(BackgroundJob.Identification,"Solving plates",new string[0],out refusal)!=null,"A process that names no captures was refused");
   });
   Test("Background: stopping a process, waiting for it and its own progress",()=>{
    var jobs=new BackgroundJobs();string refusal;
    var job=jobs.Begin(BackgroundJob.Quality,"Measuring image quality",null,out refusal);var entered=new ManualResetEventSlim(false);
    var task=Task.Factory.StartNew(()=>job.Run((j,token)=>{entered.Set();token.WaitHandle.WaitOne(20000);Thread.Sleep(100);return token.IsCancellationRequested;}),TaskCreationOptions.LongRunning);
    Check(entered.Wait(10000),"The process did not start");Check(!jobs.WaitAll(TimeSpan.FromMilliseconds(50)),"WaitAll returned while a process was running");
    jobs.CancelAll();Check(job.CancelRequested,"Stopping did not reach the process");
    Check(task.Wait(10000)&&task.Result,"The process did not see the stop request");Check(jobs.WaitAll(TimeSpan.FromSeconds(10))&&job.Finished,"WaitAll did not see the process finish");job.End();Check(!jobs.Any,"A stopped process stayed registered");
    var failing=jobs.Begin(BackgroundJob.Identification,"Solving plates",null,out refusal);
    try{failing.Run<int>((j,token)=>{throw new IOException("boom");});}catch(IOException){}
    Check(failing.Finished,"A failed process was never marked finished");failing.End();
    var measured=jobs.Begin(BackgroundJob.Quality,"Measuring image quality",null,out refusal);
    measured.Report(new ProgressInfo{Done=3,Total=10,Stage="Measuring image quality",Text="Light_1.fit",Workers=4});
    var p=measured.Progress();Check(p.Done==3&&p.Total==10&&p.Stage=="Measuring image quality"&&p.Text=="Light_1.fit"&&p.Workers==4&&p.TotalKnown,"Progress was not kept per process: "+p.Done+"/"+p.Total+" "+p.Stage);
    var other=jobs.Begin(BackgroundJob.Identification,"Solving plates",null,out refusal);other.Report(1,2,"Solving","Frame");
    Check(measured.Progress().Done==3&&other.Progress().Done==1&&other.Progress().Stage=="Solving","Processes shared progress figures");measured.End();other.End();
   });
   Test("Background: imports, edits, checkpoints and reads continue while image quality is measured",()=>{
    string first=Path.Combine(root,"background-first"),second=Path.Combine(root,"background-second"),path=Path.Combine(root,"background-repo");BackgroundFixture(first,4,0);BackgroundFixture(second,3,10);
    using(var repo=new Repository(path)){
     repo.Import(repo.Scan(first,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var start=repo.All();Check(start.Count==4,"Fixture was not imported");
     string refusal;var job=repo.Background.Begin(BackgroundJob.Quality,"Measuring image quality",start.Select(f=>f.Hash),out refusal);Check(job!=null,"Quality could not start: "+refusal);
     var snapshot=start.Select(f=>f.Clone()).ToList();var paused=new ManualResetEventSlim(false);var resume=new ManualResetEventSlim(false);int gated=0;QualityRun run=null;
     // The run is held part-way through (after its first result) while the rest of the program works on the archive.
     var task=Task.Factory.StartNew(()=>job.Run((j,token)=>run=repo.AnalyzeQuality(snapshot,false,token,info=>{j.Report(info);if(Interlocked.Exchange(ref gated,1)==0){paused.Set();resume.Wait(60000);}})),TaskCreationOptions.LongRunning);
     try{
      Check(paused.Wait(60000),"The quality run never reported progress");Check(job.Progress().Total==4&&job.Progress().Done>=1,"The process's own progress was not updated");
      // 1. An import of new files while the run is mid-way.
      var imported=repo.Import(repo.Scan(second,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);Check(imported.Imported==3,"Import beside the run failed: "+imported.Imported);
      // 2. A metadata edit, a checkpoint and a full read.
      var edited=repo.All().First(f=>f.OriginalName=="Light_M51_0.fit");edited.Notes="edited during the run";repo.Save(edited);repo.Checkpoint(ct);
      Check(repo.All().Count==7,"Library count wrong during the run");
      // 3. Validation by a stale copy must not undo the edit.
      var stale=start.First(f=>f.OriginalName=="Light_M51_0.fit").Clone();stale.RepositoryStamp=null;repo.ValidateCapture(stale,ct);
      var stored=repo.Find(edited.Hash);Check(stored.Notes=="edited during the run"&&stored.RepositoryStamp!=null,"Validating a stale copy overwrote newer metadata or lost the stamp");
     }finally{resume.Set();}
     Check(task.Wait(120000),"The quality run did not finish");job.End();
     Check(run.Measured==4&&run.Errors.Count==0&&run.NotReached==0,"The run lost results: "+run.Report());
     var after=repo.All();Check(after.Count==7&&after.Count(f=>f.QualityScore.HasValue)==4&&repo.UnmeasuredQuality(after).Count==3,"Results or new imports are missing afterwards");
     Check(repo.Find(after.First(f=>f.OriginalName=="Light_M51_0.fit").Hash).Notes=="edited during the run","The edit was lost");
    }
    using(var reopened=new Repository(path)){Check(reopened.All().Count==7&&reopened.QualityMeasurements().Count==4,"Stored results or imports did not survive reopening");}
   });
   Test("Background: stored quality results can be read from other threads while a run keeps saving",()=>{
    string source=Path.Combine(root,"background-readers-source");BackgroundFixture(source,6,20);
    using(var repo=new Repository(Path.Combine(root,"background-readers-repo"))){
     repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var frames=repo.All();var before=repo.QualityMeasurements();int count=before.Count;
     var stop=new ManualResetEventSlim(false);var failures=new List<Exception>();
     Action reader=()=>{try{while(!stop.IsSet){int seen=0;foreach(var m in repo.QualityMeasurements().Values)seen+=m.Hash.Length>0?1:0;repo.ApplyQuality(frames.Select(f=>f.Clone()).ToList());repo.All();}}catch(Exception e){lock(failures)failures.Add(e);}};
     var readers=Enumerable.Range(0,3).Select(i=>Task.Factory.StartNew(reader,TaskCreationOptions.LongRunning)).ToArray();
     QualityRun run;try{run=repo.AnalyzeQuality(frames,false,ct,NoProgress);var forced=repo.AnalyzeQuality(repo.All(),true,ct,NoProgress);Check(forced.Measured==6,"Forced run wrong: "+forced.Summary());}finally{stop.Set();Task.WaitAll(readers);}
     Check(failures.Count==0,"A reader failed while results were stored: "+(failures.Count==0?"":failures[0].ToString()));
     Check(run.Measured==6&&count==0&&before.Count==0&&repo.QualityMeasurements().Count==6,"Published result sets changed after being handed out or results were lost");
    }
   });
   Test("Background: a capture refiled while a run holds its old location is still measured",()=>{
    string source=Path.Combine(root,"background-moved-source");BackgroundFixture(source,2,30);
    using(var repo=new Repository(Path.Combine(root,"background-moved-repo"))){
     repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var frames=repo.All();var held=frames.Select(f=>f.Clone()).ToList();
     var moving=repo.All().First();moving.Target="M31";repo.Refile(moving,ct);string oldPath=repo.FilePath(held.First(f=>f.Hash==moving.Hash));
     Check(!File.Exists(oldPath)&&File.Exists(repo.FilePath(moving)),"The fixture capture did not move");
     var run=repo.AnalyzeQuality(held,false,ct,NoProgress);Check(run.Measured==2&&run.Failed==0&&run.Errors.Count==0,"A refiled capture was reported missing: "+run.Report());
    }
   });
   Test("Background: deleting, resetting, renaming or protecting captures waits for a process that reads them",()=>{
    string source=Path.Combine(root,"background-claims-source");BackgroundFixture(source,3,40);
    using(var repo=new Repository(Path.Combine(root,"background-claims-repo"))){
     repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var frames=repo.All();string refusal;
     var job=repo.Background.Begin(BackgroundJob.Quality,"Measuring image quality",new[]{frames[0].Hash,frames[1].Hash},out refusal);Check(job!=null,refusal);
     Check(Busy(()=>repo.DeleteFrames(new[]{frames[0]},ct,NoProgress))&&File.Exists(repo.FilePath(frames[0]))&&repo.All().Count==3,"A capture under measurement was deleted");
     Check(Busy(()=>repo.DeleteFrames(new[]{frames[2],frames[1]},ct,NoProgress))&&repo.All().Count==3,"A selection that included a capture under measurement was partly deleted");
     Check(Busy(()=>repo.ResetArchive(ct,NoProgress))&&repo.All().Count==3,"The archive was reset during a process");
     Check(Busy(()=>repo.RenameTelescope("Unit-01","Unit-02",ct,NoProgress))&&repo.All().All(f=>f.Telescope=="Unit-01"),"A telescope was renamed during a process");
     Check(Busy(()=>repo.SetOriginalsProtection(false,ct,NoProgress)),"Original-file protection changed during a process");
     Check(repo.DeleteFrames(new[]{frames[2]},ct,NoProgress).Deleted==1&&repo.All().Count==2,"A capture nobody is reading could not be deleted");
     job.End();Check(repo.DeleteFrames(new[]{frames[0]},ct,NoProgress).Deleted==1&&repo.All().Count==1,"Deletion stayed blocked after the process ended");
     Check(repo.Background.Conflict(null)==null,"A finished delete left its claim behind");
    }
   });
   Test("Background: closing the archive stops running processes and waits for them",()=>{
    var repo=new Repository(Path.Combine(root,"background-dispose-repo"));string refusal;var job=repo.Background.Begin(BackgroundJob.Identification,"Solving plates",null,out refusal);var entered=new ManualResetEventSlim(false);
    var task=Task.Factory.StartNew(()=>job.Run((j,token)=>{entered.Set();token.WaitHandle.WaitOne(30000);Thread.Sleep(300);return token.IsCancellationRequested;}),TaskCreationOptions.LongRunning);
    Check(entered.Wait(10000),"The process did not start");var clock=System.Diagnostics.Stopwatch.StartNew();repo.Dispose();
    Check(job.Finished&&task.Wait(1000)&&task.Result&&clock.Elapsed.TotalSeconds<10,"Disposing the archive did not stop the process first");
    Check(repo.Background.Begin(BackgroundJob.Quality,"Measuring image quality",null,out refusal)==null&&refusal.Contains("closing"),"A process started on a closed archive");
   });
  }
 }
}
