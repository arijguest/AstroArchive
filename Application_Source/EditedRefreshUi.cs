using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  readonly LatestSearch<EditedGallery> editedRefreshWorker=new LatestSearch<EditedGallery>();
  string[] editedCaptureTargets=new string[0];
  DispatcherTimer editedRefreshTimer;int editedRefreshVersion;bool editedRefreshDisposed;
  bool CanRefreshEditedAutomatically {get{return editedReady&&!editedRefreshDisposed&&!closing&&!RepositoryOperationBlocked&&repo!=null&&Window.IsVisible&&Window.WindowState!=WindowState.Minimized&&((TabControl)Window.FindName("MainTabs")).SelectedIndex==2;}}
  void InitializeEditedRefresh(){
   editedRefreshTimer=new DispatcherTimer(DispatcherPriority.Background,Window.Dispatcher){Interval=TimeSpan.FromMilliseconds(250)};
   editedRefreshTimer.Tick+=async(s,e)=>{editedRefreshTimer.Stop();await RefreshEditedAutomatically();};
   Window.Activated+=(s,e)=>ScheduleEditedRefresh();Window.StateChanged+=(s,e)=>ScheduleEditedRefresh();Window.IsVisibleChanged+=(s,e)=>ScheduleEditedRefresh();
  }
  void ScheduleEditedRefresh(){
   if(editedRefreshTimer==null||editedRefreshDisposed)return;
   // Restore/page events coalesce; no disk reads or busy overlay on the input dispatcher.
   CancelAutomaticEditedRefresh();if(CanRefreshEditedAutomatically)editedRefreshTimer.Start();
  }
  void CancelAutomaticEditedRefresh(){
   editedRefreshVersion++;if(editedRefreshTimer!=null)editedRefreshTimer.Stop();editedRefreshWorker.Cancel();
  }
  async Task<bool> RefreshEditedAutomatically(Func<Repository,string[],CancellationToken,EditedGallery> read=null){
   if(!CanRefreshEditedAutomatically)return false;
   var repository=repo;var targets=editedCaptureTargets;var previous=editedImages;int version=++editedRefreshVersion;bool changed=false;
   try{
    var gallery=await editedRefreshWorker.Submit(token=>{var result=(read??EditedGallery.Read)(repository,targets,token);changed=!result.SameImages(previous);return result;});
    if(version!=editedRefreshVersion||repo!=repository||editedImages!=previous||!CanRefreshEditedAutomatically)return false;
    L("EditedSummary").ToolTip=gallery.Errors.Count==0?null:string.Join("\n",gallery.Errors);
    // Unchanged restores retain row objects, multiple selection, sorting and the live preview.
    if(!changed)return false;editedImages=gallery.Images;FilterEditedImages();return true;
   }catch(OperationCanceledException){return false;}
   catch(Exception error){if(version==editedRefreshVersion&&repo==repository&&CanRefreshEditedAutomatically)L("EditedSummary").ToolTip="Could not refresh edited images: "+error.Message;return false;}
  }
  void DisposeEditedRefresh(){editedRefreshDisposed=true;CancelAutomaticEditedRefresh();editedRefreshWorker.Dispose();}
 }
}
