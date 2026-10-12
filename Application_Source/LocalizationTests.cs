using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
namespace AstroArchive {
 public partial class Tests {
  static void LocalizationTests(){
   try{
    Test("Interface language follows Windows unless a language is chosen",()=>{
     Check(Localization.Resolve(null,new CultureInfo("de-DE"))=="de"&&Localization.Resolve("System",new CultureInfo("ja-JP"))=="ja"&&Localization.Resolve("",new CultureInfo("fr-CA"))=="fr","Windows language not followed");
     Check(Localization.Resolve(null,new CultureInfo("zh-CN"))=="zh-Hans"&&Localization.Resolve(null,new CultureInfo("zh-SG"))=="zh-Hans"&&Localization.Resolve(null,new CultureInfo("zh-TW"))=="en"&&Localization.Resolve(null,new CultureInfo("zh-HK"))=="en","Chinese scripts not separated");
     Check(Localization.Resolve(null,new CultureInfo("pt-BR"))=="en"&&Localization.Resolve("it",new CultureInfo("de-DE"))=="it"&&Localization.Resolve("xx",null)=="en"&&Localization.Resolve("qps",null)=="qps","Explicit or unsupported languages wrong");
    });
    Test("Translations cover exact text, counts, parts and sentences, and fall back to English",()=>{
     Localization.UseTable("de","# comment\nCancel\tAbbrechen\nRepository size\tRepository-Größe\nFiles\tDateien\n{#0} subs\t{0} Subs\nImport {#0} files\t{0} Dateien importieren\n{0} of {1}\t{1} von {0}\nMoon: {0}\tMond: {0}\nFull Moon\tVollmond\nBackups are on.\tSicherungen sind aktiv.\nLine one\\nLine two\tZeile eins\\nZeile zwei\nBroken {0}\tKaputt {1}\nEmpty\t\nPath C:\\\\new\tPfad C:\\\\neu\n");
     Check(Localization.T("Cancel")=="Abbrechen"&&Localization.T("Files:")=="Dateien:"&&Localization.T("  Cancel ")=="  Abbrechen ","Exact or padded text wrong: "+Localization.T("  Cancel "));
     Check(Localization.T("12 subs")=="12 Subs"&&Localization.T("1,204 subs")=="1,204 Subs"&&Localization.T("many subs")=="many subs","Number patterns wrong: "+Localization.T("many subs"));
     Check(Localization.T("Import 3 files")=="3 Dateien importieren"&&Localization.T("2 of 9")=="9 von 2","Placeholder order wrong");
     Check(Localization.T("Moon: Full Moon")=="Mond: Vollmond","Inserted values not translated");
     Check(Localization.T("12 subs · 3 stacks")=="12 Subs · 3 stacks"&&Localization.T("Cancel\n4 subs")=="Abbrechen\n4 Subs","Composed lines not split");
     Check(Localization.T("Backups are on. Cancel")=="Sicherungen sind aktiv. Abbrechen"&&Localization.T("Backups are on. Untranslated sentence.")=="Sicherungen sind aktiv. Untranslated sentence.","Sentences not split");
     Check(Localization.T("Line one\nLine two")=="Zeile eins\nZeile zwei","Escaped line breaks not read");
     Check(Localization.T("Path C:\\new")=="Pfad C:\\neu","Escaped backslashes misread");
     Check(Localization.T("Broken value")=="Broken value"&&Localization.T("Empty")=="Empty","Invalid entries not ignored");
     Check(Localization.T(null)==null&&Localization.T("")==""&&Localization.Has("Cancel")&&!Localization.Has("Save"),"Empty or lookup checks wrong");
     Check(Localization.F("Import {0} files",5)=="Import 5 files","Format fallback wrong");
     var dated=new List<Frame>{new Frame{Target="M31",Kind="Light",Exposure=60,AcquisitionDate="2026-10-01",Night="2026-10-01"},new Frame{Target="M31",Kind="Light",Exposure=60,AcquisitionDate="2026-10-03",Night="2026-10-03"}};
     Localization.UseTable("ja","");Check(TargetCards.Build(dated,"M31").DateRange=="10月1日 – 2026年10月3日","Japanese card dates wrong: "+TargetCards.Build(dated,"M31").DateRange);
     Localization.UseTable("de","");Check(TargetCards.Build(dated,"M31").DateRange.StartsWith("1. Okt"),"German card dates wrong: "+TargetCards.Build(dated,"M31").DateRange);
     Localization.UseTable("de","Import\tImportieren\nGuide\tHilfe\nSettings\tEinstellungen\nSave as\tSpeichern_unter");
     Check(Localization.AccessText("_Import")=="_Importieren"&&Localization.AccessText("_Settings")=="Ein_stellungen"&&Localization.AccessText("_Guide")=="_Hilfe"&&Localization.AccessText("_Unknown")=="_Unknown"&&Localization.AccessText("Save _as")=="_Speichern__unter","Access keys not kept: "+Localization.AccessText("_Settings"));
     Localization.UseTable("ja","Import\tインポート");Check(Localization.AccessText("_Import")=="インポート(_I)","Japanese access key wrong");
     Localization.UseTable("qps","");Check(Localization.T("Save")=="［Save］"&&Localization.T("42")=="42","Pseudo-localisation wrong");
     Localization.Use("en");Check(Localization.English&&Localization.T("Cancel")=="Cancel"&&Localization.Count==0,"English not restored");
    });
    Test("Every bundled language loads and keeps placeholders, line breaks and dialog ellipses",()=>{
     var assembly=Assembly.GetExecutingAssembly();
     foreach(var language in Localization.Languages.Where(l=>l.Code!="en")){
      string text;using(var stream=assembly.GetManifestResourceStream("lang-"+language.Code+".txt")){Check(stream!=null,"Missing table: "+language.Code);using(var reader=new StreamReader(stream,Encoding.UTF8))text=reader.ReadToEnd();}
      int entries=0;var keys=new HashSet<string>(StringComparer.Ordinal);
      foreach(string raw in text.Split('\n')){
       string line=raw.TrimEnd('\r');if(line.Length==0||line.StartsWith("#"))continue;int tab=line.IndexOf('\t');Check(tab>0&&line.IndexOf('\t',tab+1)<0,language.Code+": malformed line "+line);
       string key=line.Substring(0,tab),value=line.Substring(tab+1);entries++;
       Check(value.Trim().Length>0,language.Code+": empty translation for "+key);Check(keys.Add(key),language.Code+": duplicate "+key);
       var wanted=Regex.Matches(key,@"\{#?(\d)\}").Cast<Match>().Select(m=>m.Groups[1].Value).OrderBy(v=>v).ToList();var given=Regex.Matches(value,@"\{(\d)\}").Cast<Match>().Select(m=>m.Groups[1].Value).OrderBy(v=>v).ToList();
       Check(wanted.SequenceEqual(given)&&value.IndexOf("{#",StringComparison.Ordinal)<0,language.Code+": placeholders differ for "+key);
       Check(Regex.Matches(key,@"\\n").Count==Regex.Matches(value,@"\\n").Count,language.Code+": line breaks differ for "+key);
       Check(!key.EndsWith("…")||value.EndsWith("…"),language.Code+": dialog ellipsis lost for "+key);
      }
      Localization.Use(language.Code);
      Check(Localization.Current==language.Code&&entries>1500&&Localization.Count==entries,language.Code+" did not load every entry: "+Localization.Count+" of "+entries);
      foreach(string common in new[]{"Cancel","Save","Settings","Back up archive…","Create target card…"})Check(Localization.T(common)!=common,language.Code+" leaves "+common+" untranslated");
      Check(Localization.T("12 subs")!="12 subs"&&Localization.T("12 subs").Contains("12"),language.Code+" count pattern missing");
     }
    });
   }finally{Localization.Use("en");}
  }
 }
}
