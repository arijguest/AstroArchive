using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 public partial class MainUi {
  void WaitForTargetInformation(TargetInfoState state,string expected){
   var timer=System.Diagnostics.Stopwatch.StartNew();while(state.View.CurrentId!=expected&&timer.Elapsed.TotalSeconds<15){PumpPopupLayout();Thread.Sleep(10);}
   if(state.View.CurrentId!=expected)throw new Exception("Target information did not load: "+expected);
  }
  public void SmokeTargetInformation(string output){
   Directory.CreateDirectory(output);Window.Show();WaitForSearches();
   var previousRows=all;var previousEdited=editedImages;var previousRepo=repo;var previousView=C("LibraryViewBox").SelectedItem;var manualWidth=editedPreviewManualWidth;double width=Window.Width,height=Window.Height,minWidth=Window.MinWidth;int scale=settings.TextScalePercent,page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string mode=settings.ThemeMode;bool shown=settings.ShowPreview;
   string stage="fixture";
   try{
    openingRepositorySelection=openingEditedSelection=false;
    repo=new Repository(Path.Combine(output,"info-repository"));C("LibraryViewBox").SelectedItem="Show all files";
    all=new System.Collections.Generic.List<Frame>{new Frame{Hash="info-one",Target="M31",Kind="Light",Session="info-session",OriginalName="info.png",RelativePath="info.png"},new Frame{Hash="info-two",Target="M45",Kind="Light",OriginalName="other.png",RelativePath="info.png"},new Frame{Hash="info-three",Target="M31",Kind="Light",Session="info-session",OriginalName="third.png",RelativePath="info.png"}};
    var pixels=Enumerable.Repeat((byte)110,64*48*3).ToArray();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,48,96,96,PixelFormats.Rgb24,null,pixels,64*3)));using(var stream=File.Create(repo.FilePath(all[0])))encoder.Save(stream);
    var project=repo.AddEditedImages(new[]{repo.FilePath(all[0])},null,"Info fixture",CancellationToken.None,null);
    repo.SaveEditedMetadata(project,repo.EditedImages(project),new EditedMetadata{Object="M31"},CancellationToken.None);
    pixels[0]=120;encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,48,96,96,PixelFormats.Rgb24,null,pixels,64*3)));string other=Path.Combine(repo.Root,"other.png");using(var stream=File.Create(other))encoder.Save(stream);
    var otherProject=repo.AddEditedImages(new[]{other},null,"Other target",CancellationToken.None,null);repo.SaveEditedMetadata(otherProject,repo.EditedImages(otherProject),new EditedMetadata{Object="M45"},CancellationToken.None);
    editedImages=EditedGallery.Read(repo,new[]{"M31","M45"},CancellationToken.None).Images;
    Filter(true);FilterEditedImages();WaitForSearches();
    foreach(int tab in new[]{0,2}){
     stage="select target on page "+tab;GoToPage(tab);WaitForSearches();var list=tab==0?Targets:EditedTargets;list.SelectedItem=list.Items.OfType<TargetSummary>().Single(t=>t.Name=="M31");WaitForSearches();
     if(tab==0){librarySelection.Add(all[1]);G("FramesGrid").SelectedItems.Clear();PreviewSelected();WaitForTargetInformation(libraryInfo,"M31");if(librarySelection.Count!=1)throw new Exception("Opening info cleared another target selection.");G("FramesGrid").SelectedItem=all[0];}else{editedSelection.Add(editedImages.Single(i=>i.Metadata.Object=="M45"));G("EditedGrid").SelectedItems.Clear();LoadEditedPreview();WaitForTargetInformation(editedInfo,"M31");if(editedSelection.Count!=1)throw new Exception("Edited info cleared another target selection.");G("EditedGrid").SelectedItem=editedImages.Single(i=>i.Metadata.Object=="M31");}
     if(!PopulateTargetMenu(list))throw new Exception("Target info menu missing.");
     var action=list.ContextMenu.Items.OfType<MenuItem>().Single(i=>Convert.ToString(i.Header)=="Show target info");action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
     var state=tab==0?libraryInfo:editedInfo;string prefix=tab==0?"":"Edited";WaitForTargetInformation(state,"M31");
     PumpPopupLayout();
     var loadedCard=targetInformation.Find("M31",CancellationToken.None).Cards.Single();
     var labels=PopupChildren<TextBlock>(state.View).Select(t=>t.Text).ToList();
     if(!labels.Contains(Localization.T("TARGET INFO"))||!labels.Contains(Localization.T("Sources and catalogue details")))throw new Exception("Info controls did not use the selected language.");
     if(!labels.Contains(loadedCard.name)||!labels.Contains(TargetInfoPresentation.DisplaySummary(loadedCard)))throw new Exception("Translation changed catalogue data.");
     // Collapsed expanders have no rendered content in WPF. Open the credit
     // before checking the text the user can actually read.
     foreach(var expander in PopupChildren<Expander>(state.View))expander.IsExpanded=true;PumpPopupLayout();
     if(!PopupChildren<TextBlock>(state.View).Any(t=>t.Text.Contains(loadedCard.image.creator)||t.Inlines.OfType<Hyperlink>().SelectMany(link=>link.Inlines.OfType<Run>()).Any(run=>run.Text.Contains(loadedCard.image.creator))))throw new Exception("Image creator credit is missing.");
     foreach(var expander in PopupChildren<Expander>(state.View))expander.IsExpanded=false;PumpPopupLayout();
     if((tab==0?librarySelection.Count:editedSelection.Count)!=0||G(tab==0?"FramesGrid":"EditedGrid").SelectedItems.Count!=0)throw new Exception("Info action retained visible/hidden file selection.");
     if(((FrameworkElement)Window.FindName(prefix+"PreviewHost")).Visibility!=Visibility.Collapsed)throw new Exception("Image preview still occupies target information space.");
     stage="responsive layouts on page "+tab;
     foreach(string theme in new[]{"Light","Dark"})foreach(int textScale in new[]{100,150})foreach(double paneWidth in new[]{240.0,570.0}){
      state.PreferredWidth=paneWidth;Window.MinWidth=paneWidth==240?minWidth:1600;Window.Width=paneWidth==240?1060:1600;Window.Height=650;settings.ThemeMode=theme;settings.TextScalePercent=textScale;ApplyAppearance();
      var column=tab==0?(ColumnDefinition)Window.FindName("PreviewColumn"):((Grid)Window.FindName("EditedBody")).ColumnDefinitions[3];if(tab==2)editedPreviewManualWidth=paneWidth;column.Width=new GridLength(paneWidth);PumpPopupLayout();
      var scroll=PopupChildren<ScrollViewer>(state.View).First();if(scroll.ScrollableWidth>0.5||scroll.ViewportHeight<60)throw new Exception("Info pane has horizontal overflow or no reading area.");
      var thumbnail=PopupChildren<Image>(state.View).Single(i=>i.Source is BitmapSource);var thumbnailBitmap=thumbnail.Source as BitmapSource;
      if(thumbnail.ActualWidth>scroll.ViewportWidth+1||thumbnail.ActualHeight>341||Math.Abs(thumbnail.ActualWidth/thumbnail.ActualHeight-thumbnailBitmap.PixelWidth/(double)thumbnailBitmap.PixelHeight)>0.01)throw new Exception("Thumbnail is cropped, stretched, excessive or wider than the pane.");
      var map=PopupChildren<SkyGlobeView>(state.View).Single();if(!map.Context.HasPosition||map.Context.HasHorizon)throw new Exception("Target map lost its position or inferred a horizon.");
      scroll.ScrollToEnd();PumpPopupLayout();if(scroll.VerticalOffset<scroll.ScrollableHeight-1)throw new Exception("Info credits cannot be reached in a short window.");scroll.ScrollToTop();
      var body=(Grid)Window.FindName(tab==0?"LibraryBody":"EditedBody");var pane=(FrameworkElement)Window.FindName(prefix+"PreviewPane");if(pane.TranslatePoint(new Point(pane.ActualWidth,0),body).X>body.ActualWidth+1)throw new Exception("Target info pane overflows the window.");
      var root=(FrameworkElement)Window.Content;if(pane.TranslatePoint(new Point(pane.ActualWidth,0),root).X>root.ActualWidth+1)throw new Exception("Target info pane is clipped by the window.");
      Capture(Path.Combine(output,"AstroArchive_Info_"+tab+"_"+theme+"_"+textScale+"_"+paneWidth+".png"));
      foreach(var expander in PopupChildren<Expander>(state.View))expander.IsExpanded=true;PumpPopupLayout();scroll.ScrollToEnd();PumpPopupLayout();if(scroll.ScrollableWidth>0.5||scroll.VerticalOffset<scroll.ScrollableHeight-1)throw new Exception("Expanded sources and credits do not fit the pane.");
      foreach(var expander in PopupChildren<Expander>(state.View))expander.IsExpanded=false;scroll.ScrollToTop();
     }
     Window.MinWidth=minWidth;Window.Width=1060;PumpPopupLayout();var resizedRoot=(FrameworkElement)Window.Content;var resizedPane=(FrameworkElement)Window.FindName(prefix+"PreviewPane");if(resizedPane.TranslatePoint(new Point(resizedPane.ActualWidth,0),resizedRoot).X>resizedRoot.ActualWidth+1)throw new Exception("Shrinking the window clipped a widened information pane.");
     stage="session selection on page "+tab;
     if(tab==0){
      C("LibraryViewBox").SelectedItem="Session summaries";Filter(false);WaitForSearches();SelectSession(subframeSessions.First(),System.Windows.Input.ModifierKeys.None);
      OpenTargetInformation(list);WaitForTargetInformation(state,"M31");if(subframeSessions.Any(g=>g.IsSelected)||librarySelection.Count!=0)throw new Exception("Info action retained a selected session group.");
      C("LibraryViewBox").SelectedItem="Show all files";Filter(false);WaitForSearches();settings.ShowPreview=false;SetPreviewVisibility();OpenTargetInformation(list);WaitForTargetInformation(state,"M31");if(!settings.ShowPreview)throw new Exception("Info action did not reveal the hidden preview pane.");
     }
     stage="image preview on page "+tab;
     if(tab==0)G("FramesGrid").SelectedItem=all[0];else G("EditedGrid").SelectedItem=editedImages.Single(i=>i.Metadata.Object=="M31");PumpPopupLayout();
     if(((FrameworkElement)Window.FindName(prefix+"TargetInfoPane")).Visibility!=Visibility.Collapsed)throw new Exception("Selecting a file did not restore the image preview.");
     var image=(Image)Window.FindName(prefix+"PreviewImage");var timer=System.Diagnostics.Stopwatch.StartNew();while(image.Source==null&&timer.Elapsed.TotalSeconds<15){PumpPopupLayout();Thread.Sleep(10);}if(image.Source==null)throw new Exception("Returning from info did not decode the selected file.");
     ClearTargetSelection(tab==0?"FramesGrid":"EditedGrid");WaitForTargetInformation(state,"M31");
     LoadTargetInformation(prefix,"M45",null);LoadTargetInformation(prefix,"M31",null);WaitForTargetInformation(state,"M31");
     var reset=PopupChildren<Button>(state.View).Single(b=>b.Content is System.Windows.Shapes.Path);var testedMap=PopupChildren<SkyGlobeView>(state.View).Single();double home=testedMap.Camera.Yaw;testedMap.RotateView(25,10);reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Math.Abs(testedMap.Camera.Yaw-home)>0.001||reset.BorderThickness!=new Thickness(0))throw new Exception("Info sky reset differs from the preview control.");
     stage="southern target on page "+tab;var southern=targetInformation.Find("NGC6744",CancellationToken.None).Cards.Single();state.View.ShowCard(southern);PumpPopupLayout();var southMap=PopupChildren<SkyGlobeView>(state.View).Single();southMap.BringIntoView();PumpPopupLayout();if(!southMap.TargetMarkerDrawn)throw new Exception("Southern target is hidden behind the default globe view.");state.View.ShowCard(targetInformation.Find("M31",CancellationToken.None).Cards.Single());
    }
    stage="IC 434 image";var ic434=targetInformation.Find("IC434",CancellationToken.None).Cards.Single();editedInfo.View.ShowCard(ic434);PumpPopupLayout();if(!PopupChildren<Image>(editedInfo.View).Any(i=>i.Source is BitmapSource)||!PopupChildren<TextBlock>(editedInfo.View).Any(t=>t.Text.Contains("Orion")))throw new Exception("IC 434 has no visible credited image or full constellation name.");Capture(Path.Combine(output,"AstroArchive_Info_IC434.png"));
    stage="catalogue name preservation";GoToPage(0);WaitForSearches();OpenTargetInformation(Targets);WaitForTargetInformation(libraryInfo,"M31");
    libraryInfo.View.ShowCard(targetInformation.Find("Moon",CancellationToken.None).Cards.Single());PumpPopupLayout();
    if(!PopupChildren<TextBlock>(libraryInfo.View).Any(t=>t.Text=="Moon"))throw new Exception("A catalogue name was translated as an interface label.");
    File.WriteAllText(Path.Combine(output,"target-info-smoke.txt"),"PASS "+Localization.Current+": Repository/Edited context actions clear visible and retained selections; image/info transitions; offline map; short and narrow layouts; light/dark and 100/150% text; translated controls, unchanged catalogue data/attribution; credits reachable without horizontal scrolling.");
   }catch(Exception error){throw new Exception("Target information UI failed at "+stage+" ("+Localization.Current+").",error);}
   finally{CancelPreview();CancelEditedPreview();HideTargetInformation("");HideTargetInformation("Edited");if(repo!=null&&repo!=previousRepo)repo.Dispose();repo=previousRepo;all=previousRows;editedImages=previousEdited;librarySelection.Clear();editedSelection.Clear();C("LibraryViewBox").SelectedItem=previousView;Window.MinWidth=minWidth;Window.Width=width;Window.Height=height;settings.ThemeMode=mode;settings.TextScalePercent=scale;settings.ShowPreview=shown;editedPreviewManualWidth=manualWidth;ApplyAppearance();SetPreviewVisibility();Filter(true);FilterEditedImages();WaitForSearches();GoToPage(page);}
  }
 }
}
