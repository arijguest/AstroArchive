// Settings installs verified releases through the same handoff as the launcher.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AstroArchive.Installation;
namespace AstroArchive {
 public partial class MainUi {
  UpdateTarget ReleaseTarget(){
   string executable=Assembly.GetExecutingAssembly().Location;
   return UpdateClient.ResolveTarget(executable,Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
    WindowsIntegration.RegisteredRoot(),FileVersionInfo.GetVersionInfo(executable).FileVersion);
  }
  void Releases(Window owner){
   if(cancel!=null)return;
   var d=new FormWindow(owner,"AstroArchive releases",630,500);d.Text("App releases",true);
   UpdateTarget target;try{target=ReleaseTarget();}catch(Exception e){d.Text("The installation record could not be read: "+e.Message);d.CloseOnly();d.Show();return;}
   d.Text("Current version: "+target.Running.Version+"  ·  Package: "+target.Running.PackageVersion);
   d.Text("Install release downloads and verifies the update, closes AstroArchive, installs it and restarts the app. Your repositories, images and settings are kept.");
   if(target.Existing==null||target.Root!=Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))d.Text("Installation folder: "+target.Root);
   var status=new TextBlock{Text="Ready to check for a new release.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16)};Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");d.Add(status);
   var progress=new ProgressBar{Height=5,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,16)};d.Add(progress);
   var client=new UpdateClient();UpdateManifest available=null;bool busy=false;Button check=null,install=null;
   Action refresh=()=>{check.IsEnabled=!busy;install.IsEnabled=!busy&&available!=null;progress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;};
   check=d.Button("Check for new releases",async ()=>{
    if(busy)return;busy=true;available=null;status.Text="Checking the latest stable release…";refresh();
    try{target=ReleaseTarget();available=await Task.Run(()=>client.Check(target.Comparison));status.Text=available==null?"No newer release is available.":"AstroArchive "+available.application_version+" (package "+available.package_version+") is available.";}
    catch(Exception e){status.Text="Could not check for releases.\n"+e.Message;}
    finally{busy=false;refresh();}
   });
   install=d.Button("Install release",async ()=>{
    if(busy||available==null||cancel!=null)return;
    var release=available;bool started=false;
    busy=true;status.Text="Downloading and verifying AstroArchive "+release.package_version+"…";refresh();
    try{
     string verified=await Task.Run(()=>client.Prepare(release,target.Cache));
     WindowsIntegration.EnsureNoOtherApplications(target.Root,Process.GetCurrentProcess().Id);
     status.Text="Saving settings and archive index before installation…";
     SaveSettings();if(repo!=null)repo.Checkpoint(CancellationToken.None);
     int waitPid=WindowsIntegration.UpdateWaitProcess(target.Root,Process.GetCurrentProcess().Id,Assembly.GetExecutingAssembly().Location);
     using(var process=Process.Start(UpdateClient.InstallerStartInfo(release,verified,target.Root,waitPid)))
      if(process==null)throw new IOException("The release installer could not start.");
     started=true;status.Text="Installing the release. AstroArchive will restart automatically.";
    }catch(Exception e){status.Text="The release could not be installed. AstroArchive remains open.\n"+e.Message;}
    finally{busy=false;refresh();}
    // Shutdown closes both modal settings windows and disposes the repository.
    // The installer waits for this app (and its launcher) to exit before replacing files.
    if(started)Application.Current.Shutdown();
   });install.IsEnabled=false;
   d.Window.Closing+=(s,e)=>{if(busy)e.Cancel=true;};d.CloseOnly();d.Show();
  }
 }
}
