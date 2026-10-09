// Metadata rules are conservative: missing information remains Unknown.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AstroArchive {
 public class CatalogObject {
  public string Name; public string Common; public string Aliases; public string Type; public double RA; public double Dec; public double Diameter; public double? Magnitude;
  public string Label {get{return Name+(string.IsNullOrEmpty(Common)?"":"  ·  "+Common);}}
 }
 public class Candidate {public string Name{get;set;} public string Common{get;set;} public double Separation{get;set;} public string Label{get{return Name+(string.IsNullOrWhiteSpace(Common)?"":" · "+Common);}} public string DistanceText{get{return Separation.ToString("0.000",CultureInfo.InvariantCulture)+"° from centre";}} }
 public static partial class Catalog {
  public static List<CatalogObject> Objects=new List<CatalogObject>(); static Dictionary<string,string> aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);static Dictionary<string,string> descriptions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);static HashSet<string> ambiguous=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  static Dictionary<string,string> commonNames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  static List<KeyValuePair<string,string>> phrases=new List<KeyValuePair<string,string>>();
  static List<KeyValuePair<Regex,string>> filenamePatterns=new List<KeyValuePair<Regex,string>>();
  static Dictionary<string,List<KeyValuePair<Regex,string>>> phraseIndex=new Dictionary<string,List<KeyValuePair<Regex,string>>>(StringComparer.Ordinal);
  static Catalog(){
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("catalog.csv"))using(var r=new StreamReader(s)) {
    r.ReadLine();string line;while((line=r.ReadLine())!=null){string[] a=line.Split(';');if(a.Length<9)continue;double? ra=Sex(a[2],true),dec=Sex(a[3],false);if(!ra.HasValue||!dec.HasValue)continue;
     string name=CompactId(a[0]);if(!string.IsNullOrEmpty(a[6]))name="M"+a[6].TrimStart('0');
     double d,mag;var o=new CatalogObject{Name=name,Common=a[7].Split(',')[0],Aliases=a[8],Type=a[1],RA=ra.Value,Dec=dec.Value,Diameter=double.TryParse(a[4],NumberStyles.Float,CultureInfo.InvariantCulture,out d)?d/60:0,Magnitude=double.TryParse(a[5],NumberStyles.Float,CultureInfo.InvariantCulture,out mag)?(double?)mag:null};Objects.Add(o);if(!string.IsNullOrWhiteSpace(o.Common))commonNames[name]=o.Common;
     AddAlias(a[0],name);AddAlias(name,name);
     foreach(string t in a[7].Split(','))AddAlias(t,name);foreach(string t in a[8].Split(','))if(Regex.IsMatch(t.Trim(),@"^(M|NGC|IC|C|SH\s*2|B|UGC|PGC)\s*\d",RegexOptions.IgnoreCase)){AddAlias(t,name);string compact=CompactId(t);if(Regex.IsMatch(compact,@"^C\d+$")){AddAlias(compact,name);AddAlias("Caldwell "+compact.Substring(1),name);}}
    }
   }
   AddAlias("Sun","Sun");AddAlias("Solar","Sun");
   AddAlias("Moon","Moon");AddAlias("Lunar","Moon");
   AddAlias("Pleiades","M45");AddAlias("Andromeda","M31");AddAlias("Triangulum","M33");AddAlias("Pacman","NGC281");AddAlias("Pacman Nebula","NGC281");AddAlias("Wizard","NGC7380");AddAlias("Wizard Nebula","NGC7380");AddAlias("Crescent","NGC6888");AddAlias("Crescent Nebula","NGC6888");AddAlias("Hidden Galaxy","IC342");AddAlias("Orion Nebula","M42");
   AddAlias("Heart Nebula","IC1805");AddAlias("Soul Nebula","IC1848");AddAlias("Elephant's Trunk Nebula","IC1396");AddAlias("Elephant Trunk Nebula","IC1396");
   foreach(var entry in new[]{new[]{"M45","Pleiades"},new[]{"M31","Andromeda Galaxy"},new[]{"M33","Triangulum Galaxy"},new[]{"NGC281","Pacman Nebula"},new[]{"NGC7380","Wizard Nebula"},new[]{"NGC6888","Crescent Nebula"},new[]{"IC342","Hidden Galaxy"}})if(!commonNames.ContainsKey(entry[0]))commonNames[entry[0]]=entry[1];
   commonNames["IC1805"]="Heart Nebula";commonNames["IC1848"]="Soul Nebula";commonNames["IC1396"]="Elephant’s Trunk Nebula";
   foreach(string row in CatalogNames.Entries){var parts=row.Split('|');string id=CanonicalTarget(parts[0]);commonNames[id]=parts[1];AddAlias(id,id);AddAlias(parts[1],id);foreach(string alias in parts[2].Split(';'))AddAlias(alias,id);}
   foreach(string label in CatalogNames.AmbiguousNames){string key=Key(label);aliases.Remove(key);ambiguous.Add(key);}
   // A display name must itself resolve to the displayed identity. Qualify
   // shared component/pair names instead of silently choosing another object.
   foreach(var entry in commonNames.ToArray())if(KnownName(entry.Value)!=entry.Key){string label=entry.Value+" ("+entry.Key+")";commonNames[entry.Key]=label;AddAlias(label,entry.Key);}
   IndexCatalogueIds();
   foreach(var item in Objects){string common;if(commonNames.TryGetValue(item.Name,out common))item.Common=common;item.Aliases=Aliases(item.Name);}
   foreach(var phrase in phrases.GroupBy(p=>p.Key).Select(g=>g.First())){if(KnownName(phrase.Key)!=phrase.Value)continue;string pattern=@"\b"+string.Join(@"\s*",phrase.Key.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape))+@"\b";var entry=new KeyValuePair<Regex,string>(new Regex(pattern,RegexOptions.CultureInvariant),phrase.Value);filenamePatterns.Add(entry);string token="";foreach(string word in phrase.Key.Split(' ')){token+=word;List<KeyValuePair<Regex,string>> list;if(!phraseIndex.TryGetValue(token,out list))phraseIndex[token]=list=new List<KeyValuePair<Regex,string>>();list.Add(entry);}}
  }
  static void AddAlias(string a,string name){if(string.IsNullOrWhiteSpace(a))return;string owner;if(CatalogNames.NameOwners.TryGetValue(a.Trim(),out owner)&&owner!=name)return;string label;if(!descriptions.TryGetValue(name,out label))label="";descriptions[name]=label+" "+a+" "+CompactId(a);string key=Key(a);if(key.Length==0||ambiguous.Contains(key))return;string existing;if(aliases.TryGetValue(key,out existing)&&existing!=name){aliases.Remove(key);ambiguous.Add(key);}else aliases[key]=name;string words=FileSearch.Fold(a);if(words.Length>=4&&!Regex.IsMatch(key,@"^(M|NGC|IC|C|B|SH2|UGC|PGC)\d+[A-Z]?$")&&words.Any(char.IsLetter))phrases.Add(new KeyValuePair<string,string>(words,name));}
  public static string KnownName(string name){string value;string key=Key(name);return savedNames.Aliases.TryGetValue(key,out value)||aliases.TryGetValue(key,out value)?value:ObservationTargets.NamedSolar(name);}
  static HashSet<string> FilenameTargets(string filename,bool includeUnknown=false){
   string stem=Regex.Replace(Path.GetFileName(filename??""),@"\.(fit|fits|fts)(\.gz)?$","",RegexOptions.IgnoreCase);
   string text=FileSearch.Fold(stem);var found=new HashSet<string>();

   // Prefer a complete common name over a shorter name contained inside it:
   // Southern Pleiades is IC2602, while an independent Pleiades mention is M45.
   var matches=new List<Tuple<int,int,string>>();
   foreach(string token in text.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Distinct()){List<KeyValuePair<Regex,string>> list;if(phraseIndex.TryGetValue(token,out list))foreach(var phrase in list)foreach(Match match in phrase.Key.Matches(text))matches.Add(Tuple.Create(match.Index,match.Length,phrase.Value));}
   foreach(var phrase in savedNames.Phrases)foreach(Match match in phrase.Key.Matches(text))matches.Add(Tuple.Create(match.Index,match.Length,phrase.Value));
   foreach(var match in matches)if(!matches.Any(other=>other.Item2>match.Item2&&other.Item1<=match.Item1&&other.Item1+other.Item2>=match.Item1+match.Item2))found.Add(match.Item3);
   foreach(Match match in Regex.Matches(text,@"\b(MESSIER|M|NGC|IC|CALDWELL|C|BARNARD|B|UGC|PGC|SHARPLESS|SH\s*2)\s*(?:(?:NO|NUMBER)\s*)?0*(\d+)([A-Z]?)\b")){if(matches.Any(label=>match.Index>=label.Item1&&match.Index<label.Item1+label.Item2))continue;string mentioned=CataloguePrefix(match.Groups[1].Value)+match.Groups[2].Value+match.Groups[3].Value;string id=KnownName(mentioned);if(id!=null||includeUnknown)found.Add(id??mentioned);}
   if(Regex.IsMatch(text,@"\bSUN\b"))found.Add("Sun");
   return found;
  }
  public static string TargetFromFilename(string filename){if(HasFilenameConflict(filename))return null;var found=FilenameTargets(filename);if(found.Count==1)return found.First();return CometTargets.FromFilename(filename)??(FilenameTargets(filename,true).Count==0?ObservationTargets.SolarFromFilename(filename):null);}
  public static bool HasFilenameConflict(string filename){return FilenameTargets(filename,true).Count>1;}
  public static string Aliases(string target){string s,custom;string id=CanonicalTarget(target);return (descriptions.TryGetValue(id,out s)?s:"")+" "+(savedNames.Descriptions.TryGetValue(id,out custom)?custom:"");}
  // Resolve recognised labels on deserialization too, so old archives share new groups.
  // Preserve custom and conflicting labels rather than inventing an identity.
  public static string CanonicalTarget(string target){
   string text=(target??"").Trim();if(text.Length==0)return "Unknown";
   if(text.Equals("Calibration",StringComparison.OrdinalIgnoreCase))return "Calibration";
   string known=KnownName(text);if(known!=null)return known;
   if(HasFilenameConflict(text))return text.Replace('_',' ');var found=FilenameTargets(text);if(found.Count==1)return found.First();
   string comet=CometTargets.FromLabel(text);if(comet!=null)return comet;
   if(FilenameTargets(text,true).Count==0){string solar=ObservationTargets.SolarFromFilename(text);if(solar!=null)return solar;}
   return text.Replace('_',' ');
  }
  public static string ObjectId(string target){string id=CanonicalTarget(target);return !IsAmbiguous(id)&&(KnownName(id)!=null||Regex.IsMatch(id,@"^(M|NGC|IC|C|B|SH2|UGC|PGC)\d+[A-Z]?$",RegexOptions.IgnoreCase))?id:"";}
  public static string CommonName(string target){string name;string id=CanonicalTarget(target);return savedNames.Common.TryGetValue(id,out name)||commonNames.TryGetValue(id,out name)?name:"";}
  public static string Label(string target){string id=CanonicalTarget(target),common=CommonName(id);return id+(common.Length>0?" · "+common:"");}
  static string Key(string s){return NameKey(s);}
  public static string CompactId(string s){var m=Regex.Match(FileSearch.Fold(s),@"^(NGC|IC|MESSIER|M|CALDWELL|C|BARNARD|B|UGC|PGC|SHARPLESS|SH\s*2)\s*(?:(?:NO|NUMBER)\s*)?0*(\d+)(.*)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);return m.Success?CataloguePrefix(m.Groups[1].Value)+m.Groups[2].Value+m.Groups[3].Value:s;}
  static string CataloguePrefix(string prefix){prefix=prefix.ToUpperInvariant().Replace(" ","");return prefix=="MESSIER"?"M":prefix=="CALDWELL"?"C":prefix=="BARNARD"?"B":prefix=="SHARPLESS"?"SH2":prefix;}
  public static double? Sex(string s,bool hours){
   string[] p=Regex.Split((s??"").Trim().Replace("h",":").Replace("m",":").Replace("s",""),@"[:\s]+");double a,b,c;if(p.Length<2||!double.TryParse(p[0],NumberStyles.Float,CultureInfo.InvariantCulture,out a)||!double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out b))return null;c=0;if(p.Length>2&&!double.TryParse(p[2],NumberStyles.Float,CultureInfo.InvariantCulture,out c))return null;
   double v=(Math.Abs(a)+b/60+c/3600)*(s.TrimStart().StartsWith("-")?-1:1);return hours?v*15:v;
  }
  public static string Normalize(string s){
   s=(s??"").Trim().Trim('_','-');if(IsAmbiguous(s))return "Unknown";if(HasFilenameConflict(s))return s.Replace('_',' ');s=CanonicalTarget(s);string val;if(aliases.TryGetValue(Key(s),out val))return val;if(CometTargets.IsComet(s))return s;
   var m=Regex.Match(s,@"\b(MESSIER|M|NGC|IC|CALDWELL|C|BARNARD|B|UGC|PGC)\s*[_-]?\s*0*(\d+)([A-Z]?)\b",RegexOptions.IgnoreCase);if(m.Success){string id=CataloguePrefix(m.Groups[1].Value)+m.Groups[2].Value+m.Groups[3].Value.ToUpperInvariant();return aliases.TryGetValue(Key(id),out val)?val:id;}
   return s.Replace('_',' ').Trim();
  }
  public static bool IsAmbiguous(string s){return string.IsNullOrWhiteSpace(s)||ambiguous.Contains(Key(s))||Regex.IsMatch(s.Trim(),@"^(unknown|unnamed|none|n/?a|target|object|sky|test|light|raw|image|frame|manual|custom|calibration|\d+)([_\s-]*\d*)$",RegexOptions.IgnoreCase);}
  public static double Distance(double ra,double dec,double ra2,double dec2){double k=Math.PI/180;double x=Math.Sin((dec2-dec)*k/2),y=Math.Sin((ra2-ra)*k/2);double h=x*x+Math.Cos(dec*k)*Math.Cos(dec2*k)*y*y;return 2*Math.Asin(Math.Sqrt(Math.Min(1,h)))/k;}
  public static List<Candidate> Nearby(double ra,double dec,double radius){return Objects.Select(o=>new{Object=o,Separation=Distance(ra,dec,o.RA,o.Dec)}).Where(c=>c.Separation<=radius).OrderBy(c=>c.Separation).GroupBy(c=>c.Object.Name).Select(g=>g.First()).Take(30).Select(c=>new Candidate{Name=c.Object.Name,Common=CommonName(c.Object.Name),Separation=c.Separation}).ToList();}
  public static List<CatalogObject> Search(string q){
   string v=Key(q),known=KnownName(q);var custom=savedNames;
   var matches=known!=null?Objects.Where(o=>o.Name==known):Objects.Where(o=>{string common,extra;return Key(o.Name+" "+(custom.Common.TryGetValue(o.Name,out common)?common:o.Common)+" "+o.Aliases+" "+(custom.Descriptions.TryGetValue(o.Name,out extra)?extra:"")).Contains(v);});
   return matches.GroupBy(o=>o.Name).Select(g=>g.First()).Take(50).Select(o=>new CatalogObject{Name=o.Name,Common=CommonName(o.Name),Aliases=Aliases(o.Name),Type=o.Type,RA=o.RA,Dec=o.Dec,Diameter=o.Diameter,Magnitude=o.Magnitude}).ToList();
  }
 }
 public static class Classifier {
  public sealed class ShotsMetadata {public Dictionary<string,object> Raw;public Dictionary<string,string> Values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);public string Path;public string Note;public FileStamp Stamp;}
  static double? MatchNumber(string text,string pattern){var m=Regex.Match(text,pattern,RegexOptions.IgnoreCase);double d;return m.Success&&double.TryParse(m.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out d)?(double?)d:null;}
  public static bool FilenameExposureGain(string filename,out double? exposure,out double? gain){
   exposure=gain=null;var matches=Regex.Matches(Path.GetFileName(filename??""),@"(?:^|[_ -])(\d+(?:\.\d+)?)s(\d+)(?=[_. -]|$)",RegexOptions.IgnoreCase);double seconds,value;
   if(matches.Count!=1)return false;var match=matches[0];
   if(!double.TryParse(match.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out seconds)||seconds<=0||double.IsInfinity(seconds)||!double.TryParse(match.Groups[2].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||double.IsInfinity(value))return false;
   exposure=seconds;gain=value;return true;
  }
  static string CleanStem(string s){return Regex.Replace(s,@"\.(fit|fits|fts)(\.gz)?$","",RegexOptions.IgnoreCase);}
  // Seestar records a real sub count before the target, and seconds per sub after it.
  public static bool SeestarStackFilename(string filename,out int count,out double seconds){
   count=0;seconds=0;var match=Regex.Match(Path.GetFileName(filename??""),@"^Stacked_(\d+)_.+?_(\d+(?:\.\d+)?)s(?=[_. -]|$)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
   return match.Success&&int.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out count)&&count>0&&double.TryParse(match.Groups[2].Value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out seconds)&&seconds>0&&!double.IsInfinity(seconds)&&!double.IsInfinity(count*seconds);
  }
  public static bool DwarfStackCount(string filename,FitsHeader header,string make,out int count,out double total,out double seconds){
   count=0;total=seconds=0;double? sub,gain;
   if(make!="DWARFLAB"||!Regex.IsMatch(Path.GetFileName(filename??""),@"(?:^|[_-])stacked-(?:16|32)_",RegexOptions.IgnoreCase)||!FilenameExposureGain(filename,out sub,out gain))return false;
   // Device stack EXPTIME is integration; the filename carries exposure per sub.
   if(!string.IsNullOrWhiteSpace(header.Get("NCOMBINE","STACKCNT","NSTACK","STACKNUM","NSUBS","SUBCOUNT")))return false;
   double? explicitSub=header.Number("SUBEXP","SUBEXPT","EXPOSUB","EXP_SUB","SUBTIME");if(explicitSub.HasValue&&Math.Abs(explicitSub.Value-sub.Value)>0.000001)return false;
   double? integration=header.Number("TOTALEXP","TOTEXP","EXPTOTAL","INTTIME","INTEGRAT");
   string comment;header.Comments.TryGetValue("EXPTIME",out comment);
   if(!integration.HasValue){if(Regex.IsMatch(comment??"",@"per[ _-]?(sub|frame)|individual|single|milliseconds?|\[ms\]",RegexOptions.IgnoreCase))return false;integration=header.Number("EXPTIME");}
   if(!integration.HasValue||integration.Value<=0||double.IsNaN(integration.Value)||double.IsInfinity(integration.Value))return false;
   double ratio=integration.Value/sub.Value,rounded=Math.Round(ratio);if(rounded<1||rounded>int.MaxValue||Math.Abs(ratio-rounded)>0.000001)return false;
   count=(int)rounded;total=integration.Value;seconds=sub.Value;return true;
  }
  public static bool ApplyDwarfStackCount(Frame frame){
   if(frame.Kind!="Stack"||frame.StackCount>0||UserMetadata(frame,"StackCount")||UserMetadata(frame,"Exposure"))return false;
   var image=frame.Images==null?null:frame.Images.FirstOrDefault(i=>i.Key==frame.ImageKey)??frame.Images.FirstOrDefault();if(image==null||image.Headers==null)return false;
   var header=new FitsHeader{Values=image.Headers,Comments=image.Comments??new Dictionary<string,string>()};int count;double total,seconds;
   if(!DwarfStackCount(frame.OriginalName,header,frame.MakeText,out count,out total,out seconds))return false;
   frame.StackCount=count;if(frame.Facts==null)frame.Facts=new Dictionary<string,MetadataFact>();
   StackFact(frame,"StackCount",new MetadataFact{Value=count.ToString(CultureInfo.InvariantCulture),Raw=frame.OriginalName,Source="DWARF stack integration: "+Util.Num(total)+" s total / "+Util.Num(seconds)+" s per sub"});return true;
  }
  public static bool ApplyStackMetadata(Frame frame){return ApplySeestarStackExposure(frame)|ApplyDwarfStackCount(frame);}
  static bool UserMetadata(Frame frame,string field){MetadataFact fact;return frame.Facts!=null&&frame.Facts.TryGetValue(field,out fact)&&fact!=null&&fact.Source=="User";}
  static bool StackFact(Frame frame,string field,MetadataFact fact){MetadataFact old;if(frame.Facts.TryGetValue(field,out old)&&old!=null&&old.Value==fact.Value&&old.Raw==fact.Raw&&old.Source==fact.Source&&old.Unit==fact.Unit)return false;frame.Facts[field]=fact;return true;}
  public static bool ApplySeestarStackExposure(Frame frame){
   int count;double seconds;if(frame.Kind!="Stack"||frame.MakeText!="Seestar"||!SeestarStackFilename(frame.OriginalName,out count,out seconds))return false;
   bool changed=false;if(frame.Facts==null)frame.Facts=new Dictionary<string,MetadataFact>();
   if(!UserMetadata(frame,"StackCount")&&frame.StackCount<=0){frame.StackCount=count;StackFact(frame,"StackCount",new MetadataFact{Value=count.ToString(CultureInfo.InvariantCulture),Raw=frame.OriginalName,Source="Seestar stacked filename"});changed=true;}
   if(!UserMetadata(frame,"Exposure")){
    var image=frame.Images==null?null:frame.Images.FirstOrDefault(i=>i.Key==frame.ImageKey)??frame.Images.FirstOrDefault();var header=image==null?new FitsHeader():new FitsHeader{Values=image.Headers??new Dictionary<string,string>(),Comments=image.Comments??new Dictionary<string,string>()};
    double? total=header.Number("TOTALEXP","TOTEXP","EXPTOTAL","INTTIME","INTEGRAT");string comment;header.Comments.TryGetValue("EXPTIME",out comment);
    if(!total.HasValue&&Regex.IsMatch(comment??"",@"total|integrat",RegexOptions.IgnoreCase))total=header.Number("EXPTIME");
    bool explicitTotal=total.HasValue&&total.Value>0&&!double.IsInfinity(total.Value)&&!double.IsNaN(total.Value);
    if(!explicitTotal)total=(frame.StackCount>0?frame.StackCount:count)*seconds;
    if(frame.Exposure!=total){frame.Exposure=total;changed=true;}
    changed=StackFact(frame,"Exposure",new MetadataFact{Value=total.Value.ToString("R",CultureInfo.InvariantCulture),Raw=explicitTotal?header.Get("TOTALEXP","TOTEXP","EXPTOTAL","INTTIME","INTEGRAT","EXPTIME"):frame.OriginalName,Source=explicitTotal?"Header: explicit total exposure":"Seestar stacked filename: "+(frame.StackCount>0?frame.StackCount:count)+" subs × "+seconds.ToString("G",CultureInfo.InvariantCulture)+" s per sub",Unit="s"})||changed;
   }
   return changed;
  }
  public static Frame Read(string path,string root,string telescope,string model,long? enumeratedSize=null,Action<int> counted=null,Dictionary<string,ShotsMetadata> shotsCache=null,FitsHeader parsedHeader=null,FileStamp parsedStamp=null,System.Threading.CancellationToken ct=default(System.Threading.CancellationToken),PipelineMetrics metrics=null,AssetInfo asset=null,string imageKey=null,string originalName=null,string detectionContext=null,Dictionary<string,string> sessionMetadata=null){
   ct.ThrowIfCancellationRequested();FileStamp sourceStamp=parsedStamp;if(asset==null&&parsedHeader!=null)asset=new AssetInfo{Format="FITS",Header=parsedHeader,Images={new ImageDescriptor{Key="hdu:0",Label="Image 1",Width=parsedHeader.Width,Height=parsedHeader.Height,Channels=parsedHeader.Channels,Bitpix=parsedHeader.Bitpix,Offset=parsedHeader.Offset,Count=1,Encoding="FITS",Numeric=new[]{8,16,32,64,-32,-64}.Contains(parsedHeader.Bitpix),Headers=parsedHeader.Values,Comments=parsedHeader.Comments}}};if(asset==null){using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){sourceStamp=FileStamp.Read(path);asset=Assets.Inspect(path,n=>{ct.ThrowIfCancellationRequested();if(counted!=null)counted(n);});if(!sourceStamp.ContentSame(FileStamp.Read(path)))throw new InvalidDataException("Source changed during metadata inspection.");}}var selectedImage=asset.Images.FirstOrDefault(i=>i.Key==imageKey);if(selectedImage!=null)asset.Header=new FitsHeader{Width=selectedImage.Width,Height=selectedImage.Height,Channels=selectedImage.Channels,Bitpix=selectedImage.Bitpix,Values=selectedImage.Headers??new Dictionary<string,string>(),Comments=selectedImage.Comments??new Dictionary<string,string>()};FitsHeader h=asset.Header;string rel=path.Substring(root.TrimEnd('\\','/').Length).TrimStart('\\','/');string text=Path.GetFileName(root.TrimEnd('\\','/'))+"/"+rel.Replace('\\','/');text=detectionContext??text;string low=text.ToLowerInvariant();string stem=CleanStem(originalName??Path.GetFileName(path));string name=stem.ToLowerInvariant();
   var f=new Frame{SourcePath=path,OriginalName=originalName??Path.GetFileName(path),Telescope=telescope,TelescopeIdentity=telescope,Model=model,Camera="Unknown",Target="Unknown",Kind="Unknown",Calibration="Unknown",Filter="Unknown",Bayer=h.Get("BAYERPAT","BAYERPATTERN"),Mount="Unknown",MountEvidence="Not analyzed",Notes="",Status="New",Bytes=enumeratedSize??new FileInfo(path).Length,Width=h.Width,Height=h.Height,Channels=h.Channels,BinX=(int)(h.Number("XBINNING","CCDXBIN","BINNING")??0),BinY=(int)(h.Number("YBINNING","CCDYBIN","BINNING")??0)};
   f.SourceStamp=sourceStamp;f.AssociatedFiles=detectionContext==null?AssociatedMetadata.Discover(path):null;var shots=sessionMetadata??(detectionContext==null?ReadShots(path,root,f,shotsCache,counted,ct,metrics):new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase));if(sessionMetadata!=null)f.SourceMetadataPath="Preserved shotsInfo.json";
   using(var classification=metrics==null?null:metrics.Begin("Classification",Path.GetFileName(path))){
   InstrumentDetection.Apply(f,h,root,model,detectionContext);
   CameraDetection.Apply(f,h,text,Shot(shots,"cameraId","camera_id","camId","cam_id"));
   string type=h.Get("IMAGETYP","IMAGETYPE","FRAME","FRAMETYP").ToLowerInvariant();
   bool master=Regex.IsMatch(name,@"(?:^|[_-])(master|stacked|stack|staced)(?:[_-]|$)")||Regex.IsMatch(name,@"(?:^|[_-])stack[_-]?\d+")||low.Contains("cali_frame/");
   string kindPath=low.Substring(0,low.LastIndexOf('/')+1)+name;
   bool dark=type.Contains("dark")||Regex.IsMatch(kindPath,@"(?:^|[/_ -])darks?(?:[/_ -]|$)");
   bool bias=type.Contains("bias")||type.Contains("offset")||Regex.IsMatch(kindPath,@"(?:^|[/_ -])(bias|biases|offset)(?:[/_ -]|$)");
   bool flat=type.Contains("flat")||Regex.IsMatch(kindPath,@"(?:^|[/_ -])flats?(?:[/_ -]|$)");
   if(dark&&flat)f.Kind=master?"Master dark flat":"Dark flat";else if(dark)f.Kind=master?"Master dark":"Dark";else if(bias)f.Kind=master?"Master bias":"Bias";else if(flat)f.Kind=master?"Master flat":"Flat";
   else if(master||type.Contains("stack")||low.Contains("restacked/")||Regex.IsMatch(name,@"\d+x\d+(?:\.\d+)?(?:s|sec)"))f.Kind="Stack";
   else if(type.Contains("light")||Regex.IsMatch(low,@"(?:_sub|[- ]sub)(?:/|$)")||low.Contains("dwarf_raw")||Regex.IsMatch(name,@"^(light|raw|sub)[_-]")||Regex.IsMatch(low,@"(?:^|/)lights?/")||type.Contains("science"))f.Kind="Light";
   string rejection=CaptureScreening.Rejection(h,text);if(rejection.Length>0){f.Rejected=true;f.RejectionReason=rejection;f.ScreeningIssue=rejection;f.Notes+=rejection+" Excluded from stacking by default. ";if(name.Contains("weight"))f.Kind="Auxiliary";}
   f.Exposure=h.Number("EXPTIME","EXPOSURE","EXP_TIME","EXPOS");if(!f.Exposure.HasValue)f.Exposure=MatchNumber(text,@"(?:^|[/_ -])EXP(?:OSURE)?[_ =-]*(\d+(?:\.\d+)?)");
   if(!f.Exposure.HasValue)f.Exposure=MatchNumber(stem,@"(?:^|[_ -])(\d+(?:\.\d+)?)\s*(?:sec|s)(?:[_ -]|$)");
   if(!f.Exposure.HasValue)f.Exposure=MatchNumber(text,@"\d+x(\d+(?:\.\d+)?)(?:sec|s)");
   f.Gain=h.Number("GAIN");if(!f.Gain.HasValue)f.Gain=MatchNumber(text,@"GAIN[_ =-](-?\d+(?:\.\d+)?)");
   double? filenameExposure,filenameGain;if(FilenameExposureGain(f.OriginalName,out filenameExposure,out filenameGain)){if(!f.Gain.HasValue)f.Gain=filenameGain;if(!f.Exposure.HasValue&&f.Kind=="Light")f.Exposure=filenameExposure;}
   f.Temperature=h.Number("CCD-TEMP","SENSOR_T","SENSORT","CAMTEMP","TEMPERAT");if(!f.Temperature.HasValue)f.Temperature=MatchNumber(text,@"(?:^|[_ -])T?(-?\d+(?:\.\d+)?)\s*°?C(?:[_ .-]|$)");
   double? bin=MatchNumber(text,@"BIN[_ =-](\d+)");if(bin.HasValue&&!h.Number("XBINNING","BINNING").HasValue)f.BinX=f.BinY=(int)bin.Value;
   double? stackCount=h.Number("NCOMBINE","STACKCNT","NSTACK","STACKNUM","NSUBS","SUBCOUNT");
   if(!stackCount.HasValue)stackCount=MatchNumber(name,@"(?:^|[_ -])(\d+)x\d+(?:\.\d+)?(?:sec|s)(?=[_ .-]|$)")??MatchNumber(name,@"(?:stack[_-]?|^)(\d+)(?:x|$|_)");
   if(stackCount.HasValue&&stackCount.Value>0&&stackCount.Value<=int.MaxValue&&stackCount.Value==Math.Floor(stackCount.Value))f.StackCount=(int)stackCount.Value;
   if(f.Kind=="Light"&&f.StackCount>1){f.Kind="Stack";f.Notes+="Header indicates multiple combined exposures. ";}
   if(f.Kind=="Unknown"&&f.StackCount>1)f.Kind="Stack";
   if(MediaFiles.Video(path))f.Kind="Video";
   string filter=h.Get("FILTER","FILTERID","FILTNAME");if(!string.IsNullOrEmpty(filter))f.Filter=filter;
   else {double? ir=MatchNumber(text,@"(?:^|_)IR[_-]?(\d)");if(ir.HasValue)f.Filter=ir==0?"Standard":ir==1?"Astro":ir==2?"Dual band":"IR "+ir;else if(Regex.IsMatch(low,@"(?:^|[_/ -])ir[_ -]*cut(?:[_/ .-]|$)"))f.Filter="IRCUT";else if(Regex.IsMatch(low,@"(?:^|[_/ -])(duo|dual|lp)[_-]?(band|filter)?(?:[_/ -]|$)"))f.Filter="Dual band";}
   f.ObservationMode=h.Get("OBSMODE","CAPMODE","SHOOTMOD","MODE");
   if(string.IsNullOrEmpty(f.ObservationMode)&&ObservationTargets.Unstretched(null,type))f.ObservationMode=type;
   string target=h.Get("OBJECT","OBJNAME","TARGET","TARGNAME","OBSTARG");
   if(Catalog.IsAmbiguous(target)){
    var m=Regex.Match(text,@"DWARF_RAW_(?:(?:TELE|WIDE)_)?(?:(?:MOSAIC)_)?(.+?)_EXP[_-]",RegexOptions.IgnoreCase);if(m.Success)target=m.Groups[1].Value;
    if(Catalog.IsAmbiguous(target)) {var cat=Regex.Match(text,@"(?:^|[/_ -])(M|NGC|IC)[ _-]*0*(\d+)([A-Za-z]?)(?=[/_ .-]|$)",RegexOptions.IgnoreCase);if(cat.Success)target=cat.Groups[1].Value+cat.Groups[2].Value+cat.Groups[3].Value;}
    if(Catalog.IsAmbiguous(target)) {string[] parts=text.Split('/');foreach(var part in parts.Reverse().Skip(1)){if(Regex.IsMatch(part,@"[_ -]sub$",RegexOptions.IgnoreCase)){target=Regex.Replace(part,@"[_ -]sub$","",RegexOptions.IgnoreCase);break;}}}
    if(Catalog.IsAmbiguous(target)){var p=Regex.Match(stem,@"^(?:Stacked|Light|Raw|Sub|staced)[_-](.+?)(?:[_-](?:EXP|GAIN|\d+(?:\.\d+)?s|\d{4}[-_]\d{2})|$)",RegexOptions.IgnoreCase);if(p.Success)target=p.Groups[1].Value;}
   }
   if(Catalog.IsAmbiguous(target))target=Shot(shots,"targetName","target_name","objectName","object_name","target");
   if(!f.Gain.HasValue)f.Gain=ShotNumber(shots,"gain","cameraGain");if(!f.Exposure.HasValue)f.Exposure=ShotNumber(shots,"exposure_s","exposureSeconds","exposureTimeSec");
   if(f.Filter=="Unknown"){double? ir=ShotNumber(shots,"ir","irCut");if(ir.HasValue&&ir>=0&&ir<=2)f.Filter=ir==0?"Standard":ir==1?"Astro":"Dual band";}
   if(f.BinX==0){string b=Shot(shots,"binning","bin");var m=Regex.Match(b,@"^(\d+)(?:\s*[x*]\s*(\d+))?$");if(m.Success){f.BinX=int.Parse(m.Groups[1].Value);f.BinY=m.Groups[2].Success?int.Parse(m.Groups[2].Value):f.BinX;}}
   if(Catalog.IsAmbiguous(target)){string hint=ObservationTargets.ModeFromPath(text),body=ObservationTargets.NamedSolar(hint);if(body!=null)target=body;if(string.IsNullOrEmpty(f.ObservationMode))f.ObservationMode=hint;}
   string filenameTarget=Catalog.TargetFromFilename(f.OriginalName);
   if(ObservationTargets.NamedSolar(filenameTarget)!=null&&ObservationTargets.NamedSolar(target)==null&&(Catalog.KnownName(target)!=null||CometTargets.IsComet(target)))filenameTarget=null;
   f.Target=filenameTarget??Catalog.Normalize(target);f.TargetEvidence=filenameTarget!=null?"Recognised filename target":Catalog.KnownName(target)!=null||CometTargets.IsComet(f.Target)?"Recognised header/session target":"Unrecognised label; plate solving required";
   if(f.Kind.Contains("dark")||f.Kind.Contains("bias")||f.Kind.Contains("flat")||f.Kind=="Dark"||f.Kind=="Bias"||f.Kind=="Flat"){f.Target="Calibration";f.TargetEvidence="Calibration frame";}
   if(Util.MeteorFilename(f.OriginalName)){f.Target="Meteor";f.TargetEvidence="Meteor filename label; object identity omitted";}
   string obs=h.Get("DATE-OBS","DATEOBS","DATE_OBS");DateTime? dt=Util.Time(obs);bool frameTime=dt.HasValue&&obs.Length>10;
   if(!dt.HasValue){string pattern=@"(20\d{2})[-_]?(\d{2})[-_]?(\d{2})[-_T ](\d{2})[-_:]?(\d{2})[-_:]?(\d{2})(?:[-_.](\d{3}))?";var m=Regex.Match(stem,pattern);bool fromFile=m.Success;if(!m.Success)m=Regex.Match(text,pattern);if(m.Success) {try{dt=new DateTime(int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value),int.Parse(m.Groups[3].Value),int.Parse(m.Groups[4].Value),int.Parse(m.Groups[5].Value),int.Parse(m.Groups[6].Value),m.Groups[7].Success?int.Parse(m.Groups[7].Value):0,DateTimeKind.Unspecified);frameTime=fromFile;f.TimeSource=fromFile?"Filename (timezone unknown)":"Session folder (not frame time)";}catch{}}}
   else f.TimeSource=frameTime?"FITS UTC":"FITS date only";
   f.Observed=dt.HasValue&&frameTime?dt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff",CultureInfo.InvariantCulture):"";
   CaptureSessions.SaveDate(f,CaptureSessions.HeaderDate(h)??CaptureSessions.FilenameDate(f.OriginalName));
   if(dt.HasValue){DateTime local=f.TimeSource=="FITS UTC"?TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt.Value,DateTimeKind.Utc),TimeZoneInfo.Local):dt.Value;f.Night=local.AddHours(-12).ToString("yyyy-MM-dd");}else{f.Night="Unknown date";f.TimeSource="Unknown";}
   string sourceSession=Regex.Match(text,@"DWARF_RAW[^/]+",RegexOptions.IgnoreCase).Value;
   if(sourceSession.Length==0)sourceSession=Path.GetDirectoryName(rel)??"Root";
   f.RA=Catalog.Sex(h.Get("OBJCTRA"),true)??h.Number("RA_DEG","RADEG","RA_OBJ");f.Dec=Catalog.Sex(h.Get("OBJCTDEC"),false)??h.Number("DEC_DEG","DECDEG","DEC_OBJ","DEC");
   string raComment;if(!f.RA.HasValue&&h.Comments.TryGetValue("RA",out raComment)&&raComment.ToLowerInvariant().Contains("deg"))f.RA=h.Number("RA");
   if(h.Get("CTYPE1").StartsWith("RA")&&h.Get("CTYPE2").StartsWith("DEC")){f.RA=h.Number("CRVAL1")??f.RA;f.Dec=h.Number("CRVAL2")??f.Dec;}
   f.Latitude=h.Number("SITELAT","OBSGEO-B");f.Longitude=h.Number("SITELONG","SITELON","OBSGEO-L");
   string mode=h.Get("MOUNTMOD","MOUNTMODE","TRACKMOD","MOUNTTYP").Trim().ToUpperInvariant();if(mode=="EQ"||mode.Contains("EQUATORIAL")){f.Mount="EQ";f.MountEvidence="Explicit FITS mount metadata";}else if(mode=="AZ"||mode=="ALT/AZ"||mode=="ALTAZ"||mode.Contains("ALT-AZ")){f.Mount="Alt-Az";f.MountEvidence="Explicit FITS mount metadata";}
   string cal=h.Get("CALSTAT");if(h.Get("CALIBRAT","CALIBRED")=="T"||Regex.IsMatch(cal,@"[DBF]"))f.Calibration="Calibrated";
   if(h.Get("REGISTER","REGISTRD","DEROTATE")=="T"||Regex.IsMatch(name,@"^(r_|r_pp_|registered[_-])")||low.Contains("/registered/"))f.Calibration="Registered";
   if(f.Kind=="Stack")f.Calibration="Device stack";if(f.Kind.StartsWith("Master")||f.Kind=="Dark"||f.Kind=="Flat"||f.Kind=="Bias")f.Calibration="Calibration frame";
   if(f.Target=="Unknown")f.Notes+="Target needs identification. ";if(f.Kind=="Unknown")f.Notes+="Frame type needs review. ";if(f.Camera=="Unknown")f.Notes+="Camera channel unknown. ";
   f.Sky=SkyWcs.FromHeader(h,f.Width,f.Height);if(classification!=null)classification.Complete();MetadataProfiles.Apply(f,h,asset);CameraDetection.DefaultForTarget(f);f.Session=Util.HashText(telescope+"|"+sourceSession+"|"+f.Night+"|"+f.Target+"|"+f.Camera+"|"+f.Width+"x"+f.Height).Substring(0,16);if(selectedImage!=null)f.ImageKey=selectedImage.Key;ApplyStackMetadata(f);if(f.Kind=="Video")MediaFiles.ApplyVideoDuration(f,asset.DurationSeconds,asset.DurationSource);return f;
   }
  }
  static Dictionary<string,string> ReadShots(string path,string root,Frame f,Dictionary<string,ShotsMetadata> cache,Action<int> counted,System.Threading.CancellationToken ct,PipelineMetrics metrics){
   using(var scope=metrics==null?null:metrics.Begin("Session metadata",Path.GetFileName(path))){if(cache==null)return ReadShotsCore(path,root,f,cache,counted,ct);lock(cache)return ReadShotsCore(path,root,f,cache,counted,ct);}
  }
  static Dictionary<string,string> ReadShotsCore(string path,string root,Frame f,Dictionary<string,ShotsMetadata> cache,Action<int> counted,System.Threading.CancellationToken ct){string dir=Path.GetDirectoryName(path);for(int i=0;i<4&&dir!=null&&Util.Within(dir,root);i++,dir=Path.GetDirectoryName(dir)){ct.ThrowIfCancellationRequested();string sidecar=Path.Combine(dir,"shotsInfo.json");ShotsMetadata metadata;if(cache==null||!cache.TryGetValue(sidecar,out metadata)){metadata=new ShotsMetadata();if(File.Exists(sidecar)){metadata.Path=sidecar;try{if(new FileInfo(sidecar).Length>4*1024*1024)metadata.Note="Large shotsInfo.json preserved but not parsed. ";else{using(var stream=new FileStream(sidecar,FileMode.Open,FileAccess.Read,FileShare.Read)){using(var reader=new StreamReader(stream)){string json=reader.ReadToEnd();if(counted!=null)counted((int)stream.Position);ct.ThrowIfCancellationRequested();metadata.Raw=Util.Deserialize<Dictionary<string,object>>(json);Flatten(metadata.Raw,metadata.Values,0);metadata.Stamp=FileStamp.Read(sidecar);}}}}catch(System.OperationCanceledException){throw;}catch{metadata.Note="shotsInfo.json could not be parsed. ";}}if(cache!=null)cache[sidecar]=metadata;}if(metadata.Path==null)continue;f.SourceMetadataPath=metadata.Path;f.SourceMetadataStamp=metadata.Stamp;f.Notes+=metadata.Note;return metadata.Values;}return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);}
  static void Flatten(Dictionary<string,object> obj,Dictionary<string,string> values,int depth){if(obj==null||depth>5)return;foreach(var kv in obj){var nested=kv.Value as Dictionary<string,object>;if(nested!=null){Flatten(nested,values,depth+1);continue;}if(kv.Value is string||kv.Value is int||kv.Value is long||kv.Value is double||kv.Value is decimal)if(!values.ContainsKey(kv.Key))values[kv.Key]=Convert.ToString(kv.Value,CultureInfo.InvariantCulture);}}
  static string Shot(Dictionary<string,string> obj,params string[] names){foreach(string name in names){string s;if(obj.TryGetValue(name,out s)&&s.Length>0)return s;}return "";}
  static double? ShotNumber(Dictionary<string,string> obj,params string[] names){double n;return double.TryParse(Shot(obj,names),NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?(double?)n:null;}
 }
}
