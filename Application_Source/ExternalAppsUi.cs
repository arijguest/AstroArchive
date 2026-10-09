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
  sealed class ExportToFields {public ComboBox Application;public CheckBox Remember,Calibration,Unknown;public ExportDestinationFields Destination;public string Executable;}
  ExportApplication SelectedApplication(ComboBox combo){var item=combo.SelectedItem as ComboBoxItem;return item==null?null:item.Tag as ExportApplication;}
  string LocateExportApplication(ExportApplication app,Window owner){
   string path=ExternalApps.Resolve(settings,app);if(path!=null)return path;
   var picker=new OpenFileDialog{Title="Locate "+app.Name+" — application could not be found automatically",Filter=app.Name+" application|*.exe",CheckFileExists=true};
   string previous=ExternalApps.Configured(settings,app);if(!string.IsNullOrWhiteSpace(previous))try{if(Directory.Exists(Path.GetDirectoryName(previous)))picker.InitialDirectory=Path.GetDirectoryName(previous);}catch(ArgumentException){}
   if(picker.ShowDialog(owner)!=true)return null;
   try{ExternalApps.ValidateExecutable(app,picker.FileName);ExternalApps.Configure(settings,app,picker.FileName);SaveSettings();return picker.FileName;}catch(Exception error){MessageBox.Show(owner,error.Message,"Application location");return null;}
  }
  FormWindow ExportToDialog(ExternalSelection selection,out ExportToFields fields,bool fromEdited=false){
   var d=new FormWindow(Window,"Export to…",650,selection.Folder?730:470);fields=new ExportToFields();var controls=fields;
   d.Text(selection.Paths.Count+" selected "+(selection.Folder?"stacking input":"image")+(selection.Paths.Count==1?"":"s"),true);
   d.Text(selection.Folder?"Copies and verifies original files before opening your chosen app.":fromEdited?"Opens the selected Edited images in your chosen app. Save results in their image folders.":"Creates verified working copies in Edited, then opens them in your chosen app. Save processed results alongside the copies.");
   var combo=new ComboBox();fields.Application=combo;combo.Items.Add(new ComboBoxItem{Content="Choose an application…"});
   string preferred=ExternalApps.Default(settings,selection);foreach(var app in ExternalApps.All){string problem=ExternalApps.Problem(app,selection);var item=new ComboBoxItem{Content=app.Name,Tag=app,IsEnabled=problem==null,ToolTip=problem};ToolTipService.SetShowOnDisabled(item,true);combo.Items.Add(item);if(preferred==app.Id)combo.SelectedItem=item;}
   if(combo.SelectedItem==null)combo.SelectedIndex=0;d.Add(combo);
   var help=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,12)};Theme.Bind(help,TextBlock.ForegroundProperty,"Muted");d.Add(help);
   Action summary=()=>{var app=SelectedApplication(combo);help.Text=app==null?(ExternalApps.All.Any(a=>ExternalApps.Problem(a,selection)==null)?"Choose a compatible destination. Unavailable destinations below explain their restrictions.":"No destination supports this selection. Save files… can copy the originals; convert to a supported format or select fewer images to open them in an app."):app.Help;};combo.SelectionChanged+=(s,e)=>summary();summary();
   var unavailable=ExternalApps.All.Where(a=>ExternalApps.Problem(a,selection)!=null).ToList();if(unavailable.Count>0)d.Options("Unavailable destinations ("+unavailable.Count+")",()=>{foreach(var app in unavailable){d.Text(app.Name,true);d.Text(ExternalApps.Problem(app,selection));}});
   fields.Remember=d.Check(selection.Subframes?"Use this as my default for subframe folders":"Use this as my default for these file types",false);
   if(selection.Folder){
    fields.Destination=ExportDestination(d,"AstroArchive_inputs_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
    fields.Calibration=d.Check("Include matching calibration files",false);fields.Calibration.IsEnabled=selection.Paths.All(p=>ExternalApps.FileType(p)!="SER"&&ExternalApps.FileType(p)!="AVI");
    d.Options("Calibration options",()=>{controls.Unknown=d.Check("Include calibrations when the subframe calibration state is unknown",false);});
    controls.Unknown.IsEnabled=false;controls.Calibration.Checked+=(s,e)=>controls.Unknown.IsEnabled=true;controls.Calibration.Unchecked+=(s,e)=>controls.Unknown.IsEnabled=false;
   }
   d.Button("Export destination settings…",()=>{ExportDestinationSettings();string updated=ExternalApps.Default(settings,selection);if(updated!=null)combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i=>i.Tag is ExportApplication&&((ExportApplication)i.Tag).Id==updated);summary();});
   d.Accept("Export",()=>{var app=SelectedApplication(combo);if(app==null){MessageBox.Show(d.Window,"Choose an application.");return false;}string problem=ExternalApps.Problem(app,selection);if(problem!=null){MessageBox.Show(d.Window,problem);return false;}if(controls.Destination!=null&&!ValidExportDestination(d,controls.Destination))return false;controls.Executable=LocateExportApplication(app,d.Window);return controls.Executable!=null;});return d;
  }
  void RememberExport(ExternalSelection selection,ExportApplication app,bool remember){
   if(!remember)return;if(settings.ExportDefaults==null)settings.ExportDefaults=new Dictionary<string,string>();
   foreach(string key in selection.Subframes?new[]{"Subframe folders"}:selection.Paths.Select(ExternalApps.FileType).Distinct())settings.ExportDefaults[key]=app.Id;SaveSettings();
  }
  void ExportTo(List<Frame> selected){
   ExternalSelection selection;try{selection=ExternalApps.Selection(selected);}catch(Exception error){MessageBox.Show(Window,error.Message,"Export to…");return;}
   ExportToFields fields;var dialog=ExportToDialog(selection,out fields);if(!dialog.Show())return;var app=SelectedApplication(fields.Application);RememberExport(selection,app,fields.Remember.IsChecked==true);
   if(!selection.Folder){
    EditedProject project=null;string launchError=null;var paths=new List<string>();
    Run(ct=>{project=repo.CreateEditedWorkingCopies(selected,selected[0].TargetLabel+" · "+app.Name+" · "+DateTime.Now.ToString("yyyyMMdd_HHmmss"),app.Name,ct,Progress);paths=project.Sources.Select(f=>repo.EditedPath(project,f.RelativePath)).ToList();ct.ThrowIfCancellationRequested();try{ExternalApps.Launch(app,fields.Executable,paths,null,ct);}catch(OperationCanceledException){throw;}catch(Exception error){launchError=error.Message;}return "";},done=>{RefreshEdited(project.Id);GoToPage(2);ExportLaunchResult(app,Path.GetDirectoryName(paths[0]),launchError,false);});return;
   }
   bool cals=fields.Calibration.IsChecked==true,unknown=fields.Unknown.IsChecked==true&&cals;var options=fields.Destination.Options();options.Mode="Files";options.IncludeCalibration=false;options.IncludeRejected=false;List<DssJob> jobs=null;string launchFailure=null;
   Run(ct=>{
    var inputs=ExternalApps.FolderInputs(repo,selected,cals,unknown);
    // Every calibration must be readable by the receiver too, before copying.
    string problem=ExternalApps.Problem(app,new ExternalSelection{Folder=true,Paths=inputs.Select(f=>f.OriginalName??f.RelativePath).ToList()});if(problem!=null)throw new IOException(problem);
    string folder=Exporter.Create(repo,inputs,options,ct,Progress);if(app.Id=="dss")jobs=ExternalApps.WriteDssJobs(folder,selected,options.ExportedFiles,cals,unknown,ct);
    ct.ThrowIfCancellationRequested();if(jobs==null||jobs.Count==1)try{ExternalApps.Launch(app,fields.Executable,jobs==null?new string[0]:new[]{jobs[0].Path},folder,ct);}catch(OperationCanceledException){throw;}catch(Exception error){launchFailure=error.Message;}return folder;
   },folder=>{if(jobs!=null&&jobs.Count>1){ChooseDssJob(folder,jobs,app,fields.Executable);return;}ExportLaunchResult(app,folder,launchFailure,app.Id!="dss"&&app.Id!="siril");});
  }
  void ExportEditedTo(){
   if(repo==null||cancel!=null||SearchBlocked("EditedSearchBox"))return;var images=SelectedEditedImages();if(images.Count==0)return;
   var selection=new ExternalSelection{Paths=images.Select(i=>repo.EditedPath(i.Project,i.RelativePath)).ToList()};ExportToFields fields;var dialog=ExportToDialog(selection,out fields,true);if(!dialog.Show())return;
   var app=SelectedApplication(fields.Application);RememberExport(selection,app,fields.Remember.IsChecked==true);string error=null;
   Run(ct=>{try{ExternalApps.Launch(app,fields.Executable,selection.Paths,null,ct);}catch(OperationCanceledException){throw;}catch(Exception e){error=e.Message;}return "";},done=>ExportLaunchResult(app,Path.GetDirectoryName(selection.Paths[0]),error,false));
  }
  void ExportLaunchResult(ExportApplication app,string folder,string error,bool assisted){
   if(error!=null){L("StatusLabel").Text="Files ready; "+app.Name+" could not open.";var d=new FormWindow(Window,"Files ready",620,470);d.Text("Your verified files are available",true);d.Text(folder);d.Text(app.Name+" could not open them: "+error);d.Button("Open folder",()=>OpenFolder(folder));d.Button("Export destination settings…",()=>ExportDestinationSettings(1,app.Id));d.CloseOnly();d.Show();return;}
   L("StatusLabel").Text=app.Name+" opened · "+folder;
   if(assisted){var d=new FormWindow(Window,"Export complete",630,450);d.Text("Your files are ready for "+app.Name,true);d.Text(folder);d.Text(app.Help);d.Button("Open input folder",()=>OpenFolder(folder));d.CloseOnly();d.Show();}
  }
  void ChooseDssJob(string folder,List<DssJob> jobs,ExportApplication app,string executable){
   var d=new FormWindow(Window,"DSS capture groups",640,470);d.Text(jobs.Count+" separate capture groups are ready",true);d.Text("Each group has its own DSS file list and matching calibrations. Open a group to register and stack it separately. All lists are kept in the exported folder.");var combo=d.Select("Capture group",jobs.Select(j=>j.Name).ToArray(),jobs[0].Name);
   d.Button("Open selected group in DSS",()=>{try{ExternalApps.Launch(app,executable,new[]{jobs[combo.SelectedIndex].Path},folder,CancellationToken.None);L("StatusLabel").Text="DSS opened · "+jobs[combo.SelectedIndex].Name;}catch(Exception error){MessageBox.Show(d.Window,error.Message+"\n\nYour file lists remain in:\n"+folder,"DSS unavailable");}});d.Button("Open exported folder",()=>OpenFolder(folder));d.CloseOnly();d.Show();
  }
  FormWindow ExportSettingsDialog(int tab,string appId, out Dictionary<string,string> paths,out Dictionary<string,string> defaults){
   paths=ExternalApps.All.ToDictionary(a=>a.Id,a=>ExternalApps.Configured(settings,a)??"");defaults=settings.ExportDefaults==null?new Dictionary<string,string>():new Dictionary<string,string>(settings.ExportDefaults);var localPaths=paths;var localDefaults=defaults;
   var d=new FormWindow(Window,"Export destinations",650,660);d.Tabs("Defaults","Applications");d.Text("Choose defaults by file type",true);d.Text("Export to… preselects a compatible default. Mixed selections ask you to choose unless they share a default. Subframe folders can have their own preference.");
   foreach(string key in ExternalApps.FileTypes){string type=key;var eligible=ExternalApps.All.Where(a=>type=="Subframe folders"?a.FolderTypes.Length>0:a.ImageTypes.Contains(type)||a.FolderTypes.Contains(type)).ToList();string id;localDefaults.TryGetValue(type,out id);var chosen=eligible.FirstOrDefault(a=>a.Id==id);var combo=d.Select(type,new[]{"Ask each time"}.Concat(eligible.Select(a=>a.Name)).ToArray(),chosen==null?"Ask each time":chosen.Name);combo.SelectionChanged+=(s,e)=>{var app=eligible.FirstOrDefault(a=>a.Name==Convert.ToString(combo.SelectedItem));if(app==null)localDefaults.Remove(type);else localDefaults[type]=app.Id;};}
   d.Tab(1);d.Text("Application locations",true);d.Text("Leave the path empty for automatic detection. Installed apps are checked in Windows registration, common install folders and PATH. Portable apps in common Desktop/Downloads folders may be found; otherwise browse to their executable.");
   var application=d.Select("Application",ExternalApps.All.Select(a=>a.Name).ToArray(),(ExternalApps.Find(appId)??ExternalApps.All[0]).Name);var path=d.Input("Executable path (optional)","");var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,12)};Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");d.Add(status);string current=null;
   Action load=()=>{if(current!=null)localPaths[current]=path.Text.Trim();var app=ExternalApps.All.First(a=>a.Name==Convert.ToString(application.SelectedItem));current=app.Id;path.Text=localPaths[current];status.Text=path.Text.Length==0?"Automatic detection. If the app cannot be found, Export to… will ask you to locate it.":File.Exists(path.Text)?"Configured location.":"This location is unavailable. Export to… will ask you to locate the app.";};application.SelectionChanged+=(s,e)=>load();load();
   d.Button("Browse…",()=>{var app=ExternalApps.Find(current);var picker=new OpenFileDialog{Title="Locate "+app.Name,Filter="Applications|*.exe",CheckFileExists=true};if(picker.ShowDialog(d.Window)==true)try{ExternalApps.ValidateExecutable(app,picker.FileName);path.Text=picker.FileName;status.Text="Configured location.";}catch(Exception error){MessageBox.Show(d.Window,error.Message);}});
   d.Button("Find automatically",()=>{string detected=ExternalApps.Detect(ExternalApps.Find(current));if(detected==null){status.Text="Not found. Browse to the application's GUI executable.";return;}path.Text=detected;status.Text="Found: "+detected;});
   d.Button("Use automatic detection",()=>{path.Clear();status.Text="Automatic detection will be used when exporting.";});
   d.Accept("Save",()=>{localPaths[current]=path.Text.Trim();foreach(var app in ExternalApps.All){string value=localPaths[app.Id];if(value.Length>0&&value!=(ExternalApps.Configured(settings,app)??""))try{ExternalApps.ValidateExecutable(app,value);}catch(Exception error){MessageBox.Show(d.Window,app.Name+": "+error.Message);d.SelectTab(1);application.SelectedItem=app.Name;return false;}}return true;});d.SelectTab(tab);return d;
  }
  void ExportDestinationSettings(int tab=0,string appId=null){Dictionary<string,string> paths,defaults;var dialog=ExportSettingsDialog(tab,appId,out paths,out defaults);if(!dialog.Show())return;foreach(var app in ExternalApps.All)ExternalApps.Configure(settings,app,paths[app.Id]);settings.ExportDefaults=defaults;SaveSettings();L("StatusLabel").Text="Export destination settings saved.";}
 }
}
