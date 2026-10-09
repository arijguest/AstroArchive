// Fill gaps from the file itself, never from neighbouring captures or managed folders.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class MetadataCompletion {
  public Frame Previous,Detected,Updated;
  public readonly List<string> Fields=new List<string>();
  static readonly string[] fields={"Target","Kind","Model","Make","Camera","TelescopeModel","CameraModel","CameraId","AcquisitionSoftware","DeviceProfile","AcquisitionProfile","Exposure","Gain","Temperature","Filter","Mount","Calibration","Bayer","BinX","BinY","StackCount","Width","Height","Channels","RA","Dec","Latitude","Longitude","Offset","ElectronsPerAdu","ReadoutMode","OpticalConfiguration","Roi","LinearData","RegistrationState","CalibrationSteps","Format","VideoDurationSeconds","Observed","AcquisitionDate"};
  static bool Missing(object value){return value==null||value is string&&(string.IsNullOrWhiteSpace((string)value)||new[]{"Unknown","Unknown date","Other / unknown","Auto"}.Contains(((string)value).Trim(),StringComparer.OrdinalIgnoreCase));}
  static bool Empty(Frame frame,string key){var value=typeof(Frame).GetProperty(key).GetValue(frame,null);return Missing(value)||value is int&&(int)value<=0;}
  static bool Protected(Frame frame,string key){MetadataFact fact;return frame.Facts!=null&&frame.Facts.TryGetValue(key,out fact)&&fact!=null&&((fact.Source??"").StartsWith("User",StringComparison.OrdinalIgnoreCase)||(fact.Source??"").StartsWith("Saved",StringComparison.OrdinalIgnoreCase));}
  static bool Conflict(Frame frame,string key){return (frame.MetadataConflicts??new List<string>()).Any(c=>c.StartsWith(key+":",StringComparison.OrdinalIgnoreCase))||key=="Target"&&Catalog.HasFilenameConflict(frame.OriginalName)||key=="Camera"&&(frame.CameraEvidence??"").IndexOf("conflict",StringComparison.OrdinalIgnoreCase)>=0;}
  static bool AliasConflict(Frame frame,string key){
   string[] aliases;
   switch(key){
    case "Target":aliases=new[]{"OBJECT","OBJNAME","TARGET","TARGNAME","OBSTARG"};break;
    case "Exposure":aliases=new[]{"EXPTIME","EXPOSURE","EXP_TIME","EXPOS"};break;
    case "Gain":aliases=new[]{"GAIN","CAMGAIN"};break;
    case "Kind":aliases=new[]{"IMAGETYP","IMAGETYPE","FRAME","FRAMETYP"};break;
    case "Filter":aliases=new[]{"FILTER","FILTERID","FILTNAME"};break;
    case "Observed":case "AcquisitionDate":aliases=new[]{"DATE-OBS","DATEOBS","DATE_OBS"};break;
    default:return false;
   }
   var image=frame.Images==null?null:frame.Images.FirstOrDefault(i=>i.Key==frame.ImageKey)??frame.Images.FirstOrDefault();if(image==null||image.Headers==null)return false;
   var values=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(string alias in aliases){string raw;if(!image.Headers.TryGetValue(alias,out raw)||string.IsNullOrWhiteSpace(raw))continue;double number;
    values.Add(key=="Target"?Catalog.Normalize(raw):double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out number)?number.ToString("R",CultureInfo.InvariantCulture):raw.Trim());
   }
   if(key=="Target"){string filename=Catalog.TargetFromFilename(frame.OriginalName);if(filename!=null&&values.Any(value=>!Catalog.IsAmbiguous(value)&&(Catalog.KnownName(value)!=null||CometTargets.IsComet(value)||ObservationTargets.NamedSolar(value)!=null)&&Catalog.Normalize(filename)!=value))return true;}
   return values.Count>1;
  }
  void Copy(string key){var property=typeof(Frame).GetProperty(key);property.SetValue(Updated,property.GetValue(Detected,null),null);Fields.Add(key);
   MetadataFact fact;if(Detected.Facts!=null&&Detected.Facts.TryGetValue(key,out fact)&&fact!=null)Updated.Facts[key]=Util.Deserialize<MetadataFact>(Util.Serialize(fact));
   else Updated.Facts[key]=new MetadataFact{Value=Convert.ToString(property.GetValue(Updated,null),CultureInfo.InvariantCulture),Raw=Previous.OriginalName,Source="Detected from original file metadata / filename"};
  }
  void Companion(string key){if(Empty(Updated,key)&&!Empty(Detected,key)&&!Protected(Updated,key))Copy(key);}
  public static MetadataCompletion Fill(Frame current,Frame detected){
   var result=new MetadataCompletion{Previous=current.Clone(),Detected=detected,Updated=current.Clone()};
   if(result.Updated.Facts==null)result.Updated.Facts=new Dictionary<string,MetadataFact>();
   foreach(string key in fields){
    if(!Empty(current,key)||Empty(detected,key)||Protected(current,key)||Conflict(current,key)||Conflict(detected,key)||AliasConflict(detected,key))continue;
    string evidence=key=="Camera"?current.CameraEvidence:key=="Mount"?current.MountEvidence:key=="Model"||key=="Make"?current.MakeEvidence:null;
    if(evidence!=null&&(evidence.StartsWith("User",StringComparison.OrdinalIgnoreCase)||evidence.StartsWith("Saved",StringComparison.OrdinalIgnoreCase)))continue;
    if(key=="Camera"&&(detected.CameraEvidence??"").StartsWith("Target-based default"))continue;
    if(key=="StackCount"&&result.Updated.Kind!="Stack")continue;
    if(key=="Exposure"&&detected.Kind!=result.Updated.Kind&&(detected.Kind=="Stack"||detected.Kind=="Video"))continue;
    if((key=="Calibration"||key=="RegistrationState")&&detected.Kind!=result.Updated.Kind)continue;
    string paired=key=="RA"?"Dec":key=="Dec"?"RA":key=="BinX"?"BinY":key=="BinY"?"BinX":null;
    if(paired!=null&&!Empty(current,paired)&&!object.Equals(typeof(Frame).GetProperty(paired).GetValue(current,null),typeof(Frame).GetProperty(paired).GetValue(detected,null)))continue;
    if(key=="Observed"&&!Empty(current,"AcquisitionDate")&&current.AcquisitionDate!=detected.AcquisitionDate)continue;
    result.Copy(key);
    string companion=key=="Target"?"TargetEvidence":key=="Camera"?"CameraEvidence":key=="Model"||key=="Make"?"MakeEvidence":key=="Mount"?"MountEvidence":null;
    if(companion!=null){typeof(Frame).GetProperty(companion).SetValue(result.Updated,typeof(Frame).GetProperty(companion).GetValue(detected,null),null);}
    if(key=="Gain")result.Companion("GainUnit");
    if(key=="Exposure"&&detected.Kind=="Video")result.Companion("VideoDurationSource");
    if(key=="AcquisitionDate"){result.Companion("AcquisitionDateSource");result.Companion("Night");}
    if(key=="Observed"){result.Updated.TimeSource=detected.TimeSource;result.Companion("ObservedUtc");result.Companion("TimeZoneId");result.Companion("Night");}
   }
   if(!Conflict(detected,"Camera")&&CameraDetection.DefaultForTarget(result.Updated))result.Fields.Add("Camera");
   return result;
  }
 }
 public sealed partial class Repository {
  const string ConflictedShot="<conflicting session values>";
  static void FlattenCompletionShots(Dictionary<string,object> raw,Dictionary<string,string> values,int depth){
   if(raw==null)throw new InvalidDataException("Preserved session metadata is empty.");if(depth>5)throw new InvalidDataException("Preserved session metadata is too deeply nested.");
   foreach(var pair in raw){var nested=pair.Value as Dictionary<string,object>;if(nested!=null){FlattenCompletionShots(nested,values,depth+1);continue;}
    if(!(pair.Value is string||pair.Value is int||pair.Value is long||pair.Value is double||pair.Value is decimal))continue;
    string value=Convert.ToString(pair.Value,CultureInfo.InvariantCulture),previous;if(values.TryGetValue(pair.Key,out previous)&&previous!=value)values[pair.Key]=ConflictedShot;else if(!values.ContainsKey(pair.Key))values[pair.Key]=value;
   }
  }
  static void SessionConflicts(Frame frame,Dictionary<string,string> shots){
   if(shots==null)return;
   foreach(var field in new[]{new[]{"Camera","cameraId","camera_id","camId","cam_id"},new[]{"Exposure","exposure_s","exposureSeconds","exposureTimeSec"},new[]{"Gain","gain","cameraGain"},new[]{"Filter","ir","irCut"},new[]{"Target","targetName","target_name","objectName","object_name","target"},new[]{"BinX","binning","bin"},new[]{"BinY","binning","bin"}}){
    var values=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool repeated=false;
    foreach(string key in field.Skip(1)){string value;if(!shots.TryGetValue(key,out value)||string.IsNullOrWhiteSpace(value))continue;if(value==ConflictedShot){repeated=true;continue;}double number;values.Add(field[0]=="Target"?Catalog.Normalize(value):double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)?number.ToString("R",CultureInfo.InvariantCulture):value.Trim());}
    if(repeated||values.Count>1){if(frame.MetadataConflicts==null)frame.MetadataConflicts=new List<string>();frame.MetadataConflicts.Add(field[0]+": conflicting preserved session metadata");}
   }
  }
  Dictionary<string,string> PreservedShots(Frame frame,CancellationToken ct){
   if(string.IsNullOrEmpty(frame.SidecarRelativePath))return null;
   string path=FilePath(new Frame{RelativePath=frame.SidecarRelativePath});if(!Util.Within(path,Path.Combine(Meta,"session-metadata")))throw new InvalidDataException("Session metadata is outside the preserved metadata folder.");
   var before=FileStamp.Read(path);if(before.Size>4*1024*1024)throw new InvalidDataException("Session metadata exceeds the parsing limit.");
   string expected=Path.GetFileNameWithoutExtension(path);if(expected.Length!=64||Util.Hash(path,ct)!=expected)throw new InvalidDataException("Preserved session metadata failed checksum verification.");
   var shots=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);FlattenCompletionShots(Util.Deserialize<Dictionary<string,object>>(File.ReadAllText(path)),shots,0);
   if(!before.ContentSame(FileStamp.Read(path)))throw new InvalidDataException("Session metadata changed during inspection.");return shots;
  }
  public MetadataCompletion DetectMissingMetadata(Frame frame,CancellationToken ct){
   ValidateCapture(frame,ct);string path=FilePath(frame);var before=FileStamp.Read(path);var shots=PreservedShots(frame,ct);
   var detected=Classifier.Read(path,Path.GetDirectoryName(path),frame.Telescope,"Auto",ct:ct,imageKey:frame.ImageKey,originalName:frame.OriginalName,detectionContext:"/"+(frame.OriginalName??Path.GetFileName(path)),sessionMetadata:shots);
   SessionConflicts(detected,shots);
   if(!before.ContentSame(FileStamp.Read(path)))throw new InvalidDataException("Repository file changed during metadata inspection.");
   return MetadataCompletion.Fill(frame,detected);
  }
  public int ApplyMissingMetadata(MetadataCompletion proposal,CancellationToken ct){
   ct.ThrowIfCancellationRequested();var current=Find(proposal.Previous.Hash);if(current==null)throw new IOException("Capture is no longer indexed.");
   if(current.RelativePath!=proposal.Previous.RelativePath)throw new IOException("Capture location changed; preview missing metadata again.");
   ValidateCapture(current,ct);PreservedShots(current,ct);var completion=MetadataCompletion.Fill(current,proposal.Detected);if(completion.Fields.Count>0)Save(completion.Updated);return completion.Fields.Count;
  }
 }
}
