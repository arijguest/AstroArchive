// C# 5 / .NET Framework 4.8. Progress is sampled, independently of file completion.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public class StageMetric {public string Stage{get;set;} public long Files{get;set;} public long Bytes{get;set;} public double Seconds{get;set;} public double FilesPerSecond{get{return Seconds>0?Files/Seconds:0;}} public double MBPerSecond{get{return Seconds>0?Bytes/1000000.0/Seconds:0;}}}
 public class WorkerTrial {public int Workers;public int Files;public long Bytes;public double Seconds;public double MBPerSecond{get{return Seconds>0?Bytes/1000000.0/Seconds:0;}}public int Failed;}
 public sealed class PipelineMetrics {
  class Meter {public long Files,Bytes,Ticks,Start;public int Active;}
  class Sample {public double Seconds,Work;}
  readonly object gate=new object();
  readonly Dictionary<string,Meter> meters=new Dictionary<string,Meter>();
  readonly HashSet<Transfer> transfers=new HashSet<Transfer>();
  readonly Queue<Sample> samples=new Queue<Sample>();
  readonly Stopwatch clock=Stopwatch.StartNew();readonly Func<double> time;readonly Action<ProgressInfo> callback;
  double phaseStart,lastPulse=-1,lastAdvance,lastWork,settledCopies,settledVerifies,skippedBytes,skippedVerificationBytes,verificationWeight=1,copySeconds,verifySeconds,copyMeasuredBytes,verifyMeasuredBytes;int generation,workers=1,extraVerificationPasses;
  long doneBytes,totalBytes;int done,total;bool totalKnown,copyPhase,finished,finalising;string current="Starting",stage="Preparing";
  public long DoneBytes {get{lock(gate)return doneBytes;}}
  public long TotalBytes {get{lock(gate)return totalBytes;}set{lock(gate)totalBytes=value;}}
  public int Done {get{lock(gate)return done;}}
  public int Total {get{lock(gate)return total;}set{lock(gate){total=value;totalKnown=true;}}}
  public string Current {get{lock(gate)return current;}set{lock(gate)current=value;}}
  public string Stage {get{lock(gate)return stage;}set{lock(gate)stage=value;}}
  public PipelineMetrics(Action<ProgressInfo> progress,Func<double> seconds=null){
   callback=progress;time=seconds??(()=>clock.Elapsed.TotalSeconds);
   foreach(string name in new[]{"Discovery","Metadata","Duplicate checking","Copy + source hash","Verification","Cloud availability","Target identification","Rotation","Index/checkpoint"})meters[name]=new Meter();
   phaseStart=lastAdvance=time();samples.Enqueue(new Sample{Seconds=phaseStart});
  }
  public Scope Begin(string activity,string file){
   lock(gate){Meter m;if(!meters.TryGetValue(activity,out m))meters[activity]=m=new Meter();if(m.Active++==0)m.Start=Stopwatch.GetTimestamp();current=file;}
   Pulse(false);return new Scope(this,activity);
  }
  void End(string activity,long complete){lock(gate){var m=meters[activity];if(--m.Active==0)m.Ticks+=Stopwatch.GetTimestamp()-m.Start;m.Files+=complete;}}
  void Add(string activity,long bytes){lock(gate)meters[activity].Bytes+=bytes;}
  public List<StageMetric> Snapshot(){lock(gate){long tick=Stopwatch.GetTimestamp();return meters.Select(k=>new StageMetric{Stage=k.Key,Files=k.Value.Files,Bytes=k.Value.Bytes,Seconds=(k.Value.Ticks+(k.Value.Active>0?tick-k.Value.Start:0))/(double)Stopwatch.Frequency}).ToList();}}
  public void Accumulate(IEnumerable<StageMetric> previous){lock(gate){foreach(var s in previous){Meter m;if(!meters.TryGetValue(s.Stage,out m))meters[s.Stage]=m=new Meter();m.Files+=s.Files;m.Bytes+=s.Bytes;m.Ticks+=(long)(s.Seconds*Stopwatch.Frequency);}}}
  public void Phase(int count,long bytes,string name=null,bool copying=false,bool known=true,bool cleanup=false){
   lock(gate){generation++;transfers.Clear();done=0;doneBytes=0;total=count;totalBytes=bytes;totalKnown=known;copyPhase=copying;extraVerificationPasses=cleanup?2:0;finished=finalising=false;settledCopies=settledVerifies=skippedBytes=skippedVerificationBytes=lastWork=0;verificationWeight=1;copySeconds=verifySeconds=copyMeasuredBytes=verifyMeasuredBytes=0;workers=1;phaseStart=lastAdvance=time();samples.Clear();samples.Enqueue(new Sample{Seconds=phaseStart});if(name!=null)stage=name;}
   Pulse(true);
  }
  public void Discover(long bytes){lock(gate){total++;totalBytes+=Math.Max(0,bytes);}Pulse(false);}
  public void InventoryComplete(){lock(gate)totalKnown=true;Pulse(true);}
  public void Workers(int count){lock(gate){if(workers==count)return;workers=count;ResetSamples();}}
  void ResetSamples(){samples.Clear();lastWork=Work();lastAdvance=time();samples.Enqueue(new Sample{Seconds=lastAdvance,Work=lastWork});}
  double Work(){return copyPhase?settledCopies+settledVerifies*verificationWeight+transfers.Sum(t=>t.CopiedBytes+(t.VerifiedBytes+t.CleanupBytes)*verificationWeight):done;}
  public ProgressInfo Progress(bool details=true){
   lock(gate){
    double seconds=time(),work=Work();
    if(work<lastWork)ResetSamples();
    if(work>lastWork){lastAdvance=seconds;lastWork=work;}
    if(samples.Count==0||seconds-samples.Last().Seconds>=0.2)samples.Enqueue(new Sample{Seconds=seconds,Work=work});
    while(samples.Count>2&&seconds-samples.ElementAt(1).Seconds>8)samples.Dequeue();
    var first=samples.Peek();double span=seconds-first.Seconds,rate=span>0?(work-first.Work)/span:0;
    double remaining=copyPhase?Math.Max(0,(totalBytes-skippedBytes)*(1+(1+extraVerificationPasses)*verificationWeight)-skippedVerificationBytes*verificationWeight-work):Math.Max(0,total-done);
    bool stalled=!finished&&!finalising&&seconds-lastAdvance>=3;
    double? eta=finished?(double?)0:totalKnown&&!finalising&&!stalled&&remaining>0&&rate>0&&seconds-phaseStart>=1?(double?)(remaining/rate):null;
    if(eta.HasValue&&copyPhase&&transfers.Count>0){double largest=transfers.Max(t=>Math.Max(0,t.Size*(1+verificationWeight)-t.CopiedBytes-(t.VerifiedBytes+t.CleanupBytes)*verificationWeight));eta=Math.Max(eta.Value,largest/(rate/Math.Max(1,Math.Min(workers,total-done))));}
    long copied=doneBytes+transfers.Sum(t=>t.CopiedBytes);
    return new ProgressInfo{LiveMetrics=this,Done=done,Total=total,TotalKnown=totalKnown,Text=current,Stage=stage,Activity=string.Join(", ",meters.Where(k=>k.Value.Active>0).Select(k=>k.Key)),BytesDone=Math.Min(totalBytes,copied),BytesTotal=totalBytes,ElapsedSeconds=seconds,RemainingSeconds=eta,Stalled=stalled,Finalising=finalising,Finished=finished,WorkPerSecond=rate,EffectiveBytesPerSecond=copyPhase?rate/(1+(1+extraVerificationPasses)*verificationWeight):0,CopyPhase=copyPhase,EtaProvisional=copyPhase&&verifyMeasuredBytes==0,ProgressFraction=finished?1:copyPhase?(totalBytes>0?Math.Min(0.99,work/(totalBytes*(1+(1+extraVerificationPasses)*verificationWeight))):0):(totalKnown&&total>0?(double)done/total:0),Stages=details?Snapshot():null};
   }
  }
  public void UpdateLegacy(int count,int expected,string text){lock(gate){done=count;total=expected;totalKnown=expected>0;current=text;}}
  public double VerificationWeight{get{lock(gate)return verificationWeight;}}
  public bool HasVerificationTiming{get{lock(gate)return verifyMeasuredBytes>0&&copyMeasuredBytes>0;}}
  public void SeedVerificationWeight(double value){if(double.IsNaN(value)||double.IsInfinity(value)||value<0.02||value>20)return;lock(gate){verificationWeight=value;ResetSamples();}}
  void Measure(bool verification,long bytes,double seconds){
   if(bytes<=0||seconds<=0)return;if(verification){verifyMeasuredBytes+=bytes;verifySeconds+=seconds;}else{copyMeasuredBytes+=bytes;copySeconds+=seconds;}
   if(copyMeasuredBytes>0&&verifyMeasuredBytes>0){double ratio=Math.Max(0.02,Math.Min(20,(verifySeconds/verifyMeasuredBytes)/(copySeconds/copyMeasuredBytes)));if(Math.Abs(ratio-verificationWeight)>verificationWeight*0.1){verificationWeight=ratio;ResetSamples();}}
  }
  public void Complete(long bytes){lock(gate){done++;doneBytes+=Math.Max(0,bytes);}Pulse(false);}
  public Transfer Track(long bytes){lock(gate){var item=new Transfer(this,Math.Max(0,bytes),generation);transfers.Add(item);return item;}}
  public void Finalise(string text){lock(gate){finalising=true;current=text;}Pulse(true);}
  public void Finish(string text){lock(gate){finished=true;finalising=false;current=text;}Pulse(true);}
  public void Pulse(bool force){double seconds=time();lock(gate){if(!force&&lastPulse>=0&&seconds-lastPulse<0.2)return;lastPulse=seconds;}if(callback!=null)callback(new ProgressInfo{LiveMetrics=this});}
  public string Report(){return string.Join("\r\n",Snapshot().Select(s=>s.Stage+": "+s.Files+" files; "+s.Seconds.ToString("0.00",CultureInfo.InvariantCulture)+" s; "+s.FilesPerSecond.ToString("0.00",CultureInfo.InvariantCulture)+" files/s; "+s.MBPerSecond.ToString("0.00",CultureInfo.InvariantCulture)+" MB/s"));}
  public static string Duration(double seconds){var t=TimeSpan.FromSeconds(Math.Max(0,Math.Min(seconds,TimeSpan.MaxValue.TotalSeconds-1)));return ((long)t.TotalHours).ToString("00",CultureInfo.InvariantCulture)+t.ToString(@"\:mm\:ss");}
  public sealed class Transfer {
   readonly PipelineMetrics owner;readonly int version;internal long CopiedBytes,VerifiedBytes,CleanupBytes;public readonly long Size;bool resolved;double copyStart,verifyStart;
   internal Transfer(PipelineMetrics metrics,long bytes,int phase){owner=metrics;Size=bytes;version=phase;}
   public void BeginCopy(){lock(owner.gate)copyStart=owner.time();}
   public void EndCopy(){lock(owner.gate){if(!resolved&&version==owner.generation)owner.Measure(false,CopiedBytes,owner.time()-copyStart);}}
   public void BeginVerification(){lock(owner.gate)verifyStart=owner.time();}
   public void EndVerification(){lock(owner.gate){if(!resolved&&version==owner.generation)owner.Measure(true,VerifiedBytes,owner.time()-verifyStart);}}
   public void Reset(){lock(owner.gate){if(resolved||version!=owner.generation)return;if(CopiedBytes==0&&VerifiedBytes==0)return;CopiedBytes=VerifiedBytes=CleanupBytes=0;owner.ResetSamples();}}
   public void Copied(int bytes){lock(owner.gate){if(!resolved&&version==owner.generation)CopiedBytes=Math.Min(Size,CopiedBytes+bytes);}}
   public void Verified(int bytes){lock(owner.gate){if(!resolved&&version==owner.generation)VerifiedBytes=Math.Min(Size,VerifiedBytes+bytes);}}
   public void Cleanup(int bytes){lock(owner.gate){if(!resolved&&version==owner.generation)CleanupBytes=Math.Min(owner.extraVerificationPasses*Size,CleanupBytes+bytes);}}
   public void Resolve(bool success){lock(owner.gate){if(resolved||version!=owner.generation)return;resolved=true;owner.transfers.Remove(this);if(success){owner.settledCopies+=Size;owner.settledVerifies+=Size+CleanupBytes;owner.skippedVerificationBytes+=owner.extraVerificationPasses*Size-CleanupBytes;owner.doneBytes+=Size;}else{owner.skippedBytes+=Size;owner.ResetSamples();}owner.done++;}owner.Pulse(false);}
  }
  public sealed class Scope:IDisposable {PipelineMetrics owner;readonly string activity;long complete;internal Scope(PipelineMetrics metrics,string name){owner=metrics;activity=name;}public void Bytes(long bytes){owner.Add(activity,bytes);}public void Complete(long files=1){complete=files;}public void Dispose(){if(owner!=null){owner.End(activity,complete);owner=null;}}}
 }
 public static class FileRetry {
  public static bool DiskFull(Exception ex){int code=ex.HResult&65535;return code==112||code==39||(ex.Data.Contains("SQLiteCode")&&(Convert.ToInt32(ex.Data["SQLiteCode"])&255)==13);}
  public static bool Transient(Exception ex){var io=ex as System.IO.IOException;if(io==null)return false;int code=ex.HResult&65535;return new[]{5,21,32,33,53,64,121,170,231,1231}.Contains(code);}
  public static T Run<T>(Func<T> action,CancellationToken ct,Action<string> progress){for(int attempt=0;;attempt++){ct.ThrowIfCancellationRequested();try{return action();}catch(Exception ex){if(attempt>=3||!Transient(ex))throw;if(progress!=null)progress("Temporary file lock/read error; retry "+(attempt+1)+" of 3: "+ex.Message);if(ct.WaitHandle.WaitOne(250*(attempt+1)))ct.ThrowIfCancellationRequested();}}}
  public static string Detail(string path,Exception ex){return path+"\r\n"+ex.GetType().Name+" (0x"+ex.HResult.ToString("X8")+"): "+ex.Message;}
 }
}
