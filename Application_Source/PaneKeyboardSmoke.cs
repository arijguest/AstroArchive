using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace AstroArchive {
 public partial class MainUi {
  void SmokePaneKey(Key key){
   var source=Keyboard.FocusedElement as UIElement;if(source==null)throw new Exception("Pane navigation has no focused element");
   var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(source),0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};source.RaiseEvent(args);
   if(!args.Handled){args.RoutedEvent=Keyboard.KeyDownEvent;source.RaiseEvent(args);}WaitForSearches();PumpPopupLayout();
  }
  void SmokePaneKeyboard(int page){
   var targets=page==0?Targets:EditedTargets;var grid=G(page==0?"FramesGrid":"EditedGrid");var stage=(FrameworkElement)Window.FindName(page==0?"PreviewStage":"EditedPreviewStage");
   var selected=grid.SelectedItems.Cast<object>().ToList();var target=targets.SelectedItem;var session=page==0?ActiveSelectedSession:null;
   targets.Focus();SmokePaneKey(Key.Left);if(!targets.IsKeyboardFocusWithin)throw new Exception("Left at the first pane changed focus");
   SmokePaneKey(Key.Right);if(!grid.IsKeyboardFocusWithin||keyboardTargets!=null)throw new Exception("Right did not enter the file pane on page "+page);
   if(session!=null&&KeyboardSession(Keyboard.FocusedElement as DependencyObject)!=session)throw new Exception("Right did not focus the selected collapsed subgroup");
   SmokeTableColumns(grid,Key.Right,session!=null);
   SmokePaneKey(Key.Right);if(!stage.IsKeyboardFocused)throw new Exception("Right did not enter the preview pane on page "+page);
   SmokePaneKey(Key.Right);if(!stage.IsKeyboardFocused)throw new Exception("Right at the last pane changed focus");
   SmokePaneKey(Key.Left);if(!grid.IsKeyboardFocusWithin)throw new Exception("Left did not return to the file pane");
   SmokeTableColumns(grid,Key.Left,session!=null);
   SmokePaneKey(Key.Left);if(!targets.IsKeyboardFocusWithin)throw new Exception("Left did not return to the target pane");
   if(targets.SelectedItem!=target||!new System.Collections.Generic.HashSet<object>(selected).SetEquals(grid.SelectedItems.Cast<object>()))throw new Exception("Switching panes lost the active selection on page "+page);
   if(session==null)SmokeReorderedPaneColumns(targets,grid,stage);
   SmokePaneKey(Key.Right);
   if(session!=null){SmokePaneKey(Key.Down);if(targets.SelectedItem!=target||ActiveSelectedSession==session)throw new Exception("Down after a pane switch moved the target or kept the old group selection");SelectSession(session,ModifierKeys.None);}
   else if(grid.Items.Count>1){var current=grid.SelectedItem;SmokePaneKey(Key.Down);if(targets.SelectedItem!=target||grid.SelectedItem==current)throw new Exception("Down after a pane switch did not select the next file");SelectOnlyTargetRow(page==0?"FramesGrid":"EditedGrid",selected.First());}
   var search=T(page==0?"SearchBox":"EditedSearchBox");search.Focus();SmokePaneKey(Key.Right);if(!search.IsKeyboardFocused)throw new Exception("Pane navigation hijacked a text-field arrow");
   ClearTargetSelection(grid.Name);targets.Focus();SmokePaneKey(Key.Right);
   if(page==0?librarySelection.Count==0:editedSelection.Count==0)throw new Exception("Entering an unselected file pane did not select its first entry");
   if(session!=null)SelectSession(session,ModifierKeys.None);else SelectOnlyTargetRow(grid.Name,selected.First());
   if(page==0){settings.ShowPreview=false;SetPreviewVisibility();targets.Focus();SmokePaneKey(Key.Right);SmokeTableColumns(grid,Key.Right,session!=null);SmokePaneKey(Key.Right);if(!grid.IsKeyboardFocusWithin)throw new Exception("Pane navigation entered a hidden preview");settings.ShowPreview=true;SetPreviewVisibility();}
   SmokeClearerSelections(page);targets.Focus();
  }
  void SmokeTableColumns(DataGrid grid,Key direction,bool group){
   if(group)return;var columns=grid.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).ToList();if(direction==Key.Left)columns.Reverse();var selected=grid.SelectedItem;
   if(grid.CurrentCell.Column!=columns.First())throw new Exception("Entering the table did not focus its boundary column");
   foreach(var column in columns.Skip(1)){
    SmokePaneKey(direction);if(!grid.IsKeyboardFocusWithin||grid.CurrentCell.Column!=column||grid.SelectedItem!=selected)throw new Exception("A table arrow left the table before its edge or changed the selected row");
   }
  }
  void SmokeReorderedPaneColumns(ListBox targets,DataGrid grid,FrameworkElement stage){
   var order=grid.Columns.ToDictionary(c=>c,c=>c.DisplayIndex);var visible=grid.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).ToList();if(visible.Count<3)return;
   try{
    visible.First().Visibility=Visibility.Collapsed;visible.Last().DisplayIndex=0;targets.Focus();SmokePaneKey(Key.Right);SmokeTableColumns(grid,Key.Right,false);
    SmokePaneKey(Key.Right);if(!stage.IsKeyboardFocused)throw new Exception("Reordered/hidden columns blocked the preview boundary");
    SmokePaneKey(Key.Left);SmokeTableColumns(grid,Key.Left,false);SmokePaneKey(Key.Left);if(!targets.IsKeyboardFocusWithin)throw new Exception("Reordered/hidden columns blocked the target boundary");
   }finally{visible.First().Visibility=Visibility.Visible;foreach(var entry in order.OrderBy(e=>e.Value))entry.Key.DisplayIndex=entry.Value;PumpPopupLayout();}
  }
  void SmokeClearerSelections(int page){
   bool previous=settings.ClearerSelections;var targets=page==0?Targets:EditedTargets;var grid=G(page==0?"FramesGrid":"EditedGrid");
   try{foreach(bool enabled in new[]{false,true,false}){
    settings.ClearerSelections=enabled;ApplyAppearance();targets.ScrollIntoView(targets.SelectedItem);PumpPopupLayout();
    var target=targets.ItemContainerGenerator.ContainerFromItem(targets.SelectedItem) as ListBoxItem;if(target==null||!target.Focus())throw new Exception("Selection appearance target did not receive focus");PumpPopupLayout();
    var border=target.Template.FindName("Item",target) as Border;
    if(border==null||border.BorderThickness!=(enabled?new Thickness(1):new Thickness(3,0,0,0)))throw new Exception("Clearer selections did not toggle the target outline");
    SmokePaneKey(Key.Right);if(!grid.IsKeyboardFocusWithin)throw new Exception("Selection appearance lost file focus");
    var button=Keyboard.FocusedElement as Button;
    if(button!=null&&object.Equals(button.Tag,"SelectSession")){
     var outline=button.Template.FindName("ButtonFocus",button) as Border;if(outline==null||outline.Visibility!=(enabled?Visibility.Visible:Visibility.Hidden))throw new Exception("Clearer selections did not toggle the sub group outline");
    }else{
     var cell=PaneChildren<DataGridCell>(grid).FirstOrDefault(c=>c.IsKeyboardFocusWithin);if(cell==null||cell.BorderThickness!=(enabled?new Thickness(1):new Thickness(0)))throw new Exception("Clearer selections did not toggle the file outline");
    }
    if(page==0?librarySelection.Count==0:editedSelection.Count==0)throw new Exception("Toggling clearer selections cleared the selected entry");
   }}finally{settings.ClearerSelections=previous;ApplyAppearance();}
  }
 }
}
