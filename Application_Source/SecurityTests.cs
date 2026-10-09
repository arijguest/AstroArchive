// Harmless fixtures for linked archive paths, DLL substitution and CSV formulas.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
namespace AstroArchive {
 public partial class Tests {
  static void SecurityJunction(string path,string target){
   string command="New-Item -ItemType Junction -Path '"+path.Replace("'","''")+"' -Target '"+target.Replace("'","''")+"' | Out-Null";
   string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
   using(var process=Process.Start(new ProcessStartInfo(powershell,"-NoProfile -NonInteractive -Command \""+command+"\""){UseShellExecute=false,CreateNoWindow=true})){
    if(!process.WaitForExit(15000)){process.Kill();throw new Exception("Junction fixture creation timed out.");}
    Check(process.ExitCode==0&&Directory.Exists(path),"Junction fixture could not be created");
   }
  }
  static int SecuritySqliteProbe(string directory){
   using(var db=new Database(Path.Combine(directory,"probe.sqlite"))){}
   string loaded=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(m=>m.ModuleName.Equals("winsqlite3.dll",StringComparison.OrdinalIgnoreCase)).FileName;
   File.WriteAllText(Path.Combine(directory,"loaded-sqlite.txt"),loaded);
   return loaded.Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"winsqlite3.dll"),StringComparison.OrdinalIgnoreCase)?0:1;
  }
  static List<List<string>> SecurityCsvRows(string text){
   var rows=new List<List<string>>();var fields=new List<string>();var field=new StringBuilder();bool quoted=false;
   for(int i=0;i<text.Length;i++){char c=text[i];
    if(c=='"'){if(quoted&&i+1<text.Length&&text[i+1]=='"'){field.Append('"');i++;}else quoted=!quoted;}
    else if(c==','&&!quoted){fields.Add(field.ToString());field.Clear();}
    else if((c=='\r'||c=='\n')&&!quoted){fields.Add(field.ToString());rows.Add(fields);fields=new List<string>();field.Clear();if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;}
    else field.Append(c);
   }
   Check(!quoted,"CSV ended inside a quoted field");if(field.Length>0||fields.Count>0){fields.Add(field.ToString());rows.Add(fields);}return rows;
  }
  static void SecurityRegressions(){
   Test("Catalogue CSV treats imported formulas as text and preserves numeric values",()=>{
    string source=Path.Combine(root,"security-csv-source");Directory.CreateDirectory(source);
    string[] values={"=1+1","+1+1","-1+1","@SUM(1)","  =1+1","＝1+1","＋1+1","－1+1","＠SUM(1)"};
    for(int i=0;i<values.Length;i++){
     if(values[i].All(c=>c<128))Write(Path.Combine(source,"Light_M45_"+i+".fit"),16,16,(x,y)=>1000,new Dictionary<string,string>{{"CAMMODEL","'"+values[i]+"'"}});
     else{
      // FITS headers are ASCII; XISF is the real Unicode metadata input route.
      var xml=new System.Xml.Linq.XElement("xisf",new System.Xml.Linq.XElement("Image",new System.Xml.Linq.XAttribute("geometry","16:16:1"),new System.Xml.Linq.XAttribute("sampleFormat","UInt8"),new System.Xml.Linq.XAttribute("location","attachment:4096:256"),new System.Xml.Linq.XElement("FITSKeyword",new System.Xml.Linq.XAttribute("name","CAMMODEL"),new System.Xml.Linq.XAttribute("value",values[i]))));
      byte[] header=Encoding.UTF8.GetBytes(xml.ToString());using(var writer=new BinaryWriter(File.Create(Path.Combine(source,"Light_M45_"+i+".xisf")))){writer.Write(Encoding.ASCII.GetBytes("XISF0100"));writer.Write(header.Length);writer.Write(0);writer.Write(header);while(writer.BaseStream.Position<4096)writer.Write((byte)0);writer.Write(new byte[256]);}
     }
    }
    using(var repo=new Repository(Path.Combine(root,"security-csv-repo"))){
     var plan=repo.Scan(source,"Security scope","Auto",ct,NoProgress);Check(plan.Errors.Count==0&&plan.Frames.Count==values.Length,"Formula fixtures did not scan");
     Check(repo.Import(plan.Frames,ct,NoProgress).Imported==values.Length,"Formula fixtures did not import");
     var frames=repo.All();foreach(var frame in frames){frame.Temperature=-12.5;frame.Offset=-1;frame.CameraId="safe,\"quoted\"";repo.Save(frame);}
     string output=Path.Combine(root,"security-catalogue.csv");repo.ExportIndex(output);
     var rows=SecurityCsvRows(File.ReadAllText(output)).Skip(1).ToList();
     Check(rows.Count==values.Length&&rows.All(r=>r.Count==41),"CSV field boundaries changed");
     foreach(var row in rows){Check(row[27].StartsWith("\t"),"Imported metadata became a formula: "+row[27]);Check(row[18]=="-12.5"&&row[30]=="-1","Negative numeric values became text");Check(row[28]=="safe,\"quoted\"","CSV quoting changed");}
     Check(repo.All().All(f=>!f.CameraModel.StartsWith("\t")),"Export altered archived metadata");
    }
    foreach(string value in new[]{"\t=1+1","\r=1+1","\n=1+1"," \t=1+1"})Check(Repository.CatalogueCsvField(value).StartsWith("\"\t"),"Leading controls bypassed CSV protection");
    Check(Repository.CatalogueCsvField("M45")=="\"M45\""&&Repository.CatalogueCsvField(null)=="\"\"","Ordinary CSV text changed");
    string volume=Path.GetPathRoot(root);Repository.CheckManagedPath(Path.Combine(volume,"AstroArchive-path-probe-"+Guid.NewGuid().ToString("N")),volume);
   });
   WindowsTest("SQLite ignores a library placed beside the executable",()=>{
    string folder=Path.Combine(root,"security-sqlite");Directory.CreateDirectory(folder);
    string executable=Path.Combine(folder,"SqliteProbe.exe");File.Copy(typeof(Tests).Assembly.Location,executable);
    // A genuine Windows DLL safely reproduces the substitution route.
    File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"winsqlite3.dll"),Path.Combine(folder,"winsqlite3.dll"));
    using(var process=Process.Start(new ProcessStartInfo(executable,"--sqlite-load-probe "+SirilHandoff.QuoteArgument(folder)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder})){
     if(!process.WaitForExit(10000)){process.Kill();throw new Exception("SQLite probe timed out.");}
     Check(process.ExitCode==0,"SQLite loaded the adjacent library: "+(File.Exists(Path.Combine(folder,"loaded-sqlite.txt"))?File.ReadAllText(Path.Combine(folder,"loaded-sqlite.txt")):"probe failed"));
    }
   });
   WindowsTest("Imports reject linked capture, staging and sidecar folders without writing outside the archive",()=>{
    foreach(string relative in new[]{"Targets","Calibration",Path.Combine(".astroarchive","staging"),Path.Combine(".astroarchive","session-metadata")}){
     string id=Guid.NewGuid().ToString("N"),source=Path.Combine(root,"security-link-source-"+id),outside=Path.Combine(root,"security-link-outside-"+id);
     Directory.CreateDirectory(source);Directory.CreateDirectory(outside);string original=Path.Combine(source,"Light_M45.fit");
     Write(original,16,16,(x,y)=>1000,new Dictionary<string,string>{{"IMAGETYP",relative=="Calibration"?"'Dark'":"'Light'"}});
     if(relative.EndsWith("session-metadata"))File.WriteAllText(Path.Combine(source,"Light_M45.json"),"{}");
     using(var repo=new Repository(Path.Combine(root,"security-link-repo-"+id))){
      var plan=repo.Scan(source,"Security scope","Auto",ct,NoProgress);string link=Path.Combine(repo.Root,relative);SecurityJunction(link,outside);
      try{var result=repo.Import(plan.Frames,ct,NoProgress,new ImportOptions{DeleteOriginals=true,SourceRoot=source,Workers=1});Check(result.Failed==1&&result.Imported==0&&repo.All().Count==0,"Linked import was accepted: "+relative);Check(File.Exists(original),"Failed import deleted its original");Check(!Directory.EnumerateFileSystemEntries(outside).Any(),"Import wrote through a link: "+relative);}
      finally{Directory.Delete(link);}
     }
    }
   });
   WindowsTest("Repository opening and path validation reject linked metadata and directory leaves",()=>{
    string archive=Path.Combine(root,"security-meta-repo"),outside=Path.Combine(root,"security-meta-outside");Directory.CreateDirectory(archive);Directory.CreateDirectory(outside);
    string link=Path.Combine(archive,".astroarchive");SecurityJunction(link,outside);
    try{Expect(()=>{using(var repo=new Repository(archive)){}},"Linked metadata directory was accepted");Expect(()=>Repository.CheckManagedPath(link,archive),"Linked directory leaf was accepted");Check(!Directory.EnumerateFileSystemEntries(outside).Any(),"Opening a linked archive wrote external metadata");}
    finally{Directory.Delete(link);}
    Check(!FileStamp.IsRedirectTag(0x9000001a)&&FileStamp.IsRedirectTag(0xa0000003),"Cloud and junction reparse tags were confused");
   });
  }
 }
}
