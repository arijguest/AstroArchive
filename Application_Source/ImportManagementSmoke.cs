using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  public void SmokeImportManagement(string output){
   Directory.CreateDirectory(output);if(!Window.IsVisible)Window.Show();PumpPopupLayout();var previous=plan;var previousRows=all;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   try{
    var one=new Frame{SourcePath="C:\\fixtures\\one.fit",OriginalName="one.fit",Status="New",Kind="Light",Exposure=10,Session="one",Target="M45"};var two=one.Clone();two.SourcePath="C:\\fixtures\\two.fit";two.OriginalName="two.fit";
    plan=new ImportPlan{Source="C:\\fixtures",Frames=new List<Frame>{one,two}};GoToPage(1);FilterImports();G("ImportGrid").SelectedItems.Clear();
    if(SelectedImportRows().Count!=0)throw new Exception("Empty import selection fell back to all rows.");
    var empty=BuildImportTools();if(empty.Items.OfType<MenuItem>().Single(i=>Convert.ToString(i.Header)=="Edit selected metadata…").IsEnabled)throw new Exception("Metadata edit enabled without an import selection.");
    G("ImportGrid").SelectedItems.Add(one);G("ImportGrid").SelectedItems.Add(two);var menu=ThemedMenu();BuildImportContextMenu(menu);
    foreach(string action in new[]{"Edit selected metadata…","Remove selected from import list…","Stack short exposures"})if(!menu.Items.OfType<MenuItem>().Any(i=>Convert.ToString(i.Header)==action))throw new Exception("Import context action missing: "+action);
    if(SelectedImportRows().Count!=2||menu.Items.OfType<MenuItem>().Single(i=>Convert.ToString(i.Header)=="Delete selected archived copies…").IsEnabled)throw new Exception("Import selection or pending-source deletion scope incorrect.");
    SelectImportContextRow(one);if(SelectedImportRows().Count!=2)throw new Exception("Right-clicking a selected import discarded the batch selection.");
    G("ImportGrid").SelectedItems.Clear();G("ImportGrid").SelectedItems.Add(one);SelectImportContextRow(two);if(SelectedImportRows().Count!=1||SelectedImportRows()[0]!=two)throw new Exception("Right-clicking an unselected import did not select its row.");
    G("ImportGrid").SelectedItems.Add(one);
    var stack=ImportStackingMenu();if(stack.Items.Count!=2||Convert.ToString(((MenuItem)stack.Items[1]).Header)!="All imports in this list…")throw new Exception("Explicit selected/all import stacking scopes missing.");
    if(ShortSub(new Frame{Kind="Dark",Exposure=10})||ShortSub(new Frame{Kind="Stack",Exposure=10})||ShortSub(new Frame{Kind="Light",Exposure=11})||!ShortSub(one))throw new Exception("Short sub UI eligibility includes calibrations or long exposures.");
    OpenTopMenu("ImportMenu");PumpPopupLayout();Capture(Path.Combine(output,"AstroArchive_Import_Management.png"));TopMenu("ImportMenu").IsSubmenuOpen=false;
    File.WriteAllText(Path.Combine(output,"import-management-smoke.txt"),"PASS: explicit import selection, metadata/remove/context actions, archived-only physical deletion, selected/all session stacking scopes, and short-light eligibility.");
   }finally{plan=previous;all=previousRows;FilterImports();GoToPage(page);}
  }
 }
}
