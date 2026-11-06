using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace AstroArchive {
 public partial class MainUi {
  readonly CaptureFilters libraryFilters=new CaptureFilters(),importFilters=new CaptureFilters();
  bool importLive;
  List<Frame> visibleImports=new List<Frame>();
  void InitializeFailedImports(){
   var ignore=(CheckBox)Window.FindName("IgnoreFailedCheck");ignore.IsChecked=settings.IgnoreFailed;
   RoutedEventHandler changed=(s,e)=>{if(RepositoryOperationBlocked||settings.IgnoreFailed==(ignore.IsChecked==true))return;settings.IgnoreFailed=ignore.IsChecked==true;SaveSettings();InvalidateImportPlan();L("StatusLabel").Text="Import option saved. Scan the source folder again to apply it.";};
   ignore.Checked+=changed;ignore.Unchecked+=changed;
  }
  void InitializeFilters(){
   InitializeImportManagement();
   B("LibraryFiltersButton").Click+=(s,e)=>ShowFilters(false);
   B("ImportFiltersButton").Click+=(s,e)=>ShowFilters(true);
   T("ImportSearchBox").TextChanged+=(s,e)=>{Watermark("ImportSearchBox","Search import results...");ScheduleSearch("ImportSearchBox");};Watermark("ImportSearchBox","Search import results...");
   B("ImportClearButton").Click+=(s,e)=>{importFilters.Reset();T("ImportSearchBox").Text="";FilterImports();};
   B("ScreenImportsButton").Click+=(s,e)=>ScreenFiles(true);
   B("ScreenLibraryButton").Click+=(s,e)=>ScreenFiles(false);
   G("ImportGrid").SelectionChanged+=(s,e)=>{
    var frame=G("ImportGrid").SelectedItem as Frame;
    L("ImportDetailsLabel").Text=frame==null?"Select a capture to see screening or import details.":frame.OriginalName+"  ·  "+frame.Status+"  ·  "+(CaptureScreening.NeedsReview(frame)?frame.ReviewCategory+" · "+frame.ReviewReason:frame.SourceDisposition??frame.Notes);
    B("AssignUnknownTargetButton").IsEnabled=!RepositoryOperationBlocked&&UnknownImportSelection().Count>0;
    UpdateImportScope();
   };
  }
  void ApplyFilters(bool imports){if(imports)FilterImports();else Filter(true);}
  List<Frame> CurrentImportRows(){return importLive?importRows.ToList():plan==null?new List<Frame>():plan.Frames;}
  void FilterImports(){
   if(updating)return;var source=CurrentImportRows();if(Window.IsLoaded&&source.Count>2000){ScheduleSearch("ImportSearchBox",true,true);return;}CancelSearch("ImportSearchBox");ShowSearchError("ImportSearchBox",FileSearch.Parse(T("ImportSearchBox").Text));visibleImports=importFilters.Apply(source,T("ImportSearchBox").Text);
   ApplyImportRows(source,visibleImports,ImportWorkflow.Summarize(source,visibleImports,SkipFlagged),ImportWorkflow.Select(visibleImports,SkipFlagged,true).Count);
  }
  void ApplyImportRows(List<Frame> source,List<Frame> rows,ImportSummary summary,int retry,bool presorted=false){
   visibleImports=rows;
   var grid=G("ImportGrid");var selection=new HashSet<string>(grid.SelectedItems.Cast<Frame>().Select(f=>f.SourcePath));SetRows("ImportGrid",visibleImports,presorted);foreach(var frame in visibleImports.Where(f=>selection.Contains(f.SourcePath)))if(!grid.SelectedItems.Contains(frame))grid.SelectedItems.Add(frame);
   B("ImportFiltersButton").Content="Filters"+(importFilters.ActiveCount>0?" ("+importFilters.ActiveCount+")":"");
   int ready=summary.Ready;L("ImportSummaryLabel").Text=summary.Text;L("ImportSummaryLabel").Visibility=summary.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
   B("ImportButton").Content=Localization.F("Import all ready ({0})",ready);
   UiHelp.Hint(B("ImportButton"),"Import every ready file in the current filtered view.",true);
   B("ImportButton").IsEnabled=!RepositoryOperationBlocked&&repo!=null&&plan!=null&&ready>0;
   UpdateImportScope();
   B("ScreenImportsButton").IsEnabled=!RepositoryOperationBlocked&&repo!=null&&visibleImports.Any(f=>f.Status!="Deleted");
   B("ReviewImportsButton").IsEnabled=!RepositoryOperationBlocked&&repo!=null&&summary.Flagged>0;retry=plan==null?0:retry;B("RetryImportsButton").Content="Retry "+retry+" failed import"+(retry==1?"":"s");B("RetryImportsButton").IsEnabled=!RepositoryOperationBlocked&&repo!=null&&retry>0;
   B("AssignUnknownTargetButton").IsEnabled=!RepositoryOperationBlocked&&UnknownImportSelection().Count>0;
   B("ImportOptionsButton").IsEnabled=!NetworkImportBlocked;
   L("ScanLabel").Text=source.Count==0&&plan!=null&&plan.FastSkippedFiles>0&&plan.Errors.Count==0?"Nothing new to import · "+plan.FastSkippedFiles+" archived files skipped in "+plan.FastSkippedFolders+" folders":source.Count==0&&plan==null?"Choose a source folder and scan to begin.":summary.Shown+" / "+summary.Total+" shown · "+(summary.Total-summary.Shown)+" hidden by search/filters · "+importFilters.ActiveCount+" active filters"+(SkipFlagged?" · "+summary.SkippedFlagged+" flagged candidates skipped":" · flagged captures included")+(plan!=null&&plan.FastSkippedFiles>0?" · "+plan.FastSkippedFiles+" archived files skipped in "+plan.FastSkippedFolders+" folders":"")+(plan!=null&&plan.IgnoredFailed>0?"  ·  "+plan.IgnoredFailed+" failed filenames ignored":"")+(plan!=null&&plan.IgnoredRaster>0?" · "+plan.IgnoredRaster+" PNG/JPG ignored":"");
  }
  List<Frame> ReadySelectedImports(){
   return ImportWorkflow.Select(G("ImportGrid").SelectedItems.OfType<Frame>(),SkipFlagged);
  }
  void UpdateImportScope(){
   int selected=ReadySelectedImports().Count;
   var button=B("ImportSelectedButton");
   button.Content=Localization.F("Import selected ({0})",selected);
   UiHelp.Hint(button,"Import the ready files you selected in this table.",true);
   button.IsEnabled=!RepositoryOperationBlocked&&!SearchBlocked("ImportSearchBox")&&repo!=null&&plan!=null&&selected>0;
   var label=L("ImportScopeLabel");
   if(repo==null){label.Text="";label.ToolTip=null;return;}
   bool remove=((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked==true;
   string retention=Localization.T(remove?"Eligible originals removed after verification":"Originals kept");
   label.Text=Localization.F("Destination {0}",CompactPath(repo.Root))+" · "+retention;
   label.ToolTip=repo.Root+" · "+retention;
  }
  void ScreenFiles(bool imports){
   if(repo==null||RepositoryOperationBlocked||SearchBlocked(imports?"ImportSearchBox":"SearchBox"))return;
   var rows=imports?visibleImports.Where(f=>f.Status!="Deleted").ToList():Context();if(rows.Count==0)return;
   ScreenSelection(rows,imports);
  }
  ContextMenu BuildImportTools(){
   var menu=ThemedMenu();var selection=SelectedImportRows();
   menu.Items.Add(FileAction("Edit selected metadata…",()=>Edit(true,selection),selection.Count>0));
   menu.Items.Add(FileAction("Identify selected targets…",()=>Identify(true),selection.Count>0));
   menu.Items.Add(FileAction("Set Unknown targets…",AssignUnknownImportTargets,UnknownImportSelection().Count>0));
   menu.Items.Add(ImportStackingMenu());
   menu.Items.Add(new Separator());menu.Items.Add(FileAction("Remove selected from import list…",RemoveSelectedImports,selection.Count>0));
   menu.Items.Add(FileAction("Delete selected archived copies…",DeleteSelectedImportCopies,selection.Any(f=>!Repository.PendingMetadata(f,true)&&all.Any(a=>a.Hash==f.Hash))));
   menu.Items.Add(new Separator());menu.Items.Add(MenuAction("Scan report…",()=>ShowReport("Scan report",plan==null?"Scan a folder first.":plan.ScanReport),true,false));
   return menu;
  }
  void ShowImportTools(){var menu=BuildImportTools();menu.PlacementTarget=TopMenu("ImportMenu");menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;
  }
 }
}
