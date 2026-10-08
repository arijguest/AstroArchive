using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public static class MediaFiles {
  public static bool Gif(string path){return Assets.Extension(path)==".gif";}
  public static bool Video(string path){return new[]{".avi",".mp4",".mov",".m4v",".wmv",".mkv",".ser"}.Contains(Assets.Extension(path));}
  public static bool Motion(string path){return Gif(path)||Video(path);}
  static string Stem(string path){string leaf=path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)?path.Substring(0,path.Length-3):path;return Path.GetFileNameWithoutExtension(leaf).ToLowerInvariant();}
  public static string MatchingImage(string animation,IEnumerable<string> images){
   string stem=Stem(animation),shortStem=Regex.Replace(stem,@"(?:[_ -]+(?:animation|animated|processing|process|progress|timelapse|before[_ -]?after|edit[_ -]?history))+$","");
   var nearby=images.Where(p=>!Motion(p)&&string.Equals(Path.GetDirectoryName(p),Path.GetDirectoryName(animation),StringComparison.OrdinalIgnoreCase)).ToList();
   var exact=nearby.Where(p=>Stem(p)==stem).ToList();if(exact.Count>0)return exact.Count==1?exact[0]:null;
   var matches=nearby.Where(p=>Stem(p)==shortStem).ToList();return matches.Count==1?matches[0]:null;
  }
 }
}
