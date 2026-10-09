using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace AstroArchive {
 // Use proportional preferences on viewport changes and pixel widths during
 // dragging. Oversized requests retain horizontal overflow after a resize.
 public static class TableColumnResizing {
  sealed class Drag {
   public DataGridColumn Column;
   public List<DataGridColumn> Columns;
   public double[] Widths;
   public DataGridLength[] Original;
   public double Delta;
  }
  sealed class Layout {
   public readonly Dictionary<DataGridColumn,DataGridLength> Defaults=new Dictionary<DataGridColumn,DataGridLength>();
   public readonly Dictionary<DataGridColumn,double> Preferred=new Dictionary<DataGridColumn,double>();
   public List<DataGridColumn> Visible=new List<DataGridColumn>();
   public double Available=double.NaN,Overflow;
   public bool Customized,Pending,Force,Updating;
  }
  static readonly DependencyProperty LayoutProperty=DependencyProperty.RegisterAttached("Layout",typeof(Layout),typeof(TableColumnResizing),new PropertyMetadata(null));
  static readonly DependencyProperty DragProperty=DependencyProperty.RegisterAttached("Drag",typeof(Drag),typeof(TableColumnResizing),new PropertyMetadata(null));
  static DataGridColumnHeader Header(DependencyObject node){while(node!=null){var header=node as DataGridColumnHeader;if(header!=null)return header;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}return null;}
  static List<DataGridColumn> Visible(DataGrid grid){return grid.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).ToList();}
  static double Viewport(DataGrid grid){
   var viewer=grid.Template==null?null:grid.Template.FindName("DG_ScrollViewer",grid) as ScrollViewer;
   return viewer!=null&&viewer.ViewportWidth>0?viewer.ViewportWidth:Math.Max(0,grid.ActualWidth-grid.BorderThickness.Left-grid.BorderThickness.Right-grid.Padding.Left-grid.Padding.Right);
  }
  static void RememberColumns(DataGrid grid,Layout layout){foreach(var column in grid.Columns)if(!layout.Defaults.ContainsKey(column))layout.Defaults[column]=column.Width;}
  public static void Refresh(DataGrid grid){Queue(grid,true);}
  public static void Reset(DataGrid grid){
   var layout=(Layout)grid.GetValue(LayoutProperty);if(layout==null)return;
   layout.Customized=false;layout.Overflow=0;layout.Preferred.Clear();Queue(grid,true);
  }
  static void Queue(DataGrid grid,bool force){
   var layout=(Layout)grid.GetValue(LayoutProperty);if(layout==null||layout.Updating)return;
   layout.Force|=force;if(layout.Pending)return;layout.Pending=true;
   grid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{
    layout.Pending=false;bool requested=layout.Force;layout.Force=false;
    if(!grid.IsVisible||grid.GetValue(DragProperty)!=null)return;
    Fit(grid,layout,requested);
   }));
  }
  static void Fit(DataGrid grid,Layout layout,bool force){
   double available=Viewport(grid);if(available<=0)return;var columns=Visible(grid);bool changed=columns.Count!=layout.Visible.Count||columns.Any(c=>!layout.Visible.Contains(c));
   if(!force&&!changed&&Math.Abs(available-layout.Available)<0.5)return;
   RememberColumns(grid,layout);if(changed)layout.Overflow=0;layout.Visible=columns;layout.Available=available;
   var font=grid.TryFindResource("UiFontCaption");double size=font is double?(double)font:10,scale=size/10;
   var preferred=new double[columns.Count];var minimum=new double[columns.Count];
   for(int i=0;i<columns.Count;i++){
    var column=columns[i];var original=layout.Defaults[column];double custom;
    preferred[i]=layout.Customized&&layout.Preferred.TryGetValue(column,out custom)?custom:(original.IsStar?360*original.Value:original.IsAbsolute?original.Value:140)*scale;
    minimum[i]=column.MinWidth;
    if(!layout.Customized){
     var caption=new FormattedText(Convert.ToString(column.Header),CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(grid.FontFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),size,Brushes.Black,VisualTreeHelper.GetDpi(grid).PixelsPerDip);
     minimum[i]=Math.Max(minimum[i],caption.WidthIncludingTrailingWhitespace+30*scale);
     var binding=column as DataGridBoundColumn;var path=binding==null?null:binding.Binding as Binding;string field=path==null||path.Path==null?"":path.Path.Path;
     string sample=field=="ObjectId"||field=="Metadata.ObjectId"?"NGC7000":field=="Kind"||field=="KindLabel"?"Stack (1000)":null;
     if(sample!=null){var body=grid.TryFindResource("UiFontBody");double bodySize=body is double?(double)body:grid.FontSize;
      var text=new FormattedText(sample,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(grid.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),bodySize,Brushes.Black,VisualTreeHelper.GetDpi(grid).PixelsPerDip);
      minimum[i]=Math.Max(minimum[i],text.WidthIncludingTrailingWhitespace+20*scale);
     }
     if(original.IsStar)minimum[i]=Math.Max(minimum[i],120*scale);
     minimum[i]=Math.Min(minimum[i],column.MaxWidth);
    }
   }
   // The window rounds layout to physical pixels. Round the whole allocation
   // together so individual columns cannot overrun the viewport and toggle bars.
   double dpi=VisualTreeHelper.GetDpi(grid).DpiScaleX,budget=Math.Floor((available+layout.Overflow)*dpi)/dpi;
   minimum=minimum.Select(width=>Math.Ceiling(width*dpi)/dpi).ToArray();
   var maximum=columns.Select(c=>Math.Floor(c.MaxWidth*dpi)/dpi).ToArray();
   var widths=ColumnWidths.Fit(preferred,minimum,maximum,columns.Select(c=>c.CanUserResize).ToArray(),budget);
   for(int i=0;i<widths.Length;i++)widths[i]=Math.Max(minimum[i],Math.Floor(widths[i]*dpi)/dpi);
   double spare=Math.Max(0,Math.Floor((budget-widths.Sum())*dpi+0.000001)/dpi);
   foreach(int i in Enumerable.Range(0,widths.Length).Where(i=>columns[i].CanUserResize).OrderByDescending(i=>preferred[i])){
    double give=Math.Floor(Math.Min(spare,maximum[i]-widths[i])*dpi)/dpi;widths[i]+=give;spare-=give;if(spare<=0)break;
   }
   layout.Updating=true;
   try{for(int i=0;i<columns.Count;i++)columns[i].Width=new DataGridLength(widths[i]);}
   finally{layout.Updating=false;}
  }
  public static void Attach(DataGrid grid){
   if(grid.GetValue(LayoutProperty)!=null)return;var layout=new Layout();grid.SetValue(LayoutProperty,layout);RememberColumns(grid,layout);
   grid.Loaded+=(s,e)=>Queue(grid,true);grid.IsVisibleChanged+=(s,e)=>{if(grid.IsVisible)Queue(grid,false);};
   grid.SizeChanged+=(s,e)=>{if(e.WidthChanged)Queue(grid,false);};
   grid.Columns.CollectionChanged+=(s,e)=>{RememberColumns(grid,layout);Queue(grid,true);};
   grid.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((s,e)=>{if(e.ViewportWidthChange!=0)Queue(grid,false);}),true);
   Queue(grid,true);
   grid.AddHandler(Thumb.DragStartedEvent,new DragStartedEventHandler((s,e)=>{
    var thumb=e.OriginalSource as Thumb;if(thumb==null||!grid.CanUserResizeColumns)return;
    var header=Header(thumb);if(header==null||header.Column==null)return;
    var columns=Visible(grid);var column=header.Column;
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
    var drag=(Drag)grid.GetValue(DragProperty);grid.ClearValue(DragProperty);if(drag==null)return;
    if(e.Canceled){for(int i=0;i<drag.Columns.Count;i++)drag.Columns[i].Width=drag.Original[i];}
    else{
     layout.Customized=true;foreach(var column in drag.Columns)layout.Preferred[column]=column.Width.Value;
     layout.Visible=drag.Columns;layout.Available=Viewport(grid);layout.Overflow=Math.Max(0,drag.Columns.Sum(c=>c.Width.Value)-layout.Available);
    }
    Queue(grid,false);
   }),true);
  }
  internal static void Resize(IList<DataGridColumn> columns,DataGridColumn selected,double[] initial,double requested){
   int index=columns.IndexOf(selected);if(index<0||double.IsNaN(requested)||double.IsInfinity(requested))return;
   var widths=ColumnWidths.Resize(initial,columns.Select(c=>c.MinWidth).ToArray(),columns.Select(c=>c.MaxWidth).ToArray(),columns.Select(c=>c.CanUserResize).ToArray(),index,requested);
   for(int i=0;i<columns.Count;i++)columns[i].Width=new DataGridLength(widths[i]);
  }
 }
}
