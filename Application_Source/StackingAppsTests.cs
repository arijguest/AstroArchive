using System;
using System.IO;
namespace AstroArchive {
 public partial class Tests {
  static void StackingAppsTests(){
   Test("Stacking app launches retain Unicode paths and use only documented arguments",()=>{
    string folder=Path.Combine(root,"Stacking project Ω with spaces"),apps=Path.Combine(root,"Stacking apps Ω");Directory.CreateDirectory(folder);Directory.CreateDirectory(apps);File.WriteAllText(Path.Combine(folder,"manifest.json"),"{}");
    foreach(StackingApplication app in Enum.GetValues(typeof(StackingApplication))){string executable=Path.Combine(apps,app==StackingApplication.Siril?"siril.exe":app==StackingApplication.StackingWizard?"StackingWizard.exe":"Other processor.EXE");File.WriteAllText(executable,"executable fixture");var launch=StackingApps.LaunchInfo(executable,folder,app);Check(launch.FileName==Path.GetFullPath(executable)&&launch.WorkingDirectory==Path.GetFullPath(folder)&&!launch.UseShellExecute,"Application or working directory changed");Check(launch.Arguments==(app==StackingApplication.Siril?"--directory "+SirilHandoff.QuoteArgument(Path.GetFullPath(folder)):""),"Undocumented import argument supplied");}
   });
   Test("Unavailable apps and incomplete exports cannot be launched or change exported files",()=>{
    string folder=Path.Combine(root,"guarded stacking project"),executable=Path.Combine(root,"guarded-siril.exe");Directory.CreateDirectory(folder);File.WriteAllText(executable,"fixture");File.WriteAllText(Path.Combine(folder,"source.fit"),"verified copy");
    Expect(()=>StackingApps.LaunchInfo(executable,folder,StackingApplication.Other),"Folder without manifest accepted");File.WriteAllText(Path.Combine(folder,"manifest.json"),"{}");File.WriteAllText(Path.Combine(folder,"INCOMPLETE.txt"),"pending");Expect(()=>StackingApps.LaunchInfo(executable,folder,StackingApplication.Other),"Incomplete export accepted");File.Delete(Path.Combine(folder,"INCOMPLETE.txt"));Expect(()=>StackingApps.LaunchInfo(executable,folder,StackingApplication.Siril),"Non-GUI Siril executable accepted");Expect(()=>StackingApps.LaunchInfo(executable+".missing",folder,StackingApplication.Other),"Missing app accepted");string shortcut=Path.Combine(root,"stacker.cmd");File.WriteAllText(shortcut,"fixture");Expect(()=>StackingApps.LaunchInfo(shortcut,folder,StackingApplication.Other),"Shell command accepted as an app");Expect(()=>StackingApps.LaunchInfo(executable,folder,(StackingApplication)100),"Unknown app route accepted");Check(File.ReadAllText(Path.Combine(folder,"source.fit"))=="verified copy"&&File.ReadAllText(Path.Combine(folder,"manifest.json"))=="{}","Launch preparation modified an export");
   });
   Test("Stacking app preferences round-trip and preserve earlier Siril settings",()=>{
    var old=Util.Deserialize<Settings>("{\"SirilExecutable\":\"C:\\\\Apps\\\\siril.exe\"}");Check(old.SirilExecutable=="C:\\Apps\\siril.exe"&&old.StackingWizardExecutable==null&&old.OtherStackingExecutable==null,"Older settings changed");old.StackingWizardExecutable="C:\\Stacking apps Ω\\StackingWizard.exe";old.OtherStackingExecutable="C:\\Other apps\\Processor.exe";var restored=Util.Deserialize<Settings>(Util.Serialize(old));Check(restored.SirilExecutable==old.SirilExecutable&&restored.StackingWizardExecutable==old.StackingWizardExecutable&&restored.OtherStackingExecutable==old.OtherStackingExecutable,"Selected app locations were lost");
   });
  }
 }
}
