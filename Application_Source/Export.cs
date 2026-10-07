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
  public static string Create(Repository repo,List<Frame> selection,ExportOptions options,CancellationToken ct,Action<ProgressInfo> progress){
   if(selection.Count==0)throw new InvalidOperationException("Choose light frames or stacks first.");if(Util.Within(options.Parent,repo.Root))throw new IOException("Choose a project location outside the repository.");
   string dest=Path.Combine(options.Parent,Util.Safe(options.Name));if(Directory.Exists(dest)||File.Exists(dest))throw new IOException("A project with this name already exists. Choose a new name.");Directory.CreateDirectory(dest);File.WriteAllText(Path.Combine(dest,"INCOMPLETE.txt"),"This project is still being copied. Do not stack it until this marker is removed.");
   var manifest=new List<object>();var warnings=new List<string>();var all=repo.All();var copy=new List<Tuple<Frame,string>>();bool subs=options.Mode=="Subs"||options.Mode=="Both",stacks=options.Mode=="Stacks"||options.Mode=="Both";
   if(subs){var groups=selection.Where(f=>f.Kind=="Light"&&(options.IncludeRejected||!f.Rejected)).GroupBy(f=>f.Group+(options.SeparateSessions?"|"+f.Session:"")).OrderBy(g=>g.Key).ToList();int index=0;
    foreach(var g in groups){Frame light=g.First();string group=Path.Combine(options.SeparateSessions?Path.Combine("sessions",Util.Safe(light.Night)+"_"+Util.Safe(light.Session),"subs"):"subs",(++index).ToString("00")+"_"+Util.Safe(light.MakeText)+"_"+Util.Safe(light.Telescope)+"_"+Util.Safe(light.Camera)+"_"+Util.Safe(light.Filter)+"_"+Util.Safe(Util.Num(light.Exposure)+"s"));
     foreach(string d in new[]{"lights","darks","flats","biases","masters","process","output"})Directory.CreateDirectory(Path.Combine(dest,group,d));foreach(var f in g)copy.Add(Tuple.Create(f,Path.Combine(group,"lights")));
     var cals=new List<Frame>();if(options.IncludeCalibration&&(light.Calibration=="Uncalibrated"||(light.Calibration=="Unknown"&&options.IncludeUnknownCalibration))) {
      cals=all.Where(c=>new[]{"Dark","Flat","Bias","Master dark","Master flat","Master bias"}.Contains(c.Kind)&&g.All(f=>MatchesCalibration(f,c))).ToList();
      // Prefer raw calibration sets; avoid supplying a master and its raw inputs together.
      foreach(string kind in new[]{"Dark","Flat","Bias"}){var raw=cals.Where(c=>c.Kind==kind).ToList();var masters=cals.Where(c=>c.Kind=="Master "+kind.ToLowerInvariant()).OrderBy(c=>Math.Abs((c.Temperature??0)-(light.Temperature??0))).ThenByDescending(c=>c.StackCount).ToList();if(raw.Count>0)cals.RemoveAll(c=>c.Kind=="Master "+kind.ToLowerInvariant());else if(masters.Count>1){var keep=masters.First();cals.RemoveAll(c=>c.Kind==keep.Kind&&c.Hash!=keep.Hash);}}
      foreach(var c in cals)copy.Add(Tuple.Create(c,Path.Combine(group,c.Kind.StartsWith("Master")?"masters":c.Kind=="Dark"?"darks":c.Kind=="Flat"?"flats":"biases")));
     }
     if(light.Calibration=="Unknown")warnings.Add(group+": calibration status is unknown. Confirm before applying darks/flats/biases.");if(light.Calibration=="Calibrated")warnings.Add(group+": already calibrated; additional calibration files were not supplied.");
     if(light.Calibration=="Uncalibrated"&&!cals.Any(c=>c.Kind=="Dark"||c.Kind=="Master dark"))warnings.Add(group+": no darks with verified matching identity, exposure, gain, dimensions and temperature were found.");
     File.WriteAllText(Path.Combine(dest,group,"WORKFLOW.txt"),"Use lights for registration and stacking.\r\nCalibration state: "+light.Calibration+"\r\nRaw calibration sets belong in darks/flats/biases. Masters in masters must be selected explicitly in your stacking software. Do not run a script that stacks masters as raw frames.\r\nSiril Seestar preset: use the installed Seestar_Preprocessing script only after confirming these are compatible device-calibrated acquisition subs.\r\nEach group is separate because camera, filter, dimensions, exposure, gain or calibration differ. Combine group results later with appropriate weighting.\r\n");
    }
   }
   if(stacks){foreach(var f in selection.Where(f=>f.Kind=="Stack"&&(options.IncludeRejected||!f.Rejected)))copy.Add(Tuple.Create(f,Path.Combine(options.SeparateSessions?Path.Combine("sessions",Util.Safe(f.Night)+"_"+Util.Safe(f.Session),"stacks"):"stacks",Util.Safe(f.MakeText),Util.Safe(f.Telescope),Util.Safe(f.Camera),Util.Safe(f.Filter))));warnings.Add("Existing stacks are separate from individual lights. Do not mix a telescope stack with its constituent subs, or combine overlapping live-stack snapshots as independent integrations. Exposure overlap cannot be established from filenames alone.");}
   if(copy.Count==0)throw new InvalidOperationException("The selected export mode contains no eligible files.");int n=0;
   foreach(var item in copy){ct.ThrowIfCancellationRequested();Frame f=item.Item1;progress(new ProgressInfo{Done=n,Total=copy.Count,Text="Exporting "+f.OriginalName});string dir=Path.Combine(dest,item.Item2);Directory.CreateDirectory(dir);string rel=Path.Combine(item.Item2,f.Hash.Substring(0,16)+"_"+Util.SafeFile(f.OriginalName));string p=Path.Combine(dest,rel);string temp=p+".partial";try{Repository.CopyVerified(repo.FilePath(f),temp,f.Hash,ct);File.Move(temp,p);}finally{if(File.Exists(temp))File.Delete(temp);}manifest.Add(new{ExportPath=rel,Metadata=f});n++;}
   foreach(string sidecar in copy.Select(item=>item.Item1.SidecarRelativePath).Where(p=>!string.IsNullOrEmpty(p)).Distinct()){string from=Path.Combine(repo.Root,sidecar);if(Util.Within(from,repo.Root)&&File.Exists(from)){string dir=Path.Combine(dest,"session-metadata");Directory.CreateDirectory(dir);File.Copy(from,Path.Combine(dir,Path.GetFileName(sidecar)));}}
   File.WriteAllText(Path.Combine(dest,"manifest.json"),Util.Serialize(new{App="AstroArchive 1.2.0",Created=DateTime.UtcNow.ToString("o"),Mode=options.Mode,SeparateSessions=options.SeparateSessions,Files=manifest,Warnings=warnings}),new UTF8Encoding(false));
   File.WriteAllText(Path.Combine(dest,"READ_ME.txt"),"AstroArchive stacking project\r\n\r\n"+string.Join("\r\n",warnings)+"\r\n\r\nFiles are byte-for-byte copies. The repository and original captures are untouched.\r\nThis project prepares input folders; stacking is performed in your chosen software.\r\nUse manifest.json for original names, source paths, checksums, metadata and inferred mount classifications.\r\n");File.Delete(Path.Combine(dest,"INCOMPLETE.txt"));progress(new ProgressInfo{Done=n,Total=copy.Count,Text=n+" files exported"});return dest;
  }
 }
}
