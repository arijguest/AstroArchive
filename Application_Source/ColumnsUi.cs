using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  readonly Dictionary<string,List<string>> defaultColumns=new Dictionary<string,List<string>>();
  readonly Dictionary<string,List<string>> originalColumnOrder=new Dictionary<string,List<string>>();
  bool testingColumnLayouts;
  static string ColumnId(DataGridColumn column){string path=((Binding)((DataGridBoundColumn)column).Binding).Path.Path;return path=="MountText"?"Mount":path;}
  void AddCaptureColumns(string name){
   var grid=G(name);defaultColumns[name]=grid.Columns.Select(ColumnId).ToList();
   string[] fields={"CapabilityText|CAPABILITIES|240","CameraModel|CAMERA MODEL|150","CameraId|CAMERA ID|150","TelescopeModel|TELESCOPE MODEL|150","Offset|OFFSET|70","ElectronsPerAdu|E-/ADU|80","ReadoutMode|READOUT MODE|130","Roi|ROI|140","OpticalConfiguration|OPTICAL CONFIGURATION|180","ObservedUtc|UTC CAPTURE TIME|180","TimeZoneId|TIMEZONE|140","OriginalName|FILE|220","ObjectId|OBJECT ID|90","TargetName|COMMON NAME / LABEL|220","Status|STATUS|110","ReviewText|REVIEW|115","Telescope|TELESCOPE|150","InstrumentText|DEVICE / MODEL|150","Camera|CAMERA|100","Kind|FRAME TYPE|110","AcquisitionDateLabel|ACQUIRED|100","Observed|RECORDED CAPTURE TIME|160","ExposureText|EXPOSURE|85","GainText|GAIN|75","TemperatureText|TEMPERATURE|110","Filter|FILTER|100","Calibration|CALIBRATION|130","SizeText|DIMENSIONS|140","BinX|BIN X|70","BinY|BIN Y|70","Bayer|BAYER PATTERN|110","Mount|MOUNT|130","Session|SESSION|160","Bytes|SIZE (BYTES)|115","ScreeningIssue|SCREENING ISSUE|240","SourceDisposition|SOURCE ACTION|240","RelativePath|REPOSITORY PATH|280","Notes|NOTES|280"};
   foreach(string field in fields){var parts=field.Split('|');if(grid.Columns.Any(c=>ColumnId(c)==parts[0]))continue;
    grid.Columns.Add(new DataGridTextColumn{Header=parts[1],Binding=new Binding(parts[0]),Width=int.Parse(parts[2]),Visibility=Visibility.Collapsed});
   }
   originalColumnOrder[name]=grid.Columns.Select(ColumnId).ToList();grid.CanUserReorderColumns=true;
  }
  void InitializeColumnLayouts(){
   foreach(string name in new[]{"FramesGrid","ImportGrid","EditedGrid"}){
    string table=name;var grid=G(table);ApplyColumnLayout(table,SavedColumnLayout(table));
    grid.ColumnReordered+=(s,e)=>PersistColumnLayout(table);
    grid.PreviewMouseRightButtonDown+=(s,e)=>{
     var node=e.OriginalSource as DependencyObject;DataGridColumnHeader header=null;
     while(node!=null){header=node as DataGridColumnHeader;if(header!=null)break;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}
     if(header==null||header.Column==null)return;e.Handled=true;var menu=BuildColumnsMenu(table,header.Column);menu.PlacementTarget=header;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;
    };
    if(table=="EditedGrid")continue;
    var button=B(table=="FramesGrid"?"LibraryColumnsButton":"ImportColumnsButton");
    UiHelp.Tip(button,"Choose columns; drag headers to reorder.");
    button.Click+=(s,e)=>{var menu=BuildColumnsMenu(table,null);menu.PlacementTarget=button;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;};
   }
  }
  ColumnLayout SavedColumnLayout(string name){
   ColumnLayout saved;if(settings.TableLayouts==null||!settings.TableLayouts.TryGetValue(name,out saved))return null;
   if(name=="EditedGrid")saved=ColumnLayout.UpgradeEdited(saved);
   if(name=="FramesGrid")saved=ColumnLayout.UpgradeRepository(saved);
   settings.TableLayouts[name]=saved;return saved;
  }
  void ApplyColumnLayout(string name,ColumnLayout saved){
   var grid=G(name);var layout=ColumnLayout.Resolve(saved,originalColumnOrder[name],defaultColumns[name]);
   for(int i=0;i<layout.Order.Count;i++)grid.Columns.First(c=>ColumnId(c)==layout.Order[i]).DisplayIndex=i;
   foreach(var column in grid.Columns)column.Visibility=layout.Visible.Contains(ColumnId(column))?Visibility.Visible:Visibility.Collapsed;
   tableSorts[name].RemoveAll(s=>!grid.Columns.Any(c=>c.Visibility==Visibility.Visible&&c.SortMemberPath==s.PropertyName));RestoreTableSort(name);
  }
  ColumnLayout CurrentColumnLayout(string name){var columns=G(name).Columns.OrderBy(c=>c.DisplayIndex).ToList();return new ColumnLayout{Order=columns.Select(ColumnId).ToList(),Visible=columns.Where(c=>c.Visibility==Visibility.Visible).Select(ColumnId).ToList(),RepositoryGainShown=name=="FramesGrid"};}
  void PersistColumnLayout(string name){
   if(settings.TableLayouts==null)settings.TableLayouts=new Dictionary<string,ColumnLayout>();settings.TableLayouts[name]=CurrentColumnLayout(name);
   if(!testingColumnLayouts)SaveSettings();
  }
  bool SetColumnVisible(string name,DataGridColumn column,bool visible){
   var grid=G(name);if(!visible&&grid.Columns.Count(c=>c.Visibility==Visibility.Visible)<=1)return false;
   column.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
   if(!visible){tableSorts[name].RemoveAll(s=>s.PropertyName==column.SortMemberPath);RestoreTableSort(name);}
   PersistColumnLayout(name);return true;
  }
  void MoveColumn(string name,DataGridColumn column,int offset){
   var visible=G(name).Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).ToList();int index=visible.IndexOf(column),next=index+offset;
   if(index<0||next<0||next>=visible.Count)return;column.DisplayIndex=visible[next].DisplayIndex;PersistColumnLayout(name);
  }
  void ResetColumns(string name){ApplyColumnLayout(name,null);if(settings.TableLayouts!=null)settings.TableLayouts.Remove(name);if(!testingColumnLayouts)SaveSettings();}
  ContextMenu BuildColumnsMenu(string name,DataGridColumn selected){
   var menu=ThemedMenu();var grid=G(name);
   menu.Items.Add(new MenuItem{Header="Drag headings to rearrange",IsEnabled=false});
   if(selected!=null){
    var visible=grid.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).ToList();int index=visible.IndexOf(selected);
    var left=new MenuItem{Header="Move heading left",IsEnabled=index>0};left.Click+=(s,e)=>MoveColumn(name,selected,-1);menu.Items.Add(left);
    var right=new MenuItem{Header="Move heading right",IsEnabled=index>=0&&index<visible.Count-1};right.Click+=(s,e)=>MoveColumn(name,selected,1);menu.Items.Add(right);
    menu.Items.Add(new Separator());
   }
   var more=new MenuItem{Header="More headings"};
   var choices=new List<MenuItem>();Action update=()=>{int count=grid.Columns.Count(c=>c.Visibility==Visibility.Visible);foreach(var item in choices){var col=(DataGridColumn)item.Tag;item.IsChecked=col.Visibility==Visibility.Visible;item.IsEnabled=!item.IsChecked||count>1;}};
   foreach(var column in grid.Columns.OrderBy(c=>c.DisplayIndex)){
    var choice=new MenuItem{Header=column.Header,IsCheckable=true,StaysOpenOnClick=true,Tag=column};var capture=column;
    choice.Click+=(s,e)=>{SetColumnVisible(name,capture,choice.IsChecked);update();};choices.Add(choice);
    if(defaultColumns[name].Contains(ColumnId(column)))menu.Items.Add(choice);else more.Items.Add(choice);
   }
   update();menu.Items.Add(more);menu.Items.Add(new Separator());var reset=new MenuItem{Header="Restore default columns"};reset.Click+=(s,e)=>ResetColumns(name);menu.Items.Add(reset);return menu;
  }
 }
}
