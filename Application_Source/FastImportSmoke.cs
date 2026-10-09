using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  public void SmokeConnectedImport(string output){
   Directory.CreateDirectory(output);Window.Show();PumpPopupLayout();
   var originalSettings=settings;var originalRepo=repo;var originalAll=all;var originalUsb=usbTelescopes;var originalPending=pendingUsb;var originalPlan=plan;string originalStatus=L("UsbStatusLabel").Text;string source=T("SourceBox").Text,id=T("TelescopeBox").Text,model=Convert.ToString(C("ModelBox").SelectedItem),camera=Convert.ToString(C("CameraBox").SelectedItem);int tab=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   string path=Path.Combine(Path.GetTempPath(),"AstroArchive-fast-import-ui-"+Guid.NewGuid().ToString("N"));Repository temporary=null;
   try{
    temporary=new Repository(path);repo=temporary;settings=new Settings{Telescopes=new List<TelescopeProfile>{new TelescopeProfile{Id="My Seestar",Model="Seestar S50"},new TelescopeProfile{Id="My DWARF",Model="Dwarf 3"}}};TelescopeProfiles.Initialize(settings);pendingUsb=null;ReloadScopes("My DWARF",true);
    usbTelescopes=new List<UsbTelescope>{new UsbTelescope{Make="Seestar",Source=@"E:\MyWorks",Volume=new UsbVolume{Root=@"E:\",Id="ui-device"}}};SelectConnectedTelescope();UpdateTelescopeState(false);GoToPage(1);PumpPopupLayout();
    if(SelectedScope.Id!="My Seestar"||T("SourceBox").Text!=@"E:\MyWorks"||!B("AutoUploadButton").IsVisible||!B("AutoUploadButton").IsEnabled||Convert.ToString(B("AutoUploadButton").Content)!="Import from Seestar…")throw new Exception("Connected import did not select a matching profile and show its branded action.");
    var importMenu=new MenuItem();BuildImportNavigation(importMenu);if(!importMenu.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header)=="Import from Seestar…"&&m.IsEnabled))throw new Exception("Seestar selection action is missing from the Import menu.");
    L("UsbStatusLabel").Text="Seestar storage detected · My Seestar selected automatically";
    foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(Window,theme);PumpPopupLayout();Capture(Path.Combine(output,"AstroArchive_Connected_Import_"+theme+".png"));}
    var form=new FormWindow(Window,"Import matching smoke",580,400);var robust=ImportMatchingChoice(form);form.Window.Show();PumpPopupLayout();if(robust.IsChecked==true||!robust.IsVisible)throw new Exception("Robust matching is not visible or defaults on.");form.Window.Close();
    usbTelescopes=new List<UsbTelescope>{new UsbTelescope{Make="DWARFLAB",Source=@"F:\DWARF",Volume=new UsbVolume{Root=@"F:\",Id="ui-dwarf"}}};SelectConnectedTelescope();UpdateTelescopeState(false);PumpPopupLayout();if(SelectedScope.Id!="My DWARF"||Convert.ToString(B("AutoUploadButton").Content)!="Import from Dwarflab…")throw new Exception("A connected DWARF did not select its matching saved telescope or brand.");
    importMenu=new MenuItem();BuildImportNavigation(importMenu);if(!importMenu.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header)=="Import from Dwarflab…"))throw new Exception("Dwarflab selection action is missing from the Import menu.");
    string card=path+"-card";Directory.CreateDirectory(card);try{
     string folder=Path.Combine(card,"MyWorks","M45_sub"),file=Path.Combine(card,"single.fit"),outside=path+"-outside.fit";Directory.CreateDirectory(folder);File.WriteAllText(file,"");File.WriteAllText(outside,"");
     try{
      var device=new UsbTelescope{Make="Seestar",Source=card,Volume=new UsbVolume{Root=card,Id="picker-device"}};bool chooseOutside=false;var picker=new TelescopeImportPicker(Window,device,settings.Telescopes,settings.Telescopes.First(p=>p.Id=="My Seestar"),()=>folder,()=>new[]{chooseOutside?outside:file},ImportPreferencesFromDialog);
      picker.Dialog.Window.Show();PumpPopupLayout();picker.FolderButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));picker.FilesButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!picker.Validate()||picker.Selection.Folders.Count!=1||picker.Selection.Files.Count!=1)throw new Exception("Telescope picker did not combine selected folders and files.");
      var selectedPaths=picker.Selection.Paths.ToArray();bool wasPicking=usbImportPicking;usbImportPicking=true;try{SmokeImportPreferencesButton(picker.Dialog);}finally{usbImportPicking=wasPicking;}if(!picker.Validate()||!picker.Selection.Paths.SequenceEqual(selectedPaths))throw new Exception("Import preferences lost the telescope selection.");
      chooseOutside=true;picker.FilesButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(picker.Items.Items.Count!=2||picker.Error.Text.Length==0)throw new Exception("Telescope picker accepted a capture from outside its drive.");
      foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(picker.Dialog.Window,theme);PumpPopupLayout();CapturePopup(picker.Dialog.Window,Path.Combine(output,"AstroArchive_Telescope_Picker_"+theme+".png"));}
      picker.WholeDriveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!picker.Validate()||!picker.Selection.WholeSource)throw new Exception("Whole telescope selection failed.");picker.Dialog.Window.Close();
      var confirmation=TelescopeImportConfirmation.Create(Window,picker.Selection,"Seestar",ImportPreferencesFromDialog);try{
       confirmation.Window.Show();SmokeImportPreferencesButton(confirmation);
       foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(confirmation.Window,theme);PumpPopupLayout();foreach(var button in PopupChildren<Button>(confirmation.Window).Where(b=>Convert.ToString(b.Content)=="Import"||Convert.ToString(b.Content)=="Cancel"||Convert.ToString(b.Content)=="Import preferences…"))if(!button.IsVisible||button.ActualWidth+button.Margin.Left+button.Margin.Right<button.DesiredSize.Width-0.5)throw new Exception("Telescope confirmation actions clipped.");if(!PopupChildren<TextBlock>(confirmation.Window).Any(t=>t.Text.Contains("500"))||!PopupChildren<TextBlock>(confirmation.Window).Any(t=>t.Text.Contains("Keep originals")))throw new Exception("Large import summary missing.");CapturePopup(confirmation.Window,Path.Combine(output,"AstroArchive_Telescope_Confirmation_"+theme+".png"));}
      }finally{confirmation.Window.Close();}
      settings.Telescopes.RemoveAll(p=>p.Id=="My DWARF");var dwarf=new UsbTelescope{Make="DWARFLAB",Source=card,Volume=device.Volume};var setup=new TelescopeImportPicker(Window,dwarf,settings.Telescopes,null,()=>folder,importPreferences:ImportPreferencesFromDialog);setup.Dialog.Window.Show();SmokeImportPreferencesButton(setup.Dialog);
      setup.FolderButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(setup.Validate())throw new Exception("New physical telescope did not require its own name.");setup.Name.Text="My Dwarflab";if(!setup.Validate()||setup.Profile.Id!="My Dwarflab")throw new Exception("New physical telescope could not be configured in the picker.");setup.Dialog.Window.Close();
     }finally{File.Delete(outside);}
    }finally{Directory.Delete(card,true);}
    File.WriteAllText(Path.Combine(output,"fast-import-smoke.txt"),"PASS: branded USB import menu/button, compatible profile selection, mixed folder/file picker, drive containment, new telescope setup, whole-drive choice, robust matching off by default, light/dark rendering.");
   }finally{settings=originalSettings;repo=originalRepo;all=originalAll;usbTelescopes=originalUsb;pendingUsb=originalPending;ReloadScopes(settings.SelectedTelescope,false);T("SourceBox").Text=source;T("TelescopeBox").Text=id;C("ModelBox").SelectedItem=model;C("CameraBox").SelectedItem=camera;plan=originalPlan;UpdateTelescopeState(false);L("UsbStatusLabel").Text=originalStatus;Theme.Apply(Window,settings.ThemeMode);((TabControl)Window.FindName("MainTabs")).SelectedIndex=tab;if(temporary!=null)temporary.Dispose();if(Directory.Exists(path))Directory.Delete(path,true);}
  }
  void SmokeImportPreferencesButton(FormWindow parent){
   PumpPopupLayout();var button=PopupChildren<Button>(parent.Window).Single(b=>Convert.ToString(b.Content)=="Import preferences…");if(!button.IsEnabled||!button.IsVisible)throw new Exception("Import preferences action is unavailable.");
   Exception failure=null;bool opened=false;
   Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{
    Window preferences=null;try{
     preferences=parent.Window.OwnedWindows.Cast<Window>().Single(w=>w.Title=="Preferences");opened=true;preferences.UpdateLayout();
     var sections=PopupChildren<ListBox>(preferences).Single(list=>System.Windows.Automation.AutomationProperties.GetName(list)=="Preferences sections");if(sections.SelectedIndex!=1||preferences.Owner!=parent.Window)throw new Exception("Import preferences opened the wrong page or owner.");
     if(PopupChildren<Button>(preferences).Single(b=>Convert.ToString(b.Content)=="Choose repository…").IsEnabled)throw new Exception("Import preferences allows changing the picker repository.");
    }catch(Exception e){failure=e;}finally{if(preferences!=null)preferences.Close();}
   }));
   button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(failure!=null)throw new Exception("Import dialog preferences failed.",failure);if(!opened||!parent.Window.IsVisible)throw new Exception("Import preferences was blocked or dismissed its import dialog.");
  }
 }
}
