using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public sealed class EditedMetadata {
  public EditedMetadata Clone(){return (EditedMetadata)MemberwiseClone();}
  public string ImageClass{get;set;} public string Object{get;set;} public string Filters{get;set;} public string RA{get;set;} public string Dec{get;set;}
  public int? Subs{get;set;} public double? SubExposure{get;set;} public double? TotalExposure{get;set;} public double? ReportedExposure{get;set;} public string Evidence{get;set;}
  public string ObjectLabel{get{return ImageClass=="Meteor"?"Meteor":string.IsNullOrEmpty(Object)?"Unknown":Catalog.Label(Object);}}
  public string ObjectId{get{return ImageClass=="Meteor"?"":Catalog.ObjectId(Object);}}
  public string TargetName{get{string common=Catalog.CommonName(Object);return ImageClass=="Meteor"?"Meteor":common.Length>0?common:ObjectId.Length==0?ObjectLabel:"";}}
  public string TotalExposureText{get{return TotalExposure.HasValue?TotalExposure.Value.ToString("0.###",CultureInfo.InvariantCulture)+" s":"Unknown";}}
  public string SubExposureText{get{return SubExposure.HasValue?SubExposure.Value.ToString("0.###",CultureInfo.InvariantCulture)+" s":"Unknown / mixed";}}
  public string SubsText{get{return Subs.HasValue?Subs.Value.ToString(CultureInfo.InvariantCulture):"Unknown";}}
  public string FiltersLabel{get{return string.IsNullOrWhiteSpace(Filters)?"Unknown":Filters;}}
  public string Details{get{return "Object: "+ObjectLabel+"\nFilters / channels: "+FiltersLabel+"\nTotal exposure: "+TotalExposureText+"\nSubs: "+SubsText+"\nPer sub: "+SubExposureText+(ReportedExposure.HasValue?"\nReported exposure: "+ReportedExposure.Value.ToString("G",CultureInfo.InvariantCulture)+" s":"")+(string.IsNullOrEmpty(RA)?"":"\nRA (as recorded): "+RA)+(string.IsNullOrEmpty(Dec)?"":"\nDec (as recorded): "+Dec)+"\n\n"+Evidence;}}
  static readonly Regex Product=new Regex(@"(?<![\d.])(?<count>\d+)\s*[x×]\s*(?<duration>\d+(?:\.\d+)?)\s*(?<unit>ms|secs?|seconds?|s|mins?|minutes?|m|hours?|h)(?=$|[^a-z\d])",RegexOptions.IgnoreCase);
  static double? Positive(double? value){return value.HasValue&&value>0&&!double.IsNaN(value.Value)&&!double.IsInfinity(value.Value)?value:null;}
  static int? Count(double? value){return value.HasValue&&value>0&&value<=int.MaxValue&&value==Math.Floor(value.Value)?(int?)value.Value:null;}
  static double? Number(string value){double number;return double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)?Positive(number):null;}
  static double Unit(string unit){unit=unit.ToLowerInvariant();return unit=="ms"?0.001:unit.StartsWith("h")?3600:unit.StartsWith("m")?60:1;}
  static string FilenameFilters(string filename){
   var filters=new List<string>();foreach(Match match in Regex.Matches(filename??"",@"(?:^|[^a-z\d])(LRGB|RGB|SHO|HOO|HA|H-ALPHA|HALPHA|OIII|O3|SII|S2|LUMINANCE|LUM|L|R|G|B|IRCUT|DUAL[_ -]?BAND)(?=$|[^a-z\d])",RegexOptions.IgnoreCase)){
    string value=match.Groups[1].Value.ToUpperInvariant().Replace("_"," ").Replace("-"," ");filters.Add(value=="HA"||value=="H ALPHA"||value=="HALPHA"?"Ha":value=="O3"?"OIII":value=="S2"?"SII":value=="LUMINANCE"||value=="LUM"?"L":value);
   }return string.Join(", ",filters.Distinct());
  }
  public static EditedMetadata Read(string filename,FitsHeader header,EditedMetadata original=null){
   header=header??new FitsHeader();string leaf=Path.GetFileName(filename??"");string hint=leaf+" "+header.Get("IMAGETYP","PROCTYPE","PROCESS");var notes=new List<string>();
   bool starless=Regex.IsMatch(hint,@"(?:^|[^a-z])star[_ -]?less(?=$|[^a-z])",RegexOptions.IgnoreCase),stars=Regex.IsMatch(hint,@"(?:^|[^a-z])(?:stars?[_ -]?only|stars?[_ -]?layer|stars)(?=$|[^a-z])",RegexOptions.IgnoreCase);
   var result=new EditedMetadata{ImageClass=Util.MeteorFilename(leaf)?"Meteor":starless&&stars?"Unknown (conflicting labels)":starless?"Starless":stars?"Stars only":"Edited image",RA=header.Get("OBJCTRA","RA","CRVAL1"),Dec=header.Get("OBJCTDEC","DEC","CRVAL2")};
   string target=new[]{"OBJECT","OBJNAME","TARGET"}.Select(k=>header.Get(k)).FirstOrDefault(v=>!Catalog.IsAmbiguous(v)),fileTarget=Catalog.TargetFromFilename(leaf)??(Catalog.HasFilenameConflict(leaf)?null:Catalog.TargetFromFilename(filename));bool named=!Catalog.IsAmbiguous(target);result.Object=named?Catalog.Normalize(target):fileTarget;
   if(!string.IsNullOrEmpty(result.Object))notes.Add(named?"Object from image metadata":"Object from filename");
   result.Filters=header.Get("FILTER","FILTERID","FILTNAME");if(string.IsNullOrEmpty(result.Filters)||result.Filters.Equals("Unknown",StringComparison.OrdinalIgnoreCase)){result.Filters=FilenameFilters(leaf);if(result.Filters.Length==0)result.Filters=FilenameFilters(filename);if(result.Filters.Length>0)notes.Add("Filters/channel labels from filename");}else notes.Add("Filter from image metadata");
   result.Subs=Count(header.Number("NCOMBINE","STACKCNT","NSTACK","STACKNUM","NSUBS","SUBCOUNT"));result.SubExposure=Positive(header.Number("SUBEXP","SUBEXPT","EXPOSUB","EXP_SUB","SUBTIME"));result.TotalExposure=Positive(header.Number("TOTALEXP","TOTEXP","EXPTOTAL","INTTIME","INTEGRAT"));result.ReportedExposure=Positive(header.Number("EXPTIME","EXPOSURE","EXP_TIME"));
   string comment;header.Comments.TryGetValue("EXPTIME",out comment);if(result.ReportedExposure.HasValue&&comment!=null){if(Regex.IsMatch(comment,@"total|integrat",RegexOptions.IgnoreCase))result.TotalExposure=result.TotalExposure??result.ReportedExposure;else if(Regex.IsMatch(comment,@"per[ _-]?(sub|frame)|individual|single",RegexOptions.IgnoreCase))result.SubExposure=result.SubExposure??result.ReportedExposure;}
   if(result.Subs.HasValue||result.SubExposure.HasValue||result.TotalExposure.HasValue)notes.Add("Exposure/sub count from image metadata");
   double? filenameSub,filenameGain;if(Classifier.FilenameExposureGain(leaf,out filenameSub,out filenameGain)){
    if(result.SubExposure.HasValue&&Math.Abs(result.SubExposure.Value-filenameSub.Value)>0.001)notes.Add("Filename sub exposure disagrees with metadata; metadata retained");
    else{result.SubExposure=result.SubExposure??filenameSub;notes.Add("Per-sub exposure from filename exposure/gain settings; gain is not a sub-count");}
   }
   var products=Product.Matches(filename??"").Cast<Match>().Select(m=>new{Count=Count(Number(m.Groups["count"].Value)),Exposure=Number(m.Groups["duration"].Value),Unit=Unit(m.Groups["unit"].Value)}).Where(p=>p.Count.HasValue&&p.Exposure.HasValue).ToList();
   if(products.Count>0){long count=products.Sum(p=>(long)p.Count.Value);double total=products.Sum(p=>p.Count.Value*p.Exposure.Value*p.Unit);var durations=products.Select(p=>p.Exposure.Value*p.Unit).Distinct().ToList();
    bool conflict=result.Subs.HasValue&&result.Subs.Value!=count||result.TotalExposure.HasValue&&Math.Abs(result.TotalExposure.Value-total)>0.001||result.SubExposure.HasValue&&(durations.Count!=1||Math.Abs(result.SubExposure.Value-durations[0])>0.001);
    if(conflict)notes.Add("Filename exposure disagrees with metadata; metadata retained and missing values remain unknown");
    else{if(count<=int.MaxValue)result.Subs=result.Subs??(int)count;result.TotalExposure=result.TotalExposure??Positive(total);if(durations.Count==1)result.SubExposure=result.SubExposure??Positive(durations[0]);notes.Add("Exposure from filename count × duration"+(durations.Count>1?" (mixed sub durations)":""));}
   }else{
    var subs=Regex.Match(filename??"",@"(?:^|[^a-z\d])(?:subs?|frames?)[_ =-]*(\d+)(?=$|[^\d])|(?<!\d)(\d+)[_ -]*(?:subs?|frames?)(?=$|[^a-z])",RegexOptions.IgnoreCase);if(subs.Success)result.Subs=result.Subs??Count(Number(subs.Groups[1].Success?subs.Groups[1].Value:subs.Groups[2].Value));
    var total=Regex.Match(filename??"",@"(?:total(?:exp(?:osure)?)?|integration)[_ =-]*(\d+(?:\.\d+)?)[_ ]*(ms|secs?|seconds?|s|mins?|minutes?|m|hours?|h)(?=$|[^a-z\d])",RegexOptions.IgnoreCase);if(total.Success){var value=Positive((Number(total.Groups[1].Value)??0)*Unit(total.Groups[2].Value));if(result.TotalExposure.HasValue&&value.HasValue&&Math.Abs(result.TotalExposure.Value-value.Value)>0.001)notes.Add("Labelled filename total disagrees with metadata; metadata retained");else{result.TotalExposure=result.TotalExposure??value;notes.Add("Total exposure from labelled filename value");}}
   }
   bool inherit=original!=null&&(string.IsNullOrEmpty(result.Object)||string.IsNullOrEmpty(original.Object)||Catalog.CanonicalTarget(result.Object)==Catalog.CanonicalTarget(original.Object))&&(!result.Subs.HasValue||!original.Subs.HasValue||result.Subs==original.Subs)&&(!result.SubExposure.HasValue||!original.SubExposure.HasValue||result.SubExposure==original.SubExposure)&&(!result.TotalExposure.HasValue||!original.TotalExposure.HasValue||Math.Abs(result.TotalExposure.Value-original.TotalExposure.Value)<0.001)&&(string.IsNullOrEmpty(result.Filters)||string.IsNullOrEmpty(original.Filters)||result.Filters.Equals(original.Filters,StringComparison.OrdinalIgnoreCase));
   if(original!=null&&!inherit)notes.Add("Acquisition details differ from the source; missing exposure data is not inherited");
   if(inherit){result.Object=result.Object??original.Object;if(string.IsNullOrEmpty(result.Filters))result.Filters=original.Filters;result.Subs=result.Subs??original.Subs;result.SubExposure=result.SubExposure??original.SubExposure;result.TotalExposure=result.TotalExposure??original.TotalExposure;result.ReportedExposure=result.ReportedExposure??original.ReportedExposure;if(result.RA.Length==0)result.RA=original.RA;if(result.Dec.Length==0)result.Dec=original.Dec;notes.Add("Missing acquisition details retained from the imported image / archived working copy");}
   if(!result.TotalExposure.HasValue&&result.Subs.HasValue&&result.SubExposure.HasValue)result.TotalExposure=Positive(result.Subs.Value*result.SubExposure.Value);
   if(!result.SubExposure.HasValue&&result.Subs.HasValue&&result.TotalExposure.HasValue)notes.Add("Sub duration not inferred from a total that may combine different exposures or filters");
   if(result.ReportedExposure.HasValue&&!result.SubExposure.HasValue&&!result.TotalExposure.HasValue)notes.Add("EXPTIME/EXPOSURE meaning is unspecified; it is not assumed to be per sub or total");
   if(result.ImageClass=="Meteor"){result.Object=null;notes.Add("Meteor filename label; object identity omitted");}
   result.Evidence=string.Join("\n",notes);return result;
  }
 }
}
