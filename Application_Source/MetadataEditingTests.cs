using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static Frame EditableFixture(){return new Frame{OriginalName="Light_M33.fit",Target="M33",TargetEvidence="FITS OBJECT",Telescope="Unit-01",Model="Custom observatory",Camera="Telephoto",Kind="Light",Mount="Unknown",MountEvidence="Not analyzed",Exposure=30.123456789,Gain=0,Temperature=-12.75,Filter="Broadband",Calibration="Custom calibration",BinX=2,BinY=1,TelescopeModel="Reflector",CameraModel="Camera 1",CameraId="Serial 123",Offset=0,ReadoutMode="Slow",Roi="0,0,128,96",OpticalConfiguration="Reducer",TimeZoneId="UTC",LinearData=false,GainUnit="dB",Bayer="RGGB",RegistrationState="Unregistered",CalibrationSteps="Dark, flat",Notes="Recorded notes",Observed="2026-10-06T22:00:00",ObservedUtc="2026-10-06T22:00:00Z",TimeSource="FITS",Facts=new Dictionary<string,MetadataFact>{{"Exposure",new MetadataFact{Value="30.123456789",Raw="EXPTIME=30.123456789",Source="FITS header",Unit="s"}}}};}
  static void MetadataEditingTests(){
   Test("Single metadata editor populates every editable value, including zero and custom choices",()=>{
    var f=EditableFixture();var model=new MetadataEditing(new[]{f});
    foreach(var value in model.Values)Check(!value.Mixed&&value.Initial.Length>0&&!value.Changed,"Missing or dirty initial field: "+value.Field.Key);
    Check(model["Exposure"].Initial=="30.123456789"&&model["Gain"].Initial=="0"&&model["Offset"].Initial=="0"&&model["Temperature"].Initial=="-12.75","Numeric precision/zero/sign lost");
    Check(model["Binning"].Initial=="2x1"&&model["LinearData"].Initial=="Processed / stretched"&&model["Model"].Initial=="Custom observatory"&&model["Calibration"].Initial=="Custom calibration","Current custom choices/false state lost");
   });
   Test("Opening and saving unchanged metadata preserves facts, inferred mount, times and units",()=>{
    var f=EditableFixture();string before=Util.Serialize(f);var patch=new MetadataEditing(new[]{f}).Patch();Check(patch.Count==0&&patch.Validate()==null,"Untouched form generated changes");
    // CLR reflection cache order can change JSON member order after sort getters.
    var expected=Util.Json().DeserializeObject(before);
    Check(SameSnapshotValue(Util.Json().DeserializeObject(Util.Serialize(patch.Apply(f))),expected)&&SameSnapshotValue(Util.Json().DeserializeObject(Util.Serialize(f)),expected),"No-op rewrote evidence or metadata");
   });
   Test("Batch metadata distinguishes common, missing and mixed values without adopting the first file",()=>{
    var a=EditableFixture();var b=a.Clone();b.Exposure=60;b.Gain=null;b.CameraId=null;b.BinY=2;b.LinearData=true;b.Target="M45";
    var model=new MetadataEditing(new[]{a,b});foreach(string key in new[]{"Exposure","Gain","CameraId","Binning","LinearData","Target"})Check(model[key].Mixed&&model[key].Initial==""&&!model[key].Changed,"Mixed value copied: "+key);
    Check(!model["Filter"].Mixed&&model["Filter"].Initial=="Broadband","Shared filter lost");
    model["Filter"].Text="Dual band";var patch=model.Patch();Check(patch.Count==1,"Unedited mixed fields included");
    var first=patch.Apply(a);var second=patch.Apply(b);Check(first.Filter=="Dual band"&&second.Filter=="Dual band"&&first.Exposure==a.Exposure&&second.Exposure==b.Exposure&&second.Gain==null&&second.Target=="M45","Batch replacement leaked into unrelated metadata");
   });
   Test("Session-wide metadata patches change only entered fields on other session members",()=>{
    var a=EditableFixture();var other=a.Clone();other.Target="M45";other.Model="Dwarf 3";other.Gain=75;other.GainUnit="camera units";other.TimeZoneId="";
    var model=new MetadataEditing(new[]{a});model["Exposure"].Text="120";var changed=model.Patch().Apply(other);
    Check(changed.Exposure==120&&changed.Target==other.Target&&changed.Model==other.Model&&changed.Gain==other.Gain&&changed.TimeZoneId==other.TimeZoneId,"Shared originals copied into session expansion");
    Check(changed.Facts["Exposure"].Source=="User"&&changed.Facts.Count==1,"Unchanged fields attributed to user");
   });
   Test("Mixed fields accept zero, false and unknown as explicit replacements",()=>{
    var a=EditableFixture();var b=a.Clone();b.Gain=80;b.LinearData=true;b.Mount="EQ";var model=new MetadataEditing(new[]{a,b});model["Gain"].Text="0";model["LinearData"].Text="Processed / stretched";model["Mount"].Text="Unknown";
    var patch=model.Patch();Check(patch.Count==3&&patch.Validate()==null,"Falsy replacement ignored");var result=patch.Apply(b);Check(result.Gain==0&&result.LinearData==false&&result.Mount=="Unknown"&&result.MountEvidence=="User assigned","Replacement was not stored");
   });
   Test("Metadata formatting changes, resets and blanks do not create overrides",()=>{
    var model=new MetadataEditing(new[]{EditableFixture()});model["Gain"].Text="0.000";model["Binning"].Text="2*1";model["CameraId"].Text=" ";model["Filter"].Text="Broadband ";Check(model.Patch().Count==0,"Formatting-only/blank edit generated changes");
    model["Exposure"].Text="60";Check(model.Patch().Count==1,"Edit missing");model["Exposure"].Text=model["Exposure"].Initial;Check(model.Patch().Count==0,"Reset retained an override");
   });
   Test("Metadata numeric editing is invariant and retains precise acquisition values",()=>{
    var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new CultureInfo("fr-FR");var f=EditableFixture();var model=new MetadataEditing(new[]{f});Check(model["Exposure"].Initial=="30.123456789","Display uses local comma");model["Temperature"].Text="-0.123456789";Check(model.Patch().Apply(f).Temperature==-0.123456789,"Precision lost on save");model["Exposure"].Text="30,5";Check(model.Patch().Validate()!=null,"Comma accepted as thousands/decimal separator");}finally{CultureInfo.CurrentCulture=culture;}
   });
   Test("Legacy invalid optional values can be inspected and unrelated metadata changed",()=>{
    var f=EditableFixture();f.Exposure=double.NaN;f.Offset=double.PositiveInfinity;f.TimeZoneId="Old timezone";f.BinX=0;var model=new MetadataEditing(new[]{f});Check(model.Patch().Count==0&&model.Patch().Validate()==null,"Unchanged legacy value blocks save");model["Filter"].Text="Ha";Check(model.Patch().Validate()==null&&model.Patch().Apply(f).Filter=="Ha","Unrelated edit validates all original metadata");
    foreach(string invalid in new[]{"NaN","Infinity","1e999","text"}){model["Temperature"].Text=invalid;Check(model.Patch().Validate()!=null,"Invalid changed numeric accepted: "+invalid);}
   });
   Test("Inferred metadata mount remains a suggestion until explicitly confirmed",()=>{
    foreach(string stored in new[]{"Unknown","EQ (likely)","EQ?"}){var f=EditableFixture();f.Mount=stored;var model=new MetadataEditing(new[]{f});Check(model["Mount"].Initial=="EQ?"&&!model["Mount"].Changed,"Inference is missing or dirty");model["Mount"].Text="EQ";var result=model.Patch().Apply(f);Check(result.Mount=="EQ"&&result.MountEvidence=="User assigned"&&f.Mount==stored,"Confirmation not explicit or original mutated");}
   });
   Test("Changed metadata marks only its own facts and preserves recorded gain units",()=>{
    var f=EditableFixture();var model=new MetadataEditing(new[]{f});model["Gain"].Text="60";var result=model.Patch().Apply(f);Check(result.GainUnit=="dB"&&result.Facts["Exposure"].Source=="FITS header"&&result.Facts["Gain"].Source=="User","Units or untouched provenance overwritten");
    f.GainUnit=null;Check(model.Patch().Apply(f).GainUnit=="camera units","New gain missing default unit");model["GainUnit"].Text="ISO";Check(model.Patch().Apply(f).GainUnit=="ISO","Explicit unit replaced by default");
   });
   Test("Timezone edit recalculates UTC and records timezone even when timestamp is absent",()=>{
    var f=EditableFixture();var patch=new MetadataPatch(new Dictionary<string,string>{{"TimeZoneId","UTC"}});var result=patch.Apply(f);Check(Util.Time(result.ObservedUtc)==Util.Time(f.Observed)&&result.TimeSource=="User timezone"&&result.Facts["ObservedUtc"].Source=="User","UTC correction/evidence missing");f.Observed="";result=patch.Apply(f);Check(result.TimeZoneId=="UTC"&&result.Facts["TimeZoneId"].Source=="User"&&result.TimeSource==f.TimeSource,"Missing timestamp prevented timezone assignment");
    Check(new MetadataPatch(new Dictionary<string,string>{{"TimeZoneId","Bad timezone"}}).Validate()!=null,"Unknown timezone accepted");Check(new MetadataPatch(new Dictionary<string,string>{{"Binning","0x2"}}).Validate()!=null,"Invalid binning accepted");
   });
   Test("Current metadata inspection includes fields beyond editor inputs and their evidence",()=>{
    var f=EditableFixture();f.RA=56.12345;f.Dec=-14.125;f.Width=128;f.Height=96;f.AcquisitionSoftware="Device software";var details=MetadataDetail.For(f);
    Check(details.Any(r=>r.Field=="RA"&&r.Value=="56.12345")&&details.Any(r=>r.Field=="Acquisition Software"&&r.Value=="Device software")&&details.Any(r=>r.Field=="Exposure"&&r.Source=="FITS header · s"),"Recorded metadata/provenance hidden");
   });
   Test("Metadata editing persists through refile without changing source or image bytes",()=>{
    string source=Path.Combine(root,"metadata-edit-source");Directory.CreateDirectory(source);string path=Path.Combine(source,"Light_M33.fit");Write(path,64,48,(x,y)=>1700,LightHeaders(new DateTime(2026,10,6,22,0,0),"M33"));string hash=Util.Hash(path,ct);
    using(var repo=new Repository(Path.Combine(root,"metadata-edit-repo"))){var scan=repo.Scan(source,"Unit-01","Auto",ct,NoProgress);repo.Import(scan.Frames,ct,NoProgress);var before=repo.All().Single();var model=new MetadataEditing(new[]{before});model["Target"].Text="C27";model["CameraId"].Text="Camera serial";repo.Refile(model.Patch().Apply(before),ct);var after=repo.All().Single();Check(after.Target=="NGC6888"&&after.CameraId=="Camera serial"&&after.Exposure==before.Exposure&&after.Gain==before.Gain,"Saved patch missing or untouched values changed");Check(Util.Hash(repo.FilePath(after),ct)==hash&&Util.Hash(path,ct)==hash,"Metadata changed original image bytes");Check(after.Facts["CameraId"].Source=="User","User evidence did not persist");}
   });
  }
 }
}
