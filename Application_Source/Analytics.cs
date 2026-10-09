using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 // Analytics measures individual light-frame integration, never stack exposure or
 // elapsed session time. This avoids counting the same acquisition twice.
 public sealed class AnalyticsOptions {
  public string Telescope;public DateTime? From,To;public bool IncludeRejected;
  public string Caption="Repository";
 }
 public sealed class AnalyticsValue {
  public string Label;public double Value;
  public AnalyticsValue(string label,double value){Label=label;Value=value;}
 }
 public sealed class AnalyticsReport {
  public string Id,Title,Description,Unit,Style,Note;
  public List<AnalyticsValue> Values=new List<AnalyticsValue>();
 }
 public sealed class AnalyticsSnapshot {
  public int Captures,Targets,UnknownExposure,UnknownDate,ExcludedRejected,ExcludedUndated;
  public double Seconds;public string Scope,DateRange;public DateTime GeneratedUtc;
  public List<AnalyticsReport> Reports;
 }
 public static class ArchiveAnalytics {
  public static readonly string[] Titles={"Targets photographed","Imaging timeline","Time per target","Time per telescope","Filter mix","Exposure lengths"};
  static string Label(string text,string fallback){return string.IsNullOrWhiteSpace(text)?fallback:text.Trim();}
  public static bool KnownExposure(Frame frame){return frame.Exposure.HasValue&&frame.Exposure.Value>0&&!double.IsNaN(frame.Exposure.Value)&&!double.IsInfinity(frame.Exposure.Value);}
  static List<AnalyticsValue> Groups(IEnumerable<Frame> rows,Func<Frame,string> label,bool time){
   return rows.GroupBy(label,StringComparer.OrdinalIgnoreCase).Select(g=>new AnalyticsValue(g.Key,time?g.Sum(f=>f.Exposure.Value)/3600:g.Count())).OrderByDescending(v=>v.Value).ThenBy(v=>v.Label,StringComparer.OrdinalIgnoreCase).ToList();
  }
  public static List<AnalyticsValue> Compact(IEnumerable<AnalyticsValue> source,int limit){
   var values=source.ToList();if(values.Count<=limit)return values;
   var result=values.Take(limit-1).ToList();result.Add(new AnalyticsValue("Other ("+(values.Count-limit+1)+" groups)",values.Skip(limit-1).Sum(v=>v.Value)));return result;
  }
  public static AnalyticsSnapshot Build(IEnumerable<Frame> frames,AnalyticsOptions options){
   if(options.From.HasValue&&options.To.HasValue&&options.From.Value.Date>options.To.Value.Date)throw new ArgumentException("The start date must be on or before the end date.");
   var lights=frames.Where(f=>f!=null&&f.Kind=="Light"&&f.Status!="Deleted"&&(string.IsNullOrEmpty(options.Telescope)||string.Equals(f.Telescope,options.Telescope,StringComparison.OrdinalIgnoreCase))).ToList();
   var result=new AnalyticsSnapshot{Scope=Label(options.Caption,"Repository"),GeneratedUtc=DateTime.UtcNow};
   result.ExcludedRejected=options.IncludeRejected?0:lights.Count(f=>f.Rejected);if(!options.IncludeRejected)lights=lights.Where(f=>!f.Rejected).ToList();
   var dated=lights.Select(f=>new {Frame=f,Date=CaptureSessions.Date(f)}).ToList();
   bool bounded=options.From.HasValue||options.To.HasValue;
   result.ExcludedUndated=bounded?dated.Count(d=>d.Date==null):0;
   dated=dated.Where(d=>!bounded||(d.Date!=null&&(!options.From.HasValue||d.Date.Date>=options.From.Value.Date)&&(!options.To.HasValue||d.Date.Date<=options.To.Value.Date))).ToList();
   lights=dated.Select(d=>d.Frame).ToList();var timed=lights.Where(KnownExposure).ToList();
   result.Captures=lights.Count;result.Targets=lights.Select(f=>f.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count();result.Seconds=timed.Sum(f=>f.Exposure.Value);result.UnknownExposure=lights.Count-timed.Count;result.UnknownDate=dated.Count(d=>d.Date==null);
   var dates=dated.Where(d=>d.Date!=null).Select(d=>d.Date.Date).ToList();
   result.DateRange=dates.Count==0?"Acquisition dates unavailable":dates.Min().ToString("dd MMM yyyy",CultureInfo.InvariantCulture)+" – "+dates.Max().ToString("dd MMM yyyy",CultureInfo.InvariantCulture);
   if(bounded)result.Scope+=" · "+(options.From.HasValue?options.From.Value.ToString("dd MMM yyyy",CultureInfo.InvariantCulture):"Beginning")+" to "+(options.To.HasValue?options.To.Value.ToString("dd MMM yyyy",CultureInfo.InvariantCulture):"Latest");
   string coverage=result.UnknownExposure>0?result.UnknownExposure.ToString("N0",CultureInfo.InvariantCulture)+" light frames have unknown exposure; omitted from time totals.":"All included light frames have known exposure.";
   var reports=new List<AnalyticsReport>{
    new AnalyticsReport{Id="targets",Title=Titles[0],Description="Your sky, by individual light-frame count",Unit="frames",Style="donut",Values=Groups(lights,f=>f.TargetLabel,false),Note="Light frames only; stacks, video and calibration files are excluded."},
    new AnalyticsReport{Id="timeline",Title=Titles[1],Description="Recorded integration across acquisition dates",Unit="h",Style="columns"},
    new AnalyticsReport{Id="target-time",Title=Titles[2],Description="Where your integration time goes",Unit="h",Style="bars",Values=Groups(timed,f=>f.TargetLabel,true),Note=coverage},
    new AnalyticsReport{Id="telescope-time",Title=Titles[3],Description="Integration by physical telescope",Unit="h",Style="bars",Values=Groups(timed,f=>Label(f.Telescope,"Unknown telescope"),true),Note=coverage},
    new AnalyticsReport{Id="filters",Title=Titles[4],Description="The spectral palette of your archive",Unit="h",Style="donut",Values=Groups(timed,f=>Label(f.Filter,"Unknown filter"),true),Note=coverage},
    new AnalyticsReport{Id="exposures",Title=Titles[5],Description="How long you expose each individual light frame",Unit="frames",Style="columns",Note=coverage}
   };
   var timeline=dated.Where(d=>d.Date!=null&&KnownExposure(d.Frame)).ToList();
   if(timeline.Count>0){
    DateTime first=timeline.Min(d=>d.Date.Date),last=timeline.Max(d=>d.Date.Date);bool yearly=(last.Year-first.Year)*12+last.Month-first.Month>23;
    var groups=timeline.GroupBy(d=>yearly?new DateTime(d.Date.Date.Year,1,1):new DateTime(d.Date.Date.Year,d.Date.Date.Month,1)).ToDictionary(g=>g.Key,g=>g.Sum(d=>d.Frame.Exposure.Value)/3600);
    DateTime start=groups.Keys.Min(),end=groups.Keys.Max();for(var date=start;date<=end;){double hours;groups.TryGetValue(date,out hours);reports[1].Values.Add(new AnalyticsValue(date.ToString(yearly?"yyyy":"MMM yy",CultureInfo.InvariantCulture),hours));if(date==end)break;date=yearly?date.AddYears(1):date.AddMonths(1);}
    reports[1].Description="Recorded integration by "+(yearly?"year":"month")+" of acquisition";
   }
   reports[1].Note=coverage+" "+result.UnknownDate.ToString("N0",CultureInfo.InvariantCulture)+" undated frames omitted from timeline. Dates retain their recorded clock.";
   reports[5].Note+=" Bins include the lower edge and exclude the upper edge.";
   double[] upper={5,15,30,60,120,300,double.PositiveInfinity};string[] names={"< 5 s","5–15 s","15–30 s","30–60 s","1–2 min","2–5 min","≥ 5 min"};
   for(int i=0;i<upper.Length;i++){double low=i==0?0:upper[i-1],high=upper[i];reports[5].Values.Add(new AnalyticsValue(names[i],timed.Count(f=>f.Exposure.Value>=low&&f.Exposure.Value<high)));}
   result.Reports=reports;return result;
  }
  public static string Number(double value){return value.ToString(value>0&&value<.0001?"0.##E+0":value>0&&value<.01?"0.####":value>=1000?"#,##0":value>=10?"0.#":"0.##",CultureInfo.InvariantCulture);}
 }
}
