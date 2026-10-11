// C# 5 / .NET Framework 4.8. Restores the newest installed version Windows will start.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AstroArchive.Installation {
 public sealed class LaunchRecovery {
  // Blocked: the active version could not start. RolledBack: an older installed version is now active.
  public bool Blocked,RolledBack;
  public string BlockedPackage,RestoredPackage,Reason;
  public InstallRecord Record;
 }
 public sealed class LaunchBlockRecord {
  public int Schema=1;
  public string[] BlockedPackages=new string[0];
  public string BlockedPackage,RestoredPackage,Reason,RecordedUtc;
  public bool NoticePending;
 }
 public static class LaunchSafety {
  public static readonly string[] Executables={"Start.exe","AstroArchive.exe"};
  const string MarkerName="launch-blocked.json";
  // Smart App Control, App Control for Business, signature or Defender rejection.
  public static bool IsPolicyCode(int code){return code==1260||code==4551||code==577||code==225||code==226;}

  public static string Describe(string executable,int code){
   string text=code==2||code==3?"is missing":"could not start (Windows error "+code+": "+new System.ComponentModel.Win32Exception(code).Message+")";
   return (IsPolicyCode(code)?"Windows blocked ":"")+executable+" "+text;
  }

  // Returns null when both executables in the directory can be started, otherwise the first failure.
  public static string Failure(string root,string directory,Func<string,int> probe){
   foreach(string name in Executables){
    string path=InstallCore.Managed(root,directory+"\\"+name);
    if(!File.Exists(path))return name+" is missing";
    int code=0;
    for(int attempt=0;attempt<3;attempt++){
     code=probe(path);
     if(code==0||IsPolicyCode(code))break;
     Thread.Sleep(400); // Scanners can briefly hold a newly written executable.
    }
    if(code!=0)return Describe(name,code);
   }
   return null;
  }

  // Version folder names are app-X.Y.Z (legacy) or app-X.Y.Z-rN.
  static Version DirectoryVersion(string directory){var match=Regex.Match(directory??"",@"^app-(\d+\.\d+\.\d+)(?:-r([1-9]\d*))?$");if(!match.Success)throw new IOException("Invalid application directory: "+directory);return InstallCore.Parse(match.Groups[1].Value+"."+(match.Groups[2].Success?match.Groups[2].Value:"0"));}
  static string ApplicationVersion(string directory){return Regex.Match(directory,@"^app-(\d+\.\d+\.\d+)").Groups[1].Value;}
  static string PackageVersion(string directory){var match=Regex.Match(directory,@"^app-(\d+\.\d+\.\d+)-r([1-9]\d*)$");return match.Success?match.Groups[1].Value+"."+match.Groups[2].Value:null;}
  static string Package(InstallRecord record){return record.PackageVersion??record.Version;}
  static string DirectoryOf(string relative){return relative.Replace('\\','/').Split('/')[0];}

  // Checks the active version and, if Windows will not start it, activates the newest older
  // installed version that starts. Earlier versions stay on disk after updates, so no download is needed.
  public static LaunchRecovery Recover(string root,Func<string,int> probe,Action<string,InstallRecord> register){
   root=InstallCore.Root(root);
   using(var mutex=new Mutex(false,"Local\\AstroArchive.Install."+InstallCore.Hash(Encoding.UTF8.GetBytes(root.ToLowerInvariant())))){
    bool held=false;
    try{
     try{held=mutex.WaitOne(10000);}catch(AbandonedMutexException){held=true;}
     if(!held)throw new IOException("Another installation is in progress.");
     var record=InstallCore.Read(root);if(record==null)throw new IOException("No AstroArchive installation was found.");
     string failure=Failure(root,record.ActiveDirectory,probe);
     if(failure==null){ClearBlock(root,Package(record));return new LaunchRecovery{Record=record};}
     var result=new LaunchRecovery{Blocked=true,BlockedPackage=Package(record),Reason=failure,Record=record};
     Version active=DirectoryVersion(record.ActiveDirectory);
     var candidates=record.Files.Select(DirectoryOf).Distinct(StringComparer.OrdinalIgnoreCase)
      .Where(d=>!d.Equals(record.ActiveDirectory,StringComparison.OrdinalIgnoreCase)&&DirectoryVersion(d)<active)
      .Where(d=>Executables.All(name=>record.Files.Any(f=>f.Replace('\\','/').Equals(d+"/"+name,StringComparison.OrdinalIgnoreCase))))
      .OrderByDescending(DirectoryVersion).ToList();
     foreach(string directory in candidates){
      if(Failure(root,directory,probe)!=null)continue;
      var restored=new InstallRecord{Product=record.Product,Version=ApplicationVersion(directory),PackageVersion=PackageVersion(directory),ActiveDirectory=directory,Files=record.Files,Shortcuts=record.Shortcuts};
      WriteRecord(root,restored);
      try{register(root,restored);}
      catch{try{WriteRecord(root,record);register(root,record);}catch{}throw;}
      result.RolledBack=true;result.RestoredPackage=Package(restored);result.Record=InstallCore.Read(root);
      break;
     }
     RecordBlock(root,result);
     return result;
    }finally{if(held)mutex.ReleaseMutex();}
   }
  }

  public static void WriteRecord(string root,InstallRecord record){
   string path=Path.Combine(root,"install.json"),temp=Path.Combine(root,".record-new-"+Guid.NewGuid().ToString("N"));InstallCore.NoLinks(path);
   try{
    byte[] bytes=Encoding.UTF8.GetBytes(InstallCore.Json().Serialize(record));
    using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }

  static string MarkerPath(string root){string path=Path.Combine(InstallCore.Root(root),MarkerName);InstallCore.NoLinks(path);return path;}
  public static LaunchBlockRecord ReadBlock(string root){
   try{string path=MarkerPath(root);if(!File.Exists(path)||new FileInfo(path).Length>64*1024)return null;var block=InstallCore.Json().Deserialize<LaunchBlockRecord>(File.ReadAllText(path));return block==null||block.Schema!=1||block.BlockedPackages==null?null:block;}
   catch(Exception e){if(e is IOException||e is ArgumentException||e is InvalidOperationException||e is UnauthorizedAccessException)return null;throw;}
  }
  static void WriteBlock(string root,LaunchBlockRecord block){
   string path=MarkerPath(root),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllText(temp,InstallCore.Json().Serialize(block),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
  static void RecordBlock(string root,LaunchRecovery result){
   var block=ReadBlock(root)??new LaunchBlockRecord();
   block.BlockedPackages=block.BlockedPackages.Concat(new[]{result.BlockedPackage}).Distinct().ToArray();
   block.BlockedPackage=result.BlockedPackage;block.RestoredPackage=result.RestoredPackage;block.Reason=result.Reason;block.RecordedUtc=DateTime.UtcNow.ToString("o");block.NoticePending=result.RolledBack;
   WriteBlock(root,block);
  }
  // A package that later starts (for example after signing or reputation changes) is offered again,
  // and a rollback notice for a different package is no longer relevant.
  static void ClearBlock(string root,string package){
   var block=ReadBlock(root);if(block==null)return;
   bool stale=block.NoticePending&&block.RestoredPackage!=package;
   if(!block.BlockedPackages.Contains(package)&&!stale)return;
   block.BlockedPackages=block.BlockedPackages.Where(p=>p!=package).ToArray();if(stale)block.NoticePending=false;
   if(block.BlockedPackages.Length==0&&!block.NoticePending)File.Delete(MarkerPath(root));else WriteBlock(root,block);
  }
  public static bool IsBlocked(string root,string package){var block=root==null?null:ReadBlock(root);return block!=null&&block.BlockedPackages.Contains(package);}
  // Returns the pending rollback notice once, for the restored version that is now running.
  public static LaunchBlockRecord ConsumeNotice(string root,string runningPackage){
   var block=ReadBlock(root);if(block==null||!block.NoticePending||block.RestoredPackage!=runningPackage)return null;
   block.NoticePending=false;WriteBlock(root,block);return block;
  }

  // Creates the process suspended and terminates it before any of its code runs. Windows
  // Code Integrity (Smart App Control and App Control policies) is enforced at creation,
  // so this reports the same block a real launch would, without opening a window.
  public static int NativeProbe(string path){
   var startup=new StartupInfo{cb=Marshal.SizeOf(typeof(StartupInfo))};ProcessInformation process;
   var command=new StringBuilder("\""+path+"\" --launch-probe");
   if(!CreateProcessW(path,command,IntPtr.Zero,IntPtr.Zero,false,CreateSuspended|CreateNoWindow,IntPtr.Zero,Path.GetDirectoryName(path),ref startup,out process))return Marshal.GetLastWin32Error();
   try{TerminateProcess(process.hProcess,0);WaitForSingleObject(process.hProcess,5000);}
   finally{CloseHandle(process.hThread);CloseHandle(process.hProcess);}
   return 0;
  }
  const uint CreateSuspended=0x00000004,CreateNoWindow=0x08000000;
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
  struct StartupInfo{public int cb;public string lpReserved,lpDesktop,lpTitle;public int dwX,dwY,dwXSize,dwYSize,dwXCountChars,dwYCountChars,dwFillAttribute,dwFlags;public short wShowWindow,cbReserved2;public IntPtr lpReserved2,hStdInput,hStdOutput,hStdError;}
  [StructLayout(LayoutKind.Sequential)]
  struct ProcessInformation{public IntPtr hProcess,hThread;public int dwProcessId,dwThreadId;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
  static extern bool CreateProcessW(string application,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inheritHandles,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInformation information);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateProcess(IntPtr process,uint exitCode);
  [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool CloseHandle(IntPtr handle);
 }
}
