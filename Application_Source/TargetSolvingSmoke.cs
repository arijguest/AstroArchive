using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeTargetSolving(string output){
   var jobs=TargetSolving.Plan(new[]{new Frame{OriginalName="First stack.fit",Kind="Stack",Target="Unknown",Session="s",Telescope="Scope",Camera="Telephoto"},new Frame{OriginalName="Second stack.fit",Kind="Stack",Target="Unknown",Session="s",Telescope="Scope",Camera="Telephoto"},new Frame{OriginalName="Failed stack.fit",Kind="Stack",Target="Unknown",Session="s",Telescope="Scope",Camera="Telephoto"}});
   TargetSolving.Solve(jobs,(frame,token,stage)=>{if(frame.OriginalName.StartsWith("Failed"))throw new IOException("Not enough stars");return new SolveResult{RA=56.75,Dec=24.1167,Radius=.5,Solver="UI fixture"};},CancellationToken.None,null);
   string theme=settings.ThemeMode;int scale=settings.TextScalePercent;var state=Window.WindowState;
   try{
    File.WriteAllText(Path.Combine(output,"target-solving-stage.txt"),"Batch review");
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();var dialog=IdentificationDialog(jobs);
     try{dialog.Window.Show();PumpPopupLayout();var table=PopupChildren<DataGrid>(dialog.Window).Single();var boxes=PopupChildren<ComboBox>(table).ToList();if(table.Items.Count!=3||boxes.Count!=3||boxes.Count(c=>!c.IsEnabled)!=1)throw new Exception("Batch review lost independent stacks or enabled a failed solve");
      var combo=boxes.Single(c=>c.DataContext==jobs[0]);
      if(Convert.ToString(combo.SelectedValue)!=jobs[0].Result.Suggested||!PopupChildren<TextBlock>(combo).Any(t=>t.Text.Contains("M45")))throw new Exception("Nearest major target is not selected and visibly displayed");
      jobs[0].Target=Catalog.Normalize("Whirlpool Galaxy");table.Items.Refresh();PumpPopupLayout();combo=PopupChildren<ComboBox>(table).Single(c=>c.DataContext==jobs[0]);
      if(Convert.ToString(combo.SelectedValue)!="M51"||!PopupChildren<TextBlock>(combo).Any(t=>t.Text.Contains("M51")&&t.Text.Contains("Whirlpool Galaxy"))||jobs[1].Target!="M45")throw new Exception("Search target does not display both ID/common name or changed another row");
      combo.SelectedValue="M45";PumpPopupLayout();if(jobs[0].Target!="M45"||!PopupChildren<TextBlock>(combo).Any(t=>t.Text.Contains("M45")))throw new Exception("Selecting a nearby candidate did not bind/display the target");
      if(PopupChildren<CheckBox>(table).Single(c=>c.DataContext==jobs[2]).IsEnabled)throw new Exception("Failed solve can be applied");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Target_Review_"+mode+textScale+".png"));
     }finally{dialog.Window.Close();}
    }
    File.WriteAllText(Path.Combine(output,"target-solving-stage.txt"),"Progress window closure");
    foreach(WindowState ownerState in new[]{WindowState.Normal,WindowState.Maximized}){
     Window.WindowState=ownerState;cancel=new CancellationTokenSource();SetBusy(true);activeMetrics=new PipelineMetrics(null);operationClock=System.Diagnostics.Stopwatch.StartNew();IdentificationStage(new IdentificationProgress{Completed=1,Total=3,Stage="ASTAP solving · 00:04 elapsed · 5 minute limit",Detail="Job 2 of 3 · Second stack.fit\n1 Stack · Scope / Telephoto"});LiveTick();PumpPopupLayout();
     if(L("OperationHeading").Text!="Identifying targets"||!L("OperationDetail").Text.Contains("Job 2 of 3")||!L("StatusLabel").Text.Contains("ASTAP solving")||L("RateLabel").Text.Contains("filesystem"))throw new Exception("Solver progress hides its current job or reports a filesystem stall");
     if(OperationProgressVisible)throw new Exception("Target solving opened an automatic window.");cancel.Dispose();cancel=null;identificationProgress=null;activeMetrics=null;operationClock.Stop();operationClock=null;SetBusy(false);PumpPopupLayout();if(Window.WindowState!=ownerState)throw new Exception("Completing background solving changed the owner's state.");
    }
    File.WriteAllText(Path.Combine(output,"target-solving-stage.txt"),"Deferred target review");
    var restoreState=Window.WindowState;Window.WindowState=WindowState.Minimized;PumpPopupLayout();bool reviewed=false;ShowTargetReviewWhenReady(()=>reviewed=true);PumpPopupLayout();if(reviewed)throw new Exception("Completing a solve opened review.");Window.WindowState=restoreState;PumpPopupLayout();if(reviewed)throw new Exception("Restoring opened review automatically.");var entry=activities.First(a=>a.Review!=null&&a.Title=="Target identification results");OpenActivity();var review=activityCards[entry].Actions.Children.OfType<Button>().Single(b=>Convert.ToString(b.Content)=="Review results");review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!reviewed||entry.NeedsReview)throw new Exception("Activity review action lost the results.");CloseActivity();
    File.WriteAllText(Path.Combine(output,"target-solving-smoke.txt"),"PASS independent stack review, preselected nearest-major targets, per-row alias edits/candidate selection, failed-job controls, dark/light at 100/150% text, current solve stage/count/file and active progress closure with normal/maximized owner unchanged, deferred target review after restore.");
   }finally{if(cancel!=null){cancel.Dispose();cancel=null;}identificationProgress=null;activeMetrics=null;operationClock=null;SetBusy(false);Window.WindowState=state;settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();}
  }
 }
}
