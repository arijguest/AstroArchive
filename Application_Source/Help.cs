using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
namespace AstroArchive {
 public class HelpTopic {
  public string Key{get;set;}public string Title{get;set;}public string Body{get;set;}
  public override string ToString(){return Title;}
 }
 public static class HelpCatalog {
  public static List<HelpTopic> Load(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Quick_Start.txt")){if(stream==null)throw new InvalidOperationException("The bundled guide is missing.");using(var reader=new StreamReader(stream))return Parse(reader.ReadToEnd());}}
  public static List<HelpTopic> Parse(string text){
   var topics=new List<HelpTopic>();var current=new HelpTopic{Key="OVERVIEW",Title="Overview"};var body=new StringBuilder();
   foreach(string raw in (text??"").Replace("\r\n","\n").Split('\n')){string line=raw.Trim();bool heading=line.Length>0&&line.Any(char.IsLetter)&&line.All(c=>!char.IsLetter(c)||char.IsUpper(c));
    if(heading){current.Body=body.ToString().Trim();if(current.Body.Length>0)topics.Add(current);current=new HelpTopic{Key=line,Title=Title(line)};body.Clear();}else body.AppendLine(raw);
   }
   current.Body=body.ToString().Trim();if(current.Body.Length>0)topics.Add(current);return topics;
  }
  static string Title(string heading){return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(heading.ToLowerInvariant()).Replace("Usb","USB").Replace("Fits","FITS").Replace("Dwarf","DWARF").Replace("Astap","ASTAP").Replace("Api","API").Replace("Csv","CSV");}
  public static List<HelpTopic> Search(IEnumerable<HelpTopic> topics,string query){var words=Util.Tokens(query);return topics.Where(t=>words.All(w=>(t.Title+"\n"+t.Body).IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0)).ToList();}
 }
}
