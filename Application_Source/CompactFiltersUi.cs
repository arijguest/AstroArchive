using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
namespace AstroArchive {
 public sealed class FilterChoice {public string Value{get;set;}public string Label{get;set;}public string Description{get;set;}}
 public partial class MainUi {
  Popup filtersPopup;
  void ShowFilters(bool imports){
   if(filtersPopup!=null&&filtersPopup.IsOpen){filtersPopup.IsOpen=false;return;}
   var criteria=imports?importFilters:libraryFilters;var rows=imports?CurrentImportRows():all;
   FrameworkElement button=TopMenu(imports?"ImportMenu":"RepositoryMenu");
   var popup=new Popup{PlacementTarget=button,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,PopupAnimation=settings.ReducedMotion?PopupAnimation.None:PopupAnimation.Fade,VerticalOffset=6};filtersPopup=popup;
   double width=Math.Max(380,Math.Min(620,SystemParameters.WorkArea.Width-48)),height=Math.Max(300,Math.Min(650,SystemParameters.WorkArea.Height-80));
   var border=new Border{Width=width,MaxHeight=height,Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(203,213,227)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(16)};
   border.Resources.MergedDictionaries.Add(Window.Resources);TextElement.SetFontFamily(border,Window.FontFamily);border.SetResourceReference(TextElement.FontSizeProperty,"UiFontSmall");Theme.Bind(border,Border.BackgroundProperty,"Surface");Theme.Bind(border,Border.BorderBrushProperty,"Border");
   var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});border.Child=layout;
   var heading=new StackPanel{Margin=new Thickness(0,0,0,12)};heading.Children.Add(new TextBlock{Text="Filters",FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(36,50,71))});
   var count=new TextBlock{Foreground=Brushes.SlateGray,Margin=new Thickness(0,5,0,0),TextWrapping=TextWrapping.Wrap};heading.Children.Add(count);layout.Children.Add(heading);
   var body=new StackPanel();var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=height-140};Grid.SetRow(scroll,1);layout.Children.Add(scroll);
   var footer=new WrapPanel{Margin=new Thickness(0,12,0,0)};Grid.SetRow(footer,2);layout.Children.Add(footer);
   var clear=new Button{Content="Clear filters",Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,0,8,0)};footer.Children.Add(clear);
   var done=new Button{Content="Done",Padding=new Thickness(12,6,12,6),Margin=new Thickness(0),Background=new SolidColorBrush(Color.FromRgb(77,85,199)),Foreground=Brushes.White};footer.Children.Add(done);done.Click+=(s,e)=>popup.IsOpen=false;
   Action refresh=()=>{count.Text=(imports?visibleImports.Count:displayed.Count)+" captures shown · "+criteria.ActiveCount+" active filters. Changes apply immediately.";clear.IsEnabled=criteria.ActiveCount>0;};
   Action apply=()=>{ApplyFilters(imports);refresh();};
   clear.Click+=(s,e)=>{criteria.Reset();ApplyFilters(imports);popup.IsOpen=false;ShowFilters(imports);};
   var primary=FilterGrid();body.Children.Add(primary);int cell=0;
   foreach(string field in CaptureFilters.Primary)if(CaptureFilters.Useful(rows,field,criteria.Values.ContainsKey(field)))AddFilterCell(primary,FilterSelector(field,rows,criteria,apply),cell++);
   var ranges=FilterGrid();body.Children.Add(ranges);cell=0;
   foreach(string field in new[]{"Exposure","Gain"})if(CaptureFilters.Useful(rows,field,criteria.Ranges.ContainsKey(field)))AddFilterCell(ranges,RangeSelector(field,rows,criteria,apply),cell++);
   var advancedFields=CaptureFilters.Advanced.Where(field=>CaptureFilters.Useful(rows,field,criteria.Values.ContainsKey(field))).ToList();
   if(advancedFields.Count>0){var advanced=FilterGrid();cell=0;foreach(string field in advancedFields)AddFilterCell(advanced,FilterSelector(field,rows,criteria,apply),cell++);body.Children.Add(new Expander{Header="Advanced capture settings",Content=advanced,Margin=new Thickness(0,6,0,0),IsExpanded=advancedFields.Any(criteria.Values.ContainsKey)});}
   int unknown=rows.Count(f=>f.Status!="Deleted"&&CaptureSessions.Date(f)==null);
   if(unknown>0){var note=new StackPanel{Margin=new Thickness(0,12,0,0)};note.Children.Add(new TextBlock{Text=unknown+" captures have no recorded acquisition date. Session labels mark missing dates; shifted nights are not used.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.SlateGray,FontSize=11});
    var read=new Button{Content="Read missing dates from FITS",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,8,0,0),IsEnabled=cancel==null&&repo!=null};note.Children.Add(read);read.Click+=(s,e)=>{popup.IsOpen=false;Run(ct=>repo.ReadMissingAcquisitionDates(rows,imports,ct,Progress).ToString(),result=>{if(imports)FilterImports();L("StatusLabel").Text=result+" acquisition dates recovered. Files without a readable recorded date remain unknown.";});};body.Children.Add(note);
   }
   body.Children.Add(new TextBlock{Text="Fields shared by every capture are hidden. Search and other filters stay in effect.",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brushes.SlateGray,Margin=new Thickness(0,12,0,0)});
   border.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){popup.IsOpen=false;e.Handled=true;}};
   foreach(var label in FilterLabels(border)){Theme.Bind(label,TextBlock.ForegroundProperty,label.FontWeight==FontWeights.SemiBold?"Text":"Muted");label.SetResourceReference(TextBlock.FontSizeProperty,label.FontWeight==FontWeights.SemiBold?"UiFontHeading":"UiFontSmall");}
   foreach(var label in new[]{count})Theme.Bind(label,TextBlock.ForegroundProperty,"Muted");
   popup.Closed+=(s,e)=>{if(filtersPopup==popup)filtersPopup=null;if(Window.IsActive)button.Focus();};popup.Child=border;refresh();
   popup.Opened+=(s,e)=>border.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));popup.IsOpen=true;
  }
  // Popup visuals are not built until it opens; bind the declared labels through
  // the logical tree so their colours and text scale are ready for the first frame.
  static IEnumerable<TextBlock> FilterLabels(DependencyObject parent){
   var label=parent as TextBlock;if(label!=null)yield return label;
   foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())foreach(var text in FilterLabels(child))yield return text;
  }
  static Grid FilterGrid(){var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});return grid;}
  static void AddFilterCell(Grid grid,UIElement element,int cell){int row=cell/2;while(grid.RowDefinitions.Count<=row)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetColumn(element,cell%2);Grid.SetRow(element,row);grid.Children.Add(element);}
  FrameworkElement FilterSelector(string field,List<Frame> rows,CaptureFilters criteria,Action apply){
   var panel=new StackPanel{Margin=new Thickness(0,0,12,12)};panel.Children.Add(new TextBlock{Text=field=="Dimensions"?"Image size":field,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,5)});
   var options=new List<FilterChoice>{new FilterChoice{Label="All",Description="No "+field.ToLowerInvariant()+" restriction."}};
   if(field=="Session")options.AddRange(CaptureSessions.Choices(rows).Select(c=>new FilterChoice{Value=c.Key,Label=c.Label,Description=c.Description}));
   else if(field=="Review")options.AddRange(CaptureFilters.ReviewChoices.Select(v=>new FilterChoice{Value=v,Label=v,Description=v=="No issues flagged"?"May include captures that have not been screened.":v}));
   else options.AddRange(CaptureFilters.Options(rows,field).Select(v=>new FilterChoice{Value=v,Label=field=="Target"?Catalog.Label(v):v,Description=field=="Target"?Catalog.Label(v):v}));
   string selected;criteria.Values.TryGetValue(field,out selected);if(selected!=null&&!options.Any(o=>o.Value==selected))options.Add(new FilterChoice{Value=selected,Label=field=="Session"?"Selected session unavailable":selected,Description="The selected value is absent from these captures. Clear this filter to include other captures."});
   var combo=new ComboBox{ItemsSource=options,SelectedItem=options.First(o=>o.Value==selected),MinHeight=34,MaxDropDownHeight=300,IsTextSearchEnabled=true};TextSearch.SetTextPath(combo,"Label");AutomationProperties.SetName(combo,field+" filter");
   var label=new FrameworkElementFactory(typeof(TextBlock));label.SetBinding(TextBlock.TextProperty,new Binding("Label"));label.SetBinding(TextBlock.ToolTipProperty,new Binding("Description"));label.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);label.SetValue(TextBlock.MaxWidthProperty,540.0);combo.ItemTemplate=new DataTemplate{VisualTree=label};combo.ToolTip=((FilterChoice)combo.SelectedItem).Description;
   combo.SelectionChanged+=(s,e)=>{var choice=combo.SelectedItem as FilterChoice;if(choice==null)return;if(choice.Value==null)criteria.Values.Remove(field);else criteria.Values[field]=choice.Value;combo.ToolTip=choice.Description;apply();};panel.Children.Add(combo);return panel;
  }
  FrameworkElement RangeSelector(string field,List<Frame> rows,CaptureFilters criteria,Action apply){
   CaptureRange range;if(!criteria.Ranges.TryGetValue(field,out range))range=new CaptureRange();
   var values=rows.Select(f=>CaptureFilters.Number(f,field)).Where(v=>v.HasValue).Select(v=>v.Value).ToList();if(range.Minimum.HasValue)values.Add(range.Minimum.Value);if(range.Maximum.HasValue)values.Add(range.Maximum.Value);values=values.Where(v=>!double.IsNaN(v)&&!double.IsInfinity(v)).Distinct().OrderBy(v=>v).ToList();
   var panel=new StackPanel{Margin=new Thickness(0,4,12,12)};panel.Children.Add(new TextBlock{Text=field=="Exposure"?"Exposure (seconds)":"Gain",Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,5)});
   var modes=new[]{"Any value","Known values only","Between limits","Unknown only"};var mode=new ComboBox{ItemsSource=modes,SelectedIndex=(int)range.Mode,MinHeight=34};panel.Children.Add(mode);
   Func<int,double?> number=index=>values.Count==0?(double?)null:values[Math.Max(0,Math.Min(values.Count-1,index))];
   int lower=range.Minimum.HasValue?values.FindIndex(v=>v>=range.Minimum.Value):0;if(lower<0)lower=Math.Max(0,values.Count-1);int upper=range.Maximum.HasValue?values.FindLastIndex(v=>v<=range.Maximum.Value):values.Count-1;upper=Math.Max(lower,upper);
   var from=new TextBlock{Margin=new Thickness(0,8,0,0)};panel.Children.Add(from);var minimum=new Slider{Minimum=0,Maximum=Math.Max(1,values.Count-1),Value=lower,TickFrequency=1,SmallChange=1,LargeChange=Math.Max(1,values.Count/10),IsSnapToTickEnabled=true,Height=22,ToolTip="Minimum; snaps to exposure/gain values present in these captures."};panel.Children.Add(minimum);
   var to=new TextBlock();panel.Children.Add(to);var maximum=new Slider{Minimum=0,Maximum=Math.Max(1,values.Count-1),Value=upper,TickFrequency=1,SmallChange=1,LargeChange=Math.Max(1,values.Count/10),IsSnapToTickEnabled=true,Height=22,ToolTip="Maximum; snaps to values present in these captures."};panel.Children.Add(maximum);
   var unknown=new CheckBox{Content="Include unknown values",IsChecked=range.IncludeUnknown,Margin=new Thickness(0,5,0,0)};panel.Children.Add(unknown);
   AutomationProperties.SetName(mode,field+" matching mode");AutomationProperties.SetName(minimum,field+" minimum");AutomationProperties.SetName(maximum,field+" maximum");
   Func<double?,string> text=value=>value.HasValue?value.Value.ToString("G6",CultureInfo.InvariantCulture)+(field=="Exposure"?" s":""):"No known values";
   bool silent=false;Action redraw=()=>{bool enabled=mode.SelectedIndex!=(int)NumericFilterMode.Unknown;minimum.IsEnabled=maximum.IsEnabled=enabled&&values.Count>1;unknown.IsEnabled=mode.SelectedIndex==(int)NumericFilterMode.Between;from.Text="From: "+text(number((int)minimum.Value));to.Text="To: "+text(number((int)maximum.Value));};
   Action commit=()=>{if(silent)return;range.Mode=(NumericFilterMode)mode.SelectedIndex;range.Minimum=number((int)minimum.Value);range.Maximum=number((int)maximum.Value);range.IncludeUnknown=unknown.IsChecked==true;if(range.Active)criteria.Ranges[field]=range;else criteria.Ranges.Remove(field);redraw();apply();};
   minimum.ValueChanged+=(s,e)=>{if(silent)return;silent=true;if(minimum.Value>maximum.Value)maximum.Value=minimum.Value;mode.SelectedIndex=(int)NumericFilterMode.Between;silent=false;commit();};
   maximum.ValueChanged+=(s,e)=>{if(silent)return;silent=true;if(maximum.Value<minimum.Value)minimum.Value=maximum.Value;mode.SelectedIndex=(int)NumericFilterMode.Between;silent=false;commit();};
   mode.SelectionChanged+=(s,e)=>commit();unknown.Checked+=(s,e)=>commit();unknown.Unchecked+=(s,e)=>commit();redraw();return panel;
  }
 }
}
