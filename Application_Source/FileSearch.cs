using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AstroArchive {
 // One parsed query per refresh; text and fields are normalized once per row.
 public sealed class SearchDocument {
  public string Text,Target;
  public readonly Dictionary<string,string> Fields=new Dictionary<string,string>();
  public readonly Dictionary<string,double?> Numbers=new Dictionary<string,double?>();
  readonly Dictionary<string,string> folded=new Dictionary<string,string>();
  string canonical,canonicalSource;
  public string CanonicalTarget{get{if(canonical==null||canonicalSource!=Target){canonicalSource=Target;canonical=Catalog.CanonicalTarget(Target);}return canonical;}}
  public string Value(string field){string value;return field.Length==0?Text:Fields.TryGetValue(field,out value)?value:"";}
  public string Folded(string field){string value;if(!folded.TryGetValue(field,out value))folded[field]=value=FileSearch.Fold(Value(field));return value;}
  public static SearchDocument FromFrame(Frame f){
   var d=new SearchDocument{Text=f.SearchText+" "+f.SourcePath+" "+f.RelativePath+" "+f.Hash,Target=f.Target};
   d.Fields["target"]=f.TargetLabel+" "+Catalog.Aliases(f.Target);d.Fields["file"]=f.OriginalName;d.Fields["path"]=f.SourcePath+" "+f.RelativePath;
   d.Fields["device"]=f.Telescope+" "+f.InstrumentText+" "+f.TelescopeModel;d.Fields["camera"]=f.Camera+" "+f.CameraModel+" "+f.CameraId;
   d.Fields["filter"]=f.Filter;d.Fields["type"]=f.Kind;d.Fields["format"]=f.Format;d.Fields["date"]=f.AcquisitionDate+" "+f.AcquisitionDateLabel+" "+f.Night+" "+f.Observed;
   d.Fields["mount"]=f.MountText;d.Fields["notes"]=f.Notes;d.Fields["status"]=f.Status;d.Fields["review"]=f.ReviewText+" "+f.ReviewCategory+" "+f.ReviewReason;
   d.Fields["session"]=f.Session+" "+f.SessionKey;d.Fields["hash"]=f.Hash;
   d.Numbers["exposure"]=CaptureFilters.Number(f,"Exposure");d.Numbers["gain"]=CaptureFilters.Number(f,"Gain");d.Numbers["temperature"]=f.Temperature;return d;
  }
 }
 public sealed class FileSearch {
  sealed class Token{public string Text;public bool Quoted;}
  sealed class Term {
   public string Field="",Text,Known,Folded,Comparison;public double Number;public bool Exclude;public Regex Glob;
   public bool Matches(SearchDocument document){
    bool match;
    if(Comparison!=null){double? n;match=document.Numbers.TryGetValue(Field,out n)&&n.HasValue&&!double.IsNaN(n.Value)&&!double.IsInfinity(n.Value)&&(Comparison==">"?n.Value>Number:Comparison==">="?n.Value>=Number:Comparison=="<"?n.Value<Number:Comparison=="<="?n.Value<=Number:n.Value==Number);}
    else if(Known!=null&&(Field.Length==0||Field=="target")&&document.CanonicalTarget==Known)match=true;
    else if(Glob!=null){try{match=Glob.IsMatch(document.Value(Field)??"");}catch(RegexMatchTimeoutException){match=false;}}
    else match=(document.Value(Field)??"").IndexOf(Text,StringComparison.OrdinalIgnoreCase)>=0||Folded.Length>0&&Text.IndexOf('?')<0&&document.Folded(Field).IndexOf(Folded,StringComparison.Ordinal)>=0;
    return Exclude?!match:match;
   }
  }
  static readonly HashSet<string> fields=new HashSet<string>(new[]{"target","file","path","device","camera","filter","type","format","date","mount","notes","status","review","session","hash","project","source","exposure","gain","temperature"});
  static readonly Regex numeric=new Regex(@"^(exposure|gain|temperature)(?::)?(>=|<=|>|<|=)([-+]?\d+(?:\.\d+)?)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  readonly List<List<Term>> groups=new List<List<Term>>();
  public string Error{get;private set;}
  public bool IsEmpty{get;private set;}
  public bool Matches(Frame frame){return IsEmpty||Matches(SearchDocument.FromFrame(frame));}
  public bool Matches(SearchDocument document){return Error==null&&(IsEmpty||groups.Any(g=>g.All(t=>t.Matches(document))));}
  public static FileSearch Parse(string text){var result=new FileSearch();result.Read(text??"");return result;}
  static List<Token> Tokens(string input){
   var tokens=new List<Token>();var text=new StringBuilder();bool quote=false,quoted=false;
   for(int i=0;i<input.Length;i++){
    char c=input[i];if(c=='\\'&&quote&&i+1<input.Length&&input[i+1]=='"'){text.Append('"');i++;continue;}
    if(c=='"'){quote=!quote;quoted=true;continue;}
    if(!quote&&(char.IsWhiteSpace(c)||c=='|')){if(text.Length>0){tokens.Add(new Token{Text=text.ToString(),Quoted=quoted});text.Clear();quoted=false;}if(c=='|')tokens.Add(new Token{Text="OR"});}
    else text.Append(c);
   }
   if(quote)throw new FormatException("Close the quoted search phrase.");
   if(text.Length>0)tokens.Add(new Token{Text=text.ToString(),Quoted=quoted});return tokens;
  }
  void Read(string input){
   try{
    if(input.Length>4096)throw new FormatException("Keep the search below 4096 characters.");
    var tokens=Tokens(input);IsEmpty=tokens.Count==0;if(IsEmpty)return;if(tokens.Count>128)throw new FormatException("Use fewer search terms.");
    var group=new List<Term>();groups.Add(group);
    for(int i=0;i<tokens.Count;i++){
     var token=tokens[i];if(!token.Quoted&&token.Text=="OR"){if(group.Count==0)throw new FormatException("Add a search term before OR.");groups.Add(group=new List<Term>());continue;}
     var term=new Term();string value=token.Text;
     if(value.StartsWith("-",StringComparison.Ordinal)&&value.Length>1){term.Exclude=true;value=value.Substring(1);}
     var number=numeric.Match(value);
     if(number.Success){term.Field=number.Groups[1].Value.ToLowerInvariant();term.Comparison=number.Groups[2].Value;if(!double.TryParse(number.Groups[3].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out term.Number)||double.IsInfinity(term.Number))throw new FormatException("Use a finite numeric value.");group.Add(term);continue;}
     if(Regex.IsMatch(value,@"^(exposure|gain|temperature)(?::)?[<>=]",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))throw new FormatException("Complete the numeric comparison, for example exposure:>=20.");
     int colon=value.IndexOf(':');if(colon>0&&fields.Contains(value.Substring(0,colon).ToLowerInvariant())){term.Field=value.Substring(0,colon).ToLowerInvariant();value=value.Substring(colon+1);if(value.Length==0)throw new FormatException("Add a value after "+term.Field+":.");}
     if(new[]{"exposure","gain","temperature"}.Contains(term.Field)){
      double n;if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||double.IsNaN(n)||double.IsInfinity(n))throw new FormatException("Use a number or comparison for "+term.Field+".");term.Comparison="=";term.Number=n;group.Add(term);continue;
     }
     if(!token.Quoted&&(term.Field.Length==0||term.Field=="target")){
      string phrase=value;int consumed=0;
      for(int j=i+1;j<tokens.Count&&j<=i+5;j++){
       var next=tokens[j];if(next.Quoted||next.Text=="OR"||next.Text.StartsWith("-",StringComparison.Ordinal)||next.Text.Contains(":"))break;
       phrase+=" "+next.Text;if(Catalog.KnownName(phrase)!=null){value=phrase;consumed=j-i;}
      }
      i+=consumed;
     }
     if(value.All(c=>c=='-'))throw new FormatException("Add a term after the exclusion sign.");
     term.Text=value;term.Known=Catalog.KnownName(value);term.Folded=Fold(value);
     if(value.IndexOf('*')>=0||(term.Field=="file"||term.Field=="path")&&value.IndexOf('?')>=0)term.Glob=new Regex((term.Field=="file"?"^":"")+Regex.Escape(value).Replace("\\*",".*").Replace("\\?",".")+(term.Field=="file"?"$":""),RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
     group.Add(term);
    }
    if(group.Count==0)throw new FormatException("Add a search term after OR.");
   }catch(FormatException e){Error=e.Message;IsEmpty=false;groups.Clear();}
  }
  internal static string Fold(string text){
   var result=new StringBuilder();bool space=false;
   foreach(char c in (text??"").Normalize(NormalizationForm.FormD)){
    if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;
    if(char.IsLetterOrDigit(c)){if(space&&result.Length>0)result.Append(' ');result.Append(char.ToUpperInvariant(c));space=false;}else space=true;
   }
   return result.ToString();
  }
 }
}
