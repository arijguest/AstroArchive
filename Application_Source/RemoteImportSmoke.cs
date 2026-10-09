using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AstroArchive.Remote;
using RemoteConnection=AstroArchive.Remote.Connection;
namespace AstroArchive {
 internal sealed class RemoteUiFixtureSource:ISource {
  readonly string root;readonly RemoteConnection c;
  public RemoteUiFixtureSource(string root,RemoteConnection c){this.root=root;this.c=c;}
  string Local(string path){return path.TrimEnd('\\','/')==c.Folder.TrimEnd('\\','/')?root:Path.Combine(root,RemoteCapturePaths.Relative(c,path));}
  public List<Entry> List(string folder){return new DirectoryInfo(Local(folder)).GetFileSystemInfos().Select(f=>new Entry{Name=f.Name,Path=RemoteCapturePaths.Join(c,folder,f.Name),Directory=f is DirectoryInfo,Size=f is FileInfo?((FileInfo)f).Length:0,Modified=f.LastWriteTimeUtc}).ToList();}
  public Entry Stat(string path){var f=new FileInfo(Local(path));return new Entry{Path=path,Name=f.Name,Size=f.Length,Modified=f.LastWriteTimeUtc};}
  public Stream Open(string path){return File.OpenRead(Local(path));}public void Dispose(){}
 }
 public partial class MainUi {
  void WaitRemote(Task task){if(!task.IsCompleted){var frame=new DispatcherFrame();task.ContinueWith(t=>Window.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));Dispatcher.PushFrame(frame);}task.GetAwaiter().GetResult();}
  static void CaptureRemote(Window window,string path){window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(path))encoder.Save(output);}
  public void SmokeRemoteImport(string output){
   Directory.CreateDirectory(output);Window.Show();Window.UpdateLayout();SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Window.Dispatcher));string fixture=Path.Combine(output,"remote-ui-source");Directory.CreateDirectory(Path.Combine(fixture,"MyWorks","M31_sub"));File.WriteAllBytes(Path.Combine(fixture,"MyWorks","M31_sub","Light_M31_0001.fit"),new byte[5760]);File.WriteAllText(Path.Combine(fixture,"MyWorks","M31_sub","session.json"),"{}");
   var device=new DiscoveredTelescope{Kind="Seestar SMB",Name="Seestar S50 Pro",Host="192.168.1.42",Identity="ui-s50p"};var c=new RemoteConnection{Kind=device.Kind,Host=device.Host,Folder=@"\\192.168.1.42\EMMC Images",IncludeExisting=false};var profile=new TelescopeProfile{Id="My S50 Pro",Model="Seestar S50 Pro",SourceMake="Seestar",RemoteIdentity=device.Identity};
   var dialog=new RemoteImportDialog(Window,new[]{profile},profile,factory:config=>new RemoteUiFixtureSource(fixture,config),find:ct=>{var result=new DiscoveryResult();result.Devices.Add(device);return Task.FromResult(result);},startDiscovery:false);dialog.Dialog.Window.Show();
   try{
    if(dialog.ImportButton.IsEnabled||dialog.LiveButton.IsEnabled||dialog.AdvancedButton==null||dialog.DiscoveryPanel.Visibility!=Visibility.Visible)throw new Exception("Remote discovery did not start with gated import actions.");
    WaitRemote(dialog.Discover());if(!dialog.ConnectButton.IsEnabled)throw new Exception("Discovery did not enable connecting to its single telescope.");WaitRemote(dialog.ConnectSelected());if(dialog.Error.Text!=""||!dialog.LiveButton.IsEnabled||dialog.ImportButton.IsEnabled||dialog.Profiles.SelectedItem!=profile||dialog.ConnectButton.IsDefault)throw new Exception("Connecting did not resolve the saved telescope and import actions.");
    WaitRemote(dialog.Browse(c.Folder));if(!dialog.Files.Items.Cast<RemoteCaptureRow>().Single().Entry.Directory)throw new Exception("Remote folder browser did not expose capture folders.");
    WaitRemote(dialog.Search());if(dialog.Files.Items.Count!=1||!dialog.ImportButton.IsEnabled||dialog.SelectionText.Text.IndexOf("1 selected",StringComparison.Ordinal)<0)throw new Exception("Search all did not select supported captures while excluding sidecars.");
    var row=dialog.Files.Items.Cast<RemoteCaptureRow>().Single();row.Selected=false;if(dialog.ImportButton.IsEnabled)throw new Exception("Clearing the remote selection left import enabled.");row.Selected=true;
    foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(Window,theme);CaptureRemote(dialog.Dialog.Window,Path.Combine(output,"Remote-import-"+theme+".png"));}
    var warning=RemoteImportDialog.LargeImport(Window,Enumerable.Range(0,100).Select(i=>new Entry{Name="capture_"+i+".fits",Size=5760}));warning.Window.Show();try{CaptureRemote(warning.Window,Path.Combine(output,"Remote-large-import.png"));}finally{warning.Window.Close();}
    File.WriteAllText(Path.Combine(output,"remote-ui-passed.txt"),"PASS: discovery-first import gating, saved device identity, folder browsing, search-all selection, sidecar exclusion, live-import action and light/dark rendering.");
   }finally{dialog.Dialog.Window.Close();Theme.Apply(Window,settings.ThemeMode);}
  }
 }
}
