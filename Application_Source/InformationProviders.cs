using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace AstroArchive {
 public static class InformationProviders {
  static readonly SemaphoreSlim jpl=new SemaphoreSlim(1,1),wiki=new SemaphoreSlim(1,1);
  internal static Dictionary<string,object> Map(object o){var map=o as Dictionary<string,object>;if(map==null)throw new InvalidDataException("Invalid information schema.");return map;}
  internal static object Value(Dictionary<string,object> map,string key){object value;return map.TryGetValue(key,out value)?value:null;}
  internal static string Text(Dictionary<string,object> map,string key,int limit=12000){var value=Value(map,key) as string;if(value!=null&&value.Length>limit)throw new InvalidDataException("Information text exceeds its limit.");return value;}
  internal static object[] ArrayValue(Dictionary<string,object> map,string key,int limit){var array=Value(map,key) as object[];if(array==null||array.Length>limit)throw new InvalidDataException("Invalid information record count.");return array;}
  static double Number(Dictionary<string,object> map,string key){double n;if(!double.TryParse(Convert.ToString(Value(map,key),CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out n)||!OrbitalModel.Finite(n))throw new InvalidDataException("Invalid orbital number.");return n;}
  public static bool SbdbIdentity(string canonical,Dictionary<string,object> obj){
   string des=Text(obj,"des",100),prefix=Text(obj,"prefix",2),spk=Text(obj,"spkid",20);if(!Regex.IsMatch(spk??"",@"^\d{1,12}$"))return false;
   string expected=OrbitalCatalog.ProviderId(canonical);if(expected==null)return false;
   // A dated comet's des omits C/; fragments remain part of the designation.
   string actual=Regex.IsMatch(des??"",@"^\d{4} [A-Z]{1,2}\d+(?:-[A-Z0-9]+)?$")?prefix+"/"+des:des;
   if(canonical=="2I")return spk=="1003639"&&actual=="C/2019 Q4";
   if(canonical=="3I")return actual=="C/2025 N1"&&(Text(obj,"fullname",150)??"").Contains("3I");
   return string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase);
  }
  public static OrbitRecord ParseSbdb(object response,string canonical,DateTime retrieved){
   var root=Map(response);var signature=Map(Value(root,"signature"));if(Text(signature,"version",10)!="1.3"||Text(signature,"source",100)!="NASA/JPL Small-Body Database (SBDB) API")throw new InvalidDataException("Unsupported JPL API version.");
   var obj=Map(Value(root,"object"));if(!SbdbIdentity(canonical,obj))throw new InvalidDataException("JPL identity does not match the selected target.");
   var orbit=Map(Value(root,"orbit"));if(Text(orbit,"equinox",20)!="J2000"||Text(orbit,"source",20)!="JPL")throw new InvalidDataException("Unsupported orbital reference frame.");
   var values=new Dictionary<string,Dictionary<string,object>>();foreach(var item in ArrayValue(orbit,"elements",40)){var entry=Map(item);string name=Text(entry,"name",20);if(name==null||values.ContainsKey(name))throw new InvalidDataException("Duplicate orbital element.");values[name]=entry;}
   Func<string,string,double> element=(key,unit)=>{Dictionary<string,object> value;if(!values.TryGetValue(key,out value)||Text(value,"units",20)!=unit)throw new InvalidDataException("Missing orbital element or unexpected unit.");return Number(value,"value");};
   string solution=Text(orbit,"orbit_id",100),source=CometInfo.LookupUrl(canonical);var record=new OrbitRecord{object_id=canonical,provider_id=Text(obj,"spkid",20),central_body="Sun",reference_plane="ecliptic",coordinate_frame="J2000",time_standard="TDB",epoch=Number(orbit,"epoch"),e=element("e",null),q=element("q","au"),i=element("i","deg"),ascending_node=element("om","deg"),argument_of_perihelion=element("w","deg"),mean_anomaly=element("ma","deg"),perihelion_time=element("tp","TDB"),solution_id=solution,solution_date=Text(orbit,"soln_date",100),source=source,uncertainties=new Dictionary<string,string>()};
   // Osculating solutions have no assumed current-position validity. Epoch only.
   foreach(var entry in values){string sigma=Text(entry.Value,"sigma",100);if(sigma!=null)record.uncertainties[entry.Key]=sigma+" "+Text(entry.Value,"units",20);}
   var cls=Value(obj,"orbit_class") as Dictionary<string,object>;record.orbit_class=cls==null?null:Text(cls,"code",10);
   record.rights=OrbitalCatalog.Rights(source,solution+" @ "+record.epoch.ToString("R",CultureInfo.InvariantCulture),retrieved,"orbital-elements");
   if(!OrbitalModel.Valid(record))throw new InvalidDataException("Incomplete or invalid orbital solution.");return record;
  }
  public static DownloadedInformation Sbdb(string id,string language,CancellationToken token){
   string designation=OrbitalCatalog.ProviderId(id);if(designation==null)return null;jpl.Wait(token);try{
    var now=DateTime.UtcNow;var response=InformationHttp.Json(new Uri("https://ssd-api.jpl.nasa.gov/sbdb.api?sstr="+Uri.EscapeDataString(designation)+"&full-prec=true&phys-par=true"),token);var orbit=ParseSbdb(response,id,now);
    var record=new DownloadedInformation{provider="JPL",object_id=id,provider_id=orbit.provider_id,language=language,revision=orbit.solution_id+":"+orbit.epoch.ToString("R",CultureInfo.InvariantCulture),retrieved_at=now.ToString("o"),expires_at=now.AddDays(7).ToString("o"),orbit=orbit};
    var physical=Value(Map(response),"phys_par") as object[];if(physical!=null&&physical.Length<=50){record.revision+=":phys:"+Util.HashText(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(physical)).Substring(0,16);var measurements=new List<TargetInfoMeasurement>();var citations=new List<TargetInfoCitation>();
     foreach(var value in physical){var field=Map(value);string name=Text(field,"name",30),unit=Text(field,"units",30),label=name=="diameter"&&unit=="km"?"Diameter":name=="rot_per"&&unit=="h"?"Rotation period":name=="albedo"&&unit==null?"Albedo":null;if(label==null)continue;
      double number;if(!double.TryParse(Text(field,"value",100),NumberStyles.Float,CultureInfo.InvariantCulture,out number)||!OrbitalModel.Finite(number)||number<0||number>1e9||name=="albedo"&&number>2)continue;
      string sigma=Text(field,"sigma",100);double uncertainty;if(!string.IsNullOrWhiteSpace(sigma)&&(!double.TryParse(sigma,NumberStyles.Float,CultureInfo.InvariantCulture,out uncertainty)||!OrbitalModel.Finite(uncertainty)||uncertainty<0))continue;string reference=Plain(Text(field,"ref",1000));if(string.IsNullOrWhiteSpace(reference))continue;string citation="jpl:"+orbit.provider_id+":"+name;
      measurements.Add(new TargetInfoMeasurement{id=citation,object_id=id,field=label,value=number,unit=unit??"dimensionless",uncertainty=sigma,epoch=null,citation_id=citation,qualifier="Reported numerical measurement. Method is not supplied by the API; see the original reference. "+reference});
      citations.Add(new TargetInfoCitation{id=citation,source_url=orbit.source,source_title="JPL · "+reference,publisher="NASA/JPL",applies_to=id+":"+name,review_status="source_checked"});
     }
     if(measurements.Count>0){record.measurements=measurements.ToArray();record.citations=citations.ToArray();record.measurement_rights=OrbitalCatalog.Rights(orbit.source,record.revision,now,"small-body-physical-facts");}
    }return record;
   }finally{jpl.Release();}
  }
  public static DownloadedInformation Wikipedia(TargetInfoCard card,string language,CancellationToken token){
   // Exact astronomical catalogue identifiers only; no fuzzy title/name search.
   if(card.domain!="deep_sky"||card.identity_status=="disputed"||!Regex.IsMatch(card.id??"",@"^(?:NGC|IC) \d{1,5}[A-Z]*$|^M\d{1,3}$|^SH2-\d{1,3}$"))return null;
   wiki.Wait(token);try{
    string ident=card.id.StartsWith("M",StringComparison.Ordinal)&&!card.id.StartsWith("MC",StringComparison.Ordinal)?"M "+card.id.Substring(1):card.id;
    string query="SELECT DISTINCT ?item WHERE { ?item wdt:P528 \""+ident+"\"; wdt:P31/wdt:P279* wd:Q6999. } LIMIT 2";
    var search=Map(InformationHttp.Json(new Uri("https://query.wikidata.org/sparql?format=json&query="+Uri.EscapeDataString(query)),token));var bindings=ArrayValue(Map(Value(search,"results")),"bindings",2);if(bindings.Length!=1)return null;
    string item=Text(Map(Value(Map(bindings[0]),"item")),"value",200);var match=Regex.Match(item??"",@"^https?://www\.wikidata\.org/entity/(Q[1-9]\d{0,12})$");if(!match.Success)return null;
    string qid=match.Groups[1].Value;var entityRoot=Map(InformationHttp.Json(new Uri("https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&ids="+qid+"&props=claims%7Csitelinks&maxlag=5"),token));var entity=Map(Value(Map(Value(entityRoot,"entities")),qid));
    var claims=Map(Value(entity,"claims"));bool verified=false;foreach(var claim in ArrayValue(claims,"P528",100)){var snak=Map(Value(Map(claim),"mainsnak"));var dv=Value(snak,"datavalue") as Dictionary<string,object>;if(dv!=null&&Text(dv,"value",200)==ident)verified=true;}if(!verified)return null;
    var links=Map(Value(entity,"sitelinks"));string lang=language=="zh-Hans"?"zh":language;if(!InformationHttp.WikipediaHost(lang+".wikipedia.org"))lang="en";
    var link=Value(links,lang+"wiki") as Dictionary<string,object>;if(link==null){lang="en";link=Value(links,"enwiki") as Dictionary<string,object>;}if(link==null)return null;
    string title=Text(link,"title",250);var root=Map(InformationHttp.Json(new Uri("https://"+lang+".wikipedia.org/w/api.php?action=query&format=json&prop=extracts%7Cinfo%7Crevisions%7Cpageprops&meta=siteinfo&siprop=rights&exintro=true&explaintext=true&exchars=10000&inprop=url&rvprop=ids&titles="+Uri.EscapeDataString(title)+"&maxlag=5"),token));
    var result=Map(Value(root,"query"));var licence=Map(Value(result,"rights"));string licenceUrl=Text(licence,"url",300);if(licenceUrl!=null&&licenceUrl.StartsWith("//",StringComparison.Ordinal))licenceUrl="https:"+licenceUrl;if(licenceUrl!=null&&licenceUrl.StartsWith("http://creativecommons.org/",StringComparison.Ordinal))licenceUrl="https://"+licenceUrl.Substring(7);if(licenceUrl!="https://creativecommons.org/licenses/by-sa/4.0/"&&licenceUrl!="https://creativecommons.org/licenses/by-sa/4.0")return null;
    var pages=Map(Value(result,"pages"));if(pages.Count!=1)return null;var page=Map(pages.Values.First());if(Value(page,"missing")!=null)return null;
    var props=Map(Value(page,"pageprops"));if(Text(props,"wikibase_item",40)!=qid)return null;
    string text=Plain(Text(page,"extract")),url=Text(page,"fullurl",1000);if(string.IsNullOrWhiteSpace(text))return null;
    var revision=Map(ArrayValue(page,"revisions",1)[0]);string rev=Convert.ToString(Value(revision,"revid"),CultureInfo.InvariantCulture);if(!Regex.IsMatch(rev,@"^\d+$"))return null;
    DateTime now=DateTime.UtcNow;var rights=new InformationRights{provider="Wikipedia",dataset="introductions",source_url=url,source_revision=rev,retrieved_at=now.ToString("o"),licence_id="CC-BY-SA",licence_version="4.0",licence_url=licenceUrl,creator="Wikipedia contributors",full_attribution="Wikipedia contributors, “"+title+"”, revision "+rev+". "+url+"?oldid="+rev+" · history: "+url+"?action=history · CC BY-SA 4.0",modifications="Plain-text introduction; whitespace normalized. Adaptations retain CC BY-SA 4.0.",rights_evidence="MediaWiki siteinfo rights URL: "+licenceUrl};
    var identityRights=new InformationRights{provider="Wikidata",dataset="identifiers",source_url="https://www.wikidata.org/wiki/"+qid,source_revision=Convert.ToString(Value(entity,"lastrevid"),CultureInfo.InvariantCulture),retrieved_at=now.ToString("o"),licence_id="CC0",licence_version="1.0",licence_url="https://creativecommons.org/publicdomain/zero/1.0/",creator="Wikidata contributors",full_attribution="Wikidata "+qid+" · exact P528 "+ident,modifications="Selected verified article link.",rights_evidence="https://www.wikidata.org/wiki/Wikidata:Licensing"};
    var record=new DownloadedInformation{provider="Wikipedia",object_id=card.id,provider_id=qid,language=language,revision=qid+":"+rev,summary=text,text_rights=rights,identity_rights=identityRights,retrieved_at=now.ToString("o"),expires_at=now.AddDays(30).ToString("o")};
    if(!record.Valid(card.id))return null;try{InformationMedia.Add(card,record,token);}catch(OperationCanceledException){throw;}catch(Exception){}return record;
   }finally{wiki.Release();}
  }
  public static DownloadedInformation ReviewedMedia(TargetInfoCard card,string language,CancellationToken token){
   DateTime now=DateTime.UtcNow;var record=new DownloadedInformation{provider="Wikipedia",object_id=card.id,provider_id=card.id,language=language,revision="reviewed-media",retrieved_at=now.ToString("o"),expires_at=now.AddDays(90).ToString("o")};InformationMedia.Add(card,record,token);
   if(record.image!=null&&record.image.rights!=null)record.revision="media:"+Util.HashText(record.image.rights.source_url+"|"+record.image.rights.source_revision);return record.Valid(card.id)?record:null;
  }
  public static string Plain(string value){if(value==null)return null;return Regex.Replace(Regex.Replace(WebUtility.HtmlDecode(value),@"<[^>]{0,8000}>"," "),@"[\x00-\x08\x0B\x0C\x0E-\x1F]","").Trim();}
 }
}
