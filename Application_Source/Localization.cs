// Interface translations keyed by the English text. English is the source and the
// fallback: a missing translation shows the original, never an empty label.
// Tables are embedded UTF-8 files (lang-<code>.txt): "English<TAB>Translation" per
// line, with \n and \t escapes and # comments. "{0}"-style keys translate composed
// messages; their values are inserted unchanged.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public sealed class LanguageChoice {public string Code,Name;public override string ToString(){return Name;}}
 public static class Localization {
  public const string SystemDefault="System";
  // Native names, so a reader can find their language whatever is showing.
  public static readonly LanguageChoice[] Languages={
   new LanguageChoice{Code="en",Name="English"},new LanguageChoice{Code="de",Name="Deutsch"},new LanguageChoice{Code="es",Name="Español"},new LanguageChoice{Code="fr",Name="Français"},
   new LanguageChoice{Code="it",Name="Italiano"},new LanguageChoice{Code="ja",Name="日本語"},new LanguageChoice{Code="zh-Hans",Name="简体中文"}
  };
  sealed class Pattern {public Regex Match;public string Replacement;public int[] Order;}
  static Dictionary<string,string> table=new Dictionary<string,string>(StringComparer.Ordinal);static List<Pattern> patterns=new List<Pattern>();
  static string current="en";
  public static string Current{get{return current;}}
  public static bool English{get{return current=="en";}}
  public static int Count{get{return table.Count+patterns.Count;}}
  // "System" (or nothing) follows Windows; an unsupported language falls back to English.
  public static string Resolve(string setting,CultureInfo culture){
   string code=setting;
   if(string.IsNullOrEmpty(code)||code==SystemDefault){
    var ui=culture??CultureInfo.CurrentUICulture;string name=ui.Name,two=ui.TwoLetterISOLanguageName;
    if(two=="zh")return name.StartsWith("zh-Hant",StringComparison.OrdinalIgnoreCase)||name=="zh-TW"||name=="zh-HK"||name=="zh-MO"?"en":"zh-Hans";
    code=two;
   }
   return Languages.Any(l=>l.Code==code)||code=="qps"?code:"en";
  }
  public static void Use(string setting,CultureInfo culture=null){
   string code=Resolve(setting,culture);var loaded=new Dictionary<string,string>(StringComparer.Ordinal);var loadedPatterns=new List<Pattern>();
   if(code!="en"&&code!="qps"){
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("lang-"+code+".txt")){
     if(stream==null)code="en";else using(var reader=new StreamReader(stream,Encoding.UTF8))Parse(reader.ReadToEnd(),loaded,loadedPatterns);
    }
   }
   table=loaded;patterns=loadedPatterns;current=code;
  }
  public static void UseTable(string code,string text){var exact=new Dictionary<string,string>(StringComparer.Ordinal);var templates=new List<Pattern>();Parse(text??"",exact,templates);table=exact;patterns=templates;current=code;}
  // Pseudo-localisation for checking coverage: every translatable string is bracketed.
  public static bool Pseudo{get{return current=="qps";}}
  // One pass, so an escaped backslash before "n" stays a backslash.
  static string Unescape(string text){
   if(text.IndexOf('\\')<0)return text;var result=new StringBuilder(text.Length);
   for(int i=0;i<text.Length;i++){char c=text[i],next=i+1<text.Length?text[i+1]:'\0';if(c=='\\'&&(next=='n'||next=='t'||next=='\\')){result.Append(next=='n'?'\n':next=='t'?'\t':'\\');i++;}else result.Append(c);}
   return result.ToString();
  }
  static void Parse(string text,Dictionary<string,string> exact,List<Pattern> templates){
   foreach(string raw in text.Split('\n')){
    string line=raw.TrimEnd('\r');if(line.Length==0||line.StartsWith("#"))continue;int tab=line.IndexOf('\t');if(tab<=0)continue;
    string key=Unescape(line.Substring(0,tab)),value=Unescape(line.Substring(tab+1));if(value.Length==0)continue;
    if(Regex.IsMatch(key,@"\{#?\d\}")){var pattern=Compile(key,value);if(pattern!=null)templates.Add(pattern);}else exact[key]=value;
   }
  }
  static Pattern Compile(string key,string value){
   var order=new List<int>();var builder=new StringBuilder("^");int last=0;
   // {#0} only matches a number, so count patterns never capture ordinary words.
   foreach(Match m in Regex.Matches(key,@"\{(#?)(\d)\}")){builder.Append(Regex.Escape(key.Substring(last,m.Index-last)));builder.Append(m.Groups[1].Value=="#"?@"([0-9][0-9,.\u00A0\u202F]*)":"(.+?)");order.Add(int.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture));last=m.Index+m.Length;}
   builder.Append(Regex.Escape(key.Substring(last))).Append("$");
   // Every placeholder in the translation must exist in the English text.
   foreach(Match m in Regex.Matches(value,@"\{(\d)\}"))if(!order.Contains(int.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)))return null;
   return new Pattern{Match=new Regex(builder.ToString(),RegexOptions.Singleline|RegexOptions.CultureInvariant),Replacement=value,Order=order.ToArray()};
  }
  static readonly string[] separators={"\n","  ·  "," · "};
  static readonly Regex sentences=new Regex(@"(?<=[.!?…])\s+(?=[A-Z0-9“""(])",RegexOptions.CultureInvariant);
  public static string T(string english){return Translate(english,0);}
  static string Translate(string english,int depth){
   if(string.IsNullOrEmpty(english)||English)return english;if(Pseudo)return english.Any(char.IsLetter)?"［"+english+"］":english;
   string whole=Whole(english);if(whole!=null||depth>3)return whole??english;
   // Composed status lines: translate each line, " · " part or sentence on its own.
   foreach(string separator in separators){if(english.IndexOf(separator,StringComparison.Ordinal)<0)continue;var parts=english.Split(new[]{separator},StringSplitOptions.None);if(parts.Length<2)continue;return string.Join(separator,parts.Select(part=>Translate(part,depth+1)));}
   var pieces=sentences.Split(english);if(pieces.Length>1){var spaces=sentences.Matches(english).Cast<Match>().Select(m=>m.Value).ToList();var result=new StringBuilder();for(int i=0;i<pieces.Length;i++){result.Append(Translate(pieces[i],depth+1));if(i<spaces.Count)result.Append(spaces[i]);}return result.ToString();}
   return english;
  }
  static string Whole(string english){
   string value;if(table.TryGetValue(english,out value))return value;
   // Keep surrounding whitespace and a trailing colon; translate the core text.
   string trimmed=english.Trim();if(trimmed.Length!=english.Length&&trimmed.Length>0&&table.TryGetValue(trimmed,out value))return english.Substring(0,english.IndexOf(trimmed[0]))+value+english.Substring(english.IndexOf(trimmed[0])+trimmed.Length);
   if(trimmed.Length>1&&trimmed.EndsWith(":")&&table.TryGetValue(trimmed.Substring(0,trimmed.Length-1),out value)){int start=english.IndexOf(trimmed[0]);return english.Substring(0,start)+value+(current=="ja"||current=="zh-Hans"?"：":":")+english.Substring(start+trimmed.Length);}
   foreach(var pattern in patterns){
    var m=pattern.Match.Match(english);if(!m.Success)continue;string result=pattern.Replacement;
    for(int i=0;i<pattern.Order.Length;i++){string part=m.Groups[i+1].Value;string translated;result=result.Replace("{"+pattern.Order[i]+"}",table.TryGetValue(part,out translated)?translated:part);}
    return result;
   }
   return null;
  }
  // Menu and button labels mark their access key with "_". The translation keeps the
  // same key where its letter appears; Japanese and Chinese add it as "(_I)", as Windows does.
  public static string AccessText(string english){
   string shown=T(english);if(shown!=english||string.IsNullOrEmpty(english))return shown;
   int mark=english.IndexOf('_');if(mark<0||mark==english.Length-1||english[mark+1]=='_')return english;
   char key=char.ToUpperInvariant(english[mark+1]);string plain=english.Remove(mark,1),translated=T(plain);if(translated==plain)return english;
   translated=translated.Replace("_","__");
   if(current=="ja"||current=="zh-Hans")return translated+"(_"+key+")";
   for(int i=0;i<translated.Length;i++)if(char.ToUpperInvariant(translated[i])==key)return translated.Insert(i,"_");
   return "_"+translated;
  }
  public static bool Has(string english){string value;return !English&&(table.TryGetValue(english,out value)||patterns.Any(p=>p.Match.IsMatch(english)));}
  public static string F(string english,params object[] args){return string.Format(CultureInfo.CurrentCulture,T(english),args);}
 }
}
