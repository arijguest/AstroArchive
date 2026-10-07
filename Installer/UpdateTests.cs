using System;
using System.IO;
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
}
