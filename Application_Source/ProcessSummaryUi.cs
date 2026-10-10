using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  ScrollViewer processSummaryScroll;
  StackPanel processSummaryItems;
  Button showDismissedActivity;
  readonly Dictionary<ActivityEntry,ProcessSummaryCard> processSummaryCards=new Dictionary<ActivityEntry,ProcessSummaryCard>();
  sealed class ProcessSummaryCard {public TextBlock Title,Status,Parameters;public ProgressBar Bar;}
  IEnumerable<ActivityEntry> OrderedActivities(){return activities.OrderByDescending(a=>a.Running&&a.LiveImport).ThenByDescending(a=>a.Running).ThenByDescending(a=>a.NeedsReview);}
  void InitializeProcessSummaries(){
   processSummaryItems=new StackPanel();
   processSummaryScroll=new ScrollViewer{Content=processSummaryItems,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,8)};
   AutomationProperties.SetName(processSummaryScroll,"Process summaries");
   processSummaryScroll.ScrollChanged+=(s,e)=>MenuScrolling.SetEnabled(processSummaryScroll,processSummaryScroll.ScrollableHeight>0);
   ((StackPanel)Window.FindName("NotificationBanners")).Children.Add(processSummaryScroll);
  }
  static string ProcessState(ActivityEntry entry){return entry.Running?(entry.LiveImport?"Live import underway":"In progress"):entry.Resume!=null?"Paused / interrupted":entry.Failed?"Needs attention":entry.Canceled?"Stopped":entry.NeedsReview?"Completed · review results":"Completed";}
  static string ProcessParameters(ActivityEntry entry){
   var parts=new List<string>();if(!string.IsNullOrWhiteSpace(entry.ProcessContext))parts.Add(entry.ProcessContext);
   var p=entry.Progress;double seconds=entry.Running?Math.Max(p==null?0:p.ElapsedSeconds,(DateTime.UtcNow-entry.StartedUtc).TotalSeconds):entry.DurationSeconds??(p==null?0:p.ElapsedSeconds);
   parts.Add("Elapsed "+PipelineMetrics.Duration(seconds));
   if(!string.IsNullOrWhiteSpace(entry.ProcessOutcome))parts.Add(entry.ProcessOutcome);
   if(p!=null){
    if((entry.Running||entry.ProcessOutcome==null))parts.Add(p.TotalKnown?p.Done+" / "+p.Total+" files":p.Done+" inspected");
    if(p.CopyPhase&&(!entry.LiveImport||entry.Running)){parts.Add(ImportWorkflow.Size(p.BytesDone)+(p.BytesTotal>0?" / "+ImportWorkflow.Size(p.BytesTotal):""));if(entry.Running&&p.EffectiveBytesPerSecond>0)parts.Add((p.EffectiveBytesPerSecond/1000000.0).ToString("0.0")+" MB/s");}
    if(entry.Running&&p.RemainingSeconds.HasValue&&!p.Finished)parts.Add("ETA ~"+EtaEstimate.Format(p.RemainingSeconds.Value));
   }
   return string.Join(" · ",parts);
  }
  void RenderProcessSummaries(){
   if(processSummaryItems==null)return;processSummaryItems.Children.Clear();processSummaryCards.Clear();
   foreach(var entry in OrderedActivities().Where(a=>a.ProcessTracked&&!a.BannerDismissed&&(a.Running||a.DurationSeconds.HasValue||a.Resume!=null))){
    var card=new ProcessSummaryCard{Title=new TextBlock{FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap},Status=new TextBlock{TextWrapping=TextWrapping.Wrap},Parameters=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,0)},Bar=new ProgressBar{Maximum=1,Height=3,Margin=new Thickness(0,6,0,0)}};
    card.Parameters.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");card.Parameters.SetResourceReference(TextBlock.ForegroundProperty,"Muted");
    var content=new StackPanel();content.Children.Add(card.Title);content.Children.Add(card.Status);content.Children.Add(card.Parameters);content.Children.Add(card.Bar);
    var layout=new DockPanel();var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top};DockPanel.SetDock(actions,Dock.Right);layout.Children.Add(actions);
    var view=ActivityAction("Activity",OpenActivity);actions.Children.Add(view);
    var dismiss=ActivityAction("×",()=>{entry.BannerDismissed=true;RenderProcessSummaries();});dismiss.ToolTip="Dismiss this summary; the process continues in Activity.";AutomationProperties.SetName(dismiss,"Dismiss "+entry.Title+" summary");actions.Children.Add(dismiss);layout.Children.Add(content);
    var border=new Border{Child=layout,CornerRadius=new CornerRadius(6),Padding=new Thickness(12,8,6,8),Margin=new Thickness(0,0,0,4)};border.SetResourceReference(Border.BackgroundProperty,"SurfaceAlt");processSummaryItems.Children.Add(border);processSummaryCards[entry]=card;UpdateProcessSummary(entry,card);
   }
   SizeProcessSummaries();processSummaryScroll.Visibility=processSummaryCards.Count>0?Visibility.Visible:Visibility.Collapsed;
  }
  void SizeProcessSummaries(){if(processSummaryScroll!=null)processSummaryScroll.MaxHeight=Math.Min(144*Math.Max(1,settings.TextScalePercent/100.0),Math.Max(80,(Window.ActualHeight>0?Window.ActualHeight:Window.Height)*0.15));}
  void UpdateProcessSummary(ActivityEntry entry,ProcessSummaryCard card){
   card.Title.Text=ProcessState(entry)+" · "+entry.Title;
   string status=entry.Status??"";int newline=status.IndexOfAny(new[]{'\r','\n'});if(newline>=0)status=status.Substring(0,newline);card.Status.Text=status.Length>180?status.Substring(0,177)+"…":status;
   card.Parameters.Text=ProcessParameters(entry);card.Bar.Visibility=entry.Running&&!entry.LiveImport?Visibility.Visible:Visibility.Collapsed;var p=entry.Progress;card.Bar.IsIndeterminate=entry.Running&&!settings.ReducedMotion&&(p==null||!p.TotalKnown);card.Bar.Value=p==null?0:Math.Max(0,Math.Min(1,p.ProgressFraction));
  }
  void TickProcessSummaries(){foreach(var pair in processSummaryCards)UpdateProcessSummary(pair.Key,pair.Value);}
  void DismissActivitySummary(ActivityEntry entry){entry.ActivityDismissed=true;entry.Unread=false;RenderActivity();}
  void RestoreDismissedActivity(){foreach(var entry in activities){entry.ActivityDismissed=false;entry.BannerDismissed=false;}RenderActivity();}
 }
}
