using System;
namespace AstroArchive {
 public static class EtaTests {
  static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  public static void Run(Action<string,Action> test){
   test("Long ETA uses hours and short ETA uses appropriate precision",()=>{Check(EtaEstimate.Format(9000)=="2.5 h","Hours include minute noise");Check(EtaEstimate.Format(3599)=="1 h","Boundary renders 60 minutes");Check(EtaEstimate.Format(120)=="2 min"&&EtaEstimate.Format(12)=="10 sec","Short ETA precision");Check(EtaEstimate.Format(double.NaN)=="estimating","Invalid ETA");});
   test("ETA smooths transient spikes and adapts to a sustained slowdown",()=>{var estimate=new EtaEstimate();estimate.Update(600,0);double spike=estimate.Update(2400,1).Value;Check(spike>599&&spike<750,"One sample changed ETA excessively");double later=0;for(int i=2;i<=60;i++)later=estimate.Update(2400,i).Value;Check(later>2250,"Sustained slowdown concealed");});
   test("ETA is independent of sample frequency and resets after waits",()=>{var dense=new EtaEstimate();var sparse=new EtaEstimate();dense.Update(600,0);sparse.Update(600,0);double a=0;for(int i=1;i<=40;i++)a=dense.Update(900-i*0.25,i*0.25).Value;double b=sparse.Update(890,10).Value;Check(Math.Abs(a-b)<0.0001,"Sampling frequency changed finish estimate");Check(!dense.Update(null,11).HasValue,"Wait retained an ETA");Check(dense.Update(120,12)==120,"Resume retained stale estimate");dense.Reset();Check(dense.Update(30,0)==30,"New stage retained old ETA");});
  }
 }
}
