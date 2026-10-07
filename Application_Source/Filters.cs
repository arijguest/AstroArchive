// Shared filter semantics. Numeric filters use inclusive physical values, never display strings.
using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public enum NumericFilterMode {Any,Known,Between,Unknown}
 public sealed class CaptureRange {
  public NumericFilterMode Mode;public double? Minimum,Maximum;public bool IncludeUnknown;
  public bool Active {get{return Mode!=NumericFilterMode.Any;}}
  public bool Matches(double? value){
   bool known=value.HasValue&&!double.IsNaN(value.Value)&&!double.IsInfinity(value.Value);
   if(Mode==NumericFilterMode.Any)return true;if(Mode==NumericFilterMode.Unknown)return !known;
   if(!known)return Mode==NumericFilterMode.Between&&IncludeUnknown;
   if(Mode==NumericFilterMode.Known)return true;
   return (!Minimum.HasValue||value.Value>=Minimum.Value)&&(!Maximum.HasValue||value.Value<=Maximum.Value);
  }
 }
 public sealed class CaptureFilters {
  public readonly Dictionary<string,string> Values=new Dictionary<string,string>();
  public readonly Dictionary<string,CaptureRange> Ranges=new Dictionary<string,CaptureRange>();
  public static readonly string[] Fields={"Target","Session","Device","Frame type","Optical filter","Review","Camera","Mount","Calibration","Dimensions","Status","Mosaic","Panel","Mosaic state","Format","Capabilities"};
  public static readonly string[] Primary={"Target","Session","Device","Frame type","Optical filter","Review"};
  public static readonly string[] Advanced={"Camera","Mount","Calibration","Dimensions","Status","Mosaic","Panel","Mosaic state","Format","Capabilities"};
  public static readonly string[] ReviewChoices={"Needs review","No issues flagged","Passed","Not screened","Telescope rejected / reference","File integrity problem","Transfer failure"};
  public int ActiveCount {get{return Values.Count+Ranges.Count(p=>p.Value.Active);}}
  public void Reset(){Values.Clear();Ranges.Clear();}
  public static double? Number(Frame frame,string field){double? value=field=="Exposure"?frame.Exposure:field=="Gain"?frame.Gain:null;return value.HasValue&&!double.IsNaN(value.Value)&&!double.IsInfinity(value.Value)&&(field!="Exposure"||value.Value>=0)?value:null;}
  public static string Value(Frame frame,string field){
   string value;
   switch(field){
    case "Target":value=frame.Target;break;
    case "Mosaic":value=frame.MosaicText;break;
    case "Panel":value=frame.PanelText;break;
    case "Mosaic state":value=frame.MosaicLabels!=null&&frame.MosaicLabels.Count>0?string.Join("; ",frame.MosaicLabels.Select(m=>m.State).Distinct()):frame.Mosaic==null||frame.MosaicDismissed?"-":frame.Mosaic.Conflict!=null||!frame.Mosaic.Declared||frame.Mosaic.PanelKey==null&&!frame.Mosaic.Output?"Suggested":"Declared";break;
    case "Device":value=frame.Telescope;break;
    case "Frame type":value=frame.Kind;break;
    case "Mount":value=MountLabels.Type(frame.MountText);if(value.Length==0)value=frame.MountText;break;
    case "Format":value=frame.Format;break;
    case "Capabilities":value=frame.CapabilityText;break;
    case "Night":value=frame.Night;break;
    case "Exposure":value=frame.ExposureText;break;
    case "Gain":value=frame.GainText;break;
    case "Camera":value=frame.Camera;break;
    case "Session":return frame.SessionKey;
    case "Optical filter":value=frame.Filter;break;
    case "Calibration":value=frame.Calibration;break;
    case "Dimensions":value=frame.Width>0&&frame.Height>0?frame.SizeText:"Unknown";break;
    case "Status":value=frame.Status;break;
    case "Review":return frame.ReviewText;
    case "Review type":return frame.ReviewCategory;
    default:throw new ArgumentException("Unknown capture filter: "+field);
   }
   return string.IsNullOrWhiteSpace(value)?"Unknown":value;
  }
  public static bool Useful(IEnumerable<Frame> frames,string field,bool active){
   if(active||field=="Target"||field=="Session"||field=="Review"||field=="Frame type")return true;
   return field=="Exposure"||field=="Gain"?frames.Select(f=>Number(f,field)).Distinct().Take(2).Count()>1:Options(frames,field).Take(2).Count()>1;
  }
  public static IEnumerable<string> Options(IEnumerable<Frame> frames,string field){return frames.SelectMany(f=>new[]{"Mosaic","Panel","Mosaic state"}.Contains(field)&&f.MosaicLabels!=null&&f.MosaicLabels.Count>0?f.MosaicLabels.Select(m=>field=="Mosaic"?m.Name:field=="Panel"?m.Panel:m.State):new[]{Value(f,field)}).Distinct().OrderBy(v=>v);}
  public List<Frame> Apply(IEnumerable<Frame> frames,string search){
   var words=Util.Tokens(search);
   return frames.Where(f=>Values.All(pair=>Matches(f,pair.Key,pair.Value))&&Ranges.All(pair=>pair.Value.Matches(Number(f,pair.Key)))&&words.All(word=>f.SearchText.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
  }
  static bool Matches(Frame frame,string field,string value){
   if(field=="Target")return frame.Target==Catalog.CanonicalTarget(value);
   if(frame.MosaicLabels!=null&&frame.MosaicLabels.Count>0){if(field=="Mosaic")return frame.MosaicLabels.Any(m=>m.Name==value);if(field=="Panel")return frame.MosaicLabels.Any(m=>m.Panel==value);if(field=="Mosaic state")return frame.MosaicLabels.Any(m=>m.State==value);}
   if(field=="Review"||field=="Review type"){
    if(value=="No issues flagged")return !CaptureScreening.NeedsReview(frame);
    if(value=="Needs review")return CaptureScreening.NeedsReview(frame);
    if(value=="Rejected / reference"||value=="Telescope rejected / reference")return frame.Rejected;
    if(value=="File integrity problem")return CaptureScreening.FileProblem(frame);
    if(value=="Transfer failure")return frame.Status=="Failed";
   }
   return Value(frame,field)==value;
  }
 }
}
