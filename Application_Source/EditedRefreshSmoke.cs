using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeEditedRefresh(EditedProject project,string output){
   var release=new ManualResetEventSlim(false);var started=new ManualResetEventSlim(false);var state=Window.WindowState;string added=Path.Combine(repo.EditedProjectFolder(project),"M31_restore_stars.png");int reads=0;
   Func<Repository,string[],CancellationToken,EditedGallery> slow=(repository,targets,token)=>{Interlocked.Increment(ref reads);var gallery=EditedGallery.Read(repository,targets,token);started.Set();if(!release.Wait(10000))throw new Exception("Slow gallery fixture was not released");return gallery;};
   try{
    foreach(int page in new[]{0,1}){GoToPage(page);if(RefreshEditedAutomatically(slow).GetAwaiter().GetResult()||reads!=0||editedRefreshTimer.IsEnabled)throw new Exception("An unrelated page scanned the Edited gallery");}
    GoToPage(2);Window.WindowState=WindowState.Minimized;PumpPopupLayout();if(RefreshEditedAutomatically(slow).GetAwaiter().GetResult()||reads!=0)throw new Exception("Minimized window scanned the Edited gallery");
    Window.WindowState=state;PumpPopupLayout();CancelAutomaticEditedRefresh();
    var grid=G("EditedGrid");grid.SelectedItems.Clear();foreach(var row in grid.Items.Cast<EditedImage>().ToArray())grid.SelectedItems.Add(row);
    var rows=grid.ItemsSource;var selection=grid.SelectedItems.Cast<EditedImage>().ToArray();int preview=editedPreviewGeneration;var sorting=System.Windows.Data.CollectionViewSource.GetDefaultView(rows).SortDescriptions.ToArray();
    var refresh=RefreshEditedAutomatically(slow);WaitPreview(()=>started.IsSet,"Background gallery check did not start");
    bool clicked=false;var button=new Button();button.Click+=(s,e)=>clicked=true;Window.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));WaitPreview(()=>clicked,"Slow gallery read blocked input dispatch");
    if(!grid.IsEnabled||!EditedTargets.IsEnabled||!B("EditedAddButton").IsEnabled||refresh.IsCompleted)throw new Exception("Background gallery check blocked the existing controls");
    release.Set();WaitPreview(()=>refresh.IsCompleted,"Background gallery check did not complete");if(refresh.GetAwaiter().GetResult()||grid.ItemsSource!=rows||!selection.SequenceEqual(grid.SelectedItems.Cast<EditedImage>())||preview!=editedPreviewGeneration||!sorting.SequenceEqual(System.Windows.Data.CollectionViewSource.GetDefaultView(rows).SortDescriptions))throw new Exception("Unchanged restore rebuilt rows, selection, sorting or preview");
    // A completed slow scan must never overwrite a newer explicit refresh.
    release.Reset();started.Reset();var stale=RefreshEditedAutomatically(slow);WaitPreview(()=>started.IsSet,"Stale gallery fixture did not start");File.Copy(repo.EditedPath(project,project.Sources[0].RelativePath),added);RefreshEdited();rows=grid.ItemsSource;release.Set();WaitPreview(()=>stale.IsCompleted,"Canceled gallery check did not complete");if(stale.GetAwaiter().GetResult()||grid.ItemsSource!=rows||grid.Items.Count!=3)throw new Exception("Stale background result overwrote a manual refresh");
    // A real minimize/restore finds editor output without entering a busy state.
    File.Delete(added);RefreshEdited();Window.WindowState=WindowState.Minimized;PumpPopupLayout();File.Copy(repo.EditedPath(project,project.Sources[0].RelativePath),added);Window.WindowState=state;PumpPopupLayout();WaitPreview(()=>grid.Items.Count==3,"Restore did not discover a new editor output");
    if(cancel!=null||!grid.IsEnabled||repo.All().Count!=0)throw new Exception("Restore refresh blocked controls or changed the capture index");
    // Closing/switching a repository cannot apply a late snapshot from the old folder.
    release.Reset();started.Reset();var previous=repo;var switched=RefreshEditedAutomatically(slow);WaitPreview(()=>started.IsSet,"Repository switch fixture did not start");
    try{repo=null;release.Set();WaitPreview(()=>switched.IsCompleted,"Repository switch check did not complete");if(switched.GetAwaiter().GetResult())throw new Exception("Old repository snapshot was applied after switching");}finally{repo=previous;}
    File.WriteAllText(Path.Combine(output,"edited-refresh-smoke.txt"),"PASS Restore input dispatch during a held background scan; enabled controls; no scans on Repository/Import or while minimized; unchanged rows/multiselection/sort/preview retained; automatic output discovery; stale manual refresh and repository switch results discarded.");
   }finally{release.Set();CancelAutomaticEditedRefresh();Window.WindowState=state;if(File.Exists(added))File.Delete(added);RefreshEdited();}
  }
 }
}
