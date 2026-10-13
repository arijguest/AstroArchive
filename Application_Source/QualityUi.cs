// Image quality: the Tools menu commands, the measuring run and the preference hooks.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AstroArchive {
 public partial class MainUi {
  // Keeps the repository's comparison rule in step with the saved preference.
  void ApplyQualitySettings(){if(repo!=null)repo.QualityCompareTarget=settings.QualityCompareTarget;}

  // Measures the captures in view (or selected). Already measured captures are reused unless a fresh measurement is requested.
  void AnalyzeQuality(bool force){
   if(repo==null||RepositoryOperationBlocked)return;
   var rows=Context().Where(f=>f.Status!="Deleted").ToList();
   if(rows.Count==0){LocalizedMessage.Show(Window,"Choose a target or session with light frames or stacks first. Calibration frames and recordings are not scored.","Nothing to measure");return;}
   var scorable=rows.Where(f=>QualityEligibility.Reason(f).Length==0).ToList();
   if(scorable.Count==0){L("StatusLabel").Text=QualitySkipSummary(rows);return;}
   // Measures copies in the background, so importing, exporting and Settings stay available while it runs.
   var frames=rows.Select(f=>f.Clone()).ToList();var repository=repo;QualityRun run=null;
   RunBackground(BackgroundJob.Quality,"Measuring image quality",frames,(job,token)=>{run=repository.AnalyzeQuality(frames,force,token,job.Report);return run.Summary();},(message,success)=>{
    // Whatever was measured is stored, even when the run was stopped, so the library always shows it.
    if(repository!=repo)return;
    repository.ApplyQuality(all);Filter(true);
    string line=message+(run!=null&&run.Measured>0?". Sort by QUALITY or use Filters > Quality rank.":"");
    BackgroundStatus(line);if(success)completionActivity.Status=line;
    if(run!=null&&run.NeedsReport)ShowReport("Image quality report",run.Report());
   },"Stopped. Measurements already stored are kept.");
  }
  // Explains why nothing in view can be scored, using the most common reasons.
  static string QualitySkipSummary(IEnumerable<Frame> rows){
   var reasons=rows.Select(QualityEligibility.Reason).Where(r=>r.Length>0).GroupBy(r=>r).OrderByDescending(g=>g.Count()).Take(2).Select(g=>g.Key).ToList();
   return "Nothing in view can be scored"+(reasons.Count>0?": "+string.Join("; ",reasons)+".":".");
  }
  // Capture details: the score, its rank among similar frames and any flags.
  static string QualityFact(Frame f){
   if(!f.QualityScore.HasValue)return "";
   return Math.Round(f.QualityScore.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)+" of 100 · "+f.QualityRank.ToLowerInvariant()+(string.IsNullOrEmpty(f.QualityFlags)?"":" · "+f.QualityFlags);
  }
  // Session details: the spread of scores across the group.
  static string QualityGroupFact(IList<Frame> frames){
   var scores=frames.Where(f=>f.QualityScore.HasValue).Select(f=>f.QualityScore.Value).OrderBy(v=>v).ToList();if(scores.Count==0)return "";
   Func<double,string> n=v=>Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture);
   double median=scores.Count%2==1?scores[scores.Count/2]:(scores[scores.Count/2-1]+scores[scores.Count/2])/2;
   int flagged=frames.Count(f=>f.QualityScore.HasValue&&!string.IsNullOrEmpty(f.QualityFlags)),unscored=frames.Count-scores.Count;
   return "median "+n(median)+" · best "+n(scores[scores.Count-1])+" · weakest "+n(scores[0])+(flagged>0?" · "+flagged+" flagged":"")+(unscored>0?" · "+unscored+" not scored":"");
  }
  void InitializeQuality(){
   B("AnalyzeQualityButton").Click+=(s,e)=>AnalyzeQuality(false);
   ApplyQualitySettings();
  }
 }
}
