using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace AstroArchive {
 public partial class Tests {
  static Frame AnalyticsLight(string target,double? seconds,string date="2026-10-01",string telescope="Scope A",string session=null){
   return new Frame{Target=target,Kind="Light",Exposure=seconds,AcquisitionDate=date,Telescope=telescope,Filter="L",Session=session};
  }
  static void AnalyticsTests(){
   Test("Chart types restyle a copy and keep every group and headline",()=>{
    var source=Enumerable.Range(0,20).Select(i=>AnalyticsLight("Target "+i,60*(i+1),"2026-10-"+(i%9+1).ToString("00"),"Scope "+(i%3))).ToList();var data=ArchiveAnalytics.Build(source,new AnalyticsOptions());
    for(int index=0;index<6;index++){
     Check(AnalyticsGraphics.NaturalStyles[index]==data.Reports[index].Style,"Natural chart style differs from the report: "+index);
     var styles=AnalyticsGraphics.StylesFor(index);Check(styles[0]==data.Reports[index].Style&&styles.Distinct().Count()==styles.Length&&(index==1?styles.Length==1:styles.Length==3),"Chart type choices are wrong: "+index);
     foreach(string style in styles)foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout))){
      var pages=AnalyticsGraphics.Pages(data,index,true,layout,style);Check(pages.Count>0&&pages.All(p=>p.Marks.Any(m=>m.Role=="header"&&m.Text==ArchiveAnalytics.Number(data.RepositorySeconds/3600)+" h")),"Chart type lost the repository headline: "+index+" "+style);
      Check(pages.All(p=>p.Marks.Any(m=>m.Role=="chart")),"Chart type drew no chart: "+index+" "+style+" "+layout);
      if(style=="bars"&&index!=1)foreach(var value in data.Reports[index].Values)Check(pages.SelectMany(p=>p.Marks).Any(m=>m.Detail==value.Label||m.Text==value.Label),"Bars lost a group: "+index+" "+value.Label);
      if(style=="bars"&&data.Reports[index].Unit=="frames")Check(pages.SelectMany(p=>p.Marks).Any(m=>m.Text=="Light frames")&&!pages.SelectMany(p=>p.Marks).Any(m=>m.Text=="Integration (hours)"),"Frame bars are labelled as hours: "+index);
     }
    }
    Check(data.Reports[0].Style=="donut"&&data.Reports[2].Style=="bars"&&data.Reports[0].Values.Count==20,"Restyling changed the source report");
    var natural=AnalyticsGraphics.Pages(data,2,true,AnalyticsLayout.Landscape,null);Check(natural.Count==AnalyticsGraphics.Pages(data,2).Count,"Default chart type changed the pages");
    var invalid=AnalyticsGraphics.Pages(data,1,true,AnalyticsLayout.Landscape,"donut");Check(invalid.Count==AnalyticsGraphics.Pages(data,1).Count&&!invalid.SelectMany(p=>p.Marks).Any(m=>m.Kind=="polygon"&&m.Role=="chart"),"The timeline accepted an unordered chart type");
   });
   Test("Analytics preferences repair saved values and migrate 3.1.5 chart choices",()=>{
    var fresh=AnalyticsPreferences.Load(Util.Deserialize<Settings>("{}").Analytics,null);
    Check(fresh.ImageFormat=="PDF"&&fresh.CardFormat=="PNG"&&fresh.VideoFormat=="MP4"&&fresh.Layout==0&&!fresh.LightDocument&&!fresh.Video&&fresh.Palette==null&&fresh.Item==null&&fresh.Charts.Count==0&&fresh.IncludedCharts().Count==6,"Old settings did not start from the familiar defaults");
    var legacy=Util.Deserialize<Settings>("{\"AnalyticsCharts\":{\"targets\":{\"Title\":\" My targets \",\"Subtitle\":\"\",\"Style\":\"Bars\",\"Order\":\"Lowest first\",\"Palette\":\"Nebula\",\"Categories\":6},\"timeline\":{\"Title\":\"\",\"Style\":\"Donut\",\"Order\":\"Alphabetical\",\"Palette\":\"AstroArchive\"},\"filters\":{\"Style\":\"Automatic\",\"Order\":\"Alphabetical\"},\"unknown\":{\"Title\":\"Kept?\"}}}");
    var migrated=AnalyticsPreferences.Load(legacy.Analytics,legacy.AnalyticsCharts);var targets=migrated.Chart(0);
    Check(targets.Title=="My targets"&&targets.Style=="bars"&&targets.Order=="Smallest first"&&targets.Palette==null&&migrated.Palette=="Nebula","3.1.5 chart choices were not carried over");
    Check(migrated.Chart(4).Order=="A–Z"&&migrated.Chart(4).Style==null,"Alphabetical order or automatic style was not migrated");
    migrated.Normalise();Check(!migrated.Charts.ContainsKey("timeline")&&!migrated.Charts.ContainsKey("unknown"),"The timeline accepted a reordering or an unknown chart was kept");
    var broken=new AnalyticsPreferences{Item="nowhere",Layout=42,Pace=-4,Transition=9,VideoSize=7,CardResolution=-1,Palette="Rainbow",ImageFormat="TIFF",CardFormat="PDF",VideoFormat="AVI",Combined=new List<string>{"filters","nope","targets"},Charts=new Dictionary<string,AnalyticsChartOptions>{{"targets",new AnalyticsChartOptions{Title=new string('x',90),Style="columns",Order="Largest first"}},{"exposures",new AnalyticsChartOptions{Style="columns",Order="A–Z"}}}};
    broken.Normalise();
    Check(broken.Item==null&&broken.Layout==5&&broken.Pace==0&&broken.Transition==2&&broken.VideoSize==2&&broken.CardResolution==0&&broken.Palette==null&&broken.ImageFormat=="PDF"&&broken.CardFormat=="PNG"&&broken.VideoFormat=="MP4","Out of range preferences were not repaired");
    Check(broken.IncludedCharts().SequenceEqual(new[]{0,4})&&broken.Chart(0).Title.Length==AnalyticsPreferences.TitleLength&&broken.Chart(0).Style=="columns"&&broken.Chart(0).Order==null,"Combined charts or chart titles were not repaired");
    broken.Normalise();Check(!broken.Charts.ContainsKey("exposures"),"A chart left at its defaults was saved");
    Check(!broken.Include(0,false)||broken.IncludedCharts().Count>0,"Combined document lost its last chart");Check(!broken.Include(4,false)&&broken.IncludedCharts().SequenceEqual(new[]{4}),"The last combined chart was removed");
    foreach(int i in new[]{0,1,2,3,5})broken.Include(i,true);Check(broken.Combined==null&&broken.IncludedCharts().Count==6,"Including every chart did not return to the default");
    var saved=new Settings{Analytics=new AnalyticsPreferences{Item="cards",LightDocument=true,Layout=2,Palette="Aurora",Video=true,VideoFormat="GIF",ScreenResolution=true,Pace=2,Transition=1,OptionsHidden=true,Combined=new List<string>{"targets","filters"}}};saved.Analytics.Chart(2).Order="A–Z";saved.Analytics.Chart(2).Subtitle="Hours per object";
    var restored=AnalyticsPreferences.Load(Util.Deserialize<Settings>(Util.Serialize(saved)).Analytics,null);
    Check(restored.ItemIndex()==7&&restored.LightDocument&&restored.Layout==2&&restored.Palette=="Aurora"&&restored.Video&&restored.VideoFormat=="GIF"&&restored.ScreenResolution&&restored.Pace==2&&restored.Transition==1&&restored.OptionsHidden,"Analytics choices were not saved");
    Check(restored.IncludedCharts().SequenceEqual(new[]{0,4})&&restored.Chart(2).Order=="A–Z"&&restored.Chart(2).Subtitle=="Hours per object","Combined charts or chart customisation were not saved");
    restored.Reset();Check(restored.ItemIndex()==7&&restored.OptionsHidden&&!restored.LightDocument&&restored.Layout==0&&restored.Palette==null&&!restored.Video&&restored.VideoFormat=="MP4"&&restored.Pace==0&&restored.Combined==null&&restored.Charts.Count==0,"Resetting analytics options missed a choice or moved the viewer");
   });
   Test("Customised charts keep every group, reorder rankings and recolour only the series",()=>{
    var source=Enumerable.Range(0,20).SelectMany(i=>Enumerable.Range(0,i+1).Select(j=>AnalyticsLight("Target "+(char)('A'+i),60,"2026-10-0"+(i%9+1),"Scope "+(i%4)))).ToList();var data=ArchiveAnalytics.Build(source,new AnalyticsOptions());
    var plain=AnalyticsGraphics.Pages(data,2,true,AnalyticsLayout.Landscape);Check(AnalyticsGraphics.Pages(data,2,true,AnalyticsLayout.Landscape,new AnalyticsChartOptions(),null).Select(p=>p.Marks.Count).SequenceEqual(plain.Select(p=>p.Marks.Count)),"Unchanged options changed the chart");
    foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout))){
     var smallest=AnalyticsGraphics.Pages(data,2,false,layout,new AnalyticsChartOptions{Order="Smallest first",Title="Nights on each object",Subtitle="Every target, smallest first"},null);
     var labels=smallest.SelectMany(p=>p.Marks).Where(m=>m.Detail!=null&&m.Detail.StartsWith("Target ")).Select(m=>m.Detail).Distinct().ToList();
     Check(labels.Count==20&&labels[0]=="Target A"&&labels[19]=="Target T","Smallest first lost or misordered a target: "+layout+" "+string.Join(",",labels));
     Check(smallest.All(p=>p.Title=="Nights on each object")&&smallest.SelectMany(p=>p.Marks).Any(m=>m.Detail=="Nights on each object"||m.Text=="Nights on each object")&&smallest.SelectMany(p=>p.Marks).Any(m=>m.Text!=null&&m.Text.StartsWith("Every target")),"Custom title or subtitle missing: "+layout);
    }
    var donut=AnalyticsGraphics.Pages(data,0,true,AnalyticsLayout.Landscape,new AnalyticsChartOptions{Order="A–Z"},null).Single();var slices=donut.Marks.Where(m=>m.Role=="chart"&&m.Kind=="text"&&m.Detail!=null).Select(m=>m.Detail).ToList();
    Check(slices.Count==8&&slices.Take(7).SequenceEqual(new[]{"Target N","Target O","Target P","Target Q","Target R","Target S","Target T"})&&slices[7].StartsWith("Other (13"),"A–Z donut did not keep the largest groups with Other last: "+string.Join(",",slices));
    var timeline=AnalyticsGraphics.Pages(data,1,true,AnalyticsLayout.Landscape,new AnalyticsChartOptions{Order="A–Z",Style="donut"},null);Check(timeline.Select(p=>p.Marks.Count).SequenceEqual(AnalyticsGraphics.Pages(data,1,true,AnalyticsLayout.Landscape).Select(p=>p.Marks.Count)),"The timeline accepted a new order or type");
    foreach(bool dark in new[]{true,false})foreach(string palette in AnalyticsPreferences.Palettes.Skip(1)){
     var natural=AnalyticsGraphics.Pages(data,4,dark,AnalyticsLayout.Square);var coloured=AnalyticsGraphics.Pages(data,4,dark,AnalyticsLayout.Square,null,palette);
     var before=natural.SelectMany(p=>p.Marks).ToList();var after=coloured.SelectMany(p=>p.Marks).ToList();Check(before.Count==after.Count,"Palette changed the chart structure");
     Check(Enumerable.Range(0,before.Count).All(i=>before[i].Kind!="text"||before[i].Fill==after[i].Fill),"Palette recoloured lettering: "+palette);
     Check(Enumerable.Range(0,before.Count).Any(i=>before[i].Fill!=after[i].Fill),"Palette left the chart unchanged: "+palette+" "+dark);
    }
   });
   Test("The AstroArchive wordmark matches the logo height and centre in every layout",()=>{
    var data=ArchiveAnalytics.Build(new[]{AnalyticsLight("M31",600)},new AnalyticsOptions());
    foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout)))foreach(int index in Enumerable.Range(0,6))foreach(var page in AnalyticsGraphics.Pages(data,index,index%2==0,layout)){
     var logo=page.Marks.Single(m=>m.Kind=="logo");var brand=page.Marks.Single(m=>m.Text=="AstroArchive");double capitals=brand.Size*AnalyticsGraphics.BrandCapHeight;
     Check(capitals<=logo.Height+.01&&capitals>=logo.Height*.7,"Wordmark is not similar in height to the logo: "+layout+" "+capitals+" / "+logo.Height);
     Check(Math.Abs(brand.Y+brand.Size*AnalyticsGraphics.BrandCapCentre-(logo.Y+logo.Height/2))<.01,"Wordmark is not centred on the logo: "+layout);
     Check(brand.X>logo.X+logo.Width&&page.Marks.Where(m=>m.Kind=="text"&&m!=brand&&m.Role=="header"&&m.Y<logo.Y+logo.Height&&m.Y+m.Size>logo.Y).All(m=>m.X>brand.X+brand.Size*6.4||m.Centered),"Wordmark overlaps header text: "+layout);
    }
   });
   Test("Top target cards rank by integration or light frames without double counting stacks",()=>{
    var rows=new List<Frame>{AnalyticsLight("M31",60,"2026-10-01","Scope","night"),new Frame{Target="M31",Kind="Stack",Exposure=99999,Session="night",Telescope="Scope",AcquisitionDate="2026-10-01"},AnalyticsLight("M45",300),AnalyticsLight("M45",300),AnalyticsLight("M42",30),AnalyticsLight("M42",30),AnalyticsLight("M42",30),new Frame{Target="Calibration",Kind="Light",Exposure=99999},new Frame{Target="Unknown",Kind="Light",Exposure=99999}};
    var rejected=AnalyticsLight("M42",9000);rejected.Rejected=true;rows.Add(rejected);
    var byTime=TargetCards.Rank(rows);var byFrames=TargetCards.Rank(rows,true);var withRejected=TargetCards.Rank(rows,false,true);
    Check(byTime.Count==3&&byTime[0].Target=="M45"&&byTime[0].Seconds==600&&byFrames[0].Target=="M42"&&byFrames[0].Lights==3,"Top target ranking is wrong: "+string.Join(", ",byTime.Select(c=>c.Target+" "+c.Seconds)));
    Check(byTime.Single(c=>c.Target=="M31").Seconds==60&&withRejected[0].Target=="M42","Matching stacks were double counted or rejected frames ignored");
   });
   Test("Repository headline totals survive every chart scope and missing date",()=>{
    var rejected=AnalyticsLight("M45",3600,"2025-01-01","Scope B");rejected.Rejected=true;
    var deleted=AnalyticsLight("M51",7200);deleted.Status="Deleted";
    var source=new[]{AnalyticsLight("M31",1800),rejected,AnalyticsLight("M42",900,null,null),AnalyticsLight("M81",null),deleted,new Frame{Target="M31",Kind="Stack",Exposure=9000},new Frame{Kind="Video",Exposure=9000}};
    foreach(var options in new[]{new AnalyticsOptions(),new AnalyticsOptions{Telescope="Scope A",From=new DateTime(2026,10,1)},new AnalyticsOptions{UnknownTelescopeOnly=true},new AnalyticsOptions{IncludeRejected=true},new AnalyticsOptions{To=new DateTime(2000,1,1)}}){
    var data=ArchiveAnalytics.Build(source,options);Check(data.RepositoryCaptures==4&&data.RepositoryTargets==4&&data.RepositorySeconds==15300&&data.RepositoryUnknownExposure==1,"Repository headline changed with chart scope or failed to include standalone stacks");
    foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout)))foreach(int index in Enumerable.Range(0,6)){
     var page=AnalyticsGraphics.Page(data,index,true,layout);Check(page.Marks.Any(m=>m.Role=="header"&&m.Text=="4.25 h")&&page.Marks.Count(m=>m.Role=="header"&&m.Text=="4")==2,"Headline does not show whole-repository figures");
     string[] metrics={"4.25 h","4","4"},captions={"REPOSITORY INTEGRATION",layout==AnalyticsLayout.Landscape||layout==AnalyticsLayout.Widescreen?"LIGHT FRAMES IN REPOSITORY":"LIGHT FRAMES",layout==AnalyticsLayout.Landscape||layout==AnalyticsLayout.Widescreen?"TARGETS IN REPOSITORY":"REPOSITORY TARGETS"};
     for(int i=0;i<3;i++){
      double expected=layout==AnalyticsLayout.Landscape?240+i*360:layout==AnalyticsLayout.Widescreen?(240+i*360)*1.35+150:layout==AnalyticsLayout.Pinterest?(220+i*320)*1000.0/1080:layout==AnalyticsLayout.Vertical?80+(i+.5)*920/3:220+i*320;
      foreach(string text in new[]{metrics[i],captions[i]}){var matches=page.Marks.Where(m=>m.Role=="header"&&m.Text==text&&Math.Abs(m.X-expected)<.01).ToList();Check(matches.Count==1,"Metric header text did not land at its centered position for "+layout+" / "+text+" / "+expected+"; found "+string.Join(", ",page.Marks.Where(m=>m.Role=="header"&&m.Text==text).Select(m=>m.X)));Check(matches[0].Centered,"Metric header text is not centered in its card: "+text);}
     }
     var svg=XDocument.Parse(AnalyticsGraphics.Svg(new[]{page},""));XNamespace ns="http://www.w3.org/2000/svg";Check(svg.Descendants(ns+"text").Where(e=>e.Value=="REPOSITORY INTEGRATION").Single().Attribute("text-anchor").Value=="middle","SVG metric caption is not center-anchored");
    }
    }
    var unknown=ArchiveAnalytics.Build(source,new AnalyticsOptions{UnknownTelescopeOnly=true});Check(unknown.Captures==1&&unknown.Seconds==9900,"Unknown telescope scope lost its own chart data");
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
    var rows=new List<Frame>{AnalyticsLight("M31",3600,session:"shared"),AnalyticsLight("M31",1800,session:"shared"),AnalyticsLight("M45",null),AnalyticsLight("M45",double.NaN),AnalyticsLight("M45",double.PositiveInfinity),AnalyticsLight("M45",-1),AnalyticsLight("M45",0),new Frame{Target="M31",Kind="Stack",Exposure=5400,StackCount=90,Session="shared"},new Frame{Kind="Dark",Exposure=60},new Frame{Kind="Video",Exposure=120}};
    var rejected=AnalyticsLight("M45",600);rejected.Rejected=true;rows.Add(rejected);var deleted=AnalyticsLight("M45",300);deleted.Status="Deleted";rows.Add(deleted);
    var data=ArchiveAnalytics.Build(rows,new AnalyticsOptions());Check(data.Captures==7&&data.Targets==2&&data.Seconds==5400&&data.UnknownExposure==5&&data.ExcludedRejected==1,"Light-only totals or metadata coverage incorrect");
    Check(data.Reports.Count==6&&data.Reports[0].Values.Sum(v=>v.Value)==7,"Six reports or target counts incorrect");
    foreach(int index in new[]{1,2,3,4})Check(Math.Abs(data.Reports[index].Values.Sum(v=>v.Value)-1.5)<1e-10,"Time totals disagree for chart "+index);
    Check(data.Reports[5].Values.Sum(v=>v.Value)==2,"Unknown exposure entered histogram");
    Check(ArchiveAnalytics.Build(rows,new AnalyticsOptions{IncludeRejected=true}).Seconds==6000,"Including rejected frames failed");
   });
   Test("Analytics metric cards center their text in every output layout",()=>{
    var data=ArchiveAnalytics.Build(new[]{AnalyticsLight("M31",3600)},new AnalyticsOptions());
    foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout))){
     var page=AnalyticsGraphics.Page(data,0,true,layout);var label=page.Marks.Single(m=>m.Role=="header"&&m.Text=="REPOSITORY INTEGRATION");
     Check(label.Centered,"Repository integration label lacks centered alignment for "+layout);
     var svg=XDocument.Parse(AnalyticsGraphics.Svg(new[]{page},""));XNamespace ns="http://www.w3.org/2000/svg";
     Check(svg.Descendants(ns+"text").Single(e=>e.Value=="REPOSITORY INTEGRATION").Attribute("text-anchor").Value=="middle","SVG text is not centered for "+layout);
    }
   });
   Test("Stack integration is counted only when no sub of its target shares its session or date",()=>{
    var rows=new List<Frame>{AnalyticsLight("M31",600,"2026-10-01","Scope A","shared"),AnalyticsLight("M31",900,"2026-10-01","Scope A","shared"),new Frame{Target="M31",Kind="Stack",Exposure=1500,Session="shared"},AnalyticsLight("M42",120,"2026-10-02","Scope A","single"),new Frame{Target="M42",Kind="Stack",Exposure=1200,Session="single",AcquisitionDate="2026-10-02"},new Frame{Target="M42",Kind="Stack",Exposure=1800,Session="other",AcquisitionDate="2026-10-02"},new Frame{Target="M31",Kind="Stack",Exposure=2400,AcquisitionDate="2026-10-02"}};
    var data=ArchiveAnalytics.Build(rows,new AnalyticsOptions());Check(data.RepositorySeconds==4020&&data.Seconds==4020,"Stack with same-session or same-date subs was double-counted, or a standalone stack was omitted");
    foreach(int index in new[]{1,2,3,4})Check(Math.Abs(data.Reports[index].Values.Sum(v=>v.Value)-data.Seconds/3600)<1e-10,"An integration chart disagrees with the total at index "+index);
    Check(data.Reports[5].Values.Sum(v=>v.Value)==3,"Stack images were counted as individual light frames in the exposure-length histogram");
    var oneSub=new[]{AnalyticsLight("M31",600,"2026-10-01","Scope A","one"),new Frame{Target="M31",Kind="Stack",Exposure=900,Session="one"}};
    Check(CaptureGroups.ExposureFrames(oneSub).Count==1,"A single saved sub did not suppress its session's stack");
    var otherDate=new[]{AnalyticsLight("M31",600,"2026-10-01","Scope A","a"),new Frame{Target="Andromeda Galaxy",Kind="Stack",Exposure=900,Session="b",AcquisitionDate="2026-10-01"},new Frame{Target="M31",Kind="Stack",Exposure=900,Session="c",AcquisitionDate="2026-10-03"}};
    var kept=CaptureGroups.ExposureFrames(otherDate);Check(kept.Count==2&&kept.Any(f=>f.Kind=="Stack"&&f.AcquisitionDate=="2026-10-03"),"Same-date stack (alias target) was counted or another date's stack was dropped");
    var differentTarget=new[]{AnalyticsLight("M31",600,"2026-10-01","Scope A","same"),AnalyticsLight("M31",900,"2026-10-01","Scope A","same"),new Frame{Target="M42",Kind="Stack",Exposure=1800,Session="same"}};
    Check(CaptureGroups.ExposureFrames(differentTarget).Count==3,"Subframes for a different target incorrectly suppressed a stack");
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
    for(int i=0;i<6;i++){var dark=AnalyticsGraphics.Page(data,i);var light=AnalyticsGraphics.Page(data,i,false);Check(dark.Background=="#0C1220"&&dark.Marks[0].Fill==dark.Background&&light.Background=="#FFFFFF"&&light.Marks[0].Fill==light.Background,"Theme background does not match page scene");Check(dark.Marks.Any(m=>m.Fill=="#90B8FF")&&dark.Marks.Any(m=>m.Fill=="#E9EEF8"),"Dark chart does not use AstroArchive brand colours");Check(dark.Marks.Single(m=>m.Kind=="logo").Width==56,"Publication logo is not the agreed size");Check(dark.Marks.Count(m=>m.Text=="AstroArchive")==1&&!dark.Marks.Any(m=>m.Text!=null&&(m.Text.Contains("OBSERVATORY NOTES")||m.Text.Contains("known exposure")||m.Text.Contains(" · UTC"))),"Header and footer retain redundant text");var svg=XDocument.Parse(AnalyticsGraphics.Svg(new[]{dark},""));XNamespace ns="http://www.w3.org/2000/svg";Check(svg.Root.Elements(ns+"rect").First().Attribute("fill").Value==dark.Background,"SVG sheet has a white surround on dark documents");}
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
      Check(page.Marks[0].Fill==(dark?"#0C1220":"#FFFFFF")&&page.Marks.Single(m=>m.Kind=="logo").Width==70.4,"Portrait branding or document theme lost");
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
