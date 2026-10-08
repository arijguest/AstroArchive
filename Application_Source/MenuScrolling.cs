using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroArchive {
 // WPF's default wheel handler scrolls a whole set of lines for every packet,
 // including the small deltas sent by precision touchpads.
 public static class MenuScrolling {
  public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(MenuScrolling),new PropertyMetadata(false,Changed));
  static readonly DependencyProperty StateProperty=DependencyProperty.RegisterAttached("State",typeof(WheelState),typeof(MenuScrolling));
  public static bool GetEnabled(DependencyObject element){return (bool)element.GetValue(EnabledProperty);}
  public static void SetEnabled(DependencyObject element,bool enabled){element.SetValue(EnabledProperty,enabled);}
  static void Changed(DependencyObject element,DependencyPropertyChangedEventArgs args){
   var viewer=element as ScrollViewer;if(viewer==null)return;
   var old=viewer.GetValue(StateProperty) as WheelState;if(old!=null)old.Detach();
   viewer.SetValue(StateProperty,(bool)args.NewValue?new WheelState(viewer):null);
  }
  static DependencyObject Parent(DependencyObject element){
   if(element is Visual||element is System.Windows.Media.Media3D.Visual3D)return VisualTreeHelper.GetParent(element);
   var content=element as ContentElement;if(content!=null){var parent=ContentOperations.GetParent(content);if(parent!=null)return parent;var framework=content as FrameworkContentElement;if(framework!=null)return framework.Parent;}
   return LogicalTreeHelper.GetParent(element);
  }
  sealed class WheelState {
   readonly ScrollViewer viewer;double? requestedX,requestedY;
   public WheelState(ScrollViewer viewer){this.viewer=viewer;viewer.PreviewMouseWheel+=Wheel;viewer.ScrollChanged+=Scrolled;}
   public void Detach(){viewer.PreviewMouseWheel-=Wheel;viewer.ScrollChanged-=Scrolled;}
   void Scrolled(object sender,ScrollChangedEventArgs args){
    if(args.OriginalSource!=viewer)return;
    if(args.VerticalChange!=0||args.ExtentHeightChange!=0||args.ViewportHeightChange!=0)requestedY=null;
    if(args.HorizontalChange!=0||args.ExtentWidthChange!=0||args.ViewportWidthChange!=0)requestedX=null;
   }
   void Wheel(object sender,MouseWheelEventArgs args){
    if(args.Handled||args.Delta==0||viewer.CanContentScroll)return;
    // Let a nested list, text box or scroll region own its input.
    for(var element=args.OriginalSource as DependencyObject;element!=null;element=Parent(element))
     if(element is ScrollViewer){if(element!=viewer)return;break;}
    double movement=-args.Delta/120.0*Math.Max(32,viewer.FontSize*2);
    bool horizontal=viewer.ScrollableWidth>0&&((Keyboard.Modifiers&ModifierKeys.Shift)!=0||viewer.ScrollableHeight<=0);
    if(horizontal){
     requestedX=Math.Max(0,Math.Min(viewer.ScrollableWidth,(requestedX??viewer.HorizontalOffset)+movement));viewer.ScrollToHorizontalOffset(requestedX.Value);
    }else if(viewer.ScrollableHeight>0){
     requestedY=Math.Max(0,Math.Min(viewer.ScrollableHeight,(requestedY??viewer.VerticalOffset)+movement));viewer.ScrollToVerticalOffset(requestedY.Value);
    }
    // Consume input at popup boundaries too, avoiding selection changes or a
    // second scroll of a parent. Pending offsets retain rapid packets that WPF
    // coalesces before its next layout pass.
    args.Handled=true;
   }
  }
 }
}
