using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

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
    if(canceled||!live.Running||processSummaryCards.ContainsKey(live)||activityCards.ContainsKey(live)||showDismissedActivity.Visibility!=Visibility.Visible)throw new Exception("Banner dismissal failed to hide Activity, stopped work or could not be restored.");
    RestoreDismissedActivity();if(!processSummaryCards.ContainsKey(live)||!activityCards.ContainsKey(live))throw new Exception("Dismissed process controls did not return.");
    live.BannerDismissed=true;live.ActivityDismissed=true;live.Running=false;live.DurationSeconds=600;live.Progress.Finished=true;live.Status="20 files imported; originals retained.";CloseActivity();NotifyActivity(live);PumpPopupLayout();
    if(processSummaryCards.ContainsKey(live)||activityCards.ContainsKey(live)||live.Unread)throw new Exception("Completion revived a manually dismissed process.");
    RestoreDismissedActivity();PumpPopupLayout();
    if(!processSummaryCards[live].Title.Text.StartsWith("Completed")||processSummaryCards[live].Parameters.Text.Contains("ETA")||!processSummaryCards[live].Parameters.Text.Contains("Elapsed 00:10:00")||!activityCards.ContainsKey(live))throw new Exception("Restored completion lost its final parameters.");
    var paused=AddActivity("Paused import");fixtures.Add(paused);paused.ProcessTracked=true;paused.DurationSeconds=30;paused.Resume=()=>{};RenderActivity();PumpPopupLayout();
    var shift=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(Window),0,Key.LeftShift){RoutedEvent=Keyboard.KeyUpEvent};Window.RaiseEvent(shift);TickProcessSummaries();PumpPopupLayout();
    if(!processSummaryCards.ContainsKey(live))throw new Exception("A timer or modifier key cleared completion without user interaction.");
    processSummaryItems.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.MouseUpEvent});PumpPopupLayout();
    if(!processSummaryCards.ContainsKey(live))throw new Exception("Reading process summaries cleared them.");
    Window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.MouseUpEvent});PumpPopupLayout();
    if(processSummaryCards.ContainsKey(live)||!activityCards.ContainsKey(live)||live.ActivityDismissed||!processSummaryCards.ContainsKey(other)||!processSummaryCards.ContainsKey(paused))throw new Exception("Interaction discarded history, cleared active/paused work or retained a completed banner.");
    RestoreDismissedActivity();if(processSummaryCards.ContainsKey(live))throw new Exception("Show dismissed revived automatically cleared history.");
    live.BannerAutoHidden=false;RenderActivity();Window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(Window),0,Key.A){RoutedEvent=Keyboard.KeyUpEvent});PumpPopupLayout();
    if(processSummaryCards.ContainsKey(live)||!activityCards.ContainsKey(live))throw new Exception("Keyboard interaction failed to clear just the completed banner.");
    live.BannerAutoHidden=false;RenderActivity();other.Running=false;other.DurationSeconds=15;NotifyActivity(other);PumpPopupLayout();
    if(processSummaryCards.ContainsKey(live)||!activityCards.ContainsKey(live)||!processSummaryCards.ContainsKey(other)||!processSummaryCards.ContainsKey(paused))throw new Exception("A later completion did not replace the older banner while preserving history and paused work.");
    OpenActivity();PopupChildren<Button>(activityItems).Single(b=>AutomationProperties.GetName(b)=="Dismiss "+other.Title+" activity").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CloseActivity();
    if(processSummaryCards.ContainsKey(other)||activityCards.ContainsKey(other))throw new Exception("Activity dismissal left the corresponding banner visible.");
    RestoreDismissedActivity();
    for(int i=0;i<8;i++)fixtures.Add(AddActivity("Concurrent process "+i,true));PumpPopupLayout();
    if(processSummaryScroll.ComputedVerticalScrollBarVisibility!=Visibility.Visible||processSummaryScroll.ScrollableHeight<=0)throw new Exception("Concurrent summaries cannot scroll.");
    processSummaryScroll.ScrollToBottom();PumpPopupLayout();if(processSummaryScroll.VerticalOffset<=0)throw new Exception("Summary scroll did not reach other processes.");
    foreach(var entry in fixtures.Where(a=>a!=other).ToList()){activities.Remove(entry);fixtures.Remove(entry);}RenderActivity();PumpPopupLayout();
    if(processSummaryScroll.ComputedVerticalScrollBarVisibility!=Visibility.Collapsed||processSummaryScroll.ScrollableHeight>0)throw new Exception("A single compact summary retained unnecessary scrolling.");
    activities.Remove(other);fixtures.Remove(other);RenderActivity();
   }
   File.WriteAllText(Path.Combine(output,"process-summaries-smoke.txt"),"PASS: live priority and parameters; manual dismissal hides both surfaces without stopping work and survives completion; explicit restoration; click/keyboard and later completion retire banners while preserving Activity history and active/paused work; reading notifications and modifier keys preserve banners; overflow scrolling; light/dark and 100/150% text.");
   }finally{foreach(var entry in fixtures)activities.Remove(entry);settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();CloseActivity();RenderActivity();}
  }
 }
}
