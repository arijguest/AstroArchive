using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AstroArchive {
 public partial class Tests {
  static void TargetInfoTests(){
   Test("Image credits format author summaries while retaining full notices, links and rights",()=>{
    var image=new TargetInfoImage{creator="Credit Line and Copyright Alice &amp; Bob",attribution="<p>© Alice &amp; Bob — <a href=\"https://observatory.example/image?a=1&amp;b=2\">Observatory</a></p>",licence="CC-BY-SA-4.0",file_page_url="https://commons.wikimedia.org/wiki/File:Example.jpg",modifications="<p>Native processing note.</p>",rights=new InformationRights{creator="Rights creator",full_attribution="Required additional contributor credit.",licence_id="CC-BY-SA",licence_version="4.0",licence_url="https://creativecommons.org/licenses/by-sa/4.0/",modifications="Separate rights processing note."}};
    string original=image.creator,attribution=image.attribution;var shown=TargetInfoPresentation.Credits(image);
    Check(shown.Creator=="Alice & Bob"&&shown.Licence=="CC BY-SA 4.0"&&shown.LicenceUrl==image.rights.licence_url,"Author summary or licence formatting is wrong");
    Check(shown.Attributions.Any(value=>value.Contains("© Alice & Bob")&&value.Contains("https://observatory.example/image?a=1&b=2"))&&shown.Attributions.Contains(image.rights.full_attribution)&&shown.Attributions.Any(value=>value.Contains("Copyright Alice & Bob")),"Required notice, contributor or source link lost");
    Check(shown.Modifications.Length==2&&shown.Modifications.Contains("Native processing note.")&&shown.Modifications.Contains(image.rights.modifications),"Native and rights processing notes did not merge");
    Check(image.creator==original&&image.attribution==attribution,"Presentation rewrote stored attribution metadata");
    Check(TargetInfoPresentation.Credits(new TargetInfoImage{creator="en:user:Friendlystar"}).Creator=="Friendlystar"&&TargetInfoPresentation.Credits(new TargetInfoImage{creator="Credits: ESA/Hubble & NASA"}).Creator=="ESA/Hubble & NASA","Creator labels or Commons namespace leaked into the author summary");
    Check(TargetInfoPresentation.Credits(new TargetInfoImage{creator="Own work,",attribution="Own work,; source/processing credit: Hubble data"}).Creator==""&&TargetInfoPresentation.Credits(new TargetInfoImage{creator="Unknown"}).Creator=="","A placeholder was presented as a known author");
   });
   Test("Image rights supply missing credit fields without inventing a Commons creator",()=>{
    var rights=new InformationRights{creator="A real observatory",full_attribution="Required full credit",source_url="https://observatory.example/image",licence_id="CC-BY",licence_version="3.0",licence_url="https://creativecommons.org/licenses/by/3.0/",modifications="Cropped."};
    var shown=TargetInfoPresentation.Credits(new TargetInfoImage{creator=" ",attribution=" ",rights=rights});
    Check(shown.Creator==rights.creator&&shown.Attributions.Single()==rights.full_attribution&&shown.Licence=="CC BY 3.0"&&shown.SourceUrl==rights.source_url&&shown.LicenceUrl==rights.licence_url&&shown.Modifications.Single()==rights.modifications,"Fallback rights were omitted");
    Check(TargetInfoPresentation.Credits(new TargetInfoImage{creator="Own work,",rights=rights}).Creator==rights.creator,"A placeholder hid the rights record's known creator");
    var distinct=TargetInfoPresentation.Credits(new TargetInfoImage{creator="Primary photographer",rights=rights});Check(distinct.Creator=="Primary photographer"&&distinct.Attributions.Contains(rights.creator),"A distinct rights-record creator was omitted");
    shown=TargetInfoPresentation.Credits(new TargetInfoImage{creator="Unknown",file_page_url="javascript:alert(1)",licence_url="data:text/plain,no",rights=new InformationRights{source_url=rights.source_url,licence_url=rights.licence_url,full_attribution="Known source, unknown photographer."}});
    Check(shown.Creator.Length==0&&shown.SourceUrl==rights.source_url&&shown.LicenceUrl==rights.licence_url,"Unsafe URLs were retained or an author was invented");
   });
   Test("Representation provenance stays in credit details while scientific image captions remain",()=>{
    var earth=new TargetInfoImage{representation="Visible-light Apollo 17 photograph; NASA prepared version."};var shown=TargetInfoPresentation.Credits(earth);
    Check(shown.Caption=="Visible-light Apollo 17 photograph."&&shown.RepresentationCredits.Single()==earth.representation,"NASA preparation provenance remains outside credits or disappeared");
    var hubble=new TargetInfoImage{representation="Hubble Space Telescope image released as ESA/Hubble Picture of the Week potw1706a; processed colour."};shown=TargetInfoPresentation.Credits(hubble);
    Check(shown.Caption=="Hubble Space Telescope image; processed colour."&&shown.RepresentationCredits.Single()==hubble.representation,"Publisher release details were not separated from the scientific image description");
    var pluto=new TargetInfoImage{representation="New Horizons LORRI image combined with lower-resolution Ralph colour data."};shown=TargetInfoPresentation.Credits(pluto);
    Check(shown.Caption==pluto.representation&&shown.RepresentationCredits.Length==0,"Scientific instrument/data descriptions were mistaken for credits");
    var explicitCredit=new TargetInfoImage{representation="Narrowband observation. Image credit: Alice; processing by Bob."};shown=TargetInfoPresentation.Credits(explicitCredit);
    Check(shown.Caption=="Narrowband observation."&&shown.RepresentationCredits.Single()==explicitCredit.representation,"Explicit representation credit leaked into the caption or lost contributors");
    foreach(string notice in new[]{"© Alice","(c) 2026 Alice","&copy; Alice"}){var copyright=new TargetInfoImage{representation="Optical image. "+notice};var displayed=TargetInfoPresentation.Credits(copyright);Check(displayed.Caption=="Optical image."&&displayed.RepresentationCredits.Single()==TargetInfoPresentation.CreditText(copyright.representation),"Copyright notice leaked out of Credits or was dropped: "+notice);}
    Check(TargetInfoPresentation.CreditText("&lt;p&gt;Alice &amp;amp; Bob&lt;br&gt;<a href='https://example.test/credit'>Team</a>&lt;/p&gt;")=="Alice & Bob\nTeam (https://example.test/credit)","HTML entities, line breaks or attribution links are misformatted");
   });
   Test("All bundled image credits have clean display text and retain required attribution",()=>{
    int count=0;using(var zip=System.IO.Compression.ZipFile.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TargetInfo.zip"))){foreach(var entry in zip.Entries.Where(value=>value.FullName.StartsWith("cards/")&&value.FullName.EndsWith(".json"))){Dictionary<string,TargetInfoCard> cards;using(var reader=new StreamReader(entry.Open()))cards=Util.Deserialize<Dictionary<string,TargetInfoCard>>(reader.ReadToEnd());foreach(var card in cards.Values.Where(value=>value.image!=null)){
     count++;var shown=TargetInfoPresentation.Credits(card.image);Check(shown.Attributions.Length>0&&shown.Licence.Length>0&&shown.SourceUrl!=null,"Missing image attribution/licence/source: "+card.id);
     string text=shown.Creator+shown.Caption+string.Join("\n",shown.Attributions.Concat(shown.Modifications).Concat(shown.RepresentationCredits));Check(!Regex.IsMatch(text,@"<[^>]+>|&(?:[A-Za-z]+|#\d+);"),"Raw HTML/entity in image credits: "+card.id);
     if(card.id=="M96")Check(shown.Attributions.Any(value=>value.Contains("Image processing: M. Rodriguez")),"Image processing contributors were stripped");
    }}}
    Check(count>=200,"Credit audit did not cover the complete bundled image set: "+count);
   });
   MeteorShowerTests();
   Test("Meteors have a page with a licensed image reachable by every common name",()=>{
    var meteorPack=new TargetInfoCatalog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TargetInfo.zip"));
    foreach(var name in new[]{"Meteor","Meteors","Meteor shower","Shooting star"}){
     var result=meteorPack.Find(name,CancellationToken.None);Check(result.Status=="matched"&&result.Cards.Single().id=="METEORS","Meteor name not found: "+name);
    }
    var card=meteorPack.ById("METEORS",CancellationToken.None).Cards.Single();
    Check(card.image!=null&&card.image.licence=="CC-BY-4.0"&&card.image.creator.Contains("Guisard")&&TargetInfoPresentation.DisplaySummary(card).Contains("meteoroid")&&TargetInfoPresentation.Imaging(card).Contains("tripod"),"Meteors card lacks its image, summary or imaging notes");
    Check(MeteorColours.All.Length==6&&MeteorColours.All.All(c=>c.Cause.Length>20&&c.Hex.StartsWith("#")),"Meteor colour guide is incomplete");
   });
   Test("Curated overviews cover common imaging targets and the tagline shows an ID once",()=>{
    Check(TargetDescriptions.Count>=150,"Too few curated overviews: "+TargetDescriptions.Count);
    for(int m=1;m<=110;m++){var entry=TargetDescriptions.For("M"+m);Check(entry!=null&&!string.IsNullOrWhiteSpace(entry.Imaging),"Messier "+m+" lacks a curated entry");Check(m==1||m==33||entry.Overview!=null&&entry.Overview.Length>=160&&!entry.Overview.Contains("(IAU constellation code)"),"Messier "+m+" overview is too thin");}
    foreach(string id in new[]{"NGC 7000","IC 1805","NGC 6888","NGC 2237","B 33","IC 434","NGC 7293","SH2-129"})Check(TargetDescriptions.For(id)!=null&&TargetDescriptions.For(id).Overview.Length>=100,id+" lacks a curated overview");
    Check(TargetDescriptions.For("IC 434").HiddenAliases.Contains("Flame Nebula")&&TargetDescriptions.For("m31")!=null,"Alias correction or case-insensitive lookup is missing");
    var parsed=TargetDescriptions.Parse("# note\n## X 1\nFirst line.\nSecond line.\nImaging: Use Hα.\nFact: True.\nNot: A, B\n## X 2\nImaging: Only notes.");
    Check(parsed["X 1"].Overview=="First line. Second line."&&parsed["X 1"].Imaging=="Use Hα."&&parsed["X 1"].Fact=="True."&&parsed["X 1"].HiddenAliases.Length==2&&parsed["X 2"].Overview==null,"Description format was misread");
    Check(TargetInfoPresentation.Tagline(new TargetInfoCard{id="IC 434",name="IC 434",type_label="H II region"},"Orion")=="H II region · Orion","Tagline repeated the ID shown in the heading");
    Check(TargetInfoPresentation.Tagline(new TargetInfoCard{id="NGC 2237",name="NGC 2237 (Rosette A)",type_label="Emission nebula"},null)=="Emission nebula","Tagline repeated an ID contained in the heading");
    var sunflower=new TargetInfoCard{id="M63",name="Sunflower Galaxy",type_label="Spiral galaxy",aliases=new[]{"2MASX J13154932+4201454","IRAS 13135+4217","M63","NGC 5055","PGC 046153","Sunflower Galaxy","UGC 08334"}};
    Check(TargetInfoPresentation.AlternativeIds(sunflower).SequenceEqual(new[]{"NGC 5055","UGC 8334"})&&TargetInfoPresentation.AlternativeLine(sunflower)=="NGC 5055 · UGC 8334"&&TargetInfoPresentation.Tagline(sunflower,null)=="M 63 · Spiral galaxy","Alternative IDs wrong or survey identifiers shown");
    sunflower.aliases=new[]{"M63","NGC 5055"};Check(TargetInfoPresentation.Tagline(sunflower,"Canes Venatici")=="M 63 · NGC 5055 · Spiral galaxy · Canes Venatici"&&TargetInfoPresentation.AlternativeLine(sunflower)=="","A single alternative ID did not join the tagline");
    Check(TargetInfoPresentation.Tagline(new TargetInfoCard{id="M31",name="Andromeda Galaxy",type_label="Spiral galaxy"},"Andromeda")=="M 31 · Spiral galaxy · Andromeda","Tagline dropped the ID of a named object");
    var sources=TargetInfoPresentation.Sources(new TargetInfoCard{id="M42"},true);Check(sources.Any(x=>x.Url.EndsWith("/messier-42/"))&&sources.Any(x=>x.Url.Contains("simbad")),"Curated overview sources are missing");
   });
   Test("Parameters give planets an orbit, size, mass and moons and the Sun only size and mass",()=>{
    var jupiter=TargetParameters.For("JUPITER");Check(jupiter!=null&&jupiter.HasOrbit&&jupiter.Moons.Contains("Ganymede"),"Jupiter parameters missing");
    Check(TargetParameters.Orbit(jupiter)=="5.203 AU from the Sun · 11.86 years"&&TargetParameters.Diameter(jupiter)=="142,984 km"&&TargetParameters.Mass(jupiter)=="1.898 × 10²⁷ kg","Jupiter parameters formatted wrongly");
    Check(TargetParameters.Orbit(TargetParameters.For("mercury"))=="0.387 AU from the Sun · 88 days","Mercury orbit formatted wrongly");
    var sun=TargetParameters.For("SUN");Check(sun!=null&&!sun.HasOrbit&&sun.Moons.Length==0&&TargetParameters.Mass(sun)=="1.989 × 10³⁰ kg"&&TargetParameters.Diameter(sun)=="1,392,700 km","Sun should show only diameter and mass");
    Check(TargetParameters.For("M31")==null&&TargetParameters.For(null)==null,"Non-solar targets must have no parameters");
    var moon=TargetParameters.For("MOON");Check(moon!=null&&moon.HasOrbit&&TargetParameters.Orbit(moon)=="384,400 km from Earth · 27.32 days"&&TargetParameters.DistanceRange(moon)=="363,300 to 405,500 km"&&TargetParameters.Diameter(moon)=="3,475 km"&&TargetParameters.Mass(moon)=="7.346 × 10²² kg"&&TargetParameters.Gravity(moon)=="1.62 m/s²","Moon parameters wrong");
    Check(TargetParameters.ShowsMoonCount(TargetParameters.For("SATURN"))&&TargetParameters.For("SATURN").MoonCount==274&&!TargetParameters.ShowsMoonCount(TargetParameters.For("EARTH"))&&!TargetParameters.ShowsMoonCount(TargetParameters.For("MERCURY")),"Moon counts wrong");
    var halleyOrbit=CometInfo.Orbit("1P");Check(CometInfo.SemiMajorAxis(halleyOrbit)=="17.9 AU"&&CometInfo.PerihelionSpeed(halleyOrbit)=="55.1 km/s"&&!CometInfo.ShowsEscapeSpeed(halleyOrbit)&&!CometInfo.HasSemiMajorAxis(CometInfo.Orbit("C/1996 B2"))&&CometInfo.ShowsEscapeSpeed(CometInfo.Orbit("2I"))&&CometInfo.EscapeSpeed(CometInfo.Orbit("2I"))=="32.3 km/s","Comet derived orbit facts wrong");
   });
   var catalog=new TargetInfoCatalog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TargetInfo.zip"));
   Test("Galaxy distances come from catalogue redshifts and read as approximate light-years",()=>{
    Func<double,string,TargetInfoMeasurement> m=(v,b)=>new TargetInfoMeasurement{field="Distance",unit="ly",value=v,band=b};
    Check(TargetInfoPresentation.Distance(m(142000000,"redshift"))=="≈ 140 million light-years","Mid-range redshift distance wrong: "+TargetInfoPresentation.Distance(m(142000000,"redshift")));
    Check(TargetInfoPresentation.Distance(m(4230000,"redshift"))=="≈ 4.2 million light-years"&&TargetInfoPresentation.Distance(m(42300000,null))=="42 million light-years","Small distance formatted wrongly");
    Check(TargetInfoPresentation.Distance(m(1370000000,"redshift"))=="≈ 1.4 billion light-years"&&TargetInfoPresentation.Distance(m(1500,null))=="1,500 light-years","Large or tiny distance formatted wrongly");
    var card=catalog.ById("M77",CancellationToken.None).Cards.Single();var distance=(card.measurements??new TargetInfoMeasurement[0]).FirstOrDefault(x=>x.field=="Distance");
    Check(distance!=null&&distance.band=="redshift"&&distance.value>1e7&&distance.value<2e8&&!string.IsNullOrEmpty(distance.qualifier),"A galaxy with a catalogue redshift lacks its approximate distance");
    Check(!(catalog.ById("M31",CancellationToken.None).Cards.Single().measurements??new TargetInfoMeasurement[0]).Any(x=>x.field=="Distance"&&x.band=="redshift"),"A blueshifted galaxy was given a redshift distance");
   });
   Test("Thumbnail credits contain creators and acknowledgements without Commons page templates",()=>{
    var iris=catalog.ById("NGC 7023",CancellationToken.None).Cards.Single().image;
    Check(iris.creator=="Andreigusan"&&iris.attribution.StartsWith("Andreigusan — ")&&iris.licence=="CC-BY-SA-4.0","Iris thumbnail credit or licence is wrong");
    foreach(string id in new[]{"NGC 7023","NGC 6744","IC 2944","LDN 1622","M96","NGC 55","NGC 6543","NGC 6726","M3","M69"}){
     var image=catalog.ById(id,CancellationToken.None).Cards.Single().image;string text=image.creator+image.attribution;
     Check(!text.Contains("featured picture")&&!text.Contains("Commons:Licensing")&&!text.Contains("( talk )"),"Commons page prose remains in "+id);
    }
    Check(catalog.ById("M96",CancellationToken.None).Cards.Single().image.attribution.Contains("Image processing: M. Rodriguez"),"Real processing credits were stripped");
    Check(TargetInfoPresentation.Imaging(new TargetInfoCard{id="No notes",domain="deep_sky"})==null&&TargetInfoPresentation.Imaging(null)==null,"Empty imaging notes produced a section");
    Check(CometInfo.Imaging.Contains("10–30 second")&&CometInfo.Imaging.Contains("5–10 seconds")&&CometInfo.Imaging.Contains("20 seconds or less"),"Comet notes lack practical exposure guidance");
   });
   Test("Target info resolves complete aliases and preserves disputed identities",()=>{
    foreach(string label in new[]{"M31","Messier 031","NGC 0224","Andromeda Galaxy"}){var result=catalog.Find(label,CancellationToken.None);Check(result.Status=="matched"&&result.Cards.Single().id=="M31","Wrong identity for "+label);}
    Check(catalog.Find("M102",CancellationToken.None).Status=="ambiguous","Disputed Messier identity picked automatically");
    Check(catalog.Find("Spindle Galaxy",CancellationToken.None).Status=="ambiguous","Shared common name picked automatically");
    Check(catalog.Find("M310",CancellationToken.None).Status=="not_found","Partial identifier matched");
    Check(catalog.Find("My personal field",CancellationToken.None).Status=="not_found","Custom target guessed");
   });
   Test("Target info distinguishes nebula components from their clusters",()=>{
    var cluster=catalog.Find("NGC2244",CancellationToken.None);var nebula=catalog.Find("NGC2237",CancellationToken.None);
    Check(cluster.Status=="matched"&&nebula.Status=="matched"&&cluster.Cards.Single().id!=nebula.Cards.Single().id,"Rosette cluster and nebula merged in the information reader");
    Check(catalog.Find("NGC4038",CancellationToken.None).Cards.Single().id!=catalog.Find("NGC4039",CancellationToken.None).Cards.Single().id,"Galaxy pair components merged");
   });
   Test("Target info keeps verified imagery, credits and measured coordinates",()=>{
    foreach(string label in new[]{"M31","M42","M45","C23","NGC7000","IC10","IC434","Sun","Moon","Venus"}){
     var card=catalog.Find(label,CancellationToken.None).Cards.Single();
     Check(card.Thumbnail!=null&&card.Thumbnail.Length>1000&&card.image!=null&&!string.IsNullOrWhiteSpace(card.image.attribution)&&!string.IsNullOrWhiteSpace(card.image.licence),"Missing credited offline image for "+label);
     if(card.domain=="deep_sky")Check(card.measurements.Any(m=>m.field=="RA")&&card.measurements.Any(m=>m.field=="Dec"),"Missing sky position for "+label);
    }
    Check(catalog.Find("Venus",CancellationToken.None).Cards.Single().image.representation.IndexOf("radar",StringComparison.OrdinalIgnoreCase)>=0,"Radar rendering caption lost");
   });
   Test("Target info omits draft prose and binds every measurement to a source",()=>{
    var draft=catalog.Find("M31",CancellationToken.None).Cards.Single();Check(draft.review_status=="draft"&&draft.fact==null&&draft.summary.Contains("NGC 224"),"Draft prose exposed as a checked description");
    foreach(var m in draft.measurements)Check(draft.citations.Any(c=>c.id==m.citation_id),"Measurement citation missing: "+m.field);
    var sun=catalog.Find("Sun",CancellationToken.None).Cards.Single();Check(sun.review_status=="source_checked"&&!string.IsNullOrWhiteSpace(sun.summary),"Checked overview lost");
   });
   Test("Target info presents full constellations and only distinct verified sources",()=>{
    var ic=catalog.Find("IC434",CancellationToken.None).Cards.Single();var horse=catalog.Find("B33",CancellationToken.None).Cards.Single();
    Check(ic.id!=horse.id&&ic.image.object_id==ic.id&&ic.Thumbnail.SequenceEqual(horse.Thumbnail),"IC 434 image sharing changed identity or duplicated different imagery");
    Check(TargetInfoPresentation.Constellation("Ori")=="Orion"&&TargetInfoPresentation.Constellation("Orion")=="Orion"&&TargetInfoPresentation.Summary(ic).Contains("Orion")&&!TargetInfoPresentation.Summary(ic).Contains("IAU constellation code"),"Constellation metadata leaked into the overview");
    Check(TargetInfoPresentation.Constellation("Se1")=="Serpens"&&TargetInfoPresentation.Constellation("Se2")=="Serpens"&&TargetInfoPresentation.Constellation("CrA")=="Corona Australis"&&TargetInfoPresentation.Summary(catalog.Find("M5",CancellationToken.None).Cards.Single()).Contains("Serpens"),"Split Serpens codes or alternate southern constellation name were not resolved");
    Check(TargetInfoPresentation.Caption("Astronomical observation; filter bands and colour processing unspecified.")==""&&TargetInfoPresentation.Caption("Radar map.")=="Radar map.","Meaningless image caption remains or meaningful modality was lost");
    var sources=TargetInfoPresentation.Sources(catalog.Find("M31",CancellationToken.None).Cards.Single());Check(sources.Count==2&&sources.Count(x=>x.Title.StartsWith("OpenNGC"))==1&&sources.Count(x=>x.Title.StartsWith("Wikidata P2583"))==1,"Duplicate or draft OpenNGC references remain");
   });
   Test("Target info normalizer retains component suffixes and comet punctuation",()=>{
    Check(TargetInfoCatalog.Normalize("NGC 0001 NED02")=="NGC:1:NED2","NED component lost");
    Check(TargetInfoCatalog.Normalize("Sh 2-00101")=="SH2:101","Sharpless spelling unresolved");
    Check(TargetInfoCatalog.Normalize("NGC 6992A")!="NGC:6992","Object suffix lost");
    Check(TargetInfoCatalog.Normalize("C/2023 A3")!="NAME:C2023A3","Comet slash lost");
   });
   Test("Comets open a page with an overview, orbit and JPL source",()=>{
    Check(CometTargets.Designation("C/2023 A3 (Tsuchinshan-ATLAS)")=="C/2023 A3"&&CometTargets.Designation("12P/Pons-Brooks")=="12P"&&CometTargets.Designation("2I/Borisov")=="2I"&&CometTargets.Designation("C/2013 US10 (Catalina)")=="C/2013 US10"&&CometTargets.Designation("M31")==null&&CometTargets.Designation("NEOWISE")==null,"Comet designation misread");
    foreach(var pair in new[]{new[]{"C/2023 A3 (Tsuchinshan-ATLAS)","C/2023 A3"},new[]{"12P/Pons-Brooks","12P"},new[]{"2I/Borisov","2I"},new[]{"C/2019 Q4 (Borisov)","2I"},new[]{"NEOWISE","C/2020 F3"},new[]{"C/1995 O1 (Hale-Bopp)","C/1995 O1"},new[]{"C/2022 E3 (ZTF)","C/2022 E3"},new[]{"3I/ATLAS","3I"},new[]{"C/2099 B12 (Example)","C/2099 B12"}}){
     var result=catalog.Find(pair[0],CancellationToken.None);Check(result.Status=="matched"&&result.Cards.Single().id==pair[1],"Wrong comet page for "+pair[0]+": "+string.Join(",",result.Cards.Select(c=>c.id)));
     var card=result.Cards.Single();Check(CometInfo.IsComet(card)&&TargetInfoPresentation.DisplaySummary(card).Length>=80&&TargetInfoPresentation.Imaging(card)!=null,"Comet page is empty for "+pair[0]);
     var sources=TargetInfoPresentation.Sources(card,TargetDescriptions.For(card.id)!=null);Check(!sources.Any(s=>s.Url.Contains("simbad")),"Comet cites SIMBAD: "+pair[0]);
     Check((CometInfo.Orbit(card.id)!=null)==sources.Any(s=>s.Url.StartsWith("https://ssd.jpl.nasa.gov/")),"Comet orbit and its JPL source disagree for "+pair[0]);
    }
    foreach(string id in new[]{"1P","2P","9P","12P","21P","29P","55P","67P","73P","103P","109P","1I","2I","C/1995 O1","C/2006 P1","C/2014 Q2","C/2017 K2","C/2019 Y4","C/2020 F3","C/2023 A3","C/2025 A6"}){
     var card=catalog.ById(id,CancellationToken.None).Cards.Single();Check(CometInfo.IsComet(card),id+" is not treated as a comet");
     Check(TargetDescriptions.For(id)!=null&&TargetDescriptions.For(id).Overview.Length>=100&&CometInfo.Orbit(id)!=null,id+" lacks a written overview or orbit");
    }
    Check(TargetInfoPresentation.DisplaySummary(catalog.Find("C/2099 B12",CancellationToken.None).Cards.Single()).Contains("12th comet reported in the second half of January 2099"),"Designation not explained");
    Check(TargetInfoPresentation.DisplaySummary(CometInfo.Card("Comet ATLAS")).Contains("designation"),"Name-only comet lacks guidance");
    var halley=CometInfo.Orbit("1P");Check(CometInfo.Period(halley)=="75.9 years per orbit"&&CometInfo.Perihelion(halley)=="0.575 AU from the Sun"&&CometInfo.ClassLabel(halley)=="Halley-type"&&CometInfo.ShowsPerihelionDate(halley)&&CometInfo.PerihelionDate(halley)=="8 Feb 1986"&&CometInfo.ShowsAphelion(halley)&&CometInfo.Aphelion(halley)=="35.3 AU from the Sun"&&CometInfo.Aphelion(CometInfo.Orbit("2P"))=="4.1 AU from the Sun"&&!CometInfo.ShowsAphelion(CometInfo.Orbit("C/1995 O1")),"Halley orbit formatted wrongly");
    Check(!CometInfo.ShowsPerihelionDate(CometInfo.Orbit("67P"))&&CometInfo.ClassLabel(CometInfo.Orbit("2I"))=="Hyperbolic"&&CometInfo.ClassLabel(CometInfo.Orbit("C/2023 A3"))=="Near-parabolic","Comet orbit classes wrong");
    Check(!CometInfo.IsComet(catalog.Find("M31",CancellationToken.None).Cards.Single())&&!CometInfo.IsComet(catalog.Find("Jupiter",CancellationToken.None).Cards.Single())&&catalog.Find("Rosette field",CancellationToken.None).Status=="not_found","Non-comets treated as comets");
   });
   Test("Section and eclipse pages, new images and the corrected galaxy type come from the pack",()=>{
    foreach(var pair in new[]{new[]{Eclipses.SolarSection,"SOLAR ECLIPSE"},new[]{Eclipses.LunarSection,"LUNAR ECLIPSE"},new[]{TargetNavigation.CometsSection,"COMETS"}}){
     var result=catalog.Find(pair[0],CancellationToken.None);Check(result.Status=="matched"&&result.Cards.Single().id==pair[1],"No page for "+pair[0]);
     var card=result.Cards.Single();Check(card.Thumbnail!=null&&card.image.licence.Length>0&&TargetInfoPresentation.DisplaySummary(card).Length>=200&&TargetInfoPresentation.Imaging(card)!=null&&!string.IsNullOrWhiteSpace(card.fact),pair[0]+" page lacks an image, overview, fact or imaging notes");
     Check(!TargetInfoPresentation.Sources(card,TargetDescriptions.For(card.id)!=null).Any(s=>s.Url.Contains("simbad")),pair[0]+" cites SIMBAD");
    }
    Check(catalog.Find("Comet ATLAS",CancellationToken.None).Cards.Single().id!="COMETS","A named comet opened the Comets page");
    var sun=catalog.Find("Sun",CancellationToken.None).Cards.Single().image;Check(sun.file_page_url.Contains("HMI")&&sun.representation.Contains("Full solar disk"),"Sun still uses a close-up image");
    var trunk=catalog.Find("Elephant's Trunk Nebula",CancellationToken.None).Cards.Single();Check(trunk.id=="IC 1396A"&&trunk.image.creator=="Astropizza"&&TargetDescriptions.For("IC 1396A").Overview!=null,"Elephant's Trunk image or overview not updated");
    var galaxy=catalog.Find("NGC 7640",CancellationToken.None).Cards.Single();Check(galaxy.type_label=="Barred spiral galaxy"&&galaxy.Thumbnail!=null&&galaxy.image.creator=="ESA/Hubble & NASA"&&TargetDescriptions.For("NGC 7640").Overview.Contains("SB(s)c"),"NGC 7640 lacks its image or galaxy type");
   });
   Test("Surface gravity is shown in Earth gravities beside the measured value",()=>{
    Check(TargetParameters.SurfaceGravity(TargetParameters.For("EARTH"))=="1.00 g (9.8 m/s²)"&&TargetParameters.SurfaceGravity(TargetParameters.For("MOON"))=="0.17 g (1.62 m/s²)","Earth or Moon gravity in g wrong");
    Check(TargetParameters.SurfaceGravity(TargetParameters.For("JUPITER"))=="2.53 g (24.79 m/s²)"&&TargetParameters.SurfaceGravity(TargetParameters.For("SUN"))=="27.9 g (274 m/s²)"&&TargetParameters.SurfaceGravity(TargetParameters.For("PLUTO"))=="0.063 g (0.62 m/s²)","Gravity in g formatted wrongly");
   });
   Test("Information pages open in Stellarium at a catalogue position or by name",()=>{
    var m31=Stellarium.ForCard(catalog.Find("M31",CancellationToken.None).Cards.Single());Check(m31!=null&&!m31.ByName&&Math.Abs(m31.RA-10.68)<0.1&&m31.Name=="M31"&&m31.FieldDegrees>3,"Deep-sky page did not open at its position and size");
    Check(Stellarium.Script(m31).Contains("core.moveToRaDecJ2000(")&&!Stellarium.Script(m31).Contains("setDate"),"Catalogue view should not change Stellarium's time");
    var jupiter=Stellarium.ForCard(catalog.Find("Jupiter",CancellationToken.None).Cards.Single());string script=Stellarium.Script(jupiter);
    Check(jupiter.ByName&&script.Contains("core.selectObjectByName(\"Jupiter\", true);")&&script.Contains("setFlagTracking(true)")&&!script.Contains("moveToRaDecJ2000"),"Planet not opened by name");
    Check(Stellarium.WebUrl(jupiter).StartsWith(Stellarium.WebAddress+"skysource/Jupiter?"),"Stellarium Web did not select the planet");
    Check(Stellarium.ForCard(catalog.Find("C/2023 A3 (Tsuchinshan-ATLAS)",CancellationToken.None).Cards.Single())==null&&Stellarium.ForCard(catalog.Find("12P/Pons-Brooks",CancellationToken.None).Cards.Single())==null&&Stellarium.ForCard(catalog.Find("Halley",CancellationToken.None).Cards.Single())==null,"Comet pages should not offer Stellarium");
    Check(Stellarium.ForCard(catalog.Find(Eclipses.SolarSection,CancellationToken.None).Cards.Single())==null&&Stellarium.ForCard(catalog.Find(Eclipses.LunarSection,CancellationToken.None).Cards.Single())==null,"Eclipse pages should not open Stellarium");
    Check(Stellarium.ForCard(catalog.Find(TargetNavigation.CometsSection,CancellationToken.None).Cards.Single())==null&&Stellarium.ForCard(CometInfo.GroupCard())==null&&Stellarium.ForCard(null)==null,"A group page offered a single Stellarium target");
    Check(Stellarium.Script(new StellariumView{ByName=true,Name="Bad \"name\"\n"}).Contains("\"Bad \\\"name\\\" \""),"Script name not escaped");
   });
   Test("Referenced Wikidata distances replace redshift distances, stay approximate and cite their statement",()=>{
    Func<string,TargetInfoCard> byId=id=>catalog.ById(id,CancellationToken.None).Cards.Single();Func<TargetInfoCard,TargetInfoMeasurement> distanceOf=c=>(c.measurements??new TargetInfoMeasurement[0]).FirstOrDefault(x=>x.field=="Distance");
    var m31=byId("M31");var andromeda=distanceOf(m31);
    Check(andromeda!=null&&andromeda.band=="wikidata"&&andromeda.value>2e6&&andromeda.value<3.5e6&&andromeda.qualifier.Contains("0.82 Mpc")&&andromeda.qualifier.Contains("not a redshift calculation"),"M31 lacks its sourced distance");
    Check(TargetInfoPresentation.Distance(andromeda)=="≈ 2.7 million light-years","Sourced distance not formatted as approximate: "+TargetInfoPresentation.Distance(andromeda));
    var citation=m31.citations.Single(c=>c.id==andromeda.citation_id);Check(citation.source_url.StartsWith("https://www.wikidata.org/wiki/Q")&&citation.source_url.EndsWith("#P2583")&&citation.publisher=="Wikidata contributors"&&citation.dataset_id=="wikidata"&&citation.retrieved_on=="2026-10-13","Wikidata citation is incomplete");
    Check(TargetInfoPresentation.Sources(m31).Any(x=>x.Url==citation.source_url&&x.Title.StartsWith("Wikidata P2583")),"Wikidata item is missing from Sources");
    var ring=distanceOf(byId("M27"));Check(ring!=null&&ring.band=="wikidata"&&ring.value>1000&&ring.value<1500&&TargetInfoPresentation.Distance(ring)=="≈ 1,200 light-years","Planetary nebula distance missing");
    var group=distanceOf(byId("HCG079"));Check(group!=null&&group.band=="redshift"&&group.value>1e8,"An implausible 190 light-year galaxy-group distance replaced the redshift distance");
    Check(distanceOf(byId("IC 3418"))==null,"A galaxy whose Wikidata and redshift distances disagree by more than two times kept a distance");
   });
   Test("The comet snapshot gives every comet an offline orbit, names and meteor-shower parents",()=>{
    var invalid=CometCatalog.All.Where(c=>CometCatalog.Orbit(c)==null).Select(c=>c.Id).ToList();Check(CometCatalog.Count>=4000&&invalid.Count<=15&&invalid.All(id=>CometCatalog.Find(id).Epoch<2000000||CometCatalog.Find(id).TimeOfPerihelion<1000000),"Snapshot comets lack a valid orbit: "+string.Join(", ",invalid.Take(8))+" ("+invalid.Count+"); only comets from before the model's AD 763 limit may lack one");
    var tsuchinshan=CometCatalog.Find("C/2023 A3");Check(tsuchinshan!=null&&tsuchinshan.FullName.Contains("Tsuchinshan-ATLAS")&&tsuchinshan.Kind=="hyperbolic"&&CometCatalog.Find("c/2019 q4").Id=="2I"&&CometCatalog.Find("73P-B")!=null&&CometCatalog.Find("73P-B")!=CometCatalog.Find("73P")&&CometCatalog.Find("M31")==null,"Comet identities are wrong");
    Check(CometCatalog.FindByName("Hale-Bopp").Id=="C/1995 O1"&&CometCatalog.FindByName("hale bopp").Id=="C/1995 O1"&&CometCatalog.FindByName("ATLAS")==null&&CometCatalog.FindByName("Hyakutake")==null&&CometInfo.InformationDesignation("Comet Hale-Bopp")=="C/1995 O1"&&CometInfo.InformationDesignation("Comet ATLAS")==null,"Shared discoverer names must stay ambiguous");
    var hartley=CometInfo.Orbit("100P");Check(hartley!=null&&CometInfo.ClassLabel(hartley)=="Jupiter-family"&&CometInfo.Period(hartley)=="6.35 years per orbit"&&CometInfo.Perihelion(hartley)=="2.01 AU from the Sun","Snapshot comet orbit formatted wrongly: "+(hartley==null?"none":CometInfo.Period(hartley)+" "+CometInfo.Perihelion(hartley)));
    Check(CometInfo.Orbit("1P").Perihelion==new DateTime(1986,2,8)&&Math.Abs(CometInfo.Orbit("1P").Q-0.575)<1e-9,"Reviewed comet orbits must keep precedence");
    var parabolic=CometCatalog.All.First(c=>c.Kind=="parabolic"&&CometCatalog.Orbit(c)!=null);var parabolicOrbit=CometInfo.Orbit(parabolic.Id);Check(CometInfo.ClassLabel(parabolicOrbit)=="Parabolic"&&parabolicOrbit.PeriodYears==0&&!CometInfo.ShowsAphelion(parabolicOrbit),"Parabolic comet reads as periodic");
    var card=CometInfo.Card("100P");Check(card.id=="100P"&&card.name=="100P/Hartley 1","Catalogued comet stub lacks its full name: "+card.name);
    card=CometInfo.Card("Comet Hale-Bopp");Check(card.id=="C/1995 O1"&&card.name=="C/1995 O1 (Hale-Bopp)","Name-only comet did not resolve: "+card.id+" "+card.name);
    var record=OrbitalCatalog.For(CometInfo.Card("100P"));Check(record!=null&&record.object_id=="100P"&&record.solution_id.StartsWith("JPL")&&record.uncertainties.ContainsKey("e")&&InformationRights.Permitted(record.rights,true,true)&&OrbitalCatalog.Scene(record,CancellationToken.None).Full.Points.Length>10,"Snapshot comet has no complete 3D orbit");
    Check(OrbitalCatalog.For(CometInfo.Card("C/2099 B12 (Example)"))==null,"An unknown comet was given an orbit");
    Func<string,bool,string> showers=(id,suspected)=>CometCatalog.ShowerNames(CometCatalog.ShowersOf(id),suspected);
    Check(showers("109P",false)=="Perseids"&&showers("55P",false)=="Leonids"&&showers("1P",false)=="eta Aquariids, Orionids"&&showers("96P",true)=="Southern delta Aquariids"&&showers("P/2008 Y12",true)=="Southern delta Aquariids"&&showers("96P",false)==""&&showers("73P",false)==""&&showers("73P-B",false)=="","Meteor-shower parents are wrong");
    Check(CometCatalog.Showers.Count>=30&&CometCatalog.Showers.Any(x=>x.Name=="Geminids"&&x.Parents.Count==0)&&CometCatalog.ShowerEvidence(CometCatalog.ShowersOf("1P")).Contains("not a forecast"),"Meteor-shower list or evidence wrong");
    Check(TargetInfoPresentation.Sources(catalog.ById("1P",CancellationToken.None).Cards.Single()).Any(x=>x.Url==CometCatalog.ShowerUrl)&&!TargetInfoPresentation.Sources(CometInfo.Card("100P")).Any(x=>x.Url==CometCatalog.ShowerUrl),"Meteor streams missing from a parent comet's sources");
   });
   Test("Target info cancels obsolete loads and fails safely without a pack",()=>{
    using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();bool canceled=false;try{catalog.Find("M31",cancellation.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"Canceled card lookup ran");}
    bool missing=false;try{new TargetInfoCatalog(Path.Combine(root,"missing-info.zip")).Find("M31",CancellationToken.None);}catch(IOException){missing=true;}Check(missing,"Missing pack silently matched");
   });
  }
 }
}
