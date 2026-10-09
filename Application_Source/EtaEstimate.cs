using System;
using System.Globalization;

namespace AstroArchive {
 // Smooth the predicted finish time, not each frame's countdown. Sampling frequency
 // does not affect smoothing, and a genuine sustained slowdown can extend the ETA.
 public sealed class EtaEstimate {
  double? finish;double last;
  public void Reset(){finish=null;last=0;}
  public double? Update(double? remaining,double seconds){
   if(!remaining.HasValue||double.IsNaN(remaining.Value)||double.IsInfinity(remaining.Value)||remaining.Value<0){Reset();return null;}
   if(!finish.HasValue||seconds<last){finish=seconds+remaining.Value;last=seconds;return remaining;}
   double elapsed=seconds-last;if(elapsed>0){double alpha=1-Math.Exp(-elapsed/15.0);finish+=alpha*(seconds+remaining.Value-finish.Value);last=seconds;}
   return Math.Max(0,finish.Value-seconds);
  }
  public static string Format(double seconds){
   if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)return "estimating";
   if(seconds>=3570)return (Math.Round(seconds/3600.0,1,MidpointRounding.AwayFromZero)).ToString("0.#",CultureInfo.InvariantCulture)+" h";
   if(seconds>=60)return Math.Max(1,Math.Round(seconds/60.0,MidpointRounding.AwayFromZero)).ToString("0",CultureInfo.InvariantCulture)+" min";
   return Math.Max(1,Math.Round(seconds/5.0,MidpointRounding.AwayFromZero)*5).ToString("0",CultureInfo.InvariantCulture)+" sec";
  }
 }
}
