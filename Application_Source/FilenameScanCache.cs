// Filename inventory deliberately trusts names. Content verification remains opt-in.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
namespace AstroArchive {
 internal sealed class ImportName {
  public string Hash,Name,Identity,Folder,Session,SessionPath;public long Bytes;public bool ReimportAllowed;
 }
 public sealed partial class Repository {
  void RememberImportName(Frame frame,string sourcePath=null){
   if(frame==null||string.IsNullOrEmpty(frame.Hash)||string.IsNullOrEmpty(frame.OriginalName))return;
   string path=sourcePath??frame.SourcePath;if(string.IsNullOrEmpty(path))return;
   string session,relative;FilenameScanCache.Location(path,out session,out relative);
   var name=new ImportName{Hash=frame.Hash,Name=Path.GetFileName(path),Identity=frame.TelescopeIdentity??frame.Telescope??"Unknown",Folder=FilenameScanCache.Folder(path),Session=session,SessionPath=relative,Bytes=frame.Bytes};
   db.Exec("INSERT OR REPLACE INTO import_names(hash,telescope,folder,name,data) VALUES(?,?,?,?,?)",name.Hash,name.Identity,name.Folder,name.Name,Util.Serialize(name));
  }
  internal List<ImportName> ImportNames(string identity,HashSet<string> deleted,CancellationToken ct){
   if(CacheGet("import-name-inventory:1")==null)db.Transaction(()=>{
    foreach(var frame in db.Query("SELECT data FROM files").Select(Util.Deserialize<Frame>)){ct.ThrowIfCancellationRequested();RememberImportName(frame);}
    foreach(var manifest in db.Query("SELECT data FROM source_manifest WHERE status=? AND hash IN (SELECT hash FROM files)","Complete").Select(Util.Deserialize<SourceManifest>)){ct.ThrowIfCancellationRequested();RememberImportName(manifest.Metadata,manifest.Path);}
    foreach(var record in Deletions()){ct.ThrowIfCancellationRequested();RememberImportName(record.Metadata);}
    CachePut("import-name-inventory:1","ready");
   });
   var rows=db.Query("SELECT data FROM import_names WHERE telescope=? COLLATE NOCASE AND hash IN (SELECT hash FROM files)",identity).Select(Util.Deserialize<ImportName>).ToList();
   foreach(var item in db.Query("SELECT data FROM import_names WHERE telescope=? COLLATE NOCASE AND hash IN (SELECT hash FROM deleted_files) AND hash NOT IN (SELECT hash FROM files)",identity).Select(Util.Deserialize<ImportName>)){item.ReimportAllowed=!deleted.Contains(item.Hash);rows.Add(item);}
   return rows;
  }
 }
 internal sealed class FilenameScanCache {
  readonly Dictionary<string,ImportName> names=new Dictionary<string,ImportName>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,List<ImportName>> sessions=new Dictionary<string,List<ImportName>>(StringComparer.OrdinalIgnoreCase);
  readonly HashSet<string> reimportSessions=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  readonly HashSet<string> deleted;
  static readonly Regex numbered=new Regex(@"^(raw|light|frame|image|stack|stacked|capture|sub|dark|flat|bias)([_ -].*)?\d+\.(fits?|fts|xisf|ser)(\.gz)?$",RegexOptions.IgnoreCase);
  static readonly Regex date=new Regex(@"20\d{2}[-_]?\d{2}[-_]?\d{2}",RegexOptions.IgnoreCase);
  public FilenameScanCache(IEnumerable<ImportName> inventory,HashSet<string> deleted){this.deleted=deleted;var paths=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
   foreach(var item in inventory){
    if(item.ReimportAllowed){if(item.Session!=null)reimportSessions.Add(item.Session);continue;}
    if(string.IsNullOrEmpty(item.Name))continue;string key=Key(item.Name,item.Folder);ImportName prior;
    if(!names.TryGetValue(key,out prior)||deleted.Contains(prior.Hash))names[key]=item;
    if(item.Session!=null&&!deleted.Contains(item.Hash)){List<ImportName> group;if(!sessions.TryGetValue(item.Session,out group)){sessions[item.Session]=group=new List<ImportName>();paths[item.Session]=new HashSet<string>(StringComparer.OrdinalIgnoreCase);}if(paths[item.Session].Add(item.SessionPath))group.Add(item);}
   }
  }
  static string Key(string name,string folder){return numbered.IsMatch(name)&&!date.IsMatch(name)?(folder??"")+"|"+name:"*|"+name;}
  internal static string Folder(string path){string session,relative;Location(path,out session,out relative);return session==null?Path.GetFileName(Path.GetDirectoryName(path)):session+"/"+(Path.GetDirectoryName(relative)??"").Replace('\\','/');}
  internal static void Location(string path,out string session,out string relative){
   session=null;relative=null;if(string.IsNullOrEmpty(path))return;
   string directory=Path.GetDirectoryName(path);for(string p=directory;p!=null;p=Path.GetDirectoryName(p)){
    string name=Path.GetFileName(p);if(name.StartsWith("DWARF_RAW_",StringComparison.OrdinalIgnoreCase)&&date.IsMatch(name)){session=name;relative=path.Substring(p.TrimEnd('\\','/').Length).TrimStart('\\','/');return;}
   }
  }
  public bool KnownTree(string directory){string session,relative;Location(Path.Combine(directory,"probe.fit"),out session,out relative);return session!=null&&sessions.ContainsKey(session);}
  public bool TrySkip(string path,out bool excluded){ImportName found;bool match=names.TryGetValue(Key(Path.GetFileName(path),Folder(path)),out found);excluded=match&&deleted.Contains(found.Hash);return match;}
  public bool TrySkipFolder(DirectoryInfo directory,CancellationToken ct,out int files,out long bytes){
   files=0;bytes=0;List<ImportName> known;if(reimportSessions.Contains(directory.Name)||!sessions.TryGetValue(directory.Name,out known))return false;
   // One existing archived filename is enough for the user's fast session policy.
   // Probe a bounded sample; never enumerate the rest of a recognised session.
   foreach(var item in known.Take(8)){ct.ThrowIfCancellationRequested();string path=Path.GetFullPath(Path.Combine(directory.FullName,item.SessionPath));if(!Util.Within(path,directory.FullName))continue;if(File.Exists(path)){files=known.Count;bytes=known.Sum(n=>Math.Max(0,n.Bytes));return true;}}
   return false;
  }
 }
}
