// Selection-aware library actions. Context menus never fall back to all visible files.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace AstroArchive {
 public partial class MainUi {
  ContextMenu ThemedMenu(){var menu=new ContextMenu();menu.Resources.MergedDictionaries.Add(Window.Resources);return menu;}
  bool contextOnFile;
  void InitializeFileTools() {
   var grid=G("FramesGrid");
   grid.ContextMenu=ThemedMenu();
   grid.PreviewMouseRightButtonDown+=(s,e)=>{
    var row=ItemsControl.ContainerFromElement(grid,e.OriginalSource as DependencyObject) as DataGridRow;
    contextOnFile=row!=null;
    if(row==null){grid.ContextMenu.IsOpen=false;e.Handled=true;return;}
    SelectContextRow(row.Item as Frame);
    row.Focus();e.Handled=true;
   };
   grid.ContextMenuOpening+=(s,e)=>{
    var selected=SelectedFiles();
    if((e.CursorLeft>=0&&!contextOnFile)||cancel!=null||repo==null||selected.Count==0){e.Handled=true;return;}
    BuildFileMenu(grid.ContextMenu,selected);
   };
   grid.PreviewKeyDown+=(s,e)=>{
    if(e.Key==Key.Delete&&cancel==null&&repo!=null&&SelectedFiles().Count>0){e.Handled=true;DeleteFiles(SelectedFiles());}
   };
  }
  List<Frame> SelectedFiles(){return G("FramesGrid").SelectedItems.Cast<Frame>().ToList();}
  void SelectContextRow(Frame frame){if(frame==null)return;var grid=G("FramesGrid");if(!grid.SelectedItems.Contains(frame)){grid.SelectedItems.Clear();grid.SelectedItems.Add(frame);}}
  MenuItem FileAction(string title,Action action,bool enabled=true){var item=new MenuItem{Header=title,IsEnabled=enabled};item.Click+=(s,e)=>{if(cancel==null)action();};return item;}
  MenuItem ExportMenu(List<Frame> selected){
   var menu=new MenuItem{Header="Export",IsEnabled=selected.Count>0};
   menu.Items.Add(FileAction("Export selected files…",()=>ExportFiles(selected)));
   bool stackable=selected.Any(f=>f.Kind=="Light"||f.Kind=="Stack");
   menu.Items.Add(FileAction("Ready-to-stack folder…",()=>ExportProject(selected,false),stackable));
   menu.Items.Add(FileAction("Ready-to-stack with calibrations…",()=>ExportProject(selected,true),stackable));
   menu.Items.Add(FileAction("Send stack to AstroWizard…",()=>SendStackToProcessor(selected,true),AstroWizardHandoff.CanSend(selected)));
   menu.Items.Add(FileAction("Send stack to Siril…",()=>SendStackToSiril(selected),SirilHandoff.CanSend(selected)));
   return menu;
  }
  void BuildFileMenu(ContextMenu menu,List<Frame> selected){
   menu.Items.Clear();menu.Items.Add(new MenuItem{Header=selected.Count+" selected file"+(selected.Count==1?"":"s"),IsEnabled=false});
   menu.Items.Add(FileAction("Preview image…",()=>PreviewImage(selected[0]),selected.Count==1));menu.Items.Add(FileAction("Copy file paths",()=>Clipboard.SetText(string.Join(Environment.NewLine,selected.Select(repo.FilePath)))));menu.Items.Add(ExportMenu(selected));menu.Items.Add(FileAction("Export selection catalogue…",()=>ExportSelectionCsv(selected)));menu.Items.Add(new Separator());
   menu.Items.Add(FileAction("Edit metadata…",()=>Edit(false)));
   menu.Items.Add(FileAction("Identify target…",()=>Identify(false),selected.Any(f=>f.Kind=="Light"||f.Kind=="Stack"||f.Kind=="Unknown")));
   menu.Items.Add(FileAction("Show file in Explorer",()=>ShowFile(selected[0]),selected.Count==1));
   menu.Items.Add(new Separator());var delete=FileAction("Delete selected files…",()=>DeleteFiles(selected));delete.Foreground=new SolidColorBrush(Color.FromRgb(183,40,51));menu.Items.Add(delete);
  }
  void ShowExportMenu(){if(repo==null||cancel!=null)return;var selected=Context();var menu=ThemedMenu();var choices=ExportMenu(selected);foreach(MenuItem item in choices.Items.Cast<MenuItem>().ToList()){choices.Items.Remove(item);menu.Items.Add(item);}menu.PlacementTarget=B("ExportButton");menu.Placement=PlacementMode.Top;menu.IsOpen=true;}
  void ExportSelectionCsv(List<Frame> selected){var picker=new Microsoft.Win32.SaveFileDialog{FileName="AstroArchive_selection.csv",Filter="CSV catalogue|*.csv"};if(picker.ShowDialog(Window)==true){repo.ExportIndex(picker.FileName,selected);L("StatusLabel").Text=selected.Count+" catalogue rows exported.";}}
  void ShowFile(Frame frame){string path=repo.FilePath(frame);if(File.Exists(path))Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+path+"\""){UseShellExecute=true});else MessageBox.Show(Window,"This file is missing from the repository.","File unavailable");}
  void ExportFiles(List<Frame> selected){
   if(selected.Count==0)return;var d=new FormWindow(Window,"Export selected files",610,440);
   d.Text(selected.Count+" selected files",true);d.Text("Copy the selected files to a new folder, with their original names and a checksum manifest.");
   TextBox parent=d.Input("Destination folder","");d.Button("Browse destination",()=>{string p=Folder("Choose an export destination",parent.Text,d.Window);if(p!=null)parent.Text=p;});
   TextBox name=d.Input("New folder name","AstroArchive_export_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   d.Accept("Export files",()=>ValidExportDestination(d,parent,name));if(!d.Show())return;
   var options=new ExportOptions{Parent=parent.Text.Trim(),Name=name.Text.Trim(),Mode="Files",IncludeCalibration=false,IncludeRejected=true};
   Run(ct=>Exporter.Create(repo,selected,options,ct,Progress),ExportComplete);
  }
  bool ValidExportDestination(FormWindow d,TextBox parent,TextBox name){if(!Directory.Exists(parent.Text.Trim())){MessageBox.Show(d.Window,"Choose an existing destination folder.");return false;}if(string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show(d.Window,"Enter a name for the new folder.");return false;}if(Util.Within(parent.Text.Trim(),repo.Root)){MessageBox.Show(d.Window,"Choose a destination outside the repository.");return false;}if(Directory.Exists(Path.Combine(parent.Text.Trim(),Util.Safe(name.Text.Trim())))||File.Exists(Path.Combine(parent.Text.Trim(),Util.Safe(name.Text.Trim())))){MessageBox.Show(d.Window,"This folder name already exists. Choose a new name.");return false;}return true;}
  void ExportProject(List<Frame> selected,bool withCalibration){
   var items=selected.Where(f=>f.Kind=="Light"||f.Kind=="Stack").ToList();if(items.Count==0)return;
   var d=new FormWindow(Window,withCalibration?"Export with calibrations":"Export ready-to-stack folder",630,720);
   string target=items.Select(f=>f.Target).Distinct().Count()==1?items[0].Target:"Multiple targets";
   d.Text(target+"  ·  "+items.Count(f=>f.Kind=="Light")+" subs  ·  "+items.Count(f=>f.Kind=="Stack")+" stacks",true);
   TextBox parent=d.Input("Destination folder","");d.Button("Browse destination",()=>{string p=Folder("Choose where to create the stacking folder",parent.Text,d.Window);if(p!=null)parent.Text=p;});
   TextBox name=d.Input("New folder name",Util.Safe(target)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   ComboBox mode=d.Select("Inputs",new[]{"Subs","Stacks","Both"},items.All(f=>f.Kind=="Light")?"Subs":items.All(f=>f.Kind=="Stack")?"Stacks":"Both");
   CheckBox sessions=d.Check("Separate sessions into their own folders",false),calibration=d.Check("Include matching calibration files",withCalibration),unknown=d.Check("Include calibrations for subs with unknown calibration state",false),rejected=d.Check("Include files marked rejected/reference",false);
   var availableCalibrations=Exporter.ExistingCalibrations(repo,all);
   var availability=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)};Theme.Bind(availability,TextBlock.ForegroundProperty,"Muted");d.Add(availability);
   Action summary=()=>{unknown.IsEnabled=calibration.IsChecked==true&&Convert.ToString(mode.SelectedItem)!="Stacks";var lights=items.Where(f=>f.Kind=="Light"&&(rejected.IsChecked==true||!f.Rejected)).ToList();int count=Convert.ToString(mode.SelectedItem)=="Stacks"||calibration.IsChecked!=true?0:Exporter.AvailableCalibrations(lights,availableCalibrations,sessions.IsChecked==true,unknown.IsChecked==true).Count;availability.Text=calibration.IsChecked!=true?"Selected inputs only. Calibration files are omitted.":Convert.ToString(mode.SelectedItem)=="Stacks"?"Existing stacks receive no additional calibration files.":count>0?count+" matching calibration files available. Already calibrated or registered subs receive no extra calibration.":"No matching calibration files are available for these inputs. You can still export the selected captures.";};
   foreach(var check in new[]{sessions,calibration,unknown,rejected}){check.Checked+=(s,e)=>summary();check.Unchecked+=(s,e)=>summary();}mode.SelectionChanged+=(s,e)=>summary();summary();
   d.Text("Each target, camera and compatible capture group has its own input folder. Masters and raw calibration sets stay separate. Stack the exported inputs in your preferred software.");
   d.Accept("Export folder",()=>{if(!items.Any(f=>(rejected.IsChecked==true||!f.Rejected)&&(Convert.ToString(mode.SelectedItem)=="Both"||f.Kind==(Convert.ToString(mode.SelectedItem)=="Subs"?"Light":"Stack")))){MessageBox.Show(d.Window,"This input choice has no eligible files.");return false;}return ValidExportDestination(d,parent,name);});if(!d.Show())return;
   var options=new ExportOptions{Parent=parent.Text.Trim(),Name=name.Text.Trim(),Mode=Convert.ToString(mode.SelectedItem),IncludeCalibration=calibration.IsChecked==true,IncludeUnknownCalibration=unknown.IsChecked==true,IncludeRejected=rejected.IsChecked==true,SeparateSessions=sessions.IsChecked==true};
   Run(ct=>Exporter.Create(repo,items,options,ct,Progress),ExportComplete);
  }
  void ExportComplete(string path){L("StatusLabel").Text="Exported folder: "+path;var d=new FormWindow(Window,"Export complete",600,400);d.Text("Your exported folder is ready",true);d.Text(path);d.Text("Files have been copied and verified. Stacking folders include workflow notes and a manifest.");d.Button("Open exported folder",()=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true}));d.CloseOnly();d.Show();}
  void SendStackToSiril(List<Frame> selected){SendStackToProcessor(selected,false);}
  void SendStackToProcessor(List<Frame> selected,bool wizard){
   if(wizard?!AstroWizardHandoff.CanSend(selected):!SirilHandoff.CanSend(selected))return;
   string title=wizard?"AstroWizard":"Siril";
   var d=new FormWindow(Window,"Send stack to "+title,630,660);d.Text("Open this stack in "+title,true);
   d.Text("Creates a verified working copy and opens it in "+title+". Select one uncompressed FITS stack.");
   if(wizard)d.Text("Verified AstroWizard build: "+AstroWizardHandoff.VerifiedBuild+". The official executable is checked before export. Other builds require verification. Uses the same handoff as StackingWizard.");
   TextBox executable=d.Input(title+" executable",(wizard?settings.AstroWizardExecutable:settings.SirilExecutable)??"");
   d.Button("Locate "+title,()=>{var picker=new OpenFileDialog{Filter=wizard?"AstroWizard executable|*.exe":"Siril GUI|siril.exe",Title="Choose the "+title+" executable"};if(picker.ShowDialog(d.Window)==true)executable.Text=picker.FileName;});
   TextBox parent=d.Input("Working-copy destination folder","");d.Button("Browse destination",()=>{string p=Folder("Choose a "+title+" working-copy destination",parent.Text,d.Window);if(p!=null)parent.Text=p;});
   TextBox name=d.Input("New folder name",Util.Safe(selected[0].Target)+"_"+title+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   d.Accept("Send to "+title,()=>{try{if(wizard){if(!File.Exists(executable.Text.Trim()))throw new IOException("Locate AstroWizard first.");}else SirilHandoff.ValidateExecutable(executable.Text.Trim());}catch(Exception e){MessageBox.Show(d.Window,e.Message,title+" unavailable");return false;}return ValidExportDestination(d,parent,name);});if(!d.Show())return;
   string app=executable.Text.Trim();if(wizard)settings.AstroWizardExecutable=app;else settings.SirilExecutable=app;SaveSettings();
   var options=new ExportOptions{Parent=parent.Text.Trim(),Name=name.Text.Trim()};
   Run(ct=>{
    string image=wizard?AstroWizardHandoff.ExportStack(repo,selected,options,app,ct,Progress):SirilHandoff.ExportStack(repo,selected,options,app,ct,Progress);
    ct.ThrowIfCancellationRequested();
    try{using(var process=Process.Start(wizard?AstroWizardHandoff.LaunchInfo(app,image):SirilHandoff.LaunchInfo(app,image))){if(process==null)throw new IOException(title+" did not start.");}}
    catch(Exception e){throw new IOException(title+" could not be launched. Your verified working copy is saved at:\n"+image+"\n\n"+e.Message,e);}
    return image;
   },image=>L("StatusLabel").Text=title+" launched with stack: "+image);
  }
  void DeleteFiles(List<Frame> selected){
   if(repo==null||cancel!=null||selected.Count==0)return;
   var d=new FormWindow(Window,"Delete selected files",640,520);d.Text("Delete "+selected.Count+" selected file"+(selected.Count==1?"":"s")+"?",true);d.Text(repo.Root);
   d.Text("This permanently removes the selected repository copies. Deletion history is retained so future telescope imports skip the same captures. Source copies, other archive files and shared session metadata stay. Cloud-synced deletions propagate to the cloud.");
   d.Add(new TextBox{Text=string.Join("\r\n",selected.Select(f=>f.RelativePath)),IsReadOnly=true,Height=170,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
   d.Accept("Delete selected files",()=>true,true);if(!d.Show())return;CancelPreview();
   Run(ct=>{var result=repo.DeleteFrames(selected,ct,Progress);return result.Deleted+" selected files deleted."+(result.Errors.Count==0?"":"\r\n\r\n"+string.Join("\r\n",result.Errors));},message=>{plan=null;FilterImports();L("StatusLabel").Text=message.Split('\n')[0];if(message.Contains("\n"))ShowReport("File deletion report",message);});
  }
 }
}
