using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroArchive {
 // A streaming GIF89a writer with one scene-derived palette. A shared palette
 // avoids colour flicker during transitions and keeps memory bounded to a frame.
 internal sealed class AnalyticsGif:IDisposable {
  readonly BinaryWriter output;readonly int width,height;readonly byte[] lookup=new byte[32768];readonly byte[] indexed;bool finished;
  public static int[] Palette(IEnumerable<byte[]> samples){
   var histogram=new int[32768];foreach(var image in samples)for(int i=0;i<image.Length;i+=16){int bin=((image[i+2]>>3)<<10)|((image[i+1]>>3)<<5)|(image[i]>>3);histogram[bin]++;}
   var boxes=new List<List<int>>{Enumerable.Range(0,32768).Where(i=>histogram[i]>0).ToList()};
   while(boxes.Count<240){
    var box=boxes.Where(b=>b.Count>1).OrderByDescending(b=>b.Sum(i=>(long)histogram[i])*(1+Range(b,Channel(b)))).FirstOrDefault();if(box==null)break;
    int shift=Channel(box);box.Sort((a,b)=>((a>>shift)&31).CompareTo((b>>shift)&31));long half=box.Sum(i=>(long)histogram[i])/2,total=0;int cut=1;
    for(int i=0;i<box.Count-1;i++){total+=histogram[box[i]];cut=i+1;if(total>=half)break;}boxes.Remove(box);boxes.Add(box.Take(cut).ToList());boxes.Add(box.Skip(cut).ToList());
   }
   var colours=new List<int>{0x0C1220,0xFFFFFF,0x90B8FF,0x55DFEA,0xC19AFF,0xF0C36A,0x4F9BFA,0xED9ACB,0x9DD7B0,0xA6B4CC,0xE9EEF8,0x202B43,0x5951D6,0x008477,0x151E2E,0x000000};
   foreach(var box in boxes){long total=box.Sum(i=>(long)histogram[i]);if(total==0)continue;int colour=0;foreach(int shift in new[]{10,5,0}){long sum=box.Sum(i=>(long)(((i>>shift)&31)*8+4)*histogram[i]);colour=(colour<<8)|(int)(sum/total);}colours.Add(colour);}while(colours.Count<256)colours.Add(colours.Last());return colours.Take(256).ToArray();
  }
  static int Range(List<int> box,int shift){return box.Max(i=>(i>>shift)&31)-box.Min(i=>(i>>shift)&31);}
  static int Channel(List<int> box){return new[]{10,5,0}.OrderByDescending(s=>Range(box,s)).First();}
  public AnalyticsGif(string path,int width,int height,int[] palette){
   this.width=width;this.height=height;indexed=new byte[width*height];
   for(int bin=0;bin<lookup.Length;bin++){
    int r=((bin>>10)&31)*8+4,g=((bin>>5)&31)*8+4,b=(bin&31)*8+4,best=int.MaxValue,index=0;
    for(int i=0;i<256;i++){int dr=r-(palette[i]>>16),dg=g-((palette[i]>>8)&255),db=b-(palette[i]&255),error=dr*dr*2+dg*dg*4+db*db;if(error<best){best=error;index=i;}}lookup[bin]=(byte)index;
   }
   output=new BinaryWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None));output.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"));output.Write((ushort)width);output.Write((ushort)height);output.Write((byte)0xF7);output.Write((byte)0);output.Write((byte)0);
   foreach(int colour in palette){output.Write((byte)(colour>>16));output.Write((byte)(colour>>8));output.Write((byte)colour);}output.Write(new byte[]{0x21,0xFF,11});output.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));output.Write(new byte[]{3,1,0,0,0});
  }
  static readonly int[] Dither={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
  public void Add(byte[] bgra,int delay){
   if(finished)throw new InvalidOperationException("Animation is already complete.");if(bgra.Length!=width*height*4)throw new ArgumentException("Incorrect animation frame size.");
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){
    int p=(y*width+x)*4,d=Dither[(y%4)*4+x%4]/3-2,r=Math.Max(0,Math.Min(255,bgra[p+2]+d)),g=Math.Max(0,Math.Min(255,bgra[p+1]+d)),b=Math.Max(0,Math.Min(255,bgra[p]+d));indexed[y*width+x]=lookup[((r>>3)<<10)|((g>>3)<<5)|(b>>3)];
   }
   output.Write(new byte[]{0x21,0xF9,4,4});output.Write((ushort)Math.Max(1,delay));output.Write(new byte[]{0,0,0x2C});output.Write((ushort)0);output.Write((ushort)0);output.Write((ushort)width);output.Write((ushort)height);output.Write((byte)0);output.Write((byte)8);
   var block=new byte[255];int count=0,bits=0;uint pending=0;Action<int,int> code=(value,size)=>{pending|=(uint)value<<bits;bits+=size;while(bits>=8){block[count++]=(byte)pending;pending>>=8;bits-=8;if(count==255){output.Write((byte)count);output.Write(block,0,count);count=0;}}};
   var dictionary=new Dictionary<int,int>();int next=258,sizeBits=9,prefix=indexed[0];code(256,sizeBits);
   for(int i=1;i<indexed.Length;i++){
    int value=indexed[i],key=(prefix<<8)|value,found;if(dictionary.TryGetValue(key,out found)){prefix=found;continue;}
    code(prefix,sizeBits);if(next==(1<<sizeBits)&&sizeBits<12)sizeBits++;if(next<4096)dictionary[key]=next++;else{code(256,sizeBits);dictionary.Clear();next=258;sizeBits=9;}prefix=value;
   }
   code(prefix,sizeBits);if(next==(1<<sizeBits)&&sizeBits<12)sizeBits++;code(257,sizeBits);if(bits>0)block[count++]=(byte)pending;if(count>0){output.Write((byte)count);output.Write(block,0,count);}output.Write((byte)0);
  }
  public void Complete(){if(finished)return;output.Write((byte)0x3B);output.Flush();finished=true;}
  public void Dispose(){output.Dispose();}
 }
}
