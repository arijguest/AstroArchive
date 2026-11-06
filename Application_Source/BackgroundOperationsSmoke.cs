// Hold a real operation open while exercising native navigation and search.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  static Frame BackgroundCapture(Repository repository,string filename,string target,double value){
   var frame=new Frame{Target=target,Kind="Light",Telescope="Fixture",Model="Other",Make="Other",Camera="Telephoto",RelativePath=filename,OriginalName=filename,Night="2026-10-01",Session=filename,Exposure=10,Filter="Broadband",Calibration="Uncalibrated",Width=16,Height=16,Channels=1,Format="FITS",LinearData=true};
   string path=repository.FilePath(frame);ScientificFits.Write(path,new PixelImage{Width=16,Height=16,Channels=1,Pixels=Enumerable.Repeat(value,256).ToArray()},frame,CancellationToken.None);frame.Hash=Util.Hash(path,CancellationToken.None);frame.Bytes=new FileInfo(path).Length;frame.RepositoryStamp=FileStamp.Read(path);frame.Images=Fits.Inspect(path).Images;frame.ImageKey=frame.Images[0].Key;repository.Save(frame);return frame;
  }
  public void SmokeBackgroundOperations(string output){
   Directory.CreateDirectory(output);if(!Window.IsVisible)Window.Show();PumpPopupLayout();WaitForSearches();
   var original=repo;var originalRows=all;var originalPlan=plan;string query=T("SearchBox").Text,viewMode=Convert.ToString(C("LibraryViewBox").SelectedItem);int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   string folder=Path.GetFullPath(Path.Combine(output,"background-fixture-"+Guid.NewGuid().ToString("N")));Repository fixture=null;int pulses=0;
   var timer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(15)};timer.Tick+=(s,e)=>pulses++;
   using(var started=new ManualResetEventSlim())using(var finish=new ManualResetEventSlim()){
    ActivityEntry entry=null;
    try{
     fixture=new Repository(folder);repo=fixture;plan=null;
     var first=BackgroundCapture(fixture,"one.fit","M31",0.2);var second=BackgroundCapture(fixture,"two.fit","M45",0.3);string oldPath=fixture.FilePath(first),secondPath=fixture.FilePath(second);
     Refresh();T("SearchBox").Clear();WaitForSearches();timer.Start();bool completed=false;
     Run(token=>{started.Set();finish.Wait(token);var patch=new MetadataPatch(new Dictionary<string,string>{{"Notes","Saved by background fixture"},{"Target","M33"}});fixture.Refile(patch.Apply(fixture.Find(first.Hash)),token);fixture.Checkpoint(token);return "Metadata saved.";},message=>completed=true,"Background metadata fixture");
     SmokeSearchWait(()=>started.IsSet);entry=currentActivity;
     foreach(string name in new[]{"ImportMenu","ExportMenu","RepositoryMenu","GuideMenu"}){OpenTopMenu(name);PumpPopupLayout();if(!TopMenu(name).IsEnabled||!TopMenu(name).IsSubmenuOpen)throw new Exception("Background operation blocked menu "+name);TopMenu(name).IsSubmenuOpen=false;}
     if(!B("LibraryFiltersButton").IsEnabled||!B("ImportFiltersButton").IsEnabled||!B("EditedFiltersButton").IsEnabled)throw new Exception("Background work disabled filters.");
     GoToPage(0);if(!C("LibraryViewBox").IsEnabled)throw new Exception("Background work disabled session browsing.");C("LibraryViewBox").SelectedItem="Session summaries";
     ShowFilters(false);PumpPopupLayout();if(filtersPopup==null||!filtersPopup.IsOpen)throw new Exception("Filters did not open during background work.");filtersPopup.IsOpen=false;
     foreach(int targetPage in new[]{1,2,0}){GoToPage(targetPage);if(((TabControl)Window.FindName("MainTabs")).SelectedIndex!=targetPage)throw new Exception("Background work blocked page switching.");}
     T("SearchBox").Text="M45";WaitForSearches();if(displayed.Count!=1||displayed[0].Target!="M45"||!G("FramesGrid").IsEnabled)throw new Exception("Background work blocked search browsing.");
     bool concurrent=false;Run(token=>{concurrent=true;return "Unexpected";},null,"Conflicting worker");
     if(concurrent||B("EditButton").IsEnabled||B("ImportButton").IsEnabled||B("EditedAddButton").IsEnabled||B("ImportExportButton").IsEnabled)throw new Exception("Conflicting mutations or an unavailable direct export became available.");
     bool rejected=false;try{OpenRepository(Path.Combine(folder,"other"));}catch(InvalidOperationException){rejected=true;}if(!rejected||repo!=fixture)throw new Exception("Repository switched while work was active.");
     T("SearchBox").Clear();WaitForSearches();
     PreviewImage(first);
     SmokeSearchWait(()=>comparisonWindow!=null&&PopupChildren<TextBlock>(comparisonWindow).Any(t=>t.Text.EndsWith("Sampled preview",StringComparison.Ordinal)));
     var preview=comparisonWindow;
     var native=PopupChildren<CheckBox>(preview).Single(c=>Convert.ToString(c.Content)=="Native detail");
     native.IsChecked=true;
     SmokeSearchWait(()=>PopupChildren<TextBlock>(preview).Any(t=>t.Text.EndsWith("Native detail",StringComparison.Ordinal)));
     var adjacent=PopupChildren<Button>(preview).Single(b=>b.IsEnabled&&(Convert.ToString(b.Content)=="Next"||Convert.ToString(b.Content)=="Previous"));
     adjacent.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
     SmokeSearchWait(()=>preview.Title=="Preview - two.fit"&&PopupChildren<TextBlock>(preview).Any(t=>t.Text.EndsWith("Sampled preview",StringComparison.Ordinal)));
     preview.Close();if(currentActivity!=entry||cancel==null)throw new Exception("Read-only preview replaced the running operation.");
     SmokeSearchWait(()=>pulses>=3);Capture(Path.Combine(output,"AstroArchive_Background_Browsing.png"));finish.Set();SmokeSearchWait(()=>cancel==null);WaitForSearches();
     var updated=all.Single(f=>f.Hash==first.Hash);if(!completed||entry==null||entry.Running||entry.Failed||updated.Notes!="Saved by background fixture"||updated.Target!="M33"||File.Exists(oldPath)||Util.Hash(fixture.FilePath(updated),CancellationToken.None)!=first.Hash)throw new Exception("Metadata worker completion lost file bytes or did not apply its refreshed snapshot.");
     activities.Remove(entry);started.Reset();finish.Reset();bool deletionCompleted=false;T("SearchBox").Clear();WaitForSearches();
     Run(token=>{started.Set();finish.Wait(token);using(var linked=CancellationTokenSource.CreateLinkedTokenSource(token)){fixture.DeleteFrames(new[]{second,updated},linked.Token,p=>{Progress(p);if(p.Done==1)linked.Cancel();});}return "Unexpected full deletion";},message=>deletionCompleted=true,"Background deletion fixture");
     SmokeSearchWait(()=>started.IsSet);entry=currentActivity;GoToPage(1);OpenTopMenu("RepositoryMenu");PumpPopupLayout();if(!TopMenu("RepositoryMenu").IsSubmenuOpen||!T("ImportSearchBox").IsEnabled)throw new Exception("Deletion blocked browsing.");TopMenu("RepositoryMenu").IsSubmenuOpen=false;GoToPage(0);T("SearchBox").Text="M33";WaitForSearches();if(displayed.Count!=1||!G("FramesGrid").IsEnabled)throw new Exception("Deletion blocked search.");finish.Set();SmokeSearchWait(()=>cancel==null);WaitForSearches();
     if(deletionCompleted||entry==null||!entry.Canceled||entry.Failed||File.Exists(secondPath)||all.Any(f=>f.Hash==second.Hash)||!fixture.Deletions().Any(d=>d.Hash==second.Hash)||all.Count!=1||Util.Hash(fixture.FilePath(all[0]),CancellationToken.None)!=first.Hash)throw new Exception("Canceled deletion lost an unprocessed file or failed to refresh completed deletions.");
     File.WriteAllText(Path.Combine(output,"background-operations-smoke.txt"),"PASS: real FITS metadata refile and canceled batch deletion retain checksums and completed audit history; menus, full preview, page/session/filter/search browsing stay responsive; conflicting writes and repository switching stay serialized; success and cancellation publish current snapshots.");
    }finally{
     finish.Set();if(cancel!=null){cancel.Cancel();SmokeSearchWait(()=>cancel==null);}CancelDetachedPreview();foreach(var popup in Window.OwnedWindows.Cast<Window>().Where(w=>w.Title=="Preview - one.fit").ToArray())popup.Close();timer.Stop();foreach(var top in TopMenus())top.IsSubmenuOpen=false;if(filtersPopup!=null)filtersPopup.IsOpen=false;
     if(entry!=null){activities.Remove(entry);RenderActivity();}repo=original;all=originalRows;plan=originalPlan;T("SearchBox").Text=query;C("LibraryViewBox").SelectedItem=viewMode;Refresh();GoToPage(page);WaitForSearches();SetBusy(false);if(fixture!=null)fixture.Dispose();
    }
   }
  }
 }
}
