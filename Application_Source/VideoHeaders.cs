using System;
using System.IO;
using System.Text;

namespace AstroArchive {
 // Container metadata only: seek over media payloads and bound both bytes and nodes.
 public static class VideoHeaders {
  sealed class Reader {
   public readonly Stream Stream;readonly Action<int> counted;int bytes,nodes;
   public Reader(Stream stream,Action<int> counter){Stream=stream;counted=counter;}
   public byte[] Read(int count){if(count<0||count>262144-bytes)throw new InvalidDataException("Video metadata limit reached.");var data=new byte[count];int offset=0;while(offset<count){int n=Stream.Read(data,offset,count-offset);if(n==0)throw new EndOfStreamException();bytes+=n;offset+=n;if(counted!=null)counted(n);}return data;}
   public void Node(){if(++nodes>4096)throw new InvalidDataException("Video metadata limit reached.");}
  }
  public static double? Duration(string path,Action<int> counted=null){
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,4096)){
    var read=new Reader(stream,counted);double? value=null;
    try{string ext=Assets.Extension(path);if(ext==".avi")value=Avi(read);else if(ext==".mp4"||ext==".mov"||ext==".m4v")value=Mp4(read,stream.Length,0);else if(ext==".wmv")value=Asf(read);else if(ext==".mkv")value=Matroska(read,stream.Length,0);}
    catch(EndOfStreamException){}catch(InvalidDataException){}catch(OverflowException){}
    return value.HasValue&&value.Value>0&&!double.IsNaN(value.Value)&&!double.IsInfinity(value.Value)?value:null;
   }
  }
  static string Text(byte[] b,int start,int count){return Encoding.ASCII.GetString(b,start,count);}
  static ulong Number(byte[] b,int start,int count,bool big){ulong n=0;for(int i=0;i<count;i++)n=(n<<8)|b[start+(big?i:count-1-i)];return n;}
  static double? Mp4(Reader r,long end,int depth){
   if(depth>4)return null;
   while(r.Stream.Position<=end-8){r.Node();long start=r.Stream.Position;var h=r.Read(8);ulong size=Number(h,0,4,true);string type=Text(h,4,4);int header=8;if(size==1){size=Number(r.Read(8),0,8,true);header=16;}else if(size==0)size=(ulong)(end-start);if(size<(ulong)header||size>(ulong)(end-start))return null;long next=start+(long)size;
    if(type=="moov"){var duration=Mp4(r,next,depth+1);if(duration.HasValue)return duration;}
    else if(type=="mvhd"){
     if(next-r.Stream.Position<4)return null;var version=r.Read(4);int count=version[0]==0?16:version[0]==1?28:0;if(count==0||next-r.Stream.Position<count)return null;var body=r.Read(count);ulong scale=Number(body,version[0]==0?8:16,4,true),ticks=Number(body,version[0]==0?12:20,version[0]==0?4:8,true);
     if(scale>0&&ticks>0&&ticks!=(version[0]==0?uint.MaxValue:ulong.MaxValue))return ticks/(double)scale;
    }
    r.Stream.Position=next;
   }return null;
  }
  sealed class AviTiming {public ulong Microseconds,Frames,ExtendedFrames,Scale,Rate,StreamFrames;}
  static double? Avi(Reader r){
   var h=r.Read(12);if(Text(h,0,4)!="RIFF"||Text(h,8,4)!="AVI ")return null;ulong size=Number(h,4,4,false);if(size<4||size>(ulong)(r.Stream.Length-8))return null;var timing=new AviTiming();AviChunks(r,8+(long)size,0,timing);
   ulong frames=timing.ExtendedFrames>0?timing.ExtendedFrames:timing.StreamFrames;if(frames>0&&timing.Rate>0&&timing.Scale>0)return frames*(double)timing.Scale/timing.Rate;
   frames=timing.ExtendedFrames>0?timing.ExtendedFrames:timing.Frames;return frames>0&&timing.Microseconds>0?(double?)(frames*(double)timing.Microseconds/1000000):null;
  }
  static void AviChunks(Reader r,long end,int depth,AviTiming timing){
   if(depth>8)return;
   while(r.Stream.Position<=end-8){r.Node();var h=r.Read(8);string type=Text(h,0,4);long size=(long)Number(h,4,4,false),start=r.Stream.Position;if(size>end-start)return;long next=start+size;
    if(type=="LIST"&&size>=4){string list=Text(r.Read(4),0,4);if(list=="hdrl"||list=="strl"||list=="odml")AviChunks(r,next,depth+1,timing);}
    else if(type=="avih"&&size>=20){var data=r.Read(20);timing.Microseconds=Number(data,0,4,false);timing.Frames=Number(data,16,4,false);}
    else if(type=="strh"&&size>=36){var data=r.Read(36);if(Text(data,0,4)=="vids"){timing.Scale=Number(data,20,4,false);timing.Rate=Number(data,24,4,false);timing.StreamFrames=Number(data,32,4,false);}}
    else if(type=="dmlh"&&size>=4)timing.ExtendedFrames=Number(r.Read(4),0,4,false);
    r.Stream.Position=next+(size&1);if(depth==0&&type=="LIST"&&timing.Microseconds>0)return;
   }
  }
  static double? Asf(Reader r){
   var h=r.Read(30);if(new Guid(Sub(h,0,16))!=new Guid("75B22630-668E-11CF-A6D9-00AA0062CE6C"))return null;ulong size=Number(h,16,8,false);if(size<30||size>(ulong)r.Stream.Length)return null;long end=(long)size;
   while(r.Stream.Position<=end-24){r.Node();var obj=r.Read(24);ulong length=Number(obj,16,8,false);if(length<24||length>(ulong)(end-r.Stream.Position+24))return null;long next=r.Stream.Position+(long)length-24;
    if(new Guid(Sub(obj,0,16))==new Guid("8CABDCA1-A947-11CF-8EE4-00C00C205365")&&length>=104){var data=r.Read(80);if((Number(data,64,4,false)&1)!=0)return null;return Number(data,40,8,false)/10000000.0-Number(data,56,8,false)/1000.0;}
    r.Stream.Position=next;
   }return null;
  }
  static byte[] Sub(byte[] b,int start,int count){var result=new byte[count];Array.Copy(b,start,result,0,count);return result;}
  static ulong VInt(Reader r,bool id,out bool unknown){byte first=r.Read(1)[0];int length=1,mask=128;while((first&mask)==0&&length<8){length++;mask>>=1;}if((first&mask)==0||(id&&length>4))throw new InvalidDataException("Invalid EBML integer.");ulong value=(ulong)(id?first:first&(mask-1));if(length>1)value=(value<<(8*(length-1)))|Number(r.Read(length-1),0,length-1,true);unknown=!id&&value==((1UL<<(7*length))-1);return value;}
  static double? Matroska(Reader r,long end,int depth){
   if(depth>3)return null;ulong scale=1000000;double? ticks=null;
   while(r.Stream.Position<end){r.Node();bool unknown;ulong id=VInt(r,true,out unknown),size=VInt(r,false,out unknown);if(!unknown&&size>(ulong)(end-r.Stream.Position))return null;long next=unknown?end:r.Stream.Position+(long)size;
    if(id==0x18538067||id==0x1549A966){var duration=Matroska(r,next,depth+1);if(duration.HasValue)return duration;}
    else if(id==0x2AD7B1&&size>0&&size<=8)scale=Number(r.Read((int)size),0,(int)size,true);
    else if(id==0x4489&&(size==4||size==8)){var data=r.Read((int)size);if(BitConverter.IsLittleEndian)Array.Reverse(data);ticks=size==4?BitConverter.ToSingle(data,0):BitConverter.ToDouble(data,0);}
    r.Stream.Position=next;
   }return ticks.HasValue?(double?)(ticks.Value*scale/1000000000.0):null;
  }
  internal static double? SerDuration(Stream stream,long imageBytes,int frames,Action<int> counted){
   // SER has no fixed playback rate. Use its measured capture timestamp span.
   if(frames<2||imageBytes>stream.Length-178||frames>(stream.Length-178-imageBytes)/8)return null;var r=new Reader(stream,counted);stream.Position=178+imageBytes;long first=(long)Number(r.Read(8),0,8,false);stream.Position=178+imageBytes+((long)frames-1)*8;long last=(long)Number(r.Read(8),0,8,false);return first>0&&last>first&&last<=DateTime.MaxValue.Ticks?(double?)((last-first)/10000000.0):null;
  }
 }
}
