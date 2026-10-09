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
  public static CaptureGroupSummary Summarize(IEnumerable<Frame> frames,CancellationToken token=default(CancellationToken)){
   var summary=new CaptureGroupSummary();var sessions=new HashSet<string>();
   foreach(var frame in frames){token.ThrowIfCancellationRequested();summary.Captures++;sessions.Add(frame.SessionKey);if(frame.Kind=="Light")summary.Subs++;if(frame.Kind=="Video")summary.Videos++;if(frame.Kind=="Light"||frame.Kind=="Video"){if(KnownExposure(frame))summary.ExposureSeconds+=frame.Exposure.Value;else summary.UnknownExposure++;}if(frame.Kind=="Stack")summary.Stacks++;if(CaptureScreening.NeedsReview(frame))summary.Flagged++;}
   summary.Sessions=sessions.Count;return summary;
  }
  static bool KnownExposure(Frame f){return f.Exposure.HasValue&&f.Exposure.Value>0&&!double.IsNaN(f.Exposure.Value)&&!double.IsInfinity(f.Exposure.Value);}
  public static string ExposureLabel(double seconds,int unknown){
   string text=seconds>=3600?((int)(seconds/3600))+" h "+((int)(seconds%3600/60))+" min":seconds>=60?((int)(seconds/60))+" min "+((int)(seconds%60))+" s":seconds.ToString("0.#")+" s";
   return text+" total"+(unknown>0?" ("+unknown+" exposure unknown)":"");
  }
 }
}
