// C# 5 / .NET Framework 4.8. Folder signatures establish make, not a specific model.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public static class InstrumentDetection {
  public static string MakeOf(string model) {
   string text=model??"";
   if(Regex.IsMatch(text,@"\bseestar\b|^S(?:30|50)(?:\s*Pro)?$",RegexOptions.IgnoreCase))return "Seestar";
   if(Regex.IsMatch(text,@"\bdwarf(?:lab|[ _-]*(?:III|II|3|2|mini))?\b",RegexOptions.IgnoreCase))return "DWARFLAB";
   return text.Length==0||text=="Auto"||text.Contains("unknown")?"Unknown":"Other";
  }
  static string SpecificModel(string text,string make) {
   var seestar=Regex.Match(text??"",@"\bS(30|50)(?:\s*[-_]?\s*(Pro))?\b",RegexOptions.IgnoreCase);
   if(make=="Seestar"&&seestar.Success)return "Seestar S"+seestar.Groups[1].Value+(seestar.Groups[2].Success?" Pro":"");
   var dwarf=Regex.Match(text??"",@"\bDWARF(?:\s*[-_]?\s*)(mini|III|II|3|2)\b",RegexOptions.IgnoreCase);
   if(make=="DWARFLAB"&&dwarf.Success){string n=dwarf.Groups[1].Value.ToUpperInvariant();return n=="MINI"?"Dwarf mini":n=="II"||n=="2"?"Dwarf II":"Dwarf 3";}
   return make=="Seestar"?"Seestar (model unknown)":make=="DWARFLAB"?"Dwarf (model unknown)":"Other / unknown";
  }
  public static void Apply(Frame frame,FitsHeader header,string root,string requested) {
   if(!string.IsNullOrEmpty(requested)&&requested!="Auto") {
    frame.Model=requested;frame.Make=MakeOf(requested);frame.MakeEvidence="User-selected model";return;
   }
   string metadata=string.Join(" ",new[]{header.Get("TELESCOP"),header.Get("INSTRUME"),header.Get("CAMERA")});
   string make=MakeOf(metadata);
   if(make=="Seestar"||make=="DWARFLAB") {
    frame.Make=make;frame.Model=SpecificModel(metadata,make);frame.MakeEvidence="Explicit FITS instrument metadata";return;
   }
   string relative=frame.SourcePath.Substring(root.TrimEnd('\\','/').Length).TrimStart('\\','/');
   string context=Path.GetFileName(root.TrimEnd('\\','/'))+"/"+relative.Replace('\\','/');
   bool dwarf=Regex.IsMatch(context,@"(?:^|/)(?:DWARF_(?:RAW|DARK|CALIBRATION)(?:[_/]|$)|CALI_FRAME(?:/|$)|(?:sdcard[-_ ]?)?DWARF\s*(?:II|III|2|3|mini)?(?:/|$))",RegexOptions.IgnoreCase)||!string.IsNullOrEmpty(frame.SourceMetadataPath);
   bool seestar=Regex.IsMatch(context,@"(?:^|/)(?:MyWorks|EMMC Images|Seestar(?:[ _-]+S(?:30|50)(?:[ _-]+Pro)?)?)(?:/|$)",RegexOptions.IgnoreCase);
   if(dwarf) {frame.Make="DWARFLAB";frame.Model=SpecificModel(context,"DWARFLAB");frame.MakeEvidence=!string.IsNullOrEmpty(frame.SourceMetadataPath)?"DWARF shotsInfo.json session metadata":"DWARF_RAW / DWARF_DARK / CALI_FRAME folder structure";}
   else if(seestar) {frame.Make="Seestar";frame.Model=SpecificModel(context,"Seestar");frame.MakeEvidence="Seestar MyWorks / EMMC Images folder structure";}
   else if(Regex.IsMatch(Path.GetDirectoryName(frame.SourcePath)??"",@"[_-]sub$",RegexOptions.IgnoreCase)&&Regex.IsMatch(frame.OriginalName??"",@"^Light[_-]",RegexOptions.IgnoreCase)) {
    frame.Make="Seestar";frame.Model="Seestar (model unknown)";frame.MakeEvidence="Inferred from a Seestar-style sub folder and Light filename";
   } else {frame.Make="Unknown";frame.Model="Other / unknown";frame.MakeEvidence="No distinctive instrument metadata or folder signature";}
  }
 }
 public static class TargetIdentification {
  public static bool ApplyFilename(Frame frame) {
   string target=Catalog.TargetFromFilename(frame.OriginalName);if(target==null)return false;
   frame.Target=target;frame.TargetEvidence="Recognised filename target";
   frame.Notes=(frame.Notes??"")+"Target identified from filename: "+target+". ";return true;
  }
  public static bool NeedsPlateSolve(Frame frame) {
   return frame.Target!="Calibration"&&!(frame.TargetEvidence??"").StartsWith("User")&&(Catalog.HasFilenameConflict(frame.OriginalName)||(Catalog.TargetFromFilename(frame.OriginalName)==null&&Catalog.KnownName(frame.Target)==null));
  }
 }
}
