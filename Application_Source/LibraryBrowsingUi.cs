using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  void InitializeBrowsing(){
   C("LibraryViewBox").ItemsSource=new[]{"Files","By target","By target and session"};C("LibraryViewBox").SelectedIndex=0;
   C("LibraryViewBox").SelectionChanged+=(s,e)=>{if(!updating)Filter(false);};
   var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding("."){Converter=new CaptureGroupLabelConverter()});text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);text.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);text.SetResourceReference(TextBlock.ForegroundProperty,"Text");text.SetValue(TextBlock.MarginProperty,new Thickness(12,14,12,10));
   G("FramesGrid").GroupStyle.Add(new GroupStyle{HeaderTemplate=new DataTemplate{VisualTree=text}});
  }
  void DisplayLibrary(){
   var view=new ListCollectionView(displayed);string mode=Convert.ToString(C("LibraryViewBox").SelectedItem);
   if(mode!="Files"){view.GroupDescriptions.Add(new PropertyGroupDescription("TargetLabel"));if(mode=="By target and session")view.GroupDescriptions.Add(new PropertyGroupDescription("SessionKey"));}
   SetRows("FramesGrid",view);L("LibraryCount").Text=displayed.Count+" files"+(libraryFilters.ActiveCount>0?" · "+libraryFilters.ActiveCount+" active filters":"");
   var summary=CaptureGroups.Summarize(displayed);L("LibrarySummaryLabel").Text=summary.Detail;
  }
 }
 public sealed class CaptureGroupLabelConverter:IValueConverter {
  static IEnumerable<Frame> Frames(CollectionViewGroup group){foreach(var item in group.Items){var frame=item as Frame;if(frame!=null)yield return frame;else{var nested=item as CollectionViewGroup;if(nested!=null)foreach(var child in Frames(nested))yield return child;}}}
  public object Convert(object value,Type targetType,object parameter,CultureInfo culture){var group=value as CollectionViewGroup;if(group==null)return "";var frames=Frames(group).ToList();string name=System.Convert.ToString(group.Name);if(name.StartsWith("session:")){var session=CaptureSessions.Describe(frames);name=session.Dates+" · "+string.Join(", ",frames.Select(f=>f.Telescope).Distinct())+" · "+string.Join(", ",frames.Select(f=>f.Camera).Distinct());}return name+"\n"+CaptureGroups.Summarize(frames).Detail;}
  public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
}
