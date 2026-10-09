using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeSaveMetadataBatch(Action open,Action<Window> edit){
   bool checkedDialog=false;Exception failure=null;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
   timer.Tick+=(s,e)=>{
    var dialog=Window.OwnedWindows.Cast<Window>().FirstOrDefault(w=>w.Title=="Edit metadata");if(dialog==null||checkedDialog)return;checkedDialog=true;
    try{
     if(!PopupChildren<TextBlock>(dialog).Any(t=>t.Text.Contains("all 2 selected")&&t.Text.Contains("by default")))throw new Exception("Batch scope was not shown before saving.");
     var sessions=PopupChildren<CheckBox>(dialog).FirstOrDefault(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Apply changes to entire selected sessions");if(sessions!=null&&sessions.IsChecked==true)throw new Exception("Batch editor expanded beyond the selected files by default.");
     edit(dialog);PopupChildren<Button>(dialog).Single(b=>Convert.ToString(b.Content)=="Save metadata").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }catch(Exception error){failure=error;dialog.DialogResult=false;}
   };
   try{timer.Start();open();WaitPreview(()=>checkedDialog&&!metadataPreviewSuspended&&cancel==null,"Batch metadata save did not finish");if(failure!=null)throw failure;}finally{timer.Stop();}
  }
  void SmokeMetadataBatchEditing(string output){
   WaitForSearches();var previousRepo=repo;var previousRows=all;var previousPlan=plan;var previousEdited=editedImages;var selected=librarySelection.Items;var editedSelected=editedSelection.Items;bool show=settings.ShowPreview;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   string directory=Path.Combine(output,"metadata-batch-fixture");Repository temporary=null;
   try{
    CancelPreview();CancelEditedPreview();CancelAutomaticEditedRefresh();temporary=new Repository(Path.Combine(directory,"repository"));repo=temporary;plan=null;GoToPage(0);settings.ShowPreview=false;SetPreviewVisibility();librarySelection.Clear();editedSelection.Clear();
    string source=Path.Combine(directory,"source");Directory.CreateDirectory(source);var paths=new List<string>();var frames=new List<Frame>();
    foreach(string target in new[]{"M31","M45","M42"}){
     string path=Path.Combine(source,target+".png");paths.Add(path);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Gray8,null,new byte[]{(byte)(20+paths.Count),60,100,140},2)));using(var stream=File.Create(path))encoder.Save(stream);
     frames.Add(new Frame{Hash=Util.Hash(path,CancellationToken.None),SourcePath=path,OriginalName=Path.GetFileName(path),Status="New",Bytes=new FileInfo(path).Length,Target=target,Kind="Light",Session="shared-batch-session",Telescope="Scope",Filter="Broadband",Exposure=paths.Count*10,Format="PNG",Width=2,Height=2,Images=Assets.Inspect(path).Images});
    }
    var imported=repo.Import(frames,CancellationToken.None,null);if(imported.Imported!=3)throw new Exception("Metadata batch fixture failed to import: "+string.Join("; ",imported.Errors));all=repo.All();Filter(true);WaitForSearches();librarySelection.Clear();foreach(var frame in all.Where(f=>f.Target!="M42"))librarySelection.Add(frame);RestoreTargetSelection("FramesGrid");
    var menu=ThemedMenu();BuildFileMenu(menu,SelectedFiles());SmokeSaveMetadataBatch(()=>menu.Items.OfType<MenuItem>().Single(i=>Convert.ToString(i.Header)=="Edit metadata…").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)),dialog=>PopupChildren<TextBox>(dialog).Single(box=>box.Name=="MetadataFilter").Text="Ha");
    var saved=repo.All();if(saved.Count(f=>f.Filter=="Ha")!=2||saved.Single(f=>f.Target=="M42").Filter!="Broadband"||saved.Any(f=>f.Exposure!=frames.Single(original=>original.Hash==f.Hash).Exposure))throw new Exception("Repository batch metadata did not update exactly the two selected files.");
    GoToPage(1);plan=new ImportPlan{Frames=frames.Select(f=>{var clone=f.Clone();clone.Status="New";return clone;}).ToList()};FilterImports();WaitForSearches();foreach(var frame in G("ImportGrid").Items.OfType<Frame>().Where(f=>f.Target!="M42"))G("ImportGrid").SelectedItems.Add(frame);
    var importMenu=BuildImportTools();SmokeSaveMetadataBatch(()=>importMenu.Items.OfType<MenuItem>().Single(i=>Convert.ToString(i.Header)=="Edit selected metadata…").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)),dialog=>PopupChildren<TextBox>(dialog).Single(box=>box.Name=="MetadataFilter").Text="OIII");
    if(plan.Frames.Count(f=>f.Filter=="OIII")!=2||plan.Frames.Single(f=>f.Target=="M42").Filter!="Broadband"||plan.Frames.Any(f=>f.Exposure!=frames.Single(original=>original.Hash==f.Hash).Exposure))throw new Exception("Import metadata did not update the selected batch independently of its session.");
    GoToPage(2);repo.AddEditedImages(paths.Take(1),null,"First batch project",CancellationToken.None,null);repo.AddEditedImages(paths.Skip(1),null,"Second batch project",CancellationToken.None,null);RefreshEdited();WaitForSearches();editedSelection.Clear();foreach(var image in editedImages.Where(i=>i.Metadata.Object!="M42"))editedSelection.Add(image);RestoreTargetSelection("EditedGrid");
    var editedMenu=ThemedMenu();BuildEditedFileMenu(editedMenu,SelectedEditedImages());SmokeSaveMetadataBatch(()=>editedMenu.Items.OfType<MenuItem>().Single(i=>Convert.ToString(i.Header)=="Edit metadata…").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)),dialog=>PopupChildren<TextBox>(dialog).Single(box=>System.Windows.Automation.AutomationProperties.GetName(box)=="Filters / channels").Text="L, Ha");
    var edited=EditedGallery.Read(repo,new string[0],CancellationToken.None).Images;if(edited.Count(i=>i.Metadata.Filters=="L, Ha")!=2||edited.Single(i=>i.Metadata.Object=="M42").Metadata.Filters=="L, Ha")throw new Exception("Edited metadata did not save the batch across both projects.");
    File.WriteAllText(Path.Combine(output,"metadata-batch-smoke.txt"),"PASS: menu-driven saves update all selected Repository, Import and Edited files by default, across targets/projects, preserve untouched values and leave unselected session members unchanged.");
   }finally{
    foreach(var dialog in Window.OwnedWindows.Cast<Window>().Where(w=>w.Title=="Edit metadata").ToArray())dialog.Close();CancelPreview();CancelEditedPreview();metadataPreviewSuspended=false;CancelAutomaticEditedRefresh();repo=previousRepo;all=previousRows;plan=previousPlan;editedImages=previousEdited;librarySelection.Clear();foreach(var frame in selected)librarySelection.Add(frame);editedSelection.Clear();foreach(var image in editedSelected)editedSelection.Add(image);settings.ShowPreview=show;SetPreviewVisibility();Filter(true);FilterImports();FilterEditedImages();WaitForSearches();GoToPage(page);if(temporary!=null)temporary.Dispose();if(Directory.Exists(directory))Directory.Delete(directory,true);
   }
  }
 }
}
