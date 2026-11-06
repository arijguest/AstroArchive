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

  public static string EntryPoint(InstallRecord record){return record.DirectLaunch?"AstroArchive.exe":"Start.exe";}

  // A launcher-only reputation rejection can use the application if Windows
  // independently allows that exact application binary.
  static string CheckVersion(string root,string directory,Func<string,int> probe,Action<int> delay,bool retryPolicy,out bool direct){
   delay=delay??Thread.Sleep;
   direct=false;
   string launcherFailure=null;
   foreach(string name in Executables){
    string path=InstallCore.Managed(root,directory+"\\"+name);
    if(!File.Exists(path))return name+" is missing";
    int code=0;
    for(int attempt=0;attempt<5;attempt++){
     code=probe(path);
     if(code==0)break;
     // A newly extracted file can be refused while reputation/scanning settles.
     // Recheck the same bytes through Windows; persistent blocks still roll back.
     if(code==4551&&retryPolicy&&launcherFailure==null){if(attempt==4)break;delay(new[]{500,1500,3000,5000}[attempt]);continue;}
     if(IsPolicyCode(code)||attempt>=2)break;
     delay(400);
    }
    if(code==4551&&name=="Start.exe"){launcherFailure=Describe(name,code);continue;}
    if(code!=0)return (launcherFailure==null?"":launcherFailure+". ")+Describe(name,code);
   }
   direct=launcherFailure!=null;
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
  public static LaunchRecovery Recover(string root,Func<string,int> probe,Action<string,InstallRecord> register,Action<int> delay=null){
   root=InstallCore.Root(root);
   using(var mutex=new Mutex(false,"Local\\AstroArchive.Install."+InstallCore.Hash(Encoding.UTF8.GetBytes(root.ToLowerInvariant())))){
    bool held=false;
    try{
     try{held=mutex.WaitOne(10000);}catch(AbandonedMutexException){held=true;}
     if(!held)throw new IOException("Another installation is in progress.");
     var record=InstallCore.Read(root);if(record==null)throw new IOException("No AstroArchive installation was found.");
     bool direct;
     string failure=CheckVersion(root,record.ActiveDirectory,probe,delay,true,out direct);
     if(failure==null){
      if(record.DirectLaunch!=direct){
       var next=new InstallRecord{Product=record.Product,Version=record.Version,PackageVersion=record.PackageVersion,ActiveDirectory=record.ActiveDirectory,
        Files=record.Files,Shortcuts=record.Shortcuts,DirectLaunch=direct};
       Activate(root,record,next,register);
       record=InstallCore.Read(root);
      }
      ClearBlock(root,Package(record));return new LaunchRecovery{Record=record};
     }
     var result=new LaunchRecovery{Blocked=true,BlockedPackage=Package(record),Reason=failure,Record=record};
     Version active=DirectoryVersion(record.ActiveDirectory);
     var candidates=record.Files.Select(DirectoryOf).Distinct(StringComparer.OrdinalIgnoreCase)
      .Where(d=>!d.Equals(record.ActiveDirectory,StringComparison.OrdinalIgnoreCase)&&DirectoryVersion(d)<active)
      .Where(d=>Executables.All(name=>record.Files.Any(f=>f.Replace('\\','/').Equals(d+"/"+name,StringComparison.OrdinalIgnoreCase))))
      .OrderByDescending(DirectoryVersion).ToList();
     foreach(string directory in candidates){
      if(CheckVersion(root,directory,probe,delay,false,out direct)!=null)continue;
      var restored=new InstallRecord{Product=record.Product,Version=ApplicationVersion(directory),PackageVersion=PackageVersion(directory),ActiveDirectory=directory,Files=record.Files,Shortcuts=record.Shortcuts,DirectLaunch=direct};
      Activate(root,record,restored,register);
      result.RolledBack=true;result.RestoredPackage=Package(restored);result.Record=InstallCore.Read(root);
      break;
     }
     RecordBlock(root,result);
     return result;
    }finally{if(held)mutex.ReleaseMutex();}
   }
  }

  static void Activate(string root,InstallRecord previous,InstallRecord next,Action<string,InstallRecord> register){
   WriteRecord(root,next);
   try{register(root,next);}
   catch{try{WriteRecord(root,previous);register(root,previous);}catch{}throw;}
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

  // True when the file has DOS and PE headers. Windows treats anything else as a 16-bit
  // program and opens a modal "Unsupported 16-Bit Application" dialog, which would stall
  // a silent setup, so damaged files are rejected before Windows is asked to start them.
  public static bool IsWindowsExecutable(string path){
   using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
    var header=new byte[64];if(file.Read(header,0,64)!=64||header[0]!='M'||header[1]!='Z')return false;
    int offset=BitConverter.ToInt32(header,0x3c);if(offset<64||offset>file.Length-4)return false;
    var signature=new byte[4];file.Position=offset;
    return file.Read(signature,0,4)==4&&signature[0]=='P'&&signature[1]=='E'&&signature[2]==0&&signature[3]==0;
   }
  }

  // Creates the process suspended and terminates it before any of its code runs. Windows
  // Code Integrity (Smart App Control and App Control policies) is enforced at creation,
  // so this reports the same block a real launch would, without opening a window.
  public static int NativeProbe(string path){
   try{if(!IsWindowsExecutable(path))return BadExecutableFormat;}
   catch(UnauthorizedAccessException){return 5;}
   catch(IOException error){return error.HResult&0xffff;}
   var startup=new StartupInfo{cb=Marshal.SizeOf(typeof(StartupInfo))};ProcessInformation process;
   var command=new StringBuilder("\""+path+"\" --launch-probe");
   // Report failures as error codes; never let Windows show an error dialog to a silent setup.
   uint previous,ignored;SetThreadErrorMode(FailCriticalErrors|NoGpFaultErrorBox|NoOpenFileErrorBox,out previous);
   try{
    if(!CreateProcessW(path,command,IntPtr.Zero,IntPtr.Zero,false,CreateSuspended|CreateNoWindow,IntPtr.Zero,Path.GetDirectoryName(path),ref startup,out process))return Marshal.GetLastWin32Error();
   }finally{SetThreadErrorMode(previous,out ignored);}
   try{TerminateProcess(process.hProcess,0);WaitForSingleObject(process.hProcess,5000);}
   finally{CloseHandle(process.hThread);CloseHandle(process.hProcess);}
   return 0;
  }
  const int BadExecutableFormat=193;
  const uint CreateSuspended=0x00000004,CreateNoWindow=0x08000000;
  const uint FailCriticalErrors=0x0001,NoGpFaultErrorBox=0x0002,NoOpenFileErrorBox=0x8000;
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetThreadErrorMode(uint mode,out uint previous);
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
