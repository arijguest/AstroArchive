using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace AstroArchive {
 // UI projection of the database, read one card/image at a time, off the UI thread.
 public sealed class TargetInfoCard {
  public string id,name,domain,type_label,constellation,parent_body,summary,fact,review_status,identity_status,content_tier;
  public string[] aliases,source_urls;
  public TargetInfoMeasurement[] measurements;
  public TargetInfoCitation[] citations;
  public TargetInfoImage image;
  public byte[] Thumbnail;
 }
 public sealed class TargetInfoMeasurement {
  public string id,object_id,field,unit,epoch,band,uncertainty,qualifier,citation_id;
  public double value;
 }
 public sealed class TargetInfoCitation {
  public string id,source_url,source_title,publisher,applies_to,review_status;
 }
 public sealed class TargetInfoImage {
  public string object_id,local_path,creator,attribution,licence,licence_url,file_page_url,representation,modifications,sha256;
 }
 public sealed class TargetInfoResult {
  public string Status,Message;
  public List<TargetInfoCard> Cards=new List<TargetInfoCard>();
 }
 public sealed class TargetInfoIndex {
  public int format_version;
  public string data_version;
  public Dictionary<string,string> entries;
  public Dictionary<string,string[]> aliases;
  public string[] uncertain,nonexistent;
 }
 public sealed class TargetInfoCatalog {
  readonly string path;readonly object gate=new object();TargetInfoIndex index;
  readonly Dictionary<string,TargetInfoCard> cache=new Dictionary<string,TargetInfoCard>();
  readonly LinkedList<string> recent=new LinkedList<string>();
  public TargetInfoCatalog(string path){this.path=path;}
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=16*1024*1024,RecursionLimit=64};}
  static byte[] Read(ZipArchive zip,string name,int limit){
   var entry=zip.GetEntry(name);if(entry==null||entry.Length>limit)throw new InvalidDataException("Target information pack is incomplete.");
   using(var input=entry.Open())using(var output=new MemoryStream()){var buffer=new byte[8192];int n;while((n=input.Read(buffer,0,buffer.Length))>0){if(output.Length+n>limit)throw new InvalidDataException("Target information entry is too large.");output.Write(buffer,0,n);}return output.ToArray();}
  }
  public static string Normalize(string query){
   string text=Regex.Replace((query??"").Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant(),@"\s+"," ").Replace("VAN DEN BERGH","VDB");
   var patterns=new[]{@"^(?:M|MESSIER)\s*0*(\d+)$",@"^(NGC|IC)\s*0*(\d+)([A-Z]*)(?:\s+NED\s*0*(\d+))?$",@"^(?:C|CALDWELL)\s*0*(\d+)$",@"^(?:B|BARNARD)\s*0*(\d+)([A-Z]?)$",@"^(?:SH\s*2|SHARPLESS\s*2)\s*-?\s*0*(\d+)$",@"^(LDN|LBN|HCG|ARP|MEL|MELOTTE|CL|COLLINDER|VDB|VD B|GUM|RCW|CED|CEDERBLAD|DWB|STOCK|BERKELEY|TRUMPLER|KING)\s*0*(\d+)([A-Z]?)$"};
   for(int i=0;i<patterns.Length;i++){
    var m=Regex.Match(text,patterns[i],RegexOptions.CultureInvariant);if(!m.Success)continue;
    Func<int,string> number=n=>m.Groups[n].Value.TrimStart('0').Length==0?"0":m.Groups[n].Value.TrimStart('0');
    if(i==0||i==2||i==3||i==4)return new[]{"M","","C","B","SH2"}[i]+":"+number(1)+(i==3?m.Groups[2].Value:"");
    string prefix=m.Groups[1].Value;if(prefix=="MELOTTE")prefix="MEL";if(prefix=="COLLINDER")prefix="CL";if(prefix=="VD B")prefix="VDB";if(prefix=="CEDERBLAD")prefix="CED";
    return prefix+":"+number(2)+m.Groups[3].Value+(i==1&&m.Groups[4].Success?":NED"+number(4):"");
   }
   if(Regex.IsMatch(text,@"^\(?\d+\)?\s+[A-Z][A-Z0-9 -]+$"))text=Regex.Replace(text,@"^\((\d+)\)","$1");
   if(Regex.IsMatch(text,@"^(?:[CPDXAI]\s*/|\d+[PID]\s*/|PK(?=\s*\d)|PN(?=\s*[A-Z0-9]))"))return "DES:"+Regex.Replace(text,@"\s+","");
   return "NAME:"+text;
  }
  TargetInfoCard Card(ZipArchive zip,string id,CancellationToken token){
   TargetInfoCard card;if(cache.TryGetValue(id,out card)){recent.Remove(id);recent.AddLast(id);return card;}
   string entry;if(!index.entries.TryGetValue(id,out entry))throw new InvalidDataException("Target card is missing.");
   token.ThrowIfCancellationRequested();var cards=Json().Deserialize<Dictionary<string,TargetInfoCard>>(Encoding.UTF8.GetString(Read(zip,entry,8*1024*1024)));
   if(cards==null||!cards.TryGetValue(id,out card))throw new InvalidDataException("Target card is missing.");
   if(card==null||card.id!=id)throw new InvalidDataException("Target card identity is invalid.");
   if(card.image!=null){try{card.Thumbnail=Read(zip,card.image.local_path,2*1024*1024);}catch(InvalidDataException){card.Thumbnail=null;}}
   cache[id]=card;recent.AddLast(id);while(recent.Count>12){cache.Remove(recent.First.Value);recent.RemoveFirst();}return card;
  }
  public TargetInfoResult Find(string query,CancellationToken token){return Load(query,false,token);}
  public TargetInfoResult ById(string id,CancellationToken token){return Load(id,true,token);}
  TargetInfoResult Load(string query,bool byId,CancellationToken token){
   token.ThrowIfCancellationRequested();lock(gate){token.ThrowIfCancellationRequested();
    using(var file=File.OpenRead(path))using(var zip=new ZipArchive(file,ZipArchiveMode.Read)){
     if(index==null){index=Json().Deserialize<TargetInfoIndex>(Encoding.UTF8.GetString(Read(zip,"index.json",16*1024*1024)));if(index==null||index.format_version!=1||index.entries==null||index.aliases==null)throw new InvalidDataException("Unsupported target information pack.");}
     var result=new TargetInfoResult();string term=Normalize(query);string[] ids;
     if(byId)ids=index.entries.ContainsKey(query)?new[]{query}:new string[0];
     else if(!index.aliases.TryGetValue(term,out ids))ids=new string[0];
     bool uncertain=!byId&&(index.uncertain??new string[0]).Contains(term);
     result.Status=ids.Length==1&&!uncertain?"matched":ids.Length>0?"ambiguous":"not_found";
     result.Message=result.Status=="ambiguous"?"This name has more than one possible identity. Choose an object to read about; your files will not be renamed.":!byId&&(index.nonexistent??new string[0]).Contains(term)?"This catalogue designation is listed as nonexistent.":"No verified overview is available for this target yet.";
     // Ambiguous matches are displayed as explicit choices, never picked by order.
     foreach(string id in ids.Take(30)){token.ThrowIfCancellationRequested();result.Cards.Add(Card(zip,id,token));}
     token.ThrowIfCancellationRequested();return result;
    }
   }
  }
 }
}
