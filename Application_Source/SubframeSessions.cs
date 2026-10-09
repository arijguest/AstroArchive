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
  internal sealed class Membership {
   readonly Dictionary<Frame,string> keys=new Dictionary<Frame,string>();readonly Dictionary<string,int> counts=new Dictionary<string,int>();
   public Membership(IEnumerable<Frame> rows,CancellationToken token){foreach(var frame in rows){token.ThrowIfCancellationRequested();string key;if(!keys.TryGetValue(frame,out key)){key=Key(frame);keys[frame]=key;}if(key!=null){int count;counts.TryGetValue(key,out count);counts[key]=count+1;}}}
   public string Get(Frame frame){return keys[frame];}
   public bool Merged(Frame frame){string key=Get(frame);return key!=null&&counts[key]>1;}
  }
  public static string Key(Frame frame){
   if(frame.Kind!="Light")return null;var date=string.IsNullOrWhiteSpace(frame.Session)?CaptureSessions.Date(frame):null;
   if(string.IsNullOrWhiteSpace(frame.Session)&&date==null)return null;
   return Util.Serialize(new[]{Catalog.CanonicalTarget(frame.Target),frame.SessionKey,date==null?"":date.Date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)});
  }
  public static List<SubframeSession> Build(IEnumerable<Frame> rows,CancellationToken token=default(CancellationToken)){
   var list=rows.ToList();return Build(list,new Membership(list,token),token);
  }
  internal static List<SubframeSession> Build(IEnumerable<Frame> rows,Membership membership,CancellationToken token){
   return rows.Select(f=>{token.ThrowIfCancellationRequested();return f;}).Where(membership.Merged).GroupBy(membership.Get).Select(g=>{
    token.ThrowIfCancellationRequested();var frames=g.ToList();var session=CaptureSessions.Describe(frames);var summary=CaptureGroups.Summarize(frames,token);
    string filters=string.Join(", ",frames.Select(f=>string.IsNullOrWhiteSpace(f.Filter)?"Unknown filter":f.Filter).Distinct());
    return new SubframeSession{Key=g.Key,Frames=frames,Label=frames[0].TargetLabel+" · "+session.Dates+" · "+frames.Count+" subs · "+SubExposureLabel(frames)+" · "+CaptureGroups.ExposureLabel(summary.ExposureSeconds,summary.UnknownExposure)+" · "+filters+" · "+frames[0].Telescope+" / "+frames[0].Camera};
   }).ToList();
  }
  static string SubExposureLabel(List<Frame> frames){
   var known=frames.Where(f=>f.Exposure.HasValue&&f.Exposure.Value>0&&!double.IsNaN(f.Exposure.Value)&&!double.IsInfinity(f.Exposure.Value)).Select(f=>f.Exposure.Value).ToList();
   if(known.Count==0)return "per-sub exposure unknown";var lengths=known.Distinct().OrderBy(s=>s).ToList();
   Func<double,string> seconds=s=>s.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
   return (lengths.Count==1?seconds(lengths[0]):seconds(lengths.First())+"–"+seconds(lengths.Last()))+" s/sub"+(lengths.Count>1?" (mixed)":"")+(known.Count<frames.Count?" + unknown":"");
  }
 }
}
