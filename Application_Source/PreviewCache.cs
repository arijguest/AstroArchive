using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
namespace AstroArchive {
 // Only sampled display data is retained; this cache never writes source files.
 public sealed class PreviewCache {
  sealed class Entry {public string Key;public PreviewData Data;public long Bytes;}
  readonly LinkedList<Entry> entries=new LinkedList<Entry>();readonly object sync=new object();readonly long limit;long bytes;
  public PreviewCache(long maximumBytes=64L*1024*1024){limit=Math.Max(0,maximumBytes);}
  public long Bytes{get{lock(sync)return bytes;}}
  public int Count{get{lock(sync)return entries.Count;}}
  static string Key(string path,Frame frame){
   var file=new FileInfo(path);return Path.GetFullPath(path)+"|"+file.Length.ToString(CultureInfo.InvariantCulture)+"|"+file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)+"|"+(frame==null?"":frame.Format+"|"+frame.ImageKey+"|"+frame.ImageIndex+"|"+frame.Bayer);
  }
  public PreviewData Get(string path,Frame frame,Func<PreviewData> decode,CancellationToken ct){
   ct.ThrowIfCancellationRequested();string key=Key(path,frame);
   lock(sync){for(var node=entries.First;node!=null;node=node.Next)if(string.Equals(node.Value.Key,key,StringComparison.OrdinalIgnoreCase)){var data=node.Value.Data.Copy();entries.Remove(node);entries.AddFirst(node);return data;}}
   var decoded=decode();ct.ThrowIfCancellationRequested();long size=checked((long)decoded.Pixels.Length*sizeof(double));
   if(size<=limit&&key==Key(path,frame))lock(sync){
    for(var node=entries.First;node!=null;){var next=node.Next;if(string.Equals(node.Value.Key,key,StringComparison.OrdinalIgnoreCase)){bytes-=node.Value.Bytes;entries.Remove(node);}node=next;}
    while(bytes+size>limit&&entries.Last!=null){bytes-=entries.Last.Value.Bytes;entries.RemoveLast();}
    entries.AddFirst(new Entry{Key=key,Data=decoded.Copy(),Bytes=size});bytes+=size;
   }
   return decoded.Copy();
  }
  public void Clear(){lock(sync){entries.Clear();bytes=0;}}
 }
}
