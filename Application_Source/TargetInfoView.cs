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
 // A single scrolling surface: no clipped facts, miniature text or nested scrollbars.
 public sealed class TargetInfoView:UserControl {
  readonly StackPanel content=new StackPanel();
  readonly ScrollViewer scroll;
  Grid body;StackPanel article,mapPanel;SkyGlobeView globe;
  public Action<string> ChooseObject;
  public string CurrentId{get;private set;}
  public TargetInfoView(){
   scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(4,2,4,8)};
   Content=scroll;AutomationProperties.SetName(this,"Target information");SizeChanged+=(s,e)=>ArrangeBody();
  }
  static TextBlock Text(string value,string colour="Text",string size="UiFontControl"){
   var text=new TextBlock{Text=value??"",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};text.SetResourceReference(TextBlock.ForegroundProperty,colour);text.SetResourceReference(TextBlock.FontSizeProperty,size);return text;
  }
  // Catalogue names, prose and attribution are data, so the interface translation
  // hook must not interpret them as labels or number templates.
  static TextBlock Data(string value,string colour="Text",string size="UiFontControl"){
   var text=Text("",colour,size);text.SetBinding(TextBlock.TextProperty,new Binding{Source=value??""});return text;
  }
  void Begin(string title){
   CurrentId=null;body=null;mapPanel=null;globe=null;content.Children.Clear();scroll.ScrollToTop();
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
   var constellation=SkyFigures.Figures.FirstOrDefault(f=>f.Id==card.constellation);
   content.Children.Add(Data(card.id+" · "+card.type_label+(string.IsNullOrEmpty(card.constellation)?"":" · "+(constellation==null?card.constellation:constellation.Name)),"Muted"));
   if(card.identity_status=="disputed"||card.domain=="historical_designation")content.Children.Add(Text("Historical designation · identity is disputed.","Muted"));
   if(card.Thumbnail!=null){try{
    var bitmap=new BitmapImage();using(var stream=new MemoryStream(card.Thumbnail)){bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=640;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();}
    var image=new Image{Source=bitmap,Stretch=Stretch.Uniform,MaxHeight=180,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,2,0,8)};AutomationProperties.SetName(image,card.name+". "+card.image.representation);content.Children.Add(image);
    content.Children.Add(Data(card.image.representation,"Muted","UiFontCaption"));
   }catch(NotSupportedException){}catch(IOException){}catch(ArgumentException){}}
   body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition());body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});content.Children.Add(body);
   article=new StackPanel();body.Children.Add(article);
   article.Children.Add(string.IsNullOrWhiteSpace(card.summary)?Text("A checked overview is not available for this target yet."):Data(card.summary));
   if(!string.IsNullOrWhiteSpace(card.fact)){var fact=new Border{Padding=new Thickness(10),CornerRadius=new CornerRadius(6),Margin=new Thickness(0,2,0,10),Child=Data(card.fact)};fact.SetResourceReference(Border.BackgroundProperty,"SurfaceAlt");article.Children.Add(fact);}
   var facts=new Grid{Margin=new Thickness(0,4,0,10)};facts.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto,MaxWidth=115});facts.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});article.Children.Add(facts);
   Action<string,string,string> add=(label,value,evidence)=>{
    int row=facts.RowDefinitions.Count;facts.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var key=Text(label,"Muted");key.Margin=new Thickness(0,0,12,7);Grid.SetRow(key,row);facts.Children.Add(key);var val=Data(value);val.ToolTip=evidence;Grid.SetRow(val,row);Grid.SetColumn(val,1);facts.Children.Add(val);
   };
   if(!string.IsNullOrEmpty(card.parent_body))add("Parent",card.parent_body,null);
   var measurements=card.measurements??new TargetInfoMeasurement[0];
   var ra=measurements.FirstOrDefault(m=>m.field=="RA");var dec=measurements.FirstOrDefault(m=>m.field=="Dec");
   if(ra!=null)add("RA (J2000)",Hours(ra.value),Evidence(card,ra));
   if(dec!=null)add("Dec (J2000)",dec.value.ToString("+0.###;-0.###;0",CultureInfo.InvariantCulture)+"°",Evidence(card,dec));
   var major=measurements.FirstOrDefault(m=>m.field=="MajAx");var minor=measurements.FirstOrDefault(m=>m.field=="MinAx");
   if(major!=null)add("Apparent size",major.value.ToString("0.##",CultureInfo.InvariantCulture)+(minor==null?"":" × "+minor.value.ToString("0.##",CultureInfo.InvariantCulture))+" arcmin",Evidence(card,major));
   var magnitude=measurements.FirstOrDefault(m=>m.field=="V-Mag")??measurements.FirstOrDefault(m=>m.field=="B-Mag");
   if(magnitude!=null)add(Localization.F("Magnitude ({0})",magnitude.field.Substring(0,1)),magnitude.value.ToString("0.##",CultureInfo.InvariantCulture),Evidence(card,magnitude));
   if(card.domain=="deep_sky"){
    mapPanel=new StackPanel{Margin=new Thickness(0,4,0,10)};body.Children.Add(mapPanel);mapPanel.Children.Add(Text("LOCATION IN THE SKY","Muted","UiFontCaption"));
    if(ra!=null&&dec!=null){
     var sky=CaptureSky.Resolve(new Frame{Target=card.id,RA=ra.value,Dec=dec.value},null);sky.TargetLabel=card.name;sky.TimeLabel="J2000 catalogue position";sky.Summary="Celestial sky";sky.Key="target-info:"+card.id;sky.Evidence="J2000 catalogue coordinates; no observing time or local horizon is implied.";
     globe=new SkyGlobeView{Height=230,MinHeight=160};globe.SetContext(sky);globe.Camera.SetHome(90-ra.value,dec.value);globe.ResetView();AutomationProperties.SetName(globe,"Sky location of "+card.name);mapPanel.Children.Add(globe);
     var reset=new Button{Content="Reset sky view",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(8,3,8,3),Margin=new Thickness(0,0,0,6)};reset.Click+=(s,e)=>globe.ResetView();mapPanel.Children.Add(reset);
     mapPanel.Children.Add(Text("Catalogue position · drag to rotate. Scroll or pinch over the map to zoom.","Muted","UiFontCaption"));
    }else mapPanel.Children.Add(Text("Catalogue coordinates are not available for this target.","Muted"));
   }
   var sources=new StackPanel();
   sources.Children.Add(Text(card.review_status=="draft"?"Catalogue summary only. The extended description is awaiting review.":"Description checked against its cited sources.","Muted"));
   foreach(var m in measurements){sources.Children.Add(Data(Localization.T(MeasurementLabel(m.field))+": "+m.value.ToString("0.#####",CultureInfo.InvariantCulture)+" "+m.unit+(string.IsNullOrEmpty(m.epoch)?"":" · "+m.epoch)+(string.IsNullOrEmpty(m.band)?"":" · "+m.band)+"\n"+m.qualifier,"Muted"));var citation=(card.citations??new TargetInfoCitation[0]).FirstOrDefault(c=>c.id==m.citation_id);if(citation!=null)sources.Children.Add(Link(citation.source_title,citation.source_url));}
   foreach(var citation in card.citations??new TargetInfoCitation[0]){sources.Children.Add(Link(citation.source_title,citation.source_url));sources.Children.Add(Text(CitationFields(citation.applies_to),"Muted","UiFontCaption"));}
   foreach(string url in card.source_urls??new string[0])sources.Children.Add(Link(Localization.T("Object source"),url));
   var aliases=card.aliases??new string[0];if(aliases.Length>0)sources.Children.Add(Data(Localization.F("Catalogue names: {0}",string.Join(" · ",aliases)),"Muted"));
   content.Children.Add(Disclosure("Sources and catalogue details",sources));
   if(card.image!=null){var credits=new StackPanel();credits.Children.Add(Data(card.image.attribution));credits.Children.Add(Data(card.image.representation,"Muted"));credits.Children.Add(Data(card.image.modifications,"Muted"));credits.Children.Add(Link(card.image.licence,card.image.licence_url));credits.Children.Add(Link(Localization.T("Original image and full credit"),card.image.file_page_url));content.Children.Add(Disclosure(Localization.F("Image credit · {0}",card.image.licence),credits));}
   content.Children.Add(Text("Select a file to return to its image preview.","Muted","UiFontCaption"));ArrangeBody();
  }
  static string MeasurementLabel(string field){
   switch(field){case "RA":return "Right ascension";case "Dec":return "Declination";case "MajAx":return "Major axis";case "MinAx":return "Minor axis";case "PosAng":return "Position angle";case "SurfBr":return "Surface brightness";case "RadVel":return "Radial velocity";case "Redshift":return "Redshift";default:return field.EndsWith("-Mag")?"Magnitude ("+field.Substring(0,1)+" band)":field;}
  }
  static string CitationFields(string value){
   string[] fields;try{fields=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string[]>(value);}catch{fields=new[]{value??""};}
   return Localization.F("Source for: {0}",string.Join(", ",fields.Select(f=>f.StartsWith("measurement:")?MeasurementLabel(f.Substring(f.LastIndexOf(':')+1)):f=="catalogue_summary"?"catalogue summary":f=="overview"?"overview":f=="fact"?"additional fact":"object information").Select(Localization.T).Distinct()));
  }
  static string Hours(double degrees){int seconds=(int)Math.Round(degrees/15*3600)%86400;return (seconds/3600).ToString("00")+"h "+(seconds%3600/60).ToString("00")+"m "+(seconds%60).ToString("00")+"s";}
  static string Evidence(TargetInfoCard card,TargetInfoMeasurement m){var citation=(card.citations??new TargetInfoCitation[0]).FirstOrDefault(c=>c.id==m.citation_id);return m.qualifier+(citation==null?"":"\n"+citation.source_title+"\n"+citation.source_url);}
  static Expander Disclosure(string title,StackPanel panel){return new Expander{Header=Text(title,"Muted"),Content=panel,Margin=new Thickness(0,4,0,8),HorizontalContentAlignment=HorizontalAlignment.Stretch};}
  static TextBlock Link(string label,string url){
   var text=Text("");Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http")){text.SetBinding(TextBlock.TextProperty,new Binding{Source=label});return text;}
   var link=new Hyperlink(new Run(label)){NavigateUri=uri,ToolTip=url};link.SetResourceReference(Hyperlink.ForegroundProperty,"Accent");link.RequestNavigate+=(s,e)=>{try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){}e.Handled=true;};text.Inlines.Add(link);return text;
  }
  void ArrangeBody(){
   if(body==null)return;bool wide=ActualWidth>=540&&mapPanel!=null;
   body.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);body.ColumnDefinitions[1].Width=wide?new GridLength(230):new GridLength(0);
   if(mapPanel!=null){Grid.SetColumn(mapPanel,wide?1:0);Grid.SetRow(mapPanel,wide?0:1);mapPanel.Margin=new Thickness(wide?16:0,4,0,10);if(globe!=null)globe.Height=wide?230:Math.Min(280,Math.Max(160,ActualWidth-40));}
  }
 }
}
