// C# 5 / .NET Framework 4.8. One installed instance, with startup update checks.
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using System.Threading.Tasks;

namespace AstroArchive.Installation {
 static class Launcher {
  static void Log(Exception error) {
   try {
    string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroArchive", "updates");
    InstallCore.NoLinks(directory); Directory.CreateDirectory(directory);
    string path = Path.Combine(directory, "last-error.txt"); InstallCore.NoLinks(path);
    File.WriteAllText(path, DateTime.UtcNow.ToString("u") + "\r\n" + error);
   } catch { }
  }

  static bool ConfirmUpdate(UpdateManifest update,string current,string notes){
   using(var form=new Form{Text="Install AstroArchive release",ClientSize=new System.Drawing.Size(680,490),MinimumSize=new System.Drawing.Size(600,440),StartPosition=FormStartPosition.CenterScreen,Font=new System.Drawing.Font("Segoe UI",10),Padding=new Padding(24)}){
    var title=new Label{Text="Package "+update.package_version+" is available. Installed: "+current+".\nYour repositories, images and settings will be retained.",Dock=DockStyle.Top,Height=76};
    var body=new TextBox{Text=notes,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=System.Drawing.Color.White};
    var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0)};
    var install=new Button{Text="Install release",AutoSize=true,MinimumSize=new System.Drawing.Size(150,38),DialogResult=DialogResult.OK};var skip=new Button{Text="Later",AutoSize=true,MinimumSize=new System.Drawing.Size(100,38),DialogResult=DialogResult.Cancel};
    actions.Controls.Add(install);actions.Controls.Add(skip);form.Controls.Add(body);form.Controls.Add(actions);form.Controls.Add(title);form.AcceptButton=install;form.CancelButton=skip;return form.ShowDialog()==DialogResult.OK;
   }
  }
  static bool Update(string root, InstallRecord record, bool explicitCheck) {
   bool accepted = false;
   try {
    var client = new UpdateClient(); var update = client.Check(record);
    if (update == null) {
     if (explicitCheck) MessageBox.Show("AstroArchive is up to date.", "AstroArchive updates");
     return false;
    }
    string notes;try{notes=client.ReleaseNotes(update);}catch{notes="Release notes are unavailable. View this release at "+UpdateClient.ReleasePage(update);}
    if(!ConfirmUpdate(update,record.PackageVersion??record.Version,notes))return false;
    accepted = true;
    string cache = UpdateClient.InstallationCache(root);
    string setup = null;
    Exception downloadError = null;
    using (var progress = new Form { Text = "Updating AstroArchive", ClientSize = new System.Drawing.Size(560,170), Padding=new Padding(24), Font=new System.Drawing.Font("Segoe UI",10),
      StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog,
      ControlBox = false }) {
     var label=new Label{Text="Downloading and verifying the update…",Dock=DockStyle.Fill,TextAlign=System.Drawing.ContentAlignment.MiddleCenter};var bar=new ProgressBar{Dock=DockStyle.Bottom,Height=12,Maximum=100};progress.Controls.Add(label);progress.Controls.Add(bar);
     client.Progress=p=>{if(progress.IsDisposed||!progress.IsHandleCreated)return;try{progress.BeginInvoke(new Action(()=>{if(progress.IsDisposed)return;bar.Value=p.Percent;label.Text="Downloading package "+update.package_version+": "+p.Percent+"%\n"+(p.Received/1048576.0).ToString("0.0")+" / "+(p.Total/1048576.0).ToString("0.0")+" MB";}));}catch(InvalidOperationException){}};
     progress.Shown += async (sender, args) => {
      try { setup = await Task.Run(() => client.Prepare(update, cache)); }
      catch (Exception error) { downloadError = error; }
      finally { client.Progress=null;progress.Close(); }
     };
     progress.ShowDialog();
    }
    if (downloadError != null) throw downloadError;
    if (setup == null) throw new IOException("The update download did not complete.");
    // The installer waits for this launcher to exit before changing application files.
    using(var process=Process.Start(UpdateClient.InstallerStartInfo(update,setup,root,Process.GetCurrentProcess().Id)))
     if(process==null)throw new IOException("The update installer could not start.");
    return true;
   } catch (Exception error) {
    Log(error);
    if (explicitCheck || accepted) MessageBox.Show("The update could not be installed. Your current version is still available.\r\n\r\n" + error.Message,
      "AstroArchive updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    return false;
   }
  }

  [STAThread] static int Main(string[] args) {
   try {
    Application.EnableVisualStyles();
    string root = InstallCore.Root(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)));
    var record = InstallCore.Read(root);
    if (record == null) throw new IOException("Installation record is missing. Run the installer again to repair it.");
    using (var mutex = new Mutex(false, InstallCore.ApplicationMutexName(root))) {
     bool held = false;
     try {
      try { held = mutex.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
      if (!held) throw new IOException("AstroArchive is already running for this installation.");
      bool explicitCheck = Array.IndexOf(args, "--updates") >= 0;
      if (Array.IndexOf(args, "--no-updates") < 0 && Update(root, record, explicitCheck)) return 0;
      if (explicitCheck) return 0;
      string executable = InstallCore.Managed(root, record.ActiveDirectory + "\\AstroArchive.exe");
      if (!File.Exists(executable)) throw new IOException("Application is missing. Run the installer again to repair it.");
      using (var process = Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = false })) {
       process.WaitForExit(); return process.ExitCode;
      }
     } finally { if (held) mutex.ReleaseMutex(); }
    }
   } catch (Exception error) {
    MessageBox.Show(error.Message, "AstroArchive", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1;
   }
  }
 }
}
