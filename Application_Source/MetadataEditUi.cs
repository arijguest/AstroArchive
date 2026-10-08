using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroArchive {
 public sealed class MetadataEditor {
  public readonly FormWindow Form;public readonly MetadataEditing Model;public readonly CheckBox Sessions;
  public readonly Dictionary<string,Control> Inputs=new Dictionary<string,Control>();
  readonly List<Action> resets=new List<Action>();readonly TextBlock summary;readonly int selectedCount;
  public MetadataEditor(Window owner,List<Frame> selected){
   selectedCount=selected.Count;Model=new MetadataEditing(selected);
   Form=new FormWindow(owner,"Edit metadata",780,740);
   Form.Text(selected.Count==1?selected[0].OriginalName:selected.Count+" selected files",true);
   Form.Text("Existing values are filled in. Mixed fields keep each file’s value until you enter a replacement. Blank fields keep existing values.");
   summary=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};Theme.Bind(summary,TextBlock.ForegroundProperty,"Muted");Form.Add(summary);
   Sessions=Form.Check("Apply changes to entire selected sessions",false);Sessions.Checked+=(s,e)=>UpdateSummary();Sessions.Unchecked+=(s,e)=>UpdateSummary();
   Form.Button("Reset changes",()=>{foreach(var reset in resets)reset();UpdateSummary();});
   Form.Tabs("Capture","Equipment","Processing","Current metadata");
   for(int tab=0;tab<3;tab++){
    Form.Tab(tab);var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());Form.Add(grid);
    var panels=new List<FrameworkElement>();
    foreach(var value in Model.Values.Where(v=>v.Field.Tab==tab)){var panel=Field(value,selected);panels.Add(panel);grid.Children.Add(panel);}
    Action arrange=()=>{bool pair=grid.ActualWidth>=570*owner.FontSize/14;int columns=pair?2:1;grid.RowDefinitions.Clear();for(int row=0;row<(panels.Count+columns-1)/columns;row++)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});for(int i=0;i<panels.Count;i++){Grid.SetRow(panels[i],i/columns);Grid.SetColumn(panels[i],i%columns);Grid.SetColumnSpan(panels[i],pair?1:2);panels[i].Margin=new Thickness(0,0,pair&&i%2==0?16:0,10);}};
    grid.SizeChanged+=(s,e)=>arrange();arrange();
   }
   Form.Tab(3);Form.Text("Inspect the recorded values and their sources for any selected file. This view does not change the batch edit fields.");
   var picker=Form.Select("File",selected.Select((f,i)=>(i+1)+" · "+f.OriginalName).ToArray(),"1 · "+selected[0].OriginalName);
   var details=new DataGrid{IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false,Height=360,SelectionMode=DataGridSelectionMode.Single};
   foreach(string name in new[]{"Field","Value","Source"}){var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding(name)));details.Columns.Add(new DataGridTextColumn{Header=name,Binding=new System.Windows.Data.Binding(name),ElementStyle=style,Width=new DataGridLength(name=="Value"?2:1,DataGridLengthUnitType.Star)});}
   details.ItemsSource=MetadataDetail.For(selected[0]);picker.SelectionChanged+=(s,e)=>{if(picker.SelectedIndex>=0)details.ItemsSource=MetadataDetail.For(selected[picker.SelectedIndex]);};Form.Add(details);
   Form.SelectTab(0);UpdateSummary();
   Form.Accept("Save metadata",()=>{string error=Model.Patch().Validate();if(error==null)return true;MessageBox.Show(Form.Window,error,"Check metadata",MessageBoxButton.OK,MessageBoxImage.Information);return false;});
  }
  FrameworkElement Field(MetadataEditValue value,List<Frame> selected){
   var panel=new StackPanel();var caption=new TextBlock{Text=value.Field.Label,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,5)};caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");panel.Children.Add(caption);
   Control input;
   if(value.Field.Options.Length>0){
    string initial=value.Initial.Length==0?value.KeepLabel:value.Initial;
    var options=new[]{value.KeepLabel}.Concat(value.Initial.Length==0?new string[0]:new[]{value.Initial}).Concat(value.Field.Options).Distinct().ToArray();
    var combo=new ComboBox{ItemsSource=options,SelectedItem=initial};input=combo;
    combo.SelectionChanged+=(s,e)=>{value.Text=Convert.ToString(combo.SelectedItem);UpdateSummary();};resets.Add(()=>combo.SelectedItem=initial);
   }else{
    var text=new TextBox{Text=value.Initial};input=text;
    if(value.Field.Key=="Notes"){text.AcceptsReturn=true;text.TextWrapping=TextWrapping.Wrap;text.MinHeight=70;}
    text.TextChanged+=(s,e)=>{value.Text=text.Text;UpdateSummary();};resets.Add(()=>text.Text=value.Initial);
   }
   input.Name="Metadata"+value.Field.Key;Inputs[value.Field.Key]=input;panel.Children.Add(input);
   string hint=value.Mixed?"Mixed · "+string.Join(" / ",value.Values.Take(3).Select(v=>v.Length==0?"not recorded":v))+(value.Values.Length>3?" / …":""):value.Initial.Length==0?"Not recorded":"";
   if(value.Field.Key=="Mount"&&!value.Mixed&&value.Initial.EndsWith("?"))hint="Suggested / inferred · select EQ or Alt-Az to confirm";
   UiHelp.Tip(input,value.Mixed?"Mixed values stay unchanged until replaced.":value.Field.Key=="Mount"?"A ? marks an inference. Blank keeps existing values.":"Blank keeps existing values.");
   if(hint.Length>0){var label=new TextBlock{Text=hint,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,4,0,0)};Theme.Bind(label,TextBlock.ForegroundProperty,"Muted");if(value.Mixed)UiHelp.Tip(label,value.Values.Length+" different values. See Current metadata.");panel.Children.Add(label);}
   return panel;
  }
  void UpdateSummary(){if(summary==null)return;int count=Model.Values.Count(v=>v.Changed);summary.Text=count==0?"No changes yet":count+" changed field"+(count==1?"":"s")+" · "+(Sessions.IsChecked==true?"all files in the selected sessions":selectedCount+" selected file"+(selectedCount==1?"":"s"));}
 }
 public partial class MainUi {
  void Edit(bool imported){
   if(repo==null)return;var selected=Context(imported);if(selected.Count==0)return;
   var editor=new MetadataEditor(Window,selected);if(!editor.Form.Show())return;
   MetadataPatch patch=editor.Model.Patch();if(patch.Count==0){L("StatusLabel").Text="No metadata changes.";return;}
   var items=selected;
   if(editor.Sessions.IsChecked==true){var ids=new HashSet<string>(selected.Where(f=>!string.IsNullOrEmpty(f.Session)).Select(f=>f.Session));items=(imported?plan.Frames:all).Where(f=>(ids.Contains(f.Session)||selected.Contains(f))&&(!imported||f.Status!="Deleted")).ToList();}
   Run(ct=>{
    // Validate all timezone conversions before any file is moved or saved.
    var updates=new List<Tuple<Frame,Frame>>();foreach(var original in items){ct.ThrowIfCancellationRequested();updates.Add(Tuple.Create(original,patch.Apply(original)));}
    foreach(var update in updates){ct.ThrowIfCancellationRequested();if(!PendingImport(update.Item1,imported))repo.Refile(update.Item2,ct);if(imported){int row=plan.Frames.IndexOf(update.Item1);if(row>=0)plan.Frames[row]=update.Item2;}}
    return items.Count+" captures updated · "+patch.Count+" changed fields.";
   },message=>{if(imported)FilterImports();L("StatusLabel").Text=message;});
  }
 }
}
