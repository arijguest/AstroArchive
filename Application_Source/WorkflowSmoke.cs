using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeWorkflow(){
   C("LibraryViewBox").SelectedItem="By target and session";Filter(false);
   var view=G("FramesGrid").ItemsSource as ListCollectionView;
   if(view==null||view.GroupDescriptions.Count!=2||view.Groups==null||view.Groups.Count==0)throw new Exception("Target/session grouping is missing.");
   if(string.IsNullOrEmpty(L("LibrarySummaryLabel").Text))throw new Exception("Library exposure summary is missing.");
   C("LibraryViewBox").SelectedItem="Files";Filter(false);
   var grid=G("FramesGrid");if(grid.Items.Count>0){grid.SelectedIndex=0;Details();var facts=((Grid)Window.FindName("DetailsFacts")).Children.OfType<TextBlock>().Select(t=>t.Text).ToList();string all=L("DetailsTitle").Text+"\n"+string.Join("\n",facts)+"\n"+L("DetailsLabel").Text;
    if(L("DetailsTitle").Text.Length==0||facts.Count==0||facts.Count%2!=0||facts.Any(t=>t=="-"||t.StartsWith("Unknown",StringComparison.OrdinalIgnoreCase))||new[]{"suggest","evidence","Archive · ","not a detected","Camera channel unknown"}.Any(junk=>all.IndexOf(junk,StringComparison.OrdinalIgnoreCase)>=0))throw new Exception("Capture details are not a focused label/value list: "+all);grid.SelectedIndex=-1;}
   CancelPreview();if(B("OpenPreviewButton").IsEnabled)throw new Exception("Show in File Explorer stayed enabled with nothing previewed.");
   var flagged=new Frame{Status="New",Rejected=true};
   if(!SkipFlagged||ImportWorkflow.Select(new[]{flagged},SkipFlagged).Count!=0)throw new Exception("Flagged captures are not skipped by default.");
   if(L("ImportSummaryLabel").Text.Length==0)throw new Exception("Import count summary is missing.");
  }
 }
}
