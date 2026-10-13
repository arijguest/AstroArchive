using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public static class RepositoryOrdering {
  public static List<Frame> Order(IEnumerable<Frame> source,IEnumerable<SearchSort> sorting,CultureInfo culture,bool allTargets,bool sessionSummaries,CancellationToken token){
   List<SubframeSession> sessions;return Order(source,sorting,culture,allTargets,sessionSummaries,token,out sessions);
  }
  public static List<Frame> Order(IEnumerable<Frame> source,IEnumerable<SearchSort> sorting,CultureInfo culture,bool allTargets,bool sessionSummaries,CancellationToken token,out List<SubframeSession> sessions){
   var rows=source.ToList();var membership=sessionSummaries?new SubframeSessions.Membership(rows,token):null;
   // Stable partition after column sorting retains the user's sort within each section. In a single target's table the
   // grouped subs sit last (section 3, ahead of calibration) so loose files and stacks are never split up by session headers.
   var sections=Enumerable.Range(0,5).Select(i=>new List<Frame>()).ToArray();foreach(var frame in SearchOrdering.Order(rows,sorting,culture,token)){
    token.ThrowIfCancellationRequested();int section=CaptureSky.IsCalibration(frame)?4:allTargets?(frame.Kind=="Video"?3:sessionSummaries&&membership.Merged(frame)||!sessionSummaries&&frame.Kind=="Light"?0:frame.Kind=="Stack"?1:2):frame.Kind=="Video"?0:frame.Kind=="Stack"?1:sessionSummaries&&membership.Merged(frame)?3:2;sections[section].Add(frame);
   }
   var ordered=sections.SelectMany(s=>s).ToList();
   // A section such as Comets holds several targets; list each one's captures together, in the sorted order.
   if(!allTargets)ordered=TargetNavigation.GroupByTarget(ordered,f=>f.Target);sessions=sessionSummaries?SubframeSessions.Build(ordered,membership,token):null;return ordered;
  }
 }
}
