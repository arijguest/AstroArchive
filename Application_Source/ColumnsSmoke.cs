// Windows smoke validates bindings, native reordering and independent layouts.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeColumnLayouts(){
   SmokeColumnResizing();
   foreach(string name in new[]{"FramesGrid","ImportGrid","EditedGrid"})foreach(var column in G(name).Columns){
    string id=ColumnId(column),expected=id=="ObjectId"||id=="Metadata.ObjectId"?"ID":id=="TargetName"||id=="Metadata.TargetName"?"OBJECT":id=="Kind"&&name!="EditedGrid"?"TYPE":id=="AcquisitionDateLabel"?"DATE":null;
    if(expected!=null&&Convert.ToString(column.Header)!=expected)throw new Exception("Concise table header missing: "+name+" / "+id);
   }
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
  void SmokeColumnResizing(){
   var dialog=new FormWindow(Window,"Column resizing fixture",760,480);
   var grid=new DataGrid{Height=220,ItemsSource=new[]{new Frame{OriginalName=new string('x',200),Target="M45",Kind="Light"}}};
   var file=new DataGridTextColumn{Header="LONG FILE CAPTION",Binding=new System.Windows.Data.Binding("OriginalName"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)};
   var target=new DataGridTextColumn{Header="TARGET",Binding=new System.Windows.Data.Binding("Target"),Width=180};var kind=new DataGridTextColumn{Header="TYPE",Binding=new System.Windows.Data.Binding("Kind"),Width=180};grid.Columns.Add(file);grid.Columns.Add(target);grid.Columns.Add(kind);dialog.Add(grid);dialog.CloseOnly();
   try{
    dialog.Window.Show();PumpPopupLayout();var header=PopupChildren<DataGridColumnHeader>(grid).Single(h=>h.Column==file);header.ApplyTemplate();var thumb=(Thumb)header.Template.FindName("PART_RightHeaderGripper",header);
    var columns=grid.Columns.ToList();var scroll=PopupChildren<ScrollViewer>(grid).First(s=>s.Name=="DG_ScrollViewer");
    var initial=columns.Select(c=>c.ActualWidth).ToArray();double windowWidth=dialog.Window.Width;
    if(file.ActualWidth<=target.ActualWidth||Math.Abs(columns.Sum(c=>c.ActualWidth)-scroll.ViewportWidth)>1)throw new Exception("Default widths do not fill the table or prioritize File.");
    dialog.Window.Width=windowWidth+180;PumpPopupLayout();
    if(file.ActualWidth<=initial[0]||target.ActualWidth<=initial[1]||Math.Abs(columns.Sum(c=>c.ActualWidth)-scroll.ViewportWidth)>1)throw new Exception("Default columns did not respond to a wider window.");
    dialog.Window.Width=windowWidth;PumpPopupLayout();if(Math.Abs(file.ActualWidth-initial[0])>1)throw new Exception("Window shrink did not restore proportional default widths.");
    initial=columns.Select(c=>c.ActualWidth).ToArray();double increase=target.ActualWidth-target.MinWidth+kind.ActualWidth-kind.MinWidth+400;
    thumb.RaiseEvent(new DragStartedEventArgs(0,0));thumb.RaiseEvent(new DragDeltaEventArgs(increase,0));thumb.RaiseEvent(new DragCompletedEventArgs(increase,0,false));PumpPopupLayout();
    if(file.Width.Value<initial[0]+increase-0.1||Math.Abs(target.Width.Value-target.MinWidth)>0.1||Math.Abs(kind.Width.Value-kind.MinWidth)>0.1)throw new Exception("Star resizing hit the viewport limit instead of shrinking neighbours and scrolling.");
    if(scroll.ScrollableWidth<=0||!grid.ClipToBounds)throw new Exception("Wide columns have no clipped horizontal overflow.");
    double overflow=columns.Sum(c=>c.ActualWidth)-scroll.ViewportWidth,dragged=file.ActualWidth;
    dialog.Window.Width=windowWidth+120;PumpPopupLayout();
    if(file.ActualWidth<=dragged||Math.Abs(columns.Sum(c=>c.ActualWidth)-scroll.ViewportWidth-overflow)>1)throw new Exception("A completed drag froze responsive widths or lost intentional overflow.");
    dialog.Window.Width=windowWidth;PumpPopupLayout();
    double before=file.Width.Value;thumb.RaiseEvent(new DragStartedEventArgs(0,0));thumb.RaiseEvent(new DragDeltaEventArgs(-150,0));thumb.RaiseEvent(new DragCompletedEventArgs(-150,0,false));PumpPopupLayout();if(file.Width.Value>=before||target.Width.Value<=target.MinWidth)throw new Exception("Shrinking a column did not return space to neighbours.");
    var widths=columns.Select(c=>c.Width).ToArray();thumb.RaiseEvent(new DragStartedEventArgs(0,0));thumb.RaiseEvent(new DragDeltaEventArgs(90,0));thumb.RaiseEvent(new DragCompletedEventArgs(90,0,true));if(!columns.Select(c=>c.Width).SequenceEqual(widths))throw new Exception("Canceled resize did not restore widths.");
    TableColumnResizing.Resize(columns,file,initial,20);PumpPopupLayout();if(file.Width.Value!=file.MinWidth||file.MinWidth>48)throw new Exception("Caption minimum prevented a narrow column.");
    var cell=PopupChildren<DataGridCell>(grid).First();if(!cell.ClipToBounds||!header.ClipToBounds)throw new Exception("Narrow cells or headings can paint over neighbours.");
    TableColumnResizing.Reset(grid);PumpPopupLayout();
    if(Math.Abs(file.ActualWidth-initial[0])>1||Math.Abs(columns.Sum(c=>c.ActualWidth)-scroll.ViewportWidth)>1||scroll.ScrollableWidth>1)throw new Exception("Restoring defaults retained dragged widths or horizontal overflow.");
    double beforeHide=file.ActualWidth;kind.Visibility=Visibility.Collapsed;TableColumnResizing.Refresh(grid);PumpPopupLayout();
    if(file.ActualWidth<=beforeHide||Math.Abs(file.ActualWidth+target.ActualWidth-scroll.ViewportWidth)>1)throw new Exception("Hidden columns did not release their space.");
    kind.Visibility=Visibility.Visible;TableColumnResizing.Refresh(grid);PumpPopupLayout();if(Math.Abs(file.ActualWidth-initial[0])>1)throw new Exception("Showing a column did not restore its default proportion.");
   }finally{dialog.Window.Close();}
  }
 }
}
