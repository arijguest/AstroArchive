using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public sealed partial class AnalyticsWindow {
  AnalyticsChartOptions ChartConfiguration(int index){AnalyticsChartOptions value;return configurations.TryGetValue(snapshot.Reports[index].Id,out value)?value:null;}
  double CustomDuration(){double seconds;return double.TryParse(duration.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out seconds)&&seconds>=1&&seconds<=300&&!double.IsNaN(seconds)&&!double.IsInfinity(seconds)?seconds:0;}
  FormWindow ConfigurationDialog(){
   StopPlayback();var form=new FormWindow(this,"Customize analytics chart",660,760);
   form.Text("Make it yours",true);form.Text("Changes apply to this chart in the preview and every export. Repository headline totals stay complete.");
   var graph=form.Select("Chart",ArchiveAnalytics.Titles,ArchiveAnalytics.Titles[charts.SelectedIndex==6?0:charts.SelectedIndex]);
   var image=new Image{Height=140,Margin=new Thickness(0,8,0,8)};AccessibleName(image,"Custom chart preview");form.Add(image);
   var title=form.Input("Title (blank uses default)","");title.MaxLength=60;var subtitle=form.Input("Subtitle (blank uses default)","");subtitle.MaxLength=140;
   var options=new WrapPanel{Margin=new Thickness(0,10,0,0)};form.Add(options);
   var style=new ComboBox{ItemsSource=new[]{"Automatic","Donut","Bars"},SelectedIndex=0,Width=260};var density=new ComboBox{ItemsSource=new[]{"4 · Bold","6 · Balanced","8 · Detailed"},SelectedIndex=2,Width=260};
   var order=new ComboBox{ItemsSource=new[]{"Highest first","Lowest first","Alphabetical"},SelectedIndex=0,Width=260};var palette=new ComboBox{ItemsSource=new[]{"AstroArchive","Nebula","Aurora"},SelectedIndex=0,Width=260};
   AddField(options,"Chart style",style);AddField(options,"Categories per page",density);AddField(options,"Order",order);AddField(options,"Colour palette",palette);
   foreach(FrameworkElement field in options.Children)field.Margin=new Thickness(0,0,16,10);
   form.Text("Rankings keep every category on continuation pages. Donuts combine remaining categories in Other. Timeline and exposure bins retain their chronological order.");
   bool loading=false;
   Func<AnalyticsChartOptions> draft=()=>new AnalyticsChartOptions{Title=title.Text,Subtitle=subtitle.Text,Style=Convert.ToString(style.SelectedItem),Categories=density.SelectedIndex==0?4:density.SelectedIndex==1?6:8,Order=Convert.ToString(order.SelectedItem),Palette=Convert.ToString(palette.SelectedItem)};
   Action draw=()=>{if(loading)return;image.Source=AnalyticsExport.Preview(new[]{AnalyticsGraphics.Page(snapshot,graph.SelectedIndex,documentTheme.SelectedIndex==0,(AnalyticsLayout)documentLayout.SelectedIndex,draft())});};
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
