using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void OptimisationPreviewTests(){
   Test("Optimised mono and RGB renders retain the previous bytes in every stretch mode",()=>{
    // Digests captured from temp f2fcd09 before changing the renderer.
    string[][] expected={
     new[]{"2FF5C9D74BA66790A0323996543E259E39ED213CCB17B7CD0319EEA503410C06","72492D394EBF4D613D7E0DDFAA899C53F8F2EB7DF62D62983BDDC98337F77DFA","C98A91245DDD624FACF358BCC47EAE629CBAE007B747998E9760D51A0C8AB032","72492D394EBF4D613D7E0DDFAA899C53F8F2EB7DF62D62983BDDC98337F77DFA"},
     new[]{"147F7157A5722E4719AA0B105B79D31025B0B46C846839FA9936A67AC1AF2AF3","4973F04996DD3E73DAD50253985FFAF20CD4DC85563BDD3FE1685895C5CB96C7","D821506E5FF8D13E0394643BE7F81341A07AD925EB26CF49E316A410CA0A3A84","34537AD0CAFBA3FDB8FC38679C38D49198F7CF30AC6B13EE8A438B98709A988D"}};
    for(int kind=0;kind<2;kind++){
     int channels=kind==0?1:3;var data=new PreviewData{Width=17,Height=13,Channels=channels,Minimum=-0.5,Maximum=0.5,FlipY=true,Pixels=Enumerable.Range(0,17*13*channels).Select(i=>((i*7919)%100003-50000)/100000.0).ToArray()};data.Pixels[0]=double.NaN;data.Pixels[1]=double.PositiveInfinity;data.Pixels[2]=double.NegativeInfinity;
     for(int mode=0;mode<PreviewData.StretchModes.Length;mode++)using(var sha=SHA256.Create())Check(BitConverter.ToString(sha.ComputeHash(data.Render(PreviewData.StretchModes[mode],ct))).Replace("-","")==expected[kind][mode],"Display bytes changed for "+channels+" channels / "+PreviewData.StretchModes[mode]);
    }
   });
   Test("Planar full resolution rendering shares RGB samples and ignores alpha without changing colours",()=>{
    var image=new PixelImage{Width=2,Height=2,Channels=4,Pixels=new[]{0.0,0.25,0.5,1,0.1,0.35,0.6,0.9,0.2,0.45,0.7,0.8,double.NaN,0,0,0}};
    var descriptor=new ImageDescriptor{Width=2,Height=2,Channels=4,Bitpix=-64,Headers=new Dictionary<string,string>()};var full=FullResolutionPreview.Create(new Frame{Format="XISF"},image,descriptor,"fixture.xisf",ct);
    var interleaved=new PreviewData{Width=2,Height=2,Channels=3,Pixels=new[]{0.0,0.1,0.2,0.25,0.35,0.45,0.5,0.6,0.7,1.0,0.9,0.8}};
    Check(ReferenceEquals(full.Pixels,image.Pixels)&&full.Planar,"Native RGB allocated another full sample buffer");foreach(string mode in PreviewData.StretchModes)Check(full.Render(mode,ct).SequenceEqual(interleaved.Render(mode,ct)),"Planar/alpha display differs for "+mode);
   });
   Test("Preview statistics follow shared immutable samples and current display context",()=>{
    var data=new PreviewData{Width=100,Height=1,Channels=3,Pixels=Enumerable.Range(0,100).SelectMany(i=>new[]{0.01+i*0.00001,0.02+i*0.00001,0.03+i*0.00001}).ToArray()};var other=data.Copy();data.Render("Auto",ct);other.Filter="LP";other.Render("Strong",ct);other.Render("Auto per channel",ct);
    Check(data.StatisticsBuilds==1&&other.StatisticsBuilds==1&&other.DisplayMode=="Auto per channel","Statistics were rebuilt or display context leaked");data.Target="Sun";Check(data.Render("Strong",ct).SequenceEqual(data.Render("Linear",ct))&&data.StatisticsBuilds==1,"Cached statistics overrode solar context");
    other.Pixels=(double[])other.Pixels.Clone();other.Render("Auto",ct);Check(other.StatisticsBuilds==2,"Replacement samples reused old statistics");
    var canceled=new PreviewData{Width=1,Height=1,Channels=1,Pixels=new[]{0.1}};using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>canceled.Render("Auto",cancel.Token),"Canceled statistics were accepted");Check(canceled.StatisticsBuilds==0,"Cancellation published statistics");}
   });
   Test("Streamed SER previews retain frame selection endian colour and bin averages",()=>{
    const int width=2804,height=4;foreach(int colour in new[]{0,8,100,101})foreach(int endian in new[]{0,1}){
     int channels=colour>=100?3:1;var raw=new List<byte>();for(int frame=0;frame<2;frame++)for(int y=0;y<height;y++)for(int x=0;x<width;x++)for(int c=0;c<channels;c++){
      int rgb=colour==101?2-c:c;ushort value=(ushort)(colour==8?(y%2==0?(x%2==0?9000:4000):(x%2==0?4000:1000)):channels==3?1000+rgb*2000+frame*100:frame*1000+x);
      raw.Add((byte)(endian==0?value:value>>8));raw.Add((byte)(endian==0?value>>8:value));
     }
     string path=MakeSer("sampled-"+colour+"-"+endian+".ser",colour,endian,16,2,raw.ToArray(),width,height);var info=Assets.Inspect(path);var frameInfo=new Frame{Format="SER",Images=info.Images,Bayer=info.Header.Get("BAYERPAT")};var data=Assets.Display(frameInfo,path,1,ct);
     Check(data.Width<=1400&&data.SourceWidth==width&&data.Maximum==65535,"SER preview geometry/range changed");
     if(colour==8)Check(data.Pixels.Take(3).SequenceEqual(new[]{9000.0,4000,1000}),"Bayer SER bins changed");else if(channels==3)Check(data.Pixels.Take(3).SequenceEqual(new[]{1100.0,3100,5100}),"SER RGB/BGR frame changed");else Check(data.Pixels[0]==1001&&data.Pixels.Last()==3802.5,"SER edge bin average or frame selection changed");
    }
   });
   Test("Sampled XISF handles attached inline shuffled and interleaved layouts without full double samples",()=>{
    foreach(bool inline in new[]{false,true})foreach(bool normal in new[]{false,true}){
     string path=Path.Combine(root,"sampled-"+inline+"-"+normal+".xisf");byte[] raw=normal?new byte[]{10,30,50,20,40,60}:new byte[]{10,20,30,40,50,60};WriteXisf(path,"2:1:3","UInt8",raw,"",inline,normal?"pixelStorage=\"normal\"":"");var info=Assets.Inspect(path);var frame=new Frame{Format="XISF",Images=info.Images};var data=Assets.Display(frame,path,0,ct);Check(data.Pixels.SequenceEqual(new double[]{10,30,50,20,40,60}),"Sampled XISF channel layout changed");
    }
    string shuffled=Path.Combine(root,"sampled-shuffled.xisf");WriteXisf(shuffled,"4:1:1","UInt16",new byte[]{128,1,3,5,7,2,4,6,8},"lz4+sh:8:2",false);var asset=Assets.Inspect(shuffled);Check(Assets.Display(new Frame{Format="XISF",Images=asset.Images},shuffled,0,ct).Pixels.SequenceEqual(new[]{513.0,1027,1541,2055}),"Sampled XISF shuffle changed values");
   });
  }
  static void OptimisationBrowsingTests(){
   Test("Shared session membership retains section ordering final member order and singleton files",()=>{
    var rows=new[]{new Frame{Kind="Light",Target="M31",Session="one",Telescope="A",Exposure=10},new Frame{Kind="Stack",Target="M31",Exposure=100},new Frame{Kind="Light",Target="M31",Session="one",Telescope="A",Exposure=5},new Frame{Kind="Light",Target="M31",Session="one",Telescope="B",Exposure=20},new Frame{Kind="Bias",Target="Calibration",Exposure=0}};
    List<SubframeSession> groups;var ordered=RepositoryOrdering.Order(rows,new[]{new SearchSort{Property="Exposure",Descending=true}},CultureInfo.InvariantCulture,true,true,ct,out groups);
    Check(ordered.SequenceEqual(new[]{rows[0],rows[2],rows[1],rows[3],rows[4]}),"Section partition merged a singleton/instrument or lost user ordering");Check(groups.Count==1&&groups[0].Frames.SequenceEqual(new[]{rows[0],rows[2]})&&groups[0].Label.Contains("5–10s"),"Session frame order or label changed");
    Check(SearchOrdering.Order(rows,new SearchSort[0],null,ct).SequenceEqual(rows),"Empty sort descriptors changed order");using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>SearchOrdering.Order(new Frame[0],new SearchSort[0],null,cancel.Token),"Empty sort ignored cancellation");}
   });
  }
  static void OptimisationEditedTests(){
   WindowsTest("Edited headers on NTFS are reused and same-size same-time external edits are reread",()=>{
    string path=Path.Combine(root,"native-header-cache.fit");Write(path,8,8,(x,y)=>1000,new Dictionary<string,string>{{"OBJECT","'M31'"}});int reads=0;var cache=new EditedHeaderCache(4096,null,file=>{reads++;return Assets.Inspect(file).Header;});
    if(!FileStamp.Read(path).Reliable){Console.WriteLine("SKIP header reuse requires NTFS/ReFS change stamps");return;}
    Thread.Sleep(2100); // Let the filesystem clock advance before testing unchanged reuse.
    Check(cache.Get(path,ct).Get("OBJECT")=="M31"&&cache.Get(path,ct).Get("OBJECT")=="M31"&&reads==1,"Native unchanged image reopened");DateTime modified=File.GetLastWriteTimeUtc(path);long size=new FileInfo(path).Length;
    Write(path,8,8,(x,y)=>1000,new Dictionary<string,string>{{"OBJECT","'M51'"}});File.SetLastWriteTimeUtc(path,modified);Check(new FileInfo(path).Length==size&&cache.Get(path,ct).Get("OBJECT")=="M51"&&reads==2,"Native same-size/same-time edit reused stale metadata");
   });
   Test("Recent writes are reread even when reliable file stamps share a clock tick",()=>{
    int reads=0;var stamp=new FileStamp{Reliable=true,Identity="recent",Size=1,Modified=1,Created=1,Changed=DateTime.UtcNow.ToFileTimeUtc()};var cache=new EditedHeaderCache(4096,path=>stamp.Clone(),path=>{reads++;var header=new FitsHeader();header.Values["OBJECT"]=reads==1?"M31":"M51";return header;});
    Check(cache.Get("recent.fit",ct).Get("OBJECT")=="M31"&&cache.Get("recent.fit",ct).Get("OBJECT")=="M51"&&reads==2&&cache.Count==0,"A write within one clock tick reused stale metadata");
   });
   Test("Edited header caching invalidates by file identity and change time and does not cache errors or cloud files",()=>{
    var stamp=new FileStamp{Reliable=true,Identity="fixture",Size=42,Modified=10,Created=1,Changed=10};int reads=0;bool fail=false;
    var cache=new EditedHeaderCache(4096,path=>stamp.Clone(),path=>{reads++;if(fail)throw new IOException("Transient header failure");var header=new FitsHeader();header.Values["OBJECT"]="M31";return header;});string file=Path.Combine(root,"cache-header.fit");
    var first=cache.Get(file,ct);first.Values["OBJECT"]="M51";Check(cache.Get(file,ct).Get("OBJECT")=="M31"&&reads==1,"Cache shared mutable headers or reopened unchanged image");
    stamp.Changed++;cache.Get(file,ct);Check(reads==2,"Same-size/same-timestamp change reused old header");stamp.Identity="replaced";cache.Get(file,ct);Check(reads==3,"Replacement file reused old header");
    stamp.Changed++;fail=true;Expect(()=>cache.Get(file,ct),"Transient read failure swallowed");fail=false;cache.Get(file,ct);Check(reads==5,"Transient failure cached");
    stamp.Attributes=0x1000;cache.Get(file,ct);cache.Get(file,ct);Check(reads==7&&cache.Count==0,"Cloud stamps were trusted");stamp.Attributes=0;stamp.Reliable=false;cache.Get(file,ct);cache.Get(file,ct);Check(reads==9&&cache.Count==0,"Unreliable stamps were trusted");
    using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>cache.Get(file,cancel.Token),"Canceled refresh read headers");Check(reads==9,"Canceled refresh reached decoder");}
   });
   Test("Edited header cache stays bounded and evicts least recently used headers",()=>{
    int reads=0;var cache=new EditedHeaderCache(900,path=>new FileStamp{Reliable=true,Identity=path,Size=1,Modified=1,Created=1,Changed=1},path=>{reads++;return new FitsHeader();});cache.Get("a.fit",ct);cache.Get("b.fit",ct);cache.Get("a.fit",ct);Check(reads==3&&cache.Count==1,"Header cache did not evict");cache.Clear();Check(cache.Count==0,"Header clear retained entries");
   });
  }
 }
}
