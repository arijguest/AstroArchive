using System;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public static class MountLabels {
  // Keep exposure guesses in the display layer: Unknown remains available to analysis.
  public static string Type(string value){
   string key=(value??"").Trim().ToUpperInvariant().Replace('–','-').Replace('—','-');
   if(Regex.IsMatch(key,@"^(EQ|EQUATORIAL)(\b|\()"))return "EQ";
   if(Regex.IsMatch(key,@"^(ALT[\s/_-]*AZ(?:IMUTH)?|AZ)(\b|\()"))return "Alt-Az";
   return "";
  }
  public static bool Inferred(string value){return Regex.IsMatch(value??"",@"\?|\b(likely|inferred|probable|possible|estimated|tentative|uncertain|suspected)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);}
  public static string Normalize(string value){string type=Type(value);return type.Length>0?type+(Inferred(value)?"?":""):string.IsNullOrWhiteSpace(value)?"Unknown":value.Trim();}
  static bool Confirmed(Frame frame){return (frame.MountEvidence??"").StartsWith("User",StringComparison.OrdinalIgnoreCase)||(frame.MountEvidence??"").StartsWith("Explicit",StringComparison.OrdinalIgnoreCase);}
  static bool Unknown(Frame frame){return string.IsNullOrWhiteSpace(frame.Mount)||(frame.Mount??"").Trim().StartsWith("Unknown",StringComparison.OrdinalIgnoreCase);}
  public static string Display(Frame frame){
   string type=Type(frame.Mount);if(type.Length>0)return type+(Inferred(frame.Mount)&&!Confirmed(frame)?"?":"");
   if(Unknown(frame))return frame.Exposure.HasValue&&!double.IsNaN(frame.Exposure.Value)&&!double.IsInfinity(frame.Exposure.Value)&&frame.Exposure.Value>20?"EQ?":"Alt-Az?";
   return frame.Mount.Trim();
  }
  public static string Evidence(Frame frame){return Type(frame.Mount).Length==0&&Unknown(frame)?"Exposure-based suggestion: over 20 s suggests EQ?; 20 s or less (or unknown exposure) suggests Alt-Az?. Mount not confirmed."+(string.IsNullOrWhiteSpace(frame.MountEvidence)?"":"\n"+frame.MountEvidence):frame.MountEvidence;}
 }
}
