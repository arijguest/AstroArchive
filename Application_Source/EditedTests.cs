using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void EditedRegressions(){
   Test("Edited filename classes distinguish starless and stars only without guessing star fields",()=>{
    Check(EditedMetadata.Read("M31_starless_120x60s_Ha.fit",null).ImageClass=="Starless","Starless filename missed");Check(EditedMetadata.Read("M31_stars-only.tif",null).ImageClass=="Stars only","Stars-only filename missed");Check(EditedMetadata.Read("M31_stars.fit",null).ImageClass=="Stars only","Stars layer missed");Check(EditedMetadata.Read("M31_starfield.fits",null).ImageClass=="Edited image","Star field misclassified");Check(EditedMetadata.Read("M31_starless_stars.fit",null).ImageClass.Contains("conflicting"),"Conflicting class invented");
   });
   Test("Edited filename products recover objects filters subs and total exposure including mixed durations",()=>{
    var metadata=EditedMetadata.Read("M31_120x60s_Ha_starless.fit",null);Check(metadata.Object=="M31"&&metadata.Filters=="Ha"&&metadata.Subs==120&&metadata.SubExposure==60&&metadata.TotalExposure==7200,"Filename acquisition data wrong");
    metadata=EditedMetadata.Read("NGC7000_Ha_20x5min_OIII_40x180s_stars.tif",null);Check(metadata.Subs==60&&metadata.SubExposure==null&&metadata.TotalExposure==13200&&metadata.Filters=="Ha, OIII","Mixed durations combined incorrectly");Check(EditedMetadata.Read("M51_subs120_total2h.fit",null).TotalExposure==7200,"Labelled total missed");Check(EditedMetadata.Read("M31_20261008_starless.fit",null).TotalExposure==null,"Calendar date invented exposure");
   });
   Test("Explicit edited metadata wins and ambiguous exposure is retained without inventing a total",()=>{
    var header=new FitsHeader();header.Values["OBJECT"]="M51";header.Values["FILTER"]="L";header.Values["NCOMBINE"]="50";header.Values["EXPTIME"]="120";
    var result=EditedMetadata.Read("M31_starless.fit",header);Check(result.Object=="M51"&&result.Subs==50&&result.ReportedExposure==120&&result.TotalExposure==null&&result.SubExposure==null,"Ambiguous EXPTIME multiplied");header.Values["SUBEXP"]="120";result=EditedMetadata.Read("M51_starless.fit",header);Check(result.TotalExposure==6000,"Explicit per-sub duration lost");header.Values.Remove("SUBEXP");header.Comments["EXPTIME"]="Total integrated exposure (s)";Check(EditedMetadata.Read("M51.fit",header).TotalExposure==120,"Explicit total comment ignored");
    header.Values["TOTEXP"]="6000";result=EditedMetadata.Read("M51_40x60s.fit",header);Check(result.TotalExposure==6000&&result.Subs==50&&result.SubExposure==null&&result.Evidence.Contains("disagrees"),"Conflicting filename overwritten metadata");
   });
   Test("Unknown edited acquisition data stays unknown and malformed numeric fields are refused",()=>{
    var header=new FitsHeader();header.Values["TOTEXP"]="NaN";header.Values["NCOMBINE"]="-2";header.Values["SUBEXP"]="Infinity";var result=EditedMetadata.Read("holiday_starless.tif",header);Check(result.Object==null&&result.Subs==null&&result.TotalExposure==null&&result.SubExposure==null,"Invalid metadata invented numbers");Check(EditedMetadata.Read("M31_M51_stars.fit",null).Object==null,"Ambiguous target guessed");
   });
   Test("Siril handoff automatically creates a portable independent Edited working copy",()=>{
    string source=Path.Combine(root,"edited-siril-source");var header=LightHeaders(new DateTime(2026,10,8,21,0,0),"M51");header["NCOMBINE"]="12";header["SUBEXP"]="60";string file=Path.Combine(source,"Stacked_M51.fit");Write(file,64,48,(x,y)=>1700,header);string app=Path.Combine(root,"siril.exe");File.WriteAllText(app,"fixture");
    using(var repo=new Repository(Path.Combine(root,"edited-siril-repo"))){repo.Import(repo.Scan(source,"Scope-1","Auto",ct,NoProgress).Frames,ct,NoProgress);var capture=repo.All().Single();var project=SirilHandoff.CreateWorkingCopy(repo,new List<Frame>{capture},"Whirlpool edit",app,ct,NoProgress);List<string> errors;Check(repo.EditedProjects(out errors).Single().Id==project.Id&&errors.Count==0,"Working copy not registered");string image=repo.EditedPath(project,project.Sources[0].RelativePath);Check(Util.Hash(image,ct)==capture.Hash&&SirilHandoff.LaunchInfo(app,image).WorkingDirectory==Path.GetDirectoryName(image),"Editor got wrong working copy");
     File.AppendAllText(image,"editor update");Check(Util.Hash(repo.FilePath(capture),ct)==capture.Hash&&Util.Hash(file,ct)==capture.Hash,"Editing changed archived/original pixels");string output=Path.Combine(Path.GetDirectoryName(image),"M51_starless.fits");Write(output,64,48,(x,y)=>2300,new Dictionary<string,string>());var row=repo.EditedImages(project).Single(i=>i.Filename=="M51_starless.fits");Check(row.Kind=="Editor output"&&row.Metadata.ImageClass=="Starless"&&row.Metadata.TotalExposure==720,"Output lost working-copy acquisition provenance");
     repo.Scan(repo.Root,"Scope-1","Auto",ct,NoProgress,true);Check(repo.All().Count==1,"Reindex imported mutable editor images");repo.ResetArchive(ct,NoProgress);Check(File.Exists(output)&&repo.EditedProjects(out errors).Count==1,"Archive reset deleted Edited project");
    }
   });
   Test("Unverified editor handoffs create no Edited project",()=>{
    using(var repo=new Repository(Path.Combine(root,"edited-invalid-app-repo"))){string app=Path.Combine(root,"fake-AstroWizard.exe");File.WriteAllText(app,"invalid build");Expect(()=>AstroWizardHandoff.CreateWorkingCopy(repo,new List<Frame>{new Frame{Kind="Stack",OriginalName="stack.fit"}},"Must not exist",app,ct,NoProgress),"Invalid editor accepted");Check(!Directory.Exists(repo.EditedFolder),"Invalid editor created a project");}
   });
   Test("Adding edited images preserves filename collisions and survives repository moves",()=>{
    string source=Path.Combine(root,"edited-add-source");string one=Path.Combine(source,"one","M31_starless.fit"),two=Path.Combine(source,"two","M31_starless.fit");Write(one,64,48,(x,y)=>1400,new Dictionary<string,string>());Write(two,64,48,(x,y)=>2200,new Dictionary<string,string>());string destination=Path.Combine(root,"edited-add-repo"),moved=destination+"-moved";
    using(var repo=new Repository(destination)){var project=repo.AddEditedImages(new[]{one,two},null,"Andromeda",ct,NoProgress);Check(repo.EditedImages(project).Count==2&&project.Sources.Select(s=>s.RelativePath).Distinct().Count()==2,"Collision overwrote an image");Check(project.Sources.Select(s=>Util.Hash(repo.EditedPath(project,s.RelativePath),ct)).OrderBy(h=>h).SequenceEqual(new[]{Util.Hash(one,ct),Util.Hash(two,ct)}.OrderBy(h=>h)),"Copies lost bytes");Check(repo.All().Count==0,"Edited images entered capture index");}
    Directory.Move(destination,moved);using(var repo=new Repository(moved)){List<string> errors;Check(repo.EditedImages(repo.EditedProjects(out errors).Single()).Count==2,"Moving repository lost Edited images");}
   });
   Test("Folder crawl reviews nested metadata and unreadable images before verified copying",()=>{
    string source=Path.Combine(root,"edited-folder-source");Write(Path.Combine(source,"M31_10x60s_Ha_starless.fit"),64,48,(x,y)=>1900,new Dictionary<string,string>());Write(Path.Combine(source,"nested","M31_10x60s_Ha_stars.fit"),64,48,(x,y)=>2400,new Dictionary<string,string>());File.WriteAllText(Path.Combine(source,"broken.fit"),"not FITS");File.WriteAllText(Path.Combine(source,"notes.txt"),"retain");
    using(var repo=new Repository(Path.Combine(root,"edited-folder-repo"))){var top=repo.ScanEditedFolder(source,false,ct,NoProgress);Check(top.Images.Count==2,"Non-recursive scan entered a subfolder");var plan=repo.ScanEditedFolder(source,true,ct,NoProgress);Check(plan.Images.Count==3&&plan.Images.Count(i=>i.Include)==2&&plan.Images.Single(i=>i.Filename=="broken.fit").Problem!=null,"Unreadable images were not isolated");Check(!Directory.Exists(repo.EditedFolder),"Review copied images");var project=repo.ImportEditedFolder(plan,"Existing edits",ct,NoProgress);var images=repo.EditedImages(project);Check(images.Count==2&&images.All(i=>i.Metadata.Subs==10&&i.Metadata.TotalExposure==600)&&project.Sources.Any(s=>s.RelativePath.Contains("nested")),"Folder structure or metadata lost");Check(Directory.GetFiles(source,"*.fit",SearchOption.AllDirectories).Length==3&&repo.All().Count==0,"Crawl altered sources or original index");}
   });
   Test("Folder import refuses changed sources and removes its unpublished project",()=>{
    string source=Path.Combine(root,"edited-stale-source"),file=Path.Combine(source,"M31_starless.fit");Write(file,64,48,(x,y)=>1800,new Dictionary<string,string>());using(var repo=new Repository(Path.Combine(root,"edited-stale-repo"))){var plan=repo.ScanEditedFolder(source,true,ct,NoProgress);File.AppendAllText(file,"changed");Expect(()=>repo.ImportEditedFolder(plan,"Stale",ct,NoProgress),"Changed source accepted");List<string> errors;Check(repo.EditedProjects(out errors).Count==0&&errors.Count==0&&!Directory.EnumerateDirectories(repo.EditedFolder).Any(),"Changed import left a partial project");}
   });
   Test("Cancelled edited copies retain sources and no partial project",()=>{
    string source=Path.Combine(root,"edited-cancel-source"),file=Path.Combine(source,"M51_starless.fit");Write(file,64,48,(x,y)=>1800,new Dictionary<string,string>());using(var repo=new Repository(Path.Combine(root,"edited-cancel-repo")))using(var cancel=new CancellationTokenSource()){Expect(()=>repo.AddEditedImages(new[]{file},null,"Cancelled",cancel.Token,p=>cancel.Cancel()),"Cancelled copy succeeded");List<string> errors;Check(repo.EditedProjects(out errors).Count==0&&errors.Count==0&&File.Exists(file)&&!Directory.GetFiles(repo.EditedFolder,"*",SearchOption.AllDirectories).Any(),"Cancellation retained a partial image or lost original");}
   });
   Test("Edited paths and malformed manifests cannot escape the workspace",()=>{
    string source=Path.Combine(root,"edited-boundary-source"),file=Path.Combine(source,"M51.fit");Write(file,64,48,(x,y)=>1800,new Dictionary<string,string>());using(var repo=new Repository(Path.Combine(root,"edited-boundary-repo"))){var project=repo.AddEditedImages(new[]{file},null,"Boundary",ct,NoProgress);Expect(()=>repo.EditedPath(project,"../escape.fit"),"Traversal accepted");project.Sources[0].RelativePath="../../../../escape.fit";File.WriteAllText(Path.Combine(repo.EditedProjectFolder(project),"edited-project.json"),Util.Serialize(project));List<string> errors;Check(repo.EditedProjects(out errors).Count==0&&errors.Count==1&&File.Exists(file),"Forged manifest accepted or source affected");Expect(()=>repo.ScanEditedFolder(repo.Root,true,ct,NoProgress),"Repository crawled into itself");}
   });
  }
 }
}
