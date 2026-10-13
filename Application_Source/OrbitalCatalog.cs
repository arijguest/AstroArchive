using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace AstroArchive {
 public static class OrbitalCatalog {
  static readonly Dictionary<string,string> small=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"CERES","1"},{"PLUTO","134340"},{"ERIS","136199"},{"HAUMEA","136108"},{"MAKEMAKE","136472"}};
  static readonly Lazy<Dictionary<string,OrbitRecord>> essentials=new Lazy<Dictionary<string,OrbitRecord>>(()=>{
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OrbitEssentials.json")){if(stream==null)return new Dictionary<string,OrbitRecord>();using(var reader=new StreamReader(stream))return new JavaScriptSerializer{MaxJsonLength=262144,RecursionLimit=16}.Deserialize<Dictionary<string,OrbitRecord>>(reader.ReadToEnd());}
  });
  public static string ProviderId(string id){string result;if(small.TryGetValue(id??"",out result))return result;
   if(Regex.IsMatch(id??"",@"^(?:\d{1,4}[PDI](?:-[A-Z0-9]+)?|[CPDXAI]/\d{4} [A-Z]{1,2}\d{1,3}(?:-[A-Z0-9]+)?)$"))return id;return null;
  }
  public static InformationRights Rights(string source,string revision,DateTime retrieved,string dataset){return new InformationRights{provider="JPL",dataset=dataset,source_url=source,source_revision=revision,retrieved_at=retrieved.ToUniversalTime().ToString("o"),licence_id="numerical-facts",licence_version="not applicable",licence_url="https://www.jpl.nasa.gov/caltechjpl-privacy-policies-and-important-notices/",creator="NASA/JPL Solar System Dynamics",full_attribution="NASA/JPL Solar System Dynamics · numerical orbital elements · "+source,modifications="Numerical values projected to AU, degrees and Julian days; approximate two-body geometry.",rights_evidence="Public scientific numerical facts, not JPL prose, images or software; 17 USC 102(b). API fair-use policy is observed separately: https://ssd-api.jpl.nasa.gov/doc/index.php"};}
  public static OrbitRecord For(TargetInfoCard card){
   if(card==null||card.domain!="solar_system")return null;if(OrbitalModel.Valid(card.orbit))return card.orbit;
   var planet=PlanetEphemeris.Orbit(card.id,new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc));if(planet!=null)return planet;
   OrbitRecord record;return essentials.Value.TryGetValue(card.id,out record)&&OrbitalModel.Valid(record)?record:null;
  }
  // The eight planets, nearest the Sun first. Their orbits are the context drawn around other targets.
  public static readonly string[] Planets={"MERCURY","VENUS","EARTH","MARS","JUPITER","SATURN","URANUS","NEPTUNE"};
  public static string PlanetName(string id){return string.IsNullOrEmpty(id)?id:id.Substring(0,1).ToUpperInvariant()+id.Substring(1).ToLowerInvariant();}
  public static OrbitScene Scene(OrbitRecord orbit,CancellationToken token){
   if(!OrbitalModel.Valid(orbit))return null;var scene=new OrbitScene{Record=orbit,Full=OrbitalModel.Path(orbit,false,token),Inner=OrbitalModel.Path(orbit,true,token)};
   // A planet's own path carries its name too, so the solar-system overview can colour all eight alike.
   if(Array.IndexOf(Planets,orbit.object_id)>=0){string own=PlanetName(orbit.object_id);scene.Full.Name=own;scene.Inner.Name=own;}
   if(orbit.central_body=="Sun")foreach(string id in Planets){
    token.ThrowIfCancellationRequested();if(id==orbit.object_id)continue;var context=PlanetEphemeris.Orbit(id,new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc));var path=OrbitalModel.Path(context,false,token);if(path!=null)path.Name=PlanetName(id);scene.Context.Add(path);}
   return scene;
  }
 }
}
