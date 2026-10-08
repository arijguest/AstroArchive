using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  string unknownImportTarget="";
  ComboBox ImportTargetChoice(FormWindow dialog,string value){
   dialog.Text("Target for Unknown light/stack captures",true);
   var choice=new ComboBox{IsEditable=true,IsTextSearchEnabled=false,Text=value,Margin=new Thickness(0,6,0,8)};
   choice.DropDownOpened+=(s,e)=>{string text=choice.Text;choice.ItemsSource=Catalog.Search(text).Select(o=>Catalog.Label(o.Name)).ToList();choice.Text=text;};
   UiHelp.Tip(choice,"Enter an object ID or name. Blank keeps Unknown.");dialog.Add(choice);return choice;
  }
  static bool ValidImportTarget(string value,TextBlock error,bool optional){
   error.Text="";if(optional&&string.IsNullOrWhiteSpace(value))return true;
   try{ImportPolicy.Target(value);return true;}catch(ArgumentException e){error.Text=e.Message;return false;}
  }
  List<Frame> UnknownImportSelection(){var selected=G("ImportGrid").SelectedItems.Cast<Frame>().ToList();return (selected.Count>0?selected:visibleImports).Where(ImportPolicy.UnknownScience).ToList();}
  void AssignUnknownImportTargets(){
   var rows=UnknownImportSelection();if(cancel!=null||rows.Count==0)return;
   var dialog=new FormWindow(Window,"Assign Unknown targets",620,410);dialog.Text("Assign "+rows.Count+" Unknown capture"+(rows.Count==1?"":"s"),true);
   dialog.Text(G("ImportGrid").SelectedItems.Count>0?"Apply to selected Unknown lights and stacks. Known targets and calibrations keep their labels.":"Apply to the visible Unknown lights and stacks. Filter or select files first when they contain different targets.");
   var target=ImportTargetChoice(dialog,"");var error=new TextBlock{TextWrapping=TextWrapping.Wrap};dialog.Add(error);
   dialog.Accept("Assign target",()=>ValidImportTarget(target.Text,error,false));if(!dialog.Show())return;
   int count=ImportPolicy.AssignUnknown(rows,target.Text);FilterImports();UpdateNavigationState();L("StatusLabel").Text=count+" Unknown capture targets assigned for import.";
  }
  CheckBox ImportMatchingChoice(FormWindow dialog){
   var choice=dialog.Check("Robust file matching (slower)",settings.RobustImportMatching);
   UiHelp.Tip(choice,"Check file/metadata changes and missing copies; off skips known names/DWARF sessions.");return choice;
  }
  void AddImportPolicyControls(FormWindow dialog,out CheckBox flagged,out CheckBox failed,out CheckBox raster,out CheckBox originals){
   flagged=dialog.Check("Skip flagged captures",SkipFlagged);
   failed=dialog.Check("Ignore failed filenames",settings.IgnoreFailed);
   raster=dialog.Check("Ignore non-raw files (PNG/JPG/JPEG)",settings.IgnoreRasterImports);
   UiHelp.Tip(failed,"Skip filenames containing “failed”; keep originals.");
   UiHelp.Tip(raster,"Skip PNG/JPG/JPEG files; keep originals.");
   originals=dialog.Check("Delete originals after verified import",((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked==true);
   originals.IsEnabled=((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsEnabled;
  }
 }
}
