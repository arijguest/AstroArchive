using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace AstroArchive {
 public partial class Tests {
  static byte[] VideoJoin(params byte[][] parts){return parts.SelectMany(p=>p).ToArray();}
  static byte[] VideoNumber(ulong n,int count,bool big=true){var b=new byte[count];for(int i=0;i<count;i++){b[big?count-1-i:i]=(byte)n;n>>=8;}return b;}
  static byte[] VideoText(string text){return Encoding.ASCII.GetBytes(text);}
  static byte[] VideoBox(string type,params byte[][] parts){var data=VideoJoin(parts);return VideoJoin(VideoNumber((ulong)data.Length+8,4),VideoText(type),data);}
  static byte[] VideoChunk(string type,params byte[][] parts){var data=VideoJoin(parts);return VideoJoin(VideoText(type),VideoNumber((ulong)data.Length,4,false),data,data.Length%2==0?new byte[0]:new byte[1]);}
  static byte[] VideoMovie(ulong scale,ulong ticks,bool versionOne=false){return VideoBox("moov",VideoBox("mvhd",VideoJoin(new byte[]{versionOne?(byte)1:(byte)0,0,0,0},new byte[versionOne?16:8],VideoNumber(scale,4),VideoNumber(ticks,versionOne?8:4),new byte[80])));}
  static string VideoWrite(string name,byte[] bytes){string path=Path.Combine(root,name);File.WriteAllBytes(path,bytes);return path;}
  static void VideoTests(){
   Test("MP4 MOV and M4V report container duration as Video exposure",()=>{
    foreach(string ext in new[]{"mp4","MOV","m4v"}){
     string path=VideoWrite("Stacked_M45_20x30s."+ext,VideoJoin(VideoBox("ftyp",VideoText("isom"),new byte[4]),VideoMovie(1000,5500)));
     var frame=Classifier.Read(path,root,"Scope","Auto");Check(frame.Kind=="Video"&&frame.Exposure==5.5&&frame.VideoDurationSeconds==5.5&&frame.ExposureText=="5.5 s",ext+" used filename exposure");Check(frame.ExposureTooltip.Contains("Video length")&&!frame.ExposureTooltip.Contains("per sub"),"Duration tooltip described sub exposure");Check(new CaptureFilters{Values={{"Frame type","Video"}}}.Matches(frame)&&FileSearch.Parse("type:Video exposure>5").Matches(frame),"Video filter/search failed");Check(CaptureGroups.Summarize(new[]{frame}).Subs==0&&SubframeSessions.Build(new[]{frame}).Count==0,"Recording became a sub session");
    }
   });
   Test("MP4 version-one duration skips an extended multi-gigabyte media atom",()=>{
    string path=Path.Combine(root,"large-tail-metadata.mp4");long offset=5L*1024*1024*1024;
    using(var stream=File.Create(path)){byte[] header=VideoJoin(VideoNumber(1,4),VideoText("mdat"),VideoNumber((ulong)offset,8));stream.Write(header,0,header.Length);stream.Position=offset;byte[] tail=VideoMovie(1000,4294968000,true);stream.Write(tail,0,tail.Length);}
    long bytes=0;double? duration=VideoHeaders.Duration(path,n=>bytes+=n);Check(duration==4294968&&bytes<100,"MP4 duration overflowed or read media payload");File.Delete(path);
   });
   Test("AVI duration prefers video stream timing over rounded microseconds",()=>{
    var main=new byte[56];Array.Copy(VideoNumber(33367,4,false),0,main,0,4);Array.Copy(VideoNumber(300,4,false),0,main,16,4);
    var video=new byte[56];Array.Copy(VideoText("vids"),video,4);Array.Copy(VideoNumber(1001,4,false),0,video,20,4);Array.Copy(VideoNumber(30000,4,false),0,video,24,4);Array.Copy(VideoNumber(300,4,false),0,video,32,4);
    byte[] body=VideoJoin(VideoText("AVI "),VideoChunk("LIST",VideoText("hdrl"),VideoChunk("avih",main),VideoChunk("LIST",VideoText("strl"),VideoChunk("strh",video))),VideoChunk("LIST",VideoText("movi"),new byte[1048576]));
    string path=VideoWrite("Light_Jupiter_30s.avi",VideoJoin(VideoText("RIFF"),VideoNumber((ulong)body.Length,4,false),body));long bytes=0;var frame=Classifier.Read(path,root,"Scope","Auto",counted:n=>bytes+=n);
    Check(frame.Kind=="Video"&&frame.Exposure==10.01&&bytes<200,"AVI duration decoded media or used per-frame exposure");
   });
   Test("WMV and MKV duration normalize their container time units",()=>{
    var properties=new byte[80];Array.Copy(VideoNumber(75000000,8,false),0,properties,40,8);Array.Copy(VideoNumber(500,8,false),0,properties,56,8);
    var file=VideoJoin(new Guid("8CABDCA1-A947-11CF-8EE4-00C00C205365").ToByteArray(),VideoNumber(104,8,false),properties);var header=VideoJoin(new Guid("75B22630-668E-11CF-A6D9-00AA0062CE6C").ToByteArray(),VideoNumber(134,8,false),VideoNumber(1,4,false),new byte[]{1,2},file);
    Check(Classifier.Read(VideoWrite("recording.wmv",header),root,"Scope","Auto").Exposure==7,"ASF play duration/preroll wrong");
    byte[] floating=BitConverter.GetBytes(2500.0);if(BitConverter.IsLittleEndian)Array.Reverse(floating);
    byte[] info=VideoJoin(new byte[]{0x44,0x89,0x88},floating,new byte[]{0x2a,0xd7,0xb1,0x83,0x0f,0x42,0x40});byte[] segment=VideoJoin(new byte[]{0x15,0x49,0xa9,0x66,(byte)(0x80+info.Length)},info);
    string path=VideoWrite("recording.mkv",VideoJoin(new byte[]{0x18,0x53,0x80,0x67,0xff},segment));var frame=Classifier.Read(path,root,"Scope","Auto");Check(frame.Kind=="Video"&&frame.Exposure==2.5,"Matroska timecode scale/float wrong");
   });
   Test("SER exposes its measured recording span and remains unknown without timestamps",()=>{
    string path=MakeSer("video-span.ser",0,0,8,2,new byte[8]);long start=new DateTime(2026,10,9,1,0,0,DateTimeKind.Utc).Ticks;using(var stream=new FileStream(path,FileMode.Append))using(var writer=new BinaryWriter(stream)){writer.Write(start);writer.Write(start+12345678);}
    var frame=Classifier.Read(path,root,"Scope","Auto");Check(frame.Kind=="Video"&&frame.Exposure==1.2345678&&frame.ExposureTooltip.Contains("timestamp span"),"SER duration guessed a playback rate");
    var unknown=Classifier.Read(MakeSer("video-no-times.ser",0,0,8,2,new byte[8]),root,"Scope","Auto");Check(unknown.Kind=="Video"&&!unknown.Exposure.HasValue,"SER without times invented duration");
   });
   Test("Malformed missing and excessive video metadata stay bounded with unknown duration",()=>{
    foreach(byte[] data in new[]{new byte[0],VideoMovie(0,500),VideoMovie(1000,uint.MaxValue),VideoJoin(VideoNumber(4,4),VideoText("moov"))}){
     var frame=Classifier.Read(VideoWrite("Light_M45_60s.mp4",data),root,"Scope","Auto");Check(frame.Kind=="Video"&&!frame.Exposure.HasValue,"Video fell back to filename exposure");
    }
    string excessive=VideoWrite("many-boxes.mp4",VideoJoin(Enumerable.Repeat(VideoBox("free"),5000).ToArray()));long bytes=0;Check(!VideoHeaders.Duration(excessive,n=>bytes+=n).HasValue&&bytes<=4096*8,"Metadata node limit failed");
    using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>VideoHeaders.Duration(excessive,n=>cancel.Token.ThrowIfCancellationRequested()),"Video metadata swallowed cancellation");}
    foreach(string ext in new[]{"avi","mp4","mov","m4v","wmv","mkv"})Check(Classifier.Read(VideoWrite("Dark_Stack_30s."+ext,new byte[0]),root,"Scope","Auto").Kind=="Video","Filename overrode recording type: "+ext);
   });
   Test("Legacy video metadata recovers type and duration once without rewriting recording bytes",()=>{
    string destination=Path.Combine(root,"legacy-video-repo"),hash,path;
    using(var repo=new Repository(destination)){path=Path.Combine(repo.Root,"capture.mp4");File.WriteAllBytes(path,VideoMovie(1000,7000));hash=Util.Hash(path,ct);repo.Save(new Frame{Hash=hash,OriginalName="Light_M45_60s.mp4",RelativePath="capture.mp4",Kind="Light",Exposure=60,Target="M45",Format="MP4",ClassificationVersion="compat-1"});}
    using(var repo=new Repository(destination)){var frame=repo.All().Single();Check(frame.Kind=="Video"&&frame.Exposure==7&&frame.VideoDurationSeconds==7&&frame.ClassificationVersion==Assets.ClassificationVersion&&Util.Hash(path,ct)==hash,"Legacy video kept per-frame exposure or altered payload");}
    File.Delete(path);using(var repo=new Repository(destination))Check(repo.All().Single().Exposure==7,"Already migrated recording was reopened or duration discarded");
   });
  }
 }
}
