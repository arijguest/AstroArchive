using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Web.Script.Serialization;

namespace AstroArchive {
 public sealed class ProviderPermission {
  public string provider,dataset,terms_url,checked_at,commercial_conditions,required_attribution,evidence;
  public bool allowed_display,allowed_local_cache,allowed_bundle,allowed_redistribution;
 }
 public sealed class InformationRights {
  public string provider,dataset,source_url,source_revision,retrieved_at,licence_id,licence_version,licence_url,creator,full_attribution,modifications,rights_evidence;
  static readonly Dictionary<string,ProviderPermission> permissions=Load();
  static Dictionary<string,ProviderPermission> Load(){
   var result=new Dictionary<string,ProviderPermission>(StringComparer.Ordinal);using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("InformationProviders.json")){
    if(stream==null)return result;using(var reader=new StreamReader(stream)){var rows=new JavaScriptSerializer{MaxJsonLength=65536,RecursionLimit=8}.Deserialize<ProviderPermission[]>(reader.ReadToEnd());
     foreach(var row in rows)result[row.provider+":"+row.dataset]=row;}}
   return result;
  }
  public static bool Permitted(InformationRights item,bool cache,bool bundle=false){
   ProviderPermission permission;Uri url,licence;DateTime date;
   if(item==null||!permissions.TryGetValue((item.provider??"")+":"+(item.dataset??""),out permission)||!permission.allowed_display||cache&&!permission.allowed_local_cache||bundle&&(!permission.allowed_bundle||!permission.allowed_redistribution))return false;
   if(string.IsNullOrWhiteSpace(item.full_attribution)||item.full_attribution.Length>8000||string.IsNullOrWhiteSpace(item.rights_evidence)||item.rights_evidence.Length>8000||string.IsNullOrWhiteSpace(item.source_revision)||item.source_revision.Length>250||
    (item.creator??"").Length>8000||(item.modifications??"").Length>8000||(item.source_url??"").Length>2000||(item.licence_url??"").Length>1000||!DateTime.TryParse(item.retrieved_at,out date)||!Uri.TryCreate(item.source_url,UriKind.Absolute,out url)||url.Scheme!="https"||url.Port!=443||url.UserInfo.Length>0||!Uri.TryCreate(item.licence_url,UriKind.Absolute,out licence)||licence.Scheme!="https"||licence.Port!=443||licence.UserInfo.Length>0)return false;
   if(item.provider=="JPL")return (item.dataset=="orbital-elements"||item.dataset=="planetary-approximation"||item.dataset=="small-body-physical-facts")&&item.licence_id=="numerical-facts"&&(url.Host=="ssd.jpl.nasa.gov"||url.Host=="ssd-api.jpl.nasa.gov");
   if(item.provider=="Wikidata")return item.dataset=="identifiers"&&item.licence_id=="CC0"&&url.Host=="www.wikidata.org";
   if(item.provider=="Wikipedia")return item.dataset=="introductions"&&item.licence_id=="CC-BY-SA"&&item.licence_version=="4.0"&&InformationHttp.WikipediaHost(url.Host);
   if(item.provider=="Commons")return item.dataset=="reviewed-media"&&url.Host=="commons.wikimedia.org"&&
    (item.licence_id=="CC0"||item.licence_id=="public-domain"||(item.licence_id=="CC-BY"||item.licence_id=="CC-BY-SA")&&(item.licence_version=="4.0"||item.licence_version=="3.0"||item.licence_version=="2.5"||item.licence_version=="2.0"));
   return false;
  }
 }
 public sealed class DownloadedInformation {
  public int schema=1,policy=1;public string provider,object_id,provider_id,language,revision,retrieved_at,expires_at,summary;
  public InformationRights text_rights,identity_rights,measurement_rights;public TargetInfoMeasurement[] measurements;public TargetInfoCitation[] citations;public TargetInfoImage image;public byte[] thumbnail;public OrbitRecord orbit;
  [ScriptIgnore]public object DecodedThumbnail;
  public bool Valid(string id){
   DateTime retrieved,expires;if(schema!=1||policy!=1||object_id!=id||string.IsNullOrEmpty(provider)||string.IsNullOrEmpty(provider_id)||provider_id.Length>150||string.IsNullOrEmpty(revision)||revision.Length>250||
    !DateTime.TryParse(retrieved_at,out retrieved)||!DateTime.TryParse(expires_at,out expires)||expires<retrieved)return false;
   if(summary!=null&&(summary.Length>12000||!InformationRights.Permitted(text_rights,true)||!InformationRights.Permitted(identity_rights,true)))return false;
   if(orbit!=null&&(!OrbitalModel.Valid(orbit)||orbit.object_id!=id||!InformationRights.Permitted(orbit.rights,true)))return false;
   if(measurements!=null&&(measurements.Length>20||citations==null||citations.Length>20||!InformationRights.Permitted(measurement_rights,true)||measurements.Any(m=>m.object_id!=id||!OrbitalModel.Finite(m.value)||m.value<0||!new[]{"Diameter","Rotation period","Albedo"}.Contains(m.field)||!citations.Any(c=>c.id==m.citation_id))))return false;
   if(thumbnail!=null&&(thumbnail.Length>2*1024*1024||image==null||image.object_id!=id||!InformationRights.Permitted(image.rights,true)||!InformationHttp.Raster(thumbnail)))return false;
   return summary!=null||orbit!=null||thumbnail!=null;
  }
 }
}
