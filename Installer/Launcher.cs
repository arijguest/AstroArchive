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

  static bool Update(string root, InstallRecord record, bool explicitCheck) {
   bool accepted = false;
   try {
    var client = new UpdateClient(); var update = client.Check(record);
    if (update == null) {
     if (explicitCheck) MessageBox.Show("AstroArchive is up to date.", "AstroArchive updates");
     return false;
    }
    if (MessageBox.Show("AstroArchive " + update.application_version + " (package " + update.package_version +
      ") is available. Install it now?\r\n\r\nYour repositories, images and settings will be retained.",
      "AstroArchive update", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
      MessageBoxDefaultButton.Button1) != DialogResult.Yes) return false;
    accepted = true;
    string cache = UpdateClient.InstallationCache(root);
    string setup = null;
    Exception downloadError = null;
    using (var progress = new Form { Text = "Updating AstroArchive", Width = 430, Height = 125,
      StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog,
      ControlBox = false }) {
     progress.Controls.Add(new Label { Text = "Downloading and verifying the update...", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
     progress.Shown += async (sender, args) => {
      try { setup = await Task.Run(() => client.Prepare(update, cache)); }
      catch (Exception error) { downloadError = error; }
      finally { progress.Close(); }
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
