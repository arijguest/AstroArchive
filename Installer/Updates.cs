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
  public string release_notes;
  public long size;
  public bool? authenticode_signed;
 }
 public static class UpdateDiagnostics {
  public static string LogPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","updates","last-error.txt");}}
  public static string Message(Exception error,string log){
   string message=WindowsPolicyError.IsPolicyBlock(error)?"Windows blocked the update installer under Application Control or signature verification. Installation did not start. A trusted signed release is needed if Windows does not recognise this package.\r\n\r\n"+error.Message:error.Message;
   return message+(log==null?"":"\r\n\r\nDetails: "+log);
  }
  public static string Details(Exception error,UpdateManifest update,string installer,DateTime attemptedUtc,Func<string,DateTime,string> policyEvents=null){
   var text=new StringBuilder(DateTime.UtcNow.ToString("u")+"\r\n"+WindowsPolicyError.Details(error));
   if(update!=null)text.Append("\r\nPackage: ").Append(update.package_version).Append("\r\nExpected SHA-256: ").Append(update.sha256).Append("\r\nFeed reports Authenticode signed: ").Append(update.authenticode_signed.HasValue?update.authenticode_signed.Value.ToString():"not reported");
   if(!string.IsNullOrEmpty(installer)){
    text.Append("\r\nInstaller: ").Append(installer);
    try{InstallCore.NoLinks(installer);if(File.Exists(installer)&&new FileInfo(installer).Length<=UpdateClient.MaximumInstallerSize)text.Append("\r\nSaved SHA-256: ").Append(InstallCore.HashFile(installer));}catch(Exception e){text.Append("\r\nSaved checksum unavailable: ").Append(e.Message);}
    if(WindowsPolicyError.IsPolicyBlock(error)&&attemptedUtc!=DateTime.MinValue){
     try{text.Append("\r\n\r\n").Append((policyEvents??PolicyEvents)(installer,attemptedUtc));}catch(Exception e){text.Append("\r\nPolicy events unavailable: ").Append(e.Message);}
    }
   }
   return text.ToString();
  }
  public static string Record(Exception error,UpdateManifest update,string installer,DateTime attemptedUtc){
   try{string path=LogPath;InstallCore.NoLinks(path);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,Details(error,update,installer,attemptedUtc),new UTF8Encoding(false));return path;}catch{return null;}
  }
  static string PolicyEvents(string installer,DateTime attemptedUtc){
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return "Code Integrity events are available on Windows only.";
   return ReadWindowsEvents(installer,attemptedUtc);
  }
  public static bool MatchesInstaller(string eventXml,string installer){
   var xml=new System.Xml.XmlDocument{XmlResolver=null};xml.LoadXml(eventXml);
   string path=installer.Replace('/','\\');
   // Code Integrity uses device paths. Compare the complete path after the drive prefix.
   string suffix=path.Length>2&&path[1]==':'?path.Substring(2):path;
   foreach(System.Xml.XmlNode field in xml.SelectNodes("//*[local-name()='Data' and @Name='FileName']"))
    if(field.InnerText.Trim().Replace('/','\\').EndsWith(suffix,StringComparison.OrdinalIgnoreCase))return true;
   return false;
  }
  static string ReadWindowsEvents(string installer,DateTime attemptedUtc){
   string since=attemptedUtc.ToUniversalTime().AddSeconds(-5).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ",System.Globalization.CultureInfo.InvariantCulture);
   string query="*[System[(EventID=3077 or EventID=3033) and TimeCreated[@SystemTime >= '"+since+"']]]";
   var request=new System.Diagnostics.Eventing.Reader.EventLogQuery("Microsoft-Windows-CodeIntegrity/Operational",System.Diagnostics.Eventing.Reader.PathType.LogName,query){ReverseDirection=true};
   var output=new StringBuilder();var watch=Stopwatch.StartNew();int count=0;
   using(var reader=new System.Diagnostics.Eventing.Reader.EventLogReader(request)){
    for(int examined=0;examined<40&&watch.Elapsed.TotalSeconds<2&&count<6;examined++){
     using(var entry=reader.ReadEvent(TimeSpan.FromMilliseconds(250))){
      if(entry==null)break;string xml=entry.ToXml();if(!MatchesInstaller(xml,installer))continue;
      output.Append("\r\nCode Integrity event ").Append(entry.Id).Append(" at ").Append(entry.TimeCreated).Append("\r\n");
      if(xml.IndexOf("0283ac0f-fff1-49ae-ada1-8a933130cad6",StringComparison.OrdinalIgnoreCase)>=0)output.Append("Policy: Windows Smart App Control (VerifiedAndReputableDesktop)\r\n");
      // Raw event fields retain the exact policy ID and rejected path in every locale.
      output.Append(xml.Substring(0,Math.Min(xml.Length,32768))).Append("\r\n");count++;
     }
    }
   }
   return count==0?"No matching Code Integrity events were available for this installer at the launch time.":output.ToString();
  }
 }
 public sealed class UpdateDownloadProgress {public long Received,Total;public int Percent {get{return Total<=0?0:(int)Math.Max(0,Math.Min(100,Received*100/Total));}}}
 public sealed class UpdateReceipt {public int Schema=1;public string PackageVersion,PreviousPackageVersion,InstalledUtc;}
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
  public Action<UpdateDownloadProgress> Progress;

  public static bool TrustedDownload(Uri uri) {
   return uri != null && uri.Scheme == "https" && uri.Port == 443 &&
    uri.UserInfo.Length == 0 && (uri.Host == "github.com" ||
    uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com" || uri.Host == "api.github.com");
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
    manifest.size < 1 || manifest.size > MaximumInstallerSize || (manifest.release_notes!=null&&manifest.release_notes.Length>48000))
    throw new IOException("Invalid update asset, checksum or size.");
  }

  public static string InstallerName(UpdateManifest manifest) { return "AstroArchive" + manifest.package_version + ".exe"; }
  public static string InstallationCache(string root){return Path.Combine(InstallCore.Root(root),"updates");}
  public static string ReleasePage(UpdateManifest manifest){Validate(manifest);return Repository+"/releases/tag/v"+manifest.package_version;}
  public string ReleaseNotes(UpdateManifest manifest){
   Validate(manifest);if(!string.IsNullOrWhiteSpace(manifest.release_notes))return manifest.release_notes;
   byte[] bytes=Fetch(new Uri("https://api.github.com/repos/arijguest/AstroArchive/releases/tags/v"+manifest.package_version),256*1024);
   if(bytes==null||bytes.Length>256*1024)throw new IOException("Release notes response is too large.");
   var release=InstallCore.Json().Deserialize<System.Collections.Generic.Dictionary<string,object>>(Encoding.UTF8.GetString(bytes));object body;
   string notes=release!=null&&release.TryGetValue("body",out body)?body as string:null;
   return string.IsNullOrWhiteSpace(notes)?"No release notes were provided for this package.":notes.Substring(0,Math.Min(notes.Length,48000));
  }
  static string ReceiptPath(string root){string path=Path.Combine(InstallCore.Root(root),"update-receipt.json");InstallCore.NoLinks(path);return path;}
  public static void RecordInstalledUpdate(string root,string previousPackage){
   var record=InstallCore.Read(root);if(record==null)throw new IOException("Cannot record an update without an active installation.");
   string path=ReceiptPath(root);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllText(temp,InstallCore.Json().Serialize(new UpdateReceipt{PackageVersion=record.PackageVersion??record.Version,PreviousPackageVersion=previousPackage,InstalledUtc=DateTime.UtcNow.ToString("o")}),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public static UpdateReceipt ConsumeInstalledUpdate(string root,string runningPackage){
   string path=ReceiptPath(root);if(!File.Exists(path))return null;if(new FileInfo(path).Length>4096)throw new IOException("Invalid update confirmation.");
   var receipt=InstallCore.Json().Deserialize<UpdateReceipt>(File.ReadAllText(path));
   if(receipt==null||receipt.Schema!=1||receipt.PackageVersion!=runningPackage)return null;
   var installed=InstallCore.Read(root);if(installed==null||(installed.PackageVersion??installed.Version)!=runningPackage)return null;
   File.Delete(path);return receipt;
  }

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
   Action<long> report=received=>{if(Progress!=null)Progress(new UpdateDownloadProgress{Received=received,Total=manifest.size});};report(0);
   var uri=new Uri(manifest.download_url ?? manifest.url);
   byte[] bytes = Fetch == Download ? DownloadProgressive(uri,manifest.size,report) : Fetch(uri,manifest.size);
   if (bytes == null || bytes.LongLength != manifest.size ||
    !string.Equals(InstallCore.Hash(bytes), manifest.sha256, StringComparison.OrdinalIgnoreCase))
    throw new IOException("Downloaded update failed SHA-256 verification.");
   report(bytes.LongLength);
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

  static byte[] Download(Uri uri,long limit){return DownloadProgressive(uri,limit,null);}
  static byte[] DownloadProgressive(Uri uri, long limit,Action<long> progress) {
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
       if(progress!=null)progress(output.Length);
      }
      return output.ToArray();
     }
    }
   }
   throw new IOException("Too many update redirects.");
  }
 }
}
