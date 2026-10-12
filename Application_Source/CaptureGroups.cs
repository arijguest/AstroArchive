using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class CaptureGroupSummary {
  public int Captures,Subs,Videos,Stacks,Sessions,UnknownExposure,Flagged;
  public double ExposureSeconds;
  public string Detail {get{return Captures+" captures · "+Subs+" subs"+(Videos>0?" · "+Videos+" video"+(Videos==1?"":"s"):"")+" · "+Stacks+" stacks · "+Sessions+" sessions · "+CaptureGroups.ExposureLabel(ExposureSeconds,UnknownExposure)+(Flagged>0?" · "+Flagged+" flagged":"");}}
 }
 public static class CaptureGroups {
  static string TargetKey(Frame frame){return Catalog.CanonicalTarget(frame.Target).ToUpperInvariant();}
  static string DateKey(Frame frame){var date=CaptureSessions.Date(frame);return date==null?null:date.Date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);}
  // A stack is counted only when no light sub of its target was saved in the same session or on the same date;
  // otherwise its integration would duplicate those subs.
  public static List<Frame> ExposureFrames(IEnumerable<Frame> frames,CancellationToken token=default(CancellationToken)){
   var rows=new List<Frame>();foreach(var frame in frames){token.ThrowIfCancellationRequested();if(frame!=null)rows.Add(frame);}
   var sessions=new HashSet<string>(StringComparer.Ordinal);var dates=new HashSet<string>(StringComparer.Ordinal);
   foreach(var frame in rows){token.ThrowIfCancellationRequested();if(frame.Kind!="Light")continue;string target=TargetKey(frame);if(!string.IsNullOrWhiteSpace(frame.Session))sessions.Add(frame.Session.Trim().ToUpperInvariant()+"\0"+target);string date=DateKey(frame);if(date!=null)dates.Add(date+"\0"+target);}
   return rows.Where(f=>{
    if(f.Kind=="Light"||f.Kind=="Video")return true;
    if(f.Kind!="Stack")return false;
    string target=TargetKey(f);
    if(!string.IsNullOrWhiteSpace(f.Session)&&sessions.Contains(f.Session.Trim().ToUpperInvariant()+"\0"+target))return false;
    string date=DateKey(f);return date==null||!dates.Contains(date+"\0"+target);
   }).ToList();
  }
  public static CaptureGroupSummary Summarize(IEnumerable<Frame> frames,CancellationToken token=default(CancellationToken)){
   var rows=frames.ToList();var summary=new CaptureGroupSummary();var sessions=new HashSet<string>();var exposure=ExposureFrames(rows,token);
   foreach(var frame in rows){token.ThrowIfCancellationRequested();summary.Captures++;sessions.Add(frame.SessionKey);if(frame.Kind=="Light")summary.Subs++;if(frame.Kind=="Video")summary.Videos++;if(frame.Kind=="Stack")summary.Stacks++;if(CaptureScreening.NeedsReview(frame))summary.Flagged++;}
   foreach(var frame in exposure){token.ThrowIfCancellationRequested();if(KnownExposure(frame))summary.ExposureSeconds+=frame.Exposure.Value;else summary.UnknownExposure++;}
   summary.Sessions=sessions.Count;return summary;
  }
  static bool KnownExposure(Frame f){return f.Exposure.HasValue&&f.Exposure.Value>0&&!double.IsNaN(f.Exposure.Value)&&!double.IsInfinity(f.Exposure.Value);}
  public static string ExposureLabel(double seconds,int unknown){
   string text=seconds>=3600?((int)(seconds/3600))+" h "+((int)(seconds%3600/60))+" min":seconds>=60?((int)(seconds/60))+" min "+((int)(seconds%60))+" s":seconds.ToString("0.#")+" s";
   return text+" total"+(unknown>0?" ("+unknown+" exposure unknown)":"");
  }
 }
}
