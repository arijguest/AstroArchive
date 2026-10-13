using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace AstroArchive {
 public partial class Tests {
  static void TargetInfoTests(){
   Test("Curated overviews cover common imaging targets and the tagline shows an ID once",()=>{
    Check(TargetDescriptions.Count>=150,"Too few curated overviews: "+TargetDescriptions.Count);
    for(int m=1;m<=110;m++){var entry=TargetDescriptions.For("M"+m);Check(entry!=null&&!string.IsNullOrWhiteSpace(entry.Imaging),"Messier "+m+" lacks a curated entry");Check(m==1||m==33||entry.Overview!=null&&entry.Overview.Length>=160&&!entry.Overview.Contains("(IAU constellation code)"),"Messier "+m+" overview is too thin");}
    foreach(string id in new[]{"NGC 7000","IC 1805","NGC 6888","NGC 2237","B 33","IC 434","NGC 7293","SH2-129"})Check(TargetDescriptions.For(id)!=null&&TargetDescriptions.For(id).Overview.Length>=100,id+" lacks a curated overview");
    Check(TargetDescriptions.For("IC 434").HiddenAliases.Contains("Flame Nebula")&&TargetDescriptions.For("m31")!=null,"Alias correction or case-insensitive lookup is missing");
    var parsed=TargetDescriptions.Parse("# note\n## X 1\nFirst line.\nSecond line.\nImaging: Use Hα.\nFact: True.\nNot: A, B\n## X 2\nImaging: Only notes.");
    Check(parsed["X 1"].Overview=="First line. Second line."&&parsed["X 1"].Imaging=="Use Hα."&&parsed["X 1"].Fact=="True."&&parsed["X 1"].HiddenAliases.Length==2&&parsed["X 2"].Overview==null,"Description format was misread");
    Check(TargetInfoPresentation.Tagline(new TargetInfoCard{id="IC 434",name="IC 434",type_label="H II region"},"Orion")=="H II region · Orion","Tagline repeated the ID shown in the heading");
    Check(TargetInfoPresentation.Tagline(new TargetInfoCard{id="NGC 2237",name="NGC 2237 (Rosette A)",type_label="Emission nebula"},null)=="Emission nebula","Tagline repeated an ID contained in the heading");
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
    var sources=TargetInfoPresentation.Sources(catalog.Find("M31",CancellationToken.None).Cards.Single());Check(sources.Count==1,"Duplicate or draft OpenNGC references remain");
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
    Check(!CometInfo.ShowsPerihelionDate(CometInfo.Orbit("67P"))&&CometInfo.ClassLabel(CometInfo.Orbit("2I"))=="Hyperbolic (interstellar)"&&CometInfo.ClassLabel(CometInfo.Orbit("C/2023 A3"))=="Near-parabolic","Comet orbit classes wrong");
    Check(!CometInfo.IsComet(catalog.Find("M31",CancellationToken.None).Cards.Single())&&!CometInfo.IsComet(catalog.Find("Jupiter",CancellationToken.None).Cards.Single())&&catalog.Find("Rosette field",CancellationToken.None).Status=="not_found","Non-comets treated as comets");
   });
   Test("Target info cancels obsolete loads and fails safely without a pack",()=>{
    using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();bool canceled=false;try{catalog.Find("M31",cancellation.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"Canceled card lookup ran");}
    bool missing=false;try{new TargetInfoCatalog(Path.Combine(root,"missing-info.zip")).Find("M31",CancellationToken.None);}catch(IOException){missing=true;}Check(missing,"Missing pack silently matched");
   });
  }
 }
}
