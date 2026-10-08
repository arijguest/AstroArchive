using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Input;

namespace AstroArchive {
 // An orthographic 3D sphere drawn as one cached WPF drawing, without a 3D scene,
 // animation timer, web view or individual UI elements for stars and line segments.
 public sealed class SkyGlobeView:FrameworkElement {
  public static readonly DependencyProperty LineBrushProperty=Palette("LineBrush");
  public static readonly DependencyProperty TextBrushProperty=Palette("TextBrush");
  public static readonly DependencyProperty AccentBrushProperty=Palette("AccentBrush");
  public static readonly DependencyProperty SurfaceBrushProperty=Palette("SurfaceBrush");
  static DependencyProperty Palette(string name){return DependencyProperty.Register(name,typeof(Brush),typeof(SkyGlobeView),new FrameworkPropertyMetadata(Brushes.Gray,FrameworkPropertyMetadataOptions.AffectsRender));}
  CaptureSky context;SkyVector[] stars;DrawingGroup drawing;double lastWidth,lastHeight;object[] palette;
  SkyVector right,up,front;double radius;Point centre;internal int DrawingBuilds;
  internal readonly SkyGlobeCamera Camera=new SkyGlobeCamera();
  Point dragPoint;bool dragging;
  public CaptureSky Context{get{return context;}}
  const string NavigationHelp="Drag to rotate. Scroll or pinch to zoom. Arrow keys rotate; plus/minus zoom. Double-click, Home or the reset button restores the capture view.";
  void ViewChanged(){drawing=null;InvalidateVisual();}
  public void ResetView(){Camera.Reset();ViewChanged();}
  internal void RotateView(double dx,double dy){double scale=180/(Math.PI*Math.Max(25,radius));Camera.Orbit(-dx*scale,dy*scale);ViewChanged();}
  internal void ZoomView(double factor){Camera.Magnify(factor);ViewChanged();}
  protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){
   base.OnMouseLeftButtonDown(e);if(e.StylusDevice!=null)return;Focus();
   if(e.ClickCount==2){ResetView();e.Handled=true;return;}
   dragPoint=e.GetPosition(this);dragging=CaptureMouse();if(dragging)Cursor=Cursors.SizeAll;e.Handled=true;
  }
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;if(e.LeftButton!=MouseButtonState.Pressed){ReleaseMouseCapture();return;}var point=e.GetPosition(this);RotateView(point.X-dragPoint.X,point.Y-dragPoint.Y);dragPoint=point;e.Handled=true;}
  protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);if(!dragging)return;ReleaseMouseCapture();e.Handled=true;}
  protected override void OnLostMouseCapture(MouseEventArgs e){base.OnLostMouseCapture(e);dragging=false;Cursor=Cursors.Hand;}
  protected override void OnMouseWheel(MouseWheelEventArgs e){base.OnMouseWheel(e);ZoomView(Math.Pow(1.2,e.Delta/120.0));e.Handled=true;}
  protected override void OnManipulationStarting(ManipulationStartingEventArgs e){base.OnManipulationStarting(e);e.ManipulationContainer=this;e.Mode=ManipulationModes.Translate|ManipulationModes.Scale;e.Handled=true;}
  protected override void OnManipulationDelta(ManipulationDeltaEventArgs e){
   base.OnManipulationDelta(e);if(e.IsInertial){e.Complete();e.Handled=true;return;}
   RotateView(e.DeltaManipulation.Translation.X,e.DeltaManipulation.Translation.Y);ZoomView(Math.Sqrt(e.DeltaManipulation.Scale.X*e.DeltaManipulation.Scale.Y));e.Handled=true;
  }
  protected override void OnKeyDown(KeyEventArgs e){
   base.OnKeyDown(e);switch(e.Key){
    case Key.Left:Camera.Orbit(10,0);break;case Key.Right:Camera.Orbit(-10,0);break;
    case Key.Up:Camera.Orbit(0,-10);break;case Key.Down:Camera.Orbit(0,10);break;
    case Key.Add:case Key.OemPlus:Camera.Magnify(1.2);break;case Key.Subtract:case Key.OemMinus:Camera.Magnify(1/1.2);break;
    case Key.Home:case Key.D0:case Key.NumPad0:case Key.F:Camera.Reset();break;default:return;
   }ViewChanged();e.Handled=true;
  }
  public SkyGlobeView(){
   SetResourceReference(LineBrushProperty,"Muted");SetResourceReference(TextBrushProperty,"Text");SetResourceReference(AccentBrushProperty,"Accent");SetResourceReference(SurfaceBrushProperty,"SurfaceAlt");
   Focusable=true;IsManipulationEnabled=true;Cursor=Cursors.Hand;ClipToBounds=true;IsVisibleChanged+=(s,e)=>{if(!IsVisible&&IsMouseCaptured)ReleaseMouseCapture();};Unloaded+=(s,e)=>{if(IsMouseCaptured)ReleaseMouseCapture();};AutomationProperties.SetHelpText(this,NavigationHelp);SetContext(CaptureSky.Resolve(null,null));
  }
  public void SetContext(CaptureSky value){
   value=value??CaptureSky.Resolve(null,null);bool changed=context==null||context.Key!=value.Key;context=value;
   ToolTip=value.TargetLabel+"\n"+value.Summary+"\n"+value.Evidence+"\n"+NavigationHelp;AutomationProperties.SetName(this,"Capture sky. "+value.TargetLabel+". "+value.TimeLabel+". "+value.Summary);
   if(!changed)return;
   var target=value.Orientation.Map(SkyVector.Equatorial(value.RA,value.Dec));Camera.SetHome(value.HasPosition?Math.Atan2(target.X,target.Y)*180/Math.PI:180,value.HasHorizon&&value.Altitude<0?-25:25);
   stars=SkyFigures.Stars.Select(value.Orientation.Map).ToArray();drawing=null;InvalidateVisual();
  }
  Point Project(SkyVector vector){return new Point(centre.X+radius*vector.Dot(right),centre.Y-radius*vector.Dot(up));}
  static Pen Stroke(Brush brush,double opacity,double width){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();var pen=new Pen(copy,width);pen.Freeze();return pen;}
  static Brush Tint(Brush brush,double opacity){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();return copy;}
  void Ring(DrawingContext dc,double altitude,Pen visible,Pen faint){
   var previous=SkyVector.Horizontal(0,altitude);for(int angle=5;angle<=360;angle+=5){var next=SkyVector.Horizontal(angle,altitude);dc.DrawLine((previous.Dot(front)+next.Dot(front))/2>=0?visible:faint,Project(previous),Project(next));previous=next;}
  }
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);dc.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize));if(ActualWidth<50||ActualHeight<50)return;
   var colours=new[]{GetValue(LineBrushProperty),GetValue(TextBrushProperty),GetValue(AccentBrushProperty),GetValue(SurfaceBrushProperty)};
   if(drawing==null||lastWidth!=ActualWidth||lastHeight!=ActualHeight||palette==null||colours.Where((c,i)=>!ReferenceEquals(c,palette[i])).Any()){
    lastWidth=ActualWidth;lastHeight=ActualHeight;palette=colours;drawing=Build((Brush)colours[0],(Brush)colours[1],(Brush)colours[2],(Brush)colours[3]);DrawingBuilds++;
   }dc.DrawDrawing(drawing);
  }
  DrawingGroup Build(Brush line,Brush text,Brush accent,Brush surface){
   var group=new DrawingGroup();centre=new Point(ActualWidth/2,ActualHeight/2);radius=Math.Max(5,Math.Min(ActualWidth/2-10,ActualHeight/2-10))*Camera.Zoom;
   var target=context.Orientation.Map(SkyVector.Equatorial(context.RA,context.Dec));double yaw=Camera.Yaw*Math.PI/180,tilt=Camera.Tilt*Math.PI/180;
   right=new SkyVector(Math.Cos(yaw),-Math.Sin(yaw),0);up=new SkyVector(-Math.Sin(yaw)*Math.Sin(tilt),-Math.Cos(yaw)*Math.Sin(tilt),Math.Cos(tilt));front=new SkyVector(Math.Sin(yaw)*Math.Cos(tilt),Math.Cos(yaw)*Math.Cos(tilt),Math.Sin(tilt));
   Pen horizon=Stroke(line,0.85,1),grid=Stroke(line,0.22,0.7),rear=Stroke(line,0.10,0.7),below=Stroke(line,0.13,0.7),figures=Stroke(line,0.60,0.8),near=Stroke(line,0.9,1);
   using(var dc=group.Open()){
    dc.DrawEllipse(surface,Stroke(line,0.25,0.8),centre,radius,radius);Ring(dc,0,horizon,rear);Ring(dc,45,grid,rear);Ring(dc,-45,grid,rear);
    foreach(double azimuth in new[]{0.0,90,180,270}){var previous=SkyVector.Horizontal(azimuth,-90);for(int alt=-85;alt<=90;alt+=5){var next=SkyVector.Horizontal(azimuth,alt);dc.DrawLine((previous.Dot(front)+next.Dot(front))/2>=0?grid:rear,Project(previous),Project(next));previous=next;}}
    var points=stars.Select(Project).ToArray();
    foreach(var figure in SkyFigures.Figures)foreach(var path in figure.Paths)for(int i=1;i<path.Length;i++){
     var a=stars[path[i-1]];var b=stars[path[i]];double depth=(a.Dot(front)+b.Dot(front))/2;
     Pen pen=depth<0?rear:context.HasHorizon&&(a.Z+b.Z)/2<0?below:context.HasPosition&&(a.Dot(target)+b.Dot(target))/2>0.82?near:figures;
     dc.DrawLine(pen,points[path[i-1]],points[path[i]]);
    }
    Brush starBrush=Tint(text,0.65),faintStar=Tint(line,0.25);for(int i=0;i<stars.Length;i++)if(stars[i].Dot(front)>0)dc.DrawEllipse(context.HasHorizon&&stars[i].Z<0?faintStar:starBrush,null,points[i],1.05,1.05);
    if(context.HasPosition&&target.Dot(front)>=0){
     var point=Project(target);dc.DrawEllipse(Tint(accent,0.12),Stroke(accent,1,1.5),point,6,6);dc.DrawEllipse(accent,null,point,2,2);
     dc.DrawLine(Stroke(accent,0.8,1),new Point(point.X-10,point.Y),new Point(point.X-7,point.Y));dc.DrawLine(Stroke(accent,0.8,1),new Point(point.X+7,point.Y),new Point(point.X+10,point.Y));
    }
   }group.Freeze();return group;
  }
 }
}
