using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public sealed class CaptureGroupSummary {
  public int Captures,Subs,Stacks,Sessions,UnknownExposure,Flagged;
  public double ExposureSeconds;
  public string Detail {get{return Captures+" captures · "+Subs+" subs · "+Stacks+" stacks · "+Sessions+" sessions · "+CaptureGroups.ExposureLabel(ExposureSeconds,UnknownExposure)+(Flagged>0?" · "+Flagged+" flagged":"");}}
 }
 public static class CaptureGroups {
  public static CaptureGroupSummary Summarize(IEnumerable<Frame> frames){
   var rows=frames.ToList();var subs=rows.Where(f=>f.Kind=="Light").ToList();
   return new CaptureGroupSummary{Captures=rows.Count,Subs=subs.Count,Stacks=rows.Count(f=>f.Kind=="Stack"),Sessions=rows.Select(f=>f.Target+"|"+f.SessionGroup).Distinct().Count(),ExposureSeconds=subs.Where(KnownExposure).Sum(f=>f.Exposure.Value),UnknownExposure=subs.Count(f=>!KnownExposure(f)),Flagged=rows.Count(CaptureScreening.NeedsReview)};
  }
  static bool KnownExposure(Frame f){return f.Exposure.HasValue&&f.Exposure.Value>0&&!double.IsNaN(f.Exposure.Value)&&!double.IsInfinity(f.Exposure.Value);}
  public static string ExposureLabel(double seconds,int unknown){
   string text=seconds>=3600?((int)(seconds/3600))+" h "+((int)(seconds%3600/60))+" min":seconds>=60?((int)(seconds/60))+" min "+((int)(seconds%60))+" s":seconds.ToString("0.#")+" s";
   return text+" in subs"+(unknown>0?" ("+unknown+" exposure unknown)":"");
  }
 }
}
