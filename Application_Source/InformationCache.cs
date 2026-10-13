using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace AstroArchive {
 // Revision-keyed blobs plus atomic lookup pointers. Never inside an archive/install.
 public sealed class InformationCache {
  readonly object gate=new object();readonly string directory;readonly Dictionary<string,int> active=new Dictionary<string,int>();
  public int LimitMiB=100;public int Generation {get;private set;}
  public InformationCache(string directory){this.directory=Path.GetFullPath(directory);}
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=4*1024*1024,RecursionLimit=24};}
  public static string Lookup(string provider,string id,string language){return Util.HashText(provider+"|"+id+"|"+language+"|schema:1|policy:1");}
  string FilePath(string key,string extension){if(key.Length!=64||key.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid cache key.");string path=Path.Combine(directory,key+extension);AstroArchive.Installation.InstallCore.NoLinks(path);return path;}
  public void Protect(string key,bool value){lock(gate){int count;active.TryGetValue(key,out count);if(value)active[key]=count+1;else if(count>1)active[key]=count-1;else active.Remove(key);}}
  public DownloadedInformation Read(string provider,string id,string language){lock(gate){
   try{string lookup=FilePath(Lookup(provider,id,language),".idx");if(!File.Exists(lookup)||new FileInfo(lookup).Length>128)return null;
    string key=File.ReadAllText(lookup),path=FilePath(key,".json");if(!File.Exists(path)||new FileInfo(path).Length>4*1024*1024)return null;
    var record=Json().Deserialize<DownloadedInformation>(File.ReadAllText(path));if(record==null||record.provider!=provider||record.language!=language||!record.Valid(id)||Util.HashText(Lookup(provider,id,language)+"|"+record.revision)!=key)return null;
    File.SetLastWriteTimeUtc(path,DateTime.UtcNow);return record;
   }catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}catch(UnauthorizedAccessException){}return null;
  }}
  public void Write(DownloadedInformation record,int generation){if(record==null||!record.Valid(record.object_id))throw new InvalidDataException("Unapproved downloaded information.");lock(gate){
   if(generation!=Generation)return;AstroArchive.Installation.InstallCore.NoLinks(directory);Directory.CreateDirectory(directory);
   string lookup=Lookup(record.provider,record.object_id,record.language),key=Util.HashText(lookup+"|"+record.revision),json=Json().Serialize(record);if(Encoding.UTF8.GetByteCount(json)>4*1024*1024)throw new InvalidDataException("Information exceeds the cache record limit.");
   Atomic(FilePath(key,".json"),json);Atomic(FilePath(lookup,".idx"),key);Trim();
  }}
  static void Atomic(string path,string text){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public long Usage {get{lock(gate){if(!Directory.Exists(directory))return 0;return Files().Sum(f=>f.Length);}}}
  IEnumerable<FileInfo> Files(){AstroArchive.Installation.InstallCore.NoLinks(directory);return new DirectoryInfo(directory).EnumerateFiles().Take(12000).Where(f=>f.Extension==".json"||f.Extension==".idx").ToArray();}
  public void Trim(){lock(gate){if(!Directory.Exists(directory))return;var files=Files().ToArray();long size=files.Sum(f=>f.Length),limit=Math.Max(10,Math.Min(500,LimitMiB))*1048576L;
   var protectedBlobs=new HashSet<string>();foreach(string lookup in active.Keys)try{string pointer=FilePath(lookup,".idx");if(File.Exists(pointer))protectedBlobs.Add(File.ReadAllText(pointer));}catch(IOException){}
   foreach(var file in files.Where(f=>f.Extension==".json").OrderBy(f=>f.LastWriteTimeUtc)){if(size<=limit)break;if(protectedBlobs.Contains(Path.GetFileNameWithoutExtension(file.Name)))continue;AstroArchive.Installation.InstallCore.NoLinks(file.FullName);size-=file.Length;file.Delete();}
   foreach(var pointer in files.Where(f=>f.Extension==".idx"))try{AstroArchive.Installation.InstallCore.NoLinks(pointer.FullName);if(pointer.Length>128||!File.Exists(FilePath(File.ReadAllText(pointer.FullName),".json")))pointer.Delete();}catch(ArgumentException){}catch(IOException){}
  }}
  public void Clear(){lock(gate){Generation++;if(!Directory.Exists(directory))return;foreach(var file in Files()){AstroArchive.Installation.InstallCore.NoLinks(file.FullName);file.Delete();}}}
 }
}
