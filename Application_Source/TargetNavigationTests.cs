using System;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void TargetNavigationTests(){
   Test("Target groups use catalogue types and recognise comet designations",()=>{
    foreach(string target in new[]{"M42","M57","NGC6888","C27","IC1805","B33","NGC6960"})Check(TargetNavigation.Group(target)=="Nebulae","Nebula catalogue type missing: "+target);
    foreach(string target in new[]{"M31","M51","NGC253"})Check(TargetNavigation.Group(target)=="Galaxies","Galaxy misgrouped: "+target);
    foreach(string target in new[]{"M45","M13","M44","C14"})Check(TargetNavigation.Group(target)=="Star clusters","Cluster catalogue type missing: "+target);
    foreach(string target in new[]{"C/2023 A3 (Tsuchinshan-ATLAS)","C2023A3","C/2014 UN271","12P/Pons-Brooks","1P Halley","2I/Borisov","3I/ATLAS","Comet ATLAS","NEOWISE"})Check(TargetNavigation.Group(target)=="Solar system","Comet designation missing: "+target);
    foreach(string target in new[]{"Sun","Solar","Moon","Jupiter"})Check(TargetNavigation.Group(target)=="Solar system","Solar-system target misgrouped: "+target);
    Check(TargetNavigation.Group("Calibration")=="Calibration"&&TargetNavigation.Group("Unknown")=="Unidentified"&&TargetNavigation.Group("My wide field")=="Other targets","Unclassified/calibration targets lost");
    Check(TargetNavigation.Group("A/2017 U1")=="Other targets"&&TargetNavigation.Group("1I/Oumuamua")=="Other targets","Asteroidal or unclassified interstellar designation treated as a comet");
    Check(TargetNavigation.Group("Custom nebula and galaxy")=="Other targets","Conflicting custom type guessed");
   });
   Test("Grouped navigation retains every file and keeps type sections contiguous",()=>{
    var rows=new[]{new Frame{Target="M31",Kind="Light",Exposure=60},new Frame{Target="C/2023 A3",Kind="Light",Exposure=30},new Frame{Target="M42",Kind="Light",Exposure=90},new Frame{Target="NGC6888",Kind="Stack",StackCount=1445,Exposure=3600},new Frame{Target="12P/Pons-Brooks",Kind="Light",Exposure=30},new Frame{Target="M42",Kind="Dark",Exposure=90},new Frame{Target="Unknown",Kind="Unknown"},new Frame{Target="Meteor",Kind="Light"},new Frame{Target="My wide field",Kind="Light"},new Frame{Target="M45",Kind="Light"}};
    var summaries=TargetNavigation.Build(rows);Check(summaries.First().Name=="All targets"&&summaries.First().Files==rows.Length&&summaries.Sum(s=>s.Name=="All targets"?0:s.Files)==rows.Length,"Navigation lost or duplicated files");
    var types=summaries.Skip(1).Select(s=>s.Group).ToList();Check(types.Count(type=>type=="Solar system")==2&&types.IndexOf("Solar system")+1==types.LastIndexOf("Solar system")&&types.IndexOf("Nebulae")+1==types.LastIndexOf("Nebulae"),"Target types scattered across the list");
    Check(summaries.Single(s=>s.Name=="M42").Files==2&&summaries.Last().Group=="Unidentified","Frame types omitted or unidentified targets misplaced");
    var groups=types.Distinct().ToList();Check(groups.IndexOf("Meteors")>groups.IndexOf("Star clusters")&&groups.IndexOf("Meteors")+1==groups.IndexOf("Other targets"),"Meteors are not just above Other targets");
    var stack=rows.Single(f=>f.Kind=="Stack");var filters=new CaptureFilters();filters.Values["Frame type"]="Stack";Check(stack.KindLabel=="Stack (1445)"&&filters.Apply(rows,"stack").Single()==stack&&CaptureFilters.Options(rows,"Frame type").Contains("Stack"),"Stack count changed its filtering identity");
    Check(new Frame{Kind="Stack"}.KindLabel=="Stack"&&new Frame{Kind="Light",StackCount=1445}.KindLabel=="Light","Missing stack count or a non-stack gained a misleading count");
    var filtered=TargetNavigation.Build(rows.Where(f=>f.Kind=="Stack"));Check(filtered.Count==2&&filtered[0].Files==1&&filtered[1].Name=="NGC6888","Filtered target list retained empty sections");
    Check(TargetNavigation.Build(new Frame[0]).Single().Name=="All targets","Empty repository lost the All targets option");
   });
   Test("Concise target rows keep identity counts and honest exposure totals in tooltips",()=>{
    var rows=new[]{new Frame{Target="M42",Kind="Light",Exposure=3600},new Frame{Target="M42",Kind="Light",Exposure=60},new Frame{Target="M42",Kind="Light"},new Frame{Target="M42",Kind="Stack",Exposure=7200},new Frame{Target="M42",Kind="Dark",Exposure=900}};
    var target=TargetNavigation.Build(rows).Single(s=>s.Name=="M42");Check(target.DisplayName=="M42 - Orion Nebula"&&target.FileCount=="5"&&target.Subline==TargetNavigation.Identifier("M42")+" · 1h 1m + ?","Short row lost identity or counted stack/calibration exposure");
    Check(target.Tooltip.Contains("M42")&&target.Tooltip.Contains("5 files · 3 subs · 1 stack")&&target.Tooltip.Contains("1 h 1 min total")&&target.Tooltip.Contains("exposure unknown"),"Full details omitted from the tooltip");
    var stack=TargetNavigation.Build(new[]{new Frame{Target="C/2023 A3",Kind="Stack",Exposure=7200}})[1];Check(stack.Subline==""&&!stack.Tooltip.Contains("total"),"Stack total presented as acquisition exposure");
    Check(TargetNavigation.Exposure(20)=="20s"&&TargetNavigation.Exposure(90)=="1m 30s"&&TargetNavigation.Exposure(3600)=="1h","Compact exposure labels misleading");
    var comet=TargetNavigation.Build(new[]{new Frame{Target="C/2023 A3 (Tsuchinshan-ATLAS)",Kind="Light",Exposure=30}})[1];Check(comet.DisplayName=="C/2023 A3 - Tsuchinshan-ATLAS"&&comet.Subline=="30s"&&comet.Tooltip.Contains("C/2023 A3 (Tsuchinshan-ATLAS)"),"Short comet label lost its identity");
    Check(TargetNavigation.ShortName("12P/Pons-Brooks")=="12P - Pons-Brooks"&&TargetNavigation.Identifier("12P/Pons-Brooks")=="","Numbered comet labels remained verbose");
   });
   Test("Repository ordering keeps merged subs stacks and calibrations in their sections",()=>{
    var a=new Frame{Target="M31",Kind="Light",Session="fixture",OriginalName="z-sub.fit"};var b=a.Clone();b.OriginalName="a-sub.fit";
    var stack=new Frame{Target="M31",Kind="Stack",OriginalName="a-stack.fit"};var single=new Frame{Target="M31",Kind="Light",OriginalName="single.fit"};
    var dark=new Frame{Target="M31",Kind="Master dark",OriginalName="a-dark.fit"};var flat=new Frame{Target="Calibration",Kind="Master flat",OriginalName="flat.fit"};var rows=new[]{dark,stack,single,a,flat,b};
    foreach(bool descending in new[]{false,true}){var sorts=new[]{new SearchSort{Property="OriginalName",Descending=descending}};
     var ordered=RepositoryOrdering.Order(rows,sorts,System.Globalization.CultureInfo.InvariantCulture,true,true,ct);Check(ordered.Take(2).All(f=>f==a||f==b)&&ordered[2]==stack&&ordered[3]==single&&ordered.Skip(4).All(CaptureSky.IsCalibration),"All Targets sections mixed");
     Check(ordered[0]==(descending?a:b),"Column sort lost within merged subs");
     var target=RepositoryOrdering.Order(rows,sorts,System.Globalization.CultureInfo.InvariantCulture,false,true,ct);Check(target[0]==stack&&target.Skip(4).All(CaptureSky.IsCalibration),"Single-target stacks or calibration order wrong");
     var filtered=RepositoryOrdering.Order(rows.Where(f=>f!=b),sorts,System.Globalization.CultureInfo.InvariantCulture,true,true,ct);Check(filtered[0]==stack,"Filtered singleton retained merged-group priority");
    }
   });
   Test("Target rows omit repeated catalogue IDs without merging shared names",()=>{
    var targets=TargetNavigation.Build(new[]{new Frame{Target="IC434"},new Frame{Target="NGC2024"}});
    Check(targets.Single(t=>t.Name=="IC434").DisplayName=="IC434 - Flame Nebula","IC434 repeats its ID after the common name");
    Check(targets.Single(t=>t.Name=="NGC2024").DisplayName=="NGC2024 - Flame Nebula"&&targets.Count==3,"Shared display names merged catalogue identities");
    Check(Catalog.KnownName(Catalog.CommonName("IC434"))=="IC434"&&Catalog.KnownName("Flame Nebula")=="NGC2024","Display formatting changed name resolution");
   });
  }
 }
}
