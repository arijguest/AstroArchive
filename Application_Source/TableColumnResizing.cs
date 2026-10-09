using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace AstroArchive {
 // Freeze star widths during a drag so WPF's star budget cannot cap the request.
 // Neighbours give up space first; once they reach their minimum, the table scrolls.
 public static class TableColumnResizing {
  sealed class Drag {
   public DataGridColumn Column;
   public List<DataGridColumn> Columns;
   public double[] Widths;
   public DataGridLength[] Original;
   public double Delta;
  }
  static readonly DependencyProperty AttachedProperty=DependencyProperty.RegisterAttached("Attached",typeof(bool),typeof(TableColumnResizing),new PropertyMetadata(false));
  static readonly DependencyProperty DragProperty=DependencyProperty.RegisterAttached("Drag",typeof(Drag),typeof(TableColumnResizing),new PropertyMetadata(null));
  static DataGridColumnHeader Header(DependencyObject node){while(node!=null){var header=node as DataGridColumnHeader;if(header!=null)return header;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}return null;}
  public static void Attach(DataGrid grid){
   if((bool)grid.GetValue(AttachedProperty))return;grid.SetValue(AttachedProperty,true);
   grid.AddHandler(Thumb.DragStartedEvent,new DragStartedEventHandler((s,e)=>{
    var thumb=e.OriginalSource as Thumb;if(thumb==null||!grid.CanUserResizeColumns)return;
    var header=Header(thumb);if(header==null||header.Column==null)return;
    var columns=grid.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).ToList();var column=header.Column;
    if(thumb.Name=="PART_LeftHeaderGripper"){int index=columns.IndexOf(column);if(index<=0)return;column=columns[index-1];}
    else if(thumb.Name!="PART_RightHeaderGripper")return;
    if(!column.CanUserResize)return;
    var drag=new Drag{Column=column,Columns=columns,Widths=columns.Select(c=>c.ActualWidth).ToArray(),Original=columns.Select(c=>c.Width).ToArray()};grid.SetValue(DragProperty,drag);
    for(int i=0;i<columns.Count;i++)columns[i].Width=new DataGridLength(drag.Widths[i]);
   }),true);
   grid.AddHandler(Thumb.DragDeltaEvent,new DragDeltaEventHandler((s,e)=>{
    var drag=(Drag)grid.GetValue(DragProperty);if(drag==null)return;drag.Delta+=e.HorizontalChange;
    Resize(drag.Columns,drag.Column,drag.Widths,drag.Widths[drag.Columns.IndexOf(drag.Column)]+drag.Delta);e.Handled=true;
   }),true);
   grid.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler((s,e)=>{
    var drag=(Drag)grid.GetValue(DragProperty);grid.ClearValue(DragProperty);if(drag!=null&&e.Canceled)for(int i=0;i<drag.Columns.Count;i++)drag.Columns[i].Width=drag.Original[i];
   }),true);
  }
  internal static void Resize(IList<DataGridColumn> columns,DataGridColumn selected,double[] initial,double requested){
   int index=columns.IndexOf(selected);if(index<0||double.IsNaN(requested)||double.IsInfinity(requested))return;
   var widths=ColumnWidths.Resize(initial,columns.Select(c=>c.MinWidth).ToArray(),columns.Select(c=>c.MaxWidth).ToArray(),columns.Select(c=>c.CanUserResize).ToArray(),index,requested);
   for(int i=0;i<columns.Count;i++)columns[i].Width=new DataGridLength(widths[i]);
  }
 }
}
