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
 public enum TargetCardLayout {Portrait,Vertical,Square,Widescreen}
 public static class TargetCards {
  public static readonly string[] LayoutLabels={"Portrait · 4:5","Vertical · 9:16","Square · 1:1","Widescreen · 16:9"};
  public static void Size(TargetCardLayout layout,out int width,out int height){width=layout==TargetCardLayout.Widescreen?1920:1080;height=layout==TargetCardLayout.Vertical?1920:layout==TargetCardLayout.Portrait?1350:1080;}
  public static string Duration(double seconds){
   if(seconds<=0)return "0 m";if(seconds<60)return Math.Round(seconds).ToString("0",CultureInfo.InvariantCulture)+" s";
   double minutes=Math.Round(seconds/60);int hours=(int)(minutes/60),rest=(int)(minutes%60);return hours==0?rest+" m":rest==0?hours+" h":hours+" h "+rest+" m";
  }
  static string Day(DateTime date,bool year){return date.ToString(year?"d MMM yyyy":"d MMM",CultureInfo.InvariantCulture);}
  public static TargetCardData Build(IEnumerable<Frame> frames,string target){
   string key=Catalog.CanonicalTarget(target);
   var rows=frames.Where(f=>f!=null&&f.Target==key&&f.Status!="Deleted"&&!f.Rejected&&(f.Kind=="Light"||f.Kind=="Stack")).ToList();
   var integration=CaptureGroups.ExposureFrames(rows).ToList();var timed=integration.Where(ArchiveAnalytics.KnownExposure).ToList();
   string id=Catalog.ObjectId(key),common=Catalog.CommonName(key);
   var data=new TargetCardData{Target=key,Title=id.Length>0?id:Catalog.Label(key),Subtitle=id.Length>0?common:"",Seconds=timed.Sum(f=>f.Exposure.Value),Lights=rows.Count(f=>f.Kind=="Light"),Stacks=rows.Count(f=>f.Kind=="Stack"),UnknownExposure=integration.Count-timed.Count};
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
