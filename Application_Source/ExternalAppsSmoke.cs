using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeExternalExports(string output){
   string theme=settings.ThemeMode;int scale=settings.TextScalePercent;var defaults=settings.ExportDefaults;var paths=settings.ExternalApplicationPaths;
   try{
    settings.ExportDefaults=new Dictionary<string,string>{{"TIFF","gimp"}};
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();ExportToFields fields;var dialog=ExportToDialog(new ExternalSelection{Paths=new List<string>{"Finished Ω.tiff"}},out fields);
     try{dialog.Window.Show();PumpPopupLayout();VerifyWindowIcon(dialog.Window);if(SelectedApplication(fields.Application).Id!="gimp"||fields.Remember.IsChecked==true)throw new Exception("Export default or opt-in changed");if(fields.Application.Items.OfType<ComboBoxItem>().Count(i=>i.Tag is ExportApplication)!=8)throw new Exception("Destinations missing");if(fields.Application.Items.OfType<ComboBoxItem>().Single(i=>i.Tag is ExportApplication&&((ExportApplication)i.Tag).Id=="dss").IsEnabled)throw new Exception("DSS accepts a finished image");foreach(var button in PopupChildren<Button>(dialog.Window).Where(b=>Convert.ToString(b.Content)=="Export"||Convert.ToString(b.Content)=="Cancel"))if(!button.IsVisible||button.ActualWidth+button.Margin.Left+button.Margin.Right<button.DesiredSize.Width-0.5)throw new Exception("Export actions clipped");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_ExportTo_"+mode+textScale+".png"));}finally{dialog.Window.Close();}
     var folder=ExportToDialog(new ExternalSelection{Folder=true,Subframes=true,Paths=new List<string>{"sub01.fit","sub02.fit"}},out fields);try{folder.Window.Show();PumpPopupLayout();if(fields.Destination==null||fields.Calibration.IsChecked==true||fields.Unknown.IsEnabled)throw new Exception("Folder options unsafe");fields.Calibration.IsChecked=true;if(!fields.Unknown.IsEnabled)throw new Exception("Unknown calibration option does not follow checkbox");fields.Calibration.IsChecked=false;if(fields.Unknown.IsEnabled)throw new Exception("Unknown calibration remains enabled");CapturePopup(folder.Window,Path.Combine(output,"AstroArchive_ExportTo_Folder_"+mode+textScale+".png"));}finally{folder.Window.Close();}
     Dictionary<string,string> localPaths,localDefaults;var preferences=ExportSettingsDialog(1,"astrowizard",out localPaths,out localDefaults);try{preferences.Window.Show();PumpPopupLayout();var appCombo=PopupChildren<ComboBox>(preferences.Window).Single();if(Convert.ToString(appCombo.SelectedItem)!="AstroWizard")throw new Exception("Application settings focused wrong destination");localPaths["astrowizard"]="cancelled.exe";localDefaults["TIFF"]="photoshop";if(settings.ExportDefaults["TIFF"]!="gimp"||settings.ExternalApplicationPaths!=paths)throw new Exception("Cancelled settings mutated live preferences");CapturePopup(preferences.Window,Path.Combine(output,"AstroArchive_Export_Settings_"+mode+textScale+".png"));preferences.SelectTab(0);PumpPopupLayout();var scroll=PopupChildren<ScrollViewer>(preferences.Window).First();scroll.ScrollToEnd();PumpPopupLayout();if(!PopupChildren<ComboBox>(preferences.Window).Any(c=>c.Items.Contains("Stacking Wizard")))throw new Exception("Folder default setting unreachable");}finally{preferences.Window.Close();}
    }
    File.WriteAllText(Path.Combine(output,"external-export-smoke.txt"),"PASS: eight destinations, compatible choices, type defaults, safe folder options, cancellation isolation and visible actions in both themes at 100/150% text.");
   }finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;settings.ExportDefaults=defaults;settings.ExternalApplicationPaths=paths;ApplyAppearance();}
  }
 }
}
