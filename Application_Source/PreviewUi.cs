using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace AstroArchive {
 public partial class MainUi {
  CancellationTokenSource previewCancel;int previewGeneration;PreviewData previewData;Frame previewFrame;string previewPath;bool previewReady,choosingStretch;
  public static string CompactPath(string path){if(string.IsNullOrEmpty(path))return "Set an archive folder in Settings";if(path.Length<=75)return path;return Path.GetPathRoot(path)+"…"+Path.DirectorySeparatorChar+new DirectoryInfo(path).Name;}
  void InitializeWorkspace(){
   Theme.Apply(Window,settings.ThemeMode);C("StretchMode").ItemsSource=PreviewData.StretchModes;C("StretchMode").SelectedItem=settings.PreviewStretch??"Auto";if(C("StretchMode").SelectedIndex<0)C("StretchMode").SelectedItem="Auto";
   settings.PreviewStretch=Convert.ToString(C("StretchMode").SelectedItem);
   C("PreviewZoom").ItemsSource=new[]{"Fit","100%","200%","400%"};C("PreviewZoom").SelectedIndex=0;
   B("CoffeeButton").Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo("https://ko-fi.com/arijguest"){UseShellExecute=true});}catch(Exception error){MessageBox.Show(Window,"Could not open your browser. Visit https://ko-fi.com/arijguest\n\n"+error.Message,"Ko-fi link",MessageBoxButton.OK,MessageBoxImage.Information);}};
   B("ThemeButton").Click+=(s,e)=>{settings.ThemeMode=Theme.IsDark(settings.ThemeMode)?"Light":"Dark";Theme.Apply(Window,settings.ThemeMode);SaveSettings();};
   B("PreviewToggle").Click+=(s,e)=>{settings.ShowPreview=!settings.ShowPreview;SetPreviewVisibility();SaveSettings();if(settings.ShowPreview)PreviewSelected();};
   B("OpenPreviewButton").Click+=(s,e)=>OpenPreviewFile();
   C("StretchMode").SelectionChanged+=(s,e)=>{if(!previewReady||choosingStretch)return;if(previewPath==null||ScientificPreview(previewPath)){settings.PreviewStretch=Convert.ToString(C("StretchMode").SelectedItem);SaveSettings();}if(previewPath!=null)LoadPreview(previewPath,false);};
   C("PreviewZoom").SelectionChanged+=(s,e)=>PreviewSize();((FrameworkElement)Window.FindName("PreviewStage")).SizeChanged+=(s,e)=>PreviewSize();
   SystemEvents.UserPreferenceChanged+=AppearanceChanged;previewReady=true;SetPreviewVisibility();
  }
  void SetPreviewVisibility(){((FrameworkElement)Window.FindName("PreviewPane")).Visibility=settings.ShowPreview?Visibility.Visible:Visibility.Collapsed;((FrameworkElement)Window.FindName("PreviewDivider")).Visibility=settings.ShowPreview?Visibility.Visible:Visibility.Collapsed;((ColumnDefinition)Window.FindName("PreviewColumn")).Width=new GridLength(settings.ShowPreview?330:0);((ColumnDefinition)Window.FindName("PreviewDividerColumn")).Width=new GridLength(settings.ShowPreview?10:0);B("PreviewToggle").Content=settings.ShowPreview?"Hide preview":"Preview";if(!settings.ShowPreview)CancelPreview();}
  void CancelPreview(){previewGeneration++;if(previewCancel!=null){previewCancel.Cancel();previewCancel.Dispose();previewCancel=null;}previewData=null;previewFrame=null;previewPath=null;C("StretchMode").IsEnabled=true;var image=Window.FindName("PreviewImage") as Image;if(image!=null)image.Source=null;}
  void AppearanceChanged(object sender,UserPreferenceChangedEventArgs args){if(Window.Dispatcher.HasShutdownStarted)return;Window.Dispatcher.BeginInvoke(new Action(()=>{if(string.IsNullOrEmpty(settings.ThemeMode)||settings.ThemeMode=="System")Theme.Apply(Window,"System");}));}
  void DisposePreview(){SystemEvents.UserPreferenceChanged-=AppearanceChanged;CancelPreview();}
  void UpdateSelection(){if(!previewReady)return;int count=G("FramesGrid").SelectedItems.Count;L("SelectionLabel").Text=count==0?"Ctrl / Shift to select · Right-click for file actions":count+" selected · Right-click for file actions";if(settings.ShowPreview)PreviewSelected();}
  void PreviewSelected(){if(!previewReady)return;Frame f=G("FramesGrid").SelectedItem as Frame;if(f==null){CancelPreview();L("PreviewName").Text="Select a capture";L("PreviewInfo").Text="";PreviewMessage("Select a file or open an image.\n\nFITS · XISF · TIFF · PNG · JPEG");return;}if(repo==null)return;
   if(!settings.ShowPreview){settings.ShowPreview=true;SetPreviewVisibility();SaveSettings();}try{string path=repo.FilePath(f);if(path!=previewPath||previewFrame==null||previewFrame.Target!=f.Target||previewFrame.ObservationMode!=f.ObservationMode||previewFrame.Filter!=f.Filter)LoadPreview(path,true,f);}catch(Exception error){PreviewMessage(error.Message);}
  }
  void OpenPreviewFile(){var picker=new OpenFileDialog{Title="Preview an astrophotography image",Filter="Astrophotography images|*.fit;*.fits;*.fts;*.fit.gz;*.fits.gz;*.fts.gz;*.xisf;*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.wdp;*.hdp;*.jxr;*.dng;*.cr2;*.cr3;*.nef;*.arw;*.raf;*.orf;*.rw2|All files|*.*"};if(picker.ShowDialog(Window)!=true)return;settings.ShowPreview=true;SetPreviewVisibility();SaveSettings();LoadPreview(picker.FileName,true);}
  void PreviewMessage(string message){L("PreviewMessage").Text=message;L("PreviewMessage").Visibility=Visibility.Visible;}
  async void LoadPreview(string path,bool reload,Frame frame=null){
   if(previewCancel!=null){previewCancel.Cancel();previewCancel.Dispose();}previewCancel=new CancellationTokenSource();CancellationToken token=previewCancel.Token;int generation=++previewGeneration;
   if(reload){previewFrame=frame==null?null:frame.Clone();C("StretchMode").IsEnabled=true;}Frame context=previewFrame;
   PreviewData previous=reload?null:previewData;previewPath=path;if(reload){previewData=null;((Image)Window.FindName("PreviewImage")).Source=null;L("PreviewInfo").Text="";}
   if(reload){choosingStretch=true;C("StretchMode").SelectedItem=ScientificPreview(path)&&!(context!=null&&ObservationTargets.Unstretched(context.Target,context.ObservationMode))?settings.PreviewStretch??"Auto":"Linear";choosingStretch=false;}
   L("PreviewName").Text=Path.GetFileName(path);L("PreviewName").ToolTip=path;PreviewMessage(previous==null?"Loading image…":"Stretching…");string mode=Convert.ToString(C("StretchMode").SelectedItem);
   try{
    var result=await Task.Run(()=>{PreviewData data=previous??DecodePreview(path,token);data.ApplyContext(context,path);byte[] rgb=data.Render(mode,token);var bitmap=BitmapSource.Create(data.Width,data.Height,96,96,PixelFormats.Rgb24,null,rgb,data.Width*3);bitmap.Freeze();return Tuple.Create(data,bitmap);},token);
    Window.Dispatcher.Invoke(new Action(()=>{if(generation!=previewGeneration||token.IsCancellationRequested)return;previewData=result.Item1;C("StretchMode").IsEnabled=!previewData.SkipStretch;if(previewData.SkipStretch){choosingStretch=true;C("StretchMode").SelectedItem="Linear";choosingStretch=false;}((Image)Window.FindName("PreviewImage")).Source=result.Item2;L("PreviewMessage").Visibility=Visibility.Collapsed;L("PreviewInfo").Text=previewData.SourceWidth+" × "+previewData.SourceHeight+" · "+previewData.Description+" · "+previewData.DisplayMode+(previewData.SourceWidth>previewData.Width?" · sampled preview":"")+"\nDisplay only · source pixels unchanged";PreviewSize();}));
   }catch(OperationCanceledException){}catch(Exception error){if(!Window.Dispatcher.HasShutdownStarted)Window.Dispatcher.Invoke(new Action(()=>{if(generation!=previewGeneration)return;previewData=null;((Image)Window.FindName("PreviewImage")).Source=null;PreviewMessage("Preview unavailable\n\n"+error.Message);L("PreviewInfo").Text=ScientificPreview(path)?"FITS / XISF preview":"RAW formats require a compatible Windows image codec.";}));}
  }
  void PreviewSize(){var image=(Image)Window.FindName("PreviewImage");if(image.Source==null||previewData==null)return;var scroll=(ScrollViewer)Window.FindName("PreviewScroll");string zoom=Convert.ToString(C("PreviewZoom").SelectedItem);double scale=zoom=="100%"?1:zoom=="200%"?2:zoom=="400%"?4:Math.Min(Math.Max(1,scroll.ActualWidth-4)/previewData.Width,Math.Max(1,scroll.ActualHeight-4)/previewData.Height);
   // Percentages refer to original image pixels, although display sampling is bounded.
   if(zoom!="Fit")scale*=Math.Max((double)previewData.SourceWidth/previewData.Width,(double)previewData.SourceHeight/previewData.Height);image.Width=Math.Max(1,previewData.Width*scale);image.Height=Math.Max(1,previewData.Height*scale);
  }
  void SmokePreview(string output){
   // Exercise the Windows codecs with generated high-depth and common raster data.
   int width=80,height=60;ushort[] rgb=new ushort[width*height*3];for(int y=0;y<height;y++)for(int x=0;x<width;x++){int i=(y*width+x)*3;rgb[i]=(ushort)(x*700);rgb[i+1]=(ushort)(y*900);rgb[i+2]=(ushort)(x*400+y*300);}
   var source=BitmapSource.Create(width,height,96,96,PixelFormats.Rgb48,null,rgb,width*6);source.Freeze();
   foreach(string extension in new[]{"png","tiff","jpg"}){BitmapEncoder encoder=extension=="png"?(BitmapEncoder)new PngBitmapEncoder():extension=="tiff"?(BitmapEncoder)new TiffBitmapEncoder():new JpegBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));string path=Path.Combine(output,"codec-test."+extension);using(var stream=File.Create(path))encoder.Save(stream);var decoded=DecodePreview(path,CancellationToken.None);if(decoded.SourceWidth!=width||decoded.SourceHeight!=height||decoded.Channels!=3)throw new Exception("Raster preview geometry: "+extension);if(extension!="jpg"&&Math.Abs(decoded.Pixels[(20*width+30)*3]-rgb[(20*width+30)*3]/65535.0)>0.02)throw new Exception("High depth preview precision: "+extension);decoded.Render("Auto",CancellationToken.None);}
   settings.ShowPreview=true;SetPreviewVisibility();var data=new PreviewData{Width=420,Height=320,SourceWidth=420,SourceHeight=320,Channels=3,Pixels=new double[420*320*3],Description="Generated colour preview"};
   for(int y=0;y<data.Height;y++)for(int x=0;x<data.Width;x++){double a=Math.Exp(-((x-190)*(x-190)/9500.0+(y-150)*(y-150)/4500.0)),b=Math.Exp(-((x-220)*(x-220)+(y-160)*(y-160))/40.0);int i=(y*data.Width+x)*3;data.Pixels[i]=0.008+0.002*a+0.6*b;data.Pixels[i+1]=0.008+0.004*a+0.7*b;data.Pixels[i+2]=0.008+0.009*a+0.8*b;}
   var gesture=new ImagePreviewWindow(Window,"Generated colour preview",data.Width,data.Height,data.Render("Auto",CancellationToken.None));try{gesture.Show();PumpPopupLayout();gesture.SmokeGestures();SavePopup((FrameworkElement)gesture.Content,Path.Combine(output,"AstroArchive_Gesture_Preview.png"));}finally{gesture.Close();}
   previewData=data;byte[] pixels=data.Render("Auto",CancellationToken.None);((Image)Window.FindName("PreviewImage")).Source=BitmapSource.Create(data.Width,data.Height,96,96,PixelFormats.Rgb24,null,pixels,data.Width*3);L("PreviewMessage").Visibility=Visibility.Collapsed;L("PreviewName").Text="Generated test image";L("PreviewInfo").Text="420 × 320 · RGB · Auto stretch";C("StretchMode").SelectedItem="Auto";C("PreviewZoom").SelectedItem="Fit";
   Theme.Apply(Window,"Dark");Window.UpdateLayout();PreviewSize();Capture(Path.Combine(output,"AstroArchive_Dark_UI.png"));
   Theme.Apply(Window,"Light");Window.UpdateLayout();Capture(Path.Combine(output,"AstroArchive_Light_UI.png"));
   settings.ShowPreview=false;SetPreviewVisibility();Window.UpdateLayout();if(((ColumnDefinition)Window.FindName("PreviewColumn")).Width.Value!=0)throw new Exception("Preview did not collapse");Capture(Path.Combine(output,"AstroArchive_Compact_UI.png"));settings.ShowPreview=true;SetPreviewVisibility();Theme.Apply(Window,"Dark");Window.UpdateLayout();
  }
  static bool ScientificPreview(string path){return Util.IsFits(path)||path.EndsWith(".xisf",StringComparison.OrdinalIgnoreCase);}
  static PreviewData DecodePreview(string path,CancellationToken ct){
   if(Util.IsFits(path))return Fits.Preview(path,ct);if(path.EndsWith(".xisf",StringComparison.OrdinalIgnoreCase))return Xisf.Read(path,ct);
   if(path.EndsWith(".fz",StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("Decompress tile-compressed FITS (.fz) before previewing.");
   ct.ThrowIfCancellationRequested();using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
    var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand);var frame=decoder.Frames[0];if(frame.PixelWidth<1||frame.PixelHeight<1||(long)frame.PixelWidth*frame.PixelHeight>200000000)throw new NotSupportedException("Image exceeds the preview size limit.");
    ct.ThrowIfCancellationRequested();double scale=Math.Min(1,1400.0/Math.Max(frame.PixelWidth,frame.PixelHeight));BitmapSource source=scale<1?(BitmapSource)new TransformedBitmap(frame,new ScaleTransform(scale,scale)):frame;
    bool floating=frame.Format==PixelFormats.Gray32Float||frame.Format==PixelFormats.Rgb128Float||frame.Format==PixelFormats.Rgba128Float||frame.Format==PixelFormats.Prgba128Float;
    var converted=new FormatConvertedBitmap(source,floating?PixelFormats.Rgb128Float:PixelFormats.Rgb48,null,0);int w=converted.PixelWidth,h=converted.PixelHeight;
    var data=new PreviewData{Width=w,Height=h,SourceWidth=frame.PixelWidth,SourceHeight=frame.PixelHeight,Channels=3,Pixels=new double[w*h*3],Description=Path.GetExtension(path).TrimStart('.').ToUpperInvariant()+" · "+frame.Format.BitsPerPixel+" bit"};
    if(floating){float[] samples=new float[w*h*4];converted.CopyPixels(samples,w*16,0);for(int n=0;n<w*h;n++){if(n%65536==0)ct.ThrowIfCancellationRequested();for(int c=0;c<3;c++)data.Pixels[n*3+c]=samples[n*4+c];}}
    else{ushort[] samples=new ushort[w*h*3];converted.CopyPixels(samples,w*6,0);for(int n=0;n<samples.Length;n++){if(n%65536==0)ct.ThrowIfCancellationRequested();data.Pixels[n]=samples[n]/65535.0;}}return data;
   }
  }
 }
}
