using System;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void TableCatalogTests(){
   Test("Saved layouts survive serialization and schema changes without empty tables",()=>{
    var settings=new Settings{TableLayouts=new System.Collections.Generic.Dictionary<string,ColumnLayout>{
     {"FramesGrid",new ColumnLayout{Order=new System.Collections.Generic.List<string>{"GainText","Removed","OriginalName","GainText"},Visible=new System.Collections.Generic.List<string>{"GainText","Removed","GainText"}}}
    }};
    settings=Util.Deserialize<Settings>(Util.Serialize(settings));
    var layout=ColumnLayout.Resolve(settings.TableLayouts["FramesGrid"],new[]{"OriginalName","ExposureText","GainText","Camera"},new[]{"OriginalName","ExposureText"});
    Check(layout.Order.SequenceEqual(new[]{"GainText","OriginalName","ExposureText","Camera"})&&layout.Visible.SequenceEqual(new[]{"GainText"}),"Saved order, hidden choices or new columns were lost");
    Check(!settings.TableLayouts.ContainsKey("ImportGrid"),"Independent table settings became linked");
    layout=ColumnLayout.Resolve(new ColumnLayout{Visible=new System.Collections.Generic.List<string>{"Removed"}},new[]{"OriginalName","ExposureText"},new[]{"OriginalName","ExposureText"});
    Check(layout.Visible.Count==2,"Invalid preferences left no visible headings");
    Check(Util.Deserialize<Settings>("{}").TableLayouts==null,"Old settings changed the default layout");
   });
   Test("Caldwell IDs resolve consistently in labels filenames search and old archives",()=>{
    foreach(string label in new[]{"C27","c027","C 027","Caldwell 27","Caldwell-027","NGC6888","Crescent Nebula"}){
     Check(Catalog.KnownName(label)=="NGC6888"&&Catalog.CommonName(label)=="Crescent Nebula","Crescent alias missing: "+label);
     var frame=Util.Deserialize<Frame>("{\"Target\":\""+label+"\"}");Check(frame.ObjectId=="NGC6888"&&frame.TargetName=="Crescent Nebula","Legacy target did not show its common name");
    }
    Check(Catalog.TargetFromFilename("Light_Caldwell_027_001.fit")=="NGC6888"&&Catalog.TargetFromFilename("Light_C27_001.fits.gz")=="NGC6888","Caldwell filename requires solving");
    Check(!Catalog.HasFilenameConflict("C27_NGC6888_Crescent_Nebula.fit")&&Catalog.TargetFromFilename("C27_M31.fit")==null,"Same-target aliases conflict or mixed targets were guessed");
    Check(Catalog.Search("C27").First().Name=="NGC6888"&&new CaptureFilters().Apply(new[]{new Frame{Target="NGC6888"}},"C27").Count==1,"Caldwell IDs are missing from catalogue/library search");
    Check(Catalog.TargetFromFilename("Light_CAM27_C270_001.fit")==null&&Catalog.KnownName("C999")==null,"Unknown numbers or camera labels matched Caldwell IDs");
    using(var stream=typeof(Catalog).Assembly.GetManifestResourceStream("catalog.csv"))using(var reader=new StreamReader(stream)){
     reader.ReadLine();string line;int checkedIds=0;
     while((line=reader.ReadLine())!=null){var fields=line.Split(';');if(System.Text.RegularExpressions.Regex.IsMatch(fields[0],@"^C\d+$")){Check(Catalog.KnownName("Caldwell "+int.Parse(fields[0].Substring(1)))==Catalog.CompactId(fields[0]),"Caldwell-only object did not resolve");checkedIds++;}foreach(string alias in fields[8].Split(',')){var match=System.Text.RegularExpressions.Regex.Match(alias.Trim(),@"^C\s+(\d+)$");if(!match.Success)continue;
       string id="C"+int.Parse(match.Groups[1].Value);Check(Catalog.KnownName(id)==Catalog.KnownName(fields[0]),"Bundled Caldwell ID not resolved: "+id);checkedIds++;
     }}Check(checkedIds==109,"Caldwell coverage changed unexpectedly");
    }
   });
   Test("Expanded common names preserve unambiguous astronomical identities",()=>{
    foreach(var pair in new[]{new[]{"M17","Swan Nebula"},new[]{"M45","Seven Sisters"},new[]{"NGC2359","Thor's Helmet"},new[]{"NGC6334","Cat's Paw Nebula"},new[]{"NGC6960","Western Veil Nebula"},new[]{"IC2177","Seagull Nebula"},new[]{"IC443","Jellyfish Nebula"},new[]{"B33","Horsehead Nebula"},new[]{"C9","Cave Nebula"},new[]{"C14","Double Cluster"},new[]{"C41","Hyades"},new[]{"C99","Coalsack Nebula"}}){
     Check(Catalog.KnownName(pair[1])==pair[0]&&Catalog.CommonName(pair[0]).Length>0,"Common label missing or wrong: "+pair[1]);
     Check(Catalog.TargetFromFilename(pair[1].Replace(' ','_')+"_001.fit")==pair[0],"Common-name filename not recognised: "+pair[1]);
    }
    Check(Catalog.KnownName("C33")=="NGC6992"&&Catalog.KnownName("C34")=="NGC6960","Veil components collapsed");
    Check(Catalog.TargetFromFilename("Crescent_Nebula_Andromeda_Galaxy.fit")==null,"Ambiguous common names selected a target");
   });
  }
 }
}
