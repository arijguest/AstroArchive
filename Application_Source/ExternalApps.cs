// External processors receive verified copies, never mutable archive originals.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace AstroArchive {
 public sealed class ExportApplication {
  public string Id,Name,Help; public string[] Executables,ImageTypes,FolderTypes;
  public override string ToString(){return Name;}
 }
 public sealed class ExternalSelection {
  public List<string> Paths=new List<string>(); public bool Folder,Subframes;
 }
 public sealed class DssJob {public string Name,Path;}
 public static class ExternalApps {
  public static readonly ExportApplication[] All={
   new ExportApplication{Id="pixinsight",Name="PixInsight",Executables=new[]{"PixInsight.exe"},ImageTypes=new[]{"FITS","TIFF","XISF","PNG","JPEG"},FolderTypes=new[]{"FITS","TIFF","XISF","Camera RAW"},Help="Images open through PixInsight's script interface. For subframes, opens PixInsight and the exported folder; choose the inputs in WBPP."},
   new ExportApplication{Id="siril",Name="Siril",Executables=new[]{"siril.exe"},ImageTypes=new[]{"FITS","TIFF","PNG","JPEG","Camera RAW"},FolderTypes=new[]{"FITS","TIFF","PNG","JPEG","SER","AVI","Camera RAW"},Help="Opens one image directly. For subframes or recordings, starts in the exported folder; convert or load the sequence in Siril."},
   new ExportApplication{Id="dss",Name="DeepSkyStacker (DSS)",Executables=new[]{"DeepSkyStacker.exe"},ImageTypes=new string[0],FolderTypes=new[]{"FITS","TIFF","Camera RAW"},Help="Loads a typed DSS file list of subframes and optional matching calibrations. Separate capture groups get separate lists. Registration and stacking remain your choice."},
   new ExportApplication{Id="gimp",Name="GIMP",Executables=new[]{"gimp.exe","gimp-3.exe","gimp-3.0.exe","gimp-3.2.exe","gimp-2.10.exe"},ImageTypes=new[]{"FITS","TIFF","PNG","JPEG","BMP","GIF"},FolderTypes=new string[0],Help="Opens working images directly. FITS support depends on the installed GIMP FITS plug-in. Camera RAW requires conversion or an external loader."},
   new ExportApplication{Id="photoshop",Name="Photoshop",Executables=new[]{"Photoshop.exe"},ImageTypes=new[]{"TIFF","PNG","JPEG","BMP","GIF"},FolderTypes=new string[0],Help="Opens working images using Photoshop's Windows automation interface. Use TIFF for astronomical images; FITS and XISF need conversion first."},
   new ExportApplication{Id="as4",Name="AutoStakkert! 4 (AS!4)",Executables=new[]{"AutoStakkert.exe","AutoStakkert4.exe","AutoStakkert!4.exe"},ImageTypes=new string[0],FolderTypes=new[]{"SER","AVI","TIFF","PNG","BMP"},Help="Opens AS!4 and the exported input folder. Load SER, uncompressed AVI, or a supported image sequence in AS!4. AVI codecs and image precision depend on AS!4."},
   new ExportApplication{Id="astrowizard",Name="AstroWizard",Executables=new[]{"AstroWizard.exe","AstroWizard-Windows.exe"},ImageTypes=new[]{"FITS","TIFF","XISF"},FolderTypes=new string[0],Help="Opens one working image via AstroWizard's file argument. Supported here: FITS, TIFF and XISF."},
   new ExportApplication{Id="stackingwizard",Name="Stacking Wizard",Executables=new[]{"StackingWizard.exe","StackingWizard-Windows.exe","Stacking Wizard.exe"},ImageTypes=new string[0],FolderTypes=new[]{"FITS","Camera RAW"},Help="Opens Stacking Wizard and the exported input folder. Choose the folder in the app; no verified external folder-import interface is available. Camera RAW and compressed FITS require a recent build."}
  };
  public static readonly string[] FileTypes={"FITS","TIFF","XISF","PNG","JPEG","BMP","GIF","SER","AVI","Camera RAW","Subframe folders"};
  public static ExportApplication Find(string id){return All.FirstOrDefault(a=>a.Id==id);}
  public static string FileType(string path){
   string name=(path??"").ToLowerInvariant();
   if(name.EndsWith(".gz")||name.EndsWith(".fz"))name=name.Substring(0,name.Length-3);
   string ext=System.IO.Path.GetExtension(name);
   if(new[]{".fits",".fit",".fts"}.Contains(ext)||System.IO.Path.GetExtension(path??"").Equals(".fz",StringComparison.OrdinalIgnoreCase))return "FITS";
   if(ext==".tif"||ext==".tiff")return "TIFF";if(ext==".jpg"||ext==".jpeg")return "JPEG";
   if(new[]{".dng",".cr2",".cr3",".nef",".nrw",".arw",".raf",".orf",".rw2",".pef",".srw"}.Contains(ext))return "Camera RAW";
   return ext.Length>0?ext.Substring(1).ToUpperInvariant():"Unknown";
  }
  public static ExternalSelection Selection(IEnumerable<Frame> frames){
   var items=frames.ToList();if(items.Count==0)throw new InvalidOperationException("Select files to export.");
   if(items.Any(f=>f.Rejected||f.Status=="Failed"||CaptureScreening.FileProblem(f)))throw new InvalidOperationException("Select usable files only. Rejected or failed files can still be copied with Export files….");
   if(items.Any(f=>f.Kind=="Light")&&items.Any(f=>f.Kind!="Light"))throw new InvalidOperationException("Select subframes on their own, or select finished images. Matching calibrations can be added in the export dialog.");
   bool subs=items.All(f=>f.Kind=="Light");return new ExternalSelection{Paths=items.Select(f=>f.OriginalName??f.RelativePath).ToList(),Subframes=subs,Folder=subs||items.Any(f=>new[]{"SER","AVI"}.Contains(FileType(f.OriginalName??f.RelativePath)))};
  }
  public static string Problem(ExportApplication app,ExternalSelection selection){
   if(selection.Paths.Count==0)return "Select files to export.";
   var types=selection.Folder?app.FolderTypes:app.ImageTypes;
   if(types.Length==0)return selection.Folder?"This destination opens finished images; select images rather than subframes.":"This destination needs subframes or a recording; select those in Repository.";
   var unsupported=selection.Paths.Select(FileType).Distinct().Where(t=>!types.Contains(t)).ToList();
   if(unsupported.Count>0)return "Unsupported here: "+string.Join(", ",unsupported)+". Choose another destination or save/convert the files first.";
   if(!selection.Folder&&(app.Id=="siril"||app.Id=="astrowizard")&&selection.Paths.Count!=1)return "Select one image for "+app.Name+". Folder handoff is available for Siril subframes.";
   // Restrict direct image opening to the contract verified for uncompressed files.
   if(!selection.Folder&&(app.Id=="astrowizard"||app.Id=="gimp")&&selection.Paths.Any(p=>p.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".fz",StringComparison.OrdinalIgnoreCase)))return "Save an uncompressed FITS copy before opening it in "+app.Name+".";
   return null;
  }
  public static string Default(Settings settings,ExternalSelection selection){
   if(settings.ExportDefaults==null)return null;string id;
   if(selection.Subframes&&settings.ExportDefaults.TryGetValue("Subframe folders",out id)&&Find(id)!=null&&Problem(Find(id),selection)==null)return id;
   var defaults=selection.Paths.Select(p=>{string value;return settings.ExportDefaults.TryGetValue(FileType(p),out value)?value:null;}).Distinct().ToList();
   if(defaults.Count!=1||Find(defaults[0])==null)return null;
   return Problem(Find(defaults[0]),selection)==null?defaults[0]:null;
  }
  public static string Configured(Settings settings,ExportApplication app){
   string path;if(settings.ExternalApplicationPaths!=null&&settings.ExternalApplicationPaths.TryGetValue(app.Id,out path))return path;
   return app.Id=="siril"?settings.SirilExecutable:app.Id=="stackingwizard"?settings.StackingWizardExecutable:null;
  }
  public static void Configure(Settings settings,ExportApplication app,string path){
   if(settings.ExternalApplicationPaths==null)settings.ExternalApplicationPaths=new Dictionary<string,string>();settings.ExternalApplicationPaths[app.Id]=path??"";
   if(app.Id=="siril")settings.SirilExecutable=path;if(app.Id=="stackingwizard")settings.StackingWizardExecutable=path;
  }
  static bool ExecutableName(ExportApplication app,string path){
   string name=System.IO.Path.GetFileName(path);if(app.Executables.Any(n=>n.Equals(name,StringComparison.OrdinalIgnoreCase)))return true;
   if(app.Id=="gimp")return System.Text.RegularExpressions.Regex.IsMatch(name,@"^gimp-\d+(\.\d+)?\.exe$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
   if(app.Id=="astrowizard"||app.Id=="stackingwizard")return name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&name.Replace(" ","").StartsWith(app.Id,StringComparison.OrdinalIgnoreCase)&&name.IndexOf("setup",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("install",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("console",StringComparison.OrdinalIgnoreCase)<0;
   return false;
  }
  public static void ValidateExecutable(ExportApplication app,string path){if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))throw new IOException("Locate the "+app.Name+" application in Export destinations settings.");if(!ExecutableName(app,path))throw new IOException("Choose the "+app.Name+" GUI executable, rather than an installer or command-line tool.");}
  public static string FirstExecutable(ExportApplication app,IEnumerable<string> paths){foreach(string path in paths)try{if(!string.IsNullOrWhiteSpace(path)&&File.Exists(path)&&ExecutableName(app,path))return System.IO.Path.GetFullPath(path);}catch(ArgumentException){}return null;}
  static IEnumerable<string> ExecutablesIn(ExportApplication app,string folder){var files=new List<string>();foreach(string directory in new[]{folder,System.IO.Path.Combine(folder,"bin")}){files.AddRange(app.Executables.Select(n=>System.IO.Path.Combine(directory,n)));try{if(Directory.Exists(directory))files.AddRange(Directory.GetFiles(directory,"*.exe").Where(p=>ExecutableName(app,p)));}catch(IOException){}catch(UnauthorizedAccessException){}}return files;}
  static string[] Directories(string folder){try{return Directory.Exists(folder)?Directory.GetDirectories(folder):new string[0];}catch(IOException){return new string[0];}catch(UnauthorizedAccessException){return new string[0];}}
  static bool RelatedFolder(ExportApplication app,string path){string name=(path??"").Replace('\\','/').Split('/').Last().Replace(" ","").Replace("!","").Replace("-","").ToLowerInvariant();return name.Contains(app.Id)||app.Id=="dss"&&name.Contains("deepskystacker")||app.Id=="as4"&&name.Contains("autostakkert")||app.Id=="photoshop"&&name.Contains("adobe");}
  public static string Detect(ExportApplication app){
   var candidates=new List<string>();
   foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{using(var root=RegistryKey.OpenBaseKey(hive,view)){
    foreach(string name in app.Executables)using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+name)){if(key!=null)candidates.Add(Convert.ToString(key.GetValue(null)).Trim('"'));}
    using(var uninstall=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall")){if(uninstall!=null)foreach(string name in uninstall.GetSubKeyNames())using(var key=uninstall.OpenSubKey(name)){if(key==null)continue;string display=Convert.ToString(key.GetValue("DisplayName"));if(!RelatedFolder(app,display))continue;string location=Convert.ToString(key.GetValue("InstallLocation"));if(Directory.Exists(location))candidates.AddRange(ExecutablesIn(app,location));}}
   }}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}
   string registered=FirstExecutable(app,candidates);if(registered!=null)return registered;
   foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(';').Where(p=>!string.IsNullOrWhiteSpace(p)))try{candidates.AddRange(ExecutablesIn(app,folder.Trim('"')));}catch(ArgumentException){}
   var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),AppDomain.CurrentDomain.BaseDirectory};
   foreach(string root in roots.Where(Directory.Exists).Distinct()){
    candidates.AddRange(ExecutablesIn(app,root));foreach(string folder in Directories(root).Where(p=>RelatedFolder(app,p))){candidates.AddRange(ExecutablesIn(app,folder));foreach(string child in Directories(folder))candidates.AddRange(ExecutablesIn(app,child));}
   }
   return FirstExecutable(app,candidates);
  }
  public static string Resolve(Settings settings,ExportApplication app){string configured=Configured(settings,app);if(!string.IsNullOrWhiteSpace(configured)){try{ValidateExecutable(app,configured);return System.IO.Path.GetFullPath(configured);}catch(IOException){return null;}}return Detect(app);}
  static string Arguments(IEnumerable<string> paths){return string.Join(" ",paths.Select(SirilHandoff.QuoteArgument));}
  public static ProcessStartInfo LaunchInfo(ExportApplication app,string executable,IEnumerable<string> files,string folder=null,string bridge=null){
   ValidateExecutable(app,executable);var paths=files.Select(System.IO.Path.GetFullPath).ToList();foreach(string path in paths)if(!File.Exists(path))throw new IOException("Exported input is missing: "+path);
   if(folder!=null&&!Directory.Exists(folder))throw new IOException("Exported folder is missing.");
   if(folder==null){string problem=Problem(app,new ExternalSelection{Paths=paths});if(problem!=null)throw new IOException(problem);}
   else if(app.FolderTypes.Length==0)throw new IOException(app.Name+" does not support folder handoff.");
   if(folder!=null&&app.Id=="dss"&&(paths.Count!=1||!File.ReadLines(paths[0]).Take(2).SequenceEqual(new[]{"DSS file list","CHECKED\tTYPE\tFILE"})))throw new IOException("Choose one generated DSS file list.");
   string args="";
   if(folder!=null){if(app.Id=="siril")args="--directory "+SirilHandoff.QuoteArgument(System.IO.Path.GetFullPath(folder));else if(app.Id=="dss")args=Arguments(paths.Take(1));}
   else if(app.Id=="pixinsight"){if(!File.Exists(bridge))throw new IOException("PixInsight handoff script is missing.");args=SirilHandoff.QuoteArgument("--run="+bridge);}
   else if(app.Id=="siril"||app.Id=="astrowizard")args=Arguments(paths.Take(1));
   else if(app.Id=="gimp")args=Arguments(paths);
   if(args.Length>30000)throw new IOException("This selection exceeds Windows' application argument limit. Select fewer images; your verified copies remain available.");
   return new ProcessStartInfo(System.IO.Path.GetFullPath(executable),args){UseShellExecute=false,WorkingDirectory=folder==null?System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(executable)):System.IO.Path.GetFullPath(folder)};
  }
  public static string PixInsightScript(IEnumerable<string> files){return "// Generated by AstroArchive; paths are data.\nvar paths = "+Util.Serialize(files.Select(System.IO.Path.GetFullPath).ToArray()).Replace("\u2028","\\u2028").Replace("\u2029","\\u2029")+";\nfor (var i = 0; i < paths.length; ++i) {\n var windows = ImageWindow.open(paths[i]);\n for (var j = 0; j < windows.length; ++j) windows[j].show();\n}\n";}
  public static void Launch(ExportApplication app,string executable,IEnumerable<string> files,string folder,CancellationToken ct){
   var paths=files.ToList();ct.ThrowIfCancellationRequested();ValidateExecutable(app,executable);if(folder==null){string problem=Problem(app,new ExternalSelection{Paths=paths});if(problem!=null)throw new IOException(problem);foreach(string path in paths)if(!File.Exists(path))throw new IOException("Exported input is missing: "+path);}
   if(app.Id=="photoshop"){
    // COM is Adobe's supported Windows interface. Check the selected installation
    // so a configured path cannot silently dispatch to a different Photoshop.
    Type type=Type.GetTypeFromProgID("Photoshop.Application");if(type==null)throw new IOException("This Photoshop installation has no Windows automation registration. Run Photoshop once or repair its installation.");
    object instance=null;try{instance=Activator.CreateInstance(type);string installed=Convert.ToString(type.InvokeMember("Path",BindingFlags.GetProperty,null,instance,null));if(!System.IO.Path.GetFullPath(installed).TrimEnd('\\').Equals(System.IO.Path.GetDirectoryName(executable).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new IOException("Windows automation points to a different Photoshop installation. Use that installation in Export destinations settings.");foreach(string path in paths){ct.ThrowIfCancellationRequested();if(!File.Exists(path))throw new IOException("Exported input is missing: "+path);type.InvokeMember("Open",BindingFlags.InvokeMethod,null,instance,new object[]{path});}}finally{if(instance!=null&&Marshal.IsComObject(instance))Marshal.FinalReleaseComObject(instance);}return;
   }
   string script=null;if(app.Id=="pixinsight"&&folder==null){string directory=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","handoffs");Directory.CreateDirectory(directory);script=Exporter.WriteMetadataText(System.IO.Path.Combine(directory,Guid.NewGuid().ToString("N")+".js"),PixInsightScript(paths),ct);if(script.Contains(","))throw new IOException("PixInsight cannot use a script location containing a comma. Open the retained working copies manually.");}
   ct.ThrowIfCancellationRequested();using(var process=Process.Start(LaunchInfo(app,executable,paths,folder,script))){if(process==null)throw new IOException(app.Name+" did not start.");}
  }
  public static List<Frame> FolderInputs(Repository repo,List<Frame> selected,bool calibrations,bool unknown){
   var inputs=selected.GroupBy(f=>f.Hash).Select(g=>g.First()).ToList();if(calibrations)inputs.AddRange(Exporter.AvailableCalibrations(inputs,Exporter.ExistingCalibrations(repo,repo.All()),false,unknown));return inputs.GroupBy(f=>f.Hash).Select(g=>g.First()).ToList();
  }
  static string DssType(Frame frame){string kind=frame.Kind.ToLowerInvariant().Replace("master ","");return kind=="light"?"light":kind=="dark flat"?"darkflat":kind=="dark"?"dark":kind=="flat"?"flat":kind=="bias"?"offset":null;}
  public static List<DssJob> WriteDssJobs(string folder,List<Frame> selected,List<ExportedFile> exported,bool calibrations,bool unknown,CancellationToken ct){
   var jobs=new List<DssJob>();var cals=exported.Select(f=>f.Frame).Where(f=>Assets.IsCalibration(f.Kind)).ToList();int index=0;
   foreach(var planned in Exporter.StackingGroups(selected,cals,false,unknown,calibrations)){var group=planned.Inputs;
    ct.ThrowIfCancellationRequested();var inputs=group.ToList();inputs.AddRange(planned.Calibrations);
    var rows=new List<string>{"DSS file list","CHECKED\tTYPE\tFILE"};foreach(var frame in inputs.GroupBy(f=>f.Hash).Select(g=>g.First())){var file=exported.FirstOrDefault(f=>f.Frame.Hash==frame.Hash);string type=DssType(frame);if(file==null||type==null)throw new IOException("DSS file list cannot map an exported input: "+frame.OriginalName);if(file.Path.IndexOfAny(new[]{'\r','\n','\t'})>=0)throw new IOException("DSS cannot represent a filename containing a tab or newline.");rows.Add("1\t"+type+"\t"+file.Path);}
    string name=(++index).ToString("00")+"_"+Util.Safe(group.First().TargetLabel)+"_"+Util.Safe(group.First().Filter);string path=Exporter.WriteMetadataText(System.IO.Path.Combine(folder,name+".txt"),string.Join("\r\n",rows)+"\r\n",ct);jobs.Add(new DssJob{Name=name+" · "+group.Count()+" subframes",Path=path});
   }return jobs;
  }
 }
}
