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
   // Stable partition after column sorting retains the user's sort within each section.
   var sections=Enumerable.Range(0,5).Select(i=>new List<Frame>()).ToArray();foreach(var frame in SearchOrdering.Order(rows,sorting,culture,token)){
    token.ThrowIfCancellationRequested();int section=CaptureSky.IsCalibration(frame)?4:allTargets?(frame.Kind=="Video"?3:sessionSummaries&&membership.Merged(frame)||!sessionSummaries&&frame.Kind=="Light"?0:frame.Kind=="Stack"?1:2):frame.Kind=="Video"?0:frame.Kind=="Stack"?1:2;sections[section].Add(frame);
   }
   var ordered=sections.SelectMany(s=>s).ToList();sessions=sessionSummaries?SubframeSessions.Build(ordered,membership,token):null;return ordered;
  }
 }
}
