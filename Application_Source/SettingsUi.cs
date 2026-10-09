using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
namespace AstroArchive {
 public partial class MainUi {
  void Configure(int initialTab=0,bool focusReleases=false){if(cancel!=null)return;CreateSettingsDialog(initialTab,focusReleases).Show();}
  FormWindow CreateSettingsDialog(int initialTab=0,bool focusReleases=false){
   var d=new FormWindow(Window,"Settings",700,680);
   d.Tabs("Preferences","Import & processing","Plate solving","Repository","Accessibility");
   d.Text("Appearance",true);
   var theme=d.Select("Theme",new[]{"System","Light","Dark"},settings.ThemeMode??"System");
   var preview=d.Check("Show image preview",settings.ShowPreview);
   Func<bool> save=null;
   var releases=AddReleaseSettings(d,()=>save());
   d.Tab(3);d.Text("Active repository",true);
   var repository=d.Input("Archive folder",settings.Repository??"");repository.IsReadOnly=true;
   d.Button("Choose repository folder",()=>{string p=Folder("Choose your local repository",repository.Text,d.Window);if(p!=null)repository.Text=p;});
   d.Text("The repository path opens its folder in File Explorer. A change takes effect when you save settings.");
   AddDumpSettings(d);
   d.Tab(2);d.Text("Image compatibility",true);
   d.Button("Supported formats and conversion",()=>ShowReport("Image compatibility",FormatGuide));
   d.Button("Open optional codec folder",()=>{Directory.CreateDirectory(NativeCodecs.Folder);OpenFolder(NativeCodecs.Folder);});
   d.Text("Optional codecs: CFITSIO "+(NativeFits.Available?"available":"unavailable")+" · Zstandard "+(PixelCodecs.ZstdAvailable?"available":"unavailable"));
   d.Text("Plate solving",true);
   d.Text("Use local ASTAP with an installed star database, or Astrometry.net with your own API key. Online solving sends detected star coordinates, never the full image.");
   var astap=d.Input("ASTAP executable",settings.Astap??"");
   d.Button("Browse ASTAP",()=>{var p=new OpenFileDialog{Filter="Windows executable|*.exe",Title="Choose ASTAP executable"};if(p.ShowDialog(d.Window)==true)astap.Text=p.FileName;});
   var database=d.Input("ASTAP star database folder (blank uses ASTAP default)",settings.StarDatabase??"");
   d.Button("Browse star database",()=>{string p=Folder("ASTAP star database folder",database.Text,d.Window);if(p!=null)database.Text=p;});
   var fov=d.Input("ASTAP image height in degrees (blank: automatic; useful for wide cameras)",settings.FieldHeight.HasValue?Util.Num(settings.FieldHeight):"");
   var online=d.Check("Use Astrometry.net instead of ASTAP",settings.UseOnline);
   d.Text("Astrometry.net API key (saved with Windows user encryption)");
   var key=new PasswordBox{Password=PlateSolve.Unprotect(settings.ApiKeyProtected),Padding=new Thickness(10,8,10,8)};d.Add(key);
   System.Windows.Automation.AutomationProperties.SetName(key,"Astrometry.net API key");
   d.Text("Solving and rotation are off by default. Choose their scope in Import; both can take much longer than copying.");
   d.Tab(1);
   var workers=d.Select("Copy workers",new[]{"Auto (start at 1, benchmark 1/2/4/8)","1","2","4","8"},settings.CopyWorkers==0?"Auto (start at 1, benchmark 1/2/4/8)":settings.CopyWorkers.ToString());
   d.Text("Auto samples smaller files at 1, 2, 4 and 8 workers when at least 64 files / 64 MB are ready. It reuses the best setting for compatible source/archive jobs. Storage and file sizes affect the result.");
   var observingSite=AddObservingSite(d);
   d.Tab(3);d.Text("Archive data",true);
   var delete=d.Button("Delete all archive data",()=>{d.Window.Close();ResetArchive();});
   delete.Background=new SolidColorBrush(Color.FromRgb(183,40,51));delete.BorderBrush=delete.Background;delete.Foreground=Brushes.White;delete.IsEnabled=repo!=null;
   var accessibility=AddAccessibilityPreferences(d);
   save=()=>{
    double? field=ParseOptional(fov.Text);
    if(fov.Text.Trim().Length>0&&(!field.HasValue||field<=0||field>180)){d.SelectTab(2);fov.Focus();MessageBox.Show(d.Window,"Image height must be greater than 0 and no more than 180 degrees.","Check settings");return false;}
    if(online.IsChecked==true&&string.IsNullOrWhiteSpace(key.Password)){d.SelectTab(2);key.Focus();MessageBox.Show(d.Window,"Enter your Astrometry.net API key to enable online solving.","Check settings");return false;}
    string path=repository.Text.Trim();
    if(path.Length>0&&(repo==null||!string.Equals(path,repo.Root,StringComparison.OrdinalIgnoreCase))){
     try{OpenRepository(path,false);}catch(Exception e){d.SelectTab(3);MessageBox.Show(d.Window,e.Message,"Repository could not be opened",MessageBoxButton.OK,MessageBoxImage.Warning);return false;}
    }
    settings.ThemeMode=Convert.ToString(theme.SelectedItem);settings.ShowPreview=preview.IsChecked==true;
    accessibility.Save(settings);ApplyAppearance();SetPreviewVisibility();if(settings.ShowPreview)PreviewSelected();
    settings.FieldHeight=field;settings.Astap=astap.Text.Trim();settings.StarDatabase=database.Text.Trim();settings.UseOnline=online.IsChecked==true;
    settings.AutoSolve=false;settings.AutoRotation=false;
    int count;settings.CopyWorkers=int.TryParse(Convert.ToString(workers.SelectedItem),out count)?count:0;
    settings.ApiKeyProtected=PlateSolve.Protect(key.Password);observingSite.Save(settings);RefreshSkyPreviews();SaveSettings();
    L("StatusLabel").Text="Settings saved.";return true;
   };
   d.Accept("Save settings",()=>save());d.SelectTab(initialTab);
   if(focusReleases)d.Window.Loaded+=(s,e)=>{d.SelectTab(0);releases.View.BringIntoView();releases.Check.Focus();};
   return d;
  }
 }
}
