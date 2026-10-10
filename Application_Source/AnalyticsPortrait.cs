using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 public enum AnalyticsLayout {Landscape,Vertical,Portrait,Square,Widescreen,Pinterest}
 public static partial class AnalyticsGraphics {
  public static void LayoutSize(AnalyticsLayout layout,out double width,out double height){
   width=layout==AnalyticsLayout.Landscape?1200:layout==AnalyticsLayout.Widescreen?1920:layout==AnalyticsLayout.Pinterest?1000:1080;
   height=layout==AnalyticsLayout.Landscape?800:layout==AnalyticsLayout.Vertical?1920:layout==AnalyticsLayout.Portrait?1350:layout==AnalyticsLayout.Pinterest?1500:1080;
  }
  static AnalyticsPage SocialPage(AnalyticsSnapshot data,int index,int part,int parts,List<AnalyticsValue> values,bool dark,AnalyticsLayout layout){
   if(layout==AnalyticsLayout.Vertical)return PortraitPage(data,index,part,parts,values,dark);
   if(layout==AnalyticsLayout.Widescreen){
    var wide=Page(data,index,part,parts,values,dark,AnalyticsLayout.Landscape);ScaleScene(wide,1.35,150);wide.CanvasWidth=1920;wide.CanvasHeight=1080;
    wide.Marks[0].X=0;wide.Marks[0].Width=1920;wide.Marks[1].X=0;wide.Marks[1].Width=1920;return wide;
   }
   var p=CompactSocialPage(data,index,part,parts,values,dark,layout==AnalyticsLayout.Square?1080:layout==AnalyticsLayout.Pinterest?1620:1350);
   if(layout==AnalyticsLayout.Pinterest){ScaleScene(p,1000.0/1080,0);p.CanvasWidth=1000;p.CanvasHeight=1500;}return p;
  }
  static void ScaleScene(AnalyticsPage page,double scale,double offsetX){
   foreach(var mark in page.Marks){mark.X=mark.X*scale+offsetX;mark.Y*=scale;mark.Width*=scale;mark.Height*=scale;mark.Size*=scale;mark.Radius*=scale;if(mark.Points!=null)for(int i=0;i<mark.Points.Length;i+=2){mark.Points[i]=mark.Points[i]*scale+offsetX;mark.Points[i+1]*=scale;}}
  }
  static AnalyticsPage CompactSocialPage(AnalyticsSnapshot data,int index,int part,int parts,List<AnalyticsValue> values,bool dark,double height){
   var report=data.Reports[index];var p=new AnalyticsPage{Title=report.Title,CanvasWidth=1080,CanvasHeight=height,Background=dark?"#0C1220":"#FFFFFF"};
   Box(p,0,0,1080,height,"#FFFFFF");Box(p,0,0,1080,6,Accent);p.Marks.Add(new AnalyticsMark{Kind="logo",X=60,Y=28,Width=64,Height=64});
   Text(p,"AstroArchive",140,44,26,Ink,true);Text(p,(index+1).ToString("00")+" / 06"+(parts>1?" · "+(part+1)+"/"+parts:""),parts>1?850:932,50,16,Accent,true);
   Text(p,report.Title,60,120,44,Ink,true);Lines(p,report.Description+(parts>1?" · Groups "+(part*8+1)+"–"+Math.Min((part+1)*8,report.Values.Count)+" of "+report.Values.Count:""),60,180,120,18,Muted,2,960);
   Box(p,60,244,960,86,"#F5F7FC");string[] metrics={ArchiveAnalytics.Number(data.RepositorySeconds/3600)+" h",data.RepositoryCaptures.ToString("N0",CultureInfo.InvariantCulture),data.RepositoryTargets.ToString("N0",CultureInfo.InvariantCulture)},captions={"REPOSITORY INTEGRATION","LIGHT FRAMES","REPOSITORY TARGETS"};
   for(int i=0;i<3;i++){Text(p,metrics[i],80+i*320,254,28,Ink,true);Text(p,captions[i],80+i*320,298,13,Muted,true);}
   p.Role="chart";double total=report.Values.Sum(v=>v.Value);
   if(total<=0){Lines(p,data.Captures==0?"No light frames in this scope":"No known exposure data for this chart",60,490,70,28,Ink,2,960);Lines(p,"Try another telescope or date range, or complete missing capture metadata.",60,575,110,19,Muted,3,960);}
   else if(report.Style=="donut")PortraitDonut(p,values,total,report.Unit,true);
   else if(report.Style=="bars")PortraitBars(p,values,report.Unit,report.Values.Max(v=>v.Value),true);
   else PortraitColumns(p,report.Values,report.Unit,true);
   p.Role="footer";Lines(p,report.Note,60,height-155,150,14,Muted,2,960);Box(p,60,height-90,960,1,"#DCE2EF");Lines(p,"Charts · "+data.Scope+" · "+data.Captures.ToString("N0",CultureInfo.InvariantCulture)+" frames · "+ArchiveAnalytics.Number(data.Seconds/3600)+" h",60,height-76,140,12,Muted,1,960);
   Text(p,Fit(data.DateRange,680,12),60,height-42,12,Muted);Text(p,"astroarchive.arijguest.com",800,height-24,12,Accent);ApplyTheme(p,dark);Polish(p,dark,index);return p;
  }
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
   Lines(p,report.Title,80,238,60,52,Ink,2,920,true);
   string description=report.Description+(parts>1?" · Groups "+(part*8+1)+"–"+Math.Min((part+1)*8,report.Values.Count)+" of "+report.Values.Count:"");
   Lines(p,description,80,318,80,23,Muted,2,920);
   Box(p,80,510,920,134,"#F5F7FC");
   string[] metrics={ArchiveAnalytics.Number(data.RepositorySeconds/3600)+" h",data.RepositoryCaptures.ToString("N0",CultureInfo.InvariantCulture),data.RepositoryTargets.ToString("N0",CultureInfo.InvariantCulture)};
   string[] captions={"REPOSITORY INTEGRATION","LIGHT FRAMES","REPOSITORY TARGETS"};
   for(int i=0;i<3;i++){double x=100+i*306;double size=TextWidth(metrics[i],38)>270?28:38;Text(p,metrics[i],x,532,size,Ink,true);Text(p,captions[i],x,594,16,Muted,true);}
   p.Role="chart";double total=report.Values.Sum(v=>v.Value);
   if(total<=0){
    Lines(p,data.Captures==0?"No light frames in this scope":"No known exposure data for this chart",80,900,60,32,Ink,2,920);
    Lines(p,"Try another telescope or date range, or complete missing capture metadata.",80,1010,80,23,Muted,3,920);
   }else if(report.Style=="donut")PortraitDonut(p,values,total,report.Unit);
   else if(report.Style=="bars")PortraitBars(p,values,report.Unit,report.Values.Max(v=>v.Value));
   else PortraitColumns(p,report.Values,report.Unit);
   p.Role="footer";Lines(p,report.Note,80,1540,110,18,Muted,3,920);
   Box(p,80,1630,920,1,"#DCE2EF");
   Lines(p,"Charts · "+data.Scope+" · "+data.Captures.ToString("N0",CultureInfo.InvariantCulture)+" frames · "+ArchiveAnalytics.Number(data.Seconds/3600)+" h",80,1648,120,16,Muted,2,920);
   Text(p,Fit(data.DateRange,920,16),80,1710,16,Muted);
   Text(p,"astroarchive.arijguest.com",80,1745,18,Accent);
   ApplyTheme(p,dark);Polish(p,dark,index);return p;
  }
  static void PortraitDonut(AnalyticsPage p,List<AnalyticsValue> values,double total,string unit,bool compact=false){
   bool sideLegend=compact&&p.CanvasHeight<1500;
   double cx=sideLegend?285:540,cy=sideLegend?(370+p.CanvasHeight-210)/2:compact?640:810,radius=sideLegend?170:240,inner=sideLegend?120:170;
   RingTrack(p,cx,cy,radius,inner);double angle=-Math.PI/2;int colour=0;
   foreach(var value in values){
    double sweep=value.Value/total*2*Math.PI;int steps=Math.Max(2,(int)Math.Ceiling(sweep*60));var points=new List<double>();
    for(int i=0;i<=steps;i++){double gap=Math.Min(.012,sweep*.12);double a=angle+gap+(sweep-2*gap)*i/steps;points.Add(cx+radius*Math.Cos(a));points.Add(cy+radius*Math.Sin(a));}
    for(int i=steps;i>=0;i--){double gap=Math.Min(.012,sweep*.12);double a=angle+gap+(sweep-2*gap)*i/steps;points.Add(cx+inner*Math.Cos(a));points.Add(cy+inner*Math.Sin(a));}
    p.Marks.Add(new AnalyticsMark{Kind="polygon",Points=points.ToArray(),Fill=Colours[colour%Colours.Length],Role="chart"});
    double y=sideLegend?cy-values.Count*58/2.0+colour*58:(compact?925:1070)+colour*52;Box(p,sideLegend?510:90,y+7,sideLegend?14:18,sideLegend?14:18,Colours[colour%Colours.Length]);
    Text(p,Fit(value.Label,sideLegend?460:550,sideLegend?18:22),sideLegend?540:126,y,sideLegend?18:22,Ink,false,value.Label);
    Text(p,Fit(ArchiveAnalytics.Number(value.Value)+" "+(unit=="frames"&&value.Value==1?"frame":unit),sideLegend?320:175,sideLegend?16:21),sideLegend?540:710,y+(sideLegend?25:0),sideLegend?16:21,Ink,true);
    Text(p,(value.Value/total*100).ToString("0.#",CultureInfo.InvariantCulture)+"%",sideLegend?930:910,y+(sideLegend?25:2),sideLegend?16:18,Muted);
    angle+=sweep;colour++;
   }
   string centre=ArchiveAnalytics.Number(total);double size=sideLegend?42:TextWidth(centre,56)>240?36:56;
   Text(p,centre,cx-TextWidth(centre,size)/2,cy-34,size,Ink,true);
   string label=unit=="h"?"HOURS":"LIGHT FRAMES";Text(p,label,cx-TextWidth(label,17)/2,cy+32,17,Muted,true);
  }
  static void PortraitBars(AnalyticsPage p,List<AnalyticsValue> values,string unit,double maximum,bool compact=false){
   double max=NiceMax(maximum);const double start=80,width=650;
   for(int i=0;i<values.Count;i++){
    var value=values[i];double y=compact?370+i*Math.Min(92,(p.CanvasHeight-580)/values.Count):710+i*92,offset=compact?29:50,barHeight=compact?18:24;
    if(compact)Text(p,Fit(value.Label,920,18),start,y,18,Ink,false,value.Label);else Lines(p,value.Label,start,y,90,21,Ink,2,920);
    Box(p,start,y+offset,width,barHeight,"#E8ECF4");Box(p,start,y+offset,Math.Max(.5,value.Value/max*width),barHeight,Colours[i%Colours.Length]);
    p.Marks.Last().Animation="bar";
    Text(p,Fit(ArchiveAnalytics.Number(value.Value)+" "+unit,240,compact?18:21),760,y+offset-2,compact?18:21,Ink,true);
   }
   for(int i=0;i<=4;i++)Text(p,ArchiveAnalytics.Number(max*i/4),start+width*i/4,compact?p.CanvasHeight-210:1480,16,Muted);
   Text(p,"Integration (hours)",80,compact?p.CanvasHeight-185:1510,16,Muted);
  }
  static void PortraitColumns(AnalyticsPage p,List<AnalyticsValue> source,string unit,bool compact=false){
   var values=source.ToList();
   if(values.Count>8){int chunk=(int)Math.Ceiling(values.Count/8.0);values=Enumerable.Range(0,(int)Math.Ceiling(values.Count/(double)chunk)).Select(i=>{var bucket=source.Skip(i*chunk).Take(chunk).ToList();return new AnalyticsValue(bucket[0].Label+"–"+bucket.Last().Label,bucket.Sum(v=>v.Value));}).ToList();}
   double max=NiceMax(values.Max(v=>v.Value)),left=150,top=compact?400:760,bottom=compact?p.CanvasHeight-285:1370,width=850;
   for(int i=0;i<=4;i++){double y=bottom-(bottom-top)*i/4;Box(p,left,y,width,1,"#E8ECF4");Text(p,Fit(ArchiveAnalytics.Number(max*i/4),70,18),70,y-10,18,Muted);}
   double cell=width/values.Count;
   for(int i=0;i<values.Count;i++){
    var value=values[i];double h=value.Value/max*(bottom-top),x=left+cell*i+cell*.15;
    if(value.Value>0){Box(p,x,bottom-h,cell*.7,h,unit=="h"?Accent:Colours[i%Colours.Length]);p.Marks.Last().Animation="column";}
    Text(p,Fit(ArchiveAnalytics.Number(value.Value),cell-8,16),left+cell*i+4,bottom-h-28,16,Ink,true);
    Lines(p,value.Label,left+cell*i+4,bottom+20,30,16,Muted,3,cell-8);
   }
   Text(p,unit=="h"?"Integration (hours)":"Light frames",80,compact?354:704,19,Muted);
  }
 }
}
