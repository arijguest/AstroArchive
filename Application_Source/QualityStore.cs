// Stored image-quality measurements and the parallel runner that produces them.
// Rows are keyed by the capture's content hash, so renamed or moved files reuse their result and edited files are measured again.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AstroArchive {
 public sealed class QualityRun {
  public int Eligible,Measured,Reused,Skipped,Failed,TooFewStars,NoObject,Workers;
  public List<string> Errors=new List<string>();
  public string Summary(){
   if(Eligible==0)return "No light frames in this view can be scored. Stacks, calibration frames, recordings and non-linear images are skipped.";
   string text=Measured+" measured";if(Reused>0)text+=" · "+Reused+" already measured";if(TooFewStars>0)text+=" · "+TooFewStars+" with too few stars";if(NoObject>0)text+=" · "+NoObject+" without a measurable object";if(Failed>0)text+=" · "+Failed+" unreadable";if(Skipped>0)text+=" · "+Skipped+" not scorable";
   return text;
  }
 }
 public sealed partial class Repository {
  readonly object qualityGate=new object();bool qualityTable;Dictionary<string,QualityMeasurement> qualityCache;
  // Compare against similar frames from the same target instead of the same session (star fields only).
  public bool QualityCompareTarget{get;set;}

  void EnsureQualityTable(){
   lock(qualityGate){if(qualityTable)return;db.Exec("CREATE TABLE IF NOT EXISTS quality(hash TEXT PRIMARY KEY,version INTEGER NOT NULL,kind INTEGER NOT NULL,status INTEGER NOT NULL,data TEXT NOT NULL)");qualityTable=true;}
  }
  // All stored measurements by content hash. The dictionary is shared: treat it as read-only.
  public Dictionary<string,QualityMeasurement> QualityMeasurements(){
   lock(qualityGate){
    if(qualityCache!=null)return qualityCache;EnsureQualityTable();var cache=new Dictionary<string,QualityMeasurement>(StringComparer.OrdinalIgnoreCase);
    foreach(string row in db.Query("SELECT hash||'|'||version||'|'||kind||'|'||status||'|'||data FROM quality")){
     var parts=row.Split(new[]{'|'},5);int version,kind,status;
     if(parts.Length<5||!int.TryParse(parts[1],out version)||!int.TryParse(parts[2],out kind)||!int.TryParse(parts[3],out status))continue;
     cache[parts[0]]=QualityMeasurement.Unpack(parts[0],version,kind,status,parts[4]);
    }
    qualityCache=cache;return cache;
   }
  }
  void SaveQuality(IList<QualityMeasurement> batch){
   if(batch.Count==0)return;EnsureQualityTable();
   db.Transaction(()=>{foreach(var m in batch)db.Exec("INSERT OR REPLACE INTO quality(hash,version,kind,status,data) VALUES(?,?,?,?,?)",m.Hash,m.Version.ToString(),m.Kind.ToString(),m.Status.ToString(),m.Pack());});
   lock(qualityGate){if(qualityCache!=null)foreach(var m in batch)qualityCache[m.Hash]=m;}
  }
  // Forgets every stored measurement (used by tests and by an explicit reset).
  public void ClearQuality(){EnsureQualityTable();db.Exec("DELETE FROM quality");lock(qualityGate)qualityCache=null;}
  // Refreshes the transient quality fields of captures already held in memory.
  public void ApplyQuality(IList<Frame> frames){QualityAnnotator.Apply(frames,QualityMeasurements(),QualityCompareTarget);}

  // Captures that can be scored but have no current measurement.
  public List<Frame> UnmeasuredQuality(IEnumerable<Frame> frames){
   var known=QualityMeasurements();var list=new List<Frame>();
   foreach(var frame in frames){if(frame.Status=="Deleted"||QualityEligibility.Reason(frame).Length>0)continue;QualityMeasurement m;if(known.TryGetValue(frame.Hash??"",out m)&&m.Version==QualityMeter.Version&&m.Kind==QualityEligibility.KindOf(frame))continue;list.Add(frame);}
   return list;
  }
  public QualityRun AnalyzeMissingQuality(CancellationToken ct,Action<ProgressInfo> progress){return AnalyzeQuality(UnmeasuredQuality(All()),false,ct,progress);}

  public QualityRun AnalyzeQuality(IEnumerable<Frame> selection,bool force,CancellationToken ct,Action<ProgressInfo> progress){
   var run=new QualityRun();EnsureQualityTable();var known=QualityMeasurements();var todo=new List<Frame>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var frame in selection){
    if(frame==null||frame.Status=="Deleted"||string.IsNullOrEmpty(frame.Hash)||!seen.Add(frame.Hash))continue;
    if(QualityEligibility.Reason(frame).Length>0){run.Skipped++;continue;}
    run.Eligible++;QualityMeasurement old;
    if(!force&&known.TryGetValue(frame.Hash,out old)&&old.Version==QualityMeter.Version&&old.Kind==QualityEligibility.KindOf(frame)){run.Reused++;Tally(run,old);continue;}
    todo.Add(frame);
   }
   if(todo.Count==0){if(progress!=null)progress(new ProgressInfo{Done=0,Total=0,Stage="Image quality",Text=run.Summary()});return run;}
   long pixels=todo.Max(f=>Math.Max(1L,(long)f.Width*Math.Max(1,f.Height)*Math.Max(1,f.Channels)));
   // Each worker holds the decoded samples (8 bytes) plus a float plane and its bins; keep the total bounded.
   int byMemory=(int)Math.Max(1L,1500000000L/(pixels*20L));int workers=Math.Max(1,Math.Min(Math.Min(Math.Max(1,Environment.ProcessorCount-1),byMemory),8));run.Workers=workers;
   var gate=new object();var pending=new List<QualityMeasurement>();int done=0;
   try{
    Parallel.ForEach(todo,new ParallelOptions{MaxDegreeOfParallelism=workers,CancellationToken=ct},frame=>{
     ct.ThrowIfCancellationRequested();QualityMeasurement m;string error=null;
     try{m=MeasureFrame(frame,FilePath(frame),ct);}
     catch(OperationCanceledException){throw;}
     catch(Exception e){m=new QualityMeasurement{Hash=frame.Hash,Version=QualityMeter.Version,Kind=QualityEligibility.KindOf(frame),Status=QualityStatus.Failed};error=frame.OriginalName+": "+e.Message;}
     lock(gate){
      pending.Add(m);Tally(run,m);run.Measured++;if(error!=null&&run.Errors.Count<20)run.Errors.Add(error);done++;
      if(pending.Count>=32){SaveQuality(pending);pending.Clear();}
      if(progress!=null)progress(new ProgressInfo{Done=done,Total=todo.Count,Workers=workers,Stage="Measuring image quality",Text=frame.OriginalName});
     }
    });
   }finally{lock(gate){SaveQuality(pending);pending.Clear();}}
   if(progress!=null)progress(new ProgressInfo{Done=todo.Count,Total=todo.Count,Workers=workers,Stage="Measuring image quality",Text=run.Summary()});
   return run;
  }
  static void Tally(QualityRun run,QualityMeasurement m){
   if(m.Status==QualityStatus.TooFewStars)run.TooFewStars++;else if(m.Status==QualityStatus.NoObject)run.NoObject++;else if(m.Status==QualityStatus.Failed)run.Failed++;
  }
  // Reads and measures one capture. Public so tests can measure a file without touching the database.
  public static QualityMeasurement MeasureFrame(Frame frame,string path,CancellationToken ct){
   var image=Assets.Read(frame,path,frame.ImageIndex??0,ct);ct.ThrowIfCancellationRequested();
   var plane=QualityMeter.Plane(image.Pixels,image.Width,image.Height,image.Channels);int w=image.Width,h=image.Height,channels=image.Channels;image.Pixels=null;
   ct.ThrowIfCancellationRequested();
   QualityMeasurement m=QualityEligibility.IsDisc(frame)?QualityMeter.MeasureDisc(plane,w,h):QualityMeter.MeasureStars(plane,w,h,channels==1&&QualityEligibility.HasMosaic(frame));
   m.Hash=frame.Hash;return m;
  }
 }
}
