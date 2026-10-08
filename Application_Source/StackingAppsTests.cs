using System;
using System.IO;
namespace AstroArchive {
 public partial class Tests {
  static void StackingAppsTests(){
   Test("Stacking app launches retain Unicode paths and use only documented arguments",()=>{
    string folder=Path.Combine(root,"Stacking project Ω with spaces"),apps=Path.Combine(root,"Stacking apps Ω");Directory.CreateDirectory(folder);Directory.CreateDirectory(apps);
    foreach(StackingApplication app in Enum.GetValues(typeof(StackingApplication))){string executable=Path.Combine(apps,app==StackingApplication.Siril?"siril.exe":app==StackingApplication.StackingWizard?"StackingWizard.exe":"Other processor.EXE");File.WriteAllText(executable,"executable fixture");var launch=StackingApps.LaunchInfo(executable,folder,app);Check(launch.FileName==Path.GetFullPath(executable)&&launch.WorkingDirectory==Path.GetFullPath(folder)&&!launch.UseShellExecute,"Application or working directory changed");Check(launch.Arguments==(app==StackingApplication.Siril?"--directory "+SirilHandoff.QuoteArgument(Path.GetFullPath(folder)):""),"Undocumented import argument supplied");}
   });
   Test("Unavailable apps and incomplete exports cannot be launched or change exported files",()=>{
    string folder=Path.Combine(root,"guarded stacking project"),executable=Path.Combine(root,"guarded-siril.exe");Directory.CreateDirectory(folder);File.WriteAllText(executable,"fixture");File.WriteAllText(Path.Combine(folder,"source.fit"),"verified copy");
    Check(StackingApps.LaunchInfo(executable,folder,StackingApplication.Other).WorkingDirectory==Path.GetFullPath(folder),"Plain export without metadata rejected");Expect(()=>StackingApps.LaunchInfo(executable,folder+"-missing",StackingApplication.Other),"Missing export accepted");File.WriteAllText(Path.Combine(folder,"manifest.json"),"{}");File.WriteAllText(Path.Combine(folder,"INCOMPLETE.txt"),"pending");Expect(()=>StackingApps.LaunchInfo(executable,folder,StackingApplication.Other),"Incomplete export accepted");File.Delete(Path.Combine(folder,"INCOMPLETE.txt"));Expect(()=>StackingApps.LaunchInfo(executable,folder,StackingApplication.Siril),"Non-GUI Siril executable accepted");Expect(()=>StackingApps.LaunchInfo(executable+".missing",folder,StackingApplication.Other),"Missing app accepted");string shortcut=Path.Combine(root,"stacker.cmd");File.WriteAllText(shortcut,"fixture");Expect(()=>StackingApps.LaunchInfo(shortcut,folder,StackingApplication.Other),"Shell command accepted as an app");Expect(()=>StackingApps.LaunchInfo(executable,folder,(StackingApplication)100),"Unknown app route accepted");Check(File.ReadAllText(Path.Combine(folder,"source.fit"))=="verified copy"&&File.ReadAllText(Path.Combine(folder,"manifest.json"))=="{}","Launch preparation modified an export");
   });
   Test("Open after export follows input mode rejection and conversion choices",()=>{
    var stack=new Frame{Kind="Stack",OriginalName="Stack.fit",Format="FITS"};var light=new Frame{Kind="Light",OriginalName="Light.fit"};var options=new ExportOptions{Mode="Files"};
    Check(SirilHandoff.StackAfterExport(new[]{stack,light},options)==stack,"Mixed file selection lost its single stack");Check(SirilHandoff.StackAfterExport(new[]{stack,stack.Clone()},options)==null,"Multiple stacks offered an ambiguous launch");options.Mode="Subs";Check(SirilHandoff.StackAfterExport(new[]{stack,light},options)==null,"Subs-only export offered a stack launch");
    options.Mode="Stacks";stack.Rejected=true;Check(SirilHandoff.StackAfterExport(new[]{stack},options)==null,"Excluded stack offered a launch");options.IncludeRejected=true;Check(SirilHandoff.StackAfterExport(new[]{stack},options)==stack,"Explicitly included stack could not open");
    stack.OriginalName="Stack.xisf";stack.Format="XISF";Check(SirilHandoff.StackAfterExport(new[]{stack},options)==null,"Unconverted XISF offered a FITS launch");options.ConvertToFits=true;Check(SirilHandoff.StackAfterExport(new[]{stack},options)==stack,"Converted stack was not offered");options.Mode="Files";Check(SirilHandoff.StackAfterExport(new[]{stack},options)==null,"Original-file export assumed conversion");
    stack.OriginalName="Stack.fit.gz";stack.Format="FITS";Check(SirilHandoff.StackAfterExport(new[]{stack},options)==null,"Compressed original offered an unsupported direct open");
   });
   Test("Stacking app preferences round-trip and preserve earlier Siril settings",()=>{
    var old=Util.Deserialize<Settings>("{\"SirilExecutable\":\"C:\\\\Apps\\\\siril.exe\"}");Check(old.SirilExecutable=="C:\\Apps\\siril.exe"&&old.StackingWizardExecutable==null&&old.OtherStackingExecutable==null,"Older settings changed");old.ExportWorkingDirectory=Path.Combine(root,"Export workspace Ω");old.StackingWizardExecutable="C:\\Stacking apps Ω\\StackingWizard.exe";old.OtherStackingExecutable="C:\\Other apps\\Processor.exe";var restored=Util.Deserialize<Settings>(Util.Serialize(old));Check(restored.SirilExecutable==old.SirilExecutable&&restored.StackingWizardExecutable==old.StackingWizardExecutable&&restored.OtherStackingExecutable==old.OtherStackingExecutable&&restored.ExportWorkingDirectory==old.ExportWorkingDirectory,"Selected app locations or working directory were lost");
   });
  }
 }
}
