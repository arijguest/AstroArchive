using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
namespace AstroArchive {
 // A real acquisition session, scoped to a target and physical instrument.
 public sealed class SubframeSession:INotifyPropertyChanged {
  bool selected,expanded;public string Key,Label;public List<Frame> Frames;
  public event PropertyChangedEventHandler PropertyChanged;
  void Changed(string name){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(name));}
  public bool IsSelected{get{return selected;}set{if(selected==value)return;selected=value;Changed("IsSelected");}}
  public bool Expanded{get{return expanded;}set{if(expanded==value)return;expanded=value;Changed("Expanded");}}
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
