using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace AstroArchive {
 public partial class Tests {
  static void TargetInfoTests(){
   var catalog=new TargetInfoCatalog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TargetInfo.zip"));
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
   Test("Target info cancels obsolete loads and fails safely without a pack",()=>{
    using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();bool canceled=false;try{catalog.Find("M31",cancellation.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"Canceled card lookup ran");}
    bool missing=false;try{new TargetInfoCatalog(Path.Combine(root,"missing-info.zip")).Find("M31",CancellationToken.None);}catch(IOException){missing=true;}Check(missing,"Missing pack silently matched");
   });
  }
 }
}
