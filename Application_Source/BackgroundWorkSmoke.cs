using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  // A background process leaves every command available, keeps its own progress and Stop button, refuses a second run of the
  // same kind, and ends into Activity (stopped, failed or done) without opening a dialog.
  void SmokeBackground(string output){
   var savedRepo=repo;var savedAll=all;var savedPlan=plan;
   try{
    repo=new Repository(Path.Combine(output,"background-smoke-repository"));SetBusy(false);
    var frames=new List<Frame>{new Frame{Hash="background-a",OriginalName="a.fit"},new Frame{Hash="background-b",OriginalName="b.fit"}};
    using(var started=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
     string stoppedText=null;bool stoppedOk=true;
     RunBackground(BackgroundJob.Quality,"Background process fixture",frames,(job,token)=>{job.Report(1,4,"Measuring image quality","fixture.fit");started.Set();release.Wait(token);return "Fixture finished.";},(text,ok)=>{stoppedText=text;stoppedOk=ok;},"Fixture stopped.");
     SmokeSearchWait(()=>started.IsSet);
     var entry=activities.FirstOrDefault(a=>a.Title=="Background process fixture");
     if(entry==null||!entry.Running||entry.Cancel==null||!entry.ProcessTracked)throw new Exception("A background process is not tracked in Activity.");
     if(cancel!=null||RepositoryOperationBlocked||!BackgroundBusy)throw new Exception("A background process took the foreground lane.");
     LiveTick(true);
     if(entry.Progress==null||entry.Progress.Done!=1||entry.Progress.Total!=4||entry.Status==null||!entry.Status.Contains("fixture.fit"))throw new Exception("A background process lost its own progress.");
     if(!B("SettingsButton").IsEnabled||!B("ScanButton").IsEnabled||!TopMenu("SettingsMenu").IsEnabled||!TopMenu("ImportMenu").IsEnabled||!TopMenu("ExportMenu").IsEnabled)throw new Exception("Importing, exporting or Settings was disabled by a background process.");
     if(B("AnalyzeQualityButton").IsEnabled||!B("SolveButton").IsEnabled)throw new Exception("Only the command that is already running should be unavailable.");
     RunBackground(BackgroundJob.Quality,"Second background fixture",frames,(job,token)=>"unexpected",null);
     if(backgroundRuns.Count!=1||activities.Any(a=>a.Title=="Second background fixture")||!L("StatusLabel").Text.Contains("already running"))throw new Exception("A second background process of the same kind started.");
     if(repo.Background.Conflict(new[]{"background-a"})==null||repo.Background.Conflict(new[]{"elsewhere"})!=null)throw new Exception("The archive does not know which captures the process is reading.");
     OpenActivity();activityCards[entry].Actions.Children.OfType<Button>().Single(b=>Convert.ToString(b.Content)=="Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
     SmokeSearchWait(()=>!entry.Running);
     if(!entry.Canceled||entry.Failed||stoppedOk||stoppedText!="Fixture stopped."||BackgroundBusy||!B("AnalyzeQualityButton").IsEnabled)throw new Exception("Stopping a background process did not end it cleanly.");
     CloseActivity();
    }
    using(var started=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
     bool done=false;
     RunBackground(BackgroundJob.Identification,"Background result fixture",frames,(job,token)=>{started.Set();release.Wait(token);return "Fixture finished.";},(text,ok)=>{done=ok;ShowReport("Background fixture results",text);});
     SmokeSearchWait(()=>started.IsSet);
     if(!B("AnalyzeQualityButton").IsEnabled||B("SolveButton").IsEnabled)throw new Exception("A different kind of process was blocked by a running one.");
     release.Set();SmokeSearchWait(()=>!BackgroundBusy);
     var entry=activities.First(a=>a.Title=="Background result fixture");
     if(!done||entry.Running||entry.Failed||entry.Canceled||entry.Report!="Fixture finished."||!entry.DurationSeconds.HasValue||Window.OwnedWindows.Cast<Window>().Any(w=>w.Title=="Background fixture results"))throw new Exception("A finished background process lost its result or opened a dialog.");
     if(!B("SolveButton").IsEnabled)throw new Exception("A finished background process left its command disabled.");
    }
    RunBackground(BackgroundJob.Quality,"Background failure fixture",frames,(job,token)=>{throw new IOException("fixture failure");},null);
    SmokeSearchWait(()=>!BackgroundBusy);
    var failed=activities.First(a=>a.Title=="Background failure fixture");
    if(!failed.Failed||failed.Running||!failed.NeedsReview||failed.Report!="fixture failure")throw new Exception("A failed background process was not reported in Activity.");
    foreach(var fixture in activities.Where(a=>a.Title.EndsWith("fixture")).ToList())activities.Remove(fixture);RenderActivity();
    File.WriteAllText(Path.Combine(output,"background-smoke.txt"),"PASS: background processes keep importing, exporting and Settings available, report their own progress, stop from Activity, refuse a second run of the same kind and end into Activity without a dialog.");
   }finally{
    foreach(var run in backgroundRuns.ToList())run.Job.Cancel();
    if(repo!=null&&repo!=savedRepo)repo.Dispose();
    repo=savedRepo;all=savedAll;plan=savedPlan;SetBusy(false);
   }
  }
 }
}
