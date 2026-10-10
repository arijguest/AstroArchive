using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 public static partial class AnalyticsGraphics {
  public static int SheetColumns(IList<AnalyticsPage> pages){return pages[0].Portrait?1:pages.Count>1?2:1;}
  public static void SheetSize(IList<AnalyticsPage> pages,out double width,out double height){
   int columns=SheetColumns(pages);width=pages[0].CanvasWidth*columns;height=pages[0].CanvasHeight*Math.Ceiling(pages.Count/(double)columns);
  }
  static AnalyticsPage PortraitPage(AnalyticsSnapshot data,int index,int part,int parts,List<AnalyticsValue> values,bool dark){
   var report=data.Reports[index];
   var p=new AnalyticsPage{Title=report.Title,CanvasWidth=1080,CanvasHeight=1920,Background=dark?"#0C1220":"#FFFFFF"};
   Box(p,0,0,p.CanvasWidth,p.CanvasHeight,"#FFFFFF");Box(p,0,0,p.CanvasWidth,8,Accent);
   p.Marks.Add(new AnalyticsMark{Kind="logo",X=80,Y=112,Width=88,Height=88});
   Text(p,"AstroArchive",186,139,34,Ink,true);
   Text(p,(index+1).ToString("00",CultureInfo.InvariantCulture)+" / 06"+(parts>1?" · "+(part+1)+"/"+parts:""),parts>1?800:866,150,18,Accent,true);
   Lines(p,report.Title,80,250,60,48,Ink,2,920,true);
   string description=report.Description+(parts>1?" · Groups "+(part*8+1)+"–"+Math.Min((part+1)*8,report.Values.Count)+" of "+report.Values.Count:"");
   Lines(p,description,80,386,80,23,Muted,3,920);
   Box(p,80,510,920,134,"#F5F7FC");
   string[] metrics={ArchiveAnalytics.Number(data.Seconds/3600)+" h",data.Captures.ToString("N0",CultureInfo.InvariantCulture),data.Targets.ToString("N0",CultureInfo.InvariantCulture)};
   string[] captions={"INTEGRATION","LIGHT FRAMES","TARGETS"};
   for(int i=0;i<3;i++){double x=100+i*306;double size=TextWidth(metrics[i],38)>270?28:38;Text(p,metrics[i],x,532,size,Ink,true);Text(p,captions[i],x,594,16,Muted,true);}
   double total=report.Values.Sum(v=>v.Value);
   if(total<=0){
    Lines(p,data.Captures==0?"No light frames in this scope":"No known exposure data for this chart",80,900,60,32,Ink,2,920);
    Lines(p,"Try another telescope or date range, or complete missing capture metadata.",80,1010,80,23,Muted,3,920);
   }else if(report.Style=="donut")PortraitDonut(p,values,total,report.Unit);
   else if(report.Style=="bars")PortraitBars(p,values,report.Unit,report.Values.Max(v=>v.Value));
   else PortraitColumns(p,report.Values,report.Unit);
   Lines(p,report.Note,80,1540,110,18,Muted,3,920);
   Box(p,80,1630,920,1,"#DCE2EF");
   Lines(p,data.Scope,80,1648,120,16,Muted,2,920);
   Text(p,Fit(data.DateRange,920,16),80,1710,16,Muted);
   Text(p,"astroarchive.arijguest.com",80,1745,18,Accent);
   ApplyTheme(p,dark);return p;
  }
  static void PortraitDonut(AnalyticsPage p,List<AnalyticsValue> values,double total,string unit){
   double angle=-Math.PI/2;int colour=0;
   foreach(var value in values){
    double sweep=value.Value/total*2*Math.PI;int steps=Math.Max(2,(int)Math.Ceiling(sweep*60));var points=new List<double>();
    for(int i=0;i<=steps;i++){double a=angle+sweep*i/steps;points.Add(540+190*Math.Cos(a));points.Add(854+190*Math.Sin(a));}
    for(int i=steps;i>=0;i--){double a=angle+sweep*i/steps;points.Add(540+136*Math.Cos(a));points.Add(854+136*Math.Sin(a));}
    p.Marks.Add(new AnalyticsMark{Kind="polygon",Points=points.ToArray(),Fill=Colours[colour%Colours.Length]});
    double y=1070+colour*52;Box(p,90,y+7,18,18,Colours[colour%Colours.Length]);
    Text(p,Fit(value.Label,550,22),126,y,22,Ink,false,value.Label);
    Text(p,Fit(ArchiveAnalytics.Number(value.Value)+" "+(unit=="frames"&&value.Value==1?"frame":unit),175,21),710,y,21,Ink,true);
    Text(p,(value.Value/total*100).ToString("0.#",CultureInfo.InvariantCulture)+"%",910,y+2,18,Muted);
    angle+=sweep;colour++;
   }
   string centre=ArchiveAnalytics.Number(total);double size=TextWidth(centre,50)>220?32:50;
   Text(p,centre,540-TextWidth(centre,size)/2,820,size,Ink,true);
   string label=unit=="h"?"HOURS":"LIGHT FRAMES";Text(p,label,540-TextWidth(label,17)/2,886,17,Muted,true);
  }
  static void PortraitBars(AnalyticsPage p,List<AnalyticsValue> values,string unit,double maximum){
   double max=NiceMax(maximum);const double start=80,width=650;
   for(int i=0;i<values.Count;i++){
    var value=values[i];double y=710+i*92;
    Lines(p,value.Label,start,y,90,21,Ink,2,920);
    Box(p,start,y+50,width,24,"#E8ECF4");Box(p,start,y+50,Math.Max(.5,value.Value/max*width),24,Colours[i%Colours.Length]);
    Text(p,Fit(ArchiveAnalytics.Number(value.Value)+" "+unit,240,21),760,y+48,21,Ink,true);
   }
   for(int i=0;i<=4;i++)Text(p,ArchiveAnalytics.Number(max*i/4),start+width*i/4,1480,16,Muted);
   Text(p,"Integration (hours)",80,1510,16,Muted);
  }
  static void PortraitColumns(AnalyticsPage p,List<AnalyticsValue> source,string unit){
   var values=source.ToList();
   if(values.Count>8){int chunk=(int)Math.Ceiling(values.Count/8.0);values=Enumerable.Range(0,(int)Math.Ceiling(values.Count/(double)chunk)).Select(i=>{var bucket=source.Skip(i*chunk).Take(chunk).ToList();return new AnalyticsValue(bucket[0].Label+"–"+bucket.Last().Label,bucket.Sum(v=>v.Value));}).ToList();}
   double max=NiceMax(values.Max(v=>v.Value));const double left=150,top=760,bottom=1370,width=850;
   for(int i=0;i<=4;i++){double y=bottom-(bottom-top)*i/4;Box(p,left,y,width,1,"#E8ECF4");Text(p,Fit(ArchiveAnalytics.Number(max*i/4),70,18),70,y-10,18,Muted);}
   double cell=width/values.Count;
   for(int i=0;i<values.Count;i++){
    var value=values[i];double h=value.Value/max*(bottom-top),x=left+cell*i+cell*.15;
    if(value.Value>0)Box(p,x,bottom-h,cell*.7,h,Accent);
    Text(p,Fit(ArchiveAnalytics.Number(value.Value),cell-8,16),left+cell*i+4,bottom-h-28,16,Ink,true);
    Lines(p,value.Label,left+cell*i+4,1390,30,16,Muted,3,cell-8);
   }
   Text(p,unit=="h"?"Integration (hours)":"Light frames",80,704,19,Muted);
  }
 }
}
