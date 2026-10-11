using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace AstroArchive {
 // User-local state deliberately lives outside versioned installation folders
 // and repository settings, so updates and recovered archives retain it.
 public sealed class SavedWindowPlacement {
  public int Left{get;set;}public int Top{get;set;}public int Width{get;set;}public int Height{get;set;}
  public string Monitor{get;set;}public int WorkOffsetX{get;set;}public int WorkOffsetY{get;set;}
  public bool Maximized{get;set;}
 }
 internal sealed class MainWindowPlacement:IDisposable {
  readonly Window window;readonly string path;readonly DispatcherTimer saveTimer;
  bool restoring,loaded,lastMaximized;SavedWindowPlacement saved;
  public MainWindowPlacement(Window window,string path){
   this.window=window;this.path=path;
   try{if(File.Exists(path)){var candidate=Util.Deserialize<SavedWindowPlacement>(File.ReadAllText(path));if(Valid(candidate))saved=candidate;}}catch(Exception error){Debug.WriteLine("Window placement: "+error.Message);}
   if(saved!=null)window.WindowStartupLocation=WindowStartupLocation.Manual;
   saveTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};saveTimer.Tick+=SaveTick;
   window.SourceInitialized+=Restore;window.Loaded+=Loaded;window.LocationChanged+=Changed;window.SizeChanged+=Resized;window.StateChanged+=StateChanged;window.Closing+=Closing;
  }
  static bool Valid(SavedWindowPlacement value){return value!=null&&value.Width>=320&&value.Height>=240&&value.Width<=100000&&value.Height<=100000&&Math.Abs((long)value.Left)<=100000&&Math.Abs((long)value.Top)<=100000&&Math.Abs((long)value.WorkOffsetX)<=100000&&Math.Abs((long)value.WorkOffsetY)<=100000;}
  void Restore(object sender,EventArgs args){
   if(saved==null)return;restoring=true;
   try{
    IntPtr monitor=IntPtr.Zero;
    MonitorCallback find=(handle,dc,rectangle,data)=>{var information=Info(handle);if(string.Equals(information.Device,saved.Monitor,StringComparison.OrdinalIgnoreCase))monitor=handle;return true;};
    EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,find,IntPtr.Zero);
    if(monitor==IntPtr.Zero){var screen=new NativeRect{Left=saved.Left+saved.WorkOffsetX,Top=saved.Top+saved.WorkOffsetY,Right=saved.Left+saved.WorkOffsetX+saved.Width,Bottom=saved.Top+saved.WorkOffsetY+saved.Height};monitor=MonitorFromRect(ref screen,2);}
    var info=Info(monitor);
    // WINDOWPLACEMENT uses workspace coordinates, not screen coordinates.
    // Adjust if a taskbar moved, then clamp to the selected monitor's work area.
    int left=saved.Left+saved.WorkOffsetX-(info.Work.Left-info.Monitor.Left),top=saved.Top+saved.WorkOffsetY-(info.Work.Top-info.Monitor.Top);
    int width=Math.Min(saved.Width,info.Work.Right-info.Work.Left),height=Math.Min(saved.Height,info.Work.Bottom-info.Work.Top);
    left=Math.Max(info.Monitor.Left,Math.Min(left,info.Monitor.Left+info.Work.Right-info.Work.Left-width));
    top=Math.Max(info.Monitor.Top,Math.Min(top,info.Monitor.Top+info.Work.Bottom-info.Work.Top-height));
    var placement=new NativePlacement{Length=Marshal.SizeOf(typeof(NativePlacement)),ShowCommand=saved.Maximized?3:1,Normal=new NativeRect{Left=left,Top=top,Right=left+width,Bottom=top+height}};
    // Allow a smaller display than the one the window was last used on.
    var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);var scale=source.CompositionTarget.TransformFromDevice;
    window.MinWidth=Math.Min(window.MinWidth,(info.Work.Right-info.Work.Left)*scale.M11);window.MinHeight=Math.Min(window.MinHeight,(info.Work.Bottom-info.Work.Top)*scale.M22);
    if(!SetWindowPlacement(new WindowInteropHelper(window).Handle,ref placement))throw new InvalidOperationException("Windows could not restore the main window.");
    lastMaximized=saved.Maximized;
   }catch(Exception error){Debug.WriteLine("Window placement restore: "+error.Message);window.WindowStartupLocation=WindowStartupLocation.CenterScreen;}
   finally{restoring=false;}
  }
  void Loaded(object sender,RoutedEventArgs args){loaded=true;lastMaximized=window.WindowState==WindowState.Maximized;}
  void Changed(object sender,EventArgs args){if(!loaded||restoring||window.WindowState==WindowState.Minimized)return;saveTimer.Stop();saveTimer.Start();}
  void Resized(object sender,SizeChangedEventArgs args){Changed(sender,args);}
  void StateChanged(object sender,EventArgs args){if(window.WindowState!=WindowState.Minimized)lastMaximized=window.WindowState==WindowState.Maximized;Changed(sender,args);}
  void Closing(object sender,System.ComponentModel.CancelEventArgs args){Save();}
  void SaveTick(object sender,EventArgs args){Save();}
  public void Save(){
   saveTimer.Stop();if(!loaded||restoring)return;
   try{
    IntPtr handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
    var placement=new NativePlacement{Length=Marshal.SizeOf(typeof(NativePlacement))};if(!GetWindowPlacement(handle,ref placement))return;
    var info=Info(MonitorFromWindow(handle,2));var bounds=placement.Normal;
    var value=new SavedWindowPlacement{Left=bounds.Left,Top=bounds.Top,Width=bounds.Right-bounds.Left,Height=bounds.Bottom-bounds.Top,Monitor=info.Device,WorkOffsetX=info.Work.Left-info.Monitor.Left,WorkOffsetY=info.Work.Top-info.Monitor.Top,Maximized=window.WindowState==WindowState.Minimized?lastMaximized:window.WindowState==WindowState.Maximized};
    if(!Valid(value))return;Directory.CreateDirectory(Path.GetDirectoryName(path));Util.AtomicText(path,Util.Serialize(value));saved=value;
   }catch(Exception error){Debug.WriteLine("Window placement save: "+error.Message);}
  }
  public void Dispose(){saveTimer.Stop();window.SourceInitialized-=Restore;window.Loaded-=Loaded;window.LocationChanged-=Changed;window.SizeChanged-=Resized;window.StateChanged-=StateChanged;window.Closing-=Closing;}
  static MonitorInfo Info(IntPtr monitor){var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(!GetMonitorInfo(monitor,ref info))throw new InvalidOperationException("The display work area is unavailable.");return info;}
  [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;}
  [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]struct NativePlacement{public int Length,Flags,ShowCommand;public NativePoint MinPosition,MaxPosition;public NativeRect Normal;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct MonitorInfo{public int Size;public NativeRect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
  delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,IntPtr rectangle,IntPtr data);
  [DllImport("user32.dll")]static extern bool GetWindowPlacement(IntPtr window,ref NativePlacement placement);
  [DllImport("user32.dll")]static extern bool SetWindowPlacement(IntPtr window,ref NativePlacement placement);
  [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
  [DllImport("user32.dll")]static extern IntPtr MonitorFromRect(ref NativeRect rectangle,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo information);
  [DllImport("user32.dll")]static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
 }
 public partial class MainUi {
  MainWindowPlacement mainWindowPlacement;
  void InitializeWindowPlacement(bool test){if(!test)mainWindowPlacement=new MainWindowPlacement(Window,Path.Combine(Path.GetDirectoryName(config),"window-placement.json"));}
  void SaveMainWindowPlacement(){if(mainWindowPlacement!=null)mainWindowPlacement.Save();}
 }
}
