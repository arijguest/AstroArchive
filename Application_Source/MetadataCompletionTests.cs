using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static void MetadataCompletionTests(){
   Test("Unknown cameras default to Tele for selected DSO and solar targets only",()=>{
    foreach(string target in new[]{"NGC7000","M31","M45","Moon","Mars","C/2023 A3"}){var frame=new Frame{Target=target,Camera="Unknown",Kind="Light"};Check(CameraDetection.DefaultForTarget(frame)&&frame.Camera=="Telephoto"&&frame.Facts["Camera"].Source.StartsWith("Target-based default"),"Target fallback missing: "+target);Check(!CameraDetection.DefaultForTarget(frame),"Camera fallback was not idempotent");}
    foreach(string target in new[]{"Unknown","Calibration","Meteor","Unidentified field","Sirius"}){var frame=new Frame{Target=target,Camera="Unknown"};Check(!CameraDetection.DefaultForTarget(frame),"Unidentified/non-DSO target defaulted: "+target);}
    foreach(string camera in new[]{"Wide","Telephoto","Primary","Custom camera"}){var frame=new Frame{Target="M31",Camera=camera};Check(!CameraDetection.DefaultForTarget(frame)&&frame.Camera==camera,"Recorded camera replaced");}
    var calibration=new Frame{Target="M31",Camera="Unknown",Kind="Dark"};Check(!CameraDetection.DefaultForTarget(calibration),"Calibration camera guessed");
    var conflict=new Frame{Target="M31",Camera="Unknown",CameraEvidence="Conflicting camera markers; manual review required"};Check(!CameraDetection.DefaultForTarget(conflict),"Camera conflict hidden");
    var overridden=new Frame{Target="M31",Camera="Unknown"};Assets.UserFact(overridden,"Camera");Check(!CameraDetection.DefaultForTarget(overridden),"Explicit user camera override replaced");
   });
   Test("Target edits and import assignments default an unknown camera without changing batch intent",()=>{
    var original=new Frame{Target="Unknown",Camera="Unknown",Kind="Light",Status="New"};var patch=new MetadataPatch(new Dictionary<string,string>{{"Target","M31"}});var updated=patch.Apply(original);
    Check(updated.Camera=="Telephoto"&&original.Camera=="Unknown"&&updated.Facts["Target"].Source=="User"&&updated.Facts["Camera"].Source.StartsWith("Target-based default"),"Target edit camera/evidence incorrect");
    Check(new MetadataPatch(new Dictionary<string,string>()).Apply(original).Camera=="Unknown","No-op edit generated camera metadata");
    original.Camera="Wide";Check(patch.Apply(original).Camera=="Wide","Target edit replaced Wide");original.Camera="Unknown";
    var explicitPatch=new MetadataPatch(new Dictionary<string,string>{{"Target","M31"},{"Camera","Wide"}});Check(explicitPatch.Apply(original).Camera=="Wide","Explicit simultaneous camera edit lost");
    Check(ImportPolicy.AssignUnknown(new[]{original},"Moon")==1&&original.Camera=="Telephoto","Import target assignment did not default camera");
    original.Camera="Unknown";var job=new TargetSolveJob{Representative=original,Result=new SolveResult{RA=10,Dec=20,Solver="fixture"}};Check(TargetSolving.Apply(job,original,"M31").Camera=="Telephoto","Plate-solved target did not default camera");
   });
   Test("Scanning applies target defaults after explicit camera metadata and computes matching sessions",()=>{
    string directory=Path.Combine(root,"completion-camera-source");Directory.CreateDirectory(directory);string path=Path.Combine(directory,"capture.fit");var headers=LightHeaders(new DateTime(2026,10,6,21,0,0),"M31");headers.Remove("CAMID");Write(path,16,12,(x,y)=>100,headers);
    var frame=Classifier.Read(path,directory,"Scope","Auto");Check(frame.Camera=="Telephoto"&&frame.Facts["Camera"].Source.StartsWith("Target-based default"),"Scan fallback missing");
    string session=frame.Session;headers["CAMID"]="'0'";Write(path,16,12,(x,y)=>100,headers);frame=Classifier.Read(path,directory,"Scope","Auto");Check(frame.Session==session,"Target-default and explicitly detected Tele produced different session IDs");headers.Remove("CAMID");
    headers["CAMMODEL"]="ASI2600MC";Write(path,16,12,(x,y)=>100,headers);frame=Classifier.Read(path,directory,"Scope","Auto");Check(frame.Camera=="Primary"&&frame.Session!=session,"Explicit generic camera metadata/session replaced with Tele");
    headers.Remove("CAMMODEL");headers["CAMID"]="1";Write(path,16,12,(x,y)=>100,headers);frame=Classifier.Read(path,directory,"Scope","Auto");Check(frame.Camera=="Wide","Explicit Wide marker replaced");
   });
   Test("Metadata completion preserves known values, zero, false, identity and user overrides",()=>{
    var current=new Frame{Hash="identity",RelativePath="same/file.fit",SourcePath="old/telescope.fit",Target="M31",Kind="Light",Exposure=0,Gain=0,Temperature=0,Offset=0,LinearData=false,Filter="Unknown",Camera="Wide",Mount="Unknown",Notes="Keep notes",Session="keep-session",Telescope="Physical unit",Observed="2026-10-06T23:00:00"};Assets.UserFact(current,"Filter");
    var detected=new Frame{Target="M42",Kind="Stack",Exposure=60,Gain=120,Temperature=-10,Offset=20,LinearData=true,Filter="Dual band",Camera="Telephoto",Mount="EQ",MountEvidence="Explicit FITS mount metadata",BinX=2,BinY=2,CameraId="CAM-123",Observed="2026-10-07T01:00:00"};
    var result=MetadataCompletion.Fill(current,detected);var frame=result.Updated;
    Check(frame.Exposure==0&&frame.Gain==0&&frame.Temperature==0&&frame.Offset==0&&frame.LinearData==false&&frame.Filter=="Unknown"&&frame.Camera=="Wide"&&frame.Target==current.Target&&frame.Kind==current.Kind&&frame.Observed==current.Observed,"Known value/override replaced");
    Check(frame.Mount=="EQ"&&frame.MountEvidence==detected.MountEvidence&&frame.BinX==2&&frame.BinY==2&&frame.CameraId=="CAM-123","Supported gaps were not filled");
    Check(frame.Hash==current.Hash&&frame.RelativePath==current.RelativePath&&frame.SourcePath==current.SourcePath&&frame.Notes==current.Notes&&frame.Session==current.Session&&frame.Telescope==current.Telescope&&current.CameraId==null,"Identity, provenance or original mutated");
    Check(!result.Fields.Contains("StackCount")&&!result.Fields.Contains("Exposure")&&MetadataCompletion.Fill(frame,detected).Fields.Count==0,"Completion not conservative/idempotent");
   });
   Test("Metadata completion leaves conflicting aliases and camera markers for review",()=>{
    var header=new Dictionary<string,string>{{"OBJECT","M31"},{"TARGET","M42"},{"EXPTIME","10"},{"EXPOSURE","20"},{"GAIN","60"}};
    var detected=new Frame{Target="M31",Exposure=10,Gain=60,Camera="Unknown",CameraEvidence="Conflicting camera markers",Images=new List<ImageDescriptor>{new ImageDescriptor{Key="hdu:0",Headers=header}},ImageKey="hdu:0",MetadataConflicts=new List<string>{"Gain: conflicting aliases"}};
    var current=new Frame{Target="Unknown",Camera="Unknown"};var result=MetadataCompletion.Fill(current,detected);
    Check(result.Updated.Target=="Unknown"&&result.Updated.Camera=="Unknown"&&!result.Updated.Exposure.HasValue&&!result.Updated.Gain.HasValue,"Conflicting metadata applied");
    current.Target="M31";current.CameraEvidence="Conflicting camera markers";Check(MetadataCompletion.Fill(current,detected).Updated.Camera=="Unknown","Indexed camera conflict defaulted");
    current.CameraEvidence="No marker";Check(MetadataCompletion.Fill(current,detected).Updated.Camera=="Unknown","Detected camera conflict defaulted");
    current.Target="Meteor";current.CameraEvidence=null;detected.Camera="Telephoto";detected.CameraEvidence="Target-based default: M31";Check(MetadataCompletion.Fill(current,detected).Updated.Camera=="Unknown","Camera inferred from a different target");
   });
   Test("Repository completion recovers headers and preserved session values without using generated folders or changing originals",()=>{
    string directory=Path.Combine(root,"completion-repository-source");Directory.CreateDirectory(directory);string path=Path.Combine(directory,"capture.fit");var headers=LightHeaders(new DateTime(2026,10,6,21,0,0),"NGC7000");headers.Remove("EXPTIME");headers.Remove("GAIN");headers.Remove("CAMID");Write(path,16,12,(x,y)=>100,headers);File.WriteAllText(Path.Combine(directory,"shotsInfo.json"),"{\"exposure_s\":15,\"gain\":80,\"cameraId\":1,\"targetName\":\"NGC7000\"}");
    using(var repo=new Repository(Path.Combine(root,"completion-repository"))){var scan=repo.Scan(directory,"Scope","Auto",ct,NoProgress);var imported=repo.Import(scan.Frames,ct,NoProgress);Check(imported.Imported==1,"Completion fixture not imported");var frame=repo.All().Single();string hash=frame.Hash,relative=frame.RelativePath;frame.Exposure=null;frame.Gain=null;frame.Camera="Unknown";frame.CameraEvidence="No camera marker";frame.Target="Unknown";frame.AcquisitionDate=null;frame.Observed=null;frame.ObservedUtc=null;frame.Facts=null;frame.Filter="Unknown";repo.Save(frame);Directory.Delete(directory,true);
     var proposal=repo.DetectMissingMetadata(frame,ct);Check(proposal.Updated.Target==Catalog.Normalize("NGC7000")&&proposal.Updated.Exposure==15&&proposal.Updated.Gain==80&&proposal.Updated.Camera=="Wide"&&!string.IsNullOrEmpty(proposal.Updated.AcquisitionDate),"Original headers/session metadata not recovered");
     Check(proposal.Updated.Filter=="Unknown"&&proposal.Updated.Mount==frame.Mount,"Archive folders used as metadata");
     frame.Gain=42;Assets.UserFact(frame,"Gain");repo.Save(frame);int count=repo.ApplyMissingMetadata(proposal,ct);var saved=repo.Find(hash);Check(count>0&&saved.Gain==42&&saved.Hash==hash&&saved.RelativePath==relative&&Util.Hash(repo.FilePath(saved),ct)==hash,"Preview application replaced a new edit or changed/moved original bytes");
     Check(repo.DetectMissingMetadata(saved,ct).Fields.Count==0,"Repository completion not idempotent");
     using(var canceled=new CancellationTokenSource()){canceled.Cancel();Expect(()=>repo.ApplyMissingMetadata(proposal,canceled.Token),"Canceled metadata application accepted");}
     File.AppendAllText(repo.FilePath(saved),"changed");Expect(()=>repo.DetectMissingMetadata(saved,ct),"Changed repository file accepted for metadata completion");
    }
   });
   Test("Opening a repository defaults legacy DSO cameras without moving files",()=>{
    string directory=Path.Combine(root,"completion-legacy");string hash=Util.HashText("legacy-camera");
    using(var repo=new Repository(directory)){repo.Save(new Frame{Hash=hash,Target="M31",Kind="Light",Camera="Unknown",RelativePath="original/location.fit"});}
    using(var repo=new Repository(directory)){var frame=repo.Find(hash);Check(frame.Camera=="Telephoto"&&frame.RelativePath=="original/location.fit"&&frame.CameraEvidence.StartsWith("Target-based default"),"Legacy camera fallback not persisted conservatively");}
   });
   Test("Completion avoids mismatched coordinate pairs and processing states",()=>{
    var current=new Frame{Kind="Light",Target="Unknown",RA=10,BinX=2};var detected=new Frame{Kind="Stack",Target="Unknown",RA=20,Dec=30,BinX=1,BinY=1,Calibration="Device stack",RegistrationState="Registered",Exposure=120,StackCount=4};var result=MetadataCompletion.Fill(current,detected).Updated;
    Check(!result.Dec.HasValue&&result.BinY==0&&result.Calibration==null&&result.RegistrationState==null&&!result.Exposure.HasValue&&result.StackCount==0,"Contradictory paired/processing metadata combined");
   });
   Test("Completion detects conflicting preserved session fields and refuses altered sidecars",()=>{
    string directory=Path.Combine(root,"completion-conflicting-shots");Directory.CreateDirectory(directory);string path=Path.Combine(directory,"capture.fit");var headers=LightHeaders(new DateTime(2026,10,6,21,0,0),"M31");headers.Remove("CAMID");headers.Remove("EXPTIME");Write(path,16,12,(x,y)=>100,headers);File.WriteAllText(Path.Combine(directory,"shotsInfo.json"),"{\"first\":{\"cameraId\":0,\"exposure_s\":10},\"second\":{\"cameraId\":1,\"exposure_s\":20}}");
    using(var repo=new Repository(Path.Combine(root,"completion-conflicting-repo"))){repo.Import(repo.Scan(directory,"Scope","Auto",ct,NoProgress).Frames,ct,NoProgress);var frame=repo.All().Single();frame.Camera="Unknown";frame.CameraEvidence=null;frame.Exposure=null;frame.Facts=null;frame.LinearData=null;repo.Save(frame);var proposal=repo.DetectMissingMetadata(frame,ct);
     Check(proposal.Updated.Camera=="Unknown"&&!proposal.Updated.Exposure.HasValue&&proposal.Updated.LinearData==true,"Conflicting sidecar metadata guessed or unrelated field lost");
     File.AppendAllText(repo.FilePath(new Frame{RelativePath=frame.SidecarRelativePath}),"altered");Expect(()=>repo.ApplyMissingMetadata(proposal,ct),"Changed sidecar accepted after preview");Check(!repo.Find(frame.Hash).LinearData.HasValue,"Partial additions saved despite invalid sidecar");
    }
   });
   Test("Video completion uses container duration rather than filename sub exposure",()=>{
    string path=VideoWrite("completion_Jupiter_60s.mp4",VideoMovie(1000,12500));var detected=Classifier.Read(path,root,"Scope","Auto");var current=new Frame{OriginalName=Path.GetFileName(path),Target="Jupiter",Kind="Video",Camera="Unknown",Hash="keep",RelativePath="keep.mp4"};var result=MetadataCompletion.Fill(current,detected);
    Check(result.Updated.Exposure==12.5&&result.Updated.VideoDurationSeconds==12.5&&result.Updated.Camera=="Telephoto"&&result.Updated.Facts["Exposure"].Source==detected.VideoDurationSource&&!string.IsNullOrEmpty(detected.VideoDurationSource),"Video completion guessed exposure or lost duration evidence");
   });
  }
 }
}
