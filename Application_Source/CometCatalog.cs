using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AstroArchive {
 // One comet of the bundled NASA/JPL Small-Body Database snapshot: identity, osculating
 // elements (AU, degrees, Julian days TDB), 1-sigma values, MOIDs and observation span.
 public sealed class CometRecord {
  public string Id,SpkId,FullName,Name,OrbitClass,Kind,OrbitId,FirstObservation,LastObservation;
  public double Epoch,E,Q,I,Node,Peri,TimeOfPerihelion;
  public double? MeanAnomaly,PeriodYears,SemiMajorAxis,Aphelion,EarthMoid,JupiterMoid,ArcDays;
  public int Observations;
  public Dictionary<string,string> Sigmas=new Dictionary<string,string>();
 }
 public sealed class MeteorParent {public string Comet,ListedAs,Status;public string[] Sources;}
 public sealed class MeteorShower {public string Code,Name,Peak;public List<MeteorParent> Parents=new List<MeteorParent>();}
 // Offline comet orbits for every comet in the snapshot and the meteor showers they feed.
 // CometCatalog.tsv.gz and MeteorShowers.json are produced by scripts/build-comet-catalog.py.
 // Reviewed values and fresh downloaded orbits keep precedence over this snapshot.
 public static class CometCatalog {
  public const string Snapshot="2026-10-13";
  public const string ShowerSource="NASA/JPL Meteor Streams · NASA Science";
  public const string ShowerUrl="https://ssd.jpl.nasa.gov/sb/meteor_streams.html";
  static readonly Lazy<Dictionary<string,CometRecord>> records=new Lazy<Dictionary<string,CometRecord>>(()=>Load(Open("CometCatalog.tsv.gz")));
  static readonly Lazy<ILookup<string,CometRecord>> names=new Lazy<ILookup<string,CometRecord>>(()=>records.Value.Values.Where(r=>!string.IsNullOrEmpty(r.Name)).ToLookup(r=>NameKey(r.Name)));
  static readonly Lazy<List<MeteorShower>> showers=new Lazy<List<MeteorShower>>(LoadShowers);
  static Stream Open(string resource){return Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);}
  public static int Count{get{return records.Value.Count;}}
  public static IEnumerable<CometRecord> All{get{return records.Value.Values;}}
  public static IList<MeteorShower> Showers{get{return showers.Value;}}
  static string NameKey(string name){return Regex.Replace((name??"").ToUpperInvariant(),@"[^A-Z0-9]+","");}
  static double? Number(string text){double value;return !string.IsNullOrWhiteSpace(text)&&double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!double.IsNaN(value)&&!double.IsInfinity(value)?(double?)value:null;}
  static Dictionary<string,CometRecord> Load(Stream stream){
   var result=new Dictionary<string,CometRecord>(StringComparer.OrdinalIgnoreCase);if(stream==null)return result;
   using(stream)using(var reader=new StreamReader(new GZipStream(stream,CompressionMode.Decompress),System.Text.Encoding.UTF8)){
    var header=(reader.ReadLine()??"").Split('\t');var column=new Dictionary<string,int>();for(int k=0;k<header.Length;k++)column[header[k]]=k;
    string line;while((line=reader.ReadLine())!=null){
     var f=line.Split('\t');Func<string,string> text=key=>{int at;return column.TryGetValue(key,out at)&&at<f.Length&&f[at].Length>0?f[at]:null;};Func<string,double?> number=key=>Number(text(key));
     var e=number("e");var q=number("q");var i=number("i");var node=number("node");var peri=number("peri");var epoch=number("epoch");var tp=number("tp");
     if(text("id")==null||!e.HasValue||!q.HasValue||!i.HasValue||!node.HasValue||!peri.HasValue||!epoch.HasValue||!tp.HasValue)continue;
     string orbitClass=text("orbit_class");if(orbitClass=="JFC")orbitClass="JFc";
     var record=new CometRecord{Id=text("id"),SpkId=text("spkid"),FullName=text("full_name"),Name=text("name"),OrbitClass=orbitClass,Kind=text("kind"),OrbitId=text("orbit_id"),FirstObservation=text("first_obs"),LastObservation=text("last_obs"),
      Epoch=epoch.Value,E=e.Value,Q=q.Value,I=i.Value,Node=node.Value,Peri=peri.Value,TimeOfPerihelion=tp.Value,MeanAnomaly=number("ma"),PeriodYears=number("period_years"),SemiMajorAxis=number("a"),Aphelion=number("aphelion"),EarthMoid=number("moid_earth"),JupiterMoid=number("moid_jupiter"),ArcDays=number("arc_days"),Observations=(int)(number("observations")??0)};
     foreach(string key in new[]{"e","q","i","node","peri","tp"}){string sigma=text("sigma_"+key);if(sigma!=null)record.Sigmas[key]=sigma;}
     result[record.Id]=record;
    }
   }
   return result;
  }
  static List<MeteorShower> LoadShowers(){
   var result=new List<MeteorShower>();using(var stream=Open("MeteorShowers.json")){if(stream==null)return result;
    using(var reader=new StreamReader(stream,System.Text.Encoding.UTF8)){
     var root=new JavaScriptSerializer{MaxJsonLength=262144,RecursionLimit=12}.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());object list;if(!root.TryGetValue("showers",out list))return result;
     foreach(var row in ((System.Collections.IEnumerable)list).Cast<Dictionary<string,object>>()){
      var shower=new MeteorShower{Code=(string)row["code"],Name=(string)row["name"],Peak=row.ContainsKey("peak")?(string)row["peak"]:null};
      foreach(var parent in ((System.Collections.IEnumerable)row["parents"]).Cast<Dictionary<string,object>>())shower.Parents.Add(new MeteorParent{Comet=(string)parent["comet"],ListedAs=(string)parent["listed_as"],Status=(string)parent["status"],Sources=((System.Collections.IEnumerable)parent["sources"]).Cast<object>().Select(s=>(string)s).ToArray()});
      result.Add(shower);
     }
    }
   }
   return result;
  }
  // An exact designation or fragment: "C/2023 A3", "1P", "73P-B". 2I and 3I answer to their provisional names.
  public static CometRecord Find(string id){CometRecord record;return string.IsNullOrWhiteSpace(id)?null:records.Value.TryGetValue(CometInfo.Canonical(id.Trim()),out record)?record:null;}
  // A discoverer or survey name such as "Hale-Bopp" when exactly one comet carries it. Shared names (ATLAS, SOHO, PANSTARRS) stay ambiguous.
  public static CometRecord FindByName(string name){var matches=string.IsNullOrWhiteSpace(name)?null:names.Value[NameKey(name)].ToList();return matches!=null&&matches.Count==1?matches[0]:null;}
  public static string DisplayName(CometRecord record){return record==null?null:string.IsNullOrWhiteSpace(record.FullName)?record.Id:record.FullName;}
  // The complete osculating orbit for the 3D viewer and the target pane, in the form used by downloaded and audited orbits.
  public static OrbitRecord Orbit(string id){return Orbit(Find(id));}
  public static OrbitRecord Orbit(CometRecord comet){
   if(comet==null)return null;string source=CometInfo.LookupUrl(comet.Id);
   var sigmas=new Dictionary<string,string>();string value;
   if(comet.Sigmas.TryGetValue("e",out value))sigmas["e"]=value+" ";if(comet.Sigmas.TryGetValue("q",out value))sigmas["q"]=value+" au";if(comet.Sigmas.TryGetValue("i",out value))sigmas["i"]=value+" deg";
   if(comet.Sigmas.TryGetValue("node",out value))sigmas["om"]=value+" deg";if(comet.Sigmas.TryGetValue("peri",out value))sigmas["w"]=value+" deg";if(comet.Sigmas.TryGetValue("tp",out value))sigmas["tp"]=value+" TDB";
   var record=new OrbitRecord{object_id=comet.Id,provider_id=comet.SpkId,central_body="Sun",reference_plane="ecliptic",coordinate_frame="J2000",time_standard="TDB",epoch=comet.Epoch,e=comet.E,q=comet.Q,i=comet.I,ascending_node=comet.Node,argument_of_perihelion=comet.Peri,mean_anomaly=comet.MeanAnomaly,perihelion_time=comet.TimeOfPerihelion,
    solution_id=comet.OrbitId,orbit_class=comet.OrbitClass,source=source,uncertainties=sigmas,
    rights=OrbitalCatalog.Rights(source,(comet.OrbitId??"JPL")+" @ "+comet.Epoch.ToString("0.#####",CultureInfo.InvariantCulture),new DateTime(2026,10,13,0,0,0,DateTimeKind.Utc),"orbital-elements")};
   record.rights.modifications="Numerical values copied from the bundled 13 October 2026 Small-Body Database snapshot; approximate two-body geometry.";
   return OrbitalModel.Valid(record)?record:null;
  }
  // Showers whose parent is, or is suspected to be, this comet. Fragments are matched on their own designation only.
  public static List<KeyValuePair<MeteorShower,MeteorParent>> ShowersOf(string id){
   var result=new List<KeyValuePair<MeteorShower,MeteorParent>>();string canonical=CometInfo.Canonical((id??"").Trim());if(canonical.Length==0)return result;
   foreach(var shower in showers.Value)foreach(var parent in shower.Parents)if(string.Equals(CometInfo.Canonical(parent.Comet),canonical,StringComparison.OrdinalIgnoreCase))result.Add(new KeyValuePair<MeteorShower,MeteorParent>(shower,parent));
   return result;
  }
  public static string ShowerNames(IEnumerable<KeyValuePair<MeteorShower,MeteorParent>> pairs,bool suspected){return string.Join(", ",pairs.Where(p=>(p.Value.Status!="listed")==suspected).Select(p=>p.Key.Name).Distinct());}
  public static string ShowerEvidence(IEnumerable<KeyValuePair<MeteorShower,MeteorParent>> pairs){
   var peaks=pairs.Where(p=>!string.IsNullOrEmpty(p.Key.Peak)).Select(p=>p.Key.Name+": "+p.Key.Peak).Distinct();
   return "Parent comets as listed by NASA/JPL Meteor Streams and NASA Science. Suspected or candidate parents are marked as such; sources disagree on some. Peaks are approximate, not a forecast. Not listed here does not mean no shower.\n"+string.Join("\n",peaks)+"\n"+ShowerUrl;
  }
 }
}
