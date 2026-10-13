// Stored image-quality measurements and the parallel runner that produces them.
// Rows are keyed by the capture's content hash, so renamed or moved files reuse their result and edited files are measured again.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AstroArchive {
 public sealed class QualityRun {
  public int Eligible,Measured,Reused,Skipped,Failed,TooFewStars,NoObject,Unsuitable,SetAside,NotReached,Workers;
  // One line per file that could not be read or was set aside, plus any problem with the run itself.
  public List<string> Errors=new List<string>();
  public readonly Dictionary<string,int> SkipReasons=new Dictionary<string,int>(StringComparer.Ordinal);
  int omitted;const int MaxLines=500;
  internal void Skip(string reason){Skipped++;int count;SkipReasons.TryGetValue(reason,out count);SkipReasons[reason]=count+1;}
  internal void Problem(string line){if(Errors.Count<MaxLines)Errors.Add(line);else omitted++;}
  public bool NeedsReport{get{return Failed>0||Unsuitable>0||NotReached>0||Errors.Count>0;}}
  public string Summary(){
   if(Eligible==0)return "No light frames or stacks in this view can be scored. Calibration frames, recordings and non-linear images are skipped.";
   string text=Measured+" measured";if(Reused>0)text+=" · "+Reused+" already measured";if(TooFewStars>0)text+=" · "+TooFewStars+" with too few stars";if(NoObject>0)text+=" · "+NoObject+" without a measurable object";if(Failed>0)text+=" · "+Failed+" unreadable";if(Unsuitable+SetAside>0)text+=" · "+(Unsuitable+SetAside)+" set aside as unsuitable";if(NotReached>0)text+=" · "+NotReached+" not reached";if(Skipped>0)text+=" · "+Skipped+" not scorable";
   return text;
  }
  // The full account of a run: what was scored, what was set aside or unreadable and why.
  public string Report(){
   var lines=new List<string>{Summary(),""};
   lines.Add("Measured now: "+Measured+(TooFewStars+NoObject>0?" ("+(TooFewStars>0?TooFewStars+" with too few stars":"")+(TooFewStars>0&&NoObject>0?", ":"")+(NoObject>0?NoObject+" without a measurable object":"")+")":""));
   lines.Add("Already measured: "+Reused);
   if(Unsuitable>0)lines.Add("Set aside as unsuitable: "+Unsuitable+". They are skipped from now on; Re-measure image quality tries them again.");
   if(SetAside>0)lines.Add("Skipped, set aside by an earlier run: "+SetAside);
   if(Failed>0)lines.Add("Could not be read: "+Failed+". They are tried again on the next run.");
   if(NotReached>0)lines.Add("Not reached because the run stopped early: "+NotReached+". Run it again to measure them.");
   if(Skipped>0){lines.Add("Not scorable: "+Skipped);foreach(var reason in SkipReasons.OrderByDescending(r=>r.Value).ThenBy(r=>r.Key))lines.Add("  "+reason.Value+" · "+reason.Key);}
   if(Errors.Count>0){lines.Add("");lines.Add("Files:");lines.AddRange(Errors.Select(e=>"  "+e));if(omitted>0)lines.Add("  and "+omitted+" more");}
   return string.Join("\r\n",lines);
  }
 }
 // Sorts measuring errors into files that are unsuitable (set aside) and files that could not be read (tried again).
 public static class QualityFailure {
  public static bool OutOfMemory(Exception e){return e is OutOfMemoryException;}
  public static bool Unsuitable(Exception e){return !(e is UnauthorizedAccessException)&&!(e is IOException&&!(e is EndOfStreamException));}
  public static string Describe(Exception e){
   if(e is OutOfMemoryException)return "too large to measure with the memory available";
   if(e is OverflowException)return "too large to measure";
   if(e is FileNotFoundException||e is DirectoryNotFoundException)return "the archived file is missing";
   if(e is UnauthorizedAccessException)return "access to the file was denied";
   string message=string.IsNullOrWhiteSpace(e.Message)?e.GetType().Name:e.Message.Trim();
   if(e is NotSupportedException||e is NotImplementedException)return "this image layout cannot be measured ("+message+")";
   return message;
  }
 }
 public sealed partial class Repository {
  // qualityGate only guards the fields below and is never held while the database lock is taken, so a background run and
  // the interface (or an import) can use the index at the same time without waiting on each other in a circle.
  readonly object qualityGate=new object();bool qualityTable;Dictionary<string,QualityMeasurement> qualityCache;long qualityVersion;
  // Compare against similar frames from the same target instead of the same session (star fields only).
  public bool QualityCompareTarget{get;set;}

  void EnsureQualityTable(){
   lock(qualityGate)if(qualityTable)return;
   // Creating the table twice is harmless, so two threads reaching here together need no further coordination.
   db.Exec("CREATE TABLE IF NOT EXISTS quality(hash TEXT PRIMARY KEY,version INTEGER NOT NULL,kind INTEGER NOT NULL,status INTEGER NOT NULL,data TEXT NOT NULL)");
   lock(qualityGate)qualityTable=true;
  }
  // All stored measurements by content hash. The dictionary is a snapshot that is never changed once handed out (a save
  // publishes a new one), so it is safe to read from any thread while a background run keeps storing results.
  public Dictionary<string,QualityMeasurement> QualityMeasurements(){
   while(true){
    long version;lock(qualityGate){if(qualityCache!=null)return qualityCache;version=qualityVersion;}
    EnsureQualityTable();var cache=new Dictionary<string,QualityMeasurement>(StringComparer.OrdinalIgnoreCase);
    foreach(string row in db.Query("SELECT hash||'|'||version||'|'||kind||'|'||status||'|'||data FROM quality")){
     var parts=row.Split(new[]{'|'},5);int number,kind,status;
     if(parts.Length<5||!int.TryParse(parts[1],out number)||!int.TryParse(parts[2],out kind)||!int.TryParse(parts[3],out status))continue;
     cache[parts[0]]=QualityMeasurement.Unpack(parts[0],number,kind,status,parts[4]);
    }
    // A save that landed while the rows were being read may be missing from them; read again in that case.
    lock(qualityGate){if(qualityCache!=null)return qualityCache;if(qualityVersion==version){qualityCache=cache;return cache;}}
   }
  }
  void SaveQuality(IList<QualityMeasurement> batch){
   if(batch.Count==0)return;EnsureQualityTable();
   db.Transaction(()=>{foreach(var m in batch)db.Exec("INSERT OR REPLACE INTO quality(hash,version,kind,status,data) VALUES(?,?,?,?,?)",m.Hash,m.Version.ToString(),m.Kind.ToString(),m.Status.ToString(),m.Pack());});
   lock(qualityGate){
    qualityVersion++;
    if(qualityCache!=null){var next=new Dictionary<string,QualityMeasurement>(qualityCache,StringComparer.OrdinalIgnoreCase);foreach(var m in batch)next[m.Hash]=m;qualityCache=next;}
   }
  }
  // Forgets every stored measurement (used by tests and by an explicit reset).
  public void ClearQuality(){EnsureQualityTable();db.Exec("DELETE FROM quality");lock(qualityGate){qualityVersion++;qualityCache=null;}}
  // Refreshes the transient quality fields of captures already held in memory.
  public void ApplyQuality(IList<Frame> frames){QualityAnnotator.Apply(frames,QualityMeasurements(),QualityCompareTarget);}

  // Captures that can be scored but have no current measurement.
  public List<Frame> UnmeasuredQuality(IEnumerable<Frame> frames){
   var known=QualityMeasurements();var list=new List<Frame>();
   foreach(var frame in frames){if(frame.Status=="Deleted"||QualityEligibility.Reason(frame).Length>0)continue;QualityMeasurement m;if(known.TryGetValue(frame.Hash??"",out m)&&m.Version==QualityMeter.Version&&m.Kind==QualityEligibility.KindOf(frame)&&m.Status!=QualityStatus.Failed)continue;list.Add(frame);}
   return list;
  }
  public QualityRun AnalyzeMissingQuality(CancellationToken ct,Action<ProgressInfo> progress){return AnalyzeQuality(UnmeasuredQuality(All()),false,ct,progress);}

  public QualityRun AnalyzeQuality(IEnumerable<Frame> selection,bool force,CancellationToken ct,Action<ProgressInfo> progress){
   var run=new QualityRun();EnsureQualityTable();var known=QualityMeasurements();var todo=new List<Frame>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var frame in selection){
    if(frame==null||frame.Status=="Deleted"||string.IsNullOrEmpty(frame.Hash)||!seen.Add(frame.Hash))continue;
    string reason;try{reason=QualityEligibility.Reason(frame);}catch(Exception e){reason="its details could not be read ("+e.Message+")";}
    if(reason.Length>0){run.Skip(reason);continue;}
    run.Eligible++;QualityMeasurement old;
    if(!force&&known.TryGetValue(frame.Hash,out old)&&old.Version==QualityMeter.Version&&old.Kind==QualityEligibility.KindOf(frame)){
     if(old.Status==QualityStatus.Unsuitable){run.SetAside++;continue;}
     // Unreadable files are tried again; the problem may have been the drive, not the file.
     if(old.Status!=QualityStatus.Failed){run.Reused++;Tally(run,old);continue;}
    }
    todo.Add(frame);
   }
   if(todo.Count==0){Report(progress,new ProgressInfo{Done=0,Total=0,Stage="Image quality",Text=run.Summary()});return run;}
   long pixels=todo.Max(f=>{long samples=(long)f.Width*Math.Max(1,f.Height)*Math.Max(1,f.Channels);return Math.Max(1L,samples>0?samples:f.Bytes/2);});
   // Each worker holds the decoded samples (8 bytes) plus a float plane and its bins; keep the total bounded.
   int byMemory=(int)Math.Max(1L,1500000000L/(pixels*20L));int workers=Math.Max(1,Math.Min(Math.Min(Math.Max(1,Environment.ProcessorCount-1),byMemory),8));run.Workers=workers;
   var gate=new object();var pending=new List<QualityMeasurement>();var retry=new List<Frame>();int done=0;
   Action flush=()=>{if(pending.Count==0)return;try{SaveQuality(pending);}catch(Exception e){run.Problem("Measurements could not be saved: "+e.Message);}pending.Clear();};
   // alone: the second try for a frame that ran out of memory beside other workers.
   Action<Frame,bool> measure=(frame,alone)=>{
    QualityMeasurement m=null;string error=null;bool unsuitable=false;
    try{m=MeasureStored(frame,ct);if(m.Status==QualityStatus.Failed){m.Status=QualityStatus.Unsuitable;error="the image is too small or incomplete to measure";}}
    catch(OperationCanceledException){if(ct.IsCancellationRequested)throw;error="measuring stopped before it finished";}
    catch(Exception e){if(!alone&&QualityFailure.OutOfMemory(e)){lock(gate)retry.Add(frame);return;}error=QualityFailure.Describe(e);unsuitable=QualityFailure.Unsuitable(e);}
    if(m==null)m=new QualityMeasurement{Hash=frame.Hash,Version=QualityMeter.Version,Kind=QualityEligibility.KindOf(frame),Status=unsuitable?QualityStatus.Unsuitable:QualityStatus.Failed};
    lock(gate){
     pending.Add(m);Tally(run,m);done++;
     if(m.Status==QualityStatus.Unsuitable){run.Unsuitable++;run.Problem(frame.OriginalName+": set aside, "+error);}
     else if(m.Status==QualityStatus.Failed)run.Problem(frame.OriginalName+": could not be read, "+error);
     else run.Measured++;
     if(pending.Count>=32)flush();
     Report(progress,new ProgressInfo{Done=done,Total=todo.Count,Workers=workers,Stage="Measuring image quality",Text=frame.OriginalName});
    }
   };
   try{
    try{
     Parallel.ForEach(todo,new ParallelOptions{MaxDegreeOfParallelism=workers,CancellationToken=ct},frame=>{
      ct.ThrowIfCancellationRequested();
      try{measure(frame,false);}catch(OperationCanceledException){throw;}catch(Exception e){lock(gate)run.Problem(frame.OriginalName+": "+QualityFailure.Describe(e));}
     });
    }catch(AggregateException e){
     // Keep what was measured and say why the rest was not reached.
     if(ct.IsCancellationRequested)throw new OperationCanceledException(ct);
     lock(gate)foreach(var inner in e.Flatten().InnerExceptions)run.Problem("The run stopped early: "+QualityFailure.Describe(inner));
    }
    if(retry.Count>0){GC.Collect();foreach(var frame in retry.ToList()){ct.ThrowIfCancellationRequested();measure(frame,true);}}
   }finally{lock(gate)flush();}
   lock(gate)run.NotReached=Math.Max(0,todo.Count-done);
   Report(progress,new ProgressInfo{Done=done,Total=todo.Count,Workers=workers,Stage="Measuring image quality",Text=run.Summary()});
   return run;
  }
  // Measures an archived capture. A run holds copies of the captures from when it started, so a file that was moved since
  // (renamed by a metadata edit or a target change) is looked for again at the place the archive now records.
  QualityMeasurement MeasureStored(Frame frame,CancellationToken ct){
   try{return MeasureFrame(frame,FilePath(frame),ct);}
   catch(Exception e){
    if(!(e is FileNotFoundException||e is DirectoryNotFoundException))throw;
    var now=Find(frame.Hash);if(now==null||string.Equals(now.RelativePath,frame.RelativePath,StringComparison.OrdinalIgnoreCase))throw;
    return MeasureFrame(frame,FilePath(now),ct);
   }
  }
  // Progress display problems must not stop a measuring run.
  static void Report(Action<ProgressInfo> progress,ProgressInfo info){if(progress==null)return;try{progress(info);}catch(Exception){}}
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
