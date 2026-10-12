using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace AstroArchive {
 // A compact phase glyph oriented as the observer sees it: waxing light on the
 // right for northern sites, on the left for southern sites.
 public sealed class MoonPhaseIcon:FrameworkElement {
  public static readonly DependencyProperty IlluminationProperty=DependencyProperty.Register("Illumination",typeof(double),typeof(MoonPhaseIcon),new FrameworkPropertyMetadata(0.5,FrameworkPropertyMetadataOptions.AffectsRender));
  public static readonly DependencyProperty WaxingProperty=DependencyProperty.Register("Waxing",typeof(bool),typeof(MoonPhaseIcon),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.AffectsRender));
  public static readonly DependencyProperty SouthernProperty=DependencyProperty.Register("Southern",typeof(bool),typeof(MoonPhaseIcon),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
  public static readonly DependencyProperty LineBrushProperty=DependencyProperty.Register("LineBrush",typeof(Brush),typeof(MoonPhaseIcon),new FrameworkPropertyMetadata(Brushes.Gray,FrameworkPropertyMetadataOptions.AffectsRender));
  public static readonly DependencyProperty TextBrushProperty=DependencyProperty.Register("TextBrush",typeof(Brush),typeof(MoonPhaseIcon),new FrameworkPropertyMetadata(Brushes.Black,FrameworkPropertyMetadataOptions.AffectsRender));
  public double Illumination{get{return (double)GetValue(IlluminationProperty);}set{SetValue(IlluminationProperty,value);}}
  public bool Waxing{get{return (bool)GetValue(WaxingProperty);}set{SetValue(WaxingProperty,value);}}
  public bool Southern{get{return (bool)GetValue(SouthernProperty);}set{SetValue(SouthernProperty,value);}}
  public MoonPhaseIcon(){SetResourceReference(LineBrushProperty,"Muted");SetResourceReference(TextBrushProperty,"Text");SnapsToDevicePixels=true;}
  public void Show(MoonSnapshot moon){if(moon==null)return;Illumination=moon.Illumination;Waxing=moon.Waxing;Southern=moon.SouthernView;}
  public static double LimbAngle(bool waxing,bool southern){return waxing!=southern?0:180;}
  protected override void OnRender(DrawingContext dc){
   double size=Math.Max(2,Math.Min(ActualWidth,ActualHeight)/2-0.6);var point=new Point(ActualWidth/2,ActualHeight/2);
   var line=((Brush)GetValue(LineBrushProperty)).CloneCurrentValue();line.Opacity=0.55;line.Freeze();var outline=((Brush)GetValue(TextBrushProperty)).CloneCurrentValue();outline.Opacity=0.6;outline.Freeze();var pen=new Pen(outline,1);pen.Freeze();
   SkyGlobeView.DrawMoonDisc(dc,point,size,Illumination,LimbAngle(Waxing,Southern),line,pen,1);
  }
 }
 public partial class MainUi {
  static string Degrees(double value){return value.ToString("0",CultureInfo.InvariantCulture)+"°";}
  void SkyFact(Grid facts,string label,string value,UIElement icon=null){
   if(string.IsNullOrWhiteSpace(value))return;int row=facts.RowDefinitions.Count;facts.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var name=new TextBlock{Text=label,Margin=new Thickness(0,0,14,4)};name.SetResourceReference(TextBlock.ForegroundProperty,"Muted");name.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Grid.SetRow(name,row);
   var text=new TextBlock{Text=value,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};text.SetResourceReference(TextBlock.ForegroundProperty,"Text");text.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");
   FrameworkElement cell=text;if(icon!=null){var panel=new DockPanel{Margin=new Thickness(0,0,0,4)};DockPanel.SetDock(icon,Dock.Left);panel.Children.Add(icon);panel.Children.Add(text);cell=panel;}else text.Margin=new Thickness(0,0,0,4);
   Grid.SetRow(cell,row);Grid.SetColumn(cell,1);facts.Children.Add(name);facts.Children.Add(cell);
  }
  // Mirrors the globe: the same CaptureSky snapshot drives the drawn Moon and these
  // Capture details facts. Nothing is added to the default preview view.
  void UpdateMoonDetails(CaptureSky sky,Frame frame){
   var panel=(FrameworkElement)Window.FindName("SkyFactsPanel");var facts=(Grid)Window.FindName("SkyFacts");var note=L("SkyFactsNote");
   facts.Children.Clear();facts.RowDefinitions.Clear();note.Text="";
   bool relevant=frame!=null&&sky!=null&&!CaptureSky.IsCalibration(frame);var moon=relevant?sky.Moon:null;
   panel.Visibility=relevant&&(sky.HasPosition||moon!=null)?Visibility.Visible:Visibility.Collapsed;
   if(!relevant)return;
   if(sky.HasPosition&&sky.HasHorizon)SkyFact(facts,"Target elevation",(sky.ApproximatePosition?"≈ ":"")+CaptureSky.Elevation(sky.Altitude,sky.Azimuth));
   if(moon!=null){
    var icon=new MoonPhaseIcon{Width=14,Height=14,Margin=new Thickness(0,1,6,0),VerticalAlignment=VerticalAlignment.Center};icon.Show(moon);
    SkyFact(facts,"Moon",moon.PhaseName+" · "+moon.IlluminationText+" illuminated",icon);
    if(moon.HasHorizon)SkyFact(facts,"Moon elevation",CaptureSky.Elevation(moon.Altitude,moon.Azimuth));
    if(moon.HasTarget)SkyFact(facts,"Moon to target",(sky.ApproximatePosition?"≈ ":"")+Degrees(moon.TargetSeparation)+" apart");
   }
   note.Text=moon==null?"Moon details need a recorded capture time.":!moon.HasHorizon?"Choose an observing town / city in Settings > Sky & solving for elevations.":"Geometric elevations at the capture time; refraction excluded.";
  }
 }
}
