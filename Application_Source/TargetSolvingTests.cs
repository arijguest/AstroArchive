using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static Frame SolveLight(string name,string target="Unknown",string session="s1"){return new Frame{Hash=name,OriginalName=name,Kind="Light",Target=target,Session=session,Telescope="Scope-1",Camera="Telephoto",Width=64,Height=48};}
  static SolveResult PleiadesSolution(){return new SolveResult{RA=56.75,Dec=24.1167,Radius=0.5,Solver="Test solver"};}
  static void TargetSolvingTests(){
   Test("Selected Lights share one solve per session target and physical sensor; stacks remain independent",()=>{
    var frames=new[]{SolveLight("a","M51"),SolveLight("b","Whirlpool Galaxy"),SolveLight("other-target","M31"),SolveLight("other-session","M51","s2"),SolveLight("other-camera","M51"),SolveLight("no-session-a","M51",""),SolveLight("no-session-b","M51","")};frames[4].Camera="Wide angle";frames[1].Exposure=120;frames[1].Filter="Ha";
    var stack=SolveLight("stack","M51");stack.Kind="Stack";var stack2=stack.Clone();stack2.Hash="stack2";stack2.OriginalName="stack2";
    var jobs=TargetSolving.Plan(frames.Concat(new[]{stack,stack2}));Check(jobs.Count==8&&jobs[0].Frames.Count==2&&jobs[0].Frames.Contains(frames[1]),"Grouping merged unrelated frames or solved compatible filters separately");Check(jobs.Count(j=>j.Representative.Kind=="Stack")==2,"Stacks share a representative");
    var cropped=frames[0].Clone();cropped.Roi="100,100,64,48";Check(TargetSolving.Plan(new[]{frames[0],cropped}).Count==2,"Different sensor crops shared a solve");
   });
   Test("A Light subgroup solves once without decoding its other selected images",()=>{
    var frames=new[]{SolveLight("one"),SolveLight("two"),SolveLight("three")};frames[1].Rejected=true;int calls=0;var jobs=TargetSolving.Plan(frames);var events=new List<IdentificationProgress>();TargetSolving.Solve(jobs,(frame,token,progress)=>{calls++;Check(!frame.Rejected,"Rejected representative selected");progress("Solving star field");return PleiadesSolution();},ct,p=>events.Add(p));Check(calls==1&&jobs[0].Include&&jobs[0].Target=="M45"&&events.Last().Completed==1&&events.Any(p=>p.Detail.Contains("3 Light frames")),"Batch count, matching or group progress lost");
    foreach(var frame in frames){var updated=TargetSolving.Apply(jobs[0],frame,"M45");Check(updated.Target=="M45"&&updated.Facts["Target"].Value=="M45"&&frame.Target=="Unknown","Metadata was not grouped or original changed before storing");if(frame!=jobs[0].Representative)Check(updated.Sky.Approximate&&!updated.Sky.HasFootprint,"Group members received exact representative geometry");}
   });
   Test("Nearest major target beats nearer minor catalogue entries and resolves multiple major targets",()=>{
    var result=new SolveResult{RA=10,Dec=0,Radius=1};var objects=new[]{new CatalogObject{Name="PGC1",Type="G",RA=10.001,Dec=0,Magnitude=15},new CatalogObject{Name="M31",Type="G",RA=10.3,Dec=0},new CatalogObject{Name="M33",Type="G",RA=10.2,Dec=0},new CatalogObject{Name="M45",Type="OCl",RA=13,Dec=0}};
    PlateSolve.MatchTargets(result,objects);Check(result.Suggested=="M33"&&result.Candidates[0].Name=="M33"&&result.Candidates.Last().Name=="PGC1","Nearest major preference or field limit wrong");PlateSolve.MatchTargets(result,objects.Take(1));Check(result.Suggested==null&&result.Candidates.Count==1,"Minor-only field assigned automatically");
    Check(PlateSolve.Major(new CatalogObject{Name="NGC1",Type="G",Aliases="C1",Magnitude=13})&&PlateSolve.Major(new CatalogObject{Name="IC1",Type="Neb",Common="Named nebula"})&&!PlateSolve.Major(new CatalogObject{Name="Star",Type="*",Magnitude=1}),"Major catalogue policy wrong");
   });
   Test("Solved image centre rather than WCS reference point determines target matching",()=>{
    var solution=new SolveResult{RA=10,Dec=0,Radius=1,Sky=new SkyGeometry{RA=20,Dec=0}};PlateSolve.MatchTargets(solution,new[]{new CatalogObject{Name="M31",Type="G",RA=20.1,Dec=0},new CatalogObject{Name="M33",Type="G",RA=10.1,Dec=0}});Check(solution.Suggested=="M31"&&solution.RA==20,"Reference pixel mistaken for field centre");
   });
   Test("Major targets outside a solved rectangular footprint are excluded",()=>{
    var result=new SolveResult{RA=10,Dec=0,Radius=1,Sky=new SkyGeometry{RA=10,Dec=0,WidthDegrees=1,HeightDegrees=1,Corners=new List<SkyPoint>{SkyWcs.Inverse(-.5,-.5,10,0),SkyWcs.Inverse(.5,-.5,10,0),SkyWcs.Inverse(.5,.5,10,0),SkyWcs.Inverse(-.5,.5,10,0)}}};PlateSolve.MatchTargets(result,new[]{new CatalogObject{Name="M31",Type="G",RA=10.6,Dec=0},new CatalogObject{Name="M33",Type="G",RA=10.45,Dec=.45}});Check(result.Suggested=="M33"&&result.Candidates.Count==1,"Circumscribed field radius included an off-image target");
   });
   Test("Grouped solve metadata persists only on selected captures without altering image bytes",()=>{
    string source=Path.Combine(root,"group-solve-source");for(int i=0;i<3;i++){var headers=LightHeaders(new DateTime(2026,10,8,21,i,0),"Unknown");Write(Path.Combine(source,"Light_unidentified_"+i+".fit"),64,48,(x,y)=>1800+i,headers);}
    using(var repo=new Repository(Path.Combine(root,"group-solve-repo"))){repo.Import(repo.Scan(source,"Scope","Auto",ct,NoProgress).Frames,ct,NoProgress);var rows=repo.All();foreach(var frame in rows){frame.Session="shared";frame.Target="Unknown";frame.RA=frame.Dec=null;frame.Sky=null;repo.Save(frame);}var selected=rows.Take(2).ToList();var jobs=TargetSolving.Plan(selected);int calls=0;TargetSolving.Solve(jobs,(frame,token,stage)=>{calls++;return PleiadesSolution();},ct,null);foreach(var frame in selected)repo.Refile(TargetSolving.Apply(jobs[0],frame,jobs[0].Target),ct);var saved=repo.All();Check(calls==1&&saved.Count(f=>f.Target=="M45")==2&&saved.Single(f=>f.Hash==rows[2].Hash).Target=="Unknown"&&saved.All(f=>Util.Hash(repo.FilePath(f),ct)==f.Hash),"Group application changed unselected captures or image bytes");
     var config=new Settings{Astap="missing-test-solver.exe"};var representative=saved.First(f=>f.Target=="M45");var cached=PleiadesSolution();cached.Suggested="PGC-minor";repo.CachePut(repo.SolverKey(representative,config),Util.Serialize(cached));Check(repo.CachedSolve(representative,config,ct,null).Suggested=="M45","Old cached match was not reranked");}
   });
   Test("Group target updates preserve each member's existing pointing and acquisition metadata",()=>{
    var a=SolveLight("a");var b=SolveLight("b");b.RA=57;b.Dec=24;b.Sky=new SkyGeometry{RA=57,Dec=24,Evidence="Existing WCS"};b.Exposure=60;b.Observed="2026-10-08T22:00:00Z";var job=TargetSolving.Plan(new[]{a,b}).Single();job.Result=PleiadesSolution();job.Representative=a;
    var changed=TargetSolving.Apply(job,b,"M45");Check(changed.RA==57&&changed.Sky.RA==57&&changed.Sky.Evidence=="Existing WCS"&&changed.Exposure==60&&changed.Observed==b.Observed,"Shared target replaced individual pointing or capture facts");
   });
   Test("Failed stack solves retain metadata and do not prevent later solves",()=>{
    var frames=new[]{SolveLight("bad"),SolveLight("good")};foreach(var f in frames)f.Kind="Stack";var jobs=TargetSolving.Plan(frames);int calls=0;TargetSolving.Solve(jobs,(frame,token,progress)=>{calls++;if(frame.OriginalName=="bad")throw new IOException("No solution");return PleiadesSolution();},ct,null);Check(calls==2&&!jobs[0].Include&&jobs[0].Error=="No solution"&&jobs[1].Include&&frames.All(f=>f.Target=="Unknown"),"Failed batch changed metadata or stopped other stacks");
   });
   Test("Canceling a solve prevents later jobs and never silently applies a partial batch",()=>{
    using(var token=new CancellationTokenSource()){var jobs=TargetSolving.Plan(new[]{SolveLight("one",session:"a"),SolveLight("two",session:"b")});int calls=0;Expect(()=>TargetSolving.Solve(jobs,(frame,cancel,progress)=>{calls++;token.Cancel();cancel.ThrowIfCancellationRequested();return PleiadesSolution();},token.Token,null),"Cancellation ignored");Check(calls==1&&jobs.All(j=>!j.Include)&&jobs.All(j=>j.Frames.All(f=>f.Target=="Unknown")),"Canceled solve ran another job or changed captures");}
   });
   WindowsTest("A canceled or timed-out ASTAP process is stopped promptly",()=>{
    string path=Path.Combine(root,"mock-solver-hang.fit");Write(path,64,48,(x,y)=>1800,new Dictionary<string,string>{{"TESTHANG","T"}});string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MockAstap.exe");
    foreach(bool cancel in new[]{false,true})using(var token=new CancellationTokenSource())using(var process=Process.Start(new ProcessStartInfo(executable,"-f "+SirilHandoff.QuoteArgument(path)+" -o "+SirilHandoff.QuoteArgument(path+".solution")){UseShellExecute=false,CreateNoWindow=true})){if(cancel)token.CancelAfter(250);var clock=Stopwatch.StartNew();Expect(()=>PlateSolve.WaitForSolver(process,token.Token,TimeSpan.FromMilliseconds(500),null),"Unresponsive solver returned normally");Check(process.HasExited&&clock.Elapsed.TotalSeconds<5,"Solver cancellation/timeout hung or left the process alive");}
   });
  }
 }
}
