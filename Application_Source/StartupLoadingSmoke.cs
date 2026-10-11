using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  public void SmokeStartup(string output){
   Directory.CreateDirectory(output);Window.Show();PumpPopupLayout();
   foreach(bool contrast in new[]{false,true}){
    var view=new StartupLoadingWindow(contrast);
    try{
     view.Window.Show();view.SetProgress(65,"Opening your archive…");PumpPopupLayout();
     VerifyWindowIcon(view.Window);
     string version=((TextBlock)view.Window.FindName("VersionLabel")).Text;
     if(version!="v"+FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion)throw new Exception("Splash version does not match the executable.");
     var bar=(ProgressBar)view.Window.FindName("LoadingProgress");view.SetProgress(40,"Opening your archive…");if(bar.Value!=65||bar.IsIndeterminate)throw new Exception("Startup progress moved backwards or became indeterminate.");
     string remark=((TextBlock)view.Window.FindName("RemarkLabel")).Text;view.Rotate();if(remark==((TextBlock)view.Window.FindName("RemarkLabel")).Text)throw new Exception("Startup remark did not rotate.");
     string note=((TextBlock)view.Window.FindName("NoteHeading")).Text;view.Rotate();if(note==((TextBlock)view.Window.FindName("NoteHeading")).Text)throw new Exception("Hints and facts did not alternate.");
     if(AutomationProperties.GetName(bar)!="Application startup progress")throw new Exception("Startup progress is not accessible.");
     CapturePopup(view.Window,Path.Combine(output,contrast?"startup-high-contrast.png":"startup-rosette.png"));
     view.Window.Width=640;view.Window.Height=430;PumpPopupLayout();CapturePopup(view.Window,Path.Combine(output,contrast?"startup-high-contrast-small.png":"startup-rosette-small.png"));
     view.SetProgress(100,"Your workspace is ready.");if(bar.Value!=100)throw new Exception("Startup never completed.");
    }finally{view.Window.Close();}
   }
   // Block this UI thread as real startup does; the splash must still rotate.
   using(var splash=new StartupSplash()){
    if(!splash.IsAlive)throw new Exception("The independent splash dispatcher failed.");
    string before=null;splash.Inspect(v=>before=((TextBlock)v.Window.FindName("RemarkLabel")).Text);
    Thread.Sleep(4300);
    splash.Inspect(v=>{if(before==((TextBlock)v.Window.FindName("RemarkLabel")).Text)throw new Exception("The splash froze while the application thread was busy.");});
    splash.Report(95,"Preparing the first view…");splash.Complete();
   }
   using(var splash=new StartupSplash()){
    splash.Inspect(v=>v.Window.Close());bool cancelled=false;try{splash.Report(40,"Building your workspace…");}catch(OperationCanceledException){cancelled=true;}
    if(!cancelled||!splash.Cancelled)throw new Exception("Closing the splash did not cancel startup.");
   }
   SmokeWindowPlacement(output);
   File.WriteAllText(Path.Combine(output,"startup-ui-passed.txt"),"PASS: branded splash, actual version, progress, rotating content, responsive dispatcher, cancellation, high contrast and window placement.");
  }
  void SmokeWindowPlacement(string output){
   string path=Path.Combine(output,"placement-test","window-placement.json");if(File.Exists(path))File.Delete(path);
   var area=SystemParameters.WorkArea;var first=new Window{Title="Placement fixture",Width=Math.Min(980,area.Width-80),Height=Math.Min(700,area.Height-80),Left=area.Left+40,Top=area.Top+40,WindowStartupLocation=WindowStartupLocation.Manual};
   SavedWindowPlacement normal;
   using(var placement=new MainWindowPlacement(first,path)){
    first.Show();PumpPopupLayout();placement.Save();normal=Util.Deserialize<SavedWindowPlacement>(File.ReadAllText(path));first.Close();
   }
   var reopened=new Window{Title="Reopened fixture",Width=500,Height=400};
   using(var placement=new MainWindowPlacement(reopened,path)){
    reopened.Show();PumpPopupLayout();placement.Save();var restored=Util.Deserialize<SavedWindowPlacement>(File.ReadAllText(path));
    if(restored.Left!=normal.Left||restored.Top!=normal.Top||restored.Width!=normal.Width||restored.Height!=normal.Height||reopened.WindowState!=WindowState.Normal)throw new Exception("Normal window placement was not restored.");
    reopened.WindowState=WindowState.Maximized;PumpPopupLayout();placement.Save();reopened.WindowState=WindowState.Minimized;PumpPopupLayout();placement.Save();
    if(!Util.Deserialize<SavedWindowPlacement>(File.ReadAllText(path)).Maximized)throw new Exception("Minimizing lost the maximized state.");reopened.Close();
   }
   var maximized=new Window{Title="Update restart fixture",Width=500,Height=400};
   using(var placement=new MainWindowPlacement(maximized,path)){
    maximized.Show();PumpPopupLayout();if(maximized.WindowState!=WindowState.Maximized)throw new Exception("Maximized state was not restored after reopening.");
    maximized.WindowState=WindowState.Normal;PumpPopupLayout();placement.Save();var restored=Util.Deserialize<SavedWindowPlacement>(File.ReadAllText(path));
    if(restored.Width!=normal.Width||restored.Height!=normal.Height)throw new Exception("Maximize changed the normal restore size.");maximized.Close();
   }
   normal.Left=90000;normal.Top=-90000;normal.Monitor="disconnected-monitor";normal.Width=9000;normal.Height=9000;normal.Maximized=false;Util.AtomicText(path,Util.Serialize(normal));
   var moved=new Window{Title="Disconnected monitor fixture",Width=500,Height=400};
   using(var placement=new MainWindowPlacement(moved,path)){
    moved.Show();PumpPopupLayout();var bounds=new Rect(moved.Left,moved.Top,moved.ActualWidth,moved.ActualHeight);
    if(!area.Contains(bounds))throw new Exception("Window on a disconnected monitor was not returned to the work area.");moved.Close();
   }
   File.WriteAllText(path,"{broken json");var fallback=new Window{Width=500,Height=400};using(var placement=new MainWindowPlacement(fallback,path)){fallback.Show();PumpPopupLayout();fallback.Close();}
  }
 }
}
