using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace AstroArchive {
 // A compact index keyed by the selected GeoNames city ID. It avoids loading
 // the full place/search catalogue just to resolve an unzoned capture clock.
 public static class CityTimeZones {
  sealed class Zone {public string Iana,Windows;public TimeZoneInfo Rules;public bool Loaded;}
  static readonly Lazy<Dictionary<int,Zone>> cities=new Lazy<Dictionary<int,Zone>>(Load);
  static Dictionary<int,Zone> Load(){
   var result=new Dictionary<int,Zone>();var shared=new Dictionary<string,Zone>();
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("city-timezones.tsv.gz")){
    if(stream==null)throw new InvalidOperationException("The offline city timezone index is missing.");
    using(var gzip=new GZipStream(stream,CompressionMode.Decompress))using(var reader=new StreamReader(gzip,Encoding.UTF8)){
     reader.ReadLine();string line;while((line=reader.ReadLine())!=null){var fields=line.Split('\t');int id;if(fields.Length!=3||!int.TryParse(fields[0],NumberStyles.None,CultureInfo.InvariantCulture,out id)||fields[1].Length==0||fields[2].Length==0)throw new InvalidDataException("Invalid city timezone record.");
      Zone zone;if(!shared.TryGetValue(fields[1],out zone)){zone=new Zone{Iana=fields[1],Windows=fields[2]};shared[fields[1]]=zone;}result.Add(id,zone);
     }
    }
   }return result;
  }
  public static TimeZoneInfo ForCity(int cityId){
   Zone zone;if(!cities.Value.TryGetValue(cityId,out zone))return null;
   lock(zone){if(!zone.Loaded){
    // .NET Framework on Windows needs CLDR's Windows ID; Unix tests use IANA.
    string[] ids=Environment.OSVersion.Platform==PlatformID.Win32NT?new[]{zone.Windows,zone.Iana}:new[]{zone.Iana,zone.Windows};
    foreach(string id in ids)try{zone.Rules=TimeZoneInfo.FindSystemTimeZoneById(id);break;}catch(TimeZoneNotFoundException){}catch(InvalidTimeZoneException){}
    zone.Loaded=true;
   }return zone.Rules;}
  }
 }
}
