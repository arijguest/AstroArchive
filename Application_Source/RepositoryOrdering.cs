using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public static class RepositoryOrdering {
  public static List<Frame> Order(IEnumerable<Frame> source,IEnumerable<SearchSort> sorting,CultureInfo culture,bool allTargets,bool sessionSummaries,CancellationToken token){
   var rows=source.ToList();var merged=allTargets&&sessionSummaries?new HashSet<Frame>(SubframeSessions.Build(rows,token).SelectMany(s=>s.Frames)):new HashSet<Frame>();
   // Stable partition after column sorting retains the user's sort within each section.
   return SearchOrdering.Order(rows,sorting,culture,token).OrderBy(f=>{
    token.ThrowIfCancellationRequested();if(CaptureSky.IsCalibration(f))return 3;
    if(allTargets){if(sessionSummaries&&merged.Contains(f)||!sessionSummaries&&f.Kind=="Light")return 0;return f.Kind=="Stack"?1:2;}
    return f.Kind=="Stack"?0:1;
   }).ToList();
  }
 }
}
