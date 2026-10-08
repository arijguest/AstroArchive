using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroArchive {
 public partial class MainUi {
  bool changingSessionSelection;string sessionSelectionAnchor,activeSessionKey;
  SubframeSession ActiveSelectedSession{get{return subframeSessions.FirstOrDefault(g=>g.IsSelected&&g.Key==activeSessionKey)??subframeSessions.FirstOrDefault(g=>g.IsSelected);}}
  static SubframeSession HeaderSession(DependencyObject source){
   for(var node=source;node!=null;node=VisualTreeHelper.GetParent(node)){
    var button=node as Button;if(button==null||!object.Equals(button.Tag,"SelectSession"))continue;
    var group=button.DataContext as CollectionViewGroup;return group==null?null:group.Name as SubframeSession;
   }return null;
  }
  void ClearSessionSelection(){foreach(var group in subframeSessions)group.IsSelected=false;activeSessionKey=null;}
  void SessionFileSelectionChanged(SelectionChangedEventArgs args){
   if(!changingSessionSelection&&args.AddedItems.OfType<Frame>().Any()&&(Keyboard.Modifiers&ModifierKeys.Control)==0)ClearSessionSelection();
   if(!changingSessionSelection)Details();
  }
  void SelectSession(SubframeSession group,ModifierKeys modifiers){
   if(group==null)return;bool control=(modifiers&ModifierKeys.Control)!=0,range=(modifiers&ModifierKeys.Shift)!=0;
   changingSessionSelection=true;
   try{
    if(!control){G("FramesGrid").SelectedItems.Clear();if(!range)ClearSessionSelection();}
    var view=G("FramesGrid").ItemsSource as ListCollectionView;
    var groups=view==null||view.Groups==null?subframeSessions:view.Groups.OfType<CollectionViewGroup>().Select(g=>g.Name as SubframeSession).Where(g=>g!=null).ToList();
    int anchor=groups.FindIndex(g=>g.Key==sessionSelectionAnchor),index=groups.IndexOf(group);
    if(range&&anchor>=0&&index>=0){if(!control)ClearSessionSelection();foreach(var item in groups.Skip(Math.Min(anchor,index)).Take(Math.Abs(anchor-index)+1))item.IsSelected=true;}
    else{group.IsSelected=!control||!group.IsSelected;sessionSelectionAnchor=group.Key;}
    activeSessionKey=group.IsSelected?group.Key:null;
   }finally{changingSessionSelection=false;}
   Details();
  }
  void SelectContextSession(SubframeSession group){if(!group.IsSelected)SelectSession(group,ModifierKeys.None);}
  void InitializeSessionSelection(){
   var grid=G("FramesGrid");
   grid.AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e)=>{var group=HeaderSession(e.OriginalSource as DependencyObject);if(group!=null){SelectSession(group,Keyboard.Modifiers);e.Handled=true;}}));
   grid.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape&&subframeSessions.Any(g=>g.IsSelected)){ClearSessionSelection();grid.SelectedItems.Clear();Details();e.Handled=true;}};
  }
 }
}
