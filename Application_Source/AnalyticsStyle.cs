using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 public static partial class AnalyticsGraphics {
  public static string Blend(string first,string second,double amount){
   int a=int.Parse(first.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture),b=int.Parse(second.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);amount=Math.Max(0,Math.Min(1,amount));
   Func<int,int> channel=shift=>(int)Math.Round(((a>>shift)&255)*(1-amount)+((b>>shift)&255)*amount);
   return "#"+((channel(16)<<16)|(channel(8)<<8)|channel(0)).ToString("X6",CultureInfo.InvariantCulture);
  }
  public static void Bounds(AnalyticsMark mark,out double x,out double y,out double width,out double height){
   x=mark.X;y=mark.Y;width=mark.Width;height=mark.Height;if(mark.Points==null)return;
   x=Enumerable.Range(0,mark.Points.Length/2).Min(i=>mark.Points[i*2]);y=Enumerable.Range(0,mark.Points.Length/2).Min(i=>mark.Points[i*2+1]);
   width=Enumerable.Range(0,mark.Points.Length/2).Max(i=>mark.Points[i*2])-x;height=Enumerable.Range(0,mark.Points.Length/2).Max(i=>mark.Points[i*2+1])-y;
  }
  static void Polish(AnalyticsPage page,bool dark,int index){
   string panel=dark?"#151E2E":"#F5F7FC",ink=dark?"#E9EEF8":Ink,accent=dark?"#90B8FF":Accent;
   var decorations=new List<AnalyticsMark>();
   // A restrained constellation gives the document a recognisable night-sky
   // identity, while leaving the plot and small lettering entirely clear.
   double w=page.CanvasWidth,h=page.CanvasHeight;
   for(int i=0;i<22;i++){
    double x=w*(.60+((i*37)%100)/270.0),y=18+((i*53)%140)*Math.Min(1,h/1080);
    decorations.Add(new AnalyticsMark{Kind="rect",X=x,Y=y,Width=i%7==0?3:1.5,Height=i%7==0?3:1.5,Radius=2,Fill=Blend(page.Background,accent,i%7==0?.42:.17),Role="decoration"});
   }
   decorations.Add(new AnalyticsMark{Kind="rect",X=0,Y=8,Width=w,Height=h-8,Fill=page.Background,FillEnd=dark?"#111B30":"#FAF9FF",Role="decoration"});
   decorations.Reverse();page.Marks.InsertRange(1,decorations);
   var additions=new List<AnalyticsMark>();
   foreach(var mark in page.Marks.ToList()){
    if(mark.Kind=="rect"&&mark.Fill==panel&&mark.Role=="header"){
     // Separate, softly shaded metric cards rather than a dense statistics strip.
     double gap=12,cell=mark.Width/3;mark.Width=cell-gap;mark.Radius=12;mark.FillEnd=dark?"#202C43":"#EDEFFC";
     for(int i=1;i<3;i++)additions.Add(new AnalyticsMark{Kind="rect",X=mark.X+i*cell,Y=mark.Y,Width=cell-gap,Height=mark.Height,Radius=12,Fill=panel,FillEnd=mark.FillEnd,Role="header"});
     for(int i=0;i<3;i++)additions.Add(new AnalyticsMark{Kind="rect",X=mark.X+i*cell+1,Y=mark.Y+16,Width=3,Height=mark.Height-32,Radius=1.5,Fill=i==0?(dark?"#55DFEA":"#008477"):i==1?accent:(dark?"#C19AFF":"#8756A5"),Role="header"});
    }
    if(mark.Kind=="text"&&mark.Fill==ink&&mark.Bold&&mark.Role=="header"&&mark.Text!="AstroArchive"){
     if(mark.Text.EndsWith(" h",StringComparison.Ordinal))mark.Fill=dark?"#55DFEA":"#008477";
    }
    bool colour=mark.Fill!=null&&(Colours.Contains(mark.Fill)||new[]{"#90B8FF","#55DFEA","#C19AFF","#F0C36A","#4F9BFA","#ED9ACB","#9DD7B0","#A6B4CC"}.Contains(mark.Fill));
    if(mark.Role=="chart"&&colour&&(mark.Kind=="polygon"||mark.Kind=="rect"&&mark.Width>20&&mark.Height>3)){
     mark.FillEnd=Blend(mark.Fill,dark?"#735AF5":"#3B4EB5",.38);mark.Radius=Math.Min(8,Math.Min(mark.Width,mark.Height)/2);mark.VerticalGradient=mark.Animation=="column";
    }
    if(mark.Kind=="rect"&&mark.Role=="chart"&&(mark.Fill=="#28354B"||mark.Fill=="#E8ECF4")&&mark.Height>3)mark.Radius=mark.Height/2;
    if(mark.Kind=="rect"&&mark.Y==0){mark.Height=4;mark.FillEnd=dark?"#55DFEA":"#008477";}
   }
   // Cards must be behind their labels; keep the scene's original order otherwise.
   int metric=page.Marks.FindIndex(m=>m.Kind=="rect"&&m.Fill==panel&&m.Radius==12);if(metric>=0)page.Marks.InsertRange(metric+1,additions);
   foreach(var mark in page.Marks.Where(m=>m.Role=="chart"&&m.Kind=="rect"&&m.Fill==panel)){mark.FillEnd=dark?"#1B2940":"#EFF0FA";}

  }
  static void RingTrack(AnalyticsPage p,double cx,double cy,double radius,double inner){
   foreach(double[] band in new[]{new[]{radius+9,radius+7},new[]{inner-7,inner-8}}){
    var points=new List<double>();for(int i=0;i<=128;i++){double a=i*Math.PI/64;points.Add(cx+band[0]*Math.Cos(a));points.Add(cy+band[0]*Math.Sin(a));}for(int i=128;i>=0;i--){double a=i*Math.PI/64;points.Add(cx+band[1]*Math.Cos(a));points.Add(cy+band[1]*Math.Sin(a));}
    p.Marks.Add(new AnalyticsMark{Kind="polygon",Points=points.ToArray(),Fill=Blend(p.Background,p.Background=="#FFFFFF"?Accent:"#90B8FF",.16),Role="decoration"});
   }
  }
 }
}
