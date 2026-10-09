using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeBackupAction(Action open,string screenshot){
   string before=Util.Serialize(settings);Exception failure=null;bool checkedDialog=false;
   Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
    Window dialog=null;try{dialog=Window.OwnedWindows.Cast<Window>().Single(w=>w.Title=="Back up archive");PumpPopupLayout();VerifyWindowIcon(dialog);
     if(!PopupChildren<TextBox>(dialog).Single().IsReadOnly||PopupChildren<CheckBox>(dialog).Single().IsChecked==true||!PopupChildren<Button>(dialog).Any(b=>b.IsVisible&&Convert.ToString(b.Content)=="Choose backup folder…"))throw new Exception("Backup menu did not open destination and ZIP options");
     CapturePopup(dialog,screenshot);checkedDialog=true;
    }catch(Exception error){failure=error;}finally{if(dialog!=null)dialog.DialogResult=false;}
   }));open();PumpPopupLayout();if(failure!=null)throw failure;if(!checkedDialog||before!=Util.Serialize(settings))throw new Exception("Canceling the backup menu changed settings or never opened the dialog");
  }
  void SmokeArchiveSafety(string output){
   string theme=settings.ThemeMode,folder=settings.BackupDestination;int scale=settings.TextScalePercent;bool compressed=settings.CompressBackup;
   var previous=repo;var fixture=new Repository(Path.Combine(output,"safety-dialog-repository"));repo=fixture;
   try{settings.BackupDestination="";settings.CompressBackup=false;foreach(string mode in new[]{"Light","Dark"})foreach(int textScale in new[]{100,150}){
    settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();TextBox destination;CheckBox zip;var backup=BackupDialog(out destination,out zip);
    try{backup.Window.Show();PumpPopupLayout();VerifyWindowIcon(backup.Window);if(zip.IsChecked==true||!destination.IsReadOnly)throw new Exception("Backup defaults or destination picker changed.");if(!PopupChildren<TextBlock>(backup.Window).Any(t=>t.Text.Contains("can take a long time"))||!PopupChildren<TextBlock>(backup.Window).Any(t=>t.Text.Contains("resolution")))throw new Exception("Backup time or lossless explanation missing.");foreach(var button in PopupChildren<Button>(backup.Window).Where(b=>Convert.ToString(b.Content)=="Create backup"||Convert.ToString(b.Content)=="Cancel"))if(!button.IsVisible||button.ActualWidth+button.Margin.Left+button.Margin.Right<button.DesiredSize.Width-0.5)throw new Exception("Backup actions clipped.");CapturePopup(backup.Window,Path.Combine(output,"AstroArchive_Backup_"+mode+textScale+".png"));}finally{backup.Window.Close();}
    PreferenceFields fields;var safety=PreferencesDialog(Window,6,out fields);try{safety.Window.Show();PumpPopupLayout();var sections=PopupChildren<ListBox>(safety.Window).Single(list=>AutomationProperties.GetName(list)=="Preferences sections");if(((TextBlock)((ListBoxItem)sections.SelectedItem).Content).Text!="Backups")throw new Exception("Backups is missing from the actual Preferences sidebar");
     var create=PopupChildren<Button>(safety.Window).Single(b=>Convert.ToString(b.Content)=="Back up archive…");if(!create.IsVisible||!create.IsEnabled||!PopupChildren<Button>(safety.Window).Any(b=>Convert.ToString(b.Content)=="Protect originals…"))throw new Exception("Safety preferences are missing or collapsed");CapturePopup(safety.Window,Path.Combine(output,"AstroArchive_Safety_"+mode+textScale+".png"));
     SmokeBackupAction(()=>create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),Path.Combine(output,"AstroArchive_Backup_From_Preferences_"+mode+textScale+".png"));
    }finally{safety.Window.Close();}
    OpenTopMenu("RepositoryMenu");PumpPopupLayout();var root=TopMenu("RepositoryMenu");var action=root.Items.OfType<MenuItem>().Single(item=>Convert.ToString(item.Header)=="Back up archive…");if(!action.IsVisible||!action.IsEnabled)throw new Exception("Repository menu does not expose a usable backup action");root.IsSubmenuOpen=false;SmokeBackupAction(()=>action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)),Path.Combine(output,"AstroArchive_Backup_From_Menu_"+mode+textScale+".png"));
   }
   repo=null;PopulateNavigation("RepositoryMenu");if(TopMenu("RepositoryMenu").Items.OfType<MenuItem>().Single(item=>Convert.ToString(item.Header)=="Back up archive…").IsEnabled)throw new Exception("Backup is enabled without a repository");repo=fixture;
   var idle=cancel;using(var busy=new CancellationTokenSource()){try{cancel=busy;PopulateNavigation("RepositoryMenu");if(TopMenu("RepositoryMenu").Items.OfType<MenuItem>().Single(item=>Convert.ToString(item.Header)=="Back up archive…").IsEnabled)throw new Exception("Backup is enabled while an operation is busy");}finally{cancel=idle;}}
   if(Directory.EnumerateFileSystemEntries(fixture.Root).Any(path=>Path.GetFileName(path).StartsWith("AstroArchive-backup-")))throw new Exception("Canceling backup created archive files");
   File.WriteAllText(Path.Combine(output,"archive-safety-smoke.txt"),"PASS: real Repository menu and visible Preferences Backups section open folder/ZIP options; cancellation keeps settings; unavailable while busy or without a repository; optional protection, time warning and actions in light/dark themes at 100/150% text.");
   }finally{TopMenu("RepositoryMenu").IsSubmenuOpen=false;repo=previous;fixture.Dispose();settings.ThemeMode=theme;settings.TextScalePercent=scale;settings.BackupDestination=folder;settings.CompressBackup=compressed;ApplyAppearance();PopulateNavigation("RepositoryMenu");}
  }
 }
}
