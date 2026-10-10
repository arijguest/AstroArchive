using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace AstroArchive {
 public sealed class AnalyticsMark {
  public string Kind,Fill,FillEnd,Text,Detail,Role,Animation;public double X,Y,Width,Height,Size,Radius;public bool Bold,VerticalGradient;
  public double[] Points;
 }
 public sealed class AnalyticsPage {
  public const double Width=1200,Height=800;
  public double CanvasWidth=Width,CanvasHeight=Height;
  public bool Portrait {get{return CanvasHeight>CanvasWidth;}}
  public string Title,Background="#0C1220",Role="header";public List<AnalyticsMark> Marks=new List<AnalyticsMark>();
 }
 // One scene supplies preview, raster, SVG and PDF so exported layouts agree.
 public static partial class AnalyticsGraphics {
  public const string Ink="#202B43",Muted="#5F6D86",Accent="#5951D6";
  static readonly string[] Colours={"#5951D6","#008477","#CB5078","#B37608","#427AB5","#8756A5","#6B813A","#64748B"};
  static void Box(AnalyticsPage p,double x,double y,double w,double h,string colour){p.Marks.Add(new AnalyticsMark{Kind="rect",X=x,Y=y,Width=w,Height=h,Fill=colour,Role=p.Role});}
  static void Text(AnalyticsPage p,string text,double x,double y,double size,string colour,bool bold=false,string detail=null){p.Marks.Add(new AnalyticsMark{Kind="text",Text=text,X=x,Y=y,Size=size,Fill=colour,Bold=bold,Detail=detail,Role=p.Role});}
  static string Short(string text,int length){if(text.Length<=length)return text;int count=length-1;if(count>0&&char.IsHighSurrogate(text[count-1]))count--;return text.Substring(0,count)+"…";}
  static double TextWidth(string text,double size){return text.Sum(c=>char.IsSurrogate(c)?.6:c>=0x3000?1:"MWmw@%".IndexOf(c)>=0?.95:"ilI.,:;!| '".IndexOf(c)>=0?.3:char.IsUpper(c)?.75:.6)*size;}
  static string Fit(string text,double width,double size){int length=text.Length;while(length>1&&TextWidth(Short(text,length),size)>width)length--;return Short(text,length);}
  static void Lines(AnalyticsPage p,string text,double x,double y,int characters,double size,string colour,int limit,double width=0,bool bold=false){
   var remaining=text??"";int line=0;while(remaining.Length>0&&line<limit){int count=Math.Min(characters,remaining.Length);if(width>0)while(count>1&&TextWidth(remaining.Substring(0,count),size)>width)count--;if(count<remaining.Length){int space=remaining.LastIndexOf(' ',count-1,count);if(space>count/2)count=space;if(count>0&&char.IsHighSurrogate(remaining[count-1]))count--;}string part=remaining.Substring(0,count);remaining=remaining.Substring(count).TrimStart();if(line==limit-1&&remaining.Length>0){part=Short(part+" "+remaining,characters);if(width>0)part=Fit(part,width,size);}Text(p,part,x,y+line*(size+5),size,colour,bold,text);line++;}
  }
  public static AnalyticsPage Page(AnalyticsSnapshot data,int index,bool dark=true,AnalyticsLayout layout=AnalyticsLayout.Landscape,AnalyticsChartOptions options=null){
   return Pages(data,index,dark,layout,options)[0];
  }
  public static List<AnalyticsPage> Pages(AnalyticsSnapshot data,int index,bool dark=true,AnalyticsLayout layout=AnalyticsLayout.Landscape,AnalyticsChartOptions options=null){
   data=Configure(data,index,options);int limit=options!=null&&(options.Categories==4||options.Categories==6)?options.Categories:8;
   var report=data.Reports[index];int count=report.Style=="bars"?Math.Max(1,(int)Math.Ceiling(report.Values.Count/(double)limit)):1;
   var pages=Enumerable.Range(0,count).Select(part=>Page(data,index,part,count,report.Style=="bars"?report.Values.Skip(part*limit).Take(limit).ToList():ArchiveAnalytics.Compact(report.Values,limit),dark,layout,limit)).ToList();foreach(var page in pages)ConfigurePalette(page,dark,options);return pages;
  }
  static AnalyticsPage Page(AnalyticsSnapshot data,int index,int part,int parts,List<AnalyticsValue> values,bool dark,AnalyticsLayout layout,int limit=8){
   if(layout!=AnalyticsLayout.Landscape)return SocialPage(data,index,part,parts,values,dark,layout,limit);
   var report=data.Reports[index];var p=new AnalyticsPage{Title=report.Title,Background=dark?"#0C1220":"#FFFFFF"};
   Box(p,0,0,1200,800,"#FFFFFF");Box(p,0,0,1200,8,Accent);
   p.Marks.Add(new AnalyticsMark{Kind="logo",X=56,Y=24,Width=70,Height=70});
   Text(p,"AstroArchive",142,46,24,Ink,true);
   Text(p,(index+1).ToString("00",CultureInfo.InvariantCulture)+" / 06"+(parts>1?" · "+(part+1)+"/"+parts:""),parts>1?980:1060,47,17,Accent,true);
   double titleSize=Math.Max(24,Math.Min(42,1080/Math.Max(1,TextWidth(report.Title,1))));Text(p,Fit(report.Title,1080,titleSize),60,112,titleSize,Ink,true,report.Title);Text(p,Fit(report.Description+(parts>1?" · Groups "+(part*limit+1)+"–"+Math.Min((part+1)*limit,report.Values.Count)+" of "+report.Values.Count:""),1080,17),60,165,17,Muted);
   Box(p,60,210,1080,78,"#F5F7FC");
   string[] metrics={ArchiveAnalytics.Number(data.RepositorySeconds/3600)+" h",data.RepositoryCaptures.ToString("N0",CultureInfo.InvariantCulture),data.RepositoryTargets.ToString("N0",CultureInfo.InvariantCulture)};
   string[] captions={"REPOSITORY INTEGRATION","LIGHT FRAMES IN REPOSITORY","TARGETS IN REPOSITORY"};
   for(int i=0;i<3;i++){Text(p,metrics[i],80+i*360,220,26,Ink,true);Text(p,captions[i],80+i*360,258,10,Muted,true);}
   p.Role="chart";double total=report.Values.Sum(v=>v.Value);
   if(total<=0){Text(p,data.Captures==0?"No light frames in this scope":"No known exposure data for this chart",60,390,26,Ink,true);Text(p,"Try another telescope or date range, or complete missing capture metadata.",60,433,17,Muted);}
   else if(report.Style=="donut")Donut(p,values,total,report.Unit);
   else if(report.Style=="bars")Bars(p,values,report.Unit,report.Values.Max(v=>v.Value));
   else Columns(p,report.Values,report.Unit);
   // Keep interpretation and missing-data caveats, without repeating the brand
   // or technical export details around the chart.
   p.Role="footer";Lines(p,report.Note,60,700,133,12,Muted,2,1080);
   Box(p,60,738,1080,1,"#DCE2EF");
   Lines(p,"Charts · "+data.Scope+" · "+data.Captures.ToString("N0",CultureInfo.InvariantCulture)+" frames · "+ArchiveAnalytics.Number(data.Seconds/3600)+" h",60,752,108,11,Muted,1,820);
   Text(p,data.DateRange,60,775,10,Muted);
   Text(p,"astroarchive.arijguest.com",910,762,11,Accent);
   ApplyTheme(p,dark);Polish(p,dark,index);return p;
  }
  static void ApplyTheme(AnalyticsPage p,bool dark){
   if(dark){
    var colours=new Dictionary<string,string>{{"#FFFFFF",p.Background},{Ink,"#E9EEF8"},{Muted,"#A6B4CC"},{"#F5F7FC","#151E2E"},{"#DCE2EF","#34415A"},{"#E8ECF4","#28354B"}};
    string[] branded={"#90B8FF","#55DFEA","#C19AFF","#F0C36A","#4F9BFA","#ED9ACB","#9DD7B0","#A6B4CC"};
    for(int i=0;i<Colours.Length;i++)colours[Colours[i]]=branded[i];
    foreach(var mark in p.Marks){string replacement;if(mark.Fill!=null&&colours.TryGetValue(mark.Fill,out replacement))mark.Fill=replacement;}
   }
  }
  static void Donut(AnalyticsPage p,List<AnalyticsValue> values,double total,string unit){
   const double radius=175,inner=123;RingTrack(p,265,477,radius,inner);double angle=-Math.PI/2;int colour=0;foreach(var value in values){
    double sweep=value.Value/total*2*Math.PI;int steps=Math.Max(2,(int)Math.Ceiling(sweep*60));var points=new List<double>();
    for(int i=0;i<=steps;i++){double gap=Math.Min(.012,sweep*.12);double a=angle+gap+(sweep-2*gap)*i/steps;points.Add(265+radius*Math.Cos(a));points.Add(477+radius*Math.Sin(a));}
    for(int i=steps;i>=0;i--){double gap=Math.Min(.012,sweep*.12);double a=angle+gap+(sweep-2*gap)*i/steps;points.Add(265+inner*Math.Cos(a));points.Add(477+inner*Math.Sin(a));}
    p.Marks.Add(new AnalyticsMark{Kind="polygon",Points=points.ToArray(),Fill=Colours[colour%Colours.Length],Role="chart"});
    double slot=values.Count<=4?64:42,y=477-values.Count*slot/2.0+colour*slot;Box(p,485,y+5,12,12,Colours[colour%Colours.Length]);
    string label=Fit(Short(value.Label,49),390,15);Text(p,label,510,y,15,Ink,false,value.Label);
    Text(p,ArchiveAnalytics.Number(value.Value)+" "+(unit=="frames"&&value.Value==1?"frame":unit),935,y,15,Ink,true);
    Text(p,(value.Value/total*100).ToString("0.#",CultureInfo.InvariantCulture)+"%",1076,y,13,Muted);
    angle+=sweep;colour++;
   }
   string centre=ArchiveAnalytics.Number(total);double size=centre.Length>8?28:42;
   Text(p,centre,265-TextWidth(centre,size)/2,440,size,Ink,true);Text(p,unit=="h"?"HOURS":"LIGHT FRAMES",unit=="h"?244:221,493,12,Muted,true);
   if(values.Any(v=>v.Label.StartsWith("Other (")))Text(p,"Remaining groups combined in Other",485,657,12,Muted);
  }
  static double NiceMax(double value){double scale=Math.Pow(10,Math.Floor(Math.Log10(value)));double n=value/scale;return (n<=1?1:n<=2?2:n<=5?5:10)*scale;}
  static void Bars(AnalyticsPage p,List<AnalyticsValue> values,string unit,double maximum){
   double max=NiceMax(maximum);const double start=445,width=580;
   for(int i=0;i<=4;i++){double x=start+width*i/4;Box(p,x,323,1,326,"#E8ECF4");Text(p,ArchiveAnalytics.Number(max*i/4),x-5,659,11,Muted);}
   for(int i=0;i<values.Count;i++){
    var value=values[i];double slot=326.0/values.Count,barHeight=Math.Min(70,slot*.65),y=323+i*slot+(slot-barHeight)/2;
    Lines(p,value.Label,60,y+barHeight/2-9,value.Label.Length>80?58:40,value.Label.Length>80?11:15,Ink,2,360);
    Box(p,start,y,Math.Max(.5,value.Value/max*width),barHeight,Colours[i%Colours.Length]);
    p.Marks.Last().Animation="bar";
    Text(p,ArchiveAnalytics.Number(value.Value)+" "+unit,1050,y+barHeight/2-8,15,Ink,true);
   }
   Text(p,unit=="h"?"Integration (hours)":"Light frames",60,658,12,Muted);
  }
  static void Columns(AnalyticsPage p,List<AnalyticsValue> source,string unit){
   var values=source.ToList();if(values.Count>18){int chunk=(int)Math.Ceiling(values.Count/18.0);values=Enumerable.Range(0,(int)Math.Ceiling(values.Count/(double)chunk)).Select(i=>{var bucket=source.Skip(i*chunk).Take(chunk).ToList();return new AnalyticsValue(bucket[0].Label+"–"+bucket.Last().Label,bucket.Sum(v=>v.Value));}).ToList();}
   double max=NiceMax(values.Max(v=>v.Value));const double left=122,top=333,bottom=615,width=988;
   for(int i=0;i<=4;i++){double y=bottom-(bottom-top)*i/4;Box(p,left,y,width,1,"#E8ECF4");Text(p,ArchiveAnalytics.Number(max*i/4),60,y-8,12,Muted);}
   double cell=width/values.Count;
   for(int i=0;i<values.Count;i++){
    var value=values[i];double h=value.Value/max*(bottom-top),x=left+cell*i+cell*.15;
    if(value.Value>0){Box(p,x,bottom-h,cell*.7,h,unit=="h"?Accent:Colours[i%Colours.Length]);p.Marks.Last().Animation="column";}
    if(values.Count<=12)Text(p,ArchiveAnalytics.Number(value.Value),x,bottom-h-22,11,Ink,true);
    Lines(p,value.Label,left+cell*i+3,628,(int)Math.Max(5,cell/6),values.Count>12?10:12,Muted,2);
   }
   Text(p,unit=="h"?"Integration (hours)":"Light frames",60,307,12,Muted);
  }
  public static string Svg(IList<AnalyticsPage> pages,string logoBase64){
   bool all=pages.Count>1;int columns=SheetColumns(pages);double width,height;SheetSize(pages,out width,out height);
   var b=new StringBuilder();using(var writer=XmlWriter.Create(b,new XmlWriterSettings{OmitXmlDeclaration=true,Indent=true})){
    writer.WriteStartElement("svg","http://www.w3.org/2000/svg");writer.WriteAttributeString("width",N(width));writer.WriteAttributeString("height",N(height));writer.WriteAttributeString("viewBox","0 0 "+N(width)+" "+N(height));
    writer.WriteElementString("title",all?"AstroArchive · Analytics collection":pages[0].Title);
    writer.WriteElementString("desc","Individual light-frame analytics. Time is recorded integration, not elapsed observing time. Stacks, videos and calibration files are excluded.");
    writer.WriteStartElement("defs");writer.WriteStartElement("image");writer.WriteAttributeString("id","astroarchive-logo");writer.WriteAttributeString("width","1");writer.WriteAttributeString("height","1");writer.WriteAttributeString("href","http://www.w3.org/1999/xlink","data:image/png;base64,"+logoBase64);writer.WriteEndElement();
    for(int page=0;page<pages.Count;page++)for(int m=0;m<pages[page].Marks.Count;m++){var mark=pages[page].Marks[m];if(mark.FillEnd==null)continue;writer.WriteStartElement("linearGradient");writer.WriteAttributeString("id","g"+page+"-"+m);writer.WriteAttributeString("x2",mark.VerticalGradient?"0%":"100%");writer.WriteAttributeString("y2",mark.VerticalGradient?"100%":"0%");foreach(bool end in new[]{false,true}){writer.WriteStartElement("stop");writer.WriteAttributeString("offset",end?"100%":"0%");writer.WriteAttributeString("stop-color",end?mark.FillEnd:mark.Fill);writer.WriteEndElement();}writer.WriteEndElement();}writer.WriteEndElement();
    writer.WriteStartElement("rect");writer.WriteAttributeString("width",N(width));writer.WriteAttributeString("height",N(height));writer.WriteAttributeString("fill",pages[0].Background);writer.WriteEndElement();
    for(int i=0;i<pages.Count;i++){
     writer.WriteStartElement("g");writer.WriteAttributeString("transform","translate("+N(i%columns*pages[i].CanvasWidth)+","+N(i/columns*pages[i].CanvasHeight)+")");
     for(int m=0;m<pages[i].Marks.Count;m++){var mark=pages[i].Marks[m];
      if(mark.Kind=="logo"){writer.WriteStartElement("use");writer.WriteAttributeString("href","http://www.w3.org/1999/xlink","#astroarchive-logo");writer.WriteAttributeString("transform","translate("+N(mark.X)+","+N(mark.Y)+") scale("+N(mark.Width)+","+N(mark.Height)+")");writer.WriteEndElement();continue;}
      string kind=mark.Kind;writer.WriteStartElement(kind);
      if(kind=="polygon"){var points=new StringBuilder();for(int j=0;j<mark.Points.Length;j+=2)points.Append(N(mark.Points[j])+","+N(mark.Points[j+1])+" ");writer.WriteAttributeString("points",points.ToString().Trim());}
      else{writer.WriteAttributeString("x",N(mark.X));writer.WriteAttributeString("y",N(kind=="text"?mark.Y+mark.Size*.91:mark.Y));}
      if(kind=="rect"){writer.WriteAttributeString("width",N(mark.Width));writer.WriteAttributeString("height",N(mark.Height));if(mark.Radius>0)writer.WriteAttributeString("rx",N(mark.Radius));}
      writer.WriteAttributeString("fill",mark.FillEnd==null?mark.Fill:"url(#g"+i+"-"+m+")");
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
