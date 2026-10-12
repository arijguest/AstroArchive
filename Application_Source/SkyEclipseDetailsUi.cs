using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroArchive {
 // A cartoon of the eclipse at the capture time, drawn like the Moon phase glyph: the
 // Sun with the Moon's silhouette across it, or the Moon with Earth's shadow on it.
 // Sizes and separation are true to scale; the direction is a fixed, readable diagonal.
 public sealed class EclipseIcon:FrameworkElement {
  public static readonly DependencyProperty TextBrushProperty=DependencyProperty.Register("TextBrush",typeof(Brush),typeof(EclipseIcon),new FrameworkPropertyMetadata(Brushes.Black,FrameworkPropertyMetadataOptions.AffectsRender));
  EclipseInfo info;
  public EclipseInfo Info{get{return info;}}
  public EclipseIcon(){SetResourceReference(TextBrushProperty,"Text");SnapsToDevicePixels=true;}
  public void Show(EclipseInfo eclipse){info=eclipse;InvalidateVisual();}
  static Brush Fill(Color colour){var brush=new SolidColorBrush(colour);brush.Freeze();return brush;}
  static Pen Line(Color colour,double width){var pen=new Pen(Fill(colour),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();return pen;}
  internal static readonly Color SunFill=Color.FromRgb(255,206,84),SunEdge=Color.FromRgb(232,148,36),MoonShadow=Color.FromRgb(38,44,62),Umbra=Color.FromArgb(215,168,62,36);
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);if(info==null)return;double side=Math.Min(ActualWidth,ActualHeight);if(side<8)return;var centre=new Point(ActualWidth/2,ActualHeight/2);
   double stroke=Math.Max(1,side*0.04);var outline=((Brush)GetValue(TextBrushProperty)).CloneCurrentValue();outline.Opacity=0.55;outline.Freeze();var edge=new Pen(outline,stroke);edge.Freeze();
   if(info.Solar){
    double sun=side*0.3,scale=sun/info.SunRadiusArcmin,moon=info.MoonRadiusArcmin*scale,offset=info.SeparationArcmin*scale/Math.Sqrt(2);
    var rays=Line(SunEdge,stroke*1.4);for(int i=0;i<12;i++){double a=i*Math.PI/6+Math.PI/12;dc.DrawLine(rays,new Point(centre.X+Math.Cos(a)*sun*1.22,centre.Y+Math.Sin(a)*sun*1.22),new Point(centre.X+Math.Cos(a)*sun*1.55,centre.Y+Math.Sin(a)*sun*1.55));}
    dc.DrawEllipse(Fill(SunFill),Line(SunEdge,stroke),centre,sun,sun);
    // The Moon arrives from the upper right; its silhouette is clipped to the sky around the Sun.
    var moonCentre=new Point(centre.X+offset,centre.Y-offset);dc.PushClip(new EllipseGeometry(centre,sun*1.6,sun*1.6));
    dc.DrawEllipse(Fill(MoonShadow),edge,moonCentre,moon,moon);dc.Pop();
    if(info.Kind=="Total"){var corona=new RadialGradientBrush();corona.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,240,200),0.6));corona.GradientStops.Add(new GradientStop(Color.FromArgb(150,255,240,200),0.72));corona.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,240,200),1));corona.Freeze();dc.DrawEllipse(corona,null,centre,sun*1.5,sun*1.5);}
   }else{
    double moon=side*0.42,scale=moon/info.MoonRadiusArcmin,offset=info.SeparationArcmin*scale;
    // Earth's shadow arrives from the left.
    var shadowCentre=new Point(centre.X-offset,centre.Y);var disc=new EllipseGeometry(centre,moon,moon);
    dc.DrawEllipse(Fill(SkyGlobeView.MoonLight),null,centre,moon,moon);
    var crater=Fill(Color.FromArgb(70,150,130,90));dc.DrawEllipse(crater,null,new Point(centre.X-moon*0.3,centre.Y-moon*0.28),moon*0.2,moon*0.2);dc.DrawEllipse(crater,null,new Point(centre.X+moon*0.32,centre.Y+moon*0.1),moon*0.14,moon*0.14);dc.DrawEllipse(crater,null,new Point(centre.X-moon*0.05,centre.Y+moon*0.42),moon*0.11,moon*0.11);
    dc.PushClip(disc);
    dc.DrawEllipse(Fill(Color.FromArgb(95,70,70,90)),null,shadowCentre,info.PenumbraRadiusArcmin*scale,info.PenumbraRadiusArcmin*scale);
    dc.DrawEllipse(Fill(Umbra),null,shadowCentre,info.ShadowRadiusArcmin*scale,info.ShadowRadiusArcmin*scale);
    dc.Pop();dc.DrawEllipse(null,edge,centre,moon,moon);
   }
  }
 }
 // How much was covered from the first contact to the last, with the capture marked.
 public sealed class EclipseCurve:FrameworkElement {
  public static readonly DependencyProperty LineBrushProperty=Palette("LineBrush"),AccentBrushProperty=Palette("AccentBrush"),SurfaceBrushProperty=Palette("SurfaceBrush");
  static DependencyProperty Palette(string name){return DependencyProperty.Register(name,typeof(Brush),typeof(EclipseCurve),new FrameworkPropertyMetadata(Brushes.Gray,FrameworkPropertyMetadataOptions.AffectsRender));}
  public EclipseCurve(){SetResourceReference(LineBrushProperty,"Muted");SetResourceReference(AccentBrushProperty,"Accent");SetResourceReference(SurfaceBrushProperty,"Surface");}
  EclipseEvent eclipse;DateTime capture;
  public EclipseEvent Event{get{return eclipse;}}
  public void Show(EclipseEvent value,DateTime utc){eclipse=value;capture=utc;InvalidateVisual();}
  // Point on the curve at a fraction of the event, for drawing and checks.
  internal double Value(double[] curve,double position){if(curve==null||curve.Length<2)return 0;double x=position*(curve.Length-1);int i=Math.Min(curve.Length-2,(int)Math.Floor(x));return curve[i]+(curve[i+1]-curve[i])*(x-i);}
  Geometry Area(double[] curve,Rect plot){
   var geometry=new StreamGeometry();using(var g=geometry.Open()){g.BeginFigure(new Point(plot.Left,plot.Bottom),true,true);for(int i=0;i<curve.Length;i++)g.LineTo(new Point(plot.Left+plot.Width*i/(curve.Length-1),plot.Bottom-plot.Height*curve[i]),true,true);g.LineTo(new Point(plot.Right,plot.Bottom),true,false);}
   geometry.Freeze();return geometry;
  }
  static Brush Tint(object brush,double opacity){var copy=((Brush)brush??Brushes.Gray).CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();return copy;}
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);if(eclipse==null||ActualWidth<20||ActualHeight<10)return;var plot=new Rect(4,5,ActualWidth-8,ActualHeight-7);
   object muted=GetValue(LineBrushProperty),accent=GetValue(AccentBrushProperty);
   dc.DrawRoundedRectangle(Tint(muted,0.08),null,new Rect(0,0,ActualWidth,ActualHeight),4,4);
   // Totality (or annularity) as a band, so a short central phase is still visible.
   if(eclipse.CentralStart.HasValue&&eclipse.CentralEnd.HasValue){double a=eclipse.Position(eclipse.CentralStart.Value),b=eclipse.Position(eclipse.CentralEnd.Value);dc.DrawRectangle(Tint(muted,0.16),null,new Rect(plot.Left+plot.Width*a,plot.Top,Math.Max(1.5,plot.Width*(b-a)),plot.Height));}
   if(!eclipse.Solar)dc.DrawGeometry(Tint(muted,0.22),null,Area(eclipse.PenumbralCurve,plot));
   var colour=eclipse.Solar?EclipseIcon.SunEdge:Color.FromRgb(196,84,52);
   var fill=new SolidColorBrush(Color.FromArgb(110,colour.R,colour.G,colour.B));fill.Freeze();var line=new Pen(new SolidColorBrush(colour),1.4);line.Freeze();
   dc.DrawGeometry(fill,line,Area(eclipse.Curve,plot));
   var baseline=new Pen(Tint(muted,0.5),1);baseline.Freeze();dc.DrawLine(baseline,new Point(plot.Left,plot.Bottom),new Point(plot.Right,plot.Bottom));
   double x=eclipse.Position(capture),value=Value(eclipse.Curve,x);if(value<=0&&!eclipse.Solar)value=Value(eclipse.PenumbralCurve,x);
   var marker=new Point(plot.Left+plot.Width*x,plot.Bottom-plot.Height*value);var pen=new Pen(Tint(accent,0.9),1.5);pen.Freeze();
   dc.DrawLine(pen,new Point(marker.X,plot.Top-3),new Point(marker.X,plot.Bottom));dc.DrawEllipse(Tint(accent,1),new Pen(Tint(GetValue(SurfaceBrushProperty),1),1.2),marker,3.6,3.6);
  }
 }
 public partial class MainUi {
  // Eclipse times follow the capture's own timezone, then the observing town's, then UTC.
  TimeZoneInfo EclipseZone(Frame frame){
   if(frame!=null&&!string.IsNullOrEmpty(frame.TimeZoneId)&&!string.Equals(frame.TimeZoneId,"UTC",StringComparison.OrdinalIgnoreCase))try{return TimeZoneInfo.FindSystemTimeZoneById(frame.TimeZoneId);}catch(TimeZoneNotFoundException){}catch(InvalidTimeZoneException){}
   if(settings!=null&&settings.ObservingCityId.HasValue)try{var zone=CityTimeZones.ForCity(settings.ObservingCityId.Value);if(zone!=null)return zone;}catch(Exception error){if(!(error is TimeZoneNotFoundException||error is InvalidTimeZoneException||error is System.IO.InvalidDataException))throw;}
   return TimeZoneInfo.Utc;
  }
  internal static string ZoneLabel(TimeZoneInfo zone,DateTime utc){
   var offset=zone.GetUtcOffset(utc);if(offset==TimeSpan.Zero)return "UTC";
   return "UTC"+(offset<TimeSpan.Zero?"−":"+")+Math.Abs(offset.Hours).ToString(CultureInfo.InvariantCulture)+(offset.Minutes!=0?":"+Math.Abs(offset.Minutes).ToString("00",CultureInfo.InvariantCulture):"");
  }
  internal static string ClockTime(DateTime utc,TimeZoneInfo zone){return TimeZoneInfo.ConvertTimeFromUtc(utc,zone).ToString("HH:mm",CultureInfo.InvariantCulture);}
  internal static string Span(DateTime from,DateTime to,TimeZoneInfo zone){return ClockTime(from,zone)+" – "+ClockTime(to,zone)+" · "+TargetNavigation.Exposure(Math.Round((to-from).TotalSeconds));}
  static TextBlock EclipseText(string text,string size,string colour,FontWeight weight){var block=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontWeight=weight};block.SetResourceReference(TextBlock.FontSizeProperty,size);block.SetResourceReference(TextBlock.ForegroundProperty,colour);return block;}
  // A card at the top of Sky at capture: cartoon, coverage at capture and the whole event
  // as a curve, followed by its times as ordinary facts.
  void EclipseDetails(Grid facts,CaptureSky sky,Frame frame){
   var at=sky.Eclipse;var utc=sky.Utc.Value;var e=Eclipses.Event(at,utc,sky.Latitude,sky.Longitude);
   if(e==null){SkyFact(facts,at.Title,at.Summary);return;}
   var zone=EclipseZone(frame);
   var card=new Border{CornerRadius=new CornerRadius(6),Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,0,0,8)};card.SetResourceReference(Border.BackgroundProperty,"SurfaceAlt");
   var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
   layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var icon=new EclipseIcon{Width=54,Height=54,Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center};icon.Show(at);AutomationProperties.SetName(icon,at.Title+": "+at.Summary);layout.Children.Add(icon);
   bool umbral=at.Solar||at.Obscuration>0;double covered=umbral?at.Obscuration:at.PenumbralObscuration;
   var heading=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(heading,1);
   heading.Children.Add(EclipseText(e.Title,"UiFontBody","Text",FontWeights.SemiBold));
   var percent=EclipseText(MoonEphemeris.Percent(covered),"UiFontTitle","Text",FontWeights.SemiBold);percent.Name="EclipseCoverage";percent.Margin=new Thickness(0,1,0,0);heading.Children.Add(percent);
   heading.Children.Add(EclipseText(at.Solar?"Sun covered at capture":umbral?"Moon in Earth's shadow at capture":"Moon in Earth's outer shadow at capture","UiFontSmall","Muted",FontWeights.Normal));
   layout.Children.Add(heading);
   var curve=new EclipseCurve{Height=40,Margin=new Thickness(0,8,0,0)};curve.Show(e,utc);Grid.SetRow(curve,1);Grid.SetColumnSpan(curve,2);
   curve.ToolTip="Coverage from first to last contact. The marker shows the capture.";AutomationProperties.SetName(curve,"Eclipse coverage over time");layout.Children.Add(curve);
   var ends=new Grid{Margin=new Thickness(0,2,0,0)};Grid.SetRow(ends,2);Grid.SetColumnSpan(ends,2);
   var first=EclipseText(ClockTime(e.Start,zone),"UiFontCaption","Muted",FontWeights.Normal);var last=EclipseText(ClockTime(e.End,zone),"UiFontCaption","Muted",FontWeights.Normal);last.HorizontalAlignment=HorizontalAlignment.Right;
   var zoneText=EclipseText(ZoneLabel(zone,e.Greatest),"UiFontCaption","Muted",FontWeights.Normal);zoneText.HorizontalAlignment=HorizontalAlignment.Center;
   ends.Children.Add(first);ends.Children.Add(zoneText);ends.Children.Add(last);layout.Children.Add(ends);
   card.Child=layout;card.Name="EclipseCard";
   int row=facts.RowDefinitions.Count;facts.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(card,row);Grid.SetColumnSpan(card,2);facts.Children.Add(card);
   bool umbralEvent=e.Solar||e.Peak.Obscuration>0;
   SkyFact(facts,"Maximum",MoonEphemeris.Percent(umbralEvent?e.Peak.Obscuration:e.Peak.PenumbralObscuration)+" at "+ClockTime(e.Greatest,zone));
   SkyFact(facts,"Began",ClockTime(e.Start,zone));SkyFact(facts,"Ended",ClockTime(e.End,zone));
   SkyFact(facts,"Duration",TargetNavigation.Exposure(Math.Round(e.Duration.TotalSeconds)));
   if(e.PartialStart.HasValue)SkyFact(facts,"Partial phase",Span(e.PartialStart.Value,e.PartialEnd.Value,zone));
   if(e.CentralStart.HasValue)SkyFact(facts,e.Solar&&e.Kind=="Annular"?"Annularity":"Totality",Span(e.CentralStart.Value,e.CentralEnd.Value,zone));
  }
 }
}
