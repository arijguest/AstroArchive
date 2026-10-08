using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 // A real acquisition session, scoped to a target and physical instrument.
 public sealed class SubframeSession {
  public bool Expanded{get;set;} public string Key,Label;public List<Frame> Frames;
 }
 public static class SubframeSessions {
  public static List<SubframeSession> Build(IEnumerable<Frame> rows){
   return rows.Where(f=>f.Kind=="Light"&&(!string.IsNullOrEmpty(f.Session)||CaptureSessions.Date(f)!=null)).GroupBy(f=>f.Target+"|"+f.SessionKey+(string.IsNullOrEmpty(f.Session)?"|"+CaptureSessions.Date(f).Date.ToString("yyyy-MM-dd"):"")).Where(g=>g.Count()>1).Select(g=>{
    var frames=g.ToList();var session=CaptureSessions.Describe(frames);var summary=CaptureGroups.Summarize(frames);
    string filters=string.Join(", ",frames.Select(f=>string.IsNullOrWhiteSpace(f.Filter)?"Unknown filter":f.Filter).Distinct());
    return new SubframeSession{Key=g.Key,Frames=frames,Label=frames[0].TargetLabel+" · "+session.Dates+" · "+frames.Count+" subs · "+CaptureGroups.ExposureLabel(summary.ExposureSeconds,summary.UnknownExposure)+" · "+filters+" · "+frames[0].Telescope+" / "+frames[0].Camera};
   }).ToList();
  }
 }
}
