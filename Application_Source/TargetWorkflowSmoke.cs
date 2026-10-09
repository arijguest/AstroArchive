using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  static void TargetWorkflowCheck(bool condition,string message){if(!condition)throw new Exception(message);}
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
   C("LibraryViewBox").SelectedItem="Session summaries";PumpPopupLayout();SelectSession(subframeSessions.First(),System.Windows.Input.ModifierKeys.None);TargetWorkflowCheck(SelectedFiles().Count==3,"Session selection dropped a hidden target.");
   B("LibrarySelectionCounter").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TargetWorkflowCheck(SelectedFiles().Count==0&&B("LibrarySelectionCounter").Visibility==Visibility.Collapsed,"Clear kept hidden repository selections.");
   var tabs=(TabControl)Window.FindName("MainTabs");tabs.SelectedIndex=2;string source=Path.Combine(fixture,"source");Directory.CreateDirectory(source);int value=20;
   foreach(string name in new[]{"M31.png","M45.png","C2023A3_Tsuchinshan-ATLAS.png"}){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Gray8,null,new byte[]{(byte)value++,80,160,240},2)));using(var stream=File.Create(Path.Combine(source,name)))encoder.Save(stream);}
   repo.AddEditedImages(Directory.GetFiles(source),null,"Selection fixture",CancellationToken.None,null);RefreshEdited();WaitForSearches();
   TargetWorkflowCheck(EditedTargets.Items.Cast<TargetSummary>().Any(t=>CometTargets.IsComet(t.Name)&&t.Group=="Solar system"),"Edited comet detection/grouping failed.");
   EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M31");G("EditedGrid").SelectedIndex=0;PumpPopupLayout();width=EditedTargets.ActualWidth;headerHeight=((FrameworkElement)B("EditedSelectionCounter").Parent).ActualHeight;
   EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M45");G("EditedGrid").SelectedIndex=0;PumpPopupLayout();
   TargetWorkflowCheck(SelectedEditedImages().Count==2&&B("EditedSelectionCounter").IsVisible,"Edited selection did not span targets.");
   TargetWorkflowCheck(Math.Abs(EditedTargets.ActualWidth-width)<0.1&&Math.Abs(((FrameworkElement)B("EditedSelectionCounter").Parent).ActualHeight-headerHeight)<0.1,"Edited counter changed pane dimensions.");
   BuildEditedFileMenu(G("EditedGrid").ContextMenu,SelectedEditedImages());TargetWorkflowCheck(G("EditedGrid").ContextMenu.Items.OfType<MenuItem>().Any(i=>Convert.ToString(i.Header)=="Export files…"&&i.IsEnabled),"Edited menu lacks file export.");
   string destination=Path.Combine(fixture,"export");Directory.CreateDirectory(destination);Exporter.CreateEdited(repo,SelectedEditedImages(),new ExportOptions{Parent=destination},CancellationToken.None,null);
   TargetWorkflowCheck(Directory.GetFiles(destination).Select(Path.GetFileName).OrderBy(n=>n).SequenceEqual(new[]{"M31.png","M45.png"})&&Directory.GetFiles(destination).All(p=>Util.Hash(p,CancellationToken.None)==Util.Hash(Path.Combine(source,Path.GetFileName(p)),CancellationToken.None)),"Export ignored the cross-target batch or changed image bytes.");
   RefreshEdited();WaitForSearches();TargetWorkflowCheck(SelectedEditedImages().Count==2&&G("EditedGrid").SelectedItems.Count==1,"Edited refresh lost selections.");
   EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M31");TargetWorkflowCheck(G("EditedGrid").SelectedItems.Count==1,"Returning to an Edited target lost its selection.");
   foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(Window,theme);PumpPopupLayout();Capture(Path.Combine(output,"AstroArchive_Target_Selection_"+theme+".png"));}
   B("EditedSelectionCounter").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TargetWorkflowCheck(SelectedEditedImages().Count==0,"Clear left a hidden Edited selection.");
   File.WriteAllText(Path.Combine(output,"target-workflow-smoke.txt"),"PASS: Repository and Edited retain selections across targets, searches, sorting modes and refresh; counters clear all files without enlarging panes; comet grouping, device search, menu and verified export work together.");
  }
 }
}
