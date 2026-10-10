using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
     repo=fixture;var menu=AnalyticsMenu();if(!menu.IsEnabled||menu.Items.OfType<MenuItem>().Count()!=7)throw new Exception("Analytics menu does not offer six reports and Export all.");
     foreach(string mode in new[]{"Light","Dark"}){
      Theme.Apply(Window,mode);var dialog=new AnalyticsWindow(Window,source,"Observatory & field notes",0);
      try{
       dialog.Show();PumpPopupLayout();var image=PopupChildren<Image>(dialog).Single(i=>AutomationProperties.GetName(i)=="Branded analytics export preview");
       var status=PopupChildren<TextBlock>(dialog).Single(t=>AutomationProperties.GetName(t)=="Analytics status");
       SmokeSearchWait(()=>image.Source!=null);PumpPopupLayout();VerifyWindowIcon(dialog);Readable(dialog.Foreground,dialog.Background,"Analytics "+mode);
       var choices=PopupChildren<ListBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Analytics charts");if(choices.Items.Count!=7)throw new Exception("Analytics chart navigation is incomplete.");
       choices.SelectedIndex=6;PumpPopupLayout();if(!PopupChildren<TextBlock>(dialog).Any(t=>t.Text=="All six charts · Combined preview"))throw new Exception("Combined preview is unavailable.");
       var documentTheme=PopupChildren<ComboBox>(dialog).Single(c=>AutomationProperties.GetName(c)=="Document theme");if(Convert.ToString(documentTheme.SelectedItem)!="Dark")throw new Exception("Analytics document theme did not default to Dark independently of application theme.");
       CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_DarkDocument.png"));var darkPreview=image.Source;documentTheme.SelectedIndex=1;PumpPopupLayout();if(image.Source==darkPreview)throw new Exception("Light document selection did not redraw the preview.");CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+"_LightDocument.png"));documentTheme.SelectedIndex=0;
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
    foreach(bool dark in new[]{true,false})foreach(string format in new[]{"PNG","JPEG","PDF","SVG"})foreach(bool allCharts in new[]{false,true}){
     var themed=Enumerable.Range(0,6).SelectMany(i=>AnalyticsGraphics.Pages(data,i,dark)).ToList();var chosen=allCharts?themed:new List<AnalyticsPage>{themed[0]};string path=Path.Combine(output,"analytics-"+(dark?"dark-":"light-")+(allCharts?"all":"single")+"."+(format=="JPEG"?"jpg":format.ToLowerInvariant()));
     AnalyticsExport.Save(path,chosen,format,150);if(new FileInfo(path).Length<100)throw new Exception("Empty analytics export: "+format);
     if(format=="PNG"||format=="JPEG"){
      using(var input=File.OpenRead(path)){var image=BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];if(image.PixelWidth!=(allCharts?3750:1875)||image.PixelHeight!=(allCharts?3750:1250))throw new Exception("Incorrect raster export dimensions.");var rgb=new System.Windows.Media.Imaging.FormatConvertedBitmap(image,System.Windows.Media.PixelFormats.Bgra32,null,0);var pixel=new byte[4];rgb.CopyPixels(new Int32Rect(0,25,1,1),pixel,4,0);if(dark?pixel[2]>20||pixel[1]>25||pixel[0]>40:pixel[2]<250||pixel[1]<250||pixel[0]<250)throw new Exception("Raster export background does not match document theme.");}
     }else if(format=="SVG"){var svg=XDocument.Load(path);XNamespace ns="http://www.w3.org/2000/svg";if(svg.Root.Elements(ns+"rect").First().Attribute("fill").Value!=chosen[0].Background)throw new Exception("SVG document theme does not match the preview.");}else if(!File.ReadAllText(path).StartsWith("%PDF-1.4"))throw new Exception("Incorrect PDF export header.");
    }
    string overwrite=Path.Combine(output,"analytics-overwrite.png");File.WriteAllText(overwrite,"existing output");AnalyticsExport.Save(overwrite,new[]{pages[0]},"PNG",150);
    using(var input=File.OpenRead(overwrite))BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
    File.WriteAllText(overwrite,"keep on failure");bool failed=false;try{AnalyticsExport.Save(overwrite,new[]{pages[0]},"invalid",150);}catch(ArgumentException){failed=true;}
    if(!failed||File.ReadAllText(overwrite)!="keep on failure"||Directory.GetFiles(output,"*.tmp").Length!=0)throw new Exception("Failed export changed the destination or retained partial output.");
    File.WriteAllText(Path.Combine(output,"analytics-smoke.txt"),"PASS: Repository menu, six reports, combined preview, independent dark/light document and application themes, themed PNG/JPEG/PDF/SVG exports, telescope/date scoping, stale-export prevention, PNG/JPEG decoding, SVG/PDF outputs and atomic overwrite checks.");
   }finally{repo=savedRepo;Theme.Apply(Window,theme);}
  }
 }
}
