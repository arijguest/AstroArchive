using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void WorkflowRegressions(){
   Test("Canonical target IDs unify old labels, catalogue aliases and combined names",()=>{
    var rows=new[]{"Heart Nebula","IC 1805","IC1805 · Heart Nebula"}.Select(s=>Util.Deserialize<Frame>("{\"Target\":\""+s+"\"}")).ToList();
    Check(rows.Select(f=>f.Target).Distinct().Single()=="IC1805"&&rows.All(f=>f.CommonName=="Heart Nebula"&&f.ObjectId=="IC1805"),"Heart categories did not merge");
    foreach(string name in new[]{"M 051","NGC5194","Whirlpool Galaxy","M51 (Whirlpool Galaxy)"})Check(new Frame{Target=name}.TargetLabel=="M51 · Whirlpool Galaxy","Whirlpool ID/name mismatch: "+name);
    Check(new Frame{Target="My custom field"}.Target=="My custom field"&&new Frame{Target="Calibration"}.Target=="Calibration","Custom or calibration label changed");
    Check(new Frame{Target="M31 and M33"}.Target=="M31 and M33","Conflicting targets were merged");
   });
   Test("Name-only and ID-only imports share target folders and export groups",()=>{
    string source=Path.Combine(root,"canonical-source");Directory.CreateDirectory(source);
    foreach(var pair in new[]{Tuple.Create("name.fit","Heart Nebula",1000),Tuple.Create("id.fit","IC1805",2000)})Write(Path.Combine(source,pair.Item1),64,48,(x,y)=>pair.Item3+x,LightHeaders(new DateTime(2026,10,6,21,0,0),pair.Item2));
    using(var repo=new Repository(Path.Combine(root,"canonical-repo"))){var plan=repo.Scan(source,"Unit-01","Auto",ct,NoProgress);Check(plan.Frames.All(f=>f.Target=="IC1805")&&plan.Frames.Select(f=>f.Session).Distinct().Count()==1,"Source labels split the session");repo.Import(plan.Frames,ct,NoProgress);
     var frames=repo.All();Check(frames.All(f=>f.RelativePath.StartsWith(Path.Combine("Targets","IC1805"))),"Target folders split by common name");
     using(var db=new Database(repo.WorkingIndex)){var f=frames.First();db.Exec("UPDATE files SET data=? WHERE hash=?",Util.Serialize(f).Replace("\"Target\":\"IC1805\"","\"Target\":\"Heart Nebula\""),f.Hash);}
     frames=repo.All();Check(frames.Select(f=>f.Target).Distinct().Count()==1&&frames.All(f=>File.Exists(repo.FilePath(f))),"Legacy categories or file paths changed incorrectly");
     string dest=Exporter.Create(repo,frames,new ExportOptions{CreateNewFolder=true,AddMetadata=true,Parent=root,Name="canonical-export",Mode="Subs",IncludeCalibration=false},ct,NoProgress);Check(Directory.GetDirectories(Path.Combine(dest,"subs")).Length==1&&!Directory.Exists(Path.Combine(dest,"targets")),"Alias labels split export inputs");
    }
   });
   Test("Import summary counts the actual filtered and unflagged payload",()=>{
    var rows=new[]{new Frame{Status="New",Bytes=100},new Frame{Status="New",Bytes=200,Rejected=true},new Frame{Status="Duplicate (cached)"},new Frame{Status="Deleted"},new Frame{Status="Unreadable",IntegrityIssue="cut payload"},new Frame{Status="Failed",Bytes=300,TransferIssue="locked"},new Frame{Status="Restore",Bytes=400,Screened=true}};
    var summary=ImportWorkflow.Summarize(rows,rows,true);Check(summary.Ready==2&&summary.ReadyBytes==500&&summary.Duplicates==1&&summary.Deleted==1&&summary.Rejected==1&&summary.Unreadable==1&&summary.TransferFailed==1,"Summary counts differ from import selection");
    summary=ImportWorkflow.Summarize(rows,rows.Take(2),true);Check(summary.Total==7&&summary.Shown==2&&summary.Ready==1&&summary.ReadyBytes==100&&summary.SkippedFlagged==1,"Hidden candidates included in total size");
    Check(ImportWorkflow.Select(rows,false).Count==4&&ImportWorkflow.Select(rows,true,true).Single()==rows[5],"Retry or flagged opt-in scope differs");
   });
   Test("Review distinguishes rejection, integrity and transfer problems including overlaps",()=>{
    var rejected=new Frame{Status="New",Rejected=true,RejectionReason="CAPSTAT: FAILED"};var broken=new Frame{Status="Unreadable",IntegrityIssue="truncated FITS"};var transfer=new Frame{Status="Failed",TransferIssue="access denied"};
    Check(rejected.ReviewCategory=="Telescope rejected / reference"&&rejected.ReviewReason.Contains("CAPSTAT")&&!CaptureScreening.FileProblem(rejected),"Telescope rejection confused with unreadable file");
    Check(broken.ReviewCategory=="File integrity problem"&&transfer.ReviewCategory=="Transfer failure","File and transfer categories differ");
    broken.Rejected=true;broken.RejectionReason="filename rejected";Check(broken.ReviewReason.Contains("filename rejected")&&broken.ReviewReason.Contains("truncated"),"Overlapping reasons lost");
    var filters=new CaptureFilters();filters.Values["Review type"]="File integrity problem";Check(filters.Apply(new[]{rejected,broken,transfer},"").Single()==broken,"Problem-type filtering failed");
    Check(new Frame{Status="New"}.ReviewText=="Not screened","Unscreened capture represented as a pass");
    Check(new Frame{Status="Missing",ScreeningIssue=""}.ReviewReason.Contains("Missing"),"Previously screened missing file had no review reason");
   });
   Test("Session summaries separate devices and count subs exposure without stacks",()=>{
    var rows=new[]{new Frame{Target="Heart Nebula",Night="2026-10-06",Telescope="A",Camera="Telephoto",Session="same",Kind="Light",Exposure=60},new Frame{Target="IC1805",Night="2026-10-06",Telescope="A",Camera="Telephoto",Session="same",Kind="Light",Exposure=120},new Frame{Target="IC1805",Night="2026-10-06",Telescope="B",Camera="Telephoto",Session="same",Kind="Stack",Exposure=999,StackCount=50},new Frame{Target="IC1805",Night="2026-10-06",Telescope="B",Camera="Telephoto",Session="same",Kind="Light"}};
    var summary=CaptureGroups.Summarize(rows);Check(summary.Captures==4&&summary.Subs==3&&summary.Stacks==1&&summary.Sessions==2&&summary.ExposureSeconds==180&&summary.UnknownExposure==1,"Integration or session count misleading");
    Check(rows.Select(f=>f.Target).Distinct().Count()==1&&rows.Select(f=>f.SessionGroup).Distinct().Count()==2,"Session identity mixed instruments");
   });
   Test("Failed transfer retries use the current plan and leave other candidates untouched",()=>{
    string source=Path.Combine(root,"retry-plan-source");Directory.CreateDirectory(source);for(int i=0;i<2;i++)Write(Path.Combine(source,"capture"+i+".fit"),64,48,(x,y)=>1000+i,LightHeaders(new DateTime(2026,10,6,21,0,0),"M51"));
    using(var repo=new Repository(Path.Combine(root,"retry-plan-repo"))){var plan=repo.Scan(source,"Unit-01","Auto",ct,NoProgress,false,null,true);string staging=Path.Combine(repo.Meta,"staging");File.WriteAllText(staging,"block staging");Check(repo.Import(plan.Frames.Take(1),ct,NoProgress).Failed==1,"Injected transfer did not fail");File.Delete(staging);
     var retry=ImportWorkflow.Select(plan.Frames,true,true);Check(retry.Count==1&&retry[0].ReviewReason.Length>0,"Retry lost diagnostic or selected untouched input");Check(repo.Import(retry,ct,NoProgress).Imported==1&&plan.Frames[1].Status=="New"&&repo.All().Count==1,"Retry imported unrelated candidates");Check(plan.Frames[0].Status=="Imported"&&!CaptureScreening.NeedsReview(plan.Frames[0]),"Successful retry retained failure state");
    }
   });
   Test("Explicit reimport permission persists the deletion log and refreshes a deleted candidate",()=>{
    string source=Path.Combine(root,"allow-source"),destination=Path.Combine(root,"allow-repo"),moved=Path.Combine(root,"allow-portable");Directory.CreateDirectory(source);Write(Path.Combine(source,"capture.fit"),64,48,(x,y)=>2200,LightHeaders(new DateTime(2026,10,6,21,0,0),"Heart Nebula"));string hash;
    using(var repo=new Repository(destination)){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);hash=repo.All().Single().Hash;repo.DeleteFrames(repo.All(),ct,NoProgress);var excluded=repo.Scan(source,"Unit-01","Auto",ct,NoProgress,false,null,true).Frames.Single();Check(excluded.Status=="Deleted","Missing initial exclusion");
     Check(repo.AllowReimport(new[]{hash},ct)==1&&!repo.Deletions().Single().Excluded&&repo.Deletions().Single().AllowedUtc.Length>0,"Permission or audit not recorded");var ready=repo.RecheckAllowedSource(excluded,ct);Check(ready.Status=="New"&&ready.Target=="IC1805","Deleted row not available without full rescan");Check(repo.All().Count==0&&repo.Scan(source,"Unit-01","Auto",ct,NoProgress,false,null,true).Frames.Single().Status=="New","Permission pretended to restore a file or retained exclusion");
    }
    Directory.CreateDirectory(Path.Combine(moved,".astroarchive"));File.Copy(Path.Combine(destination,".astroarchive","index.sqlite"),Path.Combine(moved,".astroarchive","index.sqlite"));using(var repo=new Repository(moved))Check(!repo.Deletions().Single().Excluded&&repo.Scan(source,"Unit-01","Auto",ct,NoProgress,false,null,true).Frames.Single().Status=="New","Portable restore lost reimport permission");
    using(var repo=new Repository(destination)){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);repo.DeleteFrames(repo.All(),ct,NoProgress);Check(repo.Deletions().Single().Excluded&&repo.Deletions().Single().Events.Count==3&&repo.Scan(source,"Unit-01","Auto",ct,NoProgress,false,null,true).Frames.Single().Status=="Deleted","Deleting again did not renew exclusion");}
   });
   Test("Retry retains source-change guards and classifies stale content as an integrity problem",()=>{
    string source=Path.Combine(root,"retry-stale-source");Directory.CreateDirectory(source);string file=Path.Combine(source,"capture.fit");Write(file,64,48,(x,y)=>1000,LightHeaders(new DateTime(2026,10,6,21,0,0),"M51"));
    using(var repo=new Repository(Path.Combine(root,"retry-stale-repo"))){var plan=repo.Scan(source,"Unit-01","Auto",ct,NoProgress);var frame=plan.Frames.Single();frame.Status="Failed";Write(file,64,48,(x,y)=>2000,LightHeaders(new DateTime(2026,10,6,21,0,0),"M51"));
     var retry=ImportWorkflow.Select(plan.Frames,true,true);Check(repo.Import(retry,ct,NoProgress).Failed==1&&repo.All().Count==0,"Changed source was copied from a stale plan");Check(CaptureScreening.FileProblem(frame)&&ImportWorkflow.Select(plan.Frames,true,true).Count==0,"Integrity failure offered as an ordinary transfer retry");
    }
   });
   Test("Cancelled reimport permission does not lift an exclusion",()=>{
    string source=Path.Combine(root,"allow-cancel-source");Directory.CreateDirectory(source);Write(Path.Combine(source,"capture.fit"),64,48,(x,y)=>2300,new Dictionary<string,string>());using(var repo=new Repository(Path.Combine(root,"allow-cancel-repo"))){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);repo.DeleteFrames(repo.All(),ct,NoProgress);using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>repo.AllowReimport(repo.Deletions().Select(d=>d.Hash),cancel.Token),"Cancelled action succeeded");}Check(repo.Deletions().Single().Excluded,"Cancellation changed the deletion log");}
   });
  }
 }
}
