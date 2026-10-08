using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class TargetSolveJob {
  public List<Frame> Frames;public Frame Representative;public SolveResult Result{get;set;}
  public IEnumerable<Candidate> Choices{get{var choices=Result==null||Result.Candidates==null?new List<Candidate>():Result.Candidates.ToList();if(!string.IsNullOrEmpty(Target)&&!choices.Any(c=>c.Name==Target))choices.Add(new Candidate{Name=Target,Common=Catalog.CommonName(Target)});return choices;}}
  public string Error{get;set;}public string Target{get;set;}public bool Include{get;set;}
  public bool Solved{get{return Result!=null;}}
  public string Filename{get{return Representative.OriginalName;}}
  string scope;public string Scope{get{return scope??(scope=Frames.Count+" "+(Representative.Kind=="Light"?"Light frame"+(Frames.Count==1?"":"s"):Representative.Kind)+" · "+CaptureSessions.Describe(Frames).Dates+" · "+Representative.Telescope+" / "+Representative.Camera);}}
  public string Match{get{return Error??(Result==null?"Pending":Result.MatchReason??"Choose a target");}}
  public string Centre{get{return Result==null?"":Result.RA.ToString("0.00000",CultureInfo.InvariantCulture)+"°, "+Result.Dec.ToString("0.00000",CultureInfo.InvariantCulture)+"°";}}
  public string TargetLabel{get{return string.IsNullOrEmpty(Target)?"Choose target…":Catalog.Label(Target);}}
 }
 public sealed class IdentificationProgress {
  public int Completed,Total;public string Stage,Detail;public bool Metadata;
 }
 public static class TargetSolving {
  // Use exactly the same identity as the visible Light subgroup. A crop,
  // header variation or image index does not change the session's target.
  static string GroupKey(Frame frame,int index){return SubframeSessions.Key(frame)??"file:"+index;}
  public static List<TargetSolveJob> Plan(IEnumerable<Frame> selected){
   return selected.Select((f,i)=>new{Frame=f,Key=GroupKey(f,i)}).GroupBy(f=>f.Key).Select(g=>{
    var frames=g.Select(f=>f.Frame).ToList();var candidates=frames.Where(f=>!f.Rejected&&!CaptureScreening.FileProblem(f)).ToList();if(candidates.Count==0)candidates=frames;
    return new TargetSolveJob{Frames=frames,Representative=candidates[candidates.Count/2]};
   }).ToList();
  }
  public static void Solve(List<TargetSolveJob> jobs,Func<Frame,CancellationToken,Action<string>,SolveResult> solver,CancellationToken ct,Action<IdentificationProgress> progress){
   for(int i=0;i<jobs.Count;i++){
    ct.ThrowIfCancellationRequested();var job=jobs[i];int completed=i;Action<string> stage=message=>{if(progress!=null)progress(new IdentificationProgress{Completed=completed,Total=jobs.Count,Stage=message,Detail="Job "+(completed+1)+" of "+jobs.Count+" · "+job.Filename+"\n"+job.Scope});};
    stage("Preparing representative image");try{
     var samples=job.Representative.Kind=="Light"?job.Frames.Where(f=>!f.Rejected&&!CaptureScreening.FileProblem(f)).ToList():new List<Frame>();
     var attempts=new[]{job.Representative,samples.FirstOrDefault(),samples.LastOrDefault()}.Where(f=>f!=null).Distinct().Take(3).ToList();
     for(int attempt=0;attempt<attempts.Count;attempt++){
      ct.ThrowIfCancellationRequested();job.Representative=attempts[attempt];stage("Preparing representative "+(attempt+1)+" of "+attempts.Count);
      try{job.Result=solver(job.Representative,ct,stage);ct.ThrowIfCancellationRequested();PlateSolve.MatchTargets(job.Result);break;}
      catch(OperationCanceledException){throw;}catch(Exception){if(attempt+1==attempts.Count)throw;stage("Representative failed; trying another group frame");}
     }
     job.Target=job.Result.Suggested;job.Include=!string.IsNullOrEmpty(job.Target);job.Error=null;
    }
    catch(OperationCanceledException){throw;}catch(Exception error){job.Error=error.Message;job.Result=null;job.Include=false;}
    if(progress!=null)progress(new IdentificationProgress{Completed=i+1,Total=jobs.Count,Stage=job.Solved?"Field solved": "Solve failed; continuing",Detail=job.Filename+"\n"+job.Scope});
   }
  }
  public static Frame WithPointing(TargetSolveJob job,Frame original){
   if(job.Result==null)throw new InvalidOperationException("This field has not been solved.");
   var updated=original.Clone();if(original==job.Representative){updated.RA=job.Result.RA;updated.Dec=job.Result.Dec;updated.Sky=job.Result.Sky==null?new SkyGeometry{RA=job.Result.RA,Dec=job.Result.Dec,Evidence="Plate solved using "+job.Result.Solver}:job.Result.Sky.Clone();}
   else if(!updated.RA.HasValue||!updated.Dec.HasValue){if(updated.Sky!=null&&SkyWcs.ValidPosition(updated.Sky.RA,updated.Sky.Dec)){updated.RA=updated.Sky.RA;updated.Dec=updated.Sky.Dec;}
    else{updated.RA=job.Result.RA;updated.Dec=job.Result.Dec;updated.Sky=new SkyGeometry{RA=job.Result.RA,Dec=job.Result.Dec,Approximate=true,Evidence="Approximate pointing from same-session solved frame"};}}
   return updated;
  }
  public static Frame Apply(TargetSolveJob job,Frame original,string target){
   target=Catalog.Normalize(target);if(Catalog.IsAmbiguous(target))throw new ArgumentException("Choose a target before applying this match.");
   var updated=WithPointing(job,original);updated.Target=target;updated.TargetEvidence=original==job.Representative?"Plate solved using "+job.Result.Solver:"Target from same-session plate-solved Light frame: "+job.Filename;
   if(updated.Facts==null)updated.Facts=new Dictionary<string,MetadataFact>();updated.Facts["Target"]=new MetadataFact{Value=target,Raw=target,Source=updated.TargetEvidence};
   updated.Notes=(updated.Notes??"")+updated.TargetEvidence+". ";return updated;
  }
 }
}
