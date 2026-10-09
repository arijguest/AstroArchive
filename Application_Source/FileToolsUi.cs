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
    if((e.CursorLeft>=0&&!contextOnFile)||RepositoryOperationBlocked||repo==null||selected.Count==0){e.Handled=true;return;}
    BuildFileMenu(grid.ContextMenu,selected);
   };
   grid.PreviewKeyDown+=(s,e)=>{
    if(e.Key==Key.Delete&&!SearchBlocked("SearchBox")&&!RepositoryOperationBlocked&&repo!=null&&SelectedFiles().Count>0){e.Handled=true;DeleteFiles(SelectedFiles());}
   };
  }
  List<Frame> SelectedFiles(){return librarySelection.Items;}
  void SelectContextRow(Frame frame){if(frame==null)return;var grid=G("FramesGrid");if(!SelectedFiles().Contains(frame)){ClearSessionSelection();grid.SelectedItems.Clear();grid.SelectedItems.Add(frame);}}
  MenuItem FileAction(string title,Action action,bool enabled=true){var item=new MenuItem{Header=title,IsEnabled=enabled};UiHelp.For(item,title);item.Click+=(s,e)=>{if(!RepositoryOperationBlocked&&!ActiveSearchBlocked)action();};return item;}
  MenuItem ExportMenu(List<Frame> selected){
   var menu=new MenuItem{Header="Export",IsEnabled=selected.Count>0};menu.Items.Add(FileAction("Export to…",()=>ExportTo(selected),selected.Count>0));menu.Items.Add(FileAction("Export files…",()=>ExportFiles(selected)));menu.Items.Add(FileAction("Stacking folder…",()=>ExportProject(selected,false),selected.Any(f=>f.Kind=="Light"||f.Kind=="Stack")));return menu;
  }
  void BuildFileMenu(ContextMenu menu,List<Frame> selected){
   menu.Items.Clear();menu.Items.Add(new MenuItem{Header=selected.Count+" selected file"+(selected.Count==1?"":"s"),IsEnabled=false});menu.Items.Add(FileAction("Preview…",()=>PreviewImage(selected[0]),selected.Count==1));menu.Items.Add(FileAction("Edit metadata…",()=>Edit(false)));menu.Items.Add(ExportMenu(selected));menu.Items.Add(FileAction("Create Edited copies…",()=>CreateEditedCopies(selected)));menu.Items.Add(FileAction("Open file location",()=>ShowFile(selected[0]),selected.Count==1));
   var more=Branch("More actions",FileAction("Copy file paths",()=>Clipboard.SetText(string.Join(Environment.NewLine,selected.Select(repo.FilePath)))),FileAction("Identify target…",()=>Identify(false),selected.Any(f=>f.Kind=="Light"||f.Kind=="Stack"||f.Kind=="Unknown")),FileAction("Review detected metadata…",()=>ReviewMetadata(selected)),FileAction("Export catalogue CSV…",()=>ExportSelectionCsv(selected)));
   if(selected.Count==1&&selected[0].Images!=null&&(selected[0].Images.Count>1||selected[0].Images.Any(i=>i.Count>1)))more.Items.Add(FileAction("Choose HDU / page / frame…",()=>PreviewFile(selected[0])));
   menu.Items.Add(more);menu.Items.Add(new Separator());var delete=FileAction("Delete files…",()=>DeleteFiles(selected));delete.Foreground=new SolidColorBrush(Color.FromRgb(183,40,51));menu.Items.Add(delete);
  }
  void ShowExportMenu(){if(repo==null||RepositoryOperationBlocked)return;var menu=ThemedMenu();var choices=new MenuItem();BuildExportNavigation(choices);foreach(var item in choices.Items.Cast<object>().ToList()){choices.Items.Remove(item);menu.Items.Add(item);}menu.PlacementTarget=B("ExportButton");menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;}
  void ExportSelectionCsv(List<Frame> selected){var picker=new Microsoft.Win32.SaveFileDialog{FileName="AstroArchive_selection.csv",Filter="CSV catalogue|*.csv"};if(picker.ShowDialog(Window)==true){string destination=picker.FileName;var snapshot=selected.Select(f=>f.Clone()).ToList();Run(ct=>{ct.ThrowIfCancellationRequested();repo.ExportIndex(destination,snapshot);return snapshot.Count+" catalogue rows exported.";},message=>{L("StatusLabel").Text=message;if(completionActivity!=null)completionActivity.OutputPath=Path.GetDirectoryName(destination);},"Exporting catalogue CSV");}}
  void ShowFile(Frame frame){string path=repo.FilePath(frame);if(File.Exists(path))Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+path+"\""){UseShellExecute=true});else MessageBox.Show(Window,"This file is missing from the repository.","File unavailable");}
  sealed class ExportDestinationFields {
   public TextBox Parent,Name;public CheckBox Metadata,NewFolder;public ComboBox AfterExport;public Action RefreshAfterExport;
   public ExportOptions Options(){return new ExportOptions{Parent=Parent.Text.Trim(),Name=Name.Text.Trim(),AddMetadata=Metadata.IsChecked==true,CreateNewFolder=NewFolder.IsChecked==true};}
  }
  ExportDestinationFields ExportDestination(FormWindow d,string suggestedName){
   var fields=new ExportDestinationFields();fields.Parent=d.Input("Destination folder",settings.ExportWorkingDirectory??"");d.Button("Choose folder…",()=>{string path=Folder("Choose an export destination",fields.Parent.Text,d.Window);if(path!=null)fields.Parent.Text=path;});
   fields.NewFolder=d.Check("Create new folder",false);var namePanel=new StackPanel{Visibility=Visibility.Collapsed};var caption=new TextBlock{Text="New folder name",Margin=new Thickness(0,6,0,5)};caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");namePanel.Children.Add(caption);fields.Name=new TextBox{Text=suggestedName,IsEnabled=false};UiHelp.For(fields.Name,"New folder name");namePanel.Children.Add(fields.Name);d.Add(namePanel);
   Action nameVisibility=()=>{fields.Name.IsEnabled=fields.NewFolder.IsChecked==true;namePanel.Visibility=fields.Name.IsEnabled?Visibility.Visible:Visibility.Collapsed;};fields.NewFolder.Checked+=(s,e)=>nameVisibility();fields.NewFolder.Unchecked+=(s,e)=>nameVisibility();
   fields.Metadata=new CheckBox{Content="Add Metadata",Margin=new Thickness(0,14,0,3),IsChecked=false};UiHelp.Tip(fields.Metadata,"Include a manifest, companion metadata and workflow notes.");return fields;
  }
  bool ValidExportDestination(FormWindow d,ExportDestinationFields fields){try{Exporter.Destination(repo,fields.Options());if(fields.AfterExport!=null&&Convert.ToString(fields.AfterExport.SelectedItem)=="Siril")SirilHandoff.ValidateExecutable(settings.SirilExecutable);return true;}catch(Exception e){MessageBox.Show(d.Window,e.Message);return false;}}
  void ExportAfterChoice(FormWindow dialog,ExportDestinationFields fields,List<Frame> selected,Func<ExportOptions> options){
   fields.AfterExport=selected.Any(f=>f.Kind=="Stack")?dialog.Select("Open with… after export",new[]{"None","Siril"},"None"):new ComboBox{ItemsSource=new[]{"None","Siril"},SelectedItem="None"};
   fields.RefreshAfterExport=()=>{bool eligible=SirilHandoff.StackAfterExport(selected,options())!=null;fields.AfterExport.IsEnabled=eligible;if(!eligible)fields.AfterExport.SelectedItem="None";};
   UiHelp.Tip(fields.AfterExport,"Open one exported FITS stack in Siril. Multiple stacks and subs-only exports cannot open a single image.");
   dialog.Button("Export preferences…",()=>{string previous=settings.ExportWorkingDirectory??"";Configure(2,dialog.Window);if(string.IsNullOrWhiteSpace(fields.Parent.Text)||fields.Parent.Text==previous)fields.Parent.Text=settings.ExportWorkingDirectory??"";fields.RefreshAfterExport();});
   fields.RefreshAfterExport();
  }
  void RunExport(List<Frame> selected,ExportOptions options,ExportDestinationFields fields,bool stacking){
   bool openSiril=Convert.ToString(fields.AfterExport.SelectedItem)=="Siril";string executable=settings.SirilExecutable;var result=openSiril?new ExportResult():null;
   Run(ct=>Exporter.Create(repo,selected,options,ct,Progress,result),path=>{
    if(openSiril){try{using(var process=Process.Start(SirilHandoff.ExportLaunchInfo(executable,result))){if(process==null)throw new IOException("Siril did not start.");}L("StatusLabel").Text="Exported to "+path+" · opened stack in Siril";if(completionActivity!=null)completionActivity.OutputPath=path;return;}
     catch(Exception error){if(completionActivity!=null){completionActivity.NeedsReview=true;completionActivity.ReportTitle="Siril handoff";completionActivity.Report="Your export is complete: "+path+"\nSiril could not open the stack: "+error.Message;}}}
    ExportComplete(path,stacking);
   });
  }
  void ExportFiles(List<Frame> selected){
   if(selected.Count==0)return;var d=new FormWindow(Window,"Export files",610,560);
   d.Text(selected.Count+" selected files",true);d.Text("Original files are copied and verified. Existing files are kept; duplicate names receive a numbered suffix.");
   var destination=ExportDestination(d,"AstroArchive_export_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   Func<ExportOptions> exportOptions=()=>{var choice=destination.Options();choice.Mode="Files";choice.IncludeCalibration=false;choice.IncludeRejected=true;return choice;};
   ExportAfterChoice(d,destination,selected,exportOptions);d.Advanced("More options",()=>d.Add(destination.Metadata));
   d.Accept("Export files",()=>ValidExportDestination(d,destination));if(!d.Show())return;
   RunExport(selected,exportOptions(),destination,false);
  }
  void ExportProject(List<Frame> selected,bool withCalibration){
   var items=selected.Where(f=>f.Kind=="Light"||f.Kind=="Stack").ToList();if(items.Count==0)return;var d=new FormWindow(Window,"Export stacking folder",630,700);string target=items.Select(f=>f.Target).Distinct().Count()==1?items[0].Target:"Multiple targets";d.Text(target+" · "+items.Count(f=>f.Kind=="Light")+" subs · "+items.Count(f=>f.Kind=="Stack")+" stacks",true);
   var destination=ExportDestination(d,Util.Safe(target)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));bool mixed=items.Any(f=>f.Kind=="Light")&&items.Any(f=>f.Kind=="Stack");var mode=mixed?d.Select("Inputs",new[]{"Subs","Stacks","Both"},"Both"):new ComboBox{ItemsSource=new[]{"Subs","Stacks","Both"},SelectedItem=items[0].Kind=="Light"?"Subs":"Stacks"};
   if(mixed)UiHelp.Tip(mode,"Stacks and subs are exported separately. Do not combine stacks with their constituent subs or overlapping live-stack snapshots.");
   var availableCalibrations=Exporter.ExistingCalibrations(repo,all);var calibration=d.Check("Include matching calibrations",withCalibration);calibration.Visibility=items.Any(f=>f.Kind=="Light")?Visibility.Visible:Visibility.Collapsed;calibration.IsEnabled=availableCalibrations.Count>0;
   var availability=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};Theme.Bind(availability,TextBlock.ForegroundProperty,"Muted");if(items.Any(f=>f.Kind=="Light"))d.Add(availability);
   var convert=d.Check("Convert supported images to FITS",false);convert.Visibility=items.Any(Exporter.RequiresConversion)?Visibility.Visible:Visibility.Collapsed;UiHelp.Tip(convert,"Creates derived FITS from linear, decodable inputs. Archived originals stay unchanged.");
   var unknown=d.Check("Confirm unknown-state subs are uncalibrated",false);unknown.Visibility=items.Any(f=>f.Kind=="Light"&&f.Calibration=="Unknown")?Visibility.Visible:Visibility.Collapsed;unknown.IsEnabled=false;UiHelp.Tip(unknown,"Enable only if these subs have not already been calibrated.");
   CheckBox sessions=null,rejected=null;Button review=null;
   Func<ExportOptions> exportOptions=()=>{var choice=destination.Options();choice.Mode=Convert.ToString(mode.SelectedItem);choice.IncludeCalibration=calibration.IsEnabled&&calibration.IsChecked==true;choice.IncludeUnknownCalibration=unknown!=null&&unknown.IsChecked==true;choice.IncludeRejected=rejected!=null&&rejected.IsChecked==true;choice.SeparateSessions=sessions!=null&&sessions.IsChecked==true;choice.ConvertToFits=convert.IsChecked==true;return choice;};ExportAfterChoice(d,destination,items,exportOptions);
   d.Advanced("More options",()=>{sessions=d.Check("Separate sessions into folders",false);d.Add(destination.Metadata);rejected=d.Check("Include rejected/reference files",false);review=d.Button("Review calibration matches…",()=>ShowReport("Calibration matching",CalibrationReport(items.Where(f=>f.Kind=="Light").ToList(),availableCalibrations)));review.Visibility=calibration.Visibility;});
   Action summary=()=>{try{bool subs=Convert.ToString(mode.SelectedItem)!="Stacks";calibration.IsEnabled=subs&&availableCalibrations.Count>0;unknown.IsEnabled=calibration.IsEnabled&&calibration.IsChecked==true;review.IsEnabled=subs;var lights=items.Where(f=>f.Kind=="Light"&&(rejected.IsChecked==true||!f.Rejected)).ToList();int count=calibration.IsEnabled&&calibration.IsChecked==true?Exporter.AvailableCalibrations(lights,availableCalibrations,sessions.IsChecked==true,unknown.IsChecked==true).Count:0;int unknownCount=lights.Count(f=>f.Calibration=="Unknown");availability.Text=!subs?"Stacks receive no additional calibration.":availableCalibrations.Count==0?"No calibration files available.":calibration.IsChecked!=true?"Calibration files omitted.":count>0?count+" matching calibration file"+(count==1?"":"s")+". Inputs with different calibration sets use separate folders.":"No matching calibration files. Review calibration matches for the reason.";if(subs&&calibration.IsChecked==true&&availableCalibrations.Count>0&&unknownCount>0&&unknown.IsChecked!=true)availability.Text=(count>0?availability.Text+" ":"")+unknownCount+" subs have unknown processing state. Confirm they are uncalibrated to include their calibration files.";}catch(Exception error){availability.Text=error.Message;}};
   mode.SelectionChanged+=(s,e)=>{destination.RefreshAfterExport();summary();};foreach(var check in new[]{convert,rejected}){check.Checked+=(s,e)=>destination.RefreshAfterExport();check.Unchecked+=(s,e)=>destination.RefreshAfterExport();}foreach(var check in new[]{sessions,calibration,unknown,rejected}){check.Checked+=(s,e)=>summary();check.Unchecked+=(s,e)=>summary();}summary();
   d.Accept("Export",()=>{var options=exportOptions();if(!items.Any(f=>(options.IncludeRejected||!f.Rejected)&&(options.Mode=="Both"||options.Mode=="Subs"&&f.Kind=="Light"||options.Mode=="Stacks"&&f.Kind=="Stack"))){MessageBox.Show(d.Window,"This input choice has no eligible files.");return false;}return ValidExportDestination(d,destination);});if(d.Show())RunExport(items,exportOptions(),destination,true);
  }
  void ExportComplete(string path){ExportComplete(path,false);}
  void ExportComplete(string path,bool stacking){L("StatusLabel").Text="Exported folder: "+path;if(completionActivity!=null){completionActivity.OutputPath=path;completionActivity.Status=stacking?"Stacking folder is ready. Files copied and verified.":"Files exported and verified.";}}
  FormWindow ExportCompleteDialog(string path,bool stacking){
   var d=new FormWindow(Window,"Export complete",640,360);d.Text(stacking?"Stacking folder ready":"Files exported",true);d.Text(path);d.Text("Copied and verified.");d.Button("Open folder",()=>{try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception error){MessageBox.Show(d.Window,error.Message,"Folder unavailable");}});d.CloseOnly();return d;
  }
  void DeleteFailedFiles(){
   if(repo==null||RepositoryOperationBlocked)return;var matches=repo.FailedFiles();
   if(matches.Count==0){L("StatusLabel").Text="No repository filenames contain 'failed'.";return;}
   DeleteFiles(matches,true);
  }
  void PurgeNonRawFiles(){
   if(repo==null||RepositoryOperationBlocked)return;var matches=repo.NonRawFiles();
   if(matches.Count==0){L("StatusLabel").Text="No PNG/JPG/JPEG files are indexed in this repository.";return;}
   DeleteFiles(matches,false,true);
  }
  void DeleteFiles(List<Frame> selected,bool failedNames=false,bool nonRawFiles=false){
   if(repo==null||RepositoryOperationBlocked||selected.Count==0)return;
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
