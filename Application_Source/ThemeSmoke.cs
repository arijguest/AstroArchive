// Exercise the separate popup trees where native white surfaces hid theme text.
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  static double Luminance(Color colour){Func<byte,double> linear=v=>{double s=v/255.0;return s<=0.04045?s/12.92:Math.Pow((s+0.055)/1.055,2.4);};return 0.2126*linear(colour.R)+0.7152*linear(colour.G)+0.0722*linear(colour.B);}
  static void Readable(Brush text,Brush background,string label){var foreground=text as SolidColorBrush;var surface=background as SolidColorBrush;if(foreground==null||surface==null)throw new Exception(label+" did not resolve solid theme brushes.");double a=Luminance(foreground.Color),b=Luminance(surface.Color);if((Math.Max(a,b)+0.05)/(Math.Min(a,b)+0.05)<4.5)throw new Exception(label+" has unreadable contrast.");}
  static void CapturePopup(Window window,string path){var visual=(FrameworkElement)window.Content;visual.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))png.Save(stream);}
  void SmokePopupThemes(string output){
   foreach(string mode in new[]{"Dark","Light"}){
    Theme.Apply(Window,mode);var dialog=new FormWindow(Window,"Processing capture",650,480);dialog.Tabs("Processing","Details");dialog.Text("Processing captures",true);dialog.Text("Verifying copied files before updating the repository.");
    var message=new TextBox{Text="Copy complete. Verifying checksums…",IsReadOnly=true};dialog.Add(message);var progress=new ProgressBar{Value=65,Maximum=100,Height=8,Margin=new Thickness(0,12,0,12)};dialog.Add(progress);dialog.CloseOnly();
    var help=new HelpWindow(Window,HelpCatalog.Load(),"IMAGE PREVIEW AND TABLES");var tooltip=B("SettingsButton").ToolTip as ToolTip;
    try{
     dialog.Window.Show();help.Show();PumpPopupLayout();
     Readable(dialog.Window.Foreground,dialog.Window.Background,mode+" processing window");Readable(message.Foreground,message.Background,mode+" processing text");Readable(help.Article.Foreground,help.Article.Background,mode+" guide article");Readable(help.TopicList.Foreground,help.TopicList.Background,mode+" guide topics");
     foreach(var label in PopupChildren<TextBlock>(dialog.Window).Where(t=>!string.IsNullOrWhiteSpace(t.Text)))Readable(label.Foreground,dialog.Window.Background,mode+" dialog label: "+label.Text);
     CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Processing_"+mode+".png"));
     if(tooltip==null)throw new Exception("Settings tooltip was not created.");tooltip.PlacementTarget=B("SettingsButton");tooltip.IsOpen=true;PumpPopupLayout();
     Readable(tooltip.Foreground,tooltip.Background,mode+" tooltip");var body=tooltip.Content as TextBlock;if(body!=null)Readable(body.Foreground,tooltip.Background,mode+" tooltip text");
     tooltip.IsOpen=false;Theme.Apply(Window,mode=="Dark"?"Light":"Dark");PumpPopupLayout();Readable(message.Foreground,message.Background,"Changed processing theme");Readable(help.Article.Foreground,help.Article.Background,"Changed guide theme");
    }finally{if(tooltip!=null)tooltip.IsOpen=false;help.Close();dialog.Window.Close();}
   }
   Theme.Apply(Window,"Dark");
  }
 }
}
