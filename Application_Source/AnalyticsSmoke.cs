using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace AstroArchive {
 public partial class MainUi {
  public void SmokeAnalytics(string output){
   Directory.CreateDirectory(output);Window.Show();PumpPopupLayout();var savedRepo=repo;string theme=settings.ThemeMode;
   var source=Enumerable.Range(0,240).Select(i=>new Frame{Target=new[]{"M31","M45","NGC6888"}[i%3],Kind="Light",Telescope=i%2==0?"Garden scope":"Travel scope",Filter=i%2==0?"Hα + OIII":"Broadband",Exposure=new[]{15.0,30,60,120,300}[i%5],AcquisitionDate=new DateTime(2026,1,1).AddDays(i%210).ToString("yyyy-MM-dd")}).ToList();
   try{
    repo=null;if(AnalyticsMenu().IsEnabled)throw new Exception("Analytics is enabled without a repository.");
    using(var fixture=new Repository(Path.Combine(output,"analytics-repository"))){
     repo=fixture;var menu=AnalyticsMenu();if(!menu.IsEnabled||menu.HasItems)throw new Exception("Analytics does not open directly as a single menu action.");
     bool opened=false;Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{var direct=Window.OwnedWindows.OfType<AnalyticsWindow>().SingleOrDefault();if(direct!=null){opened=PopupChildren<ListBox>(direct).Any(c=>AutomationProperties.GetName(c)=="Analytics charts"&&c.Items.Count==7);direct.Close();}}));
     menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));if(!opened)throw new Exception("Analytics menu click did not open the complete workspace");
     foreach(string mode in new[]{"Light","Dark"}){
      Theme.Apply(Window,mode);var customConfigurations=new Dictionary<string,AnalyticsChartOptions>();int configurationSaves=0;var dialog=new AnalyticsWindow(Window,source,"Observatory & field notes",0,customConfigurations,()=>configurationSaves++);
      try{
       dialog.Show();PumpPopupLayout();var image=PopupChildren<Image>(dialog).Single(i=>AutomationProperties.GetName(i)=="Branded analytics export preview");
       var status=PopupChildren<TextBlock>(dialog).Single(t=>AutomationProperties.GetName(t)=="Analytics status");
       SmokeSearchWait(()=>image.Source!=null);PumpPopupLayout();VerifyWindowIcon(dialog);Readable(dialog.Foreground,dialog.Background,"Analytics "+mode);
       var choices=PopupChildren<ListBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Analytics charts");if(choices.Items.Count!=7)throw new Exception("Analytics chart navigation is incomplete.");
       choices.SelectedIndex=6;PumpPopupLayout();if(!PopupChildren<TextBlock>(dialog).Any(t=>t.Text=="All six charts · Combined preview"))throw new Exception("Combined preview is unavailable.");
       var documentTheme=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Document theme");if(Convert.ToString(documentTheme.SelectedItem)!="Dark")throw new Exception("Analytics document theme did not default to Dark independently of application theme.");
       CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_DarkDocument.png"));var darkPreview=image.Source;documentTheme.SelectedIndex=1;PumpPopupLayout();if(image.Source==darkPreview)throw new Exception("Light document selection did not redraw the preview.");CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_LightDocument.png"));documentTheme.SelectedIndex=0;
       var layout=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Document layout");var resolution=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Image resolution");
       if(layout.SelectedIndex!=0)throw new Exception("Existing landscape layout is not the default.");
       layout.SelectedIndex=1;choices.SelectedIndex=0;PumpPopupLayout();
       if(Math.Abs(image.Source.Width/image.Source.Height-9.0/16)>0.001||resolution.SelectedIndex!=2)throw new Exception("Portrait preview or social resolution did not update.");
       CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_Portrait.png"));
       choices.SelectedIndex=6;PumpPopupLayout();if(Math.Abs(image.Source.Width/image.Source.Height-9.0/16)>0.001)throw new Exception("Portrait collection preview did not start with a readable page.");
       var pageChoice=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Preview page");pageChoice.SelectedIndex=0;PumpPopupLayout();if(Math.Abs(image.Source.Width/image.Source.Height-9.0/96)>0.001)throw new Exception("Portrait collection's combined preview lost a chart.");choices.SelectedIndex=0;PumpPopupLayout();
       documentTheme.SelectedIndex=1;PumpPopupLayout();if(Math.Abs(image.Source.Width/image.Source.Height-9.0/16)>0.001)throw new Exception("Light portrait preview lost its composition.");
       double[][] sizes={new[]{1080.0,1920},new[]{1080.0,1350},new[]{1080.0,1080},new[]{1920.0,1080},new[]{1000.0,1500}};
       for(int option=1;option<=5;option++){layout.SelectedIndex=option;PumpPopupLayout();if(Math.Abs(image.Source.Width/image.Source.Height-sizes[option-1][0]/sizes[option-1][1])>0.001||!Convert.ToString(resolution.SelectedItem).StartsWith(sizes[option-1][0].ToString("0")+" × "+sizes[option-1][1].ToString("0")))throw new Exception("Social layout preview or pixel preset is incorrect: "+option);CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_Social_"+option+".png"));}
       layout.SelectedIndex=0;choices.SelectedIndex=6;documentTheme.SelectedIndex=0;PumpPopupLayout();if(resolution.Items.Count!=2)throw new Exception("Portrait-only resolution remained in the landscape options.");
       var outputKind=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Export type");outputKind.SelectedIndex=1;
       var exportFormat=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Export format");if(exportFormat.Items.Count!=2||Convert.ToString(exportFormat.SelectedItem)!="MP4")throw new Exception("Video choices were not streamlined.");foreach(string motion in new[]{"MP4","GIF"}){exportFormat.SelectedItem=motion;PumpPopupLayout();var play=PopupChildren<Button>(dialog).Single(b=>AutomationProperties.GetName(b)=="Play animation preview");if(!play.IsVisible||!play.IsEnabled||resolution.IsEnabled)throw new Exception("Animated export controls are unavailable or retain image DPI.");play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));SmokeSearchWait(()=>Convert.ToString(play.Content).Contains("Pause"));PumpPopupLayout();var before=image.Source;SmokeSearchWait(()=>image.Source!=before);play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!Convert.ToString(play.Content).Contains("Play"))throw new Exception("Animation preview did not pause.");CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_"+motion+".png"));}
       var pace=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Pace");var length=PopupChildren<TextBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Length · seconds");pace.SelectedIndex=3;length.Text="0";PumpPopupLayout();if(PopupChildren<Button>(dialog).Any(b=>Convert.ToString(b.Content).StartsWith("Export")&&b.IsEnabled))throw new Exception("Invalid custom duration remained exportable");length.Text="15";PumpPopupLayout();if(!PopupChildren<TextBlock>(dialog).Any(t=>t.Text.StartsWith("15 s · 6 scenes")))throw new Exception("Custom video length was not reflected in the summary");pace.SelectedIndex=0;outputKind.SelectedIndex=0;if(Convert.ToString(exportFormat.SelectedItem)!="PDF"||exportFormat.Items.Count!=4)throw new Exception("Document choices did not restore after video mode");
       SmokeAnalyticsCustomization(dialog,output,mode,customConfigurations,()=>configurationSaves);
       PopupChildren<Expander>(dialog).Single().IsExpanded=true;PumpPopupLayout();
       var picker=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Telescope");picker.SelectedIndex=1;
       SmokeSearchWait(()=>status.Text!="Updating analytics…");if(!PopupChildren<TextBlock>(dialog).Any(t=>t.Text.StartsWith("120 light frames")))throw new Exception("Analytics telescope scope did not refresh.");
       var from=PopupChildren<DatePicker>(dialog).Single(c=>AutomationProperties.GetName(c)=="From (inclusive)");var to=PopupChildren<DatePicker>(dialog).Single(c=>AutomationProperties.GetName(c)=="To (inclusive)");from.SelectedDate=new DateTime(2026,10,2);to.SelectedDate=new DateTime(2026,10,1);
       SmokeSearchWait(()=>status.Text!="Updating analytics…");if(status.Text!="The start date must be on or before the end date.")throw new Exception("Analytics accepted an inverted date range.");
       var export=PopupChildren<Button>(dialog).Single(b=>b.IsVisible&&Convert.ToString(b.Content)=="Export all…");if(export.IsEnabled)throw new Exception("Stale analytics remained exportable after invalid scope.");
      }finally{dialog.Close();}
     }
    }
    var data=ArchiveAnalytics.Build(source,new AnalyticsOptions{Caption="Publication fixture"});var pages=Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(data,i)).ToList();
    foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout)))foreach(bool dark in new[]{true,false})foreach(string format in new[]{"PNG","JPEG","PDF","SVG"})foreach(bool allCharts in new[]{false,true}){
     var themed=Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(data,i,dark,layout)).ToList();var chosen=allCharts?themed:new List<AnalyticsPage>{themed[0]};string path=Path.Combine(output,"analytics-"+(layout==AnalyticsLayout.Landscape?"":layout.ToString().ToLowerInvariant()+"-")+(dark?"dark-":"light-")+(allCharts?"all":"single")+"."+(format=="JPEG"?"jpg":format.ToLowerInvariant()));
     AnalyticsExport.Save(path,chosen,format,layout==AnalyticsLayout.Landscape?150:96);if(new FileInfo(path).Length<100)throw new Exception("Empty analytics export: "+format);
     if(format=="PNG"||format=="JPEG"){
      using(var input=File.OpenRead(path)){var image=BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];double w,h;AnalyticsGraphics.SheetSize(chosen,out w,out h);double factor=layout==AnalyticsLayout.Landscape?150/96.0:1;if(image.PixelWidth!=(int)Math.Ceiling(w*factor)||image.PixelHeight!=(int)Math.Ceiling(h*factor))throw new Exception("Incorrect raster export dimensions.");var rgb=new System.Windows.Media.Imaging.FormatConvertedBitmap(image,System.Windows.Media.PixelFormats.Bgra32,null,0);var pixel=new byte[4];rgb.CopyPixels(new Int32Rect(0,25,1,1),pixel,4,0);if(dark?pixel[2]>20||pixel[1]>25||pixel[0]>40:pixel[2]<250||pixel[1]<250||pixel[0]<250)throw new Exception("Raster export background does not match document theme.");}
     }else if(format=="SVG"){var svg=XDocument.Load(path);XNamespace ns="http://www.w3.org/2000/svg";if(svg.Root.Elements(ns+"rect").First().Attribute("fill").Value!=chosen[0].Background)throw new Exception("SVG document theme does not match the preview.");}else if(!File.ReadAllText(path).StartsWith("%PDF-1.4"))throw new Exception("Incorrect PDF export header.");
    }
    string overwrite=Path.Combine(output,"analytics-overwrite.png");File.WriteAllText(overwrite,"existing output");AnalyticsExport.Save(overwrite,new[]{pages[0]},"PNG",150);
    using(var input=File.OpenRead(overwrite))BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
    File.WriteAllText(overwrite,"keep on failure");bool failed=false;try{AnalyticsExport.Save(overwrite,new[]{pages[0]},"invalid",150);}catch(ArgumentException){failed=true;}
    if(!failed||File.ReadAllText(overwrite)!="keep on failure"||Directory.GetFiles(output,"*.tmp").Length!=0)throw new Exception("Failed export changed the destination or retained partial output.");
    SmokeAnalyticsMedia(output,data);
    File.WriteAllText(Path.Combine(output,"analytics-smoke.txt"),"PASS: Repository menu, six reports, combined preview, landscape default and five social layout selectors, exact social pixel sizes, dark/light document and application themes, single/combined PNG/JPEG/PDF/SVG exports, telescope/date scoping, stale-export prevention, decoding and atomic overwrite checks.");
   }finally{repo=savedRepo;Theme.Apply(Window,theme);}
  }
  void SmokeAnalyticsCustomization(AnalyticsWindow dialog,string output,string mode,Dictionary<string,AnalyticsChartOptions> configurations,Func<int> saves){
   var charts=PopupChildren<ListBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Analytics charts");charts.SelectedIndex=0;PumpPopupLayout();Exception failure=null;bool applied=false;
   Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{
    var form=dialog.OwnedWindows.Cast<Window>().SingleOrDefault(w=>w.Title=="Customize analytics chart");
    try{
     if(form==null)throw new Exception("Chart customization did not open");PumpPopupLayout();var title=PopupChildren<TextBox>(form).Single(t=>AutomationProperties.GetName(t)=="Title (blank uses default)");title.Text="My observatory";
     var style=PopupChildren<ComboBox>(form).Single(c=>AutomationProperties.GetName(c)=="Chart style");style.SelectedItem="Bars";PopupChildren<ComboBox>(form).Single(c=>AutomationProperties.GetName(c)=="Colour palette").SelectedItem="Nebula";PumpPopupLayout();
     if(PopupChildren<Image>(form).Single(i=>AutomationProperties.GetName(i)=="Custom chart preview").Source==null)throw new Exception("Custom settings have no live preview");CapturePopup(form,Path.Combine(output,"AstroArchive_Analytics_Custom_"+mode+".png"));
     PopupChildren<Button>(form).Single(b=>Convert.ToString(b.Content)=="Apply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));applied=true;
    }catch(Exception error){failure=error;if(form!=null)form.Close();}
   }));
   PopupChildren<Button>(dialog).Single(b=>AutomationProperties.GetName(b)=="Customize chart").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(failure!=null)throw failure;
   if(!applied||saves()!=1||configurations["targets"].Title!="My observatory"||configurations["targets"].Style!="Bars")throw new Exception("Custom chart settings were not applied and saved");
   charts.SelectedIndex=6;PumpPopupLayout();
  }
  static void AnimationReference(string path,AnalyticsAnimation animation,double time,int width,int height){
   byte[] pixels=animation.Frame(time,width,height);var bitmap=BitmapSource.Create(width,height,96,96,System.Windows.Media.PixelFormats.Bgra32,null,pixels,width*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
  }
  static void SmokeAnalyticsMedia(string output,AnalyticsSnapshot data){
   foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout)))foreach(bool dark in new[]{true,false}){
    var page=AnalyticsGraphics.Page(data,2,dark,layout);var selected=new[]{page};var options=new AnalyticsVideoOptions{SecondsPerChart=2,FramesPerSecond=4,MaximumEdge=320};var animation=new AnalyticsAnimation(selected,options);int width,height;AnalyticsAnimation.Dimensions(page,options.MaximumEdge,out width,out height);
    string prefix=Path.Combine(output,"media-"+layout+"-"+(dark?"dark":"light"));AnimationReference(prefix+"-reference.png",animation,1.75,width,height);
    foreach(string format in new[]{"MP4","GIF"}){
     string path=prefix+"."+format.ToLowerInvariant();int last=-1;AnalyticsAnimation.Save(path,selected,format,options,(percent,message)=>{if(percent<last)throw new Exception("Video progress moved backwards.");last=percent;},CancellationToken.None);
     if(last!=100||new FileInfo(path).Length<100)throw new Exception("Animated export did not finish.");
     if(format=="GIF")using(var input=File.OpenRead(path)){var gif=BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);if(gif.Frames.Count!=8||gif.Frames.Any(f=>f.PixelWidth!=width||f.PixelHeight!=height))throw new Exception("GIF frames or dimensions incorrect.");foreach(var frame in gif.Frames){var bytes=new byte[width*height*4];new FormatConvertedBitmap(frame,System.Windows.Media.PixelFormats.Bgra32,null,0).CopyPixels(bytes,width*4,0);}}
     else{byte[] bytes=File.ReadAllBytes(path);string container=System.Text.Encoding.ASCII.GetString(bytes);if(!container.Contains("ftyp")||!container.Contains("moov")||!container.Contains("avc1"))throw new Exception("MP4 is not a completed H.264 container.");}
    }
   }
   foreach(AnalyticsLayout layout in Enum.GetValues(typeof(AnalyticsLayout))){
    var page=AnalyticsGraphics.Page(data,5,true,layout);int width,height;AnalyticsAnimation.Dimensions(page,720,out width,out height);if(width%2!=0||height%2!=0||Math.Abs(width/(double)height-page.CanvasWidth/page.CanvasHeight)>.000001)throw new Exception("Reduced video size changed the chosen ratio.");
    var native=new AnalyticsVideoOptions{SecondsPerChart=.125,FramesPerSecond=24};AnalyticsAnimation.Save(Path.Combine(output,"media-native-"+layout+".mp4"),new[]{page},"MP4",native,null,CancellationToken.None);
   }
   var story=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Page(data,i,true,AnalyticsLayout.Vertical)).ToList();var settings=new AnalyticsVideoOptions{SecondsPerChart=1.5,FramesPerSecond=4,MaximumEdge=320,Transition="Zoom"};var sequence=new AnalyticsAnimation(story,settings);if(sequence.Duration!=9)throw new Exception("Story duration omits a chart.");
   foreach(string format in new[]{"MP4","GIF"})AnalyticsAnimation.Save(Path.Combine(output,"media-story."+format.ToLowerInvariant()),story,format,settings,null,CancellationToken.None);
   var customTiming=new AnalyticsVideoOptions{TotalSeconds=3,FramesPerSecond=4,MaximumEdge=320};var customMovie=new AnalyticsAnimation(story,customTiming);if(customMovie.Duration!=3)throw new Exception("Custom length changed with six scenes");foreach(string format in new[]{"MP4","GIF"})AnalyticsAnimation.Save(Path.Combine(output,"media-custom-length."+format.ToLowerInvariant()),story,format,customTiming,null,CancellationToken.None);
   foreach(string transition in new[]{"Glide","Zoom","Dissolve"}){settings.Transition=transition;var movie=new AnalyticsAnimation(story,settings);AnimationReference(Path.Combine(output,"media-transition-"+transition+".png"),movie,1.75,180,320);}
   foreach(string format in new[]{"MP4","GIF"}){
    string path=Path.Combine(output,"media-cancelled."+format.ToLowerInvariant());File.WriteAllText(path,"preserve previous output");using(var cancelled=new CancellationTokenSource()){bool stopped=false;try{AnalyticsAnimation.Save(path,story,format,settings,(p,m)=>{if(p>5)cancelled.Cancel();},cancelled.Token);}catch(OperationCanceledException){stopped=true;}if(!stopped||File.ReadAllText(path)!="preserve previous output")throw new Exception("Cancelled media export overwrote an existing document.");}
   }
   if(Directory.GetFiles(output,"*.tmp.*").Length!=0)throw new Exception("Media export retained partial output.");File.WriteAllText(Path.Combine(output,"analytics-media-smoke.txt"),"PASS: H.264 MP4 and looping GIF exports, all six ratios, both themes, complete six-chart stories, transition preview, timing, progress, streaming GIF decoding and atomic cancellation.");
  }
 }
}
