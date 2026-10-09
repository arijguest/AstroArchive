using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public static class ImportPolicy {
  // Keep the persisted IgnoreRasterImports option compatible with older settings.
  public static bool RasterFilename(string path){string extension=Path.GetExtension(path??"");return extension.Equals(".png",StringComparison.OrdinalIgnoreCase)||extension.Equals(".jpg",StringComparison.OrdinalIgnoreCase)||extension.Equals(".jpeg",StringComparison.OrdinalIgnoreCase)||extension.Equals(".mp4",StringComparison.OrdinalIgnoreCase);}
  public static bool UnknownScience(Frame frame){return (frame.Kind=="Light"||frame.Kind=="Stack")&&Catalog.IsAmbiguous(frame.Target)&&CaptureScreening.Importable(frame);}
  public static string Target(string value){
   string target=Catalog.CanonicalTarget(value);
   if(Catalog.IsAmbiguous(target)||target=="Calibration"||Catalog.HasFilenameConflict(value))throw new ArgumentException("Choose one named target or enter a specific object label.");
   return target;
  }
  public static int AssignUnknown(IEnumerable<Frame> frames,string value){
   string target=Target(value);int count=0;
   foreach(var frame in frames.Where(UnknownScience)){
    frame.Target=target;frame.TargetEvidence="User assigned during import";Assets.UserFact(frame,"Target");count++;
   }
   return count;
  }
 }
}
