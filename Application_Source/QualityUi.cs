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
   if(rows.Count==0){LocalizedMessage.Show(Window,"Choose a target or session with light frames first. Stacks, calibration frames and recordings are not scored.","Nothing to measure");return;}
   var scorable=rows.Where(f=>QualityEligibility.Reason(f).Length==0).ToList();
   if(scorable.Count==0){L("StatusLabel").Text=QualitySkipSummary(rows);return;}
   QualityRun run=null;
   Run(ct=>{run=repo.AnalyzeQuality(rows,force,ct,Progress);return run.Summary();},message=>{
    L("StatusLabel").Text=message+(run.Measured>0?". Sort by QUALITY or use Filters > Quality rank.":"");
    if(run.Errors.Count>0)ShowReport("Image quality",string.Join("\n",run.Errors));
   },"Measuring image quality");
  }
  // Explains why nothing in view can be scored, using the most common reasons.
  static string QualitySkipSummary(IEnumerable<Frame> rows){
   var reasons=rows.Select(QualityEligibility.Reason).Where(r=>r.Length>0).GroupBy(r=>r).OrderByDescending(g=>g.Count()).Take(2).Select(g=>g.Key).ToList();
   return "Nothing in view can be scored"+(reasons.Count>0?": "+string.Join("; ",reasons)+".":".");
  }
  void InitializeQuality(){
   B("AnalyzeQualityButton").Click+=(s,e)=>AnalyzeQuality(false);
   ApplyQualitySettings();
  }
 }
}
