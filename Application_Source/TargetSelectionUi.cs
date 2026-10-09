using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  readonly TargetSelection<Frame> librarySelection=new TargetSelection<Frame>(f=>!string.IsNullOrEmpty(f.Hash)?f.Hash:(f.RelativePath??f.SourcePath??f.OriginalName??"")+"\0"+f.Target+"\0"+f.SessionKey);
  readonly TargetSelection<EditedImage> editedSelection=new TargetSelection<EditedImage>(i=>i.Project.Id+"\0"+i.RelativePath);
  bool restoringTargetSelection;
  void CaptureTargetSelection(string name){
   if(restoringTargetSelection)return;
   if(name=="FramesGrid"){
    librarySelection.ChangeVisible(G(name).Items.OfType<Frame>(),G(name).SelectedItems.OfType<Frame>().Concat(subframeSessions.Where(g=>g.IsSelected).SelectMany(g=>g.Frames)));
   }else if(name=="EditedGrid"){
    editedSelection.ChangeVisible(G(name).Items.OfType<EditedImage>(),G(name).SelectedItems.OfType<EditedImage>());
   }
   UpdateSelectionCounters();
  }
  void RestoreTargetSelection(string name,object preferred=null){
   bool previous=restoringTargetSelection;restoringTargetSelection=true;
   try{var grid=G(name);grid.SelectedItems.Clear();
    if(name=="FramesGrid"){librarySelection.Reconcile(all);foreach(var frame in grid.Items.OfType<Frame>().Where(librarySelection.Contains).OrderBy(f=>object.ReferenceEquals(f,preferred)?0:1))grid.SelectedItems.Add(frame);}
    else if(name=="EditedGrid"){editedSelection.Reconcile(editedImages);foreach(var image in grid.Items.OfType<EditedImage>().Where(editedSelection.Contains).OrderBy(i=>object.ReferenceEquals(i,preferred)?0:1))grid.SelectedItems.Add(image);}
   }finally{restoringTargetSelection=previous;}UpdateSelectionCounters();
  }
  void UpdateSelectionCounters(){
   foreach(string name in new[]{"LibrarySelectionCounter","EditedSelectionCounter"}){var button=Window.FindName(name) as Button;if(button==null)continue;int count=name=="LibrarySelectionCounter"?librarySelection.Count:editedSelection.Count;button.Content=count.ToString("N0")+" selected";button.Visibility=count>1?Visibility.Visible:Visibility.Collapsed;}
  }
  void ClearTargetSelection(string name){
   restoringTargetSelection=true;try{if(name=="FramesGrid"){librarySelection.Clear();ClearSessionSelection();}else editedSelection.Clear();G(name).SelectedItems.Clear();}finally{restoringTargetSelection=false;}
   UpdateSelectionCounters();if(name=="FramesGrid")Details();else{UpdateEditedActions();LoadEditedPreview();}
  }
  void InitializeTargetSelection(){
   B("LibrarySelectionCounter").Click+=(s,e)=>ClearTargetSelection("FramesGrid");B("EditedSelectionCounter").Click+=(s,e)=>ClearTargetSelection("EditedGrid");
  }
 }
}
