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
     repo=fixture;var menu=AnalyticsMenu();if(!menu.IsEnabled||menu.Items.Count!=0)throw new Exception("Analytics still opens a chart submenu instead of its pane.");
     bool opened=false;Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{var analytics=Application.Current.Windows.OfType<AnalyticsWindow>().FirstOrDefault();if(analytics!=null){opened=true;analytics.Close();}}));menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));if(!opened)throw new Exception("Analytics entry did not open its pane directly.");
     foreach(string mode in new[]{"Light","Dark"}){
      Theme.Apply(Window,mode);var dialog=new AnalyticsWindow(Window,source,"Observatory & field notes",0);
      try{
       dialog.Show();PumpPopupLayout();var image=PopupChildren<Image>(dialog).Single(i=>AutomationProperties.GetName(i)=="Branded analytics export preview");
       var status=PopupChildren<TextBlock>(dialog).Single(t=>AutomationProperties.GetName(t)=="Analytics status");
       SmokeSearchWait(()=>image.Source!=null);PumpPopupLayout();VerifyWindowIcon(dialog);Readable(dialog.Foreground,dialog.Background,"Analytics "+mode);
       var choices=PopupChildren<ListBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Analytics charts");if(choices.Items.Count!=8)throw new Exception("Analytics chart navigation is incomplete.");
       var exportOptions=PopupChildren<Expander>(dialog).Single(c=>AutomationProperties.GetName(c)=="Export options");if(exportOptions.IsExpanded)throw new Exception("Detailed export options are open before they are wanted.");
       var chartType=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Chart type");if(!chartType.IsVisible||chartType.Items.Count!=3)throw new Exception("Chart type choice is unavailable.");
       var natural=image.Source;chartType.SelectedIndex=1;PumpPopupLayout();if(image.Source==natural)throw new Exception("Chart type did not redraw the preview.");CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_ChartType.png"));
       choices.SelectedIndex=1;PumpPopupLayout();if(chartType.IsVisible)throw new Exception("The timeline offered an unordered chart type.");choices.SelectedIndex=0;PumpPopupLayout();if(chartType.SelectedIndex!=1)throw new Exception("Chart type was not kept for its chart.");chartType.SelectedIndex=0;PumpPopupLayout();
       exportOptions.IsExpanded=true;PumpPopupLayout();
       choices.SelectedIndex=6;PumpPopupLayout();if(!PopupChildren<TextBlock>(dialog).Any(t=>t.Text=="All six charts · Combined preview"))throw new Exception("Combined preview is unavailable.");
       var documentTheme=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Document theme");if(Convert.ToString(documentTheme.SelectedItem)!="Dark")throw new Exception("Analytics document theme did not default to Dark independently of application theme.");
       CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_DarkDocument.png"));var darkPreview=image.Source;documentTheme.SelectedIndex=1;PumpPopupLayout();if(image.Source==darkPreview)throw new Exception("Light document selection did not redraw the preview.");CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_LightDocument.png"));documentTheme.SelectedIndex=0;
       var exportFormat=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Export format");exportFormat.SelectedItem="PNG";PumpPopupLayout();
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
       var imageMode=PopupChildren<RadioButton>(dialog).Single(r=>AutomationProperties.GetName(r)=="Images & documents");var videoMode=PopupChildren<RadioButton>(dialog).Single(r=>AutomationProperties.GetName(r)=="Video & GIF");
       exportFormat.SelectedItem="PDF";if(imageMode.IsChecked!=true||exportFormat.Items.Count!=4||exportFormat.Items.Contains("MP4"))throw new Exception("Image/document outputs are not clearly separated from video.");
       videoMode.IsChecked=true;PumpPopupLayout();if(exportFormat.Items.Count!=2||exportFormat.Items.Contains("PNG"))throw new Exception("Video output retained image formats.");foreach(string motion in new[]{"MP4","GIF"}){exportFormat.SelectedItem=motion;PumpPopupLayout();var play=PopupChildren<Button>(dialog).Single(b=>AutomationProperties.GetName(b)=="Play animation preview");if(!play.IsVisible||!play.IsEnabled||resolution.IsEnabled)throw new Exception("Animated export controls are unavailable or retain image DPI.");play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));SmokeSearchWait(()=>Convert.ToString(play.Content).Contains("Pause"));PumpPopupLayout();var before=image.Source;SmokeSearchWait(()=>image.Source!=before);play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!Convert.ToString(play.Content).Contains("Play"))throw new Exception("Animation preview did not pause.");CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_"+motion+".png"));}
       double dialogWidth=dialog.Width,dialogHeight=dialog.Height;dialog.Width=820;dialog.Height=600;dialog.Resources["UiFontControl"]=21.0;dialog.Resources["UiFontSmall"]=18.0;dialog.Resources["UiFontTitle"]=24.0;PumpPopupLayout();
       var dialogRoot=(FrameworkElement)dialog.Content;
       if(image.ActualHeight<60)throw new Exception("Analytics controls left no usable preview in a short window.");
       foreach(var control in new FrameworkElement[]{imageMode,videoMode,PopupChildren<Button>(dialog).Single(b=>b.IsVisible&&Convert.ToString(b.Content)=="Export all…"),PopupChildren<Button>(dialog).Single(b=>b.IsVisible&&Convert.ToString(b.Content)=="Close")})if(!control.IsVisible||control.TranslatePoint(new Point(control.ActualWidth,control.ActualHeight),dialogRoot).Y>dialogRoot.ActualHeight+1)throw new Exception("Output mode or export actions are clipped in a short analytics window.");
       CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_SmallVideo.png"));dialog.Width=dialogWidth;dialog.Height=dialogHeight;foreach(string resource in new[]{"UiFontControl","UiFontSmall","UiFontTitle"})dialog.Resources.Remove(resource);
       imageMode.IsChecked=true;PumpPopupLayout();if(exportFormat.SelectedItem as string!="PDF"||exportFormat.Items.Count!=4)throw new Exception("Image format choice was not retained.");
       choices.SelectedIndex=AnalyticsWindow.CardsIndex;SmokeSearchWait(()=>status.Text.Contains("card")&&!status.Text.StartsWith("Preparing"));PumpPopupLayout();
       if(exportFormat.Items.Count!=2||!exportFormat.Items.Contains("PNG")||!PopupChildren<Button>(dialog).Single(b=>AutomationProperties.GetName(b)=="Choose card targets").IsVisible||layout.IsVisible)throw new Exception("Target cards lack their targets, layout or image formats.");
       if(!status.Text.StartsWith("3 cards"))throw new Exception("Top target cards were not prepared: "+status.Text);CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_Cards.png"));
       videoMode.IsChecked=true;PumpPopupLayout();if(!exportFormat.Items.Contains("MP4"))throw new Exception("Target card video is unavailable.");imageMode.IsChecked=true;choices.SelectedIndex=6;PumpPopupLayout();if(exportFormat.Items.Count!=4)throw new Exception("Chart formats were not restored after target cards.");exportFormat.SelectedItem="PDF";
       PopupChildren<Expander>(dialog).Single(c=>AutomationProperties.GetName(c)=="Analytics scope").IsExpanded=true;PumpPopupLayout();
       var picker=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Telescope");picker.SelectedIndex=1;
       SmokeSearchWait(()=>status.Text!="Updating analytics…");if(!PopupChildren<TextBlock>(dialog).Any(t=>t.Text.StartsWith("120 light frames")))throw new Exception("Analytics telescope scope did not refresh.");
       var from=PopupChildren<DatePicker>(dialog).Single(c=>AutomationProperties.GetName(c)=="From (inclusive)");var to=PopupChildren<DatePicker>(dialog).Single(c=>AutomationProperties.GetName(c)=="To (inclusive)");from.SelectedDate=new DateTime(2026,10,2);to.SelectedDate=new DateTime(2026,10,1);
       SmokeSearchWait(()=>status.Text!="Updating analytics…");if(status.Text!="The start date must be on or before the end date.")throw new Exception("Analytics accepted an inverted date range.");
       var export=PopupChildren<Button>(dialog).Single(b=>b.IsVisible&&Convert.ToString(b.Content)=="Export all…");if(export.IsEnabled)throw new Exception("Stale analytics remained exportable after invalid scope.");
      }finally{dialog.Close();}
     }
    }
    // Chart, document and output choices come back the next time Analytics opens.
    var remembered=AnalyticsPreferences.Defaults();int saves=0;
    Func<AnalyticsWindow,string,ComboBox> combo=(window,name)=>PopupChildren<ComboBox>(window).Single(c=>AutomationProperties.GetName(c)==name);
    var first=new AnalyticsWindow(Window,source,"Remembered choices",0,null,remembered,()=>saves++);
    try{
     first.Show();PumpPopupLayout();var firstImage=PopupChildren<Image>(first).Single(i=>AutomationProperties.GetName(i)=="Branded analytics export preview");SmokeSearchWait(()=>firstImage.Source!=null);
     combo(first,"Chart type").SelectedIndex=2;combo(first,"Order").SelectedIndex=2;combo(first,"Colour palette").SelectedIndex=3;combo(first,"Document theme").SelectedIndex=1;PumpPopupLayout();
     PopupChildren<TextBox>(first).Single(t=>AutomationProperties.GetName(t)=="Chart title").Text="Favourite targets";SmokeSearchWait(()=>PopupChildren<TextBlock>(first).Any(t=>t.Text=="Favourite targets · Export preview"));
     if(!PopupChildren<TextBlock>(first).Any(t=>t.Text=="Share of light frames · Customised"))throw new Exception("The chart list does not show a customised chart.");
     CapturePopup(first,Path.Combine(output,"AstroArchive_Analytics_Customised.png"));
     var list=PopupChildren<ListBox>(first).Single(c=>AutomationProperties.GetName(c)=="Analytics charts");list.SelectedIndex=AnalyticsWindow.CombinedIndex;PumpPopupLayout();
     PopupChildren<CheckBox>(first).Single(c=>AutomationProperties.GetName(c)=="Include "+ArchiveAnalytics.Titles[1]).IsChecked=false;PumpPopupLayout();
     if(!PopupChildren<TextBlock>(first).Any(t=>t.Text=="5 of 6 charts · Combined preview"))throw new Exception("The combined document did not leave out an unticked chart.");
     var hide=PopupChildren<Button>(first).Single(b=>AutomationProperties.GetName(b)=="Show or hide options");var paper=(FrameworkElement)firstImage.Parent;double wide=paper.ActualWidth;hide.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();
     if(combo(first,"Document theme").IsVisible||paper.ActualWidth<=wide+100)throw new Exception("Hiding options did not give the preview more room.");hide.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();
     CapturePopup(first,Path.Combine(output,"AstroArchive_Analytics_Combined_Choice.png"));
    }finally{first.Close();}
    if(saves!=1||remembered.Item!="combined"||!remembered.LightDocument||remembered.Palette!="Colour-blind safe"||remembered.Chart(0).Title!="Favourite targets"||remembered.Chart(0).Style!="columns"||remembered.Chart(0).Order!="A–Z"||remembered.IncludedCharts().Count!=5||remembered.OptionsHidden)throw new Exception("Analytics choices were not remembered when the window closed.");
    var second=new AnalyticsWindow(Window,source,"Remembered choices",-1,null,remembered,null);
    try{
     second.Show();PumpPopupLayout();var secondImage=PopupChildren<Image>(second).Single(i=>AutomationProperties.GetName(i)=="Branded analytics export preview");SmokeSearchWait(()=>secondImage.Source!=null);
     var list=PopupChildren<ListBox>(second).Single(c=>AutomationProperties.GetName(c)=="Analytics charts");if(list.SelectedIndex!=AnalyticsWindow.CombinedIndex||combo(second,"Document theme").SelectedIndex!=1||combo(second,"Colour palette").SelectedIndex!=3)throw new Exception("Analytics did not reopen with the remembered chart and document.");
     list.SelectedIndex=0;PumpPopupLayout();if(combo(second,"Chart type").SelectedIndex!=2||combo(second,"Order").SelectedIndex!=2||PopupChildren<TextBox>(second).Single(t=>AutomationProperties.GetName(t)=="Chart title").Text!="Favourite targets")throw new Exception("Analytics did not restore the chart's customisation.");
     PopupChildren<Button>(second).Single(b=>Convert.ToString(b.Content)=="Reset this chart").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();
     if(combo(second,"Chart type").SelectedIndex!=0||PopupChildren<TextBox>(second).Single(t=>AutomationProperties.GetName(t)=="Chart title").Text!="")throw new Exception("Reset this chart kept its customisation.");
    }finally{second.Close();}
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
   foreach(string transition in new[]{"Glide","Zoom","Dissolve"}){settings.Transition=transition;var movie=new AnalyticsAnimation(story,settings);AnimationReference(Path.Combine(output,"media-transition-"+transition+".png"),movie,1.75,180,320);}
   foreach(string format in new[]{"MP4","GIF"}){
    string path=Path.Combine(output,"media-cancelled."+format.ToLowerInvariant());File.WriteAllText(path,"preserve previous output");using(var cancelled=new CancellationTokenSource()){bool stopped=false;try{AnalyticsAnimation.Save(path,story,format,settings,(p,m)=>{if(p>5)cancelled.Cancel();},cancelled.Token);}catch(OperationCanceledException){stopped=true;}if(!stopped||File.ReadAllText(path)!="preserve previous output")throw new Exception("Cancelled media export overwrote an existing document.");}
   }
   if(Directory.GetFiles(output,"*.tmp.*").Length!=0)throw new Exception("Media export retained partial output.");File.WriteAllText(Path.Combine(output,"analytics-media-smoke.txt"),"PASS: H.264 MP4 and looping GIF exports, all six ratios, both themes, complete six-chart stories, transition preview, timing, progress, streaming GIF decoding and atomic cancellation.");
  }
 }
}
