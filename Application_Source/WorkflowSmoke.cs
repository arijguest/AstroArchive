using System;
using System.Linq;
using System.Windows.Data;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeWorkflow(){
   C("LibraryViewBox").SelectedItem="By target and session";Filter(false);
   var view=G("FramesGrid").ItemsSource as ListCollectionView;
   if(view==null||view.GroupDescriptions.Count!=2||view.Groups==null||view.Groups.Count==0)throw new Exception("Target/session grouping is missing.");
   if(string.IsNullOrEmpty(L("LibrarySummaryLabel").Text))throw new Exception("Library exposure summary is missing.");
   C("LibraryViewBox").SelectedItem="Files";Filter(false);
   var flagged=new Frame{Status="New",Rejected=true};
   if(!SkipFlagged||ImportWorkflow.Select(new[]{flagged},SkipFlagged).Count!=0)throw new Exception("Flagged captures are not skipped by default.");
   if(L("ImportSummaryLabel").Text.Length==0)throw new Exception("Import count summary is missing.");
  }
 }
}
