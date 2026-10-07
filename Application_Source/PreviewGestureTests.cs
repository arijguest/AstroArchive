using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void PreviewGestureTests(){
   Test("Unknown metadata display leaves targets and stored values intact",()=>{
    var frame=new Frame{Target="Unknown",Make="Unknown",Model="Other / unknown",Kind="Unknown",Camera="Unknown",Mount="Unknown",Night="Unknown date",Filter="Unknown",Calibration="Unknown"};var reopened=Util.Deserialize<Frame>(Util.Serialize(frame));
    foreach(string value in new[]{null,"","Unknown","Unknown date","Other / unknown","?"})Check(TableText.Display(value)=="-","Unknown metadata is visible as text");
    Check(TableText.Display(frame.Target,true)=="Unknown"&&TableText.Display(null,true)=="Unknown","Unknown target replaced with dash");Check(frame.ExposureText=="-"&&frame.GainText=="-"&&frame.TemperatureText=="-"&&frame.SizeText=="-"&&frame.PixelCount==null,"Absent numeric fields are not dashes");Check(reopened.Kind=="Unknown"&&reopened.Calibration=="Unknown","Presentation changed persisted metadata");Check(TableText.Display("Seestar (model unknown)")=="Seestar / -"&&TableText.Display("Bias")=="Bias"&&TableText.Display("Uncalibrated")=="Uncalibrated","Known metadata hidden");frame.Exposure=0;frame.Gain=0;frame.Temperature=0;Check(frame.ExposureText=="0 s"&&frame.GainText=="0"&&frame.TemperatureText=="0 °C","Zero values mistaken for absent metadata");
   });
   Test("Pinch zoom retains its focal point and fit recenters after panning",()=>{
    var zoom=new PreviewZoom();zoom.Fit(800,600,400,200);Check(zoom.Scale==2&&zoom.X==0&&zoom.Y==100,"Fit did not center the preview");double imageX=(300-zoom.X)/zoom.Scale,imageY=(250-zoom.Y)/zoom.Scale;zoom.Zoom(1.8,300,250);Check(Math.Abs((300-zoom.X)/zoom.Scale-imageX)<0.000001&&Math.Abs((250-zoom.Y)/zoom.Scale-imageY)<0.000001,"Pinch moved the focal image pixel");zoom.Pan(40,-20);zoom.Fit(800,600,400,200);Check(zoom.Scale==2&&zoom.X==0&&zoom.Y==100,"Fit did not recover the image after panning");zoom.Zoom(100000,200,100);Check(zoom.Scale==32,"Zoom upper bound absent");zoom.Zoom(0.00000001,200,100);Check(zoom.Scale==0.01,"Zoom lower bound absent");zoom.Zoom(double.NaN,0,0);Check(zoom.Scale==0.01&&!double.IsNaN(zoom.X),"Invalid gesture corrupted preview transform");
   });
   Test("FITS preview stretch handles negative blank constant and invalid pixels",()=>{
    var image=new FitsImage{Width=6,Height=1,Pixels=new[]{-20.0,-10,0,20,double.NaN,double.PositiveInfinity}};var pixels=PreviewPixels.Render(image);Check(pixels[0]==0&&pixels[3]==255&&pixels[1]>pixels[0]&&pixels[2]>pixels[1]&&pixels[4]==0&&pixels[5]==0,"Stretch mishandled valid or blank pixels");Check(PreviewPixels.Render(new FitsImage{Width=2,Height=1,Pixels=new[]{123.0,123.0}}).All(p=>p==127),"Constant bias preview failed");Check(PreviewPixels.Render(new FitsImage{Width=1,Height=1,Pixels=new[]{double.NaN}})[0]==0,"All-blank image failed");Expect(()=>PreviewPixels.Render(new FitsImage{Width=2,Height=1,Pixels=new[]{1.0}}),"Invalid dimensions accepted");
   });
   Test("Bias filename header and master frames remain visible in the repository",()=>{
    string source=Path.Combine(root,"bias-visible-source");Directory.CreateDirectory(source);Write(Path.Combine(source,"Bias.fit"),64,48,(x,y)=>1010,new Dictionary<string,string>());Write(Path.Combine(source,"Master_Bias.fits"),64,48,(x,y)=>1020,new Dictionary<string,string>());Write(Path.Combine(source,"header_only.fit"),64,48,(x,y)=>1030,new Dictionary<string,string>{{"IMAGETYP","'BIAS'"},{"EXPTIME","0"}});Write(Path.Combine(source,"offset.fit"),64,48,(x,y)=>1040,new Dictionary<string,string>());
    using(var repository=new Repository(Path.Combine(root,"bias-visible-repo"))){var scan=repository.Scan(source,"Bias-scope","Auto",ct,NoProgress);Check(scan.Frames.Count(f=>f.Kind=="Bias")==3&&scan.Frames.Count(f=>f.Kind=="Master bias")==1,"Bias captures not classified");repository.Import(scan.Frames,ct,NoProgress);var rows=repository.All();Check(rows.Count==4&&rows.All(f=>f.Target=="Calibration"&&TableText.Display(f.Kind).IndexOf("bias",StringComparison.OrdinalIgnoreCase)>=0),"Bias captures missing from repository table data");Check(rows.Single(f=>f.OriginalName=="header_only.fit").ExposureText=="0 s","Bias zero exposure hidden");}
   });
  }
 }
}
