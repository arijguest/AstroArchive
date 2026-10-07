using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 // Shared portrait display and input handling for the sidebar and popup.
 public class PreviewViewport {
  readonly Grid host,viewport;readonly Image image;readonly StackPanel controls;readonly Viewbox overlay;
  readonly Button zoomOutButton,zoomInButton,fitButton;readonly List<Button> panButtons=new List<Button>();
  readonly PreviewZoom zoom=new PreviewZoom();readonly MatrixTransform transform=new MatrixTransform();
  PreviewGeometry geometry;bool fitting=true,dragging;Point previous;
  public PreviewViewport(Grid host,Grid viewport,Image image){
   this.host=host;this.viewport=viewport;this.image=image;
   viewport.Background=Brushes.Black;viewport.ClipToBounds=true;viewport.IsManipulationEnabled=true;viewport.Focusable=true;
   viewport.HorizontalAlignment=HorizontalAlignment.Center;viewport.VerticalAlignment=VerticalAlignment.Top;
   // Grid gives oversized children a layout clip before their render transform.
   // Canvas measures the full bitmap, so only the final viewport clips zoom/pan.
   viewport.Children.Remove(image);var imageLayer=new Canvas{IsHitTestVisible=false};imageLayer.Children.Add(image);viewport.Children.Insert(0,imageLayer);
   image.Stretch=Stretch.Fill;image.HorizontalAlignment=HorizontalAlignment.Left;image.VerticalAlignment=VerticalAlignment.Top;image.IsHitTestVisible=false;image.RenderTransform=transform;
   RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
   controls=new StackPanel{Orientation=Orientation.Horizontal,IsEnabled=false};var toolbar=new Border{Child=controls,Padding=new Thickness(4),CornerRadius=new CornerRadius(7),Background=new SolidColorBrush(Color.FromArgb(230,20,29,46)),BorderBrush=new SolidColorBrush(Color.FromArgb(150,148,165,192)),BorderThickness=new Thickness(1)};
   overlay=new Viewbox{Child=toolbar,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(6),Visibility=Visibility.Collapsed};
   zoomOutButton=AddButton(controls,"−","Zoom out",()=>ZoomAt(1/1.25,Center));fitButton=AddButton(controls,"Fit","Recenter image",Fit);fitButton.Width=38;fitButton.FontSize=12;zoomInButton=AddButton(controls,"+","Zoom in",()=>ZoomAt(1.25,Center));
   controls.Children.Add(new Border{Width=1,Height=20,Background=new SolidColorBrush(Color.FromArgb(150,148,165,192)),Margin=new Thickness(5,0,5,0)});
   panButtons.Add(AddButton(controls,"←","Pan left",()=>Pan(-40,0)));panButtons.Add(AddButton(controls,"↑","Pan up",()=>Pan(0,-40)));panButtons.Add(AddButton(controls,"↓","Pan down",()=>Pan(0,40)));panButtons.Add(AddButton(controls,"→","Pan right",()=>Pan(40,0)));
   viewport.Children.Add(overlay);host.SizeChanged+=(s,e)=>Resize();
   viewport.PreviewMouseWheel+=(s,e)=>{if(geometry==null)return;ZoomAt(Math.Pow(1.2,e.Delta/120.0),e.GetPosition(viewport));e.Handled=true;};
   viewport.ManipulationStarting+=(s,e)=>{if(geometry==null||FromControl(e.OriginalSource)){e.Cancel();return;}e.ManipulationContainer=viewport;e.Mode=ManipulationModes.Scale|ManipulationModes.Translate;fitting=false;e.Handled=true;};
   viewport.ManipulationDelta+=(s,e)=>{if(geometry==null)return;Manipulate(Math.Sqrt(e.DeltaManipulation.Scale.X*e.DeltaManipulation.Scale.Y),e.ManipulationOrigin,e.DeltaManipulation.Translation.X,e.DeltaManipulation.Translation.Y);e.Handled=true;};
   viewport.ManipulationBoundaryFeedback+=(s,e)=>e.Handled=true;
   viewport.MouseLeftButtonDown+=(s,e)=>{if(geometry==null||e.StylusDevice!=null||FromControl(e.OriginalSource))return;viewport.Focus();previous=e.GetPosition(viewport);dragging=viewport.CaptureMouse();viewport.Cursor=Cursors.SizeAll;e.Handled=true;};
   viewport.MouseMove+=(s,e)=>{if(!dragging)return;if(e.LeftButton!=MouseButtonState.Pressed){viewport.ReleaseMouseCapture();return;}var point=e.GetPosition(viewport);Pan(point.X-previous.X,point.Y-previous.Y);previous=point;e.Handled=true;};
   viewport.MouseLeftButtonUp+=(s,e)=>{if(dragging){viewport.ReleaseMouseCapture();e.Handled=true;}};viewport.LostMouseCapture+=(s,e)=>{dragging=false;viewport.Cursor=Cursors.Arrow;};
   viewport.KeyDown+=(s,e)=>{if(geometry==null)return;if(e.Key==Key.F)Fit();else if(e.Key==Key.Add||e.Key==Key.OemPlus)ZoomAt(1.25,Center);else if(e.Key==Key.Subtract||e.Key==Key.OemMinus)ZoomAt(1/1.25,Center);else if(e.Key==Key.Left)Pan(-40,0);else if(e.Key==Key.Right)Pan(40,0);else if(e.Key==Key.Up)Pan(0,-40);else if(e.Key==Key.Down)Pan(0,40);else return;e.Handled=true;};
   UiHelp.Tip(viewport,"Scroll or pinch to zoom. After zooming, drag or use the arrows to pan. Fit restores the whole image. Landscape previews rotate into portrait.");
  }
  bool FromControl(object source){var element=source as DependencyObject;while(element!=null&&element!=viewport){if(element is ButtonBase)return true;element=VisualTreeHelper.GetParent(element);}return false;}
  Button AddButton(Panel panel,string symbol,string label,Action action){
   var button=new Button{Content=symbol,Width=28,Height=28,Padding=new Thickness(0),Margin=new Thickness(1,0,1,0),FontSize=16,Background=new SolidColorBrush(Color.FromArgb(220,20,29,46)),Foreground=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromArgb(150,148,165,192)),BorderThickness=new Thickness(1)};
   AutomationProperties.SetName(button,label);UiHelp.Tip(button,label=="Recenter image"?"Show the whole image and reset panning (F).":label.StartsWith("Pan")?label+" after zooming.":label);ToolTipService.SetShowOnDisabled(button,true);button.Click+=(s,e)=>{action();e.Handled=true;};panel.Children.Add(button);return button;
  }
  public void SetImage(BitmapSource source,bool reset){
   if(viewport.IsMouseCaptured)viewport.ReleaseMouseCapture();
   image.Source=source;controls.IsEnabled=source!=null;overlay.Visibility=source==null?Visibility.Collapsed:Visibility.Visible;
   viewport.Background=source==null?Brushes.Transparent:Brushes.Black;if(source==null){geometry=null;return;}
   bool changed=geometry==null||image.Width!=source.PixelWidth||image.Height!=source.PixelHeight;
   image.Width=source.PixelWidth;image.Height=source.PixelHeight;geometry=new PreviewGeometry(source.PixelWidth,source.PixelHeight);
   if(reset||changed)fitting=true;Resize();
  }
  public void Resize(){
   if(geometry==null)return;double width,height;geometry.Frame(host.ActualWidth,host.ActualHeight,out width,out height);viewport.Width=width;viewport.Height=height;overlay.MaxWidth=Math.Max(0,width-12);overlay.MaxHeight=Math.Max(0,height-12);
   if(fitting)Fit();else Apply();
  }
  Point Center{get{return new Point(viewport.Width/2,viewport.Height/2);}}
  public void Fit(){if(geometry==null)return;fitting=true;zoom.Fit(viewport.Width,viewport.Height,geometry.Width,geometry.Height);Apply();}
  void ZoomAt(double factor,Point origin){if(geometry==null)return;fitting=false;zoom.Zoom(factor,origin.X,origin.Y);Apply();}
  void Pan(double x,double y){if(geometry==null)return;fitting=false;zoom.Pan(x,y);Apply();}
  void Manipulate(double factor,Point origin,double x,double y){fitting=false;zoom.Zoom(factor,origin.X,origin.Y);zoom.Pan(x,y);Apply();}
  void Apply(){if(geometry==null)return;zoom.Constrain(viewport.Width,viewport.Height,geometry.Width,geometry.Height);double scale=zoom.Scale;transform.Matrix=geometry.Rotated?new Matrix(0,scale,-scale,0,zoom.X+image.Height*scale,zoom.Y):new Matrix(scale,0,0,scale,zoom.X,zoom.Y);
   double fitted=Math.Min(viewport.Width/geometry.Width,viewport.Height/geometry.Height);bool enlarged=scale>fitted+0.000001;zoomOutButton.IsEnabled=enlarged;zoomInButton.IsEnabled=scale<32;foreach(var button in panButtons)button.IsEnabled=enlarged;
  }
  public void SmokeGestures(){
   Fit();double fitted=zoom.Scale;if(geometry==null||viewport.Width<=0||viewport.Height<=0)throw new InvalidOperationException("Preview frame has no image area.");
   if(Math.Abs(viewport.Width/viewport.Height-geometry.Width/geometry.Height)>0.000001||Math.Abs(zoom.X)>0.000001||Math.Abs(zoom.Y)>0.000001)throw new InvalidOperationException("Preview frame does not match the portrait image.");
   if(geometry.Rotated&&(transform.Matrix.M11!=0||transform.Matrix.M12!=fitted||transform.Matrix.M21!=-fitted))throw new InvalidOperationException("Landscape preview did not rotate into portrait.");
   if(zoomOutButton.IsEnabled||panButtons.Exists(b=>b.IsEnabled))throw new InvalidOperationException("Fitted preview offers pan/zoom-out actions that cannot move the image.");
   zoomInButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(zoom.Scale<=fitted||!zoomOutButton.IsEnabled||panButtons.Exists(b=>!b.IsEnabled))throw new InvalidOperationException("Zoom button did not enable image navigation.");zoomOutButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Math.Abs(zoom.Scale-fitted)>0.000001||zoomOutButton.IsEnabled)throw new InvalidOperationException("Zoom-out button did not restore the fitted image.");fitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.PreviewMouseWheelEvent};viewport.RaiseEvent(wheel);if(!wheel.Handled||zoom.Scale<=fitted)throw new InvalidOperationException("Preview wheel zoom did not update the image transform.");
   // Start each arrow at the centre so a pointer-anchored wheel cannot put it at a boundary.
   for(int i=0;i<panButtons.Count;i++){Fit();zoomInButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));double oldX=zoom.X,oldY=zoom.Y;panButtons[i].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(i==0&&zoom.X>=oldX||i==1&&zoom.Y>=oldY||i==2&&zoom.Y<=oldY||i==3&&zoom.X<=oldX)throw new InvalidOperationException("Preview pan button did not move the image: "+i);}
   fitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Math.Abs(zoom.Scale-fitted)>0.000001||Math.Abs(zoom.X)>0.000001||Math.Abs(zoom.Y)>0.000001)throw new InvalidOperationException("Preview recenter button did not restore its frame.");
   Manipulate(1.5,Center,10,-10);if(zoom.Scale<=fitted)throw new InvalidOperationException("Preview pinch did not zoom.");
   Pan(-100000,100000);if(zoom.X>0||zoom.Y>0||zoom.X+geometry.Width*zoom.Scale<viewport.Width-0.000001||zoom.Y+geometry.Height*zoom.Scale<viewport.Height-0.000001)throw new InvalidOperationException("Preview pan exposed an empty border.");
   ZoomAt(0.00001,Center);if(Math.Abs(zoom.Scale-fitted)>0.000001)throw new InvalidOperationException("Preview zoomed out beyond the full image.");Fit();
  }
 }
}
