// Background processes: long, mostly read-only tasks (measuring image quality, plate solving) that run beside imports,
// exports and Settings instead of locking the window.
// The registry belongs to a Repository and is the one place that decides which archive changes may overlap them. A process
// names the captures it reads. The few operations that delete or move files in bulk claim those captures (or the whole
// archive) exclusively, so the two never touch the same file at once while everything else keeps going.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace AstroArchive {
 // Raised when an archive change would overlap a background process, or the reverse.
 public sealed class BackgroundBusyException:IOException {public BackgroundBusyException(string message):base(message){}}

 public sealed class BackgroundJob:IDisposable {
  public const string Quality="quality",Identification="identification";
  public readonly string Kind,Title;
  // A process reads for a long time and can be stopped; an exclusive claim covers a short archive change and cannot.
  public readonly bool Exclusive;
  public readonly CancellationTokenSource Cancellation=new CancellationTokenSource();
  // Progress is kept per process so one run never overwrites the figures of another.
  public readonly PipelineMetrics Metrics=new PipelineMetrics(null);
  public readonly Stopwatch Clock=Stopwatch.StartNew();
  readonly HashSet<string> captures;readonly BackgroundJobs owner;readonly ManualResetEventSlim finished=new ManualResetEventSlim(false);volatile bool cancelRequested;
  // captures: content hashes this entry reads or changes; null means it may touch any capture.
  internal BackgroundJob(BackgroundJobs owner,string kind,string title,bool exclusive,IEnumerable<string> captures){
   this.owner=owner;Kind=kind;Title=title;Exclusive=exclusive;this.captures=captures==null?null:new HashSet<string>(captures.Where(h=>!string.IsNullOrEmpty(h)),StringComparer.OrdinalIgnoreCase);
  }
  public bool CancelRequested{get{return cancelRequested;}}
  public bool Finished{get{return finished.IsSet;}}
  // How many captures this entry names; -1 when it covers the whole archive.
  public int CaptureCount{get{return captures==null?-1:captures.Count;}}
  public void Cancel(){cancelRequested=true;try{Cancellation.Cancel();}catch(ObjectDisposedException){}}
  // True when both entries could touch the same file. An entry that names no captures covers everything.
  internal bool Overlaps(BackgroundJob other){
   if(captures==null||other.captures==null)return true;
   foreach(string hash in captures)if(other.captures.Contains(hash))return true;
   return false;
  }
  // Runs the work and always marks the process finished, even when it fails or is canceled.
  public T Run<T>(Func<BackgroundJob,CancellationToken,T> work){try{return work(this,Cancellation.Token);}finally{finished.Set();}}
  public bool Wait(TimeSpan limit){return finished.Wait(limit);}
  // Takes ProgressInfo from engine code (image quality, the solver) and turns it into this process's own progress.
  public void Report(ProgressInfo p){
   if(p==null||p.LiveMetrics!=null)return;
   if(p.Stage!=null)Metrics.Stage=p.Stage;
   if(p.Workers>0)Metrics.Workers(p.Workers);
   Metrics.UpdateLegacy(p.Done,p.Total,p.Text);
  }
  public void Report(int done,int total,string stage,string text){Metrics.Stage=stage;Metrics.UpdateLegacy(done,total,text);}
  public ProgressInfo Progress(){var p=Metrics.Progress(false);p.ElapsedSeconds=Clock.Elapsed.TotalSeconds;return p;}
  // Releases the entry. Safe to call more than once.
  public void End(){owner.End(this);}
  internal void MarkFinished(){finished.Set();}
  // Exclusive claims are used in using blocks.
  public void Dispose(){End();}
 }

 public sealed class BackgroundJobs {
  readonly object gate=new object();readonly List<BackgroundJob> items=new List<BackgroundJob>();bool closed;
  // True while any process (not a short exclusive claim) is running.
  public bool Any{get{lock(gate)return items.Any(j=>!j.Exclusive);}}
  public List<BackgroundJob> Running{get{lock(gate)return items.Where(j=>!j.Exclusive).ToList();}}
  public BackgroundJob Find(string kind){lock(gate)return items.FirstOrDefault(j=>!j.Exclusive&&j.Kind==kind);}
  static string Busy(BackgroundJob busy){
   return busy.Exclusive?busy.Title+" is in progress. Try again when it finishes.":busy.Title+" is still running on these files. Let it finish, or stop it from Activity, then try again.";
  }
  // Starts a process of the given kind over the given captures (null: any). Only one of each kind runs at a time,
  // and nothing starts over captures that an archive change has claimed.
  public BackgroundJob Begin(string kind,string title,IEnumerable<string> captures,out string refusal){
   var job=new BackgroundJob(this,kind,title,false,captures);
   lock(gate){
    refusal=null;
    if(closed){refusal="This archive is closing.";return null;}
    var same=items.FirstOrDefault(j=>!j.Exclusive&&j.Kind==kind);
    if(same!=null){refusal=same.Title+" is already running. Follow it in Activity.";return null;}
    var claim=items.FirstOrDefault(j=>j.Exclusive&&j.Overlaps(job));
    if(claim!=null){refusal=Busy(claim);return null;}
    items.Add(job);return job;
   }
  }
  // Claims captures (null: the whole archive) for a short archive change that removes or moves files. Throws when a
  // process is using any of them. Dispose the claim when the change is done.
  public BackgroundJob Claim(string title,IEnumerable<string> captures){
   var job=new BackgroundJob(this,"claim",title,true,captures);
   lock(gate){
    var busy=items.FirstOrDefault(j=>j.Overlaps(job));
    if(busy!=null)throw new BackgroundBusyException(Busy(busy));
    items.Add(job);return job;
   }
  }
  // The friendly refusal for an archive change over these captures that would collide with a running process, or null.
  // Claims nothing; use it to explain a disabled command before the user tries it.
  public string Conflict(IEnumerable<string> captures){
   var probe=new BackgroundJob(this,"probe",string.Empty,true,captures);
   lock(gate){var busy=items.FirstOrDefault(j=>j.Overlaps(probe));return busy==null?null:Busy(busy);}
  }
  internal void End(BackgroundJob job){lock(gate)items.Remove(job);job.MarkFinished();}
  // Stops every process. Quality results already stored are kept.
  public void CancelAll(){foreach(var job in Running)job.Cancel();}
  // Waits for the running processes to wind down; false if one did not finish in time.
  public bool WaitAll(TimeSpan limit){
   var end=DateTime.UtcNow+limit;
   foreach(var job in Running){var left=end-DateTime.UtcNow;if(left<=TimeSpan.Zero||!job.Wait(left))return false;}
   return true;
  }
  // No process may start once the archive is closing.
  public void Close(){lock(gate)closed=true;}
 }

 public sealed partial class Repository {
  // Background processes and exclusive claims on this archive's captures.
  public readonly BackgroundJobs Background=new BackgroundJobs();
 }
}
