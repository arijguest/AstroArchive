using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  static IEnumerable<T> PaneChildren<T>(DependencyObject root) where T:DependencyObject{
   if(root==null)yield break;
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);var match=child as T;if(match!=null)yield return match;foreach(var nested in PaneChildren<T>(child))yield return nested;}
  }
  void PaneNavigationKeys(object sender,KeyEventArgs args){NavigatePane(args,Keyboard.Modifiers);}
  void NavigatePane(KeyEventArgs args,ModifierKeys modifiers){
   if(args.Handled||(args.Key!=Key.Left&&args.Key!=Key.Right)||modifiers!=ModifierKeys.None)return;
   int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;if(page!=0&&page!=2)return;
   var targets=page==0?Targets:EditedTargets;var table=G(page==0?"FramesGrid":"EditedGrid");var preview=(FrameworkElement)Window.FindName(page==0?"PreviewPane":"EditedPreviewPane");
   if(TopMenus().Any(menu=>menu.IsSubmenuOpen)||new[]{targets.ContextMenu,table.ContextMenu}.Any(menu=>menu!=null&&menu.IsOpen)||activityPanel!=null&&activityPanel.Visibility==Visibility.Visible)return;
   int pane=-1;
   for(var node=args.OriginalSource as DependencyObject;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node)){
    if(node is TextBoxBase||node is PasswordBox||node is ComboBox||node is RangeBase||node is MenuBase||node is MenuItem)return;
    if(node==targets)pane=0;else if(node==table)pane=1;else if(node==preview)pane=2;
   }
   if(pane<0&&args.OriginalSource==Window&&keyboardTargets==targets)pane=0;if(pane<0)return;
   args.Handled=true;int next=pane+(args.Key==Key.Right?1:-1);if(next<0||next>2)return;
   var destination=next==0?(FrameworkElement)targets:next==1?(FrameworkElement)table:(FrameworkElement)Window.FindName(page==0?"PreviewStage":"EditedPreviewStage");
   if(!destination.IsVisible||!destination.IsEnabled)return;
   keyboardTargets=null;
   if(next==0){if(targets.SelectedItem==null&&targets.Items.Count>0)targets.SelectedIndex=0;if(targets.SelectedItem!=null)targets.ScrollIntoView(targets.SelectedItem);targets.Focus();}
   else if(next==1)FocusFilePane(table);
   else destination.Focus();
  }
  void FocusFilePane(DataGrid grid){
   object item=grid.SelectedItem??grid.Items.Cast<object>().FirstOrDefault();
   var frame=item as Frame;var session=grid==G("FramesGrid")?(ActiveSelectedSession??subframeSessions.FirstOrDefault(g=>!g.Expanded&&g.Frames.Contains(frame))):null;
   if(session!=null&&!session.Expanded){
    SelectSession(session,ModifierKeys.None);grid.ScrollIntoView(session.Frames.First());grid.UpdateLayout();var button=PaneChildren<Button>(grid).FirstOrDefault(b=>object.Equals(b.Tag,"SelectSession")&&b.DataContext is CollectionViewGroup&&((CollectionViewGroup)b.DataContext).Name is SubframeSession&&((SubframeSession)((CollectionViewGroup)b.DataContext).Name).Key==session.Key);
    if(button!=null&&button.IsVisible&&button.Focus())return;
   }
   var column=grid.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).FirstOrDefault();
   if(item!=null&&column!=null){SelectOnlyTargetRow(grid.Name,item);grid.ScrollIntoView(item,column);grid.UpdateLayout();grid.CurrentCell=new DataGridCellInfo(item,column);
    var row=grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;if(row!=null&&row.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)))return;
   }
   grid.Focus();
  }
  void DescribePaneNavigation(){
   foreach(string name in new[]{"PreviewStage","EditedPreviewStage"})UiHelp.Describe((FrameworkElement)Window.FindName(name),"Left/Right switches panes. Ctrl+Left/Right or Up/Down pans; F fits; plus/minus zooms. Drag to pan; scroll or pinch to zoom.");
  }
 }
}
