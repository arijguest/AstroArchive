using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  internal static readonly string[] BackupFrequencies=BackupSchedule.Frequencies;
  internal static readonly string[] BackupKeepChoices={"All backups","Last 5","Last 10","Last 30"};
  internal static int BackupKeepCount(string label){return label=="Last 5"?5:label=="Last 10"?10:label=="Last 30"?30:0;}
  internal static string BackupKeepLabel(int keep){return keep==5?"Last 5":keep==10?"Last 10":keep==30?"Last 30":"All backups";}
  sealed class BackupPreferenceFields {
   public ComboBox Frequency,Keep;public CheckBox Incremental,Compress;public TextBox Folder;
   public void Save(Settings draft){draft.BackupFrequency=Convert.ToString(Frequency.SelectedItem)??"Off";draft.BackupKeep=BackupKeepCount(Convert.ToString(Keep.SelectedItem));draft.BackupIncremental=Incremental.IsChecked==true;draft.CompressBackup=Compress.IsChecked==true;draft.BackupDestination=Folder.Text.Trim().Length==0?null:Folder.Text.Trim();}
  }
  static TextBlock BackupNote(string text){var note=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,6)};note.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(note,TextBlock.ForegroundProperty,"Muted");return note;}
  string BackupStatus(){
   DateTime last;bool known=DateTime.TryParse(settings.LastBackupUtc,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out last);
   string text=known?"Last backup: "+last.ToLocalTime().ToString("d MMM yyyy HH:mm",CultureInfo.CurrentCulture):"No backup recorded on this PC yet.";
   if(settings.BackupFrequency!="Off"&&BackupFrequencies.Contains(settings.BackupFrequency))text+=" · Next: "+(known&&!BackupDue(settings,DateTime.UtcNow)?(last+BackupInterval(settings.BackupFrequency)).ToLocalTime().ToString("d MMM yyyy HH:mm",CultureInfo.CurrentCulture):"when AstroArchive is next idle");
   return text;
  }
  BackupPreferenceFields AddArchiveSafetySettings(FormWindow dialog){
   dialog.Text("Backups",true);
   dialog.Text("Create a verified copy of the whole archive, including originals, Edited files and the current database. Choose a normal folder or lossless ZIP in the backup dialog.");
   var backup=dialog.Button("Back up archive…",()=>{dialog.Window.Close();BackUpArchive();});backup.IsEnabled=repo!=null;
   if(repo==null)dialog.Text("Choose a repository in General to enable backups.");
   var fields=new BackupPreferenceFields();
   dialog.Text("Automatic backups",true);
   dialog.Text("While AstroArchive is open and idle, it repeats your backup on this schedule. If the backup drive is not connected, Activity reminds you instead.");
   fields.Frequency=dialog.Select("Back up automatically",BackupFrequencies,BackupFrequencies.Contains(settings.BackupFrequency)?settings.BackupFrequency:"Off");
   fields.Folder=dialog.Input("Backup folder",settings.BackupDestination??"");fields.Folder.IsReadOnly=true;
   dialog.Button("Choose backup folder…",()=>{string path=Folder("Choose a backup destination outside the archive",fields.Folder.Text,dialog.Window);if(path!=null)fields.Folder.Text=path;});
   fields.Compress=dialog.Check("Compress as ZIP (lossless)",settings.CompressBackup);
   fields.Incremental=dialog.Check("Copy only new and changed files (incremental)",settings.BackupIncremental);
   dialog.Add(BackupNote("The first backup in a folder is complete. Later folder backups share unchanged files with the previous backup, so each stays complete on its own; later ZIPs build on the earlier ZIPs in the same folder."));
   fields.Keep=dialog.Select("Keep",BackupKeepChoices,BackupKeepLabel(settings.BackupKeep));
   dialog.Add(BackupNote("Older backups of this archive are removed after each backup, except earlier ZIPs that newer ones still need. Other files in the folder are never touched."));
   dialog.Add(BackupNote(BackupStatus()));
   dialog.Text("Restore a backup",true);dialog.Text("Folder backups open directly: choose the backup’s Repository folder in Settings > General. Restore a backup… checks every file and rebuilds a folder or incremental ZIP backup into a new folder.");
   var restore=dialog.Button("Restore a backup…",()=>{dialog.Window.Close();RestoreBackup();});restore.IsEnabled=!NetworkImportBlocked;
   dialog.Text("Originals protection",true);
   dialog.Text(repo==null?"Open an archive to protect originals or create a backup.":"Originals: "+(repo.OriginalsProtected?"protected":"not protected"));
   dialog.Text("Optional Windows protection helps prevent deleting or renaming archived originals in File Explorer. Edited files, Dump and archive metadata remain writable. Protection is separate from a backup.");
   var protect=dialog.Button(repo!=null&&repo.OriginalsProtected?"Turn off protection…":"Protect originals…",()=>{dialog.Window.Close();ChangeArchiveProtection();});protect.IsEnabled=repo!=null&&(repo.OriginalsProtected||repo.ProtectionAvailability==null);
   if(repo!=null&&repo.ProtectionAvailability!=null)dialog.Text(repo.ProtectionAvailability);
   return fields;
  }
  // Called from Save preferences; an automatic schedule needs somewhere to write.
  void ValidateBackupPreferences(FormWindow d,BackupPreferenceFields fields){
   if(fields==null)return;string frequency=Convert.ToString(fields.Frequency.SelectedItem);string folder=fields.Folder.Text.Trim();
   if(frequency!="Off"&&folder.Length==0){d.SelectTab(6);throw new ArgumentException("Choose a backup folder for automatic backups.");}
   if(folder.Length>0&&repo!=null){d.SelectTab(6);Repository.ValidateBackupDestination(folder,repo.Root);}
  }
  void ChangeArchiveProtection(){
   if(repo==null||RepositoryOperationBlocked)return;bool enable=!repo.OriginalsProtected;
   var dialog=new FormWindow(Window,enable?"Protect archived originals":"Turn off archive protection",560,440);
   dialog.Text(enable?"Protect archived originals":"Remove deletion protection",true);
   dialog.Text(enable?"Windows will block deletion and renaming of archived originals in File Explorer for this account. AstroArchive can still apply metadata changes and confirmed deletions. Reading, previewing and copying images continue normally.":"Archived originals will use their previous deletion permissions. Existing images and backups are kept.");
   dialog.Text(enable?"This requires a local NTFS drive. Applying protection to a large archive can take a while; there is no background scan once it is applied. The Windows owner can deliberately change permissions, so keep a backup too.":"Removing protection may take a while. Once started, it finishes restoring permissions even if Cancel is pressed.");
   dialog.Accept(enable?"Protect originals":"Turn off protection",()=>true);if(!dialog.Show())return;
   cancellationMessage="Protection change canceled. Previous permissions restored.";OpenDumpProgress(enable?"Protecting originals":"Removing protection");Run(ct=>{repo.SetOriginalsProtection(enable,ct,Progress);return enable?"Archived originals are protected.":"Archive protection is off.";},message=>L("StatusLabel").Text=message);
  }
  static string BackupSummary(BackupResult result){
   string summary="Backup verified: "+result.Files.ToString("N0")+" files";
   if(result.Incremental)summary+=" · "+result.Copied.ToString("N0")+" new or changed copied ("+ImportWorkflow.Size(result.CopiedBytes)+"), "+result.Reused.ToString("N0")+" unchanged shared with the previous backup";
   if(result.LinksUnavailable)summary+=". This drive cannot share unchanged files, so they were copied";
   return summary+".";
  }
  void BackUpArchive(){
   TextBox destination;CheckBox compress,incremental;var dialog=BackupDialog(out destination,out compress,out incremental);if(dialog==null)return;
   if(!dialog.Show())return;settings.BackupDestination=destination.Text;settings.CompressBackup=compress.IsChecked==true;settings.BackupIncremental=incremental.IsChecked==true;SaveSettings();
   RunBackup(destination.Text,compress.IsChecked==true,incremental.IsChecked==true,false);
  }
  void RunBackup(string folder,bool zipped,bool incremental,bool automatic){
   BackupResult result=null;List<string> pruned=new List<string>();int keep=settings.BackupKeep;
   cancellationMessage="Backup canceled. Existing backups are kept.";OpenDumpProgress(automatic?"Automatic backup":"Backing up archive");
   Run(ct=>{
    result=repo.CreateBackup(folder,zipped,ct,Progress,incremental);
    try{pruned=BackupSets.Prune(folder,repo.RepositoryId(),keep,ct);}catch(Exception e){if(e is OperationCanceledException||!(e is IOException||e is UnauthorizedAccessException))throw;}
    return BackupSummary(result)+(pruned.Count>0?" Removed "+pruned.Count+" older backup"+(pruned.Count==1?"":"s")+".":"");
   },message=>{
    settings.LastBackupUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);settings.BackupReminderUtc=null;SaveSettings();
    L("StatusLabel").Text=message;if(completionActivity!=null){completionActivity.OutputPath=result.Compressed?Path.GetDirectoryName(result.Path):result.Path;completionActivity.ReportTitle="Restore this backup";completionActivity.Report=result.Path+"\n"+message+"\nTo restore, choose Settings > Backups > Restore a backup…"+(result.Compressed?"":", or choose the backup’s Repository folder in Settings > General.");}
    if(!automatic&&settings.BackupFrequency=="Off")Window.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(OfferBackupSchedule));
   },automatic?"Automatic backup":"Backing up archive");
  }
  FormWindow BackupDialog(out TextBox destination,out CheckBox compress,out CheckBox incremental){
   destination=null;compress=null;incremental=null;if(repo==null||RepositoryOperationBlocked)return null;var dialog=new FormWindow(Window,"Back up archive",600,570);
   dialog.Text("Create a recoverable backup",true);dialog.Text("Copies originals, edited files and the current archive database. Choose a folder outside the archive; a separate drive is best.");
   dialog.Text((repositorySize==null?"":"This archive uses "+repositorySize.Summary+". ")+"Large backups can take a long time to create and verify. You can keep using Windows and cancel safely.");
   var folderInput=dialog.Input("Save backup in",settings.BackupDestination??"");destination=folderInput;folderInput.IsReadOnly=true;
   dialog.Button("Choose backup folder…",()=>{string path=Folder("Choose a backup destination outside the archive",folderInput.Text,dialog.Window);if(path!=null)folderInput.Text=path;});
   compress=dialog.Check("Compress as ZIP (lossless)",settings.CompressBackup);dialog.Text("Without compression, the backup is a normal folder. ZIP compression preserves every image byte, including resolution and metadata.");
   incremental=dialog.Check("Copy only new and changed files (incremental)",settings.BackupIncremental);dialog.Text("The first backup in a folder is complete. Later backups copy only what changed since the previous backup there, yet each folder backup stays complete on its own.");
   dialog.Accept("Create backup",()=>{try{if(string.IsNullOrWhiteSpace(folderInput.Text))throw new IOException("Choose a backup folder first.");Repository.ValidateBackupDestination(folderInput.Text,repo.Root);return true;}catch(Exception error){MessageBox.Show(dialog.Window,error.Message,"Choose a backup folder",MessageBoxButton.OK,MessageBoxImage.Information);return false;}});
   return dialog;
  }
  // Offered once a manual backup succeeds, while no schedule is set.
  void OfferBackupSchedule(){
   if(closing||repo==null||settings.BackupFrequency!="Off"||string.IsNullOrEmpty(settings.BackupDestination))return;
   var d=new FormWindow(Window,"Keep this backup up to date",560,410);d.Text("Back up automatically?",true);
   d.Text("AstroArchive can repeat this backup on a schedule while it is open and idle. Each run copies only new and changed files to the same folder.");
   var frequency=d.Select("How often",new[]{"Daily","Weekly","Monthly"},"Weekly");var keep=d.Select("Keep",BackupKeepChoices,BackupKeepLabel(settings.BackupKeep));
   d.Add(BackupNote("Change this any time in Settings > Backups."));
   d.Accept("Turn on",()=>true);if(!d.Show())return;
   settings.BackupFrequency=Convert.ToString(frequency.SelectedItem);settings.BackupKeep=BackupKeepCount(Convert.ToString(keep.SelectedItem));settings.BackupIncremental=true;SaveSettings();
   L("StatusLabel").Text="Automatic "+settings.BackupFrequency.ToLowerInvariant()+" backups are on.";
  }
  void RestoreBackup(){
   if(NetworkImportBlocked||cancel!=null)return;
   var picker=new Microsoft.Win32.OpenFileDialog{Title="Choose a backup to restore",Filter="AstroArchive backups (backup.json, *.zip)|backup.json;*.zip",CheckFileExists=true};if(picker.ShowDialog(Window)!=true)return;
   string backup=picker.FileName;BackupManifest manifest;
   try{manifest=BackupSets.ReadManifest(backup);if(manifest==null||manifest.Files==null)throw new InvalidDataException("This backup lists no files.");}catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException))throw;MessageBox.Show(Window,"This is not a readable AstroArchive backup: "+e.Message,"Restore a backup",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
   bool zip=backup.EndsWith(".zip",StringComparison.OrdinalIgnoreCase);string source=zip?backup:Path.GetDirectoryName(backup);DateTime created;DateTime.TryParse(manifest.CreatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out created);
   var d=new FormWindow(Window,"Restore a backup",600,520);d.Text("Restore "+(manifest.Archive??"archive"),true);
   d.Text((created==default(DateTime)?"":"Backed up "+created.ToLocalTime().ToString("d MMM yyyy HH:mm",CultureInfo.CurrentCulture)+" · ")+manifest.Files.Count.ToString("N0")+" files · "+ImportWorkflow.Size(manifest.Files.Sum(f=>f.Bytes))+(zip&&manifest.Chain!=null&&manifest.Chain.Count>1?" · builds on "+(manifest.Chain.Count-1)+" earlier ZIP"+(manifest.Chain.Count==2?"":"s")+" in the same folder":""));
   d.Text("AstroArchive copies every file into a new folder and checks its SHA-256 checksum. The backup itself is not changed.");
   string suggested=Util.Safe((manifest.Archive??"AstroArchive")+" restored "+DateTime.Now.ToString("yyyy-MM-dd"));
   string near=repo!=null?Path.GetDirectoryName(repo.Root.TrimEnd('\\','/')):Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
   var parent=d.Input("Restore into a new folder inside",near??"");parent.IsReadOnly=true;
   d.Button("Choose folder…",()=>{string path=Folder("Choose where to create the restored archive",parent.Text,d.Window);if(path!=null)parent.Text=path;});
   var name=d.Input("New folder name",suggested);string target=null;
   d.Accept("Restore",()=>{
    try{if(string.IsNullOrWhiteSpace(parent.Text)||!Directory.Exists(parent.Text))throw new IOException("Choose an existing folder to restore into.");string folder=Util.Safe(name.Text.Trim());target=Path.Combine(parent.Text,folder);if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any())throw new IOException("A folder named "+folder+" already exists there. Choose another name.");if(!zip&&Util.Within(target,source))throw new IOException("Restore outside the backup folder.");return true;}
    catch(Exception e){if(!(e is IOException||e is ArgumentException||e is UnauthorizedAccessException))throw;MessageBox.Show(d.Window,e.Message,"Restore a backup",MessageBoxButton.OK,MessageBoxImage.Information);return false;}
   });
   if(!d.Show()||target==null)return;BackupRestoreResult restored=null;
   cancellationMessage="Restore canceled. The backup is unchanged and the incomplete restore was removed.";OpenDumpProgress("Restoring backup");
   Run(ct=>{restored=BackupSets.Restore(source,target,ct,Progress);return "Restored and verified "+restored.Files.ToString("N0")+" files into "+restored.Repository+".";},message=>{
    L("StatusLabel").Text=message;if(completionActivity!=null)completionActivity.OutputPath=restored.Repository;string repository=restored.Repository;
    Window.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{if(closing||RepositoryOperationBlocked)return;if(MessageBox.Show(Window,message+"\n\nOpen the restored archive now?","Backup restored",MessageBoxButton.YesNo,MessageBoxImage.Information)==MessageBoxResult.Yes){try{OpenRepository(repository);}catch(Exception e){MessageBox.Show(Window,e.Message,"Restored archive could not open",MessageBoxButton.OK,MessageBoxImage.Warning);}}}));
   },"Restoring backup");
  }
  // Automatic schedule: checked every few minutes; runs only when nothing else is happening.
  DispatcherTimer backupTimer;
  internal static TimeSpan BackupInterval(string frequency){return BackupSchedule.Interval(frequency);}
  internal static bool BackupDue(Settings s,DateTime utcNow){return s!=null&&BackupSchedule.Due(s.BackupFrequency,s.LastBackupUtc,utcNow);}
  void InitializeBackupSchedule(bool test){
   if(test)return;backupTimer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(3)};
   backupTimer.Tick+=(s,e)=>{backupTimer.Interval=TimeSpan.FromMinutes(10);CheckScheduledBackup();};backupTimer.Start();
  }
  void DisposeBackupSchedule(){if(backupTimer!=null)backupTimer.Stop();}
  void CheckScheduledBackup(){
   if(repo==null||closing||!BackupDue(settings,DateTime.UtcNow))return;
   if(cancel!=null||operationBusy||remoteSessions.Count>0||RepositoryOperationBlocked||NetworkImportBlocked||Window.OwnedWindows.Cast<Window>().Any(w=>w.IsVisible))return;
   string folder=settings.BackupDestination,problem=null;
   try{
    if(string.IsNullOrWhiteSpace(folder))problem="Choose a backup folder in Settings > Backups.";
    else{string drive=Path.GetPathRoot(Path.GetFullPath(folder));if(!Directory.Exists(drive)||!Directory.Exists(folder)&&!Directory.Exists(Path.GetDirectoryName(folder.TrimEnd('\\','/'))))problem="Connect the backup drive for "+folder+", or choose another folder in Settings > Backups.";else Repository.ValidateBackupDestination(folder,repo.Root);}
   }catch(Exception e){if(!(e is IOException||e is ArgumentException||e is UnauthorizedAccessException||e is NotSupportedException))throw;problem=e.Message;}
   if(problem!=null){RemindBackup(problem);return;}
   RunBackup(folder,settings.CompressBackup,settings.BackupIncremental,true);
  }
  // One reminder a day at most, so a disconnected drive never nags.
  void RemindBackup(string problem){
   DateTime last;if(DateTime.TryParse(settings.BackupReminderUtc,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out last)&&DateTime.UtcNow-last<TimeSpan.FromDays(1))return;
   settings.BackupReminderUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);SaveSettings();
   var entry=AddActivity("Backup due");entry.Status=settings.BackupFrequency+" backup is due. "+problem;entry.NeedsReview=true;entry.ActionLabel="Backup settings";entry.Review=()=>Configure(6);NotifyActivity(entry);
  }
 }
}
