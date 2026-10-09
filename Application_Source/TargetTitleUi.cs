using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace AstroArchive {
 public sealed class TargetTitle:TextBlock {
  public static readonly DependencyProperty FullTitleProperty=DependencyProperty.Register("FullTitle",typeof(string),typeof(TargetTitle),new PropertyMetadata("",(s,e)=>((TargetTitle)s).UpdateTitle()));
  public string FullTitle{get{return (string)GetValue(FullTitleProperty);}set{SetValue(FullTitleProperty,value);}}
  public TargetTitle(){Loaded+=(s,e)=>UpdateTitle();SizeChanged+=(s,e)=>UpdateTitle();}
  void UpdateTitle(){
   string full=FullTitle??"",compact=TargetNavigation.WithoutObjectType(full),title=full;
   double width=Math.Max(0,ActualWidth-Padding.Left-Padding.Right);
   if(compact!=full&&width>0){
    var measured=new FormattedText(full,CultureInfo.CurrentUICulture,FlowDirection,new Typeface(FontFamily,FontStyle,FontWeight,FontStretch),FontSize,Foreground??Brushes.Black,VisualTreeHelper.GetDpi(this).PixelsPerDip);
    if(measured.WidthIncludingTrailingWhitespace>width+0.5)title=compact;
   }
   if(Text!=title)Text=title;
  }
 }
}
