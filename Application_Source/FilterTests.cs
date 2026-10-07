using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static Frame Dated(string session,string timestamp){return new Frame{Session=session,Telescope="Unit-01",Camera="Telephoto",Target="M51",Observed=timestamp,TimeSource="FITS UTC",OriginalName="capture.fit",Night="1999-01-01"};}
  static void FilterRegressions(){
   Test("Session labels use actual UTC acquisition dates across midnight",()=>{
    var rows=new[]{Dated("one","2024-09-26T23:59:59Z"),Dated("one","2024-09-27T00:00:01Z")};var choice=CaptureSessions.Choices(rows).Single();
    Check(choice.Label=="26/09/24-27/09/24"&&choice.Count==2&&rows.Select(f=>f.SessionKey).Distinct().Count()==1,"Calendar span or identity changed at midnight");
    rows[1].Night="2024-09-26";Check(CaptureSessions.Choices(rows).Single().Label==choice.Label,"Shifted night affected dates");
    rows[0].Observed="2024-09-27T00:30:00+02:00";Check(CaptureSessions.Date(rows[0]).Text=="26/09/24","UTC offset date depended on machine timezone");
   });
   Test("Classifier records date-only FITS without inventing a timestamp",()=>{
    string source=Path.Combine(root,"calendar-source");Directory.CreateDirectory(source);string path=Path.Combine(source,"Light_M51.fit");Write(path,64,48,(x,y)=>1000,new Dictionary<string,string>{{"DATE-OBS","'2024-09-27'"}});
    var frame=Classifier.Read(path,source,"Unit-01","Auto");Check(frame.Observed==""&&frame.AcquisitionDate=="2024-09-27"&&frame.AcquisitionDateSource=="FITS date only"&&frame.AcquisitionDateLabel=="27/09/24","Date-only header lost or shifted");
    Check(Util.Deserialize<Frame>(Util.Serialize(frame)).AcquisitionDateLabel=="27/09/24","Date metadata did not round trip");
   });
   Test("Classifier combines a real cross-midnight session into one date range",()=>{
    string source=Path.Combine(root,"midnight-source");Directory.CreateDirectory(source);
    for(int i=0;i<2;i++)Write(Path.Combine(source,"Light_M51_"+i+".fit"),64,48,(x,y)=>1000+i,LightHeaders(new DateTime(2024,9,i==0?26:27,i==0?23:0,30,0),"M51"));
    // These times share an observing night in UTC; the recorded calendar days must both appear.
    var rows=Directory.GetFiles(source).Select(p=>Classifier.Read(p,source,"Unit-01","Auto")).ToList();
    Check(rows.All(f=>f.AcquisitionDateSource=="FITS UTC")&&CaptureSessions.Describe(rows).Dates=="26/09/24-27/09/24","Actual header dates did not survive classification");
   });
   Test("Filename dates are supported but folder dates and shifted nights remain unknown",()=>{
    var file=new Frame{Session="file",OriginalName="Light_M51_20240927.fit",Night="2024-09-26"};Check(CaptureSessions.Date(file).Text=="27/09/24","Date-only frame filename missed");
    var folder=new Frame{Session="folder",OriginalName="raw.fit",Observed="2024-09-27T01:00:00",TimeSource="Session folder (not frame time)",SourcePath=Path.Combine("2024-09-27","raw.fit"),Night="2024-09-26"};
    Check(CaptureSessions.Date(folder)==null&&CaptureSessions.Describe(new[]{folder}).Label=="Dates unknown","Folder/night guessed as acquisition date");
    Check(CaptureSessions.FilenameDate("capture_20241340.fit")==null,"Invalid calendar date accepted");
   });
   Test("Date collisions keep unique labels and exact session filtering",()=>{
    Frame a=Dated("session-a","2024-09-27T01:00:00Z"),b=Dated("session-b","2024-09-27T02:00:00Z"),device=a.Clone(),camera=a.Clone();device.Telescope="Unit-02";camera.Camera="Wide";
    var rows=new[]{a,b,device,camera};var choices=CaptureSessions.Choices(rows);Check(choices.Count==4&&choices.Select(c=>c.Label).Distinct().Count()==4&&choices.All(c=>c.Label.StartsWith("27/09/24 · session ")),"Same-date sessions merged or indistinguishable");
    var filters=new CaptureFilters();filters.Values["Session"]=a.SessionKey;Check(filters.Apply(rows,"").Single()==a,"Date-only label broadened session selection");
    string key=a.SessionKey;a.SourceRoot="a different import root";a.Night="another shifted night";Check(a.SessionKey==key,"Known session identity changed with source root or night");
    Check(CaptureSessions.Choices(rows.Reverse()).Select(c=>c.Label).SequenceEqual(choices.Select(c=>c.Label)),"Labels depend on input ordering");
   });
   Test("Partial dates and mixed clock bases are visible in session labels",()=>{
    Frame utc=Dated("same","2024-09-27T01:00:00Z"),filename=new Frame{Session="same",Telescope="Unit-01",Camera="Telephoto",OriginalName="raw_20240926.fit"},unknown=new Frame{Session="same",Telescope="Unit-01",Camera="Telephoto",OriginalName="raw.fit",Night="2024-09-26"};
    var choice=CaptureSessions.Choices(new[]{utc,filename,unknown}).Single();Check(choice.Label=="26/09/24-27/09/24 · partial dates · mixed clocks"&&choice.MissingDates==1&&choice.Description.Contains("timezone unknown"),"Incomplete/mixed date evidence hidden");
    var earlier=Dated("earlier","2024-08-01T12:00:00Z");Check(CaptureSessions.Choices(new[]{utc,earlier,new Frame{Session="no-date"}}).Last().Dates=="Dates unknown","Unknown date sorted before known dates");
   });
   Test("Numeric bounds use inclusive unrounded values with explicit unknown handling",()=>{
    var range=new CaptureRange{Mode=NumericFilterMode.Between,Minimum=0.125,Maximum=30};Check(range.Matches(0.125)&&range.Matches(30)&&!range.Matches(0.1249)&&!range.Matches(30.0001),"Inclusive slider boundaries rounded or expanded");
    Check(!range.Matches(null)&&!range.Matches(double.NaN)&&!range.Matches(double.PositiveInfinity),"Nonfinite numeric metadata treated as known");range.IncludeUnknown=true;Check(range.Matches(null)&&range.Matches(double.NegativeInfinity),"Unknown opt-in ignored");
    range.Mode=NumericFilterMode.Known;Check(range.Matches(-1)&&!range.Matches(null),"Known mode limited physical value or included unknown");range.Mode=NumericFilterMode.Unknown;Check(!range.Matches(0)&&range.Matches(null),"Unknown mode rejected/accepted wrong values");range.Mode=NumericFilterMode.Any;Check(range.Matches(null)&&range.Matches(1234),"Any mode still restricted values");
   });
   Test("Exposure and gain filters combine with session and alias search and clear fully",()=>{
    var rows=new[]{Dated("same","2024-09-27T01:00:00Z"),Dated("same","2024-09-27T02:00:00Z"),Dated("other","2024-09-27T03:00:00Z")};rows[0].Exposure=0;rows[0].Gain=-2;rows[1].Exposure=30.001;rows[1].Gain=80;rows[2].Exposure=60;rows[2].Gain=80;
    var filters=new CaptureFilters();filters.Values["Session"]=rows[0].SessionKey;filters.Ranges["Exposure"]=new CaptureRange{Mode=NumericFilterMode.Between,Minimum=30.001,Maximum=60};filters.Ranges["Gain"]=new CaptureRange{Mode=NumericFilterMode.Known};
    Check(filters.ActiveCount==3&&filters.Apply(rows,"Whirlpool Galaxy").Single()==rows[1],"Combined physical bounds/session/name search failed");
    Check(CaptureFilters.Number(rows[0],"Exposure")==0&&CaptureFilters.Number(rows[0],"Gain")==-2,"Zero exposure or negative gain discarded");rows[0].Exposure=-1;Check(!CaptureFilters.Number(rows[0],"Exposure").HasValue,"Negative exposure treated as a known duration");
    filters.Reset();Check(filters.ActiveCount==0&&filters.Ranges.Count==0&&filters.Apply(rows,"").Count==3,"Clear left a hidden numeric filter");
   });
   Test("Redundant controls hide constants while retaining useful unknown/known differences",()=>{
    Frame a=new Frame{Camera="Telephoto",Gain=80,Width=3840,Height=2160},b=a.Clone();var rows=new[]{a,b};
    Check(!CaptureFilters.Useful(rows,"Camera",false)&&!CaptureFilters.Useful(rows,"Gain",false)&&CaptureFilters.Useful(rows,"Gain",true),"Constant fields visible or active filter hidden");b.Gain=null;Check(CaptureFilters.Useful(rows,"Gain",false),"Unknown/known difference hidden");
    Check(new[]{"Session","Target","Review","Frame type"}.All(f=>CaptureFilters.Useful(rows,f,false))&&!CaptureFilters.Fields.Contains("Night")&&!CaptureFilters.Fields.Contains("Review type"),"Essential or redundant selectors wrong");
    Check(CaptureFilters.Value(new Frame(),"Dimensions")=="Unknown","Unrecorded dimensions shown as a real image size");
   });
   Test("Combined review selector distinguishes passes, unscreened and overlapping failures",()=>{
    var rows=new[]{new Frame{Status="New",Screened=true},new Frame{Status="New"},new Frame{Status="Unreadable",Rejected=true,IntegrityIssue="cut"},new Frame{Status="Failed",Rejected=true}};var filters=new CaptureFilters();
    filters.Values["Review"]="Passed";Check(filters.Apply(rows,"").Single()==rows[0],"Passed included unscreened");filters.Values["Review"]="Not screened";Check(filters.Apply(rows,"").Single()==rows[1],"Unscreened missed or included problems");filters.Values["Review"]="Telescope rejected / reference";Check(filters.Apply(rows,"").Count==2,"Rejected capture with another failure lost");filters.Values["Review"]="File integrity problem";Check(filters.Apply(rows,"").Single()==rows[2],"Integrity category excluded overlap");filters.Values["Review"]="Transfer failure";Check(filters.Apply(rows,"").Single()==rows[3],"Transfer category excluded overlap");
   });
   Test("Reading missing library dates preserves file identity/status and persists metadata",()=>{
    string source=Path.Combine(root,"date-refresh-source"),destination=Path.Combine(root,"date-refresh-repo");Directory.CreateDirectory(source);Write(Path.Combine(source,"capture.fit"),64,48,(x,y)=>1000,new Dictionary<string,string>{{"DATE-OBS","'2024-09-27'"}});
    using(var repo=new Repository(destination)){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var frame=repo.All().Single();frame.AcquisitionDate=frame.AcquisitionDateSource=null;frame.Status="Verified";repo.Save(frame);string path=frame.RelativePath,hash=frame.Hash;Check(repo.ReadMissingAcquisitionDates(new[]{frame},false,ct,NoProgress)==1,"Legacy date-only header not recovered");
     var recovered=repo.All().Single();Check(recovered.AcquisitionDateLabel=="27/09/24"&&recovered.Status=="Verified"&&recovered.RelativePath==path&&Util.Hash(repo.FilePath(recovered),ct)==hash,"Date refresh altered file/status/path");
     using(var db=new Database(Path.Combine(destination,".astroarchive","index.sqlite")))Check(Util.Deserialize<Frame>(db.Query("SELECT data FROM files").Single()).AcquisitionDate=="2024-09-27","Recovered date not checkpointed");
    }
   });
   Test("Reading missing import dates never overwrites indexed metadata with duplicate status",()=>{
    string source=Path.Combine(root,"date-duplicate-source");Directory.CreateDirectory(source);Write(Path.Combine(source,"capture.fit"),64,48,(x,y)=>2000,new Dictionary<string,string>{{"DATE-OBS","'2024-09-27'"}});
    using(var repo=new Repository(Path.Combine(root,"date-duplicate-repo"))){repo.Import(repo.Scan(source,"Unit-01","Auto",ct,NoProgress).Frames,ct,NoProgress);var archived=repo.All().Single();archived.AcquisitionDate=archived.AcquisitionDateSource=null;archived.Status="Verified";repo.Save(archived);var duplicate=archived.Clone();duplicate.Status="Duplicate";duplicate.SourcePath="another-source.fit";
     Check(repo.ReadMissingAcquisitionDates(new[]{duplicate},true,ct,NoProgress)==1&&duplicate.AcquisitionDateLabel=="27/09/24"&&duplicate.Status=="Duplicate","Duplicate display date missing or status changed");var saved=repo.All().Single();Check(saved.Status=="Verified"&&saved.SourcePath==archived.SourcePath,"Import display metadata replaced archive metadata");
     var candidate=archived.Clone();candidate.Status="New";candidate.Hash=null;candidate.SourcePath=Path.Combine(source,"capture.fit");candidate.SourceStamp=FileStamp.Read(candidate.SourcePath);Check(repo.ReadMissingAcquisitionDates(new[]{candidate},true,ct,NoProgress)==1&&repo.All().Count==1,"Source date refresh imported a file");
    }
   });
   Test("Missing-date refresh skips changed, missing and unreadable files and respects cancellation",()=>{
    string source=Path.Combine(root,"date-invalid-source");Directory.CreateDirectory(source);string path=Path.Combine(source,"capture.fit");Write(path,64,48,(x,y)=>1000,new Dictionary<string,string>{{"DATE-OBS","'2024-09-27'"}});var changed=new Frame{Status="New",OriginalName="raw.fit",SourcePath=path,SourceStamp=FileStamp.Read(path)};changed.SourceStamp.Size++;
    var missing=new Frame{Status="New",OriginalName="missing.fit",SourcePath=Path.Combine(source,"missing.fit")};string bad=Path.Combine(source,"bad.fit");File.WriteAllText(bad,"not FITS");var unreadable=new Frame{Status="Unreadable",OriginalName="bad.fit",SourcePath=bad};
    using(var repo=new Repository(Path.Combine(root,"date-invalid-repo"))){Check(repo.ReadMissingAcquisitionDates(new[]{changed,missing,unreadable},true,ct,NoProgress)==0&&new[]{changed,missing,unreadable}.All(f=>CaptureSessions.Date(f)==null),"Changed/invalid file acquired an invented date");using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>repo.ReadMissingAcquisitionDates(new[]{changed},true,cancel.Token,NoProgress),"Date read ignored cancellation");}}
   });
  }
 }
}
