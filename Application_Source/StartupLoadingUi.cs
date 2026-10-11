using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AstroArchive {
 internal sealed class StartupLoadingWindow {
  public readonly Window Window;
  readonly StartupMessageDeck remarks,hints,facts;readonly DispatcherTimer timer;int ticks;bool fact;
  TextBlock Label(string name){return (TextBlock)Window.FindName(name);}
  public StartupLoadingWindow(bool? highContrast=null){
   var assembly=Assembly.GetExecutingAssembly();
   using(var stream=assembly.GetManifestResourceStream("StartupWindow.xaml"))Window=(Window)XamlReader.Load(stream);
   Window.Icon=ApplicationIcon.Image;
   ((Image)Window.FindName("BrandLogo")).Source=LoadImage("AstroArchive_Logo.png",68);
   ((Image)Window.FindName("NebulaImage")).Source=LoadImage("StartupRosette.jpg",1320);
   Label("VersionLabel").Text="v"+FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
   var random=new Random();remarks=new StartupMessageDeck(StartupMessages.Remarks,random);hints=new StartupMessageDeck(StartupMessages.Hints,random);facts=new StartupMessageDeck(StartupMessages.Facts,random);
   Label("RemarkLabel").Text=remarks.Next();fact=random.Next(2)==0;NextNote();SetProgress(0,"Preparing your workspace…");
   AutomationProperties.SetLiveSetting(Label("StageLabel"),AutomationLiveSetting.Polite);
   var area=SystemParameters.WorkArea;Window.Width=Math.Min(880,Math.Max(1,area.Width-24));Window.Height=Math.Min(590,Math.Max(1,area.Height-24));
   if(highContrast??SystemParameters.HighContrast){
    Window.Background=SystemColors.WindowBrush;
    Window.Resources["Canvas"]=SystemColors.WindowBrush;Window.Resources["Surface"]=SystemColors.WindowBrush;
    Window.Resources["Text"]=SystemColors.WindowTextBrush;Window.Resources["Muted"]=SystemColors.WindowTextBrush;
    Window.Resources["HeadingAccent"]=SystemColors.WindowTextBrush;Window.Resources["Accent"]=SystemColors.HighlightBrush;
    Window.Resources["Border"]=SystemColors.WindowTextBrush;Window.Resources["Focus"]=SystemColors.HighlightBrush;
    ((Image)Window.FindName("NebulaImage")).Visibility=Visibility.Hidden;
    ((Border)Window.FindName("HeroShade")).Background=SystemColors.WindowBrush;((Border)Window.FindName("HeroFloorShade")).Background=Brushes.Transparent;
   }
   ((Button)Window.FindName("CancelStartup")).Click+=(s,e)=>Window.Close();
   Window.KeyDown+=(s,e)=>{if(e.Key==Key.Escape)Window.Close();};
   ((Border)Window.FindName("Header")).MouseLeftButtonDown+=(s,e)=>{if(e.ButtonState==MouseButtonState.Pressed)Window.DragMove();};
   Window.SourceInitialized+=(s,e)=>AstroArchive.Installation.ShellIdentity.ConfigureWindow(new WindowInteropHelper(Window).Handle,assembly.Location);
   timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};timer.Tick+=(s,e)=>Rotate();
   Window.Loaded+=(s,e)=>timer.Start();Window.Closed+=(s,e)=>timer.Stop();
  }
  static BitmapImage LoadImage(string name,int width){
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){
    if(stream==null)throw new InvalidDataException("Missing startup image: "+name);
    var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=width;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
   }
  }
  public void SetProgress(int percent,string stage){
   var bar=(ProgressBar)Window.FindName("LoadingProgress");bar.Value=Math.Max(bar.Value,Math.Min(100,Math.Max(0,percent)));
   Label("PercentLabel").Text=bar.Value.ToString("0")+"%";Label("StageLabel").Text=stage;
   if(bar.Value==100){timer.Stop();Label("RemarkLabel").Text="Wishing you a clear night";}
  }
  internal void Rotate(){Label("RemarkLabel").Text=remarks.Next();if(++ticks%2==0)NextNote();}
  void NextNote(){Label("NoteHeading").Text=fact?"A LITTLE COSMIC PERSPECTIVE":"MAKE THE MOST OF ASTROARCHIVE";Label("NoteLabel").Text=fact?facts.Next():hints.Next();fact=!fact;}
 }

 // The splash has its own dispatcher: slow repository I/O on the application
 // thread cannot freeze its messages, progress rendering or cancel button.
 internal sealed class StartupSplash:IDisposable {
  readonly Thread thread;readonly ManualResetEventSlim ready=new ManualResetEventSlim();
  Dispatcher dispatcher;StartupLoadingWindow view;bool finishing;volatile bool cancelled;int disposed;
  public bool Cancelled{get{return cancelled;}}
  internal bool IsAlive{get{return thread.IsAlive;}}
  public StartupSplash(){
   thread=new Thread(Run){IsBackground=true,Name="AstroArchive startup window"};thread.SetApartmentState(ApartmentState.STA);thread.Start();
   // Usually one render frame. Never hold up application launch on a failed splash.
   ready.Wait(TimeSpan.FromSeconds(3));
  }
  void Run(){
   try{
    dispatcher=Dispatcher.CurrentDispatcher;view=new StartupLoadingWindow();
    view.Window.ContentRendered+=(s,e)=>ready.Set();
    view.Window.Closed+=(s,e)=>{if(!finishing)cancelled=true;dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);};
    view.Window.Show();Dispatcher.Run();
   }catch(Exception error){Debug.WriteLine("Startup window: "+error);}
   finally{ready.Set();}
  }
  public void Report(int percent,string stage){if(cancelled)throw new OperationCanceledException("Opening AstroArchive was cancelled.");Post(()=>view.SetProgress(percent,stage));}
  void Post(Action action){var target=dispatcher;if(target==null||target.HasShutdownStarted||target.HasShutdownFinished)return;try{target.BeginInvoke(DispatcherPriority.Normal,action);}catch(InvalidOperationException){}}
  public void Complete(){
   Post(()=>{finishing=true;view.SetProgress(100,"Your workspace is ready.");dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>view.Window.Close()));});
  }
  internal void Inspect(Action<StartupLoadingWindow> inspect){dispatcher.Invoke(DispatcherPriority.Background,new Action(()=>inspect(view)));}
  public void Dispose(){
   if(Interlocked.Exchange(ref disposed,1)!=0)return;
   Post(()=>{finishing=true;if(view!=null)view.Window.Close();dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);});
   if(thread.Join(TimeSpan.FromSeconds(3)))ready.Dispose();
  }
 }
}
