// C# 5 / .NET Framework 4.8, Windows 10/11 x64. Fully offline setup and local updates.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace AstroArchive.Installation {
 class PayloadManifest {public string Version,PackageVersion;public int Revision;public Dictionary<string,string> Hashes;}
 static class Setup {
  public static string ApplicationVersion=ReleaseVersion.Application;
  static byte[] Resource(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)throw new IOException("Missing installer payload: "+name);using(var bytes=new MemoryStream()){stream.CopyTo(bytes);return bytes.ToArray();}}}
  public static InstallPackage Package(){var manifest=InstallCore.Json().Deserialize<PayloadManifest>(System.Text.Encoding.UTF8.GetString(Resource("payload.json")));var package=new InstallPackage{Version=manifest.Version,PackageVersion=manifest.PackageVersion,Revision=manifest.Revision};using(var input=new MemoryStream(Resource("payload.zip")))using(var zip=new ZipArchive(input,ZipArchiveMode.Read)){foreach(var entry in zip.Entries){if(entry.Length>100*1024*1024)throw new IOException("Payload entry is too large.");string hash;if(!manifest.Hashes.TryGetValue(entry.FullName,out hash)||package.Files.Any(f=>f.Name==entry.FullName))throw new IOException("Unexpected installer payload.");using(var stream=entry.Open())using(var bytes=new MemoryStream()){stream.CopyTo(bytes);package.Files.Add(new PayloadFile{Name=entry.FullName,Hash=hash,Bytes=bytes.ToArray()});}}}if(package.Files.Count!=manifest.Hashes.Count)throw new IOException("Incomplete installer payload.");return package;}
  static string Arg(string[] args,string name,string fallback){int i=Array.IndexOf(args,name);if(i<0)return fallback;if(i+1>=args.Length)throw new IOException("A value is required after "+name);return args[i+1];}
  static bool Has(string[] args,string name){return Array.IndexOf(args,name)>=0;}
  static InstallCore Engine(Action<string> progress){return new InstallCore{Progress=progress,EnsureClosed=WindowsIntegration.EnsureClosed,Register=WindowsIntegration.Register,Unregister=WindowsIntegration.Unregister,ShortcutPaths=WindowsIntegration.ShortcutPaths};}
  static void Wait(string[] args){int id;if(int.TryParse(Arg(args,"--waitpid","0"),out id)&&id>0)try{using(var p=Process.GetProcessById(id)){if(!p.WaitForExit(60000))throw new IOException("AstroArchive is still running. Close it and run setup again.");}}catch(ArgumentException){}}
  static void Launch(string root){var record=InstallCore.Read(root);Process.Start(new ProcessStartInfo(InstallCore.Managed(root,record.ActiveDirectory+"\\Start.exe")){UseShellExecute=true});}
  static void CleanupTempSelf(){string location=Assembly.GetExecutingAssembly().Location,name=Path.GetFileName(location);if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"^AstroArchive-uninstall-[a-f0-9]+\.exe$")||Path.GetDirectoryName(location)!=Path.GetTempPath().TrimEnd('\\','/'))return;try{Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe"),"/d /q /c \"timeout /t 2 /nobreak >nul & del /f /q "+name+"\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(location)});}catch{}}
  [STAThread]static int Main(string[] args){bool silent=Has(args,"--silent");try{AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);WindowsIntegration.CheckEnvironment();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Wait(args);string self=Assembly.GetExecutingAssembly().Location;bool remove=Has(args,"--uninstall")||Path.GetFileName(self).Equals("Uninstall.exe",StringComparison.OrdinalIgnoreCase);string root=Arg(args,"--root",remove?Path.GetDirectoryName(self):WindowsIntegration.RegisteredRoot());
    if(remove){root=InstallCore.Root(root);if(!silent&&MessageBox.Show("Remove AstroArchive application files and shortcuts? Your repositories, images and user settings will be retained.","Uninstall AstroArchive",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return 0;if(self.Equals(Path.Combine(root,"Uninstall.exe"),StringComparison.OrdinalIgnoreCase)){string temp=Path.Combine(Path.GetTempPath(),"AstroArchive-uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(self,temp);Process.Start(new ProcessStartInfo(temp,"--uninstall --silent --root "+WindowsIntegration.Quote(root)+" --waitpid "+Process.GetCurrentProcess().Id){UseShellExecute=false});return 0;}Engine(s=>{}).Uninstall(root);if(!silent)MessageBox.Show("AstroArchive was removed. Repositories and settings were retained.","AstroArchive");CleanupTempSelf();return 0;}
    if(Has(args,"--ui-test")){using(var form=new SetupForm(root,false)){form.Show();Application.DoEvents();form.Smoke(Arg(args,"--ui-test",null));}return 0;}
    if(silent){Engine(s=>{}).Install(root,Package(),File.ReadAllBytes(self),Has(args,"--update"));if(Has(args,"--restart"))Launch(root);return 0;}using(var form=new SetupForm(root,Has(args,"--update"))){Application.Run(form);return form.Result;}
   }catch(Exception e){try{File.WriteAllText(Path.Combine(Path.GetTempPath(),"AstroArchive-setup-error.txt"),e.ToString());}catch{}if(!silent)MessageBox.Show(e.Message,"AstroArchive Setup",MessageBoxButtons.OK,MessageBoxIcon.Error);CleanupTempSelf();return 1;}}
  sealed class SetupForm:Form {
   TextBox location;Button install,browse,cancel;Label status,summary;ProgressBar progress;CheckBox launch;bool busy;readonly bool updateOnly;public int Result=1;
   public SetupForm(string root,bool onlyUpdate){
    updateOnly=onlyUpdate;Text="AstroArchive "+ApplicationVersion+" Setup";Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
    AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(720,490);MinimumSize=new Size(700,520);MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
    Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(242,245,250);
    var header=new Panel{Dock=DockStyle.Top,Height=112,BackColor=Color.FromArgb(17,28,55)};
    using(var stream=new MemoryStream(Resource("logo.png")))header.Controls.Add(new PictureBox{Image=new Bitmap(Image.FromStream(stream)),SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(24,18),Size=new Size(76,76)});
    header.Controls.Add(new Label{Text="AstroArchive",ForeColor=Color.White,Font=new Font("Segoe UI",23,FontStyle.Bold),Location=new Point(120,23),AutoSize=true});
    header.Controls.Add(new Label{Text="Version "+ApplicationVersion+"  ·  Windows 10 / 11",ForeColor=Color.FromArgb(194,210,235),Location=new Point(123,72),AutoSize=true});
    var footer=new Panel{Dock=DockStyle.Bottom,Height=108,BackColor=Color.White,Padding=new Padding(28,12,28,16)};
    status=new Label{Text="Ready. Close AstroArchive before continuing.",Dock=DockStyle.Top,Height=25,AutoEllipsis=true,ForeColor=Color.FromArgb(77,96,124)};footer.Controls.Add(status);
    progress=new ProgressBar{Dock=DockStyle.Top,Height=5,Visible=false,Style=ProgressBarStyle.Marquee};footer.Controls.Add(progress);progress.BringToFront();
    var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=40,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
    install=new Button{Text="Install AstroArchive",AutoSize=true,MinimumSize=new Size(164,36),Padding=new Padding(12,3,12,3),BackColor=Color.FromArgb(77,85,199),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand};install.FlatAppearance.BorderSize=0;install.Click+=InstallClick;actions.Controls.Add(install);
    cancel=new Button{Text="Cancel",Size=new Size(100,36),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(36,50,71),Cursor=Cursors.Hand,Margin=new Padding(0,3,12,3)};cancel.FlatAppearance.BorderColor=Color.FromArgb(203,213,227);cancel.Click+=(sender,args)=>Close();actions.Controls.Add(cancel);footer.Controls.Add(actions);
    var body=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28,20,28,16),ColumnCount=1,RowCount=6};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    for(int row=0;row<5;row++)body.RowStyles.Add(new RowStyle(SizeType.AutoSize));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
    body.Controls.Add(new Label{Text=updateOnly?"Update your capture library":"Set up your capture library",AutoSize=true,Font=new Font("Segoe UI",15,FontStyle.Bold),ForeColor=Color.FromArgb(36,50,71),Margin=new Padding(0,0,0,12)},0,0);
    summary=new Label{AutoSize=true,MaximumSize=new Size(650,0),ForeColor=Color.FromArgb(77,96,124),Margin=new Padding(0,0,0,18)};body.Controls.Add(summary,0,1);
    body.Controls.Add(new Label{Text="Installation folder",AutoSize=true,ForeColor=Color.FromArgb(83,98,120),Margin=new Padding(0,0,0,7)},0,2);
    var folder=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,AutoSize=true,Margin=new Padding(0,0,0,12)};folder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));folder.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
    location=new TextBox{Text=root,Dock=DockStyle.Fill,Margin=new Padding(0,4,12,0),ReadOnly=updateOnly};folder.Controls.Add(location,0,0);
    browse=new Button{Text="Browse…",Size=new Size(98,32),Margin=new Padding(0),Enabled=!updateOnly,Cursor=Cursors.Hand};browse.Click+=(sender,args)=>{string path=AstroArchive.NativeFolderPicker.Select(Handle,"Choose an installation folder",location.Text);if(path!=null)location.Text=path;};folder.Controls.Add(browse,1,0);body.Controls.Add(folder,0,3);
    body.Controls.Add(new Label{Text="Your repositories, images and settings are kept.\nDesktop and Start menu shortcuts are included. No administrator access is needed.",AutoSize=true,MaximumSize=new Size(650,0),ForeColor=Color.FromArgb(77,96,124),Margin=new Padding(0,0,0,16)},0,4);
    launch=new CheckBox{Text="Launch AstroArchive when finished",Checked=true,AutoSize=true,Margin=new Padding(0,0,0,0)};body.Controls.Add(launch,0,5);
    Controls.Add(body);Controls.Add(footer);Controls.Add(header);location.TextChanged+=(sender,args)=>Describe();Describe();
    AcceptButton=install;CancelButton=cancel;FormClosing+=(sender,args)=>{if(busy)args.Cancel=true;};
   }
   void Describe(){
    try{var current=InstallCore.Read(location.Text);if(current==null){install.Text="Install AstroArchive";summary.Text="Organise telescope captures and prepare verified stacking folders.";}
     else{bool repair=InstallCore.Parse(current.PackageVersion??current.Version)==InstallCore.Parse(ReleaseVersion.Package);install.Text=repair?"Repair installation":"Update AstroArchive";summary.Text=repair?"This version is installed. Setup can verify and replace its application files.":"Upgrade from "+current.Version+" to "+ApplicationVersion+" in the existing installation.";}
    }catch{install.Text="Install AstroArchive";summary.Text="Choose an empty folder or an existing AstroArchive installation.";}
   }
   public void Smoke(string output){
    Directory.CreateDirectory(output);PerformLayout();Update();
    Rectangle area=RectangleToScreen(ClientRectangle);foreach(Control control in new Control[]{location,browse,launch,status,install,cancel,summary}){Rectangle bounds=control.RectangleToScreen(control.ClientRectangle);if(control.Visible&&!area.Contains(bounds))throw new IOException("Installer control is clipped: "+control.GetType().Name);}
    if(location.Width<300||install.Height<30)throw new IOException("Installer controls are too small.");
    using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(output,"AstroArchive_Installer_UI.png"),System.Drawing.Imaging.ImageFormat.Png);}
    File.WriteAllText(Path.Combine(output,"installer-ui-smoke.txt"),"PASS: installer layout, folder picker, launch preference, action controls and render.");
   }
   async void InstallClick(object sender,EventArgs args){
    if(busy)return;busy=true;install.Enabled=browse.Enabled=cancel.Enabled=location.Enabled=launch.Enabled=false;progress.Visible=true;status.Text="Preparing and verifying application files…";
    try{string root=location.Text;var engine=Engine(message=>BeginInvoke(new Action(()=>status.Text=message)));await Task.Run(()=>engine.Install(root,Package(),File.ReadAllBytes(Assembly.GetExecutingAssembly().Location),updateOnly));
     Result=0;status.Text="AstroArchive is ready. Your shortcuts have been added.";summary.Text="Installation complete. Your capture library is ready to open.";progress.Visible=false;install.Visible=false;cancel.Enabled=true;cancel.Text="Finish";AcceptButton=cancel;
     if(launch.Checked){try{Launch(root);}catch(Exception e){MessageBox.Show("Installation completed, but the app could not start: "+e.Message,Text);}}
    }catch(Exception e){status.Text="Setup could not finish. You can retry.";MessageBox.Show(e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);install.Enabled=cancel.Enabled=location.Enabled=launch.Enabled=true;browse.Enabled=!updateOnly;progress.Visible=false;}
    finally{busy=false;}
   }
  }
 }
}
