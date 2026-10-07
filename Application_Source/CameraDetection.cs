// DWARF cam_0 = telephoto, cam_1 = wide; dimensions alone do not identify a camera.
using System;using System.Collections.Generic;using System.Linq;using System.Text.RegularExpressions;
namespace AstroArchive {public static class CameraDetection {
 static string Camera(string value){string text=Regex.Replace((value??"").ToLowerInvariant(),@"[ _-]","");return new[]{"0","cam0","camera0","tele","telephoto"}.Contains(text)?"Telephoto":new[]{"1","cam1","camera1","wide","wideangle"}.Contains(text)?"Wide":null;}
 public static void Apply(Frame f,FitsHeader h,string context,string sidecar){var cameras=new HashSet<string>();var evidence=new List<string>();foreach(string key in new[]{"CAMID","CAMERAID","CHANNEL","CAMERA","CAMNAME"}){string camera=Camera(h.Get(key));if(camera!=null){cameras.Add(camera);evidence.Add("FITS "+key+"="+h.Get(key));}}
 foreach(Match marker in Regex.Matches(context??"",@"(?:^|[/\\_ .-])(cam[_-]?[01]|telephoto|tele|wide(?:[_-]?angle)?)(?=$|[/\\_ .-])",RegexOptions.IgnoreCase)){string camera=Camera(marker.Groups[1].Value);if(camera!=null){cameras.Add(camera);evidence.Add("Folder/filename "+marker.Groups[1].Value);}}
 string json=Camera(sidecar);if(json!=null){cameras.Add(json);evidence.Add("shotsInfo camera ID");}f.Camera=cameras.Count==1?cameras.First():"Unknown";f.CameraEvidence=cameras.Count>1?"Conflicting camera markers; manual review required":cameras.Count==0?"No camera marker; dimensions alone are insufficient":string.Join("; ",evidence.Distinct());if(cameras.Count>1)f.Notes+="Conflicting tele/wide metadata. ";
 }}}
