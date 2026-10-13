// Background processes in the window: measuring image quality and solving plates run beside imports, exports and Settings.
// They do not take the foreground lane (the cancel token and the busy state), so every command stays available. Each one has
// its own Activity entry with progress and a Stop button and ends into Activity instead of a dialog. The repository decides
// which archive changes may overlap them (BackgroundWork.cs); this file only starts, shows and finishes them.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  sealed class BackgroundRun {public BackgroundJob Job;public ActivityEntry Entry;}
  readonly List<BackgroundRun> backgroundRuns=new List<BackgroundRun>();
  bool BackgroundBusy{get{return backgroundRuns.Count>0;}}

  // The status line belongs to the foreground operation while there is one; otherwise a background result may use it.
  void BackgroundStatus(string text){if(cancel==null&&remoteSessions.Count==0)L("StatusLabel").Text=text;}

  // Runs work on a worker thread as a background process over the given captures (hand it copies, not window rows).
  // finished runs on the window thread when the process ends, however it ends, unless the window is closing; its text is
  // the summary, or the reason the process stopped.
  async void RunBackground(string kind,string title,IEnumerable<Frame> frames,Func<BackgroundJob,CancellationToken,string> work,Action<string,bool> finished,string stoppedMessage=null){
   if(repo==null||closing)return;
   string refusal;var job=repo.Background.Begin(kind,title,frames.Select(f=>f.Hash),out refusal);
   if(job==null){L("StatusLabel").Text=refusal;return;}
   var entry=AddActivity(title,true);entry.ProcessContext="Background";var run=new BackgroundRun{Job=job,Entry=entry};backgroundRuns.Add(run);
   entry.Cancel=()=>{job.Cancel();entry.Status="Stopping after the current file…";};
   UpdateNavigationState();
   string result=null,error=null,details=null;bool success=false;
   try{result=await Task.Run(()=>job.Run((j,token)=>work(j,token)));success=true;}
   catch(OperationCanceledException){error=stoppedMessage??"Stopped. Completed work is kept.";}
   catch(Exception e){error=title+" could not finish: "+e.Message;details=e.Message;}
   finally{
    job.End();backgroundRuns.Remove(run);
    entry.Running=false;entry.Cancel=null;entry.Failed=details!=null;entry.Canceled=!success&&details==null;entry.DurationSeconds=job.Clock.Elapsed.TotalSeconds;
    entry.Status=error??(string.IsNullOrWhiteSpace(result)?"Completed.":result);
    if(entry.Progress!=null){entry.Progress.Finished=true;entry.Progress.ElapsedSeconds=entry.DurationSeconds.Value;}
    if(details!=null){entry.ReportTitle="Operation could not finish";entry.Report=details;entry.NeedsReview=true;}
    if(!closing)UpdateNavigationState();
   }
   var previous=completionActivity;completionActivity=entry;
   try{if(!closing&&finished!=null){finished(error??result,success);if(success&&!entry.NeedsReview&&entry.Review==null&&!entry.Failed&&string.IsNullOrWhiteSpace(entry.Status))entry.Status="Completed.";}}
   catch(Exception failure){entry.Failed=true;entry.NeedsReview=true;entry.Status="Results could not be displayed.";entry.Report=failure.Message;}
   finally{completionActivity=previous;if(!closing)NotifyActivity(entry);}
   // A window that was closed while this ran closes now, once nothing else is still working.
   if(closing&&!BackgroundBusy)Window.Close();
  }

  // Called from the live timer: copies each process's own progress to its Activity entry and open card.
  void TickBackground(){
   foreach(var run in backgroundRuns){
    var entry=run.Entry;var p=run.Job.Progress();entry.Progress=p;
    string line=(p.Stage==null?"":p.Stage+": ")+(p.Text??"");
    entry.Status=entry.Canceled?"Stopping after the current file…":line.Length==0?"Preparing…":line;if(entry.Canceled)p.RemainingSeconds=null;
    ActivityCard card;
    if(activityCards.TryGetValue(entry,out card)){card.Status.Text=entry.Status;card.Rate.Text=ProcessParameters(entry);card.Bar.IsIndeterminate=!settings.ReducedMotion&&!p.TotalKnown&&!p.Finished;card.Bar.Value=Math.Max(0,Math.Min(1,p.ProgressFraction));}
   }
  }

  // Closing the window stops the processes; the window closes when the last one has wound down.
  void CancelBackground(){foreach(var run in backgroundRuns.ToList()){run.Job.Cancel();run.Entry.Canceled=true;}}

  // A command that is already running as a background process cannot start again until it ends.
  void ApplyBackgroundAvailability(){
   bool free=repo!=null&&!RepositoryOperationBlocked&&!SearchBlocked("SearchBox");
   B("AnalyzeQualityButton").IsEnabled=free&&repo.Background.Find(BackgroundJob.Quality)==null;
   B("SolveButton").IsEnabled=free&&repo.Background.Find(BackgroundJob.Identification)==null;
  }

  // Explains, before the user is asked anything, that a background process is still reading the captures an archive change
  // would remove or move. The repository enforces the same rule when the change itself starts.
  bool BackgroundConflict(IEnumerable<Frame> frames){
   if(repo==null)return false;
   string message=repo.Background.Conflict(frames==null?null:frames.Select(f=>f.Hash));if(message==null)return false;
   LocalizedMessage.Show(Window,message,"A background process is running");return true;
  }
 }
}
