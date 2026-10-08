using System;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void FileSearchTests(){
   var rows=new[]{
    new Frame{Target="C27",Kind="Light",Telescope="Scope-1",Filter="Dual band",OriginalName="Light_NGC6888_60s.fit",SourcePath=@"D:\Observations\North Field\Light_NGC6888_60s.fit",Exposure=60,Gain=80,Temperature=-5,Night="2026-10-06",Notes="Wide_field · José",Status="New"},
    new Frame{Target="M45",Kind="Stack",Telescope="Scope-2",Filter="Broadband",OriginalName="Stack_Failed_M45.fits",Exposure=10,Gain=0,Status="Failed"},
    new Frame{Target="M45",Kind="Light",Telescope="Scope-1",Filter="Broadband",OriginalName="Light_M45_20s.fit",Exposure=20,Status="New"}};
   var filters=new CaptureFilters();
   Test("Search resolves multiword names and spaced catalogue IDs with other terms",()=>{foreach(string query in new[]{"Crescent Nebula filter:dual","NGC 6888 Scope-1","target:\"Crescent Nebula\"","target:C27"})Check(filters.Apply(rows,query).SequenceEqual(rows.Take(1)),query+" did not resolve the same target");});
   Test("Search phrases handle accents and filename separators but retain order",()=>{Check(filters.Apply(rows,"\"wide field\" jose").Single()==rows[0],"Phrase/accent matching failed");Check(filters.Apply(rows,"\"field wide\"").Count==0,"Phrase order lost");Check(filters.Apply(rows,"\"north field\"").Single()==rows[0],"Source path missing from search");});
   Test("Search exclusions and alternative groups respect active table filters",()=>{Check(filters.Apply(rows,"target:M45 -type:Stack").Single()==rows[2],"Exclusion failed");Check(filters.Apply(rows,"C27 OR M45 -failed").Count==2,"OR precedence failed");filters.Values["Frame type"]="Light";try{Check(filters.Apply(rows,"C27 | M45").Count==2,"OR bypassed filters");}finally{filters.Reset();}});
   Test("Search fields stay scoped and wildcards match filenames",()=>{Check(filters.Apply(rows,"file:*.fits").Single()==rows[1],"Wildcard extension matching failed");Check(filters.Apply(rows,"file:*.fit").Count==2,"Glob should respect filename extension boundaries");Check(filters.Apply(rows,"notes:M45").Count==0,"Scoped term leaked to target");Check(filters.Apply(rows,"date:2026-10 device:Scope-1").Single()==rows[0],"Fields did not combine");Check(filters.Apply(rows,@"path:D:\Observations").Single()==rows[0],"Windows path colon was interpreted as a field");});
   Test("Search numeric comparisons retain physical values and exclude unknowns",()=>{Check(filters.Apply(rows,"exposure:>=20 gain:80 temperature:<0").Single()==rows[0],"Comparisons failed");Check(filters.Apply(rows,"exposure=20").Single()==rows[2],"Exact comparison failed");Check(filters.Apply(rows,"gain:0").Single()==rows[1],"Zero became unknown");Check(filters.Apply(rows,"-exposure:>20").Count==2,"Negative comparison failed");});
   Test("Incomplete search syntax never broadens an import view",()=>{foreach(string query in new[]{"\"unfinished","exposure:>","exposure>=","gain:NaN","M45 OR","OR M45","file:"})Check(FileSearch.Parse(query).Error!=null&&filters.Apply(rows,query).Count==0,"Incomplete query returned files: "+query);Check(filters.Apply(rows,"---").Count==0,"Punctuation matched every normalized row");Check(filters.Apply(rows,"  ").Count==3,"Empty search did not restore all rows");});
   Test("Search engine supports edited project documents with the same query rules",()=>{var document=new SearchDocument{Target="M45",Text="M45 Pleiades blue edit"};document.Fields["target"]="M45 Pleiades";document.Fields["project"]="Blue edit";document.Fields["file"]="Pleiades_stars.png";Check(FileSearch.Parse("target:M 45 project:\"blue edit\" -file:*.jpg").Matches(document),"Edited document rules differ");});
  }
 }
}
