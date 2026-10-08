using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
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
  public static string Key(Frame frame){
   if(frame.Kind!="Light")return null;var date=string.IsNullOrWhiteSpace(frame.Session)?CaptureSessions.Date(frame):null;
   if(string.IsNullOrWhiteSpace(frame.Session)&&date==null)return null;
   return Util.Serialize(new[]{Catalog.CanonicalTarget(frame.Target),frame.SessionKey,date==null?"":date.Date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)});
  }
  public static List<SubframeSession> Build(IEnumerable<Frame> rows,CancellationToken token=default(CancellationToken)){
   return rows.Select(f=>{token.ThrowIfCancellationRequested();return f;}).Where(f=>Key(f)!=null).GroupBy(Key).Where(g=>g.Count()>1).Select(g=>{
    token.ThrowIfCancellationRequested();var frames=g.ToList();var session=CaptureSessions.Describe(frames);var summary=CaptureGroups.Summarize(frames,token);
    string filters=string.Join(", ",frames.Select(f=>string.IsNullOrWhiteSpace(f.Filter)?"Unknown filter":f.Filter).Distinct());
    return new SubframeSession{Key=g.Key,Frames=frames,Label=frames[0].TargetLabel+" · "+session.Dates+" · "+frames.Count+" subs · "+CaptureGroups.ExposureLabel(summary.ExposureSeconds,summary.UnknownExposure)+" · "+filters+" · "+frames[0].Telescope+" / "+frames[0].Camera};
   }).ToList();
  }
 }
}
