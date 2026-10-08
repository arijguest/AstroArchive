// AstroArchive 1.11.0. C# 5, .NET Framework 4.8, Windows 10/11 x64.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AstroArchive {
 public static class Util {
  public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 100 }; }
  public static string Serialize(object o) { return Json().Serialize(o); }
  public static T Deserialize<T>(string s) { return Json().Deserialize<T>(s); }
  public static string Hash(string path, System.Threading.CancellationToken ct,Action<int> counted=null) {
   ct.ThrowIfCancellationRequested();using(var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1048576))return Hash(fs,ct,counted);
  }
  public static string Hash(Stream stream, System.Threading.CancellationToken ct,Action<int> counted=null) {
   using (var lease=FileTransfer.Rent()) using (var sha=SHA256.Create()) {
    byte[] b=lease.Buffer; int n; while((n=stream.Read(b,0,b.Length))>0) { ct.ThrowIfCancellationRequested(); sha.TransformBlock(b,0,n,b,0);if(counted!=null)counted(n); }
    sha.TransformFinalBlock(new byte[0],0,0); return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
   }
  }
  public static string HashText(string s) { using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant(); }
  public static string Safe(string s) {
   s=Regex.Replace(s??"Unknown", "[<>:\"/\\\\|?*\\x00-\\x1F]", "_").Trim().TrimEnd('.', ' ');
   if(s.Length==0) s="Unknown"; if(s.Length>68) s=s.Substring(0,56)+"_"+HashText(s).Substring(0,8);
   if(Regex.IsMatch(s,"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\\.|$)",RegexOptions.IgnoreCase)) s="_"+s;
   return s;
  }
  public static bool IsFits(string s) { return Regex.IsMatch(s,@"\.(fit|fits|fts)(\.gz)?$",RegexOptions.IgnoreCase); }
  public static bool IsImageAsset(string path){return Assets.Supported(path);}
  public static bool MeteorFilename(string path){return Regex.IsMatch(Path.GetFileName(path??""),@"(?:^|[^a-z])meteor(?=$|[^a-z])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
  public static bool FailedFilename(string path) { return Path.GetFileName(path??"").IndexOf("failed",StringComparison.OrdinalIgnoreCase)>=0; }
  public static bool FailedFilename(Frame frame) { return FailedFilename(!string.IsNullOrEmpty(frame.OriginalName)?frame.OriginalName:frame.SourcePath??frame.RelativePath); }
  public static string SafeFile(string s) {var m=Regex.Match(s??"",@"(\.(fit|fits|fts)(\.gz)?)$",RegexOptions.IgnoreCase);string ext=m.Success?m.Value:Path.GetExtension(s??"");string stem=ext.Length>0?s.Substring(0,s.Length-ext.Length):s;return Safe(stem)+ext;}
  public static bool Within(string path,string parent) { string p=Path.GetFullPath(path).TrimEnd('\\','/'), r=Path.GetFullPath(parent).TrimEnd('\\','/'); return p.Equals(r,StringComparison.OrdinalIgnoreCase)||p.StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase); }
  public static string Num(double? n) { return n.HasValue?n.Value.ToString("0.###",CultureInfo.InvariantCulture):"?"; }
  public static DateTime? Time(string s) { DateTime d; return DateTime.TryParse(s,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out d)?(DateTime?)d:null; }
  public static void AtomicText(string path,string text) {
   string t=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   File.WriteAllText(t,text,new UTF8Encoding(false));
   if(File.Exists(path)) File.Replace(t,path,path+".bak"); else File.Move(t,path);
  }
  public static string[] Tokens(string s) { return (s??"").Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries); }
 }
 public class Frame {
  public string Hash {get;set;} public string RelativePath {get;set;} public string SourcePath {get;set;} public string SourceRoot {get;set;} public string OriginalName {get;set;}
  public string Telescope {get;set;} public string TelescopeIdentity {get;set;} public string Model {get;set;} public string Camera {get;set;} string target="Unknown";
  public string Target {get{return Util.MeteorFilename(OriginalName)?"Meteor":target;}set{target=Catalog.CanonicalTarget(value);}}
  public string ObjectId {get{return Catalog.ObjectId(Target);}} public string CommonName {get{return Catalog.CommonName(Target);}}
  public string TargetLabel {get{return Catalog.Label(Target);}}
  public string TargetName {get{return CommonName.Length>0?CommonName:ObjectId.Length==0?Target:"";}}
  public string SessionKey {get{return CaptureSessions.Key(this);}}
  public string AcquisitionDate {get;set;} public string AcquisitionDateSource {get;set;}
  public string AcquisitionDateLabel {get{var date=CaptureSessions.Date(this);return date==null?"Unknown":date.Text;}}
  public string SessionGroup {get{return SessionKey;}}
  public string Make {get;set;} public string MakeEvidence {get;set;} public string TargetEvidence {get;set;} public string SourceDisposition {get;set;}
  public string ObservationMode {get;set;} public string Kind {get;set;} public string Calibration {get;set;} public string Filter {get;set;} public string Bayer {get;set;}
  public string Mount {get;set;} public string MountEvidence {get;set;} public string Observed {get;set;} public string TimeSource {get;set;}
  [ScriptIgnore]public string MountText{get{return MountLabels.Display(this);}}
  [ScriptIgnore]public string MountEvidenceText{get{return MountLabels.Evidence(this);}}
  public string Night {get;set;} public string Session {get;set;} public string Notes {get;set;} public string Status {get;set;}
  public string SourceMetadataPath {get;set;} public string SidecarRelativePath {get;set;}
  public double? Exposure {get;set;} public double? Gain {get;set;} public double? Temperature {get;set;} public double? RA {get;set;} public double? Dec {get;set;}
  public double? Latitude {get;set;} public double? Longitude {get;set;} public int Width {get;set;} public int Height {get;set;}
  public int Channels {get;set;} public int BinX {get;set;} public int BinY {get;set;} public int StackCount {get;set;}
  public FileStamp SourceStamp {get;set;} public FileStamp RepositoryStamp {get;set;} public FileStamp SourceMetadataStamp {get;set;} public string CameraEvidence {get;set;} public long Bytes {get;set;} public bool Rejected {get;set;} public string RotationReport {get;set;}
  public string TelescopeModel{get;set;}public string CameraModel{get;set;}public string CameraId{get;set;}public string AcquisitionSoftware{get;set;}public string DeviceProfile{get;set;}public string AcquisitionProfile{get;set;}public string ClassificationVersion{get;set;}
  public string OpticalConfiguration{get;set;}public string ReadoutMode{get;set;}public string Roi{get;set;}public double? Offset{get;set;}public double? ElectronsPerAdu{get;set;}public string GainUnit{get;set;}public string ObservedUtc{get;set;}public string TimeZoneId{get;set;}public bool? LinearData{get;set;}public string RegistrationState{get;set;}public string CalibrationSteps{get;set;}
  public string Format{get;set;}public string ImageKey{get;set;}public int? ImageIndex{get;set;}public List<ImageDescriptor> Images{get;set;}public List<AssociatedFile> AssociatedFiles{get;set;}public Dictionary<string,MetadataFact> Facts{get;set;}public List<string> MetadataConflicts{get;set;}
  [ScriptIgnore]public string CapabilityText{get{return Assets.Capabilities(this);}}
  public bool Screened {get;set;} public string ScreeningIssue {get;set;}
  public string RejectionReason {get;set;} public string IntegrityIssue {get;set;} public string TransferIssue {get;set;}
  public string ReviewCategory {get{return CaptureScreening.Category(this);}}
  public string ReviewReason {get{return CaptureScreening.Reason(this);}}
  public MosaicHint Mosaic {get;set;} public SkyGeometry Sky {get;set;}
  [ScriptIgnore]public List<MosaicLabel> MosaicLabels {get;set;}
  [ScriptIgnore]public bool MosaicDismissed {get;set;}
  public string MosaicText {get{return MosaicLabels!=null&&MosaicLabels.Count>0?string.Join("; ",MosaicLabels.Select(m=>m.Name).Distinct()):Mosaic==null||MosaicDismissed?"-":Mosaic.Name??"Mosaic candidate";}}
  public string PanelText {get{return MosaicLabels!=null&&MosaicLabels.Count>0?string.Join("; ",MosaicLabels.Select(m=>m.Panel)):Mosaic==null||MosaicDismissed?"-":Mosaic.PanelKey??"Unassigned";}}
  public string ReviewText {get{return CaptureScreening.NeedsReview(this)?"Needs review":Screened?"Passed":"Not screened";}}
  public string ExposureText {get{return Exposure.HasValue?Util.Num(Exposure)+" s":"-";}}
  public string MakeText {get{return !string.IsNullOrEmpty(Make)?Make:InstrumentDetection.MakeOf(Model);}}
  public string InstrumentText {get{return string.IsNullOrEmpty(Model)||Model=="Auto"?MakeText:Model;}}
  public string GainText {get{return Gain.HasValue?Util.Num(Gain):"-";}} public string TemperatureText {get{return Temperature.HasValue?Util.Num(Temperature)+" °C":"-";}}
  [ScriptIgnore]public long? PixelCount {get{return Width>0&&Height>0?(long?)((long)Width*Height*Math.Max(1,Channels)):null;}}
  public string SizeText {get{return Width<=0||Height<=0?"-":Width+" × "+Height+(Channels>1?" × "+Channels:"");}}
  public string SearchText {get{return string.Join(" ",new[]{Target,ObjectId,CommonName,Catalog.Aliases(Target),Telescope,TelescopeModel,CameraModel,CameraId,AcquisitionSoftware,ReadoutMode,Roi,OpticalConfiguration,Format,CapabilityText,MakeText,Model,MakeEvidence,TargetEvidence,SourceDisposition,Camera,Kind,Calibration,Filter,Mount,MountText,Night,AcquisitionDate,AcquisitionDateLabel,Observed,OriginalName,Notes,Status,ReviewText,ScreeningIssue,MosaicText,PanelText,Util.Num(Exposure),Util.Num(Gain),Util.Num(Temperature),SizeText,"bin"+BinX+"x"+BinY});}}
  public string Group {get{return string.Join("|",new[]{Telescope,MakeText,Camera,Filter,Width.ToString(),Height.ToString(),Channels.ToString(),BinX.ToString(),BinY.ToString(),Bayer,Calibration,Util.Num(Exposure),Util.Num(Gain),Assets.CompatibilityKey(this)});}}
  public Frame Clone() { var copy=(Frame)MemberwiseClone();copy.Facts=Facts==null?null:Facts.ToDictionary(p=>p.Key,p=>new MetadataFact{Value=p.Value.Value,Raw=p.Value.Raw,Source=p.Value.Source,Unit=p.Value.Unit});copy.MetadataConflicts=MetadataConflicts==null?null:new List<string>(MetadataConflicts);copy.AssociatedFiles=AssociatedFiles==null?null:AssociatedFiles.Select(a=>new AssociatedFile{SourcePath=a.SourcePath,RelativePath=a.RelativePath,Hash=a.Hash,Role=a.Role,Stamp=a.Stamp==null?null:a.Stamp.Clone()}).ToList();copy.SourceStamp=SourceStamp==null?null:SourceStamp.Clone();copy.RepositoryStamp=RepositoryStamp==null?null:RepositoryStamp.Clone();copy.SourceMetadataStamp=SourceMetadataStamp==null?null:SourceMetadataStamp.Clone();copy.Mosaic=Mosaic==null?null:Mosaic.Clone();copy.Sky=Sky==null?null:Sky.Clone();copy.MosaicLabels=MosaicLabels==null?null:MosaicLabels.Select(m=>m.Clone()).ToList();return copy; }
 }
 public class TargetSummary {
  public string Name{get;set;}public int Files{get;set;}public int Subs{get;set;}public int Stacks{get;set;}public int Sessions{get;set;}public double ExposureSeconds{get;set;}public int UnknownExposure{get;set;}
  public string Label{get{return Name=="All targets"?Name:Catalog.Label(Name);}}
  public string Group{get{return TargetNavigation.Group(Name);}}
  public string DisplayName{get{return TargetNavigation.ShortName(Name);}}
  public string FileCount{get{return Files.ToString("N0");}}
  public string Subline{get{string id=TargetNavigation.Identifier(Name);string exposure=Subs==0?"":ExposureSeconds>0?TargetNavigation.Exposure(ExposureSeconds)+(UnknownExposure>0?" + ?":""):"exposure unknown";return string.Join(" · ",new[]{id,exposure}.Where(s=>s.Length>0));}}
  public string Detail{get{return Subs+" subs  ·  "+Stacks+" stacks  ·  "+Sessions+" sessions\n"+CaptureGroups.ExposureLabel(ExposureSeconds,UnknownExposure);}}
  public string Tooltip{get{return Label+(Group.Length>0?"\n"+Group:"")+"\n"+Files+" file"+(Files==1?"":"s")+" · "+Subs+" subs · "+Stacks+" stack"+(Stacks==1?"":"s")+" · "+Sessions+" session"+(Sessions==1?"":"s")+(Subs>0?"\n"+CaptureGroups.ExposureLabel(ExposureSeconds,UnknownExposure):"")+"\nFile count includes all frame types. Exposure totals include acquisition subs only.";}}
 }
 public class Settings { public int TextScalePercent{get;set;}public bool ComfortableRows{get;set;}public bool HighContrast{get;set;}public bool ReducedMotion{get;set;}public bool GuideSeen{get;set;}public bool GuideCompleted{get;set;} public int? ObservingCityId{get;set;} public string ObservingCity{get;set;} public Dictionary<string,ColumnLayout> TableLayouts{get;set;} public List<TelescopeProfile> Telescopes{get;set;} public string SelectedTelescope{get;set;} public Settings(){AutoSolve=false;AutoRotation=false;ThemeMode="System";PreviewStretch="Auto";ShowPreview=true;} public string ThemeMode {get;set;} public string PreviewStretch {get;set;} public bool ShowPreview {get;set;} public bool IgnoreFailed {get;set;} public int CopyWorkers {get;set;} public string Repository{get;set;} public string LastSource{get;set;} public string Telescope{get;set;} public string Model{get;set;} public double? Latitude{get;set;} public double? Longitude{get;set;} public double? FieldHeight{get;set;} public string SirilExecutable{get;set;} public string Astap{get;set;} public string StarDatabase{get;set;} public string ApiKeyProtected{get;set;} public bool UseOnline{get;set;} public bool AutoSolve{get;set;} public bool AutoRotation{get;set;} }
 public class ProgressInfo {[ScriptIgnore]public PipelineMetrics LiveMetrics{get;set;}public int Done{get;set;} public int Total{get;set;} public string Text{get;set;} public string Stage{get;set;} public long BytesDone{get;set;} public long BytesTotal{get;set;} public double ElapsedSeconds{get;set;} public double? RemainingSeconds{get;set;} public List<StageMetric> Stages{get;set;} public bool TotalKnown{get;set;} public bool Stalled{get;set;} public bool Finalising{get;set;} public bool Finished{get;set;} public double ProgressFraction{get;set;} public string Activity{get;set;} public double WorkPerSecond{get;set;} public bool CopyPhase{get;set;} public bool EtaProvisional{get;set;} public double EffectiveBytesPerSecond{get;set;} }
 public class ImportPlan {public List<Frame> Frames=new List<Frame>(); public List<string> Errors=new List<string>(); public string Source; public long Bytes; public int CacheHits;public int MetadataCacheHits;public int IgnoredFailed; public PipelineMetrics Metrics; }
 public class ImportOptions {public bool DeleteOriginals;public bool IgnoreFailed;internal bool DumpInbox;public string SourceRoot; public int Workers; public bool Retune; public bool CloudSource; public Action<Frame> OnFrame;public PipelineMetrics ScanMetrics;public bool DeferFinish;}
 public class ImportResult {public int SkippedDeleted;public int IgnoredFailed;public int Imported;public int OriginalsDeleted;public int OriginalsKept;public int Duplicates;public int Failed;public int Workers=1;public List<WorkerTrial> Trials=new List<WorkerTrial>();public List<string> Errors=new List<string>();public List<string> CleanupErrors=new List<string>();public List<string> Warnings=new List<string>();public PipelineMetrics Metrics;}
 public class PairResult {public double Angle{get;set;} public double Rms{get;set;} public double Uncertainty{get;set;} public double Scale{get;set;} public int Matches{get;set;} public double Coverage{get;set;} }
 public class RotationPoint { public string File{get;set;} public string Time{get;set;} public double Minutes{get;set;} public double Angle{get;set;} public double Rms{get;set;} public int Stars{get;set;} public double Error{get;set;} }
 public class RotationResult {
  public string Session{get;set;} public string Mount{get;set;} public string Evidence{get;set;} public string AnalyzedAt{get;set;}
  public double SpanMinutes{get;set;} public double DriftDegrees{get;set;} public double RateDegreesMinute{get;set;} public double ResidualDegrees{get;set;}
  public List<RotationPoint> Points=new List<RotationPoint>(); public List<string> Rejected=new List<string>();
 }
}
