using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  void PreviewImage(Frame frame){
   if(repo==null||cancel!=null||frame==null)return;var capture=frame.Clone();string mediaPath=repo.FilePath(capture);if(MediaFiles.Motion(mediaPath)){Run(ct=>{repo.ValidateCapture(capture,ct);return mediaPath;},path=>new ImagePreviewWindow(Window,capture.OriginalName,path).ShowDialog());return;}PreviewData pixels=null;byte[] rendered=null;
   Run(ct=>{Progress(new ProgressInfo{Stage="Loading image preview",Text=capture.OriginalName});repo.ValidateCapture(capture,ct);string path=repo.FilePath(capture);pixels=DecodePreview(path,ct,capture);pixels.ApplyContext(capture,path);ct.ThrowIfCancellationRequested();rendered=pixels.Render(ScientificPreview(path)?settings.PreviewStretch??"Auto per channel":"Linear",ct);ct.ThrowIfCancellationRequested();return "";},r=>new ImagePreviewWindow(Window,capture.OriginalName,pixels.Width,pixels.Height,rendered).ShowDialog());
  }
 }
 public class ImagePreviewWindow:Window {
  readonly PreviewViewport preview;MotionPreview motion;
  public ImagePreviewWindow(Window owner,string filename,string path):this(owner,filename,480,640,new byte[480*640*3]){
   var host=(Grid)Content;var message=new TextBlock{Text="Loading preview…",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12)};host.Children.Add(message);
   Loaded+=(s,e)=>{motion=new MotionPreview(preview,path,info=>message.Visibility=Visibility.Collapsed,error=>{message.Text=error;message.Visibility=Visibility.Visible;});motion.Start();};Closed+=(s,e)=>{if(motion!=null)motion.Dispose();};
  }
  public ImagePreviewWindow(Window owner,string filename,int width,int height,byte[] pixels){
   Owner=owner;Title="Preview - "+filename;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;FontFamily=owner.FontFamily;FontSize=13;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,Control.BackgroundProperty,"Canvas");Theme.Bind(this,Control.ForegroundProperty,"Text");
   // Reserve the native window chrome, then size the client area to the source image.
   var geometry=new PreviewGeometry(width,height,0);var work=SystemParameters.WorkArea;double clientWidth,clientHeight;geometry.Frame(Math.Min(1100,work.Width-64),Math.Max(1,Math.Min(760,work.Height-100)-PreviewViewport.ToolbarSpace),out clientWidth,out clientHeight);
   Width=clientWidth+2*SystemParameters.ResizeFrameVerticalBorderWidth+16;Height=clientHeight+PreviewViewport.ToolbarSpace+SystemParameters.CaptionHeight+2*SystemParameters.ResizeFrameHorizontalBorderHeight+16;MinWidth=Math.Min(Width,260);MinHeight=Math.Min(Height,360);
   var host=new Grid{Margin=new Thickness(8)};var viewport=new Grid();var image=new Image();viewport.Children.Add(image);host.Children.Add(viewport);Content=host;preview=new PreviewViewport(host,viewport,image,false,true);
   var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,pixels,width*3);bitmap.Freeze();preview.SetImage(bitmap,true);
   Loaded+=(s,e)=>preview.Resize();PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.F){preview.Fit();e.Handled=true;}};
  }
  public void SmokeGestures(){preview.SmokeGestures();preview.SmokeRotation();}
 }
}
