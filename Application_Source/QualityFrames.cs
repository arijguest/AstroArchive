// Quality score properties for captures, which frames are scored, and how scores reach the in-memory capture list.
// Nothing here is saved with the capture record: scores are recomputed from stored measurements.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AstroArchive {
 public partial class Frame {
  double qualityPercentile=-1;
  [ScriptIgnore]public double? QualityScore{get;set;}
  // 0 = weakest, 1 = strongest among the frames it was compared with; -1 when not scored.
  [ScriptIgnore]public double QualityPercentile{get{return qualityPercentile;}set{qualityPercentile=value;}}
  [ScriptIgnore]public string QualityFlags{get;set;}
  [ScriptIgnore]public string QualityDetail{get;set;}
  [ScriptIgnore]public double? ExposureFit{get;set;}
  [ScriptIgnore]public string ExposureLabel{get;set;}
  [ScriptIgnore]public double QualityOrder{get{return QualityScore.HasValue?QualityScore.Value:-1;}}
  [ScriptIgnore]public double ExposureOrder{get{return ExposureFit.HasValue?ExposureFit.Value:-1;}}
  [ScriptIgnore]public string QualityBand{get{return !QualityScore.HasValue?"":QualityScore.Value>=70?"High":QualityScore.Value>=45?"Mid":"Low";}}
  [ScriptIgnore]public string ExposureBand{get{return !ExposureFit.HasValue?"":ExposureLabel=="Good"?"High":ExposureLabel=="Under"?"Mid":"Low";}}
  [ScriptIgnore]public string QualityText{get{if(!QualityScore.HasValue)return "-";string flag=string.IsNullOrEmpty(QualityFlags)?"":" ⚑";return Math.Round(QualityScore.Value).ToString(CultureInfo.InvariantCulture)+flag;}}
  [ScriptIgnore]public string ExposureFitText{get{return ExposureFit.HasValue?Math.Round(ExposureFit.Value).ToString(CultureInfo.InvariantCulture)+" "+ExposureLabel:"-";}}
  [ScriptIgnore]public string QualityTooltip{get{
   if(!string.IsNullOrEmpty(QualityDetail))return QualityDetail;string reason=QualityEligibility.Reason(this);
   return reason.Length>0?"Not scored: "+reason:"Not measured yet. Use Repository > Review and analysis > Analyse image quality.";}}
  // Filter label for the quality-rank choice list.
  [ScriptIgnore]public string QualityRank{get{
   if(!QualityScore.HasValue)return "Not scored";double p=QualityPercentile;
   return p>=0.9?"Top 10%":p>=0.75?"Top 25%":p>=0.5?"Upper half":p>=0.25?"Lower half":"Weakest 25%";}}
 }

 public static class QualityOrdering {
  // The library's established order (target, date, session), with each session's captures listed best-scored first.
  // Captures without a score (stacks, calibration frames, unmeasured subs) keep their time order after the scored ones.
  public static List<Frame> Order(IEnumerable<Frame> rows){
   return rows.Select(f=>new{Frame=f,Date=CaptureSessions.Date(f)}).OrderBy(f=>f.Frame.Target).ThenBy(f=>f.Date==null?DateTime.MaxValue:f.Date.Date).ThenBy(f=>f.Frame.SessionKey).ThenByDescending(f=>f.Frame.QualityOrder).ThenBy(f=>f.Frame.Observed).ThenBy(f=>f.Frame.OriginalName).Select(f=>f.Frame).ToList();
  }
 }

 public static class QualityEligibility {
  static readonly Regex Mosaic=new Regex("^[RGB]{4}$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  public static bool HasMosaic(Frame frame){return frame!=null&&Mosaic.IsMatch(frame.Bayer??"");}
  // Planetary, lunar and solar stills are scored by disc sharpness instead of star shape.
  public static bool IsDisc(Frame frame){return frame!=null&&ObservationTargets.Unstretched(frame.Target,frame.ObservationMode);}
  public static int KindOf(Frame frame){return IsDisc(frame)?QualityKind.Disc:QualityKind.Stars;}
  // Empty when the capture can be scored; otherwise the short reason it cannot. Reads only stored metadata.
  public static string Reason(Frame f){
   if(f==null)return "No capture";string kind=f.Kind??"";
   if(kind=="Video")return "recordings are not scored";
   if(kind=="Stack"||CaptureSky.IsCalibration(f))return kind=="Stack"?"stacks are not graded":"calibration frames are not scored";
   if(kind!="Light")return "only light frames are scored";
   if(Array.IndexOf(new[]{"Deleted","Missing","Unreadable","Failed","Changed"},f.Status)>=0)return "the archived file is not available";
   if(string.IsNullOrEmpty(f.RelativePath))return "not yet in the repository";
   if(f.Target=="Meteor")return "meteor captures are not scored";
   string format=(f.Format??"").ToUpperInvariant();
   if(format=="JPEG"||format=="JPG"||format=="GIF"||format=="BMP")return "image data is not linear";
   if(format=="SER")return "recordings are not scored";
   if(f.LinearData==false)return "image data is not linear";
   if(f.LinearData==null&&format.Length>0&&format!="FITS"&&format!="XISF")return "linearity of the image data is unknown";
   if(!Assets.CanDecode(f))return "this format cannot be decoded here";
   var image=Assets.Selected(f);
   if(image!=null){
    if(image.Channels!=1&&image.Channels!=3)return "unsupported channel layout";
    if(image.Count>1&&!(f.ImageIndex.HasValue&&f.ImageIndex.Value>=0&&f.ImageIndex.Value<image.Count))return "the file holds several images";
    if(image.Width<96||image.Height<96)return "the image is too small";
    if((long)image.Width*image.Height*image.Channels>Assets.MaxSamples)return "the image exceeds the decoder limit";
   }
   return "";
  }
 }

 public static class QualityAnnotator {
  // Clears and refills the transient quality properties of the supplied captures.
  public static void Apply(IList<Frame> frames,IDictionary<string,QualityMeasurement> measurements,bool compareTarget){
   foreach(var frame in frames){frame.QualityScore=null;frame.QualityPercentile=-1;frame.QualityFlags=null;frame.QualityDetail=null;frame.ExposureFit=null;frame.ExposureLabel=null;}
   if(measurements==null||measurements.Count==0)return;
   var items=new List<QualityItem>();var owners=new List<Frame>();
   foreach(var frame in frames){
    QualityMeasurement m;if(string.IsNullOrEmpty(frame.Hash)||!measurements.TryGetValue(frame.Hash,out m))continue;
    if(m.Version!=QualityMeter.Version||m.Kind!=QualityEligibility.KindOf(frame))continue;
    if(QualityEligibility.Reason(frame).Length>0)continue;
    string scope=m.Kind==QualityKind.Disc||!compareTarget?frame.SessionKey:frame.Target;
    items.Add(new QualityItem{M=m,Cohort=(m.Kind==QualityKind.Disc?frame.Target+"|":"")+frame.Group+"|"+scope});owners.Add(frame);
   }
   if(items.Count==0)return;
   var outcomes=QualityScorer.Compute(items);
   for(int i=0;i<owners.Count;i++){var o=outcomes[i];var f=owners[i];f.QualityScore=o.Score;f.QualityPercentile=o.Percentile;f.QualityFlags=o.Flags;f.QualityDetail=o.Detail;f.ExposureFit=o.ExposureFit;f.ExposureLabel=o.ExposureLabel;}
  }
 }
}
