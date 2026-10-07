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
   if(repo==null||cancel!=null||frame==null)return;var capture=frame.Clone();PreviewData pixels=null;byte[] rendered=null;
   Run(ct=>{Progress(new ProgressInfo{Stage="Loading image preview",Text=capture.OriginalName});repo.ValidateCapture(capture,ct);string path=repo.FilePath(capture);pixels=DecodePreview(path,ct,capture);pixels.ApplyContext(capture,path);ct.ThrowIfCancellationRequested();rendered=pixels.Render(ScientificPreview(path)?settings.PreviewStretch??"Auto":"Linear",ct);ct.ThrowIfCancellationRequested();return "";},r=>new ImagePreviewWindow(Window,capture.OriginalName,pixels.Width,pixels.Height,rendered).ShowDialog());
  }
 }
 public class ImagePreviewWindow:Window {
  readonly PreviewViewport preview;
  public ImagePreviewWindow(Window owner,string filename,int width,int height,byte[] pixels){
   Owner=owner;Title="Image preview - "+filename;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;FontFamily=owner.FontFamily;FontSize=13;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,Control.BackgroundProperty,"Canvas");Theme.Bind(this,Control.ForegroundProperty,"Text");
   // Reserve the native window chrome, then size the client area to the portrait image.
   var geometry=new PreviewGeometry(width,height);var work=SystemParameters.WorkArea;double clientWidth,clientHeight;geometry.Frame(Math.Min(560,work.Width-64),Math.Max(1,Math.Min(760,work.Height-100)-PreviewViewport.ToolbarSpace),out clientWidth,out clientHeight);
   Width=clientWidth+2*SystemParameters.ResizeFrameVerticalBorderWidth+16;Height=clientHeight+PreviewViewport.ToolbarSpace+SystemParameters.CaptionHeight+2*SystemParameters.ResizeFrameHorizontalBorderHeight+16;MinWidth=Math.Min(Width,260);MinHeight=Math.Min(Height,360);
   var host=new Grid{Margin=new Thickness(8)};var viewport=new Grid();var image=new Image();viewport.Children.Add(image);host.Children.Add(viewport);Content=host;preview=new PreviewViewport(host,viewport,image);
   var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,pixels,width*3);bitmap.Freeze();preview.SetImage(bitmap,true);
   Loaded+=(s,e)=>preview.Resize();PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.F){preview.Fit();e.Handled=true;}};
  }
  public void SmokeGestures(){preview.SmokeGestures();}
 }
}
