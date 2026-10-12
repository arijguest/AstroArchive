using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroArchive {
 // How one chart is presented. Blank values follow the chart's own defaults, so
 // improved defaults still reach charts nobody has customised.
 public sealed class AnalyticsChartOptions {
  public string Title{get;set;}public string Subtitle{get;set;}public string Style{get;set;}public string Order{get;set;}
  // Only settings written by the unpublished 3.1.5 build carry a palette per chart.
  public string Palette{get;set;}
  public bool Unchanged(){return string.IsNullOrWhiteSpace(Title)&&string.IsNullOrWhiteSpace(Subtitle)&&Style==null&&Order==null;}
  public AnalyticsChartOptions Copy(){return (AnalyticsChartOptions)MemberwiseClone();}
 }
 // Everything Analytics remembers between visits. Telescope, dates and the
 // document label belong to one repository, so they are not kept.
 public sealed class AnalyticsPreferences {
  public const int TitleLength=60,SubtitleLength=140;
  public static readonly string[] Items={"targets","timeline","target-time","telescope-time","filters","exposures","combined","cards"};
  public static readonly string[] Orders={"Largest first","Smallest first","A–Z"};
  public static readonly string[] Palettes={"AstroArchive","Nebula","Aurora","Colour-blind safe"};
  public string Item{get;set;}
  public bool LightDocument{get;set;}
  public int Layout{get;set;}
  public string Palette{get;set;}
  public bool Video{get;set;}
  public string ImageFormat{get;set;}public string CardFormat{get;set;}public string VideoFormat{get;set;}
  public bool ScreenResolution{get;set;}
  public int Pace{get;set;}public int Transition{get;set;}public int VideoSize{get;set;}public int CardResolution{get;set;}
  public bool OptionsHidden{get;set;}
  // Report ids in the combined document, in chart order. Null keeps all six.
  public List<string> Combined{get;set;}
  public Dictionary<string,AnalyticsChartOptions> Charts{get;set;}
  // Rankings can be reordered; the timeline and exposure lengths keep their own order.
  public static bool Orderable(int index){return index==0||index==2||index==3||index==4;}
  public AnalyticsChartOptions Chart(int index){
   if(index<0||index>5)return new AnalyticsChartOptions();if(Charts==null)Charts=new Dictionary<string,AnalyticsChartOptions>(StringComparer.Ordinal);
   AnalyticsChartOptions options;if(!Charts.TryGetValue(Items[index],out options)){options=new AnalyticsChartOptions();Charts[Items[index]]=options;}return options;
  }
  public bool Included(int index){return index>=0&&index<6&&(Combined==null||Combined.Contains(Items[index]));}
  public List<int> IncludedCharts(){return Enumerable.Range(0,6).Where(Included).ToList();}
  // Keeps at least one chart in the combined document.
  public bool Include(int index,bool include){
   if(index<0||index>5)return false;var chosen=IncludedCharts();if(include){if(!chosen.Contains(index))chosen.Add(index);}else{if(chosen.Count==1&&chosen[0]==index)return false;chosen.Remove(index);}
   chosen.Sort();Combined=chosen.Count==6?null:chosen.Select(i=>Items[i]).ToList();return true;
  }
  public int ItemIndex(){return Math.Max(0,Array.IndexOf(Items,Item??""));}
  public static AnalyticsPreferences Defaults(){var value=new AnalyticsPreferences();value.Normalise();return value;}
  // Returns every design and export choice to its default. The chart being
  // viewed and whether the options panel is shown are left as they are.
  public void Reset(){
   var value=Defaults();LightDocument=value.LightDocument;Layout=value.Layout;Palette=value.Palette;Video=value.Video;ImageFormat=value.ImageFormat;CardFormat=value.CardFormat;VideoFormat=value.VideoFormat;
   ScreenResolution=value.ScreenResolution;Pace=value.Pace;Transition=value.Transition;VideoSize=value.VideoSize;CardResolution=value.CardResolution;Combined=null;Charts=value.Charts;
  }
  // Reads saved preferences, or migrates the per-chart choices of the
  // unpublished 3.1.5 build, and repairs anything out of range.
  public static AnalyticsPreferences Load(AnalyticsPreferences saved,Dictionary<string,AnalyticsChartOptions> legacy){
   var value=saved??new AnalyticsPreferences();
   if(legacy!=null&&(value.Charts==null||value.Charts.Count==0)){
    value.Charts=new Dictionary<string,AnalyticsChartOptions>(StringComparer.Ordinal);
    foreach(var pair in legacy){if(pair.Value!=null)value.Charts[pair.Key]=pair.Value.Copy();}
    if(value.Palette==null)value.Palette=legacy.Values.Where(o=>o!=null).Select(o=>Known(Palettes,o.Palette)).FirstOrDefault(p=>p!=null&&p!=Palettes[0]);
   }
   value.Normalise();return value;
  }
  static string Known(string[] choices,string value){return value==null?null:choices.FirstOrDefault(c=>string.Equals(c,value.Trim(),StringComparison.OrdinalIgnoreCase));}
  static int Clamp(int value,int count){return Math.Max(0,Math.Min(count-1,value));}
  static string Limit(string text,int length){if(string.IsNullOrWhiteSpace(text))return null;text=text.Trim();return text.Length<=length?text:text.Substring(0,length);}
  public void Normalise(){
   if(Item!=null&&Array.IndexOf(Items,Item)<0)Item=null;
   Layout=Clamp(Layout,Enum.GetValues(typeof(AnalyticsLayout)).Length);Pace=Clamp(Pace,3);Transition=Clamp(Transition,3);VideoSize=Clamp(VideoSize,3);CardResolution=Clamp(CardResolution,3);
   Palette=Known(Palettes,Palette);if(Palette==Palettes[0])Palette=null;
   ImageFormat=Known(new[]{"PNG","JPEG","PDF","SVG"},ImageFormat)??"PDF";CardFormat=Known(new[]{"PNG","JPEG"},CardFormat)??"PNG";VideoFormat=Known(new[]{"MP4","GIF"},VideoFormat)??"MP4";
   if(Combined!=null){var chosen=Enumerable.Range(0,6).Where(i=>Combined.Contains(Items[i])).ToList();Combined=chosen.Count==0||chosen.Count==6?null:chosen.Select(i=>Items[i]).ToList();}
   var charts=new Dictionary<string,AnalyticsChartOptions>(StringComparer.Ordinal);
   if(Charts!=null)foreach(var pair in Charts){
    int index=Array.IndexOf(Items,pair.Key);if(index<0||index>5||pair.Value==null)continue;var options=pair.Value;
    options.Title=Limit(options.Title,TitleLength);options.Subtitle=Limit(options.Subtitle,SubtitleLength);options.Palette=null;
    // The 3.1.5 build wrote "Automatic", "Donut" and "Bars"; chart types are now lower-case ids.
    var styles=AnalyticsGraphics.StylesFor(index);options.Style=Known(styles,options.Style);if(options.Style==styles[0])options.Style=null;
    string order=options.Order==null?null:options.Order.Trim();
    if(string.Equals(order,"Lowest first",StringComparison.OrdinalIgnoreCase))order=Orders[1];else if(string.Equals(order,"Alphabetical",StringComparison.OrdinalIgnoreCase))order=Orders[2];
    order=Orderable(index)?Known(Orders,order):null;options.Order=order==Orders[0]?null:order;
    if(!options.Unchanged())charts[pair.Key]=options;
   }
   Charts=charts;
  }
 }
 public static partial class AnalyticsGraphics {
  static readonly string[][] PaletteColours={
   new[]{"#8756A5","#CB5078","#5951D6","#008477","#B37608","#6B813A","#427AB5","#64748B"},new[]{"#C19AFF","#ED9ACB","#90B8FF","#55DFEA","#F0C36A","#9DD7B0","#4F9BFA","#A6B4CC"},
   new[]{"#008477","#427AB5","#6B813A","#5951D6","#B37608","#8756A5","#CB5078","#64748B"},new[]{"#55DFEA","#90B8FF","#9DD7B0","#C19AFF","#F0C36A","#4F9BFA","#ED9ACB","#A6B4CC"},
   // Okabe–Ito hues, darkened for white paper and lifted for the midnight theme.
   new[]{"#0072B2","#D55E00","#009E73","#CC79A7","#8F7A00","#56B4E9","#E69F00","#666666"},new[]{"#56B4E9","#E69F00","#2EC4A0","#E08FBD","#F0E442","#7FA8FF","#F07D3A","#BBBBBB"}};
  // A customised copy: title, subtitle, chart type and order change only the
  // presentation. Every group stays in the document, and smaller groups are
  // gathered into Other by size before a donut or column chart is reordered.
  public static List<AnalyticsPage> Pages(AnalyticsSnapshot data,int index,bool dark,AnalyticsLayout layout,AnalyticsChartOptions options,string palette){
   List<AnalyticsPage> pages;
   if(options==null||options.Unchanged())pages=Pages(data,index,dark,layout);
   else{
    var copy=data.Copy();var report=copy.Reports[index];
    if(!string.IsNullOrWhiteSpace(options.Title))report.Title=options.Title.Trim();
    if(!string.IsNullOrWhiteSpace(options.Subtitle))report.Description=options.Subtitle.Trim();
    string style=options.Style!=null&&StylesFor(index).Contains(options.Style)?options.Style:report.Style;
    if(style!=report.Style){
     // Donuts and columns read best by size; ordered lengths keep their order as bars.
     if(style!="bars"&&report.Style=="columns")report.Values=report.Values.OrderByDescending(v=>v.Value).ThenBy(v=>v.Label,StringComparer.OrdinalIgnoreCase).ToList();
     if(style=="columns")report.Values=ArchiveAnalytics.Compact(report.Values.OrderByDescending(v=>v.Value),12);
     report.Style=style;
    }
    string order=AnalyticsPreferences.Orderable(index)?options.Order:null;
    if(order!=null&&order!=AnalyticsPreferences.Orders[0]){
     if(style=="bars")report.Values=Arrange(report.Values,order);
     else{
      int limit=style=="columns"?12:8;var compact=ArchiveAnalytics.Compact(report.Values.OrderByDescending(v=>v.Value).ThenBy(v=>v.Label,StringComparer.OrdinalIgnoreCase),limit);
      var other=report.Values.Count>limit?compact[compact.Count-1]:null;var shown=Arrange(other==null?compact:compact.Take(compact.Count-1),order);if(other!=null)shown.Add(other);report.Values=shown;
     }
    }
    pages=Pages(copy,index,dark,layout);
   }
   foreach(var page in pages)Recolour(page,dark,palette);
   return pages;
  }
  static List<AnalyticsValue> Arrange(IEnumerable<AnalyticsValue> values,string order){
   return order==AnalyticsPreferences.Orders[2]?values.OrderBy(v=>v.Label,StringComparer.CurrentCultureIgnoreCase).ToList():values.OrderBy(v=>v.Value).ThenBy(v=>v.Label,StringComparer.OrdinalIgnoreCase).ToList();
  }
  // Swaps the chart's series colours for another palette. Lettering keeps its
  // theme colours, so contrast checks still hold.
  public static void Recolour(AnalyticsPage page,bool dark,string palette){
   int choice=Array.IndexOf(AnalyticsPreferences.Palettes,palette??"");if(choice<1)return;
   string[] original=dark?DarkColours:Colours,replacement=PaletteColours[(choice-1)*2+(dark?1:0)];
   foreach(var mark in page.Marks){
    if(mark.Role!="chart"||mark.Kind=="text"||mark.Fill==null)continue;int i=Array.IndexOf(original,mark.Fill);if(i<0)continue;
    mark.Fill=replacement[i];if(mark.FillEnd!=null)mark.FillEnd=Blend(mark.Fill,dark?"#735AF5":"#3B4EB5",.38);
   }
  }
 }
}
