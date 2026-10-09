using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void TargetNamesTests(){
   Test("Every supplementary common name and alias resolves to its intended object",()=>{
    int checkedNames=0;var errors=new List<string>();foreach(string row in CatalogNames.Entries){var fields=row.Split('|');Check(fields.Length==3,"Malformed catalogue supplement: "+row);string id=Catalog.KnownName(fields[0]);Check(id!=null&&Catalog.Objects.Any(o=>o.Name==id),"Supplement has no catalogue position: "+row);
     Check(Catalog.CommonName(id)==fields[1],"Preferred display name missing: "+row);
     foreach(string label in new[]{fields[1]}.Concat(fields[2].Split(';')).Where(s=>s.Length>0)){
      if(Catalog.KnownName(label)!=id||Catalog.CanonicalTarget(label)!=id)errors.Add("Alias: "+label+" -> "+(Catalog.KnownName(label)??"unresolved")+", expected "+id);
      if(Catalog.TargetFromFilename("Light_"+label.Replace(' ','_')+"_001.fit")!=id)errors.Add("Filename: "+label);
      if(Catalog.Search(label).FirstOrDefault(o=>o.Name==id)==null)errors.Add("Search: "+label);checkedNames++;
     }
    }Check(errors.Count==0,string.Join("; ",errors));Check(checkedNames>150,"Common-name coverage shrank");
   });
   Test("Catalogue display names always resolve to their own identity and shared names remain qualified",()=>{
    foreach(var item in Catalog.Objects.Where(o=>!string.IsNullOrEmpty(Catalog.CommonName(o.Name))))Check(Catalog.KnownName(Catalog.CommonName(item.Name))==item.Name,"Display name points to another object: "+item.Name);
    Check(Catalog.KnownName("Eagle Nebula")=="M16"&&Catalog.KnownName("Flame Nebula")=="NGC2024"&&Catalog.KnownName("Eastern Veil")=="NGC6992","Whole-region name lost its declared owner");
    Check(Catalog.KnownName("Antennae Galaxies")==null&&Catalog.CommonName("NGC4038").Contains("NGC4038")&&Catalog.CommonName("NGC4039").Contains("NGC4039"),"Shared galaxy names silently chose an individual object");
    string evidence;Check(Catalog.KnownName("Lobster Nebula")==null&&EditedTargetMatcher.Default.Resolve("Lobster Nebula",out evidence)==null&&Catalog.KnownName("Lobster Nebula (NGC6357)")=="NGC6357","Ambiguous nickname silently selected another nebula");
   });
   Test("C23 NGC891 and Silver Sliver aliases share display identity grouping and search",()=>{
    var ids=new[]{"C23","C 023","Caldwell-23","Caldwell No. 23","NGC 00891","UGC1831","PGC9031","Silver Sliver Galaxy","Outer Limits Galaxy"};
    foreach(string label in ids){var frame=new Frame{Target=label};Check(frame.Target=="NGC891"&&frame.TargetName=="Silver Sliver Galaxy"&&TargetNavigation.Group(label)=="Galaxies","Silver Sliver identity/display wrong: "+label);Check(new CaptureFilters().Apply(new[]{frame},label).Count==1&&Catalog.Search(label).First().Name=="NGC891","Alternate identity not searchable: "+label);}
    var target=TargetNavigation.Build(ids.Select(label=>new Frame{Target=label})).Single(t=>t.Name=="NGC891");Check(target.Files==ids.Length&&target.DisplayName=="NGC891 - Silver Sliver Galaxy"&&target.Subline.StartsWith("C23")&&target.Subline.Contains("UGC1831")&&target.Subline.Contains("PGC9031"),"Aliases split targets or disappeared from the navigation row");
    Check(Catalog.TargetFromFilename("C23_NGC891_Silver_Sliver_Galaxy.fit")=="NGC891"&&!Catalog.HasFilenameConflict("C23_NGC891_Silver_Sliver_Galaxy.fit"),"Equivalent names conflicted");
   });
   Test("Common-name normalization handles full catalogue prefixes accents and punctuation",()=>{
    foreach(var pair in new[]{new[]{"Messier No. 051","M51"},new[]{"Messier-0051","M51"},new[]{"Caldwell Number 023","NGC891"},new[]{"Barnard 033","B33"},new[]{"Sh2-155","C9"},new[]{"Sharpless 155","C9"},new[]{"Böde’s Galaxy","M81"},new[]{"Sílver–Sliver Galaxy","NGC891"}}){
     Check(Catalog.KnownName(pair[0])==pair[1]&&Catalog.CanonicalTarget(pair[0])==pair[1]&&Catalog.TargetFromFilename("Light_"+pair[0]+"_001.fit")==pair[1],"Prefix/spelling normalization differs by workflow: "+pair[0]);
    }
    Check(Catalog.KnownName("891")==null&&Catalog.TargetFromFilename("Light_camera891.fit")==null&&Catalog.KnownName("NGC23")!="NGC891","Numbers or catalogue prefixes were guessed");
   });
   Test("Conflicting and unrecognised catalogue IDs cannot silently inherit a known target",()=>{
    foreach(string label in new[]{"C23_M51","NGC891_NGC999999","Silver Sliver Galaxy_NGC891A"})Check(Catalog.HasFilenameConflict(label)&&Catalog.TargetFromFilename(label+".fit")==null&&Catalog.CanonicalTarget(label)!="NGC891","Contradictory ID ignored: "+label);
    string evidence;Check(EditedTargetMatcher.Default.Resolve("NGC892",out evidence)==null&&Catalog.KnownName("NGC892")!="NGC891","Catalogue digits were fuzzy matched");
    Check(Catalog.TargetFromFilename("unknown_NGC999999.fit")==null,"Unknown catalogue number was invented");
   });
   Test("Saved names persist across restart and immediately serve archives imports and fuzzy matching",()=>{
    var before=EditedTargetMatcher.Default;var rule=new TargetNameRule{Id="C23",CommonName="Arija's Silver Sliver",Aliases=new List<string>{"Aríja’s Sliver"}};
    var saved=Util.Deserialize<Settings>(Util.Serialize(new Settings{TargetNames=new List<TargetNameRule>{rule}}));
    try{
     Catalog.ConfigureNames(saved.TargetNames);string evidence;
     Check(Catalog.KnownName("Arijas Sliver")=="NGC891"&&Catalog.TargetFromFilename("Light_Arijas_Sliver_001.fit")=="NGC891"&&Catalog.CommonName("C23")==rule.CommonName,"Remembered names missing from shared registry");
     Check(!ReferenceEquals(before,EditedTargetMatcher.Default)&&EditedTargetMatcher.Default.Resolve("Arijas Slivr final",out evidence)=="NGC891","Edited fuzzy matcher kept its old catalogue snapshot");
     var archived=Util.Deserialize<Frame>("{\"Target\":\"Arijas Sliver\"}");Check(archived.Target=="NGC891"&&archived.TargetName==rule.CommonName&&Catalog.Search("Arijas Sliver").First().Common==rule.CommonName,"Existing archive or target picker did not pick up saved name");
     var header=new FitsHeader();header.Values["OBJECT"]="Arijas Sliver";Check(EditedMetadata.Read("result.fit",header).Object=="NGC891"&&CaptureSky.FromHeader(header,"FITS","capture.fit").Target=="NGC891","Edited or sky metadata bypassed saved names");
     Catalog.ConfigureNames(null);Check(Catalog.KnownName("Arijas Sliver")==null&&Catalog.CommonName("NGC891")=="Silver Sliver Galaxy","Restoring built-in names retained a stale alias");
     Catalog.ConfigureNames(saved.TargetNames);Check(Util.Deserialize<Frame>("{\"Target\":\"Arijas Sliver\"}").ObjectId=="NGC891","Saved names did not survive application restart");
    }finally{Catalog.ConfigureNames(null);}
   });
   Test("Invalid saved aliases are rejected atomically without changing catalogue ownership",()=>{
    try{
     Catalog.ConfigureNames(new[]{new TargetNameRule{Id="NGC891",Aliases=new List<string>{"Personal Sliver"}}});
     foreach(string alias in new[]{"M51","C24","NGC999999","Whirlpool Galaxy","Lobster Nebula","Unknown","Calibration","Personal Sliver"}){
      var rules=new[]{new TargetNameRule{Id="NGC891",Aliases=new List<string>{"Personal Sliver"}},new TargetNameRule{Id="M31",CommonName=alias}};
      Expect(()=>Catalog.ConfigureNames(rules),"Conflicting saved alias accepted: "+alias);Check(Catalog.KnownName("Personal Sliver")=="NGC891"&&Catalog.KnownName("C24")=="NGC1275","Invalid update partially changed the registry");
     }
     Expect(()=>Catalog.ConfigureNames(new[]{new TargetNameRule{Id="NGC891",Aliases=new List<string>{"M51 unknown"}}}),"A name containing a conflicting ID was accepted");
    }finally{Catalog.ConfigureNames(null);}
   });
   Test("Different telescope target labels import together without changing original image data",()=>{
    string source=Path.Combine(root,"alias-name-source");string[] names={"C23","NGC891","Silver_Sliver_Galaxy"};
    for(int i=0;i<names.Length;i++)Write(Path.Combine(source,"Light_"+names[i]+"_00"+i+".fit"),64,48,(x,y)=>1500+i,LightHeaders(new DateTime(2026,10,8,21,i,0),names[i].Replace('_',' ')));
    using(var repo=new Repository(Path.Combine(root,"alias-name-repo"))){var scan=repo.Scan(source,"Scope","Auto",ct,NoProgress);repo.Import(scan.Frames,ct,NoProgress);var rows=repo.All();Check(rows.Count==3&&rows.All(f=>f.Target=="NGC891"&&f.TargetName=="Silver Sliver Galaxy")&&TargetNavigation.Build(rows).Single(t=>t.Name=="NGC891").Files==3&&rows.All(f=>Util.Hash(repo.FilePath(f),ct)==f.Hash),"Imports split equivalent identities or changed image bytes");}
   });
  }
 }
}
