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
   Run(ct=>{Progress(new ProgressInfo{Stage="Loading image preview",Text=capture.OriginalName});repo.ValidateCapture(capture,ct);string path=repo.FilePath(capture);pixels=DecodePreview(path,ct);pixels.ApplyContext(capture,path);ct.ThrowIfCancellationRequested();rendered=pixels.Render(ScientificPreview(path)?settings.PreviewStretch??"Auto":"Linear",ct);ct.ThrowIfCancellationRequested();return "";},r=>new ImagePreviewWindow(Window,capture.OriginalName,pixels.Width,pixels.Height,rendered).ShowDialog());
  }
 }
 public class ImagePreviewWindow:Window {
  readonly Grid viewport=new Grid{Background=Brushes.Black,ClipToBounds=true,IsManipulationEnabled=true};
  readonly PreviewZoom zoom=new PreviewZoom();readonly MatrixTransform transform=new MatrixTransform();readonly TextBlock percentage=new TextBlock{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,14,0)};
  readonly int imageWidth,imageHeight;bool fitting=true,dragging;Point previous;
  public ImagePreviewWindow(Window owner,string filename,int width,int height,byte[] pixels){
   Owner=owner;Title="Image preview - "+filename;Width=1000;Height=760;MinWidth=500;MinHeight=400;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;FontFamily=owner.FontFamily;FontSize=13;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,Control.BackgroundProperty,"Canvas");Theme.Bind(this,Control.ForegroundProperty,"Text");imageWidth=width;imageHeight=height;
   var layout=new Grid{Margin=new Thickness(14)};layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=layout;
   var toolbar=new WrapPanel{Margin=new Thickness(0,0,0,10)};layout.Children.Add(toolbar);AddButton(toolbar,"Fit",Fit);AddButton(toolbar,"Zoom out",()=>ZoomAt(1/1.25,Center));AddButton(toolbar,"Zoom in",()=>ZoomAt(1.25,Center));AddButton(toolbar,"100%",()=>ZoomAt(1/zoom.Scale,Center));toolbar.Children.Add(percentage);AddButton(toolbar,"Close",Close);
   Grid.SetRow(viewport,1);layout.Children.Add(viewport);var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,pixels,width*3);bitmap.Freeze();var image=new Image{Source=bitmap,Width=width,Height=height,Stretch=Stretch.None,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,RenderTransform=transform,IsHitTestVisible=false};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);viewport.Children.Add(image);
   var hint=new TextBlock{Text="Pinch to zoom · Wheel or trackpad to zoom · Drag to pan · F to fit\nColour preview (sampled "+width+" × "+height+")",Margin=new Thickness(0,10,0,0),TextWrapping=TextWrapping.Wrap};Theme.Bind(hint,TextBlock.ForegroundProperty,"Muted");Grid.SetRow(hint,2);layout.Children.Add(hint);
   viewport.SizeChanged+=(s,e)=>{if(fitting)Fit();};viewport.PreviewMouseWheel+=(s,e)=>{ZoomAt(Math.Pow(1.2,e.Delta/120.0),e.GetPosition(viewport));e.Handled=true;};
   viewport.ManipulationStarting+=(s,e)=>{e.ManipulationContainer=viewport;e.Mode=ManipulationModes.Scale|ManipulationModes.Translate;e.Handled=true;};
   viewport.ManipulationDelta+=(s,e)=>{fitting=false;zoom.Zoom(Math.Sqrt(e.DeltaManipulation.Scale.X*e.DeltaManipulation.Scale.Y),e.ManipulationOrigin.X,e.ManipulationOrigin.Y);zoom.Pan(e.DeltaManipulation.Translation.X,e.DeltaManipulation.Translation.Y);Apply();e.Handled=true;};
   viewport.ManipulationBoundaryFeedback+=(s,e)=>e.Handled=true;
   viewport.PreviewMouseLeftButtonDown+=(s,e)=>{if(e.StylusDevice!=null)return;previous=e.GetPosition(viewport);dragging=viewport.CaptureMouse();viewport.Cursor=Cursors.SizeAll;e.Handled=true;};
   viewport.PreviewMouseMove+=(s,e)=>{if(!dragging)return;if(e.LeftButton!=MouseButtonState.Pressed){viewport.ReleaseMouseCapture();return;}var point=e.GetPosition(viewport);fitting=false;zoom.Pan(point.X-previous.X,point.Y-previous.Y);previous=point;Apply();};
   viewport.PreviewMouseLeftButtonUp+=(s,e)=>{if(dragging){viewport.ReleaseMouseCapture();e.Handled=true;}};viewport.LostMouseCapture+=(s,e)=>{dragging=false;viewport.Cursor=Cursors.Arrow;};
   PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape)Close();else if(e.Key==Key.F)Fit();else if(e.Key==Key.Add||e.Key==Key.OemPlus)ZoomAt(1.25,Center);else if(e.Key==Key.Subtract||e.Key==Key.OemMinus)ZoomAt(1/1.25,Center);else return;e.Handled=true;};
  }
  public void SmokeGestures(){Fit();double fitted=zoom.Scale;var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.PreviewMouseWheelEvent};viewport.RaiseEvent(wheel);if(!wheel.Handled||zoom.Scale<=fitted||transform.Matrix.M11!=zoom.Scale)throw new InvalidOperationException("Preview wheel zoom did not update the image transform.");zoom.Pan(30,-20);Fit();if(Math.Abs(zoom.Scale-fitted)>0.000001||transform.Matrix.OffsetX!=zoom.X||transform.Matrix.OffsetY!=zoom.Y)throw new InvalidOperationException("Preview Fit did not restore its viewport.");}
  Point Center{get{return new Point(viewport.ActualWidth/2,viewport.ActualHeight/2);}}
  void AddButton(Panel toolbar,string text,Action action){var button=new Button{Content=text};UiHelp.For(button,text);button.Click+=(s,e)=>action();toolbar.Children.Add(button);}
  void Fit(){fitting=true;zoom.Fit(viewport.ActualWidth,viewport.ActualHeight,imageWidth,imageHeight);Apply();}
  void ZoomAt(double factor,Point origin){fitting=false;zoom.Zoom(factor,origin.X,origin.Y);Apply();}
  void Apply(){transform.Matrix=new Matrix(zoom.Scale,0,0,zoom.Scale,zoom.X,zoom.Y);percentage.Text=(zoom.Scale*100).ToString("0")+"%";}
 }
}
