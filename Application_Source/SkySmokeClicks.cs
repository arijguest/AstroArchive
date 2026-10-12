using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AstroArchive {
 public partial class MainUi {
  // Clicking a constellation names it until the next click or Escape; clicking a body asks for Capture details.
  void SmokeSkyClicks(Window window,SkyGlobeView view,string output){
   var frame=new Frame{Target="M42",Kind="Light",ObservedUtc="2026-01-15T22:00:00Z",Latitude=51.5,Longitude=0};view.SetContext(CaptureSky.Resolve(frame,null));view.ResetView();PumpPopupLayout();
   int requests=0;EventHandler counted=(s,e)=>requests++;view.DetailsRequested+=counted;
   try{
    int orion=Array.FindIndex(SkyFigures.Figures,f=>f.Name=="Orion");var probe=view.FigureProbe(orion);if(double.IsNaN(probe.X))throw new Exception("Orion is not on the near side for the click check");
    if(view.FigureAt(probe)!=orion)throw new Exception("Clicking an Orion line found another constellation");
    view.Tap(probe);view.InvalidateVisual();PumpPopupLayout();
    if(view.SelectedConstellation!="Orion"||view.ConstellationLabelBounds.IsEmpty||!new Rect(view.RenderSize).Contains(view.ConstellationLabelBounds))throw new Exception("Clicked constellation was not named inside the map");
    if(requests!=0)throw new Exception("A constellation click opened Capture details");
    CapturePopup(window,Path.Combine(output,"AstroArchive_Sky_Constellation_Name.png"));
    view.Tap(probe);PumpPopupLayout();if(view.SelectedConstellation!=null)throw new Exception("Clicking the named constellation again did not dismiss its name");
    view.Tap(probe);view.InvalidateVisual();PumpPopupLayout();var label=view.ConstellationLabelBounds;view.Tap(new Point(label.X+label.Width/2,label.Y+label.Height/2));PumpPopupLayout();if(view.SelectedConstellation!=null)throw new Exception("Clicking the name did not dismiss it");
    view.Tap(probe);PumpPopupLayout();view.Focus();var escape=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(view),0,Key.Escape){RoutedEvent=Keyboard.KeyDownEvent};view.RaiseEvent(escape);if(!escape.Handled||view.SelectedConstellation!=null)throw new Exception("Escape did not dismiss the constellation name");
    view.Tap(probe);PumpPopupLayout();view.SetContext(CaptureSky.Resolve(new Frame{Target="M31",Kind="Light",ObservedUtc="2026-01-15T22:00:00Z",Latitude=51.5,Longitude=0},null));if(view.SelectedConstellation!=null)throw new Exception("A new capture kept the previous constellation name");
    view.SetContext(CaptureSky.Resolve(frame,null));view.ResetView();view.InvalidateVisual();PumpPopupLayout();
    if(!view.TargetMarkerDrawn)throw new Exception("Target marker missing for the click check");
    var target=view.TargetBounds;view.Tap(new Point(target.X+target.Width/2,target.Y+target.Height/2));if(requests!=1||view.SelectedConstellation!=null)throw new Exception("Clicking the target did not ask for Capture details");
    var moonFrame=new Frame{Target="Moon",Kind="Light",ObservedUtc="2026-01-15T22:00:00Z"};view.SetContext(CaptureSky.Resolve(moonFrame,null));view.ResetView();view.InvalidateVisual();PumpPopupLayout();
    if(!view.MoonDrawn)throw new Exception("Moon missing for the click check");var moon=view.MoonBounds;view.Tap(new Point(moon.X+moon.Width/2,moon.Y+moon.Height/2));if(requests!=2)throw new Exception("Clicking the Moon did not ask for Capture details");
    var sunFrame=new Frame{Target="Sun",Kind="Light",Observed="2024-04-08T18:17:16Z",TimeSource="FITS UTC",Latitude=25.29,Longitude=-104.14};view.SetContext(CaptureSky.Resolve(sunFrame,null));view.ResetView();view.InvalidateVisual();PumpPopupLayout();
    if(!view.EclipseDrawn)throw new Exception("Eclipse close-up missing for the click check");var close=view.EclipseBounds;view.Tap(new Point(close.X+close.Width/2,close.Y+close.Height/2));if(requests!=3)throw new Exception("Clicking the eclipse close-up did not ask for Capture details");
    // The main map opens the Capture details popup itself.
    GoToPage(0);var globe=(SkyGlobeView)Window.FindName("PreviewSky");var details=(Popup)Window.FindName("PreviewDetailsPopup");details.IsOpen=false;
    if(globe.IsVisible){globe.SetContext(CaptureSky.Resolve(sunFrame,null));globe.InvalidateVisual();PumpPopupLayout();if(globe.SunDrawn){var sun=globe.SunBounds;globe.Tap(new Point(sun.X+sun.Width/2,sun.Y+sun.Height/2));PumpPopupLayout();PumpPopupLayout();if(!details.IsOpen)throw new Exception("Clicking the Sun did not open Capture details");details.IsOpen=false;}}
   }finally{view.DetailsRequested-=counted;}
  }
  // Eclipse captures show a cartoon, the coverage at capture, the event as a curve and its times.
  void SmokeEclipseDetails(string output){
   GoToPage(0);string theme=settings.ThemeMode;var details=(Popup)Window.FindName("PreviewDetailsPopup");var facts=(Grid)Window.FindName("SkyFacts");
   var cases=new[]{
    new{Name="Solar",Frame=new Frame{Target="Sun",Kind="Light",Observed="2024-04-08T18:10:00Z",TimeSource="FITS UTC",Latitude=25.29,Longitude=-104.14},Rows=new[]{"Maximum","Began","Ended","Duration","Totality"}},
    new{Name="Lunar",Frame=new Frame{Target="Moon",Kind="Light",Observed="2025-03-14T05:40:00Z",TimeSource="FITS UTC",Latitude=40.7,Longitude=-74.0},Rows=new[]{"Maximum","Began","Ended","Duration","Partial phase","Totality"}}};
   try{
    foreach(var c in cases){
     var sky=CaptureSky.Resolve(c.Frame,null);UpdateMoonDetails(sky,c.Frame);PumpPopupLayout();
     var card=facts.Children.OfType<Border>().FirstOrDefault(b=>b.Name=="EclipseCard");if(card==null)throw new Exception(c.Name+" eclipse card missing from Capture details");
     var parts=((Grid)card.Child).Children;var icon=parts.OfType<EclipseIcon>().FirstOrDefault();var curve=parts.OfType<EclipseCurve>().FirstOrDefault();
     if(icon==null||icon.Info!=sky.Eclipse||curve==null||curve.Event==null||curve.Event.Solar!=sky.Eclipse.Solar)throw new Exception(c.Name+" eclipse card lacks its cartoon or coverage curve");
     var labels=facts.Children.OfType<TextBlock>().Select(t=>t.Text).ToList();var missing=c.Rows.Where(r=>!labels.Contains(r)).ToList();if(missing.Count>0)throw new Exception(c.Name+" eclipse details omit: "+string.Join(", ",missing));
     foreach(string mode in new[]{"Dark","Light"}){Theme.Apply(Window,mode);details.IsOpen=true;PumpPopupLayout();SavePopup((FrameworkElement)details.Child,Path.Combine(output,"AstroArchive_Eclipse_Details_"+c.Name+"_"+mode+".png"));details.IsOpen=false;}
    }
   }finally{details.IsOpen=false;Theme.Apply(Window,theme);}
  }
 }
}
