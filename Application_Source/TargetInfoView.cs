using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 // One scrolling surface keeps the image, facts, map and references reachable.
 public sealed class TargetInfoView:UserControl {
  readonly StackPanel content=new StackPanel();readonly ScrollViewer scroll;
  Grid body,mapHost;StackPanel article,mapPanel;SkyGlobeView globe;Image photo;Button reset;
  public Action<string> ChooseObject;
  public string CurrentId{get;private set;}
  public TargetInfoView(){
   scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(4,2,4,8)};
   Content=scroll;AutomationProperties.SetName(this,"Target information");SizeChanged+=(s,e)=>ArrangeBody();scroll.SizeChanged+=(s,e)=>ArrangeBody();scroll.ScrollChanged+=(s,e)=>{if(Math.Abs(e.ViewportHeightChange)>0.1)ArrangeBody();};
  }
  static TextBlock Text(string value,string colour="Text",string size="UiFontControl"){
   var text=new TextBlock{Text=value??"",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};text.SetResourceReference(TextBlock.ForegroundProperty,colour);text.SetResourceReference(TextBlock.FontSizeProperty,size);return text;
  }
  // Scientific names, prose and attribution must not be treated as UI labels.
  static TextBlock Data(string value,string colour="Text",string size="UiFontControl"){
   var text=Text("",colour,size);text.SetBinding(TextBlock.TextProperty,new Binding{Source=value??""});return text;
  }
  void Begin(string title){
   CurrentId=null;body=null;mapPanel=null;mapHost=null;globe=null;photo=null;reset=null;content.Children.Clear();scroll.ScrollToTop();
   var label=Text("TARGET INFO","Muted","UiFontCaption");label.FontWeight=FontWeights.SemiBold;content.Children.Add(label);
   var heading=Data(title,"Text","UiFontHeading");heading.FontWeight=FontWeights.SemiBold;content.Children.Add(heading);
  }
  public void ShowMessage(string title,string message){Begin(Localization.T(title));content.Children.Add(Text(message,"Muted"));}
  public void ShowChoices(string target,TargetInfoResult result){
   Begin(target);content.Children.Add(Text(result.Message,"Muted"));
   foreach(var card in result.Cards){string id=card.id;var button=new Button{Content=Data(card.name+" · "+card.id+"\n"+card.type_label),HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,0,6),Padding=new Thickness(10,8,10,4)};AutomationProperties.SetName(button,"Read about "+card.name+" "+id);button.Click+=(s,e)=>{if(ChooseObject!=null)ChooseObject(id);};content.Children.Add(button);}
  }
  public void ShowCard(TargetInfoCard card,string context=null){
   Begin(card.name);CurrentId=card.id;
   if(!string.IsNullOrEmpty(context))content.Children.Add(Data(context,"Muted","UiFontCaption"));
   string constellation=TargetInfoPresentation.Constellation(card.constellation);
   // The ID is shown once: in the heading when the object has no other name, otherwise here.
   content.Children.Add(Data(TargetInfoPresentation.Tagline(card,constellation),"Muted"));
   var curated=TargetDescriptions.For(card.id);
   if(card.identity_status=="disputed"||card.domain=="historical_designation")content.Children.Add(Text("Historical designation · identity is disputed.","Muted"));
   if(card.Thumbnail!=null&&card.image!=null){try{
    var bitmap=new BitmapImage();using(var stream=new MemoryStream(card.Thumbnail)){bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=960;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();}
    photo=new Image{Source=bitmap,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,2,0,6)};RenderOptions.SetBitmapScalingMode(photo,BitmapScalingMode.HighQuality);AutomationProperties.SetName(photo,card.name);content.Children.Add(photo);
    AddImageCredit(card.image);
   }catch(NotSupportedException){}catch(IOException){}catch(ArgumentException){}}
   body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition());body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});content.Children.Add(body);
   article=new StackPanel();body.Children.Add(article);
   string summary=TargetInfoPresentation.DisplaySummary(card);string factText=curated!=null&&!string.IsNullOrWhiteSpace(curated.Fact)?curated.Fact:card.fact;
   article.Children.Add(Section("OVERVIEW",true));article.Children.Add(string.IsNullOrWhiteSpace(summary)?Text("No information is available for this target.","Muted"):Data(summary));
   if(!string.IsNullOrWhiteSpace(factText))article.Children.Add(Callout(Data(factText)));
   if(curated!=null&&!string.IsNullOrWhiteSpace(curated.Imaging)){article.Children.Add(Section("IMAGING NOTES"));article.Children.Add(Callout(Data(curated.Imaging)));}
   article.Children.Add(Section("AT A GLANCE"));var facts=Facts();article.Children.Add(facts);
   if(!string.IsNullOrEmpty(card.parent_body))AddFact(facts,"Parent",card.parent_body,null);
   var measurements=card.measurements??new TargetInfoMeasurement[0];
   var ra=measurements.FirstOrDefault(m=>m.field=="RA");var dec=measurements.FirstOrDefault(m=>m.field=="Dec");
   if(ra!=null)AddFact(facts,"RA (J2000)",Hours(ra.value),Evidence(card,ra));
   if(dec!=null)AddFact(facts,"Dec (J2000)",dec.value.ToString("+0.###;-0.###;0",CultureInfo.InvariantCulture)+"°",Evidence(card,dec));
   var major=measurements.FirstOrDefault(m=>m.field=="MajAx");var minor=measurements.FirstOrDefault(m=>m.field=="MinAx");
   if(major!=null)AddFact(facts,"Apparent size",major.value.ToString("0.##",CultureInfo.InvariantCulture)+(minor==null?"":" × "+minor.value.ToString("0.##",CultureInfo.InvariantCulture))+" arcmin",Evidence(card,major));
   var magnitude=measurements.FirstOrDefault(m=>m.field=="V-Mag")??measurements.FirstOrDefault(m=>m.field=="B-Mag");
   if(magnitude!=null)AddFact(facts,Localization.F("Magnitude ({0})",magnitude.field.Substring(0,1)),magnitude.value.ToString("0.##",CultureInfo.InvariantCulture),Evidence(card,magnitude));
   if(card.domain=="deep_sky"){
    mapPanel=new StackPanel{Margin=new Thickness(0,4,0,10)};body.Children.Add(mapPanel);mapPanel.Children.Add(Section("LOCATION IN THE SKY"));
    if(ra!=null&&dec!=null){
     var sky=CaptureSky.Resolve(new Frame{Target=card.id,RA=ra.value,Dec=dec.value},null);sky.TargetLabel=card.name;sky.TimeLabel="J2000 catalogue position";sky.Summary="Celestial sky";sky.Key="target-info:"+card.id;sky.Evidence="J2000 catalogue coordinates; no observing time or local horizon is implied.";
     mapHost=new Grid{Height=230,MinHeight=160};mapPanel.Children.Add(mapHost);
     globe=new SkyGlobeView{Margin=new Thickness(4)};globe.SetContext(sky);globe.Camera.SetHome(90-ra.value,dec.value);globe.ResetView();AutomationProperties.SetName(globe,"Sky location of "+card.name);mapHost.Children.Add(globe);
     reset=new Button{Width=26,Height=26,MinHeight=0,Padding=new Thickness(0),Margin=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
     var icon=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 5,7 A 7,7 0 1 1 5,15 M 5,3 L 5,7 L 9,7"),Width=18,Height=18,Stretch=Stretch.Uniform,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new Binding("Foreground"){Source=reset});reset.Content=icon;AutomationProperties.SetName(reset,"Reset sky view");UiHelp.Hint(reset,"Reset sky view.");reset.Click+=(s,e)=>globe.ResetView();mapHost.Children.Add(reset);globe.GlobeResized+=(s,e)=>PositionReset();mapHost.SizeChanged+=(s,e)=>PositionReset();
     mapPanel.Children.Add(Text("Catalogue position · drag to rotate. Scroll or pinch over the map to zoom.","Muted","UiFontCaption"));
    }else mapPanel.Children.Add(Text("Catalogue coordinates are not available for this target.","Muted"));
   }
   var sources=new StackPanel();
   var hidden=curated==null?new string[0]:curated.HiddenAliases;
   var aliases=(card.aliases??new string[0]).Where(a=>TargetInfoCatalog.Normalize(a)!=TargetInfoCatalog.Normalize(card.id)&&!string.Equals(a,card.name,StringComparison.OrdinalIgnoreCase)&&!hidden.Contains(a,StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   if(aliases.Length>0)sources.Children.Add(Data(Localization.F("Catalogue names: {0}",string.Join(" · ",aliases)),"Muted","UiFontCaption"));
   var moreFacts=Facts();
   foreach(var measurement in measurements.Where(m=>new[]{"PosAng","SurfBr","RadVel","Redshift"}.Contains(m.field)))AddFact(moreFacts,MeasurementLabel(measurement.field),measurement.value.ToString("0.#####",CultureInfo.InvariantCulture)+(measurement.unit=="dimensionless"?"":" "+measurement.unit),Evidence(card,measurement));
   if(moreFacts.Children.Count>0)sources.Children.Add(moreFacts);
   foreach(var source in TargetInfoPresentation.Sources(card,curated!=null&&!string.IsNullOrWhiteSpace(curated.Overview)))sources.Children.Add(Link(source.Title,source.Url));
   if(sources.Children.Count>0){content.Children.Add(Section("SOURCES"));content.Children.Add(Disclosure("Sources and catalogue details",sources));}
   content.Children.Add(Text("Select a file to return to its image preview.","Muted","UiFontCaption"));ArrangeBody();
  }
  void AddImageCredit(TargetInfoImage image){
   string credit=TargetInfoPresentation.Credit(image);bool longCredit=credit.Length>120||credit.Contains("://");
   var line=Text("","Muted","UiFontCaption");line.ToolTip=Data(image.attribution+"\n"+image.modifications,"Text","UiFontSmall");line.Inlines.Add(Linked(longCredit&&!string.IsNullOrWhiteSpace(image.creator)?image.creator:credit,image.file_page_url));line.Inlines.Add(new Run(" · "));line.Inlines.Add(Linked(image.licence,image.licence_url));content.Children.Add(line);
   if(longCredit){var full=new StackPanel();full.Children.Add(Data(image.attribution,"Muted","UiFontCaption"));content.Children.Add(Disclosure("Full image credit",full));}
   string caption=TargetInfoPresentation.Caption(image.representation);if(caption.Length>0)content.Children.Add(Data(caption,"Muted","UiFontCaption"));
  }
  // Sections share a quiet caption and divider so the pane reads as distinct parts.
  static FrameworkElement Section(string title,bool first=false){
   var panel=new StackPanel{Margin=new Thickness(0,first?6:12,0,6)};
   if(!first){var rule=new Border{Height=1,Margin=new Thickness(0,0,0,8),Opacity=0.7};rule.SetResourceReference(Border.BackgroundProperty,"Border");panel.Children.Add(rule);}
   var caption=Text(title,"Muted","UiFontCaption");caption.FontWeight=FontWeights.SemiBold;caption.Margin=new Thickness(0);panel.Children.Add(caption);return panel;
  }
  static Border Callout(UIElement child){var callout=new Border{Padding=new Thickness(10,8,10,2),CornerRadius=new CornerRadius(6),Margin=new Thickness(0,2,0,8),Child=child};callout.SetResourceReference(Border.BackgroundProperty,"SurfaceAlt");return callout;}
  static Grid Facts(){var grid=new Grid{Margin=new Thickness(0,4,0,10)};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto,MaxWidth=115});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});return grid;}
  static void AddFact(Grid grid,string label,string value,string evidence){
   int row=grid.RowDefinitions.Count;grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var key=Text(label,"Muted");key.Margin=new Thickness(0,0,12,7);Grid.SetRow(key,row);grid.Children.Add(key);var val=Data(value);if(!string.IsNullOrWhiteSpace(evidence))val.ToolTip=Data(evidence,"Text","UiFontSmall");Grid.SetRow(val,row);Grid.SetColumn(val,1);grid.Children.Add(val);
  }
  static string MeasurementLabel(string field){switch(field){case "PosAng":return "Position angle";case "SurfBr":return "Surface brightness";case "RadVel":return "Radial velocity";case "Redshift":return "Redshift";default:return field;}}
  static string Hours(double degrees){int seconds=(int)Math.Round(degrees/15*3600)%86400;return (seconds/3600).ToString("00")+"h "+(seconds%3600/60).ToString("00")+"m "+(seconds%60).ToString("00")+"s";}
  static string Evidence(TargetInfoCard card,TargetInfoMeasurement m){var citation=(card.citations??new TargetInfoCitation[0]).FirstOrDefault(c=>c.id==m.citation_id);return m.qualifier+(citation==null?"":"\n"+citation.source_title+"\n"+citation.source_url);}
  static Expander Disclosure(string title,StackPanel panel){return new Expander{Header=Text(title,"Muted"),Content=panel,Margin=new Thickness(0,2,0,6),HorizontalContentAlignment=HorizontalAlignment.Stretch};}
  internal static Inline Linked(string label,string url){
   Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))return new Run(label);
   var link=new Hyperlink(new Run(label)){NavigateUri=uri,ToolTip=url};link.SetResourceReference(Hyperlink.ForegroundProperty,"Accent");link.RequestNavigate+=(s,e)=>{try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){}e.Handled=true;};return link;
  }
  static TextBlock Link(string label,string url){var text=Text("","Muted","UiFontCaption");text.Inlines.Add(Linked(label,url));return text;}
  void PositionReset(){SkyGlobeView.PlaceReset(reset,globe,mapHost);}
  void ArrangeBody(){
   if(photo!=null){var bitmap=photo.Source as BitmapSource;double ratio=bitmap.PixelWidth/(double)bitmap.PixelHeight,width=Math.Max(1,ActualWidth-scroll.Padding.Left-scroll.Padding.Right-SystemParameters.VerticalScrollBarWidth-8),cap=Math.Min(340,Math.Max(160,scroll.ActualHeight*0.48));double height=Math.Min(cap,width/ratio);photo.Height=height;photo.Width=height*ratio;}
   if(body==null)return;bool wide=ActualWidth>=540&&mapPanel!=null;body.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);body.ColumnDefinitions[1].Width=wide?new GridLength(230):new GridLength(0);
   if(mapPanel!=null){Grid.SetColumn(mapPanel,wide?1:0);Grid.SetRow(mapPanel,wide?0:1);mapPanel.Margin=new Thickness(wide?16:0,4,0,10);if(mapHost!=null)mapHost.Height=wide?230:Math.Min(280,Math.Max(160,ActualWidth-40));}
  }
 }
}
