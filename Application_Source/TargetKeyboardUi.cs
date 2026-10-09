using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  ListBox keyboardTargets;
  ListBox TargetListFromElement(DependencyObject source){
   for(var node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))if(node==Targets||node==EditedTargets)return (ListBox)node;
   return null;
  }
  void InitializeTargetKeyboard(){
   foreach(var list in new[]{Targets,EditedTargets}){
    var targets=list;targets.GotKeyboardFocus+=(s,e)=>keyboardTargets=targets;
   }
   Window.PreviewMouseDown+=(s,e)=>keyboardTargets=TargetListFromElement(e.OriginalSource as DependencyObject);
   Window.PreviewKeyDown+=PaneNavigationKeys;Window.PreviewKeyDown+=TargetNavigationKeys;
  }
  void RestoreTargetKeyboardFocus(ListBox list){
   if(keyboardTargets!=list||!list.IsVisible||!list.IsEnabled)return;
   if(list.SelectedItem!=null)list.ScrollIntoView(list.SelectedItem);list.Focus();
  }
  void TargetNavigationKeys(object sender,KeyEventArgs args){
   if(args.Handled)return;
   if(args.Key!=Key.Up&&args.Key!=Key.Down){keyboardTargets=null;return;}
   if(TopMenus().Any(menu=>menu.IsSubmenuOpen)||Targets.ContextMenu!=null&&Targets.ContextMenu.IsOpen||EditedTargets.ContextMenu!=null&&EditedTargets.ContextMenu.IsOpen||ViewerDropdownOpen(args.OriginalSource as DependencyObject))return;
   var list=TargetListFromElement(args.OriginalSource as DependencyObject)??keyboardTargets;if(list==null||!list.IsVisible)return;
   args.Handled=true;if(!list.IsEnabled||list.Items.Count==0)return;keyboardTargets=list;
   int next=Math.Max(0,Math.Min(list.Items.Count-1,list.SelectedIndex+(args.Key==Key.Down?1:-1)));
   list.SelectedIndex=next;RestoreTargetKeyboardFocus(list);
  }
 }
}
