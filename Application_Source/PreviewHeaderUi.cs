using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace AstroArchive {
 public sealed class PreviewStretchLabelConverter:IValueConverter {
  public object Convert(object value,Type targetType,object parameter,CultureInfo culture){return object.Equals(value,"Auto per channel")?"Auto RGB":value;}
  public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){return Binding.DoNothing;}
 }
 public partial class MainUi {
  void InitializePreviewHeader(string prefix){
   var button=B(prefix+"PreviewDetailsButton");var popup=(Popup)Window.FindName(prefix+"PreviewDetailsPopup");
   button.Click+=(s,e)=>popup.IsOpen=!popup.IsOpen;
   popup.Opened+=(s,e)=>((FrameworkElement)popup.Child).Focus();
   popup.Child.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){popup.IsOpen=false;button.Focus();e.Handled=true;}};
   Window.Deactivated+=(s,e)=>popup.IsOpen=false;
  }
  void ClosePreviewDetails(string prefix){((Popup)Window.FindName(prefix+"PreviewDetailsPopup")).IsOpen=false;}
 }
}
