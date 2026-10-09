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
  static SubframeSession KeyboardSession(DependencyObject source){
   for(var node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node)){
    var element=node as FrameworkElement;if(element==null)continue;
    if(!(element is Button&&object.Equals(element.Tag,"SelectSession"))&&!(element is System.Windows.Controls.Primitives.ToggleButton&&element.Name=="SessionChevron"))continue;
    var group=element.DataContext as CollectionViewGroup;return group==null?null:group.Name as SubframeSession;
   }return null;
  }
  void ClearSessionSelection(){foreach(var group in subframeSessions)group.IsSelected=false;activeSessionKey=null;}
  void SessionFileSelectionChanged(SelectionChangedEventArgs args){
   if(restoringTargetSelection)return;
   if(!changingSessionSelection&&args.AddedItems.OfType<Frame>().Any()&&(Keyboard.Modifiers&ModifierKeys.Control)==0)ClearSessionSelection();
   if(!changingSessionSelection){CaptureTargetSelection("FramesGrid");Details();}
  }
  void SelectSession(SubframeSession group,ModifierKeys modifiers){
   if(group==null)return;bool control=(modifiers&ModifierKeys.Control)!=0,range=(modifiers&ModifierKeys.Shift)!=0;
   changingSessionSelection=true;
   try{
    if(!control&&!range)librarySelection.Clear();
    if(!control){G("FramesGrid").SelectedItems.Clear();if(!range)ClearSessionSelection();}
    var view=G("FramesGrid").ItemsSource as ListCollectionView;
    var groups=view==null||view.Groups==null?subframeSessions:view.Groups.OfType<CollectionViewGroup>().Select(g=>g.Name as SubframeSession).Where(g=>g!=null).ToList();
    int anchor=groups.FindIndex(g=>g.Key==sessionSelectionAnchor),index=groups.IndexOf(group);
    if(range&&anchor>=0&&index>=0){if(!control)ClearSessionSelection();foreach(var item in groups.Skip(Math.Min(anchor,index)).Take(Math.Abs(anchor-index)+1))item.IsSelected=true;}
    else{group.IsSelected=!control||!group.IsSelected;sessionSelectionAnchor=group.Key;}
    if(control&&!group.IsSelected)foreach(var frame in group.Frames)G("FramesGrid").SelectedItems.Remove(frame);
    activeSessionKey=group.IsSelected?group.Key:null;
   }finally{changingSessionSelection=false;}
   CaptureTargetSelection("FramesGrid");Details();
  }
  void SelectContextSession(SubframeSession group){if(!group.IsSelected)SelectSession(group,ModifierKeys.None);}
  void InitializeSessionSelection(){
   var grid=G("FramesGrid");
   grid.AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e)=>{var group=HeaderSession(e.OriginalSource as DependencyObject);if(group!=null){SelectSession(group,Keyboard.Modifiers);e.Handled=true;}}));
   grid.PreviewKeyDown+=(s,e)=>{
    if(e.Handled||(e.Key!=Key.Up&&e.Key!=Key.Down)||KeyboardSession(e.OriginalSource as DependencyObject)==null)return;
    var source=Keyboard.FocusedElement as UIElement;if(source==null)return;
    e.Handled=true;if(!source.MoveFocus(new TraversalRequest(e.Key==Key.Down?FocusNavigationDirection.Down:FocusNavigationDirection.Up)))return;
    if((Keyboard.Modifiers&ModifierKeys.Control)!=0)return;
    var focused=Keyboard.FocusedElement as DependencyObject;var group=KeyboardSession(focused);
    if(group!=null)SelectSession(group,Keyboard.Modifiers);
    else{
     var row=ItemsControl.ContainerFromElement(grid,focused) as DataGridRow;var frame=row==null?null:row.Item as Frame;
     if(frame!=null)SelectOnlyTargetRow("FramesGrid",frame);
    }
   };
  }
 }
}
