using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AstroArchive {
 // Target overlay: thin white circles that cover each catalogue object, with the object's ID set into a break in the ring.
 public partial class PreviewViewport {
  List<TargetMark> targetMarks;string targetNote;Action targetAction;string targetActionLabel;Button targetToggle;
  int targetRings;readonly List<Rect> targetLabels=new List<Rect>();
  public const double MinimumTargetRadius=8;
  // Null marks hide the overlay. A note without marks explains why nothing is circled; an action adds a button to it.
  public void SetTargets(IList<TargetMark> marks,string note,Action action=null,string actionLabel=null){targetMarks=marks==null?null:new List<TargetMark>(marks);targetNote=marks==null?null:note;targetAction=marks==null?null:action;targetActionLabel=actionLabel;DrawTargets();}
  public int TargetCircles{get{return targetRings;}}
  public IList<Rect> TargetLabelBounds{get{return targetLabels.ToList();}}
  public string TargetNoteText{get{var note=overlayLayer.Children.OfType<Border>().FirstOrDefault();if(note==null)return null;var text=((Panel)note.Child).Children.OfType<TextBlock>().FirstOrDefault();return text==null?null:text.Text;}}
  public Button TargetNoteButton{get{var note=overlayLayer.Children.OfType<Border>().FirstOrDefault();return note==null?null:((Panel)note.Child).Children.OfType<Button>().FirstOrDefault();}}
  // Large preview windows carry their own switch in the zoom toolbar; it changes the same setting as the sidebar buttons.
  public void ShowTargetToggle(Action toggle){
   if(targetToggle!=null)return;controls.Children.Add(new Border{Width=1,Height=20,Background=new SolidColorBrush(Color.FromArgb(150,148,165,192)),Margin=new Thickness(5,0,5,0)});
   targetToggle=AddButton(controls,"◎","Show targets in frame",toggle);
  }
  public void SetTargetToggle(bool on){if(targetToggle==null)return;targetToggle.Background=on?new SolidColorBrush(Color.FromArgb(235,88,82,214)):new SolidColorBrush(Color.FromArgb(220,20,29,46));AutomationProperties.SetItemStatus(targetToggle,Localization.T(on?"On":"Off"));UiHelp.Hint(targetToggle,Localization.T(TargetOverlay.Tooltip(on)));}
  public Button TargetToggleButton{get{return targetToggle;}}

  sealed class Ring {public TargetMark Mark;public Point Centre;public double Radius;}
  void DrawTargets(){
   overlayLayer.Children.Clear();targetRings=0;targetLabels.Clear();
   bool show=targetMarks!=null&&geometry!=null&&media==null&&image.Source!=null&&image.Width>0&&image.Height>0;overlayLayer.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(!show)return;
   double width=viewport.Width,height=viewport.Height;overlayLayer.Width=width;overlayLayer.Height=height;var view=new Rect(0,0,width,height);var placed=new List<Rect>();
   if(!string.IsNullOrEmpty(targetNote)){var note=Note(Localization.T(targetNote),width);overlayLayer.Children.Add(note);note.Measure(new Size(Math.Max(0,width-16),double.PositiveInfinity));Canvas.SetLeft(note,8);Canvas.SetTop(note,8);placed.Add(new Rect(8,8,note.DesiredSize.Width,note.DesiredSize.Height));}
   double scale=zoom.Scale;var rings=new List<Ring>();
   foreach(var mark in targetMarks){
    Point centre=transform.Matrix.Transform(new Point(mark.X*image.Width,mark.Y*image.Height));double radius=Math.Max(MinimumTargetRadius,(mark.RadiusX*image.Width+mark.RadiusY*image.Height)/2*scale);
    if(RingVisible(centre,radius,view))rings.Add(new Ring{Mark=mark,Centre=centre,Radius=radius});
   }
   var ink=viewport.TryFindResource("TargetMark") as Brush??Brushes.White;var shade=new SolidColorBrush(Color.FromArgb(150,0,0,0));
   object size=viewport.TryFindResource("UiFontSmall");double fontSize=(size is double)?Math.Max(10,(double)size+1):12;
   var typeface=new Typeface(TextElement.GetFontFamily(viewport),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);double dip=VisualTreeHelper.GetDpi(viewport).PixelsPerDip;
   var thin=new GeometryGroup();var bold=new GeometryGroup();var text=new GeometryGroup{FillRule=FillRule.Nonzero};
   // Rings in priority order claim label space first; every visible ring is drawn whether or not its label fits.
   foreach(var ring in rings){
    var label=TargetLabel.Build(ring.Mark.Label,typeface,fontSize,dip);
    Geometry outline;Rect bounds;Geometry glyphs;
    if(TargetLabel.PlaceOnRing(label,ring.Centre,ring.Radius,view,placed,out outline,out glyphs,out bounds)||TargetLabel.PlaceBeside(label,ring.Centre,ring.Radius,view,placed,out outline,out glyphs,out bounds)){placed.Add(bounds);targetLabels.Add(bounds);text.Children.Add(glyphs);}
    else outline=new EllipseGeometry(ring.Centre,ring.Radius,ring.Radius);
    (ring.Mark.Primary?bold:thin).Children.Add(outline);targetRings++;
   }
   foreach(var path in new[]{
    new Path{Data=thin,Stroke=shade,StrokeThickness=3},new Path{Data=bold,Stroke=shade,StrokeThickness=3.6},new Path{Data=text,Stroke=shade,StrokeThickness=2.6,StrokeLineJoin=PenLineJoin.Round},
    new Path{Data=thin,Stroke=ink,StrokeThickness=1.1},new Path{Data=bold,Stroke=ink,StrokeThickness=1.7},new Path{Data=text,Fill=ink}}){path.IsHitTestVisible=false;overlayLayer.Children.Add(path);}
   // Keep the note above the rings.
   var first=overlayLayer.Children.OfType<Border>().FirstOrDefault();if(first!=null){overlayLayer.Children.Remove(first);overlayLayer.Children.Add(first);}
  }
  // A ring shows when part of its circumference crosses the view: not wholly outside it, and not so large that the view sits inside the circle.
  internal static bool RingVisible(Point centre,double radius,Rect view){
   double nearX=Math.Max(view.Left,Math.Min(centre.X,view.Right)),nearY=Math.Max(view.Top,Math.Min(centre.Y,view.Bottom)),near=Math.Sqrt((nearX-centre.X)*(nearX-centre.X)+(nearY-centre.Y)*(nearY-centre.Y));
   double farX=Math.Max(Math.Abs(view.Left-centre.X),Math.Abs(view.Right-centre.X)),farY=Math.Max(Math.Abs(view.Top-centre.Y),Math.Abs(view.Bottom-centre.Y));
   return near<=radius&&Math.Sqrt(farX*farX+farY*farY)>=radius;
  }
  Border Note(string message,double width){
   var panel=new StackPanel();var text=new TextBlock{Text=message,TextWrapping=TextWrapping.Wrap,MaxWidth=Math.Max(60,Math.Min(280,width-28))};text.SetResourceReference(TextBlock.ForegroundProperty,"Text");text.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");panel.Children.Add(text);
   if(targetAction!=null&&!string.IsNullOrEmpty(targetActionLabel)){var action=targetAction;var button=new Button{Content=Localization.T(targetActionLabel),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,6,0,0),Padding=new Thickness(10,3,10,3),MinHeight=0};AutomationProperties.SetName(button,Localization.T(targetActionLabel));button.Click+=(s,e)=>{action();e.Handled=true;};panel.Children.Add(button);}
   var chip=new Border{Child=panel,Padding=new Thickness(8,5,8,6),CornerRadius=new CornerRadius(5),BorderThickness=new Thickness(1)};chip.SetResourceReference(Border.BackgroundProperty,"Surface");chip.SetResourceReference(Border.BorderBrushProperty,"Border");return chip;
  }
 }
 // Lays a short label along a ring, upright and reading left to right, in a gap cut from the ring where the gap is on screen.
 public sealed class TargetLabel {
  public string Text;public double FontSize;public double Width,Height;public List<Geometry> Glyphs=new List<Geometry>();public List<double> Advances=new List<double>();public double Middle;
  public static TargetLabel Build(string text,Typeface typeface,double fontSize,double dip){
   var label=new TargetLabel{Text=text,FontSize=fontSize};
   foreach(char c in text){var formatted=new FormattedText(c.ToString(),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,typeface,fontSize,Brushes.White,dip);label.Glyphs.Add(formatted.BuildGeometry(new Point(0,0)));label.Advances.Add(formatted.WidthIncludingTrailingWhitespace);label.Height=Math.Max(label.Height,formatted.Height);label.Middle=formatted.Baseline-fontSize*0.36;}
   label.Width=label.Advances.Sum();return label;
  }
  const double Inset=2,Gap=4;
  static Point On(Point centre,double radius,double angle){return new Point(centre.X+radius*Math.Cos(angle),centre.Y+radius*Math.Sin(angle));}
  // Angles are screen angles (y down): -π/2 is the top of the ring. Candidates start at the top and widen alternately both ways.
  static IEnumerable<double> Candidates(double radius){
   double step=Math.Max(Math.PI/90,Math.Min(Math.PI/12,26/radius));yield return -Math.PI/2;
   for(double offset=step;offset<=Math.PI+1e-9;offset+=step){yield return -Math.PI/2+offset;yield return -Math.PI/2-offset;}
  }
  public static bool PlaceOnRing(TargetLabel label,Point centre,double radius,Rect view,List<Rect> placed,out Geometry ring,out Geometry glyphs,out Rect bounds){
   ring=null;glyphs=null;bounds=Rect.Empty;double span=(label.Width+2*Gap)/radius;
   // Small rings cannot carry text along their edge without it curling; those labels sit beside the ring instead.
   if(span>Math.PI*0.75||radius<label.Height*1.6)return false;
   foreach(double angle in Candidates(radius)){
    bool below=Math.Sin(angle)>0.25;var group=new GeometryGroup{FillRule=FillRule.Nonzero};var box=Rect.Empty;bool inside=true;double run=0;
    for(int i=0;i<label.Glyphs.Count;i++){
     double offset=(run+label.Advances[i]/2-label.Width/2)/radius;run+=label.Advances[i];
     double at=below?angle-offset:angle+offset;Point p=On(centre,radius,at);
     double reach=label.FontSize*0.62;var glyphBox=new Rect(p.X-reach,p.Y-reach,2*reach,2*reach);if(!Contains(view,glyphBox)){inside=false;break;}box.Union(glyphBox);
     var transform=new TransformGroup();transform.Children.Add(new TranslateTransform(-label.Advances[i]/2,-label.Middle));transform.Children.Add(new RotateTransform(at*180/Math.PI+(below?-90:90)));transform.Children.Add(new TranslateTransform(p.X,p.Y));
     var glyph=label.Glyphs[i].Clone();glyph.Transform=transform;group.Children.Add(glyph);
    }
    if(!inside||placed.Any(r=>r.IntersectsWith(box)))continue;
    ring=Arc(centre,radius,angle,span/2);glyphs=group;bounds=box;return true;
   }
   return false;
  }
  // Beside a small ring: the label sits just outside a short break, on the first side with room.
  public static bool PlaceBeside(TargetLabel label,Point centre,double radius,Rect view,List<Rect> placed,out Geometry ring,out Geometry glyphs,out Rect bounds){
   ring=null;glyphs=null;bounds=Rect.Empty;
   foreach(double degrees in new[]{-45.0,-135,45,135,-90,90,0,180}){
    double angle=degrees*Math.PI/180,cos=Math.Cos(angle),sin=Math.Sin(angle);Point edge=On(centre,radius+3,angle);
    double left=cos>0.3?edge.X:cos< -0.3?edge.X-label.Width:edge.X-label.Width/2,top=sin>0.3?edge.Y:sin< -0.3?edge.Y-label.Height:edge.Y-label.Height/2;
    var box=new Rect(left,top,label.Width,label.Height);if(!Contains(view,box)||placed.Any(r=>r.IntersectsWith(box)))continue;
    var group=new GeometryGroup{FillRule=FillRule.Nonzero};double x=left;
    for(int i=0;i<label.Glyphs.Count;i++){var glyph=label.Glyphs[i].Clone();glyph.Transform=new TranslateTransform(x,top);group.Children.Add(glyph);x+=label.Advances[i];}
    ring=Arc(centre,radius,angle,Math.PI/7);glyphs=group;bounds=box;return true;
   }
   return false;
  }
  static bool Contains(Rect view,Rect box){return box.Left>=view.Left+Inset&&box.Top>=view.Top+Inset&&box.Right<=view.Right-Inset&&box.Bottom<=view.Bottom-Inset;}
  // The ring minus a break of half-width `half` (radians) centred on `angle`.
  public static Geometry Arc(Point centre,double radius,double angle,double half){
   double start=angle+half,end=angle-half+2*Math.PI;
   var figure=new PathFigure{StartPoint=On(centre,radius,start),IsClosed=false,IsFilled=false};
   figure.Segments.Add(new ArcSegment(On(centre,radius,end),new Size(radius,radius),0,end-start>Math.PI,SweepDirection.Clockwise,true));
   var path=new PathGeometry();path.Figures.Add(figure);return path;
  }
 }
}
