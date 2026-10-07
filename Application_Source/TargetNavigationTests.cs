using System;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void TargetNavigationTests(){
   Test("Target groups use catalogue types and recognise comet designations",()=>{
    foreach(string target in new[]{"M42","M57","NGC6888","C27","IC1805","B33","NGC6960"})Check(TargetNavigation.Group(target)=="Nebulae","Nebula catalogue type missing: "+target);
    foreach(string target in new[]{"M31","M51","NGC253"})Check(TargetNavigation.Group(target)=="Galaxies","Galaxy misgrouped: "+target);
    foreach(string target in new[]{"M45","M13","M44","C14"})Check(TargetNavigation.Group(target)=="Star clusters","Cluster catalogue type missing: "+target);
    foreach(string target in new[]{"C/2023 A3 (Tsuchinshan-ATLAS)","C2023A3","C/2014 UN271","12P/Pons-Brooks","1P Halley","2I/Borisov","3I/ATLAS","Comet ATLAS","NEOWISE"})Check(TargetNavigation.Group(target)=="Comets","Comet designation missing: "+target);
    foreach(string target in new[]{"Sun","Solar","Moon","Jupiter"})Check(TargetNavigation.Group(target)=="Solar system","Solar-system target misgrouped: "+target);
    Check(TargetNavigation.Group("Calibration")=="Calibration"&&TargetNavigation.Group("Unknown")=="Unidentified"&&TargetNavigation.Group("My wide field")=="Other targets","Unclassified/calibration targets lost");
    Check(TargetNavigation.Group("A/2017 U1")=="Other targets"&&TargetNavigation.Group("1I/Oumuamua")=="Other targets","Asteroidal or unclassified interstellar designation treated as a comet");
    Check(TargetNavigation.Group("Custom nebula and galaxy")=="Other targets","Conflicting custom type guessed");
   });
   Test("Grouped navigation retains every file and keeps type sections contiguous",()=>{
    var rows=new[]{new Frame{Target="M31",Kind="Light",Exposure=60},new Frame{Target="C/2023 A3",Kind="Light",Exposure=30},new Frame{Target="M42",Kind="Light",Exposure=90},new Frame{Target="NGC6888",Kind="Stack",Exposure=3600},new Frame{Target="12P/Pons-Brooks",Kind="Light",Exposure=30},new Frame{Target="M42",Kind="Dark",Exposure=90},new Frame{Target="Unknown",Kind="Unknown"}};
    var summaries=TargetNavigation.Build(rows);Check(summaries.First().Name=="All targets"&&summaries.First().Files==rows.Length&&summaries.Sum(s=>s.Name=="All targets"?0:s.Files)==rows.Length,"Navigation lost or duplicated files");
    var types=summaries.Skip(1).Select(s=>s.Group).ToList();Check(types.Count(type=>type=="Comets")==2&&types.IndexOf("Comets")+1==types.LastIndexOf("Comets")&&types.IndexOf("Nebulae")+1==types.LastIndexOf("Nebulae"),"Target types scattered across the list");
    Check(summaries.Single(s=>s.Name=="M42").Files==2&&summaries.Last().Group=="Unidentified","Frame types omitted or unidentified targets misplaced");
    var filtered=TargetNavigation.Build(rows.Where(f=>f.Kind=="Stack"));Check(filtered.Count==2&&filtered[0].Files==1&&filtered[1].Name=="NGC6888","Filtered target list retained empty sections");
    Check(TargetNavigation.Build(new Frame[0]).Single().Name=="All targets","Empty repository lost the All targets option");
   });
   Test("Concise target rows keep identity counts and honest exposure totals in tooltips",()=>{
    var rows=new[]{new Frame{Target="M42",Kind="Light",Exposure=3600},new Frame{Target="M42",Kind="Light",Exposure=60},new Frame{Target="M42",Kind="Light"},new Frame{Target="M42",Kind="Stack",Exposure=7200},new Frame{Target="M42",Kind="Dark",Exposure=900}};
    var target=TargetNavigation.Build(rows).Single(s=>s.Name=="M42");Check(target.DisplayName=="Orion Nebula"&&target.FileCount=="5"&&target.Subline=="M42 · 1h 1m + ?","Short row lost identity or counted stack/calibration exposure");
    Check(target.Tooltip.Contains("M42")&&target.Tooltip.Contains("5 files · 3 subs · 1 stack")&&target.Tooltip.Contains("exposure unknown"),"Full details omitted from the tooltip");
    var stack=TargetNavigation.Build(new[]{new Frame{Target="C/2023 A3",Kind="Stack",Exposure=7200}})[1];Check(stack.Subline==""&&!stack.Tooltip.Contains("in subs"),"Stack total presented as acquisition exposure");
    Check(TargetNavigation.Exposure(20)=="20s"&&TargetNavigation.Exposure(90)=="1m 30s"&&TargetNavigation.Exposure(3600)=="1h","Compact exposure labels misleading");
    var comet=TargetNavigation.Build(new[]{new Frame{Target="C/2023 A3 (Tsuchinshan-ATLAS)",Kind="Light",Exposure=30}})[1];Check(comet.DisplayName=="Tsuchinshan-ATLAS"&&comet.Subline=="C/2023 A3 · 30s"&&comet.Tooltip.Contains("C/2023 A3 (Tsuchinshan-ATLAS)"),"Short comet label lost its identity");
    Check(TargetNavigation.ShortName("12P/Pons-Brooks")=="Pons-Brooks"&&TargetNavigation.Identifier("12P/Pons-Brooks")=="12P","Numbered comet labels remained verbose");
   });
  }
 }
}
