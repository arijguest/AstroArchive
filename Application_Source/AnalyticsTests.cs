using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace AstroArchive {
 public partial class Tests {
  static Frame AnalyticsLight(string target,double? seconds,string date="2026-10-01",string telescope="Scope A"){
   return new Frame{Target=target,Kind="Light",Exposure=seconds,AcquisitionDate=date,Telescope=telescope,Filter="L"};
  }
  static void AnalyticsTests(){
   Test("Custom chart configurations preserve repository totals categories and chronology",()=>{
    var data=ArchiveAnalytics.Build(Enumerable.Range(0,19).Select(i=>AnalyticsLight("Custom "+i,(i+1)*60)),new AnalyticsOptions());string original=Util.Serialize(data);
    foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout)))foreach(bool dark in new[]{true,false}){
     var options=new AnalyticsChartOptions{Title="My observatory",Subtitle="The sky this season",Style="Bars",Categories=4,Order="Alphabetical",Palette="Nebula"};
     var pages=AnalyticsGraphics.Pages(data,0,dark,layout,options);Check(pages.Count==5&&pages.All(p=>p.Title==options.Title),"Custom ranking pagination or title was lost");
     foreach(var value in data.Reports[0].Values)Check(pages.SelectMany(p=>p.Marks).Any(m=>m.Detail==value.Label),"Custom ranking dropped a category");
     Check(pages.All(p=>p.Marks.Any(m=>m.Role=="header"&&m.Text=="19")),"Customization changed repository headline totals");
     options.Style="Donut";var donut=AnalyticsGraphics.Pages(data,2,dark,layout,options);Check(donut.Count==1&&donut[0].Marks.Any(m=>m.Detail!=null&&m.Detail.StartsWith("Other (")),"Custom donut lost remaining categories");
     var timeline=AnalyticsGraphics.Pages(data,1,dark,layout,options);Check(timeline[0].Marks.Any(m=>m.Animation=="column")&&!timeline[0].Marks.Any(m=>m.Animation=="bar"),"Style override broke chronological charts");
    }
    Check(Util.Serialize(data)==original,"Custom settings mutated the shared snapshot");
    var settings=new Settings{AnalyticsCharts=new Dictionary<string,AnalyticsChartOptions>{{"targets",new AnalyticsChartOptions{Title="Saved chart",Categories=6,Palette="Aurora"}}}};var restored=Util.Deserialize<Settings>(Util.Serialize(settings));Check(restored.AnalyticsCharts["targets"].Title=="Saved chart"&&restored.AnalyticsCharts["targets"].Categories==6,"Custom chart settings did not survive settings persistence");
   });
   Test("Video timing scales reveals and transitions to exact total duration",()=>{
    foreach(int scenes in new[]{1,6,20})foreach(double duration in new[]{.125,1,9,24,300}){
     var timing=new AnalyticsMotionTiming(scenes,4,duration);Check(timing.Duration==duration&&Math.Abs(timing.SceneSeconds*scenes-duration)<1e-9,"Custom duration changed with continuation pages");Check(timing.TransitionSeconds>0&&timing.RevealSeconds+timing.StaggerSeconds<timing.SceneSeconds*.6&&timing.TransitionSeconds<timing.SceneSeconds*.2,"Animation consumed the reading hold in a short clip");
    }
    Check(new AnalyticsMotionTiming(6,4).Duration==24,"Existing pace preset changed");Expect(()=>new AnalyticsMotionTiming(6,4,double.NaN),"Invalid duration accepted");
   });
   Test("Repository headline totals survive every chart scope and missing date",()=>{
    var rejected=AnalyticsLight("M45",3600,"2025-01-01","Scope B");rejected.Rejected=true;
    var deleted=AnalyticsLight("M51",7200);deleted.Status="Deleted";
    var source=new[]{AnalyticsLight("M31",1800),rejected,AnalyticsLight("M42",900,null,null),AnalyticsLight("M81",null),deleted,new Frame{Target="M31",Kind="Stack",Exposure=9000},new Frame{Kind="Video",Exposure=9000}};
    foreach(var options in new[]{new AnalyticsOptions(),new AnalyticsOptions{Telescope="Scope A",From=new DateTime(2026,10,1)},new AnalyticsOptions{UnknownTelescopeOnly=true},new AnalyticsOptions{IncludeRejected=true},new AnalyticsOptions{To=new DateTime(2000,1,1)}}){
     var data=ArchiveAnalytics.Build(source,options);Check(data.RepositoryCaptures==4&&data.RepositoryTargets==4&&data.RepositorySeconds==6300&&data.RepositoryUnknownExposure==1,"Repository headline changed with chart scope or counted non-light/deleted files");
     foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout)))foreach(int index in Enumerable.Range(0,6)){var page=AnalyticsGraphics.Page(data,index,true,layout);Check(page.Marks.Any(m=>m.Role=="header"&&m.Text=="1.75 h")&&page.Marks.Count(m=>m.Role=="header"&&m.Text=="4")==2,"Headline does not show whole-repository figures");}
    }
    var unknown=ArchiveAnalytics.Build(source,new AnalyticsOptions{UnknownTelescopeOnly=true});Check(unknown.Captures==1&&unknown.Seconds==900,"Unknown telescope scope lost its own chart data");
   });
   Test("Styled charts share vector gradients and rounded cards in both themes",()=>{
    var data=ArchiveAnalytics.Build(new[]{AnalyticsLight("M31",3600),AnalyticsLight("M42",600)},new AnalyticsOptions());
    foreach(bool dark in new[]{true,false}){
     var pages=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Page(data,i,dark)).ToList();var svg=XDocument.Parse(AnalyticsGraphics.Svg(pages,""));XNamespace ns="http://www.w3.org/2000/svg";var ids=svg.Descendants(ns+"linearGradient").Select(e=>e.Attribute("id").Value).ToList();Check(ids.Count>6&&ids.Distinct().Count()==ids.Count,"Vector gradients missing or collide across pages");foreach(var element in svg.Descendants().Where(e=>e.Attribute("fill")!=null&&e.Attribute("fill").Value.StartsWith("url(#"))){string id=element.Attribute("fill").Value.Substring(5).TrimEnd(')');Check(ids.Contains(id),"SVG refers to a missing gradient");}
     Check(pages.All(p=>p.Marks.Count(m=>m.Role=="header"&&m.Kind=="rect"&&m.Radius==12)==3),"Metrics did not receive consistent rounded cards");
     using(var output=new MemoryStream()){AnalyticsPdf.Write(output,pages,new byte[]{0,0,0},1,1,m=>"0 0 1 1 re f\n");Check(Encoding.ASCII.GetString(output.ToArray()).Contains("/ShadingType 2"),"PDF lost vector gradient shading");}
    }
   });
   Test("Analytics measures light-frame integration without double counting",()=>{
    var rows=new List<Frame>{AnalyticsLight("M31",3600),AnalyticsLight("M31",1800),AnalyticsLight("M45",null),AnalyticsLight("M45",double.NaN),AnalyticsLight("M45",double.PositiveInfinity),AnalyticsLight("M45",-1),AnalyticsLight("M45",0),new Frame{Target="M31",Kind="Stack",Exposure=5400,StackCount=90},new Frame{Kind="Dark",Exposure=60},new Frame{Kind="Video",Exposure=120}};
    var rejected=AnalyticsLight("M45",600);rejected.Rejected=true;rows.Add(rejected);var deleted=AnalyticsLight("M45",300);deleted.Status="Deleted";rows.Add(deleted);
    var data=ArchiveAnalytics.Build(rows,new AnalyticsOptions());Check(data.Captures==7&&data.Targets==2&&data.Seconds==5400&&data.UnknownExposure==5&&data.ExcludedRejected==1,"Light-only totals or metadata coverage incorrect");
    Check(data.Reports.Count==6&&data.Reports[0].Values.Sum(v=>v.Value)==7,"Six reports or target counts incorrect");
    foreach(int index in new[]{1,2,3,4})Check(Math.Abs(data.Reports[index].Values.Sum(v=>v.Value)-1.5)<1e-10,"Time totals disagree for chart "+index);
    Check(data.Reports[5].Values.Sum(v=>v.Value)==2,"Unknown exposure entered histogram");
    Check(ArchiveAnalytics.Build(rows,new AnalyticsOptions{IncludeRejected=true}).Seconds==6000,"Including rejected frames failed");
   });
   Test("Analytics filters use inclusive acquisition dates and physical telescope",()=>{
    var rows=new[]{AnalyticsLight("M31",60,"2026-09-30"),AnalyticsLight("M31",60,"2026-10-01"),AnalyticsLight("M31",60,"2026-10-02"),AnalyticsLight("M31",60,"2026-10-02","Scope B"),AnalyticsLight("M31",60,null)};
    var data=ArchiveAnalytics.Build(rows,new AnalyticsOptions{Telescope="scope a",From=new DateTime(2026,10,1),To=new DateTime(2026,10,2)});
    Check(data.Captures==2&&data.Seconds==120&&data.ExcludedUndated==1,"Date boundaries or telescope scope incorrect");
    Check(ArchiveAnalytics.Build(rows,new AnalyticsOptions()).UnknownDate==1,"Undated coverage lost");
    var shifted=AnalyticsLight("M31",60,null);shifted.Night="2026-10-01";shifted.Observed="2026-10-01T20:00:00";shifted.TimeSource="Session folder";
    Check(ArchiveAnalytics.Build(new[]{shifted},new AnalyticsOptions()).UnknownDate==1,"Shifted observing night treated as acquisition date");
    Expect(()=>ArchiveAnalytics.Build(rows,new AnalyticsOptions{From=new DateTime(2026,10,2),To=new DateTime(2026,10,1)}),"Inverted date range accepted");
   });
   Test("Analytics timeline retains gaps and histogram boundaries",()=>{
    var rows=new[]{AnalyticsLight("M31",5,"2026-01-01"),AnalyticsLight("M31",15,"2026-03-01"),AnalyticsLight("M31",30),AnalyticsLight("M31",60),AnalyticsLight("M31",120),AnalyticsLight("M31",300)};
    var data=ArchiveAnalytics.Build(rows,new AnalyticsOptions());Check(data.Reports[1].Values.Count==10&&data.Reports[1].Values[1].Value==0,"Empty acquisition months disappeared");
    Check(data.Reports[5].Values[0].Value==0&&data.Reports[5].Values.Skip(1).All(v=>v.Value==1),"Exposure boundary classified twice or in wrong bucket");
    var years=ArchiveAnalytics.Build(new[]{AnalyticsLight("M31",3600,"2020-01-01"),AnalyticsLight("M31",3600,"2026-01-01")},new AnalyticsOptions());Check(years.Reports[1].Values.Count==7&&years.Reports[1].Values.Sum(v=>v.Value)==2,"Long timeline totals changed");
   });
   Test("Analytics compaction preserves totals, identity and missing categories",()=>{
    var values=Enumerable.Range(0,50).Select(i=>new AnalyticsValue("Target "+i,i+1)).ToList();var compact=ArchiveAnalytics.Compact(values,8);
    Check(compact.Count==8&&compact.Sum(v=>v.Value)==values.Sum(v=>v.Value)&&compact.Last().Label=="Other (43 groups)","Chart compaction dropped data");
    var frame=AnalyticsLight("M31",60);frame.Filter=null;frame.Telescope=null;var data=ArchiveAnalytics.Build(new[]{frame},new AnalyticsOptions());
    Check(data.Reports[3].Values[0].Label=="Unknown telescope"&&data.Reports[4].Values[0].Label=="Unknown filter","Unknown category guessed or lost");
   });
   Test("Long analytics rankings export every category on a shared scale",()=>{
    var rows=Enumerable.Range(0,25).Select(i=>AnalyticsLight("Custom target "+i,(i+1)*60,"2026-10-01","Physical telescope "+i)).ToList();
    var data=ArchiveAnalytics.Build(rows,new AnalyticsOptions());
    foreach(int index in new[]{2,3}){
     var pages=AnalyticsGraphics.Pages(data,index);Check(pages.Count==4,"Long ranking did not continue across pages");
     foreach(var value in data.Reports[index].Values)Check(pages.SelectMany(p=>p.Marks).Any(m=>m.Detail==value.Label),"Ranked category lost: "+value.Label);
     var maxima=pages.Select(p=>p.Marks.Where(m=>m.Kind=="text"&&m.Y==659).Last().Text).Distinct().Count();Check(maxima==1,"Ranking axes changed between continuation pages");
    }
   });
   Test("Analytics SVG embeds branding, escapes labels and has six panels",()=>{
    var frame=AnalyticsLight("M31",3600);frame.Filter="Hα & <OIII>";var data=ArchiveAnalytics.Build(new[]{frame},new AnalyticsOptions{Caption="Archive & <Observatory>"});
    var pages=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Page(data,i)).ToList();string svg=AnalyticsGraphics.Svg(pages,"dGVzdA==");var document=XDocument.Parse(svg);XNamespace ns="http://www.w3.org/2000/svg",link="http://www.w3.org/1999/xlink";
    Check(document.Root.Attribute("viewBox").Value=="0 0 2400 2400"&&document.Root.Elements(ns+"g").Count()==6,"Combined SVG layout wrong");
    Check(document.Descendants(ns+"image").Count()==1&&document.Descendants(ns+"image").Single().Attribute(link+"href").Value.StartsWith("data:image/png;base64,")&&document.Descendants(ns+"use").Count()==6,"Logo was not embedded once and shared across pages");
    Check(svg.Contains("Hα &amp; &lt;OIII&gt;")&&svg.Contains("Archive &amp; &lt;Observatory&gt;"),"Unicode or XML label escaping lost");
    foreach(var page in pages){Check(page.Marks.All(m=>m.Kind=="polygon"||m.X>=0&&m.Y>=0&&m.X<1200&&m.Y<800),"Mark outside publication page");}
    File.WriteAllText(Path.Combine(root,"analytics-fixture.svg"),svg,Encoding.UTF8);
   });
   Test("Analytics PDF has distinct print pages and valid cross reference offsets",()=>{
    var data=ArchiveAnalytics.Build(new[]{AnalyticsLight("M31",3600)},new AnalyticsOptions());var pages=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Page(data,i)).ToList();
    using(var output=new MemoryStream()){
     AnalyticsPdf.Write(output,pages,new byte[]{255,255,255},1,1,mark=>"0 0 1 1 re f\n");string pdf=Encoding.ASCII.GetString(output.ToArray());
     Check(pdf.StartsWith("%PDF-1.4")&&pdf.Contains("/Count 6")&&pdf.Contains("/MediaBox [0 0 900 600]")&&pdf.EndsWith("%%EOF\n"),"PDF container malformed");
     long xref=long.Parse(pdf.Split(new[]{"startxref\n"},StringSplitOptions.None)[1].Split('\n')[0]);Check(pdf.Substring((int)xref).StartsWith("xref\n"),"PDF cross reference position incorrect");
     File.WriteAllBytes(Path.Combine(root,"analytics-fixture.pdf"),output.ToArray());
    }
   });
   Test("Analytics uses branded dark documents by default and clean light alternatives",()=>{
    var data=ArchiveAnalytics.Build(new[]{AnalyticsLight("M31",3600)},new AnalyticsOptions{Caption="Observatory"});
    for(int i=0;i<6;i++){var dark=AnalyticsGraphics.Page(data,i);var light=AnalyticsGraphics.Page(data,i,false);Check(dark.Background=="#0C1220"&&dark.Marks[0].Fill==dark.Background&&light.Background=="#FFFFFF"&&light.Marks[0].Fill==light.Background,"Theme background does not match page scene");Check(dark.Marks.Any(m=>m.Fill=="#90B8FF")&&dark.Marks.Any(m=>m.Fill=="#E9EEF8"),"Dark chart does not use AstroArchive brand colours");Check(dark.Marks.Single(m=>m.Kind=="logo").Width==70,"Publication logo was not enlarged");Check(dark.Marks.Count(m=>m.Text=="AstroArchive")==1&&!dark.Marks.Any(m=>m.Text!=null&&(m.Text.Contains("OBSERVATORY NOTES")||m.Text.Contains("known exposure")||m.Text.Contains(" · UTC"))),"Header and footer retain redundant text");var svg=XDocument.Parse(AnalyticsGraphics.Svg(new[]{dark},""));XNamespace ns="http://www.w3.org/2000/svg";Check(svg.Root.Elements(ns+"rect").First().Attribute("fill").Value==dark.Background,"SVG sheet has a white surround on dark documents");}
   });
   Test("Empty analytics produces informative charts with finite coordinates",()=>{
    var data=ArchiveAnalytics.Build(new Frame[0],new AnalyticsOptions());Check(data.Captures==0&&data.Seconds==0&&data.UnknownExposure==0,"Empty totals incorrect");
    var pages=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Page(data,i)).ToList();string svg=AnalyticsGraphics.Svg(pages,"");Check(!svg.Contains("NaN")&&!svg.Contains("Infinity")&&svg.Contains("No light frames in this scope"),"Empty chart geometry invalid");XDocument.Parse(svg);
   });
   Test("Portrait analytics reflows all charts into branded 9:16 pages",()=>{
    var rows=Enumerable.Range(0,25).Select(i=>AnalyticsLight("Custom target "+i,(i+1)*60,"2026-10-01","Physical telescope "+i)).ToList();
    var data=ArchiveAnalytics.Build(rows,new AnalyticsOptions{Caption="Portrait observatory"});
    foreach(bool dark in new[]{true,false})foreach(int index in Enumerable.Range(0,6)){
     var pages=AnalyticsGraphics.Pages(data,index,dark,AnalyticsLayout.Vertical);
     foreach(var page in pages){
      Check(page.CanvasWidth==1080&&page.CanvasHeight==1920&&page.Portrait,"Portrait dimensions are not 9:16");
      Check(page.Marks[0].Fill==(dark?"#0C1220":"#FFFFFF")&&page.Marks.Single(m=>m.Kind=="logo").Width==88,"Portrait branding or document theme lost");
      Check(page.Marks.All(m=>m.Kind=="polygon"?Enumerable.Range(0,m.Points.Length/2).All(i=>m.Points[i*2]>=0&&m.Points[i*2]<1080&&m.Points[i*2+1]>=0&&m.Points[i*2+1]<1920):m.X>=0&&m.Y>=0&&m.X+m.Width<=1080&&m.Y+m.Height<=1920),"Portrait chart geometry escaped its page");
     }
     if(index==2||index==3){Check(pages.Count==4,"Portrait ranking dropped continuation pages");foreach(var value in data.Reports[index].Values)Check(pages.SelectMany(p=>p.Marks).Any(m=>m.Detail==value.Label),"Portrait ranking lost a category");Check(pages.Select(p=>p.Marks.Where(m=>m.Kind=="text"&&m.Y==1480).Last().Text).Distinct().Count()==1,"Portrait ranking scales differ across pages");}
    }
    var collection=Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(data,i,true,AnalyticsLayout.Vertical)).ToList();var document=XDocument.Parse(AnalyticsGraphics.Svg(collection,""));
    Check(document.Root.Attribute("viewBox").Value=="0 0 1080 "+(collection.Count*1920),"Portrait SVG collection is not stacked vertically");
    using(var output=new MemoryStream()){AnalyticsPdf.Write(output,collection,new byte[]{0,0,0},1,1,mark=>"0 0 1 1 re f\n");var pdf=Encoding.ASCII.GetString(output.ToArray());Check(pdf.Contains("/MediaBox [0 0 810 1440]")&&pdf.Contains("/Count "+collection.Count),"PDF lost portrait page dimensions or continuation pages");}
    var empty=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Page(ArchiveAnalytics.Build(new Frame[0],new AnalyticsOptions()),i,true,AnalyticsLayout.Vertical)).ToList();var svg=AnalyticsGraphics.Svg(empty,"");Check(svg.Contains("No light frames in this scope")&&!svg.Contains("NaN")&&!svg.Contains("Infinity"),"Empty portrait charts have invalid geometry");
    rows[0].Telescope=new string('W',80)+" 🌌 "+new string('W',100);rows[0].Filter=rows[0].Telescope;XDocument.Parse(AnalyticsGraphics.Svg(Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(ArchiveAnalytics.Build(rows,new AnalyticsOptions()),i,true,AnalyticsLayout.Vertical)).ToList(),""));
   });
   Test("Long Unicode analytics labels remain valid across publication exports",()=>{
    var frame=AnalyticsLight("M31",60);frame.Filter=new string('W',40)+" 🌌 "+new string('W',100);frame.Telescope=new string('W',39)+"🌌 "+new string('W',100);
    var data=ArchiveAnalytics.Build(new[]{frame},new AnalyticsOptions{Caption=new string('W',107)+"🌌 Observatory"});
    XDocument.Parse(AnalyticsGraphics.Svg(Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(data,i)).ToList(),""));
   });
   Test("Common social layouts retain branding, chart categories and exact page ratios",()=>{
    double[][] sizes={new[]{1200.0,800},new[]{1080.0,1920},new[]{1080.0,1350},new[]{1080.0,1080},new[]{1920.0,1080},new[]{1000.0,1500}};
    var data=ArchiveAnalytics.Build(Enumerable.Range(0,18).Select(i=>AnalyticsLight("Target "+i,600,"2026-10-01","Instrument "+i)).ToList(),new AnalyticsOptions());
    foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout)))foreach(int index in Enumerable.Range(0,6)){
     var pages=AnalyticsGraphics.Pages(data,index,true,layout);var size=sizes[(int)layout];
     foreach(var page in pages){Check(page.CanvasWidth==size[0]&&page.CanvasHeight==size[1],"Social page dimensions differ from the selected format");Check(page.Marks.Single(m=>m.Kind=="logo").Width>0&&page.Marks.Any(m=>m.Text=="AstroArchive"),"Social format lost the branding");Check(page.Marks.Where(m=>m.Kind=="rect"||m.Kind=="logo").All(m=>m.X>=0&&m.Y>=0&&m.X+m.Width<=page.CanvasWidth+.01&&m.Y+m.Height<=page.CanvasHeight+.01),"Social layout clipped a graphic");}
     if(index==2||index==3)foreach(var value in data.Reports[index].Values)Check(pages.SelectMany(p=>p.Marks).Any(m=>m.Detail==value.Label),"Social ranking lost a category");
     var svg=XDocument.Parse(AnalyticsGraphics.Svg(new[]{pages[0]},""));Check(svg.Root.Attribute("viewBox").Value=="0 0 "+AnalyticsGraphics.N(size[0])+" "+AnalyticsGraphics.N(size[1]),"Social SVG size does not match the page");
     using(var output=new MemoryStream()){AnalyticsPdf.Write(output,pages,new byte[]{0,0,0},1,1,mark=>"0 0 1 1 re f\n");Check(Encoding.ASCII.GetString(output.ToArray()).Contains("/MediaBox [0 0 "+AnalyticsGraphics.N(size[0]*.75)+" "+AnalyticsGraphics.N(size[1]*.75)+"]"),"Social PDF page aspect is incorrect");}
    }
   });
  }
 }
}
