// Verified working copies, measured quality gates and Siril's documented scientific pipeline.
// Uses the library's existing quality measurements, scores and green/yellow bands.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace AstroArchive {
 public sealed class SessionStackOptions {
  public string SirilExecutable;public bool QualityWeighting=true;public bool DeleteOriginals;public Action<string> JobCreated;
  internal ISessionStackRunner Runner;
 }
 public sealed class SessionStackGroup {
  public string Key,Label;public List<Frame> Inputs=new List<Frame>();public List<Frame> Calibrations=new List<Frame>();
 }
 public sealed class SessionStackPlan {
  public int Eligible,Skipped;public List<SessionStackGroup> Groups=new List<SessionStackGroup>();public List<string> Warnings=new List<string>();
 }
 public sealed class SessionStackQuality {
  public string Hash,Name,Grade,Reason,MeasurementData;public double ValidFraction;
  [ScriptIgnore]public QualityMeasurement Measurement;
 }
 public sealed class SessionStackResult {
  public int Stacked,Deleted,QualityExcluded;public string RecoveryFolder,WorkingFolder;
  public List<Frame> Outputs=new List<Frame>();public List<string> Warnings=new List<string>(),Errors=new List<string>();
  public List<SessionStackQuality> Quality=new List<SessionStackQuality>();
  public string Report{get{return Stacked+" sessions stacked; "+Deleted+" archive originals removed; "+QualityExcluded+" red/unmeasurable frames retained.\r\n"+
   "Job and recovery folder: "+RecoveryFolder+"\r\n"+(WorkingFolder==null?"Temporary processing copies cleared.":"Temporary processing copies: "+WorkingFolder)+"\r\nSource folders and calibration originals are retained. Recovery copies remain until you explicitly remove them.\r\n\r\n"+
   string.Join("\r\n",Warnings.Concat(Errors).Distinct(StringComparer.Ordinal))+"\r\n\r\nQuality follows the library's current comparison setting. Only native green/high or yellow/mid frames are supplied to Siril.\r\n"+
   string.Join("\r\n",Quality.Select(q=>q.Grade+" · "+q.Name+": "+q.Reason));}}
 }
 public sealed class SessionStackRecoveryItem {public Frame Metadata;public string File;}
 public sealed class SessionStackRecovery {
  public string Repository,CreatedUtc,State,WorkingFolder;public List<SessionStackRecoveryItem> Originals=new List<SessionStackRecoveryItem>();public List<Frame> Outputs=new List<Frame>();
 }
 internal interface ISessionStackRunner {void Run(string executable,string workingDirectory,string script,CancellationToken ct,Action<string> log);}
 internal sealed class SessionSirilRunner:ISessionStackRunner {
  public void Run(string executable,string directory,string script,CancellationToken ct,Action<string> log){
   string scriptPath=Path.Combine(directory,"stack.ssf"),configuration=Path.Combine(directory,"siril.config");
   SessionStacking.DurableText(scriptPath,script);SessionStacking.DurableText(configuration,"");
   var start=new ProcessStartInfo(executable,"-o -i "+SirilHandoff.QuoteArgument(configuration)+" -d "+SirilHandoff.QuoteArgument(directory)+" -s "+SirilHandoff.QuoteArgument(scriptPath)){
    UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=directory};
   var tail=new StringBuilder();object gate=new object();DateTime lastProgress=DateTime.MinValue;bool accepting=true,outputEnded=false,errorEnded=false;Exception loggingFailure=null;
   using(var writer=new StreamWriter(Path.Combine(directory,"siril.log"),false,new UTF8Encoding(false)))using(var process=new Process{StartInfo=start}){
    Action<string> record=line=>{if(line==null)return;bool update=false;lock(gate){if(!accepting)return;try{writer.WriteLine(line);writer.Flush();tail.AppendLine(line);if(tail.Length>12000)tail.Remove(0,tail.Length-12000);if((DateTime.UtcNow-lastProgress).TotalMilliseconds>=200){lastProgress=DateTime.UtcNow;update=true;}}catch(Exception e){loggingFailure=e;accepting=false;}}if(update&&log!=null)log(line);};
    process.OutputDataReceived+=(s,e)=>{if(e.Data==null){lock(gate)outputEnded=true;}else record(e.Data);};process.ErrorDataReceived+=(s,e)=>{if(e.Data==null){lock(gate)errorEnded=true;}else record(e.Data);};
    ct.ThrowIfCancellationRequested();if(!process.Start())throw new IOException("Siril could not be started.");
    try{try{process.PriorityClass=ProcessPriorityClass.BelowNormal;}catch{}process.BeginOutputReadLine();process.BeginErrorReadLine();var clock=Stopwatch.StartNew();
     while(!process.WaitForExit(200)){ct.ThrowIfCancellationRequested();lock(gate)if(loggingFailure!=null)throw new IOException("Siril's processing log could not be saved; originals are retained.",loggingFailure);if(clock.Elapsed.TotalHours>1)throw new TimeoutException("Siril exceeded the one-hour session deadline; originals are retained.");}
     // Grandchildren can inherit a redirected pipe after Siril exits. Drain available logs briefly;
     // a parameterless WaitForExit would wait indefinitely for those unrelated pipe handles.
     for(int i=0;i<20;i++){lock(gate)if(outputEnded&&errorEnded)break;if(ct.WaitHandle.WaitOne(50))ct.ThrowIfCancellationRequested();}
     ct.ThrowIfCancellationRequested();lock(gate)if(loggingFailure!=null)throw new IOException("Siril's processing log could not be saved; originals are retained.",loggingFailure);if(process.ExitCode!=0)throw new IOException("Siril failed ("+process.ExitCode+"). Originals are retained.\r\n"+tail);
    }finally{if(!process.HasExited){try{process.Kill();process.WaitForExit(5000);}catch{}}lock(gate)accepting=false;try{process.CancelOutputRead();}catch(InvalidOperationException){}try{process.CancelErrorRead();}catch(InvalidOperationException){}}
   }
  }
 }
 public static class SessionStacking {
  public static string ResolveExecutable(string configured){
   var candidates=new List<string>();if(!string.IsNullOrWhiteSpace(configured)){string path=Path.GetFullPath(configured);candidates.Add(Path.GetFileName(path).Equals("siril.exe",StringComparison.OrdinalIgnoreCase)?Path.Combine(Path.GetDirectoryName(path),"siril-cli.exe"):path);}
   else foreach(string program in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})if(!string.IsNullOrEmpty(program))candidates.Add(Path.Combine(program,"Siril","bin","siril-cli.exe"));
   string found=candidates.FirstOrDefault(File.Exists);if(found==null)throw new IOException("Install Siril 1.4 or later, then choose siril.exe or siril-cli.exe. Automatic stacking uses its local command-line engine.");ValidateExecutable(found);return found;
  }
  public static void ValidateExecutable(string path){if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)||!Path.GetFileName(path).Equals("siril-cli.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Choose the installed Siril command-line executable (siril-cli.exe), or its neighbouring siril.exe in the stacking dialog.");}
  public static bool Eligible(Frame f){return f!=null&&f.Kind=="Light"&&!QualityEligibility.IsDisc(f)&&f.StackCount<=1&&f.Exposure.HasValue&&f.Exposure.Value>0&&f.Exposure.Value<=10&&!double.IsNaN(f.Exposure.Value)&&!f.Rejected&&!CaptureScreening.NeedsReview(f)&&f.Status!="Deleted"&&(f.Images==null||f.Images.Count<=1)&&(Assets.Selected(f)==null||Assets.Selected(f).Count<=1)&&Assets.CanStack(f);}
  public static SessionStackPlan Plan(IEnumerable<Frame> selection,IEnumerable<Frame> library){
   var result=new SessionStackPlan();var chosen=(selection??Enumerable.Empty<Frame>()).Where(f=>f!=null).GroupBy(f=>f.Hash??f.SourcePath??f.RelativePath??Guid.NewGuid().ToString()).Select(g=>g.First()).ToList();
   var eligible=chosen.Where(Eligible).ToList();result.Eligible=eligible.Count;result.Skipped=chosen.Count-eligible.Count;
   var calibrations=(library??Enumerable.Empty<Frame>()).Where(f=>Assets.IsCalibration(f.Kind)).ToList();
   foreach(var session in eligible.GroupBy(f=>Util.Serialize(new[]{f.SessionKey,f.Target,f.Group})).OrderBy(g=>g.Key)){
    foreach(var group in Exporter.StackingGroups(session,calibrations,true,false,true)){
     if(group.Inputs.Count<2){result.Warnings.Add(group.Inputs[0].OriginalName+": fewer than two compatible short subframes in this session; retained.");continue;}
     Frame first=group.Inputs[0];var planned=new SessionStackGroup{Key=Util.HashText(session.Key+"|"+Util.Serialize(group.Calibrations.Select(f=>f.Hash).OrderBy(h=>h).ToArray())),Label=first.TargetLabel+" · "+first.Night+" · "+first.Camera+" · "+first.Filter,Inputs=group.Inputs,Calibrations=group.Calibrations};
     result.Groups.Add(planned);result.Warnings.AddRange(CalibrationMatching.Warnings(group.Inputs,group.Calibrations));
     foreach(var flats in group.Calibrations.Where(f=>f.Kind=="Flat").GroupBy(f=>f.Group))result.Warnings.AddRange(CalibrationMatching.Warnings(flats,group.Calibrations.Where(f=>f.Kind=="Dark flat"||f.Kind=="Master dark flat")));
     if(first.Calibration=="Unknown")result.Warnings.Add(planned.Label+": calibration state is unknown; no extra calibration is assumed.");
     if(first.Calibration=="Uncalibrated"&&!group.Calibrations.Any(f=>f.Kind=="Dark"||f.Kind=="Master dark"))result.Warnings.Add(planned.Label+": no safely matching dark was found; stacking continues without a dark.");
    }
   }return result;
  }
  static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
  public static bool QualityAccepted(Frame frame){return frame!=null&&(frame.QualityBand=="High"||frame.QualityBand=="Mid");}
  static string QualityGrade(Frame frame){return frame.QualityBand=="High"?"Green":frame.QualityBand=="Mid"?"Yellow":"Red";}
  internal static void DurableText(string path,string text){string temp=path+"."+Guid.NewGuid().ToString("N")+".partial";try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}Repository.CommitTemporary(temp,path,CancellationToken.None);}finally{Repository.TryRemove(temp);}}
  static string DirectoryFor(string root,string path){Repository.CheckManagedPath(Path.Combine(path,"check"),root);Directory.CreateDirectory(path);Repository.CheckManagedPath(Path.Combine(path,"check"),root);return path;}
  static string DirectoryFor(Repository repo,string path){return DirectoryFor(repo.Root,path);}
  static string WorkDirectory(){string root=Path.GetFullPath(Path.GetTempPath());if(!FileStamp.CanTraverse(new DirectoryInfo(root)))throw new IOException("The temporary processing folder is linked; choose a local Windows temporary folder.");string path=Path.Combine(root,"AstroArchive-stacking",Guid.NewGuid().ToString("N").Substring(0,16));if(Path.Combine(path,"session-0001","process","r_pp_light_00000001.fits").Length>240)throw new IOException("The Windows temporary folder path is too long for Siril. Choose a shorter local TEMP folder; originals are retained.");return DirectoryFor(root,path);}
  static void ClearWorkDirectory(string path){string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"AstroArchive-stacking"));path=Path.GetFullPath(path);if(!Util.Within(path,root)||path.Equals(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid temporary processing cleanup path.");Repository.CheckManagedPath(Path.Combine(path,"check"),root);foreach(string folder in Directory.EnumerateDirectories(path,"*",SearchOption.AllDirectories))Repository.CheckManagedPath(Path.Combine(folder,"check"),root);Directory.Delete(path,true);}
  static void PreserveLog(string work,string evidence){string path=Path.Combine(work,"siril.log");if(File.Exists(path))Repository.CopyVerified(path,Path.Combine(evidence,"siril.log"),Util.Hash(path,CancellationToken.None),CancellationToken.None);}
  static PixelImage ReadVerified(Repository repo,Frame frame,CancellationToken ct){frame=repo.Located(frame);string path=repo.FilePath(frame);repo.ValidateCapture(frame,ct);var before=FileStamp.Read(path);var image=Assets.Read(frame,path,frame.ImageIndex??0,ct);if(!before.ContentSame(FileStamp.Read(path)))throw new IOException("Capture changed during processing: "+frame.OriginalName);return image;}
  static void Prepare(Repository repo,Frame frame,string destination,CancellationToken ct){ScientificFits.Write(destination,ReadVerified(repo,frame,ct),frame,ct);Fits.Validate(destination,ct);}
  static string Quote(string value){if((value??"").IndexOfAny(new[]{'\r','\n','"'})>=0)throw new IOException("A stacking path contains unsupported control or quote characters.");return "\""+value.Replace('\\','/')+"\"";}
  // All script paths and names below are generated by this job, never interpolated capture names.
  static string Master(Repository repo,string work,string role,List<Frame> inputs,StringBuilder script,CancellationToken ct,string correction){
   if(inputs.Count==0)return null;string raw=DirectoryFor(work,Path.Combine(work,"raw-"+role));
   for(int i=0;i<inputs.Count;i++)Prepare(repo,inputs[i],Path.Combine(raw,"cal"+(i+1).ToString("D5")+".fits"),ct);
   if(inputs.Count==1){if(correction==null)return "raw-"+role+"/cal00001.fits";script.AppendLine("cd "+Quote(raw));script.AppendLine("calibrate_single cal00001.fits"+correction);return "raw-"+role+"/pp_cal00001.fits";}
   DirectoryFor(work,Path.Combine(work,role));script.AppendLine("cd "+Quote(raw));script.AppendLine("convert "+role+" -out=../"+role);script.AppendLine("cd ../"+role);
   string sequence=role;if(correction!=null){script.AppendLine("calibrate "+role+correction);sequence="pp_"+role;}
   script.AppendLine("stack "+sequence+" med "+(role=="flat"?"-norm=mul":"-nonorm")+" -out=../master-"+role+" -32b");return "master-"+role+".fits";
  }
  static string BuildScript(Repository repo,SessionStackGroup group,string work,List<Frame> accepted,SessionStackOptions options,CancellationToken ct,List<string> warnings){
   var script=new StringBuilder("requires 1.4.0\nsetext fits\nset32bits\nsetcompress 0\nsetcpu 2\nsetmem 0.5\n");var cals=group.Calibrations;
   string dark=Master(repo,work,"dark",cals.Where(f=>f.Kind=="Dark"||f.Kind=="Master dark").ToList(),script,ct,null);
   string bias=Master(repo,work,"bias",cals.Where(f=>f.Kind=="Bias"||f.Kind=="Master bias").ToList(),script,ct,null);
   var flats=cals.Where(f=>f.Kind=="Flat"||f.Kind=="Master flat").ToList();string flat=null;
   if(flats.Any(f=>f.Kind=="Master flat"))flat=Master(repo,work,"flat",flats.Where(f=>f.Kind=="Master flat").Take(1).ToList(),script,ct,null);
   else if(flats.Count>0){var flatSet=flats.GroupBy(f=>f.Group).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).First().ToList();
    var darkFlats=CalibrationMatching.Select(flatSet,cals.Where(f=>f.Kind=="Dark flat"||f.Kind=="Master dark flat"));warnings.AddRange(CalibrationMatching.Warnings(flatSet,darkFlats));
    string darkFlat=Master(repo,work,"dark-flat",darkFlats,script,ct,null),correction=darkFlat!=null?" -dark=../"+darkFlat:bias!=null?" -bias=../"+bias:null;
    if(correction==null)warnings.Add(group.Label+": raw flats lack a safely matching dark-flat/bias; they are retained and not applied automatically.");
    else flat=Master(repo,work,"flat",flatSet,script,ct,correction);
   }
   string lights=DirectoryFor(work,Path.Combine(work,"lights"));DirectoryFor(work,Path.Combine(work,"process"));
   script.AppendLine("cd "+Quote(lights));script.AppendLine("convert light -out=../process");script.AppendLine("cd ../process");
   bool cfa=accepted[0].Channels==1&&QualityEligibility.HasMosaic(accepted[0]);
   string calibration=(dark!=null?" -dark=../"+dark:bias!=null?" -bias=../"+bias:"")+(flat!=null?" -flat=../"+flat:"")+(cfa?" -cfa -debayer":"");
   string sequence="light";if(calibration.Length>0){script.AppendLine("calibrate light"+calibration);sequence="pp_light";}
   // A complete-cohort quality reference is selected first; Siril measures registration quality for weighting.
   script.AppendLine("setref "+sequence+" 1");script.AppendLine("register "+sequence+" -transf=similarity");
   script.AppendLine("stack r_"+sequence+" rej "+(accepted.Count>=5?"w 3 3":"none")+" -norm=addscale"+(options.QualityWeighting?" -weight=wfwhm":"")+" -filter-included -out=../result -32b");script.AppendLine("close");return script.ToString();
  }
  static Frame ImportOutput(Repository repo,SessionStackGroup group,string work,string evidence,List<Frame> accepted,CancellationToken ct,out int contributors){
   string path=Path.Combine(work,"result.fits");var header=Fits.Validate(path,ct);double? count=header.Number("NCOMBINE","STACKCNT","NSTACK");
   if(count.HasValue&&(count.Value<2||count.Value>accepted.Count||count.Value!=Math.Floor(count.Value)))throw new InvalidDataException("Stack output reports an invalid number of contributing subframes; originals retained.");
   contributors=count.HasValue&&count.Value>=2&&count.Value<=accepted.Count&&count.Value==Math.Floor(count.Value)?(int)count.Value:0;
   int expectedChannels=accepted[0].Channels==1&&QualityEligibility.HasMosaic(accepted[0])?3:Math.Max(1,accepted[0].Channels);
   if(header.Width!=accepted[0].Width||header.Height!=accepted[0].Height||header.Channels!=expectedChannels)throw new InvalidDataException("Stack dimensions or channels differ from the planned session.");
   var output=Classifier.Read(path,work,accepted[0].Telescope,accepted[0].Model,ct:ct);var pixels=Assets.Read(output,path,0,ct).Pixels;int finite=0;double low=double.MaxValue,high=double.MinValue;
   foreach(double pixel in pixels)if(Finite(pixel)){finite++;low=Math.Min(low,pixel);high=Math.Max(high,pixel);}
   if(finite<(long)pixels.Length*0.95||!(high>low))throw new InvalidDataException("Stack output has insufficient finite signal or no dynamic range; originals retained.");
   Frame first=accepted[0];output.Kind="Stack";output.Target=first.Target;output.Telescope=first.Telescope;output.TelescopeIdentity=first.TelescopeIdentity;output.Make=first.Make;output.Model=first.Model;output.Camera=first.Camera;output.Night=first.Night;output.Session=first.Session;output.AcquisitionDate=first.AcquisitionDate;output.AcquisitionDateSource=first.AcquisitionDateSource;
   output.CameraId=first.CameraId;output.CameraModel=first.CameraModel;output.TelescopeModel=first.TelescopeModel;output.Filter=first.Filter;output.Calibration="Registered";output.RegistrationState="Registered";output.LinearData=true;output.StackCount=contributors;output.Exposure=contributors>0?(double?)(contributors*first.Exposure.Value):header.Number("EXPTIME");output.Status="New";
   output.Notes="AstroArchive automatic session stack. "+accepted.Count+" green/yellow inputs supplied; "+contributors+" contributors reported by Siril. Input hashes and calibration recipe are in "+evidence+". "+(first.Calibration=="Unknown"?"Input calibration was unknown; no calibration was assumed.":"");
   output.Hash=Util.Hash(path,ct);output.SourceMetadataPath=null;output.SourceMetadataStamp=null;
   var imported=repo.Import(new[]{output},ct,null,new ImportOptions{SourceRoot=work,Workers=1});if(imported.Failed>0||imported.Imported+imported.Duplicates!=1)throw new IOException("Stack import did not complete: "+string.Join("; ",imported.Errors));
   var stored=repo.Find(output.Hash);if(stored==null||Util.Hash(repo.FilePath(stored),ct)!=output.Hash)throw new IOException("Imported stack failed final checksum verification; originals retained.");stored.SourceDisposition="Derived session stack; provenance retained in "+evidence;repo.Save(stored);repo.Checkpoint(ct);return stored;
  }
  public static SessionStackResult Run(Repository repo,IEnumerable<Frame> selection,SessionStackOptions options,CancellationToken ct,Action<ProgressInfo> progress){
   if(repo==null)throw new ArgumentNullException("repo");options=options??new SessionStackOptions();ct.ThrowIfCancellationRequested();string executable=options.Runner==null?ResolveExecutable(options.SirilExecutable):options.SirilExecutable;
   var requested=(selection??Enumerable.Empty<Frame>()).ToList();var current=new List<Frame>();foreach(var frame in requested){ct.ThrowIfCancellationRequested();var actual=repo.Find(frame.Hash);if(actual==null)throw new IOException("A selected capture is no longer indexed. Refresh before stacking.");current.Add(actual);}
   var plan=Plan(current,Exporter.ExistingCalibrations(repo,repo.All()));if(plan.Groups.Count==0)throw new InvalidOperationException("Select at least two compatible individual light frames of 10 seconds or less from one session.");
   long workBytes=checked(plan.Groups.Sum(g=>g.Inputs.Sum(f=>(long)f.Width*f.Height*Math.Max(1,f.Channels)*(f.Channels==1&&QualityEligibility.HasMosaic(f)?40L:24L)))+plan.Groups.SelectMany(g=>g.Calibrations).Sum(f=>(long)f.Width*f.Height*Math.Max(1,f.Channels)*16L)+100L*1024*1024);
   long archiveBytes=checked(plan.Groups.Sum(g=>(long)g.Inputs[0].Width*g.Inputs[0].Height*3L*4L)+(options.DeleteOriginals?plan.Groups.SelectMany(g=>g.Inputs).Sum(f=>f.Bytes):0)+100L*1024*1024);
   string drive=Path.GetPathRoot(repo.Root),tempDrive=Path.GetPathRoot(Path.GetTempPath());bool shared=string.Equals(drive,tempDrive,StringComparison.OrdinalIgnoreCase);
   if((!drive.StartsWith("\\\\",StringComparison.Ordinal)&&new DriveInfo(drive).AvailableFreeSpace<archiveBytes+(shared?workBytes:0))||(!shared&&!tempDrive.StartsWith("\\\\",StringComparison.Ordinal)&&new DriveInfo(tempDrive).AvailableFreeSpace<workBytes))throw new IOException("There is insufficient free space for verified working images, stack outputs and recovery copies on the archive or Windows temporary drive. Originals are retained.");
   string job=DirectoryFor(repo,Path.Combine(repo.Meta,"stacking","job-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,8)));
   var result=new SessionStackResult{RecoveryFolder=job};result.Warnings.AddRange(plan.Warnings);if(plan.Skipped>0)result.Warnings.Add(plan.Skipped+" ineligible selected files were retained.");
   var cleanup=new List<Frame>();var recovery=new SessionStackRecovery{Repository=repo.Root,CreatedUtc=DateTime.UtcNow.ToString("o"),State="Preparing"};IDisposable processingClaim=null;string workRoot=null;
   DurableText(Path.Combine(job,"recovery.json"),Util.Serialize(recovery));if(options.JobCreated!=null)options.JobCreated(job);
   try{workRoot=WorkDirectory();result.WorkingFolder=recovery.WorkingFolder=workRoot;DurableText(Path.Combine(job,"recovery.json"),Util.Serialize(recovery));processingClaim=repo.Background.Claim("Stacking sessions",plan.Groups.SelectMany(g=>g.Inputs.Concat(g.Calibrations)).Select(f=>f.Hash));
   var qualityRun=repo.AnalyzeQuality(plan.Groups.SelectMany(g=>g.Inputs).ToList(),false,ct,progress);if(qualityRun.NeedsReport)result.Warnings.Add(qualityRun.Report());
   var scored=repo.All();repo.ApplyQuality(scored);var byHash=scored.ToDictionary(f=>f.Hash);var measurements=repo.QualityMeasurements();
   foreach(var group in plan.Groups)group.Inputs=group.Inputs.Select(f=>byHash.ContainsKey(f.Hash)?byHash[f.Hash]:f).ToList();
   for(int g=0;g<plan.Groups.Count;g++){
    ct.ThrowIfCancellationRequested();var group=plan.Groups[g];string name="session-"+(g+1).ToString("D4");string work=DirectoryFor(workRoot,Path.Combine(workRoot,name)),evidence=DirectoryFor(repo,Path.Combine(job,name));string lights=DirectoryFor(work,Path.Combine(work,"lights"));var rows=new List<SessionStackQuality>();var prepared=new Dictionary<string,string>();
    try{for(int i=0;i<group.Inputs.Count;i++){
     ct.ThrowIfCancellationRequested();Frame frame=group.Inputs[i];if(progress!=null)progress(new ProgressInfo{Stage="Measuring session quality",Done=i,Total=group.Inputs.Count,Text=frame.OriginalName});
     QualityMeasurement measured;measurements.TryGetValue(frame.Hash,out measured);var row=new SessionStackQuality{Hash=frame.Hash,Name=frame.OriginalName,Grade=QualityGrade(frame),Reason=(frame.QualityScore.HasValue?"Native quality "+frame.QualityScore.Value.ToString("0.0",CultureInfo.InvariantCulture)+" / 100. ":"Unscored; retained. ")+(frame.QualitySummary??frame.QualityDetail??""),Measurement=measured,MeasurementData=measured==null?null:measured.Pack()};rows.Add(row);
     if(!QualityAccepted(frame))continue;
     try{var image=ReadVerified(repo,frame,ct);row.ValidFraction=image.Pixels.Count(Finite)/(double)image.Pixels.Length;if(row.ValidFraction<0.98)throw new InvalidDataException("Fewer than 98% of samples are finite.");
      string path=Path.Combine(lights,"measure"+(i+1).ToString("D5")+".fits");prepared[frame.Hash]=path;ScientificFits.Write(path,image,frame,ct);
     }catch(OperationCanceledException){throw;}catch(Exception e){row.Grade="Red";row.Measurement=null;row.Reason="Working-copy preparation failed; retained: "+e.Message;}
    }
    result.Quality.AddRange(rows);result.QualityExcluded+=rows.Count(q=>q.Grade=="Red");DurableText(Path.Combine(evidence,"quality.json"),Util.Serialize(rows));
    var allowed=new HashSet<string>(rows.Where(q=>q.Grade=="Green"||q.Grade=="Yellow").Select(q=>q.Hash));
    var accepted=group.Inputs.Where(f=>allowed.Contains(f.Hash)).OrderByDescending(f=>f.QualityScore??0).ThenBy(f=>f.Hash,StringComparer.Ordinal).ToList();
    // Remove red working files before Siril's directory-wide convert command.
    foreach(var frame in group.Inputs.Where(f=>!allowed.Contains(f.Hash)))if(prepared.ContainsKey(frame.Hash)&&!Repository.TryRemove(prepared[frame.Hash]))throw new IOException("A red working image could not be excluded; the session was not sent to Siril.");
    if(accepted.Count<2){result.Warnings.Add(group.Label+": fewer than two green/yellow frames; this session was retained.");continue;}
    // Stable numbered input names make the complete-cohort best shape the reference.
    for(int i=0;i<accepted.Count;i++)File.Move(prepared[accepted[i].Hash],Path.Combine(lights,"input"+(i+1).ToString("D5")+".fits"));
    DurableText(Path.Combine(evidence,"inputs.json"),Util.Serialize(new{Inputs=accepted,Calibrations=group.Calibrations,Quality=rows,QualityWeighting=options.QualityWeighting}));
    string script=BuildScript(repo,group,work,accepted,options,ct,result.Warnings);DurableText(Path.Combine(evidence,"stack.ssf"),script);int sequence=g;
    (options.Runner??new SessionSirilRunner()).Run(executable,work,script,ct,line=>{if(progress!=null)progress(new ProgressInfo{Stage="Stacking session "+(sequence+1)+" / "+plan.Groups.Count,Done=sequence,Total=plan.Groups.Count,Text=line});});
    ct.ThrowIfCancellationRequested();int contributors;Frame output=ImportOutput(repo,group,work,evidence,accepted,ct,out contributors);result.Outputs.Add(output);recovery.Outputs.Add(output.Clone());result.Stacked++;
    if(contributors==accepted.Count)cleanup.AddRange(accepted);else result.Warnings.Add(group.Label+": Siril did not verify that every supplied frame contributed; all session originals were retained.");
    DurableText(Path.Combine(evidence,"complete.json"),Util.Serialize(new{Output=output,InputHashes=accepted.Select(f=>f.Hash).ToArray(),Contributors=contributors}));
   }catch(OperationCanceledException){throw;}catch(Exception e){result.Errors.Add(group.Label+": "+e.Message);}finally{try{PreserveLog(work,evidence);}catch(Exception e){result.Errors.Add("Processing log could not be preserved; originals retained: "+e.Message);}}}
   ct.ThrowIfCancellationRequested();processingClaim.Dispose();processingClaim=null;
   if(options.DeleteOriginals&&cleanup.Count>0&&result.Errors.Count==0){
    string originals=DirectoryFor(repo,Path.Combine(job,"recovery-originals"));
    foreach(var frame in cleanup.GroupBy(f=>f.Hash).Select(g=>g.First())){
     ct.ThrowIfCancellationRequested();if(!Eligible(frame)||frame.Kind!="Light")throw new IOException("Only verified contributing light subframes may be removed.");
     string name=frame.Hash+"_"+Util.SafeFile(frame.OriginalName);Repository.CopyVerified(repo.FilePath(frame),Path.Combine(originals,name),frame.Hash,ct);recovery.Originals.Add(new SessionStackRecoveryItem{Metadata=frame.Clone(),File="recovery-originals/"+name});
    }
    foreach(var output in result.Outputs)if(Util.Hash(repo.FilePath(output),ct)!=output.Hash)throw new IOException("An imported stack changed before cleanup. All originals retained.");
    repo.Checkpoint(ct);recovery.State="Recovery verified; cleanup pending";DurableText(Path.Combine(job,"recovery.json"),Util.Serialize(recovery));ct.ThrowIfCancellationRequested();
    var deleted=repo.DeleteFrames(cleanup,ct,progress,true);result.Deleted=deleted.Deleted;result.Errors.AddRange(deleted.Errors);recovery.State="Completed; recovery copies retained";
   }else{recovery.State="Completed; originals retained";if(options.DeleteOriginals&&result.Errors.Count>0)result.Warnings.Add("A session failed, so no original cleanup was started for this batch.");}
   return result;
   }catch(OperationCanceledException){recovery.State="Canceled; completed stacks and any recovery copies retained";throw;}
   catch{recovery.State="Failed; completed stacks and any recovery copies retained";throw;}
   finally{if(processingClaim!=null)processingClaim.Dispose();if(recovery.Originals.Count>0)try{result.Deleted=recovery.Originals.Count(item=>repo.Find(item.Metadata.Hash)==null);}catch(Exception e){result.Warnings.Add("The final original-removal count could not be refreshed: "+e.Message);}if(workRoot!=null&&!ct.IsCancellationRequested&&result.Errors.Count==0&&recovery.State.StartsWith("Completed")){try{ClearWorkDirectory(workRoot);result.WorkingFolder=recovery.WorkingFolder=null;}catch(Exception e){result.Warnings.Add("Temporary processing copies retained: "+e.Message);}}DurableText(Path.Combine(job,"recovery.json"),Util.Serialize(recovery));DurableText(Path.Combine(job,"report.txt"),result.Report);}
  }
  public static ImportResult RestoreOriginals(Repository repo,string job,CancellationToken ct,Action<ProgressInfo> progress){
   job=Path.GetFullPath(job);string baseFolder=Path.Combine(repo.Meta,"stacking");if(!Util.Within(job,baseFolder)||job.Equals(baseFolder,StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a session stacking job in this repository.");
   Repository.CheckManagedPath(Path.Combine(job,"recovery.json"),repo.Root);var recovery=Util.Deserialize<SessionStackRecovery>(File.ReadAllText(Path.Combine(job,"recovery.json")));
   if(recovery==null||!string.Equals(recovery.Repository,repo.Root,StringComparison.OrdinalIgnoreCase)||recovery.Originals==null)throw new IOException("The recovery manifest belongs to a different repository.");
   var frames=new List<Frame>();foreach(var item in recovery.Originals){ct.ThrowIfCancellationRequested();string path=Path.GetFullPath(Path.Combine(job,item.File));if(!Util.Within(path,Path.Combine(job,"recovery-originals"))||item.Metadata==null||item.Metadata.Kind!="Light")throw new IOException("Invalid original recovery entry.");Repository.CheckManagedPath(path,repo.Root);if(Util.Hash(path,ct)!=item.Metadata.Hash)throw new IOException("A recovery copy changed; originals were not restored.");var frame=item.Metadata.Clone();frame.SourcePath=path;frame.SourceRoot=job;frame.SourceStamp=FileStamp.Read(path);frame.SourceMetadataPath=null;frame.SourceMetadataStamp=null;frame.Status="New";frames.Add(frame);}
   repo.AllowReimport(frames.Select(f=>f.Hash),ct);return repo.Import(frames,ct,progress,new ImportOptions{SourceRoot=job,Workers=1});
  }
 }
}
