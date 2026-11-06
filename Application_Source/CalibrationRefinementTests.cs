// Synthetic calibration recognition, fallback ranking and verified export checks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void CalibrationRefinementTests(){
   Test("Temperature fallback preserves exact-before-partial calibration ranking",()=>{
    var light=CalibrationFixture("Light",10);light.Calibration="Uncalibrated";
    var exact=light.Clone();exact.Kind="Master dark";exact.Hash="exact-distant";exact.Temperature=8;
    var partial=exact.Clone();partial.Hash="partial-nearby";partial.Temperature=-9;partial.CameraId=null;
    Check(CalibrationMatching.Evaluate(light,partial).Usable&&!CalibrationMatching.Evaluate(light,partial).Accepted,"Newer partial-match semantics were lost");
    Check(CalibrationMatching.Choose(new[]{light},new[]{partial,exact},"Dark").Single()==exact,"A partial match displaced a fully identified calibration");
    Check(CalibrationMatching.Choose(new[]{light},new[]{partial},"Dark").Single()==partial,"A usable partial calibration was dropped when it was the only candidate");
    partial.Gain=null;Check(!CalibrationMatching.Evaluate(light,partial).Usable,"Temperature fallback relaxed an unknown-gain safeguard");
   });
   Test("Distant temperature calibration remains usable with a clear fallback warning",()=>{
    var light=CalibrationFixture("Light",10);light.Calibration="Uncalibrated";
    var dark=light.Clone();dark.Kind="Dark";dark.Hash="distant";dark.OriginalName="Dark_10s.fit";dark.Temperature=8;
    var farther=dark.Clone();farther.Hash="farther";farther.Temperature=20;
    Check(CalibrationMatching.Evaluate(light,dark).Accepted,"Temperature difference still blocks otherwise compatible calibration");
    Check(CalibrationMatching.Evaluate(light,dark).Reasons.Any(r=>r.Contains("18 C")),"Temperature advisory is absent");
    var selected=CalibrationMatching.Select(new[]{light},new[]{dark,farther});
    Check(selected.Count==1&&selected[0]==dark,"Fallback mixed different distant temperature sets");
    Check(CalibrationMatching.Warnings(new[]{light},selected).Single().Contains("No closer compatible"),"Fallback warning does not explain the choice");
    var closer=dark.Clone();closer.Hash="nearby";closer.Temperature=-9;
    selected=CalibrationMatching.Select(new[]{light},new[]{dark,farther,closer});
    Check(selected.Count==1&&selected[0]==closer&&CalibrationMatching.Warnings(new[]{light},selected).Count==0,"Closer calibration was ignored or unnecessarily warned");
    closer.Gain=101;selected=CalibrationMatching.Select(new[]{light},new[]{dark,closer});
    Check(selected.Count==1&&selected[0]==dark,"Temperature preference overrode the gain safety check");
    dark.Temperature=null;Check(!CalibrationMatching.Evaluate(light,dark).Accepted,"Unknown sensor temperature was treated as known");
   });
   Test("A nearby master takes precedence over distant raw darks and dark flats remain scoped to flats",()=>{
    var light=CalibrationFixture("Light",10);light.Calibration="Uncalibrated";
    var raw=light.Clone();raw.Kind="Dark";raw.Hash="raw";raw.Temperature=2;
    var master=raw.Clone();master.Kind="Master dark";master.Hash="master";master.Temperature=-9;master.StackCount=20;
    Check(Exporter.CalibrationFor(new[]{light},new List<Frame>{raw,master},false).Single()==master,"Distant raw dark displaced a better matching master");
    var flat=light.Clone();flat.Kind="Flat";flat.Exposure=0.5;
    var darkFlat=flat.Clone();darkFlat.Kind="Dark flat";darkFlat.Hash="dark-flat";darkFlat.Temperature=8;
    Check(CalibrationMatching.Select(new[]{flat},new[]{darkFlat}).Single()==darkFlat,"Distant matching dark flat rejected");
    Check(CalibrationMatching.Select(new[]{light},new[]{darkFlat}).Count==0,"Dark flat supplied to lights");
    Check(CalibrationMatching.Warnings(new[]{flat},new[]{darkFlat}).Count==1,"Dark-flat temperature fallback not flagged");
   });
   Test("Seestar compound calibration names and combined-frame headers identify masters",()=>{
    string source=Path.Combine(root,"seestar-calibration-recognition","MyWorks");
    var cases=new[]{Tuple.Create("MasterDark_10.0s_8C.fit","Master dark"),Tuple.Create("MasterFlat_0.5s.fit","Master flat"),Tuple.Create("MasterBias.fit","Master bias"),Tuple.Create("DarkFlat_0.5s.fit","Dark flat"),Tuple.Create("dark_10.0s.fit","Master dark")};
    foreach(var item in cases){
     string file=Path.Combine(source,item.Item1);var header=new Dictionary<string,string>{{"TELESCOP","'Seestar S50'"},{"CAMID","'0'"},{"XBINNING","1"},{"YBINNING","1"}};
     if(item.Item1.StartsWith("dark_")){header["IMAGETYP"]="'Dark'";header["NCOMBINE"]="20";}
     Write(file,64,48,(x,y)=>1200,header);var frame=Classifier.Read(file,source,"Seestar-01","Auto");
     Check(frame.Make=="Seestar"&&frame.Kind==item.Item2&&frame.Target=="Calibration"&&frame.Calibration=="Calibration frame",item.Item1+" was misidentified as "+frame.Kind+" / "+frame.Calibration);
    }
    string explicitMaster=Path.Combine(source,"calibration.fit");Write(explicitMaster,64,48,(x,y)=>1300,new Dictionary<string,string>{{"IMAGETYP","'Master Dark'"},{"TELESCOP","'Seestar S50'"}});
    Check(Classifier.Read(explicitMaster,source,"Seestar-01","Auto").Kind=="Master dark","Explicit master header was ignored");
   });
   Test("Explicit capture evidence outranks weak calibration folders while specific master names remain recognised",()=>{
    string source=Path.Combine(root,"calibration-evidence-priority");
    foreach(string folder in new[]{"dark","flat","CALI_FRAME"}){
     string path=Path.Combine(source,folder,"Light_M45.fit");Write(path,64,48,(x,y)=>1800,LightHeaders(new DateTime(2026,10,6,21,0,0),"M45"));
     var frame=Classifier.Read(path,source,"Seestar-01","Seestar S50");Check(frame.Kind=="Light"&&frame.Target=="M45","Parent "+folder+" overrode the LIGHT header/filename");
    }
    string namedMaster=Path.Combine(source,"flat","MasterDark_10s.fit");Write(namedMaster,64,48,(x,y)=>1200,new Dictionary<string,string>{{"IMAGETYP","'LIGHT'"}});
    Check(Classifier.Read(namedMaster,source,"Seestar-01","Seestar S50").Kind=="Master dark","Specific device master filename was lost to generic LIGHT or parent flat hint");
    string headerFlat=Path.Combine(source,"dark","calibration.fit");Write(headerFlat,64,48,(x,y)=>2000,new Dictionary<string,string>{{"IMAGETYP","'FLAT'"}});
    Check(Classifier.Read(headerFlat,source,"Seestar-01","Seestar S50").Kind=="Flat","Parent dark hint converted a specific FLAT header to a dark flat");
    string noType=Path.Combine(source,"dark","Light_M45_001.fit");Write(noType,64,48,(x,y)=>1600,new Dictionary<string,string>());
    Check(Classifier.Read(noType,source,"Seestar-01","Seestar S50").Kind=="Light","Light filename did not outrank weak parent dark hint");
   });
   Test("Stacking folder export includes distant compatible calibration and records the advisory",()=>{
    string source=Path.Combine(root,"calibration-fallback-export-source");
    var lightHeader=LightHeaders(new DateTime(2026,10,6,21,0,0),"M45");lightHeader["XBINNING"]=lightHeader["YBINNING"]="1";lightHeader["EXPTIME"]="10";lightHeader["CCD-TEMP"]="-10";
    string lightFile=Path.Combine(source,"Light_M45.fit");Write(lightFile,64,48,(x,y)=>1800+x,lightHeader);
    var darkHeader=new Dictionary<string,string>(lightHeader);darkHeader["IMAGETYP"]="'Master Dark'";darkHeader["NCOMBINE"]="20";darkHeader["CCD-TEMP"]="8";
    string darkFile=Path.Combine(source,"MasterDark.fit");Write(darkFile,64,48,(x,y)=>1200+x,darkHeader);
    using(var repo=new Repository(Path.Combine(root,"calibration-fallback-export-repo"))){
     repo.Import(repo.Scan(source,"Seestar-01","Seestar S50",ct,NoProgress).Frames,ct,NoProgress);
     var light=repo.All().Single(f=>f.Kind=="Light");light.Calibration="Uncalibrated";repo.Save(light);
     string folder=ExportDestinationFolder("calibration-fallback-export");var result=new ExportResult();
     Exporter.Create(repo,new List<Frame>{light},new ExportOptions{Parent=folder,Mode="Subs",IncludeCalibration=true,AddMetadata=true},ct,NoProgress,result);
     Check(result.Files.Count==2&&result.Files.Any(f=>f.Frame.Kind=="Master dark"),"Fallback calibration was omitted from stacking folders");
     Check(File.ReadAllText(Path.Combine(folder,"READ_ME.txt")).Contains("18 C")&&File.ReadAllText(Path.Combine(folder,"manifest.json")).Contains("No closer compatible"),"Export documentation omitted temperature fallback warning");
     Check(repo.All().Count==2&&File.Exists(lightFile)&&File.Exists(darkFile)&&result.Files.All(f=>Util.Hash(f.Path,ct)==f.Frame.Hash),"Calibration export changed or removed source data");
    }
   });
  }
 }
}
