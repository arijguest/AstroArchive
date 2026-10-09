// Native UI checks use injected release operations; they never download or install.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using AstroArchive.Installation;
namespace AstroArchive {
 public partial class MainUi {
  static string TipText(FrameworkElement control){var tip=control.ToolTip as ToolTip;var text=tip==null?null:tip.Content as TextBlock;return text==null?Convert.ToString(control.ToolTip):text.Text;}
  void FinishReleaseTask(Task task){SmokeSearchWait(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
  void SmokeUxCleanup(string output){
   var saved=Util.Serialize(settings);bool scanEnabled=B("ScanButton").IsEnabled;B("ScanButton").IsEnabled=true;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   try{
    foreach(string name in new[]{"SettingsButton","LibraryFiltersButton","ImportFiltersButton","FramesGrid","ImportGrid","SourceBox"})if(((FrameworkElement)Window.FindName(name)).ToolTip!=null)throw new Exception("Redundant tooltip remains: "+name);
    settings.RobustImportMatching=false;UpdateNavigationState();if(!TipText(B("ScanButton")).Contains("Quick scan"))throw new Exception("Quick scan help is missing.");
    settings.RobustImportMatching=true;UpdateNavigationState();if(!TipText(B("ScanButton")).Contains("Robust scan")||!AutomationProperties.GetHelpText(B("ScanButton")).Contains("Robust scan"))throw new Exception("Robust scan help is stale.");
    ShowSearchError("SearchBox",FileSearch.Parse("\"unfinished"));if(!TipText(B("SearchHelpButton")).Contains("quote"))throw new Exception("Search help does not expose the error.");
    ShowSearchError("SearchBox",FileSearch.Parse(""));if(T("SearchBox").ToolTip!=null||string.IsNullOrEmpty(AutomationProperties.GetHelpText(T("SearchBox"))))throw new Exception("Search lost accessible help or retains ordinary hover text.");
    var editor=new MetadataEditor(Window,new List<Frame>{new Frame{OriginalName="scope.fit",Target="M33",Exposure=30}},27);
    try{editor.Form.Window.Show();editor.Sessions.IsChecked=true;((TextBox)editor.Inputs["Filter"]).Text="Ha";PumpPopupLayout();
     if(!PopupChildren<TextBlock>(editor.Form.Window).Any(t=>t.Text.Contains("27 files in the selected sessions")&&t.Text.Contains("Filter")))throw new Exception("Metadata session scope is not quantified.");
     if(editor.Inputs["Exposure"].ToolTip!=null||!TipText(editor.Inputs["Roi"]).Contains("Sensor crop"))throw new Exception("Metadata repeats generic tips or lacks specialist help.");
    }finally{editor.Form.Window.Close();}
    foreach(string theme in new[]{"Light","Dark"})foreach(int scale in new[]{100,150}){
     settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();var dialog=CreateSettingsDialog();
     try{dialog.Window.Show();PumpPopupLayout();if(dialog.Window.Title!="Settings"||PopupChildren<Button>(dialog.Window).Count(b=>Convert.ToString(b.Content)=="Check for releases")!=1||PopupChildren<Button>(dialog.Window).Any(b=>Convert.ToString(b.Content)=="Check for and install new releases"))throw new Exception("Settings retains a nested release dialog.");
      var install=PopupChildren<Button>(dialog.Window).Single(b=>Convert.ToString(b.Content)=="Install and restart");if(install.IsEnabled)throw new Exception("Unchecked release can be installed.");
      foreach(var label in PopupChildren<TextBlock>(dialog.Window).Where(t=>t.IsVisible&&!string.IsNullOrWhiteSpace(t.Text))){
       System.Windows.Media.Brush background=dialog.Window.Background;
       for(DependencyObject parent=System.Windows.Media.VisualTreeHelper.GetParent(label);parent!=null;parent=System.Windows.Media.VisualTreeHelper.GetParent(parent)){
        var border=parent as Border;var surface=border==null?null:border.Background as System.Windows.Media.SolidColorBrush;if(surface!=null&&surface.Color.A==255){background=surface;break;}
       }
       Readable(label.Foreground,background,"Release settings "+theme+" "+label.Text);
      }
      var scroll=PopupChildren<ScrollViewer>(dialog.Window).First();scroll.ScrollToEnd();PumpPopupLayout();
      if(install.ActualWidth<120||install.ActualHeight<30)throw new Exception("Release installation action is clipped.");
      CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Release_Settings_"+theme+scale+".png"));
     }finally{dialog.Window.Close();}
    }
    SmokeReleaseStates(output);
    File.WriteAllText(Path.Combine(output,"ux-cleanup-smoke.txt"),"PASS: quiet controls, scan-mode help, accessible search errors, quantified metadata scope, embedded release settings in both themes at 100/150% text, no-update/offline/notes-failure states, validation before installation, protected settings during installation, retry after failure, exactly-once completion and closed-page async safety. No network downloads or real installation.");
   }finally{settings=Util.Deserialize<Settings>(saved);B("ScanButton").IsEnabled=scanEnabled;ApplyAppearance();GoToPage(page);UpdateNavigationState();}
  }
  void SmokeReleaseStates(string output){
   var release=new UpdateManifest{application_version="1.2.0",package_version="1.2.0.2"};
   var dialog=new FormWindow(Window,"Settings release state fixture",700,680);dialog.Tabs("Preferences","Other settings");var preference=dialog.Select("Fixture preference",new[]{"A","B"},"A");
   bool valid=true,failCheck=false,failNotes=false;int downloads=0,restarts=0,saves=0;
   TaskCompletionSource<bool> pending=null;UpdateManifest result=null;
   var panel=new ReleaseSettingsPanel(dialog,"Current version: fixture",null,()=>{saves++;return valid;},
    ()=>{if(failCheck)throw new IOException("fixture offline");return Task.FromResult(result);},
    r=>{if(failNotes)throw new IOException("fixture notes unavailable");return Task.FromResult("Fixture release notes.");},
    async(r,p)=>{downloads++;p.Report(new UpdateDownloadProgress{Received=50,Total=100});if(pending!=null)await pending.Task;},
    ()=>restarts++,r=>{});
   dialog.CloseOnly();dialog.Window.Show();PumpPopupLayout();
   try{
    FinishReleaseTask(panel.CheckAsync());if(panel.Install.IsEnabled||!panel.Status.Text.Contains("No newer"))throw new Exception("No-update state is incorrect.");
    failCheck=true;FinishReleaseTask(panel.CheckAsync());if(panel.Install.IsEnabled||!panel.Check.IsEnabled||!panel.Status.Text.Contains("fixture offline"))throw new Exception("Failed checks are not retryable.");
    failCheck=false;result=release;failNotes=true;FinishReleaseTask(panel.CheckAsync());if(!panel.Install.IsEnabled||panel.NotesSection.Visibility!=Visibility.Visible||!panel.Notes.Text.Contains("could not be loaded"))throw new Exception("Notes failure prevents installation or lacks fallback.");
    failNotes=false;FinishReleaseTask(panel.CheckAsync());panel.NotesSection.IsExpanded=true;PumpPopupLayout();CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Release_Available.png"));
    valid=false;FinishReleaseTask(panel.InstallAsync());if(downloads!=0||saves!=1||restarts!=0)throw new Exception("Installation ignores settings validation.");
    valid=true;pending=new TaskCompletionSource<bool>();var operation=panel.InstallAsync();PumpPopupLayout();
    if(!panel.Installing||preference.IsEnabled||panel.Check.IsEnabled||panel.Install.IsEnabled)throw new Exception("Installation does not protect pending settings.");
    dialog.Window.Close();if(!dialog.Window.IsVisible)throw new Exception("Settings closed during installation.");
    pending.SetException(new IOException("fixture download failed"));FinishReleaseTask(operation);PumpPopupLayout();
    if(panel.Busy||!preference.IsEnabled||!panel.Install.IsEnabled||!panel.Status.Text.Contains("fixture download failed")||restarts!=0)throw new Exception("Failed installation cannot retry in Settings.");
    CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Release_Failure.png"));
    pending=null;FinishReleaseTask(panel.InstallAsync());if(downloads!=2||restarts!=1||saves!=3)throw new Exception("Retry did not save and complete exactly once.");
   }finally{dialog.Window.Close();}
   var closing=new FormWindow(Window,"Release check closure fixture",640,550);var completion=new TaskCompletionSource<UpdateManifest>();
   var late=new ReleaseSettingsPanel(closing,"Fixture",null,()=>true,()=>completion.Task,r=>Task.FromResult("Notes"),(r,p)=>Task.FromResult(0),()=>{},r=>{});
   closing.CloseOnly();closing.Window.Show();var task=late.CheckAsync();closing.Window.Close();string before=late.Status.Text;completion.SetResult(release);FinishReleaseTask(task);
   if(late.Status.Text!=before||late.Available!=null)throw new Exception("Late release check changed a closed Settings page.");
  }
 }
}
