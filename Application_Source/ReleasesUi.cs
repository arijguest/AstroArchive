// Reuse the launcher's bounded HTTPS and checksum validation from Settings.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AstroArchive.Installation;
namespace AstroArchive {
 public partial class MainUi {
  InstallRecord RunningRelease(){
   string app=Assembly.GetExecutingAssembly().Location,root=Path.GetDirectoryName(Path.GetDirectoryName(app));
   var installed=InstallCore.Read(root);
   if(installed!=null&&string.Equals(InstallCore.Managed(root,installed.ActiveDirectory+"\\AstroArchive.exe"),app,StringComparison.OrdinalIgnoreCase))return installed;
   string version=Assembly.GetExecutingAssembly().GetName().Version.ToString(3);return new InstallRecord{Version=version,PackageVersion=version+".0"};
  }
  void Releases(Window owner){
   var d=new FormWindow(owner,"AstroArchive releases",630,470);d.Text("App releases",true);
   InstallRecord installed;try{installed=RunningRelease();}catch(Exception e){d.Text("The installation record could not be read: "+e.Message);d.CloseOnly();d.Show();return;}
   d.Text("Current version: "+installed.Version+"  ·  Package: "+installed.PackageVersion);
   d.Text("Check the latest stable release and download its Windows installer. Downloads are verified before saving. Close AstroArchive before running the installer.");
   var status=new TextBlock{Text="Ready to check for a new release.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.SlateGray,Margin=new Thickness(0,12,0,16)};d.Add(status);
   var progress=new ProgressBar{Height=5,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,16)};d.Add(progress);
   var client=new UpdateClient();UpdateManifest available=null;bool busy=false;Button check=null,download=null;string saved=null;
   Action refresh=()=>{check.IsEnabled=!busy;download.IsEnabled=!busy&&available!=null;progress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;};
   check=d.Button("Check for new releases",async ()=>{
    if(busy)return;busy=true;available=null;status.Text="Checking the latest stable release…";refresh();
    try{available=await Task.Run(()=>client.Check(installed));status.Text=available==null?"AstroArchive is up to date.":"AstroArchive "+available.application_version+" (package "+available.package_version+") is available.";}
    catch(Exception e){status.Text="Could not check for releases. Try again when connected.\n"+e.Message;}
    finally{busy=false;refresh();}
   });
   download=d.Button("Download release",async ()=>{
    if(busy||available==null)return;
    var save=new SaveFileDialog{Title="Save AstroArchive installer",FileName=UpdateClient.InstallerName(available),Filter="Windows installer|*.exe",OverwritePrompt=true};if(save.ShowDialog(d.Window)!=true)return;
    string destination=save.FileName,temporary=destination+"."+Guid.NewGuid().ToString("N")+".partial";var release=available;
    busy=true;status.Text="Downloading and verifying AstroArchive "+release.package_version+"…";refresh();
    try{
     await Task.Run(()=>{
      string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","updates");string verified=client.Prepare(release,cache);
      try{InstallCore.NoLinks(destination);using(var from=File.OpenRead(verified))using(var to=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){from.CopyTo(to);to.Flush(true);}if(!string.Equals(InstallCore.HashFile(temporary),release.sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Saved installer failed verification.");if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);}
      finally{if(File.Exists(temporary))File.Delete(temporary);File.Delete(verified);Directory.Delete(Path.GetDirectoryName(verified));}
     });
     saved=destination;status.Text="Downloaded and verified:\n"+saved+"\n\nClose AstroArchive, then run this installer to update.";
    }catch(Exception e){status.Text="The release could not be downloaded.\n"+e.Message;}
    finally{busy=false;refresh();}
   });download.IsEnabled=false;
   d.Button("Open download folder",()=>{if(saved!=null&&File.Exists(saved))Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+saved+"\""){UseShellExecute=true});});
   d.Window.Closing+=(s,e)=>{if(busy)e.Cancel=true;};d.CloseOnly();d.Show();
  }
 }
}
