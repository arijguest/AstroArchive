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
  void BuildFileMenu(ContextMenu menu,List<Frame> selected){
   menu.Items.Clear();menu.Items.Add(new MenuItem{Header=selected.Count+" selected file"+(selected.Count==1?"":"s"),IsEnabled=false});menu.Items.Add(FileAction("Preview…",()=>PreviewImage(selected[0]),selected.Count==1));menu.Items.Add(StarAction(selected.Cast<IAnnotated>().ToList()));menu.Items.Add(NotesAction(selected.Cast<IAnnotated>().ToList()));menu.Items.Add(FileAction("Edit metadata…",()=>Edit(false,selected)));menu.Items.Add(FileAction("Set capture town / city…",()=>SetCaptureSite(false,selected)));menu.Items.Add(ExportMenu(selected));menu.Items.Add(FileAction("Create Edited copies…",()=>CreateEditedCopies(selected)));menu.Items.Add(FileAction("Open file location",()=>ShowFile(selected[0]),selected.Count==1));
   var more=Branch("More actions",FileAction("Copy file paths",()=>Clipboard.SetText(string.Join(Environment.NewLine,selected.Select(repo.FilePath)))),FileAction("Identify target…",()=>Identify(false),selected.Any(f=>f.Kind=="Light"||f.Kind=="Stack"||f.Kind=="Unknown")),FileAction("Review detected metadata…",()=>ReviewMetadata(selected)));
   if(selected.Count==1&&selected[0].Images!=null&&(selected[0].Images.Count>1||selected[0].Images.Any(i=>i.Count>1)))more.Items.Add(FileAction("Choose HDU / page / frame…",()=>PreviewFile(selected[0])));
   menu.Items.Add(more);menu.Items.Add(new Separator());var delete=FileAction("Delete files…",()=>DeleteFiles(selected));delete.Foreground=new SolidColorBrush(Color.FromRgb(183,40,51));menu.Items.Add(delete);
  }
  void ShowFile(Frame frame){string path=repo.FilePath(frame);if(File.Exists(path))Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+path+"\""){UseShellExecute=true});else LocalizedMessage.Show(Window,"This file is missing from the repository.","File unavailable");}
  void ExportComplete(string path){ExportComplete(path,false);}
  void ExportComplete(string path,bool stacking){L("StatusLabel").Text="Exported folder: "+path;if(completionActivity!=null){completionActivity.OutputPath=path;completionActivity.Status=stacking?"Stacking folder is ready. Files copied and verified.":"Files exported and verified.";}}
  FormWindow ExportCompleteDialog(string path,bool stacking){
   var d=new FormWindow(Window,"Export complete",640,360);d.Text(stacking?"Stacking folder ready":"Files exported",true);d.Text(path);d.Text("Copied and verified.");d.Button("Open folder",()=>{try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception error){LocalizedMessage.Show(d.Window,error.Message,"Folder unavailable");}});d.CloseOnly();return d;
  }
  void DeleteFailedFiles(){
   if(repo==null||RepositoryOperationBlocked)return;var matches=repo.FailedFiles();
   if(matches.Count==0){L("StatusLabel").Text="No repository filenames contain 'failed'.";return;}
   DeleteFiles(matches,true);
  }
  void PurgeNonRawFiles(){
   if(repo==null||RepositoryOperationBlocked)return;var matches=repo.NonRawFiles();
   if(matches.Count==0){L("StatusLabel").Text="No PNG/JPG/JPEG/MP4 files are indexed in this repository.";return;}
   DeleteFiles(matches,false,true);
  }
  void DeleteFiles(List<Frame> selected,bool failedNames=false,bool nonRawFiles=false){
   if(repo==null||RepositoryOperationBlocked||selected.Count==0)return;
   var d=new FormWindow(Window,nonRawFiles?"Purge non-raw files":failedNames?"Delete failed":"Delete selected files",640,520);d.Text("Delete "+selected.Count+" "+(failedNames||nonRawFiles?"matching":"selected")+" file"+(selected.Count==1?"":"s")+"?",true);d.Text(repo.Root);
   if(failedNames)d.Text("Searches the entire active repository for filenames containing 'failed', regardless of case. Current filters and telescope selection do not limit this action.");
   if(nonRawFiles)d.Text("Finds indexed PNG/JPG/JPEG/MP4 files in the entire active repository, regardless of case or current filters. Edited images, FITS, TIFF, XISF, SER and camera RAW files are excluded from this purge.");
   d.Text("This permanently removes the selected repository copies. Deletion history is retained so future telescope imports skip the same captures. Source copies, other archive files and shared session metadata stay. Cloud-synced deletions propagate to the cloud.");
   d.Add(new TextBox{Text=string.Join("\r\n",selected.Select(f=>f.RelativePath)),IsReadOnly=true,Height=170,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
   d.Accept(nonRawFiles?"Purge non-raw files":failedNames?"Delete failed files":"Delete selected files",()=>true,true);if(!d.Show())return;CancelPreview();
   Run(ct=>{var result=repo.DeleteFrames(selected,ct,Progress);return result.Deleted+" selected files deleted."+(result.Errors.Count==0?"":"\r\n\r\n"+string.Join("\r\n",result.Errors));},message=>{plan=null;FilterImports();L("StatusLabel").Text=message.Split('\n')[0];if(message.Contains("\n"))ShowReport("File deletion report",message);});
  }
 }
}
