using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace AstroArchive {
 // An orthographic 3D sphere drawn as one cached WPF drawing, without a 3D scene,
 // animation timer, web view or individual UI elements for stars and line segments.
 public sealed class SkyGlobeView:FrameworkElement {
  public static readonly DependencyProperty LineBrushProperty=Palette("LineBrush");
  public static readonly DependencyProperty TextBrushProperty=Palette("TextBrush");
  public static readonly DependencyProperty AccentBrushProperty=Palette("AccentBrush");
  public static readonly DependencyProperty SurfaceBrushProperty=Palette("SurfaceBrush");
  public static readonly DependencyProperty FontSizeProperty=DependencyProperty.Register("FontSize",typeof(double),typeof(SkyGlobeView),new FrameworkPropertyMetadata(10.0,FrameworkPropertyMetadataOptions.AffectsRender));
  static DependencyProperty Palette(string name){return DependencyProperty.Register(name,typeof(Brush),typeof(SkyGlobeView),new FrameworkPropertyMetadata(Brushes.Gray,FrameworkPropertyMetadataOptions.AffectsRender));}
  public double FontSize{get{return (double)GetValue(FontSizeProperty);}set{SetValue(FontSizeProperty,value);}}
  CaptureSky context;SkyVector[] stars,labels;DrawingGroup drawing;double lastWidth,lastHeight,lastFont;object[] palette;
  SkyVector right,up,front;double radius;Point centre;internal int DrawingBuilds;
  public CaptureSky Context{get{return context;}}
  public SkyGlobeView(){
   SetResourceReference(LineBrushProperty,"Muted");SetResourceReference(TextBrushProperty,"Text");SetResourceReference(AccentBrushProperty,"Accent");SetResourceReference(SurfaceBrushProperty,"SurfaceAlt");
   ClipToBounds=true;SetContext(CaptureSky.Resolve(null,null));
  }
  public void SetContext(CaptureSky value){
   value=value??CaptureSky.Resolve(null,null);bool changed=context==null||context.Key!=value.Key;context=value;
   ToolTip=value.Evidence;AutomationProperties.SetName(this,"Capture sky. "+value.TargetLabel+". "+value.TimeLabel+". "+value.Summary);
   if(!changed)return;stars=SkyFigures.Stars.Select(value.Orientation.Map).ToArray();labels=SkyFigures.Figures.Select(f=>value.Orientation.Map(f.Label)).ToArray();drawing=null;InvalidateVisual();
  }
  Point Project(SkyVector vector){return new Point(centre.X+radius*vector.Dot(right),centre.Y-radius*vector.Dot(up));}
  static Pen Stroke(Brush brush,double opacity,double width){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();var pen=new Pen(copy,width);pen.Freeze();return pen;}
  static Brush Tint(Brush brush,double opacity){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();return copy;}
  FormattedText Text(string value,Brush brush){return new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),FontSize,brush);}
  Rect Label(DrawingContext dc,string value,SkyVector vector,Brush brush,List<Rect> occupied,bool required){
   var point=Project(vector);var text=Text(value,brush);var box=new Rect(point.X-text.Width/2,point.Y-text.Height/2,text.Width,text.Height);box.Inflate(3,2);
   if(box.Left<0||box.Right>ActualWidth||box.Top<0||box.Bottom>ActualHeight||!required&&occupied.Any(b=>b.IntersectsWith(box)))return Rect.Empty;
   dc.DrawText(text,new Point(point.X-text.Width/2,point.Y-text.Height/2));occupied.Add(box);return box;
  }
  void Ring(DrawingContext dc,double altitude,Pen visible,Pen faint){
   var previous=SkyVector.Horizontal(0,altitude);for(int angle=5;angle<=360;angle+=5){var next=SkyVector.Horizontal(angle,altitude);dc.DrawLine((previous.Dot(front)+next.Dot(front))/2>=0?visible:faint,Project(previous),Project(next));previous=next;}
  }
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);if(ActualWidth<50||ActualHeight<50)return;
   var colours=new[]{GetValue(LineBrushProperty),GetValue(TextBrushProperty),GetValue(AccentBrushProperty),GetValue(SurfaceBrushProperty)};
   if(drawing==null||lastWidth!=ActualWidth||lastHeight!=ActualHeight||lastFont!=FontSize||palette==null||colours.Where((c,i)=>!ReferenceEquals(c,palette[i])).Any()){
    lastWidth=ActualWidth;lastHeight=ActualHeight;lastFont=FontSize;palette=colours;drawing=Build((Brush)colours[0],(Brush)colours[1],(Brush)colours[2],(Brush)colours[3]);DrawingBuilds++;
   }dc.DrawDrawing(drawing);
  }
  DrawingGroup Build(Brush line,Brush text,Brush accent,Brush surface){
   var group=new DrawingGroup();centre=new Point(ActualWidth/2,ActualHeight/2);radius=Math.Max(5,Math.Min(ActualWidth/2-25,ActualHeight/2-20));
   var target=context.Orientation.Map(SkyVector.Equatorial(context.RA,context.Dec));double yaw=context.HasPosition?Math.Atan2(target.X,target.Y):Math.PI,tilt=(context.HasHorizon&&context.Altitude<0?-25:25)*Math.PI/180;
   right=new SkyVector(Math.Cos(yaw),-Math.Sin(yaw),0);up=new SkyVector(-Math.Sin(yaw)*Math.Sin(tilt),-Math.Cos(yaw)*Math.Sin(tilt),Math.Cos(tilt));front=new SkyVector(Math.Sin(yaw)*Math.Cos(tilt),Math.Cos(yaw)*Math.Cos(tilt),Math.Sin(tilt));
   Pen horizon=Stroke(line,0.85,1),grid=Stroke(line,0.22,0.7),rear=Stroke(line,0.10,0.7),below=Stroke(line,0.13,0.7),figures=Stroke(line,0.60,0.8),near=Stroke(line,0.9,1);var occupied=new List<Rect>();
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
    if(context.HasHorizon){
     var names=new[]{"N","E","S","W"};for(int i=0;i<4;i++){var vector=SkyVector.Horizontal(i*90,0);vector=new SkyVector(vector.X*1.15,vector.Y*1.15,0);Label(dc,names[i],vector,text,occupied,true);}
    }else{Label(dc,"NCP",new SkyVector(0,0,1.12),text,occupied,true);Label(dc,"SCP",new SkyVector(0,0,-1.12),text,occupied,true);}
    if(context.HasPosition){
     var point=Project(target);occupied.Add(new Rect(point.X-15,point.Y-15,30,30));dc.DrawEllipse(Tint(accent,0.12),Stroke(accent,1,1.5),point,6,6);dc.DrawEllipse(accent,null,point,2,2);
     dc.DrawLine(Stroke(accent,0.8,1),new Point(point.X-10,point.Y),new Point(point.X-7,point.Y));dc.DrawLine(Stroke(accent,0.8,1),new Point(point.X+7,point.Y),new Point(point.X+10,point.Y));
    }
    var chosen=new HashSet<string>();int count=0;
    foreach(int i in Enumerable.Range(0,labels.Length).OrderByDescending(i=>context.HasPosition?labels[i].Dot(target):labels[i].Dot(front))){
     if(labels[i].Dot(front)<0.12||context.HasHorizon&&labels[i].Z<-0.1||chosen.Contains(SkyFigures.Figures[i].Name))continue;
     var box=Label(dc,SkyFigures.Figures[i].Name,labels[i],line,occupied,false);if(!box.IsEmpty){chosen.Add(SkyFigures.Figures[i].Name);if(++count==6)break;}
    }
   }group.Freeze();return group;
  }
 }
}
