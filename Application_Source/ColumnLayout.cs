// Stable property IDs keep saved table layouts independent of display labels.
using System.Collections.Generic;
using System;
using System.Linq;
namespace AstroArchive {
 public sealed class ColumnLayout {
  public List<string> Order{get;set;}public List<string> Visible{get;set;}
  public bool RepositoryGainShown{get;set;}
  public static ColumnLayout UpgradeRepository(ColumnLayout saved){
   if(saved==null||saved.RepositoryGainShown)return saved;
   var order=saved.Order==null?null:saved.Order.ToList();
   if(order!=null&&!saved.VisibleContains("GainText")){order.Remove("GainText");order.Insert(order.Contains("ExposureText")?order.IndexOf("ExposureText")+1:order.Count,"GainText");}
   var visible=saved.Visible==null?null:saved.Visible.ToList();if(visible!=null&&!visible.Contains("GainText"))visible.Add("GainText");
   return new ColumnLayout{Order=order,Visible=visible,RepositoryGainShown=true};
  }
  bool VisibleContains(string id){return Visible!=null&&Visible.Contains(id);}
  public static ColumnLayout UpgradeEdited(ColumnLayout saved){
   if(saved==null||saved.Order!=null&&saved.Order.Contains("FileType"))return saved;
   var order=(saved.Order??new List<string>{"Filename"}).ToList();order.Insert(order.IndexOf("Filename")+1,"FileType");
   var visible=saved.Visible==null?null:saved.Visible.Where(id=>id!="Metadata.TotalExposureText").ToList();if(visible!=null&&!visible.Contains("FileType"))visible.Add("FileType");
   return new ColumnLayout{Order=order,Visible=visible,RepositoryGainShown=saved!=null&&saved.RepositoryGainShown};
  }
  public static ColumnLayout Resolve(ColumnLayout saved,IEnumerable<string> available,IEnumerable<string> defaults){
   var ids=available.Distinct().ToList();var known=new HashSet<string>(ids);
   var order=(saved==null||saved.Order==null?ids:saved.Order).Where(known.Contains).Distinct().ToList();order.AddRange(ids.Where(id=>!order.Contains(id)));
   var visible=(saved==null||saved.Visible==null?defaults:saved.Visible).Where(known.Contains).Distinct().ToList();
   if(visible.Count==0)visible=defaults.Where(known.Contains).Distinct().ToList();
   if(visible.Count==0&&ids.Count>0)visible.Add(ids[0]);
   return new ColumnLayout{Order=order,Visible=visible,RepositoryGainShown=saved!=null&&saved.RepositoryGainShown};
  }
 }
 public static class ColumnWidths {
  // Fit relative preferences to the current viewport, respecting readable
  // defaults or the smaller minima allowed after an explicit user drag.
  public static double[] Fit(double[] preferred,double[] minimum,double[] maximum,bool[] resizable,double available){
   var widths=new double[preferred.Length];var pending=new List<int>();
   if(double.IsNaN(available)||double.IsInfinity(available)||available<=0)return (double[])preferred.Clone();
   for(int i=0;i<widths.Length;i++)if(resizable[i])pending.Add(i);else{widths[i]=Math.Max(minimum[i],Math.Min(maximum[i],preferred[i]));available-=widths[i];}
   double least=pending.Sum(i=>minimum[i]),most=pending.Sum(i=>maximum[i]);
   if(available<=least){foreach(int i in pending)widths[i]=minimum[i];return widths;}
   if(available>=most){foreach(int i in pending)widths[i]=maximum[i];return widths;}
   if(pending.Count==0)return widths;
   double low=0,high=Math.Max(1,available/pending.Sum(i=>Math.Max(1,preferred[i])));
   Func<double,double> total=scale=>pending.Sum(i=>Math.Max(minimum[i],Math.Min(maximum[i],Math.Max(1,preferred[i])*scale)));
   while(total(high)<available)high*=2;
   for(int step=0;step<56;step++){double middle=(low+high)/2;if(total(middle)<available)low=middle;else high=middle;}
   foreach(int i in pending)widths[i]=Math.Max(minimum[i],Math.Min(maximum[i],Math.Max(1,preferred[i])*high));
   return widths;
  }
  public static double[] Resize(double[] initial,double[] minimum,double[] maximum,bool[] resizable,int selected,double requested){
   var widths=(double[])initial.Clone();if(selected<0||selected>=widths.Length||double.IsNaN(requested)||double.IsInfinity(requested))return widths;
   double desired=Math.Max(minimum[selected],Math.Min(maximum[selected],requested)),delta=desired-widths[selected];widths[selected]=desired;
   var neighbours=Enumerable.Range(selected+1,widths.Length-selected-1).Concat(Enumerable.Range(0,selected).Reverse()).Where(i=>resizable[i]).ToList();
   if(delta>0){foreach(int i in neighbours){double take=Math.Min(delta,Math.Max(0,widths[i]-minimum[i]));widths[i]-=take;delta-=take;if(delta<=0)break;}}
   else if(delta<0){foreach(int i in neighbours){double give=Math.Min(-delta,Math.Max(0,maximum[i]-widths[i]));widths[i]+=give;delta+=give;if(delta>=0)break;}}
   return widths;
  }
 }
}
