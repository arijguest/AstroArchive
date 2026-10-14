using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
namespace AstroArchive {
 public sealed class ObservingCity {
  public int Id;public string Name,Ascii,CountryCode,Country,Region;public double Latitude,Longitude;public long Population;internal string SearchKey,NameKey,AsciiKey;
  public string Label{get{return Name+" · "+(string.IsNullOrEmpty(Region)?"":Region+" · ")+Country;}}
  public override string ToString(){return Label;}
 }
 public static class ObservingCities {
  static readonly Lazy<List<ObservingCity>> cities=new Lazy<List<ObservingCity>>(Load);
  public static List<ObservingCity> All{get{return cities.Value;}}
  public static bool ValidCoordinates(double latitude,double longitude){return !double.IsNaN(latitude)&&!double.IsInfinity(latitude)&&!double.IsNaN(longitude)&&!double.IsInfinity(longitude)&&Math.Abs(latitude)<=90&&Math.Abs(longitude)<=180;}
  public static string Key(string text){var result=new StringBuilder();bool space=false;foreach(char c in (text??"").Normalize(NormalizationForm.FormD)){
    if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;
    if(char.IsLetterOrDigit(c)){if(space&&result.Length>0)result.Append(' ');space=false;result.Append(char.ToUpperInvariant(c));}else space=true;
   }return result.ToString();}
  static List<ObservingCity> Load(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("cities.tsv.gz")){if(stream==null)throw new InvalidOperationException("The offline town/city catalogue is missing.");using(var gzip=new GZipStream(stream,CompressionMode.Decompress))using(var reader=new StreamReader(gzip,Encoding.UTF8))return Read(reader);}}
  internal static List<ObservingCity> Read(TextReader reader){
   var result=new List<ObservingCity>();var shared=new Dictionary<string,string>();Func<string,string> intern=s=>{string stored;if(!shared.TryGetValue(s,out stored))shared[s]=stored=s;return stored;};
   reader.ReadLine();string line;while((line=reader.ReadLine())!=null){var fields=line.Split('\t');int id;long population;double latitude,longitude;
    if(fields.Length!=10||!int.TryParse(fields[0],out id)||!long.TryParse(fields[8],out population)||!double.TryParse(fields[6],NumberStyles.Float,CultureInfo.InvariantCulture,out latitude)||!double.TryParse(fields[7],NumberStyles.Float,CultureInfo.InvariantCulture,out longitude)||!ValidCoordinates(latitude,longitude))throw new InvalidDataException("Invalid offline town/city record.");
    var city=new ObservingCity{Id=id,Name=fields[1],Ascii=fields[2],CountryCode=intern(fields[3]),Country=intern(fields[4]),Region=intern(fields[5]),Latitude=latitude,Longitude=longitude,Population=population};
    city.NameKey=Key(city.Name);city.AsciiKey=Key(city.Ascii);string aliases=city.CountryCode=="GB"?"UK Britain Great Britain":city.CountryCode=="US"?"USA United States America":"";
    city.SearchKey="|"+city.NameKey+"|"+city.AsciiKey+"|"+string.Join("|",fields[9].Split(',').Select(Key))+"| "+Key(city.Country+" "+city.Region+" "+city.CountryCode+" "+aliases);result.Add(city);
   }return result;
  }
  public static List<ObservingCity> Search(string query,int limit=80){string key=Key(query);if(key.Length<2)return new List<ObservingCity>();var terms=key.Split(' ');
   return All.Where(c=>terms.All(t=>t.Length>2?c.SearchKey.Contains(t):c.NameKey.StartsWith(t,StringComparison.Ordinal)||c.AsciiKey.StartsWith(t,StringComparison.Ordinal)||HasWord(c.SearchKey,t))).OrderBy(c=>Rank(c,terms)).ThenByDescending(c=>c.Population).ThenBy(c=>c.Label,StringComparer.Ordinal).Take(limit).ToList();
  }
  static bool HasWord(string text,string word){int position=0;while((position=text.IndexOf(word,position,StringComparison.Ordinal))>=0){int end=position+word.Length;if((position==0||!char.IsLetterOrDigit(text[position-1]))&&(end==text.Length||!char.IsLetterOrDigit(text[end])))return true;position=end;}return false;}
  static int Rank(ObservingCity city,string[] terms){string prefix="";foreach(string term in terms){prefix=prefix.Length==0?term:prefix+" "+term;if(city.SearchKey.Contains("|"+prefix+"|"))return 0;}return city.SearchKey.Contains("|"+terms[0])?1:2;}
 }
 // Settings are edited in a draft so canceling a dialog retains the previous site.
 public sealed class ObservingSiteChoice {
  public int? CityId;public string CityLabel,CountryCode;public double? Latitude,Longitude;
  public ObservingSiteChoice(Settings settings){CityId=settings.ObservingCityId;CityLabel=settings.ObservingCity;CountryCode=settings.ObservingCountryCode;Latitude=settings.Latitude;Longitude=settings.Longitude;}
  public string Label{get{return !string.IsNullOrEmpty(CityLabel)?CityLabel:Latitude.HasValue&&Longitude.HasValue?"Previously saved location":"Not selected";}}
  public void Select(ObservingCity city){if(city==null||!ObservingCities.ValidCoordinates(city.Latitude,city.Longitude))throw new ArgumentException("Choose a valid town or city.");CityId=city.Id;CityLabel=city.Label;CountryCode=city.CountryCode;Latitude=city.Latitude;Longitude=city.Longitude;}
  public void Clear(){CityId=null;CityLabel=null;CountryCode=null;Latitude=null;Longitude=null;}
  public void Save(Settings settings){settings.ObservingCityId=CityId;settings.ObservingCity=CityLabel;settings.ObservingCountryCode=CountryCode;settings.Latitude=Latitude;settings.Longitude=Longitude;}
 }
 // Where a capture was taken: a town/city the user chose for the file, then
 // coordinates recorded in its metadata, then the observing town in Settings.
 public sealed class CaptureSite {
  public double Latitude,Longitude;public int? CityId;public string Label;public bool Chosen,Recorded;
  static bool Valid(double? latitude,double? longitude){return latitude.HasValue&&longitude.HasValue&&ObservingCities.ValidCoordinates(latitude.Value,longitude.Value);}
  public static bool HasChoice(Frame frame){return frame!=null&&Valid(frame.CaptureLatitude,frame.CaptureLongitude);}
  public static CaptureSite For(Frame frame,Settings settings){
   if(HasChoice(frame))return new CaptureSite{Latitude=frame.CaptureLatitude.Value,Longitude=frame.CaptureLongitude.Value,CityId=frame.CaptureCityId,Label=string.IsNullOrEmpty(frame.CaptureCity)?"Chosen capture town / city":frame.CaptureCity,Chosen=true};
   if(frame!=null&&Valid(frame.Latitude,frame.Longitude))return new CaptureSite{Latitude=frame.Latitude.Value,Longitude=frame.Longitude.Value,Label="Capture location",Recorded=true};
   if(settings!=null&&Valid(settings.Latitude,settings.Longitude))return new CaptureSite{Latitude=settings.Latitude.Value,Longitude=settings.Longitude.Value,CityId=settings.ObservingCityId,Label=settings.ObservingCity??"Saved observing location"};
   return null;
  }
  // The town whose timezone rules convert an unzoned capture clock.
  public static int? ZoneCity(Frame frame,Settings settings){if(HasChoice(frame)&&frame.CaptureCityId.HasValue)return frame.CaptureCityId;return settings==null?null:settings.ObservingCityId;}
  public static string Describe(Frame frame,Settings settings){var site=For(frame,settings);return site==null?"Not set":site.Chosen?site.Label:site.Recorded?"Recorded in file metadata":site.Label+" · Settings default";}
  // Null clears the choice so the file falls back to its metadata or Settings.
  public static void Apply(Frame frame,ObservingCity city){
   if(city!=null&&!ObservingCities.ValidCoordinates(city.Latitude,city.Longitude))throw new ArgumentException("Choose a valid town or city.");
   frame.CaptureCityId=city==null?(int?)null:city.Id;frame.CaptureCity=city==null?null:city.Label;frame.CaptureLatitude=city==null?(double?)null:city.Latitude;frame.CaptureLongitude=city==null?(double?)null:city.Longitude;
  }
 }
}
