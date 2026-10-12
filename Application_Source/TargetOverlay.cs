// Optional preview overlay: offline catalogue targets placed on a frame through its plate-solved or WCS footprint.
using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroArchive {
 // X and Y are fractions of the displayed image (0,0 top left); radii are fractions of its width and height.
 public sealed class TargetMark {public string Name,Common,Type;public double X,Y,RadiusX,RadiusY;public bool Primary;
  public string Label{get{return string.IsNullOrWhiteSpace(Common)?Name:Name+" · "+Common;}}
 }
 public sealed class TargetOverlayResult {public List<TargetMark> Marks=new List<TargetMark>();public string Note;}
 public static class TargetOverlay {
  public const int MaxMarks=12;
  static readonly HashSet<string> Ignored=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"*","**","*Ass","Other","Dup","NonEx"};
  // flipY: the preview draws the last stored row at the top, as FITS does. Solved geometry always counts rows from the first stored row.
  public static TargetOverlayResult For(Frame frame,bool flipY,IEnumerable<CatalogObject> catalogue=null){
   var result=new TargetOverlayResult();
   if(frame==null||CaptureSky.IsCalibration(frame)){result.Note="";return result;}
   var sky=frame.Sky;
   if(sky==null||!SkyWcs.ValidPosition(sky.RA,sky.Dec)){result.Note="Plate solve this capture to mark targets in the frame";return result;}
   if(sky.Approximate||!sky.HasFootprint){result.Note="Pointing is approximate, so targets are not marked";return result;}
   var corners=sky.Corners.Select(p=>SkyWcs.Project(p,sky.RA,sky.Dec)).ToList();if(corners.Any(p=>p==null)){result.Note="Field geometry is unusable for marking targets";return result;}
   // Corners run first-row left, first-row right, last-row right, last-row left. The gnomonic plane is linear in pixels for a TAN solution.
   double ax=corners[1][0]-corners[0][0],ay=corners[1][1]-corners[0][1],bx=corners[3][0]-corners[0][0],by=corners[3][1]-corners[0][1],det=ax*by-ay*bx;
   if(Math.Abs(det)<1e-12){result.Note="Field geometry is unusable for marking targets";return result;}
   string target=string.IsNullOrWhiteSpace(frame.Target)?"":Catalog.CanonicalTarget(frame.Target);
   double reach=Math.Sqrt(sky.WidthDegrees*sky.WidthDegrees+sky.HeightDegrees*sky.HeightDegrees)/2+0.01;
   var found=new List<KeyValuePair<CatalogObject,TargetMark>>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var item in catalogue??Catalog.Objects){
    if(item==null||Ignored.Contains(item.Type??"")||!SkyWcs.ValidPosition(item.RA,item.Dec)||Catalog.Distance(sky.RA,sky.Dec,item.RA,item.Dec)>reach)continue;
    var p=SkyWcs.Project(new SkyPoint(item.RA,item.Dec),sky.RA,sky.Dec);if(p==null)continue;
    double dx=p[0]-corners[0][0],dy=p[1]-corners[0][1],u=(dx*by-dy*bx)/det,v=(ax*dy-ay*dx)/det;
    if(u<0||u>1||v<0||v>1||!seen.Add(item.Name))continue;
    string common=Catalog.CommonName(item.Name);double radius=Math.Max(0,item.Diameter)/2;
    found.Add(new KeyValuePair<CatalogObject,TargetMark>(item,new TargetMark{Name=item.Name,Common=string.IsNullOrWhiteSpace(common)?item.Common:common,Type=item.Type,X=u,Y=flipY?1-v:v,RadiusX=radius/sky.WidthDegrees,RadiusY=radius/sky.HeightDegrees,Primary=target.Length>0&&string.Equals(item.Name,target,StringComparison.OrdinalIgnoreCase)}));
   }
   // The capture's own target first, then the well-known objects, then the brightest and largest of the rest.
   result.Marks=found.OrderByDescending(f=>f.Value.Primary).ThenByDescending(f=>PlateSolve.Major(f.Key)).ThenBy(f=>f.Key.Magnitude??99).ThenByDescending(f=>f.Key.Diameter).ThenBy(f=>f.Key.Name,StringComparer.Ordinal).Take(MaxMarks).Select(f=>f.Value).ToList();
   result.Note=result.Marks.Count==0?"No catalogue targets in this field":"";
   return result;
  }
  public static string Tooltip(bool on){return on?"Hide targets in frame":"Show targets in frame (plate-solved captures)";}
 }
}
