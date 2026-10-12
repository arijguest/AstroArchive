using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  Frame previewSkyFrame;
  void InitializeSkyPreview(string prefix){
   InitializePreviewHeader(prefix);
   var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");var reset=B(prefix+"PreviewSkyResetButton");reset.Click+=(s,e)=>globe.ResetView();globe.SizeChanged+=(s,e)=>PositionSkyReset(prefix);globe.GlobeResized+=(s,e)=>PositionSkyReset(prefix);
   // The preview collapses and resizes the panel without always resizing the globe (it keeps
   // its last size while hidden), so the panel's own layout changes must reposition reset too.
   var panel=(Grid)Window.FindName(prefix+"PreviewSkyPanel");panel.SizeChanged+=(s,e)=>PositionSkyReset(prefix);panel.IsVisibleChanged+=(s,e)=>{if(panel.IsVisible)Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>PositionSkyReset(prefix)));};UiHelp.Hint(reset,"Reset sky view.");
   // Clicking the Moon, Sun or target opens Capture details, where their facts are listed.
   // Deferred so the click finishes before the light-dismiss popup takes the mouse.
   globe.DetailsRequested+=(s,e)=>Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(()=>{var popup=Window.FindName(prefix+"PreviewDetailsPopup") as System.Windows.Controls.Primitives.Popup;if(popup!=null&&globe.IsVisible)popup.IsOpen=true;}));
   UpdateCaptureSky(prefix,null);
  }
  void UpdateCaptureSky(string prefix,Frame frame){
   if(prefix.Length==0)previewSkyFrame=frame;
   var globe=Window.FindName(prefix+"PreviewSky") as SkyGlobeView;if(globe==null)return;var sky=CaptureSky.Resolve(StackWithSubframeClock(frame),settings);globe.SetContext(sky);if(prefix.Length==0)UpdateMoonDetails(sky,frame);
   var viewport=prefix.Length==0?previewViewport:editedPreviewViewport;if(viewport!=null)viewport.SetRemainderEnabled(!CaptureSky.IsCalibration(frame)&&!sky.BelowHorizon);PositionSkyReset(prefix);
   // Startup restores the window and preview size after this runs; place it again once layout settles.
   Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>PositionSkyReset(prefix)));
  }
  // Lunar and planetary stacks are often saved without a usable clock (a date only,
  // or a stacking time). Their subs record when the light arrived: use the median
  // sub time from the same session so the Moon or planet can still be placed.
  Frame StackWithSubframeClock(Frame frame){return StackWithSubframeClock(frame,all);}
  Frame StackWithSubframeClock(Frame frame,IList<Frame> pool){
   if(frame==null||frame.Kind!="Stack"||pool==null||CaptureSky.CaptureUtc(frame,settings).HasValue)return frame;
   string body=ObservationTargets.NamedSolar(frame.Target)??ObservationTargets.NamedSolar(ObservationTargets.ModeBody(frame.ObservationMode));if(body==null)return frame;
   string session=frame.SessionKey;var date=CaptureSessions.Date(frame);
   var times=pool.Where(f=>f!=null&&f.Kind=="Light"&&f.Status!="Deleted"&&string.Equals(f.Target,frame.Target,StringComparison.OrdinalIgnoreCase)&&(f.SessionKey==session||date!=null&&CaptureSessions.Date(f)!=null&&CaptureSessions.Date(f).Date==date.Date))
    .Select(f=>CaptureSky.CaptureUtc(f,settings)).Where(t=>t.HasValue).Select(t=>t.Value).OrderBy(t=>t).ToList();
   if(times.Count==0)return frame;var copy=frame.Clone();copy.ObservedUtc=times[times.Count/2].ToString("o",System.Globalization.CultureInfo.InvariantCulture);copy.Observed=copy.ObservedUtc;copy.TimeSource="Median subframe capture time (UTC)";copy.TimeZoneId="UTC";return copy;
  }
  sealed class EclipseSection {public string Key,Section;public EclipseInfo Eclipse;}
  readonly System.Runtime.CompilerServices.ConditionalWeakTable<Frame,EclipseSection> eclipseSections=new System.Runtime.CompilerServices.ConditionalWeakTable<Frame,EclipseSection>();
  // Sun and Moon captures taken during an eclipse list under their own section.
  // This is display only: the stored target, folders and file names do not change.
  // Safe to call from search workers: it reads frames and settings only.
  string TargetSection(Frame frame){return TargetSection(frame,all);}
  string TargetSection(Frame frame,IList<Frame> pool){var e=FrameEclipse(frame,pool);return e==null?frame.Target:e.Solar?Eclipses.SolarSection:Eclipses.LunarSection;}
  EclipseInfo FrameEclipse(Frame frame,IList<Frame> pool){
   if(frame==null||CaptureSky.IsCalibration(frame))return null;string body=ObservationTargets.NamedSolar(frame.Target)??(Catalog.IsAmbiguous(frame.Target)?ObservationTargets.NamedSolar(ObservationTargets.ModeBody(frame.ObservationMode)):null);if(body!="Sun"&&body!="Moon")return null;
   var current=settings;string key=string.Join("|",new object[]{frame.Target,frame.Kind,frame.Observed,frame.ObservedUtc,frame.TimeZoneId,frame.TimeSource,frame.Latitude,frame.Longitude,frame.Session,current.Latitude,current.Longitude,current.ObservingCity,current.ObservingCityId});
   EclipseSection cached;if(eclipseSections.TryGetValue(frame,out cached)&&cached.Key==key)return cached.Eclipse;
   var timed=StackWithSubframeClock(frame,pool);var utc=CaptureSky.CaptureUtc(timed,current);bool site=false;double latitude=0,longitude=0;
   if(frame.Latitude.HasValue&&frame.Longitude.HasValue&&ObservingCities.ValidCoordinates(frame.Latitude.Value,frame.Longitude.Value)){latitude=frame.Latitude.Value;longitude=frame.Longitude.Value;site=true;}
   else if(current.Latitude.HasValue&&current.Longitude.HasValue&&ObservingCities.ValidCoordinates(current.Latitude.Value,current.Longitude.Value)){latitude=current.Latitude.Value;longitude=current.Longitude.Value;site=true;}
   var eclipse=Eclipses.For(body,utc,site,latitude,longitude);eclipseSections.Remove(frame);eclipseSections.Add(frame,new EclipseSection{Key=key,Eclipse=eclipse});return eclipse;
  }
  void RefreshSkyPreviews(){UpdateCaptureSky("",previewSkyFrame);}
  void PositionSkyReset(string prefix){
   var panel=(Grid)Window.FindName(prefix+"PreviewSkyPanel");var globe=(SkyGlobeView)Window.FindName(prefix+"PreviewSky");
   // Before the panel is laid out its size is zero; a later layout pass places the button.
   if(!panel.IsVisible||panel.ActualWidth<=0||panel.ActualHeight<=0)return;
   SkyGlobeView.PlaceReset(B(prefix+"PreviewSkyResetButton"),globe,panel);
  }
  static Frame ReadSkyFrame(string path,string fallbackTarget=null){
   try{var info=Assets.Inspect(path);return CaptureSky.FromHeader(info.Header,info.Format,Path.GetFileName(path),fallbackTarget);}
   catch(Exception error){if(!(error is IOException||error is UnauthorizedAccessException||error is NotSupportedException||error is ArgumentException||error is OverflowException))throw;
    return new Frame{OriginalName=Path.GetFileName(path),Target=fallbackTarget??Catalog.TargetFromFilename(path)};
   }
  }
 }
}
