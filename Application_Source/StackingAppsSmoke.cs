using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeStackingApps(string output){
   string theme=settings.ThemeMode;int scale=settings.TextScalePercent;int launches=0;
   try{
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();
     string path=Path.Combine(Path.GetTempPath(),"Stacking export Ω with a long folder name", "M51 Whirlpool Galaxy", "Verified stacking inputs");StackingApplication? clicked=null;
     var dialog=ExportCompleteDialog(path,true,app=>{clicked=app;launches++;});
     try{
      dialog.Window.Show();PumpPopupLayout();VerifyWindowIcon(dialog.Window);if(dialog.Window.Owner!=Window)throw new Exception("Export popup lost its owner");
      var panel=PopupChildren<WrapPanel>(dialog.Window).Single();var buttons=panel.Children.OfType<Button>().ToList();if(buttons.Count!=3)throw new Exception("Stacking app choices are missing");
      foreach(StackingApplication app in Enum.GetValues(typeof(StackingApplication))){var button=buttons.Single(b=>Convert.ToString(b.Content)==StackingApps.Name(app));button.BringIntoView();PumpPopupLayout();if(!button.IsVisible||button.ActualWidth+button.Margin.Left+button.Margin.Right<button.DesiredSize.Width-0.5||button.ActualHeight+button.Margin.Top+button.Margin.Bottom<button.DesiredSize.Height-0.5)throw new Exception("Stacking app button is clipped at "+textScale+"% text");var bounds=button.TransformToAncestor(panel).TransformBounds(new Rect(button.RenderSize));if(bounds.Left<0||bounds.Right>panel.ActualWidth+0.5)throw new Exception("Stacking app row overflows at "+textScale+"% text");clicked=null;button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(clicked!=app||!dialog.Window.IsVisible)throw new Exception("Stacking app button dispatched the wrong app or closed the popup");}
      PopupChildren<ScrollViewer>(dialog.Window).First().ScrollToHome();PumpPopupLayout();SavePopup((FrameworkElement)dialog.Window.Content,Path.Combine(output,"AstroArchive_Stacking_Apps_"+mode+textScale+".png"));
     }finally{dialog.Window.Close();}
    }
    var ordinary=ExportCompleteDialog("Ordinary file export",false);try{ordinary.Window.Show();PumpPopupLayout();if(PopupChildren<WrapPanel>(ordinary.Window).Any())throw new Exception("Ordinary file export offered stacking apps");}finally{ordinary.Window.Close();}
    if(launches!=12)throw new Exception("Stacking app dispatch checks were skipped");File.WriteAllText(Path.Combine(output,"stacking-apps-smoke.txt"),"PASS stacking export completion buttons, dispatch, persistent popup, owner, long Unicode path and unclipped layout in light/dark at 100/150% text; ordinary exports stay concise.");
   }finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();}
  }
 }
}
