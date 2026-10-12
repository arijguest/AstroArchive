// Optional preview overlay: offline catalogue targets placed on a frame through its plate-solved or WCS footprint.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstroArchive {
 // X and Y are fractions of the displayed image (0,0 top left); radii are fractions of its width and height and cover the whole object.
 public sealed class TargetMark {public string Name,Type;public double X,Y,RadiusX,RadiusY;public bool Primary;
  public string Label{get{return TargetOverlay.ShortId(Name);}}
 }
 public sealed class TargetOverlayResult {public List<TargetMark> Marks=new List<TargetMark>();public string Note;public bool CanSolve;}
 public static class TargetOverlay {
  // Duplicate entries repeat another object; nonexistent ones were never real.
  static readonly HashSet<string> Ignored=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"Dup","NonEx"};
  // OpenNGC measures some complexes by their central cluster. These sizes (arcminutes) cover the whole nebula.
  static readonly Dictionary<string,double> FullExtent=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase){{"IC1396",170},{"IC1805",150},{"IC1848",150}};
  // Well-known features that the catalogue folds into a larger object: name, RA and Dec (degrees), size (arcminutes).
  static readonly CatalogObject[] Features={new CatalogObject{Name="IC1396A",Type="HII",RA=324.2083,Dec=57.5000,Diameter=20/60.0}};
  // A small margin keeps each circle just outside its object's catalogued edge.
  const double Margin=1.1;
  public static string ShortId(string name){return Regex.Replace(name??"",@"^(NGC|IC|PGC|UGC|ESO|Mel|Cr|HCG|MWSC)(?=\d)","$1 ");}
  public static IEnumerable<CatalogObject> Catalogue{get{return Catalog.Objects.Concat(Features);}}
  // The overlay draws in display rows. FITS previews draw the last stored row at the top; solved geometry counts rows from the first stored row,
  // except a WCS read from XISF keywords, which follows the FITS bottom-up convention while XISF pixels run top-down.
  public static bool DisplayFlip(bool previewFlip,SkyGeometry sky,string format){
   bool headerXisf=sky!=null&&string.Equals(format,"XISF",StringComparison.OrdinalIgnoreCase)&&(sky.Evidence??"").StartsWith("Existing FITS WCS",StringComparison.Ordinal);
   return previewFlip!=headerXisf;
  }
  public static TargetOverlayResult For(Frame frame,bool flipY,IEnumerable<CatalogObject> catalogue=null){
   if(frame==null||CaptureSky.IsCalibration(frame))return new TargetOverlayResult{Note=""};
   var sky=frame.Sky;
   if(sky==null||!SkyWcs.ValidPosition(sky.RA,sky.Dec))return new TargetOverlayResult{Note="This capture has no plate solution",CanSolve=true};
   if(sky.Approximate||!sky.HasFootprint)return new TargetOverlayResult{Note="This capture has only approximate pointing",CanSolve=true};
   return For(sky,frame.Target,flipY,catalogue);
  }
  public static TargetOverlayResult For(SkyGeometry sky,string target,bool flipY,IEnumerable<CatalogObject> catalogue=null){
   var result=new TargetOverlayResult();
   if(sky==null||!sky.HasFootprint||sky.Approximate||!SkyWcs.ValidPosition(sky.RA,sky.Dec)){result.Note="This image has no plate solution";return result;}
   var corners=sky.Corners.Select(p=>SkyWcs.Project(p,sky.RA,sky.Dec)).ToList();if(corners.Any(p=>p==null)){result.Note="Field geometry is unusable for marking targets";return result;}
   // Corners run first-row left, first-row right, last-row right, last-row left. The gnomonic plane is linear in pixels for a TAN solution.
   double ax=corners[1][0]-corners[0][0],ay=corners[1][1]-corners[0][1],bx=corners[3][0]-corners[0][0],by=corners[3][1]-corners[0][1],det=ax*by-ay*bx;
   if(Math.Abs(det)<1e-12){result.Note="Field geometry is unusable for marking targets";return result;}
   string primary=string.IsNullOrWhiteSpace(target)?"":Catalog.CanonicalTarget(target);
   double halfDiagonal=Math.Sqrt(sky.WidthDegrees*sky.WidthDegrees+sky.HeightDegrees*sky.HeightDegrees)/2;
   var found=new List<KeyValuePair<CatalogObject,TargetMark>>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var item in catalogue??Catalogue){
    if(item==null||Ignored.Contains(item.Type??"")||!SkyWcs.ValidPosition(item.RA,item.Dec))continue;
    double extent;double radius=(FullExtent.TryGetValue(item.Name??"",out extent)?extent/60:Math.Max(0,item.Diameter))/2*Margin;
    if(Catalog.Distance(sky.RA,sky.Dec,item.RA,item.Dec)>halfDiagonal+radius+0.01)continue;
    var p=SkyWcs.Project(new SkyPoint(item.RA,item.Dec),sky.RA,sky.Dec);if(p==null)continue;
    double dx=p[0]-corners[0][0],dy=p[1]-corners[0][1],u=(dx*by-dy*bx)/det,v=(ax*dy-ay*dx)/det,ru=radius/sky.WidthDegrees,rv=radius/sky.HeightDegrees;
    // Keep any object whose circle reaches into the frame, including large ones centred outside it.
    if(u+ru<0||u-ru>1||v+rv<0||v-rv>1||!seen.Add(item.Name))continue;
    found.Add(new KeyValuePair<CatalogObject,TargetMark>(item,new TargetMark{Name=item.Name,Type=item.Type,X=u,Y=flipY?1-v:v,RadiusX=ru,RadiusY=rv,Primary=primary.Length>0&&string.Equals(item.Name,primary,StringComparison.OrdinalIgnoreCase)}));
   }
   // Label priority: the capture's own target, then well-known objects, then the brightest and largest.
   result.Marks=found.OrderByDescending(f=>f.Value.Primary).ThenByDescending(f=>PlateSolve.Major(f.Key)).ThenBy(f=>f.Key.Magnitude??99).ThenByDescending(f=>f.Key.Diameter).ThenBy(f=>f.Key.Name,StringComparer.Ordinal).Select(f=>f.Value).ToList();
   result.Note=result.Marks.Count==0?"No catalogue objects in this field":"";
   return result;
  }
  public static string Tooltip(bool on){return on?"Hide targets in frame":"Show targets in frame";}
 }
}
