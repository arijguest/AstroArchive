using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  bool dumpStartupChecked;
  void AddDumpSettings(FormWindow dialog){
   dialog.Text("Dump folder",true);
   dialog.Text("Drop FITS files or telescope folders into Dump inside your archive. On opening the archive, AstroArchive sorts them and removes successfully verified inputs, including duplicates. Failed or unsupported files stay. Edit metadata to assign each physical telescope ID.");
   if(repo==null)return;
   dialog.Text(repo.DumpFolder);
   dialog.Button("Open dump folder",()=>{try{repo.EnsureDumpFolder();Process.Start(new ProcessStartInfo(repo.DumpFolder){UseShellExecute=true});}catch(Exception e){MessageBox.Show(dialog.Window,e.Message,"Dump folder unavailable");}});
   dialog.Button("Process dump folder now",()=>{dialog.Window.Close();ProcessDumpUi();});
  }
  void ProcessDumpUi(){
   if(repo==null||cancel!=null||closing)return;
   ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked=false;plan=null;BeginLive(true);DumpResult result=null;bool ignoreFailed=settings.IgnoreFailed;
   Run(ct=>{result=repo.ProcessDump(ct,Progress,settings.CopyWorkers,LiveFrame,ignoreFailed);return result.Summary;},summary=>{
    plan=result.Plan;FilterImports();
    L("ScanLabel").Text=summary;L("StatusLabel").Text=summary;
    if(result.NeedsReview)ShowReport("Dump folder: files retained for review",repo.LastReport);
   });
  }
 }
}
