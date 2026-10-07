// C# 5 / .NET Framework 4.8. Versioned app folders and atomic installation records.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace AstroArchive.Installation {
 public class InstallRecord {
  public string Product="AstroArchive",Version,PackageVersion,ActiveDirectory;
  public string[] Files=new string[0],Shortcuts=new string[0];
 }
 public class PayloadFile {public string Name,Hash;public byte[] Bytes;}
 public class InstallPackage {public string Version,PackageVersion;public int Revision;public List<PayloadFile> Files=new List<PayloadFile>();public string DirectoryName{get{return "app-"+Version+"-r"+Revision;}}}
 public sealed class InstallCore {
  public Action<string> Progress=s=>{};
  public Action<string> TestHook=s=>{};
  void Report(string text){try{Progress(text);}catch{}}
  public Action<string,InstallRecord> Register=(r,m)=>{};
  public Action<string,InstallRecord> Unregister=(r,m)=>{};
  public Action<string> EnsureClosed=r=>{};
  public Func<string,string[]> ShortcutPaths=r=>new string[0];
  static readonly HashSet<string> names=new HashSet<string>(new[]{"AstroArchive.exe","Start.exe","Quick_Start.txt","Validation.txt","Catalogue_Notice.md","OpenNGC_README.md","Release_Notes.txt"},StringComparer.OrdinalIgnoreCase);
  public static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
  public static string HashFile(string path){using(var h=SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
  public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=8*1024*1024};}
  public static Version Parse(string text){if(!Regex.IsMatch(text??"",@"^\d+\.\d+\.\d+(?:\.\d+)?$"))throw new IOException("Invalid installation version.");return new Version(text.Split('.').Length==3?text+".0":text);}
  public static string Root(string path){if(string.IsNullOrWhiteSpace(path))throw new IOException("Choose an installation folder.");string root=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);if(root.Length==0||root.Equals(Path.GetPathRoot(Path.GetFullPath(path)).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))throw new IOException("Choose an application folder, not a drive root.");NoLinks(root);return root;}
  public static void NoLinks(string path){string p=Path.GetFullPath(path);while(p!=null){if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked installation paths are not supported: "+p);p=Path.GetDirectoryName(p);}}
  public static string Managed(string root,string relative){string rel=(relative??"").Replace('\\','/');string[] parts=rel.Split('/');if(parts.Length!=2||!Regex.IsMatch(parts[0],@"^app-\d+\.\d+\.\d+(?:-r[1-9]\d*)?$")||!names.Contains(parts[1]))throw new IOException("Unexpected managed application path: "+relative);string p=Path.Combine(root,parts[0],parts[1]);NoLinks(p);return p;}
  public static InstallRecord Read(string root){string p=Path.Combine(root,"install.json");if(!File.Exists(p))return null;NoLinks(p);if(new FileInfo(p).Length>1024*1024)throw new IOException("Installation record is too large.");var record=Json().Deserialize<InstallRecord>(File.ReadAllText(p));if(record==null||record.Product!="AstroArchive"||record.Files==null)throw new IOException("This is not an AstroArchive installation.");Parse(record.Version);if(record.PackageVersion!=null)Parse(record.PackageVersion);foreach(string file in record.Files)Managed(root,file);string active=record.ActiveDirectory??"app-"+record.Version;if(!Regex.IsMatch(active,@"^app-\d+\.\d+\.\d+(?:-r[1-9]\d*)?$"))throw new IOException("Invalid active application directory.");record.ActiveDirectory=active;if(!record.Files.Any(f=>f.Replace('\\','/')==active+"/AstroArchive.exe"))throw new IOException("Installation record has no active application.");return record;}
  static string Relative(string directory,string name){return directory+"\\"+name;}
  static void WriteDurable(string path,byte[] bytes){using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}}
  static void RemoveFile(string path){if(File.Exists(path))File.Delete(path);}
  static void EmptyDirectory(string path){if(Directory.Exists(path)&&!Directory.EnumerateFileSystemEntries(path).Any())Directory.Delete(path);}
  static void RemoveStage(string path){if(path!=null&&Directory.Exists(path)){NoLinks(path);foreach(string file in Directory.EnumerateFiles(path)){NoLinks(file);File.Delete(file);}EmptyDirectory(path);}}
  static void Preflight(string root,InstallRecord old){if(old==null)return;foreach(string rel in old.Files){string path=Managed(root,rel);if(File.Exists(path))using(File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None)){} }}
  public InstallRecord Install(string path,InstallPackage package,byte[] setup,bool updateOnly=false){
   string root=Root(path);Parse(package.Version);Parse(package.PackageVersion);if(Parse(package.PackageVersion).Major!=Parse(package.Version).Major||Parse(package.PackageVersion).Minor!=Parse(package.Version).Minor||Parse(package.PackageVersion).Build!=Parse(package.Version).Build||Parse(package.PackageVersion).Revision!=package.Revision)throw new IOException("Installer and application versions do not agree.");if(package.Revision<1||package.DirectoryName!="app-"+package.Version+"-r"+package.Revision)throw new IOException("Invalid installer package.");
   if(!package.Files.Any(f=>f.Name=="AstroArchive.exe")||!package.Files.Any(f=>f.Name=="Start.exe")||package.Files.Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=package.Files.Count)throw new IOException("Incomplete installer payload.");
   foreach(var f in package.Files)if(!names.Contains(f.Name)||f.Bytes==null||Hash(f.Bytes)!=f.Hash)throw new IOException("Installer payload verification failed: "+f.Name);
   Directory.CreateDirectory(root);using(var mutex=new Mutex(false,"Local\\AstroArchive.Install."+Hash(System.Text.Encoding.UTF8.GetBytes(root.ToLowerInvariant())))){
    bool held=false;try{try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("Another installation is already in progress.");
     var old=Read(root);if(updateOnly&&old==null)throw new IOException("An update requires an existing AstroArchive installation.");if(old==null&&Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("Choose an empty installation folder. Existing unrelated files are retained.");
     if(old!=null&&(Parse(old.Version)>Parse(package.Version)||Parse(old.PackageVersion??old.Version)>Parse(package.PackageVersion)))throw new IOException("Downgrades are blocked. Use the same or a newer installer.");
     EnsureClosed(root);Preflight(root,old);NoLinks(root);string id=Guid.NewGuid().ToString("N"),stage=Path.Combine(root,".stage-"+id),target=Path.Combine(root,package.DirectoryName),backup=Path.Combine(root,".backup-"+id),uninstall=Path.Combine(root,"Uninstall.exe"),uninstallBackup=Path.Combine(root,".uninstall-backup-"+id),uninstallTemp=Path.Combine(root,".uninstall-new-"+id),recordPath=Path.Combine(root,"install.json"),recordTemp=Path.Combine(root,".record-new-"+id),recordBackup=Path.Combine(root,".record-backup-"+id);
     bool movedTarget=false,publishedTarget=false,movedUninstaller=false,publishedUninstaller=false,publishedRecord=false,movedRecord=false,registrationStarted=false;
     var current=new InstallRecord{Version=package.Version,PackageVersion=package.PackageVersion,ActiveDirectory=package.DirectoryName,Files=(old==null?new string[0]:old.Files).Concat(package.Files.Select(f=>Relative(package.DirectoryName,f.Name))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),Shortcuts=ShortcutPaths(root)};
     try{Directory.CreateDirectory(stage);foreach(var f in package.Files){Report("Verifying "+f.Name);string dest=Path.Combine(stage,f.Name);WriteDurable(dest,f.Bytes);if(HashFile(dest)!=f.Hash)throw new IOException("Written payload failed verification: "+f.Name);}WriteDurable(uninstallTemp,setup);if(HashFile(uninstallTemp)!=Hash(setup))throw new IOException("Uninstaller failed verification.");TestHook("staged");EnsureClosed(root);Preflight(root,old);
      if(Directory.Exists(target)){NoLinks(target);if(old==null||!old.Files.Any(f=>f.Replace('\\','/').StartsWith(package.DirectoryName+"/",StringComparison.OrdinalIgnoreCase)))throw new IOException("Existing application directory is not managed by this installation.");if(Directory.EnumerateDirectories(target).Any()||Directory.EnumerateFiles(target).Any(f=>!package.Files.Any(x=>string.Equals(Path.GetFileName(f),x.Name,StringComparison.OrdinalIgnoreCase))))throw new IOException("The application directory contains additional files. Retain these elsewhere before repairing this package.");Directory.Move(target,backup);movedTarget=true;}
      Report(old==null?"Installing application":"Updating application");Directory.Move(stage,target);publishedTarget=true;if(File.Exists(uninstall)){NoLinks(uninstall);File.Move(uninstall,uninstallBackup);movedUninstaller=true;}File.Move(uninstallTemp,uninstall);publishedUninstaller=true;
      TestHook("before-record");WriteDurable(recordTemp,System.Text.Encoding.UTF8.GetBytes(Json().Serialize(current)));if(File.Exists(recordPath)){File.Replace(recordTemp,recordPath,recordBackup);movedRecord=true;}else File.Move(recordTemp,recordPath);publishedRecord=true;
      registrationStarted=true;Register(root,current);TestHook("registered");
      try{RemoveFile(recordBackup);RemoveFile(uninstallBackup);if(movedTarget)RemoveStage(backup);}catch(Exception e){Report("Installed; backup cleanup deferred: "+e.Message);}Report("Installation complete");return current;
     }catch{if(publishedRecord)RemoveFile(recordPath);if(movedRecord&&File.Exists(recordBackup))File.Move(recordBackup,recordPath);if(publishedUninstaller)RemoveFile(uninstall);if(movedUninstaller&&File.Exists(uninstallBackup))File.Move(uninstallBackup,uninstall);if(publishedTarget)RemoveStage(target);if(movedTarget&&Directory.Exists(backup))Directory.Move(backup,target);if(registrationStarted){try{if(old==null)Unregister(root,current);else Register(root,old);}catch{}}throw;
     }finally{try{RemoveStage(stage);RemoveFile(uninstallTemp);RemoveFile(recordTemp);}catch(Exception e){Report("Temporary file cleanup deferred: "+e.Message);}}
    }finally{if(held)mutex.ReleaseMutex();}
   }
  }
  public void Uninstall(string path){string root=Root(path);using(var mutex=new Mutex(false,"Local\\AstroArchive.Install."+Hash(System.Text.Encoding.UTF8.GetBytes(root.ToLowerInvariant())))){bool held=false;try{try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("Another installation is in progress.");var record=Read(root);if(record==null)throw new IOException("No AstroArchive installation was found.");EnsureClosed(root);Preflight(root,record);foreach(string rel in record.Files){string file=Managed(root,rel);Report("Removing "+Path.GetFileName(file));RemoveFile(file);EmptyDirectory(Path.GetDirectoryName(file));}Unregister(root,record);RemoveFile(Path.Combine(root,"Uninstall.exe"));RemoveFile(Path.Combine(root,"install.json"));EmptyDirectory(root);Report("Application removed. Repositories and settings retained.");}finally{if(held)mutex.ReleaseMutex();}}}
 }
}
