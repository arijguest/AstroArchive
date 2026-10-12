using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AstroArchive {
 public partial class MainUi {
  ExportApplication SelectedApplication(ComboBox combo){var item=combo.SelectedItem as ComboBoxItem;return item==null?null:item.Tag as ExportApplication;}
  string LocateExportApplication(ExportApplication app,Window owner){
   string path=ExternalApps.Resolve(settings,app);if(path!=null)return path;
   var picker=new OpenFileDialog{Title="Locate "+app.Name+" — application could not be found automatically",Filter=app.Name+" application|*.exe",CheckFileExists=true};
   string previous=ExternalApps.Configured(settings,app);if(!string.IsNullOrWhiteSpace(previous))try{if(Directory.Exists(Path.GetDirectoryName(previous)))picker.InitialDirectory=Path.GetDirectoryName(previous);}catch(ArgumentException){}
   if(picker.ShowDialog(owner)!=true)return null;
   try{ExternalApps.ValidateExecutable(app,picker.FileName);ExternalApps.Configure(settings,app,picker.FileName);SaveSettings();return picker.FileName;}catch(Exception error){LocalizedMessage.Show(owner,error.Message,"Application location");return null;}
  }
  void RememberExport(ExternalSelection selection,ExportApplication app,bool remember){
   if(!remember)return;if(settings.ExportDefaults==null)settings.ExportDefaults=new Dictionary<string,string>();
   foreach(string key in selection.Subframes?new[]{"Subframe folders"}:selection.Paths.Select(ExternalApps.FileType).Distinct())settings.ExportDefaults[key]=app.Id;SaveSettings();
  }
  void ExportLaunchResult(ExportApplication app,string folder,string error,bool assisted){
   L("StatusLabel").Text=error!=null?"Files ready; "+app.Name+" could not open.":app.Name+" opened · "+folder;
   if(completionActivity!=null){completionActivity.OutputPath=folder;completionActivity.Status=L("StatusLabel").Text;if(error!=null){completionActivity.NeedsReview=true;completionActivity.ReportTitle="Processor handoff";completionActivity.Report=app.Name+" could not open the files: "+error+"\n\nVerified files remain in: "+folder;}else if(assisted){completionActivity.ReportTitle="Using the exported inputs";completionActivity.Report=app.Help;}}
  }
  void ChooseDssJob(string folder,List<DssJob> jobs,ExportApplication app,string executable){
   var d=new FormWindow(Window,"DSS capture groups",640,470);d.Text(jobs.Count+" separate capture groups are ready",true);d.Text("Each group has its own DSS file list and matching calibrations. Open a group to register and stack it separately. All lists are kept in the exported folder.");var combo=d.Select("Capture group",jobs.Select(j=>j.Name).ToArray(),jobs[0].Name);
   d.Button("Open selected group in DSS",()=>{try{ExternalApps.Launch(app,executable,new[]{jobs[combo.SelectedIndex].Path},folder,CancellationToken.None);L("StatusLabel").Text="DSS opened · "+jobs[combo.SelectedIndex].Name;}catch(Exception error){LocalizedMessage.Show(d.Window,error.Message+"\n\nYour file lists remain in:\n"+folder,"DSS unavailable");}});d.Button("Open exported folder",()=>OpenFolder(folder));d.CloseOnly();d.Show();
  }
  sealed class ExportPreferenceFields {
   public Dictionary<string,string> Paths,Defaults;public ComboBox Application;public TextBox Path;public Expander DefaultOptions,ApplicationOptions;public Action StorePath;
   public void Save(Settings draft){StorePath();foreach(var app in ExternalApps.All)ExternalApps.Configure(draft,app,Paths[app.Id]);draft.ExportDefaults=new Dictionary<string,string>(Defaults);}
  }
  ExportPreferenceFields AddExportPreferences(FormWindow d){
   var f=new ExportPreferenceFields{Paths=ExternalApps.All.ToDictionary(a=>a.Id,a=>ExternalApps.Configured(settings,a)??""),Defaults=settings.ExportDefaults==null?new Dictionary<string,string>():new Dictionary<string,string>(settings.ExportDefaults)};
   f.DefaultOptions=d.Advanced("Default applications",()=>{
    d.Text("Choose an app by file type. Mixed selections ask unless they share a compatible default.");
    foreach(string key in ExternalApps.FileTypes){string type=key;var eligible=ExternalApps.All.Where(a=>type=="Subframe folders"?a.FolderTypes.Length>0:a.ImageTypes.Contains(type)||a.FolderTypes.Contains(type)).ToList();string id;f.Defaults.TryGetValue(type,out id);var chosen=eligible.FirstOrDefault(a=>a.Id==id);var combo=d.Select(type,new[]{"Ask each time"}.Concat(eligible.Select(a=>a.Name)).ToArray(),chosen==null?"Ask each time":chosen.Name);combo.SelectionChanged+=(sender,args)=>{var app=eligible.FirstOrDefault(a=>a.Name==Convert.ToString(combo.SelectedItem));if(app==null)f.Defaults.Remove(type);else f.Defaults[type]=app.Id;};}
   });
   f.ApplicationOptions=d.Advanced("Application locations",()=>{
    d.Text("Leave the path empty for automatic detection. Export asks you to locate an app if it cannot be found.");
    f.Application=d.Select("Application",ExternalApps.All.Select(a=>a.Name).ToArray(),"Siril");f.Path=d.Input("Executable path (optional)","");var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,12)};Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");d.Add(status);string current=null;
    f.StorePath=()=>{if(current!=null)f.Paths[current]=f.Path.Text.Trim();};Action load=()=>{f.StorePath();var app=ExternalApps.All.First(a=>a.Name==Convert.ToString(f.Application.SelectedItem));current=app.Id;f.Path.Text=f.Paths[current];status.Text=f.Path.Text.Length==0?"Automatic detection.":File.Exists(f.Path.Text)?"Configured location.":"Unavailable. Export will ask you to locate the app.";};f.Application.SelectionChanged+=(sender,args)=>load();load();
    d.Button("Browse…",()=>{var app=ExternalApps.Find(current);var picker=new OpenFileDialog{Title="Locate "+app.Name,Filter="Applications|*.exe",CheckFileExists=true};if(picker.ShowDialog(d.Window)==true)try{ExternalApps.ValidateExecutable(app,picker.FileName);f.Path.Text=picker.FileName;status.Text="Configured location.";}catch(Exception error){LocalizedMessage.Show(d.Window,error.Message);}});
    d.Button("Find automatically",()=>{string detected=ExternalApps.Detect(ExternalApps.Find(current));if(detected==null){status.Text="Not found. Browse to the application's GUI executable.";return;}f.Path.Text=detected;status.Text="Found: "+detected;});
    d.Button("Use automatic detection",()=>{f.Path.Clear();status.Text="Automatic detection will be used when exporting.";});
   });return f;
  }
  void ValidateExportPreferences(FormWindow d,ExportPreferenceFields f){
   f.StorePath();foreach(var app in ExternalApps.All){string value=f.Paths[app.Id];if(value.Length>0&&value!=(ExternalApps.Configured(settings,app)??""))try{ExternalApps.ValidateExecutable(app,value);}catch(Exception error){d.SelectTab(2);f.ApplicationOptions.IsExpanded=true;f.Application.SelectedItem=app.Name;throw new IOException(app.Name+": "+error.Message,error);}}
  }
  void ExportDestinationSettings(int tab=0,string appId=null,Window owner=null){
   if(RepositoryOperationBlocked)return;PreferenceFields fields;var dialog=PreferencesDialog(owner??Window,2,out fields);if(tab==1){fields.Exports.ApplicationOptions.IsExpanded=true;fields.Exports.Application.SelectedItem=(ExternalApps.Find(appId)??ExternalApps.All[0]).Name;}else fields.Exports.DefaultOptions.IsExpanded=true;dialog.Show();
  }
 }
}
