using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  static void TargetWorkflowCheck(bool condition,string message){if(!condition)throw new Exception(message);}
  void SmokeSelectionEscape(UIElement source){source.Focus();source.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(source),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});PumpPopupLayout();}
  void SmokeTableClick(string name,object item,MouseButton button=MouseButton.Left){
   var grid=G(name);grid.ScrollIntoView(item);PumpPopupLayout();var row=grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
   TargetWorkflowCheck(row!=null,"Click fixture did not realize a row in "+name);var cell=PopupChildren<DataGridCell>(row).First();
   cell.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,button){RoutedEvent=button==MouseButton.Left?UIElement.PreviewMouseLeftButtonDownEvent:UIElement.PreviewMouseRightButtonDownEvent});PumpPopupLayout();
  }
  public void SmokeTargetWorkflow(string output){
   Directory.CreateDirectory(output);Window.Show();PumpPopupLayout();string fixture=Path.Combine(Path.GetTempPath(),"AstroArchive-target-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
   OpenRepository(Path.Combine(fixture,"repository"),false);((TabControl)Window.FindName("MainTabs")).SelectedIndex=0;PumpPopupLayout();
   foreach(string name in new[]{"SearchHelpButton","ImportSearchHelpButton","EditedSearchHelpButton"})TargetWorkflowCheck(Window.FindName(name)==null,"A search help button remains.");
   all=new List<Frame>{new Frame{Hash="one",Target="M31",Kind="Light",OriginalName="one.fit",Session="fixture"},new Frame{Hash="two",Target="M31",Kind="Light",OriginalName="two.fit",Session="fixture"},new Frame{Hash="three",Target="M45",Kind="Stack",StackCount=1445,OriginalName="three.fit",Model="Seestar S50 Pro",Make="Seestar"},new Frame{Hash="four",Target="NEAT",Kind="Stack",OriginalName="four.fit"}};
   C("LibraryViewBox").SelectedItem="Show all files";Filter(true);Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M31");PumpPopupLayout();
   double width=Targets.ActualWidth,headerHeight=((FrameworkElement)B("LibrarySelectionCounter").Parent).ActualHeight;
   G("FramesGrid").SelectedItem=displayed[0];Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M45");G("FramesGrid").SelectedItem=displayed[0];PumpPopupLayout();
   var typeColumn=G("FramesGrid").Columns.OfType<DataGridBoundColumn>().Single(c=>ColumnId(c)=="Kind");TargetWorkflowCheck(((System.Windows.Data.Binding)typeColumn.Binding).Path.Path=="KindLabel"&&typeColumn.SortMemberPath=="Kind"&&PopupChildren<TextBlock>(G("FramesGrid")).Any(t=>t.Text=="Stack (1445)"),"Stack count did not render or changed the column identity/sort.");
   TargetWorkflowCheck(SelectedFiles().Count==2&&B("LibrarySelectionCounter").IsVisible&&Convert.ToString(B("LibrarySelectionCounter").Content)=="2 selected","Repository selection did not span targets: "+SelectedFiles().Count+" files, counter="+B("LibrarySelectionCounter").Content+", visible="+B("LibrarySelectionCounter").IsVisible);
   TargetWorkflowCheck(Math.Abs(Targets.ActualWidth-width)<0.1&&Math.Abs(((FrameworkElement)B("LibrarySelectionCounter").Parent).ActualHeight-headerHeight)<0.1,"The counter enlarged the target pane or header.");
   T("SearchBox").Text="M45 S50 Pro stack";WaitForSearches();TargetWorkflowCheck(displayed.Count==1&&SelectedFiles().Count==2,"Search lost a hidden selection or device match.");
   T("SearchBox").Clear();WaitForSearches();Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M31");TargetWorkflowCheck(G("FramesGrid").SelectedItems.Count==1,"Returning to a target lost its selection.");
   G("FramesGrid").SelectedItems.Add(displayed.Single(f=>f.Hash=="two"));TargetWorkflowCheck(SelectedFiles().Count==3,"Adding a second visible capture lost another target.");
   SmokeTableClick("FramesGrid",displayed.Single(f=>f.Hash=="one"),MouseButton.Right);TargetWorkflowCheck(SelectedFiles().Count==3&&Context().Count==3,"Right-clicking a selected row reduced the metadata batch.");
   SmokeTableClick("FramesGrid",displayed.Single(f=>f.Hash=="one"));TargetWorkflowCheck(SelectedFiles().Count==1&&SelectedFiles().Single().Hash=="one"&&G("FramesGrid").SelectedItems.Count==1,"Plain click retained visible or hidden selections.");
   Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M45");SmokeTableClick("FramesGrid",displayed.Single());TargetWorkflowCheck(SelectedFiles().Count==1&&SelectedFiles().Single().Hash=="three","Clicking another target did not replace the repository batch.");
   Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M31");C("LibraryViewBox").SelectedItem="Session summaries";PumpPopupLayout();SelectSession(subframeSessions.First(),System.Windows.Input.ModifierKeys.None);TargetWorkflowCheck(SelectedFiles().Count==2&&SelectedFiles().All(f=>f.Target=="M31"),"Plain session selection retained a hidden target.");
   SmokeSelectionEscape(T("SearchBox"));TargetWorkflowCheck(SelectedFiles().Count==0&&!subframeSessions.Any(group=>group.IsSelected)&&B("LibrarySelectionCounter").Visibility==Visibility.Collapsed,"Escape from search kept hidden repository/session selections.");
   var tabs=(TabControl)Window.FindName("MainTabs");tabs.SelectedIndex=2;string source=Path.Combine(fixture,"source");Directory.CreateDirectory(source);int value=20;
   foreach(string name in new[]{"M31.png","M45.png","C2023A3_Tsuchinshan-ATLAS.png"}){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Gray8,null,new byte[]{(byte)value++,80,160,240},2)));using(var stream=File.Create(Path.Combine(source,name)))encoder.Save(stream);}
   repo.AddEditedImages(Directory.GetFiles(source),null,"Selection fixture",CancellationToken.None,null);RefreshEdited();WaitForSearches();
   TargetWorkflowCheck(EditedTargets.Items.Cast<TargetSummary>().Any(t=>CometTargets.IsComet(t.Name)&&t.Group=="Solar system"),"Edited comet detection/grouping failed.");
   EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M31");G("EditedGrid").SelectedIndex=0;PumpPopupLayout();width=EditedTargets.ActualWidth;headerHeight=((FrameworkElement)B("EditedSelectionCounter").Parent).ActualHeight;
   EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M45");G("EditedGrid").SelectedIndex=0;PumpPopupLayout();
   TargetWorkflowCheck(SelectedEditedImages().Count==2&&B("EditedSelectionCounter").IsVisible&&Convert.ToString(B("EditedSelectionCounter").Content)=="2 selected","Edited selection did not span targets.");
   TargetWorkflowCheck(Math.Abs(EditedTargets.ActualWidth-width)<0.1&&Math.Abs(((FrameworkElement)B("EditedSelectionCounter").Parent).ActualHeight-headerHeight)<0.1,"Edited counter changed pane dimensions.");
   T("EditedSearchBox").Text="M45";WaitForSearches();TargetWorkflowCheck(G("EditedGrid").Items.Count==1&&SelectedEditedImages().Count==2,"Edited search lost a hidden target selection.");
   SortTable("EditedGrid",G("EditedGrid").Columns.First(c=>c.SortMemberPath=="Filename"),false);WaitForSearches();TargetWorkflowCheck(SelectedEditedImages().Count==2&&G("EditedGrid").SelectedItems.Count==1,"Edited background sorting lost the cross-target batch.");
   T("EditedSearchBox").Clear();WaitForSearches();tabs.SelectedIndex=0;tabs.SelectedIndex=2;WaitForSearches();TargetWorkflowCheck(SelectedEditedImages().Count==2,"Changing pages lost the Edited batch.");
   BuildEditedFileMenu(G("EditedGrid").ContextMenu,SelectedEditedImages());TargetWorkflowCheck(G("EditedGrid").ContextMenu.Items.OfType<MenuItem>().Any(i=>Convert.ToString(i.Header)=="Export files…"&&i.IsEnabled),"Edited menu lacks file export.");
   string destination=Path.Combine(fixture,"export");Directory.CreateDirectory(destination);Exporter.CreateEdited(repo,SelectedEditedImages(),new ExportOptions{Parent=destination},CancellationToken.None,null);
   TargetWorkflowCheck(Directory.GetFiles(destination).Select(Path.GetFileName).OrderBy(n=>n).SequenceEqual(new[]{"M31.png","M45.png"})&&Directory.GetFiles(destination).All(p=>Util.Hash(p,CancellationToken.None)==Util.Hash(Path.Combine(source,Path.GetFileName(p)),CancellationToken.None)),"Export ignored the cross-target batch or changed image bytes.");
   RefreshEdited();WaitForSearches();TargetWorkflowCheck(SelectedEditedImages().Count==2&&G("EditedGrid").SelectedItems.Count==1,"Edited refresh lost selections.");
   EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M31");TargetWorkflowCheck(G("EditedGrid").SelectedItems.Count==1,"Returning to an Edited target lost its selection.");
   foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(Window,theme);PumpPopupLayout();Capture(Path.Combine(output,"AstroArchive_Target_Selection_"+theme+".png"));}
   SmokeTableClick("EditedGrid",G("EditedGrid").Items[0],MouseButton.Right);TargetWorkflowCheck(SelectedEditedImages().Count==2,"Right-click reduced the Edited metadata batch.");
   SmokeTableClick("EditedGrid",G("EditedGrid").Items[0]);TargetWorkflowCheck(SelectedEditedImages().Count==1&&SelectedEditedImages().Single().Metadata.Object=="M31"&&G("EditedGrid").SelectedItems.Count==1,"Plain click kept a hidden Edited selection.");
   EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M45");SmokeTableClick("EditedGrid",G("EditedGrid").Items[0]);TargetWorkflowCheck(SelectedEditedImages().Count==1&&SelectedEditedImages().Single().Metadata.Object=="M45","Click in another Edited target retained the old selection.");
   SmokeSelectionEscape(T("EditedSearchBox"));TargetWorkflowCheck(SelectedEditedImages().Count==0&&G("EditedGrid").SelectedItems.Count==0&&B("EditedSelectionCounter").Visibility==Visibility.Collapsed,"Escape from search left a hidden Edited selection.");
   tabs.SelectedIndex=1;SetRows("ImportGrid",all.Take(2).ToList());WaitForSearches();foreach(var frame in G("ImportGrid").Items.OfType<Frame>())G("ImportGrid").SelectedItems.Add(frame);
   SmokeSelectionEscape(T("ImportSearchBox"));TargetWorkflowCheck(G("ImportGrid").SelectedItems.Count==0,"Escape from search retained Import selections.");
   File.WriteAllText(Path.Combine(output,"target-workflow-smoke.txt"),"PASS: Repository and Edited retain additive batches across targets, searches, sorting and refresh; plain clicks replace visible and hidden selections; right-click preserves selected batches; counters and Escape clear all files; comet grouping, device search, menu and verified export work together.");
  }
 }
}
