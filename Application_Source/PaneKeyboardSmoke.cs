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
   SmokePaneKey(Key.Right);if(!stage.IsKeyboardFocused)throw new Exception("Right did not enter the preview pane on page "+page);
   SmokePaneKey(Key.Right);if(!stage.IsKeyboardFocused)throw new Exception("Right at the last pane changed focus");
   SmokePaneKey(Key.Left);if(!grid.IsKeyboardFocusWithin)throw new Exception("Left did not return to the file pane");
   SmokePaneKey(Key.Left);if(!targets.IsKeyboardFocusWithin)throw new Exception("Left did not return to the target pane");
   if(targets.SelectedItem!=target||!new System.Collections.Generic.HashSet<object>(selected).SetEquals(grid.SelectedItems.Cast<object>()))throw new Exception("Switching panes lost the active selection on page "+page);
   SmokePaneKey(Key.Right);
   if(session!=null){SmokePaneKey(Key.Down);if(targets.SelectedItem!=target||ActiveSelectedSession==session)throw new Exception("Down after a pane switch moved the target or kept the old group selection");SelectSession(session,ModifierKeys.None);}
   else if(grid.Items.Count>1){var current=grid.SelectedItem;SmokePaneKey(Key.Down);if(targets.SelectedItem!=target||grid.SelectedItem==current)throw new Exception("Down after a pane switch did not select the next file");SelectOnlyTargetRow(page==0?"FramesGrid":"EditedGrid",selected.First());}
   var search=T(page==0?"SearchBox":"EditedSearchBox");search.Focus();SmokePaneKey(Key.Right);if(!search.IsKeyboardFocused)throw new Exception("Pane navigation hijacked a text-field arrow");
   ClearTargetSelection(grid.Name);targets.Focus();SmokePaneKey(Key.Right);
   if(page==0?librarySelection.Count==0:editedSelection.Count==0)throw new Exception("Entering an unselected file pane did not select its first entry");
   if(session!=null)SelectSession(session,ModifierKeys.None);else SelectOnlyTargetRow(grid.Name,selected.First());
   if(page==0){settings.ShowPreview=false;SetPreviewVisibility();targets.Focus();SmokePaneKey(Key.Right);SmokePaneKey(Key.Right);if(!grid.IsKeyboardFocusWithin)throw new Exception("Pane navigation entered a hidden preview");settings.ShowPreview=true;SetPreviewVisibility();}
   targets.Focus();
  }
 }
}
