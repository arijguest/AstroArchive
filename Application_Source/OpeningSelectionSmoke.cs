// Exercise first opening, selection memory and asynchronous tables with real previews.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeOpeningSelection(string output){
   WaitForSearches();var previousRepo=repo;var previousRows=all;var previousEdited=editedImages;var selected=librarySelection.Items;var editedSelected=editedSelection.Items;
   bool repoOpened=repositoryPageOpened,editedOpened=editedPageOpened,repoPending=openingRepositorySelection,editedPending=openingEditedSelection,show=settings.ShowPreview,refreshDisposed=editedRefreshDisposed;
   int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string mode=Convert.ToString(C("LibraryViewBox").SelectedItem),query=T("SearchBox").Text,editedQuery=T("EditedSearchBox").Text;
   string filters=Util.Serialize(libraryFilters.Values),imageClass=Convert.ToString(C("EditedClassFilter").SelectedItem);var captureSort=tableSorts["FramesGrid"].ToList();var imageSort=tableSorts["EditedGrid"].ToList();
   string directory=Path.Combine(output,"opening-selection-fixture");Repository temporary=null;
   try{
    editedRefreshDisposed=true;CancelAutomaticEditedRefresh();CancelPreview();CancelEditedPreview();GoToPage(1);temporary=new Repository(Path.Combine(directory,"repository"));repo=temporary;
    var frames=new List<Frame>();var paths=new List<string>();string source=Path.Combine(directory,"source");Directory.CreateDirectory(source);
    foreach(string name in new[]{"Light_M31_01.png","Light_M31_02.png","Stack_M42.png"}){
     string path=Path.Combine(source,name);paths.Add(path);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Gray8,null,new byte[]{(byte)(20+paths.Count),60,100,140},2)));using(var stream=File.Create(path))encoder.Save(stream);
     frames.Add(new Frame{Hash=Util.Hash(path,CancellationToken.None),SourcePath=path,OriginalName=name,Status="New",Bytes=new FileInfo(path).Length,Target=name.Contains("M31")?"M31":"M42",Kind=name.StartsWith("Stack")?"Stack":"Light",Session="opening-session",Telescope="Scope",Camera="Telephoto",AcquisitionDate="2026-10-09",Format="PNG",Width=2,Height=2,Channels=1,Images=Assets.Inspect(path).Images});
    }
    if(repo.Import(frames,CancellationToken.None,null).Imported!=3)throw new Exception("Opening selection fixture did not import");repo.AddEditedImages(paths,null,"Opening previews",CancellationToken.None,null);
    librarySelection.Clear();editedSelection.Clear();all=repo.All();T("SearchBox").Clear();T("EditedSearchBox").Clear();libraryFilters.Reset();C("EditedClassFilter").SelectedIndex=0;C("LibraryViewBox").SelectedItem="Session summaries";settings.ShowPreview=true;SetPreviewVisibility();tableSorts["FramesGrid"].Clear();tableSorts["EditedGrid"].Clear();Filter(true);RefreshEdited();WaitForSearches();ResetOpeningSelection();
    GoToPage(0);WaitForSearches();WaitPreview(()=>((Image)Window.FindName("PreviewImage")).Source!=null,"Repository did not load its initial preview");
    var first=G("FramesGrid").Items.OfType<Frame>().First();if(((TargetSummary)Targets.SelectedItem).Name!="All targets"||ActiveSelectedSession==null||!ActiveSelectedSession.Frames.Contains(first)||librarySelection.Count!=2||previewFrame.Hash!=first.Hash)throw new Exception("Opening Repository did not select the first collapsed subgroup and preview its first capture");
    var stack=all.Single(f=>f.Kind=="Stack");Targets.SelectedItem=Targets.Items.OfType<TargetSummary>().Single(t=>t.Name=="M42");WaitForSearches();SelectOnlyTargetRow("FramesGrid",G("FramesGrid").Items.OfType<Frame>().Single());
    GoToPage(2);WaitForSearches();WaitPreview(()=>((Image)Window.FindName("EditedPreviewImage")).Source!=null,"Edited did not load its initial preview");
    if(((TargetSummary)EditedTargets.SelectedItem).Name!="All targets"||ActiveEditedImage!=G("EditedGrid").Items.OfType<EditedImage>().First()||editedSelection.Count!=1)throw new Exception("Opening Edited did not select its first sorted image");
    var chosen=G("EditedGrid").Items.OfType<EditedImage>().Last();SelectOnlyTargetRow("EditedGrid",chosen);GoToPage(0);WaitForSearches();if(((Frame)G("FramesGrid").SelectedItem).Hash!=stack.Hash||((TargetSummary)Targets.SelectedItem).Name!="M42")throw new Exception("Returning to Repository lost its selected target/file");
    GoToPage(2);WaitForSearches();if(ActiveEditedImage.RelativePath!=chosen.RelativePath||editedSelection.Count!=1)throw new Exception("Returning to Edited replaced its previous selection");
    ClearTargetSelection("EditedGrid");GoToPage(0);GoToPage(2);WaitForSearches();if(ActiveEditedImage!=null||editedSelection.Count!=0)throw new Exception("Returning to Edited replaced an intentionally cleared selection");
    GoToPage(1);librarySelection.Clear();C("LibraryViewBox").SelectedItem="Show all files";ResetOpeningSelection();GoToPage(0);WaitForSearches();if(G("FramesGrid").SelectedItem!=G("FramesGrid").Items.OfType<Frame>().First()||librarySelection.Count!=1)throw new Exception("Ungrouped opening did not select the first file");
    GoToPage(1);librarySelection.Clear();string capturePath=repo.Find(frames[0].Hash).RelativePath;all=Enumerable.Range(0,2103).Select(i=>{var frame=frames[0].Clone();frame.Hash=Util.HashText("opening-row-"+i);frame.RelativePath=capturePath;frame.OriginalName="Light_M31_"+i.ToString("D4")+".png";return frame;}).Reverse().ToList();ResetOpeningSelection();GoToPage(0);WaitForSearches();WaitPreview(()=>((Image)Window.FindName("PreviewImage")).Source!=null,"Background repository search did not populate its preview");
    if(G("FramesGrid").Items.Count!=2103||G("FramesGrid").SelectedItem!=G("FramesGrid").Items.OfType<Frame>().First()||librarySelection.Count!=1)throw new Exception("Background repository search selected stale or unsorted rows");
    GoToPage(1);editedSelection.Clear();var gallery=EditedGallery.Read(repo,new string[0],CancellationToken.None).Images;var image=gallery.First();editedImages=gallery.Concat(Enumerable.Range(0,2103).Select(i=>{var clone=Util.Deserialize<EditedImage>(Util.Serialize(image));clone.RelativePath="ZZZ_"+i.ToString("D4")+".png";return clone;})).ToList();ResetOpeningSelection();GoToPage(2);WaitForSearches();WaitPreview(()=>((Image)Window.FindName("EditedPreviewImage")).Source!=null,"Background Edited search did not populate its preview");
    if(G("EditedGrid").Items.Count<2103||ActiveEditedImage!=G("EditedGrid").Items.OfType<EditedImage>().First()||editedSelection.Count!=1)throw new Exception("Background Edited search did not select the first image");
    GoToPage(1);all.Clear();editedImages.Clear();librarySelection.Clear();editedSelection.Clear();ResetOpeningSelection();GoToPage(0);WaitForSearches();GoToPage(2);WaitForSearches();if(librarySelection.Count!=0||editedSelection.Count!=0)throw new Exception("Empty views gained a selection");
    File.WriteAllText(Path.Combine(output,"opening-selection-smoke.txt"),"PASS: Repository grouped/ungrouped and Edited select the first All Targets entry on first opening and load real PNG previews; subsequent visits remember choices and cleared selection; background tables wait for sorted search results; empty views remain empty.");
   }finally{
    CancelPreview();CancelEditedPreview();CancelAutomaticEditedRefresh();GoToPage(1);repo=previousRepo;all=previousRows;editedImages=previousEdited;repositoryPageOpened=repoOpened;editedPageOpened=editedOpened;openingRepositorySelection=repoPending;openingEditedSelection=editedPending;editedRefreshDisposed=refreshDisposed;
    librarySelection.Clear();foreach(var frame in selected)librarySelection.Add(frame);editedSelection.Clear();foreach(var image in editedSelected)editedSelection.Add(image);tableSorts["FramesGrid"]=captureSort;tableSorts["EditedGrid"]=imageSort;libraryFilters.Values.Clear();foreach(var entry in Util.Deserialize<Dictionary<string,string>>(filters))libraryFilters.Values[entry.Key]=entry.Value;T("SearchBox").Text=query;T("EditedSearchBox").Text=editedQuery;C("EditedClassFilter").SelectedItem=imageClass;C("LibraryViewBox").SelectedItem=mode;settings.ShowPreview=show;SetPreviewVisibility();Filter(true);FilterEditedImages();WaitForSearches();GoToPage(page);if(temporary!=null)temporary.Dispose();if(Directory.Exists(directory))Directory.Delete(directory,true);
   }
  }
 }
}
