// Image-quality tests on captures: which frames are scored, how scores reach the library, filters, ordering and the stored measurements.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static Frame QualitySub(string hash,string session,string timestamp,string target="M51"){
   return new Frame{Hash=hash,Session=session,Telescope="Unit-01",Camera="Telephoto",Target=target,Observed=timestamp,TimeSource="FITS UTC",OriginalName=hash+".fit",Night="1999-01-01",Kind="Light",Status="Verified",RelativePath="Unit-01/"+hash+".fit",Format="FITS",LinearData=true,Width=1200,Height=900,Channels=1,
    Images=new List<ImageDescriptor>{new ImageDescriptor{Key="hdu:0",Numeric=true,Width=1200,Height=900,Channels=1,Count=1,Bitpix=16}}};
  }
  static QualityMeasurement QualityShot(string hash,double fwhm,int stars=200,double snr=50,double ecc=0.3){
   return new QualityMeasurement{Hash=hash,Kind=QualityKind.Stars,Version=QualityMeter.Version,Status=QualityStatus.Ok,Fwhm=fwhm,Hfd=fwhm,Ecc=ecc,Snr=snr,Stars=stars,StarsUsed=80,Bg=1000,Gradient=0.02,Noise=10,Width=1200,Height=900,Peak=40000,Max=60000};
  }
  static string QualityReason(Action<Frame> change){var f=QualitySub("x","s","2026-10-05T21:00:00Z");change(f);return QualityEligibility.Reason(f);}

  static void QualityLibraryTests(){
   Test("Quality: only linear light subs are eligible and every skip gives a reason",()=>{
    Check(QualityReason(f=>{})=="","A plain FITS light sub was refused");
    Check(QualityReason(f=>f.Kind="Stack").Contains("stacks"),"A stack was graded");Check(QualityReason(f=>f.Target="Sun").Contains("solar")&&QualityReason(f=>{f.Target="Unknown";f.ObservationMode="Solar";}).Contains("solar")&&QualityReason(f=>f.Target="Jupiter")==""&&QualityReason(f=>f.Target="Moon")=="","Solar images were graded, or planetary and lunar ones refused");
    Check(QualityReason(f=>f.Kind="Video").Contains("recordings"),"A video was graded");
    foreach(string kind in new[]{"Dark","Flat","Bias","Master dark"})Check(QualityReason(f=>f.Kind=kind).Contains("calibration"),kind+" frames were graded");
    Check(QualityReason(f=>{f.Kind="Light";f.Target="Calibration";}).Contains("calibration"),"A calibration target was graded");
    Check(QualityReason(f=>f.Kind="Unknown").Contains("light"),"An unknown frame type was graded");
    foreach(string status in new[]{"Deleted","Missing","Unreadable","Failed","Changed"})Check(QualityReason(f=>f.Status=status).Contains("not available"),status+" captures were graded");
    Check(QualityReason(f=>f.RelativePath=null).Contains("repository"),"A frame outside the repository was graded");
    Check(QualityReason(f=>f.Target="Meteor").Contains("meteor"),"A meteor capture was graded");
    foreach(string format in new[]{"JPEG","GIF","BMP"})Check(QualityReason(f=>f.Format=format).Contains("linear"),format+" images were graded");
    Check(QualityReason(f=>f.Format="SER").Contains("recordings"),"A SER recording was graded");
    Check(QualityReason(f=>f.LinearData=false).Contains("linear"),"Non-linear data was graded");
    Check(QualityReason(f=>{f.Format="TIFF";f.LinearData=null;}).Contains("unknown"),"Unknown linearity was graded");
    Check(QualityReason(f=>f.Images[0].Numeric=false).Length>0,"An undecodable image was graded");
    Check(QualityReason(f=>f.Images[0].Channels=2).Contains("channel"),"A two-channel image was graded");
    Check(QualityReason(f=>f.Images[0].Count=3).Contains("several"),"A multi-image file was graded without a chosen image");
    Check(QualityReason(f=>{f.Images[0].Count=3;f.ImageIndex=1;})=="","A chosen image of a multi-image file was refused");
    Check(QualityReason(f=>{f.Images[0].Width=64;f.Images[0].Height=64;}).Contains("small"),"A tiny image was graded");
    Check(QualityReason(f=>{f.Images[0].Width=20000;f.Images[0].Height=20000;}).Length>0,"An oversized image was graded");
    Check(QualityReason(f=>f.Images[0].Channels=3)=="","A colour image was refused");
    Check(QualityEligibility.Reason(null).Length>0,"A missing capture was graded");
   });
   Test("Quality: planetary, lunar and solar stills are scored as discs and nothing else is",()=>{
    Check(QualityEligibility.IsDisc(QualitySub("a","s","2026-10-05T21:00:00Z","Jupiter"))&&QualityEligibility.IsDisc(QualitySub("b","s","2026-10-05T21:00:00Z","Moon"))&&QualityEligibility.IsDisc(QualitySub("c","s","2026-10-05T21:00:00Z","Sun")),"Planetary targets not recognised");
    Check(!QualityEligibility.IsDisc(QualitySub("d","s","2026-10-05T21:00:00Z","M51"))&&QualityEligibility.KindOf(QualitySub("e","s","2026-10-05T21:00:00Z","M51"))==QualityKind.Stars,"A deep-sky target was treated as a disc");
    var mosaic=QualitySub("f","s","2026-10-05T21:00:00Z");mosaic.Bayer="RGGB";Check(QualityEligibility.HasMosaic(mosaic)&&!QualityEligibility.HasMosaic(QualitySub("g","s","2026-10-05T21:00:00Z")),"Bayer pattern detection wrong");
   });
   Test("Quality: scores reach captures by content, compare like with like, keep earlier-version results and skip unknown or mismatched measurements",()=>{
    var frames=new List<Frame>();var measurements=new Dictionary<string,QualityMeasurement>();
    for(int i=0;i<10;i++){var f=QualitySub("one"+i,"one","2026-10-05T"+(10+i)+":00:00Z");frames.Add(f);measurements[f.Hash]=QualityShot(f.Hash,2.0+0.3*i);}
    for(int i=0;i<10;i++){var f=QualitySub("two"+i,"two","2026-10-06T"+(10+i)+":00:00Z");frames.Add(f);measurements[f.Hash]=QualityShot(f.Hash,1.6+0.1*i);}
    var stack=QualitySub("stack","one","2026-10-05T22:30:00Z");stack.Kind="Stack";frames.Add(stack);measurements[stack.Hash]=QualityShot("stack",1.0);
    var stale=QualitySub("stale","earlier","2026-10-05T22:40:00Z","M42");frames.Add(stale);var old=QualityShot("stale",4.0);old.Version=QualityMeter.OldestShown;measurements[stale.Hash]=old;
    var future=QualitySub("future","one","2026-10-05T22:45:00Z");frames.Add(future);var newer=QualityShot("future",2.0);newer.Version=QualityMeter.Version+1;measurements[future.Hash]=newer;
    var ancient=QualitySub("ancient","one","2026-10-05T22:46:00Z");frames.Add(ancient);var oldest=QualityShot("ancient",2.0);oldest.Version=QualityMeter.OldestShown-1;measurements[ancient.Hash]=oldest;
    var wrong=QualitySub("wrong","one","2026-10-05T22:50:00Z");frames.Add(wrong);var disc=QualityShot("wrong",2.0);disc.Kind=QualityKind.Disc;measurements[wrong.Hash]=disc;
    var unmeasured=QualitySub("none","one","2026-10-05T22:55:00Z");frames.Add(unmeasured);
    QualityAnnotator.Apply(frames,measurements,false);
    Check(frames.Take(20).All(f=>f.QualityScore.HasValue&&f.QualityPercentile>=0&&f.QualityPercentile<=1),"Eligible subs were not scored");
    Check(!stack.QualityScore.HasValue&&stack.QualityTooltip.Contains("stacks")&&frames[0].QualityPercentile==1,"A stack with an earlier score was still scored or ranked");
    Check(!future.QualityScore.HasValue&&!ancient.QualityScore.HasValue&&!wrong.QualityScore.HasValue&&!unmeasured.QualityScore.HasValue,"Unknown, mismatched or missing measurements were scored");
    if(QualityMeter.OldestShown<QualityMeter.Version)Check(stale.QualityScore.HasValue&&stale.QualityTooltip.Contains("earlier version"),"A program update wiped an earlier-version score");
    Check(unmeasured.QualityTooltip.Contains("Analyse image quality"),"An unmeasured sub does not say how to get a score");
    for(int i=1;i<10;i++)Check(frames[i].QualityScore.Value<frames[i-1].QualityScore.Value,"Scores did not fall with wider stars within a session");
    Check(frames[0].QualityDetail.Contains("of 10 similar frames")&&frames[10].QualityDetail.Contains("of 10 similar frames"),"Sessions were not compared separately");
    var sessionScore=frames[0].QualityScore.Value;
    QualityAnnotator.Apply(frames,measurements,true);Check(frames[0].QualityDetail.Contains("of 20 similar frames")&&frames[0].QualityScore.Value!=sessionScore,"Comparing by target did not merge sessions");
    QualityAnnotator.Apply(frames,new Dictionary<string,QualityMeasurement>(),false);Check(frames.All(f=>!f.QualityScore.HasValue&&f.QualityPercentile<0&&f.QualityFlags==null),"Clearing the measurements left scores behind");
   });
   Test("Quality: discs are compared per target and the exposure column is planetary only",()=>{
    var frames=new List<Frame>();var measurements=new Dictionary<string,QualityMeasurement>();
    for(int i=0;i<9;i++){var f=QualitySub("jup"+i,"one","2026-10-05T"+(10+i)+":00:00Z","Jupiter");frames.Add(f);measurements[f.Hash]=new QualityMeasurement{Hash=f.Hash,Kind=QualityKind.Disc,Version=QualityMeter.Version,Status=QualityStatus.Ok,Limb=0.01+0.002*i,Detail=3-0.2*i,Snr=40,Peak=45000,Max=45000,Width=1200,Height=900,Diameter=300};}
    var deep=QualitySub("deep","one","2026-10-05T23:30:00Z");frames.Add(deep);measurements["deep"]=QualityShot("deep",2.5);
    QualityAnnotator.Apply(frames,measurements,true);
    Check(frames.Take(9).All(f=>f.QualityScore.HasValue&&f.ExposureFit.HasValue&&f.ExposureFitText.Contains("Good")),"Discs lack a score or exposure fit");
    Check(deep.QualityScore.HasValue&&!deep.ExposureFit.HasValue&&deep.ExposureFitText=="-","A deep-sky frame received an exposure fit");
    Check(frames[0].QualityScore.Value>frames[8].QualityScore.Value&&frames[0].QualityDetail.Contains("of 9 similar frames"),"Disc scores were mixed with deep-sky frames");
   });
   Test("Quality: subs are listed best first inside each session and stacks never move",()=>{
    var frames=new List<Frame>();var measurements=new Dictionary<string,QualityMeasurement>();double[] widths={3.4,2.1,2.8,2.0,3.9,2.5,3.1,2.2,3.6,2.9};
    for(int i=0;i<10;i++){var f=QualitySub("s"+i,"one","2026-10-05T"+(10+i)+":00:00Z");frames.Add(f);measurements[f.Hash]=QualityShot(f.Hash,widths[i]);}
    var late=QualitySub("late","one","2026-10-06T01:00:00Z");late.Session="one";frames.Add(late);
    var other=QualitySub("other","two","2026-10-12T21:00:00Z");frames.Add(other);measurements["other"]=QualityShot("other",5);
    var stack=QualitySub("st","one","2026-10-05T20:30:00Z");stack.Kind="Stack";frames.Add(stack);
    QualityAnnotator.Apply(frames,measurements,false);
    var ordered=QualityOrdering.Order(frames.AsEnumerable().Reverse());
    var first=ordered.Where(f=>f.Session=="one"&&f.QualityScore.HasValue).ToList();for(int i=1;i<first.Count;i++)Check(first[i-1].QualityScore.Value>=first[i].QualityScore.Value,"Subs not listed by score");
    Check(first[0].Hash=="s3"&&first[first.Count-1].Hash=="s4","Best or weakest sub in the wrong place: "+first[0].Hash+" "+first[first.Count-1].Hash);
    int lastScored=ordered.FindLastIndex(f=>f.QualityScore.HasValue&&f.Session=="one"),firstUnscored=ordered.FindIndex(f=>!f.QualityScore.HasValue&&f.Session=="one");Check(firstUnscored>lastScored,"Unscored captures were listed among the scored ones");
    Check(ordered.IndexOf(other)>ordered.IndexOf(first[0]),"A later session moved ahead of an earlier one");
    var plain=frames.Select(f=>QualitySub(f.Hash,f.Session,f.Observed)).ToList();var byTime=QualityOrdering.Order(plain.AsEnumerable().Reverse());for(int i=1;i<byTime.Count;i++)Check(string.CompareOrdinal(byTime[i-1].Session,byTime[i].Session)<=0,"Unscored libraries changed order");
   });
   Test("Quality: a session past midnight or under two target names is ranked as one block",()=>{
    var frames=new List<Frame>();var measurements=new Dictionary<string,QualityMeasurement>();double[] widths={3.4,2.1,2.8,2.0,3.9,2.5,3.1,2.2,3.6,2.9};
    for(int i=0;i<10;i++){var f=QualitySub("n"+i,"night","2026-10-05T"+((20+i)%24).ToString("00")+":30:00Z");f.Observed=(i<4?"2026-10-05T":"2026-10-06T")+((20+i)%24).ToString("00")+":30:00Z";if(i%3==0)f.Target="NGC 5194";frames.Add(f);measurements[f.Hash]=QualityShot(f.Hash,widths[i]);}
    QualityAnnotator.Apply(frames,measurements,false);
    var ordered=QualityOrdering.Order(frames);for(int i=1;i<ordered.Count;i++)Check(ordered[i-1].QualityOrder>=ordered[i].QualityOrder,"Scores restart part-way through a session: "+string.Join(",",ordered.Select(f=>f.Hash)));
   });
   Test("Quality: the library table lists subs best first inside each session whatever order it was in before, until the user sorts by date",()=>{
    var frames=new List<Frame>();var measurements=new Dictionary<string,QualityMeasurement>();double[] widths={3.4,2.1,2.8,2.0,3.9,2.5,3.1,2.2,3.6,2.9};
    for(int i=0;i<10;i++){var f=QualitySub("d"+i,"night","2026-10-05T"+((20+i)%24).ToString("00")+":30:00Z");f.Observed=(i<4?"2026-10-05T":"2026-10-06T")+((20+i)%24).ToString("00")+":30:00Z";frames.Add(f);measurements[f.Hash]=QualityShot(f.Hash,widths[i]);}
    var other=QualitySub("o0","two","2026-10-12T21:00:00Z");frames.Add(other);measurements["o0"]=QualityShot("o0",2.5);var other2=QualitySub("o1","two","2026-10-12T22:00:00Z");frames.Add(other2);measurements["o1"]=QualityShot("o1",3.5);
    QualityAnnotator.Apply(frames,measurements,false);
    Func<List<Frame>,string,bool> bestFirst=(rows,session)=>{var s=rows.Where(f=>f.Session==session).ToList();for(int i=1;i<s.Count;i++)if(s[i-1].QualityOrder<s[i].QualityOrder)return false;return true;};
    var kind=new[]{new SearchSort{Property="Kind"}};var date=new[]{new SearchSort{Property="AcquisitionDateLabel"}};var none=System.Threading.CancellationToken.None;var culture=System.Globalization.CultureInfo.InvariantCulture;
    // The default table order (frame type) leaves every session best first, for any sessions mode and from any starting order.
    foreach(bool grouped in new[]{false,true}){
     var def=RepositoryOrdering.Order(frames.AsEnumerable().Reverse(),kind,culture,true,grouped,none);Check(bestFirst(def,"night")&&bestFirst(def,"two"),"Default order lost the best-first ranking (grouped="+grouped+")");
     // The user picks date ordering, then returns to the default: the earlier date order must not leak into the ranking.
     var byDate=RepositoryOrdering.Order(def,date,culture,true,grouped,none);var back=RepositoryOrdering.Order(byDate,kind,culture,true,grouped,none);
     Check(bestFirst(back,"night")&&bestFirst(back,"two"),"Returning to the default order after a date sort kept the date order (grouped="+grouped+")");
     var again=RepositoryOrdering.Order(def,kind,culture,true,grouped,none);Check(back.SequenceEqual(again),"The default order depended on the sort history (grouped="+grouped+")");
    }
   });
   Test("Quality: filter by rank, flags, exposure and score range",()=>{
    var frames=new List<Frame>();var measurements=new Dictionary<string,QualityMeasurement>();
    for(int i=0;i<12;i++){var f=QualitySub("f"+i,"one","2026-10-05T"+(10+i)+":00:00Z");frames.Add(f);measurements[f.Hash]=QualityShot(f.Hash,2.0+0.25*i,i==11?30:200);}
    var stack=QualitySub("stk","one","2026-10-05T09:00:00Z");stack.Kind="Stack";frames.Add(stack);QualityAnnotator.Apply(frames,measurements,false);
    Func<string,List<Frame>> pick=choice=>{var filters=new CaptureFilters();filters.Values["Quality rank"]=choice;return filters.Apply(frames,"");};
    Check(pick("Top 10%").Count==2||pick("Top 10%").Count==1,"Top 10% size wrong: "+pick("Top 10%").Count);Check(pick("Top 10%").All(f=>f.QualityPercentile>=0.9),"Top 10% included weaker frames");
    Check(pick("Top 25%").Count>=pick("Top 10%").Count&&pick("Top half").Count==6&&pick("Bottom half").Count==6,"Half splits wrong: "+pick("Top half").Count+" "+pick("Bottom half").Count);
    Check(pick("Weakest 25%").All(f=>f.QualityPercentile<=0.25)&&pick("Weakest 25%").Contains(frames[11]),"Weakest quarter wrong");
    Check(pick("Not scored").Single()==stack,"Not scored did not isolate the stack");Check(pick("Has quality flags").All(f=>!string.IsNullOrEmpty(f.QualityFlags))&&pick("Has quality flags").Contains(frames[11]),"Flag filter wrong");
    var range=new CaptureFilters();range.Ranges["Quality"]=new CaptureRange{Mode=NumericFilterMode.Between,Minimum=Math.Round(frames[6].QualityScore.Value),Maximum=100};var kept=range.Apply(frames,"");Check(kept.Count>=7&&kept.Contains(frames[0])&&kept.Contains(frames[6])&&!kept.Contains(frames[11])&&!kept.Contains(stack),"Score range wrong: "+kept.Count);
    Check(CaptureFilters.Useful(frames,"Quality rank",false)&&!CaptureFilters.Useful(new List<Frame>{stack},"Quality rank",false)&&CaptureFilters.Useful(frames,"Quality",false),"Quality filters offered at the wrong time");
    Check(CaptureFilters.Value(frames[0],"Quality rank")==frames[0].QualityRank&&CaptureFilters.Value(stack,"Quality rank")=="Not scored","Quality rank value wrong");
   });
   Test("Quality: measurements are stored once per content, reused, remeasured on request and skip unscorable files",()=>{
    string source=Path.Combine(root,"quality-source");Directory.CreateDirectory(source);int w=640,h=480;
    for(int i=0;i<3;i++){var plane=QualityEngineTests.Field(w,h,2.6+i,70,12,40+i);Write(Path.Combine(source,"Light_M51_"+i+".fit"),w,h,(x,y)=>plane[y*w+x],LightHeaders(new DateTime(2026,10,5,21,i,0),"M51"));}
    var dark=LightHeaders(new DateTime(2026,10,5,22,0,0),"M51");dark["IMAGETYP"]="'DARK'";Write(Path.Combine(source,"Dark_60s.fit"),w,h,(x,y)=>1000+(x*7+y*13)%11,dark);
    using(var repo=new Repository(Path.Combine(root,"quality-repo"))){
     repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var all=repo.All();Check(all.Count(f=>f.Kind=="Light")==3,"Fixture lights were not imported");
     var first=repo.AnalyzeQuality(all,false,ct,NoProgress);Check(first.Eligible==3&&first.Measured==3&&first.Reused==0&&first.Skipped==1,"First run wrong: "+first.Summary());
     var again=repo.AnalyzeQuality(repo.All(),false,ct,NoProgress);Check(again.Measured==0&&again.Reused==3,"Stored measurements were not reused: "+again.Summary());
     Check(repo.UnmeasuredQuality(repo.All()).Count==0,"Measured captures still reported as unmeasured");
     var scored=repo.All();Check(scored.Where(f=>f.Kind=="Light").All(f=>f.QualityScore.HasValue)&&scored.Where(f=>f.Kind!="Light").All(f=>!f.QualityScore.HasValue),"Library rows were not annotated on load");
     var tight=scored.Single(f=>f.OriginalName=="Light_M51_0.fit");var loose=scored.Single(f=>f.OriginalName=="Light_M51_2.fit");Check(tight.QualityScore.Value>loose.QualityScore.Value,"The sharper capture did not score higher: "+tight.QualityScore+" vs "+loose.QualityScore);
     var forced=repo.AnalyzeQuality(repo.All(),true,ct,NoProgress);Check(forced.Measured==3&&forced.Reused==0,"A fresh measurement was not forced");
     repo.ClearQuality();Check(repo.All().All(f=>!f.QualityScore.HasValue)&&repo.UnmeasuredQuality(repo.All()).Count==3,"Clearing left measurements behind");
     var found=repo.AnalyzeMissingQuality(ct,NoProgress);Check(found.Measured==3,"Missing measurements were not found: "+found.Summary());
    }
    using(var reopened=new Repository(Path.Combine(root,"quality-repo"))){Check(reopened.All().Count(f=>f.QualityScore.HasValue)==3,"Measurements did not survive reopening the repository");Check(reopened.QualityMeasurements().Count==3,"Stored measurement count wrong");
     // As after a program update that bumped the measurement version: scores stay until the next run refreshes them.
     if(QualityMeter.OldestShown<QualityMeter.Version){
      foreach(var m in reopened.QualityMeasurements().Values)m.Version=QualityMeter.OldestShown;
      Check(reopened.All().Count(f=>f.QualityScore.HasValue)==3,"Scores were lost after a measurement version change");
      Check(reopened.UnmeasuredQuality(reopened.All()).Count==3,"Earlier-version measurements are not offered for a refresh");
      var refreshed=reopened.AnalyzeQuality(reopened.All(),false,ct,NoProgress);Check(refreshed.Measured==3&&refreshed.Reused==0&&reopened.QualityMeasurements().Values.All(m=>m.Version==QualityMeter.Version),"Earlier-version measurements were not refreshed: "+refreshed.Summary());
     }}
   });
   Test("Quality: an unreadable file is recorded as unreadable instead of stopping the run",()=>{
    string source=Path.Combine(root,"quality-broken-source");Directory.CreateDirectory(source);int w=640,h=480;var plane=QualityEngineTests.Field(w,h,2.8,60,12,77);
    Write(Path.Combine(source,"Light_M51_a.fit"),w,h,(x,y)=>plane[y*w+x],LightHeaders(new DateTime(2026,10,5,21,0,0),"M51"));
    using(var repo=new Repository(Path.Combine(root,"quality-broken-repo"))){
     repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var frame=repo.All().Single();File.WriteAllBytes(repo.FilePath(frame),new byte[2880]);
     var run=repo.AnalyzeQuality(new[]{frame},true,ct,NoProgress);Check(run.Unsuitable==1&&run.Errors.Count==1&&run.Errors[0].StartsWith("Light_M51_a.fit: set aside")&&run.Summary().Contains("1 set aside")&&run.Report().Contains("Light_M51_a.fit"),"A damaged file was not reported by name: "+run.Report());
     Check(!repo.All().Single().QualityScore.HasValue&&repo.All().Single().QualityTooltip.Contains("set aside"),"A damaged file received a score or lacks its reason");
     var again=repo.AnalyzeQuality(repo.All(),false,ct,NoProgress);Check(again.SetAside==1&&again.Measured==0&&again.Unsuitable==0,"A set-aside file was measured again: "+again.Summary());
     Check(repo.UnmeasuredQuality(repo.All()).Count==0,"A set-aside file is still reported as unmeasured");
     File.Delete(repo.FilePath(frame));var missing=repo.AnalyzeQuality(repo.All(),true,ct,NoProgress);Check(missing.Failed==1&&missing.Errors[0].Contains("could not be read")&&repo.UnmeasuredQuality(repo.All()).Count==1,"A missing file was set aside instead of being tried again: "+missing.Report());
    }
   });
   Test("Quality: saved repository layouts gain the QUALITY heading once",()=>{
    var old=new ColumnLayout{Order=new List<string>{"OriginalName","ExposureText","GainText","Filter"},Visible=new List<string>{"OriginalName","ExposureText","GainText"},RepositoryGainShown=true};
    var upgraded=ColumnLayout.UpgradeRepository(old);Check(upgraded.Visible.SequenceEqual(new[]{"OriginalName","ExposureText","GainText","QualityText"})&&upgraded.RepositoryQualityShown&&upgraded.Order.SequenceEqual(old.Order),"QUALITY not added to a saved layout");
    Check(!old.Visible.Contains("QualityText")&&!old.RepositoryQualityShown,"The saved layout was changed in place");
    upgraded.Visible.Remove("QualityText");var again=ColumnLayout.UpgradeRepository(upgraded);Check(!again.Visible.Contains("QualityText"),"A column hidden by the user came back");
    Check(ColumnLayout.UpgradeRepository(null)==null,"A missing layout was invented");
    var unknown=ColumnLayout.UpgradeRepository(new ColumnLayout{Order=new List<string>{"OriginalName"},Visible=new List<string>{"OriginalName"}});Check(unknown.RepositoryGainShown&&unknown.RepositoryQualityShown&&unknown.Visible.Contains("GainText")&&unknown.Visible.Contains("QualityText"),"Old layouts did not get both new headings");
   });
   Test("Quality: the run summary says what happened in plain words",()=>{
    Check(new QualityRun().Summary().Contains("No light frames"),"Empty run summary wrong");
    var run=new QualityRun{Eligible=10,Measured=6,Reused=3,TooFewStars=1,Failed=1,Unsuitable=1,SetAside=1,Skipped=2};string text=run.Summary();Check(text.Contains("6 measured")&&text.Contains("3 already measured")&&text.Contains("1 with too few stars")&&text.Contains("1 unreadable")&&text.Contains("2 set aside")&&text.Contains("2 not scorable"),"Run summary wrong: "+text);
    Check(QualityFailure.Unsuitable(new InvalidDataException())&&QualityFailure.Unsuitable(new NotSupportedException())&&QualityFailure.Unsuitable(new OutOfMemoryException())&&QualityFailure.Unsuitable(new EndOfStreamException()),"Content problems were not set aside");
    Check(!QualityFailure.Unsuitable(new IOException("busy"))&&!QualityFailure.Unsuitable(new FileNotFoundException())&&!QualityFailure.Unsuitable(new UnauthorizedAccessException()),"Read problems were set aside instead of retried");
    Check(QualityFailure.Describe(new OutOfMemoryException()).Contains("too large")&&QualityFailure.Describe(new FileNotFoundException()).Contains("missing"),"Failure reasons are not plain");
   });
  }
 }
}
