using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
namespace AstroArchive {
 public class MetadataCellConverter:IValueConverter {
  public object Convert(object value,Type target,object parameter,CultureInfo culture){return value==null||value is string?TableText.Display(value as string,new[]{"Target","TargetLabel"}.Contains(System.Convert.ToString(parameter))):value;}
  public object ConvertBack(object value,Type target,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
 public sealed class RepositoryPositionComparer:IComparer {
  readonly Dictionary<Frame,int> positions=new Dictionary<Frame,int>();
  public RepositoryPositionComparer(IEnumerable<Frame> rows){int index=0;foreach(var frame in rows)if(!positions.ContainsKey(frame))positions[frame]=index++;}
  public int Compare(object a,object b){return positions[(Frame)a].CompareTo(positions[(Frame)b]);}
 }
 public partial class MainUi {
  readonly Dictionary<string,List<SortDescription>> tableSorts=new Dictionary<string,List<SortDescription>>();
  void InitializeTables(){
   AddCaptureColumns("FramesGrid");AddCaptureColumns("ImportGrid");defaultColumns["EditedGrid"]=G("EditedGrid").Columns.Where(c=>c.Visibility==Visibility.Visible).Select(ColumnId).ToList();originalColumnOrder["EditedGrid"]=G("EditedGrid").Columns.Select(ColumnId).ToList();var converter=new MetadataCellConverter();foreach(string name in new[]{"FramesGrid","ImportGrid","EditedGrid","MetricsGrid"}){var grid=G(name);grid.CanUserSortColumns=true;tableSorts[name]=new List<SortDescription>();
    foreach(var column in grid.Columns.OfType<DataGridBoundColumn>()){var binding=column.Binding as Binding;if(binding==null||binding.Path==null)continue;string path=binding.Path.Path;if(path=="Mount")path="MountText";var header=new Style(typeof(DataGridColumnHeader),Window.TryFindResource(typeof(DataGridColumnHeader)) as Style);string tip="Click to sort; Shift-click adds a sort column. Right-click to choose columns.";if(path=="MountText")tip+=" A ? marks an inference.";else if(path=="SizeText")tip+=" Sorted by pixel count.";header.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new ToolTip{Content=new TextBlock{Text=tip,TextWrapping=TextWrapping.Wrap,MaxWidth=360}}));column.HeaderStyle=header;column.SortMemberPath=path=="Metadata.TotalExposureText"?"Metadata.TotalExposure":path=="Metadata.SubExposureText"?"Metadata.SubExposure":path=="Metadata.SubsText"?"Metadata.Subs":path=="ExposureText"?"Exposure":path=="GainText"?"Gain":path=="TemperatureText"?"Temperature":path=="SizeText"?"PixelCount":path;
     if(((name=="FramesGrid"||name=="ImportGrid")&&path!="OriginalName")||(name=="EditedGrid"&&path=="Metadata.TargetName")){column.Binding=new Binding(path){Mode=BindingMode.OneWay,Converter=converter,ConverterParameter=path,StringFormat=binding.StringFormat};}
     if(path=="ExposureText"){var cell=new Style(typeof(TextBlock),column.ElementStyle);cell.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("ExposureTooltip")));cell.Setters.Add(new Setter(ToolTipService.ShowDurationProperty,30000));column.ElementStyle=cell;}
    }
    if(name=="FramesGrid"||name=="ImportGrid")tableSorts[name].Add(new SortDescription("Kind",ListSortDirection.Ascending));
    string table=name;TableSortIndicators.Attach(grid);grid.Sorting+=(s,e)=>{e.Handled=true;SortTable(table,e.Column,(Keyboard.Modifiers&ModifierKeys.Shift)!=0);};RestoreTableSort(name);
   }
   InitializeColumnLayouts();
  }
  void SortTable(string name,DataGridColumn column,bool additive){string property=column.SortMemberPath;if(string.IsNullOrEmpty(property))return;var direction=column.SortDirection==ListSortDirection.Ascending?ListSortDirection.Descending:ListSortDirection.Ascending;var sorts=tableSorts[name];if(!additive)sorts.Clear();int index=sorts.FindIndex(d=>d.PropertyName==property);var sort=new SortDescription(property,direction);if(index<0)sorts.Add(sort);else sorts[index]=sort;RestoreTableSort(name);}
  void SetRows(string name,IEnumerable rows,bool presorted=false){G(name).ItemsSource=rows;if(presorted){var sorts=tableSorts[name];foreach(var column in G(name).Columns){var sort=sorts.FirstOrDefault(d=>d.PropertyName==column.SortMemberPath);column.SortDirection=string.IsNullOrEmpty(sort.PropertyName)?(ListSortDirection?)null:sort.Direction;}TableSortIndicators.Update(G(name),sorts);}else RestoreTableSort(name);}
  void RestoreTableSort(string name){List<SortDescription> sorts;if(!tableSorts.TryGetValue(name,out sorts))return;var grid=G(name);if(grid.Items.CanSort)using(grid.Items.DeferRefresh()){grid.Items.SortDescriptions.Clear();if(name=="FramesGrid"&&grid.ItemsSource!=null){
    var view=grid.ItemsSource as ListCollectionView??CollectionViewSource.GetDefaultView(grid.ItemsSource) as ListCollectionView;
    if(view!=null){var target=Targets.SelectedItem as TargetSummary;var ordered=RepositoryOrdering.Order(view.SourceCollection.Cast<Frame>(),SearchSorts(name),grid.Items.Culture,target==null||target.Name=="All targets",Convert.ToString(C("LibraryViewBox").SelectedItem)=="Session summaries",System.Threading.CancellationToken.None);view.CustomSort=new RepositoryPositionComparer(ordered);}
   }else foreach(var sort in sorts)grid.Items.SortDescriptions.Add(sort);}
   foreach(var column in grid.Columns){var sort=sorts.FirstOrDefault(d=>d.PropertyName==column.SortMemberPath);column.SortDirection=string.IsNullOrEmpty(sort.PropertyName)?(ListSortDirection?)null:sort.Direction;}TableSortIndicators.Update(grid,sorts);
  }
  void SmokeTablesAndPreview(){
   foreach(string name in new[]{"FramesGrid","ImportGrid"}){var sorts=tableSorts[name];if(sorts.Count==0||sorts[0].PropertyName!="Kind"||sorts[0].Direction!=ListSortDirection.Ascending)throw new Exception("Frame Type is not the default capture sort");}
   var rows=new[]{new Frame{Kind="Light",Target="Unknown",Exposure=10},new Frame{Kind="Light",Exposure=2},new Frame{Kind="Bias",Target="Calibration",Exposure=0},new Frame{Kind="Unknown"}};
   foreach(string name in new[]{"FramesGrid","ImportGrid"}){var grid=G(name);var original=grid.ItemsSource;var saved=tableSorts[name].ToList();try{
     SetRows(name,rows);var exposure=grid.Columns.First(c=>c.SortMemberPath=="Exposure");SortTable(name,exposure,false);if(!grid.Items.Cast<Frame>().Where(f=>f.Exposure.HasValue&&(name!="FramesGrid"||!CaptureSky.IsCalibration(f))).Select(f=>f.Exposure.Value).SequenceEqual(name=="FramesGrid"?new[]{2.0,10}:new[]{0.0,2,10}))throw new InvalidOperationException("Exposure column did not sort numerically.");
     SetRows(name,rows.Reverse().ToList());if(grid.Items.Cast<Frame>().Where(f=>f.Exposure.HasValue&&(name!="FramesGrid"||!CaptureSky.IsCalibration(f))).Last().Exposure!=10||exposure.SortDirection!=ListSortDirection.Ascending)throw new InvalidOperationException("Table refresh lost its sorting.");SortTable(name,exposure,false);if(grid.Items.Cast<Frame>().Where(f=>name!="FramesGrid"||!CaptureSky.IsCalibration(f)).First().Exposure!=10)throw new InvalidOperationException("Repeated header sorting did not reverse direction.");
     var kind=grid.Columns.First(c=>c.SortMemberPath=="Kind");SortTable(name,kind,true);SortTable(name,exposure,true);if(tableSorts[name][0].PropertyName!="Exposure"||tableSorts[name][1].PropertyName!="Kind")throw new InvalidOperationException("Shift sorting changed column priority.");
     var binding=((DataGridBoundColumn)kind).Binding as Binding;if(binding==null||!object.Equals(binding.Converter.Convert("Bias",typeof(string),binding.ConverterParameter,CultureInfo.InvariantCulture),"Bias")||!object.Equals(binding.Converter.Convert("Unknown",typeof(string),binding.ConverterParameter,CultureInfo.InvariantCulture),"-"))throw new InvalidOperationException("Frame type cells hid bias or displayed Unknown.");
     var exposureStyle=((DataGridBoundColumn)exposure).ElementStyle;var exposureTip=exposureStyle.Setters.OfType<Setter>().FirstOrDefault(setter=>setter.Property==FrameworkElement.ToolTipProperty);if(exposureTip==null||!(exposureTip.Value is Binding)||((Binding)exposureTip.Value).Path.Path!="ExposureTooltip")throw new InvalidOperationException("Exposure source tooltip is missing.");
     binding=((DataGridBoundColumn)grid.Columns.First(c=>c.SortMemberPath=="TargetName")).Binding as Binding;if(!object.Equals(binding.Converter.Convert("Unknown",typeof(string),binding.ConverterParameter,CultureInfo.InvariantCulture),"-"))throw new InvalidOperationException("Common name cells did not replace Unknown with a dash.");
     var mount=grid.Columns.First(c=>ColumnId(c)=="Mount");binding=((DataGridBoundColumn)mount).Binding as Binding;if(binding.Path.Path!="MountText"||mount.SortMemberPath!="MountText")throw new InvalidOperationException("Mount column does not display and sort the suggested label.");
     string label=rows[0].MountText;if(!object.Equals(binding.Converter.Convert(label,typeof(string),binding.ConverterParameter,CultureInfo.InvariantCulture),"Alt-Az?"))throw new InvalidOperationException("Mount column hid an inferred label.");
    }finally{tableSorts[name]=saved;SetRows(name,original);}}
   var editedName=((DataGridBoundColumn)G("EditedGrid").Columns.First(c=>c.SortMemberPath=="Metadata.TargetName")).Binding as Binding;if(editedName.Converter==null||!object.Equals(editedName.Converter.Convert("Unknown",typeof(string),editedName.ConverterParameter,CultureInfo.InvariantCulture),"-")||!object.Equals(editedName.Converter.Convert("Whirlpool Galaxy",typeof(string),editedName.ConverterParameter,CultureInfo.InvariantCulture),"Whirlpool Galaxy"))throw new InvalidOperationException("Edited common-name placeholder lost its binding or a known name.");
   var metrics=G("MetricsGrid");var oldMetrics=metrics.ItemsSource;var oldSorts=tableSorts["MetricsGrid"].ToList();try{SetRows("MetricsGrid",new[]{new StageMetric{Stage="Ten",Seconds=10},new StageMetric{Stage="Two",Seconds=2}});SortTable("MetricsGrid",metrics.Columns.First(c=>c.SortMemberPath=="Seconds"),false);if(metrics.Items.Cast<StageMetric>().First().Seconds!=2)throw new InvalidOperationException("Performance column did not sort numerically.");}finally{tableSorts["MetricsGrid"]=oldSorts;SetRows("MetricsGrid",oldMetrics);}
   var menu=new ContextMenu();BuildFileMenu(menu,rows.Take(1).ToList());var preview=menu.Items.OfType<MenuItem>().First(i=>Convert.ToString(i.Header)=="Preview…");if(!preview.IsEnabled)throw new InvalidOperationException("Single-image preview is disabled.");BuildFileMenu(menu,rows.Take(2).ToList());if(menu.Items.OfType<MenuItem>().First(i=>Convert.ToString(i.Header)=="Preview…").IsEnabled)throw new InvalidOperationException("Multi-selection incorrectly enables single-image preview.");
  }
 }
}
