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
  void CheckAllTargetsRow(ListBox list){
   var entry=list.Items.Cast<TargetSummary>().Single(t=>t.Name=="All targets");list.SelectedItem=list.Items.Cast<TargetSummary>().First(t=>t.Name!="All targets");list.ScrollIntoView(entry);PumpPopupLayout();
   var row=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(entry);var title=PopupChildren<TextBlock>(row).FirstOrDefault(t=>t.Text=="All Targets");
   var surface=(Border)row.Template.FindName("Item",row);if(title==null||title.FontWeight!=FontWeights.Bold||!object.Equals(surface.Background,Window.FindResource("SurfaceAlt")))throw new Exception("All Targets lost its bold title or theme background.");
   Readable(title.Foreground,surface.Background,"All Targets");list.SelectedItem=entry;PumpPopupLayout();if(!row.IsSelected||!object.Equals(surface.Background,Window.FindResource("Selection")))throw new Exception("All Targets background concealed selection.");
  }
  public void SmokePreviewResolution(string output){
   Directory.CreateDirectory(output);Window.Show();GoToPage(0);settings.ShowPreview=true;SetPreviewVisibility();PumpPopupLayout();
   if(((ColumnDefinition)Window.FindName("PreviewColumn")).Width.Value!=280||((Border)Window.FindName("PreviewPane")).Padding.Left!=8)throw new Exception("Repository preview did not open at its narrower default width.");
   string fixture=Path.Combine(Path.GetTempPath(),"AstroArchive-preview-resolution-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);string path=Path.Combine(fixture,"native.png");
   int width=1604,height=1200;var bytes=new byte[width*height*3];for(int y=0;y<height;y++)for(int x=0;x<width;x++){int p=(y*width+x)*3;bytes[p]=(byte)(x%256);bytes[p+1]=(byte)(y%256);bytes[p+2]=(byte)((x+y)%256);}
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,bytes,width*3)));using(var stream=File.Create(path))encoder.Save(stream);string originalHash=Util.Hash(path,CancellationToken.None);
   var capture=new Frame{Target="M31",Width=width,Height=height,OriginalName="Native resolution fixture",Kind="Light",Exposure=60,Session="fixture",ObservedUtc="2026-10-09T21:00:00Z",Latitude=51.5,Longitude=0};
   LoadPreview(path,true,capture);WaitPreview(()=>previewData!=null&&L("PreviewMessage").Visibility==Visibility.Collapsed,"Inline preview did not load.");
   var inline=previewData;var bitmap=((Image)Window.FindName("PreviewImage")).Source as BitmapSource;if(bitmap==null||bitmap.PixelWidth>1400||inline.SourceWidth!=width||inline.Width>=width)throw new Exception("Inline preview did not stay sampled.");
   var full=DecodeFullPreview(path,CancellationToken.None);if(full.Width!=width||full.Height!=height)throw new Exception("Large raster preview is reduced.");
   var popup=new ImagePreviewWindow(Window,"Native resolution fixture",full.Width,full.Height,full.Render("Linear",CancellationToken.None));
   try{popup.Show();PumpPopupLayout();var native=PopupChildren<Image>(popup).Select(i=>i.Source as BitmapSource).First(b=>b!=null);if(native.PixelWidth!=width||native.PixelHeight!=height)throw new Exception("The popup discarded native pixels.");var last=new byte[3];native.CopyPixels(new Int32Rect(width-1,height-1,1,1),last,3,0);if(!last.SequenceEqual(bytes.Skip(bytes.Length-3)))throw new Exception("Native raster pixels changed.");popup.SmokeGestures();CapturePopup(popup,Path.Combine(output,"AstroArchive_Native_Popup.png"));}finally{popup.Close();}
   string serPath=Path.Combine(fixture,"native.ser");var recording=new byte[178+width*16*2];System.Text.Encoding.ASCII.GetBytes("LUCAM-RECORDER").CopyTo(recording,0);BitConverter.GetBytes(width).CopyTo(recording,26);BitConverter.GetBytes(16).CopyTo(recording,30);BitConverter.GetBytes(8).CopyTo(recording,34);BitConverter.GetBytes(2).CopyTo(recording,38);for(int p=178;p<recording.Length;p++)recording[p]=120;File.WriteAllBytes(serPath,recording);
   var serInfo=Assets.Inspect(serPath);if(Assets.Display(new Frame{Format="SER",Images=serInfo.Images},serPath,0,CancellationToken.None).Width>1400)throw new Exception("Inline SER frames no longer stay sampled.");
   var recordingPopup=new ImagePreviewWindow(Window,"Native SER fixture",serPath);try{recordingPopup.Show();WaitPreview(()=>PopupChildren<Image>(recordingPopup).Any(i=>i.Source is BitmapSource&&((BitmapSource)i.Source).PixelWidth==width),"SER popup remained a reduced frame.");}finally{recordingPopup.Close();}
   LoadPreview(path,true,capture);WaitPreview(()=>previewData!=null&&L("PreviewMessage").Visibility==Visibility.Collapsed,"Inline preview failed after popup.");if(previewData.Width!=inline.Width||!object.ReferenceEquals(previewData.Pixels,inline.Pixels)||Util.Hash(path,CancellationToken.None)!=originalHash)throw new Exception("Full-resolution popup changed the cache or source image.");
   all=new List<Frame>{capture,capture.Clone(),new Frame{Target="M45",Kind="Stack",OriginalName="Pleiades"}};C("LibraryViewBox").SelectedItem="Session summaries";Filter(true);PumpPopupLayout();
   if(!subframeSessions.Single().Label.Contains("2 subs · 60s · 2 min 0 s total"))throw new Exception("Condensed view omitted sub exposure or total.");
   var project=new EditedProject{Id=Guid.NewGuid().ToString("N"),Name="Target appearance"};editedImages=new List<EditedImage>{new EditedImage{Project=project,RelativePath="one.png",Filename="one.png",Metadata=new EditedMetadata{Object="M31",ImageClass="Edited image"}}};FilterEditedImages();
   foreach(string theme in new[]{"Light","Dark"}){Theme.Apply(Window,theme);GoToPage(2);CheckAllTargetsRow(EditedTargets);GoToPage(0);CheckAllTargetsRow(Targets);LoadPreview(path,true,capture);WaitPreview(()=>previewData!=null&&L("PreviewMessage").Visibility==Visibility.Collapsed,"Themed inline preview did not load.");PumpPopupLayout();CheckSkyFit("",previewViewport,1200,1604);Capture(Path.Combine(output,"AstroArchive_Wider_Repository_"+theme+".png"));}
   File.WriteAllText(Path.Combine(output,"preview-resolution-smoke.txt"),"PASS: compact 280px Repository preview with adaptive sky; sampled inline bitmap/cache and SER frames; native-size raster/SER popups and exact raster pixels; unchanged source bytes; per-sub exposure in condensed rows; bold All Targets theme backgrounds and selection feedback in Repository/Edited, light/dark.");
  }
 }
}
