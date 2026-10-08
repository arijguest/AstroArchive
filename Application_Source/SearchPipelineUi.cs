using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace AstroArchive {
 public partial class MainUi {
  sealed class SearchResult {
   public FileSearch Query;public List<Frame> Source,Frames;public List<EditedImage> Images;
   public List<TargetSummary> Targets;public string Target;
   public List<SubframeSession> Sessions;public CaptureGroupSummary Summary;public ImportSummary ImportSummary;public int Retry;
  }
  sealed class SearchPanel:IDisposable {
   public DispatcherTimer Timer;public readonly LatestSearch<SearchResult> Worker=new LatestSearch<SearchResult>();
   public SearchIndex<Frame> Captures;public SearchIndex<EditedImage> Edited;
   public int Version;public bool Pending,Blocked,Disposed;
   public readonly Dictionary<string,bool> Enabled=new Dictionary<string,bool>();
   public void Dispose(){Disposed=true;Version++;Timer.Stop();Worker.Dispose();Captures=null;Edited=null;}
  }
  readonly Dictionary<string,SearchPanel> searchPanels=new Dictionary<string,SearchPanel>();
  bool SearchBlocked(string name){SearchPanel state;return searchPanels.TryGetValue(name,out state)&&state.Blocked;}
  bool ActiveSearchBlocked{get{int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;return SearchBlocked(page==1?"ImportSearchBox":page==2?"EditedSearchBox":"SearchBox");}}
  SearchPanel SearchState(string name){
   SearchPanel state;if(searchPanels.TryGetValue(name,out state))return state;
   state=new SearchPanel{Timer=new DispatcherTimer(DispatcherPriority.Background,Window.Dispatcher){Interval=TimeSpan.FromMilliseconds(220)}};
   state.Timer.Tick+=(s,e)=>{state.Timer.Stop();ExecuteSearch(name,state);};searchPanels[name]=state;return state;
  }
  IEnumerable<string> SearchControls(string name){
   return name=="SearchBox"?new[]{"FramesGrid","TargetList","ExportButton","RotationButton","SolveButton","EditButton","MoreButton","ScreenLibraryButton","ReviewLibraryButton"}:
    name=="ImportSearchBox"?new[]{"ImportGrid","ImportButton","ScreenImportsButton","ReviewImportsButton","RetryImportsButton","AssignUnknownTargetButton","ImportToolsButton"}:
    new[]{"EditedGrid","EditedTargetList","EditedPreviewButton","EditedDetailsButton","EditedEditorButton","EditedOpenPreviewButton"};
  }
  void BlockSearchControls(string name,SearchPanel state,bool capture=false){foreach(string control in SearchControls(name)){var element=Window.FindName(control) as UIElement;if(element==null)continue;if(capture||!state.Enabled.ContainsKey(control))state.Enabled[control]=element.IsEnabled;element.IsEnabled=false;}}
  void RestoreSearchControls(SearchPanel state){foreach(var item in state.Enabled){var element=Window.FindName(item.Key) as UIElement;if(element!=null)element.IsEnabled=item.Value;}state.Enabled.Clear();}
  void ApplyPendingSearchControls(){foreach(var entry in searchPanels.Where(p=>p.Value.Blocked))BlockSearchControls(entry.Key,entry.Value,true);UpdateNavigationState();}
  void CancelSearch(string name,bool invalidate=true){
   SearchPanel state;if(!searchPanels.TryGetValue(name,out state))return;state.Version++;state.Timer.Stop();state.Worker.Cancel();state.Pending=false;state.Blocked=false;RestoreSearchControls(state);if(invalidate){state.Captures=null;state.Edited=null;}UpdateNavigationState();
  }
  void ScheduleSearch(string name,bool invalidate=false,bool immediate=false){
   if(updating||closing||name=="EditedSearchBox"&&!editedReady)return;
   SearchPanel state=SearchState(name);if(state.Disposed)return;state.Version++;state.Worker.Cancel();state.Timer.Stop();
   if(invalidate){state.Captures=null;state.Edited=null;}state.Pending=true;state.Blocked=true;BlockSearchControls(name,state);
   if(name=="SearchBox"){Watermark();L("LibrarySummaryLabel").Text="Searching…";}
   else if(name=="ImportSearchBox"){Watermark(name);L("ImportSummaryLabel").Text="Searching…";B("ImportButton").Content="Searching…";}
   else {L("EditedSummary").Text="Searching…";L("EditedSearchHint").Visibility=T(name).Text.Length==0?Visibility.Visible:Visibility.Collapsed;}
   UpdateNavigationState();if(immediate)ExecuteSearch(name,state);else state.Timer.Start();
  }
  SearchSort[] SearchSorts(string grid){List<SortDescription> sorts;return tableSorts.TryGetValue(grid,out sorts)?sorts.Select(s=>new SearchSort{Property=s.PropertyName,Descending=s.Direction==ListSortDirection.Descending}).ToArray():new SearchSort[0];}
  async void ExecuteSearch(string name,SearchPanel state){
   int version=state.Version;string text=T(name).Text;
   try{
    Func<CancellationToken,SearchResult> work;
    if(name=="EditedSearchBox"){
     var source=editedImages.ToArray();string imageClass=Convert.ToString(C("EditedClassFilter").SelectedItem);string target=EditedTargets.SelectedItem is TargetSummary?((TargetSummary)EditedTargets.SelectedItem).Name:"All targets";
     var sorts=SearchSorts("EditedGrid");var culture=G("EditedGrid").Items.Culture??CultureInfo.CurrentCulture;var index=state.Edited??(state.Edited=new SearchIndex<EditedImage>(EditedSearchDocument));
     work=token=>{var query=FileSearch.Parse(text);var rows=index.Find(source,query,i=>imageClass=="All images"||i.Metadata.ImageClass==imageClass,token);token.ThrowIfCancellationRequested();
      var targets=TargetNavigation.Build(rows.Select(i=>new Frame{Target=i.Metadata.ImageClass=="Meteor"?"Meteor":i.Metadata.Object,Kind="Edited image"}),token).Select(t=>(TargetSummary)new EditedTargetSummary{Name=t.Name,Files=t.Files}).ToList();
      string active=targets.Any(t=>t.Name==target)?target:"All targets";if(active!="All targets")rows=rows.Where(i=>Catalog.CanonicalTarget(i.Metadata.ImageClass=="Meteor"?"Meteor":i.Metadata.Object)==active).ToList();
      return new SearchResult{Query=query,Targets=targets,Target=active,Images=SearchOrdering.Order(rows,sorts,culture,token)};};
    }else{
     bool imports=name=="ImportSearchBox";var source=(imports?CurrentImportRows():all).ToArray();var criteria=(imports?importFilters:libraryFilters).Snapshot();bool skip=SkipFlagged;
     string target=Targets.SelectedItem is TargetSummary?((TargetSummary)Targets.SelectedItem).Name:"All targets";string mode=Convert.ToString(C("LibraryViewBox").SelectedItem);
     var sorts=SearchSorts(imports?"ImportGrid":"FramesGrid");var culture=G(imports?"ImportGrid":"FramesGrid").Items.Culture??CultureInfo.CurrentCulture;
     var index=state.Captures??(state.Captures=new SearchIndex<Frame>(SearchDocument.FromFrame));
     work=token=>{var query=FileSearch.Parse(text);var rows=index.Find(source,query,criteria.Matches,token);token.ThrowIfCancellationRequested();
      if(imports){var summary=ImportWorkflow.Summarize(source,rows,skip);int retry=ImportWorkflow.Select(rows,skip,true).Count;return new SearchResult{Query=query,Source=source.ToList(),Frames=SearchOrdering.Order(rows,sorts,culture,token),ImportSummary=summary,Retry=retry};}
      var targets=TargetNavigation.Build(rows,token);string active=targets.Any(t=>t.Name==target)?target:"All targets";if(active!="All targets")rows=rows.Where(f=>f.Target==active).ToList();
      // Preserve the established target/session order as a stable tie-breaker.
      rows=rows.Select(f=>new{Frame=f,Date=CaptureSessions.Date(f)}).OrderBy(f=>f.Frame.Target).ThenBy(f=>f.Date==null?DateTime.MaxValue:f.Date.Date).ThenBy(f=>f.Frame.SessionKey).ThenBy(f=>f.Frame.Observed).ThenBy(f=>f.Frame.OriginalName).Select(f=>f.Frame).ToList();
      rows=SearchOrdering.Order(rows,sorts,culture,token);var sessions=mode=="Session summaries"?SubframeSessions.Build(rows,token):null;token.ThrowIfCancellationRequested();
      return new SearchResult{Query=query,Frames=rows,Targets=targets,Target=active,Sessions=sessions,Summary=CaptureGroups.Summarize(rows,token)};};
    }
    SearchResult result=await state.Worker.Submit(work);
    if(state.Disposed||state.Version!=version||Window.Dispatcher.HasShutdownStarted)return;
    state.Pending=false;state.Blocked=false;RestoreSearchControls(state);ShowSearchError(name,result.Query);
    if(name=="SearchBox"){
     var view=new ListCollectionView(result.Targets);view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));updating=true;try{Targets.ItemsSource=view;Targets.SelectedItem=result.Targets.First(t=>t.Name==result.Target);}finally{updating=false;}
     displayed=result.Frames;DisplayLibrary(result.Sessions,result.Summary,true);((FrameworkElement)Window.FindName("EmptyState")).Visibility=displayed.Count==0?Visibility.Visible:Visibility.Collapsed;
     B("LibraryFiltersButton").Content="Filters"+(libraryFilters.ActiveCount>0?" ("+libraryFilters.ActiveCount+")":"");if(cancel==null)L("StatusLabel").Text=repo==null?"Choose a repository folder in Settings to begin.":displayed.Count+" visible files  ·  "+all.Count+" in repository";
    }else if(name=="ImportSearchBox")ApplyImportRows(result.Source,result.Frames,result.ImportSummary,result.Retry,true);
    else ApplyEditedSearch(result);
    UpdateNavigationState();
   }catch(OperationCanceledException){}catch(Exception error){
    if(state.Disposed||state.Version!=version||Window.Dispatcher.HasShutdownStarted)return;
    state.Pending=false;state.Blocked=true;string message="Search could not finish: "+error.Message;L(name=="SearchBox"?"LibrarySummaryLabel":name=="ImportSearchBox"?"ImportSummaryLabel":"EditedSummary").Text=message;UiHelp.Tip(T(name),message+". Change or clear the search to retry.");
   }
  }
  void ApplyEditedSearch(SearchResult result){
   var selected=ActiveEditedImage;var view=new ListCollectionView(result.Targets);view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));refreshingEditedTargets=true;
   try{EditedTargets.ItemsSource=view;EditedTargets.SelectedItem=result.Targets.First(t=>t.Name==result.Target);}finally{refreshingEditedTargets=false;}
   SetRows("EditedGrid",result.Images,true);G("EditedGrid").SelectedItem=result.Images.FirstOrDefault(i=>selected!=null&&i.Project.Id==selected.Project.Id&&i.RelativePath==selected.RelativePath);
   L("EditedSummary").Text=result.Images.Count+" images";L("EditedSearchHint").Visibility=result.Query.IsEmpty?Visibility.Visible:Visibility.Collapsed;L("EditedEmptyState").Visibility=result.Images.Count==0?Visibility.Visible:Visibility.Collapsed;UpdateEditedActions();
  }
  void DisposeSearch(){foreach(var state in searchPanels.Values)state.Dispose();}
 }
}
