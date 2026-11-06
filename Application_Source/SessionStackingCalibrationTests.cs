// Real local Siril coverage for raw flat preparation and calibration retention.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void SessionStackingCalibrationTests(){
   WindowsTest("Installed Siril prepares raw flats with dark flats and retains every calibration original",()=>{
    if(SkipInstalledSiril){skipped++;Console.WriteLine("SKIP optional installed Siril calibration recipe acceptance");return;}
    string executable;try{executable=SessionStacking.ResolveExecutable(null);}catch(IOException){skipped++;Console.WriteLine("SKIP optional installed Siril calibration acceptance");return;}
    string previousRoot=root;root=Path.Combine(Path.GetTempPath(),"AAcal-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(root);
    try{
    using(var repo=new Repository(Path.Combine(root,"session-stack-calibration-recipe-repo"))){
     var lights=StackFixture(repo,"calibration-recipe",3,false);
     foreach(var light in lights){light.Calibration="Uncalibrated";light.BinX=light.BinY=1;light.Filter="L";repo.Save(light);}
     string source=Path.Combine(root,"session-stack-calibration-recipe-source");
     foreach(string kind in new[]{"Dark","Flat","Dark flat","Bias"})for(int i=0;i<3;i++){
      int index=i;var header=LightHeaders(new DateTime(2026,10,8,20,0,0),"Calibration");header["IMAGETYP"]="'"+kind+"'";header["XBINNING"]=header["YBINNING"]="1";header["FILTER"]="'L'";header["LINEAR"]="T";
      header["EXPTIME"]=kind=="Dark"?"10":kind=="Bias"?"0.001":"0.5";header["CCD-TEMP"]=kind=="Dark flat"?"18":"10";
      Write(Path.Combine(source,kind.Replace(' ','_')+"_"+i+".fits"),480,360,(x,y)=>kind=="Flat"?15000+x*3+y+index:kind=="Bias"?50+index:100+index,header);
     }
     repo.Import(repo.Scan(source,"Stack-01","Dwarf 3",ct,NoProgress).Frames,ct,NoProgress);
     var calibrations=repo.All().Where(f=>Assets.IsCalibration(f.Kind)).ToList();Check(calibrations.Count==12,"Calibration fixture classification failed");
     var plan=SessionStacking.Plan(lights,calibrations);Check(plan.Groups.Count==1&&plan.Groups[0].Calibrations.Count==12,"Flat, dark-flat, dark or bias recipe was omitted");
     var result=SessionStacking.Run(repo,lights,new SessionStackOptions{SirilExecutable=executable,DeleteOriginals=true},ct,NoProgress);
     Check(result.Stacked==1&&result.Deleted==3&&result.Errors.Count==0,"Installed Siril flat preparation failed: "+result.Report);
     string script=File.ReadAllText(Path.Combine(result.RecoveryFolder,"session-0001","stack.ssf"));
     Check(script.Contains("calibrate flat -dark=../master-dark-flat.fits")&&script.Contains("calibrate light -dark=../master-dark.fits -flat=../master-flat.fits"),"Raw flat or light calibration used the wrong masters");
     Check(!script.Split('\n').Where(line=>line.StartsWith("calibrate ")).Any(line=>line.Contains(" -bias=")),"Bias was subtracted in addition to a dark/dark-flat");
     Check(result.Warnings.Any(w=>w.Contains("Dark flat")&&w.Contains("8 C")),"Dark-flat temperature fallback was not reported");
     Check(repo.All().Count==13&&calibrations.All(f=>repo.Find(f.Hash)!=null&&Util.Hash(repo.FilePath(f),ct)==f.Hash&&Util.Hash(f.SourcePath,ct)==f.Hash),"Calibration original or source changed during stacking cleanup");
     Check(result.Outputs.Single().StackCount==3&&result.Outputs.Single().Exposure==30,"Calibrated output integration metadata is incorrect");
    }
    }finally{root=previousRoot;}
   });
  }
 }
}
