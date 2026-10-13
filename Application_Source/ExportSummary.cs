// Compact descriptions of what an export will include, for the Export window header.
using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public sealed class ExportSummary {public string Title="",Targets="",Detail="",TargetList="";}
 public static class ExportSummaries {
  static string Count(int n,string word,string plural=null){return n.ToString("N0")+" "+(n==1?word:plural??word+"s");}
  // Most frequent targets first; long lists are shortened and kept whole in TargetList.
  public static void Targets(IEnumerable<string> labels,ExportSummary summary){
   var known=(labels??Enumerable.Empty<string>()).Where(l=>!string.IsNullOrWhiteSpace(l)&&!l.Equals("Unknown",StringComparison.OrdinalIgnoreCase)).GroupBy(l=>l.Trim(),StringComparer.OrdinalIgnoreCase).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.Key).ToList();
   summary.TargetList=string.Join("\n",known);
   summary.Targets=known.Count==0?"":known.Count<=3?string.Join(", ",known):string.Join(", ",known.Take(2))+" and "+(known.Count-2)+" more targets";
  }
  public static ExportSummary Describe(IList<Frame> frames,bool selected){
   frames=frames??new List<Frame>();var summary=new ExportSummary{Title=frames.Count==0?"No files selected":selected?Count(frames.Count,"selected file"):Count(frames.Count,"file")+" in view"};
   Targets(frames.Where(f=>f!=null&&!Assets.IsCalibration(f.Kind)).Select(f=>f.Target=="Unknown"?"":f.TargetLabel),summary);
   int subs=frames.Count(f=>f.Kind=="Light"),stacks=frames.Count(f=>f.Kind=="Stack"),videos=frames.Count(f=>f.Kind=="Video"),calibrations=frames.Count(f=>Assets.IsCalibration(f.Kind)),other=frames.Count-subs-stacks-videos-calibrations;
   summary.Detail=string.Join(" · ",new[]{subs>0?Count(subs,"sub"):"",stacks>0?Count(stacks,"stack"):"",videos>0?Count(videos,"recording"):"",calibrations>0?Count(calibrations,"calibration frame"):"",other>0?Count(other,"other file"):""}.Where(s=>s.Length>0));
   return summary;
  }
 }
}
