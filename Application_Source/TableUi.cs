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
  public object Convert(object value,Type target,object parameter,CultureInfo culture){return value==null||value is string?TableText.Display(value as string,new[]{"Target","TargetName","TargetLabel"}.Contains(System.Convert.ToString(parameter))):value;}
  public object ConvertBack(object value,Type target,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
 public partial class MainUi {
  readonly Dictionary<string,List<SortDescription>> tableSorts=new Dictionary<string,List<SortDescription>>();
  void InitializeTables(){
   var converter=new MetadataCellConverter();foreach(string name in new[]{"FramesGrid","ImportGrid","MetricsGrid"}){var grid=G(name);grid.CanUserSortColumns=true;tableSorts[name]=new List<SortDescription>();
    foreach(var column in grid.Columns.OfType<DataGridBoundColumn>()){var binding=column.Binding as Binding;if(binding==null||binding.Path==null)continue;string path=binding.Path.Path;var header=new Style(typeof(DataGridColumnHeader),Window.TryFindResource(typeof(DataGridColumnHeader)) as Style);string tip=Convert.ToString(column.Header)+": click to sort; click again to reverse. Shift-click adds a sorting column.";if(path=="SizeText")tip+=" Dimensions sort by total pixel count.";else if(path=="ExposureText"||path=="GainText"||path=="TemperatureText"||name=="MetricsGrid"&&path!="Stage")tip+=" Values sort numerically.";header.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new ToolTip{Content=new TextBlock{Text=tip,TextWrapping=TextWrapping.Wrap,MaxWidth=360}}));column.HeaderStyle=header;column.SortMemberPath=path=="ExposureText"?"Exposure":path=="GainText"?"Gain":path=="TemperatureText"?"Temperature":path=="SizeText"?"PixelCount":path;
     if(name!="MetricsGrid"&&path!="OriginalName"){column.Binding=new Binding(path){Mode=BindingMode.OneWay,Converter=converter,ConverterParameter=path,StringFormat=binding.StringFormat};}
    }
    string table=name;grid.Sorting+=(s,e)=>{e.Handled=true;SortTable(table,e.Column,(Keyboard.Modifiers&ModifierKeys.Shift)!=0);};
   }
  }
  void SortTable(string name,DataGridColumn column,bool additive){string property=column.SortMemberPath;if(string.IsNullOrEmpty(property))return;var direction=column.SortDirection==ListSortDirection.Ascending?ListSortDirection.Descending:ListSortDirection.Ascending;var sorts=tableSorts[name];if(!additive)sorts.Clear();int index=sorts.FindIndex(d=>d.PropertyName==property);var sort=new SortDescription(property,direction);if(index<0)sorts.Add(sort);else sorts[index]=sort;RestoreTableSort(name);}
  void SetRows(string name,IEnumerable rows){G(name).ItemsSource=rows;RestoreTableSort(name);}
  void RestoreTableSort(string name){List<SortDescription> sorts;if(!tableSorts.TryGetValue(name,out sorts))return;var grid=G(name);if(grid.Items.CanSort)using(grid.Items.DeferRefresh()){grid.Items.SortDescriptions.Clear();foreach(var sort in sorts)grid.Items.SortDescriptions.Add(sort);}
   foreach(var column in grid.Columns){var sort=sorts.FirstOrDefault(d=>d.PropertyName==column.SortMemberPath);column.SortDirection=string.IsNullOrEmpty(sort.PropertyName)?(ListSortDirection?)null:sort.Direction;}
  }
  void SmokeTablesAndPreview(){
   var rows=new[]{new Frame{Kind="Light",Target="Unknown",Exposure=10},new Frame{Kind="Light",Exposure=2},new Frame{Kind="Bias",Target="Calibration",Exposure=0},new Frame{Kind="Unknown"}};
   foreach(string name in new[]{"FramesGrid","ImportGrid"}){var grid=G(name);var original=grid.ItemsSource;var saved=tableSorts[name].ToList();try{
     SetRows(name,rows);var exposure=grid.Columns.First(c=>c.SortMemberPath=="Exposure");SortTable(name,exposure,false);if(!grid.Items.Cast<Frame>().Where(f=>f.Exposure.HasValue).Select(f=>f.Exposure.Value).SequenceEqual(new[]{0.0,2,10}))throw new InvalidOperationException("Exposure column did not sort numerically.");
     SetRows(name,rows.Reverse().ToList());if(grid.Items.Cast<Frame>().Last().Exposure!=10||exposure.SortDirection!=ListSortDirection.Ascending)throw new InvalidOperationException("Table refresh lost its sorting.");SortTable(name,exposure,false);if(grid.Items.Cast<Frame>().First().Exposure!=10)throw new InvalidOperationException("Repeated header sorting did not reverse direction.");
     var kind=grid.Columns.First(c=>c.SortMemberPath=="Kind");SortTable(name,kind,true);SortTable(name,exposure,true);if(tableSorts[name][0].PropertyName!="Exposure"||tableSorts[name][1].PropertyName!="Kind")throw new InvalidOperationException("Shift sorting changed column priority.");
     var binding=((DataGridBoundColumn)kind).Binding as Binding;if(binding==null||!object.Equals(binding.Converter.Convert("Bias",typeof(string),binding.ConverterParameter,CultureInfo.InvariantCulture),"Bias")||!object.Equals(binding.Converter.Convert("Unknown",typeof(string),binding.ConverterParameter,CultureInfo.InvariantCulture),"-"))throw new InvalidOperationException("Frame type cells hid bias or displayed Unknown.");
     binding=((DataGridBoundColumn)grid.Columns.First(c=>c.SortMemberPath=="TargetName")).Binding as Binding;if(!object.Equals(binding.Converter.Convert("Unknown",typeof(string),binding.ConverterParameter,CultureInfo.InvariantCulture),"Unknown"))throw new InvalidOperationException("Target cells lost the Unknown exception.");
    }finally{tableSorts[name]=saved;SetRows(name,original);}}
   var metrics=G("MetricsGrid");var oldMetrics=metrics.ItemsSource;var oldSorts=tableSorts["MetricsGrid"].ToList();try{SetRows("MetricsGrid",new[]{new StageMetric{Stage="Ten",Seconds=10},new StageMetric{Stage="Two",Seconds=2}});SortTable("MetricsGrid",metrics.Columns.First(c=>c.SortMemberPath=="Seconds"),false);if(metrics.Items.Cast<StageMetric>().First().Seconds!=2)throw new InvalidOperationException("Performance column did not sort numerically.");}finally{tableSorts["MetricsGrid"]=oldSorts;SetRows("MetricsGrid",oldMetrics);}
   var menu=new ContextMenu();BuildFileMenu(menu,rows.Take(1).ToList());var preview=menu.Items.OfType<MenuItem>().First(i=>Convert.ToString(i.Header)=="Preview image…");if(!preview.IsEnabled)throw new InvalidOperationException("Single-image preview is disabled.");BuildFileMenu(menu,rows.Take(2).ToList());if(menu.Items.OfType<MenuItem>().First(i=>Convert.ToString(i.Header)=="Preview image…").IsEnabled)throw new InvalidOperationException("Multi-selection incorrectly enables single-image preview.");
  }
 }
}
