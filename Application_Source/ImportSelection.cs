// Explicit selection before discovery. Paths stay relative to one source's metadata context.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class ImportSelection {
  public const int LargeFileSelection=500;
  public string SourceRoot{get;private set;}
  public IList<string> Folders{get;private set;}public IList<string> Files{get;private set;}
  public bool WholeSource{get{return Folders.Any(p=>SamePath(p,SourceRoot));}}
  public string Summary{get{return WholeSource?"the entire telescope drive":Folders.Count+" folder"+(Folders.Count==1?"":"s")+" and "+Files.Count+" file"+(Files.Count==1?"":"s");}}
  public IEnumerable<string> Paths{get{return Folders.Concat(Files);}}
  static bool SamePath(string a,string b){return a.TrimEnd('\\','/').Equals(b.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase);}
  public static string TelescopeBrand(string make){return make=="DWARFLAB"?"Dwarflab":make=="Seestar"?"Seestar":"connected telescope";}
  public static ImportSelection Create(string root,IEnumerable<string> paths){
   if(string.IsNullOrWhiteSpace(root)||!Directory.Exists(root))throw new IOException("The telescope drive is unavailable. Reconnect it and try again.");
   string source=Path.GetFullPath(root);var folders=new List<string>();var files=new List<string>();var checkedFolders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(string value in paths??Enumerable.Empty<string>()){
    if(string.IsNullOrWhiteSpace(value)||!Path.IsPathRooted(value))throw new IOException("Choose folders or files inside the connected telescope drive.");
    string path=Path.GetFullPath(value);if(!Util.Within(path,source))throw new IOException("Choose folders or files inside the connected telescope drive: "+source);
    bool folder=Directory.Exists(path);if(!folder&&!File.Exists(path))throw new IOException("A selected item is unavailable. Choose it again: "+path);
    string directory=folder?path:Path.GetDirectoryName(path);
    for(string p=directory;p!=null&&Util.Within(p,source);p=Path.GetDirectoryName(p)){
     if(!checkedFolders.Add(p))break;
     if(!FileStamp.CanTraverse(new DirectoryInfo(p)))throw new IOException("Linked or unavailable folders cannot be imported: "+p);
     if(!SamePath(p,source)&&SessionScanCache.SystemFolder(Path.GetFileName(p)))throw new IOException("Choose capture folders rather than application or system folders.");
     if(SamePath(p,source))break;
    }
    if(folder)folders.Add(path);else{
     if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked files cannot be imported: "+path);
     if(!Util.IsImageAsset(path))throw new IOException("This file is not a supported capture. Choose its folder to include associated metadata: "+Path.GetFileName(path));
     files.Add(path);
    }
   }
   var selected=new List<string>();foreach(string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p.Length))if(!selected.Any(p=>Util.Within(folder,p)))selected.Add(folder);
   files=files.Distinct(StringComparer.OrdinalIgnoreCase).Where(p=>!selected.Any(d=>Util.Within(p,d))).ToList();
   if(selected.Count+files.Count==0)throw new IOException("Select at least one capture folder or file.");
   return new ImportSelection{SourceRoot=source,Folders=selected.AsReadOnly(),Files=files.AsReadOnly()};
  }
  // Count names only, stopping at the prompt threshold; never open image data.
  public int CaptureCountUpTo(int limit,CancellationToken ct){
   if(limit<=0)throw new ArgumentOutOfRangeException("limit");ct.ThrowIfCancellationRequested();int count=Math.Min(Files.Count,limit);if(count==limit)return count;
   var pending=new Stack<string>(Folders);while(pending.Count>0){ct.ThrowIfCancellationRequested();var directory=new DirectoryInfo(pending.Pop());if(!FileStamp.CanTraverse(directory))continue;
    try{foreach(var entry in directory.EnumerateFileSystemInfos()){ct.ThrowIfCancellationRequested();var folder=entry as DirectoryInfo;if(folder!=null){if(!SessionScanCache.SystemFolder(folder.Name)&&FileStamp.CanTraverse(folder))pending.Push(folder.FullName);}else if((entry.Attributes&FileAttributes.ReparsePoint)==0&&Util.IsImageAsset(entry.Name)&&++count>=limit)return count;}}
    catch(IOException){}catch(UnauthorizedAccessException){}
   }return count;
  }
 }
}
