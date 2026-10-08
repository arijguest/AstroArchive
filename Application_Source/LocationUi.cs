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
   UiHelp.Tip(label,"Saved location used for sky views and mount analysis.");
   dialog.Button("Choose town / city…",()=>{var picker=new CityPicker(dialog.Window,choice.CityLabel);try{if(picker.Show()){choice.Select(picker.SelectedCity);label.Text=choice.Label;}}finally{picker.Dispose();}});
   dialog.Button("Clear observing location",()=>{choice.Clear();label.Text=choice.Label;});return choice;
  }
 }
 public sealed class CityPicker:IDisposable {
  readonly FormWindow dialog;readonly DispatcherTimer debounce;int generation;bool closed;
  public readonly TextBox SearchBox;public readonly ListBox Results;public readonly TextBlock Status;public ObservingCity SelectedCity{get{return Results.SelectedItem as ObservingCity;}}
  public Window Window{get{return dialog.Window;}}
  public CityPicker(Window owner,string currentLabel=null){
   dialog=new FormWindow(owner,"Choose observing town / city",730,600);dialog.Text("Observing town / city",true);
   dialog.Text("Search by name, region or country, then select the matching place. The list is available offline.");
   SearchBox=dialog.Input("Find a town or city","");UiHelp.Tip(SearchBox,"Enter a city and country or region.");
   Results=new ListBox{Height=270,HorizontalContentAlignment=HorizontalAlignment.Stretch,DisplayMemberPath="Label",Margin=new Thickness(0,8,0,8)};ScrollViewer.SetHorizontalScrollBarVisibility(Results,ScrollBarVisibility.Auto);dialog.Add(Results);
   UiHelp.Tip(Results,"Double-click a place to use it.");
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
