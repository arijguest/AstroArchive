using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  public void SmokePlanetarySky(string output){
   Directory.CreateDirectory(output);Window.Show();PumpPopupLayout();
   var globe=(SkyGlobeView)Window.FindName("PreviewSky");var previous=globe.Context;var savedFrame=previewSkyFrame;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   string theme=settings.ThemeMode;var popup=new FormWindow(Window,"Planetary sky check",520,620);var view=new SkyGlobeView{Height=370};popup.Add(view);
   try{
    popup.Window.Show();
    foreach(string mode in new[]{"Dark","Light"}){
     Theme.Apply(popup.Window,mode);
     foreach(string body in new[]{"Sun","Moon","Mercury","Venus","Mars","Jupiter","Saturn","Uranus","Neptune"}){
      // Retained AVI metadata with a real capture clock but no RA/Dec.
      var frame=new Frame{Target=body,Format="AVI",Kind="Video",OriginalName=body+".avi",ObservedUtc="2026-10-12T00:00:00Z"};var sky=CaptureSky.Resolve(frame,null);
      view.SetContext(sky);PumpPopupLayout();if(!sky.HasPosition||!sky.PositionOfDate||!view.TargetMarkerDrawn)throw new Exception("Timed AVI body has no target marker: "+body);
      // The home yaw must point at the same direction used to draw the marker.
      var direction=sky.TargetOnGlobe;double yaw=SkyOrientation.Wrap(Math.Atan2(direction.X,direction.Y)*180/Math.PI);
      if(Math.Abs(view.Camera.Yaw-yaw)>1e-8)throw new Exception("Planetary camera centres on the wrong epoch: "+body);
      if(body=="Moon"&&(!view.MoonDrawn||view.MoonBehindGlobe||sky.Moon.TargetSeparation>0.00001))throw new Exception("Moon target and phase disc do not coincide");
      int builds=view.DrawingBuilds;view.SetContext(CaptureSky.Resolve(frame,null));view.InvalidateVisual();PumpPopupLayout();if(view.DrawingBuilds!=builds)throw new Exception("Unchanged planet redrew its map");
      CapturePopup(popup.Window,Path.Combine(output,"AstroArchive_Planetary_Sky_"+body+"_"+mode+".png"));
     }
    }
    // A recorded site supplies the horizon; a new observing time changes it.
    var located=new Frame{Target="Jupiter",Format="AVI",Kind="Video",ObservedUtc="2026-10-12T00:00:00Z",Latitude=0,Longitude=0};var capture=CaptureSky.Resolve(located,null);
    located.Longitude=SkyOrientation.Wrap(capture.RA-SkyOrientation.Sidereal(capture.Utc.Value,0));if(located.Longitude>180)located.Longitude-=360;
    var overhead=CaptureSky.Resolve(located,null);view.SetContext(overhead);PumpPopupLayout();if(!overhead.HasHorizon||overhead.BelowHorizon||!view.TargetMarkerDrawn)throw new Exception("Planetary target did not render at a known observing site");
    var unknown=located.Clone();unknown.ObservedUtc=null;view.SetContext(CaptureSky.Resolve(unknown,null));PumpPopupLayout();if(view.Context.HasPosition||view.TargetMarkerDrawn)throw new Exception("Untimed AVI received a fabricated planetary position");
    var localClock=new Frame{Target="Jupiter",Format="AVI",Kind="Video",Observed="2026-10-12T11:00:00",TimeSource="Filename (timezone unknown)"};
    var city=new Settings{ObservingCityId=2147714,Latitude=-33.86785,Longitude=151.20732};var citySky=CaptureSky.Resolve(localClock,city);view.SetContext(citySky);PumpPopupLayout();
    if(citySky.Utc!=new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc)||!citySky.HasPosition||!citySky.HasHorizon||!view.TargetMarkerDrawn)throw new Exception("AVI observing-city timezone fallback did not position the target");
    var moonFrame=new Frame{Target="M31",Kind="Light",ObservedUtc="2026-10-12T00:00:00Z",Latitude=0,Longitude=0};
    var moonProbe=CaptureSky.Resolve(moonFrame,null);
    // Try opposite observing longitudes; one must place the Moon below the horizon.
    for(int longitude=-180;longitude<=180;longitude+=30){moonFrame.Longitude=longitude;moonProbe=CaptureSky.Resolve(moonFrame,null);if(moonProbe.Moon.Altitude<0)break;}
    UpdateMoonDetails(moonProbe,moonFrame);var moonFacts=((Grid)Window.FindName("SkyFacts")).Children;
    if(!moonFacts.OfType<TextBlock>().Any(t=>t.Text=="Not out (below the horizon)")||moonFacts.OfType<DockPanel>().Any())throw new Exception("Below-horizon Moon retained a phase glyph or illumination state");
    SmokeSkyClicks(popup.Window,view,output);SmokeEclipseDetails(output);
    File.WriteAllText(Path.Combine(output,"planetary-sky-smoke.txt"),"PASS timed AVI metadata locates all nine supported bodies; target marker/camera epoch; Moon disc coincidence; cached drawing; observer horizon; absent-clock state; observing-city timezone fallback; constellation names and body clicks; eclipse details card; light/dark screenshots.");
   }finally{popup.Window.Close();globe.SetContext(previous);previewSkyFrame=savedFrame;UpdateMoonDetails(previous,savedFrame);Theme.Apply(Window,theme);GoToPage(page);}
  }
 }
}
