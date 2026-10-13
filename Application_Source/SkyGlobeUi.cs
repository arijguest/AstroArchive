using System;
using System.Collections.Generic;
using System.Globalization;
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
  SkyVector right,up,front;double radius;Point centre;internal int DrawingBuilds;internal bool TargetMarkerDrawn;
  // Moon and Sun directions in the same frame as the drawn stars.
  SkyVector moon,sun;internal bool MoonDrawn,MoonBehindGlobe;internal Rect MoonBounds;
  internal readonly SkyGlobeCamera Camera=new SkyGlobeCamera();
  internal readonly Dictionary<string,Rect> CardinalLabels=new Dictionary<string,Rect>();
  internal Rect GlobeBounds{get{return new Rect(centre.X-radius,centre.Y-radius,2*radius,2*radius);}}
  Point dragPoint,pressPoint;bool dragging,moved;
  // A clicked constellation is outlined and named until the next click, Escape or capture.
  int selectedFigure=-1;internal Rect ConstellationLabelBounds=Rect.Empty,SunBounds=Rect.Empty,TargetBounds=Rect.Empty;
  internal string SelectedConstellation{get{return selectedFigure<0?null:SkyFigures.Figures[selectedFigure].Name;}}
  // Raised when the Moon, Sun, target or eclipse close-up is clicked, for Capture details.
  public event EventHandler DetailsRequested;
  public CaptureSky Context{get{return context;}}
  const string NavigationHelp="Drag to rotate; scroll or pinch to zoom.\nClick a constellation to name it, or the Moon, Sun or target for capture details.";
  void ViewChanged(){drawing=null;InvalidateVisual();}
  // Raised after the globe's size changes so overlay controls can follow it.
  public event EventHandler GlobeResized;
  protected override void OnRenderSizeChanged(SizeChangedInfo info){base.OnRenderSizeChanged(info);var handler=GlobeResized;if(handler!=null)handler(this,EventArgs.Empty);}
  // Unzoomed globe radius: zooming changes the drawing, not the reset control.
  internal double HomeRadius{get{return Math.Max(5,Math.Min(ActualWidth/2-14,ActualHeight/2-14));}}
  // Keeps a reset button at the globe's lower-left, sized in proportion to the globe.
  internal static void PlaceReset(System.Windows.Controls.Button button,SkyGlobeView globe,UIElement container){
   if(button==null||globe==null||container==null||!globe.IsVisible||globe.ActualWidth<=0||globe.ActualHeight<=0)return;
   double r=globe.HomeRadius,size=Math.Round(Math.Max(22,Math.Min(40,r*0.17)));
   Point corner;try{corner=globe.TranslatePoint(new Point(globe.ActualWidth/2-r,globe.ActualHeight/2+r),container);}catch(InvalidOperationException){return;}
   button.Width=button.Height=size;var icon=button.Content as FrameworkElement;if(icon!=null)icon.Width=icon.Height=size*0.7;
   var move=button.RenderTransform as TranslateTransform;if(move==null||move.IsFrozen){move=new TranslateTransform();button.RenderTransform=move;}
   move.X=Math.Max(2,corner.X);move.Y=Math.Max(2,corner.Y-size);
  }
  public void ResetView(){Camera.Reset();ViewChanged();}
  internal void RotateView(double dx,double dy){double scale=180/(Math.PI*Math.Max(25,radius));Camera.Orbit(-dx*scale,dy*scale);ViewChanged();}
  internal void ZoomView(double factor){Camera.Magnify(factor);ViewChanged();}
  protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){
   base.OnMouseLeftButtonDown(e);if(e.StylusDevice!=null)return;Focus();
   if(e.ClickCount==2){ResetView();e.Handled=true;return;}
   dragPoint=pressPoint=e.GetPosition(this);moved=false;dragging=CaptureMouse();if(dragging)Cursor=Cursors.SizeAll;e.Handled=true;
  }
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;if(e.LeftButton!=MouseButtonState.Pressed){ReleaseMouseCapture();return;}var point=e.GetPosition(this);if((point-pressPoint).Length>4)moved=true;RotateView(point.X-dragPoint.X,point.Y-dragPoint.Y);dragPoint=point;e.Handled=true;}
  protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);if(!dragging)return;bool click=!moved;ReleaseMouseCapture();if(click)Tap(e.GetPosition(this));e.Handled=true;}
  // A press that did not rotate the globe: open details for a body, else name a constellation.
  internal void Tap(Point point){
   if(context.BelowHorizon)return;
   if(OnBody(point)){RequestDetails();return;}
   int figure=ConstellationLabelBounds.Contains(point)?-1:FigureAt(point);
   if(figure==selectedFigure)figure=-1;SelectConstellation(figure);
  }
  internal void SelectConstellation(int figure){if(figure==selectedFigure)return;selectedFigure=figure;AutomationProperties.SetItemStatus(this,figure<0?"":Localization.T(SkyFigures.Figures[figure].Name));ViewChanged();}
  void RequestDetails(){var handler=DetailsRequested;if(handler!=null)handler(this,EventArgs.Empty);}
  internal bool OnBody(Point point){
   Func<Rect,bool> hit=r=>{if(r.IsEmpty)return false;r.Inflate(4,4);return r.Contains(point);};
   return TargetMarkerDrawn&&hit(TargetBounds)||MoonDrawn&&!MoonBehindGlobe&&hit(MoonBounds)||SunDrawn&&hit(SunBounds)||EclipseDrawn&&EclipseBounds.Contains(point);
  }
  // Nearest near-side figure line within reach of the pointer, or -1.
  internal int FigureAt(Point point){
   if(stars==null||radius<=0)return -1;int best=-1;double nearest=10;
   for(int f=0;f<SkyFigures.Figures.Length;f++)foreach(var path in SkyFigures.Figures[f].Paths)for(int i=1;i<path.Length;i++){
    var a=stars[path[i-1]];var b=stars[path[i]];if(a.Dot(front)<0||b.Dot(front)<0)continue;
    double d=SegmentDistance(point,Project(a),Project(b));if(d<nearest){nearest=d;best=f;}
   }
   return best;
  }
  // The middle of a near-side line of a constellation, clear of the bodies, for checks; NaN when none is.
  internal Point FigureProbe(int figure){
   foreach(var path in SkyFigures.Figures[figure].Paths)for(int i=1;i<path.Length;i++){var a=stars[path[i-1]];var b=stars[path[i]];if(a.Dot(front)>0.2&&b.Dot(front)>0.2){var p=Project(a);var q=Project(b);var middle=new Point((p.X+q.X)/2,(p.Y+q.Y)/2);if(!OnBody(middle)&&(TargetBounds.IsEmpty||(middle-new Point(TargetBounds.X+TargetBounds.Width/2,TargetBounds.Y+TargetBounds.Height/2)).Length>30))return middle;}}
   return new Point(double.NaN,double.NaN);
  }
  static double SegmentDistance(Point p,Point a,Point b){
   var ab=b-a;double length=ab.LengthSquared,t=length<1e-9?0:Math.Max(0,Math.Min(1,((p-a).X*ab.X+(p-a).Y*ab.Y)/length));
   return (p-(a+ab*t)).Length;
  }
  protected override void OnManipulationCompleted(ManipulationCompletedEventArgs e){
   base.OnManipulationCompleted(e);var total=e.TotalManipulation;
   if(total!=null&&total.Translation.Length<6&&Math.Abs(total.Scale.X*total.Scale.Y-1)<0.05)Tap(e.ManipulationOrigin);
  }
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
    case Key.Home:case Key.D0:case Key.NumPad0:case Key.F:Camera.Reset();break;
    case Key.Escape:if(selectedFigure<0)return;SelectConstellation(-1);e.Handled=true;return;
    case Key.Enter:case Key.Space:RequestDetails();e.Handled=true;return;default:return;
   }ViewChanged();e.Handled=true;
  }
  public SkyGlobeView(){
   SetResourceReference(LineBrushProperty,"Muted");SetResourceReference(TextBrushProperty,"Text");SetResourceReference(AccentBrushProperty,"Accent");SetResourceReference(SurfaceBrushProperty,"SurfaceAlt");
   Focusable=true;IsManipulationEnabled=true;Cursor=Cursors.Hand;ClipToBounds=true;IsVisibleChanged+=(s,e)=>{if(!IsVisible&&IsMouseCaptured)ReleaseMouseCapture();};Unloaded+=(s,e)=>{if(IsMouseCaptured)ReleaseMouseCapture();};AutomationProperties.SetHelpText(this,NavigationHelp.Replace("\n"," "));SetContext(CaptureSky.Resolve(null,null));
  }
  public void SetContext(CaptureSky value){
   value=value??CaptureSky.Resolve(null,null);bool changed=context==null||context.Key!=value.Key;if(changed)context=value;
   string lunar=value.Moon==null?"":"Moon: "+value.Moon.PhaseName+" · "+value.Moon.IlluminationText+" illuminated"+(value.Moon.HasHorizon?" · "+(value.Moon.Altitude<0?"below horizon":"Alt "+value.Moon.Altitude.ToString("0",CultureInfo.InvariantCulture)+"°"):"");
   if(changed)SelectConstellation(-1);ToolTip=value.TargetLabel+"\n"+value.TimeLabel+" · "+value.Summary+(value.ApproximatePosition?" · approximate":"")+(lunar.Length>0?"\n"+lunar:"")+"\n"+NavigationHelp;AutomationProperties.SetName(this,"Capture sky. "+value.TargetLabel+". "+value.TimeLabel+". "+value.Summary+(lunar.Length>0?". "+lunar:""));
   if(!changed)return;
   if(value.Moon!=null){moon=value.Orientation.MapOfDate(value.Moon.Topocentric);sun=value.Orientation.MapOfDate(value.Moon.Sun);}
   var target=value.TargetOnGlobe;Camera.SetHome(value.HasPosition?Math.Atan2(target.X,target.Y)*180/Math.PI:180,value.HasHorizon&&value.Altitude<0?-25:25);
   stars=SkyFigures.Stars.Select(value.Orientation.Map).ToArray();drawing=null;InvalidateVisual();
  }
  Point Project(SkyVector vector){return new Point(centre.X+radius*vector.Dot(right),centre.Y-radius*vector.Dot(up));}
  static Pen Stroke(Brush brush,double opacity,double width){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();var pen=new Pen(copy,width);pen.Freeze();return pen;}
  static Brush Tint(Brush brush,double opacity){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();return copy;}
  void Ring(DrawingContext dc,double altitude,Pen visible,Pen faint){
   var previous=SkyVector.Horizontal(0,altitude);for(int angle=5;angle<=360;angle+=5){var next=SkyVector.Horizontal(angle,altitude);dc.DrawLine((previous.Dot(front)+next.Dot(front))/2>=0?visible:faint,Project(previous),Project(next));previous=next;}
  }
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);if(context.BelowHorizon){CardinalLabels.Clear();TargetMarkerDrawn=false;MoonDrawn=false;return;}dc.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize));if(ActualWidth<50||ActualHeight<50)return;
   var colours=new[]{GetValue(LineBrushProperty),GetValue(TextBrushProperty),GetValue(AccentBrushProperty),GetValue(SurfaceBrushProperty)};
   if(drawing==null||lastWidth!=ActualWidth||lastHeight!=ActualHeight||palette==null||colours.Where((c,i)=>!ReferenceEquals(c,palette[i])).Any()){
    lastWidth=ActualWidth;lastHeight=ActualHeight;palette=colours;drawing=Build((Brush)colours[0],(Brush)colours[1],(Brush)colours[2],(Brush)colours[3]);DrawingBuilds++;
   }dc.DrawDrawing(drawing);
  }
  void DrawCardinals(DrawingContext dc,Brush brush){
   CardinalLabels.Clear();if(!context.HasHorizon)return;
   string[] names={"N","E","S","W"};
   for(int i=0;i<names.Length;i++){
    var direction=SkyVector.Horizontal(i*90,0);if(direction.Dot(front)<-0.001)continue;
    var label=new FormattedText(names[i],CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);
    // Follow the projected horizon direction, without forcing foreshortened
    // labels onto the rim where they would misrepresent the camera orientation.
    var anchor=new Point(centre.X+(radius+18)*direction.Dot(right),centre.Y-(radius+18)*direction.Dot(up));
    var bounds=new Rect(anchor.X-label.Width/2,anchor.Y-label.Height/2,label.Width,label.Height);
    double dx=Math.Max(bounds.Left-centre.X,Math.Max(0,centre.X-bounds.Right)),dy=Math.Max(bounds.Top-centre.Y,Math.Max(0,centre.Y-bounds.Bottom));
    if(!new Rect(RenderSize).Contains(bounds)||dx*dx+dy*dy<(radius+4)*(radius+4)||CardinalLabels.Values.Any(b=>b.IntersectsWith(bounds)))continue;
    CardinalLabels[names[i]]=bounds;dc.DrawText(label,bounds.TopLeft);
   }
  }
  internal static readonly Color MoonLight=Color.FromRgb(246,233,186);
  // Lit limb faces the Sun along the projected great circle; the terminator is an
  // ellipse whose half-width is |cos(phase angle)| of the radius.
  internal static Geometry MoonLitGeometry(Point centre,double radius,double illumination,double angleDegrees){
   double c=Math.Max(-1,Math.Min(1,2*illumination-1)),half=Math.Abs(c)*radius;var geometry=new StreamGeometry();
   using(var g=geometry.Open()){
    g.BeginFigure(new Point(centre.X,centre.Y-radius),true,true);
    g.ArcTo(new Point(centre.X,centre.Y+radius),new Size(radius,radius),0,false,SweepDirection.Clockwise,true,false);
    if(half<0.01)g.LineTo(new Point(centre.X,centre.Y-radius),true,false);
    else g.ArcTo(new Point(centre.X,centre.Y-radius),new Size(half,radius),0,false,c>0?SweepDirection.Clockwise:SweepDirection.Counterclockwise,true,false);
   }
   geometry.Transform=new RotateTransform(angleDegrees,centre.X,centre.Y);geometry.Freeze();return geometry;
  }
  internal static void DrawMoonDisc(DrawingContext dc,Point point,double size,double illumination,double angleDegrees,Brush dark,Pen outline,double opacity){
   if(opacity<1)dc.PushOpacity(opacity);
   dc.DrawEllipse(dark,null,point,size,size);
   var light=new SolidColorBrush(MoonLight);light.Freeze();if(illumination>0.001)dc.DrawGeometry(light,null,MoonLitGeometry(point,size,illumination,angleDegrees));
   dc.DrawEllipse(null,outline,point,size,size);
   if(opacity<1)dc.Pop();
  }
  void DrawMoon(DrawingContext dc,Brush line,Brush text,double opacity){
   var point=Project(moon);var toward=Project(MoonEphemeris.Toward(moon,sun,0.05));double dx=toward.X-point.X,dy=toward.Y-point.Y;
   // Looking straight along the Sun direction leaves no projected limb direction; any angle is then equivalent.
   double angle=dx*dx+dy*dy<1e-9?0:Math.Atan2(dy,dx)*180/Math.PI,size=Math.Max(6,Math.Min(11,radius*0.065));
   DrawMoonDisc(dc,point,size,context.Moon.Illumination,angle,Tint(line,0.55),Stroke(text,0.55,0.8),opacity);
   MoonDrawn=true;MoonBehindGlobe=moon.Dot(front)<0;MoonBounds=new Rect(point.X-size,point.Y-size,2*size,2*size);
  }
  internal bool SunDrawn,EclipseDrawn;internal Rect EclipseBounds;
  static readonly Color SunLight=Color.FromRgb(255,206,84);
  void DrawSun(DrawingContext dc,double opacity){
   var point=Project(sun);double size=Math.Max(6,Math.Min(12,radius*0.07));if(opacity<1)dc.PushOpacity(opacity);
   bool eclipsed=context.Eclipse!=null&&context.Eclipse.Solar;
   if(!eclipsed){var glow=new RadialGradientBrush(Color.FromArgb(110,255,214,110),Color.FromArgb(0,255,214,110));glow.Freeze();dc.DrawEllipse(glow,null,point,size*2.2,size*2.2);}
   var disc=new SolidColorBrush(SunLight);disc.Freeze();dc.DrawEllipse(disc,null,point,size,size);
   if(eclipsed){
    var e=context.Eclipse;var other=Project(moon);double dx=other.X-point.X,dy=other.Y-point.Y,length=Math.Sqrt(dx*dx+dy*dy);if(length<1e-9){dx=1;dy=0;length=1;}
    double scale=size/e.SunRadiusArcmin;var shadow=new SolidColorBrush(Color.FromRgb(22,26,38));shadow.Freeze();
    dc.PushClip(new EllipseGeometry(point,size,size));dc.DrawEllipse(shadow,null,new Point(point.X+dx/length*e.SeparationArcmin*scale,point.Y+dy/length*e.SeparationArcmin*scale),e.MoonRadiusArcmin*scale,e.MoonRadiusArcmin*scale);dc.Pop();
    dc.DrawEllipse(null,Stroke(disc,0.6,0.6),point,size,size);EclipseDrawn=true;EclipseBounds=new Rect(point.X-size,point.Y-size,2*size,2*size);
   }
   if(opacity<1)dc.Pop();SunDrawn=true;SunBounds=new Rect(point.X-size,point.Y-size,2*size,2*size);
  }
  // Companion close-up for an eclipse: the discs at their true relative size and
  // separation, oriented as on the globe, with the covered fraction.
  void DrawEclipse(DrawingContext dc,Brush line,Brush text){
   var e=context.Eclipse;double side=Math.Max(64,Math.Min(120,radius*0.62)),margin=6;EclipseBounds=new Rect(ActualWidth-side-margin,margin,side,side);
   var frame=new SolidColorBrush(e.Solar?Color.FromArgb(235,96,150,215):Color.FromArgb(235,12,16,28));frame.Freeze();dc.DrawRoundedRectangle(frame,Stroke(line,0.5,0.8),EclipseBounds,8,8);
   var middle=new Point(EclipseBounds.X+side/2,EclipseBounds.Y+side*0.44);
   var from=e.Solar?Project(sun):Project(new SkyVector(-sun.X,-sun.Y,-sun.Z));var to=Project(moon);double dx=to.X-from.X,dy=to.Y-from.Y,length=Math.Sqrt(dx*dx+dy*dy);if(length<1e-9){dx=1;dy=0;length=1;}
   double reference=e.Solar?e.SunRadiusArcmin:e.PenumbraRadiusArcmin,scale=side*(e.Solar?0.26:0.34)/reference,offset=Math.Min(side*0.4,e.SeparationArcmin*scale);
   var moonPoint=new Point(middle.X+dx/length*offset,middle.Y+dy/length*offset);double moonSize=e.MoonRadiusArcmin*scale;
   dc.PushClip(new RectangleGeometry(EclipseBounds,8,8));
   if(e.Solar){
    var corona=new RadialGradientBrush(Color.FromArgb(120,255,236,180),Color.FromArgb(0,255,236,180));corona.Freeze();dc.DrawEllipse(corona,null,middle,e.SunRadiusArcmin*scale*1.6,e.SunRadiusArcmin*scale*1.6);
    var disc=new SolidColorBrush(SunLight);disc.Freeze();dc.DrawEllipse(disc,null,middle,e.SunRadiusArcmin*scale,e.SunRadiusArcmin*scale);
    var shadow=new SolidColorBrush(Color.FromRgb(22,26,38));shadow.Freeze();dc.DrawEllipse(shadow,null,moonPoint,moonSize,moonSize);
   }else{
    var penumbra=new SolidColorBrush(Color.FromArgb(70,150,150,170));penumbra.Freeze();dc.DrawEllipse(penumbra,null,middle,e.PenumbraRadiusArcmin*scale,e.PenumbraRadiusArcmin*scale);
    var umbra=new SolidColorBrush(Color.FromArgb(150,120,30,24));umbra.Freeze();
    var disc=new SolidColorBrush(MoonLight);disc.Freeze();dc.DrawEllipse(disc,null,moonPoint,moonSize,moonSize);
    dc.PushClip(new EllipseGeometry(moonPoint,moonSize,moonSize));dc.DrawEllipse(umbra,null,middle,e.ShadowRadiusArcmin*scale,e.ShadowRadiusArcmin*scale);dc.Pop();
    dc.DrawEllipse(null,Stroke(line,0.6,0.8),middle,e.ShadowRadiusArcmin*scale,e.ShadowRadiusArcmin*scale);
   }
   dc.Pop();
   string caption=e.Solar||e.Kind!="Penumbral"?(e.Obscuration*100).ToString(e.Obscuration>=0.995?"0.#":"0",CultureInfo.InvariantCulture)+"% · "+e.Kind:"Penumbral";
   var label=new FormattedText(caption,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),11,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
   label.MaxTextWidth=side-8;label.Trimming=TextTrimming.CharacterEllipsis;label.MaxLineCount=1;dc.DrawText(label,new Point(EclipseBounds.X+(side-Math.Min(label.Width,side-8))/2,EclipseBounds.Bottom-label.Height-4));EclipseDrawn=true;
  }
  // The name sits over the constellation's label point, or the middle of its visible
  // lines when that point has turned to the far side; it never leaves the view.
  void DrawConstellationName(DrawingContext dc,Brush text,Brush accent,Brush surface,Point[] points){
   var figure=SkyFigures.Figures[selectedFigure];var label=context.Orientation.Map(figure.Label);Point anchor;
   if(label.Dot(front)>=0)anchor=Project(label);
   else{var visible=figure.Paths.SelectMany(p=>p).Distinct().Where(i=>stars[i].Dot(front)>=0).Select(i=>points[i]).ToList();if(visible.Count==0){return;}anchor=new Point(visible.Average(p=>p.X),visible.Average(p=>p.Y));}
   var name=new FormattedText(Localization.T(figure.Name),CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),12,text,VisualTreeHelper.GetDpi(this).PixelsPerDip);
   name.MaxTextWidth=Math.Max(40,ActualWidth-24);name.MaxLineCount=1;name.Trimming=TextTrimming.CharacterEllipsis;
   double width=Math.Min(name.Width,name.MaxTextWidth)+16,height=name.Height+6;
   var bounds=new Rect(anchor.X-width/2,anchor.Y-height-8,width,height);
   bounds.X=Math.Max(2,Math.Min(ActualWidth-width-2,bounds.X));bounds.Y=Math.Max(2,Math.Min(ActualHeight-height-2,bounds.Y));
   dc.DrawRoundedRectangle(Tint(surface,0.94),Stroke(accent,0.9,1),bounds,height/2,height/2);dc.DrawText(name,new Point(bounds.X+8,bounds.Y+3));
   ConstellationLabelBounds=bounds;
  }
  DrawingGroup Build(Brush line,Brush text,Brush accent,Brush surface){
   EclipseDrawn=false;
   var group=new DrawingGroup();centre=new Point(ActualWidth/2,ActualHeight/2);radius=Math.Max(5,Math.Min(ActualWidth/2-14,ActualHeight/2-14))*Camera.Zoom;
   var target=context.TargetOnGlobe;double yaw=Camera.Yaw*Math.PI/180,tilt=Camera.Tilt*Math.PI/180;
   right=new SkyVector(Math.Cos(yaw),-Math.Sin(yaw),0);up=new SkyVector(-Math.Sin(yaw)*Math.Sin(tilt),-Math.Cos(yaw)*Math.Sin(tilt),Math.Cos(tilt));front=new SkyVector(Math.Sin(yaw)*Math.Cos(tilt),Math.Cos(yaw)*Math.Cos(tilt),Math.Sin(tilt));
   Pen horizon=Stroke(line,0.85,1),grid=Stroke(line,0.22,0.7),rear=Stroke(line,0.10,0.7),below=Stroke(line,0.13,0.7),figures=Stroke(line,0.60,0.8),near=Stroke(line,0.9,1);
   using(var dc=group.Open()){
    Brush sky=surface;
    if(context.Daytime){var day=new RadialGradientBrush(Color.FromArgb(150,120,176,232),Color.FromArgb(90,70,130,205)){GradientOrigin=new Point(0.4,0.3)};day.Freeze();sky=day;}
    dc.DrawEllipse(surface,null,centre,radius,radius);dc.DrawEllipse(sky,Stroke(line,0.25,0.8),centre,radius,radius);
    // A Moon on the far hemisphere sits beneath every near-side line and marker.
    MoonDrawn=false;MoonBehindGlobe=false;MoonBounds=Rect.Empty;if(context.Moon!=null&&moon.Dot(front)<0)DrawMoon(dc,line,text,0.2);
    Ring(dc,0,horizon,rear);Ring(dc,45,grid,rear);Ring(dc,-45,grid,rear);
    foreach(double azimuth in new[]{0.0,90,180,270}){var previous=SkyVector.Horizontal(azimuth,-90);for(int alt=-85;alt<=90;alt+=5){var next=SkyVector.Horizontal(azimuth,alt);dc.DrawLine((previous.Dot(front)+next.Dot(front))/2>=0?grid:rear,Project(previous),Project(next));previous=next;}}
    var points=stars.Select(Project).ToArray();
    foreach(var figure in SkyFigures.Figures)foreach(var path in figure.Paths)for(int i=1;i<path.Length;i++){
     var a=stars[path[i-1]];var b=stars[path[i]];double depth=(a.Dot(front)+b.Dot(front))/2;
     Pen pen=depth<0?rear:context.HasHorizon&&(a.Z+b.Z)/2<0?below:context.HasPosition&&(a.Dot(target)+b.Dot(target))/2>0.82?near:figures;
     dc.DrawLine(pen,points[path[i-1]],points[path[i]]);
    }
    if(selectedFigure>=0){var outline=Stroke(accent,0.95,1.8);foreach(var path in SkyFigures.Figures[selectedFigure].Paths)for(int i=1;i<path.Length;i++)if(stars[path[i-1]].Dot(front)>=0&&stars[path[i]].Dot(front)>=0)dc.DrawLine(outline,points[path[i-1]],points[path[i]]);}
    Brush starBrush=Tint(text,context.Daytime?0.25:0.65),faintStar=Tint(line,context.Daytime?0.12:0.25);for(int i=0;i<stars.Length;i++)if(stars[i].Dot(front)>0)dc.DrawEllipse(context.HasHorizon&&stars[i].Z<0?faintStar:starBrush,null,points[i],1.05,1.05);
    // The Sun sits beneath a near-side Moon, so an eclipse reads correctly on the globe.
    SunDrawn=false;SunBounds=Rect.Empty;if(context.Moon!=null&&(context.Daytime||context.Body=="Sun")&&sun.Dot(front)>=0)DrawSun(dc,context.HasHorizon&&sun.Z<0?0.45:1);
    // A near-side Moon covers stars and figure lines but never the target marker.
    if(context.Moon!=null&&moon.Dot(front)>=0&&!(context.Eclipse!=null&&context.Eclipse.Solar))DrawMoon(dc,line,text,context.HasHorizon&&moon.Z<0?0.45:1);
    TargetMarkerDrawn=false;TargetBounds=Rect.Empty;
    if(context.HasPosition&&target.Dot(front)>=0&&!(context.Eclipse!=null&&context.Eclipse.Solar)){
     TargetMarkerDrawn=true;
     var point=Project(target);TargetBounds=new Rect(point.X-10,point.Y-6,20,12);dc.DrawEllipse(Tint(accent,0.12),Stroke(accent,1,1.5),point,6,6);dc.DrawEllipse(accent,null,point,2,2);
     dc.DrawLine(Stroke(accent,0.8,1),new Point(point.X-10,point.Y),new Point(point.X-7,point.Y));dc.DrawLine(Stroke(accent,0.8,1),new Point(point.X+7,point.Y),new Point(point.X+10,point.Y));
    }
    DrawCardinals(dc,text);
    ConstellationLabelBounds=Rect.Empty;if(selectedFigure>=0)DrawConstellationName(dc,text,accent,surface,points);
    if(context.Eclipse!=null&&!context.Eclipse.Solar)DrawEclipse(dc,line,text);
   }group.Freeze();return group;
  }
 }
}
