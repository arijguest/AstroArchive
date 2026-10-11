// C# 5 / .NET Framework 4.8. Simulated Windows launch blocks; generated directories only.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using AstroArchive.Installation;

static class LaunchSafetyTests {
 static string scratch;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static InstallPackage Package(string version){var p=new InstallPackage{Version=version,PackageVersion=version+".1",Revision=1};foreach(string name in new[]{"AstroArchive.exe","Start.exe","Release_Notes.txt"}){byte[] bytes=Encoding.UTF8.GetBytes(version+":"+name);p.Files.Add(new PayloadFile{Name=name,Bytes=bytes,Hash=InstallCore.Hash(bytes)});}return p;}
 static string Installed(string name,params string[] versions){string root=Path.Combine(scratch,name);foreach(string version in versions)new InstallCore().Install(root,Package(version),Encoding.UTF8.GetBytes("test installer"));return root;}
 // Simulates Smart App Control rejecting the named version folders (or only one executable in them).
 static Func<string,int> Blocking(params string[] blocked){return path=>blocked.Any(b=>(Path.GetFileName(Path.GetDirectoryName(path))+"/"+Path.GetFileName(path)).StartsWith(b,StringComparison.OrdinalIgnoreCase))?4551:0;}
 static UpdateManifest Manifest(string version){return new UpdateManifest{schema=1,application_version=version,package_version=version+".1",url=UpdateClient.Repository+"/releases/download/v"+version+".1/AstroArchive-"+version+"-Windows-x64-Offline-Setup.exe",sha256=new string('a',64),size=1};}
 static UpdateClient Feed(UpdateManifest manifest){return new UpdateClient{Fetch=(uri,limit)=>Encoding.UTF8.GetBytes(InstallCore.Json().Serialize(manifest))};}

 public static void Run(Action<string,Action> test,string directory){
  scratch=Path.Combine(directory,"launch-safety");
  test("Launch check keeps a version Windows starts and changes nothing",()=>{
   string root=Installed("healthy","1.2.0","1.3.0");string before=File.ReadAllText(Path.Combine(root,"install.json"));int registered=0;
   var result=LaunchSafety.Recover(root,Blocking(),(r,m)=>registered++);
   Check(!result.Blocked&&!result.RolledBack&&registered==0,"Healthy version was treated as blocked");
   Check(File.ReadAllText(Path.Combine(root,"install.json"))==before&&LaunchSafety.ReadBlock(root)==null,"Healthy check changed the installation");
  });
  test("Blocked update automatically restores the newest earlier version Windows starts",()=>{
   string root=Installed("rollback","1.2.0","1.3.0","1.4.0");InstallRecord registered=null;
   var result=LaunchSafety.Recover(root,Blocking("app-1.4.0-r1/"),(r,m)=>registered=m);
   var record=InstallCore.Read(root);
   Check(result.Blocked&&result.RolledBack&&result.BlockedPackage=="1.4.0.1"&&result.RestoredPackage=="1.3.0.1","Rollback result is incorrect");
   Check(record.ActiveDirectory=="app-1.3.0-r1"&&record.Version=="1.3.0"&&record.PackageVersion=="1.3.0.1","Earlier version was not activated");
   Check(registered!=null&&registered.ActiveDirectory=="app-1.3.0-r1","Shortcuts were not pointed at the restored version");
   Check(record.Files.Count(f=>f.StartsWith("app-1.4.0-r1"))==3&&File.Exists(Path.Combine(root,"app-1.4.0-r1","Start.exe")),"Blocked version files are no longer managed for repair or uninstall");
   Check(result.Reason.Contains("Windows blocked Start.exe")&&result.Reason.Contains("4551"),"Block reason lost the executable or Windows code");
   var block=LaunchSafety.ReadBlock(root);Check(block!=null&&block.BlockedPackages.SequenceEqual(new[]{"1.4.0.1"})&&block.NoticePending,"Blocked package was not recorded");
  });
  test("Rollback skips every earlier version Windows also blocks, checking both executables",()=>{
   string root=Installed("rollback-skip","1.2.0","1.3.0","1.4.0");
   var result=LaunchSafety.Recover(root,Blocking("app-1.4.0-r1/Start.exe","app-1.3.0-r1/AstroArchive.exe"),(r,m)=>{});
   Check(result.RolledBack&&result.RestoredPackage=="1.2.0.1"&&InstallCore.Read(root).ActiveDirectory=="app-1.2.0-r1","Rollback chose a blocked version");
  });
  test("Missing executables count as a version that cannot start",()=>{
   string root=Installed("rollback-missing","1.2.0","1.3.0");File.Delete(Path.Combine(root,"app-1.3.0-r1","AstroArchive.exe"));
   var result=LaunchSafety.Recover(root,Blocking(),(r,m)=>{});
   Check(result.RolledBack&&result.Reason=="AstroArchive.exe is missing"&&InstallCore.Read(root).ActiveDirectory=="app-1.2.0-r1","Missing application was not recovered");
  });
  test("Without an earlier working version the installation is left unchanged",()=>{
   string root=Installed("rollback-none","1.2.0");string before=File.ReadAllText(Path.Combine(root,"install.json"));int registered=0;
   var result=LaunchSafety.Recover(root,Blocking("app-1.2.0-r1/"),(r,m)=>registered++);
   Check(result.Blocked&&!result.RolledBack&&registered==0&&File.ReadAllText(Path.Combine(root,"install.json"))==before,"Unrecoverable block changed the installation");
   Check(LaunchSafety.IsBlocked(root,"1.2.0.1")&&!LaunchSafety.ReadBlock(root).NoticePending,"Unrecoverable block was not recorded");
  });
  test("Rollback never activates a newer version than the blocked one",()=>{
   string root=Installed("rollback-newer","1.2.0","1.3.0");var record=InstallCore.Read(root);record.ActiveDirectory="app-1.2.0-r1";record.Version="1.2.0";record.PackageVersion="1.2.0.1";LaunchSafety.WriteRecord(root,record);
   var result=LaunchSafety.Recover(root,Blocking("app-1.2.0-r1/"),(r,m)=>{});
   Check(result.Blocked&&!result.RolledBack&&InstallCore.Read(root).ActiveDirectory=="app-1.2.0-r1","Rollback moved to a newer version");
  });
  test("Shortcut registration failure keeps the original record",()=>{
   string root=Installed("rollback-register","1.2.0","1.3.0");bool threw=false;
   try{LaunchSafety.Recover(root,Blocking("app-1.3.0-r1/"),(r,m)=>{if(m.ActiveDirectory=="app-1.2.0-r1")throw new IOException("Injected shortcut failure");});}catch(IOException){threw=true;}
   Check(threw&&InstallCore.Read(root).ActiveDirectory=="app-1.3.0-r1","Failed registration left a partial rollback");
  });
  test("Transient launch failures retry before treating a version as blocked",()=>{
   string root=Installed("rollback-transient","1.2.0","1.3.0");int calls=0;
   var result=LaunchSafety.Recover(root,path=>++calls==1?32:0,(r,m)=>{});
   Check(!result.Blocked&&calls==3,"A transient sharing failure caused a rollback");
  });
  test("Blocked packages are not offered again and the rollback notice appears once",()=>{
   string root=Installed("rollback-feed","1.2.0","1.3.0","1.4.0");LaunchSafety.Recover(root,Blocking("app-1.4.0-r1/"),(r,m)=>{});
   var installed=InstallCore.Read(root);
   Check(Feed(Manifest("1.4.0")).Check(installed,root)==null,"Blocked package was offered again");
   Check(Feed(Manifest("1.4.0")).Check(installed)!=null,"Update check without an installation ignored the feed");
   Check(Feed(Manifest("1.5.0")).Check(installed,root)!=null,"A newer package was suppressed");
   Check(LaunchSafety.ConsumeNotice(root,"1.4.0.1")==null,"Notice was shown by a version other than the restored one");
   var notice=LaunchSafety.ConsumeNotice(root,"1.3.0.1");Check(notice!=null&&notice.BlockedPackage=="1.4.0.1"&&notice.RestoredPackage=="1.3.0.1","Rollback notice was not available");
   Check(LaunchSafety.ConsumeNotice(root,"1.3.0.1")==null&&LaunchSafety.IsBlocked(root,"1.4.0.1"),"Notice repeated or cleared the block");
  });
  test("A previously blocked package that later starts is offered and repaired normally",()=>{
   string root=Installed("rollback-recovered","1.2.0","1.3.0");LaunchSafety.Recover(root,Blocking("app-1.3.0-r1/"),(r,m)=>{});
   new InstallCore().Install(root,Package("1.3.0"),Encoding.UTF8.GetBytes("test installer"));
   var result=LaunchSafety.Recover(root,Blocking(),(r,m)=>{});
   Check(!result.Blocked&&InstallCore.Read(root).ActiveDirectory=="app-1.3.0-r1"&&!LaunchSafety.IsBlocked(root,"1.3.0.1")&&LaunchSafety.ReadBlock(root)==null,"Reinstalled package stayed blocked");
   new InstallCore().Uninstall(root);Check(!Directory.Exists(Path.Combine(root,"app-1.2.0-r1"))&&!Directory.Exists(Path.Combine(root,"app-1.3.0-r1")),"Uninstall left restored or blocked versions");
  });
  test("Damaged executables are rejected without asking Windows to start them",()=>{
   string folder=Path.Combine(scratch,"headers");Directory.CreateDirectory(folder);
   var pe=new byte[256];pe[0]=(byte)'M';pe[1]=(byte)'Z';BitConverter.GetBytes(128).CopyTo(pe,0x3c);pe[128]=(byte)'P';pe[129]=(byte)'E';
   string valid=Path.Combine(folder,"valid.exe");File.WriteAllBytes(valid,pe);
   string text=Path.Combine(folder,"text.exe");File.WriteAllText(text,"Simulated launcher that Windows refuses to start");
   string truncated=Path.Combine(folder,"truncated.exe");File.WriteAllBytes(truncated,pe.Take(40).ToArray());
   var outside=(byte[])pe.Clone();BitConverter.GetBytes(4096).CopyTo(outside,0x3c);string offset=Path.Combine(folder,"offset.exe");File.WriteAllBytes(offset,outside);
   Check(LaunchSafety.IsWindowsExecutable(valid),"A PE header was rejected");
   Check(!LaunchSafety.IsWindowsExecutable(text)&&!LaunchSafety.IsWindowsExecutable(truncated)&&!LaunchSafety.IsWindowsExecutable(offset),"A damaged executable was accepted");
   // Text is rejected before CreateProcess, which would otherwise show a modal 16-bit dialog.
   Check(LaunchSafety.NativeProbe(text)==193&&LaunchSafety.NativeProbe(Path.Combine(folder,"missing.exe"))==2,"Damaged or missing executables did not report Windows error codes");
  });
  test("Damaged block records never prevent updates",()=>{
   string root=Installed("rollback-damaged","1.2.0");File.WriteAllText(Path.Combine(root,"launch-blocked.json"),"{not json");
   Check(!LaunchSafety.IsBlocked(root,"1.2.0.1")&&Feed(Manifest("1.3.0")).Check(InstallCore.Read(root),root)!=null,"Damaged block record suppressed updates");
  });
 }
}
