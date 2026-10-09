using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AstroArchive.Installation;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeActivity(string output){
   var saved=Util.Serialize(settings);var state=Window.WindowState;var originalMonitor=releaseMonitor;var originalNotice=releaseNotice;
   try{
    foreach(string theme in new[]{"Light","Dark"})foreach(int scale in new[]{100,150}){
     settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();
     var entry=AddActivity("Import progress fixture",true);currentActivity=entry;cancel=new CancellationTokenSource();entry.Cancel=()=>cancel.Cancel();SetBusy(true);
     latestProgress=new ProgressInfo{Stage="Copying and verifying",Text="capture.fit",TotalKnown=true,Total=100,Done=25,ProgressFraction=0.25,RemainingSeconds=9000,ElapsedSeconds=600,CopyPhase=true,BytesTotal=1000000,BytesDone=250000,Stages=new System.Collections.Generic.List<StageMetric>{new StageMetric{Stage="Verification",Files=25,Seconds=30,Bytes=250000}}};LiveTick(true);PumpPopupLayout();
     if(activityPanel.Visibility==Visibility.Visible||OperationProgressVisible||((Popup)Window.FindName("OperationPopup")).IsOpen)throw new Exception("Starting background work opened a progress surface.");
     if(B("EditedAddButton").IsEnabled||!C("PageSelector").IsEnabled||!T("SearchBox").IsEnabled)throw new Exception("Conflicting edits are enabled or browsing is blocked.");
     OpenActivity();PumpPopupLayout();var card=activityCards[entry];if(card.Bar.Value!=0.25||!card.Rate.Text.Contains("2.5 h")||card.Rate.Text.Contains("150 min"))throw new Exception("Activity lost progress or hours ETA.");
     double coffeeRight=TopMenu("CoffeeMenu").TranslatePoint(new Point(TopMenu("CoffeeMenu").ActualWidth,0),Window).X;double bellLeft=activityBell.TranslatePoint(new Point(),Window).X;if(bellLeft<coffeeRight-1||bellLeft>coffeeRight+32)throw new Exception("The notification bell is not beside Support.");
     double coffeeTop=TopMenu("CoffeeMenu").TranslatePoint(new Point(),Window).Y,bellTop=activityBell.TranslatePoint(new Point(),Window).Y;if(Math.Abs(activityBell.ActualHeight-TopMenu("CoffeeMenu").ActualHeight)>0.1||Math.Abs(bellTop-coffeeTop)>0.1||Math.Abs(activityBell.ActualWidth-activityBell.ActualHeight)>0.1)throw new Exception("The notification button does not match the toolbar height and alignment.");
     CapturePopup(Window,Path.Combine(output,"AstroArchive_Activity_"+theme+scale+".png"));CloseActivity();if(cancel.IsCancellationRequested)throw new Exception("Closing Activity canceled work.");
     Window.WindowState=WindowState.Minimized;PumpPopupLayout();LiveTick(true);Window.WindowState=state;PumpPopupLayout();if(activityPanel.Visibility==Visibility.Visible||cancel.IsCancellationRequested)throw new Exception("Restore opened progress or canceled work.");
     OpenActivity();activityCards[entry].Actions.Children.OfType<Button>().Single(b=>Convert.ToString(b.Content)=="Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!cancel.IsCancellationRequested)throw new Exception("Activity Cancel lost the operation token.");
     entry.Running=false;entry.Cancel=null;entry.Canceled=true;entry.Status="Canceled. Completed changes are retained.";currentActivity=null;latestProgress=null;cancel.Dispose();cancel=null;SetBusy(false);CloseActivity();NotifyActivity(entry);if(!entry.Unread)throw new Exception("Completion did not produce an unread notification.");OpenActivity();if(entry.Unread)throw new Exception("Opening Activity did not acknowledge notifications.");CloseActivity();activities.Remove(entry);RenderActivity();
    }
    using(var started=new ManualResetEventSlim())using(var finish=new ManualResetEventSlim()){
     bool completed=false;Run(token=>{started.Set();finish.Wait(token);return "Files processed.";},message=>{completed=true;ShowReport("Background fixture results",message);},"Background worker fixture");
     SmokeSearchWait(()=>started.IsSet);var work=currentActivity;if(work==null||!work.Running)throw new Exception("Worker is not tracked.");finish.Set();SmokeSearchWait(()=>cancel==null);if(!completed||work.Running||work.Report!="Files processed."||Window.OwnedWindows.Cast<Window>().Any(w=>w.Title=="Background fixture results"))throw new Exception("Worker completion lost its report or opened a dialog.");activities.Remove(work);RenderActivity();
    }
    using(var started=new ManualResetEventSlim())using(var finish=new ManualResetEventSlim()){
     bool completed=false;Run(token=>{started.Set();finish.Wait(token);return "Unexpected completion";},message=>completed=true,"Cancelable worker fixture");SmokeSearchWait(()=>started.IsSet);var work=currentActivity;OpenActivity();activityCards[work].Actions.Children.OfType<Button>().Single(b=>Convert.ToString(b.Content)=="Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));SmokeSearchWait(()=>cancel==null);if(completed||!work.Canceled||work.Running||work.Failed)throw new Exception("Cancel did not safely finish the actual worker.");CloseActivity();activities.Remove(work);RenderActivity();
    }
    bool reviewed=false;var result=AddActivity("Review fixture");completionActivity=result;QueueActivityReview("Review fixture",()=>reviewed=true);completionActivity=null;
    if(reviewed||!result.NeedsReview)throw new Exception("A completed scan opened review automatically.");OpenActivity();activityCards[result].Actions.Children.OfType<Button>().Single(b=>Convert.ToString(b.Content)=="Review results").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!reviewed||result.NeedsReview)throw new Exception("Review results did not execute/acknowledge the action.");CloseActivity();activities.Remove(result);RenderActivity();
    var release=new UpdateManifest{schema=1,application_version="1.2.0",package_version="1.2.0.2",url=UpdateClient.Repository+"/releases/download/v1.2.0.2/AstroArchive-1.2.0-Windows-x64-Offline-Setup.exe",size=1,sha256=InstallCore.Hash(new byte[]{1})};
    releaseMonitor=new ReleaseMonitor(output,new InstallRecord{Version="1.2.0",PackageVersion="1.2.0.1"},()=>release,null,Path.Combine(output,"release-monitor-fixture.json"));FinishReleaseTask(releaseMonitor.CheckAsync());PublishReleaseState();var notice=releaseNotice;int notices=activities.Count(a=>a.Title==notice.Title);PublishReleaseState();if(activities.Count(a=>a.Title==notice.Title)!=notices)throw new Exception("Repeated release check duplicated a notification.");
    OpenActivity();CloseActivity();var about=AboutPage();try{about.Window.Show();PumpPopupLayout();if(!PopupChildren<TextBlock>(about.Window).Any(t=>t.Text.Contains("package 1.2.0.2")&&t.Text.Contains("available"))||!PopupChildren<Button>(about.Window).Any(b=>Convert.ToString(b.Content)=="Check now"))throw new Exception("About lost the release notice after reading Activity.");CapturePopup(about.Window,Path.Combine(output,"AstroArchive_About_Update.png"));}finally{about.Window.Close();}
    releaseInstalling=true;SetBusy(true);if(B("ScanButton").IsEnabled||B("EditedAddButton").IsEnabled)throw new Exception("Release download allowed competing repository operations.");releaseInstalling=false;SetBusy(false);
    activities.Remove(notice);RenderActivity();File.WriteAllText(Path.Combine(output,"activity-smoke.txt"),"PASS: embedded bell/panel, counts, hour ETA, theme/text scaling, no automatic windows, independent dismissal/cancellation, minimise/restore, deferred review, deduplicated release notices and persistent About availability.");
   }finally{completionActivity=currentActivity=null;latestProgress=null;if(cancel!=null){cancel.Dispose();cancel=null;}releaseInstalling=false;releaseMonitor=originalMonitor;releaseNotice=originalNotice;CloseActivity();Window.WindowState=state;settings=Util.Deserialize<Settings>(saved);ApplyAppearance();SetBusy(false);}
  }
 }
}
