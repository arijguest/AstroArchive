// A mixed folder/file selection basket backed by Windows pickers, scoped to USB storage.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
namespace AstroArchive {
 internal sealed class TelescopeImportPicker {
  internal readonly FormWindow Dialog;internal readonly ListBox Items;internal readonly ComboBox Profiles,Model;internal readonly TextBox Name;
  internal readonly Button FolderButton,FilesButton,WholeDriveButton,PreferencesButton;internal readonly TextBlock Error;
  internal ImportSelection Selection;internal TelescopeProfile Profile;
  readonly UsbTelescope device;readonly List<TelescopeProfile> saved;readonly List<string> paths=new List<string>();
  public TelescopeImportPicker(Window owner,UsbTelescope device,IEnumerable<TelescopeProfile> profiles,TelescopeProfile preferred,Func<string> pickFolder=null,Func<string[]> pickFiles=null,Action<Window> importPreferences=null){
   this.device=device;saved=profiles.ToList();string brand=ImportSelection.TelescopeBrand(device.Make);
   Dialog=new FormWindow(owner,"Import from "+brand,740,720);
   Dialog.Text("Choose folders or files on your telescope",true);
   Dialog.Text("Choose the captures you need; folders include subfolders. Originals stay on the telescope.");
   Dialog.Text("Telescope drive: "+device.Volume.Root);
   var choices=new List<TelescopeProfile>{new TelescopeProfile{Model="Auto"}};choices.AddRange(saved.Where(p=>TelescopeProfiles.Make(p)==device.Make||preferred!=null&&p.Id==preferred.Id));
   Profiles=new ComboBox{ItemsSource=choices,DisplayMemberPath="DisplayText",Margin=new Thickness(0,0,0,8)};System.Windows.Automation.AutomationProperties.SetName(Profiles,"Telescope for this import");UiHelp.Hint(Profiles,"Choose the physical telescope, or give a new telescope its own name.");Dialog.Add(Profiles);
   Name=Dialog.Input("Physical telescope name","");Model=Dialog.Select("Telescope model",TelescopeProfiles.Models.Where(m=>m=="Auto"||m=="Other"||InstrumentDetection.MakeOf(m)==device.Make).ToArray(),"Auto");
   Items=new ListBox{SelectionMode=SelectionMode.Extended,MinHeight=120,MaxHeight=200,Margin=new Thickness(0,12,0,8)};System.Windows.Automation.AutomationProperties.SetName(Items,"Selected telescope folders and files");Dialog.Add(Items);
   var row=new WrapPanel{Margin=new Thickness(0,0,0,8)};Dialog.Add(row);
   FolderButton=ChoiceButton(row,"Add folder…",()=>{try{string selected=pickFolder!=null?pickFolder():NativeFolderPicker.Select(new WindowInteropHelper(Dialog.Window).EnsureHandle(),"Choose a folder on "+brand,InitialFolder());if(selected!=null)Add(new[]{selected});}catch(Exception e){Error.Text=e.Message;}});
   FilesButton=ChoiceButton(row,"Add files…",()=>{try{
    string[] selected;if(pickFiles!=null)selected=pickFiles();else{var picker=new OpenFileDialog{Title="Choose captures on "+brand,InitialDirectory=InitialFolder(),Multiselect=true,CheckFileExists=true,RestoreDirectory=true,Filter="Capture files|*.fit;*.fits;*.fts;*.fit.gz;*.fits.gz;*.fts.gz;*.xisf;*.ser;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.gif;*.bmp;*.avi;*.mp4;*.mov|All files|*.*"};selected=picker.ShowDialog(Dialog.Window)==true?picker.FileNames:null;}if(selected!=null&&selected.Length>0)Add(selected);
   }catch(Exception e){Error.Text=e.Message;}});
   ChoiceButton(row,"Remove selected",()=>{foreach(string path in Items.SelectedItems.Cast<string>().ToList())paths.Remove(path);Selection=null;Refresh();});
   WholeDriveButton=ChoiceButton(row,"Entire telescope drive",()=>{try{var selection=ImportSelection.Create(device.Volume.Root,new[]{device.Volume.Root});paths.Clear();paths.AddRange(selection.Paths);Selection=selection;Refresh();}catch(Exception e){Error.Text=e.Message;}});
   Error=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};Dialog.Add(Error);
   Profiles.SelectionChanged+=(s,e)=>{var profile=Profiles.SelectedItem as TelescopeProfile;bool existing=profile!=null&&!string.IsNullOrEmpty(profile.Id);Name.Text=existing?profile.Id:"";Name.IsReadOnly=existing;Model.SelectedItem=existing?TelescopeProfiles.Model(profile.Model):"Auto";Model.IsEnabled=!existing;Selection=null;
    if(paths.Count==0&&existing&&profile.LastImportSelection!=null&&string.Equals(profile.VolumeId,device.Volume.Id,StringComparison.OrdinalIgnoreCase))foreach(string relative in profile.LastImportSelection){try{var previous=ImportSelection.Create(device.Volume.Root,new[]{Path.Combine(device.Volume.Root,relative)});paths.AddRange(previous.Paths);}catch(Exception){Error.Text="Some previously selected items are unavailable. Review the selection before importing.";}}
    Refresh();
   };
   Profiles.SelectedItem=choices.FirstOrDefault(p=>preferred!=null&&p.Id==preferred.Id)??choices[0];
   PreferencesButton=Dialog.FooterButton("Import preferences…",()=>{if(importPreferences!=null)importPreferences(Dialog.Window);});PreferencesButton.IsEnabled=importPreferences!=null;
   Dialog.Accept("Import",Validate);
  }
  Button ChoiceButton(WrapPanel row,string text,Action action){var button=new Button{Content=text,Margin=new Thickness(0,0,8,6)};UiHelp.For(button,text);button.Click+=(s,e)=>action();row.Children.Add(button);return button;}
  string InitialFolder(){return Directory.Exists(device.Source)&&Util.Within(device.Source,device.Volume.Root)?device.Source:device.Volume.Root;}
  void Refresh(){Items.ItemsSource=paths.ToArray();}
  void Add(IEnumerable<string> incoming){var selection=ImportSelection.Create(device.Volume.Root,paths.Concat(incoming));paths.Clear();paths.AddRange(selection.Paths);Selection=selection;Error.Text="";Refresh();}
  internal bool Validate(){try{
   Selection=ImportSelection.Create(device.Volume.Root,paths);string id=Name.Text.Trim();if(id.Length==0)throw new IOException("Give this physical telescope a name before importing.");
   var selected=Profiles.SelectedItem as TelescopeProfile;bool existing=selected!=null&&!string.IsNullOrEmpty(selected.Id);
   if(!existing&&saved.Any(p=>string.Equals(p.Id,id,StringComparison.OrdinalIgnoreCase)))throw new IOException("That telescope name is already saved. Choose its profile or enter a different name.");
   Profile=existing?Util.Deserialize<TelescopeProfile>(Util.Serialize(selected)):new TelescopeProfile{Id=id,SessionIdentity=id,Model=Convert.ToString(Model.SelectedItem),Camera="Auto"};Error.Text="";return true;
  }catch(Exception e){Error.Text=e.Message;return false;}}
  public bool Show(){return Dialog.Show();}
 }
 internal static class TelescopeImportConfirmation {
  internal static FormWindow Create(Window owner,ImportSelection selection,string make,Action<Window> importPreferences=null){
   var dialog=new FormWindow(owner,"Large telescope import",580,450);
   dialog.Text("Import from "+ImportSelection.TelescopeBrand(make)+"?",true);
   dialog.Text(selection.Folders.Count==0?selection.Files.Count.ToString("N0")+" capture files selected":"At least "+ImportSelection.LargeFileSelection.ToString("N0")+" capture files selected",true);
   if(selection.Folders.Count>0)dialog.Text("Selection: "+selection.Summary+".");
   dialog.Text("Large imports can take a while. Choose fewer folders or files for a quicker import.");
   dialog.Text("• Scan, check and copy eligible captures.\n• Keep originals on the telescope.\n• Cancel anytime; completed copies are retained.");
   var preferences=dialog.FooterButton("Import preferences…",()=>{if(importPreferences!=null)importPreferences(dialog.Window);});preferences.IsEnabled=importPreferences!=null;
   dialog.Accept("Import",()=>true);return dialog;
  }
 }
}
