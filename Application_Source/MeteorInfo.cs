using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroArchive {
 // The year of annual meteor showers as a ridgeline: one row per shower, ordered by peak date.
 // A bar's length is the shower's usual dates, its height eases up to the peak and its tallest point is the
 // expected hourly rate from the observer's latitude (the zenithal rate when no location is set).
 public sealed class MeteorCalendar:FrameworkElement {
  const double HeaderHeight=16,RowHeight=24,LabelWidth=118,RateWidth=34,Gap=6;
  public static readonly DependencyProperty TextBrushProperty=DependencyProperty.Register("TextBrush",typeof(Brush),typeof(MeteorCalendar),new FrameworkPropertyMetadata(Brushes.Black,FrameworkPropertyMetadataOptions.AffectsRender));
  public static readonly DependencyProperty MutedBrushProperty=DependencyProperty.Register("MutedBrush",typeof(Brush),typeof(MeteorCalendar),new FrameworkPropertyMetadata(Brushes.Gray,FrameworkPropertyMetadataOptions.AffectsRender));
  public static readonly DependencyProperty AccentBrushProperty=DependencyProperty.Register("AccentBrush",typeof(Brush),typeof(MeteorCalendar),new FrameworkPropertyMetadata(Brushes.SteelBlue,FrameworkPropertyMetadataOptions.AffectsRender));
  public static readonly DependencyProperty BorderBrushLineProperty=DependencyProperty.Register("LineBrush",typeof(Brush),typeof(MeteorCalendar),new FrameworkPropertyMetadata(Brushes.LightGray,FrameworkPropertyMetadataOptions.AffectsRender));
  public double? Latitude{get;private set;}
  public DateTime Today{get;private set;}
  public string Place{get;private set;}
  readonly double[] peakMoon;
  public MeteorCalendar(double? latitude,string place,DateTime today){
   Latitude=latitude;Place=place;Today=today;SnapsToDevicePixels=true;
   SetResourceReference(TextBrushProperty,"Text");SetResourceReference(MutedBrushProperty,"Muted");SetResourceReference(AccentBrushProperty,"Accent");SetResourceReference(BorderBrushLineProperty,"Border");
   // The Moon's light at each peak this year, from the same ephemeris as Capture details; a bright Moon washes out the faint meteors.
   peakMoon=MeteorShowers.All.Select(s=>MoonEphemeris.At(new DateTime(today.Year,s.PeakMonth,s.PeakDay,0,0,0,DateTimeKind.Utc),false,0,0).Illumination).ToArray();
   AutomationProperties.SetName(this,"Meteor showers through the year");
   MouseMove+=(sender,e)=>{ToolTip=Describe(RowAt(e.GetPosition(this).Y));};
  }
  public static double ChartHeight(){return HeaderHeight+RowHeight*MeteorShowers.All.Length+4;}
  protected override Size MeasureOverride(Size available){return new Size(double.IsInfinity(available.Width)?360:available.Width,ChartHeight());}
  int RowAt(double y){int row=(int)Math.Floor((y-HeaderHeight)/RowHeight);return row<0||row>=MeteorShowers.All.Length?-1:row;}
  public double Rate(MeteorShower shower){return Latitude.HasValue?MeteorShowers.Visibility(shower,Latitude.Value).ExpectedRate:shower.Zhr;}
  public bool BrightMoon(int row){return peakMoon[row]>=0.6;}
  public string Describe(int row){
   if(row<0)return null;var s=MeteorShowers.All[row];string text=s.Name+" · "+s.Window+" · peak "+s.Peak+" · up to about "+s.Zhr.ToString("0",CultureInfo.InvariantCulture)+" an hour under dark skies";
   if(Latitude.HasValue){var v=MeteorShowers.Visibility(s,Latitude.Value);text+=v.Visible?" · radiant reaches "+v.MaxAltitude.ToString("0",CultureInfo.InvariantCulture)+"° high from "+Place+" (about "+v.ExpectedRate.ToString("0",CultureInfo.InvariantCulture)+" an hour)":" · the radiant stays below "+MeteorShowers.MinimumAltitude.ToString("0",CultureInfo.InvariantCulture)+"° from "+Place;}
   text+=" · Moon about "+(peakMoon[row]*100).ToString("0",CultureInfo.InvariantCulture)+"% lit at the peak"+(BrightMoon(row)?", which hides faint meteors":"");return text;
  }
  FormattedText Label(string text,Brush brush,double size,double maxWidth){
   var formatted=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection,new Typeface("Segoe UI"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);
   formatted.MaxTextWidth=Math.Max(1,maxWidth);formatted.MaxLineCount=1;formatted.Trimming=TextTrimming.CharacterEllipsis;return formatted;
  }
  static Brush Faded(Brush brush,double opacity){var copy=brush.CloneCurrentValue();copy.Opacity=opacity;copy.Freeze();return copy;}
  protected override void OnRender(DrawingContext dc){
   double width=ActualWidth,left=LabelWidth+Gap,right=width-RateWidth-Gap,plot=Math.Max(10,right-left);
   var text=(Brush)GetValue(TextBrushProperty);var muted=(Brush)GetValue(MutedBrushProperty);var accent=(Brush)GetValue(AccentBrushProperty);var line=(Brush)GetValue(BorderBrushLineProperty);
   dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,width,ChartHeight()));
   var grid=new Pen(Faded(line,0.7),1);grid.Freeze();var months=CultureInfo.CurrentUICulture.DateTimeFormat;
   for(int month=1;month<=12;month++){
    double x=left+plot*MeteorShowers.DayOfYear(month,1)/365.0-plot/365.0;double next=month==12?right:left+plot*(MeteorShowers.DayOfYear(month+1,1)-1)/365.0;
    dc.DrawLine(grid,new Point(x,HeaderHeight-2),new Point(x,ChartHeight()-4));
    var name=Label(months.GetAbbreviatedMonthName(month),muted,9,next-x-1);dc.DrawText(name,new Point(x+2,0));
   }
   double maxRate=MeteorShowers.All.Max(s=>Rate(s));if(maxRate<=0)maxRate=1;
   var bar=Faded(accent,0.78);var dim=Faded(muted,0.45);var dimLine=new Pen(Faded(muted,0.55),1.2){DashStyle=new DashStyle(new double[]{2,2},0)};dimLine.Freeze();
   var ringPen=new Pen(muted,1.1);ringPen.Freeze();
   for(int row=0;row<MeteorShowers.All.Length;row++){
    var shower=MeteorShowers.All[row];double top=HeaderHeight+row*RowHeight,baseline=top+RowHeight-3,rate=Rate(shower);bool visible=!Latitude.HasValue||MeteorShowers.Visibility(shower,Latitude.Value).Visible;
    if(row%2==1)dc.DrawRectangle(Faded(line,0.18),null,new Rect(0,top,width,RowHeight));
    dc.DrawText(Label(shower.Name,visible?text:muted,10.5,LabelWidth),new Point(0,top+4));
    var rateText=Label(visible?rate.ToString("0",CultureInfo.InvariantCulture):"–",visible?text:muted,10,RateWidth);dc.DrawText(rateText,new Point(width-rateText.Width,top+4));
    double peakHeight=visible?Math.Max(4,(RowHeight-6)*Math.Sqrt(rate/maxRate)):0;
    var geometry=new StreamGeometry();
    using(var context=geometry.Open()){
     bool open=false;double lastX=0;
     for(int day=1;day<=365;day++){
      double activity=MeteorShowers.Activity(shower,day);
      if(activity<=0){if(open){context.LineTo(new Point(lastX,baseline),false,false);open=false;}continue;}
      double x=left+plot*(day-0.5)/365.0,y=baseline-(visible?peakHeight*activity:2);
      if(!open){context.BeginFigure(new Point(x,baseline),true,true);open=true;}
      context.LineTo(new Point(x,y),true,false);lastX=x;
     }
     if(open)context.LineTo(new Point(lastX,baseline),false,false);
    }
    geometry.Freeze();dc.DrawGeometry(visible?bar:dim,null,geometry);
    if(!visible)dc.DrawLine(dimLine,new Point(left+plot*shower.StartDayOfYear/365.0,baseline),new Point(left+plot*(shower.WrapsYear?365:shower.EndDayOfYear)/365.0,baseline));
    if(visible){
     var centre=new Point(left+plot*(shower.PeakDayOfYear-0.5)/365.0,baseline-peakHeight);dc.DrawEllipse(text,null,centre,2.3,2.3);
     if(BrightMoon(row))dc.DrawEllipse(null,ringPen,centre,5,5);
    }
   }
   double todayX=left+plot*(MeteorShowers.DayOfYear(Today.Month,Math.Min(Today.Day,DateTime.DaysInMonth(2001,Today.Month)))-0.5)/365.0;
   var todayPen=new Pen(accent,1.4);todayPen.Freeze();dc.DrawLine(todayPen,new Point(todayX,HeaderHeight-2),new Point(todayX,ChartHeight()-4));
  }
 }
}
