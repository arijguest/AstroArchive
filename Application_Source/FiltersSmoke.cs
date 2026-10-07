using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive { public partial class MainUi {
  void SmokeFilters(){
   libraryFilters.Values["Camera"]="Telephoto";libraryFilters.Values["Night"]="2026-10-06";Filter(true);if(displayed.Count!=2)throw new Exception("Library dropdown filters did not combine.");
   B("ClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   foreach(string removed in new[]{"MakeFilter","CloudSourceCheck","RetuneCheck"})if(Window.FindName(removed)!=null)throw new Exception("Redundant control remains: "+removed);
   plan=new ImportPlan{Source="D:\\Telescope",Frames=all.Select(f=>f.Clone()).ToList()};
   for(int i=0;i<plan.Frames.Count;i++)plan.Frames[i].SourcePath="D:\\Telescope\\"+plan.Frames[i].OriginalName;
   plan.Frames[0].Status="New";plan.Frames[1].Status="Deleted";plan.Frames[2].Status="Failed";plan.Frames[2].Rejected=true;
   importFilters.Values["Status"]="New";FilterImports();if(visibleImports.Count!=1||Convert.ToString(B("ImportButton").Content)!="Import 1 file")throw new Exception("Import button does not follow filtered results.");
   importFilters.Values.Clear();importFilters.Values["Review"]="Needs review";FilterImports();if(visibleImports.Single().Target!="NGC6888")throw new Exception("Import review filter missed a failure.");
   B("ImportClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));T("ImportSearchBox").Text="M45";if(visibleImports.Count!=2)throw new Exception("Import search failed.");
   B("ImportClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   BeginLive(false);cancel=new CancellationTokenSource();importFilters.Values["Status"]="New";var live=plan.Frames[0].Clone();live.Status="Failed";LiveFrame(live);LiveTick(true);if(visibleImports.Count!=0||B("ImportButton").IsEnabled)throw new Exception("Live status changes escaped import filters.");cancel.Dispose();cancel=null;importLive=false;latestProgress=null;importFilters.Values.Clear();FilterImports();
  }
}}
