using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void MountLabelTests(){
   Test("Unknown mounts display exposure suggestions without becoming confirmed metadata",()=>{
    foreach(double? exposure in new double?[]{null,0,10,20,20.001,60,double.NaN,double.PositiveInfinity}){
     var frame=Util.Deserialize<Frame>("{\"Mount\":\"Unknown\",\"MountEvidence\":\"Not analyzed\"}");frame.Exposure=exposure;
     string expected=exposure.HasValue&&!double.IsInfinity(exposure.Value)&&exposure.Value>20?"EQ?":"Alt-Az?";
     Check(frame.MountText==expected&&frame.Mount=="Unknown"&&frame.MountEvidence=="Not analyzed","Boundary or stored mount changed: "+exposure);
     Check(frame.MountEvidenceText.Contains("Exposure-based suggestion"),"Suggestion presented as an analysis result");
    }
    Check(new Frame{Mount=null,Exposure=60}.MountText=="EQ?"&&new Frame{Mount=" unknown ",Exposure=20}.MountText=="Alt-Az?","Legacy empty/case variants did not use the fallback");
   });
   Test("Legacy mount inferences use question marks and retain the inferred model",()=>{
    foreach(string label in new[]{"Alt/Az (likely)","Alt-Az (likely)","AltAz (inferred)","Alt Az probable","Alt-Az?"}){
     var frame=new Frame{Mount=label,Exposure=60};Check(frame.MountText=="Alt-Az?"&&MountLabels.Normalize(label)=="Alt-Az?","Old Alt-Az inference or formatting lost: "+label);
    }
    foreach(string label in new[]{"EQ (likely)","Equatorial (inferred)","EQ tentative","EQ?"})Check(new Frame{Mount=label,Exposure=10}.MountText=="EQ?"&&MountLabels.Normalize(label)=="EQ?","EQ inference changed to exposure guess: "+label);
    Check(MountLabels.Normalize("Unknown")=="Unknown","Inconclusive rotation result became a confirmed mount");
    var grouped=new[]{new Frame{Mount="EQ"},new Frame{Mount="EQ?"},new Frame{Mount="Alt-Az"},new Frame{Mount="Alt-Az?"},new Frame{Mount="Alt/Az (likely)"}};
    var filters=new CaptureFilters();filters.Values["Mount"]="EQ";Check(filters.Apply(grouped,"").Count==2,"EQ and EQ? split into different filter groups");filters.Values["Mount"]="Alt-Az";Check(filters.Apply(grouped,"").Count==3,"Confirmed, inferred and legacy Alt-Az split into different filter groups");
   });
   Test("Explicit imported mount labels stay confirmed despite exposure suggestions",()=>{
    foreach(string mode in new[]{"EQ","ALT/AZ","ALT-AZ","ALTAZ","AZ"}){
     string path=Path.Combine(root,"confirmed-mount-"+mode.Replace('/','-')+".fit");
     Write(path,64,48,(x,y)=>1000,new Dictionary<string,string>{{"MOUNTMOD","'"+mode+"'"},{"EXPTIME",mode=="EQ"?"10":"60"}});
     var frame=Classifier.Read(path,root,"Mount-test","Auto");string expected=mode=="EQ"?"EQ":"Alt-Az";
     Check(frame.Mount==expected&&frame.MountText==expected&&frame.MountEvidence.StartsWith("Explicit"),"Explicit import mount gained uncertainty or was replaced");
    }
    foreach(string evidence in new[]{"User assigned","Explicit FITS mount metadata"})Check(new Frame{Mount="Alt/Az (likely)",MountEvidence=evidence,Exposure=60}.MountText=="Alt-Az","Confirmed override did not take priority");
   });
   Test("Existing database mounts support display search filters and archive paths without reclassification",()=>{
    using(var repo=new Repository(Path.Combine(root,"mount-label-repo"))){
     var unknown=new Frame{Hash=new string('a',64),OriginalName="long.fit",RelativePath="Targets/Unknown/long.fit",Mount="Unknown",Exposure=60};
     var legacy=new Frame{Hash=new string('b',64),OriginalName="az.fit",RelativePath="Targets/AltAz/az.fit",Mount="Alt/Az (likely)",MountEvidence="Coherent rotation",Exposure=60};
     var confirmed=new Frame{Hash=new string('c',64),OriginalName="eq.fit",RelativePath="Targets/EQ/eq.fit",Mount="EQ",MountEvidence="Explicit FITS mount metadata",Exposure=10};
     foreach(var frame in new[]{unknown,legacy,confirmed})repo.Save(frame);
     var rows=repo.All();var filters=new CaptureFilters();
     Check(filters.Apply(rows,"EQ?").Single().Hash==unknown.Hash&&filters.Apply(rows,"Alt-Az?").Single().Hash==legacy.Hash,"Displayed mount labels are not searchable");
     filters.Values["Mount"]="EQ";Check(filters.Apply(rows,"").Count==2,"EQ filter omitted the suggested or confirmed mount");filters.Values["Mount"]="Alt-Az";Check(filters.Apply(rows,"").Single().Hash==legacy.Hash,"Alt-Az filter omitted a legacy inference");
     var reread=repo.All();Check(reread.Single(f=>f.Hash==unknown.Hash).Mount=="Unknown"&&reread.Single(f=>f.Hash==legacy.Hash).Mount=="Alt/Az (likely)"&&reread.Single(f=>f.Hash==legacy.Hash).RelativePath==legacy.RelativePath,"Display rewrote stored metadata or paths");
     string oldPath=repo.Destination(legacy);legacy.Mount="Alt-Az?";Check(repo.Destination(legacy)==oldPath,"New inferred formatting changed the AltAz directory");legacy.Mount="Alt-Az";Check(repo.Destination(legacy)==oldPath,"New confirmed formatting changed the AltAz directory");
     Check(repo.Destination(unknown).Split(Path.DirectorySeparatorChar).Contains("Unknown"),"Exposure guess moved an unclassified capture into EQ");
     string csv=Path.Combine(root,"mount-labels.csv");repo.ExportIndex(csv);string export=File.ReadAllText(csv);Check(export.Contains("\"EQ?\"")&&export.Contains("\"Alt-Az?\"")&&!export.Contains("(likely)"),"Catalogue export retained old mount formatting");
    }
   });
  }
 }
}
