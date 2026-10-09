using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 // Cache headers only. Project assignments, target matching and GIF inheritance are reapplied.
 internal sealed class EditedHeaderCache {
  sealed class Entry {public string Path;public FileStamp Stamp;public FitsHeader Header;public long Bytes;}
  readonly Dictionary<string,LinkedListNode<Entry>> index=new Dictionary<string,LinkedListNode<Entry>>(StringComparer.OrdinalIgnoreCase);
  readonly LinkedList<Entry> recent=new LinkedList<Entry>();readonly object gate=new object();readonly long limit;
  readonly Func<string,FileStamp> stamp;readonly Func<string,FitsHeader> inspect;long bytes;
  public EditedHeaderCache(long maximumBytes=8L*1024*1024,Func<string,FileStamp> readStamp=null,Func<string,FitsHeader> readHeader=null){limit=Math.Max(0,maximumBytes);stamp=readStamp??FileStamp.Read;inspect=readHeader??(path=>Assets.Inspect(path).Header);}
  internal int Count{get{lock(gate)return index.Count;}}
  static FitsHeader Copy(FitsHeader source){return new FitsHeader{Width=source.Width,Height=source.Height,Channels=source.Channels,Bitpix=source.Bitpix,Offset=source.Offset,DataBytes=source.DataBytes,Values=new Dictionary<string,string>(source.Values,source.Values.Comparer),Comments=new Dictionary<string,string>(source.Comments,source.Comments.Comparer)};}
  void Remove(LinkedListNode<Entry> node){recent.Remove(node);index.Remove(node.Value.Path);bytes-=node.Value.Bytes;}
  public FitsHeader Get(string path,CancellationToken ct){
   ct.ThrowIfCancellationRequested();path=Path.GetFullPath(path);var before=stamp(path);
   lock(gate){LinkedListNode<Entry> found;if(index.TryGetValue(path,out found)){if(before.VerifiedUnchanged(found.Value.Stamp)){recent.Remove(found);recent.AddFirst(found);return Copy(found.Value.Header);}Remove(found);}}
   var header=inspect(path);ct.ThrowIfCancellationRequested();var after=stamp(path);
   if(header!=null&&before.VerifiedUnchanged(after)){
    long size=512+path.Length*2L+header.Values.Concat(header.Comments).Sum(pair=>64L+2L*(pair.Key.Length+(pair.Value??"").Length));
    if(size<=limit)lock(gate){LinkedListNode<Entry> previous;if(index.TryGetValue(path,out previous))Remove(previous);while(recent.Count>0&&(bytes+size>limit||recent.Count>=4096))Remove(recent.Last);var entry=new Entry{Path=path,Stamp=after.Clone(),Header=Copy(header),Bytes=size};index[path]=recent.AddFirst(entry);bytes+=size;}
   }
   return header;
  }
  public void Clear(){lock(gate){index.Clear();recent.Clear();bytes=0;}}
 }
}
