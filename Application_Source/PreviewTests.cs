// Generated image regressions for colour fidelity, stretch and bounded decoding.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace AstroArchive {
 public partial class Tests {
  static void PreviewTests(){
   Test("Auto preview compensates IRCUT and LP colour casts without changing samples",()=>{
    var data=new PreviewData{Width=100,Height=100,Channels=3,Pixels=Enumerable.Range(0,10000).SelectMany(n=>new[]{0.04+n*0.000002,0.02+n*0.000001,0.015+n*0.00000075}).ToArray()};var original=(double[])data.Pixels.Clone();
    var natural=new PreviewData{Width=100,Height=100,Channels=3,Pixels=Enumerable.Range(0,10000).SelectMany(n=>new[]{0.01+n*0.000001,0.02+n*0.000001,0.03+n*0.000001}).ToArray()};byte[] linked=natural.Render("Auto",ct);Check(linked[15000]<linked[15001]&&linked[15001]<linked[15002]&&!natural.DisplayMode.Contains("compensated"),"Normal linked RGB colour was unexpectedly neutralised");
    var withoutFilter=data.Render("Auto",ct);Check(Math.Abs(withoutFilter[15000]-withoutFilter[15002])<=1&&data.DisplayMode.Contains("compensated"),"One-colour clipping was not detected without filter metadata");
    foreach(string filter in new[]{"IRCUT","IR-cut","IR CUT","LP","LP filter","Light Pollution","Dual band","L-Enhance"}){data.Filter=filter;byte[] compensated=data.Render("Auto",ct);Check(Math.Abs(compensated[15000]-compensated[15001])<=1&&Math.Abs(compensated[15001]-compensated[15002])<=1,"Filter cast remains for "+filter);Check(data.DisplayMode.Contains("colour compensated"),"Compensation not identified");byte[] strong=data.Render("Strong",ct);Check(strong[15000]>compensated[15000],"Strong stretch lost its strength after colour balancing");}
    Check(data.Pixels.SequenceEqual(original),"Display balancing modified source samples");data.Filter="Unknown";data.Pixels=Enumerable.Range(0,10000).SelectMany(n=>new[]{0.2+n*0.00001,0.01+n*0.000001,0.003+n*0.0000001}).ToArray();var automatic=data.Render("Auto",ct);Check(Math.Abs(automatic[15000]-automatic[15002])<=1&&data.DisplayMode.Contains("colour compensated"),"Severe undocumented filter cast remained");
    data.ApplyContext(null,Path.Combine(root,"LP","capture.fit"));Check(PreviewData.FilterColourCompensation(data.Filter),"Filter directory hint ignored");
    foreach(string filter in new[]{"CLP","LPiece","RGB","Standard"})Check(!PreviewData.FilterColourCompensation(filter),"Substring mistaken for optical filter: "+filter);
   });
   Test("Solar lunar and planetary previews stay linear with their original colour",()=>{
    var data=new PreviewData{Width=3,Height=1,Channels=3,Filter="LP",Pixels=new[]{0.2,0.3,0.1,0.4,0.6,0.2,0.6,0.9,0.3}};var expected=data.Render("Linear",ct);
    foreach(string target in new[]{"Sun","Solar","solar","Moon","Jupiter","Saturn","Mars","Venus","Mercury","Uranus","Neptune","Planetary"}){data.Target=target;foreach(string mode in PreviewData.StretchModes)Check(data.Render(mode,ct).SequenceEqual(expected),"Bright body was stretched or colour balanced: "+target+" / "+mode);Check(data.SkipStretch&&data.DisplayMode.StartsWith("Linear"),"Bright preview did not select linear");}
    data.Target="Unknown";data.ObservationMode="Planetary";Check(data.Render("Strong",ct).SequenceEqual(expected),"Capture mode failed to suppress stretch");data.ObservationMode="LIGHT";data.ApplyContext(null,Path.Combine(root,"Solar","capture.fit"));Check(data.SkipStretch,"Solar folder hint lost behind light header");
    data=new PreviewData{Target="NGC7009"};data.ApplyContext(null,Path.Combine(root,"Solar","Saturn_Nebula.fit"));Check(!data.SkipStretch,"Known planetary nebula treated as a planet");data=new PreviewData();data.ApplyContext(null,Path.Combine(root,"Saturn_Nebula.fit"));Check(!data.SkipStretch,"Nebula filename treated as a planet");Check(!ObservationTargets.Unstretched("Planetary Nebula","Science")&&!ObservationTargets.Unstretched("Sunflower Galaxy",null),"Deep sky label bypassed stretching");
   });
   Test("FITS preview uses target filter and observation mode metadata",()=>{
    string path=Path.Combine(root,"preview-solar-metadata.fit");Write(path,8,8,(x,y)=>20000+x*100+y,new Dictionary<string,string>{{"OBJECT","'Solar'"},{"FILTER","'IRCUT'"},{"OBSMODE","'SOLAR'"},{"BAYERPAT","'RGGB'"}});string hash=Util.Hash(path,ct);var data=Fits.Preview(path,ct);Check(data.Target=="Solar"&&data.Filter=="IRCUT"&&data.SkipStretch,"FITS subject/filter metadata dropped");Check(data.Render("Auto",ct).SequenceEqual(data.Render("Linear",ct)),"FITS solar image stretched");Check(Util.Hash(path,ct)==hash,"Solar preview changed FITS file");
    Write(path,8,8,(x,y)=>1000+x*10+y,new Dictionary<string,string>{{"OBJECT","'M45'"},{"FILTER","'LP'"},{"BAYERPAT","'RGGB'"}});data=Fits.Preview(path,ct);data.ApplyContext(new Frame{Target="Sun",Filter="IRCUT"},path);Check(data.SkipStretch&&data.Filter=="IRCUT","Corrected repository metadata ignored by preview");
   });
   Test("XISF FITS keywords retain solar and filter preview context",()=>{
    string path=Path.Combine(root,"solar-metadata.xisf");WriteXisf(path,"2:1:3","UInt8",new byte[]{10,20,30,40,50,60},"",true,"","<FITSKeyword name=\"OBJECT\" value=\"'Sun'\"/><FITSKeyword name=\"FILTER\" value=\"'LP'\"/>");var data=Xisf.Read(path,ct);Check(data.Target=="Sun"&&data.Filter=="LP"&&data.SkipStretch,"XISF context dropped");Check(data.Render("Auto",ct).SequenceEqual(data.Render("Linear",ct)),"XISF solar image stretched");
   });
   Test("Sun and Solar labels persist as one target across imports edits and legacy index migration",()=>{
    Check(Catalog.Normalize("Solar")=="Sun"&&Catalog.Normalize("SUN")=="Sun"&&Catalog.TargetFromFilename("Light_Sun_001.fit")=="Sun"&&Catalog.TargetFromFilename("Light_Solar_001.fit")=="Sun","Solar aliases not canonical");Check(Catalog.TargetFromFilename("Sunflower_Galaxy.fit")=="M63","Sun alias interfered with Sunflower Galaxy");
    string source=Path.Combine(root,"solar-target-source"),destination=Path.Combine(root,"solar-target-repo");Directory.CreateDirectory(source);Write(Path.Combine(source,"capture_1.fit"),8,8,(x,y)=>1200,new Dictionary<string,string>{{"OBJECT","'Solar'"},{"IMAGETYP","'LIGHT'"}});Write(Path.Combine(source,"capture_2.fit"),8,8,(x,y)=>1400,new Dictionary<string,string>{{"OBJECT","'Sun'"},{"IMAGETYP","'LIGHT'"}});
    string solarFolder=Path.Combine(source,"Solar");Directory.CreateDirectory(solarFolder);Write(Path.Combine(solarFolder,"capture_3_IRCUT.fit"),8,8,(x,y)=>1600,new Dictionary<string,string>{{"IMAGETYP","'LIGHT'"}});string working,hash,oldRelative;
    using(var repo=new Repository(destination)){var plan=repo.Scan(source,"Scope-01","Auto",ct,NoProgress);Check(plan.Frames.Count==3&&plan.Frames.All(f=>f.Target=="Sun")&&plan.Frames.Single(f=>f.OriginalName.Contains("IRCUT")).Filter=="IRCUT","Import kept separate solar targets or lost filter");repo.Import(plan.Frames,ct,NoProgress);Check(repo.All().All(f=>f.Target=="Sun"&&f.RelativePath.StartsWith(Path.Combine("Targets","Sun"))),"New imports stored in different solar sections");var frame=repo.All().First();frame.Target="Solar";repo.Save(frame);Check(repo.Find(frame.Hash).Target=="Sun","Metadata edit reintroduced Solar section");working=repo.WorkingIndex;hash=frame.Hash;oldRelative=frame.RelativePath.Replace(Path.Combine("Targets","Sun"),Path.Combine("Targets","Solar"));string legacy=Path.Combine(repo.Root,oldRelative);Directory.CreateDirectory(Path.GetDirectoryName(legacy));File.Move(repo.FilePath(frame),legacy);}
    // Simulate a pre-change portable record and cached source metadata.
    using(var db=new Database(working)){var frame=Util.Deserialize<Frame>(db.Query("SELECT data FROM files WHERE hash=?",hash).Single());frame.Target="Solar";frame.RelativePath=oldRelative;frame.RepositoryStamp=FileStamp.Read(Path.Combine(destination,oldRelative));db.Exec("UPDATE files SET data=? WHERE hash=?",Util.Serialize(frame),hash);foreach(string value in db.Query("SELECT data FROM source_manifest")){var manifest=Util.Deserialize<SourceManifest>(value);manifest.Metadata.Target="Solar";db.Exec("UPDATE source_manifest SET data=? WHERE root=? AND path=?",Util.Serialize(manifest),manifest.Root,manifest.Path);}db.Exec("INSERT INTO deleted_files(hash,data) VALUES(?,?)","old-deleted",Util.Serialize(new DeletedCapture{Hash="old-deleted",DeletedUtc="2026-10-07",Metadata=new Frame{Target="Solar"}}));}
    using(var repo=new Repository(destination)){Check(repo.All().Select(f=>f.Target).Distinct().SequenceEqual(new[]{"Sun"}),"Legacy library sections remain separate");var restored=repo.Find(hash);Check(restored.RelativePath==oldRelative&&Util.Hash(repo.FilePath(restored),ct)==hash,"Target consolidation lost or changed legacy image");Check(repo.Deletions().Single().Metadata.Target=="Sun","Deletion metadata can restore Solar alias");using(var db=new Database(working))Check(db.Query("SELECT data FROM source_manifest").Select(Util.Deserialize<SourceManifest>).All(m=>m.Metadata.Target=="Sun"),"Source manifest retained old alias");}
    using(var repo=new Repository(destination))Check(repo.All().All(f=>f.Target=="Sun"),"Solar merge did not persist across reopen");
   });
   Test("Preview preserves FITS RGB planes and source bytes",()=>{
    string path=Path.Combine(root,"preview-rgb.fits");WriteFloat(path,24,18,3);string hash=Util.Hash(path,ct);var preview=Fits.Preview(path,ct);
    Check(preview.Channels==3&&preview.SourceWidth==24,"RGB geometry");Check(preview.Pixels[0]==10&&preview.Pixels[1]==20&&preview.Pixels[2]==30,"RGB planes were mixed");Check(preview.FlipY,"FITS display origin");Check(Util.Hash(path,ct)==hash,"Preview altered source");
   });
   Test("Preview reconstructs FITS CFA colour and offsets",()=>{
    string path=Path.Combine(root,"preview-cfa.fit");Write(path,8,8,(x,y)=>y%2==0?(x%2==0?9000:4000):(x%2==0?4000:1000),new Dictionary<string,string>{{"BAYERPAT","'RGGB'"}});var data=Fits.Preview(path,ct);Check(data.Channels==3&&data.Pixels[0]==9000&&data.Pixels[1]==4000&&data.Pixels[2]==1000,"CFA colour recovery");
    Write(path,8,8,(x,y)=>y%2==0?(x%2==0?4000:9000):(x%2==0?1000:4000),new Dictionary<string,string>{{"BAYERPAT","'RGGB'"},{"XBAYROFF","1"}});data=Fits.Preview(path,ct);Check(data.Pixels[0]==9000&&data.Pixels[2]==1000,"CFA offset ignored");
   });
   Test("Preview reads scaled FITS gzip and IMAGE extensions",()=>{
    string path=Path.Combine(root,"preview-extension.fit");Write(path,12,10,(x,y)=>32000+x+y,new Dictionary<string,string>(),true);string gzip=path+".gz";using(var from=File.OpenRead(path))using(var to=File.Create(gzip))using(var gz=new GZipStream(to,CompressionMode.Compress))from.CopyTo(gz);var data=Fits.Preview(gzip,ct);Check(data.Pixels[0]==32000&&data.Width==12,"Scaled gzip preview");Check(data.Maximum==65535&&data.Minimum==0,"Unsigned display range");
   });
   Test("Preview stretch reveals faint backgrounds and preserves linked RGB",()=>{
    var data=new PreviewData{Width=100,Height=100,SourceWidth=100,SourceHeight=100,Channels=1,Pixels=Enumerable.Range(0,10000).Select(n=>0.01+n*0.000001).ToArray()};byte[] linear=data.Render("Linear",ct),auto=data.Render("Auto",ct),strong=data.Render("Strong",ct);Check(auto[15000]>linear[15000]+40,"Auto did not reveal faint data");Check(strong[15000]>auto[15000],"Strong has no additional stretch");
    data.Channels=3;data.Pixels=Enumerable.Range(0,10000).SelectMany(n=>new[]{0.01+n*0.000001,0.02+n*0.000001,0.03+n*0.000001}).ToArray();auto=data.Render("Auto",ct);Check(auto[15000]<auto[15001]&&auto[15001]<auto[15002],"Linked stretch changed channel order");byte[] independent=data.Render("Auto per channel",ct);Check(Math.Abs(independent[15000]-independent[15002])<=1,"Independent backgrounds not balanced");
   });
   Test("Preview handles constant, invalid, flipped and cancelled samples",()=>{
    var data=new PreviewData{Width=2,Height=2,Channels=1,Pixels=new[]{0.0,double.NaN,1.0,double.PositiveInfinity},FlipY=true};byte[] rgb=data.Render("Linear",ct);Check(rgb[0]==255&&rgb[3]==0&&rgb[6]==0,"Origin or invalid samples");data.Pixels=new[]{0.1,0.1,0.1,0.1};Check(data.Render("Auto",ct).Length==12,"Constant image failed");using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>data.Render("Auto",cancel.Token),"Stretch ignored cancellation");Expect(()=>Fits.Preview(Path.Combine(root,"preview-rgb.fits"),cancel.Token),"FITS preview ignored cancellation");}
   });
   Test("XISF attached UInt16 and inline RGB preserve values",()=>{
    string path=Path.Combine(root,"attached.xisf");byte[] raw=Enumerable.Range(0,12).SelectMany(n=>BitConverter.GetBytes((ushort)(1000+n))).ToArray();WriteXisf(path,"4:3:1","UInt16",raw,"",false);var data=Xisf.Read(path,ct);Check(data.Width==4&&data.Pixels[11]==1011&&data.Maximum==65535,"Attached XISF values");
    raw=new byte[]{10,20,30,40,50,60};WriteXisf(path,"2:1:3","UInt8",raw,"",true);data=Xisf.Read(path,ct);Check(data.Pixels.SequenceEqual(new double[]{10,30,50,20,40,60}),"Planar XISF RGB");
   });
   Test("XISF zlib, LZ4 and byte shuffle decode correctly",()=>{
    string path=Path.Combine(root,"compressed.xisf");byte[] raw={1,2,3,4,5,6,7,8};byte[] compressed=Zlib(raw);WriteXisf(path,"4:1:1","UInt16",compressed,"zlib:8",false);var data=Xisf.Read(path,ct);Check(data.Pixels[0]==513&&data.Pixels[3]==2055,"zlib decode");
    byte[] shuffle={1,3,5,7,2,4,6,8};byte[] lz4=new byte[]{128}.Concat(shuffle).ToArray();WriteXisf(path,"4:1:1","UInt16",lz4,"lz4+sh:8:2",false);data=Xisf.Read(path,ct);Check(data.Pixels[0]==513&&data.Pixels[3]==2055,"Shuffled LZ4 decode");Check(Xisf.Lz4(new byte[]{0x10,65,1,0},5,ct).All(b=>b==65),"Overlapping LZ4 match");
   });
   Test("XISF big endian, interleaved and floating samples",()=>{
    string path=Path.Combine(root,"big.xisf");byte[] raw=new byte[]{0,1,0,2,0,3};WriteXisf(path,"1:1:3","UInt16",raw,"",true,"byteOrder=\"big\" pixelStorage=\"normal\"");var data=Xisf.Read(path,ct);Check(data.Pixels.SequenceEqual(new double[]{1,2,3}),"Big-endian decode");
    raw=BitConverter.GetBytes(0.25f).Concat(BitConverter.GetBytes(float.NaN)).ToArray();WriteXisf(path,"2:1:1","Float32",raw,"",true);data=Xisf.Read(path,ct);Check(data.Pixels[0]==0.25&&double.IsNaN(data.Pixels[1]),"Floating samples changed");
   });
   Test("XISF CFA source metadata produces a colour preview",()=>{
    string path=Path.Combine(root,"cfa.xisf");WriteXisf(path,"2:2:1","UInt16",new ushort[]{9000,4000,4000,1000}.SelectMany(BitConverter.GetBytes).ToArray(),"",false,"","<Property id=\"PCL:CFASourcePattern\" type=\"String\">RGGB</Property>");var image=Xisf.Read(path,ct);Check(image.Channels==3&&image.Pixels.SequenceEqual(new double[]{9000,4000,1000}),"XISF CFA pattern ignored");
   });
   Test("XISF rejects corrupt, oversized and unsupported inputs",()=>{
    string path=Path.Combine(root,"bad.xisf");WriteXisf(path,"100000:100000:3","UInt16",new byte[0],"",true);Expect(()=>Xisf.Read(path,ct),"Oversized image accepted");WriteXisf(path,"2:1:1","UInt16",new byte[1],"",true);Expect(()=>Xisf.Read(path,ct),"Truncated samples accepted");byte[] compressed=Zlib(new byte[]{1,2});compressed[compressed.Length-1]^=1;Expect(()=>Xisf.Inflate(compressed,2,ct),"Corrupt zlib accepted");Expect(()=>Xisf.Lz4(new byte[]{0,0,0},4,ct),"Invalid LZ4 offset accepted");WriteXisf(path,"2:1:1","UInt8",new byte[]{1,2},"zstd:2",true);Expect(()=>Xisf.Read(path,ct),"Unsupported compression accepted");using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>Xisf.Read(Path.Combine(root,"attached.xisf"),cancel.Token),"XISF cancellation ignored");}
   });
  }
  static void WriteXisf(string path,string geometry,string format,byte[] data,string compression,bool inline,string attrs="",string property=""){
   string location=inline?"inline:base64":"attachment:4096:"+data.Length;string xml="<xisf xmlns=\"http://www.pixinsight.com/xisf\" version=\"1.0\"><Image geometry=\""+geometry+"\" sampleFormat=\""+format+"\" location=\""+location+"\" "+attrs+(compression.Length>0?" compression=\""+compression+"\"":"")+">"+(inline?Convert.ToBase64String(data):"")+property+"</Image></xisf>";byte[] header=Encoding.UTF8.GetBytes(xml);
   using(var stream=File.Create(path))using(var writer=new BinaryWriter(stream)){writer.Write(Encoding.ASCII.GetBytes("XISF0100"));writer.Write(header.Length);writer.Write(0);writer.Write(header);if(!inline){while(stream.Position<4096)writer.Write((byte)0);writer.Write(data);}}
  }
  static byte[] Zlib(byte[] bytes){using(var stream=new MemoryStream()){stream.WriteByte(0x78);stream.WriteByte(0x9C);using(var deflate=new DeflateStream(stream,CompressionMode.Compress,true))deflate.Write(bytes,0,bytes.Length);uint a=1,b=0;foreach(byte value in bytes){a=(a+value)%65521;b=(b+a)%65521;}uint hash=b<<16|a;for(int n=3;n>=0;n--)stream.WriteByte((byte)(hash>>(n*8)));return stream.ToArray();}}
 }
}
