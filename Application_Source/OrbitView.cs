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
 // Another planet at the marker date, drawn as a small dot with its astronomical symbol.
 public struct OrbitMarker {public string Glyph;public OrbitPoint Point;}
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
  public OrbitPoint? Marker;public List<OrbitMarker> ContextMarkers=new List<OrbitMarker>();Point last;bool dragging,panning;public Action Changed;
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
  double Scale {get{return Math.Max(1,Math.Min(ActualWidth,ActualHeight)-64)*0.46/Extent*Camera.Zoom;}}
  Point Project(OrbitPoint p){double scale=Scale;return new Point(ActualWidth/2+Camera.PanX+p.X*scale,ActualHeight/2+Camera.PanY-p.Y*scale);}
  sealed class Segment {public Point A,B;public double Depth;public bool Target,Below;}
  static bool VisibleLine(ref Point a,ref Point b,Rect bounds){
   // Liang–Barsky clipping avoids huge WPF coordinates at extreme orbital extents.
   double dx=b.X-a.X,dy=b.Y-a.Y,t0=0,t1=1;double[] p={-dx,dx,-dy,dy},q={a.X-bounds.Left,bounds.Right-a.X,a.Y-bounds.Top,bounds.Bottom-a.Y};
   for(int k=0;k<4;k++){if(Math.Abs(p[k])<1e-20){if(q[k]<0)return false;}else{double t=q[k]/p[k];if(p[k]<0){if(t>t1)return false;t0=Math.Max(t0,t);}else{if(t<t0)return false;t1=Math.Min(t1,t);}}}
   var origin=a;a=new Point(origin.X+t0*dx,origin.Y+t0*dy);b=new Point(origin.X+t1*dx,origin.Y+t1*dy);return true;
  }
  static readonly double[] starSeed=StarSeed();
  static double[] StarSeed(){var values=new double[3*72];uint state=2463534242;for(int i=0;i<values.Length;i++){state^=state<<13;state^=state>>17;state^=state<<5;values[i]=state/4294967296.0;}return values;}
  static readonly string[] planetGlyphs={"MERCURY","☿","VENUS","♀","EARTH","♁","MARS","♂","JUPITER","♃","SATURN","♄","URANUS","♅","NEPTUNE","♆"};
  public static string PlanetGlyph(string id){for(int i=0;i<planetGlyphs.Length;i+=2)if(planetGlyphs[i]==id)return planetGlyphs[i+1];return null;}
  Color Hue(string key){var brush=Colour(key) as SolidColorBrush;return brush==null?Colors.Gray:brush.Color;}
  Brush Faded(string key,double opacity){var copy=Colour(key).CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();return copy;}
  Pen Stroke(string key,double opacity,double width,DashStyle dashes=null){var pen=new Pen(Faded(key,opacity),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};if(dashes!=null)pen.DashStyle=dashes;pen.Freeze();return pen;}
  // A curve in the ecliptic frame, clipped to the pane so extreme zoom never produces runaway coordinates.
  void Curve(DrawingContext dc,Func<double,OrbitPoint> at,int steps,Pen pen,Rect bounds){
   Point previous=new Point(double.NaN,double.NaN);for(int k=0;k<=steps;k++){var next=Project(View(at(2*Math.PI*k/steps)));if(k>0&&OrbitalModel.Finite(previous.X)&&OrbitalModel.Finite(previous.Y)&&OrbitalModel.Finite(next.X)&&OrbitalModel.Finite(next.Y)){Point a=previous,b=next;if(VisibleLine(ref a,ref b,bounds))dc.DrawLine(pen,a,b);}previous=next;}
  }
  static string Distance(double au){return au<0.01?(au*149597870.7).ToString("N0",CultureInfo.InvariantCulture)+" km":au.ToString(au<1?"0.###":"0.##",CultureInfo.InvariantCulture)+" AU";}
  // Equatorial-plane reference: a faint disc, distance rings and spokes in the ecliptic, like the sky map's grid.
  void DrawEcliptic(DrawingContext dc,Rect bounds){
   double scale=Scale,centreX=ActualWidth/2+Camera.PanX,centreY=ActualHeight/2+Camera.PanY;if(!OrbitalModel.Finite(scale)||scale<=0)return;
   double reach=Math.Max(Math.Max(Math.Sqrt(Math.Pow(centreX,2)+Math.Pow(centreY,2)),Math.Sqrt(Math.Pow(ActualWidth-centreX,2)+Math.Pow(centreY,2))),Math.Max(Math.Sqrt(Math.Pow(centreX,2)+Math.Pow(ActualHeight-centreY,2)),Math.Sqrt(Math.Pow(ActualWidth-centreX,2)+Math.Pow(ActualHeight-centreY,2))));
   double raw=76/scale,magnitude=Math.Pow(10,Math.Floor(Math.Log10(raw))),ratio=raw/magnitude,step=magnitude*(ratio<=1.5?1:ratio<=3.5?2:ratio<=7.5?5:10);
   while(reach/(step*scale)>14)step*=2;int rings=(int)Math.Ceiling(reach/(step*scale));if(rings<1)return;
   double outer=rings*step;var ring=Stroke("Muted",0.2,1);var strong=Stroke("Muted",0.4,1);var spoke=Stroke("Muted",0.12,1);
   // Disc of the outermost ring the pane can show.
   if(outer*scale<4000){var figure=new PathFigure{IsClosed=true,IsFilled=true};for(int k=0;k<96;k++){var point=Project(View(new OrbitPoint(outer*Math.Cos(2*Math.PI*k/96),outer*Math.Sin(2*Math.PI*k/96),0)));if(k==0)figure.StartPoint=point;else figure.Segments.Add(new LineSegment(point,true));}var geometry=new PathGeometry(new[]{figure});geometry.Freeze();dc.DrawGeometry(Faded("Accent",0.05),null,geometry);}
   for(int k=0;k<8;k++){double angle=k*Math.PI/4;var a=Project(View(new OrbitPoint()));var b=Project(View(new OrbitPoint(outer*Math.Cos(angle),outer*Math.Sin(angle),0)));if(VisibleLine(ref a,ref b,bounds))dc.DrawLine(spoke,a,b);}
   for(int n=1;n<=rings;n++){double radius=n*step;bool earthDistance=Math.Abs(radius-1)<1e-9&&Scene!=null&&Scene.Record.central_body=="Sun";Curve(dc,t=>new OrbitPoint(radius*Math.Cos(t),radius*Math.Sin(t),0),Math.Min(160,Math.Max(48,(int)(radius*scale/3))),earthDistance?strong:ring,bounds);
    var tag=Project(View(new OrbitPoint(radius*Math.Cos(-Math.PI/4),radius*Math.Sin(-Math.PI/4),0)));if(bounds.Contains(tag)&&(n==1||n%2==0||rings<6))Label(dc,Distance(radius),new Point(tag.X+3,tag.Y+1),Faded("Muted",0.65));}
   // Vernal equinox direction (+X), the zero of ecliptic longitude.
   var equinox=Project(View(new OrbitPoint(outer,0,0)));if(bounds.Contains(equinox))Label(dc,"♈",new Point(equinox.X-14,equinox.Y-8),Faded("Muted",0.8),true);
  }
  // Marker names sit on a rounded plate, matching the sky map's constellation label.
  void Plate(DrawingContext dc,string text,Point at,Rect bounds){
   double size=Math.Min(20,(double)GetValue(CaptionSizeProperty)+0.5);var formatted=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),size,Colour("Text"),VisualTreeHelper.GetDpi(this).PixelsPerDip);
   double width=formatted.Width+14,height=formatted.Height+4;var plate=new Rect(Math.Max(3,Math.Min(bounds.Width-width-3,at.X)),Math.Max(3,Math.Min(bounds.Height-height-3,at.Y)),width,height);
   dc.DrawRoundedRectangle(Faded("Surface",0.92),Stroke("Accent",0.9,1),plate,height/2,height/2);dc.DrawText(formatted,new Point(plate.X+7,plate.Y+2));
  }
  // Orbit crossing the ecliptic: ascending where it rises north, descending where it dips south.
  bool Node(OrbitRecord o,bool ascending,double extent,out OrbitPoint point){
   point=new OrbitPoint();if(o.i<0.5||o.i>179.5)return false;double v=(ascending?-o.argument_of_perihelion:180-o.argument_of_perihelion)*OrbitalModel.R;if(1+o.e*Math.Cos(v)<=1e-9)return false;
   point=OrbitalModel.AtAnomaly(o,v);return OrbitalModel.Finite(point.X)&&OrbitalModel.Finite(point.Y)&&OrbitalModel.Finite(point.Z)&&point.Length<=extent*1.001;
  }
  // Height above the ecliptic, drawn as a dotted stem with a small footprint.
  void Stem(DrawingContext dc,OrbitPoint p,Pen pen,Rect bounds){
   if(Math.Abs(p.Z)<1e-6*Extent)return;var foot=Project(View(new OrbitPoint(p.X,p.Y,0)));var top=Project(View(p));
   if(!OrbitalModel.Finite(foot.X)||!OrbitalModel.Finite(foot.Y)||!OrbitalModel.Finite(top.X)||!OrbitalModel.Finite(top.Y))return;
   var a=foot;var b=top;if(VisibleLine(ref a,ref b,bounds))dc.DrawLine(pen,a,b);if(bounds.Contains(foot))dc.DrawEllipse(null,pen,foot,3,1.5);
  }
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);if(ActualWidth<20||ActualHeight<20)return;var bounds=new Rect(RenderSize);
   var backdrop=new RadialGradientBrush(Hue("SurfaceAlt"),Hue("Surface")){GradientOrigin=new Point(0.5,0.42),Center=new Point(0.5,0.42),RadiusX=0.85,RadiusY=0.85};backdrop.Freeze();
   dc.DrawRoundedRectangle(backdrop,new Pen(Colour(IsKeyboardFocused?"Accent":"Border"),IsKeyboardFocused?1.5:1),bounds,8,8);if(Scene==null)return;
   dc.PushClip(new RectangleGeometry(bounds,8,8));
   // Faint fixed stars for depth; they do not move so the orbit reads as the moving subject.
   for(int i=0;i<starSeed.Length;i+=3){dc.PushOpacity(0.1+0.3*starSeed[i+2]);dc.DrawEllipse(Colour("Text"),null,new Point(starSeed[i]*ActualWidth,starSeed[i+1]*ActualHeight),0.5+0.8*starSeed[i+2],0.5+0.8*starSeed[i+2]);dc.Pop();}
   DrawEcliptic(dc,bounds);
   var segments=new List<Segment>();var target=Camera.Inner?Scene.Inner:Scene.Full;bool clipped=target.Clipped;
   Action<OrbitPath,bool> add=(path,selected)=>{if(path==null)return;for(int k=1;k<path.Points.Length;k++){var a=View(path.Points[k-1]);var b=View(path.Points[k]);var pa=Project(a);var pb=Project(b);if(!OrbitalModel.Finite(pa.X)||!OrbitalModel.Finite(pa.Y)||!OrbitalModel.Finite(pb.X)||!OrbitalModel.Finite(pb.Y))continue;bool inside=bounds.Contains(pa)&&bounds.Contains(pb);if(selected&&!inside)clipped=true;if(VisibleLine(ref pa,ref pb,bounds))segments.Add(new Segment{A=pa,B=pb,Depth=(a.Z+b.Z)/2,Target=selected,Below=path.Points[k-1].Z+path.Points[k].Z<0});}};
   foreach(var context in Scene.Context)add(context,false);add(target,true);
   // The path north of the ecliptic is bright; the part south of it is fainter, like the sky map's below-horizon lines.
   Pen glow=Stroke("Accent",0.13,6),near=Stroke("Accent",1,2.2),far=Stroke("Accent",0.6,2.2),nearBelow=Stroke("Accent",0.5,1.5),farBelow=Stroke("Accent",0.3,1.5),other=Stroke("Muted",0.3,1),otherBelow=Stroke("Muted",0.16,1);
   foreach(var segment in segments.OrderBy(s=>s.Depth)){
    if(!segment.Target){dc.DrawLine(segment.Below?otherBelow:other,segment.A,segment.B);continue;}
    if(!segment.Below&&segment.Depth>=0)dc.DrawLine(glow,segment.A,segment.B);dc.DrawLine(segment.Below?(segment.Depth<0?farBelow:nearBelow):segment.Depth<0?far:near,segment.A,segment.B);
   }
   var stem=Stroke("Muted",0.55,1,new DashStyle(new double[]{2,3},0));
   // Planets at the marker date, to show where the other worlds were.
   foreach(var planet in ContextMarkers){var spot=Project(View(planet.Point));if(!bounds.Contains(spot))continue;dc.DrawEllipse(Faded("Muted",0.9),null,spot,2.5,2.5);Label(dc,planet.Glyph,new Point(spot.X+4,spot.Y-15),Faded("Muted",0.95),true);}
   var record=Scene.Record;OrbitPoint node;
   if(Node(record,true,target.Extent,out node)){var spot=Project(View(node));if(bounds.Contains(spot)){dc.DrawEllipse(Colour("Surface"),Stroke("Accent",0.9,1.3),spot,3.5,3.5);Label(dc,"☊",new Point(spot.X+5,spot.Y-14),Colour("Muted"),true);}}
   if(Node(record,false,target.Extent,out node)){var spot=Project(View(node));if(bounds.Contains(spot)){dc.DrawEllipse(Colour("Surface"),Stroke("Accent",0.9,1.3),spot,3.5,3.5);Label(dc,"☋",new Point(spot.X+5,spot.Y-14),Colour("Muted"),true);}}
   var centre=Project(new OrbitPoint());
   if(bounds.Contains(centre)){
    if(record.central_body=="Sun"){var halo=new RadialGradientBrush(Color.FromArgb(190,255,226,140),Color.FromArgb(0,255,180,60));halo.Freeze();dc.DrawEllipse(halo,null,centre,17,17);var disc=new RadialGradientBrush(Color.FromRgb(255,244,196),Color.FromRgb(238,176,52));disc.Freeze();dc.DrawEllipse(disc,Stroke("Text",0.4,0.8),centre,5.5,5.5);}
    else{var halo=new RadialGradientBrush(Color.FromArgb(120,120,180,255),Color.FromArgb(0,120,180,255));halo.Freeze();dc.DrawEllipse(halo,null,centre,15,15);var disc=new RadialGradientBrush(Color.FromRgb(132,184,236),Color.FromRgb(36,86,150)){GradientOrigin=new Point(0.35,0.3)};disc.Freeze();dc.DrawEllipse(disc,Stroke("Text",0.4,0.8),centre,5,5);}
    Label(dc,Localization.T(record.central_body),new Point(centre.X+10,centre.Y-19));
   }
   var periApsis=OrbitalModel.AtAnomaly(record,0);var peri=Project(View(periApsis));
   if(bounds.Contains(peri)){Stem(dc,periApsis,stem,bounds);dc.DrawEllipse(Colour("Surface"),Stroke("Accent",1,1.5),peri,3,3);Label(dc,Localization.T(record.central_body=="Sun"?"Perihelion":"Perigee"),new Point(peri.X+7,peri.Y+4));}
   if(Marker.HasValue){var marker=Project(View(Marker.Value));if(bounds.Contains(marker)){Stem(dc,Marker.Value,stem,bounds);var halo=new RadialGradientBrush(Color.FromArgb(150,Hue("Accent").R,Hue("Accent").G,Hue("Accent").B),Color.FromArgb(0,Hue("Accent").R,Hue("Accent").G,Hue("Accent").B));halo.Freeze();dc.DrawEllipse(halo,null,marker,15,15);dc.DrawEllipse(Faded("Accent",0.2),Stroke("Accent",1,1.5),marker,6.5,6.5);dc.DrawEllipse(Colour("Accent"),null,marker,2.6,2.6);Plate(dc,Catalog.DisplayId(record.object_id),new Point(marker.X+10,marker.Y-24),bounds);}}
   // Arrow follows increasing true anomaly, including retrograde orbital planes.
   int direction=target.Points.Length/2+Math.Max(1,target.Points.Length/12);if(direction<target.Points.Length-1)Arrow(dc,Project(View(target.Points[direction])),Project(View(target.Points[direction+1])),Colour("Accent"),bounds);
   if(target.Clipped){Arrow(dc,Project(View(target.Points[1])),Project(View(target.Points[0])),Colour("Accent"),bounds);Arrow(dc,Project(View(target.Points[target.Points.Length-2])),Project(View(target.Points.Last())),Colour("Accent"),bounds);}
   double au=50/Scale;var scalePen=Stroke("Muted",1,1);dc.DrawLine(scalePen,new Point(10,ActualHeight-8),new Point(60,ActualHeight-8));dc.DrawLine(scalePen,new Point(10,ActualHeight-11),new Point(10,ActualHeight-5));dc.DrawLine(scalePen,new Point(60,ActualHeight-11),new Point(60,ActualHeight-5));Label(dc,Distance(au),new Point(10,ActualHeight-23));
   // Axis gizmo on a small dial: X toward the vernal equinox, Z toward the ecliptic pole.
   var origin=new Point(ActualWidth-32,35);dc.DrawEllipse(Faded("Surface",0.55),Stroke("Border",0.9,1),origin,25,25);
   var axes=new[]{new OrbitPoint(1,0,0),new OrbitPoint(0,1,0),new OrbitPoint(0,0,1)};
   foreach(int axis in new[]{0,1,2}.OrderBy(a=>View(axes[a]).Z)){var v=View(axes[axis]);var end=new Point(origin.X+v.X*17,origin.Y-v.Y*17);dc.DrawLine(Stroke(axis==2?"Accent":"Muted",axis==2?1:0.85,1.3),origin,end);Label(dc,new[]{"X","Y","Z"}[axis],new Point(end.X+2,end.Y-7));}
   if(clipped)Label(dc,Localization.T(Camera.Inner&&Scene.Record.central_body=="Sun"&&Scene.Record.q>40?"Perihelion beyond the inner solar system":"Extent clipped"),new Point(10,8));dc.Pop();
  }
  static void Arrow(DrawingContext dc,Point a,Point b,Brush brush,Rect bounds){if(!bounds.Contains(b))return;double x=b.X-a.X,y=b.Y-a.Y,length=Math.Sqrt(x*x+y*y);if(length<1e-8)return;x/=length;y/=length;var pen=new Pen(brush,1.5);dc.DrawLine(pen,b,new Point(b.X-x*7-y*4,b.Y-y*7+x*4));dc.DrawLine(pen,b,new Point(b.X-x*7+y*4,b.Y-y*7-x*4));}
  void Label(DrawingContext dc,string text,Point at,Brush brush=null,bool symbol=false){double size=(double)GetValue(CaptionSizeProperty);var formatted=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(symbol?"Segoe UI Symbol":"Segoe UI"),Math.Min(20,size),brush??Colour("Muted"),VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(formatted,new Point(Math.Max(4,Math.Min(ActualWidth-formatted.Width-4,at.X)),Math.Max(3,Math.Min(ActualHeight-formatted.Height-3,at.Y))));}
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
   Children.Add(Text("Bright path lies north of the ecliptic, faint path south. \u260A ascending node · \u260B descending node."));
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
  // Other planets at the marker date (or the orbit epoch), so the target is seen among them.
  void SetContext(DateTime? utc){
   var list=new List<OrbitMarker>();
   if(record!=null&&record.central_body=="Sun"){
    DateTime? when=utc.HasValue?utc:OrbitalModel.Utc(record.epoch);
    if(when.HasValue)foreach(string id in new[]{"MERCURY","VENUS","EARTH","MARS","JUPITER","SATURN","URANUS","NEPTUNE"}){
     if(id==record.object_id)continue;var planet=PlanetEphemeris.Orbit(id,when.Value);OrbitPoint point;
     if(planet!=null&&OrbitalModel.Position(planet,OrbitalModel.Julian(when.Value),out point))list.Add(new OrbitMarker{Glyph=OrbitView.PlanetGlyph(id),Point=point});
    }
   }
   View.ContextMarkers=list;
  }
  void SetMarker(DateTime? utc){
   MarkerUtc=utc;if(record==null)return;SetContext(utc);OrbitPoint point=new OrbitPoint();bool available=false;string time;
   if(utc.HasValue){double? day=OrbitalModel.Dynamical(utc.Value);var planet=record.rights.dataset=="planetary-approximation"?PlanetEphemeris.Orbit(record.object_id,utc.Value):null;
    available=day.HasValue&&OrbitalModel.Position(planet??record,day.Value,out point);View.Marker=available?(OrbitPoint?)point:null;time=Localization.F("Marker: {0} UTC",utc.Value.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture));
   }else{available=OrbitalModel.Position(record,record.epoch,out point,true);View.Marker=available?(OrbitPoint?)point:null;time=Localization.F("Marker at orbit epoch: JD {0} {1}",record.epoch.ToString("0.#####",CultureInfo.InvariantCulture),record.time_standard);}
   if(!available)time=Localization.T("Dated position unavailable for this solution. Use Orbit epoch.");
   caption.Text=Localization.T("Approximate orbit · linear distances; body markers enlarged.");status.Text=(overview?Localization.T("Solar-system overview")+" · ":"")+time+"\n"+Localization.F("Source epoch: JD {0} {1} · {2}",record.epoch.ToString("0.#####",CultureInfo.InvariantCulture),record.time_standard,record.solution_id)+"\n"+Localization.T("J2000 ecliptic · two-body geometry, not a precision ephemeris. UTC → TT; TDB differs by less than 2 ms.");if(!record.valid_from.HasValue||!record.valid_to.HasValue)status.Text+="\n"+Localization.T("Snapshot positions are shown only at the orbit epoch.");if(record.object_id=="EARTH")status.Text+="\n"+Localization.T("Earth uses the Earth–Moon barycentre approximation.");View.InvalidateVisual();
  }
 }
}
