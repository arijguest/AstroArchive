using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace AstroArchive {
 public partial class Tests {
  static void MetadataBatchTests(){
   Test("Metadata moves wait for a short preview read without losing changes or bytes",()=>{
    string source=Path.Combine(root,"metadata-preview-lock-source"),path=Path.Combine(source,"Light_M45.fit");Write(path,64,48,(x,y)=>1900+x,LightHeaders(new DateTime(2026,10,6,21,0,0),"M45"));
    using(var repo=new Repository(Path.Combine(root,"metadata-preview-lock-repo"))){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var frame=repo.All().Single();var updated=frame.Clone();updated.Target="M33";Task work;
     using(var read=new FileStream(repo.FilePath(frame),FileMode.Open,FileAccess.Read,FileShare.Read)){work=Task.Run(()=>repo.Refile(updated,ct));Thread.Sleep(180);Check(!work.IsFaulted,"Metadata move failed instead of waiting for the preview reader");}
     Check(work.Wait(10000),"Metadata move did not resume after preview read");Check(repo.Find(frame.Hash).Target=="M33"&&Util.Hash(repo.FilePath(updated),ct)==frame.Hash&&File.Exists(path),"Preview overlap lost metadata or image bytes");}
   });
   Test("Metadata mutations refuse active quality claims while pending source edits remain available",()=>{
    using(var repo=new Repository(Path.Combine(root,"metadata-background-repo"))){var frame=EditableFixture();frame.Hash="metadata-claimed";frame.RelativePath="capture.fit";repo.Save(frame);string refusal;var job=repo.Background.Begin(BackgroundJob.Quality,"Measuring image quality",new[]{frame.Hash},out refusal);
     try{Check(job!=null,"Could not create quality claim");var result=new MetadataBatchResult();Expect(()=>repo.EditMetadata(new[]{frame},new MetadataPatch(new Dictionary<string,string>{{"Notes","Conflicting"}}),false,result,ct,NoProgress),"Metadata wrote through a quality process claim");Check(repo.Find(frame.Hash).Notes==frame.Notes,"Blocked mutation changed archive metadata");var pending=frame.Clone();pending.Status="New";repo.EditMetadata(new[]{pending},new MetadataPatch(new Dictionary<string,string>{{"Notes","Pending"}}),true,result,ct,NoProgress);Check(result.Updated.Single().Item2.Notes=="Pending"&&repo.Find(frame.Hash).Notes==frame.Notes,"Pending source edit changed the archived capture");}
     finally{if(job!=null)job.End();}}
   });
   Test("Pending import metadata remains detached and preserves source bytes",()=>{
    string source=Path.Combine(root,"metadata-pending-source"),path=Path.Combine(source,"Light_M45.fit");Write(path,64,48,(x,y)=>1500+x,LightHeaders(new DateTime(2026,10,6,21,0,0),"M45"));string hash=Util.Hash(path,ct);
    using(var repo=new Repository(Path.Combine(root,"metadata-pending-repo"))){var row=repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames.Single();var result=new MetadataBatchResult();repo.EditMetadata(new[]{row},new MetadataPatch(new Dictionary<string,string>{{"Target","M33"}}),true,result,ct,NoProgress);
     Check(result.Errors.Count==0&&result.Updated.Count==1&&row.Target=="M45"&&result.Updated[0].Item2.Target=="M33","Pending edit changed live source row or lost replacement");Check(repo.All().Count==0&&Util.Hash(path,ct)==hash,"Pending edit imported or changed source bytes");repo.Import(new[]{result.Updated[0].Item2},ct,NoProgress);Check(repo.All().Single().Target=="M33","Import lost edited metadata");}
   });
   Test("Archived metadata resolves a stale import path and retains newer unrelated changes",()=>{
    string source=Path.Combine(root,"metadata-stale-source"),path=Path.Combine(source,"Light_M45.fit");Write(path,64,48,(x,y)=>1700+x,LightHeaders(new DateTime(2026,10,6,21,0,0),"M45"));
    using(var repo=new Repository(Path.Combine(root,"metadata-stale-repo"))){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var stale=repo.All().Single();stale.SourcePath=path;stale.Status="Duplicate (cached)";var current=repo.Find(stale.Hash);current.Target="M33";current.Notes="Newer archive note";repo.Refile(current,ct);var result=new MetadataBatchResult();repo.EditMetadata(new[]{stale},new MetadataPatch(new Dictionary<string,string>{{"Filter","Ha"}}),true,result,ct,NoProgress);var saved=repo.Find(stale.Hash);
     Check(result.Errors.Count==0&&saved.Target=="M33"&&saved.Notes=="Newer archive note"&&saved.Filter=="Ha"&&File.Exists(repo.FilePath(saved)),"Stale import overwrote newer metadata or used an old path");Check(File.Exists(path)&&result.Updated.Single().Item2.SourcePath==path,"Source provenance or source file lost");}
   });
   Test("Canceled metadata batch checkpoints completed edits and preserves later files",()=>{
    string source=Path.Combine(root,"metadata-cancel-source");for(int i=0;i<2;i++)Write(Path.Combine(source,"Light_"+i+".fit"),64,48,(x,y)=>1800+i+x,LightHeaders(new DateTime(2026,10,6,21,i,0),"M45"));
    using(var repo=new Repository(Path.Combine(root,"metadata-cancel-repo"))){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var rows=repo.All();var result=new MetadataBatchResult();using(var stop=new CancellationTokenSource()){Expect(()=>repo.EditMetadata(rows,new MetadataPatch(new Dictionary<string,string>{{"Notes","Committed"}}),false,result,stop.Token,p=>{if(p.Done==1)stop.Cancel();}),"Metadata cancellation ignored");}
     Check(result.Updated.Count==1&&repo.All().Count(f=>f.Notes=="Committed")==1,"Completed edits lost or later edit ran after cancellation");Check(repo.All().All(f=>File.Exists(repo.FilePath(f)))&&Directory.GetFiles(source).Length==2,"Canceled metadata lost a file");using(var snapshot=new Database(Path.Combine(repo.Meta,"index.sqlite")))Check(snapshot.Query("SELECT data FROM files").Select(Util.Deserialize<Frame>).Count(f=>f.Notes=="Committed")==1,"Partial metadata completion was not checkpointed");}
   });
   Test("Multiple source aliases update one archived capture without stale paths or provenance changes",()=>{
    string source=Path.Combine(root,"metadata-alias-source"),path=Path.Combine(source,"Light_M45.fit");Write(path,64,48,(x,y)=>2100+x,LightHeaders(new DateTime(2026,10,6,21,0,0),"M45"));
    using(var repo=new Repository(Path.Combine(root,"metadata-alias-repo"))){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var actual=repo.All().Single();string archivedStatus=actual.Status;var a=actual.Clone();a.Status="Duplicate";a.SourcePath=Path.Combine(source,"alias-a.fit");var b=a.Clone();b.SourcePath=Path.Combine(source,"alias-b.fit");var result=new MetadataBatchResult();repo.EditMetadata(new[]{a,b},new MetadataPatch(new Dictionary<string,string>{{"Target","M33"}}),true,result,ct,NoProgress);var saved=repo.Find(actual.Hash);
     Check(result.Errors.Count==0&&result.Updated.Count==2&&saved.Target=="M33"&&File.Exists(repo.FilePath(saved)),"Multiple aliases failed to follow the moved capture");Check(saved.SourcePath==actual.SourcePath&&saved.Status==archivedStatus&&result.Updated[0].Item2.SourcePath==a.SourcePath&&result.Updated[1].Item2.SourcePath==b.SourcePath,"Import display provenance overwrote archive origin");}
   });
   Test("Path-changing metadata rejects modified bytes and canceled edits before saving",()=>{
    string source=Path.Combine(root,"metadata-integrity-source"),path=Path.Combine(source,"Light_M45.fit");Write(path,64,48,(x,y)=>2300+x,LightHeaders(new DateTime(2026,10,6,21,0,0),"M45"));
    using(var repo=new Repository(Path.Combine(root,"metadata-integrity-repo"))){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var original=repo.All().Single();string file=repo.FilePath(original);using(var stream=new FileStream(file,FileMode.Open,FileAccess.Write)){stream.Position=2880;stream.WriteByte(71);}var edit=original.Clone();edit.Target="M33";Expect(()=>repo.Refile(edit,ct),"Changed bytes accepted for a metadata move");Check(repo.Find(original.Hash).Target=="M45"&&File.Exists(file),"Rejected metadata changed index or removed original");
     using(var stop=new CancellationTokenSource()){stop.Cancel();edit=original.Clone();edit.Notes="Canceled";Expect(()=>repo.Refile(edit,stop.Token),"Canceled metadata save accepted");}Check(repo.Find(original.Hash).Notes!="Canceled","Canceled same-path metadata was persisted");}
   });
  }
 }
}
