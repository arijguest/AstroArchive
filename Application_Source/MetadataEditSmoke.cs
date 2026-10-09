// Native WPF coverage for current values, mixed selections and theme/accessibility.
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
  void SmokeMetadataEditor(string output){
   SmokeMetadataPreviewRelease(output);
   var first=new Frame{OriginalName="Light_M33.fit",Target="M33",Telescope="Unit-01",Model="Custom telescope",Camera="Telephoto",Kind="Light",Mount="Unknown",Exposure=30.123456789,Gain=0,Temperature=-12.75,Filter="Broadband",Calibration="Custom calibration",BinX=2,BinY=1,TelescopeModel="Reflector",CameraModel="Camera 1",CameraId="Serial 123",Offset=0,ReadoutMode="Slow",Roi="0,0,128,96",OpticalConfiguration="Reducer",TimeZoneId="UTC",LinearData=false,GainUnit="dB",Bayer="RGGB",RegistrationState="Unregistered",CalibrationSteps="Dark, flat",Notes="Recorded notes",Facts=new Dictionary<string,MetadataFact>{{"Exposure",new MetadataFact{Value="30.123456789",Source="FITS header",Unit="s"}}}};
   var second=first.Clone();second.OriginalName="Light_M45.fit";second.Target="M45";second.Exposure=60;second.CameraId=null;second.LinearData=true;
   string originalTheme=settings.ThemeMode;int originalScale=settings.TextScalePercent;
   try{
    foreach(string mode in new[]{"Dark","Light"})foreach(double scale in new[]{1.0,1.5}){
     settings.ThemeMode=mode;settings.TextScalePercent=(int)(scale*100);ApplyAppearance();
     foreach(bool batch in new[]{false,true}){
      var editor=new MetadataEditor(Window,batch?new List<Frame>{first,second}:new List<Frame>{first});
      try{
       editor.Form.Window.Show();PumpPopupLayout();
       if(editor.Model.Patch().Count!=0)throw new Exception("Opening metadata editor generated overrides.");
       foreach(var entry in editor.Inputs){var box=entry.Value as TextBox;var combo=entry.Value as ComboBox;string shown=box!=null?box.Text:Convert.ToString(combo.SelectedItem);var value=editor.Model[entry.Key];string expected=value.Field.Options.Length>0&&value.Initial.Length==0?value.KeepLabel:value.Initial;if(shown!=expected)throw new Exception("Current metadata missing: "+entry.Key);Readable(entry.Value.Foreground,entry.Value.Background,mode+" metadata "+entry.Key);}
       if(!batch&&((ComboBox)editor.Inputs["Mount"]).SelectedItem.ToString()!="EQ?")throw new Exception("Unknown mount suggestion hidden.");
       if(batch&&(!editor.Model["Exposure"].Mixed||!editor.Model["LinearData"].Mixed||!editor.Model["CameraId"].Mixed))throw new Exception("Batch mixed metadata not identified.");
       var tabs=PopupChildren<TabControl>(editor.Form.Window).Single();
       for(int tab=0;tab<4;tab++){
        tabs.SelectedIndex=tab;PumpPopupLayout();
        foreach(var control in editor.Inputs.Values.Where(c=>c.IsVisible)){if(control.ActualWidth<120||control.ActualHeight<=0)throw new Exception("Metadata input clipped: "+control.Name);}
        foreach(var label in PopupChildren<TextBlock>(editor.Form.Window).Where(t=>t.IsVisible&&!string.IsNullOrWhiteSpace(t.Text))){
         Brush background=editor.Form.Window.Background;
         for(DependencyObject parent=VisualTreeHelper.GetParent(label);parent!=null;parent=VisualTreeHelper.GetParent(parent)){
          // A Control.Background need not be painted behind its text (CheckBox).
          // Use the rendered container surfaces in the visual tree instead.
          var border=parent as Border;var panel=parent as Panel;Brush surface=border!=null?border.Background:panel!=null?panel.Background:null;var solid=surface as SolidColorBrush;
          if(solid!=null&&solid.Color.A==255){background=surface;break;}
         }
         Readable(label.Foreground,background,mode+" metadata label: "+label.Text+" ("+label.Foreground+" on "+background+")");
        }
        if(scale==1.0&&!batch)CapturePopup(editor.Form.Window,Path.Combine(output,"AstroArchive_Metadata_"+mode+"_"+tab+".png"));
       }
       var picker=PopupChildren<ComboBox>(tabs).Single();picker.SelectedIndex=batch?1:0;PumpPopupLayout();var table=PopupChildren<DataGrid>(tabs).Single();if(!table.Items.Cast<MetadataDetail>().Any(r=>r.Field=="Original Name"&&r.Value==(batch?second:first).OriginalName))throw new Exception("Current metadata viewer did not follow selected file.");
       tabs.SelectedIndex=0;((TextBox)editor.Inputs["Filter"]).Text="Ha";PumpPopupLayout();if(editor.Model.Patch().Count!=1||editor.Model.Patch().Apply(second).Exposure!=60)throw new Exception("Native edit copied another field.");
       var reset=PopupChildren<Button>(editor.Form.Window).Single(b=>Convert.ToString(b.Content)=="Reset changes");reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(editor.Model.Patch().Count!=0)throw new Exception("Native reset retained edits.");
       if(scale==1.5){editor.Form.Window.Width=480;PumpPopupLayout();tabs.SelectedIndex=2;PumpPopupLayout();foreach(var control in editor.Inputs.Values.Where(c=>c.IsVisible))if(control.ActualWidth<180)throw new Exception("Narrow metadata form failed to reflow.");}
      }finally{editor.Form.Window.Close();}
     }
    }
   }finally{settings.ThemeMode=originalTheme;settings.TextScalePercent=originalScale;ApplyAppearance();}
  }
  void SmokeMetadataPreviewRelease(string output){
   WaitForSearches();var previousRepo=repo;var previousRows=all;var previousPlan=plan;var previousEdited=editedImages;var selected=librarySelection.Items;var editedSelected=editedSelection.Items;bool show=settings.ShowPreview;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   string directory=Path.Combine(output,"metadata-preview-fixture");Repository temporary=null;var release=new ManualResetEventSlim();var started=new ManualResetEventSlim();DispatcherTimer timer=null;
   try{
    CancelPreview();CancelEditedPreview();CancelAutomaticEditedRefresh();temporary=new Repository(directory);repo=temporary;plan=null;GoToPage(0);settings.ShowPreview=true;SetPreviewVisibility();librarySelection.Clear();editedSelection.Clear();
    string gif=Path.Combine(directory,"preview.gif");File.WriteAllBytes(gif,Convert.FromBase64String("R0lGODlhAgACAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQICgAAACwAAAAAAgACAAAIBgABCAQQEAAh+QQIFAAAACwAAAAAAgACAIEAAP8AAAAAAAAAAAAIBgABCAQQEAA7"));
    var frame=new Frame{Hash=Util.Hash(gif,CancellationToken.None),RelativePath="preview.gif",OriginalName="preview.gif",Target="M31",Kind="Light",Telescope="Before-01",Format="GIF",Width=2,Height=2};repo.Save(frame);all=new List<Frame>{frame};Filter(true);WaitForSearches();G("FramesGrid").SelectedItem=displayed.Single();PreviewSelected();
    var project=repo.AddEditedImages(new[]{gif},null,"Preview fixture",CancellationToken.None,null);editedImages=EditedGallery.Read(repo,new[]{"M31"},CancellationToken.None).Images;FilterEditedImages();WaitForSearches();G("EditedGrid").SelectedIndex=0;LoadEditedPreview();
    if(previewMotion==null||editedMotion==null)throw new Exception("Metadata playback fixtures did not open.");
    Exception failure=null;bool dialogChecked=false;timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
    timer.Tick+=(s,e)=>{var dialog=Window.OwnedWindows.Cast<Window>().FirstOrDefault(w=>w.Title=="Edit metadata");if(dialog==null||dialogChecked)return;dialogChecked=true;
     try{
      if(!metadataPreviewSuspended||previewMotion!=null||editedMotion!=null||((Image)Window.FindName("PreviewImage")).Source!=null||((Image)Window.FindName("EditedPreviewImage")).Source!=null)throw new Exception("Metadata editor retained sidebar playback or pixels.");
      using(new FileStream(gif,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}using(new FileStream(repo.EditedPath(project,editedImages[0].RelativePath),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
      PreviewSelected();LoadPreview(gif,true,frame);LoadEditedPreview();if(previewMotion!=null||editedMotion!=null)throw new Exception("Selection restarted playback behind metadata editor.");
     }catch(Exception error){failure=error;}finally{dialog.DialogResult=false;}
    };timer.Start();EditEditedMetadata();WaitPreview(()=>dialogChecked&&!metadataPreviewSuspended,"Metadata cancellation did not finish");timer.Stop();if(failure!=null)throw failure;if(previewMotion==null)throw new Exception("Canceling metadata did not resume the active preview.");
    CancelPreview();CancelEditedPreview();string png=Path.Combine(directory,"preview.png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Gray8,null,new byte[]{20,60,100,140},2)));using(var stream=File.Create(png))encoder.Save(stream);
    frame=new Frame{Hash=Util.Hash(png,CancellationToken.None),RelativePath="preview.png",OriginalName="preview.png",Target="M31",Kind="Light",Telescope="Before-01",Format="PNG",Width=2,Height=2};repo.Save(frame);all=repo.All();Filter(true);WaitForSearches();ClearTargetSelection("FramesGrid");G("FramesGrid").SelectedItem=displayed.Single(f=>f.Hash==frame.Hash);CancelPreview();previewCache.Clear();
    LoadPreview(png,true,frame,(path,token,context)=>{using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){started.Set();if(!release.Wait(5000))throw new Exception("Held preview decoder was not released.");token.ThrowIfCancellationRequested();return ProgressiveFixture();}});
    WaitPreview(()=>started.IsSet,"Preview decoder did not hold the fixture file");bool released=false;dialogChecked=false;failure=null;
    timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};timer.Tick+=(s,e)=>{
     var dialog=Window.OwnedWindows.Cast<Window>().FirstOrDefault(w=>w.Title=="Edit metadata");
     if(!released){if(dialog!=null)failure=new Exception("Metadata editor opened before its preview read ended.");released=true;release.Set();return;}
     if(dialog==null||dialogChecked)return;dialogChecked=true;try{
      using(new FileStream(png,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
      if(!metadataPreviewSuspended||((Image)Window.FindName("PreviewImage")).Source!=null)throw new Exception("Pending preview repainted behind the metadata editor.");
      PopupChildren<TabControl>(dialog).Single().SelectedIndex=1;PumpPopupLayout();PopupChildren<TextBox>(dialog).Single(box=>box.Name=="MetadataTelescope").Text="After-01";PopupChildren<Button>(dialog).Single(button=>Convert.ToString(button.Content)=="Save metadata").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
     }catch(Exception error){failure=error;dialog.DialogResult=false;}
    };timer.Start();Edit(false);WaitPreview(()=>dialogChecked&&!metadataPreviewSuspended&&cancel==null,"Metadata save did not finish after releasing the preview");timer.Stop();if(failure!=null)throw failure;
    var updated=repo.Find(frame.Hash);if(updated.Telescope!="After-01"||File.Exists(png)||!File.Exists(repo.FilePath(updated))||Util.Hash(repo.FilePath(updated),CancellationToken.None)!=frame.Hash)throw new Exception("Metadata save failed to move the selected file intact.");
    WaitPreview(()=>previewData!=null&&((Image)Window.FindName("PreviewImage")).Source!=null,"Saved metadata did not resume the preview at its new path");
    File.WriteAllText(Path.Combine(output,"metadata-preview-smoke.txt"),"PASS capture and Edited playback streams released on metadata open; preview reload suppressed while modal editor is open; cancel restores active preview; held decoder drained with responsive dispatcher; telescope metadata saves and relocates unchanged bytes; preview resumes at the saved path.");
   }finally{
    release.Set();if(timer!=null)timer.Stop();foreach(var dialog in Window.OwnedWindows.Cast<Window>().Where(w=>w.Title=="Edit metadata").ToArray())dialog.Close();CancelPreview();CancelEditedPreview();metadataPreviewSuspended=false;CancelAutomaticEditedRefresh();repo=previousRepo;all=previousRows;plan=previousPlan;editedImages=previousEdited;librarySelection.Clear();foreach(var frame in selected)librarySelection.Add(frame);editedSelection.Clear();foreach(var image in editedSelected)editedSelection.Add(image);settings.ShowPreview=show;SetPreviewVisibility();Filter(true);FilterEditedImages();WaitForSearches();GoToPage(page);if(temporary!=null)temporary.Dispose();if(Directory.Exists(directory))Directory.Delete(directory,true);
   }
  }
 }
}
