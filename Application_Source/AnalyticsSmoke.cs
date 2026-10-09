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
       CapturePopup(dialog,Path.Combine(output,"AstroArchive_Analytics_"+mode+".png"));
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
    foreach(string format in new[]{"PNG","JPEG","PDF","SVG"})foreach(bool allCharts in new[]{false,true}){
     var chosen=allCharts?pages:new List<AnalyticsPage>{pages[0]};string path=Path.Combine(output,"analytics-"+(allCharts?"all":"single")+"."+(format=="JPEG"?"jpg":format.ToLowerInvariant()));
     AnalyticsExport.Save(path,chosen,format,150);if(new FileInfo(path).Length<100)throw new Exception("Empty analytics export: "+format);
     if(format=="PNG"||format=="JPEG"){
      using(var input=File.OpenRead(path)){var image=BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];if(image.PixelWidth!=(allCharts?3750:1875)||image.PixelHeight!=(allCharts?3750:1250))throw new Exception("Incorrect raster export dimensions.");}
     }else if(format=="SVG")XDocument.Load(path);else if(!File.ReadAllText(path).StartsWith("%PDF-1.4"))throw new Exception("Incorrect PDF export header.");
    }
    string overwrite=Path.Combine(output,"analytics-overwrite.png");File.WriteAllText(overwrite,"existing output");AnalyticsExport.Save(overwrite,new[]{pages[0]},"PNG",150);
    using(var input=File.OpenRead(overwrite))BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
    File.WriteAllText(overwrite,"keep on failure");bool failed=false;try{AnalyticsExport.Save(overwrite,new[]{pages[0]},"invalid",150);}catch(ArgumentException){failed=true;}
    if(!failed||File.ReadAllText(overwrite)!="keep on failure"||Directory.GetFiles(output,"*.tmp").Length!=0)throw new Exception("Failed export changed the destination or retained partial output.");
    File.WriteAllText(Path.Combine(output,"analytics-smoke.txt"),"PASS: Repository menu, six reports, combined preview, light/dark themes, telescope/date scoping, stale-export prevention, PNG/JPEG decoding, SVG/PDF outputs and atomic overwrite checks.");
   }finally{repo=savedRepo;Theme.Apply(Window,theme);}
  }
 }
}
