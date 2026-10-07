// Local spill storage keeps discovery independent of slow metadata reads.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AstroArchive {
 internal sealed class ScanEntry {
  public string Path;public FileStamp Enumerated;
  public static ScanEntry From(FileInfo info){return new ScanEntry{Path=info.FullName,Enumerated=new FileStamp{Size=info.Length,Modified=info.LastWriteTimeUtc.Ticks,Created=info.CreationTimeUtc.Ticks,Attributes=(int)info.Attributes,Identity=""}};}
 }
 internal sealed class ScanOverflow:IDisposable {
  readonly string path;BinaryWriter output;
  public ScanOverflow(string directory){path=System.IO.Path.Combine(directory,"scan-"+Guid.NewGuid().ToString("N")+".pending");}
  public void Add(ScanEntry entry){if(output==null)output=new BinaryWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536));output.Write(entry.Path);output.Write(entry.Enumerated.Size);output.Write(entry.Enumerated.Modified);output.Write(entry.Enumerated.Created);output.Write(entry.Enumerated.Attributes);}
  public void Seal(){if(output!=null){output.Dispose();output=null;}}
  public IEnumerable<ScanEntry> Read(CancellationToken ct){if(!File.Exists(path))yield break;using(var input=new BinaryReader(File.OpenRead(path))){while(input.BaseStream.Position<input.BaseStream.Length){ct.ThrowIfCancellationRequested();yield return new ScanEntry{Path=input.ReadString(),Enumerated=new FileStamp{Size=input.ReadInt64(),Modified=input.ReadInt64(),Created=input.ReadInt64(),Attributes=input.ReadInt32(),Identity=""}};}}}
  public void Dispose(){try{Seal();}finally{Repository.TryRemove(path);}}
 }
 internal sealed class HeaderCacheEntry {public FileStamp Stamp;public FitsHeader Header;public AssetInfo Asset;public string Version;}
 internal sealed class MetadataHeaderCache {
  const int Limit=2048,MaxBytes=16*1024*1024;readonly string path;readonly object gate=new object();
  readonly Dictionary<string,string> entries=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);readonly Queue<string> order=new Queue<string>();int bytes;bool dirty;
  public MetadataHeaderCache(string directory,string root){path=Path.Combine(directory,"headers180-"+Util.HashText(root.ToLowerInvariant())+".json");try{if(File.Exists(path)&&new FileInfo(path).Length<=MaxBytes){var stored=Util.Deserialize<Dictionary<string,string>>(File.ReadAllText(path));foreach(var pair in stored.Take(Limit)){entries[pair.Key]=pair.Value;order.Enqueue(pair.Key);bytes+=pair.Key.Length*2+pair.Value.Length*2;}}}catch{entries.Clear();order.Clear();bytes=0;}}
  public FitsHeader Get(string name,FileStamp stamp){var asset=GetAsset(name,stamp);return asset==null?null:asset.Header;}
  public void Put(string name,FileStamp stamp,FitsHeader header){PutAsset(name,stamp,new AssetInfo{Format="FITS",Header=header});}
  public AssetInfo GetAsset(string name,FileStamp stamp){if(!stamp.Reliable||stamp.Cloud)return null;lock(gate){string data;if(!entries.TryGetValue(name,out data))return null;try{var item=Util.Deserialize<HeaderCacheEntry>(data);return stamp.VerifiedUnchanged(item.Stamp)&&item.Version==Assets.ClassificationVersion?item.Asset:null;}catch{return null;}}}
  public void PutAsset(string name,FileStamp stamp,AssetInfo asset){if(!stamp.Reliable||stamp.Cloud)return;string data=Util.Serialize(new HeaderCacheEntry{Stamp=stamp,Header=asset.Header,Asset=asset,Version=Assets.ClassificationVersion});if(data.Length>32768)return;lock(gate){string old;if(entries.TryGetValue(name,out old))bytes-=name.Length*2+old.Length*2;else order.Enqueue(name);entries[name]=data;bytes+=name.Length*2+data.Length*2;dirty=true;while(entries.Count>Limit||bytes>MaxBytes){string key=order.Dequeue();if(entries.TryGetValue(key,out old)){bytes-=key.Length*2+old.Length*2;entries.Remove(key);}}}}
  public void Flush(){lock(gate){if(dirty){Util.AtomicText(path,Util.Serialize(entries));dirty=false;}}}
 }
}
