using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void PreviewResolutionTests(){
   Test("Native FITS popup pixels retain source dimensions while sidebar samples remain bounded",()=>{
    string path=Path.Combine(root,"native-resolution.fit");Write(path,1604,16,(x,y)=>1000+x,new Dictionary<string,string>());string hash=Util.Hash(path,ct);var info=Assets.Inspect(path);var frame=new Frame{Format=info.Format,Images=info.Images};
    var small=Assets.Display(frame,path,0,ct);var full=Assets.Display(frame,path,0,ct,true);Check(small.Width<1604&&small.Width<=1400&&small.SourceWidth==1604,"Sidebar no longer samples large FITS");
    Check(full.Width==1604&&full.Height==16&&full.Pixels[1603]==2603&&full.FlipY&&full.Maximum==65535,"Native FITS pixels were reduced, rescaled or flipped incorrectly");
    var revisit=Assets.Display(frame,path,0,ct);Check(revisit.Width==small.Width&&Util.Hash(path,ct)==hash,"Popup changed sidebar resolution or source bytes");
   });
   Test("Native CFA colour preserves every sensor location and Bayer offsets",()=>{
    string path=Path.Combine(root,"native-cfa.fit");foreach(int offset in new[]{0,1}){
     Write(path,8,8,(x,y)=>y%2==0?((x+offset)%2==0?9000:4000):((x+offset)%2==0?4000:1000),new Dictionary<string,string>{{"BAYERPAT","'RGGB'"},{"XBAYROFF",offset.ToString()}});
     var info=Assets.Inspect(path);var frame=new Frame{Format=info.Format,Images=info.Images,Bayer=offset==0?null:"RGGB"};var full=Assets.Display(frame,path,0,ct,true);Check(full.Width==8&&full.Height==8&&full.Channels==3,"Native Bayer image was binned");
     for(int pixel=0;pixel<64;pixel++)Check(full.Pixels.Skip(pixel*3).Take(3).SequenceEqual(new[]{9000.0,4000,1000}),"Native Bayer colours/offsets or edge interpolation are incorrect");
    }
   });
   Test("Native XISF popup preserves RGB planes and dimensions while inline decoding stays sampled",()=>{
    string path=Path.Combine(root,"native-rgb.xisf");byte[] raw=Enumerable.Range(0,1604*2*3).Select(n=>(byte)(n/(1604*2)*10+10)).ToArray();WriteXisf(path,"1604:2:3","UInt8",raw,"",false);var info=Assets.Inspect(path);var frame=new Frame{Format=info.Format,Images=info.Images};
    var full=Assets.Display(frame,path,0,ct,true);Check(full.Width==1604&&full.Height==2&&Enumerable.Range(0,3).Select(c=>full.Sample(0,c)).SequenceEqual(new[]{10.0,20,30})&&Enumerable.Range(0,3).Select(c=>full.Sample(3207,c)).SequenceEqual(new[]{10.0,20,30}),"Native XISF dimensions or RGB order changed");
    Check(Assets.Display(frame,path,0,ct).Width<=1400&&Xisf.Read(path,ct).Width<=1400,"Inline XISF became full resolution");
   });
   Test("Condensed subframes show per-sub exposure without inventing missing or mixed durations",()=>{
    Func<double?[],SubframeSession> group=values=>SubframeSessions.Build(values.Select(v=>new Frame{Target="M31",Kind="Light",Session="fixture",Exposure=v,Filter="RGB"})).Single();
    Check(group(new double?[]{60,60}).Label.Contains("2 subs · 60 s/sub · 2 min 0 s in subs"),"Per-sub and accumulated exposure are not distinguished");
    Check(group(new double?[]{0.25,0.25}).Label.Contains("0.25 s/sub"),"Fractional exposures lost precision");
    Check(group(new double?[]{30,60}).Label.Contains("30–60 s/sub (mixed)"),"Mixed durations presented as one exposure");
    Check(group(new double?[]{60,null}).Label.Contains("60 s/sub + unknown")&&group(new double?[]{null,0,double.NaN}).Label.Contains("per-sub exposure unknown"),"Unknown exposures guessed");
   });
  }
 }
}
