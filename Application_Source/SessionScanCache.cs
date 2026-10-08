// Fast session inventory: names locate durable imports, never prove completeness.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 internal sealed class SessionScanCache {
  readonly string root,volume,identity,model;readonly Dictionary<string,SourceManifest> manifests;readonly Dictionary<string,Frame> archive;readonly Repository repository;
  readonly HashSet<string> folders=new HashSet<string>(StringComparer.OrdinalIgnoreCase),ancestors=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,FileStamp> sidecars=new Dictionary<string,FileStamp>(StringComparer.OrdinalIgnoreCase);
  string currentFolder;readonly Dictionary<string,FileInfo> adjacent=new Dictionary<string,FileInfo>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,string> shots=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  public SessionScanCache(Repository repository,string root,string identity,string model,Dictionary<string,SourceManifest> manifests,Dictionary<string,Frame> archive){
   this.repository=repository;this.root=root;this.identity=identity;this.model=model;this.manifests=manifests;this.archive=archive;volume=FileStamp.VolumeIdentity(root);
   foreach(var item in manifests.Values.Where(m=>m.Status=="Complete"&&m.Path!=null&&Util.Within(m.Path,root))){string directory=Path.GetDirectoryName(item.Path);folders.Add(directory);for(string p=directory;p!=null&&Util.Within(p,root);p=Path.GetDirectoryName(p))ancestors.Add(p);}
  }
  public bool KnownTree(string directory){return ancestors.Contains(directory);}
  public void BeginFolder(DirectoryInfo directory,CancellationToken ct){sidecars.Clear();adjacent.Clear();currentFolder=directory.FullName;
   if(string.IsNullOrEmpty(volume)||!folders.Contains(currentFolder))return;
   // Enumerate companion names in a batch: avoid six USB metadata probes per image.
   foreach(var info in directory.EnumerateFiles()){ct.ThrowIfCancellationRequested();string extension=Path.GetExtension(info.Name);if(extension.Equals(".json",StringComparison.OrdinalIgnoreCase)||extension.Equals(".txt",StringComparison.OrdinalIgnoreCase)||extension.Equals(".log",StringComparison.OrdinalIgnoreCase))adjacent[info.FullName]=info;}
  }
  // Removable FAT/exFAT lacks trustworthy change-time. This is an inventory hint,
  // not checksum verification. Full rescan bypasses it and reads every source.
  internal static bool Matches(SourceManifest old,Frame indexed,ScanEntry entry,FileStamp current,string volume,string identity,string model){
   return old!=null&&old.Status=="Complete"&&old.Source!=null&&old.Metadata!=null&&indexed!=null&&old.Hash==indexed.Hash&&
    !string.IsNullOrEmpty(volume)&&!string.IsNullOrEmpty(old.Source.Identity)&&old.Source.Identity.StartsWith(volume+":",StringComparison.OrdinalIgnoreCase)&&
    string.Equals(old.Metadata.TelescopeIdentity??old.Metadata.Telescope,identity,StringComparison.OrdinalIgnoreCase)&&
    old.Metadata.ClassificationVersion==Assets.ClassificationVersion&&(model=="Auto"||old.Metadata.Model==model)&&
    !entry.Enumerated.Cloud&&!old.Source.Cloud&&entry.Enumerated.ContentSame(old.Source)&&
    (!old.Source.Reliable||current!=null&&current.VerifiedUnchanged(old.Source));
  }
  FileStamp Sidecar(string path){FileStamp stamp;if(sidecars.TryGetValue(path,out stamp))return stamp;if(string.Equals(Path.GetDirectoryName(path),currentFolder,StringComparison.OrdinalIgnoreCase)){FileInfo info;stamp=adjacent.TryGetValue(path,out info)?FileStamp.Read(info):null;}else stamp=File.Exists(path)?FileStamp.Read(path):null;sidecars[path]=stamp;return stamp;}
  static bool Unchanged(FileStamp current,FileStamp previous){return current!=null&&previous!=null&&!current.Cloud&&!previous.Cloud&&(previous.Reliable?current.VerifiedUnchanged(previous):current.ContentSame(previous));}
  string ShotsPath(string directory){string found;if(shots.TryGetValue(directory,out found))return found;string p=directory;for(int i=0;i<4&&p!=null&&Util.Within(p,root);i++,p=Path.GetDirectoryName(p)){string candidate=Path.Combine(p,"shotsInfo.json");if(string.Equals(p,currentFolder,StringComparison.OrdinalIgnoreCase)?adjacent.ContainsKey(candidate):File.Exists(candidate)){found=candidate;break;}}shots[directory]=found;return found;}
  bool MetadataUnchanged(SourceManifest old,ScanEntry entry){
   var frame=old.Metadata;string nearest=ShotsPath(Path.GetDirectoryName(entry.Path));
   if(!string.Equals(nearest,frame.SourceMetadataPath,StringComparison.OrdinalIgnoreCase)||nearest!=null&&!Unchanged(Sidecar(nearest),frame.SourceMetadataStamp))return false;
   var previous=frame.AssociatedFiles??new List<AssociatedFile>();int count=0;
   foreach(string name in AssociatedMetadata.Names(entry.Path)){string path=Path.Combine(Path.GetDirectoryName(entry.Path),name);FileStamp current=Sidecar(path);if(current==null||current.Size>16*1024*1024)continue;count++;var prior=previous.FirstOrDefault(a=>string.Equals(a.SourcePath,path,StringComparison.OrdinalIgnoreCase));if(prior==null||!Unchanged(current,prior.Stamp))return false;}
   return count==previous.Count;
  }
  public bool TrySkip(ScanEntry entry){
   if(string.IsNullOrEmpty(volume)||!folders.Contains(Path.GetDirectoryName(entry.Path)))return false;
   SourceManifest old;Frame indexed;if(!manifests.TryGetValue(entry.Path,out old)||string.IsNullOrEmpty(old.Hash)||!archive.TryGetValue(old.Hash,out indexed))return false;
   try{FileStamp current=old.Source!=null&&old.Source.Reliable?FileStamp.Read(new FileInfo(entry.Path),entry.Enumerated):null;
    if(!Matches(old,indexed,entry,current,volume,identity,model)||!MetadataUnchanged(old,entry))return false;
    string copy=repository.FilePath(indexed);return File.Exists(copy)&&FileStamp.Read(copy).VerifiedUnchanged(indexed.RepositoryStamp??old.Copy);
   }catch{return false;}
  }
  public static bool SystemFolder(string name){return new[]{".astroarchive","$RECYCLE.BIN","System Volume Information",".Spotlight-V100",".Trashes"}.Contains(name,StringComparer.OrdinalIgnoreCase);}
 }
}
