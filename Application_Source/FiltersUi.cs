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
   RoutedEventHandler changed=(s,e)=>{if(cancel!=null||settings.IgnoreFailed==(ignore.IsChecked==true))return;settings.IgnoreFailed=ignore.IsChecked==true;SaveSettings();InvalidateImportPlan();L("StatusLabel").Text="Import option saved. Scan the source folder again to apply it.";};
   ignore.Checked+=changed;ignore.Unchecked+=changed;
  }
  void InitializeFilters(){
   B("LibraryFiltersButton").Click+=(s,e)=>ShowFilters(false);
   B("ImportFiltersButton").Click+=(s,e)=>ShowFilters(true);
   T("ImportSearchBox").TextChanged+=(s,e)=>{Watermark("ImportSearchBox","Search import results...");FilterImports();};Watermark("ImportSearchBox","Search import results...");
   B("ImportClearButton").Click+=(s,e)=>{importFilters.Values.Clear();T("ImportSearchBox").Text="";FilterImports();};
   B("ScreenImportsButton").Click+=(s,e)=>ScreenFiles(true);
   B("ScreenLibraryButton").Click+=(s,e)=>ScreenFiles(false);
   G("ImportGrid").SelectionChanged+=(s,e)=>{
    var frame=G("ImportGrid").SelectedItem as Frame;
    L("ImportDetailsLabel").Text=frame==null?"Select a capture to see screening or import details.":frame.OriginalName+"  ·  "+frame.Status+"  ·  "+(string.IsNullOrEmpty(frame.ScreeningIssue)?frame.SourceDisposition??frame.Notes:frame.ScreeningIssue);
   };
  }
  void ShowFilters(bool imports){
   var criteria=imports?importFilters:libraryFilters;
   var rows=imports?(G("ImportGrid").ItemsSource==null?new List<Frame>():CurrentImportRows()):all;
   var menu=ThemedMenu();
   foreach(string field in CaptureFilters.Fields){
    string selected;criteria.Values.TryGetValue(field,out selected);
    var group=new MenuItem{Header=field+(selected==null?"":"  ·  "+selected)};
    AddFilterChoice(group,"All",selected==null,()=>{criteria.Values.Remove(field);ApplyFilters(imports);});
    var values=rows.Select(f=>CaptureFilters.Value(f,field)).Distinct().OrderBy(v=>v).ToList();
    if(field=="Review")values=new List<string>{"Needs review","No issues flagged","Rejected / reference","Passed","Not screened"};
    if(selected!=null&&!values.Contains(selected))values.Add(selected);
    foreach(string value in values){string choice=value;AddFilterChoice(group,choice,selected==choice,()=>{criteria.Values[field]=choice;ApplyFilters(imports);});}
    menu.Items.Add(group);
   }
   menu.Items.Add(new Separator());
   var clear=new MenuItem{Header="Reset filters",IsEnabled=criteria.Values.Count>0};clear.Click+=(s,e)=>{criteria.Values.Clear();ApplyFilters(imports);};menu.Items.Add(clear);
   menu.PlacementTarget=B(imports?"ImportFiltersButton":"LibraryFiltersButton");menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;
  }
  static void AddFilterChoice(MenuItem parent,string label,bool selected,Action action){var item=new MenuItem{Header=label,IsCheckable=true,IsChecked=selected};item.Click+=(s,e)=>action();parent.Items.Add(item);}
  void ApplyFilters(bool imports){if(imports)FilterImports();else Filter(true);}
  List<Frame> CurrentImportRows(){return importLive?importRows.ToList():plan==null?new List<Frame>():plan.Frames;}
  void FilterImports(){
   if(updating)return;var source=CurrentImportRows();visibleImports=importFilters.Apply(source,T("ImportSearchBox").Text);
   var grid=G("ImportGrid");var selection=new HashSet<string>(grid.SelectedItems.Cast<Frame>().Select(f=>f.SourcePath));SetRows("ImportGrid",visibleImports);foreach(var frame in visibleImports.Where(f=>selection.Contains(f.SourcePath)))if(!grid.SelectedItems.Contains(frame))grid.SelectedItems.Add(frame);
   B("ImportFiltersButton").Content="Filters"+(importFilters.Values.Count>0?" ("+importFilters.Values.Count+")":"")+" ▾";
   int ready=visibleImports.Count(CaptureScreening.Importable);
   B("ImportButton").Content="Import "+ready+" file"+(ready==1?"":"s");B("ImportButton").ToolTip="Imports the ready files in this filtered view.";
   B("ImportButton").IsEnabled=cancel==null&&repo!=null&&plan!=null&&ready>0;
   B("ScreenImportsButton").IsEnabled=cancel==null&&repo!=null&&visibleImports.Any(f=>f.Status!="Deleted");
   L("ScanLabel").Text=source.Count==0&&plan==null?"Choose a source folder and scan to begin.":visibleImports.Count+" / "+source.Count+" shown  ·  "+ready+" ready  ·  "+source.Count(CaptureScreening.NeedsReview)+" need review  ·  "+source.Count(f=>f.Status=="Deleted")+" previously deleted"+(plan!=null&&plan.IgnoredFailed>0?"  ·  "+plan.IgnoredFailed+" failed filenames ignored":"");
  }
  void ScreenFiles(bool imports){
   if(repo==null||cancel!=null)return;
   var rows=imports?visibleImports.Where(f=>f.Status!="Deleted").ToList():Context();if(rows.Count==0)return;
   ScreeningResult result=null;
   Run(ct=>{result=repo.Screen(rows,imports,ct,Progress);return result.Checked+" screened; "+result.Problems+" need review.";},message=>{
    if(imports)FilterImports();else Filter(true);L("StatusLabel").Text=message;
    if(result.Problems>0)ShowReport("Telescope capture screening",string.Join("\r\n\r\n",result.Errors));
   });
  }
  void ShowImportTools(){
   var menu=ThemedMenu();var selection=G("ImportGrid").SelectedItems.Cast<Frame>().Where(f=>f.Status!="Deleted").ToList();
   menu.Items.Add(FileAction("Edit selected metadata…",()=>Edit(true),selection.Count>0));
   menu.Items.Add(FileAction("Identify selected targets…",()=>Identify(true),selection.Count>0));
   menu.Items.Add(new Separator());menu.Items.Add(FileAction("Scan report…",()=>ShowReport("Scan report",plan==null?"Scan a folder first.":plan.Errors.Count==0?"All supported files were read successfully.":string.Join("\r\n\r\n",plan.Errors))));
   menu.PlacementTarget=B("ImportToolsButton");menu.Placement=PlacementMode.Top;menu.IsOpen=true;
  }
 }
}
