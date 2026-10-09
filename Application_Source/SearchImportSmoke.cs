using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeSearchImport(string output){
   var previousPlan=plan;string search=T("SearchBox").Text,importSearch=T("ImportSearchBox").Text,target=unknownImportTarget,theme=settings.ThemeMode;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex,scale=settings.TextScalePercent;
   try{
    if(((TabControl)Window.FindName("MainTabs")).Items.Count!=3||Window.FindName("MosaicGrid")!=null||CaptureFilters.Fields.Any(f=>f.Contains("Mosaic")||f=="Panel"))throw new Exception("Mosaic functionality remains in navigation or filters.");
    GoToPage(0);T("SearchBox").Text="target:M45 -type:Stack";WaitForSearches();if(displayed.Count!=1)throw new Exception("Advanced Repository search failed.");
    T("SearchBox").Text="target:C27 OR target:M45 type:Stack";WaitForSearches();if(displayed.Count!=2)throw new Exception("Repository alternatives failed.");
    T("SearchBox").Text="\"unfinished";WaitForSearches();if(displayed.Count!=0||!Convert.ToString(T("SearchBox").ToolTip).Contains("Close"))throw new Exception("Incomplete search did not show guidance and zero rows.");
    T("SearchBox").Text=search;WaitForSearches();GoToPage(1);
    plan=new ImportPlan{Frames=new System.Collections.Generic.List<Frame>{new Frame{Target="Unknown",Kind="Light",Status="New",OriginalName="light_001.fit",SourcePath="one"},new Frame{Target="M45",Kind="Stack",Status="New",SourcePath="two"},new Frame{Target="Unknown",Kind="Dark",Status="New",SourcePath="three"}}};T("ImportSearchBox").Text="";WaitForSearches();FilterImports();PumpPopupLayout();
    if(!B("ImportOptionsButton").IsVisible||!B("AssignUnknownTargetButton").IsEnabled)throw new Exception("Visible import policy and Unknown-target actions are missing.");
    Exception optionsError=null;
    Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{
     Window dialog=null;
     try{
     dialog=Window.OwnedWindows.Cast<Window>().Single(w=>w.Title=="Preferences");dialog.UpdateLayout();
     var sections=PopupChildren<ListBox>(dialog).Single(list=>System.Windows.Automation.AutomationProperties.GetName(list)=="Preferences sections");if(sections.Items.Count!=7||sections.SelectedIndex!=1)throw new Exception("Import options did not open the Import preferences section.");
     var raster=PopupChildren<CheckBox>(dialog).Single(c=>c.Content is TextBlock&&((TextBlock)c.Content).Text.Contains("PNG/JPG/JPEG/MP4"));if(raster.IsChecked!=settings.IgnoreRasterImports)throw new Exception("Import format choice is not synchronized.");
     PumpPopupLayout();
     var choice=PopupChildren<ComboBox>(dialog).Single(c=>c.IsEditable);choice.Text="C27";
     PopupChildren<Button>(dialog).Single(b=>Convert.ToString(b.Content)=="Save preferences").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
     }catch(Exception e){optionsError=e;if(dialog!=null)dialog.DialogResult=false;}
    }));ImportPreferences();if(optionsError!=null)throw new Exception("Import preferences UI validation failed.",optionsError);
    if(plan==null||plan.Frames[0].Target!="NGC6888"||plan.Frames[1].Target!="M45"||plan.Frames[2].Target!="Unknown"||B("AssignUnknownTargetButton").IsEnabled)throw new Exception("Import preferences overwrote known/calibration targets or failed to assign Unknown lights.");
    T("ImportSearchBox").Text="target:NGC 6888 -type:Dark";WaitForSearches();if(visibleImports.Count!=1)throw new Exception("Advanced import search does not share Repository rules.");
    var candidates=plan;plan=new ImportPlan{FastSkippedFiles=12,FastSkippedFolders=1};FilterImports();PumpPopupLayout();if(!L("ScanLabel").IsVisible||!L("ScanLabel").Text.StartsWith("Nothing new to import")||!L("ScanLabel").Text.Contains("12 archived files"))throw new Exception("Completed session scan lacks a visible no-new-files summary.");plan=candidates;FilterImports();
    foreach(string mode in new[]{"Light","Dark"})foreach(int percent in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=percent;ApplyAppearance();PumpPopupLayout();
     foreach(string name in new[]{"ImportOptionsButton","AssignUnknownTargetButton"}){var button=B(name);var bounds=PopupBounds(button,Window);if(bounds.Left<0||bounds.Right>Window.ActualWidth+1)throw new Exception("Import controls clipped with larger text.");Readable(button.Foreground,button.Background,mode+" "+name);}
     if(percent==100)Capture(Path.Combine(output,"AstroArchive_Import_Options_"+mode+".png"));
    }
    if(repo!=null){var tools=BuildRepositoryTools();if(!tools.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header)=="Purge non-raw files…"))throw new Exception("Repository non-raw purge is missing.");}
   }finally{plan=previousPlan;unknownImportTarget=target;settings.ThemeMode=theme;settings.TextScalePercent=scale;T("SearchBox").Text=search;WaitForSearches();T("ImportSearchBox").Text=importSearch;WaitForSearches();ApplyAppearance();FilterImports();GoToPage(page);}
   File.WriteAllText(Path.Combine(output,"search-import-smoke.txt"),"PASS: shared query semantics, syntax guidance, three pages without Mosaic, visible import options, grouped parameter tabs, Unknown-only target assignment, non-raw policy synchronization, light/dark controls at 100/150% and repository purge entry.");
  }
 }
}
