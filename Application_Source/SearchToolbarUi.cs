using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace AstroArchive {
 // Keep complete button rows when the preview pane or text size changes.
 public sealed class ToolbarButtonColumnsConverter:IMultiValueConverter {
  public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture){
   int count=int.Parse(System.Convert.ToString(parameter),CultureInfo.InvariantCulture);
   if(values.Length<2||!(values[0] is double)||!(values[1] is double))return count;
   double width=(double)values[0],font=(double)values[1];if(width<=0)return count;
   int columns=Math.Max(1,Math.Min(count,(int)(width/Math.Max(72*Math.Max(1,font/13),TranslatedWidth(values.Length>2?values[2] as Panel:null)))));
   while(count%columns!=0)columns--;return columns;
  }
  // Give each button room for its label; translated and longer English labels can exceed the minimum.
  static double TranslatedWidth(Panel panel){
   if(panel==null)return 0;double widest=0;
   foreach(var button in panel.Children.OfType<Button>().Where(b=>b.Visibility!=Visibility.Collapsed&&b.Content is string)){
    var text=new FormattedText(Localization.T((string)button.Content),CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(button.FontFamily,button.FontStyle,button.FontWeight,button.FontStretch),button.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(button).PixelsPerDip);
    widest=Math.Max(widest,text.WidthIncludingTrailingWhitespace+button.Padding.Left+button.Padding.Right+button.BorderThickness.Left+button.BorderThickness.Right+button.Margin.Left+button.Margin.Right+6);
   }
   return widest;
  }
  public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
}
