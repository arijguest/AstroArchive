using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  ObservingSiteChoice AddObservingSite(FormWindow dialog){
   var choice=new ObservingSiteChoice(settings);dialog.Text("Observing town / city",true);
   dialog.Text("Select your nearest town or city. Its centre is used for rotation analysis; without a selection, capture location metadata is used when available.");
   var label=dialog.Input("Town / city",choice.Label);label.IsReadOnly=true;
   UiHelp.Hint(label,"Saved location used for sky views and mount analysis.");
   dialog.Button("Choose town / city…",()=>{var picker=new CityPicker(dialog.Window,choice.CityLabel);try{if(picker.Show()){choice.Select(picker.SelectedCity);label.Text=choice.Label;}}finally{picker.Dispose();}});
   dialog.Button("Clear observing location",()=>{choice.Clear();label.Text=choice.Label;});return choice;
  }
  // A chosen town/city replaces recorded capture coordinates; clearing it returns
  // each file to its metadata location, then the observing town in Settings.
  sealed class CaptureSiteEdit {public ObservingCity City;public bool UseDefault;}
  CaptureSiteEdit ChooseCaptureSite(List<Frame> selected){
   var dialog=new FormWindow(Window,"Capture town / city",650,480);var edit=new CaptureSiteEdit();
   dialog.Text(selected.Count==1?selected[0].OriginalName:selected.Count+" selected files",true);
   dialog.Text("Choose where these files were captured. A chosen town or city replaces any capture location recorded in the files. Without a choice, the recorded location is used, then your observing town / city from Settings.");
   var current=selected.Select(f=>CaptureSite.Describe(f,settings)).Distinct().ToList();
   var label=dialog.Input("Town / city",current.Count==1?current[0]:"Mixed");label.IsReadOnly=true;UiHelp.Hint(label,"Location used for sky views, Stellarium and eclipse times.");
   dialog.Button("Choose town / city…",()=>{var picker=new CityPicker(dialog.Window,edit.City==null?selected.Select(f=>f.CaptureCity).FirstOrDefault(c=>!string.IsNullOrEmpty(c)):edit.City.Label);try{if(picker.Show()){edit.City=picker.SelectedCity;edit.UseDefault=false;label.Text=edit.City.Label;}}finally{picker.Dispose();}});
   var reset=dialog.Button("Use default location",()=>{edit.City=null;edit.UseDefault=true;label.Text="Default location";});reset.IsEnabled=selected.Any(CaptureSite.HasChoice);
   dialog.Accept("Save location",()=>true);
   return dialog.Show()&&(edit.City!=null||edit.UseDefault)?edit:null;
  }
  async void SetCaptureSite(bool imported,List<Frame> selected){
   if(repo==null||RepositoryOperationBlocked||metadataPreviewSuspended||SearchBlocked(imported?"ImportSearchBox":"SearchBox"))return;selected=selected.Where(f=>f.Status!="Deleted").ToList();if(selected.Count==0)return;var repository=repo;
   try{
    await SuspendMetadataPreviews();if(closing||repo!=repository||RepositoryOperationBlocked)return;
    var edit=ChooseCaptureSite(selected);if(edit==null)return;
    await RunOperation(ct=>{
     foreach(var original in selected){ct.ThrowIfCancellationRequested();var updated=original.Clone();CaptureSite.Apply(updated,edit.City);if(!PendingImport(original,imported))repo.Refile(updated,ct);if(imported){int row=plan.Frames.IndexOf(original);if(row>=0)plan.Frames[row]=updated;}}
     return selected.Count+" captures updated · "+(edit.City==null?"default location":edit.City.Name)+".";
    },message=>{if(imported)FilterImports();RefreshSkyPreviews();L("StatusLabel").Text=message;});
   }finally{ResumeMetadataPreviews();}
  }
 }
 public sealed class CityPicker:IDisposable {
  readonly FormWindow dialog;readonly DispatcherTimer debounce;int generation;bool closed;
  public readonly TextBox SearchBox;public readonly ListBox Results;public readonly TextBlock Status;public ObservingCity SelectedCity{get{return Results.SelectedItem as ObservingCity;}}
  public Window Window{get{return dialog.Window;}}
  public CityPicker(Window owner,string currentLabel=null){
   dialog=new FormWindow(owner,"Choose observing town / city",730,600);dialog.Text("Observing town / city",true);
   dialog.Text("Search by name, region or country, then select the matching place. The list is available offline.");
   SearchBox=dialog.Input("Find a town or city","");UiHelp.Hint(SearchBox,"Enter a city and country or region.");
   Results=new ListBox{Height=270,HorizontalContentAlignment=HorizontalAlignment.Stretch,DisplayMemberPath="Label",Margin=new Thickness(0,8,0,8)};ScrollViewer.SetHorizontalScrollBarVisibility(Results,ScrollBarVisibility.Auto);dialog.Add(Results);
   UiHelp.Hint(Results,"Double-click a place to use it.");
   Status=new TextBlock{Text="Type at least two characters to find a place.",TextWrapping=TextWrapping.Wrap};Theme.Bind(Status,TextBlock.ForegroundProperty,"Muted");dialog.Add(Status);
   dialog.Text("Place names and coordinates: GeoNames · CC BY 4.0. Covers towns/cities with at least 500 inhabitants and administrative seats worldwide.");
   dialog.Accept("Use town / city",()=>{if(SelectedCity!=null)return true;Status.Text="Select a town or city from the results first.";Results.Focus();return false;});
   debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(220)};debounce.Tick+=async(s,e)=>{debounce.Stop();await RefreshResults();};
   SearchBox.TextChanged+=(s,e)=>{generation++;Results.ItemsSource=null;debounce.Stop();if(ObservingCities.Key(SearchBox.Text).Length<2){Status.Text="Type at least two characters to find a place.";return;}Status.Text="Searching…";debounce.Start();};
   SearchBox.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Down&&Results.Items.Count>0){Results.SelectedIndex=0;Results.Focus();e.Handled=true;}};
   Results.MouseDoubleClick+=(s,e)=>{if(SelectedCity!=null)dialog.Window.DialogResult=true;};
   dialog.Window.Closed+=(s,e)=>Dispose();dialog.Window.Loaded+=(s,e)=>{SearchBox.Focus();SearchBox.SelectAll();};
   if(!string.IsNullOrEmpty(currentLabel))SearchBox.Text=currentLabel;
  }
  public async Task SearchNow(string query){SearchBox.Text=query;debounce.Stop();await RefreshResults();}
  async Task RefreshResults(){
   int request=++generation;string query=SearchBox.Text;if(ObservingCities.Key(query).Length<2)return;
   try{var places=await Task.Run(()=>ObservingCities.Search(query));if(closed||request!=generation)return;
    Results.ItemsSource=places;Status.Text=places.Count==0?"No matches. Try a nearby town/city or a different spelling.":places.Count>=80?"Showing the first 80 matches. Add a region or country to narrow the search.":places.Count+" matching place"+(places.Count==1?"":"s")+". Select the correct region and country.";
   }catch(Exception error){if(!closed&&request==generation)Status.Text="The offline town/city list could not be read: "+error.Message;}
  }
  public bool Show(){return dialog.Show();}
  public void Dispose(){closed=true;generation++;debounce.Stop();}
 }
}
