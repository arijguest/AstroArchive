// Selection-aware library actions. Context menus never fall back to all visible files.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
    if(e.Handled)return;
    var session=HeaderSession(e.OriginalSource as DependencyObject);if(session!=null){contextOnFile=true;SelectContextSession(session);e.Handled=true;return;}
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
    if(e.Key==Key.Delete&&!SearchBlocked("SearchBox")&&cancel==null&&repo!=null&&SelectedFiles().Count>0){e.Handled=true;DeleteFiles(SelectedFiles());}
   };
  }
  List<Frame> SelectedFiles(){return G("FramesGrid").SelectedItems.OfType<Frame>().Concat(subframeSessions.Where(g=>g.IsSelected).SelectMany(g=>g.Frames)).Distinct().ToList();}
  void SelectContextRow(Frame frame){if(frame==null)return;var grid=G("FramesGrid");if(!SelectedFiles().Contains(frame)){ClearSessionSelection();grid.SelectedItems.Clear();grid.SelectedItems.Add(frame);}}
  MenuItem FileAction(string title,Action action,bool enabled=true){var item=new MenuItem{Header=title,IsEnabled=enabled};UiHelp.For(item,title);item.Click+=(s,e)=>{if(cancel==null&&!ActiveSearchBlocked)action();};return item;}
  MenuItem ExportMenu(List<Frame> selected){
   var menu=new MenuItem{Header="Export",IsEnabled=selected.Count>0};
   menu.Items.Add(FileAction("Export to…",()=>ExportTo(selected),selected.Count>0));
   menu.Items.Add(FileAction("Save files…",()=>ExportFiles(selected),selected.Count>0));
   bool stackable=selected.Any(f=>f.Kind=="Light"||f.Kind=="Stack");
   menu.Items.Add(FileAction("Stacking folder…",()=>ExportProject(selected,false),stackable));
   menu.Items.Add(Branch("More",FileAction("Create Edited working copies…",()=>CreateEditedCopies(selected),selected.Count>0),FileAction("Selection catalogue CSV…",()=>ExportSelectionCsv(selected),selected.Count>0),FileAction("Complete repository catalogue CSV…",ExportCatalogue)));
   return menu;
  }
  void BuildFileMenu(ContextMenu menu,List<Frame> selected){
   menu.Items.Clear();menu.Items.Add(new MenuItem{Header=selected.Count+" selected file"+(selected.Count==1?"":"s"),IsEnabled=false});
   menu.Items.Add(FileAction("Preview image…",()=>PreviewImage(selected[0]),selected.Count==1));menu.Items.Add(FileAction("Copy file paths",()=>Clipboard.SetText(string.Join(Environment.NewLine,selected.Select(repo.FilePath)))));menu.Items.Add(ExportMenu(selected));menu.Items.Add(new Separator());
   menu.Items.Add(FileAction("Choose HDU / page / frame…",()=>PreviewFile(selected[0]),selected.Count==1));menu.Items.Add(FileAction("Edit metadata…",()=>Edit(false)));menu.Items.Add(FileAction("Re-detect metadata and review…",()=>ReviewMetadata(selected)));
   menu.Items.Add(FileAction("Identify target…",()=>Identify(false),selected.Any(f=>f.Kind=="Light"||f.Kind=="Stack"||f.Kind=="Unknown")));
   menu.Items.Add(FileAction("Show file in Explorer",()=>ShowFile(selected[0]),selected.Count==1));
   menu.Items.Add(new Separator());var delete=FileAction("Delete selected files…",()=>DeleteFiles(selected));delete.Foreground=new SolidColorBrush(Color.FromRgb(183,40,51));menu.Items.Add(delete);
  }
  void ShowExportMenu(){if(repo==null||cancel!=null)return;var selected=Context();var menu=ThemedMenu();var choices=ExportMenu(selected);foreach(MenuItem item in choices.Items.Cast<MenuItem>().ToList()){choices.Items.Remove(item);menu.Items.Add(item);}menu.PlacementTarget=B("ExportButton");menu.Placement=PlacementMode.Top;menu.IsOpen=true;}
  void ExportSelectionCsv(List<Frame> selected){var picker=new Microsoft.Win32.SaveFileDialog{FileName="AstroArchive_selection.csv",Filter="CSV catalogue|*.csv"};if(picker.ShowDialog(Window)==true){repo.ExportIndex(picker.FileName,selected);L("StatusLabel").Text=selected.Count+" catalogue rows exported.";}}
  void ShowFile(Frame frame){string path=repo.FilePath(frame);if(File.Exists(path))Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+path+"\""){UseShellExecute=true});else MessageBox.Show(Window,"This file is missing from the repository.","File unavailable");}
  sealed class ExportDestinationFields {
   public TextBox Parent,Name;public CheckBox Metadata,NewFolder;
   public ExportOptions Options(){return new ExportOptions{Parent=Parent.Text.Trim(),Name=Name.Text.Trim(),AddMetadata=Metadata.IsChecked==true,CreateNewFolder=NewFolder.IsChecked==true};}
  }
  ExportDestinationFields ExportDestination(FormWindow d,string suggestedName){
   var fields=new ExportDestinationFields();fields.Parent=d.Input("Destination folder","");d.Button("Browse destination",()=>{string p=Folder("Choose an export destination",fields.Parent.Text,d.Window);if(p!=null)fields.Parent.Text=p;});
   d.Options("Folder and metadata options",()=>{
    fields.NewFolder=d.Check("Create new folder",false);fields.Name=d.Input("New folder name",suggestedName);fields.Name.IsEnabled=false;
    fields.NewFolder.Checked+=(sender,args)=>fields.Name.IsEnabled=true;fields.NewFolder.Unchecked+=(sender,args)=>fields.Name.IsEnabled=false;
    fields.Metadata=d.Check("Add Metadata",false);d.Text("Include a manifest, companion metadata and workflow notes.");
   });return fields;
  }
  bool ValidExportDestination(FormWindow d,ExportDestinationFields fields){try{Exporter.Destination(repo,fields.Options());return true;}catch(Exception e){MessageBox.Show(d.Window,e.Message);return false;}}
  void ExportFiles(List<Frame> selected){
   if(selected.Count==0)return;var d=new FormWindow(Window,"Save files",610,500);
   d.Text(selected.Count+" selected files",true);d.Text("Stacks and other images copy directly to the destination. Subs keep their compatible input folders. Existing files are kept; duplicate names receive a numbered suffix.");
   var destination=ExportDestination(d,"AstroArchive_export_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   d.Accept("Export files",()=>ValidExportDestination(d,destination));if(!d.Show())return;
   var options=destination.Options();options.Mode="Files";options.IncludeCalibration=false;options.IncludeRejected=true;
   Run(ct=>Exporter.Create(repo,selected,options,ct,Progress),ExportComplete);
  }
  void ExportProject(List<Frame> selected,bool withCalibration){
   var items=selected.Where(f=>f.Kind=="Light"||f.Kind=="Stack").ToList();if(items.Count==0)return;
   var d=new FormWindow(Window,"Stacking folder",630,660);
   string target=items.Select(f=>f.Target).Distinct().Count()==1?items[0].Target:"Multiple targets";
   d.Text(target+"  ·  "+items.Count(f=>f.Kind=="Light")+" subs  ·  "+items.Count(f=>f.Kind=="Stack")+" stacks",true);
   var destination=ExportDestination(d,Util.Safe(target)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   ComboBox mode=d.Select("Inputs",new[]{"Subs","Stacks","Both"},items.All(f=>f.Kind=="Light")?"Subs":items.All(f=>f.Kind=="Stack")?"Stacks":"Both");
   CheckBox calibration=d.Check("Include matching calibration files",withCalibration),sessions=null,unknown=null,rejected=null,convert=null;var availableCalibrations=Exporter.ExistingCalibrations(repo,all);
   d.Options("Advanced stacking options",()=>{sessions=d.Check("Separate sessions into their own folders",false);unknown=d.Check("Include calibrations for subs with unknown calibration state",false);rejected=d.Check("Include files marked rejected/reference",false);convert=d.Check("Convert supported images to FITS (keeps archived originals)",false);d.Text("Non-FITS data and selected containers need explicit conversion. Confirm linearity in metadata; processed previews stay original-file exports.");d.Button("Review calibration matches and reasons",()=>ShowReport("Calibration matching",CalibrationReport(items.Where(f=>f.Kind=="Light").ToList(),availableCalibrations)));});var availability=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)};Theme.Bind(availability,TextBlock.ForegroundProperty,"Muted");d.Add(availability);
   Action summary=()=>{try{unknown.IsEnabled=calibration.IsChecked==true&&Convert.ToString(mode.SelectedItem)!="Stacks";var lights=items.Where(f=>f.Kind=="Light"&&(rejected.IsChecked==true||!f.Rejected)).ToList();int count=Convert.ToString(mode.SelectedItem)=="Stacks"||calibration.IsChecked!=true?0:Exporter.AvailableCalibrations(lights,availableCalibrations,sessions.IsChecked==true,unknown.IsChecked==true).Count;availability.Text=calibration.IsChecked!=true?"Selected inputs only. Calibration files are omitted.":Convert.ToString(mode.SelectedItem)=="Stacks"?"Existing stacks receive no additional calibration files.":count>0?count+" matching calibration files available. Already calibrated or registered subs receive no extra calibration.":"No matching calibration files are available for these inputs. You can still export the selected captures.";}catch(Exception e){availability.Text=e.Message;}};
   foreach(var check in new[]{sessions,calibration,unknown,rejected}){check.Checked+=(s,e)=>summary();check.Unchecked+=(s,e)=>summary();}mode.SelectionChanged+=(s,e)=>summary();summary();
   d.Text("Each target, camera and compatible capture group has its own input folder. Masters and raw calibration sets stay separate. Stack the exported inputs in your preferred software.");
   d.Accept("Export folder",()=>{string selectedMode=Convert.ToString(mode.SelectedItem);if(!items.Any(f=>(rejected.IsChecked==true||!f.Rejected)&&(selectedMode=="Both"||selectedMode=="Subs"&&f.Kind=="Light"||selectedMode=="Stacks"&&f.Kind=="Stack"))){MessageBox.Show(d.Window,"This input choice has no eligible files.");return false;}return ValidExportDestination(d,destination);});if(!d.Show())return;
   var options=destination.Options();options.Mode=Convert.ToString(mode.SelectedItem);options.IncludeCalibration=calibration.IsChecked==true;options.IncludeUnknownCalibration=unknown.IsChecked==true;options.IncludeRejected=rejected.IsChecked==true;options.SeparateSessions=sessions.IsChecked==true;options.ConvertToFits=convert.IsChecked==true;
   Run(ct=>Exporter.Create(repo,items,options,ct,Progress),path=>ExportComplete(path,true));
  }
  void ExportComplete(string path){ExportComplete(path,false);}
  void ExportComplete(string path,bool stacking){L("StatusLabel").Text="Exported folder: "+path;ExportCompleteDialog(path,stacking).Show();}
  FormWindow ExportCompleteDialog(string path,bool stacking){
   var d=new FormWindow(Window,"Export complete",640,stacking?480:400);d.Text(stacking?"Your stacking folder is ready":"Your exported folder is ready",true);d.Text(path);d.Text("Files have been copied and verified.");
   d.Button("Open exported folder",()=>{try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception e){MessageBox.Show(d.Window,e.Message,"Folder unavailable");}});
   if(stacking)d.Text("Use Export to… from Repository to send inputs to a processing app.");
   d.CloseOnly();return d;
  }
  void DeleteFailedFiles(){
   if(repo==null||cancel!=null)return;var matches=repo.FailedFiles();
   if(matches.Count==0){L("StatusLabel").Text="No repository filenames contain 'failed'.";return;}
   DeleteFiles(matches,true);
  }
  void PurgeNonRawFiles(){
   if(repo==null||cancel!=null)return;var matches=repo.NonRawFiles();
   if(matches.Count==0){L("StatusLabel").Text="No PNG/JPG/JPEG files are indexed in this repository.";return;}
   DeleteFiles(matches,false,true);
  }
  void DeleteFiles(List<Frame> selected,bool failedNames=false,bool nonRawFiles=false){
   if(repo==null||cancel!=null||selected.Count==0)return;
   var d=new FormWindow(Window,nonRawFiles?"Purge non-raw files":failedNames?"Delete failed":"Delete selected files",640,520);d.Text("Delete "+selected.Count+" "+(failedNames||nonRawFiles?"matching":"selected")+" file"+(selected.Count==1?"":"s")+"?",true);d.Text(repo.Root);
   if(failedNames)d.Text("Searches the entire active repository for filenames containing 'failed', regardless of case. Current filters and telescope selection do not limit this action.");
   if(nonRawFiles)d.Text("Finds indexed PNG/JPG/JPEG files in the entire active repository, regardless of case or current filters. Edited images, FITS, TIFF, XISF, SER and camera RAW files are excluded from this purge.");
   d.Text("This permanently removes the selected repository copies. Deletion history is retained so future telescope imports skip the same captures. Source copies, other archive files and shared session metadata stay. Cloud-synced deletions propagate to the cloud.");
   d.Add(new TextBox{Text=string.Join("\r\n",selected.Select(f=>f.RelativePath)),IsReadOnly=true,Height=170,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
   d.Accept(nonRawFiles?"Purge non-raw files":failedNames?"Delete failed files":"Delete selected files",()=>true,true);if(!d.Show())return;CancelPreview();
   Run(ct=>{var result=repo.DeleteFrames(selected,ct,Progress);return result.Deleted+" selected files deleted."+(result.Errors.Count==0?"":"\r\n\r\n"+string.Join("\r\n",result.Errors));},message=>{plan=null;FilterImports();L("StatusLabel").Text=message.Split('\n')[0];if(message.Contains("\n"))ShowReport("File deletion report",message);});
  }
 }
}
