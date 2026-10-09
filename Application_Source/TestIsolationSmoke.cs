using System;
using System.IO;
using System.Threading;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeTestIsolation(string output){
   string state=Path.GetFullPath(Path.Combine(output,"test-state"));
   if(!Util.Within(config,state)||!string.Equals(Repository.LocalIndexBase,Path.Combine(state,"repositories"),StringComparison.OrdinalIgnoreCase))throw new Exception("UI checks share installed application state.");
   SaveSettings();SaveSettings();
   if(!File.Exists(config)||!File.Exists(config+".bak"))throw new Exception("UI settings are not persisted inside the test state folder.");
   using(var fixture=new Repository(Path.Combine(output,"state-isolation-repository"))){
    if(!Util.Within(fixture.WorkingIndex,Repository.LocalIndexBase))throw new Exception("UI checks share installed repository indexes.");
    fixture.Checkpoint(CancellationToken.None);
   }
   File.WriteAllText(Path.Combine(output,"test-isolation-smoke.txt"),"PASS: UI settings, settings backups and working repository indexes use the selected test output folder, independently of installed application state.");
  }
 }
}
