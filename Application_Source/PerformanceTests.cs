// Generated fixtures and an injected clock exercise timing without sleeping.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroArchive {
 public partial class Tests {
  // JSON object member order is not part of the snapshot contract. In particular,
  // .NET Framework reflection caches can enumerate properties in a different order.
  static bool SameSnapshotValue(object left,object right){
   var a=left as IDictionary<string,object>;var b=right as IDictionary<string,object>;
   if(a!=null||b!=null)return a!=null&&b!=null&&a.Count==b.Count&&a.All(p=>b.ContainsKey(p.Key)&&SameSnapshotValue(p.Value,b[p.Key]));
   var x=left as System.Collections.IList;var y=right as System.Collections.IList;
   if(x!=null||y!=null)return x!=null&&y!=null&&x.Count==y.Count&&Enumerable.Range(0,x.Count).All(i=>SameSnapshotValue(x[i],y[i]));
   return object.Equals(left,right);
  }
  static void PerformanceTests(){
   Test("ETA advances within the first copy and includes destination verification",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(1,100,"Import",true);var item=metrics.Track(100);
    item.Copied(10);seconds=2;var p=metrics.Progress();Check(p.Done==0&&p.BytesDone==10&&p.RemainingSeconds.HasValue,"First-file progress missing");Check(Math.Abs(p.RemainingSeconds.Value-38)<0.01,"Verification absent from remaining work");
    item.Copied(90);seconds=3;p=metrics.Progress();Check(p.ProgressFraction<1&&!p.Finished&&p.RemainingSeconds>0,"Copy completion concealed verification");item.Verified(100);item.Resolve(true);metrics.Finalise("Index");Check(metrics.Progress().Finalising&&!metrics.Progress().RemainingSeconds.HasValue,"Premature job completion");metrics.Finish("Complete");Check(metrics.Progress().RemainingSeconds==0&&metrics.Progress().ProgressFraction==1,"Terminal completion missing");
   });
   Test("ETA learns different copy and verification costs",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(2,200,"Copy",true);var first=metrics.Track(100);first.BeginCopy();first.Copied(100);seconds=10;first.EndCopy();first.BeginVerification();first.Verified(100);seconds=12;first.EndVerification();first.Resolve(true);
    Check(Math.Abs(metrics.VerificationWeight-0.2)<0.001,"Verification speed was treated as source-copy speed");var second=metrics.Track(100);second.BeginCopy();second.Copied(20);seconds=14;var p=metrics.Progress();Check(Math.Abs(p.RemainingSeconds.Value-10)<0.01&&!p.EtaProvisional,"Measured costs did not correct remaining time");
   });
   Test("A compatible previous timing profile seeds a provisional first-file ETA",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(1,100,"Copy",true);metrics.SeedVerificationWeight(0.2);var item=metrics.Track(100);item.Copied(10);seconds=2;var p=metrics.Progress();Check(p.EtaProvisional&&Math.Abs(p.RemainingSeconds.Value-22)<0.01,"Previous measured costs were ignored");
   });
   Test("Optional original cleanup remains in the transfer work budget",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(1,100,"Copy",true,true,true);var item=metrics.Track(100);item.Copied(100);item.Verified(100);seconds=2;var p=metrics.Progress();Check(p.RemainingSeconds.HasValue&&p.ProgressFraction==0.5,"Cleanup hashes disappeared from remaining work");item.Cleanup(100);seconds=3;Check(metrics.Progress().ProgressFraction==0.75,"Cleanup hash reads did not advance progress");item.Resolve(true);metrics.Finalise("Complete index");Check(!metrics.Progress().Finished,"Cleanup completion skipped finalisation");
   });
   Test("Scan totals remain provisional until discovery finishes",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(0,0,"Scan",false,false);metrics.Discover(1000000);metrics.Discover(10);metrics.Discover(20);metrics.Complete(1000000);seconds=2;
    Check(!metrics.Progress().TotalKnown&&!metrics.Progress().RemainingSeconds.HasValue,"Partial inventory presented as final");metrics.InventoryComplete();Check(Math.Abs(metrics.Progress().RemainingSeconds.Value-4)<0.01,"Scan weighted by image bytes instead of inspected files");
   });
   Test("Starting another file preserves recent throughput samples",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(2,200,"Copy",true);var first=metrics.Track(100);first.Copied(100);first.Verified(100);first.Resolve(true);seconds=2;Check(metrics.Progress().RemainingSeconds.HasValue,"First sample missing");var second=metrics.Track(100);second.Reset();Check(metrics.Progress().RemainingSeconds.HasValue,"New attempt erased established throughput");
   });
   Test("Retries and failed files do not count repeated bytes as completed output",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(2,200,"Copy",true);var item=metrics.Track(100);item.Copied(80);seconds=2;metrics.Progress();item.Reset();Check(metrics.Progress().BytesDone==0,"Retry retained partial bytes");item.Copied(10);item.Resolve(false);Check(metrics.Progress().BytesDone==0&&metrics.Progress().Done==1,"Failure inflated completed bytes");var good=metrics.Track(100);seconds=3;good.Copied(20);Check(metrics.Progress().ProgressFraction<=0.11,"Discarded attempt counted in progress");
   });
   Test("ETA stops claiming progress during a stall and recovers on new bytes",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(1,100,"Copy",true);var item=metrics.Track(100);item.Copied(10);seconds=2;metrics.Progress();seconds=5;var p=metrics.Progress();Check(p.Stalled&&!p.RemainingSeconds.HasValue,"Stall retained an authoritative ETA");item.Copied(10);Check(!metrics.Progress().Stalled&&metrics.Progress().RemainingSeconds.HasValue,"Progress did not resume estimate");
   });
   Test("Old transfer callbacks cannot alter the next phase",()=>{
    double seconds=0;var metrics=new PipelineMetrics(NoProgress,()=>seconds);metrics.Phase(1,100,"Copy",true);var old=metrics.Track(100);metrics.Phase(2,0,"Analysis");old.Copied(100);old.Verified(100);old.Resolve(true);Check(metrics.Progress().Done==0&&metrics.Progress().BytesDone==0,"Previous phase leaked into new operation");Check(PipelineMetrics.Duration(49*3600+61)=="49:01:01","Long timer wrapped at 24 hours");
   });
   Test("Stage notifications are coalesced without building snapshots",()=>{
    double seconds=0;int pulses=0;var metrics=new PipelineMetrics(p=>{pulses++;Check(p.LiveMetrics!=null&&p.Stages==null,"Hot-path notification built a full snapshot");},()=>seconds);metrics.Phase(1,100,"Copy",true);
    for(int i=0;i<1000;i++)using(var scope=metrics.Begin("Copy + source hash","Fixture")){scope.Bytes(1);seconds+=0.0001;}Check(pulses<=2,"Stage starts flooded progress callbacks");Check(metrics.Snapshot().Single(s=>s.Stage=="Copy + source hash").Bytes==1000,"Coalescing lost counters");
   });
   Test("Fast frame snapshots preserve metadata and isolate mutable stamps",()=>{
    var frame=new Frame{Hash="abc",Target="M33",Telescope="Unit",Model="Dwarf 3",Camera="Telephoto",Kind="Light",Night="2026-10-06",Calibration="Unknown",SourceStamp=new FileStamp{Identity="id",Size=123},SourceMetadataStamp=new FileStamp{Size=30},RepositoryStamp=new FileStamp{Size=123}};var copy=frame.Clone();string original=Util.Serialize(frame),snapshot=Util.Serialize(copy);Check(SameSnapshotValue(Util.Json().DeserializeObject(snapshot),Util.Json().DeserializeObject(original)),"Snapshot lost frame fields. Original: "+original+"; snapshot: "+snapshot);copy.SourceStamp.Size++;copy.RepositoryStamp.Size++;copy.SourceMetadataStamp.Size++;Check(frame.SourceStamp.Size==123&&frame.RepositoryStamp.Size==123&&frame.SourceMetadataStamp.Size==30,"Snapshot shares mutable file stamps");
   });
   Test("Indexed filename phrases retain compact names and conflicting targets",()=>{
    Check(Catalog.TargetFromFilename("Light_HeartNebula.fit")=="IC1805","Compact common name lost");Check(Catalog.TargetFromFilename("Light_NorthAmerica_Nebula.fit")!=null,"Partially joined phrase lost");Check(Catalog.TargetFromFilename("Light_Heart_Nebula_M33.fit")==null&&Catalog.HasFilenameConflict("Light_Heart_Nebula_M33.fit"),"Conflicting name hidden");
   });
   Test("Header cache requires trusted unchanged identity and change metadata",()=>{
    string directory=Path.Combine(root,"header-cache");Directory.CreateDirectory(directory);string source=Path.Combine(root,"header-cache-source"),name=Path.Combine(source,"file.fit");var cache=new MetadataHeaderCache(directory,source);var stamp=new FileStamp{Identity="id",Size=100,Modified=2,Created=1,Changed=3,Reliable=true};cache.Put(name,stamp,new FitsHeader{Width=64,Height=48,Values=new Dictionary<string,string>{{"OBJECT","M33"}}});cache.Flush();cache=new MetadataHeaderCache(directory,source);Check(cache.Get(name,stamp).Get("OBJECT")=="M33","Persisted header lost");var changed=stamp.Clone();changed.Changed++;Check(cache.Get(name,changed)==null,"Changed source reused header");changed=stamp.Clone();changed.Reliable=false;Check(cache.Get(name,changed)==null,"Untrusted filesystem reused header");changed=stamp.Clone();changed.Attributes=0x400000;Check(cache.Get(name,changed)==null,"Cloud placeholder reused header");
   });
   Test("Discovery overflow preserves every file and shared JSON is read once",()=>{
    string source=Path.Combine(root,"overflow-source"),template=Path.Combine(root,"overflow-template.fit");Directory.CreateDirectory(source);Write(template,16,16,(x,y)=>1200,new Dictionary<string,string>());for(int i=0;i<300;i++)File.Copy(template,Path.Combine(source,"raw_"+i+".fit"));string json="{\"targetName\":\"M33\",\"cameraId\":0}";File.WriteAllText(Path.Combine(source,"shotsInfo.json"),json);
    using(var repo=new Repository(Path.Combine(root,"overflow-repo"))){bool earlyTotal=false;var plan=repo.Scan(source,"Unit","Auto",ct,p=>{if(p.LiveMetrics!=null){var live=p.LiveMetrics.Progress(false);if(live.TotalKnown&&live.Done<live.Total)earlyTotal=true;}},false,null,true,false,4);Check(plan.Frames.Count==300&&plan.Frames.Select(f=>f.SourcePath).Distinct().Count()==300&&plan.Errors.Count==0,"Spill lost or repeated files");Check(earlyTotal,"Discovery waited for metadata");Check(plan.Metrics.Snapshot().Single(s=>s.Stage=="Metadata").Bytes==300*2880+System.Text.Encoding.UTF8.GetByteCount(json),"Workers reread shared JSON");Check(Directory.GetFiles(Path.GetDirectoryName(repo.WorkingIndex),"scan-*.pending").Length==0,"Spill file left behind");}
   });
   Test("Project copies use continuous copy and verification metrics",()=>{
    string source=Path.Combine(root,"progress-export-source");Directory.CreateDirectory(source);Write(Path.Combine(source,"Light_M33.fit"),64,48,(x,y)=>1200,new Dictionary<string,string>());using(var repo=new Repository(Path.Combine(root,"progress-export-repo"))){var scan=repo.Scan(source,"Unit","Auto",ct,NoProgress,false,null,true);repo.Import(scan.Frames,ct,NoProgress);PipelineMetrics observed=null;string project=Exporter.Create(repo,repo.All(),new ExportOptions{Parent=root,Name="progress-export",Mode="Files"},ct,p=>{if(p.LiveMetrics!=null)observed=p.LiveMetrics;});Check(observed!=null&&observed.Progress().Finished&&observed.Progress().Done==1,"Export lacks shared progress");var stages=observed.Snapshot();long size=repo.All().Single().Bytes;Check(stages.Single(s=>s.Stage=="Copy + source hash").Bytes==size&&stages.Single(s=>s.Stage=="Verification").Bytes==size,"Export read/write bytes missing");Check(!File.Exists(Path.Combine(project,"INCOMPLETE.txt")),"Finished export marked incomplete");}
   });
   Test("Canceled scans clean overflow files and retain source captures",()=>{
    string source=Path.Combine(root,"overflow-source");using(var repo=new Repository(Path.Combine(root,"canceled-scan-repo")))using(var cancel=new System.Threading.CancellationTokenSource()){Expect(()=>repo.Scan(source,"Unit","Auto",cancel.Token,p=>{if(p.LiveMetrics!=null&&p.LiveMetrics.Progress(false).Total>10)cancel.Cancel();},false,null,true),"Scan ignored cancellation");Check(Directory.GetFiles(source,"*.fit").Length==300&&Directory.GetFiles(Path.GetDirectoryName(repo.WorkingIndex),"scan-*.pending").Length==0,"Canceled discovery changed sources or leaked spill");}
   });
   WindowsTest("Unimported header cache observes session changes and model overrides",()=>{
    string source=Path.Combine(root,"rescan-source"),nested=Path.Combine(source,"nested");Directory.CreateDirectory(nested);string path=Path.Combine(nested,"raw_0001.fit");Write(path,64,48,(x,y)=>1200,new Dictionary<string,string>());File.WriteAllText(Path.Combine(source,"shotsInfo.json"),"{\"targetName\":\"M33\",\"cameraId\":0}");using(var repo=new Repository(Path.Combine(root,"rescan-repo"))){repo.Scan(source,"Unit","Auto",ct,NoProgress,false,null,true);File.WriteAllText(Path.Combine(nested,"shotsInfo.json"),"{\"targetName\":\"M45\",\"cameraId\":1}");var scan=repo.Scan(source,"Unit","Dwarf 3",ct,NoProgress,false,null,true);Check(scan.MetadataCacheHits==1&&scan.Frames.Single().Target=="M45"&&scan.Frames.Single().Camera=="Wide"&&scan.Frames.Single().Model=="Dwarf 3","Header cache retained old session/override");DateTime modified=File.GetLastWriteTimeUtc(path);/* Distinct filesystem clock tick: a same-tick rewrite cannot exercise change-time invalidation. */System.Threading.Thread.Sleep(40);Write(path,64,48,(x,y)=>2400,new Dictionary<string,string>());File.SetLastWriteTimeUtc(path,modified);scan=repo.Scan(source,"Unit","Auto",ct,NoProgress,false,null,true);Check(scan.MetadataCacheHits==0,"Restored mtime concealed source change");}
   });
  }
 }
}
