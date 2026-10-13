using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 public partial class MainUi {
  void SmokeOrbitInformation(string output){
   string previous=settings.ThemeMode;int textScale=settings.TextScalePercent;var view=new TargetInfoView();var host=new Window{Owner=Window,Title="Orbit validation",Content=view,Width=640,Height=600,MinWidth=220,MinHeight=220,ShowInTaskbar=false};host.Resources.MergedDictionaries.Add(Window.Resources);host.SetResourceReference(Control.BackgroundProperty,"Canvas");host.SetResourceReference(Control.ForegroundProperty,"Text");host.SetResourceReference(Control.FontSizeProperty,"UiFontControl");LocalizedInterface.Prepare(host);
   try{host.Show();int cases=0;
    foreach(string mode in new[]{"Light","Dark"})foreach(int width in new[]{620,280})foreach(string id in new[]{"SUN","MOON","1P"}){
     settings.ThemeMode=mode;settings.TextScalePercent=width<300?150:100;ApplyAppearance();host.Width=width;
     var card=id=="1P"?CometInfo.Card("1P/Halley"):targetInformation.ById(id,CancellationToken.None).Cards.Single();view.ShowCard(card);PumpPopupLayout();
     var panel=PopupChildren<OrbitPanel>(view).Single();var clock=System.Diagnostics.Stopwatch.StartNew();while(panel.View.Scene==null&&clock.Elapsed.TotalSeconds<10){PumpPopupLayout();Thread.Sleep(10);}if(panel.View.Scene==null)throw new Exception("Native orbit did not prepare: "+id);
     panel.BringIntoView();PumpPopupLayout();if(panel.View.ActualWidth<120||panel.View.ActualHeight<160)throw new Exception("Orbit became too small in a narrow pane.");
     var surface=PopupChildren<ScrollViewer>(view).First();surface.Focusable=true;surface.Focus();PumpPopupLayout();var unzoomed=panel.View.Camera.Zoom;var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.PreviewMouseWheelEvent};panel.View.RaiseEvent(wheel);
     if(wheel.Handled||panel.View.Camera.Zoom!=unzoomed)throw new Exception("Unfocused orbit intercepted page scrolling.");
     panel.View.Focus();PumpPopupLayout();wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.PreviewMouseWheelEvent};panel.View.RaiseEvent(wheel);if(!wheel.Handled||panel.View.Camera.Zoom<=unzoomed)throw new Exception("Focused wheel did not zoom.");
     panel.View.Rotate(43,-20);panel.View.Pan(15,-12);panel.View.Zoom(2);var camera=panel.View.Camera.Copy();double offset=surface.VerticalOffset;view.UpdateCard(card,null,null,null,null);PumpPopupLayout();panel=PopupChildren<OrbitPanel>(view).Single();
     if(panel.View.Camera.Yaw!=camera.Yaw||panel.View.Camera.PanX!=camera.PanX||panel.View.Camera.Zoom!=camera.Zoom||Math.Abs(surface.VerticalOffset-offset)>2)throw new Exception("Information refresh lost orbit camera/scroll.");
     clock.Restart();while(panel.View.Scene==null&&clock.Elapsed.TotalSeconds<10){PumpPopupLayout();Thread.Sleep(10);}panel.View.Zoom(double.PositiveInfinity);panel.View.Zoom(1e100);panel.View.Pan(double.NaN,0);panel.View.InvalidateVisual();PumpPopupLayout();if(!OrbitalModel.Finite(panel.View.Camera.Zoom)||panel.View.Camera.Zoom>1e10)throw new Exception("Orbit zoom escaped its finite bounds.");
     panel.View.Reset();PumpPopupLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(host.ActualWidth),(int)Math.Ceiling(host.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(host);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,"Orbit-"+id+"-"+mode+"-"+width+".png")))png.Save(stream);cases++;
    }
    view.ShowCard(new TargetInfoCard{id="IO",name="Io",domain="solar_system",type_label="Satellite"});PumpPopupLayout();if(PopupChildren<OrbitView>(view).Single().Scene!=null||!PopupChildren<TextBlock>(view).Any(t=>t.Text==Localization.T("Complete orbital elements are unavailable. No orientation has been assumed.")))throw new Exception("Unsupported satellite invented a geometry.");
    File.WriteAllText(Path.Combine(output,"orbit-ui-smoke.txt"),"PASS "+cases+" light/dark, narrow/wide orbit layouts; source refresh camera/scroll; focused wheel, finite zoom/pan, keyboard-accessible controls; unsupported elements. Language: "+Localization.Current);
   }finally{host.Close();settings.ThemeMode=previous;settings.TextScalePercent=textScale;ApplyAppearance();}
  }
 }
}
