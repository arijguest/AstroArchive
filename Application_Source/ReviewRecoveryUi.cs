using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  bool SkipFlagged {get{return ((CheckBox)Window.FindName("SkipFlaggedCheck")).IsChecked==true;}}
  void InitializeReview(){
   ((CheckBox)Window.FindName("SkipFlaggedCheck")).Checked+=(s,e)=>FilterImports();((CheckBox)Window.FindName("SkipFlaggedCheck")).Unchecked+=(s,e)=>FilterImports();
   B("ReviewImportsButton").Click+=(s,e)=>ReviewCaptures(true);B("ReviewLibraryButton").Click+=(s,e)=>ReviewCaptures(false);
   B("RetryImportsButton").Click+=(s,e)=>Import(true);
  }
  void ReviewCaptures(bool imports){
   if(repo==null||RepositoryOperationBlocked)return;var rows=(imports?visibleImports:displayed).Where(CaptureScreening.NeedsReview).ToList();
   var d=new FormWindow(Window,"Review flagged captures",960,680);d.Text(rows.Count+" flagged captures in this view",true);d.Text("Telescope rejection, file integrity and transfer failures are shown separately. Screening checks FITS structure and rejection markers; it does not assess image quality. Unscreened captures remain labelled Not screened.");
   var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Extended,Height=310,ItemsSource=rows,Margin=new Thickness(0,8,0,12)};
   ReviewColumn(grid,"ID","ObjectId",56);ReviewColumn(grid,"OBJECT","TargetName",160);ReviewColumn(grid,"Problem type","ReviewCategory",210);ReviewColumn(grid,"File","OriginalName",280);d.Add(grid);
   var reason=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=110};d.Add(reason);
   grid.SelectionChanged+=(s,e)=>{var frame=grid.SelectedItem as Frame;reason.Text=frame==null?"Select a capture to read its full reason.":frame.TargetLabel+"\n"+frame.ReviewReason+"\n"+(imports?frame.SourcePath:frame.RelativePath);};if(rows.Count>0)grid.SelectedIndex=0;
   d.Button("Show flagged captures in the main view",()=>{(imports?importFilters:libraryFilters).Values["Review"]="Needs review";ApplyFilters(imports);d.Window.Close();});
   d.Button("Screen selected captures again",()=>{var selected=grid.SelectedItems.Cast<Frame>().ToList();if(selected.Count==0)return;d.Window.Close();ScreenSelection(selected,imports);});
   d.CloseOnly();d.Show();
  }
  static void ReviewColumn(DataGrid grid,string header,string property,double width){grid.Columns.Add(new DataGridTextColumn{Header=header,Binding=new Binding(property),Width=width});}
  void ScreenSelection(List<Frame> rows,bool imports){
   ScreeningResult result=null;Run(ct=>{result=repo.Screen(rows,imports,ct,Progress);return result.Checked+" screened; "+result.Problems+" need review.";},message=>{if(imports)FilterImports();else Filter(true);L("StatusLabel").Text=message;if(result.Problems>0)ShowReport("Capture screening",string.Join("\n\n",result.Errors));});
  }
  void ShowDeletionHistory(){
   if(repo==null||RepositoryOperationBlocked)return;var d=new FormWindow(Window,"Deletion history",960,660);d.Text("Deleted captures and import exclusions",true);d.Text("Allowing reimport lifts the checksum exclusion. It does not restore a deleted file; a source copy must still be available. The deletion and the decision to allow reimport stay in this log.");
   var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Extended,Height=280,ItemsSource=repo.Deletions().OrderByDescending(r=>r.DeletedUtc).ToList()};
   ReviewColumn(grid,"State","State",135);ReviewColumn(grid,"Target","TargetLabel",200);ReviewColumn(grid,"File","OriginalName",260);ReviewColumn(grid,"Deleted (UTC)","DeletedUtc",180);ReviewColumn(grid,"Reimport allowed (UTC)","AllowedUtc",180);ReviewColumn(grid,"SHA-256","Hash",460);d.Add(grid);
   var audit=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Height=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,12,0,12)};d.Add(audit);grid.SelectionChanged+=(s,e)=>{var record=grid.SelectedItem as DeletedCapture;audit.Text=record==null?"Select a capture to see its deletion and reimport history.":record.Audit;};
   var allow=d.Button("Allow selected captures to be imported again",()=>{
    var selected=grid.SelectedItems.Cast<DeletedCapture>().Where(r=>r.Excluded).ToList();if(selected.Count==0)return;
    var hashes=new HashSet<string>(selected.Select(r=>r.Hash));d.Window.Close();
    Run(ct=>{int count=repo.AllowReimport(hashes,ct);if(plan!=null)for(int i=0;i<plan.Frames.Count;i++)if(plan.Frames[i].Status=="Deleted"&&hashes.Contains(plan.Frames[i].Hash))plan.Frames[i]=repo.RecheckAllowedSource(plan.Frames[i],ct);return count+" deletion exclusions lifted. Import a source copy when available.";},message=>{FilterImports();L("StatusLabel").Text=message;});
   });allow.IsEnabled=false;grid.SelectionChanged+=(s,e)=>allow.IsEnabled=grid.SelectedItems.Cast<DeletedCapture>().Any(r=>r.Excluded);d.CloseOnly();d.Show();
  }
 }
}
