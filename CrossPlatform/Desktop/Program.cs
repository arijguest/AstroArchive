using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
namespace AstroArchive.Desktop;
public static class Program {
 [STAThread] public static int Main(string[] args) {
  if(args.Length>0 && args[0]=="--version") { Console.WriteLine("AstroArchive 3.1.4.1 Linux preview 1 (x64)"); return 0; }
  if(args.Length>0 && args[0]=="--analytics-media-test") return AnalyticsMediaCheck.Run(args.Skip(1).FirstOrDefault());
  if(args.Length>0 && args[0]=="--keyring-test") return PackageSelfTest.Keyring(args.Skip(1).FirstOrDefault());
  if(args.Length>0 && args[0]=="--self-test") return PackageSelfTest.Run(args.Skip(1).FirstOrDefault());
  if(args.Length>1 && args[0]=="--crash-test-worker") return PackageSelfTest.CrashWorker(args[1]);
  if(args.Length>0 && args[0]=="--ui-smoke") return NativeSmoke.Run(args.Skip(1).FirstOrDefault());
  BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); return 0;
 }
 public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
public sealed class App : Application {
 public override void Initialize() {
  Styles.Add(new FluentTheme()); Styles.Add(new StyleInclude(new Uri("avares://AstroArchive/")){Source=new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")});
  RequestedThemeVariant=ThemeVariant.Dark;
 }
 public override void OnFrameworkInitializationCompleted() {
  if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
   var window=new MainWindow(NativeSmoke.Root == null ? null : new ArchiveSession(Path.Combine(NativeSmoke.Root,"config","settings.json")));
   desktop.MainWindow=window;
   if(NativeSmoke.Root != null) window.Opened += (_,_) => NativeSmoke.Start(window,desktop);
  }
  base.OnFrameworkInitializationCompleted();
 }
}
