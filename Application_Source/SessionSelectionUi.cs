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
  bool changingSessionSelection;string sessionSelectionAnchor,activeSessionKey;Frame rowSelectionAnchor;
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
   bool control=(Keyboard.Modifiers&ModifierKeys.Control)!=0,range=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
   if(!changingSessionSelection&&(args.AddedItems.OfType<Frame>().Any()||range&&args.RemovedItems.OfType<Frame>().Any())){
    var current=G("FramesGrid").CurrentItem as Frame??args.AddedItems.OfType<Frame>().LastOrDefault();
    // Shift ranges include every session group they pass over, collapsed or not.
    if(!(range&&SelectSpan(SelectionAnchor(),current,control))&&!control)ClearSessionSelection();
    if(!range){rowSelectionAnchor=current;sessionSelectionAnchor=null;}
   }
   if(!changingSessionSelection){CaptureTargetSelection("FramesGrid");Details();}
  }
  void SelectSession(SubframeSession group,ModifierKeys modifiers){
   if(group==null)return;bool control=(modifiers&ModifierKeys.Control)!=0,range=(modifiers&ModifierKeys.Shift)!=0;
   if(range&&SelectSpan(SelectionAnchor(),group,control)){activeSessionKey=group.Key;CaptureTargetSelection("FramesGrid");Details();return;}
   changingSessionSelection=true;
   try{
    if(!control&&!range)librarySelection.Clear();
    if(!control){G("FramesGrid").SelectedItems.Clear();if(!range)ClearSessionSelection();}
    var view=G("FramesGrid").ItemsSource as ListCollectionView;
    var groups=view==null||view.Groups==null?subframeSessions:view.Groups.OfType<CollectionViewGroup>().Select(g=>g.Name as SubframeSession).Where(g=>g!=null).ToList();
    int anchor=groups.FindIndex(g=>g.Key==sessionSelectionAnchor),index=groups.IndexOf(group);
    if(range&&anchor>=0&&index>=0){if(!control)ClearSessionSelection();foreach(var item in groups.Skip(Math.Min(anchor,index)).Take(Math.Abs(anchor-index)+1))item.IsSelected=true;}
    else{group.IsSelected=!control||!group.IsSelected;sessionSelectionAnchor=group.Key;rowSelectionAnchor=null;}
    if(control&&!group.IsSelected)foreach(var frame in group.Frames)G("FramesGrid").SelectedItems.Remove(frame);
    activeSessionKey=group.IsSelected?group.Key:null;
   }finally{changingSessionSelection=false;}
   CaptureTargetSelection("FramesGrid");Details();
  }
  object SelectionAnchor(){
   if(rowSelectionAnchor!=null)return rowSelectionAnchor;
   return sessionSelectionAnchor==null?null:subframeSessions.FirstOrDefault(g=>g.Key==sessionSelectionAnchor);
  }
  // Files in table order, with each session group's files kept together.
  List<Frame> DisplayOrder(){
   var grid=G("FramesGrid");var view=grid.ItemsSource as ListCollectionView;
   if(view==null||view.Groups==null)return grid.Items.OfType<Frame>().ToList();
   return view.Groups.OfType<CollectionViewGroup>().SelectMany(g=>g.Items.OfType<Frame>()).ToList();
  }
  static int[] Span(object unit,Dictionary<Frame,int> index){
   var frame=unit as Frame;int at;if(frame!=null)return index.TryGetValue(frame,out at)?new[]{at,at}:null;
   var group=unit as SubframeSession;if(group==null)return null;
   var positions=group.Frames.Where(index.ContainsKey).Select(f=>index[f]).ToList();
   return positions.Count==0?null:new[]{positions.Min(),positions.Max()};
  }
  // Select everything between anchor and target. Groups lying inside the range are selected whole.
  bool SelectSpan(object anchor,object target,bool add){
   if(anchor==null||target==null||subframeSessions.Count==0)return false;
   var order=DisplayOrder();var index=new Dictionary<Frame,int>();for(int i=0;i<order.Count;i++)index[order[i]]=i;
   var from=Span(anchor,index);var to=Span(target,index);if(from==null||to==null)return false;
   int low=Math.Min(from[0],to[0]),high=Math.Max(from[1],to[1]);
   var grid=G("FramesGrid");bool previous=changingSessionSelection;changingSessionSelection=true;
   try{
    if(!add){grid.SelectedItems.Clear();ClearSessionSelection();}
    var covered=new HashSet<Frame>();
    foreach(var group in subframeSessions){var span=Span(group,index);if(span!=null&&span[0]>=low&&span[1]<=high){group.IsSelected=true;covered.UnionWith(group.Frames);}}
    var selected=new HashSet<Frame>(grid.SelectedItems.OfType<Frame>());
    for(int i=low;i<=high;i++)if(!covered.Contains(order[i])&&selected.Add(order[i]))grid.SelectedItems.Add(order[i]);
   }finally{changingSessionSelection=previous;}
   return true;
  }
  void SelectAllLibraryRows(){
   var grid=G("FramesGrid");changingSessionSelection=true;
   try{grid.SelectAll();foreach(var group in subframeSessions)group.IsSelected=true;}finally{changingSessionSelection=false;}
   CaptureTargetSelection("FramesGrid");Details();
  }
  void SelectContextSession(SubframeSession group){if(!group.IsSelected)SelectSession(group,ModifierKeys.None);}
  void InitializeSessionSelection(){
   var grid=G("FramesGrid");
   grid.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll,(s,e)=>{SelectAllLibraryRows();e.Handled=true;}));
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
