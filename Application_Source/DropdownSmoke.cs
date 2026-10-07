// Windows WPF regression checks for popup gutters, scrolling and themed dialogs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AstroArchive {
 public partial class MainUi {
  static void PumpPopupLayout(){
   var frame=new DispatcherFrame();
   Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));
   Dispatcher.PushFrame(frame);
  }
  static IEnumerable<T> PopupChildren<T>(DependencyObject root) where T:DependencyObject {
   if(root==null)yield break;
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){
    var child=VisualTreeHelper.GetChild(root,i);if(child is T)yield return (T)child;
    foreach(var descendant in PopupChildren<T>(child))yield return descendant;
   }
  }
  static Rect PopupBounds(FrameworkElement child,Visual root){return child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));}
  static void CheckPopupScroll(ScrollViewer viewer,bool vertical,bool horizontal){
   viewer.UpdateLayout();
   var content=viewer.Template.FindName("PART_ScrollContentPresenter",viewer) as ScrollContentPresenter;
   var down=viewer.Template.FindName("PART_VerticalScrollBar",viewer) as ScrollBar;
   var across=viewer.Template.FindName("PART_HorizontalScrollBar",viewer) as ScrollBar;
   if(content==null||down==null||across==null)throw new Exception("Popup has no scroll layout.");
   if((down.Visibility==Visibility.Visible)!=vertical||(across.Visibility==Visibility.Visible)!=horizontal)throw new Exception("Popup scrollbar visibility does not match its content.");
   Rect text=PopupBounds(content,viewer);
   if(vertical&&PopupBounds(down,viewer).Left<text.Right-0.5)throw new Exception("Vertical popup scrollbar overlaps content.");
   if(horizontal&&PopupBounds(across,viewer).Top<text.Bottom-0.5)throw new Exception("Horizontal popup scrollbar overlaps content.");
   if(vertical){
    double initial=viewer.VerticalOffset;
    ScrollBar.PageDownCommand.Execute(null,down);PumpPopupLayout();
    if(viewer.VerticalOffset<=initial)throw new Exception("Popup page-down command did not scroll.");
    viewer.ScrollToEnd();PumpPopupLayout();
    if(viewer.VerticalOffset<=0)throw new Exception("Popup cannot reach its final items.");
    viewer.ScrollToHome();PumpPopupLayout();
    if(viewer.VerticalOffset!=0)throw new Exception("Popup cannot return to its first items.");
   }
   if(horizontal){
    ScrollBar.PageRightCommand.Execute(null,across);PumpPopupLayout();
    if(viewer.HorizontalOffset<=0)throw new Exception("Popup page-right command did not scroll.");
    viewer.ScrollToLeftEnd();PumpPopupLayout();
   }
  }
  static RenderTargetBitmap PopupBitmap(FrameworkElement visual){
   var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(visual.ActualWidth)),Math.Max(1,(int)Math.Ceiling(visual.ActualHeight)),96,96,PixelFormats.Pbgra32);
   bitmap.Render(visual);return bitmap;
  }
  static void SavePopup(FrameworkElement visual,string path){
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(PopupBitmap(visual)));
   using(var stream=File.Create(path))encoder.Save(stream);
  }
  void CheckMenuSurface(ContextMenu menu){
   // The old native icon gutter painted white here, beneath the custom rows.
   var pixel=new byte[4];PopupBitmap(menu).CopyPixels(new Int32Rect(6,10,1,1),pixel,4,0);
   Color expected=((SolidColorBrush)Window.Resources["Surface"]).Color;
   if(pixel[0]!=expected.B||pixel[1]!=expected.G||pixel[2]!=expected.R||pixel[3]!=255)throw new Exception("Native menu gutter obscures the themed surface.");
  }
  ScrollViewer OpenSmokeMenu(ContextMenu menu){
   menu.PlacementTarget=B("ExportButton");menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;PumpPopupLayout();menu.UpdateLayout();
   return PopupChildren<ScrollViewer>(menu).First();
  }
  void SmokeDropdowns(string output){
   Window.Show();PumpPopupLayout();
   try{
    foreach(string mode in new[]{"Dark","Light"}){
     Theme.Apply(Window,mode);PumpPopupLayout();
     var menu=ThemedMenu();var choices=ExportMenu(all);
     foreach(MenuItem item in choices.Items.Cast<MenuItem>().ToList()){choices.Items.Remove(item);menu.Items.Add(item);}
     try{
      var viewer=OpenSmokeMenu(menu);CheckPopupScroll(viewer,false,false);CheckMenuSurface(menu);
      SavePopup(menu,Path.Combine(output,"AstroArchive_Export_"+mode+".png"));
     }finally{menu.IsOpen=false;}
     foreach(double scale in new[]{1.0,1.5,2.0}){
      var longMenu=ThemedMenu();longMenu.MaxHeight=200*scale;longMenu.MaxWidth=260*scale;
      for(int i=0;i<40;i++)longMenu.Items.Add(new MenuItem{Header="Capture "+i+" with a long descriptive filename for horizontal scrolling.fit"});
      try{
       var viewer=OpenSmokeMenu(longMenu);viewer.LayoutTransform=new ScaleTransform(scale,scale);PumpPopupLayout();
       CheckPopupScroll(viewer,true,true);
       SavePopup(longMenu,Path.Combine(output,"AstroArchive_Scrolling_Menu_"+mode+"_"+(int)(scale*100)+".png"));
      }finally{longMenu.IsOpen=false;}
     }
     var fileMenu=ThemedMenu();BuildFileMenu(fileMenu,all);
     try{
      OpenSmokeMenu(fileMenu);CheckMenuSurface(fileMenu);
      var export=fileMenu.Items.OfType<MenuItem>().Single(m=>Convert.ToString(m.Header)=="Export");
      export.IsSubmenuOpen=true;PumpPopupLayout();
      var popup=(Popup)export.Template.FindName("PART_Popup",export);
      var submenu=PopupChildren<ScrollViewer>(popup.Child).First();CheckPopupScroll(submenu,false,false);
      SavePopup((FrameworkElement)popup.Child,Path.Combine(output,"AstroArchive_Submenu_"+mode+".png"));
      // Constrain a long submenu just as a small screen would.
      for(int i=0;i<40;i++)export.Items.Add(new MenuItem{Header="Additional export choice "+i});
      ((FrameworkElement)popup.Child).MaxHeight=180;PumpPopupLayout();CheckPopupScroll(submenu,true,false);
      export.IsSubmenuOpen=false;
     }finally{fileMenu.IsOpen=false;}
     var dialog=new FormWindow(Window,"Dropdown layout smoke",480,440);
     var shortCombo=dialog.Select("Short selection",new[]{"One","Two","Three"},"One");
     var longCombo=dialog.Select("Long selection",Enumerable.Range(0,40).Select(i=>"Capture "+i).ToArray(),"Capture 0");
     longCombo.MaxDropDownHeight=160;
     try{
      dialog.Window.Show();PumpPopupLayout();
      foreach(var combo in new[]{shortCombo,longCombo}){
       combo.Focus();combo.IsDropDownOpen=true;PumpPopupLayout();
       var popup=(Popup)combo.Template.FindName("PART_Popup",combo);
       var viewer=PopupChildren<ScrollViewer>(popup.Child).First();CheckPopupScroll(viewer,combo==longCombo,false);
       if(combo==longCombo){
        combo.SelectedIndex=39;PumpPopupLayout();
        if(viewer.VerticalOffset<=0)throw new Exception("Dropdown selection did not bring the last item into view.");
       }
       SavePopup((FrameworkElement)popup.Child,Path.Combine(output,"AstroArchive_Dropdown_"+(combo==longCombo?"Long_":"Short_")+mode+".png"));
       combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(combo),0,Key.Escape){RoutedEvent=Keyboard.KeyDownEvent});PumpPopupLayout();
       if(combo.IsDropDownOpen)throw new Exception("Escape did not close the dropdown.");
      }
     }finally{dialog.Window.Close();}
    }
    File.WriteAllText(Path.Combine(output,"dropdown-smoke.txt"),"PASS: light/dark Export and nested file menus; no native gutter; short popups hide scrollbars; constrained long menus, submenus and dialog dropdowns reserve scrollbar space; 100/150/200% popup layout scales; vertical/horizontal page commands; final-item selection; Escape dismissal.");
   }finally{Theme.Apply(Window,"Dark");Window.Hide();}
  }
 }
}
