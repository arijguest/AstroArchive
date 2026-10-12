// AstroBin acquisition CSV: documented columns, aggregation, double-counting rules and metadata that is never invented.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace AstroArchive {
 public partial class Tests {
  static Frame AstroBinLight(string hash,string date,double exposure=60,string target="M45"){var f=CalibrationFixture("Light",exposure);f.Hash=hash;f.OriginalName=hash+".fit";f.Target=target;f.Calibration="Uncalibrated";f.AcquisitionDate=date;f.AcquisitionDateSource="FITS UTC";f.Session="session-"+date;return f;}
  static string[] CsvLines(string csv){Check(csv.EndsWith("\r\n")&&!csv.Contains(" ,")&&!csv.Contains(", "),"CSV line endings or comma spacing are wrong: "+csv);return csv.Substring(0,csv.Length-2).Split(new[]{"\r\n"},StringSplitOptions.None);}
  static void AstroBinTests(){
   Test("AstroBin CSV aggregates matching subs under AstroBin's documented headers",()=>{
    Check(string.Join(",",AstroBinCsv.LongExposureColumns)=="date,filter,number,duration,iso,binning,gain,sensorCooling,fNumber,darks,flats,flatDarks,bias,bortle,meanSqm,meanFwhm,temperature","Long-exposure columns differ from AstroBin's specification.");
    Check(string.Join(",",AstroBinCsv.VideoColumns)=="date,time,frames,fps,exposurePerFrame,focalLength,iso,gain,seeing,transparency,cmi,cmii,cmiii","Video columns differ from AstroBin's specification.");
    var frames=new List<Frame>{AstroBinLight("a","2026-09-19"),AstroBinLight("b","2026-09-19"),AstroBinLight("c","2026-09-19"),AstroBinLight("d","2026-09-20",120),AstroBinLight("e","2026-09-20",120)};frames[1].Temperature=-9.6;
    var report=AstroBinCsv.Build(frames,new AstroBinOptions{CountCalibrations=false});var sheet=report.Sheets.Single();var lines=CsvLines(sheet.Csv);
    Check(lines.Length==3&&lines[0]=="date,number,duration,binning,gain,sensorCooling"&&lines[1]=="2026-09-19,3,60,1,100,-10"&&lines[2]=="2026-09-20,2,120,1,100,-10","Matching subs were not aggregated by date and exposure: "+sheet.Csv);
    Check(sheet.Frames==5&&sheet.IntegrationSeconds==420&&sheet.Csv[0]=='d',"Integration total or encoding is wrong.");
   });
   Test("AstroBin CSV never invents filters, gain, cooling or dates",()=>{
    var light=AstroBinLight("only","");light.AcquisitionDate=null;light.Observed=null;light.ObservedUtc=null;light.Gain=null;light.Temperature=null;light.BinX=light.BinY=0;light.Filter="Ha";
    var report=AstroBinCsv.Build(new[]{light},new AstroBinOptions{CountCalibrations=false});var sheet=report.Sheets.Single();var lines=CsvLines(sheet.Csv);
    Check(lines[0]=="number,duration"&&lines[1]=="1,60","Unknown values were written: "+sheet.Csv);
    Check(sheet.Notes.Any(n=>n.Contains("No AstroBin filter ID for Ha"))&&sheet.Notes.Any(n=>n.Contains("no recorded acquisition date")),"Missing filter ID or date was not flagged.");
    var mapped=AstroBinCsv.Build(new[]{light},new AstroBinOptions{CountCalibrations=false,FilterIds=new Dictionary<string,string>{{"ha","https://app.astrobin.com/equipment/explorer/filter/4663/astronomik-h-alpha"}}}).Sheets.Single();
    Check(CsvLines(mapped.Csv)[0]=="filter,number,duration"&&CsvLines(mapped.Csv)[1]=="4663,1,60"&&!mapped.Notes.Any(n=>n.Contains("filter ID")),"Filter ID mapping or page address parsing failed: "+mapped.Csv);
    Check(AstroBinCsv.FilterId("4049")=="4049"&&AstroBinCsv.FilterId("Ha")==null&&AstroBinCsv.FilterId("0")==null&&AstroBinCsv.FilterId("https://example.com/filter/12/")==null,"Filter ID validation accepted invented or foreign values.");
   });
   Test("AstroBin CSV excludes calibrations, duplicates, rejected and unknown-exposure frames from integration",()=>{
    var a=AstroBinLight("a","2026-09-19");var copy=a.Clone();var sameMoment=AstroBinLight("a-resaved","2026-09-19");a.ObservedUtc=sameMoment.ObservedUtc="2026-09-19T22:01:05.0000000Z";
    var rejected=AstroBinLight("rejected","2026-09-19");rejected.Rejected=true;var unknown=AstroBinLight("no-exposure","2026-09-19");unknown.Exposure=null;
    var dark=CalibrationFixture("Dark",60);dark.Hash="dark";dark.OriginalName="dark.fit";var report=AstroBinCsv.Build(new[]{a,copy,sameMoment,rejected,unknown,dark});var sheet=report.Sheets.Single();var lines=CsvLines(sheet.Csv);
    Check(sheet.Frames==1&&lines[0]=="date,number,duration,binning,gain,sensorCooling,darks"&&lines[1]=="2026-09-19,1,60,1,100,-10,1","Calibrations, duplicates or rejected frames entered the integration: "+sheet.Csv);
    Check(report.Notes.Any(n=>n.StartsWith("2 duplicate exposures"))&&report.Notes.Any(n=>n.StartsWith("1 rejected"))&&sheet.Notes.Any(n=>n.Contains("without a recorded exposure"))&&report.Notes.Any(n=>n.StartsWith("1 selected calibration frame")),"Exclusions were not reported.");
   });
   Test("AstroBin CSV counts matching calibrations once and leaves unknown master counts blank",()=>{
    var a=AstroBinLight("a","2026-09-19");var b=AstroBinLight("b","2026-09-20");var dark=CalibrationFixture("Dark",60);dark.Hash="dark";var dark2=dark.Clone();dark2.Hash="dark2";var flat=CalibrationFixture("Flat",0.5);flat.Hash="flat";var master=CalibrationFixture("Master bias",0.001);master.Hash="bias";master.StackCount=0;
    var sheet=AstroBinCsv.Build(new[]{a,b},new AstroBinOptions{CalibrationPool=new[]{dark,dark2,flat,master}}).Sheets.Single();var lines=CsvLines(sheet.Csv);
    Check(lines[0].EndsWith(",darks,flats")&&lines[1].EndsWith(",2,1")&&lines[2].EndsWith(",,"),"Shared calibrations were double-counted or an unknown master count was invented: "+sheet.Csv);
    Check(sheet.Notes.Any(n=>n.Contains("counted once"))&&sheet.Notes.Any(n=>n.Contains("do not record how many frames")),"Calibration sharing or unknown master count was not flagged.");
    master.StackCount=25;Check(CsvLines(AstroBinCsv.Build(new[]{a},new AstroBinOptions{CalibrationPool=new[]{dark,master}}).Sheets.Single().Csv)[1].EndsWith(",1,25"),"Master frame count was not used.");
    a.Calibration="Unknown";var unknown=AstroBinCsv.Build(new[]{a},new AstroBinOptions{CalibrationPool=new[]{dark}});Check(!unknown.Sheets.Single().Csv.Contains("darks")&&unknown.Notes.Any(n=>n.Contains("unknown calibration state")),"Unknown-state subs received calibration counts without confirmation.");
    Check(AstroBinCsv.Build(new[]{a},new AstroBinOptions{CalibrationPool=new[]{dark},IncludeUnknownCalibration=true}).Sheets.Single().Csv.Contains("darks"),"Confirmed unknown-state subs lost their calibration counts.");
   });
   Test("AstroBin CSV avoids double-counting stacks and overlapping snapshots",()=>{
    var sub=AstroBinLight("sub","2026-09-19",10);var covered=AstroBinLight("covered","2026-09-19",10);covered.Kind="Stack";covered.StackCount=30;covered.Exposure=300;
    var report=AstroBinCsv.Build(new[]{sub,covered},new AstroBinOptions{CountCalibrations=false});Check(report.Sheets.Single().Frames==1&&report.Notes.Any(n=>n.Contains("subs from the same session")),"A stack was counted with its own subs.");
    var early=AstroBinLight("early","2026-09-21",10);early.Kind="Stack";early.StackCount=30;early.Exposure=300;var late=early.Clone();late.Hash="late";late.OriginalName="late.fit";late.StackCount=90;late.Exposure=900;var unknown=early.Clone();unknown.Hash="uncounted";unknown.OriginalName="uncounted.fit";unknown.Session="other";unknown.StackCount=0;
    report=AstroBinCsv.Build(new[]{early,late,unknown},new AstroBinOptions{CountCalibrations=false});var sheet=report.Sheets.Single();var lines=CsvLines(sheet.Csv);
    Check(lines[1]=="2026-09-21,90,10,1,100,-10"&&sheet.IntegrationSeconds==900,"Stack sub count and per-sub duration were not used, or snapshots were summed: "+sheet.Csv);
    Check(report.Notes.Any(n=>n.Contains("snapshots can overlap"))&&sheet.Notes.Any(n=>n.Contains("without a recorded sub count")),"Overlap or missing stack count was not flagged.");
   });
   Test("AstroBin CSV separates targets, equipment and sessions",()=>{
    var a=AstroBinLight("a","2026-09-20");var b=AstroBinLight("b","2026-09-19");b.Telescope="Second rig";var c=AstroBinLight("c","2026-09-19",60,"M31");var d=AstroBinLight("d","2026-09-20");d.Temperature=-4;
    var report=AstroBinCsv.Build(new[]{a,b,c,d},new AstroBinOptions{CountCalibrations=false});Check(report.Sheets.Count==2&&report.Sheets.Select(s=>s.Target).OrderBy(t=>t).SequenceEqual(new[]{"M31","M45"}),"Targets were combined into one AstroBin CSV.");
    var m45=report.Sheets.Single(s=>s.Target=="M45");Check(m45.Rows.Count==2&&m45.Rows[0].Date=="2026-09-19"&&m45.Rows.Select(r=>r.Equipment).Distinct().Count()==2&&m45.Rows.Last().Date=="2026-09-20","Equipment or dates were merged or misordered.");
    Check(m45.Notes.Any(n=>n.Contains("Sensor temperature varies"))&&m45.Rows.Any(r=>r.SensorCooling==-7),"Varying sensor temperature was not averaged and flagged.");
    Check(report.NotesText().Contains("M31")&&report.NotesText(m45).Contains("2026-09-19"),"Notes omitted targets or rows.");
   });
   Test("AstroBin video CSV uses recorded frame counts and leaves local times blank",()=>{
    var video=new Frame{Hash="ser",OriginalName="jupiter.ser",Kind="Video",Target="Jupiter",Telescope="C8",Camera="Planetary",Gain=250,AcquisitionDate="2026-09-19",ObservedUtc="2026-09-19T23:41:00.0000000Z",Images=new List<ImageDescriptor>{new ImageDescriptor{Key="sequence:0",Count=5000,Headers=new Dictionary<string,string>{{"FOCALLEN","2032"}}}}};
    var sheet=AstroBinCsv.Build(new[]{video}).Sheets.Single();var lines=CsvLines(sheet.VideoCsv);
    Check(sheet.Csv.Length==0&&lines[0]=="date,frames,focalLength,gain"&&lines[1]=="2026-09-19,5000,2032,250","Video acquisition columns are wrong: "+sheet.VideoCsv);
    Check(sheet.Videos.Single().UtcStart=="23:41 UTC"&&sheet.Notes.Any(n=>n.Contains("local start time")),"UTC start time was written as local time or not reported.");
   });
   Test("Export window summaries count files and shorten long target lists",()=>{
    var frames=new List<Frame>{AstroBinLight("a","2026-09-19"),AstroBinLight("b","2026-09-19"),AstroBinLight("c","2026-09-19",60,"M31")};var dark=CalibrationFixture("Dark",60);dark.Target="Unknown";frames.Add(dark);
    var summary=ExportSummaries.Describe(frames,true);Check(summary.Title=="4 selected files"&&summary.Targets==frames[0].TargetLabel+", "+frames[2].TargetLabel&&summary.Detail=="3 subs · 1 calibration frame","Selection summary is wrong: "+summary.Title+" | "+summary.Targets+" | "+summary.Detail);
    Check(ExportSummaries.Describe(frames.Take(1).ToList(),false).Title=="1 file in view"&&ExportSummaries.Describe(new List<Frame>(),true).Title=="No files selected","Visible or empty summaries are wrong.");
    var many=new ExportSummary();ExportSummaries.Targets(new[]{"M45","M31","M31","NGC 7000","IC 1396","Unknown",""},many);Check(many.Targets=="M31, IC 1396 and 2 more targets"&&many.TargetList.Split('\n').Length==4,"Long target lists were not shortened: "+many.Targets);
   });
   Test("Stacking exports write the AstroBin CSV only when requested",()=>{
    using(var repo=new Repository(Path.Combine(root,"astrobin-export-repo"))){
     var frames=ExportFixture(repo,"astrobin-export-source",true);string folder=ExportDestinationFolder("astrobin-export-off");Exporter.Create(repo,frames,new ExportOptions{Parent=folder,Mode="Subs"},ct,NoProgress);Check(!Directory.EnumerateFiles(folder,"astrobin*",SearchOption.AllDirectories).Any(),"Default exports changed: an AstroBin CSV was written without the option.");
     folder=ExportDestinationFolder("astrobin-export-on");var options=new ExportOptions{Parent=folder,Mode="Subs",IncludeAstroBinCsv=true};Exporter.Create(repo,frames,options,ct,NoProgress);string csv=Path.Combine(folder,"astrobin_acquisition.csv");
     Check(File.Exists(csv)&&File.Exists(Path.Combine(folder,"astrobin_acquisition_notes.txt"))&&options.AstroBinFiles.Count==2,"AstroBin files were not written next to the stacking folder.");
     var bytes=File.ReadAllBytes(csv);Check(bytes.Length>3&&bytes[0]==(byte)'d',"AstroBin CSV has a byte-order mark or wrong header.");var lines=CsvLines(Encoding.UTF8.GetString(bytes));
     Check(lines.Length==2&&lines[0].StartsWith("date,number,duration")&&lines[1].StartsWith("2026-10-08,1,60"),"Exported AstroBin CSV does not describe the exported sub: "+string.Join(" | ",lines));
    }
   });
  }
 }
}
