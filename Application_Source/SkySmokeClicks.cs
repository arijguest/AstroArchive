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
    if(!view.EclipseDrawn)throw new Exception("Eclipsed Sun missing for the click check");var close=view.EclipseBounds;view.Tap(new Point(close.X+close.Width/2,close.Y+close.Height/2));if(requests!=3)throw new Exception("Clicking the eclipsed Sun did not ask for Capture details");
    // The main map opens the Capture details popup itself.
    GoToPage(0);var globe=(SkyGlobeView)Window.FindName("PreviewSky");var details=(Popup)Window.FindName("PreviewDetailsPopup");details.IsOpen=false;
    if(globe.IsVisible){globe.SetContext(CaptureSky.Resolve(sunFrame,null));globe.InvalidateVisual();PumpPopupLayout();if(globe.SunDrawn){var sun=globe.SunBounds;globe.Tap(new Point(sun.X+sun.Width/2,sun.Y+sun.Height/2));PumpPopupLayout();PumpPopupLayout();if(!details.IsOpen)throw new Exception("Clicking the Sun did not open Capture details");details.IsOpen=false;}}
   }finally{view.DetailsRequested-=counted;}
  }
  void SmokeEclipseFitsPreviews(string output){
   var previous=all;var oldFrame=previewSkyFrame;bool shown=settings.ShowPreview;
   try{
    settings.ShowPreview=true;SetPreviewVisibility();GoToPage(0);
    foreach(string body in new[]{"Sun","Moon"})foreach(string kind in new[]{"Light","Stack"}){
     string path=Path.Combine(output,"eclipse-preview-fixture",body+"-"+kind+".fits");RemoteSmokeFits(path,4000);
     var frame=new Frame{Hash=body+kind,Target=body,Kind=kind,OriginalName=kind=="Stack"?"Stack(1).fits":"Light.fits",Format="FITS",Images=null,ObservedUtc=body=="Sun"?"2024-04-08T18:17:16Z":"2025-03-14T06:58:43Z",Latitude=body=="Sun"?25.29:40.7,Longitude=body=="Sun"?-104.14:-74.0};
     all=new System.Collections.Generic.List<Frame>{frame};
     if(TargetSection(frame)!= (body=="Sun"?Eclipses.SolarSection:Eclipses.LunarSection))throw new Exception("Eclipse fixture was not grouped under its section");
     CancelPreview();LoadPreview(path,true,frame);
     WaitPreview(()=>((System.Windows.Controls.Image)Window.FindName("PreviewImage")).Source!=null||L("PreviewMessage").Text.StartsWith("Preview unavailable"),"Eclipse FITS preview did not finish");
     if(L("PreviewMessage").Text.StartsWith("Preview unavailable")||previewData==null||previewData.SourceWidth!=64)throw new Exception("Eclipse "+body+" "+kind+" preview failed: "+L("PreviewMessage").Text);
     Capture(Path.Combine(output,"AstroArchive_Eclipse_Preview_"+body+"_"+kind+".png"));
    }
    // A saved location replacement must update a preview that still holds a clone.
    var original=new Frame{Hash="town-preview",Target="Sun",Kind="Light",ObservedUtc="2024-04-08T18:17:16Z",Latitude=25.29,Longitude=-104.14};all=new System.Collections.Generic.List<Frame>{original};UpdateCaptureSky("",original.Clone());
    var changed=original.Clone();CaptureSite.Apply(changed,new ObservingCity{Id=3,Name="Tokyo",Latitude=35.7,Longitude=139.7});all[0]=changed;RefreshSkyPreviews();
    var sky=((SkyGlobeView)Window.FindName("PreviewSky")).Context;
    if(sky.Eclipse!=null||!sky.BodyBelowHorizon||sky.Longitude!=139.7||FrameEclipse(changed,all)!=null)throw new Exception("Saved town retained an old solar preview or eclipse section");
   }finally{CancelPreview();all=previous;settings.ShowPreview=shown;SetPreviewVisibility();UpdateCaptureSky("",oldFrame);}
  }
  // Eclipse captures show a cartoon, the coverage at capture, the event as a curve and its times.
  void SmokeEclipseDetails(string output){
   SmokeEclipseFitsPreviews(output);
   GoToPage(0);string theme=settings.ThemeMode;var details=(Popup)Window.FindName("PreviewDetailsPopup");var facts=(Grid)Window.FindName("SkyFacts");
   var cases=new[]{
    new{Name="Solar",Frame=new Frame{Target="Sun",Kind="Light",Observed="2024-04-08T18:10:00Z",TimeSource="FITS UTC",Latitude=25.29,Longitude=-104.14},Rows=new[]{"Maximum","Began","Ended","Duration","Totality"}},
    new{Name="Lunar",Frame=new Frame{Target="Moon",Kind="Light",Observed="2025-03-14T05:40:00Z",TimeSource="FITS UTC",Latitude=40.7,Longitude=-74.0},Rows=new[]{"Maximum","Began","Ended","Duration","Partial phase","Totality"}}};
   try{
    foreach(var c in cases){
     var sky=CaptureSky.Resolve(c.Frame,null);UpdateMoonDetails(sky,c.Frame);PumpPopupLayout();
     if(c.Name=="Solar"){
      var solarLabels=facts.Children.OfType<TextBlock>().Select(t=>t.Text).ToList();
      if(!solarLabels.Contains("Sun obscured")||!solarLabels.Contains(MoonEphemeris.Percent(sky.Eclipse.Obscuration))||facts.Children.OfType<Border>().Any(b=>b.Name=="EclipseCard")||solarLabels.Any(t=>c.Rows.Contains(t)))throw new Exception("Solar details are not limited to capture obscuration");
      foreach(string mode in new[]{"Dark","Light"}){Theme.Apply(Window,mode);details.IsOpen=true;PumpPopupLayout();SavePopup((FrameworkElement)details.Child,Path.Combine(output,"AstroArchive_Eclipse_Details_Solar_"+mode+".png"));details.IsOpen=false;}
      continue;
     }
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
