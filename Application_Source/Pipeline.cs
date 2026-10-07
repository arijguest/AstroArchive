// C# 5 / .NET Framework 4.8. Stage times exclude pauses between active operations.
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
  readonly object gate=new object();readonly Dictionary<string,Meter> meters=new Dictionary<string,Meter>();readonly Stopwatch clock=Stopwatch.StartNew();readonly Action<ProgressInfo> callback;
  long lastPulse;double phaseStart;public long DoneBytes,TotalBytes;public int Done,Total;public string Current="Starting",Stage="Discovery";
  public PipelineMetrics(Action<ProgressInfo> progress){callback=progress;foreach(string s in new[]{"Discovery","Metadata","Duplicate checking","Copy + source hash","Verification","Cloud availability","Target identification","Rotation","Index/checkpoint"})meters[s]=new Meter();}
  public Scope Begin(string stage,string file){lock(gate){Meter m;if(!meters.TryGetValue(stage,out m))meters[stage]=m=new Meter();if(m.Active++==0)m.Start=Stopwatch.GetTimestamp();Stage=stage;Current=file;}Pulse(true);return new Scope(this,stage);}
  void End(string stage,long complete){lock(gate){var m=meters[stage];if(--m.Active==0)m.Ticks+=Stopwatch.GetTimestamp()-m.Start;m.Files+=complete;}Pulse(false);}
  void Add(string stage,long bytes){lock(gate)meters[stage].Bytes+=bytes;Pulse(false);}
  public List<StageMetric> Snapshot(){lock(gate){long now=Stopwatch.GetTimestamp();return meters.Select(k=>new StageMetric{Stage=k.Key,Files=k.Value.Files,Bytes=k.Value.Bytes,Seconds=(k.Value.Ticks+(k.Value.Active>0?now-k.Value.Start:0))/(double)Stopwatch.Frequency}).ToList();}}
  public void Accumulate(IEnumerable<StageMetric> previous){lock(gate){foreach(var s in previous){Meter m;if(!meters.TryGetValue(s.Stage,out m))meters[s.Stage]=m=new Meter();m.Files+=s.Files;m.Bytes+=s.Bytes;m.Ticks+=(long)(s.Seconds*Stopwatch.Frequency);}}}
  public ProgressInfo Progress(){lock(gate){double seconds=clock.Elapsed.TotalSeconds;double phase=seconds-phaseStart;double? eta=Total>0&&Done>=Total?(double?)0:TotalBytes>0&&DoneBytes>0?(double?)(phase/DoneBytes*Math.Max(0,TotalBytes-DoneBytes)):Total>0&&Done>0?(double?)(phase/Done*(Total-Done)):null;return new ProgressInfo{LiveMetrics=this,Done=Done,Total=Total,Text=Current,Stage=Stage,BytesDone=DoneBytes,BytesTotal=TotalBytes,ElapsedSeconds=seconds,RemainingSeconds=eta,Stages=Snapshot()};}}
  public void Phase(int total,long bytes){lock(gate){Done=0;DoneBytes=0;Total=total;TotalBytes=bytes;phaseStart=clock.Elapsed.TotalSeconds;}Pulse(true);}
  public void Complete(long bytes){lock(gate){Done++;DoneBytes+=bytes;}Pulse(true);}
  public void Pulse(bool force){long now=clock.ElapsedMilliseconds;lock(gate){if(!force&&now-lastPulse<200)return;lastPulse=now;}if(callback!=null)callback(Progress());}
  public string Report(){return string.Join("\r\n",Snapshot().Select(s=>s.Stage+": "+s.Files+" files; "+s.Seconds.ToString("0.00",CultureInfo.InvariantCulture)+" s; "+s.FilesPerSecond.ToString("0.00",CultureInfo.InvariantCulture)+" files/s; "+s.MBPerSecond.ToString("0.00",CultureInfo.InvariantCulture)+" MB/s"));}
  public sealed class Scope:IDisposable {PipelineMetrics owner;readonly string stage;long complete;internal Scope(PipelineMetrics m,string s){owner=m;stage=s;}public void Bytes(long bytes){owner.Add(stage,bytes);}public void Complete(long files=1){complete=files;}public void Dispose(){if(owner!=null){owner.End(stage,complete);owner=null;}}}
 }
 public static class FileRetry {
  public static bool DiskFull(Exception ex){int code=ex.HResult&65535;return code==112||code==39||(ex.Data.Contains("SQLiteCode")&&(Convert.ToInt32(ex.Data["SQLiteCode"])&255)==13);}
  public static bool Transient(Exception ex){var io=ex as System.IO.IOException;if(io==null)return false;int code=ex.HResult&65535;return new[]{5,21,32,33,53,64,121,170,231,1231}.Contains(code);}
  public static T Run<T>(Func<T> action,CancellationToken ct,Action<string> progress){for(int attempt=0;;attempt++){ct.ThrowIfCancellationRequested();try{return action();}catch(Exception ex){if(attempt>=3||!Transient(ex))throw;if(progress!=null)progress("Temporary file lock/read error; retry "+(attempt+1)+" of 3: "+ex.Message);if(ct.WaitHandle.WaitOne(250*(attempt+1)))ct.ThrowIfCancellationRequested();}}}
  public static string Detail(string path,Exception ex){return path+"\r\n"+ex.GetType().Name+" (0x"+ex.HResult.ToString("X8")+"): "+ex.Message;}
 }
}
