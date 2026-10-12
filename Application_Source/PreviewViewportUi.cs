using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AstroArchive {
 // Shared fitting, display rotation and input handling for sidebars and large previews.
 public class PreviewViewport {
  public const double ToolbarSpace=46;
  readonly Grid host,viewport;readonly Image image;readonly Canvas imageLayer,overlayLayer;readonly StackPanel controls;readonly Viewbox toolbarHost;readonly FrameworkElement remainder;readonly TranslateTransform remainderOffset=new TranslateTransform();MediaElement media;
  readonly Button playbackButton;Action togglePlayback;
  readonly Button zoomOutButton,zoomInButton,fitButton;readonly List<Button> panButtons=new List<Button>();
  readonly PreviewZoom zoom=new PreviewZoom();readonly MatrixTransform transform=new MatrixTransform();
  bool remainderEnabled=true;
  List<TargetMark> targetMarks;string targetNote;
  public void SetRemainderEnabled(bool enabled){remainderEnabled=enabled;Resize();}
  // Width/height of the frame as shown (after any quarter turn); null before any image.
  public double? FrameAspect{get{return geometry==null||geometry.Height<=0?(double?)null:geometry.Width/geometry.Height;}}
  // Raised when a new image, video or rotation changes the frame's shape, so hosts can size around it.
  public event Action FrameChanged;double? notifiedAspect;
  void NotifyFrame(){var aspect=FrameAspect;if(aspect.HasValue==notifiedAspect.HasValue&&(!aspect.HasValue||Math.Abs(aspect.Value-notifiedAspect.Value)<0.005))return;notifiedAspect=aspect;if(FrameChanged!=null)FrameChanged();}
  PreviewGeometry geometry;bool fitting=true,dragging,loading;Point previous;readonly bool portrait;int quarterTurns;readonly Button rotateLeftButton,rotateRightButton;
  public PreviewViewport(Grid host,Grid viewport,Image image,FrameworkElement remainder=null,bool portrait=true,bool allowRotation=false){
   this.host=host;this.viewport=viewport;this.image=image;this.remainder=remainder;this.portrait=portrait;
   if(remainder!=null){remainder.VerticalAlignment=VerticalAlignment.Top;remainder.RenderTransform=remainderOffset;}
   viewport.Background=Brushes.Black;viewport.ClipToBounds=true;viewport.IsManipulationEnabled=true;viewport.Focusable=true;
   viewport.HorizontalAlignment=HorizontalAlignment.Center;viewport.VerticalAlignment=VerticalAlignment.Top;
   // Grid gives oversized children a layout clip before their render transform.
   // Canvas measures the full bitmap, so only the final viewport clips zoom/pan.
   viewport.Children.Remove(image);imageLayer=new Canvas{IsHitTestVisible=false};imageLayer.Children.Add(image);viewport.Children.Insert(0,imageLayer);
   // Marks are drawn in viewport space so circles stay round and labels upright through zoom and rotation.
   overlayLayer=new Canvas{IsHitTestVisible=false,Visibility=Visibility.Collapsed};viewport.Children.Insert(1,overlayLayer);
   image.Stretch=Stretch.Fill;image.HorizontalAlignment=HorizontalAlignment.Left;image.VerticalAlignment=VerticalAlignment.Top;image.IsHitTestVisible=false;image.RenderTransform=transform;
   RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
   controls=new StackPanel{Orientation=Orientation.Horizontal,IsEnabled=false};var toolbar=new Border{Child=controls,Padding=new Thickness(4),CornerRadius=new CornerRadius(7),Background=new SolidColorBrush(Color.FromArgb(230,20,29,46)),BorderBrush=new SolidColorBrush(Color.FromArgb(150,148,165,192)),BorderThickness=new Thickness(1)};
   toolbarHost=new Viewbox{Child=toolbar,Height=ToolbarSpace-6,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(6,6,6,0),Visibility=Visibility.Collapsed};
   zoomOutButton=AddButton(controls,"−","Zoom out",()=>ZoomAt(1/1.25,Center));fitButton=AddButton(controls,"Fit","Recenter image",Fit);fitButton.Width=38;fitButton.FontSize=12;zoomInButton=AddButton(controls,"+","Zoom in",()=>ZoomAt(1.25,Center));
   controls.Children.Add(new Border{Width=1,Height=20,Background=new SolidColorBrush(Color.FromArgb(150,148,165,192)),Margin=new Thickness(5,0,5,0)});
   panButtons.Add(AddButton(controls,"←","View left",()=>Navigate(-40,0)));panButtons.Add(AddButton(controls,"↑","View up",()=>Navigate(0,-40)));panButtons.Add(AddButton(controls,"↓","View down",()=>Navigate(0,40)));panButtons.Add(AddButton(controls,"→","View right",()=>Navigate(40,0)));
   if(allowRotation){controls.Children.Add(new Border{Width=1,Height=20,Background=new SolidColorBrush(Color.FromArgb(150,148,165,192)),Margin=new Thickness(5,0,5,0)});rotateLeftButton=AddButton(controls,"↶","Rotate left 90°",()=>Rotate(-1));rotateRightButton=AddButton(controls,"↷","Rotate right 90°",()=>Rotate(1));}
   playbackButton=AddButton(controls,"Ⅱ","Pause playback",()=>{if(togglePlayback!=null)togglePlayback();});playbackButton.Visibility=Visibility.Collapsed;
   host.Children.Add(toolbarHost);host.SizeChanged+=(s,e)=>Resize();
   viewport.PreviewMouseWheel+=(s,e)=>{if(geometry==null)return;ZoomAt(Math.Pow(1.2,e.Delta/120.0),e.GetPosition(viewport));e.Handled=true;};
   viewport.ManipulationStarting+=(s,e)=>{if(geometry==null||FromControl(e.OriginalSource)){e.Cancel();return;}e.ManipulationContainer=viewport;e.Mode=ManipulationModes.Scale|ManipulationModes.Translate;fitting=false;e.Handled=true;};
   viewport.ManipulationDelta+=(s,e)=>{if(geometry==null)return;Manipulate(Math.Sqrt(e.DeltaManipulation.Scale.X*e.DeltaManipulation.Scale.Y),e.ManipulationOrigin,e.DeltaManipulation.Translation.X,e.DeltaManipulation.Translation.Y);e.Handled=true;};
   viewport.ManipulationBoundaryFeedback+=(s,e)=>e.Handled=true;
   viewport.MouseLeftButtonDown+=(s,e)=>{if(geometry==null||e.StylusDevice!=null||FromControl(e.OriginalSource))return;viewport.Focus();previous=e.GetPosition(viewport);dragging=viewport.CaptureMouse();viewport.Cursor=Cursors.SizeAll;e.Handled=true;};
   viewport.MouseMove+=(s,e)=>{if(!dragging)return;if(e.LeftButton!=MouseButtonState.Pressed){viewport.ReleaseMouseCapture();return;}var point=e.GetPosition(viewport);Pan(point.X-previous.X,point.Y-previous.Y);previous=point;e.Handled=true;};
   viewport.MouseLeftButtonUp+=(s,e)=>{if(dragging){viewport.ReleaseMouseCapture();e.Handled=true;}};viewport.LostMouseCapture+=(s,e)=>{dragging=false;viewport.Cursor=Cursors.Arrow;};
   viewport.KeyDown+=(s,e)=>{if(geometry==null)return;if(e.Key==Key.F)Fit();else if(e.Key==Key.Add||e.Key==Key.OemPlus)ZoomAt(1.25,Center);else if(e.Key==Key.Subtract||e.Key==Key.OemMinus)ZoomAt(1/1.25,Center);else if(e.Key==Key.Left)Navigate(-40,0);else if(e.Key==Key.Right)Navigate(40,0);else if(e.Key==Key.Up)Navigate(0,-40);else if(e.Key==Key.Down)Navigate(0,40);else return;e.Handled=true;};
   UiHelp.Describe(viewport,"Scroll or pinch to zoom; drag to pan. F fits the image; arrow keys pan.");
  }
  bool FromControl(object source){var element=source as DependencyObject;while(element!=null&&element!=viewport){if(element is ButtonBase)return true;element=VisualTreeHelper.GetParent(element);}return false;}
  Button AddButton(Panel panel,string symbol,string label,Action action){
   var button=new Button{Content=symbol,Width=28,Height=28,Padding=new Thickness(0),Margin=new Thickness(1,0,1,0),FontSize=16,Background=new SolidColorBrush(Color.FromArgb(220,20,29,46)),Foreground=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromArgb(150,148,165,192)),BorderThickness=new Thickness(1)};
   AutomationProperties.SetName(button,label);UiHelp.Hint(button,label=="Recenter image"?"Fit image (F).":label.StartsWith("View")?label+" (when zoomed).":label);ToolTipService.SetShowOnDisabled(button,false);button.Click+=(s,e)=>{action();e.Handled=true;};panel.Children.Add(button);return button;
  }
  public void SetImage(BitmapSource source,bool reset){
   loading=false;
   if(media!=null){imageLayer.Children.Remove(media);media=null;}image.Visibility=Visibility.Visible;
   if(source==null){SetPlayback(null,false);targetMarks=null;targetNote=null;DrawTargets();}
   if(viewport.IsMouseCaptured)viewport.ReleaseMouseCapture();
   image.Source=source;controls.IsEnabled=source!=null;toolbarHost.Visibility=source==null?Visibility.Collapsed:Visibility.Visible;
   viewport.Background=source==null?Brushes.Transparent:Brushes.Black;if(source==null){geometry=null;Resize();NotifyFrame();return;}
   bool changed=geometry==null||image.Width!=source.PixelWidth||image.Height!=source.PixelHeight;
   if(reset||geometry==null||changed&&portrait)quarterTurns=portrait&&source.PixelWidth>source.PixelHeight?1:0;
   image.Width=source.PixelWidth;image.Height=source.PixelHeight;geometry=new PreviewGeometry(source.PixelWidth,source.PixelHeight,quarterTurns);
   if(reset||changed)fitting=true;Resize();NotifyFrame();
  }
  public void BeginLoading(int width=0,int height=0){
   if(media!=null){imageLayer.Children.Remove(media);media=null;}SetPlayback(null,false);
   if(viewport.IsMouseCaptured)viewport.ReleaseMouseCapture();
   image.Visibility=Visibility.Visible;image.Source=null;controls.IsEnabled=false;loading=true;targetMarks=null;targetNote=null;DrawTargets();
   // Keep the last frame until the new pixels arrive. On a first load, indexed
   // dimensions can establish its frame without opening/decoding the file.
   if(geometry==null&&width>0&&height>0){quarterTurns=portrait&&width>height?1:0;geometry=new PreviewGeometry(width,height,quarterTurns);image.Width=width;image.Height=height;fitting=true;}
   toolbarHost.Visibility=geometry==null?Visibility.Collapsed:Visibility.Visible;viewport.Background=Brushes.Black;Resize();NotifyFrame();
  }
  public void SetMedia(MediaElement element,int width,int height){
   loading=false;
   if(media!=null&&media!=element)imageLayer.Children.Remove(media);media=element;if(!imageLayer.Children.Contains(element))imageLayer.Children.Add(element);
   targetMarks=null;targetNote=null;DrawTargets();image.Visibility=Visibility.Collapsed;image.Width=width;image.Height=height;element.Width=width;element.Height=height;element.Stretch=Stretch.Fill;element.RenderTransform=transform;
   if(geometry==null||portrait)quarterTurns=portrait&&width>height?1:0;geometry=new PreviewGeometry(width,height,quarterTurns);controls.IsEnabled=true;toolbarHost.Visibility=Visibility.Visible;viewport.Background=Brushes.Black;fitting=true;Resize();NotifyFrame();
  }
  public void SetPlayback(Action toggle,bool playing){togglePlayback=toggle;playbackButton.Visibility=toggle==null?Visibility.Collapsed:Visibility.Visible;playbackButton.Content=playing?"Ⅱ":"▶";AutomationProperties.SetName(playbackButton,playing?"Pause playback":"Play playback");UiHelp.Hint(playbackButton,playing?"Pause playback":"Play playback");}
  public bool HasPlaybackControl{get{return playbackButton.Visibility==Visibility.Visible;}}
  public void TogglePlayback(){playbackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
  public void Resize(){
   double width,height,toolbar=geometry==null?0:ToolbarSpace;
   if(geometry==null){width=host.ActualWidth;height=Math.Min(130,Math.Max(0,host.ActualHeight));}
   else geometry.Frame(host.ActualWidth,Math.Max(0,host.ActualHeight-toolbar),out width,out height);
   viewport.Width=width;viewport.Height=height;
   // Fit the whole image first. The sky receives only genuinely unused space;
   // it never reserves a fixed height or feeds its own size back into image fit.
   if(remainder!=null){double top=Math.Min(host.ActualHeight,height+toolbar),space=Math.Max(0,host.ActualHeight-top);remainderOffset.Y=top;remainder.Height=space;remainder.Visibility=remainderEnabled&&(geometry!=null||loading)&&space>=50?Visibility.Visible:Visibility.Collapsed;}
   // Keep the bitmap's existing measure path, with a separate control area
   // immediately below the image rather than a new auto-sized image row.
   toolbarHost.MaxWidth=Math.Max(0,width-12);toolbarHost.Margin=new Thickness(6,height+6,6,0);
   if(geometry!=null){if(fitting)Fit();else Apply();}
  }
  Point Center{get{return new Point(viewport.Width/2,viewport.Height/2);}}
  public int RotationQuarterTurns{get{return quarterTurns;}}
  public bool HasRotationControls{get{return rotateLeftButton!=null&&rotateRightButton!=null;}}
  public void Rotate(int direction){if(geometry==null)return;quarterTurns=((quarterTurns+direction)%4+4)%4;geometry=new PreviewGeometry(image.Width,image.Height,quarterTurns);fitting=true;Resize();NotifyFrame();}
  public void Fit(){if(geometry==null)return;fitting=true;zoom.Fit(viewport.Width,viewport.Height,geometry.Width,geometry.Height);Apply();}
  void ZoomAt(double factor,Point origin){if(geometry==null)return;fitting=false;zoom.Zoom(factor,origin.X,origin.Y);Apply();}
  void Pan(double x,double y){if(geometry==null)return;fitting=false;zoom.Pan(x,y);Apply();}
  void Navigate(double x,double y){Pan(-x,-y);}
  void Manipulate(double factor,Point origin,double x,double y){fitting=false;zoom.Zoom(factor,origin.X,origin.Y);zoom.Pan(x,y);Apply();}
  void Apply(){if(geometry==null)return;zoom.Constrain(viewport.Width,viewport.Height,geometry.Width,geometry.Height);double scale=zoom.Scale;switch(quarterTurns){case 1:transform.Matrix=new Matrix(0,scale,-scale,0,zoom.X+image.Height*scale,zoom.Y);break;case 2:transform.Matrix=new Matrix(-scale,0,0,-scale,zoom.X+image.Width*scale,zoom.Y+image.Height*scale);break;case 3:transform.Matrix=new Matrix(0,-scale,scale,0,zoom.X,zoom.Y+image.Width*scale);break;default:transform.Matrix=new Matrix(scale,0,0,scale,zoom.X,zoom.Y);break;}
   double fitted=Math.Min(viewport.Width/geometry.Width,viewport.Height/geometry.Height);bool enlarged=scale>fitted+0.000001;zoomOutButton.IsEnabled=enlarged;zoomInButton.IsEnabled=scale<32;foreach(var button in panButtons)button.IsEnabled=enlarged;
   DrawTargets();
  }
  // Null marks hide the overlay. A note without marks explains why nothing is circled.
  public void SetTargets(IList<TargetMark> marks,string note){targetMarks=marks==null?null:new List<TargetMark>(marks);targetNote=marks==null?null:note;DrawTargets();}
  public int TargetCircles{get{return overlayLayer.Children.OfType<Ellipse>().Count(e=>e.Tag!=null);}}
  public IEnumerable<Rect> TargetLabelBounds{get{return overlayLayer.Children.OfType<Border>().Where(b=>b.Tag is TargetMark).Select(b=>new Rect(Canvas.GetLeft(b),Canvas.GetTop(b),b.ActualWidth>0?b.ActualWidth:b.DesiredSize.Width,b.ActualHeight>0?b.ActualHeight:b.DesiredSize.Height));}}
  public string TargetNoteText{get{var note=overlayLayer.Children.OfType<Border>().FirstOrDefault(b=>b.Tag==null);return note==null?null:((TextBlock)note.Child).Text;}}
  void DrawTargets(){
   overlayLayer.Children.Clear();
   bool show=targetMarks!=null&&geometry!=null&&media==null&&image.Source!=null&&image.Width>0&&image.Height>0;overlayLayer.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(!show)return;
   overlayLayer.Width=viewport.Width;overlayLayer.Height=viewport.Height;var placed=new List<Rect>();var bounds=new Rect(0,0,viewport.Width,viewport.Height);double scale=zoom.Scale;
   if(!string.IsNullOrEmpty(targetNote)){var note=Chip(Localization.T(targetNote),null);((TextBlock)note.Child).MaxWidth=Math.Max(60,viewport.Width-28);overlayLayer.Children.Add(note);note.Measure(new Size(Math.Max(0,viewport.Width-16),double.PositiveInfinity));Canvas.SetLeft(note,8);Canvas.SetTop(note,8);placed.Add(new Rect(8,8,note.DesiredSize.Width,note.DesiredSize.Height));}
   // Draw the least important first so the capture's own target sits on top.
   foreach(var mark in Enumerable.Reverse(targetMarks)){
    Point centre=transform.Matrix.Transform(new Point(mark.X*image.Width,mark.Y*image.Height));
    double radius=Math.Max(9,(mark.RadiusX*image.Width+mark.RadiusY*image.Height)/2*scale);
    if(centre.X+radius<0||centre.Y+radius<0||centre.X-radius>viewport.Width||centre.Y-radius>viewport.Height)continue;
    var halo=new Ellipse{Width=radius*2,Height=radius*2,StrokeThickness=mark.Primary?4.5:3.5,Stroke=new SolidColorBrush(Color.FromArgb(150,0,0,0))};
    var ring=new Ellipse{Width=radius*2,Height=radius*2,StrokeThickness=mark.Primary?2:1.4,Tag=mark};ring.SetResourceReference(Shape.StrokeProperty,"TargetMark");if(!mark.Primary)ring.StrokeDashArray=new DoubleCollection{4,3};
    foreach(var circle in new[]{halo,ring}){Canvas.SetLeft(circle,centre.X-radius);Canvas.SetTop(circle,centre.Y-radius);overlayLayer.Children.Add(circle);}
   }
   // Labels: try each side of the circle and skip one that would cover another label, so small panes stay readable.
   foreach(var mark in targetMarks){
    Point centre=transform.Matrix.Transform(new Point(mark.X*image.Width,mark.Y*image.Height));double radius=Math.Max(9,(mark.RadiusX*image.Width+mark.RadiusY*image.Height)/2*scale);
    if(!bounds.Contains(centre))continue;
    var chip=Chip(mark.Label,mark);overlayLayer.Children.Add(chip);chip.Measure(new Size(Math.Max(0,viewport.Width-8),double.PositiveInfinity));Size size=chip.DesiredSize;double offset=Math.Min(radius,Math.Max(viewport.Width,viewport.Height))*0.71+3;
    Rect? spot=null;foreach(var candidate in new[]{new Point(centre.X+offset,centre.Y-offset-size.Height),new Point(centre.X+offset,centre.Y+offset),new Point(centre.X-offset-size.Width,centre.Y-offset-size.Height),new Point(centre.X-offset-size.Width,centre.Y+offset)}){
     var rect=new Rect(Math.Max(4,Math.Min(viewport.Width-size.Width-4,candidate.X)),Math.Max(4,Math.Min(viewport.Height-size.Height-4,candidate.Y)),size.Width,size.Height);
     if(!placed.Any(r=>r.IntersectsWith(rect))){spot=rect;break;}
    }
    // Chips join the tree before measuring so their themed text size applies.
    if(spot==null){overlayLayer.Children.Remove(chip);continue;}placed.Add(spot.Value);Canvas.SetLeft(chip,spot.Value.X);Canvas.SetTop(chip,spot.Value.Y);
   }
  }
  static Border Chip(string text,TargetMark mark){
   var label=new TextBlock{Text=text,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=180};label.SetResourceReference(TextBlock.ForegroundProperty,"Text");label.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");if(mark!=null&&mark.Primary)label.FontWeight=FontWeights.SemiBold;if(mark==null){label.TextWrapping=TextWrapping.Wrap;label.MaxWidth=260;}
   var chip=new Border{Child=label,Padding=new Thickness(5,1,5,2),CornerRadius=new CornerRadius(4),BorderThickness=new Thickness(1),Tag=mark};chip.SetResourceReference(Border.BackgroundProperty,"Surface");chip.SetResourceReference(Border.BorderBrushProperty,mark==null?"Border":"TargetMark");return chip;
  }
  public void SmokeRotation(){
   if(!HasRotationControls||geometry==null||portrait||quarterTurns!=0)throw new InvalidOperationException("Large preview did not start in source orientation with rotation controls.");
   for(int turn=1;turn<=4;turn++){rotateRightButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(quarterTurns!=turn%4)throw new InvalidOperationException("Clockwise preview rotation did not advance.");var corners=new[]{new Point(0,0),new Point(image.Width,0),new Point(0,image.Height),new Point(image.Width,image.Height)};foreach(var corner in corners){Point displayed=transform.Transform(corner);if(displayed.X< -0.001||displayed.Y< -0.001||displayed.X>viewport.Width+0.001||displayed.Y>viewport.Height+0.001)throw new InvalidOperationException("Rotation clipped a source corner.");}}
   rotateLeftButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(quarterTurns!=3)throw new InvalidOperationException("Counterclockwise preview rotation failed.");Fit();if(quarterTurns!=3)throw new InvalidOperationException("Fit discarded the user's rotation.");rotateRightButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(quarterTurns!=0)throw new InvalidOperationException("Rotation did not return to the source orientation.");
   BitmapSource original=image.Source as BitmapSource;if(original!=null){rotateRightButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));SetImage(original,false);if(quarterTurns!=1)throw new InvalidOperationException("Animated refresh discarded the user's rotation.");SetImage(original,true);if(quarterTurns!=0)throw new InvalidOperationException("New image did not reset to source orientation.");}
  }

  public void SmokeGestures(){
   Fit();double fitted=zoom.Scale;if(geometry==null||viewport.Width<=0||viewport.Height<=0)throw new InvalidOperationException("Preview frame has no image area.");
   host.UpdateLayout();Rect imageArea=viewport.TransformToAncestor(host).TransformBounds(new Rect(viewport.RenderSize)),toolbarArea=toolbarHost.TransformToAncestor(host).TransformBounds(new Rect(toolbarHost.RenderSize));
   if(toolbarHost.Parent!=host||toolbarArea.Top<imageArea.Bottom||toolbarArea.Top-imageArea.Bottom>7||toolbarArea.Bottom>host.ActualHeight+0.5)throw new InvalidOperationException("Preview toolbar does not fit directly below the image.");
   if(Math.Abs(viewport.Width/viewport.Height-geometry.Width/geometry.Height)>0.000001||Math.Abs(zoom.X)>0.000001||Math.Abs(zoom.Y)>0.000001)throw new InvalidOperationException("Preview frame does not match the portrait image.");
   if(quarterTurns==1&&(transform.Matrix.M11!=0||transform.Matrix.M12!=fitted||transform.Matrix.M21!=-fitted))throw new InvalidOperationException("Landscape preview did not rotate into portrait.");
   if(zoomOutButton.IsEnabled||panButtons.Exists(b=>b.IsEnabled))throw new InvalidOperationException("Fitted preview offers pan/zoom-out actions that cannot move the image.");
   zoomInButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(zoom.Scale<=fitted||!zoomOutButton.IsEnabled||panButtons.Exists(b=>!b.IsEnabled))throw new InvalidOperationException("Zoom button did not enable image navigation.");zoomOutButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Math.Abs(zoom.Scale-fitted)>0.000001||zoomOutButton.IsEnabled)throw new InvalidOperationException("Zoom-out button did not restore the fitted image.");fitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.PreviewMouseWheelEvent};viewport.RaiseEvent(wheel);if(!wheel.Handled||zoom.Scale<=fitted)throw new InvalidOperationException("Preview wheel zoom did not update the image transform.");
   // Start each arrow at the centre so a pointer-anchored wheel cannot put it at a boundary.
   var keys=new[]{Key.Left,Key.Up,Key.Down,Key.Right};
   for(int i=0;i<panButtons.Count;i++){
    Fit();zoomInButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));double oldX=zoom.X,oldY=zoom.Y;panButtons[i].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(i==0&&zoom.X<=oldX||i==1&&zoom.Y<=oldY||i==2&&zoom.Y>=oldY||i==3&&zoom.X>=oldX)throw new InvalidOperationException("Preview navigation direction was not reversed: "+i);
    double buttonX=zoom.X,buttonY=zoom.Y;Fit();zoomInButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(viewport),0,keys[i]){RoutedEvent=Keyboard.KeyDownEvent};viewport.RaiseEvent(key);
    if(!key.Handled||Math.Abs(zoom.X-buttonX)>0.000001||Math.Abs(zoom.Y-buttonY)>0.000001)throw new InvalidOperationException("Keyboard arrow differs from preview button: "+i);
   }
   fitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Math.Abs(zoom.Scale-fitted)>0.000001||Math.Abs(zoom.X)>0.000001||Math.Abs(zoom.Y)>0.000001)throw new InvalidOperationException("Preview recenter button did not restore its frame.");
   Manipulate(1.5,Center,10,-10);if(zoom.Scale<=fitted)throw new InvalidOperationException("Preview pinch did not zoom.");
   Pan(-100000,100000);if(zoom.X>0||zoom.Y>0||zoom.X+geometry.Width*zoom.Scale<viewport.Width-0.000001||zoom.Y+geometry.Height*zoom.Scale<viewport.Height-0.000001)throw new InvalidOperationException("Preview pan exposed an empty border.");
   ZoomAt(0.00001,Center);if(Math.Abs(zoom.Scale-fitted)>0.000001)throw new InvalidOperationException("Preview zoomed out beyond the full image.");Fit();
  }
 }
}
