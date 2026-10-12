// Off-spec files keep their metadata: tail bytes, missing padding, damaged pixels and folder-name guesses.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace AstroArchive {
 public partial class Tests {
  static void Append(string path,byte[] bytes){using(var stream=new FileStream(path,FileMode.Append))stream.Write(bytes,0,bytes.Length);}
  static void Cut(string path,long length){using(var stream=new FileStream(path,FileMode.Open))stream.SetLength(length);}
  static void ParsingRobustnessTests(){
   string folder=Path.Combine(root,"parsing-robustness");Directory.CreateDirectory(folder);
   var headers=LightHeaders(new DateTime(2026,10,6,21,0,0),"M45");
   Test("FITS tail bytes and missing final padding keep a complete image",()=>{
    string p=Path.Combine(folder,"no-padding.fit");Write(p,10,10,(x,y)=>1000+x,headers);long offset=Fits.Header(p).Offset;Cut(p,offset+200);
    var info=Assets.Inspect(p);Check(info.Images.Count==1&&info.Images[0].Numeric&&info.DataIssue==null&&(info.Note??"").Contains("padding"),"Missing final padding failed a complete image");
    var tails=new[]{Encoding.ASCII.GetBytes("trailing junk from a camera app"),new byte[2880],Encoding.ASCII.GetBytes("END".PadRight(80)+"garbage")};
    for(int n=0;n<tails.Length;n++){string q=Path.Combine(folder,"tail-"+n+".fit");Write(q,10,10,(x,y)=>1000,headers);Append(q,tails[n]);info=Assets.Inspect(q);Check(info.Images.Count==1&&info.Images[0].Numeric&&info.DataIssue==null&&(info.Note??"").Contains("Ignored"),"Trailing bytes failed a complete image: case "+n);}
    string e=Path.Combine(folder,"extension-tail.fit");Write(e,10,10,(x,y)=>1000,headers,true);Append(e,new byte[100]);info=Assets.Inspect(e);Check(info.Images.Count==1&&info.Images[0].Key=="hdu:1"&&info.Images[0].Numeric,"Tail after an image extension failed the file");
    Check(Classifier.Read(p,folder,"Scope","Auto").Target=="M45","Padding-free file lost its metadata");
   });
   Test("FITS damage before an image is still rejected",()=>{
    string p=Path.Combine(folder,"not-fits.fit");File.WriteAllText(p,"not FITS");Expect(()=>Assets.Inspect(p),"Text accepted as FITS");
    string q=Path.Combine(folder,"cut-header.fit");Write(q,10,10,(x,y)=>1000,headers);Cut(q,1000);Expect(()=>Assets.Inspect(q),"Truncated header accepted");
   });
   Test("Truncated pixel data keeps header metadata and stays unreadable",()=>{
    string source=Path.Combine(root,"parsing-truncated-source");Directory.CreateDirectory(source);
    string p=Path.Combine(source,"Light_M45_001.fit");Write(p,10,10,(x,y)=>1000,headers);long offset=Fits.Header(p).Offset;Cut(p,offset+100);
    var info=Assets.Inspect(p);Check(info.Images.Count==1&&!info.Images[0].Numeric&&(info.DataIssue??"").Contains("truncated"),"Truncated data was not flagged");
    Expect(()=>Fits.Validate(p,ct),"Screening validated truncated data");
    File.WriteAllText(Path.Combine(source,"bad.fit"),"not FITS");
    using(var repo=new Repository(Path.Combine(root,"parsing-truncated-repo"))){
     var plan=repo.Scan(source,"Scope","Auto",ct,NoProgress);var damaged=plan.Frames.Single(f=>f.OriginalName=="Light_M45_001.fit");var bad=plan.Frames.Single(f=>f.OriginalName=="bad.fit");
     Check(damaged.Status=="Unreadable"&&damaged.Rejected&&(damaged.IntegrityIssue??"").Contains("truncated"),"Damaged data was not kept out of use");
     Check(damaged.Target=="M45"&&damaged.Kind=="Light"&&damaged.Exposure==60&&damaged.Night!="Unknown date","Readable header metadata was discarded");
     Check(bad.Status=="Unreadable"&&bad.Target=="Unknown"&&plan.Errors.Count==2,"Unparseable files changed behaviour");
     Write(p,10,10,(x,y)=>1000,headers);var repaired=repo.Scan(source,"Scope","Auto",ct,NoProgress).Frames.Single(f=>f.OriginalName=="Light_M45_001.fit");
     Check(repaired.Status=="New"&&!repaired.Rejected&&repaired.IntegrityIssue==null,"Repaired file still reported damage");
    }
   });
   Test("Unsupported FITS axes keep the header as an archivable original",()=>{
    string p=Path.Combine(folder,"five-channel.fit");
    using(var s=File.Create(p)){Header(s,new List<string>{Card("SIMPLE","T"),Card("BITPIX","8"),Card("NAXIS","3"),Card("NAXIS1","2"),Card("NAXIS2","2"),Card("NAXIS3","5"),Card("CTYPE3","'RGB'"),Card("OBJECT","'M31'"),Card("IMAGETYP","'LIGHT'")});s.Write(new byte[2880],0,2880);}
    var info=Assets.Inspect(p);Check(info.Images.Count==0&&info.DataIssue==null&&info.Header.Get("OBJECT")=="M31"&&(info.Note??"").Contains("unsupported"),"Unsupported axes failed the file");
    var f=Classifier.Read(p,folder,"Scope","Auto");Check(f.Target=="M31"&&f.Kind=="Light"&&!Assets.CanDecode(f),"Unsupported-axes metadata was lost or decoding allowed");
   });
   Test("XISF attachment beyond the file keeps its keywords",()=>{
    string p=Path.Combine(folder,"short.xisf");WriteXisf(p,"4:1:1","UInt16",new byte[8],"",false,"","<FITSKeyword name=\"OBJECT\" value=\"'M42'\" comment=\"\"/>");Cut(p,4100);
    var info=Assets.Inspect(p);Check(info.Images.Count==1&&!info.Images[0].Numeric&&(info.DataIssue??"").Contains("truncated")&&info.Header.Get("OBJECT")=="M42","Truncated XISF lost metadata or stayed decodable");
   });
   Test("Folder names decide calibration type only when wholly calibration words",()=>{
    var none=new Dictionary<string,string>();
    Func<string,string,string> kind=(rootName,relative)=>{string r=Path.Combine(folder,rootName);string p=Path.Combine(r,relative.Replace('/',Path.DirectorySeparatorChar));Write(p,16,16,(x,y)=>1000,none);return Classifier.Read(p,r,"Scope","Auto").Kind;};
    Check(kind("Dark Sky Trip","Dark Nebulae/LDN673/frame_001.fit")!="Dark","Descriptive folder names marked a light as a dark");
    Check(kind("Ground Zero","M31/frame_001.fit")!="Bias","Root folder name marked a light as a bias");
    Check(kind("names","M31_offset_2.fit")!="Bias"&&kind("names","offset_001.fit")=="Bias","Offset in a filename was misread");
    Check(kind("nearest","Darks/Lights/frame_001.fit")!="Dark","A nearer lights folder did not win");
    Check(kind("library","Calibration/Dark Flats/cam_0/frame_001.fit")=="Dark flat"&&kind("Darks","frame_001.fit")=="Dark"&&kind("dwarf-root","DWARF_DARK/cam_1/frame_001.fit")=="Dark","Calibration folder conventions were lost");
   });
  }
 }
}
