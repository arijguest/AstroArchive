using System;
using System.IO;
namespace AstroArchive {
 // Decides when a copy that started small has turned out long enough to add workers.
 // Workers only ever rise: files already copying are never interrupted, and a rise that
 // does not speed the copy up ends further growth.
 public sealed class WorkerGrowth {
  public int Workers;public readonly int Cap;
  public double InitialSeconds=15,WindowSeconds=10,LongSeconds=30;
  bool frozen,raised;double windowStart,lastRate;long windowBytes;
  public WorkerGrowth(int workers,int cap){Workers=Math.Max(1,workers);Cap=Math.Max(Workers,cap);}
  // The new worker count when more should start now; otherwise 0.
  public int Check(double seconds,long bytesDone,long bytesTotal,int filesLeft){
   if(frozen||Workers>=Cap||seconds<InitialSeconds||seconds-windowStart<WindowSeconds||filesLeft<=Workers)return 0;
   double rate=(bytesDone-windowBytes)/(seconds-windowStart);if(rate<=0)return 0;
   windowBytes=bytesDone;windowStart=seconds;
   if(raised&&rate<lastRate*1.05){frozen=true;return 0;}
   if((bytesTotal-bytesDone)/rate<LongSeconds)return 0;
   lastRate=rate;raised=true;Workers=Math.Min(Cap,Workers*2);return Workers;
  }
  // Half the processors up to 8, and 2 at most when the source or archive is on a network share
  // or both sit on one drive, where more parallel copies only compete.
  public static int CapFor(int processors,bool network,bool sameDrive){
   int cap=Math.Max(2,Math.Min(8,processors/2));return network||sameDrive?Math.Min(cap,2):cap;
  }
  public static bool NetworkPath(string path){
   try{
    if(string.IsNullOrEmpty(path))return false;if(path.StartsWith("\\\\"))return true;
    string root=Path.GetPathRoot(path);return !string.IsNullOrEmpty(root)&&root.Length>=2&&root[1]==':'&&new DriveInfo(root.Substring(0,1)).DriveType==DriveType.Network;
   }catch{return false;}
  }
 }
}
