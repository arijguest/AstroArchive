using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void TargetWorkflowTests(){
   Test("Comet filenames and metadata resolve to solar-system targets without stealing catalogue objects",()=>{
    foreach(string name in new[]{"NEAT","LINEAR","NEOWISE","Pan-STARRS","SWAN","SOHO","Tsuchinshan-ATLAS","C_2002_T7_LINEAR","C2023A3_Tsuchinshan-ATLAS","12P_Pons-Brooks"}){
     var metadata=EditedMetadata.Read(name+"_stack.fit",new FitsHeader());Check(CometTargets.IsComet(metadata.Object)&&TargetNavigation.Group(metadata.Object)=="Solar system","Edited comet was lost: "+name);
    }
    foreach(string name in new[]{"C/2002 T7 (LINEAR)","C/2001 Q4 (NEAT)","12P/Pons-Brooks"})Check(CometTargets.IsComet(Catalog.Normalize(name))&&TargetNavigation.Group(Catalog.Normalize(name))=="Solar system","Metadata comet identity damaged: "+name);
    Check(Catalog.CanonicalTarget("23P/Brorsen-Metcalf")=="23P/Brorsen-Metcalf","An unfamiliar comet surname was discarded");
    Check(!TargetIdentification.NeedsPlateSolve(new Frame{Target="C/2001 Q4 (NEAT)",OriginalName="Light_001.fit",TargetEvidence="Recognised header/session target"}),"A named comet was treated as an unidentified deep-sky object");
    Check(Catalog.CanonicalTarget("M45_NEAT_stack")=="M45"&&TargetNavigation.Group("M45_NEAT_stack")=="Star clusters","Comet survey word overrode a known target");
    Check(!CometTargets.IsComet("Atlas wide field")&&!CometTargets.IsComet("A/2017 U1")&&!CometTargets.IsComet("1I/Oumuamua"),"Ambiguous label was confidently classified as a comet");
    var targets=TargetNavigation.Build(new[]{"Moon","NEAT","Jupiter","NEOWISE","M31"}.Select(t=>new Frame{Target=t}));Check(targets.Skip(1).Take(4).All(t=>t.Group=="Solar system")&&targets.Skip(3).Take(2).All(t=>CometTargets.IsComet(t.Name)),"Comets were interleaved with planets");
    Check(TargetNavigation.ShortName("M31")=="M31 - Andromeda Galaxy"&&!TargetNavigation.Identifier("M31").Split('·').Select(s=>s.Trim()).Contains("M31"),"Preferred ID was missing or duplicated");
   });
   Test("Natural searches combine object device and stack while respecting exact identities",()=>{
    var dwarf=new Frame{Target="M45",Make="DWARFLAB",Model="Dwarf 3",Kind="Light",OriginalName="one.fit"};
    var pro=new Frame{Target="M45",Make="Seestar",Model="Seestar S50 Pro",Kind="Stack",OriginalName="two.fit"};
    var plain=new Frame{Target="M45",Make="Seestar",Model="Seestar S50",Kind="Light",OriginalName="M45_processing_stack.fit"};
    var wrong=new Frame{Target="M31",Make="Seestar",Model="Seestar S50 Pro",Kind="Stack",OriginalName="M45.fit"};var rows=new[]{dwarf,pro,plain,wrong};
    foreach(string query in new[]{"M45 Dwarflab","M45 dwarf"})Check(rows.Where(FileSearch.Parse(query).Matches).SequenceEqual(new[]{dwarf}),"Dwarf brand alias failed: "+query);
    foreach(string query in new[]{"M45 S50 Pro","M45 S50Pro","M45 S50-Pro","M45 S50 Pro stack","M45 stack","M 45 stacks"})Check(rows.Where(FileSearch.Parse(query).Matches).SequenceEqual(new[]{pro}),"Natural phrase returned unrelated files: "+query);
    Check(FileSearch.Parse("M45 S50").Matches(plain)&&FileSearch.Parse("M45 S50").Matches(pro),"Model family search hid its Pro variant");
    Check(FileSearch.Parse("file:M45*").Matches(wrong)&&FileSearch.Parse("\"stack\"").Matches(plain),"Explicit filename/literal search semantics changed");
   });
   Test("Selections survive target changes and refresh but deselect visible rows and prune removed files",()=>{
    var one=new Frame{Hash="one",Target="M31"};var two=new Frame{Hash="two",Target="M45"};var three=new Frame{Hash="three",Target="M45"};
    var selection=new TargetSelection<Frame>(f=>f.Hash);selection.ChangeVisible(new[]{one},new[]{one});selection.ChangeVisible(new[]{two,three},new[]{two,three});Check(selection.Count==3,"Switching targets dropped an earlier selection");
    selection.ChangeVisible(new[]{two,three},new[]{three});Check(selection.Count==2&&selection.Contains(one)&&!selection.Contains(two),"Deselecting a visible file removed a hidden target");
    var fresh=one.Clone();selection.Reconcile(new[]{fresh,two});Check(selection.Count==1&&selection.Items[0]==fresh,"Refresh kept stale objects or deleted files");selection.Clear();Check(selection.Count==0,"Clear left a hidden selection");
   });
   Test("Edited export copies only selected images across projects and keeps colliding names and source bytes",()=>{
    string source=Path.Combine(root,"target-export-source"),file=Path.Combine(source,"M31.fit"),other=Path.Combine(source,"other","M31.fit");Write(file,64,48,(x,y)=>2000,new Dictionary<string,string>());Write(other,64,48,(x,y)=>3000,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"target-export-repo"))){
     var first=repo.AddEditedImages(new[]{file},null,"First",ct,NoProgress);var second=repo.AddEditedImages(new[]{other},null,"Second",ct,NoProgress);var rows=EditedGallery.Read(repo,new string[0],ct).Images;
     File.Copy(file,Path.Combine(repo.EditedProjectFolder(first),"unselected.fit"));string destination=Path.Combine(root,"target-export-destination");Directory.CreateDirectory(destination);File.WriteAllText(Path.Combine(destination,"M31.fit"),"keep");
     string hash=Util.Hash(file,ct),manifest=File.ReadAllText(Path.Combine(repo.EditedProjectFolder(second),"edited-project.json"));Exporter.CreateEdited(repo,rows.Concat(rows.Take(1)),new ExportOptions{Parent=destination},ct,NoProgress);
     var copies=Directory.GetFiles(destination).Where(p=>Path.GetFileName(p)!="M31.fit").ToArray();Check(copies.Length==2&&copies.Select(p=>Util.Hash(p,ct)).OrderBy(h=>h).SequenceEqual(new[]{hash,Util.Hash(other,ct)}.OrderBy(h=>h))&&File.ReadAllText(Path.Combine(destination,"M31.fit"))=="keep","Export overwrote an existing file, duplicated selection or changed bytes");
     Check(rows.All(i=>File.Exists(repo.EditedPath(i.Project,i.RelativePath)))&&File.ReadAllText(Path.Combine(repo.EditedProjectFolder(second),"edited-project.json"))==manifest&&Util.Hash(file,ct)==hash,"Export altered source images or metadata");
     string invalid=Path.Combine(root,"target-export-invalid");Directory.CreateDirectory(invalid);Expect(()=>Exporter.CreateEdited(repo,new[]{rows[0],new EditedImage{Project=first,RelativePath="../escape.fit"}},new ExportOptions{Parent=invalid},ct,NoProgress),"Invalid path accepted");Check(!Directory.EnumerateFileSystemEntries(invalid).Any(),"Invalid selection left a partial export");
     Expect(()=>Exporter.CreateEdited(repo,rows,new ExportOptions{Parent=repo.EditedProjectFolder(first)},ct,NoProgress),"Export wrote back into archive");
     using(var canceled=new CancellationTokenSource()){canceled.Cancel();Expect(()=>Exporter.CreateEdited(repo,rows,new ExportOptions{Parent=invalid},canceled.Token,NoProgress),"Canceled export ran");}Check(!Directory.EnumerateFileSystemEntries(invalid).Any(),"Canceled export left files");
    }
   });
  }
 }
}
