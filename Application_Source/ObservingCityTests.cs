using System;
using System.Globalization;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void ObservingCityTests(){
   Test("Offline city catalogue resolves regions accents aliases and coordinate signs",()=>{
    var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
     Check(ObservingCities.All.Count>230000&&ObservingCities.All.All(c=>ObservingCities.ValidCoordinates(c.Latitude,c.Longitude)),"City catalogue incomplete or coordinates invalid");
     var london=ObservingCities.Search("London UK").First();Check(london.Id==2643743&&london.CountryCode=="GB"&&Math.Abs(london.Latitude-51.50853)<0.00001&&london.Longitude<0,"Wrong London or locale changed longitude");
     var british=ObservingCities.Search("Cambridge UK").First();var american=ObservingCities.Search("Cambridge Massachusetts USA").First();
     Check(british.Id==2653941&&american.Id==4931972&&british.Label.Contains("United Kingdom")&&american.Label.Contains("Massachusetts"),"Namesake cities are not disambiguated");
     Check(ObservingCities.Search("São Paulo Brazil").First().Id==3448439&&ObservingCities.Search("Sao Paulo Brazil").First().Id==3448439,"Accent-insensitive city search failed");
     Check(ObservingCities.Search("München Germany").First().Id==2867714,"Alternate place name missing");
     var sydney=ObservingCities.Search("Sydney Australia").First();Check(sydney.Id==2147714&&sydney.Latitude<0&&sydney.Longitude>0,"Southern/eastern hemisphere signs changed");
     Check(ObservingCities.Search("New York USA").First().Id==5128581&&ObservingCities.Search("United States").Count<=80,"Search ranking or result limit incorrect");
     Check(ObservingCities.Search("x").Count==0&&ObservingCities.Search("no-such-town-zzzzzz").Count==0,"Short or unknown queries select a location");
    }finally{CultureInfo.CurrentCulture=previous;}
   });
   Test("Choosing a city is transactional and legacy coordinates remain usable",()=>{
    var settings=Util.Deserialize<Settings>("{\"Latitude\":51.5,\"Longitude\":-0.1}");var draft=new ObservingSiteChoice(settings);
    Check(draft.Label=="Previously saved location"&&draft.Latitude==51.5&&draft.Longitude==-0.1,"Old location silently changed");
    draft.Select(ObservingCities.Search("Sydney Australia").First());Check(settings.Latitude==51.5&&settings.ObservingCity==null,"Picker changed settings before Save");
    draft.Save(settings);var loaded=Util.Deserialize<Settings>(Util.Serialize(settings));Check(loaded.ObservingCityId==2147714&&loaded.ObservingCity.Contains("Sydney")&&loaded.Latitude==draft.Latitude&&loaded.Longitude==draft.Longitude,"Saved city lost its resolved coordinates");
    new ObservingSiteChoice(loaded).Clear();Check(loaded.ObservingCityId==2147714,"Canceling a clear changed saved settings");
    var clear=new ObservingSiteChoice(loaded);clear.Clear();clear.Save(loaded);Check(loaded.ObservingCityId==null&&loaded.ObservingCity==null&&loaded.Latitude==null&&loaded.Longitude==null,"Clear retained the city override instead of allowing capture metadata");
    Expect(()=>draft.Select(new ObservingCity{Latitude=double.NaN,Longitude=0}),"Invalid coordinates accepted from a city");
   });
   Test("Invalid city data is rejected rather than setting an incorrect observing site",()=>{
    Expect(()=>ObservingCities.Read(new StringReader("header\n1\tBad\tBad\tGB\tUnited Kingdom\tEngland\t95\t0\t1000\t")),"Latitude outside physical range accepted");
    Expect(()=>ObservingCities.Read(new StringReader("header\n1\tBad\tBad\tGB\tUnited Kingdom\tEngland\t0\tNaN\t1000\t")),"Non-finite longitude accepted");
   });
  }
 }
}
