using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
namespace AstroArchive {
 public sealed class AcquisitionDate {
  public DateTime Date;public string Basis;
  public string Text {get{return Date.ToString("dd/MM/yy",CultureInfo.InvariantCulture);}}
 }
 public sealed class CaptureSessionChoice {
  public string Key,Label,Dates,Description;public DateTime? First,Last;public int Count,MissingDates;
 }
 public static class CaptureSessions {
  public static string Key(Frame frame){
   string identity=frame.Session;
   if(string.IsNullOrEmpty(identity)){
    string folder="";try{folder=Path.GetDirectoryName(frame.SourcePath??frame.RelativePath??"")??"";}catch{}
    identity="unassigned:"+folder+"|"+(folder.Length==0?frame.AcquisitionDate??frame.Observed??"":"");
   }
   return "session:"+Util.HashText(Util.Serialize(new[]{identity,frame.Telescope??"",frame.Camera??""}));
  }
  static DateTime? DateOnly(string text){DateTime date;return DateTime.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)?(DateTime?)date:null;}
  public static AcquisitionDate HeaderDate(FitsHeader header){
   string raw=header.Get("DATE-OBS","DATEOBS","DATE_OBS");DateTime? date=Util.Time(raw);if(!date.HasValue)return null;
   return new AcquisitionDate{Date=date.Value.Date,Basis=raw.Trim().Length==10?"FITS date only":"FITS UTC"};
  }
  public static AcquisitionDate FilenameDate(string name){
   // Calendar dates in a frame filename are recorded dates with an unknown timezone.
   foreach(Match match in Regex.Matches(name??"",@"(?<!\d)(20\d{2})[-_]?(\d{2})[-_]?(\d{2})(?!\d)")){
    var date=DateOnly(match.Groups[1].Value+"-"+match.Groups[2].Value+"-"+match.Groups[3].Value);if(date.HasValue)return new AcquisitionDate{Date=date.Value,Basis="Filename (timezone unknown)"};
   }return null;
  }
  public static AcquisitionDate Date(Frame frame){
   var saved=DateOnly(frame.AcquisitionDate);if(saved.HasValue)return new AcquisitionDate{Date=saved.Value,Basis=frame.AcquisitionDateSource??frame.TimeSource??"Recorded date"};
   // Older indexes retain per-frame timestamps, but sometimes only a shifted night.
   // A session-folder timestamp or the shifted night is never an acquisition date.
   if(!(frame.TimeSource??"").StartsWith("Session folder")){
    DateTime timestamp;if(!string.IsNullOrWhiteSpace(frame.Observed)&&DateTime.TryParse(frame.Observed,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out timestamp)){
     if(timestamp.Kind==DateTimeKind.Local)timestamp=timestamp.ToUniversalTime();
     return new AcquisitionDate{Date=timestamp.Date,Basis=frame.TimeSource??"Recorded timestamp (timezone unknown)"};
    }
   }
   return FilenameDate(frame.OriginalName);
  }
  public static CaptureSessionChoice Describe(IEnumerable<Frame> captures){
   var rows=captures.ToList();var dated=rows.Select(Date).Where(d=>d!=null).ToList();DateTime? first=dated.Count==0?(DateTime?)null:dated.Min(d=>d.Date),last=dated.Count==0?(DateTime?)null:dated.Max(d=>d.Date);
   string range=first.HasValue?first.Value.ToString("dd/MM/yy",CultureInfo.InvariantCulture)+(last!=first?"-"+last.Value.ToString("dd/MM/yy",CultureInfo.InvariantCulture):""):"Dates unknown";
   int missing=rows.Count-dated.Count;bool mixed=dated.Any(d=>d.Basis=="FITS UTC")&&dated.Any(d=>d.Basis.IndexOf("timezone unknown",StringComparison.OrdinalIgnoreCase)>=0);
   string dates=range+(missing>0&&dated.Count>0?" · partial dates":"")+(mixed?" · mixed clocks":"");
   string context=string.Join(" · ",rows.Select(f=>f.Telescope??"Unknown device").Distinct())+" · "+string.Join(", ",rows.Select(f=>f.TargetLabel).Distinct())+" · "+string.Join(", ",rows.Select(f=>f.Camera??"Unknown camera").Distinct());
   return new CaptureSessionChoice{Key=rows.Count==0?"":Key(rows[0]),Dates=dates,Label=dates,First=first,Last=last,Count=rows.Count,MissingDates=missing,Description=context+"\n"+rows.Count+" captures; "+dated.Count+" with recorded acquisition dates; "+missing+" dates unknown.\n"+string.Join(", ",dated.Select(d=>d.Basis).Distinct())+(mixed?"\nMixed clock bases: UTC timestamps and unzoned filename dates are kept in their recorded calendar dates.":"")+"\nShifted night and file modification times are not used."};
  }
  public static List<CaptureSessionChoice> Choices(IEnumerable<Frame> frames){
   var choices=frames.GroupBy(Key).Select(Describe).OrderByDescending(c=>c.Last).ThenBy(c=>c.Key).ToList();
   foreach(var collision in choices.GroupBy(c=>c.Dates).Where(g=>g.Count()>1)){
    int index=0;foreach(var choice in collision.OrderBy(c=>c.Description).ThenBy(c=>c.Key)){index++;choice.Label=choice.Dates+" · session "+index+" · "+choice.Description.Split('\n')[0];}
   }return choices;
  }
  public static void SaveDate(Frame frame,AcquisitionDate date){if(date==null)return;frame.AcquisitionDate=date.Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);frame.AcquisitionDateSource=date.Basis;}
 }
 public sealed partial class Repository {
  public int ReadMissingAcquisitionDates(IEnumerable<Frame> frames,bool imports,CancellationToken ct,Action<ProgressInfo> progress){
   var rows=frames.Where(f=>f.Status!="Deleted"&&CaptureSessions.Date(f)==null).ToList();var indexed=All().ToDictionary(f=>f.Hash);int updated=0;bool saved=false;
   for(int i=0;i<rows.Count;i++){
    ct.ThrowIfCancellationRequested();var frame=rows[i];if(progress!=null)progress(new ProgressInfo{Done=i,Total=rows.Count,Stage="Reading acquisition dates",Text=frame.OriginalName});
    bool source=imports&&(CaptureScreening.Importable(frame)||frame.Status=="Unreadable"||frame.Status=="Duplicate in source");
    try{Frame archived=null;if(!source&&(frame.Hash==null||!indexed.TryGetValue(frame.Hash,out archived)))continue;
     string path=source?frame.SourcePath:FilePath(archived);var stamp=FileStamp.Read(path);var expected=source?frame.SourceStamp:archived.RepositoryStamp;if(expected!=null&&!expected.ContentSame(stamp))continue;
     var date=CaptureSessions.HeaderDate(Fits.Header(path));if(date==null||!stamp.ContentSame(FileStamp.Read(path)))continue;CaptureSessions.SaveDate(frame,date);
     if(archived!=null){CaptureSessions.SaveDate(archived,date);Save(archived);saved=true;}updated++;
    }catch(OperationCanceledException){throw;}catch(IOException){}catch(InvalidDataException){}catch(NotSupportedException){}catch(OverflowException){}catch(UnauthorizedAccessException){}
   }
   if(saved)Checkpoint(CancellationToken.None);return updated;
  }
 }
}
