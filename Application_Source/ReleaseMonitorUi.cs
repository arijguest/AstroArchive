using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AstroArchive.Installation;

namespace AstroArchive {
 public partial class MainUi {
  ReleaseMonitor releaseMonitor;Task<ReleaseMonitor> releaseMonitorLoading;Task<UpdateManifest> releaseRefreshTask;
  DispatcherTimer releaseTimer;CancellationTokenSource releaseDownloadCancel;bool releaseMonitorDisposed;
  Action releaseChanged;ActivityEntry releaseNotice;
  void InitializeReleaseMonitoring(bool test){
   if(test)return;releaseTimer=new DispatcherTimer(DispatcherPriority.Background,Window.Dispatcher){Interval=TimeSpan.FromSeconds(20)};
   releaseTimer.Tick+=async(s,e)=>{releaseTimer.Interval=TimeSpan.FromMinutes(1);await PeriodicReleaseCheck();};releaseTimer.Start();
  }
  async Task<ReleaseMonitor> EnsureReleaseMonitor(){
   if(releaseMonitor!=null)return releaseMonitor;
   if(releaseMonitorLoading==null)releaseMonitorLoading=Task.Run(()=>{var target=ReleaseTarget();return new ReleaseMonitor(target.Root,target.Comparison,()=>new UpdateClient().Check(target.Comparison));});
   try{var monitor=await releaseMonitorLoading;if(releaseMonitorDisposed)return monitor;releaseMonitor=monitor;monitor.SetAutomatic(!settings.DisableAutomaticReleaseChecks);PublishReleaseState();return monitor;}
   finally{releaseMonitorLoading=null;}
  }
  async Task PeriodicReleaseCheck(){
   if(releaseMonitorDisposed||closing||releaseInstalling||settings.DisableAutomaticReleaseChecks)return;
   try{var monitor=await EnsureReleaseMonitor();if(!releaseMonitorDisposed&&monitor.Due)await CheckReleaseAsync();}catch{/* Quiet retry on the next timer tick; explicit checks expose errors. */}
  }
  Task<UpdateManifest> CheckReleaseAsync(){
   if(releaseRefreshTask!=null)return releaseRefreshTask;var task=RefreshReleaseAsync();releaseRefreshTask=task.IsCompleted?null:task;return task;
  }
  async Task<UpdateManifest> RefreshReleaseAsync(){
   try{var monitor=await EnsureReleaseMonitor();var task=monitor.CheckAsync();PublishReleaseState();return await task;}
   finally{releaseRefreshTask=null;if(!releaseMonitorDisposed)PublishReleaseState();}
  }
  void ApplyReleaseMonitoringPreference(){if(releaseMonitor!=null)releaseMonitor.SetAutomatic(!settings.DisableAutomaticReleaseChecks);}
  void PublishReleaseState(){
   if(releaseMonitorDisposed||releaseMonitor==null)return;var state=releaseMonitor.State;
   if(state.Available!=null){
    bool isNew=releaseNotice==null||releaseNotice.Title!="AstroArchive "+state.Available.package_version+" is available";
    if(isNew){if(releaseNotice!=null){releaseNotice.NeedsReview=false;releaseNotice.Review=null;}releaseNotice=AddActivity("AstroArchive "+state.Available.package_version+" is available");releaseNotice.RepositoryRoot=null;releaseNotice.Status="A new release is ready to review.";releaseNotice.NeedsReview=true;releaseNotice.ActionLabel="View release";releaseNotice.Review=()=>About();}
    if(releaseMonitor.MarkNotified())NotifyActivity(releaseNotice);else RenderActivity();
   }else if(state.CheckedUtc!=null&&state.Error==null&&releaseNotice!=null){releaseNotice.NeedsReview=false;releaseNotice.Review=null;releaseNotice.Status="No newer release is available.";releaseNotice=null;RenderActivity();}
   if(releaseChanged!=null)releaseChanged();
  }
  string ReleaseStatusText(){
   if(releaseMonitor==null)return "Release information has not been checked yet.";
   var state=releaseMonitor.State;string status=state.Available==null?(state.CheckedUtc==null?"Release information has not been checked yet.":"No newer release was found at the last successful check."):"AstroArchive "+state.Available.application_version+" (package "+state.Available.package_version+") is available.";
   DateTime date;if(DateTime.TryParse(state.CheckedUtc,out date))status+="\nLast successful check: "+date.ToLocalTime().ToString("g");
   if(releaseMonitor.Busy)status+="\nChecking releases…";else if(state.Error!=null)status+="\nThe latest check could not finish: "+state.Error;
   return status;
  }
  void AddAboutReleases(FormWindow dialog){
   dialog.Text("App releases",true);var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,10)};dialog.Add(status);
   var check=dialog.Button("Check now",async()=>{checkReleaseButtonBusy=true;try{await CheckReleaseAsync();}catch(Exception error){status.Text="Could not check for releases. "+error.Message;}finally{checkReleaseButtonBusy=false;if(!releaseMonitorDisposed)PublishReleaseState();}});
   var notes=dialog.Button("Release notes",()=>{if(releaseMonitor!=null&&releaseMonitor.State.Available!=null)OpenWebsite(UpdateClient.ReleasePage(releaseMonitor.State.Available));});
   var install=dialog.Button("Install and restart",()=>{if(releaseMonitor!=null&&releaseMonitor.State.Available!=null&&!RepositoryOperationBlocked){var manifest=releaseMonitor.State.Available;dialog.Window.Close();ConfirmReleaseInstallation(manifest);}});
   bool closed=false;Action refresh=()=>{if(closed)return;status.Text=ReleaseStatusText();check.IsEnabled=!checkReleaseButtonBusy&&(releaseMonitor==null||!releaseMonitor.Busy)&&!releaseInstalling;notes.IsEnabled=releaseMonitor!=null&&releaseMonitor.State.Available!=null;install.IsEnabled=notes.IsEnabled&&!RepositoryOperationBlocked;install.ToolTip=RepositoryOperationBlocked?"Available when the current operation finishes.":null;};
   releaseChanged+=refresh;dialog.Window.Closed+=(s,e)=>{closed=true;releaseChanged-=refresh;};refresh();
  }
  bool checkReleaseButtonBusy;
  void ConfirmReleaseInstallation(UpdateManifest release){
   if(RepositoryOperationBlocked)return;
   var dialog=new FormWindow(Window,"Install AstroArchive release",620,430);dialog.Text("Install package "+release.package_version,true);dialog.Text("The release will download and be verified in the background. AstroArchive will then close, install it and restart. Repositories, images and settings are retained.");dialog.Button("Read release notes",()=>OpenWebsite(UpdateClient.ReleasePage(release)));dialog.Accept("Install and restart",()=>true);if(dialog.Show())StartBackgroundReleaseInstallation(release);
  }
  async void StartBackgroundReleaseInstallation(UpdateManifest release){await InstallReleaseInBackground(release);}
  async Task InstallReleaseInBackground(UpdateManifest release){
   if(RepositoryOperationBlocked||closing)return;releaseInstalling=true;releaseDownloadCancel=new CancellationTokenSource();var token=releaseDownloadCancel.Token;var entry=AddActivity("Installing AstroArchive "+release.package_version,true);entry.RepositoryRoot=null;entry.Status="Downloading and verifying the release…";entry.Cancel=()=>releaseDownloadCancel.Cancel();RenderActivity();SetBusy(true);if(releaseChanged!=null)releaseChanged();
   var watch=Stopwatch.StartNew();var eta=new EtaEstimate();string verified=null;DateTime attemptedUtc=DateTime.MinValue;bool handedOff=false;Exception installFailure=null;var client=new UpdateClient();
   try{
    SaveSettings();var target=ReleaseTarget();
    IProgress<UpdateDownloadProgress> reporting=new System.Progress<UpdateDownloadProgress>(p=>{if(!entry.Running||releaseMonitorDisposed||entry.Cancel==null)return;double seconds=watch.Elapsed.TotalSeconds;double? remaining=p.Received>0&&seconds>=1?(double?)((p.Total-p.Received)/(p.Received/seconds)):null;entry.Progress=new ProgressInfo{Stage="Downloading",CopyPhase=true,TotalKnown=p.Total>0,BytesDone=p.Received,BytesTotal=p.Total,Done=p.Percent,Total=100,ElapsedSeconds=seconds,RemainingSeconds=eta.Update(remaining,seconds),ProgressFraction=p.Total>0?(double)p.Received/p.Total:0,EffectiveBytesPerSecond=seconds>0?p.Received/seconds:0,EtaProvisional=true};entry.Status="Downloading package "+release.package_version+" · "+p.Percent+"%";ActivityCard card;if(activityCards.TryGetValue(entry,out card)){card.Status.Text=entry.Status;card.Rate.Text=ActivityRate(entry);card.Bar.Value=entry.Progress.ProgressFraction;card.Bar.IsIndeterminate=false;card.Details.Text=ImportWorkflow.Size(p.Received)+" / "+ImportWorkflow.Size(p.Total)+" · "+(entry.Progress.EffectiveBytesPerSecond/1000000.0).ToString("0.0")+" MB/s";}});
    client.Progress=p=>reporting.Report(p);verified=await Task.Run(()=>client.Prepare(release,target.Cache,token));client.Progress=null;token.ThrowIfCancellationRequested();
    entry.Status="Verifying installation and saving the archive…";entry.Cancel=null;RenderActivity();
    WindowsIntegration.EnsureNoOtherApplications(target.Root,Process.GetCurrentProcess().Id);if(repo!=null)repo.Checkpoint(CancellationToken.None);
    int waitPid=WindowsIntegration.UpdateWaitProcess(target.Root,Process.GetCurrentProcess().Id,System.Reflection.Assembly.GetExecutingAssembly().Location);var start=UpdateClient.InstallerStartInfo(release,verified,target.Root,waitPid);attemptedUtc=DateTime.UtcNow;
    using(var process=WindowsIntegration.StartProcess(start))if(process==null)throw new IOException("The release installer could not start.");handedOff=true;entry.Status="Installer started. AstroArchive is restarting…";
   }catch(OperationCanceledException){entry.Canceled=true;entry.Status="Update canceled. The current version remains installed.";}
   catch(Exception error){entry.Failed=true;installFailure=error;entry.Status="The release could not be installed. AstroArchive remains open.";entry.ReportTitle="Release installation failed";entry.Report=UpdateDiagnostics.Message(error,null);entry.NeedsReview=true;}
   finally{client.Progress=null;entry.Running=false;entry.Cancel=null;releaseDownloadCancel.Dispose();releaseDownloadCancel=null;releaseInstalling=false;SetBusy(false);if(releaseChanged!=null)releaseChanged();}
   if(installFailure!=null){string log=await Task.Run(()=>UpdateDiagnostics.Record(installFailure,release,verified,attemptedUtc));entry.Report=UpdateDiagnostics.Message(installFailure,log);}
   if(!handedOff)NotifyActivity(entry);
   if(handedOff)Application.Current.Shutdown();else if(closing)Window.Close();
  }
  void DisposeReleaseMonitoring(){releaseMonitorDisposed=true;if(releaseTimer!=null)releaseTimer.Stop();releaseChanged=null;if(releaseDownloadCancel!=null)releaseDownloadCancel.Cancel();}
 }
}
