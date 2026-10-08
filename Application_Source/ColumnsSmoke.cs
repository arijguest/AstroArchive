// Windows smoke validates bindings, native reordering and independent layouts.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeColumnLayouts(){
   var saved=Util.Serialize(settings.TableLayouts);testingColumnLayouts=true;
   try{
    foreach(string name in new[]{"FramesGrid","ImportGrid"}){
     var grid=G(name);if(!grid.CanUserReorderColumns||!CurrentColumnLayout(name).Visible.SequenceEqual(defaultColumns[name]))throw new Exception("Default headings changed or native reordering is disabled.");
     var exposure=grid.Columns.First(c=>ColumnId(c)=="ExposureText");var gain=grid.Columns.First(c=>ColumnId(c)=="GainText");
     var mount=grid.Columns.First(c=>ColumnId(c)=="Mount");var oldMountLayout=new ColumnLayout{Visible=new System.Collections.Generic.List<string>{"Mount"},Order=new System.Collections.Generic.List<string>{"Mount"}};ApplyColumnLayout(name,oldMountLayout);if(mount.Visibility!=Visibility.Visible||mount.DisplayIndex!=0)throw new Exception("Saved Mount heading was lost after the display binding changed.");ApplyColumnLayout(name,null);
     var menu=BuildColumnsMenu(name,null);var more=menu.Items.OfType<MenuItem>().Single(m=>Convert.ToString(m.Header)=="More headings");
     var choice=menu.Items.OfType<MenuItem>().Concat(more.Items.OfType<MenuItem>()).Single(m=>m.Tag==gain);choice.IsChecked=true;choice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
     if(gain.Visibility!=Visibility.Visible||gain.SortMemberPath!="Gain")throw new Exception("Extra column chooser lost numeric binding.");
     gain.DisplayIndex=0;typeof(DataGrid).GetMethod("OnColumnReordered",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(grid,new object[]{new DataGridColumnEventArgs(gain)});
     var layout=Util.Deserialize<ColumnLayout>(Util.Serialize(SavedColumnLayout(name)));if(layout.Order.First()!="GainText"||!layout.Visible.Contains("GainText"))throw new Exception("Native drag layout was not saved.");
     ApplyColumnLayout(name,null);ApplyColumnLayout(name,layout);if(gain.DisplayIndex!=0||gain.Visibility!=Visibility.Visible)throw new Exception("Saved headings did not restore.");
     MoveColumn(name,gain,1);if(CurrentColumnLayout(name).Visible[1]!="GainText")throw new Exception("Accessible heading movement failed.");
     SortTable(name,exposure,false);SetColumnVisible(name,exposure,false);if(tableSorts[name].Any(s=>s.PropertyName=="Exposure"))throw new Exception("Hidden column still controls sorting.");
     foreach(var column in grid.Columns.Where(c=>c.Visibility==Visibility.Visible&&c!=gain).ToList())SetColumnVisible(name,column,false);
     if(SetColumnVisible(name,gain,false)||CurrentColumnLayout(name).Visible.Count!=1)throw new Exception("Chooser permits an empty table.");
     if(name=="FramesGrid"&&SavedColumnLayout("ImportGrid")!=null)throw new Exception("Library layout changed import headings.");
     ResetColumns(name);if(!CurrentColumnLayout(name).Visible.SequenceEqual(defaultColumns[name])||SavedColumnLayout(name)!=null)throw new Exception("Restore defaults did not restore the original layout.");
    }
   }finally{settings.TableLayouts=Util.Deserialize<System.Collections.Generic.Dictionary<string,ColumnLayout>>(saved);foreach(string name in new[]{"FramesGrid","ImportGrid"})ApplyColumnLayout(name,SavedColumnLayout(name));testingColumnLayouts=false;}
  }
 }
}
