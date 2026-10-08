// Native dispatcher checks: typing returns immediately while background work waits.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AstroArchive {
 public partial class MainUi {
  void WaitForSearches(){
   SmokeSearchWait(()=>searchPanels.Values.All(s=>!s.Pending));
   foreach(var entry in searchPanels.Where(p=>p.Value.Blocked))throw new Exception("Search failed for "+entry.Key+": "+Convert.ToString(T(entry.Key).ToolTip));
  }
  static void SmokeSearchWait(Func<bool> done){
   if(done())return;var frame=new DispatcherFrame();var clock=Stopwatch.StartNew();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(10)};
   timer.Tick+=(s,e)=>{if(done()||clock.Elapsed.TotalSeconds>=20)frame.Continue=false;};timer.Start();try{Dispatcher.PushFrame(frame);}finally{timer.Stop();}if(!done())throw new Exception("Background search did not settle within 20 seconds.");
  }
  void SmokeSearchPipeline(string output){
   WaitForSearches();var savedRows=all;var savedPlan=plan;var savedEdited=editedImages;var savedRepo=repo;string search=T("SearchBox").Text,imports=T("ImportSearchBox").Text,edited=T("EditedSearchBox").Text;
   int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;var savedSorts=tableSorts["FramesGrid"].ToList();var savedFilters=libraryFilters.Snapshot();int pulses=0;
   var heartbeat=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(10)};heartbeat.Tick+=(s,e)=>pulses++;
   try{
    all=Enumerable.Range(0,6000).Select(i=>new Frame{Hash=i.ToString("x64"),Target=i%3==0?"M45":i%3==1?"M31":"NGC6888",Kind="Light",Status="New",SourcePath="fixture-"+i,OriginalName="Light_"+i.ToString("D5")+".fit",Telescope="Unit-01",Camera="Telephoto",Session="session-"+i%6,Exposure=i%120,AcquisitionDate="2026-10-06",Filter="Broadband",Screened=true}).ToList();
    libraryFilters.Reset();GoToPage(0);T("SearchBox").Clear();Filter(true);WaitForSearches();var before=displayed;var state=SearchState("SearchBox");
    using(var started=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
     var barrier=state.Worker.Submit(token=>{started.Set();if(!release.Wait(5000))throw new Exception("Native worker barrier timed out.");return null;});SmokeSearchWait(()=>started.IsSet);
     heartbeat.Start();
     try{
      var clock=Stopwatch.StartNew();foreach(string text in new[]{"M","M4","target:M45","file:*00002.fit"})T("SearchBox").Text=text;
      if(!object.ReferenceEquals(displayed,before)||!state.Pending||G("FramesGrid").IsEnabled||B("ExportButton").IsEnabled)throw new Exception("Typing synchronously refreshed results or left stale file actions enabled.");
      ScheduleSearch("SearchBox",false,true);SmokeSearchWait(()=>pulses>=3);
      SetBusy(false); // A progress refresh must not save the temporarily disabled grid.
      if(!state.Pending||clock.Elapsed.TotalSeconds>=4)throw new Exception("Dispatcher blocked behind the held search worker.");
     }finally{release.Set();}
     WaitForSearches();if(!barrier.IsCanceled||displayed.Count!=1||displayed[0].OriginalName!="Light_00002.fit"||!G("FramesGrid").IsEnabled||!Targets.IsEnabled)throw new Exception("Superseded search replaced the latest query or left its view disabled.");
    }
    var index=state.Captures;T("SearchBox").Text="target:M45 exposure:>=60";WaitForSearches();if(!object.ReferenceEquals(index,state.Captures)||displayed.Count!=all.Count(f=>f.Target=="M45"&&f.Exposure>=60))throw new Exception("Repeated query lost cache reuse or filter semantics.");
    // Metadata updates must invalidate cached search text on the same row object.
    all[2].Filter="Ha";T("SearchBox").Text="filter:Ha";Filter(true);WaitForSearches();if(object.ReferenceEquals(index,state.Captures)||displayed.Count!=1||displayed[0]!=all[2])throw new Exception("Metadata correction retained stale search documents.");
    var exposure=G("FramesGrid").Columns.First(c=>c.SortMemberPath=="Exposure");SortTable("FramesGrid",exposure,false);SortTable("FramesGrid",exposure,false);
    T("SearchBox").Text="exposure:>=100";WaitForSearches();
    // A grouped view enumerates one session at a time, not a global exposure list.
    if(!displayed.Select(f=>f.Exposure).SequenceEqual(displayed.Select(f=>f.Exposure).OrderByDescending(v=>v))||subframeSessions.Any(g=>!g.Frames.Select(f=>f.Exposure).SequenceEqual(g.Frames.Select(f=>f.Exposure).OrderByDescending(v=>v))))throw new Exception("Prepared result lost numeric ordering within session groups.");
    if(exposure.SortDirection!=System.ComponentModel.ListSortDirection.Descending||TableSortIndicators.GetMark(exposure)!="▼")throw new Exception("Prepared sort heading differs: "+exposure.SortDirection+" / "+TableSortIndicators.GetMark(exposure));
    T("SearchBox").Text="\"unfinished";WaitForSearches();if(displayed.Count!=0)throw new Exception("Invalid query retained actionable previous rows.");
    B("ClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WaitForSearches();if(displayed.Count!=6000||!G("FramesGrid").IsEnabled)throw new Exception("Clear did not restore current repository rows.");
    if(repo==null)repo=new Repository(Path.Combine(output,"search-smoke-repository"));
    plan=new ImportPlan{Frames=all.ToList()};GoToPage(1);T("ImportSearchBox").Clear();FilterImports();WaitForSearches();T("ImportSearchBox").Text="file:*00002.fit";
    if(B("ImportButton").IsEnabled)throw new Exception("Import stayed enabled while its view was stale.");Import();if(cancel!=null)throw new Exception("Import started against stale search results.");WaitForSearches();if(visibleImports.Count!=1||!B("ImportButton").IsEnabled)throw new Exception("Import result/availability did not recover.");
    T("ImportSearchBox").Text="target:M45";plan=new ImportPlan{Frames=new List<Frame>{all[2]}};FilterImports();WaitForSearches();if(visibleImports.Count!=0)throw new Exception("Replaced import plan received results from an old source.");
    var project=new EditedProject{Id=Guid.NewGuid().ToString("N"),Name="Search fixture"};editedImages=all.Select((f,i)=>new EditedImage{Project=project,Filename="edit_"+i.ToString("D5")+".png",RelativePath="fixture/edit_"+i+".png",Kind="Edited image",Metadata=new EditedMetadata{Object=f.Target,ImageClass=i%2==0?"Starless":"Stars only",Filters="Ha"}}).ToList();
    editedFocusProject=project.Id;editedFocusPath="fixture/edit_2.png";GoToPage(2);T("EditedSearchBox").Text="type:Starless file:*00002.png";WaitForSearches();if(G("EditedGrid").Items.Count!=1||ActiveEditedImage==null||ActiveEditedImage.RelativePath!="fixture/edit_2.png")throw new Exception("Edited background search or new-import focus differs from shared syntax.");
    var editedIndex=SearchState("EditedSearchBox").Edited;T("EditedSearchBox").Text="file:*00003.png";WaitForSearches();if(G("EditedGrid").Items.Count!=1||!object.ReferenceEquals(editedIndex,SearchState("EditedSearchBox").Edited))throw new Exception("Edited repeated query lost cache reuse.");
    File.WriteAllText(Path.Combine(output,"search-pipeline-smoke.txt"),"PASS: 6000-row repository/import/edited searches, rapid latest-only typing, responsive dispatcher while worker held, safe pending actions, document reuse/invalidation, numeric sort marks, invalid/clear results and replaced-source cancellation. Dispatcher pulses: "+pulses);
   }finally{
    heartbeat.Stop();foreach(string name in new[]{"SearchBox","ImportSearchBox","EditedSearchBox"})CancelSearch(name);
    if(repo!=savedRepo){repo.Dispose();repo=savedRepo;}all=savedRows;plan=savedPlan;editedImages=savedEdited;tableSorts["FramesGrid"]=savedSorts;libraryFilters.Reset();foreach(var entry in savedFilters.Values)libraryFilters.Values[entry.Key]=entry.Value;foreach(var entry in savedFilters.Ranges)libraryFilters.Ranges[entry.Key]=entry.Value;
    T("SearchBox").Text=search;T("ImportSearchBox").Text=imports;T("EditedSearchBox").Text=edited;Filter(true);FilterImports();FilterEditedImages();GoToPage(page);WaitForSearches();
   }
  }
 }
}
