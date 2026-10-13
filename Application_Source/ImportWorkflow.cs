using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public sealed class ImportSummary {
  public int Total,Shown,Ready,Duplicates,Deleted,Rejected,Unreadable,TransferFailed,Flagged,NotScreened,SkippedFlagged;
  public long ReadyBytes;
  // Only categories with files appear, so a clean scan does not list zero counts.
  public string Text {get{var parts=new List<string>();if(Ready>0)parts.Add(Ready+" ready ("+ImportWorkflow.Size(ReadyBytes)+")");if(Duplicates>0)parts.Add(Duplicates+" duplicates");if(Deleted>0)parts.Add(Deleted+" previously deleted");if(Rejected>0)parts.Add(Rejected+" telescope rejected/reference");if(Unreadable>0)parts.Add(Unreadable+" file problems");if(TransferFailed>0)parts.Add(TransferFailed+" transfer failures");if(NotScreened>0)parts.Add(NotScreened+" not screened");return Shown==0?"":string.Join("  ·  ",parts);}}
 }
 public static class ImportWorkflow {
  public static string Size(long bytes){return bytes>=1073741824?(bytes/1073741824.0).ToString("0.##")+" GB":bytes>=1048576?(bytes/1048576.0).ToString("0.##")+" MB":bytes>=1024?(bytes/1024.0).ToString("0.##")+" KB":bytes+" bytes";}
  public static List<Frame> Select(IEnumerable<Frame> rows,bool skipFlagged,bool retry=false){return rows.Where(f=>CaptureScreening.Importable(f)&&(!retry||f.Status=="Failed")&&(!skipFlagged||(retry?!f.Rejected&&!CaptureScreening.FileProblem(f):!CaptureScreening.NeedsReview(f)))).ToList();}
  public static ImportSummary Summarize(IEnumerable<Frame> source,IEnumerable<Frame> visible,bool skipFlagged){
   var all=source.ToList();var rows=visible.ToList();var ready=Select(rows,skipFlagged);var readySet=new HashSet<Frame>(ready);
   return new ImportSummary{Total=all.Count,Shown=rows.Count,Ready=ready.Count,ReadyBytes=ready.Sum(f=>Math.Max(0,f.Bytes)),Duplicates=rows.Count(f=>(f.Status??"").StartsWith("Duplicate")),Deleted=rows.Count(f=>f.Status=="Deleted"),Rejected=rows.Count(f=>f.Rejected),Unreadable=rows.Count(CaptureScreening.FileProblem),TransferFailed=rows.Count(f=>f.Status=="Failed"),Flagged=rows.Count(CaptureScreening.NeedsReview),NotScreened=rows.Count(f=>!f.Screened),SkippedFlagged=rows.Count(f=>CaptureScreening.Importable(f)&&!readySet.Contains(f))};
  }
 }
}
