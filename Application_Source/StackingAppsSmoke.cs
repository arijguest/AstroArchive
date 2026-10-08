using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeStackingApps(string output){
   string theme=settings.ThemeMode;int scale=settings.TextScalePercent;
   try{foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
    settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();var dialog=ExportCompleteDialog(Path.Combine(Path.GetTempPath(),"Export Ω with a long folder name","M51 Whirlpool Galaxy","Verified inputs"),true);
    try{dialog.Window.Show();PumpPopupLayout();VerifyWindowIcon(dialog.Window);if(dialog.Window.Owner!=Window)throw new Exception("Export popup lost its owner");var buttons=PopupChildren<Button>(dialog.Window).ToList();if(buttons.Count!=2||!buttons.Any(b=>Convert.ToString(b.Content)=="Open folder")||!buttons.Any(b=>Convert.ToString(b.Content)=="Close"))throw new Exception("Export completion retains redundant app choices");foreach(var button in buttons){button.BringIntoView();PumpPopupLayout();var bounds=PopupBounds(button,dialog.Window);if(!button.IsVisible||bounds.Right>dialog.Window.ActualWidth||bounds.Bottom>dialog.Window.ActualHeight)throw new Exception("Export completion actions are clipped");}CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Export_Complete_"+mode+textScale+".png"));}
    finally{dialog.Window.Close();}
   }File.WriteAllText(Path.Combine(output,"stacking-apps-smoke.txt"),"PASS: concise verified-export completion, folder/close actions, owner and long Unicode path in light/dark at 100/150% text.");}
   finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();}
  }
 }
}
