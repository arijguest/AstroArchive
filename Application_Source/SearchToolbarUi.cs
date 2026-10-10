using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace AstroArchive {
 // Keep complete button rows when the preview pane or text size changes.
 public sealed class ToolbarButtonColumnsConverter:IMultiValueConverter {
  public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture){
   int count=int.Parse(System.Convert.ToString(parameter),CultureInfo.InvariantCulture);
   if(values.Length<2||!(values[0] is double)||!(values[1] is double))return count;
   double width=(double)values[0],font=(double)values[1];if(width<=0)return count;
   int columns=Math.Max(1,Math.Min(count,(int)(width/(72*Math.Max(1,font/13)))));
   while(count%columns!=0)columns--;return columns;
  }
  public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
}
