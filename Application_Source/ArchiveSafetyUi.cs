using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void AddArchiveSafetySettings(FormWindow dialog){
   dialog.Text("Backups",true);
   dialog.Text("Create a verified copy of the whole archive, including originals, Edited files and the current database. Choose a normal folder or lossless ZIP in the backup dialog.");
   var backup=dialog.Button("Back up archive…",()=>{dialog.Window.Close();BackUpArchive();});backup.IsEnabled=repo!=null;
   if(repo==null)dialog.Text("Choose a repository in General to enable backups.");
   dialog.Text("Restore a backup",true);dialog.Text("Extract the ZIP if needed, then choose its Repository folder in Settings > General. Backup files include restoration instructions and checksums.");
   dialog.Text("Originals protection",true);
   dialog.Text(repo==null?"Open an archive to protect originals or create a backup.":"Originals: "+(repo.OriginalsProtected?"protected":"not protected"));
   dialog.Text("Optional Windows protection helps prevent deleting or renaming archived originals in File Explorer. Edited files, Dump and archive metadata remain writable. Protection is separate from a backup.");
   var protect=dialog.Button(repo!=null&&repo.OriginalsProtected?"Turn off protection…":"Protect originals…",()=>{dialog.Window.Close();ChangeArchiveProtection();});protect.IsEnabled=repo!=null&&(repo.OriginalsProtected||repo.ProtectionAvailability==null);
   if(repo!=null&&repo.ProtectionAvailability!=null)dialog.Text(repo.ProtectionAvailability);
  }
  void ChangeArchiveProtection(){
   if(repo==null||cancel!=null)return;bool enable=!repo.OriginalsProtected;
   var dialog=new FormWindow(Window,enable?"Protect archived originals":"Turn off archive protection",560,440);
   dialog.Text(enable?"Protect archived originals":"Remove deletion protection",true);
   dialog.Text(enable?"Windows will block deletion and renaming of archived originals in File Explorer for this account. AstroArchive can still apply metadata changes and confirmed deletions. Reading, previewing and copying images continue normally.":"Archived originals will use their previous deletion permissions. Existing images and backups are kept.");
   dialog.Text(enable?"This requires a local NTFS drive. Applying protection to a large archive can take a while; there is no background scan once it is applied. The Windows owner can deliberately change permissions, so keep a backup too.":"Removing protection may take a while. Once started, it finishes restoring permissions even if Cancel is pressed.");
   dialog.Accept(enable?"Protect originals":"Turn off protection",()=>true);if(!dialog.Show())return;
   cancellationMessage="Protection change canceled. Previous permissions restored.";OpenDumpProgress(enable?"Protecting originals":"Removing protection");Run(ct=>{repo.SetOriginalsProtection(enable,ct,Progress);return enable?"Archived originals are protected.":"Archive protection is off.";},message=>L("StatusLabel").Text=message);
  }
  void BackUpArchive(){
   TextBox destination;CheckBox compress;var dialog=BackupDialog(out destination,out compress);if(dialog==null)return;
   if(!dialog.Show())return;settings.BackupDestination=destination.Text;settings.CompressBackup=compress.IsChecked==true;SaveSettings();string folder=destination.Text;bool zipped=compress.IsChecked==true;BackupResult result=null;
   cancellationMessage="Backup canceled. Existing backups are kept.";OpenDumpProgress("Backing up archive");Run(ct=>{result=repo.CreateBackup(folder,zipped,ct,Progress);return "Backup verified: "+result.Files+" files.";},message=>{
    L("StatusLabel").Text=message;var complete=new FormWindow(Window,"Backup complete",600,390);complete.Text("Backup created and verified",true);complete.Text(result.Files+" files · "+(result.Compressed?"lossless ZIP":"normal folder"));complete.Text(result.Path);complete.Text("To restore, extract the ZIP if needed, then choose the backup’s Repository folder in Settings > General.");complete.Button("Open backup folder",()=>Process.Start(new ProcessStartInfo(result.Compressed?Path.GetDirectoryName(result.Path):result.Path){UseShellExecute=true}));complete.CloseOnly();complete.Show();
   });
  }
  FormWindow BackupDialog(out TextBox destination,out CheckBox compress){
   destination=null;compress=null;if(repo==null||cancel!=null)return null;var dialog=new FormWindow(Window,"Back up archive",600,520);
   dialog.Text("Create a recoverable backup",true);dialog.Text("Copies originals, edited files and the current archive database. Choose a folder outside the archive; a separate drive is best.");
   dialog.Text("Large backups can take a long time to create and verify. You can keep using Windows and cancel safely.");
   var folderInput=dialog.Input("Save backup in",settings.BackupDestination??"");destination=folderInput;folderInput.IsReadOnly=true;
   dialog.Button("Choose backup folder…",()=>{string path=Folder("Choose a backup destination outside the archive",folderInput.Text,dialog.Window);if(path!=null)folderInput.Text=path;});
   compress=dialog.Check("Compress as ZIP (lossless)",settings.CompressBackup);dialog.Text("Without compression, the backup is a normal folder. ZIP compression preserves every image byte, including resolution and metadata.");
   dialog.Accept("Create backup",()=>{try{if(string.IsNullOrWhiteSpace(folderInput.Text))throw new IOException("Choose a backup folder first.");Repository.ValidateBackupDestination(folderInput.Text,repo.Root);return true;}catch(Exception error){MessageBox.Show(dialog.Window,error.Message,"Choose a backup folder",MessageBoxButton.OK,MessageBoxImage.Information);return false;}});
   return dialog;
  }
 }
}
