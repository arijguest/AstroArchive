using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroArchive {
 public partial class MainUi {
  static T Descendant<T>(DependencyObject root,Func<T,bool> match) where T:DependencyObject{
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);var typed=child as T;if(typed!=null&&match(typed))return typed;var nested=Descendant(child,match);if(nested!=null)return nested;}
   return null;
  }
  bool PressStar(DataGrid grid){grid.Focus();var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(grid),0,Key.S){RoutedEvent=Keyboard.PreviewKeyDownEvent};grid.RaiseEvent(key);PumpPopupLayout();return key.Handled;}
  // Uses the Target workflow fixture: one Repository capture and one Edited image.
  void SmokeAnnotations(string output){
   var tabs=(TabControl)Window.FindName("MainTabs");tabs.SelectedIndex=0;T("SearchBox").Text="";libraryFilters.Reset();Targets.SelectedIndex=0;Filter(true);WaitForSearches();PumpPopupLayout();
   var grid=G("FramesGrid");var frame=grid.Items.OfType<Frame>().First(f=>!string.IsNullOrEmpty(f.Hash));SmokeTableClick("FramesGrid",frame);
   TargetWorkflowCheck(PressStar(grid)&&frame.IsStarred&&Annotations.ContainsKey(frame.Hash)&&File.Exists(CaptureAnnotations.PathFor(repo.Meta)),"S did not star and save the selected capture.");
   var menu=ThemedMenu();BuildFileMenu(menu,SelectedFiles());TargetWorkflowCheck(menu.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header)=="Unstar"&&m.InputGestureText=="S")&&menu.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header).StartsWith("Tags and note")),"Capture menu lacks Unstar or Tags and notes.");
   var edit=new AnnotationEdit{Note="Clouds after 1am"};edit.AddTags.Add("Reprocess");int changed;TargetWorkflowCheck(SaveAnnotations(new IAnnotated[]{frame},edit,out changed)&&changed==1,"Tags and note were not saved.");
   Details();var facts=((Grid)Window.FindName("DetailsFacts")).Children.OfType<TextBlock>().Select(t=>t.Text).ToList();TargetWorkflowCheck(facts.Contains("Starred")&&facts.Contains("Reprocess")&&facts.Contains("Clouds after 1am"),"Capture details omit stars, tags or notes: "+string.Join(" | ",facts));
   // Deselect to see the starred tint and glyph rather than the selection colour.
   SmokeSelectionEscape(T("SearchBox"));PumpPopupLayout();grid.ScrollIntoView(frame);PumpPopupLayout();var row=(DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(frame);
   TargetWorkflowCheck(row!=null&&ReferenceEquals(row.Background,Window.TryFindResource("Starred"))&&Descendant<TextBlock>(row,t=>t.Name=="StarGlyph"&&t.IsVisible)!=null,"Starred row is not tinted and marked.");
   foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(Window,theme);PumpPopupLayout();TargetWorkflowCheck(ReferenceEquals(row.Background,Window.TryFindResource("Starred")),"Starred tint did not follow the "+theme+" theme.");Capture(Path.Combine(output,"AstroArchive_Starred_"+theme+".png"));}
   T("SearchBox").Text="tag:reprocess";Filter(true);WaitForSearches();TargetWorkflowCheck(displayed.Count==1&&displayed[0].Hash==frame.Hash,"Tag search did not find the tagged capture.");T("SearchBox").Text="";Filter(true);WaitForSearches();
   annotationsRoot=null;var reloaded=repo.All().First(f=>f.Hash==frame.Hash);ApplyAnnotations(new IAnnotated[]{reloaded});TargetWorkflowCheck(reloaded.IsStarred&&reloaded.UserNote=="Clouds after 1am","Stars or notes were not restored from the repository.");
   tabs.SelectedIndex=2;PumpPopupLayout();EditedTargets.SelectedIndex=0;PumpPopupLayout();var edited=G("EditedGrid").Items.OfType<EditedImage>().First();SmokeTableClick("EditedGrid",edited);
   TargetWorkflowCheck(PressStar(G("EditedGrid"))&&edited.IsStarred&&Annotations.ContainsKey(edited.AnnotationKey),"S did not star an Edited image.");
   menu=ThemedMenu();BuildEditedFileMenu(menu,SelectedEditedImages());TargetWorkflowCheck(menu.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header)=="Unstar")&&menu.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header).StartsWith("Tags and note")),"Edited menu lacks stars or notes.");
   var clear=new AnnotationEdit{Starred=false,Note=""};clear.RemoveTags.Add("Reprocess");SaveAnnotations(new IAnnotated[]{frame,edited},clear,out changed);
   TargetWorkflowCheck(!frame.IsStarred&&!edited.IsStarred&&Annotations.Count==0,"Clearing stars, tags and notes left annotations behind.");tabs.SelectedIndex=0;PumpPopupLayout();
  }
 }
}
