using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public static class RepositoryOrdering {
  // Set from Preferences > Show starred on top. Starred files lead the list, starred sessions lead the session groups.
  public static bool StarredFirst=true;
  public static List<EditedImage> StarredImagesFirst(List<EditedImage> images){return StarredFirst?images.Where(i=>i.IsStarred).Concat(images.Where(i=>!i.IsStarred)).ToList():images;}
  public static List<Frame> Order(IEnumerable<Frame> source,IEnumerable<SearchSort> sorting,CultureInfo culture,bool allTargets,bool sessionSummaries,CancellationToken token){
   List<SubframeSession> sessions;return Order(source,sorting,culture,allTargets,sessionSummaries,token,out sessions);
  }
  public static List<Frame> Order(IEnumerable<Frame> source,IEnumerable<SearchSort> sorting,CultureInfo culture,bool allTargets,bool sessionSummaries,CancellationToken token,out List<SubframeSession> sessions){
   // Every ordering starts from the library's own order (target, date, session, best-scored sub first) rather than from whatever order was on screen,
   // so sorting by a column and returning to the default never leaves a session's subs in the previous sort's order.
   var rows=QualityOrdering.Order(source);var membership=sessionSummaries?new SubframeSessions.Membership(rows,token):null;
   // Stable partition after column sorting retains the user's sort within each section. In a single target's table the
   // grouped subs sit last (section 3, ahead of calibration) so loose files and stacks are never split up by session headers.
   var sections=Enumerable.Range(0,5).Select(i=>new List<Frame>()).ToArray();foreach(var frame in SearchOrdering.Order(rows,sorting,culture,token)){
    token.ThrowIfCancellationRequested();int section=CaptureSky.IsCalibration(frame)?4:allTargets?(frame.Kind=="Video"?3:sessionSummaries&&membership.Merged(frame)||!sessionSummaries&&frame.Kind=="Light"?0:frame.Kind=="Stack"?1:2):frame.Kind=="Video"?0:frame.Kind=="Stack"?1:sessionSummaries&&membership.Merged(frame)?3:2;sections[section].Add(frame);
   }
   var ordered=sections.SelectMany(s=>s).ToList();
   // A section such as Comets holds several targets; list each one's captures together, in the sorted order.
   if(!allTargets)ordered=TargetNavigation.GroupByTarget(ordered,f=>f.Target);
   if(StarredFirst)ordered=StarredLeading(ordered,membership,sessionSummaries,allTargets);
   sessions=sessionSummaries?SubframeSessions.Build(ordered,membership,token):null;return ordered;
  }
  // Starred files move above everything except grouped subs; among the grouped subs, fully starred sessions come first. Both moves are stable.
  static List<Frame> StarredLeading(List<Frame> ordered,SubframeSessions.Membership membership,bool sessionSummaries,bool allTargets){
   Func<Frame,bool> grouped=f=>sessionSummaries&&membership.Merged(f);
   if(sessionSummaries){
    var spots=new List<int>();for(int i=0;i<ordered.Count;i++)if(grouped(ordered[i]))spots.Add(i);
    if(spots.Count>1){
     var groups=spots.Select(i=>ordered[i]).GroupBy(membership.Get).Select(g=>g.ToList()).ToList();
     var arranged=groups.Where(g=>g.All(f=>f.IsStarred)).Concat(groups.Where(g=>!g.All(f=>f.IsStarred))).SelectMany(g=>g).ToList();
     for(int k=0;k<spots.Count;k++)ordered[spots[k]]=arranged[k];
    }
   }
   Func<Frame,bool> pinned=f=>f.IsStarred&&!grouped(f)&&!CaptureSky.IsCalibration(f);
   int lead=allTargets&&sessionSummaries?ordered.TakeWhile(grouped).Count():0;
   var rest=ordered.Skip(lead).ToList();
   return ordered.Take(lead).Concat(rest.Where(pinned)).Concat(rest.Where(f=>!pinned(f))).ToList();
  }
 }
}
