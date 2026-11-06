using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace AstroArchive {
 // Reviewed astronomical subject/file associations. P18/search candidates are
 // deliberately never promoted to an image automatically.
 public static class InformationMedia {
  public static Func<byte[],object> Decode;
  public static void DecodeSaved(DownloadedInformation record){if(record==null||record.thumbnail==null||Decode==null)return;try{record.DecodedThumbnail=Decode(record.thumbnail);}catch{record.DecodedThumbnail=null;}if(record.DecodedThumbnail==null){record.thumbnail=null;record.image=null;}}
  static readonly Lazy<Dictionary<string,TargetInfoImage>> mappings=new Lazy<Dictionary<string,TargetInfoImage>>(()=>{
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("InformationMedia.json")){if(stream==null)return new Dictionary<string,TargetInfoImage>();using(var reader=new StreamReader(stream))return new JavaScriptSerializer{MaxJsonLength=2*1024*1024,RecursionLimit=8}.Deserialize<Dictionary<string,TargetInfoImage>>(reader.ReadToEnd());}
  });
  static string Meta(Dictionary<string,object> metadata,string key,bool attribution=false){var map=InformationProviders.Value(metadata,key) as Dictionary<string,object>;if(map==null)return null;string value=InformationProviders.Text(map,"value",8000);return attribution?TargetInfoPresentation.CreditText(value):InformationProviders.Plain(value);}
  internal static string Attribution(Dictionary<string,object> metadata,string reviewed){return string.Join("\n",new[]{TargetInfoPresentation.CreditText(reviewed),Meta(metadata,"Artist",true),Meta(metadata,"Credit",true)}.Where(value=>!string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));}
  public static void Add(TargetInfoCard card,DownloadedInformation record,CancellationToken token){
   TargetInfoImage approved;if(card.Thumbnail!=null||!mappings.Value.TryGetValue(card.id,out approved))return;
   Uri source;if(!Uri.TryCreate(approved.file_page_url,UriKind.Absolute,out source)||source.Host!="commons.wikimedia.org"||!source.AbsolutePath.StartsWith("/wiki/File:",StringComparison.Ordinal))return;
   string title=Uri.UnescapeDataString(source.AbsolutePath.Substring(6));var root=InformationProviders.Map(InformationHttp.Json(new Uri("https://commons.wikimedia.org/w/api.php?action=query&format=json&prop=imageinfo&iiprop=url%7Cextmetadata%7Ctimestamp%7Csha1%7Csize&iiurlwidth=640&titles="+Uri.EscapeDataString(title)+"&maxlag=5"),token));
   var pages=InformationProviders.Map(InformationProviders.Value(InformationProviders.Map(InformationProviders.Value(root,"query")),"pages"));if(pages.Count!=1)return;
   var page=InformationProviders.Map(pages.Values.First());if(InformationProviders.Text(page,"title",500).Replace('_',' ')!=title.Replace('_',' '))return;
   var info=InformationProviders.Map(InformationProviders.ArrayValue(page,"imageinfo",1)[0]);var metadata=InformationProviders.Map(InformationProviders.Value(info,"extmetadata"));
   string licence=Meta(metadata,"LicenseShortName"),url=Meta(metadata,"LicenseUrl"),creator=Meta(metadata,"Artist");if(licence==null||creator==null)return;
   string id=null,version=null;var match=Regex.Match(licence,@"^CC BY(-SA)? (2\.0|2\.5|3\.0|4\.0)$");
   if(match.Success){id=match.Groups[1].Success?"CC-BY-SA":"CC-BY";version=match.Groups[2].Value;}else if(licence=="CC0"){id="CC0";version="1.0";}else if(licence=="Public domain"){id="public-domain";version="not applicable";}
   string expected=Regex.Replace(approved.licence??"",@"[- ]","");if(id==null||id=="public-domain"&&expected!="Publicdomain"||id=="CC0"&&!expected.StartsWith("CC01.0",StringComparison.Ordinal)||match.Success&&Regex.Replace(id+version,@"[- ]","")!=expected)return;
   if(id=="public-domain")url=approved.licence_url;if(id=="CC0")url="https://creativecommons.org/publicdomain/zero/1.0/";
   if(url!=null&&url.StartsWith("//",StringComparison.Ordinal))url="https:"+url;if(url!=null&&url.StartsWith("http://creativecommons.org/",StringComparison.Ordinal))url="https://"+url.Substring(7);
   Uri licenceUri;if(!Uri.TryCreate(url,UriKind.Absolute,out licenceUri)||licenceUri.Scheme!="https"||licenceUri.Host!="creativecommons.org"&&licenceUri.Host!="commons.wikimedia.org")return;
   DateTime reviewed,changed;if(!DateTime.TryParse(approved.reviewed_at,out reviewed)||!DateTime.TryParse(InformationProviders.Text(info,"timestamp",100),out changed)||changed.ToUniversalTime()>reviewed.Date.AddDays(1).ToUniversalTime())return;
   string revision=InformationProviders.Text(info,"timestamp",100)+":"+InformationProviders.Text(info,"sha1",100);DateTime now=DateTime.UtcNow;
   var rights=new InformationRights{provider="Commons",dataset="reviewed-media",source_url=approved.file_page_url,source_revision=revision,retrieved_at=now.ToString("o"),licence_id=id,licence_version=version,licence_url=url,creator=creator,full_attribution=Attribution(metadata,approved.attribution),modifications="Commons 640 px thumbnail; displayed without further processing.",rights_evidence="Reviewed exact subject/representation association at "+approved.reviewed_at+"; no later file overwrite. "+approved.subject_review+" Current extmetadata licence agrees. "+approved.file_page_url};
   if(!InformationRights.Permitted(rights,true))return;
   string thumbnail=InformationProviders.Text(info,"thumburl",2000);Uri uri;if(!Uri.TryCreate(thumbnail,UriKind.Absolute,out uri)||!InformationHttp.Trusted(uri))return;
   var bytes=InformationHttp.Fetch(uri,2*1024*1024,token);if(!InformationHttp.Raster(bytes))return;
   record.image=new TargetInfoImage{object_id=card.id,creator=creator,attribution=rights.full_attribution,licence=licence,licence_url=url,file_page_url=approved.file_page_url,representation=approved.representation,modifications=rights.modifications,rights=rights};record.thumbnail=bytes;DecodeSaved(record);
  }
 }
}
