using System;
using System.Collections.Generic;
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
 // A target listed inside a section (a comet under Comets), with its file count.
 public sealed class TargetInfoMember {public string Target,Label,Detail;}
 // One scrolling surface keeps the image, facts, map and references reachable.
 public sealed class TargetInfoView:UserControl {
  static readonly HashSet<string> anchorNames=new HashSet<string>(new[]{"ObjectTitle","ObjectDetails","OverviewHeading","OverviewText","ObjectImage","FunFact","ParametersHeading","ImagingNotes","ImagingNotesText","Sources","ImageCredits","OrbitPanel"});
  readonly StackPanel content=new StackPanel();readonly ScrollViewer scroll;
  StackPanel headerIcons;Grid headerRow_,creditRow_;
  TextBlock informationStatus;Button refreshButton;OrbitPanel orbitPanel;Grid body,mapHost;StackPanel article,mapPanel;SkyGlobeView globe;Image photo;Button reset;
  public Action<string> ChooseObject;public Action RefreshInformation;public bool InternetAvailable;
  // A section such as Comets lists the user's targets inside it; choosing one reads that target's page.
  public Action<string> ChooseMember;
  public Action GoBack;
  // Shown only when a card has something Stellarium can point at.
  public Action<StellariumView> OpenInStellarium;
  // The observing location the Meteors page uses to say which showers can be seen; null when none is set.
  public Func<CaptureSite> ObserverSite;
  // The dates the card's target was imaged in the repository, for the orbit's imaged markers.
  public Func<TargetInfoCard,List<DateTime>> CaptureDates;
  public const string MeteorCardId="METEORS";
  public string CurrentId{get;private set;}
  public TargetInfoView(){
   // Always reserve the scrollbar gutter: with Auto, text rewrapping at the narrower width toggled the bar on and off.
   scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(4,2,4,8)};
   Content=scroll;AutomationProperties.SetName(this,"Target information");SizeChanged+=(s,e)=>ArrangeBody();scroll.SizeChanged+=(s,e)=>ArrangeBody();scroll.ScrollChanged+=(s,e)=>{if(Math.Abs(e.ViewportHeightChange)>0.1)ArrangeBody();};
  }
  static TextBlock Text(string value,string colour="Text",string size="UiFontControl"){
   var text=new TextBlock{Text=value??"",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};text.SetResourceReference(TextBlock.ForegroundProperty,colour);text.SetResourceReference(TextBlock.FontSizeProperty,size);return text;
  }
  // Scientific names, prose and attribution must not be treated as UI labels.
  static TextBlock Data(string value,string colour="Text",string size="UiFontControl"){
   var text=Text("",colour,size);text.SetBinding(TextBlock.TextProperty,new Binding{Source=value??""});return text;
  }
  // Disclosures sit directly in the content, or beside the header icons in a row.
  IEnumerable<Expander> Expanders(){return content.Children.OfType<Expander>().Concat(content.Children.OfType<Grid>().SelectMany(g=>g.Children.OfType<Expander>()));}
  void Begin(string title){
   if(orbitPanel!=null)orbitPanel.Cancel();orbitPanel=null;informationStatus=null;refreshButton=null;headerIcons=null;headerRow_=null;creditRow_=null;CurrentId=null;body=null;mapPanel=null;mapHost=null;globe=null;photo=null;reset=null;content.Children.Clear();scroll.ScrollToTop();
   var label=Text("TARGET INFO","Muted","UiFontCaption");label.FontWeight=FontWeights.SemiBold;content.Children.Add(label);
   var heading=Data(title,"Text","UiFontHeading");heading.Name="ObjectTitle";heading.FontWeight=FontWeights.SemiBold;content.Children.Add(heading);
  }
  public void ShowMessage(string title,string message){Begin(Localization.T(title));content.Children.Add(Text(message,"Muted"));}
  public void ShowChoices(string target,TargetInfoResult result){
   Begin(target);content.Children.Add(Text(result.Message,"Muted"));
   foreach(var card in result.Cards){string id=card.id;var button=new Button{Content=Data(Catalog.DisplayIds(card.name)+" · "+Catalog.DisplayId(card.id)+"\n"+card.type_label),HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,0,6),Padding=new Thickness(10,8,10,4)};AutomationProperties.SetName(button,"Read about "+card.name+" "+id);button.Click+=(s,e)=>{if(ChooseObject!=null)ChooseObject(id);};content.Children.Add(button);}
  }
  public void ShowCard(TargetInfoCard card,string context=null){ShowCard(card,context,null,null,null);}
  // membersTitle heads the list of the user's targets inside a section; back returns from a member to its section.
  public void ShowCard(TargetInfoCard card,string context,IList<TargetInfoMember> members,string membersTitle,string back){
   Begin(Catalog.DisplayIds(card.name));CurrentId=card.id;
   string constellation=TargetInfoPresentation.Constellation(card.constellation);
   // The ID is shown once: in the heading when the object has no other name, otherwise here.
   var details=Data(TargetInfoPresentation.Tagline(card,constellation),"Muted","UiFontCaption");details.Name="ObjectDetails";
   // Refresh and Open in Stellarium are small icons at the right of the details line rather than a line of their own.
   var headerRow=new Grid();headerRow.ColumnDefinitions.Add(new ColumnDefinition());headerRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});headerRow.Children.Add(details);
   var icons=headerIcons=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(6,0,0,0)};headerRow_=headerRow;Grid.SetColumn(icons,1);headerRow.Children.Add(icons);content.Children.Add(headerRow);
   string alternatives=TargetInfoPresentation.AlternativeLine(card);if(alternatives.Length>0){var other=Data(alternatives,"Muted","UiFontCaption");other.Name="ObjectAlternativeIds";content.Children.Add(other);}
   if(!string.IsNullOrEmpty(context))content.Children.Add(Data(context,"Muted","UiFontCaption"));
   var actions=new WrapPanel{Margin=new Thickness(0,0,0,4)};
   if(!string.IsNullOrEmpty(card.downloaded_at))content.Children.Add(Data(Localization.F("Saved information · retrieved {0}",card.downloaded_at),"Muted","UiFontCaption"));
   if(!string.IsNullOrEmpty(back)&&GoBack!=null){var up=ActionButton(back,true);up.Name="BackToSection";up.Click+=(s,e)=>GoBack();actions.Children.Add(up);}
   var stellarium=OpenInStellarium==null?null:Stellarium.ForCard(card);
   if(stellarium!=null){var open=IconButton(Stellarium.IconGeometry,"Open in Stellarium");open.Name="OpenInStellarium";UiHelp.Hint(open,Localization.T(Stellarium.CardTooltip(stellarium)));open.Click+=(s,e)=>OpenInStellarium(stellarium);icons.Children.Add(open);}
   if(RefreshInformation!=null&&InformationCoordinator.Provider(card)!=null){var refresh=refreshButton=IconButton(DownloadGeometry,"Refresh information");refresh.IsEnabled=InternetAvailable;UiHelp.Hint(refresh,InternetAvailable?"Download missing or updated information for this target.":"Choose Hybrid in Preferences > General > Online access to reconnect.");refresh.Click+=(s,e)=>RefreshInformation();icons.Children.Add(refresh);}
   if(actions.Children.Count>0)content.Children.Add(actions);
   informationStatus=Text("","Muted","UiFontCaption");informationStatus.Visibility=Visibility.Collapsed;AutomationProperties.SetLiveSetting(informationStatus,AutomationLiveSetting.Polite);content.Children.Add(informationStatus);
   var curated=TargetDescriptions.For(card.id);
   if(card.identity_status=="disputed"||card.domain=="historical_designation")content.Children.Add(Text("Historical designation · identity is disputed.","Muted"));
   string summary=TargetInfoPresentation.DisplaySummary(card);string factText=curated!=null&&!string.IsNullOrWhiteSpace(curated.Fact)?curated.Fact:card.fact;
   var overview=Section("OVERVIEW",true);overview.Name="OverviewHeading";content.Children.Add(overview);
   var prose=string.IsNullOrWhiteSpace(summary)?Text("No information is available for this target.","Muted"):Data(summary);prose.Name="OverviewText";content.Children.Add(prose);
   if(card.Thumbnail!=null&&card.image!=null){try{
    var bitmap=card.DecodedThumbnail as BitmapSource;if(bitmap==null){var decoded=new BitmapImage();using(var stream=new MemoryStream(card.Thumbnail)){decoded.BeginInit();decoded.CacheOption=BitmapCacheOption.OnLoad;decoded.DecodePixelWidth=640;decoded.StreamSource=stream;decoded.EndInit();decoded.Freeze();}bitmap=decoded;}
    photo=new Image{Name="ObjectImage",Source=bitmap,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,2,0,6)};RenderOptions.SetBitmapScalingMode(photo,BitmapScalingMode.HighQuality);AutomationProperties.SetName(photo,card.name);content.Children.Add(photo);
    AddImageCredit(card.image);
   }catch(NotSupportedException){}catch(IOException){}catch(ArgumentException){}}
   if(members!=null&&members.Count>0){
    var list=Section(membersTitle??"YOUR TARGETS");list.Name="SectionMembers";content.Children.Add(list);
    foreach(var member in members){string name=member.Target;var button=new Button{Content=Data(member.Label+"\n"+member.Detail),HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,0,6),Padding=new Thickness(10,8,10,4)};AutomationProperties.SetName(button,"Read about "+member.Label);button.Click+=(s,e)=>{if(ChooseMember!=null)ChooseMember(name);};content.Children.Add(button);}
   }
   if(card.id==MeteorCardId)AddMeteors();
   if(!string.IsNullOrWhiteSpace(factText)){
    var fact=new StackPanel();var caption=Text("FUN FACT","Muted","UiFontCaption");caption.FontWeight=FontWeights.SemiBold;fact.Children.Add(caption);fact.Children.Add(Data(factText));
    var box=Callout(fact);box.Name="FunFact";content.Children.Add(box);
   }
   body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition());body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});content.Children.Add(body);
   article=new StackPanel();body.Children.Add(article);
   var parameters=Section("PARAMETERS");parameters.Name="ParametersHeading";article.Children.Add(parameters);var facts=Facts();article.Children.Add(facts);
   if(!string.IsNullOrEmpty(card.parent_body))AddFact(facts,"Parent",card.parent_body,null);
   var measurements=card.measurements??new TargetInfoMeasurement[0];
   var ra=measurements.FirstOrDefault(m=>m.field=="RA");var dec=measurements.FirstOrDefault(m=>m.field=="Dec");
   if(ra!=null)AddFact(facts,"RA (J2000)",Hours(ra.value),Evidence(card,ra));
   if(dec!=null)AddFact(facts,"Dec (J2000)",dec.value.ToString("+0.###;-0.###;0",CultureInfo.InvariantCulture)+"°",Evidence(card,dec));
   var major=measurements.FirstOrDefault(m=>m.field=="MajAx");var minor=measurements.FirstOrDefault(m=>m.field=="MinAx");
   if(major!=null)AddFact(facts,"Apparent size",major.value.ToString("0.##",CultureInfo.InvariantCulture)+(minor==null?"":" × "+minor.value.ToString("0.##",CultureInfo.InvariantCulture))+" arcmin",Evidence(card,major));
   var magnitude=measurements.FirstOrDefault(m=>m.field=="V-Mag")??measurements.FirstOrDefault(m=>m.field=="B-Mag");
   if(magnitude!=null)AddFact(facts,Localization.F("Magnitude ({0})",magnitude.field.Substring(0,1)),magnitude.value.ToString("0.##",CultureInfo.InvariantCulture),Evidence(card,magnitude));
   var distance=measurements.FirstOrDefault(m=>m.field=="Distance");
   if(distance!=null)AddFact(facts,"Distance",TargetInfoPresentation.Distance(distance),Evidence(card,distance));
   AddParameters(facts,card);
   foreach(var m in measurements.Where(m=>new[]{"Diameter","Rotation period","Albedo"}.Contains(m.field))){if(m.field=="Diameter"&&TargetParameters.For(card.id)!=null)continue;AddFact(facts,m.field,m.value.ToString("0.#####",CultureInfo.InvariantCulture)+(string.IsNullOrEmpty(m.uncertainty)?"":" ± "+m.uncertainty)+(m.unit=="dimensionless"?"":" "+m.unit),Evidence(card,m));}
   // Meteors have no catalogue parameters; their page shows the shower calendar instead.
   if(card.id==MeteorCardId&&facts.Children.Count==0){article.Children.Remove(parameters);article.Children.Remove(facts);}
   string imaging=TargetInfoPresentation.Imaging(card);
   // Imaging notes are always shown in full, not behind a disclosure.
   if(!string.IsNullOrWhiteSpace(imaging)){var notes=Section("IMAGING NOTES");notes.Name="ImagingNotes";article.Children.Add(notes);var text=Data(imaging);text.Name="ImagingNotesText";article.Children.Add(text);}
   if(card.domain=="deep_sky"){
    mapPanel=new StackPanel{Margin=new Thickness(0,4,0,10)};body.Children.Add(mapPanel);mapPanel.Children.Add(Section("LOCATION IN THE SKY"));
    if(ra!=null&&dec!=null){
     var sky=CaptureSky.Resolve(new Frame{Target=card.id,RA=ra.value,Dec=dec.value},null);sky.TargetLabel=Catalog.DisplayIds(card.name);sky.TimeLabel="J2000 catalogue position";sky.Summary="Celestial sky";sky.Key="target-info:"+card.id;sky.Evidence="J2000 catalogue coordinates; no observing time or local horizon is implied.";
     mapHost=new Grid{Height=230,MinHeight=160};mapPanel.Children.Add(mapHost);
     globe=new SkyGlobeView{Margin=new Thickness(4)};globe.SetContext(sky);globe.Camera.SetHome(90-ra.value,dec.value);globe.ResetView();AutomationProperties.SetName(globe,"Sky location of "+card.name);mapHost.Children.Add(globe);
     reset=new Button{Width=26,Height=26,MinHeight=0,Padding=new Thickness(0),Margin=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
     var icon=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 5,7 A 7,7 0 1 1 5,15 M 5,3 L 5,7 L 9,7"),Width=18,Height=18,Stretch=Stretch.Uniform,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new Binding("Foreground"){Source=reset});reset.Content=icon;AutomationProperties.SetName(reset,"Reset sky view");UiHelp.Hint(reset,"Reset sky view.");reset.Click+=(s,e)=>globe.ResetView();mapHost.Children.Add(reset);globe.GlobeResized+=(s,e)=>PositionReset();mapHost.SizeChanged+=(s,e)=>PositionReset();
     mapPanel.Children.Add(Text("Catalogue position · drag to rotate. Click the map, then scroll to zoom (or hold Ctrl and scroll).","Muted","UiFontCaption"));
    }else mapPanel.Children.Add(Text("Catalogue coordinates are not available for this target.","Muted"));
   }
   if(card.domain=="solar_system"){
    mapPanel=new StackPanel{Margin=new Thickness(0,4,0,10)};body.Children.Add(mapPanel);var orbitHeading=Section("ORBIT");mapPanel.Children.Add(orbitHeading);
    orbitPanel=new OrbitPanel{Name="OrbitPanel"};OrbitHeadingDate(orbitHeading,orbitPanel);mapPanel.Children.Add(orbitPanel);if(CaptureDates!=null)orbitPanel.CaptureDates=CaptureDates(card);orbitPanel.SetCard(card);
   }
   var sources=new StackPanel();
   var complete=OrbitalCatalog.For(card);if(complete!=null){sources.Children.Add(Link("NASA/JPL · "+complete.solution_id,complete.source));sources.Children.Add(Data(Localization.F("Source epoch: JD {0} {1} · {2}",complete.epoch.ToString("0.#####",CultureInfo.InvariantCulture),complete.time_standard,complete.solution_id),"Muted","UiFontCaption"));if(complete.uncertainties!=null&&complete.uncertainties.Count>0)sources.Children.Add(Data(string.Join(" · ",complete.uncertainties.Select(p=>p.Key+" σ "+p.Value)),"Muted","UiFontCaption"));}
   foreach(var rights in new[]{card.identity_rights,card.text_rights,card.measurement_rights,card.image==null?null:card.image.rights,complete==null?null:complete.rights})if(rights!=null){sources.Children.Add(Link(rights.full_attribution,rights.source_url));sources.Children.Add(Link(rights.licence_id+" "+rights.licence_version,rights.licence_url));if(!string.IsNullOrWhiteSpace(rights.modifications))sources.Children.Add(Data(rights.modifications,"Muted","UiFontCaption"));}
   var hidden=curated==null?new string[0]:curated.HiddenAliases;
   var aliases=(card.aliases??new string[0]).Where(a=>TargetInfoCatalog.Normalize(a)!=TargetInfoCatalog.Normalize(card.id)&&!string.Equals(a,card.name,StringComparison.OrdinalIgnoreCase)&&!hidden.Contains(a,StringComparer.OrdinalIgnoreCase)).Select(x=>Catalog.DisplayId(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   if(aliases.Length>0)sources.Children.Add(Data(Localization.F("Catalogue names: {0}",string.Join(" · ",aliases)),"Muted","UiFontCaption"));
   var moreFacts=Facts();
   foreach(var measurement in measurements.Where(m=>new[]{"PosAng","SurfBr","RadVel","Redshift"}.Contains(m.field)))AddFact(moreFacts,MeasurementLabel(measurement.field),measurement.value.ToString("0.#####",CultureInfo.InvariantCulture)+(measurement.unit=="dimensionless"?"":" "+measurement.unit),Evidence(card,measurement));
   if(moreFacts.Children.Count>0)sources.Children.Add(moreFacts);
   foreach(var source in TargetInfoPresentation.Sources(card,curated!=null&&!string.IsNullOrWhiteSpace(curated.Overview)))sources.Children.Add(Link(source.Title,source.Url));
   if(sources.Children.Count>0){var disclosure=Disclosure("SOURCES",sources);disclosure.Name="Sources";var heading=(TextBlock)disclosure.Header;heading.FontWeight=FontWeights.SemiBold;heading.SetResourceReference(TextBlock.FontSizeProperty,"UiFontCaption");heading.Margin=new Thickness(0);disclosure.Margin=new Thickness(0,12,0,6);content.Children.Add(disclosure);}
   content.Children.Add(Text("Select a file to return to its image preview.","Muted","UiFontCaption"));ArrangeBody();
  }
  // The Meteors page: when each annual shower is active and how strong it is from the observer's latitude, then what a meteor's colour means.
  void AddMeteors(){
   var site=ObserverSite==null?null:ObserverSite();var today=DateTime.Now;
   var heading=Section("METEOR SHOWERS THROUGH THE YEAR");heading.Name="MeteorShowersHeading";content.Children.Add(heading);
   var calendar=new MeteorCalendar(site==null?(double?)null:site.Latitude,site==null?null:site.Label,today){Name="MeteorCalendar",Margin=new Thickness(0,2,0,4)};content.Children.Add(calendar);
   content.Children.Add(site==null?Text("Showers are shown as seen from anywhere on Earth, at their zenithal hourly rate. Choose an observing town in Settings to see the rates from your location.","Muted","UiFontCaption"):Data(Localization.F("As seen from {0} (latitude {1}°). Rows end in the hourly rate you could expect at the radiant's highest; a dashed line means the radiant stays too low to see.",site.Label,site.Latitude.ToString("0.#",CultureInfo.InvariantCulture)),"Muted","UiFontCaption"));
   content.Children.Add(Text("Each bar spans a shower's usual dates and rises to its peak; the taller the peak, the stronger the shower. The vertical line is today, a dot marks the peak, and a ring means the Moon is more than 60% lit then. Hover a row for details.","Muted","UiFontCaption"));
   var colours=Section("WHAT COLOURS CAN MEAN");colours.Name="MeteorColoursHeading";content.Children.Add(colours);
   var list=new Grid{Margin=new Thickness(0,2,0,6)};list.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});list.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto,MaxWidth=110});list.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
   foreach(var colour in MeteorColours.All){
    int row=list.RowDefinitions.Count;list.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
    var swatch=new System.Windows.Shapes.Ellipse{Width=12,Height=12,Fill=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour.Hex)),Margin=new Thickness(0,3,8,0),VerticalAlignment=VerticalAlignment.Top};Grid.SetRow(swatch,row);list.Children.Add(swatch);
    var name=Text(colour.Name,"Text");name.Margin=new Thickness(0,0,10,7);name.FontWeight=FontWeights.SemiBold;Grid.SetRow(name,row);Grid.SetColumn(name,1);list.Children.Add(name);
    var cause=Data(colour.Cause,"Muted");cause.Margin=new Thickness(0,0,0,7);Grid.SetRow(cause,row);Grid.SetColumn(cause,2);list.Children.Add(cause);
   }
   content.Children.Add(list);content.Children.Add(Data(MeteorColours.Speed,"Muted","UiFontCaption"));
  }
  public void InformationStatus(string message,bool busy){if(informationStatus!=null){double offset=scroll.VerticalOffset,before=informationStatus.Visibility==Visibility.Visible?informationStatus.DesiredSize.Height:0;informationStatus.Text=Localization.T(message??"");informationStatus.Visibility=string.IsNullOrEmpty(message)?Visibility.Collapsed:Visibility.Visible;UpdateLayout();double after=informationStatus.Visibility==Visibility.Visible?informationStatus.DesiredSize.Height:0;if(offset>0)scroll.ScrollToVerticalOffset(offset+after-before);}if(refreshButton!=null)refreshButton.IsEnabled=InternetAvailable&&!busy;}
  static IEnumerable<FrameworkElement> NamedVisuals(DependencyObject parent){for(int k=0;k<VisualTreeHelper.GetChildrenCount(parent);k++){var child=VisualTreeHelper.GetChild(parent,k);var element=child as FrameworkElement;if(element!=null&&anchorNames.Contains(element.Name)&&element.IsVisible)yield return element;foreach(var nested in NamedVisuals(child))yield return nested;}}
  public void UpdateCard(TargetInfoCard card,string context,IList<TargetInfoMember> members,string membersTitle,string back){
   bool orbitFocused=orbitPanel!=null&&orbitPanel.View.IsKeyboardFocused;DateTime? marker=orbitPanel==null?null:orbitPanel.MarkerUtc;var anchors=NamedVisuals(content).Select(e=>new{e.Name,Y=e.TranslatePoint(new Point(),scroll).Y}).Where(e=>e.Y<=8).OrderByDescending(e=>e.Y).ToArray();double offset=scroll.VerticalOffset;var camera=orbitPanel==null?null:orbitPanel.View.Camera.Copy();double height=orbitPanel==null?280:orbitPanel.View.Height;
   var expanded=Expanders().Where(e=>e.IsExpanded).Select(e=>e.Name).ToArray();bool same=CurrentId==card.id;
   ShowCard(card,context,members,membersTitle,back);if(!same)return;
   foreach(var e in Expanders())if(expanded.Contains(e.Name))e.IsExpanded=true;
   if(camera!=null&&orbitPanel!=null){orbitPanel.MarkerUtc=marker;orbitPanel.View.Camera=camera;orbitPanel.View.Height=height;}UpdateLayout();var names=NamedVisuals(content).ToDictionary(e=>e.Name,e=>e);foreach(var anchor in anchors){FrameworkElement element;if(names.TryGetValue(anchor.Name,out element)){offset=Math.Max(0,element.TranslatePoint(new Point(),scroll).Y-anchor.Y);break;}}scroll.ScrollToVerticalOffset(offset);if(orbitFocused&&orbitPanel!=null)orbitPanel.View.Focus();
  }
  // Planets, the largest dwarf planets and the Sun carry fixed physical parameters; the Sun shows only size and mass.
  // Comets show their orbit instead.
  static void AddParameters(Grid facts,TargetInfoCard card){
   var complete=CometInfo.IsComet(card)?OrbitalCatalog.For(card):null;
   var orbit=complete!=null?CometInfo.FromComplete(complete):CometInfo.IsComet(card)?CometInfo.Orbit(card.id):null;
   if(orbit!=null){
    AddFact(facts,"Orbit type",Localization.T(CometInfo.ClassLabel(orbit)),CometInfo.SourceTitle);
    AddFact(facts,"Perihelion",CometInfo.Perihelion(orbit),"Closest distance to the Sun. "+CometInfo.SourceTitle);
    if(CometInfo.ShowsAphelion(orbit))AddFact(facts,"Aphelion",CometInfo.Aphelion(orbit),"Farthest distance from the Sun. "+CometInfo.SourceTitle);
    if(complete!=null&&complete.perihelion_time.HasValue)AddFact(facts,"Perihelion (TDB)",(CometInfo.ShowsPerihelionDate(orbit)?CometInfo.PerihelionDate(orbit)+" · ":"")+"JD "+complete.perihelion_time.Value.ToString("0.#####",CultureInfo.InvariantCulture),CometInfo.SourceTitle);else if(CometInfo.ShowsPerihelionDate(orbit))AddFact(facts,"Perihelion date",CometInfo.PerihelionDate(orbit),CometInfo.SourceTitle);
    if(orbit.PeriodYears>0)AddFact(facts,"Orbital period",CometInfo.Period(orbit),CometInfo.SourceTitle);
    AddFact(facts,"Eccentricity",CometInfo.Eccentricity(orbit),"0 is a circle; 1 or more is an open orbit. "+CometInfo.SourceTitle);
    if(CometInfo.HasSemiMajorAxis(orbit))AddFact(facts,"Semi-major axis",CometInfo.SemiMajorAxis(orbit),"Average distance from the Sun. "+CometInfo.SourceTitle);
    AddFact(facts,"Perihelion speed",CometInfo.PerihelionSpeed(orbit),"Speed at closest approach to the Sun, from the orbit. "+CometInfo.SourceTitle);
    if(CometInfo.ShowsEscapeSpeed(orbit))AddFact(facts,"Speed at infinity",CometInfo.EscapeSpeed(orbit),"Speed left once far from the Sun on an open orbit. "+CometInfo.SourceTitle);
    AddCometSnapshot(facts,card);
    return;
   }
   var set=card.domain=="solar_system"?TargetParameters.For(card.id):null;if(set==null)return;
   if(set.HasOrbit){var small=OrbitalCatalog.For(card);string value=small!=null&&small.rights.dataset=="orbital-elements"&&small.e<1?Localization.F("{0} AU from the Sun · {1} years",(small.q/(1-small.e)).ToString("0.###",CultureInfo.InvariantCulture),(2*Math.PI*Math.Sqrt(Math.Pow(small.q/(1-small.e),3)/OrbitalModel.SunMu)/365.25).ToString("0.##",CultureInfo.InvariantCulture)):TargetParameters.Orbit(set);AddFact(facts,"Orbit",value,small==null?"NASA Planetary Fact Sheet":small.source);}
   AddFact(facts,"Diameter",TargetParameters.Diameter(set),set.HasOrbit?"Mean or equatorial diameter. NASA Planetary Fact Sheet":"Nominal solar diameter. IAU 2015 Resolution B3");
   AddFact(facts,"Mass",TargetParameters.Mass(set),set.HasOrbit?"NASA Planetary Fact Sheet":"Nominal solar mass. IAU 2015 Resolution B3");
   if(set.PerigeeKm>0)AddFact(facts,"Distance range",TargetParameters.DistanceRange(set),"Closest and farthest distance from Earth. NASA Moon Fact Sheet");
   if(set.Gravity>0)AddFact(facts,"Surface gravity",TargetParameters.SurfaceGravity(set),"In Earth gravities (1 g = 9.80665 m/s²). NASA Planetary Fact Sheet");
   if(TargetParameters.ShowsMoonCount(set))AddFact(facts,"Known moons",set.MoonCount.ToString(CultureInfo.InvariantCulture),"Confirmed moons. NASA Solar System Exploration");
   if(set.Moons.Length>0)AddFact(facts,"Major moons",TargetParameters.Moons(set),null);
  }
  // Facts only the bundled Small-Body Database snapshot holds: Earth MOID, the observations behind the orbit and meteor-shower parentage.
  static void AddCometSnapshot(Grid facts,TargetInfoCard card){
   var comet=CometCatalog.Find(card.id);if(comet==null)return;
   if(comet.EarthMoid.HasValue)AddFact(facts,"Earth MOID",Localization.F("{0} AU",comet.EarthMoid.Value.ToString("0.####",CultureInfo.InvariantCulture)),"Minimum orbit intersection distance: how close the two orbital paths come to each other. It is not the comet's current distance from Earth. "+CometInfo.SourceTitle);
   if(comet.Observations>0){string span=comet.FirstObservation!=null&&comet.LastObservation!=null?comet.FirstObservation.Substring(0,Math.Min(4,comet.FirstObservation.Length))+"–"+comet.LastObservation.Substring(0,Math.Min(4,comet.LastObservation.Length)):null;
    AddFact(facts,"Observations",Localization.F("{0} observations",comet.Observations.ToString("#,##0",CultureInfo.InvariantCulture))+(span==null?"":" · "+span),"Observations used to fit "+(comet.OrbitId??"this orbit")+(comet.ArcDays.HasValue?" over a data arc of "+comet.ArcDays.Value.ToString("#,##0",CultureInfo.InvariantCulture)+" days":"")+". "+CometInfo.SourceTitle);}
   var showers=CometCatalog.ShowersOf(card.id);if(showers.Count==0)return;
   string listed=CometCatalog.ShowerNames(showers,false),suspected=CometCatalog.ShowerNames(showers,true),evidence=CometCatalog.ShowerEvidence(showers);
   if(listed.Length>0)AddFact(facts,"Meteor showers",listed,evidence);
   if(suspected.Length>0)AddFact(facts,"Suspected parent of",suspected,evidence);
  }
  // Short creator/licence credits stay visible. Longer attribution is available
  // inline through a keyboard-accessible disclosure, including processing notes.
  void AddImageCredit(TargetInfoImage image){
   string credit=TargetInfoPresentation.Credit(image);string who=!string.IsNullOrWhiteSpace(image.creator)?image.creator:credit.Length>0&&credit.Length<=60&&!credit.Contains("://")?credit:"Wikimedia Commons";
   string attribution=(image.attribution??"").Trim(),modifications=(image.modifications??"").Trim();
   string full=(attribution+"\n"+modifications).Trim();
   // The short description sits directly under the image; the credits follow it.
   string caption=TargetInfoPresentation.Caption(image.representation);if(caption.Length>0){var described=Data(caption,"Muted","UiFontCaption");described.Name="ImageCaption";described.TextAlignment=TextAlignment.Center;content.Children.Add(described);}
   var line=Text("","Muted","UiFontCaption");line.TextAlignment=TextAlignment.Center;line.ToolTip=Data(full,"Text","UiFontSmall");
   line.Inlines.Add(new Run(Localization.T("Image:")+" "));line.Inlines.Add(Linked(who,image.file_page_url));
   if(!string.IsNullOrWhiteSpace(image.licence)){line.Inlines.Add(new Run(" · "));line.Inlines.Add(Linked(image.licence,image.licence_url));}
   // Every image credit, however short, sits in the Credits dropdown rather than under the picture.
   var credits=new StackPanel();credits.Children.Add(line);
   if(attribution.Length>0&&attribution!=who)credits.Children.Add(Data(attribution,"Muted","UiFontCaption"));
   if(modifications.Length>0)credits.Children.Add(Data(modifications,"Muted","UiFontCaption"));
   var disclosure=Disclosure("Credits",credits);disclosure.Name="ImageCredits";
   // The header icons sit on the Credits row, centred on it and aligned with the image's right edge; with no image they stay by the details.
   if(headerIcons!=null&&headerIcons.Children.Count>0&&headerRow_!=null){
    headerRow_.Children.Remove(headerIcons);headerIcons.Margin=new Thickness(6,0,0,0);headerIcons.VerticalAlignment=VerticalAlignment.Center;
    var creditRow=new Grid();creditRow.ColumnDefinitions.Add(new ColumnDefinition());creditRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    creditRow.Children.Add(disclosure);Grid.SetColumn(headerIcons,1);creditRow.Children.Add(headerIcons);creditRow.HorizontalAlignment=HorizontalAlignment.Center;creditRow_=creditRow;content.Children.Add(creditRow);
   }else content.Children.Add(disclosure);
  }
  // Sections share a quiet caption and divider so the pane reads as distinct parts.
  // Puts the orbit date on the heading's line, right-aligned, in the heading's own style.
  static void OrbitHeadingDate(StackPanel heading,OrbitPanel orbit){
   var caption=heading.Children[heading.Children.Count-1];heading.Children.Remove(caption);
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   row.Children.Add(caption);Grid.SetColumn(orbit.HeaderDate,1);row.Children.Add(orbit.HeaderDate);heading.Children.Add(row);
  }
  static StackPanel Section(string title,bool first=false){
   var panel=new StackPanel{Margin=new Thickness(0,first?6:12,0,6)};
   if(!first){var rule=new Border{Height=1,Margin=new Thickness(0,0,0,8),Opacity=0.7};rule.SetResourceReference(Border.BackgroundProperty,"Border");panel.Children.Add(rule);}
   var caption=Text(title,"Muted","UiFontCaption");caption.FontWeight=FontWeights.SemiBold;caption.Margin=new Thickness(0);panel.Children.Add(caption);return panel;
  }
  // The label stays a plain interface string so the interface language translates it; the arrow is separate.
  static Button ActionButton(string label,bool back){
   var text=Text(label);text.Margin=new Thickness(0);var face=new StackPanel{Orientation=Orientation.Horizontal};
   if(back){var arrow=Text("←");arrow.Margin=new Thickness(0,0,6,0);face.Children.Add(arrow);}face.Children.Add(text);
   return new Button{Content=face,Margin=new Thickness(0,0,6,6),Padding=new Thickness(10,3,10,3),HorizontalAlignment=HorizontalAlignment.Left};
  }
  // Icon-only buttons for the details line, sharing the Stellarium glyph's size and padding.
  const string DownloadGeometry="M 12,3 L 12,15 M 7,10 L 12,15 L 17,10 M 5,19 L 19,19";
  static Button IconButton(string geometry,string name){
   var glyph=new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),StrokeThickness=1.6,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};glyph.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"Text");
   var button=new Button{Content=new Viewbox{Child=glyph},Width=28,Height=28,MinHeight=0,Padding=new Thickness(6),Margin=new Thickness(2,0,0,0)};AutomationProperties.SetName(button,Localization.T(name));return button;
  }
  static Border Callout(UIElement child){var callout=new Border{Padding=new Thickness(10,8,10,2),CornerRadius=new CornerRadius(6),Margin=new Thickness(0,2,0,8),Child=child};callout.SetResourceReference(Border.BackgroundProperty,"SurfaceAlt");return callout;}
  static Grid Facts(){var grid=new Grid{Margin=new Thickness(0,4,0,10)};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto,MaxWidth=115});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});return grid;}
  static void AddFact(Grid grid,string label,string value,string evidence){
   int row=grid.RowDefinitions.Count;grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var key=Text(label,"Muted");key.Margin=new Thickness(0,0,12,7);Grid.SetRow(key,row);grid.Children.Add(key);var val=Data(value);if(!string.IsNullOrWhiteSpace(evidence))val.ToolTip=Data(evidence,"Text","UiFontSmall");Grid.SetRow(val,row);Grid.SetColumn(val,1);grid.Children.Add(val);
  }
  static string MeasurementLabel(string field){switch(field){case "PosAng":return "Position angle";case "SurfBr":return "Surface brightness";case "RadVel":return "Radial velocity";case "Redshift":return "Redshift";default:return field;}}
  static string Hours(double degrees){int seconds=(int)Math.Round(degrees/15*3600)%86400;return (seconds/3600).ToString("00")+"h "+(seconds%3600/60).ToString("00")+"m "+(seconds%60).ToString("00")+"s";}
  static string Evidence(TargetInfoCard card,TargetInfoMeasurement m){var citation=(card.citations??new TargetInfoCitation[0]).FirstOrDefault(c=>c.id==m.citation_id);return m.qualifier+(citation==null?"":"\n"+citation.source_title+"\n"+citation.source_url);}
  // The header carries no bottom margin so its text centres on the disclosure arrow.
  static Expander Disclosure(string title,StackPanel panel){var header=Text(title,"Muted");header.Margin=new Thickness(0);return new Expander{Header=header,Content=panel,Margin=new Thickness(0,2,0,6),HorizontalContentAlignment=HorizontalAlignment.Stretch};}
  internal static Inline Linked(string label,string url){
   Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))return new Run(label);
   var link=new Hyperlink(new Run(label)){NavigateUri=uri,ToolTip=url};link.SetResourceReference(Hyperlink.ForegroundProperty,"Accent");link.RequestNavigate+=(s,e)=>{if(!AstroArchive.Installation.ConnectivityPolicy.AllowsInternet){LocalizedMessage.Show(Localization.T("Choose Hybrid in Preferences > General > Online access to reconnect."),Localization.T("Online access"),MessageBoxButton.OK,MessageBoxImage.Information);e.Handled=true;return;}try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){}e.Handled=true;};return link;
  }
  static TextBlock Link(string label,string url){var text=Text("","Muted","UiFontCaption");text.Inlines.Add(Linked(label,url));return text;}
  void PositionReset(){SkyGlobeView.PlaceReset(reset,globe,mapHost);}
  void ArrangeBody(){
   if(photo!=null){var bitmap=photo.Source as BitmapSource;double ratio=bitmap.PixelWidth/(double)bitmap.PixelHeight,width=Math.Max(1,ActualWidth-scroll.Padding.Left-scroll.Padding.Right-SystemParameters.VerticalScrollBarWidth-8),cap=Math.Min(340,Math.Max(160,scroll.ActualHeight*0.48));double height=Math.Min(cap,width/ratio);photo.Height=height;photo.Width=height*ratio;if(creditRow_!=null)creditRow_.Width=photo.Width;}
   if(body==null)return;bool wide=ActualWidth>=540&&mapPanel!=null;body.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);body.ColumnDefinitions[1].Width=wide?new GridLength(orbitPanel==null?230:Math.Max(260,ActualWidth*0.48)):new GridLength(0);
   if(mapPanel!=null){Grid.SetColumn(mapPanel,wide?1:0);Grid.SetRow(mapPanel,wide?0:1);mapPanel.Margin=new Thickness(wide?16:0,4,0,10);if(mapHost!=null)mapHost.Height=wide?230:Math.Min(280,Math.Max(160,ActualWidth-40));}
  }
 }
}
