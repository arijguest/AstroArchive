// Shared library/import filter semantics; UI menus are built separately.
using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public sealed class CaptureFilters {
  public readonly Dictionary<string,string> Values=new Dictionary<string,string>();
  public static readonly string[] Fields={"Target","Mosaic","Panel","Mosaic state","Device","Frame type","Format","Capabilities","Mount","Camera","Night","Session","Optical filter","Calibration","Exposure","Gain","Dimensions","Status","Review","Review type"};
  public static string Value(Frame frame,string field){
   string value;
   switch(field){
    case "Target":value=frame.Target;break;
    case "Mosaic":value=frame.MosaicText;break;
    case "Panel":value=frame.PanelText;break;
    case "Mosaic state":value=frame.MosaicLabels!=null&&frame.MosaicLabels.Count>0?string.Join("; ",frame.MosaicLabels.Select(m=>m.State).Distinct()):frame.Mosaic==null||frame.MosaicDismissed?"-":frame.Mosaic.Conflict!=null||!frame.Mosaic.Declared||frame.Mosaic.PanelKey==null&&!frame.Mosaic.Output?"Suggested":"Declared";break;
    case "Device":value=frame.Telescope;break;
    case "Frame type":value=frame.Kind;break;case "Format":value=frame.Format;break;case "Capabilities":value=frame.CapabilityText;break;
    case "Mount":value=MountLabels.Type(frame.MountText);if(value.Length==0)value=frame.MountText;break;
    case "Camera":value=frame.Camera;break;
    case "Night":value=frame.Night;break;
    case "Session":value=frame.Session;break;
    case "Optical filter":value=frame.Filter;break;
    case "Calibration":value=frame.Calibration;break;
    case "Exposure":value=frame.ExposureText;break;
    case "Gain":value=frame.GainText;break;
    case "Dimensions":value=frame.SizeText;break;
    case "Status":value=frame.Status;break;
    case "Review":return frame.ReviewText;
    case "Review type":return frame.ReviewCategory;
    default:throw new ArgumentException("Unknown capture filter: "+field);
   }
   return string.IsNullOrWhiteSpace(value)?"Unknown":value;
  }
  public List<Frame> Apply(IEnumerable<Frame> frames,string search){
   var words=Util.Tokens(search);
   return frames.Where(f=>Values.All(pair=>Matches(f,pair.Key,pair.Value))&&words.All(word=>f.SearchText.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
  }
  static bool Matches(Frame frame,string field,string value){if(field=="Target")return frame.Target==Catalog.CanonicalTarget(value);if(frame.MosaicLabels!=null&&frame.MosaicLabels.Count>0){if(field=="Mosaic")return frame.MosaicLabels.Any(m=>m.Name==value);if(field=="Panel")return frame.MosaicLabels.Any(m=>m.Panel==value);if(field=="Mosaic state")return frame.MosaicLabels.Any(m=>m.State==value);}if(field=="Review"&&value=="No issues flagged")return !CaptureScreening.NeedsReview(frame);if(field=="Review"&&value=="Needs review")return CaptureScreening.NeedsReview(frame);if(field=="Review"&&value=="Rejected / reference")return frame.Rejected;if(field=="Review type"){if(value=="Telescope rejected / reference")return frame.Rejected;if(value=="File integrity problem")return CaptureScreening.FileProblem(frame);if(value=="Transfer failure")return frame.Status=="Failed";}return Value(frame,field)==value;}
 }
}
