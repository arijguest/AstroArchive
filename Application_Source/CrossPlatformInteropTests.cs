// C# 5: run this same fixture protocol on Windows Framework and Linux .NET.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace AstroArchive {
 public static class CrossPlatformInterop {
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS interop: "+text);}
  static void Fits(string path,string target,int seed){Directory.CreateDirectory(Path.GetDirectoryName(path));string[] cards={"SIMPLE  =                    T","BITPIX  =                   16","NAXIS   =                    2","NAXIS1  =                   16","NAXIS2  =                   16","OBJECT  = '"+target+"'","IMAGETYP= 'LIGHT'","DATE-OBS= '2026-10-06T21:00:00'","EXPTIME =                   60","FILTER  = 'L'","END"};var bytes=new byte[5760];Encoding.ASCII.GetBytes(string.Concat(cards.Select(c=>c.PadRight(80))).PadRight(2880)).CopyTo(bytes,0);for(int i=2880;i<3392;i++)bytes[i]=(byte)(i+seed);File.WriteAllBytes(path,bytes);}
  public static int Run(string root,string mode){try{
   var ct=CancellationToken.None;Action<ProgressInfo> progress=p=>{};string archive=Path.Combine(root,"repository"),source=Path.Combine(root,"source"),exports=Path.Combine(root,"exports");Directory.CreateDirectory(exports);
   Repository.LocalIndexBase=Path.Combine(root,"working-indexes");
   if(mode=="--interop-create"){
    Fits(Path.Combine(source,"Light_M31.fit"),"M31",11);Fits(Path.Combine(source,"Light_M45.fit"),"M45",17);File.WriteAllText(Path.Combine(source,"shotsInfo.json"),"{\"targetName\":\"M31\",\"cameraId\":0}");
    using(var repo=new Repository(archive)){
     var result=repo.Import(repo.Scan(source,"Drive scope","Dwarf 3",ct,progress).Frames,ct,progress);Check(result.Imported==2,"creator imports two original captures");
     var project=repo.CreateEditedWorkingCopies(repo.All(),"Portable working copies","Editor",ct,progress);
     string nested=Path.Combine(repo.EditedProjectFolder(project),"nested");Directory.CreateDirectory(nested);Fits(Path.Combine(nested,"edited_M31.fit"),"M31",23);
     project.MetadataEdits=new Dictionary<string,EditedMetadata>();project.MetadataEdits[Path.Combine("nested","edited_M31.fit")]=new EditedMetadata{Object="M33"};
     Util.AtomicText(Path.Combine(repo.EditedProjectFolder(project),"edited-project.json"),Util.Serialize(project));
     repo.Checkpoint(ct);Check(repo.Verify(ct,progress)==0,"creator verifies archive pixels");
    }
    File.WriteAllText(Path.Combine(root,"creator.txt"),Environment.OSVersion.Platform.ToString());
   }else{
    using(var repo=new Repository(archive)){
     int count=mode=="--interop-update"?2:3;Check(repo.All().Count==count,"reader sees the other platform's current index");
     Check(repo.Verify(ct,progress)==0,"all archive hashes survive drive movement");
     Check(repo.All().All(f=>!string.IsNullOrEmpty(f.SidecarRelativePath)&&File.Exists(Path.Combine(repo.Root,f.SidecarRelativePath))),"session metadata remains accessible");
     List<string> errors;var projects=repo.EditedProjects(out errors);Check(errors.Count==0&&projects.Count==1,"Edited project identity and legacy dates survive");
     var images=repo.EditedImages(projects[0]);Check(images.Count==3&&images.Single(i=>i.Filename=="edited_M31.fit").Metadata.Object=="M33","nested Edited paths and metadata overrides survive separators");
     string exported=Exporter.Create(repo,repo.All(),new ExportOptions{Parent=exports,Name="export-"+Guid.NewGuid().ToString("N"),CreateNewFolder=true,Mode="Files",AddMetadata=true},ct,progress);
     Check(Directory.GetFiles(exported,"*.fit",SearchOption.AllDirectories).Length==count,"reader exports every expected original");
     if(mode=="--interop-update"){
      var frame=repo.All().Single(f=>f.OriginalName=="Light_M31.fit");frame.Target="M33";repo.Refile(frame,ct);
      Fits(Path.Combine(source,"Light_M42.fit"),"M42",31);var plan=repo.Scan(source,"Drive scope","Dwarf 3",ct,progress);
      var result=repo.Import(plan.Frames,ct,progress);Check(result.Imported==1&&repo.All().Count==3&&plan.Frames.Count(f=>f.Status.StartsWith("Duplicate",StringComparison.Ordinal))==2,"second platform adds one capture without duplicating originals");
      repo.Checkpoint(ct);
     }else Check(repo.All().Any(f=>f.OriginalName=="Light_M31.fit"&&f.Target=="M33"),"metadata refiling survives the return trip");
    }
   }
   Console.WriteLine("Interop complete: "+mode+" on "+Environment.OSVersion.Platform);return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 }
}
