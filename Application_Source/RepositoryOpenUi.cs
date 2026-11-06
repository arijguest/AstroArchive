using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  bool repositoryOpening;
  Repository browsedRepository;
  BrowseSnapshot browseSnapshot;

  BrowseSnapshot SnapshotFor(Repository repository){return browsedRepository==repository?browseSnapshot:null;}

  void PublishBrowseSnapshot(Repository repository,BrowseSnapshot snapshot){
   bool changed=!ReferenceEquals(SnapshotFor(repository),snapshot)||!ReferenceEquals(all,snapshot.Rows);
   browsedRepository=repository;browseSnapshot=snapshot;all=snapshot.Rows;
   if(!changed)return;
   ApplyAnnotations(all);ScheduleTotalityTags();ScheduleSolarMetadata();ScheduleRepositorySize();
   repositoryRows.Clear();
   for(int i=0;i<all.Count;i++)if(!string.IsNullOrEmpty(all[i].Hash))repositoryRows[all[i].Hash]=i;
   Filter(true,true);
  }

  void AdoptRepository(Repository candidate,bool save){
   if(repo!=candidate){
    if(repo!=null)repo.Dispose();
    repo=candidate;librarySelection.Clear();editedSelection.Clear();ResetOpeningSelection();UpdateSelectionCounters();
   }
   if(candidate.RecoveredTelescopeSettings!=null){
    settings=Util.Deserialize<Settings>(candidate.RecoveredTelescopeSettings);TelescopeProfiles.Initialize(settings);
    ApplyColumnLayout("FramesGrid",SavedColumnLayout("FramesGrid"));ApplyColumnLayout("ImportGrid",SavedColumnLayout("ImportGrid"));
    ((CheckBox)Window.FindName("IgnoreFailedCheck")).IsChecked=settings.IgnoreFailed;ReloadScopes(settings.SelectedTelescope,true);
   }
   if(!string.Equals(settings.Repository,repo.Root,StringComparison.OrdinalIgnoreCase))settings.LastBrowseTarget=null;
   settings.Repository=repo.Root;L("RepositoryLabel").Text=CompactPath(repo.Root);L("RepositoryLabel").ToolTip=repo.Root;
   if(save)SaveSettings();
   plan=null;
  }

  void OpenRepository(string path,bool save=true){
   if(repo!=null&&repo.Root.Equals(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))return;
   if(RepositoryOperationBlocked)throw new InvalidOperationException("Wait for the current operation before switching repositories.");
   if(BackgroundBusy)throw new InvalidOperationException("Wait for background processes to finish, or stop them in Activity, before switching repositories.");
   CancelAutomaticEditedRefresh();CancelPreview();CancelEditedPreview();CancelDetachedPreview();
   var candidate=new Repository(path);BrowseSnapshot snapshot;
   try{candidate.QualityCompareTarget=settings.QualityCompareTarget;candidate.EnsureDumpFolder();snapshot=candidate.ReadBrowseSnapshot(null);}
   catch{candidate.Dispose();throw;}
   AdoptRepository(candidate,save);PublishBrowseSnapshot(candidate,snapshot);RefreshEdited();FinishRepositoryOpen();
  }

  void FinishRepositoryOpen(){
   RebuildScopes(false);RefreshBackupPartial();SetBusy(false);
   if(navigationReady)BeginOpeningSelection(((TabControl)Window.FindName("MainTabs")).SelectedIndex);
   if(Window.IsLoaded){
    var opened=repo;
    Window.Dispatcher.BeginInvoke(new Action(()=>{if(!closing&&repo==opened)ProcessDumpUi();}));
   }
  }

  async Task OpenStartupRepository(string path){
   Repository candidate=null;
   try{
    if(closing)return;
    L("StatusLabel").Text="Opening your archive…";
    string lastTarget=settings.LastBrowseTarget;
    candidate=new Repository(path,true);
    candidate.QualityCompareTarget=settings.QualityCompareTarget;
    var first=await Task.Run(()=>{
     if(!Directory.Exists(candidate.Root))throw new DirectoryNotFoundException("The saved repository folder is unavailable.");
     candidate.InitializeIndex();candidate.EnsureDumpFolder();
     return candidate.OpeningRows(lastTarget);
    });
    if(closing)return;
    AdoptRepository(candidate,false);all=first;
    repositoryPageOpened=true;openingRepositorySelection=librarySelection.Count==0;
    if(!string.IsNullOrEmpty(lastTarget))viewHint=new ViewHint{Page=0,Target=lastTarget,Armed=true,At=DateTime.UtcNow};
    Filter(true);
    L("StatusLabel").Text=first.Count>0?"Showing initial captures while the full archive loads…":"Loading archive records…";

    var snapshot=await Task.Run(()=>candidate.ReadBrowseSnapshot(null));
    if(closing)return;
    repositoryOpening=false;
    PublishBrowseSnapshot(candidate,snapshot);
    // Edited has a separate, automatic refresh when its page becomes visible.
    ScheduleEditedRefresh();
    FinishRepositoryOpen();
    candidate=null;
   }catch(Exception error){
    if(repo==candidate){repo=null;all=new List<Frame>();browsedRepository=null;browseSnapshot=null;CancelPreview();}
    repositoryOpening=false;
    if(!closing){
     Filter(true);SetBusy(false);
     L("StatusLabel").Text="Settings > General > Repository: "+error.Message;
     AppLog.Error("Could not open the saved repository",error);
    }
   }finally{
    repositoryOpening=false;
    if(candidate!=null){if(repo==candidate)repo=null;candidate.Dispose();}
    if(closing&&!Window.Dispatcher.HasShutdownStarted)Window.Dispatcher.BeginInvoke(new Action(()=>Window.Close()));
   }
  }

  void RememberBrowseTarget(){
   if(repo==null||repositoryOpening||!string.Equals(settings.Repository,repo.Root,StringComparison.OrdinalIgnoreCase))return;
   var target=Targets.SelectedItem as TargetSummary;
   if(target==null||target.Name==settings.LastBrowseTarget)return;
   settings.LastBrowseTarget=target.Name;
   try{SaveSettings();}catch(Exception error){AppLog.Error("Could not save the last archive target",error);}
  }
 }
}
