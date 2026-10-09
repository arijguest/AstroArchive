using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace AstroArchive {
 public partial class MainUi {
  sealed class ImportPreferenceFields {
   public CheckBox Flagged,Failed,Raster,Originals,Robust;
   public ComboBox Model,Camera,Target,Solve,Rotation,Workers;public TextBlock Error;public Expander Current;
  }
  sealed class PreferenceFields {
   public ComboBox Theme;public CheckBox Preview,Online,AutomaticReleases;public TextBox Repository,Directory,Astap,Database,Fov;public PasswordBox Key;
   public ImportPreferenceFields Import;public ObservingSiteChoice Site;public AccessibilityChoices Accessibility;public Expander Solver;public ExportPreferenceFields Exports;
  }
  ImportPreferenceFields AddImportPreferences(FormWindow d){
   var f=new ImportPreferenceFields();d.Text("Import",true);d.Text("Saved choices apply to folder, USB and Dump imports. Changing file matching requires a new scan.");
   f.Failed=d.Check("Ignore failed filenames",settings.IgnoreFailed);f.Raster=d.Check("Ignore PNG/JPG/JPEG/MP4 files",settings.IgnoreRasterImports);f.Robust=ImportMatchingChoice(d);
   f.Workers=d.Select("Copy workers",new[]{"Auto","1","2","4","8"},settings.CopyWorkers==0?"Auto":settings.CopyWorkers.ToString());UiHelp.Hint(f.Workers,"Auto benchmarks parallel copies and remembers the fastest setting for compatible storage.");
   f.Current=d.Advanced("Current import options",()=>{
    d.Text("Applies to the current source and next manual import.");f.Model=d.Select("Instrument model",TelescopeProfiles.Models.ToArray(),Convert.ToString(C("ModelBox").SelectedItem));f.Camera=d.Select("Camera channel",new[]{"Auto","Telephoto","Wide"},Convert.ToString(C("CameraBox").SelectedItem));
    f.Target=ImportTargetChoice(d,unknownImportTarget);UiHelp.Describe(f.Target,"Fill Unknown lights/stacks only. Leave blank for mixed-target scans.");f.Error=new TextBlock{TextWrapping=TextWrapping.Wrap};d.Add(f.Error);
    f.Flagged=d.Check("Skip flagged captures",SkipFlagged);f.Solve=d.Select("Target analysis",new[]{"Off","Ambiguous only","All light/stack files"},Convert.ToString(C("ImportSolveMode").SelectedItem));f.Rotation=d.Select("Rotation analysis",new[]{"Off","Ambiguous mounts","All light sessions"},Convert.ToString(C("ImportRotationMode").SelectedItem));
    f.Originals=d.Check("Delete originals after verified import",((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked==true);f.Originals.IsEnabled=((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsEnabled;OriginalsChoiceStatus(d,f.Originals);d.Text("Originals are kept by default. Removal affects newly imported, verified files only and resets when the source changes. Cloud-synced deletions propagate.");
   });return f;
  }
  TextBlock OriginalsChoiceStatus(FormWindow dialog,CheckBox choice){
   var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,8),FontWeight=FontWeights.SemiBold};status.SetResourceReference(TextBlock.ForegroundProperty,"Text");status.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");dialog.Add(status);
   Action update=()=>{string reason=choice.IsEnabled?"":RepositoryOperationBlocked?" Wait for the current operation to finish.":repo==null?" Choose a repository, then scan a source folder to enable this option.":" Scan a source folder first to enable this option.";status.Text=(choice.IsChecked==true?"On — verified source originals will be deleted.":"Off — source originals will be kept.")+reason;UiHelp.Hint(choice,status.Text,true);};
   choice.Checked+=(s,e)=>update();choice.Unchecked+=(s,e)=>update();choice.IsEnabledChanged+=(s,e)=>update();update();return status;
  }
  FormWindow PreferencesDialog(Window owner,int section,out PreferenceFields fields){
   var d=new FormWindow(owner,"Preferences",860,710);d.Window.MinWidth=Math.Min(640,d.Window.Width);d.Sections("General","Import","Export","Sky & solving","Accessibility","Updates","Backups");var f=fields=new PreferenceFields();
   d.Text("General",true);f.Theme=d.Select("Theme",new[]{"System","Light","Dark"},settings.ThemeMode??"System");f.Preview=d.Check("Show image preview",settings.ShowPreview);
   f.Repository=d.Input("Repository folder",repo==null?settings.Repository:repo.Root);f.Repository.IsReadOnly=true;var chooseRepository=d.Button("Choose repository…",()=>{string path=Folder("Choose your repository",f.Repository.Text,d.Window);if(path!=null)f.Repository.Text=path;});chooseRepository.IsEnabled=owner==Window;if(owner!=Window)UiHelp.Hint(chooseRepository,"Close the export dialog before changing repositories.");UiHelp.Describe(f.Repository,"Changes take effect when you save preferences. Click the path on the main page to open the folder.");
   d.Advanced("Image compatibility",()=>{d.Text("Optional codecs: CFITSIO "+(NativeFits.Available?"available":"unavailable")+" · Zstandard "+(PixelCodecs.ZstdAvailable?"available":"unavailable"));d.Button("Supported formats…",()=>ShowReport("Image compatibility",FormatGuide));d.Button("Open codec folder",()=>{Directory.CreateDirectory(NativeCodecs.Folder);OpenFolder(NativeCodecs.Folder);});});
   d.Tab(1);f.Import=AddImportPreferences(d);
   d.Tab(2);d.Text("Export",true);f.Directory=d.Input("Default export folder",settings.ExportWorkingDirectory??"");d.Button("Choose export folder…",()=>{string path=Folder("Choose the default export folder",f.Directory.Text,d.Window);if(path!=null)f.Directory.Text=path;});
   d.Text("Choose Siril in the export popup to open one exported stack. Application locations and defaults are optional.");f.Exports=AddExportPreferences(d);
   d.Tab(3);d.Text("Sky & solving",true);f.Site=AddObservingSite(d);f.Online=d.Check("Use Astrometry.net instead of ASTAP",settings.UseOnline);
   f.Solver=d.Advanced("Plate solver setup",()=>{
    f.Astap=d.Input("ASTAP executable",settings.Astap??"");d.Button("Choose ASTAP…",()=>{var picker=new OpenFileDialog{Title="Locate ASTAP",Filter="Windows executable|*.exe"};if(picker.ShowDialog(d.Window)==true)f.Astap.Text=picker.FileName;});f.Database=d.Input("ASTAP star database folder",settings.StarDatabase??"");d.Button("Choose star database…",()=>{string path=Folder("Choose the ASTAP star database",f.Database.Text,d.Window);if(path!=null)f.Database.Text=path;});
    f.Fov=d.Input("Image height in degrees (blank: automatic)",settings.FieldHeight.HasValue?Util.Num(settings.FieldHeight):"");d.Text("Astrometry.net API key");f.Key=new PasswordBox{Password=PlateSolve.Unprotect(settings.ApiKeyProtected),Padding=new Thickness(10,8,10,8)};UiHelp.Hint(f.Key,"Saved with Windows user encryption. Online solving sends star coordinates, never the full image.");System.Windows.Automation.AutomationProperties.SetName(f.Key,"Astrometry.net API key");d.Add(f.Key);d.Text("ASTAP needs a local star database. Online solving needs your API key and internet access. Analysis stays off until selected for an import.");
   });f.Online.Checked+=(s,e)=>f.Solver.IsExpanded=true;if(settings.UseOnline)f.Solver.IsExpanded=true;
   d.Tab(4);f.Accessibility=AddAccessibilityPreferences(d);
   d.Tab(5);f.AutomaticReleases=d.Check("Check for releases automatically while AstroArchive is open",!settings.DisableAutomaticReleaseChecks);d.Text("Checks after startup and every six hours. Downloads and installation begin only when you request them.");AddReleaseSettings(d,()=>SavePreferences(d,f));
   d.Tab(6);AddArchiveSafetySettings(d);
   d.SelectTab(section);if(section==1)f.Import.Current.IsExpanded=true;if(section==3)f.Solver.IsExpanded=true;d.Accept("Save preferences",()=>SavePreferences(d,f));return d;
  }
  static string OptionalPath(string value){return string.IsNullOrWhiteSpace(value)?null:Path.GetFullPath(value.Trim());}
  bool SavePreferences(FormWindow d,PreferenceFields f){
   try{
    if(!ValidImportTarget(f.Import.Target.Text,f.Import.Error,true)){d.SelectTab(1);f.Import.Current.IsExpanded=true;return false;}
    double? field=ParseOptional(f.Fov.Text.Trim());if(f.Fov.Text.Trim().Length>0&&(!field.HasValue||field<=0||field>180)){d.SelectTab(3);f.Solver.IsExpanded=true;f.Fov.Focus();throw new ArgumentException("Image height must be greater than 0 and no more than 180 degrees.");}
    if(f.Online.IsChecked==true&&string.IsNullOrWhiteSpace(f.Key.Password)){d.SelectTab(3);f.Solver.IsExpanded=true;f.Key.Focus();throw new ArgumentException("Enter your Astrometry.net API key to enable online solving.");}
    ValidateExportPreferences(d,f.Exports);string directory=OptionalPath(f.Directory.Text),repository=OptionalPath(f.Repository.Text);
    if(directory!=null&&!string.Equals(directory,settings.ExportWorkingDirectory,StringComparison.OrdinalIgnoreCase)){d.SelectTab(2);if(!Directory.Exists(directory))throw new IOException("Choose an existing export folder.");Exporter.CheckDestinationPath(directory);if(repo!=null)Exporter.Destination(repo,new ExportOptions{Parent=directory});}
    if(directory!=null&&repository!=null&&Util.Within(directory,repository)){d.SelectTab(2);throw new IOException("Choose an export folder outside the repository.");}
    bool currentImportChanged=!Equals(f.Import.Model.SelectedItem,C("ModelBox").SelectedItem)||!Equals(f.Import.Camera.SelectedItem,C("CameraBox").SelectedItem)||!Equals(f.Import.Solve.SelectedItem,C("ImportSolveMode").SelectedItem)||!Equals(f.Import.Rotation.SelectedItem,C("ImportRotationMode").SelectedItem)||f.Import.Target.Text!=unknownImportTarget||f.Import.Flagged.IsChecked!=((CheckBox)Window.FindName("SkipFlaggedCheck")).IsChecked||f.Import.Originals.IsChecked!=((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked;
    bool repositoryChanged=repository!=null&&(repo==null||!string.Equals(repository,repo.Root,StringComparison.OrdinalIgnoreCase));if(repositoryChanged){d.SelectTab(0);OpenRepository(repository,false);}
    bool applyCurrentImport=!repositoryChanged||currentImportChanged;
    bool rescan=repositoryChanged||!Equals(f.Import.Model.SelectedItem,C("ModelBox").SelectedItem)||!Equals(f.Import.Camera.SelectedItem,C("CameraBox").SelectedItem)||settings.IgnoreFailed!=(f.Import.Failed.IsChecked==true)||settings.IgnoreRasterImports!=(f.Import.Raster.IsChecked==true)||settings.RobustImportMatching!=(f.Import.Robust.IsChecked==true);
    var draft=Util.Deserialize<Settings>(Util.Serialize(settings));draft.ThemeMode=Convert.ToString(f.Theme.SelectedItem);draft.ShowPreview=f.Preview.IsChecked==true;f.Accessibility.Save(draft);f.Site.Save(draft);draft.FieldHeight=field;draft.Astap=f.Astap.Text.Trim();draft.StarDatabase=f.Database.Text.Trim();draft.UseOnline=f.Online.IsChecked==true;draft.AutoSolve=false;draft.AutoRotation=false;
    draft.ApiKeyProtected=f.Key.Password==PlateSolve.Unprotect(settings.ApiKeyProtected)?settings.ApiKeyProtected:PlateSolve.Protect(f.Key.Password);f.Exports.Save(draft);draft.ExportWorkingDirectory=directory;int workers;draft.CopyWorkers=int.TryParse(Convert.ToString(f.Import.Workers.SelectedItem),out workers)?workers:0;draft.IgnoreFailed=f.Import.Failed.IsChecked==true;draft.IgnoreRasterImports=f.Import.Raster.IsChecked==true;draft.RobustImportMatching=f.Import.Robust.IsChecked==true;draft.Model=Convert.ToString(applyCurrentImport?f.Import.Model.SelectedItem:C("ModelBox").SelectedItem);draft.LastSource=T("SourceBox").Text;draft.Telescope=T("TelescopeBox").Text;
    Directory.CreateDirectory(Path.GetDirectoryName(config));draft.DisableAutomaticReleaseChecks=f.AutomaticReleases.IsChecked!=true;Util.AtomicText(config,Util.Serialize(draft));settings=draft;ApplyReleaseMonitoringPreference();
    if(applyCurrentImport){unknownImportTarget=string.IsNullOrWhiteSpace(f.Import.Target.Text)?"":ImportPolicy.Target(f.Import.Target.Text);C("ModelBox").SelectedItem=f.Import.Model.SelectedItem;C("CameraBox").SelectedItem=f.Import.Camera.SelectedItem;C("ImportSolveMode").SelectedItem=f.Import.Solve.SelectedItem;C("ImportRotationMode").SelectedItem=f.Import.Rotation.SelectedItem;((CheckBox)Window.FindName("SkipFlaggedCheck")).IsChecked=f.Import.Flagged.IsChecked;}else unknownImportTarget="";((CheckBox)Window.FindName("IgnoreFailedCheck")).IsChecked=f.Import.Failed.IsChecked;
    if(rescan)InvalidateImportPlan();else if(plan!=null&&unknownImportTarget.Length>0)ImportPolicy.AssignUnknown(plan.Frames,unknownImportTarget);((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked=!rescan&&f.Import.Originals.IsEnabled&&f.Import.Originals.IsChecked==true;
    ApplyAppearance();SetPreviewVisibility();if(settings.ShowPreview)PreviewSelected();RefreshSkyPreviews();FilterImports();UpdateNavigationState();L("StatusLabel").Text=rescan?"Preferences saved. Scan the source again to apply import changes.":"Preferences saved.";return true;
   }catch(Exception error){MessageBox.Show(d.Window,error.Message,"Preferences could not be saved",MessageBoxButton.OK,MessageBoxImage.Warning);return false;}
  }
  bool Configure(int initialTab=0,Window owner=null){if(RepositoryOperationBlocked)return false;PreferenceFields fields;return PreferencesDialog(owner??Window,initialTab,out fields).Show();}
 }
}
