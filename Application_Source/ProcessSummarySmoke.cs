using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  void SmokeProcessSummaries(string output){
   var fixtures=new List<ActivityEntry>();string theme=settings.ThemeMode;int scale=settings.TextScalePercent;
   try{foreach(string mode in new[]{"Light","Dark"})foreach(int size in new[]{100,150}){
    settings.ThemeMode=mode;settings.TextScalePercent=size;ApplyAppearance();bool canceled=false;
    var live=AddActivity("Live import · Telescope 1",true);fixtures.Add(live);live.LiveImport=true;live.ProcessContext="Telescope 1 · 192.0.2.1";live.Status="Watching for captures";live.Cancel=()=>canceled=true;live.Progress=new ProgressInfo{ElapsedSeconds=600,Done=20,Total=80,TotalKnown=true,CopyPhase=true,BytesDone=250000,BytesTotal=1000000,EffectiveBytesPerSecond=1000000,RemainingSeconds=60};
    var other=AddActivity("Archive export",true);fixtures.Add(other);other.Status="Exporting files";RenderActivity();PumpPopupLayout();
    var first=(FrameworkElement)processSummaryItems.Children[0];if(!PopupChildren<TextBlock>(first).Any(t=>t.Text.StartsWith("Live import underway"))||OrderedActivities().First()!=live)throw new Exception("Live import is not first in process summaries.");
    var summary=processSummaryCards[live];if(!summary.Parameters.Text.Contains("20 / 80 files")||!summary.Parameters.Text.Contains("ETA ~1 min")||!summary.Parameters.Text.Contains("Telescope 1")||!summary.Parameters.Text.Contains("MB/s"))throw new Exception("Process summary lost its key parameters: "+summary.Parameters.Text);
    OpenActivity();PumpPopupLayout();if(activityItems.Children[0] is Border&&!PopupChildren<TextBlock>((FrameworkElement)activityItems.Children[0]).Any(t=>t.Text.StartsWith("Live import underway")))throw new Exception("Live import is not first in Activity.");
    CapturePopup(Window,Path.Combine(output,"AstroArchive_Process_Summaries_"+mode+size+".png"));
    PopupChildren<Button>(processSummaryItems).Single(b=>AutomationProperties.GetName(b)=="Dismiss "+live.Title+" summary").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    DismissActivitySummary(live);if(canceled||!live.Running||processSummaryCards.ContainsKey(live)||activityCards.ContainsKey(live)||showDismissedActivity.Visibility!=Visibility.Visible)throw new Exception("Summary dismissal stopped work or could not be restored.");
    RestoreDismissedActivity();if(!processSummaryCards.ContainsKey(live)||!activityCards.ContainsKey(live))throw new Exception("Dismissed process controls did not return.");
    live.BannerDismissed=true;live.ActivityDismissed=true;live.Running=false;live.DurationSeconds=600;live.Progress.Finished=true;live.Status="20 files imported; originals retained.";CloseActivity();NotifyActivity(live);PumpPopupLayout();
    if(!processSummaryCards[live].Title.Text.StartsWith("Completed")||processSummaryCards[live].Parameters.Text.Contains("ETA")||!processSummaryCards[live].Parameters.Text.Contains("Elapsed 00:10:00")||!activityCards.ContainsKey(live))throw new Exception("Completion did not restore a summary with final parameters: "+processSummaryCards[live].Parameters.Text);
    for(int i=0;i<8;i++)fixtures.Add(AddActivity("Concurrent process "+i,true));PumpPopupLayout();
    if(processSummaryScroll.ComputedVerticalScrollBarVisibility!=Visibility.Visible||processSummaryScroll.ScrollableHeight<=0)throw new Exception("Concurrent summaries cannot scroll.");
    processSummaryScroll.ScrollToBottom();PumpPopupLayout();if(processSummaryScroll.VerticalOffset<=0)throw new Exception("Summary scroll did not reach other processes.");
    foreach(var entry in fixtures.Where(a=>a!=live).ToList()){activities.Remove(entry);fixtures.Remove(entry);}RenderActivity();PumpPopupLayout();
    if(processSummaryScroll.ComputedVerticalScrollBarVisibility!=Visibility.Collapsed||processSummaryScroll.ScrollableHeight>0)throw new Exception("A single compact summary retained unnecessary scrolling.");
    activities.Remove(live);fixtures.Remove(live);RenderActivity();
   }
   File.WriteAllText(Path.Combine(output,"process-summaries-smoke.txt"),"PASS: live priority in main banner and Activity, key parameters, dismissal without cancellation, restoration, final summaries, automatic overflow scrolling, light/dark and 100/150% text.");
   }finally{foreach(var entry in fixtures)activities.Remove(entry);settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();CloseActivity();RenderActivity();}
  }
 }
}
