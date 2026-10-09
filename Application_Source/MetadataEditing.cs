// Shared values and explicit patches for single-file and batch metadata editing.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public sealed class MetadataEditField {
  public string Key,Label; public int Tab; public string[] Options;
  public MetadataEditField(string key,string label,int tab,params string[] options){Key=key;Label=label;Tab=tab;Options=options;}
  public string Read(Frame frame){
   if(Key=="Mount")return frame.MountText;
   if(Key=="Binning")return frame.BinX==0&&frame.BinY==0?"":frame.BinX.ToString(CultureInfo.InvariantCulture)+"x"+frame.BinY.ToString(CultureInfo.InvariantCulture);
   if(Key=="LinearData")return frame.LinearData.HasValue?frame.LinearData.Value?"Linear acquisition data":"Processed / stretched":"";
   object value=typeof(Frame).GetProperty(Key).GetValue(frame,null);
   return value is double?((double)value).ToString("R",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture);
  }
 }
 public sealed class MetadataEditValue {
  public MetadataEditField Field; public string Initial,Text; public bool Mixed; public string[] Values;
  public string KeepLabel{get{return Mixed?"Mixed — keep existing":"Not recorded — keep existing";}}
  public MetadataEditValue(MetadataEditField field,IEnumerable<Frame> frames){
   Field=field;Values=frames.Select(field.Read).Distinct(StringComparer.Ordinal).ToArray();Mixed=Values.Length>1;
   Initial=Mixed?"":Values.FirstOrDefault()??"";Text=Initial;
  }
  public bool Changed{get{
   string value=(Text??"").Trim();if(value.Length==0||value==KeepLabel||value==Initial.Trim())return false;
   double a,b;if(MetadataEditing.Numeric.Contains(Field.Key)&&double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out a)&&double.TryParse(Initial,NumberStyles.Float,CultureInfo.InvariantCulture,out b))return a!=b;
   if(Field.Key=="Binning")return value.ToLowerInvariant().Replace('*','x')!=Initial.ToLowerInvariant();
   return value!=Initial;
  }}
 }
 public sealed class MetadataEditing {
  public static readonly string[] Numeric={"Exposure","Gain","Temperature","Offset"};
  public static readonly MetadataEditField[] Fields={
   new MetadataEditField("Target","Target (object ID or common name)",0),
   new MetadataEditField("Kind","Frame type",0,"Light","Stack","Video","Dark","Master dark","Flat","Master flat","Bias","Master bias","Dark flat","Master dark flat","Unknown","Auxiliary"),
   new MetadataEditField("Exposure","Exposure (s)",0),new MetadataEditField("Gain","Gain",0),
   new MetadataEditField("Temperature","Sensor temperature (°C)",0),new MetadataEditField("Filter","Filter",0),
   new MetadataEditField("Mount","Mount mode",0,"EQ","Alt-Az","Unknown"),
   new MetadataEditField("Telescope","Physical telescope ID",1),
   new MetadataEditField("Model","Instrument model",1,"Seestar S50 Pro","Seestar S50","Seestar S30 Pro","Seestar S30","Dwarf 3","Dwarf II","Dwarf mini","Other"),
   new MetadataEditField("Camera","Camera channel",1),new MetadataEditField("TelescopeModel","Telescope model name",1),
   new MetadataEditField("CameraModel","Camera model",1),new MetadataEditField("CameraId","Physical camera ID / serial",1),
   new MetadataEditField("OpticalConfiguration","Optical configuration ID",1),
   new MetadataEditField("Calibration","Calibration state",2,"Unknown","Uncalibrated","Calibrated","Registered","Device stack","Calibration frame"),
   new MetadataEditField("LinearData","Image data",2,"Linear acquisition data","Processed / stretched"),
   new MetadataEditField("Binning","Binning (x × y, e.g. 1x1)",2),new MetadataEditField("Bayer","Bayer pattern",2),
   new MetadataEditField("Roi","ROI identifier / x,y,width,height",2),new MetadataEditField("Offset","Camera offset",2),
   new MetadataEditField("ReadoutMode","Readout mode",2),new MetadataEditField("GainUnit","Gain unit",2),
   new MetadataEditField("TimeZoneId","Capture timezone (Windows ID)",2),
   new MetadataEditField("RegistrationState","Registration state",2),new MetadataEditField("CalibrationSteps","Calibration steps",2),
   new MetadataEditField("Notes","Notes",2)
  };
  public readonly List<MetadataEditValue> Values;
  public MetadataEditing(IEnumerable<Frame> frames){var selection=frames.ToList();if(selection.Count==0)throw new ArgumentException("Select at least one file.");Values=Fields.Select(f=>new MetadataEditValue(f,selection)).ToList();}
  public MetadataEditValue this[string key]{get{return Values.Single(v=>v.Field.Key==key);}}
  public MetadataPatch Patch(){return new MetadataPatch(Values.Where(v=>v.Changed).ToDictionary(v=>v.Field.Key,v=>v.Text.Trim()));}
 }
 public sealed class MetadataPatch {
  readonly Dictionary<string,string> changes;
  public int Count{get{return changes.Count;}}
  public MetadataPatch(IDictionary<string,string> values){changes=new Dictionary<string,string>(values);foreach(string key in changes.Keys)if(!MetadataEditing.Fields.Any(f=>f.Key==key))throw new ArgumentException("Unsupported metadata field: "+key);}
  public string Validate(){
   foreach(var change in changes){
    if(MetadataEditing.Numeric.Contains(change.Key)){double number;if(!double.TryParse(change.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)||double.IsNaN(number)||double.IsInfinity(number))return Label(change.Key)+": use a finite decimal number with a dot.";}
    if(change.Key=="Binning"&&!Regex.IsMatch(change.Value,@"^[1-9][0-9]?[xX*][1-9][0-9]?$"))return "Enter binning as 1x1, 2x2, etc. (1–99 per axis).";
    if(change.Key=="TimeZoneId"){try{TimeZoneInfo.FindSystemTimeZoneById(change.Value);}catch{return "Enter a valid Windows timezone ID, such as UTC or Eastern Standard Time.";}}
   }
   return null;
  }
  static string Label(string key){return MetadataEditing.Fields.Single(f=>f.Key==key).Label;}
  public Frame Apply(Frame original){
   string error=Validate();if(error!=null)throw new ArgumentException(error);
   Frame item=original.Clone();var facts=new HashSet<string>();
   foreach(var change in changes){
    string key=change.Key,value=change.Value;
    if(key=="Binning"){var parts=value.ToLowerInvariant().Split(new[]{'x','*'});item.BinX=int.Parse(parts[0],CultureInfo.InvariantCulture);item.BinY=int.Parse(parts[1],CultureInfo.InvariantCulture);facts.Add("BinX");facts.Add("BinY");continue;}
    if(key=="Target"){item.Target=Catalog.Normalize(value);item.TargetEvidence="User assigned";facts.Add("TargetEvidence");}
    else if(key=="LinearData")item.LinearData=value=="Linear acquisition data";
    else if(MetadataEditing.Numeric.Contains(key))typeof(Frame).GetProperty(key).SetValue(item,(double?)double.Parse(value,NumberStyles.Float,CultureInfo.InvariantCulture),null);
    else typeof(Frame).GetProperty(key).SetValue(item,value,null);
    facts.Add(key);
    if(key=="Model"){item.Make=InstrumentDetection.MakeOf(value);item.MakeEvidence="User-selected model";facts.Add("Make");facts.Add("MakeEvidence");}
    if(key=="Mount"){item.MountEvidence="User assigned";facts.Add("MountEvidence");}
   }
   if(changes.ContainsKey("Gain")&&!changes.ContainsKey("GainUnit")&&string.IsNullOrWhiteSpace(item.GainUnit)){item.GainUnit="camera units";facts.Add("GainUnit");}
   if(changes.ContainsKey("TimeZoneId")){
    var timezone=TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);DateTime? observed=Util.Time(item.Observed);
    if(observed.HasValue){DateTime local=DateTime.SpecifyKind(observed.Value,DateTimeKind.Unspecified);
     if(timezone.IsInvalidTime(local)||timezone.IsAmbiguousTime(local))throw new InvalidDataException("Capture time is invalid or ambiguous in this timezone: "+item.OriginalName);
     item.ObservedUtc=TimeZoneInfo.ConvertTimeToUtc(local,timezone).ToString("o");item.TimeSource="User timezone";facts.Add("ObservedUtc");facts.Add("TimeSource");
    }
   }
   foreach(string key in facts)Assets.UserFact(item,key);
   if(facts.Contains("Gain"))item.Facts["Gain"].Unit=item.GainUnit;
   if(changes.ContainsKey("Target")&&!changes.ContainsKey("Camera"))CameraDetection.DefaultForTarget(item);
   return item;
  }
 }
 public sealed class MetadataDetail {
  public string Field{get;set;} public string Value{get;set;} public string Source{get;set;}
  public static List<MetadataDetail> For(Frame frame){
   return typeof(Frame).GetProperties().Where(p=>p.CanWrite&&(p.PropertyType==typeof(string)||p.PropertyType==typeof(double?)||p.PropertyType==typeof(bool?)||p.PropertyType==typeof(int)||p.PropertyType==typeof(int?)||p.PropertyType==typeof(long)))
    .Select(p=>{MetadataFact fact=null;if(frame.Facts!=null)frame.Facts.TryGetValue(p.Name,out fact);return new MetadataDetail{Field=Regex.Replace(p.Name,@"([a-z])([A-Z])","$1 $2"),Value=Convert.ToString(p.GetValue(frame,null),CultureInfo.InvariantCulture),Source=fact==null?"":fact.Source+(string.IsNullOrEmpty(fact.Unit)?"":" · "+fact.Unit)};})
    .Where(row=>!string.IsNullOrEmpty(row.Value)).ToList();
  }
 }
}
