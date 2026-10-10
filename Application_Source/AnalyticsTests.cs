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
     var pages=AnalyticsGraphics.Pages(data,index,dark,true);
     foreach(var page in pages){
      Check(page.CanvasWidth==1080&&page.CanvasHeight==1920&&page.Portrait,"Portrait dimensions are not 9:16");
      Check(page.Marks[0].Fill==(dark?"#0C1220":"#FFFFFF")&&page.Marks.Single(m=>m.Kind=="logo").Width==88,"Portrait branding or document theme lost");
      Check(page.Marks.All(m=>m.Kind=="polygon"?Enumerable.Range(0,m.Points.Length/2).All(i=>m.Points[i*2]>=0&&m.Points[i*2]<1080&&m.Points[i*2+1]>=0&&m.Points[i*2+1]<1920):m.X>=0&&m.Y>=0&&m.X+m.Width<=1080&&m.Y+m.Height<=1920),"Portrait chart geometry escaped its page");
     }
     if(index==2||index==3){Check(pages.Count==4,"Portrait ranking dropped continuation pages");foreach(var value in data.Reports[index].Values)Check(pages.SelectMany(p=>p.Marks).Any(m=>m.Detail==value.Label),"Portrait ranking lost a category");Check(pages.Select(p=>p.Marks.Where(m=>m.Kind=="text"&&m.Y==1480).Last().Text).Distinct().Count()==1,"Portrait ranking scales differ across pages");}
    }
    var collection=Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(data,i,true,true)).ToList();var document=XDocument.Parse(AnalyticsGraphics.Svg(collection,""));
    Check(document.Root.Attribute("viewBox").Value=="0 0 1080 "+(collection.Count*1920),"Portrait SVG collection is not stacked vertically");
    using(var output=new MemoryStream()){AnalyticsPdf.Write(output,collection,new byte[]{0,0,0},1,1,mark=>"0 0 1 1 re f\n");var pdf=Encoding.ASCII.GetString(output.ToArray());Check(pdf.Contains("/MediaBox [0 0 810 1440]")&&pdf.Contains("/Count "+collection.Count),"PDF lost portrait page dimensions or continuation pages");}
    var empty=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Page(ArchiveAnalytics.Build(new Frame[0],new AnalyticsOptions()),i,true,true)).ToList();var svg=AnalyticsGraphics.Svg(empty,"");Check(svg.Contains("No light frames in this scope")&&!svg.Contains("NaN")&&!svg.Contains("Infinity"),"Empty portrait charts have invalid geometry");
    rows[0].Telescope=new string('W',80)+" 🌌 "+new string('W',100);rows[0].Filter=rows[0].Telescope;XDocument.Parse(AnalyticsGraphics.Svg(Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(ArchiveAnalytics.Build(rows,new AnalyticsOptions()),i,true,true)).ToList(),""));
   });
   Test("Long Unicode analytics labels remain valid across publication exports",()=>{
    var frame=AnalyticsLight("M31",60);frame.Filter=new string('W',40)+" 🌌 "+new string('W',100);frame.Telescope=new string('W',39)+"🌌 "+new string('W',100);
    var data=ArchiveAnalytics.Build(new[]{frame},new AnalyticsOptions{Caption=new string('W',107)+"🌌 Observatory"});
    XDocument.Parse(AnalyticsGraphics.Svg(Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(data,i)).ToList(),""));
   });
  }
 }
}
