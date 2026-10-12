using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AstroArchive.Remote;
using RemoteConnection = AstroArchive.Remote.Connection;

namespace AstroArchive {
 // Live import for capture software that saves to a folder on this PC or a mapped
 // share (N.I.N.A., SharpCap, ASIAIR, Ekos…). It runs as a live telescope session,
 // so Activity, pause, stop and restart recovery behave exactly as for network imports.
 public partial class MainUi {
  internal static readonly string[] WatchIntervals={"2 seconds","5 seconds","10 seconds","30 seconds","1 minute"};
  // Labels round-trip any whole number of seconds, so a custom interval from Advanced telescope access keeps its value.
  internal static int WatchSeconds(string label){if(label=="1 minute")return 60;int seconds;var parts=(label??"").Split(' ');return parts.Length==2&&parts[1]=="seconds"&&int.TryParse(parts[0],out seconds)&&seconds>0?seconds:5;}
  internal static string WatchIntervalLabel(int seconds){return seconds==60?"1 minute":seconds>0?seconds+" seconds":"5 seconds";}
  internal static string WatchFolderIssue(string folder,string repositoryRoot){
   if(string.IsNullOrWhiteSpace(folder))return "Choose the folder your capture software saves to.";
   string full;try{if(!Path.IsPathRooted(folder))return "Choose a complete folder path, such as D:\\Astrophotography\\Captures.";full=Path.GetFullPath(folder);}catch(Exception e){if(e is ArgumentException||e is NotSupportedException||e is PathTooLongException||e is System.Security.SecurityException)return "This folder path is not valid.";throw;}
   if(!Directory.Exists(full))return "This folder does not exist or is not available.";
   if(full.TrimEnd('\\','/')==(Path.GetPathRoot(full)??"").TrimEnd('\\','/'))return "Choose a dedicated capture folder rather than a whole drive.";
   if(!string.IsNullOrEmpty(repositoryRoot)&&(Util.Within(full,repositoryRoot)||Util.Within(repositoryRoot,full)))return "Choose a capture folder outside the repository. AstroArchive cannot watch its own archive or a folder that contains it.";
   return null;
  }
  void OpenLocalWatch(){
   if(NetworkImportBlocked||repo==null)return;
   string cache=Path.Combine(Path.GetDirectoryName(config),"RemoteDownloads"),current=T("SourceBox").Text.Trim();if(current.Equals(cache,StringComparison.OrdinalIgnoreCase))current="";
   var d=new FormWindow(Window,"Watch a capture folder",680,640);
   d.Text("Import new captures as they are saved",true);
   d.Text("AstroArchive checks this folder and its subfolders, and imports each capture once it has stopped changing. Keep N.I.N.A., SharpCap, ASIAIR, Ekos or your other capture software saving here. Originals stay where they are.");
   var caption=new TextBlock{Text="Capture folder",Margin=new Thickness(0,6,0,5)};caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");d.Add(caption);
   var row=new DockPanel{Margin=new Thickness(0,0,0,5)};var folderBox=new TextBox{Text=current};System.Windows.Automation.AutomationProperties.SetName(folderBox,"Capture folder to watch");
   var browse=new Button{Content="Browse…",Margin=new Thickness(8,0,0,0),Padding=new Thickness(12,5,12,5)};UiHelp.For(browse,"Browse…");DockPanel.SetDock(browse,Dock.Right);row.Children.Add(browse);row.Children.Add(folderBox);d.Add(row);
   browse.Click+=(s,e)=>{string chosen=Folder("Choose the folder your capture software saves to",folderBox.Text,d.Window);if(chosen!=null)folderBox.Text=chosen;};
   var ids=(settings.Telescopes??new List<TelescopeProfile>()).Select(p=>p.Id).Where(id=>!string.IsNullOrWhiteSpace(id)).OrderBy(id=>id,StringComparer.OrdinalIgnoreCase).ToList();
   var deviceCaption=new TextBlock{Text="Save imports as (physical telescope ID)",Margin=new Thickness(0,8,0,5)};deviceCaption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");d.Add(deviceCaption);
   var device=new ComboBox{IsEditable=true,ItemsSource=ids,Text=T("TelescopeBox").Text.Trim()};System.Windows.Automation.AutomationProperties.SetName(device,"Physical telescope ID for watched captures");UiHelp.Hint(device,"Use the same ID for every import from this telescope. A new ID is saved as a telescope.");d.Add(device);
   var interval=d.Select("Check for new captures every",WatchIntervals,"5 seconds");
   var existing=d.Check("Also import captures already in this folder",false);
   var note=new TextBlock{Text="Off: only captures saved after watching starts are imported. Scan folder imports earlier captures.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24,2,0,4)};note.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(note,TextBlock.ForegroundProperty,"Muted");d.Add(note);
   var skipFailed=d.Check("Skip “failed” files",settings.IgnoreFailed);var skipRaster=d.Check("Skip PNG/JPG/MP4",settings.IgnoreRasterImports);
   foreach(var box in new[]{skipFailed,skipRaster})UiHelp.Hint(box,"Saved import preference. Also applies to folder, USB, network and Dump imports.");
   var footer=new TextBlock{Text="Keep AstroArchive open and this PC awake while capturing. Stop watching from Activity at any time; completed imports are kept.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};footer.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(footer,TextBlock.ForegroundProperty,"Muted");d.Add(footer);
   RemoteImportRequest request=null;
   d.Accept("Start watching",()=>{
    string folder=folderBox.Text.Trim(),id=(device.Text??"").Trim(),issue=WatchFolderIssue(folder,repo.Root);
    if(issue==null&&id.Length==0)issue="Give this physical telescope a unique ID.";
    var c=new RemoteConnection{Kind="Local folder",Host="",Folder=issue==null?Path.GetFullPath(folder).TrimEnd('\\','/'):folder,PollSeconds=WatchSeconds(Convert.ToString(interval.SelectedItem)),LimitMB=0,IncludeExisting=existing.IsChecked==true};
    TelescopeProfile profile=null;
    if(issue==null){
     var saved=TelescopeProfiles.Find(settings,id);
     profile=saved!=null?Util.Deserialize<TelescopeProfile>(Util.Serialize(saved)):new TelescopeProfile{Id=id,Model=Convert.ToString(C("ModelBox").SelectedItem)??"Auto"};
     string camera=Convert.ToString(C("CameraBox").SelectedItem);if(saved==null&&(camera=="Wide"||camera=="Telephoto"))profile.Camera=camera;
     profile.LastSource=c.Folder;
     try{c.Validate(false);}catch(ArgumentException e){issue=e.Message;}
     if(issue==null)issue=ValidateRemoteRequest(new RemoteImportRequest{Connection=c,Profile=profile,Live=true});
    }
    if(issue!=null){LocalizedMessage.Show(d.Window,issue,"Watch a capture folder",MessageBoxButton.OK,MessageBoxImage.Information);return false;}
    request=new RemoteImportRequest{Connection=c,Profile=profile,Live=true,Files=new List<Entry>()};return true;
   });
   if(!d.Show()||request==null||repo==null||closing)return;
   if(settings.IgnoreFailed!=(skipFailed.IsChecked==true)||settings.IgnoreRasterImports!=(skipRaster.IsChecked==true))SaveRemoteImportFilters(new RemoteImportFilters{IgnoreFailed=skipFailed.IsChecked==true,IgnoreRaster=skipRaster.IsChecked==true});
   T("TelescopeBox").Text=request.Profile.Id;
   StartLocalWatch(request);
  }
  async void StartLocalWatch(RemoteImportRequest request){
   try{await StartRemoteImport(request);}catch(Exception error){LocalizedMessage.Show(Window,error.Message,"Folder watch could not start",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
 }
}
