using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AstroArchive {
 public sealed class ActivityEntry {
  public string Title,Status,ReportTitle,Report,OutputPath,ActionLabel,RepositoryRoot,CancelLabel,ImportKind,ProcessContext,ProcessOutcome;
  public DateTime StartedUtc=DateTime.UtcNow;
  public bool Running,Unread,NeedsReview,Failed,Canceled,NetworkImport,LiveImport,ProcessTracked,BannerDismissed,ActivityDismissed,WasRunning;
  public double? DurationSeconds;
  public ProgressInfo Progress;
  public Action Review,Cancel,Pause,Resume,Discard;public Func<bool> ResumeAvailable;
 }
 public partial class MainUi {
  readonly List<ActivityEntry> activities=new List<ActivityEntry>();
  readonly Dictionary<ActivityEntry,ActivityCard> activityCards=new Dictionary<ActivityEntry,ActivityCard>();
  ActivityEntry currentActivity,completionActivity,reviewingActivity;
  Border activityPanel,activityToast;StackPanel activityItems;Button activityBell;TextBlock activityBadge,activityToastText;
  DispatcherTimer activityToastTimer;
  string nextActivityTitle;
  bool activityOpeningReport,activityDisposed;
  sealed class ActivityCard {public TextBlock Title,Status,Rate,Details;public ProgressBar Bar;public WrapPanel Actions;}
  bool RepositoryOperationBlocked {get{return NetworkImportBlocked||remoteSessions.Count>0;}}
  void InitializeActivity(){
   var root=(Grid)Window.Content;
   var menu=(Menu)Window.FindName("MainMenu");
   var host=(Grid)menu.Parent;
   var toolbar=new Grid{HorizontalAlignment=HorizontalAlignment.Left};toolbar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});toolbar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   host.Children.Remove(menu);toolbar.Children.Add(menu);host.Children.Add(toolbar);
   activityBell=new Button{Padding=new Thickness(4),Margin=new Thickness(0,0,6,4),MinHeight=0,VerticalAlignment=VerticalAlignment.Center};
   activityBell.SetResourceReference(Control.BackgroundProperty,"SurfaceAlt");activityBell.SetResourceReference(Control.BorderBrushProperty,"Border");
   foreach(var property in new[]{FrameworkElement.HeightProperty,FrameworkElement.WidthProperty})activityBell.SetBinding(property,new Binding("ActualHeight"){Source=TopMenu("CoffeeMenu")});
   var icon=new Grid();icon.SetResourceReference(FrameworkElement.WidthProperty,"UiToolbarIconSize");icon.SetResourceReference(FrameworkElement.HeightProperty,"UiToolbarIconSize");
   var bell=new System.Windows.Shapes.Path{Data=Geometry.Parse("M18,8 C18,4.7 15.3,2 12,2 C8.7,2 6,4.7 6,8 C6,15 3,15 3,17 L21,17 C21,15 18,15 18,8 Z M10,21 C11,22.3 13,22.3 14,21"),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Stretch=Stretch.Uniform};bell.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"Text");icon.Children.Add(bell);
   activityBadge=new TextBlock{Margin=new Thickness(0,-6,-6,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,FontWeight=FontWeights.Bold,Visibility=Visibility.Collapsed};activityBadge.SetResourceReference(TextBlock.FontSizeProperty,"UiFontCaption");activityBadge.SetResourceReference(TextBlock.ForegroundProperty,"Accent");activityBadge.SetResourceReference(TextBlock.BackgroundProperty,"SurfaceAlt");icon.Children.Add(activityBadge);activityBell.Content=icon;
   AutomationProperties.SetName(activityBell,"Activity and notifications");UiHelp.Hint(activityBell,"Activity and notifications (Ctrl+Shift+N)");Grid.SetColumn(activityBell,1);toolbar.Children.Add(activityBell);
   activityPanel=new Border{Padding=new Thickness(18),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Stretch,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,6)};
   activityPanel.SetResourceReference(Border.BackgroundProperty,"Surface");activityPanel.SetResourceReference(Border.BorderBrushProperty,"Border");Grid.SetRow(activityPanel,2);Panel.SetZIndex(activityPanel,20);root.Children.Add(activityPanel);
   var layout=new DockPanel();activityPanel.Child=layout;
   var heading=new DockPanel{Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(heading,Dock.Top);layout.Children.Add(heading);
   var close=new Button{Content="Close",Padding=new Thickness(9,4,9,4)};DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);heading.Children.Add(new TextBlock{Text="Activity",FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
   close.Click+=(s,e)=>CloseActivity();activityBell.Click+=(s,e)=>{if(activityPanel.Visibility==Visibility.Visible)CloseActivity();else OpenActivity();};
   var footerRow=new WrapPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footerRow,Dock.Bottom);layout.Children.Add(footerRow);
   var footer=new Button{Content="Clear completed"};footerRow.Children.Add(footer);showDismissedActivity=new Button{Content="Show dismissed",Visibility=Visibility.Collapsed};showDismissedActivity.Click+=(s,e)=>RestoreDismissedActivity();footerRow.Children.Add(showDismissedActivity);
   footer.Click+=(s,e)=>{activities.RemoveAll(a=>!a.Running&&!a.NeedsReview&&a.Review==null);RenderActivity();};
   activityItems=new StackPanel();var scroll=new ScrollViewer{Content=activityItems,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};MenuScrolling.SetEnabled(scroll,true);layout.Children.Add(scroll);
   activityPanel.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){CloseActivity();e.Handled=true;}};
   Window.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.N&&(Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))==(ModifierKeys.Control|ModifierKeys.Shift)){if(activityPanel.Visibility==Visibility.Visible)CloseActivity();else OpenActivity();e.Handled=true;}};
   Window.PreviewMouseDown+=(s,e)=>DismissActivityOutside(e.OriginalSource as DependencyObject);
   Window.Deactivated+=(s,e)=>{if(activityPanel.Visibility==Visibility.Visible)CloseActivity(false);};
   Window.SizeChanged+=(s,e)=>SizeActivity();SizeActivity();
   activityToast=new Border{CornerRadius=new CornerRadius(6),Padding=new Thickness(12),BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,MaxWidth=420,Visibility=Visibility.Collapsed};activityToast.SetResourceReference(Border.BackgroundProperty,"SurfaceAlt");activityToast.SetResourceReference(Border.BorderBrushProperty,"Border");Grid.SetRow(activityToast,2);Panel.SetZIndex(activityToast,19);root.Children.Add(activityToast);
   var toastLayout=new DockPanel();activityToast.Child=toastLayout;var view=new Button{Content="View",Margin=new Thickness(10,0,0,0)};DockPanel.SetDock(view,Dock.Right);toastLayout.Children.Add(view);view.Click+=(s,e)=>OpenActivity();activityToastText=new TextBlock{TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetLiveSetting(activityToastText,AutomationLiveSetting.Polite);toastLayout.Children.Add(activityToastText);
   activityToastTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(6)};activityToastTimer.Tick+=(s,e)=>{activityToastTimer.Stop();activityToast.Visibility=Visibility.Collapsed;};
   InitializeProcessSummaries();RenderActivity();
  }
  void SizeActivity(){if(activityPanel!=null)activityPanel.Width=Math.Min(Math.Max(420,420*Math.Max(1,settings.TextScalePercent/100.0)),Math.Max(320,Window.ActualWidth-48));SizeProcessSummaries();}
  void OpenActivity(){if(activityPanel==null)return;activityToastTimer.Stop();activityToast.Visibility=Visibility.Collapsed;activityPanel.Visibility=Visibility.Visible;foreach(var entry in activities)entry.Unread=false;RenderActivity();activityPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));}
  void CloseActivity(bool restoreFocus=true){if(activityPanel==null)return;activityPanel.Visibility=Visibility.Collapsed;if(restoreFocus)activityBell.Focus();}
  void DismissActivityOutside(DependencyObject source){
   if(activityPanel==null||activityPanel.Visibility!=Visibility.Visible)return;
   for(var node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):node is FrameworkContentElement?((FrameworkContentElement)node).Parent:LogicalTreeHelper.GetParent(node))if(node==activityPanel||node==activityBell)return;
   // Let the same click focus/activate the control beneath the dismissed panel.
   CloseActivity(false);
  }
  ActivityEntry AddActivity(string title,bool running=false){
   var entry=new ActivityEntry{Title=title,Running=running,WasRunning=running,ProcessTracked=running&&title!="Loading image preview",Status=running?"Preparing…":"",RepositoryRoot=repo==null?null:repo.Root};activities.Insert(0,entry);
   foreach(var old in activities.Where(a=>!a.Running&&!a.NeedsReview&&a.Review==null).Skip(40).ToList())activities.Remove(old);RenderActivity();return entry;
  }
  void NotifyActivity(ActivityEntry entry){
   if(activityDisposed||closing)return;
   if(!entry.Running&&entry.WasRunning){entry.WasRunning=false;entry.BannerDismissed=false;entry.ActivityDismissed=false;}
   bool notify=!entry.Running&&entry.DurationSeconds.HasValue&&entry.DurationSeconds.Value>300;
   entry.Unread=notify&&(activityPanel==null||activityPanel.Visibility!=Visibility.Visible);RenderActivity();
   if(!notify)return;
   if(activityToast==null||activityPanel.Visibility==Visibility.Visible||!Window.IsVisible||Window.WindowState==WindowState.Minimized)return;
   activityToastText.Text=entry.Title+": "+entry.Status;activityToast.Visibility=Visibility.Visible;activityToastTimer.Stop();activityToastTimer.Start();
  }
  void RenderActivity(){
   if(activityItems==null)return;activityItems.Children.Clear();activityCards.Clear();
   if(activities.Count==0)activityItems.Children.Add(new TextBlock{Text="No recent activity. Long-running work and release notifications appear here.",TextWrapping=TextWrapping.Wrap});
   foreach(var entry in OrderedActivities().Where(a=>!a.ActivityDismissed)){
    var panel=new StackPanel();var card=new ActivityCard{Title=new TextBlock{FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap},Status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)},Rate=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)},Bar=new ProgressBar{Maximum=1,Height=6,Margin=new Thickness(0,10,0,0)},Details=new TextBlock{TextWrapping=TextWrapping.Wrap},Actions=new WrapPanel{Margin=new Thickness(0,8,0,0)}};
    AutomationProperties.SetName(card.Bar,entry.Title+" progress");var cardHeading=new DockPanel();var dismiss=ActivityAction("×",()=>DismissActivitySummary(entry));dismiss.Margin=new Thickness(8,0,0,0);dismiss.ToolTip="Dismiss this card; running work continues. Use Show dismissed to restore it.";AutomationProperties.SetName(dismiss,"Dismiss "+entry.Title+" activity");DockPanel.SetDock(dismiss,Dock.Right);cardHeading.Children.Add(dismiss);cardHeading.Children.Add(card.Title);panel.Children.Add(cardHeading);panel.Children.Add(card.Status);panel.Children.Add(card.Bar);panel.Children.Add(card.Rate);var details=new Expander{Header="Details",Content=card.Details,Margin=new Thickness(0,8,0,0)};panel.Children.Add(details);panel.Children.Add(card.Actions);
    var border=new Border{Child=panel,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(12,8,12,10),Margin=new Thickness(0,0,0,8)};border.SetResourceReference(Border.BorderBrushProperty,"Border");border.SetResourceReference(Border.BackgroundProperty,"SurfaceAlt");activityItems.Children.Add(border);activityCards[entry]=card;UpdateActivityCard(entry,card);
   }
   if(showDismissedActivity!=null)showDismissedActivity.Visibility=activities.Any(a=>a.ActivityDismissed||a.BannerDismissed)?Visibility.Visible:Visibility.Collapsed;
   RenderProcessSummaries();UpdateActivityBadge();
  }
  Button ActivityAction(string label,Action action){var button=new Button{Content=label,Margin=new Thickness(0,0,6,6),Padding=new Thickness(8,4,8,4)};button.Click+=(s,e)=>{try{action();}catch(Exception error){var entry=AddActivity(label);entry.Failed=true;entry.Status=error.Message;NotifyActivity(entry);}};return button;}
  void UpdateActivityBadge(){
   if(activityBell==null)return;int unread=activities.Count(a=>a.Unread),running=activities.Count(a=>a.Running),review=activities.Count(a=>a.NeedsReview);activityBadge.Text=unread.ToString();activityBadge.Visibility=unread==0?Visibility.Collapsed:Visibility.Visible;
   string description="Activity: "+running+" running, "+unread+" unread, "+review+" need attention";AutomationProperties.SetName(activityBell,description);activityBell.ToolTip=description+" (Ctrl+Shift+N)";
  }
  static string ActivityRate(ActivityEntry entry){
   var p=entry.Progress;if(p==null)return "Started "+entry.StartedUtc.ToLocalTime().ToString("g");
   string eta=p.Finished||!entry.Running?"":entry.NetworkImport?" · "+RemoteEstimateLabel(p):p.Finalising?" · Finalising":p.Stalled?" · "+(p.Activity!=null&&p.Activity.Contains("Cloud availability")?"Waiting for cloud provider":"Waiting for filesystem"):p.RemainingSeconds.HasValue?" · ETA ~"+EtaEstimate.Format(p.RemainingSeconds.Value):" · ETA estimating";
   return "Elapsed "+PipelineMetrics.Duration(p.ElapsedSeconds)+eta+" · "+(p.TotalKnown?p.Done+" / "+p.Total:p.Done+" inspected · "+p.Total+" discovered")+PipelineMetrics.Reading(p);
  }
  void UpdateActivityCard(ActivityEntry entry,ActivityCard card){
   card.Title.Text=(entry.ProcessTracked?ProcessState(entry)+" · ":"")+entry.Title;card.Status.Text=entry.Status;card.Rate.Text=entry.ProcessTracked?ProcessParameters(entry):ActivityRate(entry);var p=entry.Progress;card.Bar.Visibility=entry.Running||p!=null?Visibility.Visible:Visibility.Collapsed;card.Bar.IsIndeterminate=entry.Running&&!settings.ReducedMotion&&(p==null||!p.TotalKnown&&!p.Finished);card.Bar.Value=p==null?0:Math.Max(0,Math.Min(1,p.ProgressFraction));
   var detail=new StringBuilder(entry.RepositoryRoot==null?"":"Repository: "+entry.RepositoryRoot+"\n");if(p!=null){if(p.Activity!=null)detail.AppendLine(p.Activity);if(p.CopyPhase)detail.AppendLine(p.Workers+" copy workers · "+ImportWorkflow.Size(p.BytesDone)+" / "+ImportWorkflow.Size(p.BytesTotal)+" · "+(p.EffectiveBytesPerSecond/1000000.0).ToString("0.0")+" MB/s");else detail.AppendLine(p.WorkPerSecond.ToString("0.0")+" files/s");if(p.Stages!=null)foreach(var stage in p.Stages)detail.AppendLine(stage.Stage+": "+stage.Files+" files · "+stage.Seconds.ToString("0.0")+"s · "+stage.MBPerSecond.ToString("0.0")+" MB/s");}if(entry.Report!=null)detail.AppendLine("A report is available below.");card.Details.Text=detail.ToString();
   card.Actions.Children.Clear();if(entry.Running&&entry.Cancel!=null)card.Actions.Children.Add(ActivityAction(entry.Canceled?"Canceling…":entry.CancelLabel??"Cancel",()=>{entry.Canceled=true;entry.Cancel();RenderActivity();}));
   if(entry.Running&&entry.Pause!=null)card.Actions.Children.Add(ActivityAction("Pause",()=>{entry.Pause();RenderActivity();}));
   if(!entry.Running&&entry.Resume!=null){var resume=ActivityAction("Resume",entry.Resume);resume.IsEnabled=entry.ResumeAvailable==null||entry.ResumeAvailable();resume.ToolTip=resume.IsEnabled?"Verify completed copies and continue this import.":"Finish the conflicting operation or open this import's repository first.";card.Actions.Children.Add(resume);}
   if(!entry.Running&&entry.Discard!=null)card.Actions.Children.Add(ActivityAction("Cancel import",entry.Discard));
   if(entry.Review!=null){var review=ActivityAction(entry.ActionLabel??"Review results",()=>{if(RepositoryOperationBlocked)return;var action=entry.Review;entry.Review=null;entry.NeedsReview=false;entry.Unread=false;var previous=reviewingActivity;reviewingActivity=entry;try{action();if(entry.NeedsReview||entry.ActionLabel=="View release")entry.Review=action;}catch{entry.Review=action;entry.NeedsReview=true;throw;}finally{reviewingActivity=previous;RenderActivity();}});review.IsEnabled=!RepositoryOperationBlocked;review.ToolTip=review.IsEnabled?null:"Available when the current operation finishes.";card.Actions.Children.Add(review);}
   if(entry.Report!=null)card.Actions.Children.Add(ActivityAction("View report",()=>{activityOpeningReport=true;try{ShowReport(entry.ReportTitle??entry.Title,entry.Report);entry.Unread=false;if(entry.Review==null)entry.NeedsReview=false;}finally{activityOpeningReport=false;RenderActivity();}}));
   if(entry.OutputPath!=null)card.Actions.Children.Add(ActivityAction("Open folder",()=>OpenFolder(entry.OutputPath)));if(!entry.Running&&entry.NeedsReview)card.Actions.Children.Add(ActivityAction("Dismiss",()=>{entry.NeedsReview=false;entry.Unread=false;entry.Review=null;RenderActivity();}));
  }
  void UpdateActivityProgress(ProgressInfo progress){if(currentActivity==null)return;currentActivity.Progress=progress;currentActivity.Status=currentActivity.Canceled?"Canceling after the current operation…":L("StatusLabel").Text;if(currentActivity.Canceled)progress.RemainingSeconds=null;ActivityCard card;if(activityCards.TryGetValue(currentActivity,out card)){
   // Keep focused action controls intact while progress changes.
   card.Status.Text=currentActivity.Status;card.Rate.Text=ProcessParameters(currentActivity);card.Bar.IsIndeterminate=!settings.ReducedMotion&&!progress.TotalKnown&&!progress.Finished;card.Bar.Value=Math.Max(0,Math.Min(1,progress.ProgressFraction));
   if(progress.Stages!=null)card.Details.Text=(currentActivity.RepositoryRoot??"")+"\n"+L("RateLabel").ToolTip+(progress.CopyPhase?" · "+progress.Workers+" copy workers":"")+"\n"+string.Join("\n",progress.Stages.Select(s=>s.Stage+": "+s.Files+" files · "+s.Seconds.ToString("0.0")+"s · "+s.MBPerSecond.ToString("0.0")+" MB/s"));
  }}
  void RecordActivityReport(string title,string text){var entry=completionActivity??AddActivity(title);entry.ReportTitle=title;entry.Report=text;entry.NeedsReview=true;entry.Status="Completed with results to review.";NotifyActivity(entry);}
  void QueueActivityReview(string title,Action review){var entry=completionActivity??AddActivity(title);entry.Title=title;entry.Status="Ready to review";entry.NeedsReview=true;entry.Review=review;entry.ActionLabel="Review results";NotifyActivity(entry);}
  void DisposeActivity(){activityDisposed=true;if(activityToastTimer!=null)activityToastTimer.Stop();foreach(var entry in activities){entry.Cancel=null;entry.Review=null;entry.Pause=null;entry.Resume=null;entry.Discard=null;}activities.Clear();}
  static string ActivityTitle(string caller){switch(caller){case "Scan":return "Scanning source folder";case "Import":return "Importing files";case "ProcessDumpUi":return "Processing Dump folder";case "UploadUsb":return "Importing telescope files";case "ImportEditedFolder":return "Scanning edited images";case "ReviewEditedFolder":case "AddEditedImages":return "Importing edited images";case "IdentifyByPlate":return "Identifying targets";case "ApplyIdentifications":return "Applying target metadata";case "Analyze":return "Analysing rotation";case "ScreenSelection":case "ScreenCaptures":return "Screening captures";case "BackUpArchive":return "Backing up archive";case "ChangeArchiveProtection":return "Changing originals protection";case "RunExport":case "ExportFiles":case "ExportProject":case "ExportTo":return "Exporting files";case "ResetArchive":return "Clearing archive";case "DeleteFiles":return "Deleting selected files";case "RenameTelescope":return "Renaming telescope";case "Identify":return "Identifying targets from filenames";case "CreateEditedCopies":return "Creating edited working copies";case "Edit":case "EditEditedMetadata":return "Saving metadata";case "PreviewImage":case "PreviewSelected":case "PreviewEditedImage":return "Loading image preview";default:return "Processing files";}}
 }
}
