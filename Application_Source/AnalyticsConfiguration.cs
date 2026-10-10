using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroArchive {
 public sealed class AnalyticsChartOptions {
  public string Title="",Subtitle="",Style="Automatic",Order="Highest first",Palette="AstroArchive";
  public int Categories=8;
 }
 public static partial class AnalyticsGraphics {
  static AnalyticsSnapshot Configure(AnalyticsSnapshot source,int index,AnalyticsChartOptions options){
   if(options==null)return source;
   var copy=source.Copy();copy.Reports=new List<AnalyticsReport>(source.Reports);var report=source.Reports[index];
   IEnumerable<AnalyticsValue> values=report.Values;
   bool grouped=index!=1&&index!=5;
   if(grouped&&options.Order=="Lowest first")values=values.OrderBy(v=>v.Value).ThenBy(v=>v.Label,StringComparer.OrdinalIgnoreCase);
   else if(grouped&&options.Order=="Alphabetical")values=values.OrderBy(v=>v.Label,StringComparer.OrdinalIgnoreCase);
   copy.Reports[index]=new AnalyticsReport{Id=report.Id,Title=string.IsNullOrWhiteSpace(options.Title)?report.Title:options.Title.Trim(),Description=string.IsNullOrWhiteSpace(options.Subtitle)?report.Description:options.Subtitle.Trim(),Style=grouped&&options.Style=="Donut"?"donut":grouped&&options.Style=="Bars"?"bars":report.Style,Unit=report.Unit,Note=report.Note,Values=values.ToList()};
   return copy;
  }
  static void ConfigurePalette(AnalyticsPage page,bool dark,AnalyticsChartOptions options){
   if(options==null||options.Palette=="AstroArchive")return;
   string[] original=dark?new[]{"#90B8FF","#55DFEA","#C19AFF","#F0C36A","#4F9BFA","#ED9ACB","#9DD7B0","#A6B4CC"}:Colours;
   string[] palette=options.Palette=="Nebula"?(dark?new[]{"#C19AFF","#ED9ACB","#90B8FF","#55DFEA","#F0C36A","#9DD7B0","#4F9BFA","#A6B4CC"}:new[]{"#8756A5","#CB5078","#5951D6","#008477","#B37608","#6B813A","#427AB5","#64748B"}):(dark?new[]{"#55DFEA","#90B8FF","#9DD7B0","#C19AFF","#F0C36A","#4F9BFA","#ED9ACB","#A6B4CC"}:new[]{"#008477","#427AB5","#6B813A","#5951D6","#B37608","#8756A5","#CB5078","#64748B"});
   foreach(var mark in page.Marks.Where(m=>m.Role=="chart")){int i=Array.IndexOf(original,mark.Fill);if(i<0)continue;mark.Fill=palette[i];if(mark.FillEnd!=null)mark.FillEnd=Blend(mark.Fill,dark?"#735AF5":"#3B4EB5",.38);}
  }
 }
 public sealed class AnalyticsMotionTiming {
  public readonly double Duration,SceneSeconds,TransitionSeconds,RevealSeconds,StaggerSeconds;
  public AnalyticsMotionTiming(int scenes,double secondsPerChart,double totalSeconds=0){
   if(scenes<1||secondsPerChart<=0||double.IsNaN(secondsPerChart)||double.IsInfinity(secondsPerChart)||totalSeconds<0||double.IsNaN(totalSeconds)||double.IsInfinity(totalSeconds))throw new ArgumentException("Choose a valid video duration.");
   Duration=totalSeconds>0?totalSeconds:scenes*secondsPerChart;SceneSeconds=Duration/scenes;
   TransitionSeconds=Math.Min(.85,SceneSeconds*.18);RevealSeconds=Math.Min(1.1,SceneSeconds*.34);StaggerSeconds=Math.Min(.18,SceneSeconds*.07);
  }
 }
}
