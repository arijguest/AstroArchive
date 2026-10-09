// Explicit selection before discovery. Paths stay relative to one source's metadata context.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
  public string ConfirmationText(string make){
   string warning=WholeSource?"You selected the entire telescope drive. This can take a long time on crowded storage.":Files.Count>=LargeFileSelection?"You selected "+Files.Count+" files. A large import can take a long time.":"A full scan and import may take a while, especially for large folders.";
   return "Import from "+TelescopeBrand(make)+"?\n\nSelection: "+Summary+".\n\n"+warning+" Selected captures will be read and checked before eligible files are copied. Selected folders include their subfolders. Current import exclusions still apply; flagged captures remain for review.\n\nFor a faster scan, choose only the target folders or files you need. Originals will remain on the telescope. You can cancel; completed imports are retained.\n\nContinue with the full scan and import?";
  }
 }
}
