using System;
using System.Globalization;
using System.Linq;
namespace AstroArchive {
 // Day-first or month-first dates for what people read in the app. Stored and exported dates stay ISO.
 // Automatic follows the observing location (United States: month first) until a format is chosen in Preferences.
 public static class DateFormats {
  public const string Automatic="Automatic (from observing location)",DayFirst="DD/MM/YY",MonthFirst="MM/DD/YY";
  public static readonly string[] Choices={Automatic,DayFirst,MonthFirst};
  static string choice=Automatic,country;static int? pendingCity;static readonly object gate=new object();
  public static void Configure(Settings settings){
   lock(gate){
    choice=settings==null?Automatic:Normalise(settings.DateFormat);country=settings==null?null:settings.ObservingCountryCode;
    // Settings saved before the country was stored only have the city; look it up once, when first needed.
    pendingCity=string.IsNullOrEmpty(country)&&settings!=null?settings.ObservingCityId:null;
   }
  }
  public static string Normalise(string value){return value==DayFirst||value==MonthFirst?value:Automatic;}
  public static bool MonthFirstNow{get{return Resolve()==MonthFirst;}}
  public static string Resolve(string explicitChoice,string countryCode){
   explicitChoice=Normalise(explicitChoice);if(explicitChoice!=Automatic)return explicitChoice;
   return string.Equals(countryCode,"US",StringComparison.OrdinalIgnoreCase)?MonthFirst:DayFirst;
  }
  public static string Resolve(){
   lock(gate){
    if(pendingCity.HasValue){var city=ObservingCities.All.FirstOrDefault(c=>c.Id==pendingCity.Value);country=city==null?null:city.CountryCode;pendingCity=null;}
    return Resolve(choice,country);
   }
  }
  public static string Pattern{get{return MonthFirstNow?"MM/dd/yy":"dd/MM/yy";}}
  public static string Format(DateTime date){return date.ToString(Pattern,CultureInfo.InvariantCulture);}
  public static string Format(DateTime date,string time){return Format(date)+" "+time;}
  public static string Hint{get{return MonthFirstNow?"MM/DD/YY":"DD/MM/YY";}}
  // Accepts the chosen order with 2 or 4 digit years, "/", "-" or "." separators, and ISO year-first dates.
  public static bool TryParse(string text,out DateTime date){
   date=default(DateTime);text=(text??"").Trim().Replace('-','/').Replace('.','/');if(text.Length==0)return false;
   string[] iso={"yyyy/M/d"},first=MonthFirstNow?new[]{"M/d/yy","M/d/yyyy"}:new[]{"d/M/yy","d/M/yyyy"};
   return DateTime.TryParseExact(text,iso,CultureInfo.InvariantCulture,DateTimeStyles.None,out date)||DateTime.TryParseExact(text,first,CultureInfo.InvariantCulture,DateTimeStyles.None,out date);
  }
 }
}
