using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace AstroArchive {
 public sealed class AnalyticsMark {
  public string Kind,Fill,Text,Detail;public double X,Y,Width,Height,Size;public bool Bold;
  public double[] Points;
 }
 public sealed class AnalyticsPage {
  public const double Width=1200,Height=800;
  public string Title;public List<AnalyticsMark> Marks=new List<AnalyticsMark>();
 }
 // One scene supplies preview, raster, SVG and PDF so exported layouts agree.
 public static class AnalyticsGraphics {
  public const string Ink="#202B43",Muted="#5F6D86",Accent="#5951D6";
  static readonly string[] Colours={"#5951D6","#008477","#CB5078","#B37608","#427AB5","#8756A5","#6B813A","#64748B"};
  static void Box(AnalyticsPage p,double x,double y,double w,double h,string colour){p.Marks.Add(new AnalyticsMark{Kind="rect",X=x,Y=y,Width=w,Height=h,Fill=colour});}
  static void Text(AnalyticsPage p,string text,double x,double y,double size,string colour,bool bold=false,string detail=null){p.Marks.Add(new AnalyticsMark{Kind="text",Text=text,X=x,Y=y,Size=size,Fill=colour,Bold=bold,Detail=detail});}
  static string Short(string text,int length){return text.Length<=length?text:text.Substring(0,length-1)+"…";}
  static void Lines(AnalyticsPage p,string text,double x,double y,int characters,double size,string colour,int limit){
   var remaining=text;int line=0;while(remaining.Length>0&&line<limit){int count=Math.Min(characters,remaining.Length);if(count<remaining.Length){int space=remaining.LastIndexOf(' ',count-1,count);if(space>characters/2)count=space;}string part=remaining.Substring(0,count);remaining=remaining.Substring(count).TrimStart();if(line==limit-1&&remaining.Length>0)part=Short(part+" "+remaining,characters);Text(p,part,x,y+line*(size+5),size,colour,false,text);line++;}
  }
  public static AnalyticsPage Page(AnalyticsSnapshot data,int index){
   return Page(data,index,0,1,data.Reports[index].Style=="bars"?data.Reports[index].Values.Take(8).ToList():ArchiveAnalytics.Compact(data.Reports[index].Values,8));
  }
  public static List<AnalyticsPage> Pages(AnalyticsSnapshot data,int index){
   var report=data.Reports[index];int count=report.Style=="bars"?Math.Max(1,(int)Math.Ceiling(report.Values.Count/8.0)):1;
   return Enumerable.Range(0,count).Select(part=>Page(data,index,part,count,report.Style=="bars"?report.Values.Skip(part*8).Take(8).ToList():ArchiveAnalytics.Compact(report.Values,8))).ToList();
  }
  static AnalyticsPage Page(AnalyticsSnapshot data,int index,int part,int parts,List<AnalyticsValue> values){
   var report=data.Reports[index];var p=new AnalyticsPage{Title=report.Title};
   Box(p,0,0,1200,800,"#FFFFFF");Box(p,0,0,1200,8,Accent);
   p.Marks.Add(new AnalyticsMark{Kind="logo",X=60,Y=30,Width=54,Height=54});
   Text(p,"AstroArchive",128,38,23,Ink,true);Text(p,"OBSERVATORY NOTES  /  ANALYTICS",128,67,10,Muted,true);
   Text(p,(index+1).ToString("00",CultureInfo.InvariantCulture)+" / 06"+(parts>1?" · "+(part+1)+"/"+parts:""),parts>1?980:1060,47,17,Accent,true);
   Text(p,report.Title,60,118,34,Ink,true);Text(p,report.Description+(parts>1?" · Groups "+(part*8+1)+"–"+Math.Min((part+1)*8,report.Values.Count)+" of "+report.Values.Count:""),60,165,17,Muted);
   Box(p,60,210,1080,78,"#F5F7FC");
   string[] metrics={ArchiveAnalytics.Number(data.Seconds/3600)+" h",data.Captures.ToString("N0",CultureInfo.InvariantCulture),data.Targets.ToString("N0",CultureInfo.InvariantCulture)};
   string[] captions={"RECORDED INTEGRATION","INDIVIDUAL LIGHT FRAMES","TARGETS"};
   for(int i=0;i<3;i++){Text(p,metrics[i],80+i*360,220,26,Ink,true);Text(p,captions[i],80+i*360,258,10,Muted,true);}
   double total=report.Values.Sum(v=>v.Value);
   if(total<=0){Text(p,data.Captures==0?"No light frames in this scope":"No known exposure data for this chart",60,390,26,Ink,true);Text(p,"Try another telescope or date range, or complete missing capture metadata.",60,433,17,Muted);}
   else if(report.Style=="donut")Donut(p,values,total,report.Unit);
   else if(report.Style=="bars")Bars(p,values,report.Unit,report.Values.Max(v=>v.Value));
   else Columns(p,report.Values,report.Unit);
   Lines(p,report.Note,60,694,133,12,Muted,2);
   Box(p,60,738,1080,1,"#DCE2EF");
   Lines(p,data.Scope,60,752,108,11,Muted,1);
   Text(p,data.DateRange,60,775,10,Muted);
   Text(p,"astroarchive.arijguest.com",910,752,11,Accent);
   Text(p,data.GeneratedUtc.ToString("dd MMM yyyy",CultureInfo.InvariantCulture)+" · UTC",988,773,10,Muted);
   return p;
  }
  static void Donut(AnalyticsPage p,List<AnalyticsValue> values,double total,string unit){
   double angle=-Math.PI/2;int colour=0;foreach(var value in values){
    double sweep=value.Value/total*2*Math.PI;int steps=Math.Max(2,(int)Math.Ceiling(sweep*60));var points=new List<double>();
    for(int i=0;i<=steps;i++){double a=angle+sweep*i/steps;points.Add(265+156*Math.Cos(a));points.Add(477+156*Math.Sin(a));}
    for(int i=steps;i>=0;i--){double a=angle+sweep*i/steps;points.Add(265+110*Math.Cos(a));points.Add(477+110*Math.Sin(a));}
    p.Marks.Add(new AnalyticsMark{Kind="polygon",Points=points.ToArray(),Fill=Colours[colour%Colours.Length]});
    double y=477-values.Count*42/2.0+colour*42;Box(p,485,y+5,12,12,Colours[colour%Colours.Length]);
    string label=Short(value.Label,49);Text(p,label,510,y,15,Ink,false,value.Label);
    Text(p,ArchiveAnalytics.Number(value.Value)+" "+unit,935,y,15,Ink,true);
    Text(p,(value.Value/total*100).ToString("0.#",CultureInfo.InvariantCulture)+"%",1076,y,13,Muted);
    angle+=sweep;colour++;
   }
   string centre=ArchiveAnalytics.Number(total);double size=centre.Length>8?24:34;
   Text(p,centre,265-centre.Length*size*.28,447,size,Ink,true);Text(p,unit=="h"?"HOURS":"LIGHT FRAMES",unit=="h"?244:221,493,12,Muted,true);
   Text(p,"Share of "+(unit=="h"?"recorded integration":"light frames"),60,657,12,Muted);
   if(values.Any(v=>v.Label.StartsWith("Other (")))Text(p,"Largest 7 groups; remaining groups combined",485,657,12,Muted);
  }
  static double NiceMax(double value){double scale=Math.Pow(10,Math.Floor(Math.Log10(value)));double n=value/scale;return (n<=1?1:n<=2?2:n<=5?5:10)*scale;}
  static void Bars(AnalyticsPage p,List<AnalyticsValue> values,string unit,double maximum){
   double max=NiceMax(maximum);const double start=445,width=580;
   for(int i=0;i<=4;i++){double x=start+width*i/4;Box(p,x,323,1,326,"#E8ECF4");Text(p,ArchiveAnalytics.Number(max*i/4),x-5,659,11,Muted);}
   for(int i=0;i<values.Count;i++){
    var value=values[i];double slot=326.0/values.Count,barHeight=Math.Min(44,slot*.65),y=323+i*slot+(slot-barHeight)/2;
    Lines(p,value.Label,60,y+barHeight/2-9,value.Label.Length>80?58:40,value.Label.Length>80?11:15,Ink,2);
    Box(p,start,y,Math.Max(.5,value.Value/max*width),barHeight,Colours[i%Colours.Length]);
    Text(p,ArchiveAnalytics.Number(value.Value)+" "+unit,1050,y+barHeight/2-8,15,Ink,true);
   }
   Text(p,"Integration (hours)",60,658,12,Muted);
  }
  static void Columns(AnalyticsPage p,List<AnalyticsValue> source,string unit){
   var values=source.ToList();if(values.Count>18){int chunk=(int)Math.Ceiling(values.Count/18.0);values=Enumerable.Range(0,(int)Math.Ceiling(values.Count/(double)chunk)).Select(i=>{var bucket=source.Skip(i*chunk).Take(chunk).ToList();return new AnalyticsValue(bucket[0].Label+"–"+bucket.Last().Label,bucket.Sum(v=>v.Value));}).ToList();}
   double max=NiceMax(values.Max(v=>v.Value));const double left=122,top=333,bottom=615,width=988;
   for(int i=0;i<=4;i++){double y=bottom-(bottom-top)*i/4;Box(p,left,y,width,1,"#E8ECF4");Text(p,ArchiveAnalytics.Number(max*i/4),60,y-8,12,Muted);}
   double cell=width/values.Count;
   for(int i=0;i<values.Count;i++){
    var value=values[i];double h=value.Value/max*(bottom-top),x=left+cell*i+cell*.15;
    if(value.Value>0)Box(p,x,bottom-h,cell*.7,h,Accent);
    if(values.Count<=12)Text(p,ArchiveAnalytics.Number(value.Value),x,bottom-h-22,11,Ink,true);
    Lines(p,value.Label,left+cell*i+3,628,(int)Math.Max(5,cell/6),values.Count>12?10:12,Muted,2);
   }
   Text(p,unit=="h"?"Integration (hours)":"Light frames",60,307,12,Muted);
  }
  public static string Svg(IList<AnalyticsPage> pages,string logoBase64){
   bool all=pages.Count>1;double width=all?AnalyticsPage.Width*2:AnalyticsPage.Width,height=all?AnalyticsPage.Height*Math.Ceiling(pages.Count/2.0):AnalyticsPage.Height;
   var b=new StringBuilder();using(var writer=XmlWriter.Create(b,new XmlWriterSettings{OmitXmlDeclaration=true,Indent=true})){
    writer.WriteStartElement("svg","http://www.w3.org/2000/svg");writer.WriteAttributeString("width",N(width));writer.WriteAttributeString("height",N(height));writer.WriteAttributeString("viewBox","0 0 "+N(width)+" "+N(height));
    writer.WriteElementString("title",all?"AstroArchive · Analytics collection":pages[0].Title);
    writer.WriteElementString("desc","Individual light-frame analytics. Time is recorded integration, not elapsed observing time. Stacks, videos and calibration files are excluded.");
    writer.WriteStartElement("defs");writer.WriteStartElement("image");writer.WriteAttributeString("id","astroarchive-logo");writer.WriteAttributeString("width","1");writer.WriteAttributeString("height","1");writer.WriteAttributeString("href","http://www.w3.org/1999/xlink","data:image/png;base64,"+logoBase64);writer.WriteEndElement();writer.WriteEndElement();
    writer.WriteStartElement("rect");writer.WriteAttributeString("width",N(width));writer.WriteAttributeString("height",N(height));writer.WriteAttributeString("fill","#FFFFFF");writer.WriteEndElement();
    for(int i=0;i<pages.Count;i++){
     writer.WriteStartElement("g");writer.WriteAttributeString("transform","translate("+N(all?i%2*AnalyticsPage.Width:0)+","+N(all?i/2*AnalyticsPage.Height:0)+")");
     foreach(var mark in pages[i].Marks){
      if(mark.Kind=="logo"){writer.WriteStartElement("use");writer.WriteAttributeString("href","http://www.w3.org/1999/xlink","#astroarchive-logo");writer.WriteAttributeString("transform","translate("+N(mark.X)+","+N(mark.Y)+") scale("+N(mark.Width)+","+N(mark.Height)+")");writer.WriteEndElement();continue;}
      string kind=mark.Kind;writer.WriteStartElement(kind);
      if(kind=="polygon"){var points=new StringBuilder();for(int j=0;j<mark.Points.Length;j+=2)points.Append(N(mark.Points[j])+","+N(mark.Points[j+1])+" ");writer.WriteAttributeString("points",points.ToString().Trim());}
      else{writer.WriteAttributeString("x",N(mark.X));writer.WriteAttributeString("y",N(kind=="text"?mark.Y+mark.Size*.91:mark.Y));}
      if(kind=="rect"){writer.WriteAttributeString("width",N(mark.Width));writer.WriteAttributeString("height",N(mark.Height));}
      writer.WriteAttributeString("fill",mark.Fill);
      if(kind=="text"){writer.WriteAttributeString("font-family","Arial, sans-serif");writer.WriteAttributeString("font-size",N(mark.Size));writer.WriteAttributeString("font-weight",mark.Bold?"700":"400");}
      if(mark.Detail!=null)writer.WriteElementString("title",mark.Detail);
      if(kind=="text")writer.WriteString(mark.Text);
      writer.WriteEndElement();
     }writer.WriteEndElement();
    }writer.WriteEndElement();
   }return b.ToString();
  }
  public static string N(double n){return n.ToString("0.###",CultureInfo.InvariantCulture);}
 }
}
