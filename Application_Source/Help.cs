using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public enum HelpBlockKind {Paragraph,Heading,Bullet,Numbered}
 public sealed class HelpBlock {public HelpBlockKind Kind;public string Text;public int Number;}
 public class HelpTopic {
  public string Key{get;set;}public string Title{get;set;}public string Body{get;set;}
  public override string ToString(){return Title;}
 }
 public static class HelpCatalog {
  public static List<HelpTopic> Load(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Quick_Start.txt")){if(stream==null)throw new InvalidOperationException("The bundled guide is missing.");using(var reader=new StreamReader(stream))return Parse(reader.ReadToEnd());}}
  public static List<HelpTopic> Parse(string text){
   var topics=new List<HelpTopic>();var current=new HelpTopic{Key="OVERVIEW",Title="Overview"};var body=new StringBuilder();
   foreach(string raw in (text??"").Replace("\r\n","\n").Split('\n')){string line=raw.Trim();bool heading=line.Length>0&&!line.StartsWith("## ")&&!line.StartsWith("- ")&&!Regex.IsMatch(line,@"^\d+[.)]\s")&&line.Any(char.IsLetter)&&line.All(c=>!char.IsLetter(c)||char.IsUpper(c));
    if(heading){current.Body=body.ToString().Trim();if(current.Body.Length>0)topics.Add(current);current=new HelpTopic{Key=line,Title=Title(line)};body.Clear();}else body.AppendLine(raw);
   }
   current.Body=body.ToString().Trim();if(current.Body.Length>0)topics.Add(current);return topics;
  }
  static string Title(string heading){return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(heading.ToLowerInvariant()).Replace("Usb","USB").Replace("Fits","FITS").Replace("Dwarf","DWARF").Replace("Astap","ASTAP").Replace("Api","API").Replace("Csv","CSV");}
  public static List<HelpTopic> Search(IEnumerable<HelpTopic> topics,string query){var words=Util.Tokens(query);return topics.Where(t=>words.All(w=>(t.Title+"\n"+t.Body).IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0)).ToList();}
  // The offline guide stays readable as text; explicit markers supply layout.
  public static List<HelpBlock> Blocks(string body){
   var blocks=new List<HelpBlock>();HelpBlock current=null;
   foreach(string raw in (body??"").Replace("\r\n","\n").Split('\n')){
    string line=raw.Trim();if(line.Length==0){current=null;continue;}
    var number=Regex.Match(line,@"^(\d{1,3})[.)]\s+(.+)$");
    if(line.StartsWith("## ")){blocks.Add(new HelpBlock{Kind=HelpBlockKind.Heading,Text=line.Substring(3).Trim()});current=null;}
    else if(line.StartsWith("- ")){current=new HelpBlock{Kind=HelpBlockKind.Bullet,Text=line.Substring(2).Trim()};blocks.Add(current);}
    else if(number.Success){current=new HelpBlock{Kind=HelpBlockKind.Numbered,Number=Math.Max(1,int.Parse(number.Groups[1].Value,CultureInfo.InvariantCulture)),Text=number.Groups[2].Value};blocks.Add(current);}
    else if(current!=null&&(current.Kind==HelpBlockKind.Paragraph||char.IsWhiteSpace(raw[0])))current.Text+=" "+line;
    else{current=new HelpBlock{Kind=HelpBlockKind.Paragraph,Text=line};blocks.Add(current);}
   }return blocks;
  }
  public static string PlainText(string body){return string.Join("\r\n\r\n",Blocks(body).Select(b=>(b.Kind==HelpBlockKind.Bullet?"• ":b.Kind==HelpBlockKind.Numbered?b.Number+". ":"")+Regex.Replace(b.Text,@"\*\*(.+?)\*\*|`([^`]+)`",m=>m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value)));}
 }
}
