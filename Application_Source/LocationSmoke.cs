using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeObservingCity(){
   var picker=new CityPicker(Window);try{
    picker.Window.Show();PumpPopupLayout();
    var search=picker.SearchNow("Cambridge UK");while(!search.IsCompleted)PumpPopupLayout();search.GetAwaiter().GetResult();
    if(picker.Results.Items.Count==0||!picker.Results.Items.Cast<ObservingCity>().Any(c=>c.Id==2653941)||picker.SelectedCity!=null)throw new Exception("Offline city picker lost results or chose an ambiguous place automatically.");
    picker.Results.SelectedItem=picker.Results.Items.Cast<ObservingCity>().First(c=>c.Id==2653941);
    if(!picker.SelectedCity.Label.Contains("United Kingdom"))throw new Exception("Picker does not show the place's country.");
    picker.SearchBox.Text="Sydney Australia";if(picker.SelectedCity!=null)throw new Exception("New query retained an old selection.");
    var settingsForm=new FormWindow(Window,"Settings location smoke",650,500);try{AddObservingSite(settingsForm);settingsForm.CloseOnly();settingsForm.Window.Show();PumpPopupLayout();
     if(PopupChildren<TextBlock>(settingsForm.Window).Any(t=>t.Text=="Latitude"||t.Text=="Longitude"))throw new Exception("Manual coordinate fields remain in settings.");
    }finally{settingsForm.Window.Close();}
   }finally{picker.Window.Close();picker.Dispose();}
  }
 }
}
