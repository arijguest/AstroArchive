using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  // A simple TAN solution: 2000 × 1500 pixels at 3.6"/pixel, rotated and optionally mirrored, centred on the Orion Nebula.
  static FitsHeader OverlayWcs(double rotation,bool mirrored,double ra=83.82208,double dec=-5.39111,double scale=0.001){
   double t=rotation*Math.PI/180,sx=mirrored?scale:-scale;var h=new FitsHeader();
   foreach(var card in new Dictionary<string,double>{{"CRVAL1",ra},{"CRVAL2",dec},{"CRPIX1",1000.5},{"CRPIX2",750.5},{"CD1_1",sx*Math.Cos(t)},{"CD1_2",-scale*Math.Sin(t)},{"CD2_1",sx*Math.Sin(t)},{"CD2_2",scale*Math.Cos(t)}})h.Values[card.Key]=card.Value.ToString("R",CultureInfo.InvariantCulture);
   h.Values["CTYPE1"]="RA---TAN";h.Values["CTYPE2"]="DEC--TAN";return h;
  }
  static SkyPoint OverlayPixel(FitsHeader h,double x,double y){
   Func<string,double> n=k=>h.Number(k).Value;
   return SkyWcs.Inverse(n("CD1_1")*(x-n("CRPIX1"))+n("CD1_2")*(y-n("CRPIX2")),n("CD2_1")*(x-n("CRPIX1"))+n("CD2_2")*(y-n("CRPIX2")),n("CRVAL1"),n("CRVAL2"));
  }
  static void TargetOverlayTests(){
   Test("Target overlay places catalogue objects at their solved pixels in any rotation or parity",()=>{
    foreach(double rotation in new[]{0.0,37,-120})foreach(bool mirrored in new[]{false,true}){
     var header=OverlayWcs(rotation,mirrored);var frame=new Frame{Target="Test",Kind="Light",Width=2000,Height=1500,Sky=SkyWcs.FromHeader(header,2000,1500)};Check(frame.Sky!=null&&frame.Sky.HasFootprint,"Fixture WCS rejected");
     var pixels=new[]{new[]{1.0,1.0},new[]{501.0,376},new[]{1999.5,1200},new[]{1000.5,750.5}};var catalogue=new List<CatalogObject>();
     for(int i=0;i<pixels.Length;i++){var p=OverlayPixel(header,pixels[i][0],pixels[i][1]);catalogue.Add(new CatalogObject{Name="T"+i,Type="G",RA=p.RA,Dec=p.Dec,Diameter=0.2});}
     var outside=OverlayPixel(header,-40,700);catalogue.Add(new CatalogObject{Name="Outside",Type="G",RA=outside.RA,Dec=outside.Dec});
     foreach(bool flip in new[]{false,true}){
      var marks=TargetOverlay.For(frame,flip,catalogue).Marks.ToDictionary(m=>m.Name);Check(marks.Count==4&&!marks.ContainsKey("Outside"),"Wrong objects in field at "+rotation+"°: "+string.Join(",",marks.Keys));
      for(int i=0;i<pixels.Length;i++){var m=marks["T"+i];double u=(pixels[i][0]-0.5)/2000,v=(pixels[i][1]-0.5)/1500;if(flip)v=1-v;Check(Math.Abs(m.X-u)<2e-4&&Math.Abs(m.Y-v)<2e-4,"Mark T"+i+" misplaced at "+rotation+"° mirrored="+mirrored+" flip="+flip+": "+m.X+","+m.Y+" vs "+u+","+v);}
      Check(Math.Abs(marks["T1"].RadiusX*2000-110)<0.5&&Math.Abs(marks["T1"].RadiusY*1500-110)<0.5,"Radius not in pixels of the solved scale: "+marks["T1"].RadiusX*2000);
     }
    }
   });
   Test("Target overlay shows every visible object, including circles that reach in from outside the frame",()=>{
    var header=OverlayWcs(0,false);var frame=new Frame{Target="M42",Kind="Light",Width=2000,Height=1500,Sky=SkyWcs.FromHeader(header,2000,1500)};
    var result=TargetOverlay.For(frame,true);Check(result.Marks.Count>0&&result.Marks[0].Name=="M42"&&result.Marks[0].Primary&&result.Marks.Count(m=>m.Primary)==1,"Capture target not first: "+string.Join(",",result.Marks.Select(m=>m.Name)));
    Check(result.Marks.Any(m=>m.Name=="M43")&&result.Marks.Any(m=>m.Name=="NGC1977")&&result.Note=="","Orion field incomplete: "+string.Join(",",result.Marks.Select(m=>m.Label)));
    Check(result.Marks[0].Label=="M42"&&result.Marks.First(m=>m.Name=="NGC1977").Label=="NGC 1977"&&TargetOverlay.ShortId("IC1396A")=="IC 1396A","Labels are not short object IDs");
    var centre=OverlayPixel(header,1000.5,750.5);var crowded=Enumerable.Range(0,40).Select(i=>new CatalogObject{Name="G"+i,Type=i%5==0?"*":"G",RA=centre.RA+i*0.01,Dec=centre.Dec,Magnitude=20-i*0.1}).ToList();
    crowded.Add(new CatalogObject{Name="G1",Type="G",RA=centre.RA,Dec=centre.Dec+0.1});crowded.Add(new CatalogObject{Name="Twin",Type="Dup",RA=centre.RA,Dec=centre.Dec});crowded.Add(new CatalogObject{Name="Ghost",Type="NonEx",RA=centre.RA,Dec=centre.Dec});
    var edge=OverlayPixel(header,-100,750.5);crowded.Add(new CatalogObject{Name="Edge",Type="Neb",RA=edge.RA,Dec=edge.Dec,Diameter=0.4});var far=OverlayPixel(header,-600,750.5);crowded.Add(new CatalogObject{Name="Far",Type="Neb",RA=far.RA,Dec=far.Dec,Diameter=0.2});
    var all=TargetOverlay.For(frame,false,crowded).Marks;var names=all.Select(m=>m.Name).ToList();
    Check(all.Count==41&&names.Distinct().Count()==41&&names.Contains("G0")&&names.Contains("Edge")&&!names.Contains("Far")&&!names.Contains("Twin")&&!names.Contains("Ghost"),"Visible objects missing or extras shown: "+string.Join(",",names));
    Check(all.First(m=>m.Name=="Edge").X<0,"Edge object not kept outside the frame");
   });
   Test("Target overlay circles cover whole nebulae, including the Elephant's Trunk inside IC 1396",()=>{
    var header=OverlayWcs(0,false,324.2083,57.5,0.0012);var frame=new Frame{Target="Elephant's Trunk Nebula",Kind="Light",Width=2000,Height=1500,Sky=SkyWcs.FromHeader(header,2000,1500)};
    var marks=TargetOverlay.For(frame,true).Marks;var complex=marks.FirstOrDefault(m=>m.Name=="IC1396");var trunk=marks.FirstOrDefault(m=>m.Name=="IC1396A");
    Check(complex!=null&&complex.Primary&&trunk!=null,"Elephant's Trunk field lacks IC 1396 or its trunk: "+string.Join(",",marks.Select(m=>m.Name)));
    Check(Math.Abs(complex.RadiusX*2000*0.0012-170/60.0/2*1.1)<0.01,"IC 1396 circle does not cover the whole nebula: "+complex.RadiusX*2000*0.0012+"°");
    Check(Math.Abs(trunk.X-0.5)<0.01&&Math.Abs(trunk.Y-0.5)<0.01&&trunk.RadiusX*2000*0.0012>=20/60.0/2,"Trunk misplaced or too small: "+trunk.X+","+trunk.Y);
    Check(complex.X<trunk.X&&Math.Abs(complex.X-trunk.X)*2.4>0.25,"IC 1396 centre should lie east of the trunk, not on it");
   });
   Test("Target overlay rows follow the preview and the solution's convention",()=>{
    var solved=new SkyGeometry{Evidence="ASTAP WCS"};var header=new SkyGeometry{Evidence="Existing FITS WCS"};
    Check(TargetOverlay.DisplayFlip(true,header,"FITS")&&TargetOverlay.DisplayFlip(true,solved,"FITS")&&!TargetOverlay.DisplayFlip(false,solved,"XISF")&&TargetOverlay.DisplayFlip(false,header,"XISF")&&!TargetOverlay.DisplayFlip(false,solved,"TIFF"),"Display flip wrong");
   });
   Test("Target overlay explains frames it cannot mark",()=>{
    var none=TargetOverlay.For(new Frame{Target="M42",Kind="Light"},true);Check(none.Marks.Count==0&&none.Note.Contains("no plate solution")&&none.CanSolve,"Unsolved frame note missing");
    var rough=TargetOverlay.For(new Frame{Target="M42",Kind="Light",Sky=new SkyGeometry{RA=83.8,Dec=-5.4,Approximate=true}},true);Check(rough.Marks.Count==0&&rough.Note.Contains("approximate")&&rough.CanSolve,"Approximate pointing was marked");
    var centreOnly=TargetOverlay.For(new Frame{Target="M42",Kind="Light",Sky=new SkyGeometry{RA=83.8,Dec=-5.4}},true);Check(centreOnly.Marks.Count==0&&centreOnly.Note.Length>0,"Centre-only solution was marked");
    var dark=TargetOverlay.For(new Frame{Target="M42",Kind="Dark",Width=2000,Height=1500,Sky=SkyWcs.FromHeader(OverlayWcs(0,false),2000,1500)},true);Check(dark.Marks.Count==0&&dark.Note==""&&!dark.CanSolve,"Calibration frame gained an overlay");
    Check(TargetOverlay.For(null,false).Marks.Count==0,"Missing frame failed");
    var empty=TargetOverlay.For(new Frame{Target="Test",Kind="Light",Width=2000,Height=1500,Sky=SkyWcs.FromHeader(OverlayWcs(0,false),2000,1500)},false,new List<CatalogObject>());Check(empty.Marks.Count==0&&empty.Note.Contains("No catalogue objects")&&!empty.CanSolve,"Empty field note missing");
   });
  }
 }
}
