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
  void CheckUpdateNotice(){
   try{var target=ReleaseTarget();var receipt=UpdateClient.ConsumeInstalledUpdate(target.Root,target.Running.PackageVersion);if(receipt==null)return;
    L("UpdateNoticeText").Text="Update complete: AstroArchive package "+receipt.PackageVersion+" is installed.";((FrameworkElement)Window.FindName("UpdateNotice")).Visibility=Visibility.Visible;
   }catch{ /* Update confirmation must not prevent the application opening. */ }
  }
  void Releases(Window owner){
   if(cancel!=null)return;
   var d=new FormWindow(owner,"Install a release",820,760);d.Window.MinWidth=700;d.Window.MinHeight=600;d.Text("Install a release",true);
   UpdateTarget target;try{target=ReleaseTarget();}catch(Exception e){d.Text("The installation record could not be read: "+e.Message);d.CloseOnly();d.Show();return;}
   d.Text("Current version: "+target.Running.Version+"  ·  Package: "+target.Running.PackageVersion);
   d.Text("Install release downloads and verifies the update, closes AstroArchive, installs it and restarts the app. Your repositories, images and settings are kept.");
   if(target.Existing==null||target.Root!=Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))d.Text("Installation folder: "+target.Root);
   var status=new TextBlock{Text="Ready to check for a new release.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16)};Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");d.Add(status);
   var progress=new ProgressBar{Height=5,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,16)};d.Add(progress);
   var notes=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=180,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,16)};d.Add(notes);
   var client=new UpdateClient();UpdateManifest available=null;bool busy=false,downloading=false;Button check=null,install=null;
   var releasePage=new Button{Content="View release notes on GitHub",HorizontalAlignment=HorizontalAlignment.Left,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,12)};releasePage.Click+=(s,e)=>{if(available!=null)Process.Start(new ProcessStartInfo(UpdateClient.ReleasePage(available)){UseShellExecute=true});};d.Add(releasePage);
   Action refresh=()=>{check.IsEnabled=!busy;install.IsEnabled=!busy&&available!=null;progress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=!downloading;};
   var actions=new WrapPanel{Margin=new Thickness(0,8,0,0)};
   check=new Button{Content="Check for releases",MinWidth=170,MinHeight=42,Margin=new Thickness(0,0,12,12)};
   install=new Button{Content="Install release",MinWidth=150,MinHeight=42,Margin=new Thickness(0,0,0,12),Background=new SolidColorBrush(Color.FromRgb(23,107,116)),Foreground=Brushes.White};
   actions.Children.Add(check);actions.Children.Add(install);d.Add(actions);
   check.Click+=async (sender,args)=>{
    if(busy)return;busy=true;available=null;notes.Visibility=releasePage.Visibility=Visibility.Collapsed;status.Text="Checking the latest stable release…";refresh();
    try{target=ReleaseTarget();available=await Task.Run(()=>client.Check(target.Comparison));status.Text=available==null?"No newer release is available.":"AstroArchive "+available.application_version+" (package "+available.package_version+") is available.";
     if(available!=null){notes.Visibility=releasePage.Visibility=Visibility.Visible;notes.Text="Loading release notes…";try{notes.Text=await Task.Run(()=>client.ReleaseNotes(available));}catch(Exception){notes.Text="Release notes could not be loaded. View them on GitHub using the link below.";}}
    }
    catch(Exception e){status.Text="Could not check for releases.\n"+e.Message;}
    finally{busy=false;refresh();}
   };
   install.Click+=async (sender,args)=>{
    if(busy||available==null||cancel!=null)return;
    var release=available;bool started=false;
    busy=true;downloading=true;progress.Value=0;progress.Maximum=100;status.Text="Downloading and verifying AstroArchive "+release.package_version+"…";refresh();
    try{
     IProgress<UpdateDownloadProgress> reporting=new Progress<UpdateDownloadProgress>(p=>{if(!busy||!downloading)return;progress.Value=p.Percent;status.Text="Downloading package "+release.package_version+": "+p.Percent+"% · "+ImportWorkflow.Size(p.Received)+" / "+ImportWorkflow.Size(p.Total);});
     client.Progress=p=>reporting.Report(p);
     string verified=await Task.Run(()=>client.Prepare(release,target.Cache));client.Progress=null;downloading=false;refresh();
     WindowsIntegration.EnsureNoOtherApplications(target.Root,Process.GetCurrentProcess().Id);
     status.Text="Saving settings and archive index before installation…";
     SaveSettings();if(repo!=null)repo.Checkpoint(CancellationToken.None);
     int waitPid=WindowsIntegration.UpdateWaitProcess(target.Root,Process.GetCurrentProcess().Id,Assembly.GetExecutingAssembly().Location);
     using(var process=Process.Start(UpdateClient.InstallerStartInfo(release,verified,target.Root,waitPid)))
      if(process==null)throw new IOException("The release installer could not start.");
     started=true;status.Text="Installing the release. AstroArchive will restart automatically.";
    }catch(Exception e){status.Text="The release could not be installed. AstroArchive remains open.\n"+e.Message;}
    finally{client.Progress=null;downloading=false;busy=false;refresh();}
    // Shutdown closes both modal settings windows and disposes the repository.
    // The installer waits for this app (and its launcher) to exit before replacing files.
    if(started)Application.Current.Shutdown();
   };install.IsEnabled=false;
   d.Window.Closing+=(s,e)=>{if(busy)e.Cancel=true;};d.CloseOnly();d.Show();
  }
 }
}
