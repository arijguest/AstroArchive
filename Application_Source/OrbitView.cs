using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroArchive {
 public sealed class OrbitCamera {
  public double Yaw=35,Tilt=55,Zoom=1,PanX,PanY;public bool Plane,Inner;
  public OrbitCamera Copy(){return (OrbitCamera)MemberwiseClone();}
 }
 // Cached physical 3D coordinates, depth-sorted native WPF drawing. No web/code
 // downloads, timers or network work during gestures.
 public sealed class OrbitView:FrameworkElement {
  static DependencyProperty BrushProperty(string name){return DependencyProperty.Register(name+"Brush",typeof(Brush),typeof(OrbitView),new FrameworkPropertyMetadata(Brushes.Gray,FrameworkPropertyMetadataOptions.AffectsRender));}
  static readonly Dictionary<string,DependencyProperty> palette=new Dictionary<string,DependencyProperty>{{"SurfaceAlt",BrushProperty("SurfaceAlt")},{"Surface",BrushProperty("Surface")},{"Text",BrushProperty("Text")},{"Muted",BrushProperty("Muted")},{"Accent",BrushProperty("Accent")},{"Border",BrushProperty("Border")}};
  static readonly DependencyProperty CaptionSizeProperty=DependencyProperty.Register("CaptionSize",typeof(double),typeof(OrbitView),new FrameworkPropertyMetadata(11.0,FrameworkPropertyMetadataOptions.AffectsRender));
  public OrbitCamera Camera=new OrbitCamera();public OrbitScene Scene {get;private set;}
  public OrbitPoint? Marker;public List<KeyValuePair<string,OrbitPoint>> Bodies;Point last;bool dragging,panning;public Action Changed;
  public OrbitView(){
   foreach(var colour in palette)SetResourceReference(colour.Value,colour.Key);SetResourceReference(CaptionSizeProperty,"UiFontCaption");
   Cursor=Cursors.Hand;ToolTip=Localization.T("Drag or use arrows to rotate. Shift-drag to pan. Focus the orbit or hold Ctrl to zoom with the wheel. Pinch to zoom.");Focusable=true;IsManipulationEnabled=true;ClipToBounds=true;MinHeight=160;
   SetResourceReference(FocusVisualStyleProperty,SystemParameters.FocusVisualStyleKey);
   AutomationProperties.SetName(this,Localization.T("3D orbit"));AutomationProperties.SetHelpText(this,Localization.T("Drag or use arrows to rotate. Shift-drag to pan. Focus the orbit or hold Ctrl to zoom with the wheel. Pinch to zoom."));
   MouseLeftButtonDown+=(s,e)=>{Focus();last=e.GetPosition(this);dragging=true;panning=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;CaptureMouse();e.Handled=true;};
   MouseMove+=(s,e)=>{if(!dragging)return;var point=e.GetPosition(this);double dx=point.X-last.X,dy=point.Y-last.Y;last=point;if(panning)Pan(dx,dy);else Rotate(dx*0.5,dy*0.5);e.Handled=true;};
   MouseLeftButtonUp+=(s,e)=>{dragging=false;ReleaseMouseCapture();e.Handled=true;};LostMouseCapture+=(s,e)=>dragging=false;
   PreviewMouseWheel+=(s,e)=>{if(!IsKeyboardFocusWithin&&(Keyboard.Modifiers&ModifierKeys.Control)==0)return;Zoom(Math.Exp(Math.Max(-1200,Math.Min(1200,e.Delta))/600.0),e.GetPosition(this));e.Handled=true;};
   KeyDown+=(s,e)=>{bool handled=true;switch(e.Key){case Key.Left:Rotate(-5,0);break;case Key.Right:Rotate(5,0);break;case Key.Up:Rotate(0,5);break;case Key.Down:Rotate(0,-5);break;case Key.Add:case Key.OemPlus:Zoom(1.25);break;case Key.Subtract:case Key.OemMinus:Zoom(0.8);break;case Key.Home:Reset();break;case Key.Escape:MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));break;default:handled=false;break;}e.Handled=handled;};
   ManipulationStarting+=(s,e)=>{dragging=false;ReleaseMouseCapture();e.Mode=ManipulationModes.Translate|ManipulationModes.Scale;e.ManipulationContainer=this;e.Handled=true;};
   ManipulationDelta+=(s,e)=>{Focus();double scale=e.DeltaManipulation.Scale.X;if(OrbitalModel.Finite(scale)&&scale>0)Zoom(scale,e.ManipulationOrigin);if(TouchesCapturedWithin.Concat(TouchesOver).Distinct().Count()<2)Rotate(e.DeltaManipulation.Translation.X*0.5,e.DeltaManipulation.Translation.Y*0.5);else Pan(e.DeltaManipulation.Translation.X,e.DeltaManipulation.Translation.Y);e.Handled=true;};
  }
  public void SetScene(OrbitScene scene){Scene=scene;InvalidateVisual();}
  void Update(){InvalidateVisual();if(Changed!=null)Changed();}
  public void Reset(){Camera=new OrbitCamera();Update();}
  public void Rotate(double yaw,double tilt){Camera.Plane=false;Camera.Yaw=(Camera.Yaw+yaw)%360;Camera.Tilt=Math.Max(-90,Math.Min(90,Camera.Tilt+tilt));Update();}
  public void Pan(double x,double y){if(!OrbitalModel.Finite(x)||!OrbitalModel.Finite(y))return;Camera.PanX=Math.Max(-ActualWidth*2,Math.Min(ActualWidth*2,Camera.PanX+x));Camera.PanY=Math.Max(-ActualHeight*2,Math.Min(ActualHeight*2,Camera.PanY+y));Update();}
  public void Zoom(double factor){Zoom(factor,new Point(ActualWidth/2,ActualHeight/2));}
  public void Zoom(double factor,Point anchor){if(!OrbitalModel.Finite(factor)||factor<=0)return;double next=Math.Max(0.1,Math.Min(1e10,Camera.Zoom*factor)),ratio=next/Camera.Zoom;Camera.Zoom=next;
   double x=anchor.X-ActualWidth/2,y=anchor.Y-ActualHeight/2;Camera.PanX=(Camera.PanX-x)*ratio+x;Camera.PanY=(Camera.PanY-y)*ratio+y;Pan(0,0);}
  public void Fit(bool inner){Camera.Inner=inner;Camera.Zoom=1;Camera.PanX=Camera.PanY=0;Update();}
  public void Top(){Camera.Plane=false;Camera.Tilt=90;Camera.Yaw=0;Update();}
  public void Plane(){Camera.Plane=true;Update();}
  Brush Colour(string key){return GetValue(palette[key]) as Brush??Brushes.Gray;}
  double Extent {get{return Scene==null?1:Math.Max(1e-9,(Camera.Inner?Scene.Inner:Scene.Full).Extent);}}
  OrbitPoint View(OrbitPoint p){
   if(Camera.Plane&&Scene!=null){var o=Scene.Record;var a=OrbitalModel.Orient(o,1,0);var b=OrbitalModel.Orient(o,0,1);var n=new OrbitPoint(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);return new OrbitPoint(Dot(p,a),Dot(p,b),Dot(p,n));}
   double yaw=Camera.Yaw*OrbitalModel.R,tilt=Camera.Tilt*OrbitalModel.R;double x=p.X*Math.Cos(yaw)-p.Y*Math.Sin(yaw),y=p.X*Math.Sin(yaw)+p.Y*Math.Cos(yaw);return new OrbitPoint(x,y*Math.Sin(tilt)+p.Z*Math.Cos(tilt),-y*Math.Cos(tilt)+p.Z*Math.Sin(tilt));
  }
  static double Dot(OrbitPoint a,OrbitPoint b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
  Point Project(OrbitPoint p){double scale=Math.Max(1,Math.Min(ActualWidth,ActualHeight)-64)*0.46/Extent*Camera.Zoom;return new Point(ActualWidth/2+Camera.PanX+p.X*scale,ActualHeight/2+Camera.PanY-p.Y*scale);}
  sealed class Segment {public Point A,B;public double Depth;public bool Target;}
  static bool VisibleLine(ref Point a,ref Point b,Rect bounds){
   // Liang–Barsky clipping avoids huge WPF coordinates at extreme orbital extents.
   double dx=b.X-a.X,dy=b.Y-a.Y,t0=0,t1=1;double[] p={-dx,dx,-dy,dy},q={a.X-bounds.Left,bounds.Right-a.X,a.Y-bounds.Top,bounds.Bottom-a.Y};
   for(int k=0;k<4;k++){if(Math.Abs(p[k])<1e-20){if(q[k]<0)return false;}else{double t=q[k]/p[k];if(p[k]<0){if(t>t1)return false;t0=Math.Max(t0,t);}else{if(t<t0)return false;t1=Math.Min(t1,t);}}}
   var origin=a;a=new Point(origin.X+t0*dx,origin.Y+t0*dy);b=new Point(origin.X+t1*dx,origin.Y+t1*dy);return true;
  }
  // The same visual language as the sky globe: surface disc, hairline grid at low opacity,
  // faint star dots, a soft sun glow, ringed target marker and rounded name pills.
  static readonly Color SunLight=Color.FromRgb(255,206,84);
  static readonly double[] StarField=MakeStars();
  static double[] MakeStars(){var random=new Random(20260);var stars=new double[90*3];for(int i=0;i<90;i++){stars[i*3]=random.NextDouble();stars[i*3+1]=random.NextDouble();stars[i*3+2]=0.35+0.65*random.NextDouble();}return stars;}
  static Brush Tint(Brush brush,double opacity){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();return copy;}
  static Pen Stroke(Brush brush,double opacity,double width){var pen=new Pen(Tint(brush,opacity),width);pen.Freeze();return pen;}
  // 1, 2 or 5 times a power of ten: round distances for grid rings.
  static double NiceStep(double raw){if(!OrbitalModel.Finite(raw)||raw<=0)return 1;double power=Math.Pow(10,Math.Floor(Math.Log10(raw))),m=raw/power;return power*(m<1.5?1:m<3.5?2:m<7.5?5:10);}
  static string Distance(double au){return au<0.01?(au*149597870.7).ToString(au*149597870.7<10?"0.#":"0",CultureInfo.InvariantCulture)+" km":au.ToString(au<1?"0.###":au<100?"0.##":"0",CultureInfo.InvariantCulture)+" AU";}
  double ScalePixels {get{return Math.Max(1,Math.Min(ActualWidth,ActualHeight)-64)*0.46*Camera.Zoom;}}
  void Stars(DrawingContext dc,Rect bounds,Brush text){
   // Distant stars drift slowly as the camera turns, which reads as depth without implying a scale.
   double ox=Camera.Plane?0:Camera.Yaw/360*bounds.Width*1.5,oy=Camera.Plane?0:Camera.Tilt/180*bounds.Height*0.5;var bright=Tint(text,0.5);var faint=Tint(text,0.22);
   for(int i=0;i<StarField.Length/3;i++){double x=(((StarField[i*3]*bounds.Width+ox)%bounds.Width)+bounds.Width)%bounds.Width,y=(((StarField[i*3+1]*bounds.Height+oy)%bounds.Height)+bounds.Height)%bounds.Height;double size=StarField[i*3+2];dc.DrawEllipse(size>0.8?bright:faint,null,new Point(x,y),0.5+size*0.75,0.5+size*0.75);}
  }
  List<Rect> placed;
  Rect Pill(DrawingContext dc,string text,Point at,bool accent,bool force=false){
   double size=Math.Min(20,(double)GetValue(CaptionSizeProperty));var format=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),size,Colour(accent?"Text":"Muted"),VisualTreeHelper.GetDpi(this).PixelsPerDip);
   format.MaxTextWidth=Math.Max(40,ActualWidth-28);format.MaxLineCount=1;format.Trimming=TextTrimming.CharacterEllipsis;
   double width=Math.Min(format.Width,format.MaxTextWidth)+14,height=format.Height+4;var box=new Rect(Math.Max(3,Math.Min(ActualWidth-width-3,at.X)),Math.Max(3,Math.Min(ActualHeight-height-3,at.Y)),width,height);
   if(!force&&placed.Any(r=>r.IntersectsWith(box)))return Rect.Empty;placed.Add(box);
   dc.DrawRoundedRectangle(Tint(Colour("Surface"),0.92),Stroke(Colour(accent?"Accent":"Border"),accent?0.9:0.7,1),box,height/2,height/2);dc.DrawText(format,new Point(box.X+7,box.Y+2));return box;
  }
  // Distance rings and 30° spokes lie in the reference (ecliptic) plane, so they tilt with the orbit.
  void Grid(DrawingContext dc,Rect bounds,double perPixel){
   double reach=Math.Sqrt(Math.Pow(Math.Abs(Camera.PanX)+ActualWidth/2,2)+Math.Pow(Math.Abs(Camera.PanY)+ActualHeight/2,2))*perPixel;if(!OrbitalModel.Finite(reach)||reach<=0)return;
   double step=NiceStep(reach/3.5);int rings=(int)Math.Max(1,Math.Min(20,Math.Ceiling(reach/step)));var near=Stroke(Colour("Muted"),0.30,0.8);var rear=Stroke(Colour("Muted"),0.11,0.8);var spoke=Stroke(Colour("Muted"),0.14,0.7);const int Sides=96;
   for(int k=1;k<=rings;k++){double r=k*step;var previous=Project(View(new OrbitPoint(r,0,0)));double previousDepth=View(new OrbitPoint(r,0,0)).Z;
    for(int i=1;i<=Sides;i++){double angle=i*2*Math.PI/Sides;var v=View(new OrbitPoint(r*Math.Cos(angle),r*Math.Sin(angle),0));var point=Project(v);var a=previous;var b=point;if(OrbitalModel.Finite(a.X)&&OrbitalModel.Finite(a.Y)&&OrbitalModel.Finite(b.X)&&OrbitalModel.Finite(b.Y)&&VisibleLine(ref a,ref b,bounds))dc.DrawLine((previousDepth+v.Z)/2<0?rear:near,a,b);previous=point;previousDepth=v.Z;}}
   double outer=rings*step;for(int i=0;i<12;i++){double angle=i*Math.PI/6;var a=Project(View(new OrbitPoint()));var b=Project(View(new OrbitPoint(outer*Math.Cos(angle),outer*Math.Sin(angle),0)));if(OrbitalModel.Finite(a.X)&&OrbitalModel.Finite(a.Y)&&OrbitalModel.Finite(b.X)&&OrbitalModel.Finite(b.Y)&&VisibleLine(ref a,ref b,bounds))dc.DrawLine(spoke,a,b);}
   // Ring distances sit on the near side of each ring, thinned out so they never collide.
   int every=(int)Math.Ceiling(rings/6.0);for(int k=every;k<=rings;k+=every){var at=Project(View(new OrbitPoint(k*step*Math.Cos(-0.75),k*step*Math.Sin(-0.75),0)));if(OrbitalModel.Finite(at.X)&&OrbitalModel.Finite(at.Y)&&bounds.Contains(at))Pill(dc,Distance(k*step),new Point(at.X+3,at.Y-9),false);}
  }
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);if(ActualWidth<20||ActualHeight<20)return;var bounds=new Rect(RenderSize);placed=new List<Rect>();
   Brush surface=Colour("Surface"),text=Colour("Text"),muted=Colour("Muted"),accent=Colour("Accent");
   dc.DrawRoundedRectangle(Colour("SurfaceAlt"),new Pen(Colour(IsKeyboardFocused?"Accent":"Border"),IsKeyboardFocused?1.5:1),bounds,8,8);if(Scene==null)return;
   dc.PushClip(new RectangleGeometry(bounds,8,8));Stars(dc,bounds,text);
   var halo=new RadialGradientBrush(Color.FromArgb(34,SunLight.R,SunLight.G,SunLight.B),Color.FromArgb(0,SunLight.R,SunLight.G,SunLight.B));halo.Freeze();var middle=Project(new OrbitPoint());if(Scene.Record.central_body=="Sun"&&OrbitalModel.Finite(middle.X)&&OrbitalModel.Finite(middle.Y))dc.DrawEllipse(halo,null,middle,Math.Min(bounds.Width,bounds.Height)*0.45,Math.Min(bounds.Width,bounds.Height)*0.45);
   var segments=new List<Segment>();var target=Camera.Inner?Scene.Inner:Scene.Full;bool clipped=target.Clipped;double perPixel=Extent/ScalePixels;
   Grid(dc,bounds,perPixel);
   Action<OrbitPath,bool> add=(path,selected)=>{if(path==null)return;for(int k=1;k<path.Points.Length;k++){var a=View(path.Points[k-1]);var b=View(path.Points[k]);var pa=Project(a);var pb=Project(b);if(!OrbitalModel.Finite(pa.X)||!OrbitalModel.Finite(pa.Y)||!OrbitalModel.Finite(pb.X)||!OrbitalModel.Finite(pb.Y))continue;bool inside=bounds.Contains(pa)&&bounds.Contains(pb);if(selected&&!inside)clipped=true;if(VisibleLine(ref pa,ref pb,bounds))segments.Add(new Segment{A=pa,B=pb,Depth=(a.Z+b.Z)/2,Target=selected});}};
   foreach(var context in Scene.Context)add(context,false);add(target,true);
   // Subdued planet orbits first, then the target with a soft glow; far-side arcs are dimmer and thinner.
   var contextPen=Stroke(muted,0.34,1);var contextRear=Stroke(muted,0.18,1);var glow=Stroke(accent,0.14,6);var front=Stroke(accent,1,2.2);var back=Stroke(accent,0.55,1.6);
   var ordered=segments.OrderBy(s=>s.Depth).ToList();foreach(var segment in ordered)if(segment.Target&&segment.Depth>=0)dc.DrawLine(glow,segment.A,segment.B);
   foreach(var segment in ordered)dc.DrawLine(segment.Target?(segment.Depth<0?back:front):(segment.Depth<0?contextRear:contextPen),segment.A,segment.B);
   foreach(var context in Scene.Context)ContextLabel(dc,context,bounds);
   // Nodes: where the path crosses the reference plane, filled when rising and hollow when falling.
   var pathPoints=target.Points;for(int k=1;k<=pathPoints.Length;k++){if(k==pathPoints.Length&&!target.Closed)break;var a=pathPoints[k-1];var b=pathPoints[k%pathPoints.Length];if(a.Z==0||b.Z==0||(a.Z<0)==(b.Z<0))continue;double t=a.Z/(a.Z-b.Z);var node=Project(View(new OrbitPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,0)));if(OrbitalModel.Finite(node.X)&&OrbitalModel.Finite(node.Y)&&bounds.Contains(node)){bool rising=b.Z>a.Z;var geometry=new StreamGeometry();using(var context=geometry.Open()){context.BeginFigure(new Point(node.X,node.Y-3.5),true,true);context.LineTo(new Point(node.X+3.5,node.Y),true,false);context.LineTo(new Point(node.X,node.Y+3.5),true,false);context.LineTo(new Point(node.X-3.5,node.Y),true,false);}geometry.Freeze();dc.DrawGeometry(rising?Tint(accent,0.9):Tint(surface,1),Stroke(accent,0.9,1.2),geometry);}}
   if(Scene.Record.central_body=="Sun"){if(OrbitalModel.Finite(middle.X)&&OrbitalModel.Finite(middle.Y)&&bounds.Contains(middle)){var sunGlow=new RadialGradientBrush(Color.FromArgb(120,255,214,110),Color.FromArgb(0,255,214,110));sunGlow.Freeze();dc.DrawEllipse(sunGlow,null,middle,16,16);var disc=new SolidColorBrush(SunLight);disc.Freeze();dc.DrawEllipse(disc,null,middle,5,5);Pill(dc,Localization.T(Scene.Record.central_body),new Point(middle.X+10,middle.Y-24),false);}}
   else if(bounds.Contains(middle)){dc.DrawEllipse(Tint(accent,0.12),Stroke(accent,1,1.5),middle,6,6);dc.DrawEllipse(accent,null,middle,3,3);Pill(dc,Localization.T(Scene.Record.central_body),new Point(middle.X+10,middle.Y-24),false);}
   var peri=Project(View(OrbitalModel.AtAnomaly(Scene.Record,0)));if(OrbitalModel.Finite(peri.X)&&OrbitalModel.Finite(peri.Y)&&bounds.Contains(peri)){dc.DrawEllipse(surface,Stroke(accent,1,1.5),peri,3,3);Pill(dc,Localization.T(Scene.Record.central_body=="Sun"?"Perihelion":"Perigee"),new Point(peri.X+8,peri.Y+5),false);}
   // Planets on the marker date; hidden when the date is only the orbit epoch.
   if(Bodies!=null)foreach(var body in Bodies){var at=Project(View(body.Value));if(OrbitalModel.Finite(at.X)&&OrbitalModel.Finite(at.Y)&&bounds.Contains(at)){dc.DrawEllipse(Tint(text,0.85),null,at,2.5,2.5);Pill(dc,Localization.T(body.Key),new Point(at.X+6,at.Y-18),false);}}
   if(Marker.HasValue){
    var marker=Project(View(Marker.Value));var footprint=Project(View(new OrbitPoint(Marker.Value.X,Marker.Value.Y,0)));
    if(OrbitalModel.Finite(marker.X)&&OrbitalModel.Finite(marker.Y)&&OrbitalModel.Finite(footprint.X)&&OrbitalModel.Finite(footprint.Y)){
     // A dashed drop line to the plane shows height above or below the reference plane.
     var drop=new Pen(Tint(accent,0.55),1){DashStyle=new DashStyle(new double[]{3,3},0)};var a=marker;var b=footprint;if(VisibleLine(ref a,ref b,bounds))dc.DrawLine(drop,a,b);if(bounds.Contains(footprint))dc.DrawEllipse(null,Stroke(accent,0.55,1),footprint,3.5,1.5);
     if(bounds.Contains(marker)){dc.DrawEllipse(Tint(accent,0.12),Stroke(accent,1,1.5),marker,7,7);dc.DrawEllipse(accent,null,marker,2.5,2.5);var tick=Stroke(accent,0.8,1);dc.DrawLine(tick,new Point(marker.X-11,marker.Y),new Point(marker.X-8,marker.Y));dc.DrawLine(tick,new Point(marker.X+8,marker.Y),new Point(marker.X+11,marker.Y));Pill(dc,Catalog.DisplayId(Scene.Record.object_id),new Point(marker.X+10,marker.Y-24),true,true);}
    }
   }
   // Arrow follows increasing true anomaly, including retrograde orbital planes.
   int direction=target.Points.Length/2+Math.Max(1,target.Points.Length/12);if(direction<target.Points.Length-1)Arrow(dc,Project(View(target.Points[direction])),Project(View(target.Points[direction+1])),accent,bounds);
   if(target.Clipped){Arrow(dc,Project(View(target.Points[1])),Project(View(target.Points[0])),accent,bounds);Arrow(dc,Project(View(target.Points[target.Points.Length-2])),Project(View(target.Points.Last())),accent,bounds);}
   double au=50*perPixel;var scalePen=new Pen(muted,1);dc.DrawLine(scalePen,new Point(12,ActualHeight-12),new Point(62,ActualHeight-12));dc.DrawLine(scalePen,new Point(12,ActualHeight-15),new Point(12,ActualHeight-9));dc.DrawLine(scalePen,new Point(62,ActualHeight-15),new Point(62,ActualHeight-9));Label(dc,Distance(au),new Point(12,ActualHeight-32));
   // Axis triad: the reference-plane axes, with Z (towards the pole) in the accent colour.
   var axes=new[]{new OrbitPoint(1,0,0),new OrbitPoint(0,1,0),new OrbitPoint(0,0,1)};var origin=new Point(ActualWidth-34,36);dc.DrawEllipse(Tint(surface,0.85),Stroke(muted,0.35,1),origin,25,25);
   for(int axis=0;axis<3;axis++){var v=View(axes[axis]);var end=new Point(origin.X+v.X*18,origin.Y-v.Y*18);dc.DrawLine(new Pen(Colour(axis==2?"Accent":"Muted"),axis==2?1.6:1.1),origin,end);Label(dc,new[]{"X","Y","Z"}[axis],new Point(end.X+2,end.Y-7));}
   if(clipped)Pill(dc,Localization.T(Camera.Inner&&Scene.Record.central_body=="Sun"&&Scene.Record.q>40?"Perihelion beyond the inner solar system":"Extent clipped"),new Point(10,8),false,true);dc.Pop();
  }
  // Names a planet orbit once, on the clear side of the Sun, when there is room.
  void ContextLabel(DrawingContext dc,OrbitPath path,Rect bounds){
   if(path==null||String.IsNullOrEmpty(path.Name)||path.Points.Length<8)return;
   Point best=new Point();bool found=false;for(int k=0;k<path.Points.Length;k+=Math.Max(1,path.Points.Length/48)){var at=Project(View(path.Points[k]));if(!OrbitalModel.Finite(at.X)||!OrbitalModel.Finite(at.Y)||!bounds.Contains(at))continue;if(!found||at.X>best.X){best=at;found=true;}}
   if(found)Pill(dc,Localization.T(path.Name),new Point(best.X+4,best.Y-9),false);
  }
  static void Arrow(DrawingContext dc,Point a,Point b,Brush brush,Rect bounds){if(!bounds.Contains(b))return;double x=b.X-a.X,y=b.Y-a.Y,length=Math.Sqrt(x*x+y*y);if(length<1e-8)return;x/=length;y/=length;var pen=new Pen(brush,1.5);dc.DrawLine(pen,b,new Point(b.X-x*7-y*4,b.Y-y*7+x*4));dc.DrawLine(pen,b,new Point(b.X-x*7+y*4,b.Y-y*7-x*4));}
  void Label(DrawingContext dc,string text,Point at){double size=(double)GetValue(CaptionSizeProperty);var formatted=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),Math.Min(20,size),Colour("Muted"),VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(formatted,new Point(Math.Max(4,Math.Min(ActualWidth-formatted.Width-4,at.X)),Math.Max(3,Math.Min(ActualHeight-formatted.Height-3,at.Y))));}
 }
 public sealed class OrbitPanel:StackPanel {
  public readonly OrbitView View=new OrbitView{Height=280};readonly TextBlock caption,status;readonly WrapPanel controls=new WrapPanel();CancellationTokenSource work;int generation;OrbitRecord record;bool overview;public DateTime? MarkerUtc;readonly Button today;readonly TextBox date;readonly WrapPanel dates;
  public OrbitPanel(){
   Children.Add(View);Children.Add(controls);caption=Text("Approximate orbit · linear distances; body markers enlarged.");Children.Add(caption);status=Text("");Children.Add(status);
   Button("Reset orbit",()=>View.Reset());Button("−",()=>View.Zoom(0.8),"Zoom out");Button("+",()=>View.Zoom(1.25),"Zoom in");Button("Top view",()=>View.Top());Button("Orbit plane",()=>View.Plane());Button("Fit orbit",()=>View.Fit(false));Button("Inner solar system",()=>View.Fit(true));
   Button expansion=null;expansion=Button("Expand orbit",()=>{View.Height=View.Height==280?440:280;expansion.Content=Localization.T(View.Height==280?"Expand orbit":"Collapse orbit");AutomationProperties.SetName(expansion,Convert.ToString(expansion.Content));});Button("Orbit epoch",()=>SetMarker(null));today=Button("Today",()=>SetMarker(DateTime.UtcNow));
   dates=new WrapPanel();date=new TextBox{Width=110,Text=DateTime.UtcNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),Margin=new Thickness(0,4,6,0),Padding=new Thickness(7,3,7,3),MinHeight=28};date.SetResourceReference(Control.BackgroundProperty,"Surface");date.SetResourceReference(Control.ForegroundProperty,"Text");date.SetResourceReference(Control.BorderBrushProperty,"Border");AutomationProperties.SetName(date,Localization.T("Marker date (UTC)"));date.ToolTip=Localization.T("UTC date (YYYY-MM-DD). Press Enter or Set UTC date.");date.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){ApplyDate();e.Handled=true;}};dates.Children.Add(date);
   var applyDate=new Button{Content="Set UTC date",Margin=new Thickness(0,4,6,0),Padding=new Thickness(7,3,7,3),MinHeight=28};applyDate.Click+=(s,e)=>ApplyDate();dates.Children.Add(applyDate);controls.Children.Add(dates);
   Children.Add(Text("Drag to rotate · Shift-drag to pan · Focus/Ctrl + wheel or pinch to zoom."));
   Unloaded+=(s,e)=>Cancel();Loaded+=(s,e)=>{if(record!=null&&View.Scene==null)LoadScene();};
  }
  static TextBlock Text(string value){var text=new TextBlock{Text=value,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,2)};text.SetResourceReference(TextBlock.ForegroundProperty,"Muted");text.SetResourceReference(TextBlock.FontSizeProperty,"UiFontCaption");return text;}
  Button Button(string label,Action action,string accessible=null){var button=new Button{Content=label,Margin=new Thickness(0,4,6,0),Padding=new Thickness(7,3,7,3),MinHeight=28};AutomationProperties.SetName(button,Localization.T(accessible??label));button.Click+=(s,e)=>action();controls.Children.Add(button);return button;}
  public void SetCard(TargetInfoCard card){overview=card.id=="SUN";record=overview?PlanetEphemeris.Orbit("EARTH",new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)):OrbitalCatalog.For(card);LoadScene();}
  async void LoadScene(){Cancel();int current=++generation;
   if(record==null){View.SetScene(null);controls.Visibility=Visibility.Collapsed;caption.Text=Localization.T("Complete orbital elements are unavailable. No orientation has been assumed.");status.Text="";return;}
   bool supportsDate=record.valid_from.HasValue&&record.valid_to.HasValue;today.Visibility=dates.Visibility=supportsDate?Visibility.Visible:Visibility.Collapsed;today.IsEnabled=supportsDate&&OrbitalModel.Dynamical(DateTime.UtcNow).HasValue;controls.Visibility=Visibility.Visible;status.Text=Localization.T("Preparing 3D orbit…");work=new CancellationTokenSource();var token=work.Token;
   try{var value=await Task.Run(()=>OrbitalCatalog.Scene(record,token),token);if(current!=generation||token.IsCancellationRequested)return;if(overview){value.Full.Extent=35;value.Inner.Extent=6;}Action apply=()=>{if(current!=generation||token.IsCancellationRequested)return;View.SetScene(value);SetMarker(MarkerUtc);};if(Dispatcher.CheckAccess())apply();else await Dispatcher.InvokeAsync(apply);}catch(OperationCanceledException){}catch(Exception){Action failed=()=>{if(current==generation)status.Text=Localization.T("The orbit could not be drawn.");};if(Dispatcher.CheckAccess())failed();else QueueFailure(failed);}
  }
  void QueueFailure(Action action){Dispatcher.BeginInvoke(action);}
  public void Cancel(){generation++;if(work!=null){work.Cancel();work.Dispose();work=null;}}
  void ApplyDate(){DateTime value;if(!DateTime.TryParseExact(date.Text.Trim(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out value)||value.Year<1972||value.Year>2026){status.Text=Localization.T("Enter a valid UTC date (YYYY-MM-DD) between 1972 and 2026.");date.Focus();return;}SetMarker(value);}
  // Other planets on the marker date, from the same approximation the planet pages use.
  List<KeyValuePair<string,OrbitPoint>> PlanetPositions(DateTime utc,double day){
   var bodies=new List<KeyValuePair<string,OrbitPoint>>();
   foreach(string id in OrbitalCatalog.Planets){if(id==record.object_id)continue;try{var planet=PlanetEphemeris.Orbit(id,utc);OrbitPoint point;if(planet!=null&&OrbitalModel.Position(planet,day,out point))bodies.Add(new KeyValuePair<string,OrbitPoint>(OrbitalCatalog.PlanetName(id),point));}catch(Exception){}}
   return bodies;
  }
  void SetMarker(DateTime? utc){
   MarkerUtc=utc;if(record==null)return;OrbitPoint point=new OrbitPoint();bool available=false;string time;
   if(utc.HasValue){double? day=OrbitalModel.Dynamical(utc.Value);var planet=record.rights.dataset=="planetary-approximation"?PlanetEphemeris.Orbit(record.object_id,utc.Value):null;
    available=day.HasValue&&OrbitalModel.Position(planet??record,day.Value,out point);View.Marker=available?(OrbitPoint?)point:null;View.Bodies=available&&record.central_body=="Sun"?PlanetPositions(utc.Value,day.Value):null;time=Localization.F("Marker: {0} UTC",utc.Value.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture));
   }else{available=OrbitalModel.Position(record,record.epoch,out point,true);View.Marker=available?(OrbitPoint?)point:null;View.Bodies=null;time=Localization.F("Marker at orbit epoch: JD {0} {1}",record.epoch.ToString("0.#####",CultureInfo.InvariantCulture),record.time_standard);}
   if(!available)time=Localization.T("Dated position unavailable for this solution. Use Orbit epoch.");
   caption.Text=Localization.T("Approximate orbit · linear distances; body markers enlarged.");status.Text=(overview?Localization.T("Solar-system overview")+" · ":"")+time+"\n"+Localization.F("Source epoch: JD {0} {1} · {2}",record.epoch.ToString("0.#####",CultureInfo.InvariantCulture),record.time_standard,record.solution_id)+"\n"+Localization.T("J2000 ecliptic · two-body geometry, not a precision ephemeris. UTC → TT; TDB differs by less than 2 ms.");if(!record.valid_from.HasValue||!record.valid_to.HasValue)status.Text+="\n"+Localization.T("Snapshot positions are shown only at the orbit epoch.");if(record.object_id=="EARTH")status.Text+="\n"+Localization.T("Earth uses the Earth–Moon barycentre approximation.");View.InvalidateVisual();
  }
 }
}
