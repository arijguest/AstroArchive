// Release checks and verified installation live in Preferences > Updates.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using AstroArchive.Installation;
namespace AstroArchive {
 public sealed class ReleaseSettingsPanel {
  public readonly StackPanel View=new StackPanel();
  public readonly Button Check=new Button{Content="Check for releases",Margin=new Thickness(0,0,12,8)};
  public readonly Button Install=new Button{Content="Install and restart",Margin=new Thickness(0,0,0,8),IsEnabled=false};
  public readonly TextBlock Status=new TextBlock{Text="Ready to check for a new release.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,12)};
  public readonly ProgressBar Progress=new ProgressBar{Height=5,Maximum=100,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,12)};
  public readonly TextBox Notes=new TextBox{IsReadOnly=true,VerticalContentAlignment=VerticalAlignment.Top,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=180};
  public readonly Expander NotesSection=new Expander{Header="Release notes",Visibility=Visibility.Collapsed,Margin=new Thickness(0,4,0,12)};
  public readonly Button ReleaseLink=new Button{Content="View release on GitHub",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,0)};
  readonly FormWindow form;readonly Func<bool> save;readonly Func<Task<UpdateManifest>> check;readonly Func<UpdateManifest,Task<string>> notes;
  readonly Func<UpdateManifest,IProgress<UpdateDownloadProgress>,Task> install;readonly Action finished;readonly Action<UpdateManifest> open;readonly Action<bool> installationState;
  public UpdateManifest Available{get;private set;}public bool Busy{get;private set;}public bool Installing{get;private set;}bool closed;
  public ReleaseSettingsPanel(FormWindow form,string version,string installation,Func<bool> save,Func<Task<UpdateManifest>> check,Func<UpdateManifest,Task<string>> notes,Func<UpdateManifest,IProgress<UpdateDownloadProgress>,Task> install,Action finished,Action<UpdateManifest> open,Action<bool> installationState=null){
   this.form=form;this.save=save;this.check=check;this.notes=notes;this.install=install;this.finished=finished;this.open=open;this.installationState=installationState??(active=>{});
   form.Group(()=>{
    form.Text("Updates",true);form.Text(version);
    form.Text("Installation saves your settings, downloads and verifies the release, then restarts AstroArchive. Your repositories and images are kept.");
    if(!string.IsNullOrEmpty(installation))form.Text("Installation folder: "+installation);
    form.Add(View);
   });
   AutomationProperties.SetLiveSetting(Status,AutomationLiveSetting.Polite);Theme.Bind(Status,TextBlock.ForegroundProperty,"Muted");View.Children.Add(Status);View.Children.Add(Progress);
   var actions=new WrapPanel();actions.Children.Add(Check);actions.Children.Add(Install);View.Children.Add(actions);
   Theme.Bind(Install,Control.BackgroundProperty,"Accent");Theme.Bind(Install,Control.ForegroundProperty,"AccentText");
   var reading=new StackPanel();reading.Children.Add(Notes);reading.Children.Add(ReleaseLink);NotesSection.Content=reading;View.Children.Add(NotesSection);
   UiHelp.Describe(Install,"Save the current settings, install the verified release and restart AstroArchive.");
   AutomationProperties.SetName(Progress,"Release download progress");AutomationProperties.SetName(Notes,"Release notes");
   Check.Click+=async(s,e)=>await CheckAsync();Install.Click+=async(s,e)=>await InstallAsync();
   ReleaseLink.Click+=(s,e)=>{if(Available!=null)try{open(Available);}catch(Exception error){SetStatus("The release page could not open. "+error.Message);}};
   form.Window.Closing+=(s,e)=>{if(Installing)e.Cancel=true;};form.Window.Closed+=(s,e)=>closed=true;
  }
  void SetStatus(string text){Status.Text=text;AutomationProperties.SetHelpText(Status,text);}
  void Refresh(){if(closed)return;Check.IsEnabled=!Busy;Install.IsEnabled=!Busy&&Available!=null;Progress.Visibility=Busy?Visibility.Visible:Visibility.Collapsed;}
  public async Task CheckAsync(){
   if(Busy||closed)return;Busy=true;Available=null;NotesSection.Visibility=Visibility.Collapsed;Progress.IsIndeterminate=true;SetStatus("Checking the latest stable release…");Refresh();
   try{
    var release=await check();if(closed)return;Available=release;
    SetStatus(release==null?"No newer release is available.":"AstroArchive "+release.application_version+" (package "+release.package_version+") is available.");
    if(release!=null){NotesSection.Visibility=Visibility.Visible;NotesSection.IsExpanded=true;Notes.Text="Loading release notes…";
     try{string text=await notes(release);if(!closed)Notes.Text=string.IsNullOrWhiteSpace(text)?"No release notes were provided.":text;}
     catch(Exception){if(!closed)Notes.Text="Release notes could not be loaded. View the release on GitHub below.";}
    }
   }catch(Exception error){if(!closed)SetStatus("Could not check for releases. "+error.Message);}
   finally{Busy=false;if(!closed)Progress.IsIndeterminate=false;Refresh();}
  }
  public async Task InstallAsync(){
   if(Busy||closed||Available==null)return;
   installationState(true);
   try{
   try{if(!save())return;}catch(Exception error){SetStatus("Settings could not be saved. "+error.Message);return;}
   var release=Available;Busy=Installing=true;Progress.Value=0;Progress.IsIndeterminate=false;SetStatus("Downloading and verifying AstroArchive "+release.package_version+"…");Refresh();
   var unlock=form.LockEditing(View);bool started=false;
   try{
    var reporting=new System.Progress<UpdateDownloadProgress>(p=>{if(closed||!Installing)return;Progress.Value=p.Percent;SetStatus("Downloading package "+release.package_version+": "+p.Percent+"% · "+ImportWorkflow.Size(p.Received)+" / "+ImportWorkflow.Size(p.Total));});
    await install(release,reporting);started=true;SetStatus("Installing the release. AstroArchive will restart automatically.");
   }catch(Exception error){if(!closed)SetStatus("The release could not be installed. AstroArchive remains open.\n"+error.Message);}
   finally{Busy=Installing=false;unlock();Refresh();}
   if(started&&!closed)finished();
   }finally{installationState(false);}
  }
 }
 public partial class MainUi {
  bool releaseInstalling;
  UpdateTarget ReleaseTarget(){
   string executable=Assembly.GetExecutingAssembly().Location;
   return UpdateClient.ResolveTarget(executable,Assembly.GetExecutingAssembly().GetName().Version.ToString(3),WindowsIntegration.RegisteredRoot(),FileVersionInfo.GetVersionInfo(executable).FileVersion);
  }
  void CheckUpdateNotice(){
   try{var target=ReleaseTarget();var receipt=UpdateClient.ConsumeInstalledUpdate(target.Root,target.Running.PackageVersion);if(receipt==null)return;
    L("UpdateNoticeText").Text="Update complete: AstroArchive package "+receipt.PackageVersion+" is installed.";((FrameworkElement)Window.FindName("UpdateNotice")).Visibility=Visibility.Visible;
   }catch{ /* Update confirmation must not prevent the application opening. */ }
  }
  ReleaseSettingsPanel AddReleaseSettings(FormWindow d,Func<bool> save){
   UpdateTarget target=null;string version,installation=null;
   try{target=ReleaseTarget();version="Current version: "+target.Running.Version+" · Package: "+target.Running.PackageVersion;
    if(target.Existing==null||target.Root!=Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))installation=target.Root;
   }catch(Exception error){version="The installation record could not be read: "+error.Message;}
   var client=new UpdateClient();
   return new ReleaseSettingsPanel(d,version,installation,save,
    ()=>Task.Run(()=>{target=ReleaseTarget();return client.Check(target.Comparison);}),
    release=>Task.Run(()=>client.ReleaseNotes(release)),
    async(release,reporting)=>{
     if(cancel!=null)throw new InvalidOperationException("Wait for the current file operation to finish before installing.");
     string verified=null;DateTime attemptedUtc=DateTime.MinValue;Exception failure=null;
     try{
      client.Progress=p=>reporting.Report(p);verified=await Task.Run(()=>client.Prepare(release,target.Cache));client.Progress=null;
      WindowsIntegration.EnsureNoOtherApplications(target.Root,Process.GetCurrentProcess().Id);
      if(repo!=null)repo.Checkpoint(CancellationToken.None);
      int waitPid=WindowsIntegration.UpdateWaitProcess(target.Root,Process.GetCurrentProcess().Id,Assembly.GetExecutingAssembly().Location);
      var start=UpdateClient.InstallerStartInfo(release,verified,target.Root,waitPid);attemptedUtc=DateTime.UtcNow;
      using(var process=WindowsIntegration.StartProcess(start))if(process==null)throw new IOException("The release installer could not start.");
     }catch(Exception error){failure=error;}
     finally{client.Progress=null;}
     if(failure!=null){string log=await Task.Run(()=>UpdateDiagnostics.Record(failure,release,verified,attemptedUtc));throw new IOException(UpdateDiagnostics.Message(failure,log),failure);}
    },()=>Application.Current.Shutdown(),release=>OpenWebsite(UpdateClient.ReleasePage(release)),active=>releaseInstalling=active);
  }
 }
}
