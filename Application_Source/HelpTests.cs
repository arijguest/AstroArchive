using System;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void HelpTests(){
   Test("Bundled help retains workflows and searches complete topic text",()=>{
    var topics=HelpCatalog.Load();foreach(string key in new[]{"START HERE","IMPORT WORKFLOW","LIBRARY WORKFLOW","USB TRANSFER AND SAVED TELESCOPES","IMAGE PREVIEW AND TABLES","STACKING PROJECTS AND SESSIONS","TROUBLESHOOTING","KEYBOARD SHORTCUTS","FRAME TYPES AND GLOSSARY"})Check(topics.Any(t=>t.Key==key&&!string.IsNullOrWhiteSpace(t.Body)),"Missing guide topic: "+key);
    Check(topics.Select(t=>t.Key).Distinct().Count()==topics.Count,"Duplicate guide topics");Check(HelpCatalog.Search(topics,"PINCH").Any(t=>t.Key=="IMAGE PREVIEW AND TABLES"),"Full-text search is case sensitive or loses preview text");Check(HelpCatalog.Search(topics,"USB volume").Any(t=>t.Key=="USB TRANSFER AND SAVED TELESCOPES"),"Search does not combine title and body");Check(HelpCatalog.Search(topics,"no-such-help-topic-123456").Count==0&&HelpCatalog.Search(topics," \t").Count==topics.Count,"Empty search and no-result behavior");
    var parsed=HelpCatalog.Parse("About\r\n\r\nFIRST TOPIC\r\nFirst paragraph.\r\n\r\nSecond paragraph.\r\nSECOND TOPIC\r\nLast paragraph.");Check(parsed.Count==3&&parsed[1].Body.Contains("Second paragraph.")&&parsed[2].Body=="Last paragraph.","Help parser lost paragraphs or final topic");
   });
  }
 }
}
