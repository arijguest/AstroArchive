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
  async Task WaitRemoteUntil(Func<bool> condition){var clock=System.Diagnostics.Stopwatch.StartNew();while(!condition()){if(clock.Elapsed.TotalSeconds>20)throw new Exception("Remote session state timed out.");await Task.Delay(25);}}
  static void RemoteSmokeFits(string file,int value){
   Directory.CreateDirectory(Path.GetDirectoryName(file));string[] cards={"SIMPLE  =                    T","BITPIX  =                   16","NAXIS   =                    2","NAXIS1  =                   64","NAXIS2  =                   48","BZERO   =                32768","OBJECT  = 'M31'","EXPTIME =                   10","DATE-OBS= '2026-10-09T20:00:00'","END"};
   using(var stream=File.Create(file)){var header=System.Text.Encoding.ASCII.GetBytes(string.Concat(cards.Select(c=>c.PadRight(80))).PadRight(2880));stream.Write(header,0,header.Length);for(int i=0;i<64*48;i++){short pixel=(short)(value-32768);stream.WriteByte((byte)(pixel>>8));stream.WriteByte((byte)pixel);}while(stream.Length%2880!=0)stream.WriteByte(0);}
  }
  void SmokeRemoteSessions(string output){
   var original=repo;string saved=Util.Serialize(settings),sourceText=T("SourceBox").Text;var previousPlan=plan;var tasks=new List<Task>();Repository temporary=null;
   string firstRoot=Path.Combine(output,"session-s50"),secondRoot=Path.Combine(output,"session-dwarf");string firstFile=Path.Combine(firstRoot,"MyWorks","Light_old_s50.fit"),secondFile=Path.Combine(secondRoot,"MyWorks","Light_old_dwarf.fit");RemoteSmokeFits(firstFile,2200);RemoteSmokeFits(secondFile,2300);
   var first=new RemoteConnection{Kind="Seestar SMB",Host="127.0.0.1",Folder=@"\\127.0.0.1\EMMC Images",IncludeExisting=false,PollSeconds=2,LimitMB=0};var second=new RemoteConnection{Kind="DWARF FTP",Host="127.0.0.2",Folder="/",IncludeExisting=false,PollSeconds=2,LimitMB=0};
   var firstProfile=new TelescopeProfile{Id="Live UI S50",Model="Seestar S50",SourceMake="Seestar",Camera="Auto"};var secondProfile=new TelescopeProfile{Id="Live UI DWARF",Model="Dwarf 3",SourceMake="DWARFLAB",Camera="Auto"};
   Func<RemoteConnection,ISource> factory=c=>new RemoteUiFixtureSource(c.Host==first.Host?firstRoot:secondRoot,c);
   try{
    temporary=new Repository(Path.Combine(output,"session-archive"));repo=temporary;Refresh();
    tasks.Add(StartRemoteImport(new RemoteImportRequest{Connection=first,Profile=firstProfile,Live=true},factory));var a=remoteSessions.Single();WaitRemote(WaitRemoteUntil(()=>a.Latest!=null&&a.Latest.Stage=="Live import"));
    if(!B("RemoteTelescopeButton").IsEnabled||B("RemoteLiveStatusButton").Visibility!=Visibility.Visible||B("ScanButton").IsEnabled||cancel!=null)throw new Exception("Live import did not keep telescope selection available and protect conflicting operations.");
    tasks.Add(StartRemoteImport(new RemoteImportRequest{Connection=second,Profile=secondProfile,Live=true},factory));var b=remoteSessions.Single(s=>s!=a);WaitRemote(WaitRemoteUntil(()=>b.Latest!=null&&b.Latest.Stage=="Live import"));
    if(!Convert.ToString(B("RemoteLiveStatusButton").Content).Contains("2 live")||!a.Activity.Running||!b.Activity.Running||a.Activity.Cancel==b.Activity.Cancel)throw new Exception("Two telescope watchers did not receive independent state and stop controls.");
    if(ValidateRemoteRequest(new RemoteImportRequest{Connection=second,Profile=firstProfile})==null||ValidateRemoteRequest(new RemoteImportRequest{Connection=first,Profile=firstProfile,Live=true})==null)throw new Exception("Active device/profile and duplicate watcher conflicts were not caught.");
    var device=new DiscoveredTelescope{Kind=first.Kind,Host=first.Host,Name="Seestar S50"};var picker=new RemoteImportDialog(Window,settings.Telescopes,firstProfile,factory:factory,startDiscovery:false,isLiveActive:HasLiveImport,validate:ValidateRemoteRequest);picker.Dialog.Window.Show();
    try{WaitRemote(picker.Connect(device,first));WaitRemote(picker.Search());if(picker.LiveButton.IsEnabled||!picker.ImportButton.IsEnabled||!picker.LiveScopeText.Text.Contains("underway"))throw new Exception("Reconnecting during live import did not allow file selection or explain the active watcher.");foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(Window,theme);CaptureRemote(picker.Dialog.Window,Path.Combine(output,"Remote-live-select-"+theme+".png"));}}
    finally{picker.Dialog.Window.Close();}
    List<Entry> selection;using(var remote=factory(first))selection=RemoteCaptureCatalog.Search(remote,first,first.Folder,CancellationToken.None);
    WaitRemote(StartRemoteImport(new RemoteImportRequest{Connection=first,Profile=firstProfile,Files=selection},factory));
    if(!a.Activity.Running||!b.Activity.Running||remoteSessions.Count!=2||repo.All().Count!=1)throw new Exception("Selected download interrupted a watcher or failed to archive its capture.");
    WaitRemote(StartRemoteImport(new RemoteImportRequest{Connection=first,Profile=firstProfile,Files=selection},factory));
    if(repo.All().Count!=1||!activities.First().Status.Contains("1 already present"))throw new Exception("Repeating a selected download during live import produced duplicate archive rows or lost its duplicate summary.");
    foreach(string theme in new[]{"Light","Dark"})foreach(int scale in new[]{100,150}){settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();PumpPopupLayout();if(B("RemoteLiveStatusButton").ActualWidth<=0||!B("RemoteTelescopeButton").IsEnabled)throw new Exception("Live indicator or connection control was hidden by appearance settings.");CaptureRemote(Window,Path.Combine(output,"Remote-live-status-"+theme+scale+".png"));}
    B("RemoteLiveStatusButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(activityPanel.Visibility!=Visibility.Visible||!activityCards.ContainsKey(a.Activity)||!activityCards.ContainsKey(b.Activity))throw new Exception("Live indicator did not expose per-telescope Activity controls.");
    var stop=activityCards[a.Activity].Actions.Children.OfType<Button>().Single(button=>Convert.ToString(button.Content)=="Stop live import");stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WaitRemote(tasks[0]);
    if(!b.Activity.Running||b.Cancellation.IsCancellationRequested||remoteSessions.Count!=1||!Convert.ToString(B("RemoteLiveStatusButton").Content).Contains("Live import underway"))throw new Exception("Stopping one watcher also stopped the other or removed its indicator.");CloseActivity();
    string fresh=Path.Combine(secondRoot,"MyWorks","Light_new_dwarf.fit");RemoteSmokeFits(fresh,2400);WaitRemote(WaitRemoteUntil(()=>repo.All().Any(f=>f.OriginalName==Path.GetFileName(fresh))));
    if(repo.All().Single(f=>f.OriginalName==Path.GetFileName(fresh)).Telescope!=secondProfile.Id)throw new Exception("Remaining watcher lost its telescope identity.");
    B("CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WaitRemote(tasks[1]);
    if(remoteSessions.Count!=0||RepositoryOperationBlocked||B("RemoteLiveStatusButton").Visibility!=Visibility.Collapsed||!B("RemoteTelescopeButton").IsEnabled||B("CancelButton").Visibility!=Visibility.Collapsed||repo.Verify(CancellationToken.None,p=>{})!=0)throw new Exception("Stopping all sessions left a busy UI, stale indicator or damaged archive.");
    if(!File.Exists(firstFile)||!File.Exists(secondFile)||!File.Exists(fresh))throw new Exception("Session cancellation removed telescope originals.");
    File.WriteAllText(Path.Combine(output,"remote-sessions-passed.txt"),"PASS: simultaneous Seestar/DWARF watchers, additional selections and duplicate summaries while live, device/profile conflict checks, individual Activity stop, remaining watcher continues, Stop all cleanup, verified archive and retained originals; indicator and selection dialog in light/dark at 100/150% text.");
   }finally{StopRemoteImports();foreach(var task in tasks)WaitRemote(task);CloseActivity();repo=original;if(temporary!=null)temporary.Dispose();settings=Util.Deserialize<Settings>(saved);T("SourceBox").Text=sourceText;plan=previousPlan;ReloadScopes(settings.SelectedTelescope,false);SaveSettings();ApplyAppearance();Refresh();SetBusy(false);}
  }
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
   SmokeRemoteSessions(output);
  }
 }
}
