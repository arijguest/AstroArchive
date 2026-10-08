using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeArchiveSafety(string output){
   string theme=settings.ThemeMode,folder=settings.BackupDestination;int scale=settings.TextScalePercent;bool compressed=settings.CompressBackup;
   var previous=repo;var fixture=new Repository(Path.Combine(output,"safety-dialog-repository"));repo=fixture;
   try{settings.BackupDestination="";settings.CompressBackup=false;foreach(string mode in new[]{"Light","Dark"})foreach(int textScale in new[]{100,150}){
    settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();TextBox destination;CheckBox zip;var backup=BackupDialog(out destination,out zip);
    try{backup.Window.Show();PumpPopupLayout();VerifyWindowIcon(backup.Window);if(zip.IsChecked==true||!destination.IsReadOnly)throw new Exception("Backup defaults or destination picker changed.");if(!PopupChildren<TextBlock>(backup.Window).Any(t=>t.Text.Contains("can take a long time"))||!PopupChildren<TextBlock>(backup.Window).Any(t=>t.Text.Contains("resolution")))throw new Exception("Backup time or lossless explanation missing.");foreach(var button in PopupChildren<Button>(backup.Window).Where(b=>Convert.ToString(b.Content)=="Create backup"||Convert.ToString(b.Content)=="Cancel"))if(!button.IsVisible||button.ActualWidth+button.Margin.Left+button.Margin.Right<button.DesiredSize.Width-0.5)throw new Exception("Backup actions clipped.");CapturePopup(backup.Window,Path.Combine(output,"AstroArchive_Backup_"+mode+textScale+".png"));}finally{backup.Window.Close();}
    var safety=new FormWindow(Window,"Archive safety",700,680);AddArchiveSafetySettings(safety);safety.CloseOnly();try{safety.Window.Show();PumpPopupLayout();if(!PopupChildren<Button>(safety.Window).Any(b=>Convert.ToString(b.Content)=="Back up…")||!PopupChildren<Button>(safety.Window).Any(b=>Convert.ToString(b.Content)=="Protect originals…"))throw new Exception("Safety preferences are missing.");CapturePopup(safety.Window,Path.Combine(output,"AstroArchive_Safety_"+mode+textScale+".png"));}finally{safety.Window.Close();}
   }File.WriteAllText(Path.Combine(output,"archive-safety-smoke.txt"),"PASS: optional protection, backup folder default, lossless compression explanation, time warning and visible actions in light/dark themes at 100/150% text.");
   }finally{repo=previous;fixture.Dispose();settings.ThemeMode=theme;settings.TextScalePercent=scale;settings.BackupDestination=folder;settings.CompressBackup=compressed;ApplyAppearance();}
  }
 }
}
