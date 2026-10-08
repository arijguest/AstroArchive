using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace AstroArchive {
 public partial class Tests {
  static void SearchWorkTests(){
   Test("Latest search bounds queued work and rejects an old result even when cancellation is ignored",()=>{
    using(var worker=new LatestSearch<int>())using(var started=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
     int executions=0;var old=worker.Submit(token=>{Interlocked.Increment(ref executions);started.Set();if(!release.Wait(5000))throw new Exception("Barrier timed out");return -1;});Check(started.Wait(5000),"Worker did not start");
     var requests=new List<Task<int>>();try{for(int i=0;i<100;i++){int value=i;requests.Add(worker.Submit(token=>{Interlocked.Increment(ref executions);return value;}));}}finally{release.Set();}
     Check(requests.Last().Wait(5000)&&requests.Last().Result==99,"Latest result not returned");Check(old.IsCanceled&&requests.Take(99).All(t=>t.IsCanceled)&&executions==2,"Backlog or stale completion published");
    }
   });
   Test("Search cancellation and disposal complete pending tasks without executing them",()=>{
    var worker=new LatestSearch<int>();using(var started=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
     var active=worker.Submit(token=>{started.Set();if(!release.Wait(5000))throw new Exception("Barrier timed out");token.ThrowIfCancellationRequested();return 1;});Check(started.Wait(5000),"Worker did not start");var queued=worker.Submit(token=>2);worker.Dispose();release.Set();
     Check(queued.IsCanceled,"Disposed pending work remained queued");try{active.Wait(5000);}catch(AggregateException){}Check(active.IsCanceled,"Disposed active result published");Expect(()=>worker.Submit(token=>3),"Disposed worker accepted a request");
    }
   });
   Test("A failed search does not prevent subsequent requests from completing",()=>{
    using(var worker=new LatestSearch<int>()){var failed=worker.Submit(token=>{throw new InvalidOperationException("Fixture failure");});try{failed.Wait(5000);}catch(AggregateException){}Check(failed.IsFaulted,"Failure lost");var next=worker.Submit(token=>42);Check(next.Wait(5000)&&next.Result==42,"Worker did not recover");}
   });
   Test("Prepared search reuses documents across query changes with identical query/filter semantics",()=>{
    var rows=new[]{new Frame{Target="C27",Kind="Light",OriginalName="Light_NGC6888.fit",Filter="Dual band",Exposure=60,Gain=80,Notes="José Wide_field"},new Frame{Target="M45",Kind="Stack",OriginalName="Stack_M45.fits",Exposure=10,Status="Failed"},new Frame{Target="M45",Kind="Light",OriginalName="Light_M45.fit",Exposure=20}};
    int prepared=0;var index=new SearchIndex<Frame>(f=>{prepared++;return SearchDocument.FromFrame(f);});var filters=new CaptureFilters();
    foreach(string query in new[]{"Crescent Nebula filter:dual","target:M45 -type:Stack","file:*.fits OR exposure:>=20","jose \"wide field\"","\"unfinished","","mount:EQ?","gain:80","C27 OR M45"})Check(index.Find(rows,FileSearch.Parse(query),filters.Matches,ct).SequenceEqual(filters.Apply(rows,query)),"Prepared search differs: "+query);
    Check(prepared==rows.Length&&index.PreparedCount==rows.Length,"Metadata reconstructed for every query");filters.Values["Frame type"]="Light";Check(index.Find(rows,FileSearch.Parse("C27 OR M45"),filters.Matches,ct).Count==2,"Alternative query bypassed filters");
   });
   Test("Replacing a search index refreshes edited metadata without retaining old documents",()=>{
    var row=new Frame{Filter="Broadband",Exposure=10,Mount="Unknown"};var index=new SearchIndex<Frame>(SearchDocument.FromFrame);Check(index.Find(new[]{row},FileSearch.Parse("filter:broadband"),f=>true,ct).Count==1,"Initial field missing");row.Filter="Ha";row.Exposure=60;
    index=new SearchIndex<Frame>(SearchDocument.FromFrame);Check(index.Find(new[]{row},FileSearch.Parse("filter:Ha mount:EQ? exposure:>20"),f=>true,ct).Count==1,"Metadata change retained stale fields");
   });
   Test("Invalid and empty searches avoid unnecessary document preparation",()=>{
    var index=new SearchIndex<Frame>(f=>{throw new Exception("Should not prepare a document");});var rows=new[]{new Frame(),new Frame()};Check(index.Find(rows,FileSearch.Parse("\"unfinished"),f=>true,ct).Count==0,"Invalid query broadened results");Check(index.Find(rows,FileSearch.Parse(""),f=>true,ct).Count==2,"Empty search lost rows");
    using(var canceled=new CancellationTokenSource()){canceled.Cancel();Expect(()=>index.Find(rows,FileSearch.Parse(""),f=>true,canceled.Token),"Canceled scan still ran");}
   });
   Test("Search filters are immutable snapshots including numeric range state",()=>{
    var filters=new CaptureFilters();filters.Values["Frame type"]="Light";filters.Ranges["Exposure"]=new CaptureRange{Mode=NumericFilterMode.Between,Minimum=20,Maximum=60};var saved=filters.Snapshot();filters.Values["Frame type"]="Stack";filters.Ranges["Exposure"].Minimum=45;filters.Reset();
    Check(saved.Matches(new Frame{Kind="Light",Exposure=30})&&!saved.Matches(new Frame{Kind="Stack",Exposure=30})&&!saved.Matches(new Frame{Kind="Light",Exposure=10}),"UI changes altered in-flight criteria");
   });
   Test("Background sorting preserves numeric nulls stable ties multi-column priority and nested fields",()=>{
    var rows=new[]{new Frame{Kind="Light",Exposure=10,OriginalName="a"},new Frame{Kind="Light",Exposure=2,OriginalName="b"},new Frame{Kind="Bias",Exposure=null,OriginalName="c"},new Frame{Kind="Light",Exposure=10,OriginalName="d"}};
    var sorts=new[]{new SearchSort{Property="Kind"},new SearchSort{Property="Exposure",Descending=true}};Check(SearchOrdering.Order(rows,sorts,CultureInfo.InvariantCulture,ct).Select(f=>f.OriginalName).SequenceEqual(new[]{"c","a","d","b"}),"Sort priority/direction/ties lost");
    Check(SearchOrdering.Order(rows,sorts,null,ct).Select(f=>f.OriginalName).SequenceEqual(new[]{"c","a","d","b"}),"Unset table culture prevented startup sorting");
    var images=new[]{new EditedImage{Metadata=new EditedMetadata{TotalExposure=100}},new EditedImage{Metadata=new EditedMetadata{TotalExposure=2}}};Check(SearchOrdering.Order(images,new[]{new SearchSort{Property="Metadata.TotalExposure"}},CultureInfo.InvariantCulture,ct).First()==images[1],"Nested scientific values sorted as strings");
   });
   Test("Large prepared searches reuse all documents and summary accounting remains exact",()=>{
    var rows=Enumerable.Range(0,12000).Select(i=>new Frame{Target=i%2==0?"M45":"M31",Kind="Light",Status="New",Exposure=i%120,OriginalName="Light_"+i+".fit",Rejected=i%11==0,Screened=true}).ToArray();int count=0;var index=new SearchIndex<Frame>(f=>{count++;return SearchDocument.FromFrame(f);});var filters=new CaptureFilters();
    foreach(string text in new[]{"type:Light","target:M45 exposure:>=60","file:*1.fit"}){var query=FileSearch.Parse(text);var result=index.Find(rows,query,filters.Matches,ct);Check(result.Count==rows.Count(query.Matches),"Large query lost rows");}
    Check(count==rows.Length,"Large repeated query rebuilt documents");var summary=ImportWorkflow.Summarize(rows,rows,true);Check(summary.Total==12000&&summary.Ready==rows.Count(f=>!f.Rejected)&&summary.SkippedFlagged==rows.Count(f=>f.Rejected),"Large summary membership/counts wrong");
   });
  }
 }
}
