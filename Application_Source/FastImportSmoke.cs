using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeConnectedImport(string output){
   var originalSettings=settings;var originalRepo=repo;var originalAll=all;var originalUsb=usbTelescopes;var originalPending=pendingUsb;var originalPlan=plan;string originalStatus=L("UsbStatusLabel").Text;string source=T("SourceBox").Text,id=T("TelescopeBox").Text,model=Convert.ToString(C("ModelBox").SelectedItem),camera=Convert.ToString(C("CameraBox").SelectedItem);int tab=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   string path=Path.Combine(Path.GetTempPath(),"AstroArchive-fast-import-ui-"+Guid.NewGuid().ToString("N"));Repository temporary=null;
   try{
    temporary=new Repository(path);repo=temporary;settings=new Settings{Telescopes=new List<TelescopeProfile>{new TelescopeProfile{Id="My Seestar",Model="Seestar S50"},new TelescopeProfile{Id="My DWARF",Model="Dwarf 3"}}};TelescopeProfiles.Initialize(settings);pendingUsb=null;ReloadScopes("My DWARF",true);
    usbTelescopes=new List<UsbTelescope>{new UsbTelescope{Make="Seestar",Source=@"E:\MyWorks",Volume=new UsbVolume{Root=@"E:\",Id="ui-device"}}};SelectConnectedTelescope();UpdateTelescopeState(false);GoToPage(1);PumpPopupLayout();
    if(SelectedScope.Id!="My Seestar"||T("SourceBox").Text!=@"E:\MyWorks"||!B("AutoUploadButton").IsVisible||!B("AutoUploadButton").IsEnabled)throw new Exception("Connected import did not select a matching profile and show its one-click action.");
    L("UsbStatusLabel").Text="Seestar storage detected · My Seestar selected automatically";
    foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(Window,theme);PumpPopupLayout();Capture(Path.Combine(output,"AstroArchive_Connected_Import_"+theme+".png"));}
    var form=new FormWindow(Window,"Import matching smoke",580,400);var robust=ImportMatchingChoice(form);form.Window.Show();PumpPopupLayout();if(robust.IsChecked==true||!robust.IsVisible)throw new Exception("Robust matching is not visible or defaults on.");form.Window.Close();
    usbTelescopes=new List<UsbTelescope>{new UsbTelescope{Make="DWARFLAB",Source=@"F:\DWARF",Volume=new UsbVolume{Root=@"F:\",Id="ui-dwarf"}}};SelectConnectedTelescope();UpdateTelescopeState(false);PumpPopupLayout();if(SelectedScope.Id!="My DWARF")throw new Exception("A connected DWARF did not select its matching saved telescope.");
    settings.Telescopes.RemoveAll(p=>p.Id=="My DWARF");ReloadScopes("My Seestar",true);UploadUsb(usbTelescopes[0]);if(SelectedScope.Id!=null||pendingUsb!=usbTelescopes[0]||T("SourceBox").Text!=@"F:\DWARF"||Convert.ToString(C("ModelBox").SelectedItem)!="Auto")throw new Exception("Unconfigured DWARF reused an incompatible selected telescope instead of requiring setup.");
    File.WriteAllText(Path.Combine(output,"fast-import-smoke.txt"),"PASS: visible one-click connected import, automatic Seestar/DWARF profile selection, setup required when no matching telescope exists, robust matching off by default, light/dark rendering.");
   }finally{settings=originalSettings;repo=originalRepo;all=originalAll;usbTelescopes=originalUsb;pendingUsb=originalPending;ReloadScopes(settings.SelectedTelescope,false);T("SourceBox").Text=source;T("TelescopeBox").Text=id;C("ModelBox").SelectedItem=model;C("CameraBox").SelectedItem=camera;plan=originalPlan;UpdateTelescopeState(false);L("UsbStatusLabel").Text=originalStatus;Theme.Apply(Window,settings.ThemeMode);((TabControl)Window.FindName("MainTabs")).SelectedIndex=tab;if(temporary!=null)temporary.Dispose();if(Directory.Exists(path))Directory.Delete(path,true);}
  }
 }
}
