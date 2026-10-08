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
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();var dialog=IdentificationDialog(jobs);
     try{dialog.Window.Show();PumpPopupLayout();var table=PopupChildren<DataGrid>(dialog.Window).Single();var boxes=PopupChildren<ComboBox>(table).ToList();if(table.Items.Count!=3||boxes.Count!=3||boxes.Count(c=>!c.IsEnabled)!=1)throw new Exception("Batch review lost independent stacks or enabled a failed solve");
      var combo=boxes.Single(c=>c.DataContext==jobs[0]);combo.Text="Whirlpool Galaxy";PumpPopupLayout();if(Catalog.Normalize(jobs[0].Target)!="M51"||jobs[1].Target!="M45")throw new Exception("Editing one batch target changed another target or lost the alias");combo.SelectedItem=jobs[0].Result.Candidates.First(c=>c.Name=="M45");PumpPopupLayout();if(jobs[0].Target!="M45")throw new Exception("Selecting a nearby candidate did not bind the target");
      if(PopupChildren<CheckBox>(table).Single(c=>c.DataContext==jobs[2]).IsEnabled)throw new Exception("Failed solve can be applied");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Target_Review_"+mode+textScale+".png"));
     }finally{dialog.Window.Close();}
    }
    foreach(WindowState ownerState in new[]{WindowState.Normal,WindowState.Maximized}){
     Window.WindowState=ownerState;cancel=new CancellationTokenSource();SetBusy(true);activeMetrics=new PipelineMetrics(null);operationClock=System.Diagnostics.Stopwatch.StartNew();IdentificationStage(new IdentificationProgress{Completed=1,Total=3,Stage="ASTAP solving · 00:04 elapsed · 5 minute limit",Detail="Job 2 of 3 · Second stack.fit\n1 Stack · Scope / Telephoto"});LiveTick();PumpPopupLayout();
     if(L("OperationHeading").Text!="Identifying targets"||!L("OperationDetail").Text.Contains("Job 2 of 3")||!L("StatusLabel").Text.Contains("ASTAP solving")||L("RateLabel").Text.Contains("filesystem"))throw new Exception("Solver progress hides its current job or reports a filesystem stall");
     var progressWindow=operationProgressWindow;progressWindow.Activate();PumpPopupLayout();cancel.Dispose();cancel=null;identificationProgress=null;activeMetrics=null;operationClock.Stop();operationClock=null;SetBusy(false);PumpPopupLayout();if(Window.WindowState!=ownerState||operationProgressWindow!=null||progressWindow.IsVisible)throw new Exception("Closing active solving progress changed the owner's state or retained the dialog");
    }
    Window.WindowState=WindowState.Minimized;PumpPopupLayout();bool reviewed=false;ShowTargetReviewWhenReady(()=>reviewed=true);PumpPopupLayout();if(reviewed)throw new Exception("Completing a solve while minimized opened the target review");Window.WindowState=WindowState.Normal;PumpPopupLayout();if(!reviewed||Window.WindowState!=WindowState.Normal)throw new Exception("Restoring lost deferred target review or changed the owner state");
    File.WriteAllText(Path.Combine(output,"target-solving-smoke.txt"),"PASS independent stack review, preselected nearest-major targets, per-row alias edits/candidate selection, failed-job controls, dark/light at 100/150% text, current solve stage/count/file and active progress closure with normal/maximized owner unchanged.");
   }finally{if(cancel!=null){cancel.Dispose();cancel=null;}pendingTargetReview=null;identificationProgress=null;activeMetrics=null;operationClock=null;SetBusy(false);Window.WindowState=state;settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();}
  }
 }
}
