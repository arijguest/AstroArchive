using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeStackingApps(string output){
   string theme=settings.ThemeMode;int scale=settings.TextScalePercent;
   try{
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();var dialog=ExportCompleteDialog(Path.Combine(Path.GetTempPath(),"Stacking export Ω with a long folder name","Verified stacking inputs"),true);
     try{dialog.Window.Show();PumpPopupLayout();VerifyWindowIcon(dialog.Window);if(dialog.Window.Owner!=Window)throw new Exception("Export popup lost its owner");if(PopupChildren<WrapPanel>(dialog.Window).Any())throw new Exception("Completion popup duplicates destination choices");foreach(var button in PopupChildren<Button>(dialog.Window).Where(b=>Convert.ToString(b.Content)=="Close"||Convert.ToString(b.Content)=="Open exported folder")){button.BringIntoView();PumpPopupLayout();if(!button.IsVisible||button.ActualWidth+button.Margin.Left+button.Margin.Right<button.DesiredSize.Width-0.5)throw new Exception("Completion action clipped");}CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Stacking_Apps_"+mode+textScale+".png"));}finally{dialog.Window.Close();}
    }
    File.WriteAllText(Path.Combine(output,"stacking-apps-smoke.txt"),"PASS compact stacking completion, owner, Unicode path and accessible actions in light/dark at 100/150% text.");
   }finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();}
  }
 }
}
