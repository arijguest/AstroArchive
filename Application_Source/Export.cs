// Creates copy-based projects. Calibration suggestions never alter science data.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace AstroArchive {
 public class ExportOptions {public string Parent;public string Name;public string Mode="Subs";public bool SeparateSessions=false;public bool IncludeCalibration=true;public bool IncludeRejected=false;public bool IncludeUnknownCalibration=false;}
 public static class Exporter {
  public static bool MatchesCalibration(Frame light,Frame cal){
   if(light.Telescope=="Unknown"||light.Camera=="Unknown"||light.BinX<=0||light.BinY<=0||cal.BinX<=0||cal.BinY<=0||light.Calibration=="Calibrated"||light.Calibration=="Registered")return false;
   if(light.Telescope!=cal.Telescope||light.MakeText!=cal.MakeText||light.Camera!=cal.Camera||light.Width!=cal.Width||light.Height!=cal.Height||light.Channels!=cal.Channels||light.BinX!=cal.BinX||light.BinY!=cal.BinY||light.Bayer!=cal.Bayer)return false;
   if(!light.Gain.HasValue||!cal.Gain.HasValue||Math.Abs(light.Gain.Value-cal.Gain.Value)>0.001)return false;
   if(cal.Kind.EndsWith("dark",StringComparison.OrdinalIgnoreCase)||cal.Kind=="Dark")return light.Exposure.HasValue&&cal.Exposure.HasValue&&Math.Abs(light.Exposure.Value-cal.Exposure.Value)<0.001&&light.Temperature.HasValue&&cal.Temperature.HasValue&&Math.Abs(light.Temperature.Value-cal.Temperature.Value)<=3;
   if(cal.Kind.EndsWith("flat",StringComparison.OrdinalIgnoreCase)||cal.Kind=="Flat")return light.Filter!="Unknown"&&light.Filter==cal.Filter&&light.Night==cal.Night;
   return cal.Kind=="Bias"||cal.Kind=="Master bias";
  }
  public static List<Frame> ExistingCalibrations(Repository repo,IEnumerable<Frame> frames){return frames.Where(f=>new[]{"Dark","Flat","Bias","Master dark","Master flat","Master bias"}.Contains(f.Kind)&&!f.Rejected&&f.Status!="Changed"&&File.Exists(repo.FilePath(f))).ToList();}
  public static List<Frame> CalibrationFor(IEnumerable<Frame> inputs,List<Frame> all,bool allowUnknown){
   var lights=inputs.ToList();if(lights.Count==0)return new List<Frame>();Frame light=lights[0];
   if(light.Calibration!="Uncalibrated"&&!(light.Calibration=="Unknown"&&allowUnknown))return new List<Frame>();
   var cals=all.Where(c=>!c.Rejected&&new[]{"Dark","Flat","Bias","Master dark","Master flat","Master bias"}.Contains(c.Kind)&&lights.All(f=>MatchesCalibration(f,c))).ToList();
   foreach(string kind in new[]{"Dark","Flat","Bias"}){var raw=cals.Where(c=>c.Kind==kind).ToList();var masters=cals.Where(c=>c.Kind=="Master "+kind.ToLowerInvariant()).OrderBy(c=>Math.Abs((c.Temperature??0)-(light.Temperature??0))).ThenByDescending(c=>c.StackCount).ToList();if(raw.Count>0)cals.RemoveAll(c=>c.Kind=="Master "+kind.ToLowerInvariant());else if(masters.Count>1){var keep=masters.First();cals.RemoveAll(c=>c.Kind==keep.Kind&&c.Hash!=keep.Hash);}}
   return cals;
  }
  public static List<Frame> AvailableCalibrations(IEnumerable<Frame> selection,List<Frame> all,bool separateSessions,bool allowUnknown){return selection.Where(f=>f.Kind=="Light").GroupBy(f=>f.Target+"|"+f.Group+(separateSessions?"|"+f.Session:"")).SelectMany(g=>CalibrationFor(g,all,allowUnknown)).GroupBy(f=>f.Hash).Select(g=>g.First()).ToList();}
  public static string Create(Repository repo,List<Frame> selection,ExportOptions options,CancellationToken ct,Action<ProgressInfo> progress){
   if(selection.Count==0)throw new InvalidOperationException("Select files to export.");if(!new[]{"Files","Subs","Stacks","Both"}.Contains(options.Mode))throw new InvalidOperationException("Unknown export mode.");ct.ThrowIfCancellationRequested();if(Util.Within(options.Parent,repo.Root))throw new IOException("Choose a project location outside the repository.");
   string dest=Path.Combine(options.Parent,Util.Safe(options.Name));if(Directory.Exists(dest)||File.Exists(dest))throw new IOException("A project with this name already exists. Choose a new name.");
   var manifest=new List<object>();var warnings=new List<string>();var all=ExistingCalibrations(repo,repo.All());var copy=new List<Tuple<Frame,string>>();var workflows=new List<Tuple<string,string>>();bool subs=options.Mode=="Subs"||options.Mode=="Both",stacks=options.Mode=="Stacks"||options.Mode=="Both";
   bool multipleTargets=selection.Where(f=>f.Kind=="Light"||f.Kind=="Stack").Select(f=>f.Target).Distinct().Count()>1;
   if(options.Mode=="Files")foreach(var f in selection.GroupBy(f=>f.Hash).Select(g=>g.First()))copy.Add(Tuple.Create(f,Path.Combine("files",Util.Safe(f.Kind))));
   if(subs){var groups=selection.Where(f=>f.Kind=="Light"&&(options.IncludeRejected||!f.Rejected)).GroupBy(f=>f.Target+"|"+f.Group+(options.SeparateSessions?"|"+f.Session:"")).OrderBy(g=>g.Key).ToList();int index=0;
    foreach(var g in groups){Frame light=g.First();string group=Path.Combine(options.SeparateSessions?Path.Combine("sessions",Util.Safe(light.Night)+"_"+Util.Safe(light.Session),"subs"):"subs",(++index).ToString("00")+"_"+Util.Safe(light.MakeText)+"_"+Util.Safe(light.Telescope)+"_"+Util.Safe(light.Camera)+"_"+Util.Safe(light.Filter)+"_"+Util.Safe(Util.Num(light.Exposure)+"s"));
     if(multipleTargets)group=Path.Combine("targets",Util.Safe(light.Target),group);
     foreach(var f in g)copy.Add(Tuple.Create(f,Path.Combine(group,"lights")));
     var cals=new List<Frame>();if(options.IncludeCalibration&&(light.Calibration=="Uncalibrated"||(light.Calibration=="Unknown"&&options.IncludeUnknownCalibration))) {
      cals=CalibrationFor(g,all,options.IncludeUnknownCalibration);
      foreach(var c in cals)copy.Add(Tuple.Create(c,Path.Combine(group,c.Kind.StartsWith("Master")?"masters":c.Kind=="Dark"?"darks":c.Kind=="Flat"?"flats":"biases")));
     }
     if(light.Calibration=="Unknown")warnings.Add(group+": calibration status is unknown. Confirm before applying darks/flats/biases.");if(light.Calibration=="Calibrated")warnings.Add(group+": already calibrated; additional calibration files were not supplied.");
     if(options.IncludeCalibration&&light.Calibration=="Uncalibrated"&&!cals.Any(c=>c.Kind=="Dark"||c.Kind=="Master dark"))warnings.Add(group+": no darks with verified matching identity, exposure, gain, dimensions and temperature were found.");
     workflows.Add(Tuple.Create(group,light.Calibration));
    }
   }
   if(stacks){foreach(var f in selection.Where(f=>f.Kind=="Stack"&&(options.IncludeRejected||!f.Rejected)))copy.Add(Tuple.Create(f,Path.Combine(multipleTargets?Path.Combine("targets",Util.Safe(f.Target)):"",options.SeparateSessions?Path.Combine("sessions",Util.Safe(f.Night)+"_"+Util.Safe(f.Session),"stacks"):"stacks",Util.Safe(f.MakeText),Util.Safe(f.Telescope),Util.Safe(f.Camera),Util.Safe(f.Filter))));warnings.Add("Existing stacks are separate from individual lights. Do not mix a telescope stack with its constituent subs, or combine overlapping live-stack snapshots as independent integrations. Exposure overlap cannot be established from filenames alone.");}
   if(copy.Count==0)throw new InvalidOperationException("The selected export mode contains no eligible files.");
   Directory.CreateDirectory(dest);File.WriteAllText(Path.Combine(dest,"INCOMPLETE.txt"),"This export is still being copied. Do not use it until this marker is removed.");
   foreach(var workflow in workflows){string group=workflow.Item1;foreach(string d in new[]{"lights","darks","flats","biases","masters","process","output"})Directory.CreateDirectory(Path.Combine(dest,group,d));File.WriteAllText(Path.Combine(dest,group,"WORKFLOW.txt"),"Use lights for registration and stacking.\r\nCalibration state: "+workflow.Item2+"\r\nRaw calibration sets belong in darks/flats/biases. Masters in masters must be selected explicitly in your stacking software.\r\nEach group is separate because target, camera, filter, dimensions, exposure, gain or calibration differ. Combine compatible group results later with appropriate weighting.\r\n");}
   int n=0;
   foreach(var item in copy){ct.ThrowIfCancellationRequested();Frame f=item.Item1;progress(new ProgressInfo{Done=n,Total=copy.Count,Text="Exporting "+f.OriginalName});string dir=Path.Combine(dest,item.Item2);Directory.CreateDirectory(dir);string rel=Path.Combine(item.Item2,f.Hash.Substring(0,16)+"_"+Util.SafeFile(f.OriginalName));string p=Path.Combine(dest,rel);string temp=p+".partial";try{Repository.CopyVerified(repo.FilePath(f),temp,f.Hash,ct);File.Move(temp,p);}finally{if(File.Exists(temp))File.Delete(temp);}manifest.Add(new{ExportPath=rel,Metadata=f});n++;}
   foreach(string sidecar in copy.Select(item=>item.Item1.SidecarRelativePath).Where(p=>!string.IsNullOrEmpty(p)).Distinct()){string from=Path.Combine(repo.Root,sidecar);if(Util.Within(from,repo.Root)&&File.Exists(from)){string dir=Path.Combine(dest,"session-metadata");Directory.CreateDirectory(dir);File.Copy(from,Path.Combine(dir,Util.HashText(sidecar).Substring(0,12)+"_"+Path.GetFileName(sidecar)));}}
   File.WriteAllText(Path.Combine(dest,"manifest.json"),Util.Serialize(new{App="AstroArchive 1.4.0",Created=DateTime.UtcNow.ToString("o"),Mode=options.Mode,SeparateSessions=options.SeparateSessions,Files=manifest,Warnings=warnings}),new UTF8Encoding(false));
   File.WriteAllText(Path.Combine(dest,"READ_ME.txt"),(options.Mode=="Files"?"AstroArchive selected file export\r\n\r\n":"AstroArchive stacking project\r\n\r\n")+string.Join("\r\n",warnings)+"\r\n\r\nFiles are byte-for-byte copies. The repository and original captures are untouched.\r\nThis project prepares input folders; stacking is performed in your chosen software.\r\nUse manifest.json for original names, source paths, checksums, metadata and inferred mount classifications.\r\n");File.Delete(Path.Combine(dest,"INCOMPLETE.txt"));progress(new ProgressInfo{Done=n,Total=copy.Count,Text=n+" files exported"});return dest;
  }
 }
}
