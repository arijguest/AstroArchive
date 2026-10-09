using System.Linq;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  bool repositoryPageOpened,editedPageOpened,openingRepositorySelection,openingEditedSelection;
  void ResetOpeningSelection(){repositoryPageOpened=editedPageOpened=openingRepositorySelection=openingEditedSelection=false;}
  void BeginOpeningSelection(int page){
   if(repo==null||(page!=0&&page!=2))return;
   if(page==0&&!repositoryPageOpened){
    repositoryPageOpened=true;openingRepositorySelection=librarySelection.Count==0;
    if(openingRepositorySelection){bool previous=updating;updating=true;try{Targets.SelectedItem=Targets.Items.OfType<TargetSummary>().FirstOrDefault(t=>t.Name=="All targets");}finally{updating=previous;}Filter(true);}
   }else if(page==2&&!editedPageOpened){
    editedPageOpened=true;openingEditedSelection=editedSelection.Count==0;
    if(openingEditedSelection){bool previous=refreshingEditedTargets;refreshingEditedTargets=true;try{EditedTargets.SelectedItem=EditedTargets.Items.OfType<TargetSummary>().FirstOrDefault(t=>t.Name=="All targets");}finally{refreshingEditedTargets=previous;}FilterEditedImages();}
   }
   SelectOpeningRow(page==0?"FramesGrid":"EditedGrid");
  }
  void SelectOpeningRow(string name){
   int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   if(repo==null||RepositoryOperationBlocked||(name=="FramesGrid"?page!=0||!openingRepositorySelection:page!=2||!openingEditedSelection))return;
   SearchPanel search;if(searchPanels.TryGetValue(name=="FramesGrid"?"SearchBox":"EditedSearchBox",out search)&&search.Pending)return;
   if(name=="FramesGrid"){
    if(librarySelection.Count>0){openingRepositorySelection=false;return;}
    var first=G(name).Items.OfType<Frame>().FirstOrDefault();if(first==null)return;openingRepositorySelection=false;
    var session=subframeSessions.FirstOrDefault(g=>g.Frames.Contains(first));
    if(session!=null)SelectSession(session,System.Windows.Input.ModifierKeys.None);
    else{SelectOnlyTargetRow(name,first);G(name).ScrollIntoView(first);}
   }else{
    if(editedSelection.Count>0){openingEditedSelection=false;return;}
    var first=G(name).Items.OfType<EditedImage>().FirstOrDefault();if(first==null)return;openingEditedSelection=false;SelectOnlyTargetRow(name,first);G(name).ScrollIntoView(first);
   }
  }
 }
}
