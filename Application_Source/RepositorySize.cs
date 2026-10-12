// Disk space used by a repository, measured from the folder itself so it includes
// Edited copies, the Dump inbox and archive data as well as indexed captures.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace AstroArchive {
 public sealed class RepositorySizeInfo {
  public long Total,Captures,Edited,Dump,ArchiveData;public int Files;public bool Partial;
  public string Summary{get{return RepositorySize.Format(Total)+(Partial?"+":"");}}
  public string Breakdown{get{
   var parts=new List<string>{"Captures "+RepositorySize.Format(Captures)};if(Edited>0)parts.Add("Edited "+RepositorySize.Format(Edited));if(Dump>0)parts.Add("Dump "+RepositorySize.Format(Dump));parts.Add("Archive data "+RepositorySize.Format(ArchiveData));
   return "Repository size "+RepositorySize.Format(Total)+" · "+Files.ToString("N0")+" files\n"+string.Join(" · ",parts)+(Partial?"\nSome folders could not be read; the size may be larger.":"");
  }}
 }
 public static class RepositorySize {
  public static string Format(long bytes){
   double value=bytes;string[] units={"bytes","KB","MB","GB","TB"};int unit=0;while(value>=1024&&unit<units.Length-1){value/=1024;unit++;}
   return unit==0?bytes.ToString("N0",CultureInfo.CurrentCulture)+" bytes":value.ToString(value>=100?"0":"0.#",CultureInfo.CurrentCulture)+" "+units[unit];
  }
  // Linked folders are not followed, so the size never counts storage outside the archive.
  public static RepositorySizeInfo Measure(string root,string meta,string edited,string dump,CancellationToken ct){
   var info=new RepositorySizeInfo();var folders=new Stack<string>();folders.Push(root);
   while(folders.Count>0){
    ct.ThrowIfCancellationRequested();string folder=folders.Pop();IEnumerable<FileSystemInfo> entries;
    try{entries=new DirectoryInfo(folder).EnumerateFileSystemInfos();}catch(Exception e){if(e is IOException||e is UnauthorizedAccessException||e is System.Security.SecurityException){info.Partial=true;continue;}throw;}
    try{
     foreach(var entry in entries){
      ct.ThrowIfCancellationRequested();if((entry.Attributes&FileAttributes.ReparsePoint)!=0&&(entry.Attributes&FileAttributes.Directory)!=0)continue;
      if((entry.Attributes&FileAttributes.Directory)!=0){folders.Push(entry.FullName);continue;}
      long length=((FileInfo)entry).Length;info.Total+=length;info.Files++;
      if(Util.Within(entry.FullName,edited))info.Edited+=length;else if(Util.Within(entry.FullName,dump))info.Dump+=length;else if(Util.Within(entry.FullName,meta))info.ArchiveData+=length;else info.Captures+=length;
     }
    }catch(Exception e){if(e is IOException||e is UnauthorizedAccessException||e is System.Security.SecurityException)info.Partial=true;else throw;}
   }
   return info;
  }
 }
}
