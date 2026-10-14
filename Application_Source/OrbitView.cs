using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroArchive {
 // A date this target was imaged in the repository, placed on its orbit.
 public struct ImagedMark {public DateTime Utc;public OrbitPoint Point;}
 public sealed class OrbitCamera {
  public double Yaw=35,Tilt=55,Zoom=1,PanX,PanY;public bool Plane,Inner;
  public OrbitCamera Copy(){return (OrbitCamera)MemberwiseClone();}
 }
 // The real sky behind the orbits: the sky globe's stars and constellation figures, turned into
 // the ecliptic frame the orbit scene uses, so the backdrop turns with the camera as the sky would.
 static class OrbitSky {
  public static readonly OrbitPoint[] Stars;public static readonly int[] Lines;
  static OrbitSky(){
   try{
    Stars=SkyFigures.Stars.Select(s=>OrbitMath.Ecliptic(s.X,s.Y,s.Z)).ToArray();var pairs=new List<int>();
    foreach(var figure in SkyFigures.Figures)foreach(var path in figure.Paths)for(int i=1;i<path.Length;i++){pairs.Add(path[i-1]);pairs.Add(path[i]);}
    Lines=pairs.ToArray();
   }catch(Exception){Stars=new OrbitPoint[0];Lines=new int[0];}
  }
 }
 // Line segments gathered into one geometry per style: an orbit is a handful of draw calls and its joins stay smooth.
 sealed class OrbitStrokes {
  readonly StreamGeometry geometry=new StreamGeometry();readonly StreamGeometryContext context;Point end;bool open;public bool Any;
  public OrbitStrokes(){context=geometry.Open();}
  public void Add(Point a,Point b){if(!open||end!=a){context.BeginFigure(a,false,false);open=true;}context.LineTo(b,true,true);end=b;Any=true;}
  public Geometry Done(){context.Close();geometry.Freeze();return geometry;}
 }
 // Cached physical 3D coordinates in the sky globe's visual language, drawn natively in WPF. No web/code
 // downloads, timers or network work during gestures; camera moves use the frame clock only while they run.
 public sealed class OrbitView:FrameworkElement {
  static DependencyProperty BrushProperty(string name){return DependencyProperty.Register(name+"Brush",typeof(Brush),typeof(OrbitView),new FrameworkPropertyMetadata(Brushes.Gray,FrameworkPropertyMetadataOptions.AffectsRender));}
  static readonly Dictionary<string,DependencyProperty> palette=new Dictionary<string,DependencyProperty>{{"SurfaceAlt",BrushProperty("SurfaceAlt")},{"Surface",BrushProperty("Surface")},{"Text",BrushProperty("Text")},{"Muted",BrushProperty("Muted")},{"Accent",BrushProperty("Accent")},{"Border",BrushProperty("Border")}};
  static readonly DependencyProperty CaptionSizeProperty=DependencyProperty.Register("CaptionSize",typeof(double),typeof(OrbitView),new FrameworkPropertyMetadata(11.0,FrameworkPropertyMetadataOptions.AffectsRender));
  // The main window points this at the Reduce motion setting; Windows' own animation setting also counts.
  public static Func<bool> ReducedMotion=()=>!SystemParameters.ClientAreaAnimation;
  static readonly Color SunLight=Color.FromRgb(255,206,84),EarthLight=Color.FromRgb(86,146,224);
  static readonly Dictionary<string,Color> PlanetColours=new Dictionary<string,Color>{{"Mercury",Color.FromRgb(166,166,172)},{"Venus",Color.FromRgb(222,184,100)},{"Earth",Color.FromRgb(82,142,222)},{"Mars",Color.FromRgb(216,104,72)},{"Jupiter",Color.FromRgb(204,158,112)},{"Saturn",Color.FromRgb(212,184,112)},{"Uranus",Color.FromRgb(104,196,210)},{"Neptune",Color.FromRgb(96,120,224)}};
  const double TweenSeconds=0.28;
  public OrbitCamera Camera=new OrbitCamera();public OrbitScene Scene {get;private set;}
  public OrbitPoint? Marker;public bool MarkerIsNow;public List<ImagedMark> Imaged;public List<KeyValuePair<string,OrbitPoint>> Bodies;public bool Overview;public string DateLabel;
  public Action Changed,TogglePlay;Point last,pressed;bool dragging,panning;
  OrbitCamera from,to;Stopwatch clock;bool tweening;
  public OrbitView(){
   foreach(var colour in palette)SetResourceReference(colour.Value,colour.Key);SetResourceReference(CaptionSizeProperty,"UiFontCaption");
   Cursor=Cursors.Hand;ToolTip=Localization.T("Drag or use arrows to rotate. Shift-drag to pan. Focus the orbit or hold Ctrl to zoom with the wheel. Pinch to zoom.");Focusable=true;IsManipulationEnabled=true;ClipToBounds=true;MinHeight=160;
   SetResourceReference(FocusVisualStyleProperty,SystemParameters.FocusVisualStyleKey);
   AutomationProperties.SetName(this,Localization.T("3D orbit"));AutomationProperties.SetHelpText(this,Localization.T("Drag or use arrows to rotate. Shift-drag to pan. Focus the orbit or hold Ctrl to zoom with the wheel. Pinch to zoom."));
   MouseLeftButtonDown+=(s,e)=>{Focus();StopTween();pressed=e.GetPosition(this);if(e.ClickCount==2){GoReset();e.Handled=true;return;}last=e.GetPosition(this);dragging=true;panning=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;Cursor=panning?Cursors.ScrollAll:Cursors.SizeAll;CaptureMouse();e.Handled=true;};
   MouseMove+=(s,e)=>{if(!dragging)return;var point=e.GetPosition(this);double dx=point.X-last.X,dy=point.Y-last.Y;last=point;if(panning)Pan(dx,dy);else Rotate(dx*0.5,dy*0.5);e.Handled=true;};
   MouseLeftButtonUp+=(s,e)=>{if(dragging&&(e.GetPosition(this)-pressed).Length<4)ClickImaged(e.GetPosition(this));dragging=false;Cursor=Cursors.Hand;ReleaseMouseCapture();e.Handled=true;};LostMouseCapture+=(s,e)=>{dragging=false;Cursor=Cursors.Hand;};
   PreviewMouseWheel+=(s,e)=>{if(!IsKeyboardFocusWithin&&(Keyboard.Modifiers&ModifierKeys.Control)==0)return;StopTween();Zoom(Math.Exp(Math.Max(-1200,Math.Min(1200,e.Delta))/600.0),e.GetPosition(this));e.Handled=true;};
   KeyDown+=(s,e)=>{bool handled=true;StopTween();switch(e.Key){case Key.Left:Rotate(-5,0);break;case Key.Right:Rotate(5,0);break;case Key.Up:Rotate(0,5);break;case Key.Down:Rotate(0,-5);break;case Key.Add:case Key.OemPlus:Zoom(1.25);break;case Key.Subtract:case Key.OemMinus:Zoom(0.8);break;case Key.Home:GoReset();break;case Key.Space:if(TogglePlay==null)handled=false;else TogglePlay();break;case Key.Escape:MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));break;default:handled=false;break;}e.Handled=handled;};
   ManipulationStarting+=(s,e)=>{StopTween();dragging=false;ReleaseMouseCapture();e.Mode=ManipulationModes.Translate|ManipulationModes.Scale;e.ManipulationContainer=this;e.Handled=true;};
   ManipulationDelta+=(s,e)=>{Focus();double scale=e.DeltaManipulation.Scale.X;if(OrbitalModel.Finite(scale)&&scale>0)Zoom(scale,e.ManipulationOrigin);if(TouchesCapturedWithin.Concat(TouchesOver).Distinct().Count()<2)Rotate(e.DeltaManipulation.Translation.X*0.5,e.DeltaManipulation.Translation.Y*0.5);else Pan(e.DeltaManipulation.Translation.X,e.DeltaManipulation.Translation.Y);e.Handled=true;};
   Unloaded+=(s,e)=>StopTween(true);IsVisibleChanged+=(s,e)=>{if(!IsVisible)StopTween(true);};
  }
  public void SetScene(OrbitScene scene){Scene=scene;pickedMark=null;StopTween(true);InvalidateVisual();}
  void Update(){InvalidateVisual();if(Changed!=null)Changed();}
  public void Redraw(){InvalidateVisual();}
  public void Reset(){StopTween();Camera=new OrbitCamera();Update();}
  public void Rotate(double yaw,double tilt){StopTween();Camera.Plane=false;Camera.Yaw=(Camera.Yaw+yaw)%360;Camera.Tilt=Math.Max(-90,Math.Min(90,Camera.Tilt+tilt));Update();}
  public void Pan(double x,double y){if(!OrbitalModel.Finite(x)||!OrbitalModel.Finite(y))return;Camera.PanX=Math.Max(-ActualWidth*2,Math.Min(ActualWidth*2,Camera.PanX+x));Camera.PanY=Math.Max(-ActualHeight*2,Math.Min(ActualHeight*2,Camera.PanY+y));Update();}
  public void Zoom(double factor){Zoom(factor,new Point(ActualWidth/2,ActualHeight/2));}
  public void Zoom(double factor,Point anchor){if(!OrbitalModel.Finite(factor)||factor<=0)return;StopTween();double next=Math.Max(0.1,Math.Min(1e10,Camera.Zoom*factor)),ratio=next/Camera.Zoom;Camera.Zoom=next;
   double x=anchor.X-ActualWidth/2,y=anchor.Y-ActualHeight/2;Camera.PanX=(Camera.PanX-x)*ratio+x;Camera.PanY=(Camera.PanY-y)*ratio+y;Pan(0,0);}
  public void Fit(bool inner){StopTween();Camera.Inner=inner;Camera.Zoom=1;Camera.PanX=Camera.PanY=0;Update();}
  public void Top(){StopTween();Camera.Plane=false;Camera.Tilt=90;Camera.Yaw=0;Update();}
  public void Plane(){StopTween();Camera.Plane=true;Update();}
  // The same moves as buttons use them: eased over a moment, or at once when motion is reduced.
  public void GoReset(){GoTo(new OrbitCamera());}
  public void GoTop(){var next=Camera.Copy();next.Plane=false;next.Tilt=90;next.Yaw=0;GoTo(next);}
  public void GoPlane(){var next=Camera.Copy();next.Plane=true;GoTo(next);}
  public void GoFit(bool inner){var next=Camera.Copy();next.Inner=inner;next.Zoom=1;next.PanX=next.PanY=0;GoTo(next);}
  double ExtentOf(bool inner){return Scene==null?1:Math.Max(1e-9,(inner?Scene.Inner:Scene.Full).Extent);}
  void GoTo(OrbitCamera target){
   StopTween();
   // The orbit-plane view is a different frame from yaw and tilt, so it cannot be blended.
   if(Scene==null||Camera.Plane||target.Plane||ReducedMotion()||!IsVisible||ActualWidth<20){Camera=target;Update();return;}
   // Switching between Fit and Inner changes the extent; keep the picture's scale continuous so the zoom eases rather than jumps.
   if(Camera.Inner!=target.Inner){Camera.Zoom=Math.Max(0.1,Math.Min(1e10,Camera.Zoom*ExtentOf(Camera.Inner)/ExtentOf(target.Inner)));Camera.Inner=target.Inner;}
   from=Camera.Copy();to=target;clock=Stopwatch.StartNew();tweening=true;CompositionTarget.Rendering+=Tick;
  }
  void Tick(object sender,EventArgs e){
   double t=Math.Min(1,clock.Elapsed.TotalSeconds/TweenSeconds),k=OrbitMath.Ease(t);
   var step=new OrbitCamera{Yaw=OrbitMath.Angle(from.Yaw,to.Yaw,k),Tilt=from.Tilt+(to.Tilt-from.Tilt)*k,Zoom=from.Zoom*Math.Pow(to.Zoom/from.Zoom,k),PanX=from.PanX+(to.PanX-from.PanX)*k,PanY=from.PanY+(to.PanY-from.PanY)*k,Inner=to.Inner};
   if(t>=1){step=to.Copy();StopTween();}Camera=step;Update();
  }
  void StopTween(bool finish=false){if(!tweening)return;tweening=false;CompositionTarget.Rendering-=Tick;if(finish&&to!=null){Camera=to.Copy();Update();}}
  public bool Animating {get{return tweening;}}
  Brush Colour(string key){return GetValue(palette[key]) as Brush??Brushes.Gray;}
  static Brush Tint(Brush brush,double opacity){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();return copy;}
  static Brush Solid(Color colour,double opacity){var brush=new SolidColorBrush(colour){Opacity=opacity};brush.Freeze();return brush;}
  static Pen Stroke(Brush brush,double opacity,double width){var pen=new Pen(Tint(brush,opacity),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();return pen;}
  static Pen Dashes(Brush brush,double opacity,double width){var pen=new Pen(Tint(brush,opacity),width){DashStyle=new DashStyle(new double[]{3,2.5},0),LineJoin=PenLineJoin.Round};pen.Freeze();return pen;}
  static bool Finite(Point p){return OrbitalModel.Finite(p.X)&&OrbitalModel.Finite(p.Y);}
  static double Dot(OrbitPoint a,OrbitPoint b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
  // One frame's camera and palette, worked out once.
  Rect bounds;OrbitPath target;double extent,scale,cosYaw,sinYaw,cosTilt,sinTilt;OrbitPoint planeA,planeB,planeN;bool dark;List<Label> labels;
  sealed class Label {public string Text;public Point At;public bool Accent,Force;public int Priority;}
  void Prepare(){
   bounds=new Rect(RenderSize);target=Camera.Inner?Scene.Inner:Scene.Full;extent=Math.Max(1e-9,target.Extent);scale=Math.Max(1,Math.Min(ActualWidth,ActualHeight)-64)*0.46*Camera.Zoom/extent;
   double yaw=Camera.Yaw*OrbitalModel.R,tilt=Camera.Tilt*OrbitalModel.R;cosYaw=Math.Cos(yaw);sinYaw=Math.Sin(yaw);cosTilt=Math.Cos(tilt);sinTilt=Math.Sin(tilt);
   if(Camera.Plane){var o=Scene.Record;planeA=OrbitalModel.Orient(o,1,0);planeB=OrbitalModel.Orient(o,0,1);planeN=new OrbitPoint(planeA.Y*planeB.Z-planeA.Z*planeB.Y,planeA.Z*planeB.X-planeA.X*planeB.Z,planeA.X*planeB.Y-planeA.Y*planeB.X);}
   var surface=Colour("SurfaceAlt") as SolidColorBrush;dark=surface==null||(0.299*surface.Color.R+0.587*surface.Color.G+0.114*surface.Color.B)<128;labels=new List<Label>();
  }
  // Scene to view space: x right, y up, z towards the viewer. Yaw turns about the pole, tilt leans the pole towards the viewer.
  OrbitPoint View(OrbitPoint p){
   if(Camera.Plane)return new OrbitPoint(Dot(p,planeA),Dot(p,planeB),Dot(p,planeN));
   double x=p.X*cosYaw-p.Y*sinYaw,y=p.X*sinYaw+p.Y*cosYaw;return new OrbitPoint(x,y*sinTilt+p.Z*cosTilt,-y*cosTilt+p.Z*sinTilt);
  }
  Point Project(OrbitPoint v){return new Point(ActualWidth/2+Camera.PanX+v.X*scale,ActualHeight/2+Camera.PanY-v.Y*scale);}
  Point ProjectScene(OrbitPoint p){return Project(View(p));}
  static bool VisibleLine(ref Point a,ref Point b,Rect bounds){
   // Liang–Barsky clipping avoids huge WPF coordinates at extreme orbital extents.
   double dx=b.X-a.X,dy=b.Y-a.Y,t0=0,t1=1;double[] p={-dx,dx,-dy,dy},q={a.X-bounds.Left,bounds.Right-a.X,a.Y-bounds.Top,bounds.Bottom-a.Y};
   for(int k=0;k<4;k++){if(Math.Abs(p[k])<1e-20){if(q[k]<0)return false;}else{double t=q[k]/p[k];if(p[k]<0){if(t>t1)return false;t0=Math.Max(t0,t);}else{if(t<t0)return false;t1=Math.Min(t1,t);}}}
   var origin=a;a=new Point(origin.X+t0*dx,origin.Y+t0*dy);b=new Point(origin.X+t1*dx,origin.Y+t1*dy);return true;
  }
  // Visits each finite, on-screen piece of a path with its view depth; true when any of it runs outside the frame.
  bool Walk(OrbitPath path,Action<Point,Point,OrbitPoint,OrbitPoint,double> piece){
   bool outside=false;if(path==null)return false;
   for(int k=1;k<path.Points.Length;k++){
    var a=View(path.Points[k-1]);var b=View(path.Points[k]);var pa=Project(a);var pb=Project(b);if(!Finite(pa)||!Finite(pb))continue;
    if(!bounds.Contains(pa)||!bounds.Contains(pb))outside=true;
    if(VisibleLine(ref pa,ref pb,bounds))piece(pa,pb,path.Points[k-1],path.Points[k],(a.Z+b.Z)/2);
   }
   return outside;
  }
  static void Draw(DrawingContext dc,OrbitStrokes strokes,Pen pen){var geometry=strokes.Done();if(strokes.Any)dc.DrawGeometry(null,pen,geometry);}
  void Ask(string text,Point at,int priority,bool accent=false,bool force=false){labels.Add(new Label{Text=text,At=at,Priority=priority,Accent=accent,Force=force});}
  FormattedText Format(string text,Brush brush,bool bold){double size=Math.Min(20,(double)GetValue(CaptionSizeProperty));return new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);}
  // Rounded name pills, as on the sky globe. Lower priority numbers claim their space first; later ones that would overlap are left out.
  void Flush(DrawingContext dc,List<Rect> placed){
   foreach(var label in labels.OrderBy(l=>l.Priority)){
    var format=Format(label.Text,Colour(label.Accent?"Text":"Muted"),label.Accent);format.MaxTextWidth=Math.Max(40,ActualWidth-28);format.MaxLineCount=1;format.Trimming=TextTrimming.CharacterEllipsis;
    double width=Math.Min(format.Width,format.MaxTextWidth)+14,height=format.Height+4;var box=new Rect(Math.Max(3,Math.Min(ActualWidth-width-3,label.At.X)),Math.Max(3,Math.Min(ActualHeight-height-3,label.At.Y)),width,height);
    if(!label.Force&&placed.Any(r=>r.IntersectsWith(box)))continue;placed.Add(box);
    dc.DrawRoundedRectangle(Tint(Colour("Surface"),0.92),Stroke(Colour(label.Accent?"Accent":"Border"),label.Accent?0.9:0.7,1),box,height/2,height/2);dc.DrawText(format,new Point(box.X+7,box.Y+2));
   }
  }
  // Stars and constellation lines on the far side of the scene, brightest straight behind it and fading to the rim.
  void Backdrop(DrawingContext dc){
   var stars=OrbitSky.Stars;int count=stars.Length;if(count==0)return;double radius=0.62*Math.Sqrt(bounds.Width*bounds.Width+bounds.Height*bounds.Height);
   var at=new Point[count];var fade=new double[count];Brush text=Colour("Text"),muted=Colour("Muted");
   for(int i=0;i<count;i++){var v=View(stars[i]);fade[i]=Math.Max(0,Math.Min(1,-v.Z/0.3));at[i]=new Point(bounds.Width/2+v.X*radius,bounds.Height/2-v.Y*radius);}
   var lines=new[]{new OrbitStrokes(),new OrbitStrokes(),new OrbitStrokes()};var pairs=OrbitSky.Lines;
   for(int k=0;k+1<pairs.Length;k+=2){int a=pairs[k],b=pairs[k+1];double f=Math.Min(fade[a],fade[b]);if(f<=0.03)continue;var pa=at[a];var pb=at[b];if(VisibleLine(ref pa,ref pb,bounds))lines[f<0.34?0:f<0.67?1:2].Add(pa,pb);}
   double lineLevel=dark?0.2:0.15,dotLevel=dark?0.6:0.42;for(int bin=0;bin<3;bin++)Draw(dc,lines[bin],Stroke(muted,lineLevel*(bin+1)/3,0.8));
   var dots=new[]{Tint(text,dotLevel*0.35),Tint(text,dotLevel*0.65),Tint(text,dotLevel)};
   for(int i=0;i<count;i++){if(fade[i]<=0.03||!bounds.Contains(at[i]))continue;double r=i%7==0?1.5:1;dc.DrawEllipse(dots[fade[i]<0.34?0:fade[i]<0.67?1:2],null,at[i],r,r);}
  }
  // The reference plane: a faint disc with distance rings and 30° spokes, tilting with the orbit. Rings are round numbers of AU, or of km at the Earth–Moon scale.
  void Grid(DrawingContext dc){
   double perPixel=1/scale,reach=Math.Sqrt(Math.Pow(Math.Abs(Camera.PanX)+ActualWidth/2,2)+Math.Pow(Math.Abs(Camera.PanY)+ActualHeight/2,2))*perPixel;if(!OrbitalModel.Finite(reach)||reach<=0)return;
   double unit=OrbitMath.Kilometres(reach)?OrbitMath.AuKm:1,step=OrbitMath.NiceStep(reach*unit/3.5)/unit;int rings=(int)Math.Max(1,Math.Min(20,Math.Ceiling(reach/step)));const int Sides=96;Brush muted=Colour("Muted");
   var near=new OrbitStrokes();var rear=new OrbitStrokes();var spokes=new OrbitStrokes();var disc=new StreamGeometry();bool discOk=true;
   using(var context=disc.Open()){
    for(int k=1;k<=rings;k++){double r=k*step;var previous=View(new OrbitPoint(r,0,0));var from=Project(previous);
     if(k==rings){if(Finite(from))context.BeginFigure(from,true,true);else discOk=false;}
     for(int i=1;i<=Sides;i++){double angle=i*2*Math.PI/Sides;var v=View(new OrbitPoint(r*Math.Cos(angle),r*Math.Sin(angle),0));var point=Project(v);var a=from;var b=point;
      if(Finite(a)&&Finite(b)&&VisibleLine(ref a,ref b,bounds))((previous.Z+v.Z)/2<0?rear:near).Add(a,b);
      if(k==rings&&discOk){if(Finite(point))context.LineTo(point,false,false);else discOk=false;}
      from=point;previous=v;}}
   }
   disc.Freeze();if(discOk)dc.DrawGeometry(Tint(muted,dark?0.06:0.09),null,disc);
   double outer=rings*step;for(int i=0;i<12;i++){double angle=i*Math.PI/6;var a=ProjectScene(new OrbitPoint());var b=ProjectScene(new OrbitPoint(outer*Math.Cos(angle),outer*Math.Sin(angle),0));if(Finite(a)&&Finite(b)&&VisibleLine(ref a,ref b,bounds))spokes.Add(a,b);}
   Draw(dc,spokes,Stroke(muted,0.14,0.7));Draw(dc,rear,Stroke(muted,0.11,0.8));Draw(dc,near,Stroke(muted,0.3,0.8));
   // Ring distances sit on the near side of each ring, thinned out so they never collide.
   int every=(int)Math.Ceiling(rings/6.0);for(int k=every;k<=rings;k+=every){var at=ProjectScene(new OrbitPoint(k*step*Math.Cos(-0.75),k*step*Math.Sin(-0.75),0));if(Finite(at)&&bounds.Contains(at))Ask(OrbitMath.Distance(k*step),new Point(at.X+3,at.Y-9),8);}
  }
  // Body names carry an "Orbit" context so translating "Mars" here does not also rename a target called Mars elsewhere in the app.
  static string Body(string english){string key="Orbit|"+english;return Localization.Has(key)?Localization.T(key):Localization.T(english);}
  static Color ColourOf(string name,Color fallback){Color value;return name!=null&&PlanetColours.TryGetValue(name,out value)?value:fallback;}
  void Orbits(DrawingContext dc){
   var record=Scene.Record;Brush muted=Colour("Muted"),accent=Colour("Accent"),surface=Colour("Surface");
   // Planet orbits behind the target: three depth bins each, tinted by planet in the solar-system overview.
   double[] context={0.14,0.24,0.4};
   Action<OrbitPath,Brush> subdued=(path,brush)=>{var bins=new[]{new OrbitStrokes(),new OrbitStrokes(),new OrbitStrokes()};Walk(path,(a,b,p,q,depth)=>bins[depth<-0.12*extent?0:depth<0.12*extent?1:2].Add(a,b));for(int i=0;i<3;i++)Draw(dc,bins[i],Stroke(brush,context[i]*(Overview?1.4:1),1));};
   if(Overview){foreach(var path in Scene.Context)if(path!=null)subdued(path,Solid(ColourOf(path.Name,Colors.Gray),1));subdued(target,Solid(ColourOf(target.Name,Colors.Gray),1));}
   else{var bins=new[]{new OrbitStrokes(),new OrbitStrokes(),new OrbitStrokes()};foreach(var path in Scene.Context)Walk(path,(a,b,p,q,depth)=>bins[depth<-0.12*extent?0:depth<0.12*extent?1:2].Add(a,b));for(int i=0;i<3;i++)Draw(dc,bins[i],Stroke(muted,context[i],1));}
   if(Overview)return;
   // The target: a soft glow on the near side, solid above the reference plane, dashed below it, fading with depth.
   var north=new OrbitStrokes[4];var south=new OrbitStrokes[4];for(int i=0;i<4;i++){north[i]=new OrbitStrokes();south[i]=new OrbitStrokes();}var glow=new OrbitStrokes();
   bool clipped=Walk(target,(a,b,p,q,depth)=>{int bin=Math.Max(0,Math.Min(3,(int)Math.Floor((depth/extent+1)/2*4)));((p.Z+q.Z)/2<0?south:north)[bin].Add(a,b);if(depth>=0)glow.Add(a,b);})||target.Clipped;
   Draw(dc,glow,Stroke(accent,0.14,6));double[] level={0.38,0.58,0.8,1};for(int i=0;i<4;i++){Draw(dc,south[i],Dashes(accent,level[i]*0.9,1.8));Draw(dc,north[i],Stroke(accent,level[i],2.2));}
   NotedClipped=clipped;
   // Nodes: where the path crosses the reference plane, filled when rising and hollow when falling.
   var points=target.Points;var rising=new StreamGeometry();var falling=new StreamGeometry();var risingContext=rising.Open();var fallingContext=falling.Open();bool anyRising=false,anyFalling=false;
   for(int k=1;k<=points.Length;k++){
    if(k==points.Length&&!target.Closed)break;var a=points[k-1];var b=points[k%points.Length];if(a.Z==0||b.Z==0||(a.Z<0)==(b.Z<0))continue;
    double t=a.Z/(a.Z-b.Z);var node=ProjectScene(new OrbitPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,0));if(!Finite(node)||!bounds.Contains(node))continue;
    bool up=b.Z>a.Z;var sink=up?risingContext:fallingContext;sink.BeginFigure(new Point(node.X,node.Y-3.5),true,true);sink.LineTo(new Point(node.X+3.5,node.Y),true,false);sink.LineTo(new Point(node.X,node.Y+3.5),true,false);sink.LineTo(new Point(node.X-3.5,node.Y),true,false);
    if(up)anyRising=true;else anyFalling=true;if(bounds.Width>=260)Ask(Localization.T(up?"Ascending node":"Descending node"),new Point(node.X+7,node.Y-8),6);
   }
   risingContext.Close();fallingContext.Close();rising.Freeze();falling.Freeze();var outline=Stroke(accent,0.9,1.2);if(anyRising)dc.DrawGeometry(Tint(accent,0.9),outline,rising);if(anyFalling)dc.DrawGeometry(Tint(surface,1),outline,falling);
   // Closest and, for closed orbits, farthest points; the far one is left out when it lies beyond the drawn part or on top of the near one.
   string near=record.central_body=="Sun"?"Perihelion":"Perigee",far=record.central_body=="Sun"?"Aphelion":"Apogee";
   var peri=ProjectScene(OrbitalModel.AtAnomaly(record,0));bool perihelion=Finite(peri)&&bounds.Contains(peri);
   if(perihelion){dc.DrawEllipse(surface,Stroke(accent,1,1.5),peri,3,3);Ask(Localization.T(near),new Point(peri.X+8,peri.Y+5),4);}
   if(record.IsClosed&&record.q*(1+record.e)/(1-record.e)<=target.Extent*1.0001){var apo=ProjectScene(OrbitalModel.AtAnomaly(record,Math.PI));if(Finite(apo)&&bounds.Contains(apo)&&(!perihelion||(apo-peri).Length>9)){dc.DrawEllipse(surface,Stroke(accent,1,1.5),apo,3,3);Ask(Localization.T(far),new Point(apo.X+8,apo.Y+5),4);}}
   // Open paths that run off the picture end in arrowheads; the direction of travel is shown at the target itself.
   Brush ink=Tint(accent,1);
   if(target.Clipped){Arrow(dc,ProjectScene(points[1]),ProjectScene(points[0]),ink);Arrow(dc,ProjectScene(points[points.Length-2]),ProjectScene(points.Last()),ink);}
  }
  bool NotedClipped;
  void Arrow(DrawingContext dc,Point a,Point b,Brush brush){
   if(!bounds.Contains(b))return;double x=b.X-a.X,y=b.Y-a.Y,length=Math.Sqrt(x*x+y*y);if(length<1e-8)return;x/=length;y/=length;
   var head=new StreamGeometry();using(var context=head.Open()){context.BeginFigure(b,true,true);context.LineTo(new Point(b.X-x*9-y*4.5,b.Y-y*9+x*4.5),true,false);context.LineTo(new Point(b.X-x*9+y*4.5,b.Y-y*9-x*4.5),true,false);}head.Freeze();dc.DrawGeometry(brush,null,head);
  }
  // Planets on the marker date, with a hairline down to the reference plane so their height reads.
  void Planets(DrawingContext dc){
   if(Bodies==null)return;Brush muted=Colour("Muted"),text=Colour("Text");var hair=Stroke(muted,0.35,0.8);
   foreach(var body in Bodies){
    var p=body.Value;var at=ProjectScene(p);if(!Finite(at)||!bounds.Contains(at))continue;var foot=ProjectScene(new OrbitPoint(p.X,p.Y,0));
    if(Finite(foot)&&(foot-at).Length>3)dc.DrawLine(hair,at,foot);
    double r=body.Key=="Jupiter"||body.Key=="Saturn"?3.6:3;dc.DrawEllipse(Solid(ColourOf(body.Key,Colors.Gray),1),Stroke(text,0.5,0.8),at,r,r);
    Ask(Body(body.Key),new Point(at.X+6,at.Y-18),5);
   }
  }
  // The central body, drawn like the sky globe's Sun and Moon: a soft glow and a disc.
  void Centre(DrawingContext dc){
   var middle=ProjectScene(new OrbitPoint());if(!Finite(middle))return;bool sun=Scene.Record.central_body=="Sun";Color light=sun?SunLight:EarthLight;
   if(sun){double halo=Math.Min(bounds.Width,bounds.Height)*0.45;var haze=new RadialGradientBrush(Color.FromArgb((byte)(dark?34:50),light.R,light.G,light.B),Color.FromArgb(0,light.R,light.G,light.B));haze.Freeze();dc.DrawEllipse(haze,null,middle,halo,halo);}
   if(!bounds.Contains(middle))return;
   var glow=new RadialGradientBrush(Color.FromArgb((byte)(sun?120:100),light.R,light.G,light.B),Color.FromArgb(0,light.R,light.G,light.B));glow.Freeze();dc.DrawEllipse(glow,null,middle,16,16);
   dc.DrawEllipse(Solid(light,1),dark?null:Stroke(Solid(Color.FromRgb(190,130,20),1),0.7,0.8),middle,sun?5:5.5,sun?5:5.5);Ask(Body(Scene.Record.central_body),new Point(middle.X+10,middle.Y-24),3);
  }
  // The target now: the stretch of orbit it has just travelled in bold, a guide line from the central body, a pulsing-style double ring with ticks like the sky globe's, and a dashed line down to the plane.
  void MarkerGlyph(DrawingContext dc){
   if(!Marker.HasValue||Overview)return;Brush accent=Colour("Accent"),surface=Colour("Surface");var p=Marker.Value;var at=ProjectScene(p);var foot=ProjectScene(new OrbitPoint(p.X,p.Y,0));if(!Finite(at)||!Finite(foot))return;
   heading=null;Trail(dc,accent,p);
   var middle=ProjectScene(new OrbitPoint());if(Finite(middle)){var m=middle;var t=at;if(VisibleLine(ref m,ref t,bounds))dc.DrawLine(Dashes(accent,0.35,0.8),m,t);}
   var a=at;var b=foot;if((foot-at).Length>3){if(VisibleLine(ref a,ref b,bounds))dc.DrawLine(Dashes(accent,0.55,1),a,b);if(bounds.Contains(foot))dc.DrawEllipse(null,Stroke(accent,0.55,1),foot,3.5,1.5);}
   if(!bounds.Contains(at))return;
   dc.DrawEllipse(Tint(accent,0.1),Stroke(accent,0.45,1),at,15,15);dc.DrawEllipse(Tint(accent,0.2),Stroke(accent,1,2),at,8.5,8.5);dc.DrawEllipse(accent,Stroke(surface,1,1),at,3.5,3.5);
   if(heading.HasValue){var d=heading.Value;d.Normalize();Arrow(dc,at,new Point(at.X+d.X*26,at.Y+d.Y*26),Tint(accent,1));}
   Ask(Catalog.DisplayId(Scene.Record.object_id),new Point(at.X+14,at.Y-30),0,true,true);
  }
  // The recent past of the orbit, ending at the target, drawn heavier than the rest so the direction and place of the target read at once.
  void Trail(DrawingContext dc,Brush accent,OrbitPoint p){
   var points=target.Points;int n=points.Length;if(n<12)return;int near=0;double best=double.MaxValue;
   for(int k=0;k<n;k++){double dx=points[k].X-p.X,dy=points[k].Y-p.Y,dz=points[k].Z-p.Z,d=dx*dx+dy*dy+dz*dz;if(d<best){best=d;near=k;}}
   if(best>Math.Pow(extent*0.08,2))return;int length=Math.Max(3,n/10);var line=new OrbitStrokes();var previous=ProjectScene(p);
   for(int step=1;step<=length;step++){int index=near-step;if(index<0){if(!target.Closed)break;index+=n;}var q=ProjectScene(points[index]);if(!Finite(q)||!Finite(previous))break;var x=previous;var y=q;if(VisibleLine(ref x,ref y,bounds))line.Add(x,y);previous=q;}
   Draw(dc,line,Stroke(accent,0.95,3.6));
   // Heading: from a point a little behind the target along the drawn path, so the arrow lies on the path itself.
   int back=near-Math.Max(2,length/4);if(back<0){if(!target.Closed)return;back+=n;}var behind=ProjectScene(points[back]);var here=ProjectScene(p);if(Finite(behind)&&Finite(here)&&(here-behind).Length>1)heading=here-behind;
  }
  Vector? heading;
  // Dates this target was imaged: small rings on the orbit. Marks that fall close together on screen are drawn once, with a count.
  // The marks as last drawn (grouped by screen position), so a click can find one; the picked group is kept by its first date.
  sealed class MarkGroup {public Point At;public List<ImagedMark> Marks;public string Key{get{return Marks[0].Utc.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}}}
  List<MarkGroup> drawnMarks=new List<MarkGroup>();string pickedMark;
  static string Dates(List<ImagedMark> marks){
   string first=marks[0].Utc.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),last=marks[marks.Count-1].Utc.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
   return marks.Count==1?first:Localization.F("{0} nights · {1} to {2}",marks.Count,first,last);
  }
  // A click on an imaged-date ring shows the date it was taken; clicking it again or anywhere else clears it.
  void ClickImaged(Point at){
   MarkGroup hit=null;double best=11;foreach(var group in drawnMarks){double d=(group.At-at).Length;if(d<best){best=d;hit=group;}}
   string next=hit==null||hit.Key==pickedMark?null:hit.Key;if(next!=pickedMark){pickedMark=next;InvalidateVisual();}
  }
  void ImagedMarks(DrawingContext dc){
   drawnMarks=new List<MarkGroup>();if(Imaged==null||Imaged.Count==0||Overview)return;Brush accent=Colour("Accent"),surface=Colour("Surface");
   double radius=Imaged.Count>40?24:12;
   foreach(var mark in Imaged.OrderBy(m=>m.Utc)){var at=ProjectScene(mark.Point);if(!Finite(at)||!bounds.Contains(at))continue;MarkGroup found=null;foreach(var group in drawnMarks)if((group.At-at).Length<radius){found=group;break;}if(found==null)drawnMarks.Add(new MarkGroup{At=at,Marks=new List<ImagedMark>{mark}});else found.Marks.Add(mark);}
   foreach(var group in drawnMarks){
    var at=group.At;int count=group.Marks.Count;bool picked=group.Key==pickedMark;double r=count>1?6:4;
    dc.DrawEllipse(picked?Tint(accent,0.9):Tint(surface,0.95),Stroke(accent,0.95,1.6),at,r,r);if(count==1&&!picked)dc.DrawEllipse(Tint(accent,0.9),null,at,1.3,1.3);
    if(picked)Ask(Dates(group.Marks),new Point(at.X+10,at.Y+6),0,true,true);
    else if(count>1)Ask("×"+count,new Point(at.X+8,at.Y-7),7);
   }
  }
  // Scale bar, marker date and notices, over everything else.
  void Overlays(DrawingContext dc,List<Rect> placed){
   Brush surface=Colour("Surface"),muted=Colour("Muted");
   double raw=64/scale,unit=OrbitMath.Kilometres(raw)?OrbitMath.AuKm:1,value=OrbitMath.NiceStep(raw*unit)/unit,pixels=Math.Max(10,Math.Min(bounds.Width/2,value*scale));var bar=Stroke(muted,1,1);
   dc.DrawLine(bar,new Point(12,ActualHeight-12),new Point(12+pixels,ActualHeight-12));dc.DrawLine(bar,new Point(12,ActualHeight-15),new Point(12,ActualHeight-9));dc.DrawLine(bar,new Point(12+pixels,ActualHeight-15),new Point(12+pixels,ActualHeight-9));
   var scaleLabel=Format(OrbitMath.Distance(value),muted,false);var scaleAt=new Point(12,ActualHeight-14-scaleLabel.Height);dc.DrawText(scaleLabel,scaleAt);placed.Add(new Rect(scaleAt.X-3,scaleAt.Y-2,Math.Max(pixels,scaleLabel.Width)+6,scaleLabel.Height+20));
   if(!string.IsNullOrEmpty(DateLabel))Ask(DateLabel,new Point(ActualWidth,ActualHeight),1,false,true);
   if(NotedClipped)Ask(Localization.T(Camera.Inner&&Scene.Record.central_body=="Sun"&&Scene.Record.q>40?"Perihelion beyond the inner solar system":"Extent clipped"),new Point(10,44),2,false,true);
  }
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);if(ActualWidth<20||ActualHeight<20)return;var frame=new Rect(RenderSize);
   dc.DrawRoundedRectangle(Colour("SurfaceAlt"),null,frame,8,8);
   if(Scene!=null){
    Prepare();NotedClipped=false;dc.PushClip(new RectangleGeometry(frame,8,8));
    Backdrop(dc);Grid(dc);Orbits(dc);ImagedMarks(dc);Planets(dc);Centre(dc);MarkerGlyph(dc);var placed=new List<Rect>();Overlays(dc,placed);Flush(dc,placed);dc.Pop();
   }
   // The frame is drawn last so the focus ring is never hidden by the scene.
   dc.DrawRoundedRectangle(null,new Pen(Colour(IsKeyboardFocused?"Accent":"Border"),IsKeyboardFocused?1.5:1),frame,8,8);
  }
  protected override void OnDpiChanged(DpiScale oldDpi,DpiScale newDpi){base.OnDpiChanged(oldDpi,newDpi);InvalidateVisual();}
 }
}
