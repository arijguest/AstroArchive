using System;
using System.Linq;
namespace AstroArchive {
 public partial class Tests {
  static void HelpTests(){
   Test("Bundled help retains workflows and ranks searches by title, summary and text",()=>{
    var topics=HelpCatalog.Load();foreach(string key in new[]{"START HERE","IMPORT OVER WI-FI / LAN","IMPORT WORKFLOW","LIBRARY WORKFLOW","USB TRANSFER AND SAVED TELESCOPES","IMAGE PREVIEW AND TABLES","STACKING PROJECTS AND SESSIONS","TROUBLESHOOTING","KEYBOARD SHORTCUTS","FRAME TYPES AND GLOSSARY","REPORTING A PROBLEM"})Check(topics.Any(t=>t.Key==key&&!string.IsNullOrWhiteSpace(t.Body)),"Missing guide topic: "+key);
    Check(topics.Select(t=>t.Key).Distinct().Count()==topics.Count,"Duplicate guide topics");Check(HelpCatalog.Search(topics,"PINCH").First().Key=="IMAGE PREVIEW AND TABLES","Full-text search is case sensitive or ranks preview text poorly");Check(HelpCatalog.Search(topics,"USB volume").Any(t=>t.Key=="USB TRANSFER AND SAVED TELESCOPES"),"Search does not combine title and body");Check(HelpCatalog.Search(topics,"no-such-help-topic-123456").Count==0&&HelpCatalog.Search(topics," \t").Count==topics.Count,"Empty search and no-result behavior");
    Check(HelpCatalog.Search(topics,"backup").First().Key=="ARCHIVE SAFETY AND BACKUPS"&&HelpCatalog.Search(topics,"bug").First().Key=="REPORTING A PROBLEM"&&HelpCatalog.Search(topics,"smart app control").First().Key=="WINDOWS SMART APP CONTROL","Titles and keywords do not outrank passing mentions");
    var parsed=HelpCatalog.Parse("About\r\n\r\nFIRST TOPIC\r\nFirst paragraph.\r\n\r\nSecond paragraph.\r\nSECOND TOPIC\r\nLast paragraph.");Check(parsed.Count==3&&parsed[1].Body.Contains("Second paragraph.")&&parsed[2].Body=="Last paragraph.","Help parser lost paragraphs or final topic");
   });
   Test("Guide topics carry their category, summary, walkthrough and related topics",()=>{
    var topic=HelpCatalog.Parse("FIRST TOPIC\n@title First steps\n@category Import\n@summary One line.\n@walkthrough first-import\n@related SECOND TOPIC | THIRD\n@keywords alpha beta\nBody text.\nSECOND TOPIC\n@category Not a category\nMore.").ToList();
    Check(topic.Count==2&&topic[0].Title=="First steps"&&topic[0].Category=="Import"&&topic[0].Summary=="One line."&&topic[0].Walkthrough=="first-import"&&topic[0].Related.SequenceEqual(new[]{"SECOND TOPIC","THIRD"})&&topic[0].Keywords=="alpha beta","Topic metadata was not read");
    Check(topic[0].Body=="Body text."&&!topic[0].Body.Contains("@"),"Metadata leaked into the article");Check(topic[1].Category==HelpCatalog.Reference&&topic[1].Title=="Second Topic","Unknown categories or default titles wrong");
    Check(HelpCatalog.Search(topic,"beta").Single()==topic[0],"Keywords are not searchable");
   });
   Test("Help formatting preserves wrapped instructions, lists, callouts and links",()=>{
    var blocks=HelpCatalog.Blocks("A wrapped introduction\ncontinues here.\n\n## Actions\n- **Scan folder** checks files.\n  It keeps originals.\n- Review results.\n3. Choose `Export`.\n4. Pick a folder.\nA closing paragraph.");
    Check(blocks.Count==7&&blocks[0].Text=="A wrapped introduction continues here."&&blocks[1].Kind==HelpBlockKind.Heading,"Hard wrapping or section boundaries changed");
    Check(blocks[2].Kind==HelpBlockKind.Bullet&&blocks[2].Text.Contains("It keeps originals.")&&blocks[3].Kind==HelpBlockKind.Bullet,"Bullets lost continuation text or merged together");
    Check(blocks[4].Kind==HelpBlockKind.Numbered&&blocks[4].Number==3&&blocks[5].Number==4&&blocks[6].Kind==HelpBlockKind.Paragraph,"Ordered items swallowed closing prose or lost numbering");
    var callouts=HelpCatalog.Blocks("> Tip: Use **Clear**.\n  It resets filters.\n> Important: Keep a backup.\n> Maybe: not a callout.");
    Check(callouts.Count==3&&callouts[0].Kind==HelpBlockKind.Callout&&callouts[0].Label=="Tip"&&callouts[0].Text=="Use **Clear**. It resets filters."&&callouts[1].Label=="Important"&&callouts[2].Kind==HelpBlockKind.Paragraph,"Callouts were not recognised");
    string plain=HelpCatalog.PlainText("## Controls\n- **Scan folder** then `Ctrl+F`.\n2. Keep originals.\n> Note: See [the fix](walkthrough:import-disabled).");Check(plain.Contains("Controls")&&plain.Contains("• Scan folder then Ctrl+F.")&&plain.Contains("2. Keep originals.")&&plain.Contains("Note: See the fix.")&&!plain.Contains("**")&&!plain.Contains("##")&&!plain.Contains("`")&&!plain.Contains("walkthrough:"),"Saved text exposes formatting markers or loses instructions");
    Check(HelpCatalog.Links("Try [one](topic:START HERE) or [two](walkthrough:tour), not [three](https://example.com).").SequenceEqual(new[]{"topic:START HERE","walkthrough:tour"}),"Help links were not found");
   });
   Test("Every guide topic is filed, summarised and links only to topics and walkthroughs that exist",()=>{
    var topics=HelpCatalog.Load();
    Check(topics.Count==39&&HelpCatalog.Blocks(topics.Single(t=>t.Key=="START HERE").Body).Count(b=>b.Kind==HelpBlockKind.Numbered)==5,"Bundled guide lost topics or first-import steps: "+topics.Count);
    Check(HelpCatalog.Blocks(topics.Single(t=>t.Key=="KEYBOARD SHORTCUTS").Body).Count(b=>b.Kind==HelpBlockKind.Bullet)==5&&HelpCatalog.Blocks(topics.Single(t=>t.Key=="TROUBLESHOOTING").Body).Count(b=>b.Kind==HelpBlockKind.Bullet)==13,"Shortcut or troubleshooting entries lost list structure");
    foreach(var topic in topics){
     Check(!string.IsNullOrWhiteSpace(topic.Summary)&&topic.Summary.Length<=110,"Topic needs a one-line summary: "+topic.Key);
     Check(HelpCatalog.Categories.Contains(topic.Category),"Topic is not filed under a category: "+topic.Key);
     foreach(string related in topic.Related)Check(related!=topic.Key&&topics.Any(t=>t.Key==related),"Related topic missing for "+topic.Key+": "+related);
     Check(topic.Walkthrough==null||WalkthroughCatalog.Find(topic.Walkthrough)!=null,"Unknown walkthrough for "+topic.Key+": "+topic.Walkthrough);
     foreach(string link in HelpCatalog.Links(topic.Body))Check(link.StartsWith("topic:")?topics.Any(t=>"topic:"+t.Key==link):WalkthroughCatalog.Find(link.Substring("walkthrough:".Length))!=null,"Broken link in "+topic.Key+": "+link);
     // Short paragraphs: long prose is split into steps, lists and sections.
     foreach(var block in HelpCatalog.Blocks(topic.Body).Where(b=>b.Kind==HelpBlockKind.Paragraph))Check(block.Text.Length<=420,"Paragraph too long in "+topic.Key+": "+block.Text.Substring(0,Math.Min(60,block.Text.Length)));
    }
    foreach(string category in HelpCatalog.Categories)Check(topics.Any(t=>t.Category==category),"Empty guide category: "+category);
    Check(WalkthroughCatalog.Fixes().All(g=>topics.Any(t=>t.Walkthrough==g.Id||HelpCatalog.Links(t.Body).Contains("walkthrough:"+g.Id))),"A guided fix is not reachable from the guide");
    Check(topics.Single(t=>t.Key=="IMAGE PREVIEW AND TABLES").Body.Contains("Individual targets: stacks above lights")&&topics.Single(t=>t.Key=="STACKING PROJECTS AND SESSIONS").Body.Contains("Open with… after export"),"Workflow notes were lost");
    Check(!topics.Any(t=>new[]{"Sky map:","skymap","sky globe","## Capture sky"}.Any(text=>t.Body.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0)),"Skymap instructions remain in the interactive guide");
   });
   Test("Walkthroughs are complete, use known actions and checks, and end fixes with a problem report",()=>{
    var guides=WalkthroughCatalog.All();var actions=new[]{"page-selector","report","activity","browse-source","scan","clear-search","clear-import-search","refresh-usb","all-targets","connect-network","export","backup"};var checks=new[]{"repository","idle","source","scanned","ready","unfiltered","all-targets","has-files","preview-on","one-file","selection","location","solver","usb","backup"};
    Check(guides.Select(g=>g.Id).Distinct().Count()==guides.Length&&WalkthroughCatalog.Fixes().Count()>=8&&WalkthroughCatalog.Tours().Count()>=3,"Walkthrough catalogue incomplete");
    foreach(var guide in guides){
     Check(!string.IsNullOrWhiteSpace(guide.Title)&&!string.IsNullOrWhiteSpace(guide.Summary)&&guide.Steps.Length>=2,"Walkthrough incomplete: "+guide.Id);
     foreach(var step in guide.Steps){
      Check(!string.IsNullOrWhiteSpace(step.Title)&&!string.IsNullOrWhiteSpace(step.Body)&&!string.IsNullOrWhiteSpace(step.Target)&&step.Page>=-1&&step.Page<=2,"Step incomplete in "+guide.Id+": "+step.Title);
      Check((step.Action==null)==(step.Do==null),"Step action has no label or no behaviour in "+guide.Id+": "+step.Title);
      Check(step.Do==null||actions.Contains(step.Do)||step.Do.StartsWith("menu:")||step.Do.StartsWith("page:")||step.Do.StartsWith("preferences:"),"Unknown walkthrough action: "+step.Do);
      Check(step.Check==null||checks.Contains(step.Check),"Unknown walkthrough check: "+step.Check);
      Check(HelpCatalog.Blocks(step.Body).All(b=>b.Kind!=HelpBlockKind.Paragraph||b.Text.Length<=320),"Walkthrough step is a wall of text: "+step.Title);
     }
     if(guide.Fix)Check(guide.Steps.Last().Do=="report","Guided fix does not end with a problem report: "+guide.Id);
    }
    Check(WalkthroughCatalog.Find(WalkthroughCatalog.FirstImport).Steps.Length==4&&WalkthroughCatalog.Find(WalkthroughCatalog.Tour).Steps.Length==8,"Tour or first-import steps changed");
    Check(WalkthroughCatalog.Search("greyed out").Any(g=>g.Id=="import-disabled")&&WalkthroughCatalog.Search("wifi").Any(g=>g.Id=="network-telescope")&&WalkthroughCatalog.Search("").Count==0&&!WalkthroughCatalog.Search("start").Any(g=>g.Id==WalkthroughCatalog.FirstImport),"Walkthrough search wrong");
   });
  }
 }
}
