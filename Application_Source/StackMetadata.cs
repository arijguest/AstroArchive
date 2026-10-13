using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public static partial class Classifier {
  public static readonly string[] StackCountKeys={"NCOMBINE","STACKCNT","NSTACK","STACKNUM","NSUBS","SUBCOUNT","NFRAMES","NFRAME","STACKN","NEXP"};
  public static readonly string[] StackTotalKeys={"TOTALEXP","TOTEXP","EXPTOTAL","INTTIME","INTEGRAT","TOTEXPT"};
  public static readonly string[] StackSubKeys={"SUBEXP","SUBEXPT","EXPOSUB","EXP_SUB","SUBTIME","EXPTSUB","EXPFRAME"};
  static FitsHeader StackHeader(Frame frame){var image=frame.Images==null?null:frame.Images.FirstOrDefault(i=>i.Key==frame.ImageKey)??frame.Images.FirstOrDefault();return image==null?new FitsHeader():new FitsHeader{Values=image.Headers??new Dictionary<string,string>(),Comments=image.Comments??new Dictionary<string,string>()};}
  static bool PositiveExposure(double? value){return value.HasValue&&value.Value>0&&!double.IsNaN(value.Value)&&!double.IsInfinity(value.Value);}
  public static double? ExposureSeconds(FitsHeader header,params string[] keys){
   foreach(string key in keys){double? value=header.Number(key);if(!PositiveExposure(value))continue;string comment;header.Comments.TryGetValue(key,out comment);return Regex.IsMatch(comment??"",@"\b(ms|milliseconds?)\b",RegexOptions.IgnoreCase)?value/1000:value;}return null;
  }
  static double? StackSubExposure(Frame frame,FitsHeader header){
   var value=ExposureSeconds(header,StackSubKeys);if(PositiveExposure(value))return value;
   string comment;header.Comments.TryGetValue("EXPTIME",out comment);if(Regex.IsMatch(comment??"",@"per[ _-]?(sub|frame)|individual|single",RegexOptions.IgnoreCase)){value=ExposureSeconds(header,"EXPTIME");if(PositiveExposure(value))return value;}
   MetadataFact fact;double seconds;
   if(frame.Facts!=null&&frame.Facts.TryGetValue("SubExposure",out fact)&&fact!=null&&fact.Unit=="s"&&double.TryParse(fact.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out seconds)&&PositiveExposure(seconds))return seconds;
   return null;
  }
  // Session values with explicit second units can supply durations omitted from
  // newer DWARF filenames. Never interpret the 16/32 bit-depth prefix as a count.
  // DWARF's own shotsInfo.json names the per-sub exposure "exp" and the frames that
  // really stacked "shotsStacked"; a stack total that disagrees with them belongs to
  // another stack (a Mega Stack, say), so no duration is taken from the session.
  public static bool ApplyStackSessionMetadata(Frame frame,Dictionary<string,string> values){
   if(frame.Kind!="Stack"||values==null)return false;var header=StackHeader(frame);
   string comment;header.Comments.TryGetValue("EXPTIME",out comment);
   if(PositiveExposure(ExposureSeconds(header,StackSubKeys))||Regex.IsMatch(comment??"",@"per[ _-]?(sub|frame)|individual|single",RegexOptions.IgnoreCase)&&PositiveExposure(ExposureSeconds(header,"EXPTIME"))||UserMetadata(frame,"SubExposure"))return false;
   double? duration=null;string source=null,sourceRaw=null;
   foreach(string key in new[]{"exposure_s","exposureSeconds","exposureTimeSec","exp"}){
    string raw;double seconds;if(!values.TryGetValue(key,out raw)||string.IsNullOrWhiteSpace(raw))continue;
    if(!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out seconds)||!PositiveExposure(seconds)||duration.HasValue&&Math.Abs(duration.Value-seconds)>0.000001)return false;
    if(!duration.HasValue){duration=seconds;source=key;sourceRaw=raw;}
   }if(!duration.HasValue)return false;
   double? named,namedGain;if(FilenameExposureGain(frame.OriginalName,out named,out namedGain)&&Math.Abs(named.Value-duration.Value)>0.000001)return false;
   string stackedRaw;double stacked;
   if(values.TryGetValue("shotsStacked",out stackedRaw)&&double.TryParse(stackedRaw,NumberStyles.Float,CultureInfo.InvariantCulture,out stacked)&&stacked>0&&PositiveExposure(frame.Exposure)&&Math.Abs(frame.Exposure.Value/duration.Value-stacked)>0.000001*Math.Max(1,stacked))return false;
   if(frame.Facts==null)frame.Facts=new Dictionary<string,MetadataFact>();
   return StackFact(frame,"SubExposure",new MetadataFact{Value=duration.Value.ToString("R",CultureInfo.InvariantCulture),Raw=sourceRaw,Source="Session metadata: "+source,Unit="s"});
  }
  public static bool ApplyStackExposure(Frame frame){
   if(frame.Kind!="Stack")return false;var header=StackHeader(frame);bool changed=false;
   if(frame.Facts==null)frame.Facts=new Dictionary<string,MetadataFact>();
   double? declared=header.Number(StackCountKeys);
   if(!UserMetadata(frame,"StackCount")&&frame.StackCount<=0&&declared.HasValue&&declared.Value>0&&declared.Value<=int.MaxValue&&declared.Value==Math.Floor(declared.Value)){
    frame.StackCount=(int)declared.Value;changed=true;StackFact(frame,"StackCount",new MetadataFact{Value=frame.StackCount.ToString(CultureInfo.InvariantCulture),Raw=header.Get(StackCountKeys),Source="Header: combined frames"});
   }
   if(UserMetadata(frame,"Exposure"))return changed;
   double? total=ExposureSeconds(header,StackTotalKeys),sub=StackSubExposure(frame,header);string comment;header.Comments.TryGetValue("EXPTIME",out comment);
   bool explicitTotal=PositiveExposure(total),perFrame=Regex.IsMatch(comment??"",@"per[ _-]?(sub|frame)|individual|single",RegexOptions.IgnoreCase);
   double? reported=ExposureSeconds(header,"EXPTIME","EXPOSURE","EXP_TIME");
   if(!PositiveExposure(sub)&&perFrame)sub=reported;
   double? filenameSub,gain;if(!PositiveExposure(sub)&&FilenameExposureGain(frame.OriginalName,out filenameSub,out gain))sub=filenameSub;
   // A Seestar-style name gives seconds per sub; use it when its count matches the recorded one.
   int namedCount;double namedSeconds;if(!PositiveExposure(sub)&&SeestarStackFilename(frame.OriginalName,out namedCount,out namedSeconds)&&namedCount==frame.StackCount)sub=namedSeconds;
   if(!explicitTotal&&Regex.IsMatch(comment??"",@"total|integrat",RegexOptions.IgnoreCase)){total=reported;explicitTotal=PositiveExposure(total);}
   // A duration explicitly identified as per-frame, or matching a separately
   // recorded sub duration, can be multiplied by a real combined-frame count.
   if(!PositiveExposure(total)&&frame.StackCount>0&&PositiveExposure(sub)&&(perFrame||!PositiveExposure(reported)||Math.Abs(reported.Value-sub.Value)<0.000001))total=sub.Value*frame.StackCount;
   if(!PositiveExposure(total))return changed;
   if(frame.Exposure!=total){frame.Exposure=total;changed=true;}
   return StackFact(frame,"Exposure",new MetadataFact{Value=total.Value.ToString("R",CultureInfo.InvariantCulture),Raw=explicitTotal?header.Get(StackTotalKeys.Concat(new[]{"EXPTIME"}).ToArray()):header.Get("EXPTIME"),Source=explicitTotal?"Header: explicit total exposure":"Stack integration: recorded count / per-sub exposure",Unit="s"})||changed;
  }
 }
 public sealed partial class Repository {
  bool RecoverStackSession(Frame frame,Dictionary<string,Dictionary<string,string>> cache){
   if(frame.Kind!="Stack"||string.IsNullOrEmpty(frame.SidecarRelativePath))return false;
   string relative=frame.SidecarRelativePath;Dictionary<string,string> values;
   if(!cache.TryGetValue(relative,out values)){
    values=null;try{values=PreservedShots(frame,System.Threading.CancellationToken.None);}
    catch(System.IO.InvalidDataException){}catch(System.IO.IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}
    cache[relative]=values;
   }return Classifier.ApplyStackSessionMetadata(frame,values);
  }
 }
}
