// .NET Framework 4.8 / Windows 10+ x64. Registration is per user, with no elevation.
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
namespace AstroArchive.Installation {
 public static class WindowsIntegration {
  public const string RegistryPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\AstroArchive";
  public static string DefaultRoot{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","AstroArchive");}}
  public static string RegisteredRoot(){foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{using(var user=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,view))using(var key=user.OpenSubKey(RegistryPath)){string path=key==null?null:key.GetValue("InstallLocation") as string;if(!string.IsNullOrWhiteSpace(path))return path;}}catch{}return DefaultRoot;}
  public static void CheckEnvironment(){if(Environment.OSVersion.Platform!=PlatformID.Win32NT||!Environment.Is64BitOperatingSystem||Environment.OSVersion.Version.Major<10)throw new IOException("This installer requires Windows 10 or 11, 64-bit.");using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var key=machine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")){if(key==null||Convert.ToInt32(key.GetValue("Release",0))<528040)throw new IOException("Install .NET Framework 4.8 or later before using AstroArchive.");}}
  public static string Quote(string text){if(text.IndexOf('"')>=0)throw new IOException("Quotes are not supported in this path.");return "\""+text+"\"";}
  public static int UpdateWaitProcess(string root,int applicationPid,string applicationPath){
   if(!Within(applicationPath,root))return applicationPid;
   var record=InstallCore.Read(root);if(record==null)return applicationPid;
   string launcher=InstallCore.Managed(root,record.ActiveDirectory+"\\Start.exe");
   foreach(var process in Process.GetProcessesByName("Start")){
    try{if(string.Equals(process.MainModule.FileName,launcher,StringComparison.OrdinalIgnoreCase))return process.Id;}
    catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
    finally{process.Dispose();}
   }
   return applicationPid;
  }
  public static void WaitForLauncher(string root){
   using(var mutex=new Mutex(false,InstallCore.ApplicationMutexName(root))){bool held=false;
    try{try{held=mutex.WaitOne(60000);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("AstroArchive's launcher is still running. Close it and run setup again.");}
    finally{if(held)mutex.ReleaseMutex();}
   }
  }
  public static void EnsureClosed(string root){EnsureNoOtherApplications(root,0);}
  public static void EnsureNoOtherApplications(string root,int allowedPid){foreach(var p in Process.GetProcessesByName("AstroArchive")){try{if(p.Id==allowedPid)continue;string path=p.MainModule.FileName;if(Within(path,root))throw new IOException("Close the other AstroArchive window before installing or updating.");}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}finally{p.Dispose();}}}
  static bool Within(string path,string root){return Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}
  static string Menu{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"AstroArchive");}}
  static string Desktop{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"AstroArchive.lnk");}}
  public static string[] ShortcutPaths(string root){return new[]{Desktop,Path.Combine(Menu,"AstroArchive.lnk"),Path.Combine(Menu,"Uninstall AstroArchive.lnk")};}
  static object Shortcut(string path){object shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));try{return shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});}finally{Marshal.FinalReleaseComObject(shell);}}
  static string Target(string path){object shortcut=Shortcut(path);try{return Convert.ToString(shortcut.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null));}finally{Marshal.FinalReleaseComObject(shortcut);}}
  static void Link(string path,string target,string arguments,string icon,string root){if(File.Exists(path)&&!Within(Target(path),root))throw new IOException("An unrelated shortcut already uses this name: "+path);object shortcut=Shortcut(path);try{var t=shortcut.GetType();t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{target});t.InvokeMember("Arguments",BindingFlags.SetProperty,null,shortcut,new object[]{arguments});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{Path.GetDirectoryName(target)});t.InvokeMember("IconLocation",BindingFlags.SetProperty,null,shortcut,new object[]{icon+",0"});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);}finally{Marshal.FinalReleaseComObject(shortcut);}}
  static void RemoveLink(string path,string root){if(File.Exists(path)&&Within(Target(path),root))File.Delete(path);}
  public static void Register(string root,InstallRecord record){string app=Path.Combine(root,record.ActiveDirectory,"AstroArchive.exe"),launcher=Path.Combine(root,record.ActiveDirectory,"Start.exe"),uninstall=Path.Combine(root,"Uninstall.exe");Directory.CreateDirectory(Menu);Link(Path.Combine(Menu,"AstroArchive.lnk"),launcher,"",app,root);Link(Path.Combine(Menu,"Uninstall AstroArchive.lnk"),uninstall,"--uninstall --root "+Quote(root),app,root);Link(Desktop,launcher,"",app,root);RemoveLink(Path.Combine(Menu,"Update settings.lnk"),root);record.Shortcuts=new[]{Desktop,Path.Combine(Menu,"AstroArchive.lnk"),Path.Combine(Menu,"Uninstall AstroArchive.lnk")};
   using(var user=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))using(var key=user.CreateSubKey(RegistryPath)){key.SetValue("DisplayName","AstroArchive");key.SetValue("DisplayVersion",record.Version);key.SetValue("PackageVersion",record.PackageVersion??record.Version);key.SetValue("Publisher","Ari J. Guest");key.SetValue("InstallLocation",root);key.SetValue("DisplayIcon",app+",0");key.SetValue("UninstallString",Quote(uninstall)+" --uninstall --root "+Quote(root));key.SetValue("QuietUninstallString",Quote(uninstall)+" --uninstall --silent --root "+Quote(root));key.SetValue("NoModify",1);key.SetValue("NoRepair",1);key.SetValue("EstimatedSize",(int)(new FileInfo(uninstall).Length/1024+record.Files.Sum(f=>File.Exists(InstallCore.Managed(root,f))?new FileInfo(InstallCore.Managed(root,f)).Length/1024:0)));}
  }
  public static void Unregister(string root,InstallRecord record){foreach(string path in new[]{Desktop,Path.Combine(Menu,"AstroArchive.lnk"),Path.Combine(Menu,"Uninstall AstroArchive.lnk"),Path.Combine(Menu,"Update settings.lnk")})RemoveLink(path,root);if(Directory.Exists(Menu)&&!Directory.EnumerateFileSystemEntries(Menu).Any())Directory.Delete(Menu);foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})using(var user=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,view)){using(var key=user.OpenSubKey(RegistryPath)){if(key==null||!string.Equals(Convert.ToString(key.GetValue("InstallLocation")),root,StringComparison.OrdinalIgnoreCase))continue;}user.DeleteSubKeyTree(RegistryPath,false);}}
 }
}
