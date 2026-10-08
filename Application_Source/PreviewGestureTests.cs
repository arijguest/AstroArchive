using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void PreviewGestureTests(){
   Test("Large previews retain source orientation and fit every quarter turn",()=>{
    foreach(var size in new[]{new[]{640.0,480.0},new[]{480.0,640.0}})foreach(int turn in new[]{-1,0,1,2,3,4,5}){var geometry=new PreviewGeometry(size[0],size[1],turn);int normalized=(turn%4+4)%4;Check(geometry.QuarterTurns==normalized&&geometry.Width==size[normalized%2]&&geometry.Height==size[1-normalized%2],"Rotation geometry changed aspect/orientation");double width,height;geometry.Frame(900,700,out width,out height);Check(width<=900&&height<=700&&Math.Abs(width/height-geometry.Width/geometry.Height)<0.000001,"Rotated preview did not fit source aspect");}
   });
   Test("Portrait previews keep the whole image without letterboxing across frame sizes",()=>{
    foreach(var dimensions in new[]{new[]{420.0,320.0},new[]{320.0,420.0},new[]{320.0,320.0},new[]{1400.0,200.0}}){
     var geometry=new PreviewGeometry(dimensions[0],dimensions[1]);Check(geometry.Width<=geometry.Height&&geometry.Rotated==(dimensions[0]>dimensions[1]),"Preview orientation is not portrait");
     foreach(var frame in new[]{new[]{264.0,520.0},new[]{700.0,260.0},new[]{160.0,300.0}}){double width,height;geometry.Frame(frame[0],frame[1],out width,out height);Check(width<=frame[0]+0.000001&&height<=frame[1]+0.000001,"Preview exceeds the available frame");var zoom=new PreviewZoom();zoom.Fit(width,height,geometry.Width,geometry.Height);Check(Math.Abs(zoom.X)<0.000001&&Math.Abs(zoom.Y)<0.000001,"Preview has black bars");
      double fitted=zoom.Scale;zoom.Zoom(2,width/2,height/2);zoom.Pan(-10000,10000);zoom.Constrain(width,height,geometry.Width,geometry.Height);Check(zoom.X<=0&&zoom.Y<=0&&zoom.X+geometry.Width*zoom.Scale>=width-0.000001&&zoom.Y+geometry.Height*zoom.Scale>=height-0.000001,"Panning exposes empty borders");zoom.Zoom(0.01,width/2,height/2);zoom.Constrain(width,height,geometry.Width,geometry.Height);Check(Math.Abs(zoom.Scale-fitted)<0.000001&&Math.Abs(zoom.X)<0.000001&&Math.Abs(zoom.Y)<0.000001,"Zoom out does not stop at the whole image");
     }
    }
   });
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
