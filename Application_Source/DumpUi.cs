using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace AstroArchive {
 public partial class MainUi {
  bool dumpStartupChecked,operationBusy,dumpChecking;
  CancellationTokenSource dumpCheckCancel;
  bool OperationProgressVisible {get{return false;}}
  void InitializeProgressVisibility(){Window.StateChanged+=(s,e)=>UpdateProgressVisibility();Window.IsVisibleChanged+=(s,e)=>UpdateProgressVisibility();}
  void UpdateProgressVisibility(){
   ((Popup)Window.FindName("OperationPopup")).IsOpen=false;
   if(!Window.IsVisible||Window.WindowState==WindowState.Minimized){if(activityToast!=null)activityToast.Visibility=Visibility.Collapsed;if(filtersPopup!=null)filtersPopup.IsOpen=false;ClosePreviewDetails("");}
  }
  void OpenDumpProgress(string title="Processing Dump folder"){nextActivityTitle=title;}
  void CloseDumpProgress(){nextActivityTitle=null;}
  async void ProcessDumpUi(){
   if(repo==null||RepositoryOperationBlocked)return;
   var repository=repo;bool ignoreFailed=settings.IgnoreFailed,ignoreRaster=settings.IgnoreRasterImports;
   bool pending=false;dumpChecking=true;dumpCheckCancel=new CancellationTokenSource();var token=dumpCheckCancel.Token;SetBusy(true);
   try{pending=await Task.Run(()=>repository.HasPendingDumpFiles(token,ignoreFailed,ignoreRaster));}
   catch(OperationCanceledException){}
   catch(Exception error){if(repo==repository&&!closing)L("StatusLabel").Text="Dump folder check could not finish: "+error.Message;}
   finally{dumpChecking=false;dumpCheckCancel.Dispose();dumpCheckCancel=null;SetBusy(false);}
   if(closing){Window.Close();return;}
   if(closing||!Window.IsVisible)return;
   if(repo!=repository){ProcessDumpUi();return;}
   if(!pending||cancel!=null||releaseInstalling)return;
   if(!ConfirmSourceDeletion(repository.DumpFolder,true))return;
   if(repo!=repository||RepositoryOperationBlocked)return;
   OpenDumpProgress();
   ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked=false;plan=null;BeginLive(true);DumpResult result=null;
   int workers=settings.CopyWorkers;pendingImportRecord=new ImportResumeRecord{Kind="Dump",Title="Dump folder import",Repository=repository.Root,Source=repository.DumpFolder,Workers=workers,IgnoreFailed=ignoreFailed,IgnoreRaster=ignoreRaster,DeleteOriginals=true};
   Run(ct=>{result=repository.ProcessDump(ct,Progress,workers,LiveFrame,ignoreFailed,ignoreRaster);if(result.Import.Failed+result.Plan.Errors.Count>0)foregroundImportRecord.State="Interrupted";return result.Summary;},summary=>{
    plan=result.Plan;FilterImports();
    L("StatusLabel").Text=summary;
    if(result.NeedsReview)ShowReport("Dump folder: files retained for review",repo.LastReport);
   });
  }
 }
}
