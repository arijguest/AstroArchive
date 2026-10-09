using System;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void HelpTests(){
   Test("Bundled help retains workflows and searches complete topic text",()=>{
    var topics=HelpCatalog.Load();foreach(string key in new[]{"START HERE","IMPORT OVER WI-FI / LAN","IMPORT WORKFLOW","LIBRARY WORKFLOW","USB TRANSFER AND SAVED TELESCOPES","IMAGE PREVIEW AND TABLES","STACKING PROJECTS AND SESSIONS","TROUBLESHOOTING","KEYBOARD SHORTCUTS","FRAME TYPES AND GLOSSARY"})Check(topics.Any(t=>t.Key==key&&!string.IsNullOrWhiteSpace(t.Body)),"Missing guide topic: "+key);
    Check(topics.Select(t=>t.Key).Distinct().Count()==topics.Count,"Duplicate guide topics");Check(HelpCatalog.Search(topics,"PINCH").Any(t=>t.Key=="IMAGE PREVIEW AND TABLES"),"Full-text search is case sensitive or loses preview text");Check(HelpCatalog.Search(topics,"USB volume").Any(t=>t.Key=="USB TRANSFER AND SAVED TELESCOPES"),"Search does not combine title and body");Check(HelpCatalog.Search(topics,"no-such-help-topic-123456").Count==0&&HelpCatalog.Search(topics," \t").Count==topics.Count,"Empty search and no-result behavior");
    var parsed=HelpCatalog.Parse("About\r\n\r\nFIRST TOPIC\r\nFirst paragraph.\r\n\r\nSecond paragraph.\r\nSECOND TOPIC\r\nLast paragraph.");Check(parsed.Count==3&&parsed[1].Body.Contains("Second paragraph.")&&parsed[2].Body=="Last paragraph.","Help parser lost paragraphs or final topic");
   });
   Test("Help formatting preserves wrapped instructions and distinct list items",()=>{
    var blocks=HelpCatalog.Blocks("A wrapped introduction\ncontinues here.\n\n## Actions\n- **Scan folder** checks files.\n  It keeps originals.\n- Review results.\n3. Choose `Export`.\n4. Pick a folder.\nA closing paragraph.");
    Check(blocks.Count==7&&blocks[0].Text=="A wrapped introduction continues here."&&blocks[1].Kind==HelpBlockKind.Heading,"Hard wrapping or section boundaries changed");
    Check(blocks[2].Kind==HelpBlockKind.Bullet&&blocks[2].Text.Contains("It keeps originals.")&&blocks[3].Kind==HelpBlockKind.Bullet,"Bullets lost continuation text or merged together");
    Check(blocks[4].Kind==HelpBlockKind.Numbered&&blocks[4].Number==3&&blocks[5].Number==4&&blocks[6].Kind==HelpBlockKind.Paragraph,"Ordered items swallowed closing prose or lost numbering");
    string plain=HelpCatalog.PlainText("## Controls\n- **Scan folder** then `Ctrl+F`.\n2. Keep originals.");Check(plain.Contains("Controls")&&plain.Contains("• Scan folder then Ctrl+F.")&&plain.Contains("2. Keep originals.")&&!plain.Contains("**")&&!plain.Contains("##")&&!plain.Contains("`"),"Saved text exposes formatting markers or loses instructions");
   });
   Test("Guide sections stay searchable without promoting formatted content to topics",()=>{
    var parsed=HelpCatalog.Parse("FIRST TOPIC\n## USB\n- FITS\n1. USB\nSECOND TOPIC\nLast paragraph.");Check(parsed.Count==2&&parsed[0].Body.Contains("## USB")&&parsed[0].Body.Contains("1. USB"),"Subheadings or list labels became guide topics");
    var topics=HelpCatalog.Load();Check(topics.Count==29&&HelpCatalog.Blocks(topics.Single(t=>t.Key=="START HERE").Body).Count(b=>b.Kind==HelpBlockKind.Numbered)==5,"Bundled guide lost topics or first-import steps");
    Check(HelpCatalog.Blocks(topics.Single(t=>t.Key=="KEYBOARD SHORTCUTS").Body).Count(b=>b.Kind==HelpBlockKind.Bullet)==5&&HelpCatalog.Blocks(topics.Single(t=>t.Key=="TROUBLESHOOTING").Body).Count(b=>b.Kind==HelpBlockKind.Bullet)==10,"Shortcut or troubleshooting entries lost list structure");
    Check(topics.Single(t=>t.Key=="IMAGE PREVIEW AND TABLES").Body.Contains("Individual targets: stacks above lights")&&topics.Single(t=>t.Key=="STACKING PROJECTS AND SESSIONS").Body.Contains("Open with… after export"),"Workflow notes were lost");
    Check(!topics.Any(t=>new[]{"Sky map:","skymap","sky globe","## Capture sky"}.Any(text=>t.Body.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0)),"Skymap instructions remain in the interactive guide");
   });
  }
 }
}
