using System;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void TableCatalogTests(){
   Test("Older Edited headings adopt File Type once and preserve later column choices",()=>{
    var old=new ColumnLayout{Order=new System.Collections.Generic.List<string>{"Kind","Filename","Metadata.TotalExposureText","Source"},Visible=new System.Collections.Generic.List<string>{"Kind","Filename","Metadata.TotalExposureText","Source"}};
    var upgraded=ColumnLayout.UpgradeEdited(old);Check(upgraded.Order.SequenceEqual(new[]{"Kind","Filename","FileType","Metadata.TotalExposureText","Source"})&&upgraded.Visible.Contains("FileType")&&!upgraded.Visible.Contains("Metadata.TotalExposureText")&&upgraded.Visible.Contains("Source"),"Edited upgrade lost custom order/visibility or retained old headings");
    Check(!old.Order.Contains("FileType")&&old.Visible.Contains("Metadata.TotalExposureText"),"Upgrade mutated original layout");upgraded.Visible.Add("Metadata.TotalExposureText");Check(object.ReferenceEquals(upgraded,ColumnLayout.UpgradeEdited(upgraded))&&upgraded.Visible.Contains("Metadata.TotalExposureText"),"Repeated upgrade overwrote a later user choice");
    var settings=Util.Deserialize<Settings>(Util.Serialize(new Settings{TableLayouts=new System.Collections.Generic.Dictionary<string,ColumnLayout>{{"EditedGrid",upgraded}}}));Check(ColumnLayout.UpgradeEdited(settings.TableLayouts["EditedGrid"]).Visible.Contains("Metadata.TotalExposureText"),"Upgrade marker did not survive settings serialization");
   });
   Test("Stack exposure preserves reported FITS values and separates exposure gain and real sub counts",()=>{
    string directory=Path.Combine(root,"stack-exposure");Directory.CreateDirectory(directory);
    var names=new[]{"M 101_30s40_Astro_20260522-013922633","Elephant's Trunk Nebula(1)_60s40_Astro_20261003-214231776","Heart Nebula_10s60_Astro_20260909-005516848","Elephant's Trunk Nebula(2)_60s40_Astro_20261003-220509309","M 31_Astro_20260816-004149349","M 31_30s40_Astro_20260815-230831858","NGC 7380_10s60_Duo-Band_20260908-214801527","C 20_30s40_Duo-Band_20260904-214653515","M 101_30s40_Astro_20260522-003541497","M 31_Astro_20260906-174558740","HD 237015_Astro_20260910-214058719","M 101_Astro_20260522-000957161","HD 237015_10s60_Astro_20260909-234054137","Soul Nebula_10s60_Duo-Band_20261006-010341085","M 101_30s40_Astro_20260521-233238543"};
    var exposures=new[]{7140,5880,5710,5160,4620,3990,3900,3810,2790,2730,2360,2310,2100,1950,1680};
    for(int i=0;i<names.Length;i++){
     string path=Path.Combine(directory,"stacked-16_"+names[i]+".fits");Write(path,64,48,(x,y)=>1000,new System.Collections.Generic.Dictionary<string,string>{{"EXPTIME",exposures[i].ToString(System.Globalization.CultureInfo.InvariantCulture)},{"INSTRUME","'DWARF 3'"}});
     var frame=Classifier.Read(path,directory,"Dwarf-03","Auto");Check(frame.Kind=="Stack"&&frame.Exposure==exposures[i]&&frame.StackCount==0,"Filename numbers replaced header exposure or invented a sub-count: "+names[i]);
     Check(frame.ExposureTooltip.Contains("EXPTIME = "+exposures[i]),"Saved FITS exposure source missing from tooltip");
     var legacy=Util.Deserialize<Frame>(Util.Serialize(frame));legacy.Facts=null;Check(legacy.ExposureTooltip.Contains("EXPTIME = "+exposures[i]),"Existing archive image headers lost exposure evidence");
     double? sub,gain;if(Classifier.FilenameExposureGain(path,out sub,out gain)){Check(frame.Gain==gain&&frame.ExposureTooltip.Contains(Util.Num(sub)+" s per sub"),"Exposure/gain settings not separated from integration");var edited=EditedMetadata.Read(path,Fits.Header(path));Check(edited.SubExposure==sub&&!edited.Subs.HasValue&&!edited.TotalExposure.HasValue&&edited.ReportedExposure==exposures[i],"Edited import guessed a count or multiplied an unspecified header exposure");}
    }
    string counted=Path.Combine(directory,"Stacked_M106_16x10s.fits");Write(counted,64,48,(x,y)=>1000,new System.Collections.Generic.Dictionary<string,string>());var countedFrame=Classifier.Read(counted,directory,"Unit-01","Auto");Check(countedFrame.StackCount==16&&countedFrame.Exposure==10,"Explicit count × duration filename lost its sub-count");Check(EditedMetadata.Read(counted,Fits.Header(counted)).TotalExposure==160,"Sixteen confirmed ten-second subs did not total 160 seconds");
    string headerCount=Path.Combine(directory,"stacked-16_M106_10s60.fits");Write(headerCount,64,48,(x,y)=>1000,new System.Collections.Generic.Dictionary<string,string>{{"EXPTIME","160"},{"NCOMBINE","16"},{"GAIN","45"}});var confirmed=Classifier.Read(headerCount,directory,"Dwarf-03","Auto");Check(confirmed.Exposure==160&&confirmed.StackCount==16&&confirmed.Gain==45&&confirmed.ExposureTooltip.Contains("Combined frames: 16"),"Header count/gain precedence or exposure changed");var confirmedEdit=EditedMetadata.Read(headerCount,Fits.Header(headerCount));Check(confirmedEdit.Subs==16&&confirmedEdit.SubExposure==10&&confirmedEdit.TotalExposure==160,"Confirmed header count and filename duration did not recover edited total");
    Write(headerCount,64,48,(x,y)=>1000,new System.Collections.Generic.Dictionary<string,string>{{"NCOMBINE","16.5"}});Check(Classifier.Read(headerCount,directory,"Dwarf-03","Auto").StackCount==0,"Fractional stack count was truncated");
    double? mixedSub,mixedGain;Check(!Classifier.FilenameExposureGain("M106_30s40_10s60.fits",out mixedSub,out mixedGain)&&!EditedMetadata.Read("M106_30s40_10s60_starless.fits",null).SubExposure.HasValue,"Conflicting filename settings gained a single sub duration");
    string aliasCount=Path.Combine(directory,"M106.fits");Write(aliasCount,64,48,(x,y)=>1000,new System.Collections.Generic.Dictionary<string,string>{{"NSUBS","16"}});var aliasFrame=Classifier.Read(aliasCount,directory,"Unit-01","Auto");Check(aliasFrame.Kind=="Stack"&&aliasFrame.StackCount==16,"Recognised combined-frame alias did not classify the stack");
   });
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
   Test("Catalogue aliases consolidate one object while keeping catalogue prefixes distinct",()=>{
    foreach(string label in new[]{"M106","M 106","NGC4258","NGC 04258","UGC7353","UGC 07353","PGC39600","PGC 039600"}){
     Check(Catalog.KnownName(label)=="M106"&&Catalog.CanonicalTarget(label)=="M106","M106 cross-catalogue alias missed: "+label);
     var frame=Util.Deserialize<Frame>("{\"Target\":\""+label+"\"}");Check(frame.ObjectId=="M106","Existing archive retained alternate ID: "+label);
     Check(Catalog.TargetFromFilename("Light_"+label.Replace(' ','_')+"_001.fit")=="M106","Filename alias missed: "+label);
     Check(new CaptureFilters().Apply(new[]{new Frame{Target="M106"}},label).Count==1,"Search missed alternate ID: "+label);
    }
    Check(Catalog.TargetFromFilename("M106_NGC4258_UGC7353_PGC39600.fit")=="M106"&&!Catalog.HasFilenameConflict("M106_NGC4258.fit"),"Equivalent catalogue aliases were treated as different objects");
    Check(Catalog.CanonicalTarget("NGC106")=="NGC106"&&Catalog.KnownName("NGC106")!="M106"&&Catalog.HasFilenameConflict("M106_NGC106.fit")&&Catalog.TargetFromFilename("M106_NGC106.fit")==null,"Equal numeric IDs across catalogues were merged");
    Check(Catalog.KnownName("106")==null&&Catalog.TargetFromFilename("capture_106.fit")==null,"Bare numbers gained catalogue identities");
    var targets=TargetNavigation.Build(new[]{new Frame{Target="M106"},new Frame{Target="NGC4258"},new Frame{Target="UGC7353"},new Frame{Target="NGC106"}});Check(targets.Single(t=>t.Name=="M106").Files==3&&targets.Single(t=>t.Name=="NGC106").Files==1,"Navigation split aliases or combined distinct objects");
    Check(EditedMetadata.Read("NGC4258_PGC39600_starless.fit",null).Object=="M106","Edited import did not share catalogue identities");
   });
   Test("Bundled PGC and UGC aliases resolve only when one astronomical identity owns them",()=>{
    var ownership=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    using(var stream=typeof(Catalog).Assembly.GetManifestResourceStream("catalog.csv"))using(var reader=new StreamReader(stream)){reader.ReadLine();string line;while((line=reader.ReadLine())!=null){var fields=line.Split(';');string target=Catalog.CanonicalTarget(fields[0]);foreach(string alias in fields[8].Split(',')){
     if(!System.Text.RegularExpressions.Regex.IsMatch(alias.Trim(),@"^(PGC|UGC)\s*\d+[A-Z]?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;
     string key=Catalog.CompactId(alias.Trim());System.Collections.Generic.HashSet<string> owners;if(!ownership.TryGetValue(key,out owners))ownership[key]=owners=new System.Collections.Generic.HashSet<string>();owners.Add(target);
    }}}
    Check(ownership.Count>1000,"Too few catalogue aliases checked");foreach(var alias in ownership){string resolved=Catalog.KnownName(alias.Key);Check(alias.Value.Count==1?resolved==alias.Value.Single():resolved==null,"Ambiguous or missing bundled alias: "+alias.Key);if(alias.Value.Count>1)Check(Catalog.ObjectId(alias.Key)==""&&Catalog.Normalize(alias.Key)=="Unknown","Ambiguous alias acquired an object ID: "+alias.Key);}
   });
   Test("IC colloquial names work in existing labels searches and capture filenames",()=>{
    foreach(var pair in new[]{new[]{"IC63","Ghost of Cassiopeia"},new[]{"IC410","Tadpole Nebula"},new[]{"IC418","Spirograph Nebula"},new[]{"NGC1909","Witch Head Nebula"},new[]{"IC2391","Omicron Velorum Cluster"},new[]{"IC2602","Southern Pleiades"},new[]{"IC2944","Running Chicken Nebula"},new[]{"IC3568","Lemon Slice Nebula"},new[]{"IC4406","Retina Nebula"},new[]{"IC4592","Blue Horsehead Nebula"},new[]{"IC4604","Rho Ophiuchi Nebula"},new[]{"IC4665","Summer Beehive Cluster"},new[]{"IC4756","Graff's Cluster"}}){
     Check(Catalog.KnownName(pair[1])==pair[0]&&Catalog.CommonName(pair[0])==pair[1],"IC common name missing: "+pair[1]);
     Check(Catalog.TargetFromFilename("Light_"+pair[1].Replace(' ','_')+"_001.fit")==pair[0],"Common-name filename conflicts with a shorter name: "+pair[1]);
     var frame=Util.Deserialize<Frame>("{\"Target\":\""+pair[0]+"\"}");Check(frame.TargetName==pair[1]&&new CaptureFilters().Apply(new[]{frame},pair[1]).Count==1,"Existing capture does not display/search its IC nickname");
     Check(Catalog.Search(pair[1]).Any(item=>item.Name==pair[0]),"Catalogue search missed "+pair[1]);
    }
    Check(Catalog.KnownName("IC 0063")=="IC63"&&Catalog.KnownName("Cassiopeia's Ghost")=="IC63"&&Catalog.KnownName("IC2118")=="NGC1909","Alternate IC names/IDs lost their canonical identity");
    Check(Catalog.KnownName("IC59")!="IC63"&&Catalog.KnownName("Horsehead Nebula")=="B33"&&Catalog.KnownName("Pleiades")=="M45","IC nicknames merged different astronomical objects");
    Check(Catalog.TargetFromFilename("B33_Blue_Horsehead_Nebula.fit")==null&&Catalog.TargetFromFilename("Southern_Pleiades_M45.fit")==null&&Catalog.TargetFromFilename("Southern_Pleiades_and_Pleiades.fit")==null,"Explicit mixed-object filenames acquired a single target");
    Check(Catalog.CommonName("IC405")=="Flaming Star Nebula"&&Catalog.CommonName("IC5146")=="Cocoon Nebula"&&Catalog.CommonName("IC2220")=="Toby Jug Nebula","Existing IC common names changed unexpectedly");
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
