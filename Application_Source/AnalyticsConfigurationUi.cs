using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 public sealed partial class AnalyticsWindow {
  AnalyticsChartOptions ChartConfiguration(int index){AnalyticsChartOptions value;return configurations.TryGetValue(snapshot.Reports[index].Id,out value)?value:null;}
  double CustomDuration(){double seconds;return double.TryParse(duration.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out seconds)&&seconds>=1&&seconds<=300&&!double.IsNaN(seconds)&&!double.IsInfinity(seconds)?seconds:0;}
  FormWindow ConfigurationDialog(){
   StopPlayback();var form=new FormWindow(this,"Customize analytics chart",660,760);
   form.Text("Make it yours",true);form.Text("Changes apply to this chart in the preview and every export. Repository headline totals stay complete.");
   var graph=form.Select("Chart",ArchiveAnalytics.Titles,ArchiveAnalytics.Titles[charts.SelectedIndex==6?0:charts.SelectedIndex]);
   var image=new Image{Height=170,Margin=new Thickness(0,12,0,8)};AccessibleName(image,"Custom chart preview");form.Add(image);
   var title=form.Input("Title (blank uses default)","");title.MaxLength=60;var subtitle=form.Input("Subtitle (blank uses default)","");subtitle.MaxLength=140;
   var style=form.Select("Chart style",new[]{"Automatic","Donut","Bars"},"Automatic");var density=form.Select("Categories per page",new[]{"4 · Bold","6 · Balanced","8 · Detailed"},"8 · Detailed");
   var order=form.Select("Order",new[]{"Highest first","Lowest first","Alphabetical"},"Highest first");var palette=form.Select("Colour palette",new[]{"AstroArchive","Nebula","Aurora"},"AstroArchive");
   form.Text("Rankings keep every category on continuation pages. Donuts combine remaining categories in Other. Timeline and exposure bins retain their chronological order.");
   bool loading=false;
   Func<AnalyticsChartOptions> draft=()=>new AnalyticsChartOptions{Title=title.Text,Subtitle=subtitle.Text,Style=Convert.ToString(style.SelectedItem),Categories=density.SelectedIndex==0?4:density.SelectedIndex==1?6:8,Order=Convert.ToString(order.SelectedItem),Palette=Convert.ToString(palette.SelectedItem)};
   Action draw=()=>{if(loading)return;image.Source=AnalyticsExport.Preview(AnalyticsGraphics.Pages(snapshot,graph.SelectedIndex,documentTheme.SelectedIndex==0,(AnalyticsLayout)documentLayout.SelectedIndex,draft()).GetRange(0,1));};
   Action<AnalyticsChartOptions> fill=value=>{loading=true;value=value??new AnalyticsChartOptions();title.Text=value.Title??"";subtitle.Text=value.Subtitle??"";style.SelectedItem=value.Style??"Automatic";density.SelectedIndex=value.Categories==4?0:value.Categories==6?1:2;order.SelectedItem=value.Order??"Highest first";palette.SelectedItem=value.Palette??"AstroArchive";if(style.SelectedIndex<0)style.SelectedIndex=0;if(order.SelectedIndex<0)order.SelectedIndex=0;if(palette.SelectedIndex<0)palette.SelectedIndex=0;style.IsEnabled=order.IsEnabled=density.IsEnabled=graph.SelectedIndex!=1&&graph.SelectedIndex!=5;loading=false;draw();};
   graph.SelectionChanged+=(s,e)=>fill(ChartConfiguration(graph.SelectedIndex));title.TextChanged+=(s,e)=>draw();subtitle.TextChanged+=(s,e)=>draw();style.SelectionChanged+=(s,e)=>draw();density.SelectionChanged+=(s,e)=>draw();order.SelectionChanged+=(s,e)=>draw();palette.SelectionChanged+=(s,e)=>draw();
   form.FooterButton("Reset defaults",()=>fill(null));
   var error=new TextBlock{TextWrapping=TextWrapping.Wrap};Theme.Bind(error,TextBlock.ForegroundProperty,"Muted");form.Add(error);
   form.Accept("Apply",()=>{string id=snapshot.Reports[graph.SelectedIndex].Id;var next=draft();AnalyticsChartOptions previous;bool existed=configurations.TryGetValue(id,out previous);configurations[id]=next;
    try{if(saveConfigurations!=null)saveConfigurations();BuildPages();RenderPreview();return true;}catch(Exception failure){if(existed)configurations[id]=previous;else configurations.Remove(id);error.Text="Changes could not be saved. "+failure.Message;return false;}
   });
   fill(ChartConfiguration(graph.SelectedIndex));return form;
  }
 }
}
