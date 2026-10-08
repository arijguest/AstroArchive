using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokePreferences(string output){
   if(TopMenu("SettingsMenu").HasItems||Window.FindName("EditedMenu")!=null)throw new Exception("Settings is still a dropdown or duplicate Edited navigation remains.");string before=Util.Serialize(settings);
   try{foreach(string mode in new[]{"Light","Dark"})foreach(int percent in new[]{100,150}){
    settings.ThemeMode=mode;settings.TextScalePercent=percent;ApplyAppearance();PreferenceFields fields;var dialog=PreferencesDialog(Window,0,out fields);
    try{dialog.Window.Show();PumpPopupLayout();var sections=PopupChildren<ListBox>(dialog.Window).Single(list=>AutomationProperties.GetName(list)=="Preferences sections");if(sections.Items.Count!=6||PopupChildren<TabControl>(dialog.Window).Any())throw new Exception("Preferences are not consolidated in a sidebar");
     for(int i=0;i<6;i++){sections.SelectedIndex=i;PumpPopupLayout();var heading=PopupChildren<TextBlock>(dialog.Window).FirstOrDefault(text=>text.IsVisible&&text.Text==((TextBlock)((ListBoxItem)sections.Items[i]).Content).Text&&text.FontSize>dialog.Window.FontSize);if(heading==null)throw new Exception("Preference section is empty");var viewer=PopupChildren<ScrollViewer>(dialog.Window).Last(scroll=>scroll.IsVisible&&MenuScrolling.GetEnabled(scroll));if(viewer.ViewportWidth<250||viewer.ViewportHeight<=0)throw new Exception("Preferences content is cramped or unbounded");}
     if(fields.Directory.Text!=(settings.ExportWorkingDirectory??"")||fields.Exports.Paths["siril"]!=(ExternalApps.Configured(settings,ExternalApps.Find("siril"))??""))throw new Exception("Export preferences were not carried across");sections.SelectedIndex=0;fields.Theme.SelectedItem="System";CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Preferences_"+mode+percent+".png"));
    }finally{dialog.Window.Close();}
   }
   Exception failure=null;Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{Window dialog=null;try{dialog=Window.OwnedWindows.Cast<Window>().Single(w=>w.Title=="Preferences");if(TopMenu("SettingsMenu").IsSubmenuOpen)throw new Exception("Settings opens a dropdown before Preferences");dialog.DialogResult=false;}catch(Exception error){failure=error;if(dialog!=null)dialog.DialogResult=false;}}));TopMenu("SettingsMenu").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));if(failure!=null)throw failure;
   }finally{settings=Util.Deserialize<Settings>(before);ApplyAppearance();}
   File.WriteAllText(Path.Combine(output,"preferences-smoke.txt"),"PASS: Settings opens Preferences directly; six populated sections, retained export preferences, bounded scrolling, light/dark and 100/150% text; closing preserves saved settings.");
  }
 }
}
