using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
namespace AstroArchive { public partial class MainUi {
  void SmokeFilters(){
   libraryFilters.Values["Camera"]="Telephoto";libraryFilters.Values["Session"]=all[0].SessionKey;Filter(true);if(displayed.Count!=1)throw new Exception("Library acquisition-session filters did not combine.");
   libraryFilters.Ranges["Exposure"]=new CaptureRange{Mode=NumericFilterMode.Between,Minimum=30,Maximum=60};
   GoToPage(0);PumpPopupLayout();if(!B("LibraryFiltersButton").IsVisible)throw new Exception("Repository Filters button is hidden.");B("LibraryFiltersButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();if(filtersPopup==null||!filtersPopup.IsOpen)throw new Exception("Compact filters panel did not open.");
   var body=(FrameworkElement)filtersPopup.Child;if(!MenuScrolling.GetEnabled(PopupChildren<ScrollViewer>(body).First()))throw new Exception("Filter panel lacks proportional scrolling.");var minimum=PopupChildren<Slider>(body).First(slider=>AutomationProperties.GetName(slider)=="Exposure minimum");minimum.Value=minimum.Maximum;
   if(libraryFilters.Ranges["Exposure"].Minimum!=60||displayed.Count!=1)throw new Exception("Range slider did not filter the displayed session.");
   PopupChildren<Button>(body).Single(button=>Convert.ToString(button.Content)=="Clear filters").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();
   if(libraryFilters.ActiveCount!=0||displayed.Count!=3)throw new Exception("Panel reset retained categories or ranges.");filtersPopup.IsOpen=false;
   B("ClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   foreach(string removed in new[]{"MakeFilter","CloudSourceCheck","RetuneCheck"})if(Window.FindName(removed)!=null)throw new Exception("Redundant control remains: "+removed);
   plan=new ImportPlan{Source="D:\\Telescope",Frames=all.Select(f=>f.Clone()).ToList()};
   for(int i=0;i<plan.Frames.Count;i++)plan.Frames[i].SourcePath="D:\\Telescope\\"+plan.Frames[i].OriginalName;
   plan.Frames[0].Status="New";plan.Frames[1].Status="Deleted";plan.Frames[2].Status="Failed";plan.Frames[2].Rejected=true;
   importFilters.Values["Status"]="New";FilterImports();if(visibleImports.Count!=1||Convert.ToString(B("ImportButton").Content)!="Import 1 file")throw new Exception("Import button does not follow filtered results.");
   importFilters.Reset();importFilters.Values["Review"]="Needs review";FilterImports();if(visibleImports.Single().Target!="NGC6888")throw new Exception("Import review filter missed a failure.");
   B("ImportClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));T("ImportSearchBox").Text="M45";WaitForSearches();if(visibleImports.Count!=2)throw new Exception("Import search failed.");
   B("ImportClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   GoToPage(1);PumpPopupLayout();if(!B("ImportFiltersButton").IsVisible)throw new Exception("Import Filters button is hidden.");B("ImportFiltersButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();if(filtersPopup==null||!filtersPopup.IsOpen||filtersPopup.PlacementTarget!=B("ImportFiltersButton"))throw new Exception("Import Filters button did not open its panel.");filtersPopup.IsOpen=false;GoToPage(0);
   BeginLive(false);cancel=new CancellationTokenSource();importFilters.Values["Status"]="New";var live=plan.Frames[0].Clone();live.Status="Failed";LiveFrame(live);LiveTick(true);if(visibleImports.Count!=0||B("ImportButton").IsEnabled)throw new Exception("Live status changes escaped import filters.");cancel.Dispose();cancel=null;importLive=false;latestProgress=null;importFilters.Reset();FilterImports();
  }
}}
