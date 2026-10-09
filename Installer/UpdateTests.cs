using System;
using System.IO;
using System.Linq;
using System.Text;
using AstroArchive.Installation;

static class UpdateTests {
 static readonly byte[] Installer = Encoding.UTF8.GetBytes("fixture installer; never executed");
 static UpdateManifest Manifest(string version = "1.2.0", int revision = 2) {
  return new UpdateManifest { schema = 1, application_version = version, package_version = version + "." + revision,
   url = UpdateClient.Repository + "/releases/download/v" + version + "." + revision + "/AstroArchive-" + version + "-Windows-x64-Offline-Setup.exe",
   sha256 = InstallCore.Hash(Installer), size = Installer.Length };
 }
 static UpdateClient Client(UpdateManifest manifest) {
  return new UpdateClient { Fetch = (uri, limit) => uri.AbsoluteUri == UpdateClient.Feed ?
   Encoding.UTF8.GetBytes(InstallCore.Json().Serialize(manifest)) : Installer };
 }
 static InstallRecord Installed(string version = "1.2.0", int revision = 1) {
  return new InstallRecord { Version = version, PackageVersion = version + "." + revision };
 }
 static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
 static void Refused(Action action) {
  try { action(); } catch (IOException) { return; }
  throw new Exception("Expected update refusal.");
 }
 public static void Run(Action<string, Action> test, string scratch) {
  test("Canceled update preparations leave no installer or partial cache",()=>{
   var manifest=Manifest();string cache=Path.Combine(scratch,"cancel-download");using(var stop=new System.Threading.CancellationTokenSource()){
    var client=Client(manifest);client.Progress=p=>{if(p.Received>0)stop.Cancel();};bool canceled=false;try{client.Prepare(manifest,cache,stop.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"Cancellation did not stop preparation");Check(!Directory.Exists(cache)||!Directory.EnumerateFiles(cache,"*",SearchOption.AllDirectories).Any(),"Canceled preparation left a cached installer");
   }
  });
  test("Update policy diagnostics retain native code, verified bytes and exact launch context",()=>{
   var manifest=Manifest();manifest.authenticode_signed=false;string setup=Client(manifest).Prepare(manifest,Path.Combine(scratch,"policy-cache"));var attempted=DateTime.UtcNow;
   var error=new IOException("Could not start "+setup,new System.ComponentModel.Win32Exception(4551));
   string report=UpdateDiagnostics.Details(error,manifest,setup,attempted,(path,time)=>{Check(path==setup&&time==attempted,"Policy query lost the rejected launch");return "Event 3077; Policy ID: {0283ac0f-fff1-49ae-ada1-8a933130cad6}";});
   Check(report.Contains("Native Windows error: 4551")&&report.Contains("Installer: "+setup)&&report.Contains("Saved SHA-256: "+manifest.sha256)&&report.Contains("Feed reports Authenticode signed: False")&&report.Contains("Event 3077"),"Policy log lost the native code, file, signature status or policy evidence");
   Check(File.ReadAllBytes(setup).SequenceEqual(Installer),"Diagnostics changed the rejected installer");
   string log=Path.Combine(scratch,"last-error.txt");Check(UpdateDiagnostics.Message(error,log).Contains(log)&&!UpdateDiagnostics.Message(error,log).Contains("AstroArchive-setup-error.txt"),"Update error points at the setup log even though setup never ran");
  });
  test("Unavailable policy events preserve the original update failure",()=>{
   var error=new System.ComponentModel.Win32Exception(4551);string report=UpdateDiagnostics.Details(error,null,"missing.exe",DateTime.UtcNow,(path,time)=>{throw new UnauthorizedAccessException("Access denied");});
   Check(report.Contains("Native Windows error: 4551")&&report.Contains("Policy events unavailable: Access denied"),"Event log permissions concealed the update failure");
   report=UpdateDiagnostics.Details(new IOException("Disk full"),null,"missing.exe",DateTime.UtcNow,(path,time)=>{throw new Exception("Unrelated errors must not inspect policy events");});
   Check(!report.Contains("Policy events")&&UpdateDiagnostics.Message(new IOException("Disk full"),null)=="Disk full","Ordinary download failure was relabeled or queried policy events");
   report=UpdateDiagnostics.Details(error,null,Path.Combine(scratch,"missing-installer.exe"),DateTime.UtcNow);
   Check(report.Contains("Native Windows error: 4551"),"Native event lookup concealed the original error");
  });
  test("Code Integrity evidence matches the rejected file rather than the parent process",()=>{
   string installer=@"C:\Users\arija\AppData\Local\Programs\AstroArchive\updates\abc\AstroArchive1.10.1.1.exe";
   string device=@"\Device\HarddiskVolume3\Users\arija\AppData\Local\Programs\AstroArchive\updates\abc\AstroArchive1.10.1.1.exe";
   Func<string,string,string> xml=(file,parent)=>"<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><EventData><Data Name='FileName'>"+file+"</Data><Data Name='ProcessName'>"+parent+"</Data></EventData></Event>";
   Check(UpdateDiagnostics.MatchesInstaller(xml(device.ToUpperInvariant(),"Start.exe"),installer),"Native device path did not match the installer");
   Check(!UpdateDiagnostics.MatchesInstaller(xml(device.Replace("updates\\abc", "updates\\other"),device),installer),"Event from a different cached download was attributed to this launch");
   Check(!UpdateDiagnostics.MatchesInstaller(xml("other.exe",device),installer),"Parent process was mistaken for the rejected file");
  });
  test("Release notes use the feed when present and a bounded exact-tag API fallback",()=>{
   var manifest=Manifest();manifest.release_notes="New import and session views.";var client=new UpdateClient{Fetch=(uri,limit)=>{throw new Exception("Embedded notes should not fetch");}};Check(client.ReleaseNotes(manifest)==manifest.release_notes,"Embedded notes changed");
   manifest.release_notes=null;string requested=null;long maximum=0;client.Fetch=(uri,limit)=>{requested=uri.AbsoluteUri;maximum=limit;return Encoding.UTF8.GetBytes("{\"body\":\"Package notes\"}");};Check(client.ReleaseNotes(manifest)=="Package notes"&&requested.EndsWith("/releases/tags/v1.2.0.2")&&maximum==256*1024,"Notes fallback used the wrong package or unbounded response");
   Check(UpdateClient.ReleasePage(manifest)==UpdateClient.Repository+"/releases/tag/v1.2.0.2","Release page differs from package");manifest.release_notes=new string('a',48001);Refused(()=>UpdateClient.Validate(manifest));
  });
  test("Verified update downloads report bytes and percentage without changing cached content",()=>{
   var manifest=Manifest();var client=Client(manifest);var progress=new System.Collections.Generic.List<UpdateDownloadProgress>();client.Progress=p=>progress.Add(p);string setup=client.Prepare(manifest,Path.Combine(scratch,"progress-cache"));
   Check(progress.First().Received==0&&progress.Last().Received==manifest.size&&progress.Last().Percent==100&&progress.All(p=>p.Total==manifest.size),"Download progress totals differ");Check(InstallCore.HashFile(setup)==manifest.sha256,"Progress changed downloaded bytes");
   Check(new UpdateDownloadProgress{Received=20,Total=10}.Percent==100&&new UpdateDownloadProgress{Received=10,Total=0}.Percent==0,"Progress percentage was not bounded");
  });
  test("Successful update confirmation is version-specific and consumed once",()=>{
   string root=Path.Combine(scratch,"receipt-root");new InstallCore().Install(root,FixturePackage("1.2.0",2),Installer);UpdateClient.RecordInstalledUpdate(root,"1.2.0.1");
   Check(UpdateClient.ConsumeInstalledUpdate(root,"1.2.0.1")==null&&File.Exists(Path.Combine(root,"update-receipt.json")),"Wrong running version claimed an update");var receipt=UpdateClient.ConsumeInstalledUpdate(root,"1.2.0.2");Check(receipt.PackageVersion=="1.2.0.2"&&receipt.PreviousPackageVersion=="1.2.0.1"&&receipt.InstalledUtc.Length>0,"Confirmation lost update metadata");Check(UpdateClient.ConsumeInstalledUpdate(root,"1.2.0.2")==null,"Update confirmation repeated");
   UpdateClient.RecordInstalledUpdate(root,"1.2.0.1");new InstallCore().Install(root,FixturePackage("1.3.0",1),Installer);Check(UpdateClient.ConsumeInstalledUpdate(root,"1.2.0.2")==null,"Stale confirmation ignored active installation");
  });
  test("Installed release targets keep downloads outside the active application folder",()=>{
   string root=Path.Combine(scratch,"installed release with spaces");var package=FixturePackage("1.2.0",1);var record=new InstallCore().Install(root,package,Installer);
   string app=InstallCore.Managed(root,record.ActiveDirectory+"\\AstroArchive.exe");var target=UpdateClient.ResolveTarget(app,"1.2.0",Path.Combine(scratch,"unused registered root"));
   Check(target.Root==root&&target.Cache==Path.Combine(root,"updates")&&target.Running.PackageVersion=="1.2.0.1","Installed update target was not retained");
   var manifest=Manifest();string setup=Client(manifest).Prepare(manifest,target.Cache);var start=UpdateClient.InstallerStartInfo(manifest,setup,target.Root,12345);
   Check(start.FileName==setup&&!start.UseShellExecute&&start.WorkingDirectory==Path.GetDirectoryName(setup),"Verified installer launch location changed");
   Check(start.Arguments=="--update --silent --root \""+root+"\" --waitpid 12345 --restart","Update did not wait, install silently and restart");
   new InstallCore().Install(root,FixturePackage("1.2.0",2),Installer,true);Check(InstallCore.Read(root).PackageVersion=="1.2.0.2"&&File.Exists(setup),"Cached update prevented activation or was deleted");
   new InstallCore().Install(root,FixturePackage("1.2.0",2),Installer);Check(InstallCore.Read(root).PackageVersion=="1.2.0.2","Updates folder prevented same-package repair");
  });
  test("Portable release targets use a managed registered installation and local program downloads",()=>{
   string directory=Path.Combine(scratch,"portable program"),root=Path.Combine(scratch,"portable installed target");Directory.CreateDirectory(directory);string app=Path.Combine(directory,"AstroArchive.exe");File.WriteAllText(app,"portable");
   var target=UpdateClient.ResolveTarget(app,"1.2.0",root);Check(target.Root==root&&target.Cache==Path.Combine(directory,"updates")&&target.Existing==null,"Portable install target or download folder incorrect");
   var manifest=Manifest();string setup=Client(manifest).Prepare(manifest,target.Cache);var start=UpdateClient.InstallerStartInfo(manifest,setup,root,4321);
   Check(!start.Arguments.Contains("--update")&&start.Arguments.Contains("--waitpid 4321 --restart"),"Portable install incorrectly required an existing record");Check(File.ReadAllText(app)=="portable"&&!Directory.Exists(root),"Preparing portable installation changed application files");
  });
  test("A retained older app version still updates its own installation root",()=>{
   string root=Path.Combine(scratch,"retained older version");var older=new InstallCore().Install(root,FixturePackage("1.2.0",1),Installer);string app=InstallCore.Managed(root,older.ActiveDirectory+"\\AstroArchive.exe");new InstallCore().Install(root,FixturePackage("1.3.0",1),Installer);
   var target=UpdateClient.ResolveTarget(app,"1.2.0",Path.Combine(scratch,"wrong registered root"),"1.2.0.1");Check(target.Root==root&&target.Cache==Path.Combine(root,"updates")&&target.Running.PackageVersion=="1.2.0.1"&&target.Comparison.PackageVersion=="1.3.0.1","Retained application was treated as an unrelated portable copy");
  });
  test("Portable checks respect the embedded package revision and newer registered installations",()=>{
   string directory=Path.Combine(scratch,"portable revision"),root=Path.Combine(scratch,"registered newer");Directory.CreateDirectory(directory);string app=Path.Combine(directory,"AstroArchive.exe");File.WriteAllText(app,"portable");
   var target=UpdateClient.ResolveTarget(app,"1.3.0",root,"1.3.0.2");Check(Client(Manifest("1.3.0",1)).Check(target.Comparison)==null,"Published older package offered to a newer portable build");
   new InstallCore().Install(root,FixturePackage("1.4.0",1),Installer);target=UpdateClient.ResolveTarget(app,"1.3.0",root,"1.3.0.2");Check(target.Comparison.PackageVersion=="1.4.0.1"&&Client(Manifest("1.3.0",3)).Check(target.Comparison)==null,"Registered installation downgrade offered");
  });
  test("Installer launch revalidates cached bytes and retains the existing installation on corruption",()=>{
   string root=Path.Combine(scratch,"launch corruption root");new InstallCore().Install(root,FixturePackage("1.2.0",1),Installer);
   var manifest=Manifest();string setup=Client(manifest).Prepare(manifest,UpdateClient.InstallationCache(root));var damaged=new byte[Installer.Length];File.WriteAllBytes(setup,damaged);
   Refused(()=>UpdateClient.InstallerStartInfo(manifest,setup,root,10));Check(InstallCore.Read(root).PackageVersion=="1.2.0.1","Corrupt cached update changed the installation");
  });
  test("Installer handoff rejects stale releases and invalid wait processes",()=>{
   string root=Path.Combine(scratch,"stale launch root");new InstallCore().Install(root,FixturePackage("1.2.0",3),Installer);var manifest=Manifest();string setup=Client(manifest).Prepare(manifest,UpdateClient.InstallationCache(root));
   Refused(()=>UpdateClient.InstallerStartInfo(manifest,setup,root,10));Refused(()=>UpdateClient.InstallerStartInfo(manifest,setup,root,0));Check(InstallCore.Read(root).PackageVersion=="1.2.0.3","Stale update replaced a newer installed package");
  });
  test("Fresh install handoff refuses unrelated files without overwriting them",()=>{
   string root=Path.Combine(scratch,"foreign launch target"),cache=Path.Combine(scratch,"foreign launch downloads");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"keep.txt"),"keep");var manifest=Manifest();string setup=Client(manifest).Prepare(manifest,cache);
   Refused(()=>UpdateClient.InstallerStartInfo(manifest,setup,root,10));Check(File.ReadAllText(Path.Combine(root,"keep.txt"))=="keep"&&!File.Exists(Path.Combine(root,"install.json")),"Foreign folder was modified");
  });
  test("Application handoff mutex matches launcher identity for equivalent installation paths",()=>{
   string root=Path.Combine(scratch,"launcher identity");Check(InstallCore.ApplicationMutexName(root)==InstallCore.ApplicationMutexName(root+Path.DirectorySeparatorChar),"Launcher and installer mutex identities differ");
   Check(InstallCore.ApplicationMutexName(root)!=InstallCore.ApplicationMutexName(root+"-other"),"Separate installations shared an update mutex");
  });
  test("Concise release installers are validated and used for verified downloads", () => {
   var manifest=Manifest("1.3.0",1);manifest.download_url=UpdateClient.Repository+"/releases/download/v1.3.0.1/AstroArchive1.3.0.1.exe";
   string requested=null;var client=new UpdateClient{Fetch=(uri,limit)=>{requested=uri.AbsoluteUri;return Installer;}};
   string path=client.Prepare(manifest,Path.Combine(scratch,"concise-cache"));Check(requested==manifest.download_url&&Path.GetFileName(path)=="AstroArchive1.3.0.1.exe","Concise installer was not used");
   manifest.url=manifest.download_url;manifest.download_url=null;UpdateClient.Validate(manifest);
  });
  test("Concise download URLs cannot change repository or package", () => {
   var manifest=Manifest();manifest.download_url=UpdateClient.Repository+"/releases/download/v1.2.0.3/AstroArchive1.2.0.3.exe";Refused(()=>UpdateClient.Validate(manifest));
   manifest.download_url="https://example.com/AstroArchive1.2.0.2.exe";Refused(()=>UpdateClient.Validate(manifest));
  });
  test("Update feed discovers a newer installer revision", () => {
   Check(Client(Manifest()).Check(Installed()).package_version == "1.2.0.2", "Revision upgrade was missed.");
  });
  test("Update feed ignores current and older packages", () => {
   Check(Client(Manifest()).Check(Installed("1.2.0", 2)) == null, "Same revision offered.");
   Check(Client(Manifest()).Check(Installed("1.2.0", 3)) == null, "Older revision offered.");
   Check(Client(Manifest("1.1.0", 99)).Check(Installed()) == null, "Application downgrade offered.");
  });
  test("Update versions compare numerically rather than as text", () => {
   Check(Client(Manifest("1.10.0")).Check(Installed("1.9.0")) != null, "Numeric application upgrade missed.");
   Check(Client(Manifest("1.2.0", 10)).Check(Installed("1.2.0", 9)) != null, "Numeric revision upgrade missed.");
  });
  test("Update feed adopts installations without a package version", () => {
   Check(Client(Manifest()).Check(new InstallRecord { Version = "1.2.0" }) != null, "Legacy package not upgraded.");
  });
  test("Verified update bytes are saved without touching installation data", () => {
   string cache = Path.Combine(scratch, "update-cache");
   string path = Client(Manifest()).Prepare(Manifest(), cache);
   Check(InstallCore.HashFile(path) == InstallCore.Hash(Installer), "Saved bytes changed.");
   Check(Path.GetFullPath(path).StartsWith(Path.GetFullPath(cache) + Path.DirectorySeparatorChar), "Cache path escaped.");
  });
  test("Corrupt and truncated updates leave no executable", () => {
   string cache = Path.Combine(scratch, "bad-update-cache");
   var client = new UpdateClient { Fetch = (uri, limit) => Encoding.UTF8.GetBytes("corrupt") };
   Refused(() => client.Prepare(Manifest(), cache));
   var sameSize = new UpdateClient { Fetch = (uri, limit) => new byte[Installer.Length] };
   Refused(() => sameSize.Prepare(Manifest(), cache));
   Check(!Directory.Exists(cache), "Unverified installer saved.");
  });
  test("Update feed rejects unrelated repositories and mismatched asset versions", () => {
   var manifest = Manifest(); manifest.url = manifest.url.Replace("arijguest", "other-owner");
   Refused(() => UpdateClient.Validate(manifest));
   manifest = Manifest(); manifest.url = manifest.url.Replace("v1.2.0.2", "v1.2.0.3");
   Refused(() => UpdateClient.Validate(manifest));
  });
  test("Update schema, package agreement, checksum and size are required", () => {
   var manifest = Manifest(); manifest.schema = 2; Refused(() => UpdateClient.Validate(manifest));
   manifest = Manifest(); manifest.application_version = "1.3.0"; Refused(() => UpdateClient.Validate(manifest));
   manifest = Manifest(); manifest.sha256 = "invalid"; Refused(() => UpdateClient.Validate(manifest));
   manifest = Manifest(); manifest.size = UpdateClient.MaximumInstallerSize + 1; Refused(() => UpdateClient.Validate(manifest));
   manifest = Manifest(); manifest.size = 0; Refused(() => UpdateClient.Validate(manifest));
  });
  test("Update redirects require HTTPS and known GitHub download hosts", () => {
   Check(UpdateClient.TrustedDownload(new Uri("https://release-assets.githubusercontent.com/asset")), "Release asset host rejected.");
   Check(!UpdateClient.TrustedDownload(new Uri("http://github.com/asset")), "Plain HTTP accepted.");
   Check(!UpdateClient.TrustedDownload(new Uri("https://github.com.evil.example/asset")), "Lookalike host accepted.");
   Check(!UpdateClient.TrustedDownload(new Uri("https://github.com:444/asset")), "Unexpected port accepted.");
   Check(!UpdateClient.TrustedDownload(new Uri("https://user@github.com/asset")), "Userinfo accepted.");
  });
  test("Oversized update feed is refused", () => {
   var client = new UpdateClient { Fetch = (uri, limit) => new byte[65537] };
   Refused(() => client.Check(Installed()));
  });
  test("Offline update checks preserve existing installations", () => {
   var installed = Installed();
   var client = new UpdateClient { Fetch = (uri, limit) => { throw new IOException("offline"); } };
   Refused(() => client.Check(installed));
   Check(installed.Version == "1.2.0" && installed.PackageVersion == "1.2.0.1", "Offline check changed the record.");
  });
 }
 static InstallPackage FixturePackage(string version,int revision){var package=new InstallPackage{Version=version,PackageVersion=version+"."+revision,Revision=revision};foreach(string name in new[]{"AstroArchive.exe","Start.exe"}){var bytes=Encoding.UTF8.GetBytes(version+":"+revision+":"+name);package.Files.Add(new PayloadFile{Name=name,Bytes=bytes,Hash=InstallCore.Hash(bytes)});}return package;}
}
