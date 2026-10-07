// Generated captures exercise filename exclusions through every import path.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void FailedFilenameTests(){
   Test("Ignore failed skips matching filenames before reading FITS data",()=>{
    string source=Path.Combine(root,"failed-name-scan-source");Directory.CreateDirectory(source);
    File.WriteAllText(Path.Combine(source,"capture_FAILED.fits.gz"),"not gzip or FITS");
    File.WriteAllText(Path.Combine(source,"unfailed.fts"),"not FITS");
    string good=Path.Combine(source,"failed-folder","healthy.fit");Write(good,64,48,(x,y)=>1200,LightHeaders(new DateTime(2026,10,7),"Failed Nebula"));
    using(var repo=new Repository(Path.Combine(root,"failed-name-scan-repo"))){
     var plan=repo.Scan(source,"Scope","Auto",ct,NoProgress,ignoreFailed:true);
     Check(plan.IgnoredFailed==2&&plan.Errors.Count==0&&plan.Frames.Single().OriginalName=="healthy.fit","Ignored files were read, or folder/target text was matched");
     Check(plan.Metrics.Snapshot().Single(m=>m.Stage=="Discovery").Files==1,"Ignored files polluted discovery totals");
     var unfiltered=repo.Scan(source,"Scope","Auto",ct,NoProgress);
     Check(unfiltered.IgnoredFailed==0&&unfiltered.Frames.Count==3&&unfiltered.Errors.Count==2,"Default scan unexpectedly ignores filenames");
    }
   });
   Test("Import enforces ignore failed without removing sources or suppressing retries",()=>{
    string source=Path.Combine(root,"failed-name-import-source");Directory.CreateDirectory(source);
    string failed=Path.Combine(source,"Light_FaIlEd.fit"),good=Path.Combine(source,"healthy.fit");
    Write(failed,64,48,(x,y)=>1300,new Dictionary<string,string>());Write(good,64,48,(x,y)=>1400,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"failed-name-import-repo"))){
     var plan=repo.Scan(source,"Scope","Auto",ct,NoProgress);plan.Frames.Single(f=>f.OriginalName=="healthy.fit").Status="Failed";
     var result=repo.Import(plan.Frames,ct,NoProgress,new ImportOptions{IgnoreFailed=true,DeleteOriginals=true,SourceRoot=source});
     Check(result.Imported==1&&result.IgnoredFailed==1&&result.Failed==0&&repo.All().Single().OriginalName=="healthy.fit","Import ignored by status rather than filename, or bypassed exclusion");
     Check(File.Exists(failed)&&repo.Deletions().Count==0&&repo.LastReport.Contains("1 failed filenames ignored"),"Ignored original was removed, recorded as deleted, or omitted from report");
     var enabledAgain=repo.Import(plan.Frames.Where(f=>f.OriginalName=="Light_FaIlEd.fit"),ct,NoProgress);
     Check(enabledAgain.Imported==1&&repo.All().Count==2,"Turning ignore off did not allow the original capture");
    }
   });
   Test("An import containing only failed filenames stays empty",()=>{
    using(var repo=new Repository(Path.Combine(root,"failed-name-only-repo"))){
     var input=new Frame{OriginalName="FAILED.fit",SourcePath=Path.Combine(root,"does-not-exist.fit"),Status="New",Bytes=6000};
     var result=repo.Import(new[]{input},ct,NoProgress,new ImportOptions{IgnoreFailed=true});
     Check(result.IgnoredFailed==1&&result.Imported==0&&result.Failed==0&&repo.All().Count==0&&result.Metrics.TotalBytes==0,"All-ignored input copied data or failed");
    }
   });
   Test("Delete failed finds all indexed filename matches and retains other captures",()=>{
    string source=Path.Combine(root,"failed-name-delete-source");Directory.CreateDirectory(source);
    string[] names={"Light_FAILED.fit","unfailed.fit","healthy.fit"};
    for(int i=0;i<names.Length;i++)Write(Path.Combine(source,names[i]),64,48,(x,y)=>1500+100*i,new Dictionary<string,string>());
    string compressed=Path.Combine(source,"Dark_failed.fits.gz");using(var output=File.Create(compressed))using(var gzip=new GZipStream(output,CompressionMode.Compress)){byte[] data=File.ReadAllBytes(Path.Combine(source,names[0]));data[2880]++;gzip.Write(data,0,data.Length);}
    using(var repo=new Repository(Path.Combine(root,"failed-name-delete-repo"))){
     repo.Import(repo.Scan(source,"Scope-A","Auto",ct,NoProgress).Frames,ct,NoProgress);
     var other=repo.All().Single(f=>f.OriginalName=="unfailed.fit");other.Telescope="Scope-B";repo.Refile(other,ct);
     var healthy=repo.All().Single(f=>f.OriginalName=="healthy.fit");healthy.Status="Failed";healthy.Target="Failed Nebula";repo.Save(healthy);
     var matches=repo.FailedFiles();Check(matches.Count==3&&matches.Any(f=>f.Telescope=="Scope-B"),"Finder missed a telescope, substring or compressed filename");
     var paths=matches.Select(repo.FilePath).ToList();var result=repo.DeleteFrames(matches,ct,NoProgress);
     Check(result.Deleted==3&&result.Errors.Count==0&&paths.All(p=>!File.Exists(p))&&repo.All().Single().OriginalName=="healthy.fit","Cleanup deleted nonmatching files or missed indexed copies");
     Check(names.All(n=>File.Exists(Path.Combine(source,n)))&&File.Exists(compressed)&&repo.Deletions().Count==3,"Cleanup removed originals or lost deletion history");
     Check(repo.Scan(source,"Scope-A","Auto",ct,NoProgress).Frames.Count(f=>f.Status=="Deleted")==3,"Deleted failures became eligible for reimport");
    }
   });
   Test("USB auto upload respects ignore failed and reports skipped filenames",()=>{
    string source=Path.Combine(root,"name-rule-usb-source");Directory.CreateDirectory(source);
    File.WriteAllText(Path.Combine(source,"solving_FAILED.fit"),"broken");Write(Path.Combine(source,"healthy.fit"),64,48,(x,y)=>1800,new Dictionary<string,string>());
    using(var repo=new Repository(Path.Combine(root,"failed-name-usb-repo"))){
     var result=UsbAutoUpload.Run(repo,new TelescopeProfile{Id="USB-Scope",Model="Auto"},source,1,ct,NoProgress,ignoreFailed:true);
     Check(result.Import.Imported==1&&result.Import.IgnoredFailed==1&&result.Plan.Errors.Count==0&&result.Summary.Contains("1 failed filenames ignored"),"Automatic upload did not inherit exclusions or summary");
     Check(File.Exists(Path.Combine(source,"solving_FAILED.fit")),"Automatic upload removed ignored input");
    }
   });
   Test("Dump import retains ignored files and applies the saved filename rule",()=>{
    using(var repo=new Repository(Path.Combine(root,"failed-name-dump-repo"))){
     repo.EnsureDumpFolder();string ignored=Path.Combine(repo.DumpFolder,"capture_failed.fit");File.WriteAllText(ignored,"broken");Write(Path.Combine(repo.DumpFolder,"healthy.fit"),64,48,(x,y)=>1900,new Dictionary<string,string>());
     var result=repo.ProcessDump(ct,NoProgress,ignoreFailed:true);
     Check(result.Import.Imported==1&&result.Import.IgnoredFailed==1&&result.Plan.Errors.Count==0&&File.Exists(ignored),"Dump ignored input was read or removed");
     Check(repo.LastReport.Contains("1 failed filenames ignored"),"Dump report omitted exclusions");
    }
   });
  }
 }
}
