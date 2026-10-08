using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  static void WaitPreview(Func<bool> complete,string failure){
   var frame=new DispatcherFrame();DateTime until=DateTime.UtcNow.AddSeconds(8);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
   timer.Tick+=(s,e)=>{if(complete()||DateTime.UtcNow>=until)frame.Continue=false;};timer.Start();try{Dispatcher.PushFrame(frame);}finally{timer.Stop();}if(!complete())throw new Exception(failure);
  }
  static byte[] PreviewPixel(Image image){byte[] pixel=new byte[4];((BitmapSource)image.Source).CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);return pixel;}
  void SmokeMedia(string output){
   string directory=Path.Combine(Path.GetTempPath(),"AstroArchive-media-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);Window popup=null;MotionPreview motion=null;
   try{
    string gif=Path.Combine(directory,"processing.gif");File.WriteAllBytes(gif,Convert.FromBase64String("R0lGODlhAgACAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQICgAAACwAAAAAAgACAAAIBgABCAQQEAAh+QQIFAAAACwAAAAAAgACAIEAAP8AAAAAAAAAAAAIBgABCAQQEAA7"));
    var host=new Grid();var stage=new Grid();var image=new Image();stage.Children.Add(image);host.Children.Add(stage);var viewport=new PreviewViewport(host,stage,image);
    popup=new Window{Owner=Window,Width=360,Height=440,Content=host};popup.Show();PumpPopupLayout();string failure=null;
    motion=new MotionPreview(viewport,gif,info=>{},message=>failure=message);motion.Start();if(failure!=null||image.Source==null||!viewport.HasPlaybackControl)throw new Exception("GIF first frame/playback control missing: "+failure);
    var first=PreviewPixel(image);WaitPreview(()=>!first.SequenceEqual(PreviewPixel(image)),"GIF preview remained on its first frame.");
    viewport.TogglePlayback();if(motion.Playing)throw new Exception("GIF Pause button did not pause.");var paused=PreviewPixel(image);DateTime pausedUntil=DateTime.UtcNow.AddMilliseconds(350);WaitPreview(()=>DateTime.UtcNow>=pausedUntil,"Pause wait failed.");if(!paused.SequenceEqual(PreviewPixel(image)))throw new Exception("Paused GIF continued animating.");
    viewport.TogglePlayback();WaitPreview(()=>!paused.SequenceEqual(PreviewPixel(image)),"GIF Play button did not resume.");motion.Dispose();motion=null;if(viewport.HasPlaybackControl)throw new Exception("Playback control survived disposal.");
    string ser=Path.Combine(directory,"planet.ser");byte[] recording=new byte[178+8];System.Text.Encoding.ASCII.GetBytes("LUCAM-RECORDER").CopyTo(recording,0);BitConverter.GetBytes(2).CopyTo(recording,26);BitConverter.GetBytes(2).CopyTo(recording,30);BitConverter.GetBytes(8).CopyTo(recording,34);BitConverter.GetBytes(2).CopyTo(recording,38);for(int i=178;i<182;i++)recording[i]=30;for(int i=182;i<186;i++)recording[i]=220;File.WriteAllBytes(ser,recording);viewport.SetImage(null,true);
    motion=new MotionPreview(viewport,ser,info=>{},message=>failure=message);motion.Start();WaitPreview(()=>image.Source!=null||failure!=null,"SER preview did not load.");if(failure!=null)throw new Exception(failure);first=PreviewPixel(image);WaitPreview(()=>!first.SequenceEqual(PreviewPixel(image)),"SER frames did not play.");viewport.TogglePlayback();if(motion.Playing||!viewport.HasPlaybackControl)throw new Exception("SER pause button missing.");motion.Dispose();motion=null;
    SavePopup(host,Path.Combine(output,"AstroArchive_Media_Preview.png"));
    File.WriteAllText(Path.Combine(output,"media-smoke.txt"),"PASS animated GIF frames and pause/resume; SER playback and pause; shared playback control cleanup. AVI/MP4/MOV/WMV/MKV use Windows-installed codecs.");
   }finally{if(motion!=null)motion.Dispose();if(popup!=null)popup.Close();Directory.Delete(directory,true);}
  }
  void SmokeSessionSummaries(){
   var previous=displayed;string mode=Convert.ToString(C("LibraryViewBox").SelectedItem);var grid=G("FramesGrid");
   try{
    displayed=new System.Collections.Generic.List<Frame>{new Frame{Target="M31",Kind="Light",Session="fixture",Telescope="Dwarf-3",Camera="Tele",Exposure=60,Filter="Ha",OriginalName="one.fit"},new Frame{Target="M31",Kind="Light",Session="fixture",Telescope="Dwarf-3",Camera="Tele",Exposure=60,Filter="Ha",OriginalName="two.fit"},new Frame{Target="M31",Kind="Stack",OriginalName="stack.fit"}};
    // Set without triggering the regular filter, which uses the real archive.
    updating=true;C("LibraryViewBox").SelectedItem="Session summaries";updating=false;DisplayLibrary();PumpPopupLayout();
    var view=(ListCollectionView)grid.ItemsSource;if(view.Groups.Count!=2||subframeSessions.Count!=1||subframeSessions[0].Expanded)throw new Exception("Session summaries did not collapse only related subs.");
    var expander=PopupChildren<Expander>(grid).FirstOrDefault(e=>e.DataContext is CollectionViewGroup&&((CollectionViewGroup)e.DataContext).Name is SubframeSession);if(expander==null||expander.IsExpanded)throw new Exception("Collapsed session control did not render.");
    var session=subframeSessions[0];var selector=PopupChildren<Button>(grid).First(b=>object.Equals(b.Tag,"SelectSession"));selector.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();
    if(!session.IsSelected||grid.SelectedItems.Count!=0||SelectedFiles().Count!=2||Context().Count!=2||!L("SelectionLabel").Text.Contains("1 group"))throw new Exception("Collapsed group was not selected as its own entity");
    var chevron=PopupChildren<System.Windows.Controls.Primitives.ToggleButton>(expander).First(b=>b.Name=="SessionChevron");if(chevron.ActualWidth<34)throw new Exception("Session chevron lacks a roomy click area");
    var label=PopupChildren<TextBlock>(selector).First();if(label.FontSize!=Window.FontSize||label.FontWeight!=FontWeights.Normal)throw new Exception("Group text does not match file row typography");
    expander.IsExpanded=true;PumpPopupLayout();if(!subframeSessions[0].Expanded||PopupChildren<DataGridRow>(grid).Count(r=>r.IsVisible)<3)throw new Exception("Expanding a session did not reveal its files beside the stack.");if(!session.IsSelected)throw new Exception("Expanding lost group selection");grid.SelectedItem=displayed[0];if(session.IsSelected||SelectedFiles().Count!=1)throw new Exception("Individual file selection did not replace group selection");
    SelectSession(session,System.Windows.Input.ModifierKeys.Control);if(SelectedFiles().Count!=2||grid.SelectedItems.Count!=1)throw new Exception("Group/file selection duplicated members");SelectContextRow(displayed[0]);if(!session.IsSelected||SelectedFiles().Count!=2)throw new Exception("Right-click changed an already selected group member");
    DisplayLibrary();PumpPopupLayout();if(!subframeSessions[0].Expanded||!subframeSessions[0].IsSelected||SelectedFiles().Count!=2)throw new Exception("Refresh lost group state");
    displayed.Add(new Frame{Target="M45",Kind="Light",Session="second",Telescope="Dwarf-3",Camera="Tele",OriginalName="three.fit"});displayed.Add(new Frame{Target="M45",Kind="Light",Session="second",Telescope="Dwarf-3",Camera="Tele",OriginalName="four.fit"});DisplayLibrary();SelectSession(subframeSessions[0],System.Windows.Input.ModifierKeys.None);SelectSession(subframeSessions[1],System.Windows.Input.ModifierKeys.Shift);if(SelectedFiles().Count!=4)throw new Exception("Shift selection omitted a session group");SelectSession(subframeSessions[0],System.Windows.Input.ModifierKeys.Control);if(SelectedFiles().Count!=2)throw new Exception("Ctrl selection did not toggle a group");
    displayed=displayed.Take(3).ToList();DisplayLibrary();if(SelectedFiles().Count!=0)throw new Exception("Filtered-out session remained actionable");
    updating=true;C("LibraryViewBox").SelectedItem="Show all files";updating=false;DisplayLibrary();if(((ListCollectionView)grid.ItemsSource).GroupDescriptions.Count!=0||grid.Items.Count!=3)throw new Exception("Show all files did not restore a flat table.");
   }finally{updating=true;C("LibraryViewBox").SelectedItem=mode;updating=false;displayed=previous;DisplayLibrary();}
  }
 }
}
