// C# 5 / .NET Framework 4.8. Public GitHub releases; no account or token needed.
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Linq;

namespace AstroArchive.Installation {
 public sealed class UpdateManifest {
  public int schema;
  public string application_version, package_version, url, download_url, sha256;
  public long size;
 }
 public sealed class UpdateTarget {
  public string Root, Cache;
  public InstallRecord Running, Existing;
  public InstallRecord Comparison {get{return Existing!=null&&InstallCore.Parse(Existing.PackageVersion??Existing.Version)>InstallCore.Parse(Running.PackageVersion??Running.Version)?Existing:Running;}}
 }

 public sealed class UpdateClient {
  public const string Repository = "https://github.com/arijguest/AstroArchive";
  public const string Feed = Repository + "/releases/latest/download/update.json";
  public const long MaximumInstallerSize = 128L * 1024 * 1024;
  public Func<Uri, long, byte[]> Fetch = Download;

  public static bool TrustedDownload(Uri uri) {
   return uri != null && uri.Scheme == "https" && uri.Port == 443 &&
    uri.UserInfo.Length == 0 && (uri.Host == "github.com" ||
    uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");
  }

  public static void Validate(UpdateManifest manifest) {
   if (manifest == null || manifest.schema != 1 ||
    !Regex.IsMatch(manifest.application_version ?? "", @"^\d+\.\d+\.\d+$") ||
    !Regex.IsMatch(manifest.package_version ?? "", @"^\d+\.\d+\.\d+\.[1-9]\d*$"))
    throw new IOException("Invalid update version or schema.");
   Version app = InstallCore.Parse(manifest.application_version), package = InstallCore.Parse(manifest.package_version);
   if (app.Major != package.Major || app.Minor != package.Minor || app.Build != package.Build || package.Revision > 65534)
    throw new IOException("Update application and package versions do not agree.");
   string expected = Repository + "/releases/download/v" + manifest.package_version +
    "/AstroArchive-" + manifest.application_version + "-Windows-x64-Offline-Setup.exe";
   string concise = Repository + "/releases/download/v" + manifest.package_version + "/" + InstallerName(manifest);
   if ((!string.Equals(manifest.url, expected, StringComparison.Ordinal) && !string.Equals(manifest.url, concise, StringComparison.Ordinal)) ||
    (manifest.download_url != null && !string.Equals(manifest.download_url, concise, StringComparison.Ordinal)) ||
    !Regex.IsMatch(manifest.sha256 ?? "", @"^[a-fA-F0-9]{64}$") ||
    manifest.size < 1 || manifest.size > MaximumInstallerSize)
    throw new IOException("Invalid update asset, checksum or size.");
  }

  public static string InstallerName(UpdateManifest manifest) { return "AstroArchive" + manifest.package_version + ".exe"; }
  public static string InstallationCache(string root){return Path.Combine(InstallCore.Root(root),"updates");}

  public static UpdateTarget ResolveTarget(string executable,string version,string registeredRoot,string runningPackage=null){
   InstallCore.Parse(version);string app=Path.GetFullPath(executable),directory=Path.GetDirectoryName(app),parent=Path.GetDirectoryName(directory);
   runningPackage=runningPackage??version+".0";
   var package=InstallCore.Parse(runningPackage);var application=InstallCore.Parse(version);
   if(package.Major!=application.Major||package.Minor!=application.Minor||package.Build!=application.Build)throw new IOException("Running application and package versions do not agree.");
   InstallCore.NoLinks(app);
   var record=parent==null?null:InstallCore.Read(parent);
   if(record!=null&&record.Files.Any(relative=>string.Equals(InstallCore.Managed(parent,relative),app,StringComparison.OrdinalIgnoreCase))){
    bool active=string.Equals(InstallCore.Managed(parent,record.ActiveDirectory+"\\AstroArchive.exe"),app,StringComparison.OrdinalIgnoreCase);
    return new UpdateTarget{Root=InstallCore.Root(parent),Cache=InstallationCache(parent),Running=active?record:new InstallRecord{Version=version,PackageVersion=runningPackage},Existing=record};
   }
   string root=InstallCore.Root(registeredRoot);
   return new UpdateTarget{Root=root,Cache=Path.Combine(directory,"updates"),Running=new InstallRecord{Version=version,PackageVersion=runningPackage},Existing=InstallCore.Read(root)};
  }

  public static ProcessStartInfo InstallerStartInfo(UpdateManifest manifest,string installer,string root,int waitPid){
   Validate(manifest);root=InstallCore.Root(root);if(waitPid<=0)throw new IOException("An application process is required for update handoff.");
   InstallCore.NoLinks(installer);var file=new FileInfo(installer);
   if(!file.Exists||file.Length!=manifest.size||!string.Equals(InstallCore.HashFile(installer),manifest.sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Installer failed verification before launch.");
   var installed=InstallCore.Read(root);
   if(installed==null&&Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("The installation folder contains unrelated files. Choose an empty folder for installation.");
   if(installed!=null&&(InstallCore.Parse(manifest.application_version)<InstallCore.Parse(installed.Version)||InstallCore.Parse(manifest.package_version)<=InstallCore.Parse(installed.PackageVersion??installed.Version)))throw new IOException("This release is no longer newer than the installed package. Check for releases again.");
   string arguments=(installed==null?"":"--update ")+"--silent --root "+Quote(root)+" --waitpid "+waitPid+" --restart";
   return new ProcessStartInfo(Path.GetFullPath(installer),arguments){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(installer))};
  }
  static string Quote(string path){if(path.IndexOf('"')>=0)throw new IOException("Quotes are not supported in the installation path.");return "\""+path+"\"";}

  public UpdateManifest Check(InstallRecord installed) {
   byte[] bytes = Fetch(new Uri(Feed), 64 * 1024);
   if (bytes == null || bytes.Length > 64 * 1024) throw new IOException("Update feed is too large.");
   var manifest = InstallCore.Json().Deserialize<UpdateManifest>(Encoding.UTF8.GetString(bytes));
   Validate(manifest);
   if (InstallCore.Parse(manifest.application_version) < InstallCore.Parse(installed.Version) ||
    InstallCore.Parse(manifest.package_version) <= InstallCore.Parse(installed.PackageVersion ?? installed.Version)) return null;
   return manifest;
  }

  public string Prepare(UpdateManifest manifest, string cache) {
   Validate(manifest);
   byte[] bytes = Fetch(new Uri(manifest.download_url ?? manifest.url), manifest.size);
   if (bytes == null || bytes.LongLength != manifest.size ||
    !string.Equals(InstallCore.Hash(bytes), manifest.sha256, StringComparison.OrdinalIgnoreCase))
    throw new IOException("Downloaded update failed SHA-256 verification.");
   InstallCore.NoLinks(cache);
   string directory = Path.Combine(cache, Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(directory);
   string path = Path.Combine(directory, InstallerName(manifest));
   try {
    InstallCore.NoLinks(path);
    using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
     file.Write(bytes, 0, bytes.Length); file.Flush(true);
    }
    if (!string.Equals(InstallCore.HashFile(path), manifest.sha256, StringComparison.OrdinalIgnoreCase))
     throw new IOException("Saved update failed SHA-256 verification.");
    return path;
   } catch { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(directory)) Directory.Delete(directory); throw; }
  }

  static byte[] Download(Uri uri, long limit) {
   if (limit < 1 || limit > MaximumInstallerSize) throw new IOException("Invalid download limit.");
   // TLS and certificate validation remain enabled. Every redirect is checked.
   ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
   for (int hop = 0; hop < 6; hop++) {
    if (!TrustedDownload(uri)) throw new IOException("Untrusted update download destination.");
    var request = (HttpWebRequest)WebRequest.Create(uri);
    request.AllowAutoRedirect = false; request.Timeout = 10000; request.ReadWriteTimeout = 15000;
    request.UserAgent = "AstroArchive-Updater";
    using (var response = (HttpWebResponse)request.GetResponse()) {
     int status = (int)response.StatusCode;
     if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308) {
      string location = response.Headers["Location"];
      if (string.IsNullOrEmpty(location)) throw new IOException("Update redirect has no destination.");
      uri = new Uri(uri, location); continue;
     }
     if (status != 200 || response.ContentLength > limit) throw new IOException("Invalid update download response.");
     using (var stream = response.GetResponseStream()) using (var output = new MemoryStream()) {
      byte[] buffer = new byte[65536]; int count;
      var watch = System.Diagnostics.Stopwatch.StartNew();
      while ((count = stream.Read(buffer, 0, buffer.Length)) > 0) {
       if (output.Length + count > limit || watch.Elapsed.TotalMinutes > 3) throw new IOException("Update download exceeded its limit.");
       output.Write(buffer, 0, count);
      }
      return output.ToArray();
     }
    }
   }
   throw new IOException("Too many update redirects.");
  }
 }
}
