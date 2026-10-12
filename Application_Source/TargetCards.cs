// The facts on a shareable target card. Integration follows Analytics: individual
// lights, plus standalone stacks only when their subs are not in the archive.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 public sealed class TargetCardData {
  public string Target,Title,Subtitle,DateRange,Equipment;public double Seconds;public int Lights,Stacks,Nights,UnknownExposure;
  public List<AnalyticsValue> Filters=new List<AnalyticsValue>();
 }
 // Zoom (1–4×), pan (-1 to 1 across the overhang) and clockwise quarter turns.
 public sealed class TargetCardPlacement {
  public double Zoom=1,PanX,PanY;public int Rotation;
  public TargetCardPlacement Copy(){return (TargetCardPlacement)MemberwiseClone();}
 }
 // A target's last card choices, so its image and framing come back next time.
 // An empty Path means the star field was chosen; null Path keeps the default image.
 public sealed class TargetCardMemory {
  public string Path{get;set;}public bool Fill{get;set;}public double Zoom{get;set;}public double PanX{get;set;}public double PanY{get;set;}public int Rotation{get;set;}public string Description{get;set;}
  public TargetCardPlacement Placement(){return new TargetCardPlacement{Zoom=Math.Max(1,Math.Min(4,double.IsNaN(Zoom)?1:Zoom)),PanX=Math.Max(-1,Math.Min(1,double.IsNaN(PanX)?0:PanX)),PanY=Math.Max(-1,Math.Min(1,double.IsNaN(PanY)?0:PanY)),Rotation=TargetCards.QuarterTurn(Rotation)};}
 }
 public sealed class TargetCardChoice {public string Target,Label;public double Seconds;public int Lights;public override string ToString(){return Label;}}
 public enum TargetCardLayout {Portrait,Vertical,Square,Widescreen}
 public static class TargetCards {
  public static readonly string[] LayoutLabels={"Portrait · 4:5","Vertical · 9:16","Square · 1:1","Widescreen · 16:9"};
  public static void Size(TargetCardLayout layout,out int width,out int height){width=layout==TargetCardLayout.Widescreen?1920:1080;height=layout==TargetCardLayout.Vertical?1920:layout==TargetCardLayout.Portrait?1350:1080;}
  public static string Duration(double seconds){
   if(seconds<=0)return "0 m";if(seconds<60)return Math.Round(seconds).ToString("0",CultureInfo.InvariantCulture)+" s";
   double minutes=Math.Round(seconds/60);int hours=(int)(minutes/60),rest=(int)(minutes%60);return hours==0?rest+" m":rest==0?hours+" h":hours+" h "+rest+" m";
  }
  // Month names follow the interface language; English keeps the invariant "1 Oct 2026".
  internal static string Day(DateTime date,bool year){
   string code=Localization.Current;if(code=="ja"||code=="zh-Hans")return date.ToString(year?"yyyy年M月d日":"M月d日",CultureInfo.InvariantCulture);
   var culture=Localization.English||Localization.Pseudo?CultureInfo.InvariantCulture:CultureInfo.GetCultureInfo(code);
   return date.ToString(code=="de"?(year?"d. MMM yyyy":"d. MMM"):(year?"d MMM yyyy":"d MMM"),culture);
  }
  public static int QuarterTurn(int degrees){return ((int)Math.Round(degrees/90.0)%4+4)%4*90;}
  public static bool Eligible(string target){return !string.IsNullOrWhiteSpace(target)&&target!="All targets"&&target!="Calibration"&&!Catalog.IsAmbiguous(target);}
  // Imaged targets, most integration (or most light frames) first. Target keeps
  // the stored name so it matches the archive and the target list.
  public static List<TargetCardChoice> Rank(IEnumerable<Frame> frames,bool byFrames=false,bool includeRejected=false){
   return frames.Where(f=>f!=null&&f.Status!="Deleted"&&(f.Kind=="Light"||f.Kind=="Stack")&&Eligible(f.Target)).GroupBy(f=>f.Target,StringComparer.Ordinal).Select(g=>{var data=Build(g,g.Key,includeRejected);return new TargetCardChoice{Target=g.Key,Label=Catalog.Label(data.Target),Seconds=data.Seconds,Lights=data.Lights};})
    .Where(c=>c.Seconds>0||c.Lights>0).OrderByDescending(c=>byFrames?c.Lights:c.Seconds).ThenByDescending(c=>byFrames?c.Seconds:c.Lights).ThenBy(c=>c.Label,StringComparer.OrdinalIgnoreCase).ToList();
  }
  public static TargetCardData Build(IEnumerable<Frame> frames,string target,bool includeRejected=false){
   string key=Catalog.CanonicalTarget(target);
   var rows=frames.Where(f=>f!=null&&(f.Target==key||f.Target==target)&&f.Status!="Deleted"&&(includeRejected||!f.Rejected)&&(f.Kind=="Light"||f.Kind=="Stack")).ToList();
   var integration=CaptureGroups.ExposureFrames(rows).ToList();var timed=integration.Where(ArchiveAnalytics.KnownExposure).ToList();
   string id=Catalog.ObjectId(key),common=Catalog.CommonName(key);
   var data=new TargetCardData{Target=key,Title=id.Length>0?Catalog.DisplayId(id):Catalog.Label(key),Subtitle=id.Length>0?Catalog.DisplayIds(common):"",Seconds=timed.Sum(f=>f.Exposure.Value),Lights=rows.Count(f=>f.Kind=="Light"),Stacks=rows.Count(f=>f.Kind=="Stack"),UnknownExposure=integration.Count-timed.Count};
   var dates=rows.Select(f=>CaptureSessions.Date(f)).Where(d=>d!=null).Select(d=>d.Date.Date).Distinct().OrderBy(d=>d).ToList();data.Nights=dates.Count;
   data.DateRange=dates.Count==0?"":dates.Count==1?Day(dates[0],true):dates[0].Year==dates[dates.Count-1].Year?Day(dates[0],false)+" – "+Day(dates[dates.Count-1],true):Day(dates[0],true)+" – "+Day(dates[dates.Count-1],true);
   data.Filters=ArchiveAnalytics.Compact(timed.GroupBy(f=>string.IsNullOrWhiteSpace(f.Filter)||f.Filter=="Unknown"?"No filter recorded":f.Filter.Trim(),StringComparer.OrdinalIgnoreCase).Select(g=>new AnalyticsValue(g.First().Filter==null||string.IsNullOrWhiteSpace(g.First().Filter)||g.First().Filter=="Unknown"?"No filter recorded":g.First().Filter.Trim(),g.Sum(f=>f.Exposure.Value))).OrderByDescending(v=>v.Value).ThenBy(v=>v.Label,StringComparer.OrdinalIgnoreCase),5);
   Func<IEnumerable<string>,string> top=values=>string.Join(", ",values.Where(v=>!string.IsNullOrWhiteSpace(v)&&v!="Auto"&&!v.StartsWith("Unknown",StringComparison.OrdinalIgnoreCase)).GroupBy(v=>v.Trim(),StringComparer.OrdinalIgnoreCase).OrderByDescending(g=>g.Count()).Take(2).Select(g=>g.Key));
   string telescopes=top(rows.Select(f=>!string.IsNullOrWhiteSpace(f.TelescopeModel)?f.TelescopeModel:f.InstrumentText)),cameras=top(rows.Select(f=>f.CameraModel));
   data.Equipment=string.Join(" · ",new[]{telescopes,cameras}.Where(s=>s.Length>0));
   return data;
  }
 }
}
