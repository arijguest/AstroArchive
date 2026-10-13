using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public enum HelpBlockKind {Paragraph,Heading,Bullet,Numbered,Callout}
 public sealed class HelpBlock {public HelpBlockKind Kind;public string Text;public int Number;public string Label;}
 public class HelpTopic {
  public string Key{get;set;}public string Title{get;set;}public string Body{get;set;}
  // Optional "@name value" lines under a topic heading: where it is filed, a one-line answer,
  // the guided walkthrough that shows it in the app, related topics and extra search words.
  public string Category{get;set;}public string Summary{get;set;}public string Walkthrough{get;set;}public string Keywords{get;set;}
  public List<string> Related{get;set;}
  public HelpTopic(){Category=HelpCatalog.Reference;Related=new List<string>();}
  public override string ToString(){return Title;}
 }
 public static class HelpCatalog {
  public const string Reference="Reference";
  // Categories in reading order; the guide lists topics under these headings.
  public static readonly string[] Categories={"Get started","Import","Browse and preview","Edited images","Export and stacking","Analysis and metadata","Keep your archive safe","Fix a problem",Reference};
  static readonly string[] callouts={"Tip","Note","Important"};
  static readonly Regex inline=new Regex(@"\*\*(.+?)\*\*|`([^`]+)`|\[([^\]]+)\]\(((?:topic|walkthrough):[^)]+)\)");
  public static List<HelpTopic> Load(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Quick_Start.txt")){if(stream==null)throw new InvalidOperationException("The bundled guide is missing.");using(var reader=new StreamReader(stream))return Parse(reader.ReadToEnd());}}
  public static List<HelpTopic> Parse(string text){
   var topics=new List<HelpTopic>();var current=new HelpTopic{Key="OVERVIEW",Title="Overview"};var body=new StringBuilder();
   foreach(string raw in (text??"").Replace("\r\n","\n").Split('\n')){string line=raw.Trim();bool heading=line.Length>0&&!line.StartsWith("## ")&&!line.StartsWith("- ")&&!line.StartsWith(">")&&!line.StartsWith("@")&&!Regex.IsMatch(line,@"^\d+[.)]\s")&&line.Any(char.IsLetter)&&line.All(c=>!char.IsLetter(c)||char.IsUpper(c));
    if(heading){Finish(current,body,topics);current=new HelpTopic{Key=line,Title=Title(line)};body.Clear();}
    else if(!Metadata(current,line))body.AppendLine(raw);
   }
   Finish(current,body,topics);return topics;
  }
  static void Finish(HelpTopic topic,StringBuilder body,List<HelpTopic> topics){topic.Body=body.ToString().Trim();if(topic.Body.Length>0)topics.Add(topic);}
  static bool Metadata(HelpTopic topic,string line){
   var match=Regex.Match(line,@"^@(title|category|summary|walkthrough|related|keywords)\s+(.+)$");if(!match.Success)return false;string value=match.Groups[2].Value.Trim();
   switch(match.Groups[1].Value){
    case "title":topic.Title=value;break;
    case "category":topic.Category=Categories.Contains(value)?value:Reference;break;
    case "summary":topic.Summary=value;break;
    case "walkthrough":topic.Walkthrough=value;break;
    case "keywords":topic.Keywords=value;break;
    default:topic.Related.AddRange(value.Split('|').Select(v=>v.Trim()).Where(v=>v.Length>0));break;
   }return true;
  }
  static string Title(string heading){return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(heading.ToLowerInvariant()).Replace("Usb","USB").Replace("Fits","FITS").Replace("Dwarf","DWARF").Replace("Astap","ASTAP").Replace("Api","API").Replace("Csv","CSV");}
  public static int CategoryOrder(string category){int index=Array.IndexOf(Categories,category);return index<0?Categories.Length:index;}
  // Every word must appear somewhere; titles outrank summaries and keywords, which outrank body text.
  public static List<HelpTopic> Search(IEnumerable<HelpTopic> topics,string query){
   var words=Util.Tokens(query);var list=topics.ToList();
   return list.Select((topic,index)=>new{topic,index,score=Score(topic,words)}).Where(r=>r.score>0||words.Length==0).OrderByDescending(r=>r.score).ThenBy(r=>r.index).Select(r=>r.topic).ToList();
  }
  static int Score(HelpTopic topic,IList<string> words){
   int score=0;foreach(string word in words){int best=Has(topic.Title,word)?6:Has(topic.Summary,word)||Has(topic.Keywords,word)?3:Has(topic.Body,word)||Has(topic.Category,word)?1:0;if(best==0)return 0;score+=best;}return score;
  }
  static bool Has(string text,string word){return text!=null&&text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0;}
  // The offline guide stays readable as text; explicit markers supply layout.
  public static List<HelpBlock> Blocks(string body){
   var blocks=new List<HelpBlock>();HelpBlock current=null;
   foreach(string raw in (body??"").Replace("\r\n","\n").Split('\n')){
    string line=raw.Trim();if(line.Length==0){current=null;continue;}
    var number=Regex.Match(line,@"^(\d{1,3})[.)]\s+(.+)$");var callout=Regex.Match(line,@"^>\s*(\w+):\s*(.+)$");
    if(line.StartsWith("## ")){blocks.Add(new HelpBlock{Kind=HelpBlockKind.Heading,Text=line.Substring(3).Trim()});current=null;}
    else if(line.StartsWith("- ")){current=new HelpBlock{Kind=HelpBlockKind.Bullet,Text=line.Substring(2).Trim()};blocks.Add(current);}
    else if(number.Success){current=new HelpBlock{Kind=HelpBlockKind.Numbered,Number=Math.Max(1,int.Parse(number.Groups[1].Value,CultureInfo.InvariantCulture)),Text=number.Groups[2].Value};blocks.Add(current);}
    else if(callout.Success&&callouts.Contains(callout.Groups[1].Value)){current=new HelpBlock{Kind=HelpBlockKind.Callout,Label=callout.Groups[1].Value,Text=callout.Groups[2].Value};blocks.Add(current);}
    else if(current!=null&&(current.Kind==HelpBlockKind.Paragraph||char.IsWhiteSpace(raw[0])))current.Text+=" "+line;
    else{current=new HelpBlock{Kind=HelpBlockKind.Paragraph,Text=line};blocks.Add(current);}
   }return blocks;
  }
  // Inline markup: **bold**, `keys`, and [label](topic:KEY) or [label](walkthrough:id) links.
  public static IEnumerable<Match> Inlines(string text){return inline.Matches(text??"").Cast<Match>();}
  public static string StripInline(string text){return inline.Replace(text??"",m=>m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Success?m.Groups[2].Value:m.Groups[3].Value);}
  public static IEnumerable<string> Links(string body){return Inlines(body).Where(m=>m.Groups[4].Success).Select(m=>m.Groups[4].Value);}
  public static string PlainText(string body){return string.Join("\r\n\r\n",Blocks(body).Select(b=>(b.Kind==HelpBlockKind.Bullet?"• ":b.Kind==HelpBlockKind.Numbered?b.Number+". ":b.Kind==HelpBlockKind.Callout?b.Label+": ":"")+StripInline(b.Text)));}
  public static string PlainText(HelpTopic topic){return topic.Title+"\r\n"+(string.IsNullOrEmpty(topic.Summary)?"":topic.Summary+"\r\n\r\n")+PlainText(topic.Body);}
 }
}
