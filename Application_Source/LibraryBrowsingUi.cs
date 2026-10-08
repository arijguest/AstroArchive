using System;
using System.Collections.Generic;
using System.Globalization;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  void InitializeBrowsing(){
   C("LibraryViewBox").ItemsSource=new[]{"Session summaries","Show all files","By target","By target and session"};C("LibraryViewBox").SelectedIndex=0;
   C("LibraryViewBox").SelectionChanged+=(s,e)=>{if(!updating)Filter(false);};
   G("FramesGrid").GroupStyle.Add(new GroupStyle{ContainerStyle=(Style)Window.FindResource("CaptureSessionGroupStyle")});
   InitializeSessionSelection();
  }
  List<SubframeSession> subframeSessions=new List<SubframeSession>();
  void DisplayLibrary(){
   var view=new ListCollectionView(displayed);string mode=Convert.ToString(C("LibraryViewBox").SelectedItem);
   if(mode=="Session summaries"){
    var previous=subframeSessions.ToDictionary(g=>g.Key);subframeSessions=SubframeSessions.Build(displayed);foreach(var session in subframeSessions){SubframeSession old;if(previous.TryGetValue(session.Key,out old)){session.Expanded=old.Expanded;session.IsSelected=old.IsSelected;}}
    view.GroupDescriptions.Add(new SubframeSessionDescription(subframeSessions));
   }else{subframeSessions.Clear();activeSessionKey=null;if(mode!="Files"&&mode!="Show all files"){view.GroupDescriptions.Add(new PropertyGroupDescription("TargetLabel"));if(mode=="By target and session")view.GroupDescriptions.Add(new PropertyGroupDescription("SessionKey"));}}
   changingSessionSelection=true;try{SetRows("FramesGrid",view);}finally{changingSessionSelection=false;}Details();
   var summary=CaptureGroups.Summarize(displayed);L("LibrarySummaryLabel").Text=summary.Detail;
  }
 }
 public sealed class SubframeSessionDescription:GroupDescription {
  readonly Dictionary<Frame,SubframeSession> sessions=new Dictionary<Frame,SubframeSession>();
  public SubframeSessionDescription(IEnumerable<SubframeSession> groups){foreach(var group in groups)foreach(var frame in group.Frames)sessions[frame]=group;}
  public override object GroupNameFromItem(object item,int level,CultureInfo culture){var frame=(Frame)item;SubframeSession group;return sessions.TryGetValue(frame,out group)?(object)group:frame;}
 }
 public sealed class PlainCaptureGroupConverter:IValueConverter {
  public object Convert(object value,Type target,object parameter,CultureInfo culture){var group=value as CollectionViewGroup;return group!=null&&group.Name is Frame;}
  public object ConvertBack(object value,Type target,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
 public sealed class CaptureGroupLabelConverter:IValueConverter {
  static IEnumerable<Frame> Frames(CollectionViewGroup group){foreach(var item in group.Items){var frame=item as Frame;if(frame!=null)yield return frame;else{var nested=item as CollectionViewGroup;if(nested!=null)foreach(var child in Frames(nested))yield return child;}}}
  public object Convert(object value,Type targetType,object parameter,CultureInfo culture){var group=value as CollectionViewGroup;if(group==null)return "";var frames=Frames(group).ToList();var compact=group.Name as SubframeSession;if(compact!=null)return compact.Label;string name=System.Convert.ToString(group.Name);if(name.StartsWith("session:")){var session=CaptureSessions.Describe(frames);name=session.Dates+" · "+string.Join(", ",frames.Select(f=>f.Telescope).Distinct())+" · "+string.Join(", ",frames.Select(f=>f.Camera).Distinct());}return name+"\n"+CaptureGroups.Summarize(frames).Detail;}
  public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
}
