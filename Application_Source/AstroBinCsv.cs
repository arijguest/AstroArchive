// AstroBin acquisition CSVs from archived original metadata. Unknown values stay blank and are reported; nothing is estimated.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace AstroArchive {
 public sealed class AstroBinOptions {
  // Candidate calibration frames, e.g. Exporter.ExistingCalibrations. Selected calibration frames are added automatically.
  public IEnumerable<Frame> CalibrationPool;
  // Exact calibration sets per light hash, as copied by a stacking export. Takes precedence over CalibrationPool.
  public Dictionary<string,List<Frame>> LightCalibrations;
  public bool CountCalibrations=true,IncludeUnknownCalibration,IncludeRejected;
  public Dictionary<string,string> FilterIds;
 }
 public sealed class AstroBinRow {
  public string Target,Date="",Equipment="",FilterName="",FilterId="";public int Number,Subs,Stacks;public double Duration;
  public int? Iso,Binning,SensorCooling,Darks,Flats,FlatDarks,Bias;public double? Gain,MeanSqm,Temperature;
  public double IntegrationSeconds{get{return Number*Duration;}}
 }
 public sealed class AstroBinVideoRow {
  public string Target,Name,Date="",Equipment="",UtcStart="";public int? Frames,Iso;public double? Fps,FocalLength,Gain;
 }
 public sealed class AstroBinSheet {
  public string Target,Label;public readonly List<AstroBinRow> Rows=new List<AstroBinRow>();public readonly List<AstroBinVideoRow> Videos=new List<AstroBinVideoRow>();public readonly List<string> Notes=new List<string>();
  public double IntegrationSeconds{get{return Rows.Sum(r=>r.IntegrationSeconds);}}
  public int Frames{get{return Rows.Sum(r=>r.Number);}}
  public string Csv{get{return Rows.Count==0?"":AstroBinCsv.LongExposureCsv(Rows);}}
  public string VideoCsv{get{return Videos.Count==0?"":AstroBinCsv.VideoCsv(Videos);}}
 }
 public sealed class AstroBinReport {
  public readonly List<AstroBinSheet> Sheets=new List<AstroBinSheet>();public readonly List<string> Notes=new List<string>();
  public bool Empty{get{return Sheets.All(s=>s.Rows.Count==0&&s.Videos.Count==0);}}
  public string NotesText(AstroBinSheet sheet=null){
   var b=new StringBuilder("AstroBin acquisition notes"+(sheet!=null&&Sheets.Count>1?" · "+sheet.Label:"")+"\r\n\r\n");
   b.Append("Values come from the archived original metadata. Unknown values are left blank; AstroBin ignores blank and invalid cells.\r\n");
   b.Append("Import in AstroBin: edit the image, open Acquisition, then import the CSV. Each target needs its own AstroBin image.\r\n");
   var notes=Notes.Concat(sheet==null?Sheets.SelectMany(s=>s.Notes.Select(n=>Sheets.Count>1?s.Label+": "+n:n)):sheet.Notes).ToList();
   if(notes.Count>0)b.Append("\r\nCheck before uploading:\r\n"+string.Join("\r\n",notes.Select(n=>"- "+n))+"\r\n");
   foreach(var s in sheet==null?Sheets:new List<AstroBinSheet>{sheet}){
    if(s.Rows.Count>0){b.Append("\r\n"+s.Label+" · "+s.Frames+" frames · "+CaptureGroups.ExposureLabel(s.IntegrationSeconds,0)+"\r\n");foreach(var r in s.Rows)b.Append("  "+(r.Date.Length>0?r.Date:"date unknown")+" · "+r.Equipment+(r.FilterName.Length>0?" · "+r.FilterName:"")+" · "+r.Number+" × "+AstroBinCsv.Format(r.Duration,4)+" s"+(r.Stacks>0?" (from "+r.Stacks+" stack"+(r.Stacks==1?"":"s")+")":"")+"\r\n");}
    foreach(var v in s.Videos)b.Append("  "+v.Name+" · "+(v.Date.Length>0?v.Date:"date unknown")+(v.UtcStart.Length>0?" · started "+v.UtcStart:"")+(v.Frames.HasValue?" · "+v.Frames+" frames":"")+"\r\n");
   }
   return b.ToString();
  }
 }
 public static class AstroBinCsv {
  public static readonly string[] LongExposureColumns={"date","filter","number","duration","iso","binning","gain","sensorCooling","fNumber","darks","flats","flatDarks","bias","bortle","meanSqm","meanFwhm","temperature"};
  public static readonly string[] VideoColumns={"date","time","frames","fps","exposurePerFrame","focalLength","iso","gain","seeing","transparency","cmi","cmii","cmiii"};
  const double MinDuration=0.0001,MaxDuration=999999.9999;
  static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
  public static string Format(double value,int decimals){double rounded=Math.Round(value,decimals,MidpointRounding.AwayFromZero);if(rounded==0)rounded=0;return rounded.ToString(decimals==0?"0":"0."+new string('#',decimals),Invariant);}
  static string Optional(double? value,int decimals){return value.HasValue?Format(value.Value,decimals):"";}
  static string Optional(int? value){return value.HasValue?value.Value.ToString(Invariant):"";}
  // Only the columns with values are written, in AstroBin's documented order; number and duration are mandatory.
  static string Table(string[] columns,List<string[]> rows,params string[] required){
   var used=Enumerable.Range(0,columns.Length).Where(i=>required.Contains(columns[i])||rows.Any(r=>r[i].Length>0)).ToList();
   var b=new StringBuilder(string.Join(",",used.Select(i=>columns[i]))+"\r\n");foreach(var row in rows)b.Append(string.Join(",",used.Select(i=>row[i]))+"\r\n");return b.ToString();
  }
  public static string LongExposureCsv(IEnumerable<AstroBinRow> rows){
   return Table(LongExposureColumns,rows.Select(r=>new[]{r.Date,r.FilterId,r.Number.ToString(Invariant),Format(r.Duration,4),Optional(r.Iso),Optional(r.Binning),Optional(r.Gain,2),Optional(r.SensorCooling),"",Optional(r.Darks),Optional(r.Flats),Optional(r.FlatDarks),Optional(r.Bias),"",Optional(r.MeanSqm,2),"",Optional(r.Temperature,2)}).ToList(),"number","duration");
  }
  public static string VideoCsv(IEnumerable<AstroBinVideoRow> rows){
   return Table(VideoColumns,rows.Select(r=>new[]{r.Date,"",Optional(r.Frames),Optional(r.Fps,2),"",Optional(r.FocalLength,2),Optional(r.Iso),Optional(r.Gain,2),"","","","",""}).ToList(),"date","frames");
  }
  // Accepts a bare AstroBin equipment ID or a filter page address.
  public static string FilterId(string text){
   string value=(text??"").Trim();if(Regex.IsMatch(value,@"^\d{1,9}$"))return value.TrimStart('0').Length==0?null:value;
   var match=Regex.Match(value,@"astrobin\.com/.*?/filter/(\d{1,9})(?:[/?#]|$)",RegexOptions.IgnoreCase);return match.Success?match.Groups[1].Value:null;
  }
  static bool KnownFilter(string name){return !string.IsNullOrWhiteSpace(name)&&!new[]{"Unknown","None","-"}.Contains(name.Trim(),StringComparer.OrdinalIgnoreCase);}
  public static List<string> FilterNames(IEnumerable<Frame> frames){return frames.Where(f=>f!=null&&(f.Kind=="Light"||f.Kind=="Stack")&&KnownFilter(f.Filter)).Select(f=>f.Filter.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToList();}
  static string Header(Frame f,params string[] keys){
   var image=Assets.Selected(f);if(image==null||image.Headers==null)return "";
   foreach(string key in keys)foreach(var pair in image.Headers)if(string.Equals(pair.Key,key,StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(pair.Value))return pair.Value.Trim().Trim('\'').Trim();
   return "";
  }
  static double? HeaderNumber(Frame f,params string[] keys){double n;return double.TryParse(Header(f,keys).Replace('D','E'),NumberStyles.Float,Invariant,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?(double?)n:null;}
  static int? Whole(double? value,double min,double max){return value.HasValue&&value.Value==Math.Floor(value.Value)&&value.Value>=min&&value.Value<=max?(int?)value.Value:null;}
  static bool Usable(Frame f,AstroBinOptions options){return (options.IncludeRejected||!f.Rejected)&&f.Status!="Failed"&&!CaptureScreening.FileProblem(f);}
  // The same exposure archived twice (identical bytes, or the same camera at the same recorded instant) is counted once.
  static string Moment(Frame f){
   DateTime time;if(string.IsNullOrEmpty(f.ObservedUtc)||!DateTime.TryParse(f.ObservedUtc,Invariant,DateTimeStyles.RoundtripKind,out time)||time.TimeOfDay==TimeSpan.Zero)return null;
   return Util.Serialize(new[]{f.Kind,f.Telescope??"",f.Camera??"",f.CameraId??"",time.ToUniversalTime().Ticks.ToString(Invariant),Util.Num(f.Exposure)});
  }
  static string DateText(Frame f){var date=CaptureSessions.Date(f);return date==null?"":date.Date.ToString("yyyy-MM-dd",Invariant);}
  static string Equipment(Frame f){var parts=new[]{f.Telescope,f.Camera}.Where(p=>!string.IsNullOrWhiteSpace(p)&&p!="Unknown").ToList();return parts.Count==0?"Unknown equipment":string.Join(" · ",parts);}
  static string Plural(int n,string word){return n+" "+word+(n==1?"":"s");}
  static string Names(IEnumerable<Frame> frames){var names=frames.Select(f=>f.OriginalName??f.RelativePath??"").Distinct().ToList();return string.Join(", ",names.Take(3))+(names.Count>3?" and "+(names.Count-3)+" more":"");}
  public static AstroBinReport Build(IEnumerable<Frame> selection,AstroBinOptions options=null,CancellationToken ct=default(CancellationToken)){
   options=options??new AstroBinOptions();var report=new AstroBinReport();
   var unique=new List<Frame>();var hashes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var moments=new HashSet<string>(StringComparer.Ordinal);int duplicates=0;
   foreach(var f in (selection??Enumerable.Empty<Frame>()).Where(f=>f!=null).OrderBy(f=>f.Rejected?1:0)){
    ct.ThrowIfCancellationRequested();if(!string.IsNullOrEmpty(f.Hash)&&!hashes.Add(f.Hash)){duplicates++;continue;}
    string moment=Assets.IsCalibration(f.Kind)?null:Moment(f);if(moment!=null&&!moments.Add(moment)){duplicates++;continue;}unique.Add(f);
   }
   if(duplicates>0)report.Notes.Add(Plural(duplicates,"duplicate exposure")+" (same file, or same camera at the same recorded time) counted once.");
   var selectedCalibrations=unique.Where(f=>Assets.IsCalibration(f.Kind)).ToList();
   var captures=unique.Where(f=>f.Kind=="Light"||f.Kind=="Stack"||f.Kind=="Video").ToList();
   int other=unique.Count-selectedCalibrations.Count-captures.Count;if(other>0)report.Notes.Add(Plural(other,"file")+" with an unknown frame type excluded. Set the frame type in Edit metadata to include them.");
   var unusable=captures.Where(f=>!Usable(f,options)).ToList();if(unusable.Count>0)report.Notes.Add(Plural(unusable.Count,"rejected or damaged capture")+" excluded from integration.");
   captures=captures.Except(unusable).ToList();
   // Stacks duplicate their subs: count a stack only when none of its target's subs share its session or date.
   var counted=CaptureGroups.ExposureFrames(captures,ct);var covered=captures.Where(f=>f.Kind=="Stack"&&!counted.Contains(f)).ToList();
   if(covered.Count>0)report.Notes.Add(Plural(covered.Count,"stack")+" not counted because subs from the same session or date are included: "+Names(covered)+".");
   // Live-stack snapshots of one session can overlap; count only the largest.
   var stacks=counted.Where(f=>f.Kind=="Stack").ToList();var overlapping=new List<Frame>();
   foreach(var g in stacks.GroupBy(f=>Util.Serialize(new[]{f.Target,f.Telescope??"",f.Camera??"",f.Filter??"",string.IsNullOrWhiteSpace(f.Session)?"date:"+DateText(f):"session:"+f.Session.Trim().ToUpperInvariant()})).Where(g=>g.Count()>1)){
    var keep=g.OrderByDescending(f=>f.StackCount).ThenByDescending(f=>f.Exposure??0).ThenByDescending(f=>f.ObservedUtc??f.Observed??"",StringComparer.Ordinal).First();overlapping.AddRange(g.Where(f=>f!=keep));
   }
   if(overlapping.Count>0)report.Notes.Add(Plural(overlapping.Count,"stack")+" from a session that already has a larger stack not counted; live-stack snapshots can overlap. Add them in AstroBin only if they are independent: "+Names(overlapping)+".");
   counted=counted.Except(overlapping).ToList();
   var lightCalibrations=CalibrationSets(counted.Where(f=>f.Kind=="Light").ToList(),selectedCalibrations,options,report);
   foreach(var target in counted.GroupBy(f=>f.Target??"Unknown").OrderBy(g=>g.First().TargetLabel,StringComparer.OrdinalIgnoreCase)){ct.ThrowIfCancellationRequested();report.Sheets.Add(Sheet(target.ToList(),lightCalibrations,options));}
   if(selectedCalibrations.Count>0){var used=new HashSet<string>(lightCalibrations.Values.SelectMany(v=>v).Select(f=>f.Hash),StringComparer.OrdinalIgnoreCase);int unmatched=selectedCalibrations.Count(f=>!used.Contains(f.Hash));report.Notes.Add(Plural(selectedCalibrations.Count,"selected calibration frame")+" excluded from integration"+(unmatched>0?"; "+unmatched+" match none of the selected subs and are not counted":"")+".");}
   if(captures.Count==0)report.Notes.Add("No usable lights, stacks or recordings selected.");
   return report;
  }
  static Dictionary<string,List<Frame>> CalibrationSets(List<Frame> lights,List<Frame> selectedCalibrations,AstroBinOptions options,AstroBinReport report){
   var sets=new Dictionary<string,List<Frame>>(StringComparer.OrdinalIgnoreCase);if(lights.Count==0)return sets;
   if(!options.CountCalibrations){report.Notes.Add("Calibration frames are not included, so darks, flats, flat darks and bias are blank.");return sets;}
   if(options.LightCalibrations!=null){foreach(var light in lights){List<Frame> set;if(light.Hash!=null&&options.LightCalibrations.TryGetValue(light.Hash,out set))sets[light.Hash]=set;}}
   else{
    var pool=(options.CalibrationPool??Enumerable.Empty<Frame>()).Concat(selectedCalibrations.Where(f=>Usable(f,options))).Where(f=>f!=null&&Assets.IsCalibration(f.Kind)).GroupBy(f=>f.Hash??"").Select(g=>g.First()).ToList();
    if(pool.Count>0)foreach(var group in Exporter.StackingGroups(lights,pool,false,options.IncludeUnknownCalibration,true))foreach(var light in group.Inputs)if(light.Hash!=null)sets[light.Hash]=group.Calibrations;
   }
   int unknown=lights.Count(f=>f.Calibration=="Unknown"&&!options.IncludeUnknownCalibration);if(unknown>0)report.Notes.Add(Plural(unknown,"sub")+" with an unknown calibration state: calibration frame counts are left blank for them. Confirm they are uncalibrated to count matching calibrations.");
   int calibrated=lights.Count(f=>f.Calibration!="Uncalibrated"&&f.Calibration!="Unknown");if(calibrated>0)report.Notes.Add(Plural(calibrated,"sub")+" calibrated before archiving: their calibration frames are not in the archive, so darks, flats and bias stay blank.");
   return sets;
  }
  sealed class Entry {public Frame Frame;public int Number;public double Duration;}
  static AstroBinSheet Sheet(List<Frame> frames,Dictionary<string,List<Frame>> lightCalibrations,AstroBinOptions options){
   var first=frames[0];var sheet=new AstroBinSheet{Target=first.Target??"Unknown",Label=first.TargetLabel};var entries=new List<Entry>();
   var noExposure=new List<Frame>();var noCount=new List<Frame>();var outOfRange=new List<Frame>();
   foreach(var f in frames.Where(f=>f.Kind=="Light")){if(!f.Exposure.HasValue||f.Exposure.Value<=0){noExposure.Add(f);continue;}entries.Add(new Entry{Frame=f,Number=1,Duration=f.Exposure.Value});}
   foreach(var f in frames.Where(f=>f.Kind=="Stack")){if(f.StackCount<=0){noCount.Add(f);continue;}if(!f.Exposure.HasValue||f.Exposure.Value<=0){noExposure.Add(f);continue;}entries.Add(new Entry{Frame=f,Number=f.StackCount,Duration=f.Exposure.Value/f.StackCount});}
   entries.RemoveAll(e=>{bool bad=e.Duration<MinDuration-1e-9||e.Duration>MaxDuration+1e-9;if(bad)outOfRange.Add(e.Frame);return bad;});
   if(noExposure.Count>0)sheet.Notes.Add(Plural(noExposure.Count,"capture")+" without a recorded exposure omitted (duration is mandatory): "+Names(noExposure)+".");
   if(noCount.Count>0)sheet.Notes.Add(Plural(noCount.Count,"stack")+" without a recorded sub count omitted (number is mandatory). Add the count in Edit metadata: "+Names(noCount)+".");
   if(outOfRange.Count>0)sheet.Notes.Add(Plural(outOfRange.Count,"capture")+" with an exposure outside AstroBin's 0.0001–999999.9999 s range omitted: "+Names(outOfRange)+".");
   if(entries.Any(e=>Math.Abs(Math.Round(e.Duration,4)-e.Duration)>1e-9))sheet.Notes.Add("Some exposures have more than four decimals and are rounded to AstroBin's precision.");
   if(entries.Any(e=>e.Frame.Kind=="Stack"))sheet.Notes.Add("Stack durations are the recorded total integration divided by the recorded sub count.");
   var filterIds=options.FilterIds==null?new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase):new Dictionary<string,string>(options.FilterIds,StringComparer.OrdinalIgnoreCase);var missingFilters=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var asymmetric=0;var temperatureSpread=new List<string>();var counted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var unknownMasters=new List<Frame>();var sharedCalibrations=false;
   Func<Entry,string> key=e=>{var f=e.Frame;return Util.Serialize(new[]{DateText(f),Equipment(f),KnownFilter(f.Filter)?f.Filter.Trim().ToUpperInvariant():"",Format(e.Duration,4),Binning(f).HasValue?Binning(f).Value.ToString(Invariant):"",f.Gain.HasValue?Format(f.Gain.Value,2):"",Optional(Whole(HeaderNumber(f,"ISOSPEED","ISO"),1,int.MaxValue))});};
   foreach(var group in entries.GroupBy(key).OrderBy(g=>DateText(g.First().Frame).Length==0?1:0).ThenBy(g=>DateText(g.First().Frame),StringComparer.Ordinal).ThenBy(g=>Equipment(g.First().Frame),StringComparer.OrdinalIgnoreCase).ThenBy(g=>g.First().Frame.Filter??"",StringComparer.OrdinalIgnoreCase).ThenBy(g=>g.First().Duration)){
    var f=group.First().Frame;var rowFrames=group.Select(e=>e.Frame).ToList();var row=new AstroBinRow{Target=sheet.Target,Date=DateText(f),Equipment=Equipment(f),Number=group.Sum(e=>e.Number),Duration=group.First().Duration,Subs=group.Count(e=>e.Frame.Kind=="Light"),Stacks=group.Count(e=>e.Frame.Kind=="Stack"),Binning=Binning(f),Gain=f.Gain.HasValue?(double?)Math.Round(f.Gain.Value,2):null,Iso=Whole(HeaderNumber(f,"ISOSPEED","ISO"),1,int.MaxValue)};
    if(KnownFilter(f.Filter)){row.FilterName=f.Filter.Trim();string id;if(filterIds.TryGetValue(row.FilterName,out id)&&FilterId(id)!=null)row.FilterId=FilterId(id);else missingFilters.Add(row.FilterName);}
    if(rowFrames.Any(r=>r.BinX!=r.BinY&&(r.BinX>0||r.BinY>0)||r.BinX>4))asymmetric+=rowFrames.Count;
    var temperatures=rowFrames.Where(r=>r.Temperature.HasValue).Select(r=>r.Temperature.Value).ToList();
    if(temperatures.Count>0){row.SensorCooling=Whole(Math.Round(temperatures.Average(),MidpointRounding.AwayFromZero),-274,100);if(temperatures.Max()-temperatures.Min()>2)temperatureSpread.Add(RowName(row)+": "+Format(temperatures.Min(),1)+" to "+Format(temperatures.Max(),1)+" °C");if(temperatures.Count<rowFrames.Count)temperatureSpread.Add(RowName(row)+": "+(rowFrames.Count-temperatures.Count)+" of "+rowFrames.Count+" without a sensor temperature");}
    row.MeanSqm=Mean(rowFrames,0,100,"AOCSKYQU","SQM","SKYQUAL","MPSAS");row.Temperature=Mean(rowFrames,-88,58,"AOCAMBT","AMBTEMP","AMB_TEMP");
    var calibrationSets=rowFrames.Where(r=>r.Kind=="Light"&&r.Hash!=null&&lightCalibrations.ContainsKey(r.Hash)).Select(r=>lightCalibrations[r.Hash]).ToList();
    if(calibrationSets.Count>0){
     var fresh=calibrationSets.SelectMany(s=>s).Where(c=>c!=null&&c.Hash!=null).GroupBy(c=>c.Hash,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();if(fresh.Any(c=>counted.Contains(c.Hash)))sharedCalibrations=true;fresh=fresh.Where(c=>counted.Add(c.Hash)).ToList();
     row.Darks=CalibrationCount(fresh,"Dark",unknownMasters);row.Flats=CalibrationCount(fresh,"Flat",unknownMasters);row.FlatDarks=CalibrationCount(fresh,"Dark flat",unknownMasters);row.Bias=CalibrationCount(fresh,"Bias",unknownMasters);
    }
    sheet.Rows.Add(row);
   }
   if(missingFilters.Count>0)sheet.Notes.Add("No AstroBin filter ID for "+string.Join(", ",missingFilters.OrderBy(n=>n))+"; the filter column is blank for "+(missingFilters.Count==1?"it":"them")+". Add IDs here or choose the filter in AstroBin.");
   if(asymmetric>0)sheet.Notes.Add(Plural(asymmetric,"capture")+" use binning AstroBin does not accept (only 1×1 to 4×4); binning left blank.");
   if(temperatureSpread.Count>0)sheet.Notes.Add("Sensor temperature varies; sensorCooling is the rounded mean. "+string.Join("; ",temperatureSpread)+".");
   if(unknownMasters.Count>0)sheet.Notes.Add(Plural(unknownMasters.Count,"master calibration file")+" do not record how many frames they combine; that calibration count is left blank: "+Names(unknownMasters)+".");
   if(sharedCalibrations)sheet.Notes.Add("Calibration frames shared by several rows are counted once, on their first row.");
   if(sheet.Rows.Any(r=>r.Date.Length==0))sheet.Notes.Add("Some captures have no recorded acquisition date; their date is blank.");
   var bases=frames.Select(CaptureSessions.Date).Where(d=>d!=null).Select(d=>d.Basis).Distinct().ToList();
   if(bases.Any(b=>b=="FITS UTC")&&bases.Any(b=>b.IndexOf("timezone unknown",StringComparison.OrdinalIgnoreCase)>=0))sheet.Notes.Add("Dates mix UTC headers and clocks with an unknown timezone; a night that crosses midnight can appear on two dates.");
   else if(bases.Count>0&&bases.All(b=>b=="FITS UTC"||b=="FITS date only"))sheet.Notes.Add("Dates are the UTC acquisition dates recorded in the original headers.");
   var conflicts=frames.Count(f=>f.MetadataConflicts!=null&&f.MetadataConflicts.Count>0);if(conflicts>0)sheet.Notes.Add(Plural(conflicts,"capture")+" have conflicting metadata values. Review them in Edit metadata before uploading.");
   var ratios=frames.Select(f=>HeaderNumber(f,"FOCRATIO")).Where(r=>r.HasValue&&r.Value>0).Select(r=>Format(r.Value,2)).Distinct().ToList();if(ratios.Count>0)sheet.Notes.Add("Recorded focal ratio f/"+string.Join(", f/",ratios)+". AstroBin's fNumber is for camera lenses, so it is not filled in; add it in AstroBin if a lens was used.");
   var videos=frames.Where(f=>f.Kind=="Video").OrderBy(f=>f.ObservedUtc??f.Observed??"",StringComparer.Ordinal).ThenBy(f=>f.OriginalName,StringComparer.OrdinalIgnoreCase).ToList();
   foreach(var f in videos){var image=Assets.Selected(f);DateTime start;sheet.Videos.Add(new AstroBinVideoRow{Target=sheet.Target,Name=f.OriginalName??f.RelativePath,Date=DateText(f),Equipment=Equipment(f),Frames=image!=null&&image.Count>1?(int?)image.Count:null,Fps=Positive(HeaderNumber(f,"FPS","FRAMERATE")),FocalLength=Positive(HeaderNumber(f,"FOCALLEN")),Iso=Whole(HeaderNumber(f,"ISOSPEED","ISO"),1,int.MaxValue),Gain=f.Gain.HasValue?(double?)Math.Round(f.Gain.Value,2):null,UtcStart=!string.IsNullOrEmpty(f.ObservedUtc)&&DateTime.TryParse(f.ObservedUtc,Invariant,DateTimeStyles.RoundtripKind,out start)?start.ToUniversalTime().ToString("HH:mm",Invariant)+" UTC":""});}
   if(videos.Count>0){
    sheet.Notes.Add("Recordings: AstroBin's time column is the local start time, which the archive does not know, so it is blank"+(sheet.Videos.Any(v=>v.UtcStart.Length>0)?"; recorded UTC start times are listed below":"")+". Exposure per frame, seeing, transparency and central meridians are not recorded.");
    int noFrames=sheet.Videos.Count(v=>!v.Frames.HasValue);if(noFrames>0)sheet.Notes.Add(Plural(noFrames,"recording")+" without a recorded frame count; frames is blank.");
   }
   return sheet;
  }
  static string RowName(AstroBinRow row){return (row.Date.Length>0?row.Date:"undated")+(row.FilterName.Length>0?" "+row.FilterName:"")+" "+Format(row.Duration,4)+" s";}
  static int? Binning(Frame f){return f.BinX==f.BinY&&f.BinX>=1&&f.BinX<=4?(int?)f.BinX:null;}
  static double? Positive(double? value){return value.HasValue&&value.Value>0?value:null;}
  static double? Mean(List<Frame> frames,double min,double max,params string[] keys){var values=frames.Select(f=>HeaderNumber(f,keys)).Where(v=>v.HasValue&&v.Value>=min&&v.Value<=max).Select(v=>v.Value).ToList();return values.Count==0?(double?)null:Math.Round(values.Average(),2);}
  // Raw frames count one each; a master counts the frames it records combining, otherwise the total is unknown.
  static int? CalibrationCount(List<Frame> calibrations,string kind,List<Frame> unknownMasters){
   var raw=calibrations.Where(c=>c.Kind==kind).ToList();var masters=calibrations.Where(c=>c.Kind=="Master "+kind.ToLowerInvariant()).ToList();
   if(raw.Count==0&&masters.Count==0)return null;var unknown=masters.Where(m=>m.StackCount<=0).ToList();if(unknown.Count>0){unknownMasters.AddRange(unknown);return null;}
   return raw.Count+masters.Sum(m=>m.StackCount);
  }
 }
}
