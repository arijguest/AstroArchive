using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static bool SkipInstalledSiril;
  sealed class StackRunnerDouble:ISessionStackRunner {
   public Action<string,string,CancellationToken> Execute;
   public void Run(string executable,string folder,string script,CancellationToken token,Action<string> log){Execute(folder,script,token);}
  }
  static List<Frame> StackFixture(Repository repo,string suffix,int count,bool calibrated=true){
   string source=Path.Combine(root,"session-stack-source-"+suffix);var random=new Random(51);var stars=new List<Star>();
   for(int y=44;y<360-35;y+=36)for(int x=44;x<480-35;x+=36)stars.Add(new Star(x+random.NextDouble()*5,y+random.NextDouble()*5,4000+random.NextDouble()*8000));
   for(int i=0;i<count;i++){
    int frame=i;var noise=new Random(901+i);var samples=new double[480*360];for(int p=0;p<samples.Length;p++)samples[p]=1200+noise.NextDouble()*35;
    foreach(var star in stars){double sx=star.X+i*0.35,sy=star.Y-i*0.2;for(int y=Math.Max(0,(int)sy-10);y<Math.Min(360,(int)sy+11);y++)for(int x=Math.Max(0,(int)sx-10);x<Math.Min(480,(int)sx+11);x++)samples[y*480+x]+=star.Flux*Math.Exp(-((x-sx)*(x-sx)+(y-sy)*(y-sy))/8.0);}
    var header=LightHeaders(new DateTime(2026,10,8,20,0,0).AddSeconds(i*12),"M45");header["EXPTIME"]="10";header["LINEAR"]="T";header["CALIBRAT"]=calibrated?"T":"F";
    Write(Path.Combine(source,"Light_"+frame.ToString("D3")+".fits"),480,360,(x,y)=>samples[y*480+x],header);
   }
   var scan=repo.Scan(source,"Stack-01","Dwarf 3",ct,NoProgress);var imported=repo.Import(scan.Frames,ct,NoProgress);Check(imported.Imported==count,"Stack fixture import failed");var frames=repo.All();Check(frames.All(SessionStacking.Eligible),"Stack fixture has ineligible metadata: "+string.Join("; ",frames.Select(f=>f.Kind+" "+f.Calibration+" "+f.Format+" "+f.LinearData+" "+f.CapabilityText)));return frames;
  }
  static StackRunnerDouble StackOutputDouble(int contributors,bool broken=false){
   return new StackRunnerDouble{Execute=(folder,script,token)=>{
    Check(script.Contains("register ")&&script.Contains("-weight=wfwhm")&&script.Contains("-filter-included"),"Scientific registration/weighting/inclusion contract missing");
    if(broken){File.WriteAllText(Path.Combine(folder,"result.fits"),"truncated result");return;}
    string first=Directory.GetFiles(Path.Combine(folder,"lights"),"*.fits").OrderBy(p=>p).First();var asset=Assets.Inspect(first);var image=Fits.ReadPixels(first,asset.Images[0],0,token);
    var headers=new Dictionary<string,string>{{"IMAGETYP","'STACK'"},{"STACKCNT",contributors.ToString()},{"EXPTIME",(contributors*10).ToString()},{"LINEAR","T"},{"CALIBRAT","T"}};
    Write(Path.Combine(folder,"result.fits"),image.Width,image.Height,(x,y)=>image.Pixels[y*image.Width+x]+7,headers);
   }};
  }
  static void SessionStackingTests(){
   Test("Session stacks group short individual lights by session and retain unsafe selections",()=>{
    Func<string,string,double,Frame> frame=(hash,session,seconds)=>new Frame{Hash=hash,Kind="Light",Exposure=seconds,StackCount=1,Status="Imported",Session=session,Telescope="Unit",Camera="Telephoto",Target="M45",Filter="None",Width=480,Height=360,Channels=1,Calibration="Calibrated"};
    var rows=new List<Frame>{frame("a","one",10),frame("b","one",10),frame("c","two",10),frame("d","two",10),frame("e","one",11),frame("f","one",0)};
    var rejected=frame("g","one",10);rejected.Rejected=true;rows.Add(rejected);var stack=frame("h","one",10);stack.Kind="Stack";rows.Add(stack);
    var plan=SessionStacking.Plan(rows.Concat(new[]{rows[0]}),new Frame[0]);Check(plan.Groups.Count==2&&plan.Eligible==4&&plan.Skipped==4,"Session grouping, exposure boundary, selection deduplication or eligibility failed");Check(plan.Groups.All(g=>g.Inputs.Count==2&&g.Inputs.Select(f=>f.Session).Distinct().Count()==1),"Sessions merged");
    var container=frame("container","one",10);container.Images=new List<ImageDescriptor>{new ImageDescriptor(),new ImageDescriptor()};container.ImageIndex=0;Check(!SessionStacking.Eligible(container),"An image slice could authorize deleting an entire multi-image container");
   });
   Test("Session quality gate uses the native green/yellow bands and retains red or unscored captures",()=>{
    var green=new Frame{QualityScore=70};var yellow=new Frame{QualityScore=45};var red=new Frame{QualityScore=44.99};var unscored=new Frame();
    Check(green.QualityBand=="High"&&yellow.QualityBand=="Mid"&&SessionStacking.QualityAccepted(green)&&SessionStacking.QualityAccepted(yellow)&&!SessionStacking.QualityAccepted(red)&&!SessionStacking.QualityAccepted(unscored),"Stacking disagrees with the library's established quality colors");
   });
   Test("Failed or invalid session stacks retain every indexed original and source byte",()=>{
    using(var repo=new Repository(Path.Combine(root,"session-stack-fault-repo"))){var frames=StackFixture(repo,"fault",2);var paths=frames.ToDictionary(f=>f.Hash,repo.FilePath);
     foreach(bool broken in new[]{false,true}){
      var runner=broken?StackOutputDouble(2,true):new StackRunnerDouble{Execute=(folder,script,token)=>{throw new IOException("Synthetic external failure");}};
      var result=SessionStacking.Run(repo,frames,new SessionStackOptions{Runner=runner,DeleteOriginals=true},ct,NoProgress);Check(result.Stacked==0&&result.Deleted==0&&result.Errors.Count==1&&repo.All().Count==2,"Failed output was imported or triggered original cleanup");
      foreach(var f in frames)Check(Util.Hash(paths[f.Hash],ct)==f.Hash&&Util.Hash(f.SourcePath,ct)==f.Hash,"Failure changed original bytes");
     }
    }
   });
   Test("Verified session stacks remove only contributors after durable recovery and restore exact originals",()=>{
    using(var repo=new Repository(Path.Combine(root,"session-stack-success-repo"))){var frames=StackFixture(repo,"success",2);var result=SessionStacking.Run(repo,frames,new SessionStackOptions{Runner=StackOutputDouble(2),DeleteOriginals=true},ct,NoProgress);
     Check(result.Stacked==1&&result.Deleted==2&&result.Errors.Count==0&&repo.All().Count==1&&repo.All()[0].Kind=="Stack"&&repo.All()[0].Exposure==20,"Verified output was not imported before original cleanup: "+result.Report);
     var recovery=Util.Deserialize<SessionStackRecovery>(File.ReadAllText(Path.Combine(result.RecoveryFolder,"recovery.json")));Check(recovery.Originals.Count==2,"Recovery manifest incomplete");
     foreach(var item in recovery.Originals)Check(Util.Hash(Path.Combine(result.RecoveryFolder,item.File),ct)==item.Metadata.Hash&&File.Exists(item.Metadata.SourcePath),"Recovery or source originals missing");
     var restored=SessionStacking.RestoreOriginals(repo,result.RecoveryFolder,ct,NoProgress);Check(restored.Imported==2&&restored.Failed==0&&repo.All().Count==3&&frames.All(f=>Util.Hash(repo.FilePath(repo.Find(f.Hash)),ct)==f.Hash),"Exact original recovery failed");
    }
   });
   Test("Registration exclusions prevent all original cleanup for the affected session",()=>{
    using(var repo=new Repository(Path.Combine(root,"session-stack-exclusion-repo"))){var frames=StackFixture(repo,"excluded",3);var result=SessionStacking.Run(repo,frames,new SessionStackOptions{Runner=StackOutputDouble(2),DeleteOriginals=true},ct,NoProgress);Check(result.Stacked==1&&result.Deleted==0&&repo.All().Count==4&&result.Warnings.Any(w=>w.Contains("every supplied frame")),"A frame omitted by registration allowed deletion");}
   });
   Test("Stack cleanup restores changed originals detected after recovery copying",()=>{
    using(var repo=new Repository(Path.Combine(root,"session-stack-mutation-repo"))){var frames=StackFixture(repo,"mutation",2);string changed=null;
     var result=SessionStacking.Run(repo,frames,new SessionStackOptions{Runner=StackOutputDouble(2),DeleteOriginals=true},ct,p=>{if(changed==null&&p.Stage=="Deleting files"){var frame=frames.Single(f=>f.OriginalName==p.Text);changed=repo.FilePath(frame);File.WriteAllText(changed,"external modification after recovery copying");}});
     Check(result.Stacked==1&&result.Deleted==1&&result.Errors.Count==1&&changed!=null&&File.ReadAllText(changed)=="external modification after recovery copying","Changed bytes were deleted or staged rollback failed");Check(repo.All().Count==2,"Changed original lost its index record");
     var recovery=Util.Deserialize<SessionStackRecovery>(File.ReadAllText(Path.Combine(result.RecoveryFolder,"recovery.json")));Check(recovery.Originals.All(i=>Util.Hash(Path.Combine(result.RecoveryFolder,i.File),ct)==i.Metadata.Hash),"Changed current file corrupted verified recovery copies");
    }
   });
   Test("Cancellation during verified staged deletion restores the moved original",()=>{
    using(var repo=new Repository(Path.Combine(root,"session-stack-staged-cancel-repo"))){var frames=StackFixture(repo,"staged-cancel",2);using(var stop=new CancellationTokenSource()){
     Expect(()=>repo.DeleteFrames(frames,stop.Token,p=>{if(p.Stage=="Verifying before deletion")stop.Cancel();},true),"Staged verification ignored cancellation");Check(repo.All().Count==2&&frames.All(f=>Util.Hash(repo.FilePath(f),ct)==f.Hash),"Cancellation stranded a staged original or committed its deletion");
    }}
   });
   Test("Canceled cleanup reports completed removals and retains exact recovery copies",()=>{
    using(var repo=new Repository(Path.Combine(root,"session-stack-cleanup-count-repo"))){var frames=StackFixture(repo,"cleanup-count",2);string job=null;using(var stop=new CancellationTokenSource()){
     Expect(()=>SessionStacking.Run(repo,frames,new SessionStackOptions{Runner=StackOutputDouble(2),DeleteOriginals=true,JobCreated=folder=>job=folder},stop.Token,p=>{if(p.Stage=="Verifying before deletion"&&p.Done==1)stop.Cancel();}),"Cleanup cancellation was ignored");
     Check(job!=null&&File.ReadAllText(Path.Combine(job,"report.txt")).Contains("1 archive originals removed"),"Canceled cleanup lost its completed deletion count");var recovery=Util.Deserialize<SessionStackRecovery>(File.ReadAllText(Path.Combine(job,"recovery.json")));Check(recovery.Originals.Count==2&&recovery.Originals.All(item=>Util.Hash(Path.Combine(job,item.File),ct)==item.Metadata.Hash)&&repo.All().Count==2,"Canceled cleanup lost an output, remaining original or recovery copy");
    }}
   });
   Test("Canceled stacking cannot import an unchecked result or start original cleanup",()=>{
    using(var repo=new Repository(Path.Combine(root,"session-stack-cancel-repo"))){var frames=StackFixture(repo,"cancel",2);using(var stop=new CancellationTokenSource()){string created=null;
     var output=StackOutputDouble(2);var runner=new StackRunnerDouble{Execute=(folder,script,token)=>{output.Run(null,folder,script,token,null);stop.Cancel();}};
     Expect(()=>SessionStacking.Run(repo,frames,new SessionStackOptions{Runner=runner,DeleteOriginals=true,JobCreated=folder=>created=folder},stop.Token,NoProgress),"Cancellation ignored");Check(repo.All().Count==2&&frames.All(f=>File.Exists(repo.FilePath(f))),"Canceled result changed the archive");
     Check(created!=null&&File.Exists(Path.Combine(created,"report.txt"))&&Util.Deserialize<SessionStackRecovery>(File.ReadAllText(Path.Combine(created,"recovery.json"))).State.StartsWith("Canceled"),"Canceled processing lost its job/recovery link or durable report");Check(repo.Background.Conflict(frames.Select(f=>f.Hash))==null,"Canceled processing left an archive claim behind");
    }}
   });
   WindowsTest("Installed Siril registers and quality-weights a real synthetic session locally",()=>{
    if(SkipInstalledSiril){skipped++;Console.WriteLine("SKIP optional installed Siril acceptance");return;}
    string executable;try{executable=SessionStacking.ResolveExecutable(null);}catch(IOException){skipped++;Console.WriteLine("SKIP optional installed Siril acceptance");return;}
    using(var repo=new Repository(Path.Combine(root,"session-stack-siril-repo"))){var frames=StackFixture(repo,"siril",4);var result=SessionStacking.Run(repo,frames,new SessionStackOptions{SirilExecutable=executable,DeleteOriginals=false},ct,NoProgress);
     Check(result.Stacked==1&&result.Deleted==0&&result.Errors.Count==0&&result.Outputs.Single().StackCount==4&&result.Outputs.Single().Exposure==40,"Installed Siril failed its synthetic workflow: "+result.Report);Check(repo.All().Count==5&&result.Quality.All(q=>q.Grade=="Green"||q.Grade=="Yellow"),"Installed workflow did not retain inputs or measure quality");
     foreach(var frame in frames)Check(Util.Hash(repo.FilePath(frame),ct)==frame.Hash,"Installed Siril modified an original");
    }
   });
   SessionStackingInstalledCancellationTests();
   WindowsTest("Installed Siril builds matching raw darks with temperature fallback and preserves calibration originals",()=>{
    if(SkipInstalledSiril){skipped++;Console.WriteLine("SKIP optional installed Siril calibration acceptance");return;}
    string executable;try{executable=SessionStacking.ResolveExecutable(null);}catch(IOException){skipped++;Console.WriteLine("SKIP optional installed Siril calibration acceptance");return;}
    using(var repo=new Repository(Path.Combine(root,"session-stack-siril-cal-repo"))){var lights=StackFixture(repo,"siril-cal",2,false);foreach(var light in lights){light.BinX=light.BinY=1;repo.Save(light);}string source=Path.Combine(root,"session-stack-dark-source");
     for(int i=0;i<2;i++){int index=i;var header=LightHeaders(new DateTime(2026,10,8,20,0,0),"M45");header["IMAGETYP"]="'DARK'";header["EXPTIME"]="10";header["CCD-TEMP"]="30";header["LINEAR"]="T";header["CALIBRAT"]="F";header["XBINNING"]=header["YBINNING"]="1";Write(Path.Combine(source,"Dark_"+i+".fits"),480,360,(x,y)=>120+index+(x*7+y*3)%13,header);}
     var imported=repo.Import(repo.Scan(source,"Stack-01","Dwarf 3",ct,NoProgress).Frames,ct,NoProgress);Check(imported.Imported==2,"Raw dark fixture import failed");var darks=repo.All().Where(f=>f.Kind=="Dark").ToList();var planned=SessionStacking.Plan(lights,repo.All());Check(darks.Count==2&&planned.Groups.Single().Calibrations.Count==2&&planned.Warnings.Any(w=>w.Contains("20")),"Safe large-temperature fallback was not selected");
     var result=SessionStacking.Run(repo,lights,new SessionStackOptions{SirilExecutable=executable,DeleteOriginals=true},ct,NoProgress);Check(result.Stacked==1&&result.Deleted==2&&result.Errors.Count==0&&result.Outputs.Single().StackCount==2,"Installed raw-dark workflow failed: "+result.Report);
     foreach(var dark in darks)Check(repo.Find(dark.Hash)!=null&&Util.Hash(repo.FilePath(dark),ct)==dark.Hash&&Util.Hash(dark.SourcePath,ct)==dark.Hash,"A calibration original was changed or removed");
     var recovery=Util.Deserialize<SessionStackRecovery>(File.ReadAllText(Path.Combine(result.RecoveryFolder,"recovery.json")));Check(recovery.Originals.Count==2&&recovery.Originals.All(i=>i.Metadata.Kind=="Light"),"Calibration frames entered original cleanup");
    }
   });
  }
  static void SessionStackingInstalledCancellationTests(){
   WindowsTest("Installed Siril cancels on first external progress and retains originals and its job report",()=>{
    if(SkipInstalledSiril){skipped++;Console.WriteLine("SKIP optional installed Siril cancellation acceptance");return;}
    string executable;try{executable=SessionStacking.ResolveExecutable(null);}catch(IOException){skipped++;Console.WriteLine("SKIP optional installed Siril cancellation acceptance");return;}
    using(var repo=new Repository(Path.Combine(root,"session-stack-live-cancel-repo"))){var frames=StackFixture(repo,"live-cancel",2);string job=null;bool external=false;using(var stop=new CancellationTokenSource()){
     Expect(()=>SessionStacking.Run(repo,frames,new SessionStackOptions{SirilExecutable=executable,DeleteOriginals=true,JobCreated=folder=>job=folder},stop.Token,p=>{if((p.Stage??"").StartsWith("Stacking session")){external=true;stop.Cancel();}}),"Installed Siril cancellation was ignored");Check(external&&repo.All().Count==2&&frames.All(f=>Util.Hash(repo.FilePath(f),ct)==f.Hash)&&File.Exists(Path.Combine(job,"report.txt")),"External cancellation lost an original or its report");Check(repo.Background.Conflict(frames.Select(f=>f.Hash))==null,"External cancellation retained an archive claim");
    }}
   });
  }
 }
}
