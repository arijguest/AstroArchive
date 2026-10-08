using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void ExternalAppsTests(){
   Test("Export defaults normalize formats and never force incompatible or mixed destinations",()=>{
    var settings=new Settings{ExportDefaults=new Dictionary<string,string>{{"TIFF","gimp"},{"FITS","siril"},{"Subframe folders","dss"}}};
    Check(ExternalApps.FileType("a.FITS.gz")=="FITS"&&ExternalApps.FileType("b.FIT.FZ")=="FITS"&&ExternalApps.FileType("c.TIF")=="TIFF"&&ExternalApps.FileType("d.CR3")=="Camera RAW","File type aliases failed");
    Check(ExternalApps.Default(settings,new ExternalSelection{Paths=new List<string>{"a.tif","b.tiff"}})=="gimp","Matching defaults did not apply");
    Check(ExternalApps.Default(settings,new ExternalSelection{Paths=new List<string>{"a.tif","b.fit"}})==null,"Mixed defaults silently selected an app");
    Check(ExternalApps.Default(settings,new ExternalSelection{Paths=new List<string>{"a.fit","b.fit"}})==null,"Single-file Siril contract ignored");
    Check(ExternalApps.Default(settings,new ExternalSelection{Folder=true,Subframes=true,Paths=new List<string>{"a.fit","b.fit"}})=="dss","Folder default ignored");
    Check(ExternalApps.Default(settings,new ExternalSelection{Folder=true,Paths=new List<string>{"a.ser"}})==null,"Incompatible folder default applied");
    settings.ExportDefaults["Subframe folders"]="siril";settings.ExportDefaults["AVI"]="as4";Check(ExternalApps.Default(settings,new ExternalSelection{Folder=true,Paths=new List<string>{"a.avi"}})=="as4","Recording default overridden by subframe preference");
    settings.ExportDefaults["FITS"]="pixinsight";Check(ExternalApps.Default(settings,new ExternalSelection{Paths=new List<string>{"a.fit","b.fit"}})=="pixinsight","Multi-image default failed");
    Expect(()=>ExternalApps.Selection(new[]{new Frame{Kind="Light",OriginalName="a.fit"},new Frame{Kind="Stack",OriginalName="b.fit"}}),"Stack mixed with its subs");Expect(()=>ExternalApps.Selection(new[]{new Frame{Kind="Light",Rejected=true,OriginalName="a.fit"}}),"Rejected sub accepted");
    Check(ExternalApps.Problem(ExternalApps.Find("photoshop"),new ExternalSelection{Paths=new List<string>{"a.fits"}})!=null&&ExternalApps.Problem(ExternalApps.Find("as4"),new ExternalSelection{Folder=true,Paths=new List<string>{"a.avi","b.ser"}})==null,"Format contracts wrong");
   });
   Test("Export settings preserve legacy paths and explicit choices across serialization",()=>{
    var old=Util.Deserialize<Settings>("{\"SirilExecutable\":\"C:\\\\Apps\\\\siril.exe\"}");Check(ExternalApps.Configured(old,ExternalApps.Find("siril"))==old.SirilExecutable,"Legacy Siril setting lost");
    ExternalApps.Configure(old,ExternalApps.Find("siril"),"C:\\Siril Ω\\siril.exe");ExternalApps.Configure(old,ExternalApps.Find("stackingwizard"),"C:\\Apps\\StackingWizard.exe");old.ExportDefaults=new Dictionary<string,string>{{"FITS","siril"}};
    var restored=Util.Deserialize<Settings>(Util.Serialize(old));Check(restored.SirilExecutable==old.SirilExecutable&&restored.StackingWizardExecutable==old.StackingWizardExecutable&&restored.ExportDefaults["FITS"]=="siril"&&restored.ExternalApplicationPaths["siril"]==old.SirilExecutable,"Export settings did not round-trip");
    ExternalApps.Configure(restored,ExternalApps.Find("siril"),"");Check(ExternalApps.Configured(restored,ExternalApps.Find("siril"))==""&&restored.SirilExecutable=="","Automatic detection did not clear legacy override");
   });
   Test("External app launches use verified contracts with quoted Unicode paths",()=>{
    string folder=ExportDestinationFolder("External inputs Ω with spaces"),apps=ExportDestinationFolder("External apps Ω");string image=Path.Combine(folder,"image Ω with spaces.tiff");File.WriteAllText(image,"fixture");
    foreach(var app in ExternalApps.All){string exe=Path.Combine(apps,app.Executables[0]);File.WriteAllText(exe,"GUI fixture");ExternalApps.ValidateExecutable(app,exe);Check(ExternalApps.FirstExecutable(app,new[]{exe+"-missing",exe})==exe,"Discovery candidate validation failed");if(app.FolderTypes.Length>0&&app.Id!="dss"){var launch=ExternalApps.LaunchInfo(app,exe,new string[0],folder);Check(!launch.UseShellExecute&&launch.FileName==exe&&launch.WorkingDirectory==folder,"Folder launch changed paths or used shell");Check(launch.Arguments==(app.Id=="siril"?"--directory "+SirilHandoff.QuoteArgument(folder):""),"Invented a folder-import argument");}else if(app.Id=="gimp"||app.Id=="astrowizard"){var launch=ExternalApps.LaunchInfo(app,exe,new[]{image});Check(launch.Arguments==SirilHandoff.QuoteArgument(image)&&!launch.UseShellExecute,"Image argument malformed");}}
    var wizard=ExternalApps.Find("astrowizard");string setup=Path.Combine(apps,"AstroWizard-Setup.exe");File.WriteAllText(setup,"fixture");Expect(()=>ExternalApps.ValidateExecutable(wizard,setup),"Installer accepted as GUI");var siril=ExternalApps.Find("siril");string cli=Path.Combine(apps,"siril-cli.exe");File.WriteAllText(cli,"fixture");Expect(()=>ExternalApps.ValidateExecutable(siril,cli),"Headless executable accepted");
    Expect(()=>ExternalApps.LaunchInfo(siril,Path.Combine(apps,"siril.exe"),new[]{image,image}),"Extra images silently dropped");Expect(()=>ExternalApps.LaunchInfo(wizard,Path.Combine(apps,"AstroWizard.exe"),new[]{image+".missing"}),"Missing image accepted");Expect(()=>ExternalApps.LaunchInfo(ExternalApps.Find("gimp"),Path.Combine(apps,"gimp.exe"),new string[0],folder),"Image editor accepted a folder handoff");
    string literal=Path.Combine(folder,"literal 'quote'; Ω.tif"),script=ExternalApps.PixInsightScript(new[]{literal});Check(script.Contains("ImageWindow.open")&&script.Contains(Util.Serialize(new[]{literal}))&&script.Contains("windows[j].show()"),"PixInsight bridge treats filenames as code");
    var cancelled=new CancellationToken(true);Expect(()=>ExternalApps.Launch(wizard,Path.Combine(apps,"AstroWizard.exe"),new[]{image},null,cancelled),"Cancelled handoff launched");
   });
   Test("DSS lists use published collision paths and split incompatible capture groups",()=>{
    using(var repo=new Repository(Path.Combine(root,"dss-export-repo"))){var rows=ExportFixture(repo,"dss-export-source",true);var header=LightHeaders(new DateTime(2026,10,8,21,1,0),"M45");header["EXPTIME"]="30";string source=Path.Combine(root,"dss-extra-source");Write(Path.Combine(source,"Light_M45_second.fit"),64,48,(x,y)=>2000,header);repo.Import(repo.Scan(source,"Scope-1","Auto",ct,NoProgress).Frames,ct,NoProgress);rows=repo.All();Check(rows.Count==2,"Missing DSS fixture");string folder=ExportDestinationFolder("dss-export-inputs"),existing=Path.Combine(folder,"01_M45_Broadband.txt");File.WriteAllText(existing,"keep old file list");var options=new ExportOptions{Parent=folder,Mode="Files",IncludeCalibration=false};Exporter.Create(repo,rows,options,ct,NoProgress);var first=options.ExportedFiles.Select(f=>f.Path).ToList();Exporter.Create(repo,rows,options,ct,NoProgress);Check(options.ExportedFiles.Count==2&&options.ExportedFiles.All(f=>!first.Contains(f.Path)),"Export map retained earlier paths or lost collisions");var jobs=ExternalApps.WriteDssJobs(folder,rows,options.ExportedFiles,false,false,ct);Check(jobs.Count==2&&File.ReadAllText(existing)=="keep old file list","DSS groups mixed or list overwritten");foreach(var job in jobs){var lines=File.ReadAllLines(job.Path);Check(lines.Length==3&&lines[0]=="DSS file list"&&lines[1]=="CHECKED\tTYPE\tFILE"&&lines[2].StartsWith("1\tlight\t"),"DSS file list protocol wrong");Check(options.ExportedFiles.Any(f=>lines[2].EndsWith(f.Path))&&File.Exists(lines[2].Substring("1\tlight\t".Length)),"DSS points to originals or stale filenames");}Check(!File.Exists(Path.Combine(folder,"manifest.json")),"DSS depended on opt-in metadata");}
   });
   Test("DSS retains calibration roles and omits extra calibration for calibrated subs",()=>{
    string folder=ExportDestinationFolder("dss-calibration-roles");var light=CalibrationFixture("Light",60);light.Hash="light";light.Target="M45";light.Calibration="Uncalibrated";
    var dark=CalibrationFixture("Dark",60);dark.Hash="dark";var bias=CalibrationFixture("Bias",0);bias.Hash="bias";var flat=CalibrationFixture("Flat",0.5);flat.Hash="flat";var darkFlat=CalibrationFixture("Dark flat",0.5);darkFlat.Hash="darkflat";
    // Exercise the exact calibration decision used by folder exports.
    var accepted=Exporter.CalibrationFor(new[]{light},new List<Frame>{dark,bias},false);Check(accepted.Any(f=>f.Hash=="dark"),"Calibration fixture did not match");
    var map=new[]{light,dark,bias,flat,darkFlat}.Select(f=>{string path=Path.Combine(folder,f.Hash+".fit");File.WriteAllText(path,"fixture");return new ExportedFile{Frame=f,Path=path};}).ToList();var job=ExternalApps.WriteDssJobs(folder,new List<Frame>{light},map,true,false,ct).Single();string list=File.ReadAllText(job.Path);foreach(string role in new[]{"dark","offset","flat","darkflat"})Check(list.Contains("1\t"+role+"\t"),"Calibration role lost: "+role);light.Calibration="Calibrated";var calibrated=ExternalApps.WriteDssJobs(folder,new List<Frame>{light},map,true,false,ct).Single();Check(File.ReadAllLines(calibrated.Path).Length==3,"Already calibrated subs received extra calibration");
   });
  }
 }
}
