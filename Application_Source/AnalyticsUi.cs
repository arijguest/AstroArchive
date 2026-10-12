using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AstroArchive {
 public partial class MainUi {
  MenuItem AnalyticsMenu(){
   var menu=MenuAction("Analytics",()=>ShowAnalytics(-1),repo!=null&&!NetworkImportBlocked,false);
   UiHelp.Hint(menu,"Explore six branded charts and target cards; export images, PDF, SVG or animated MP4/GIF stories.");return menu;
  }
  // A negative index reopens the chart viewed last.
  void ShowAnalytics(int index){ShowAnalytics(index,null);}
  void ShowAnalytics(int index,string cardTarget){
   if(repo==null||NetworkImportBlocked)return;
   // Freeze only the metadata needed by analytics. No files are opened or decoded.
   var frames=all.Where(f=>(f.Kind=="Light"||f.Kind=="Stack")&&f.Status!="Deleted").Select(f=>new Frame{Target=f.Target,Telescope=f.Telescope,Filter=f.Filter,Kind=f.Kind,Exposure=f.Exposure,Rejected=f.Rejected,Session=f.Session,AcquisitionDate=f.AcquisitionDate,AcquisitionDateSource=f.AcquisitionDateSource,Observed=f.Observed,TimeSource=f.TimeSource,OriginalName=f.OriginalName}).ToList();
   string caption=new DirectoryInfo(repo.Root).Name;
   // Choices are saved when the window closes. Card layout and credit are shared
   // with Create target card, so both places open with the same card.
   var preferences=AnalyticsPreferences.Load(settings.Analytics,settings.AnalyticsCharts);var cardSource=CardSource(cardTarget);
   Action save=()=>{settings.Analytics=preferences;settings.AnalyticsCharts=null;settings.CardLayout=cardSource.Layout;settings.CardCredit=cardSource.Credit;SaveSettings();};
   new AnalyticsWindow(Window,frames,caption,index,cardSource,preferences,save).ShowDialog();
  }
 }
 public sealed partial class AnalyticsWindow:Window {
  public const int CardsIndex=7,CombinedIndex=6;
  readonly List<Frame> frames;readonly ListBox charts;readonly Image preview;readonly TextBlock summary,status,previewTitle,exportHint;
  readonly RadioButton imagesOutput,videoOutput;readonly ComboBox telescope,format,resolution,previewPage,documentTheme,documentLayout,chartStyle,chartOrder,palette;readonly DatePicker from,to;readonly CheckBox rejected;readonly TextBox caption,chartTitle,chartSubtitle;readonly Border paper,optionsPanel;
  readonly Button export,exportAll,close,resetChart,resetAll,optionsToggle;readonly Expander scope,exportOptions;readonly DispatcherTimer debounce,designDebounce;readonly Grid body;readonly FrameworkElement chartStyleField,chartOrderField,layoutField,paletteField,captionField;
  readonly StackPanel chartSection,combinedSection,cardSection;readonly List<CheckBox> combinedCharts=new List<CheckBox>();readonly List<TextBlock> chartDetails=new List<TextBlock>();
  readonly StackPanel motionControls;readonly ComboBox pace,transition,videoSize;readonly Button play;readonly TextBlock motionSummary;readonly ProgressBar progress;readonly DispatcherTimer playback;
  readonly AnalyticsPreferences prefs;readonly Action savePreferences;readonly string repositoryName;
  static readonly string[] ChartDetails={"Share of light frames","Integration through time","Your most imaged objects","Physical instrument totals","Integration by filter","Distribution of sub lengths"};
  AnalyticsAnimation animation;System.Diagnostics.Stopwatch playbackClock;CancellationTokenSource exportCancellation;
  AnalyticsSnapshot snapshot;List<AnalyticsPage> pages;List<AnalyticsPage>[] reportPages;int generation;bool exporting,closed,ready,choosingOutput,choosingStyle,applying,formatsForCards;string lastOutput,lastImageFormat="PDF",lastCardFormat="PNG",lastVideoFormat="MP4";
  static TextBlock Label(string text,double size=13,bool bold=false){var label=new TextBlock{Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};label.SetResourceReference(TextBlock.FontSizeProperty,size>=16?"UiFontTitle":size<=12?"UiFontSmall":"UiFontControl");return label;}
  static void AccessibleName(DependencyObject control,string name){AutomationProperties.SetName(control,name);}
  // An output choice reads as its purpose first, then the formats it offers.
  static StackPanel OutputLabel(string title,string formats){var panel=new StackPanel();panel.Children.Add(new TextBlock{Text=title,TextWrapping=TextWrapping.Wrap});var detail=new TextBlock{Text=formats,FontWeight=FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,0)};detail.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(detail,TextBlock.ForegroundProperty,"Muted");panel.Children.Add(detail);return panel;}
  // Options are grouped under short headings with a hairline between groups.
  static StackPanel Section(Panel parent,string heading,bool first=false){
   var section=new StackPanel{Margin=new Thickness(0,first?0:6,0,0)};
   if(!first){var rule=new Border{Height=1,Margin=new Thickness(0,0,0,12)};Theme.Bind(rule,Border.BackgroundProperty,"Border");section.Children.Add(rule);}
   var title=Label(heading,13,true);title.Margin=new Thickness(0,0,0,8);section.Children.Add(title);parent.Children.Add(section);return section;
  }
  public AnalyticsWindow(Window owner,List<Frame> source,string repositoryName,int initial):this(owner,source,repositoryName,initial,null){}
  public AnalyticsWindow(Window owner,List<Frame> source,string repositoryName,int initial,AnalyticsCardSource cardSource):this(owner,source,repositoryName,initial,cardSource,null,null){}
  public AnalyticsWindow(Window owner,List<Frame> source,string repositoryName,int initial,AnalyticsCardSource cardSource,AnalyticsPreferences preferences,Action save){
   frames=source;cards=cardSource;prefs=preferences??AnalyticsPreferences.Defaults();savePreferences=save;this.repositoryName=repositoryName;
   Owner=owner;Icon=ApplicationIcon.Image;Title="Analytics · AstroArchive";WindowStartupLocation=WindowStartupLocation.CenterOwner;
   Width=Math.Min(1320,SystemParameters.WorkArea.Width-24);Height=Math.Min(900,SystemParameters.WorkArea.Height-24);MinWidth=Math.Min(820,Width);MinHeight=Math.Min(600,Height);
   FontFamily=owner.FontFamily;FontSize=owner.FontSize;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,BackgroundProperty,"Canvas");Theme.Bind(this,ForegroundProperty,"Text");
   var root=new Grid{Margin=new Thickness(24)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var header=new DockPanel{Margin=new Thickness(0,0,0,14)};root.Children.Add(header);
   var brand=new Image{Source=((Image)owner.FindName("BrandLogo")).Source,Width=48,Height=48,Margin=new Thickness(0,0,14,0)};DockPanel.SetDock(brand,Dock.Left);header.Children.Add(brand);
   var heading=new StackPanel();heading.Children.Add(Label("Archive analytics",25,true));summary=Label("Preparing your observatory notes…");Theme.Bind(summary,TextBlock.ForegroundProperty,"Muted");heading.Children.Add(summary);header.Children.Add(heading);
   // Scope decides what is counted; everything about presentation lives in the options panel.
   scope=new Expander{Header="Scope · Entire repository",Margin=new Thickness(0,0,0,14)};AccessibleName(scope,"Analytics scope");Grid.SetRow(scope,1);root.Children.Add(scope);
   var filters=new WrapPanel{Margin=new Thickness(0,12,0,6)};var scopeScroll=new ScrollViewer{Content=filters,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=190};scope.Content=scopeScroll;
   telescope=new ComboBox{MinWidth=180,MaxWidth=260};telescope.Items.Add("All telescopes");foreach(string value in frames.Select(f=>string.IsNullOrWhiteSpace(f.Telescope)?"":f.Telescope).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t=>t)){telescope.Items.Add(new ComboBoxItem{Content=string.IsNullOrWhiteSpace(value)?"Unknown telescope":value,Tag=value});}telescope.SelectedIndex=0;
   from=new DatePicker{Width=145};to=new DatePicker{Width=145};
   AddField(filters,"Telescope",telescope);AddField(filters,"From (inclusive)",from);AddField(filters,"To (inclusive)",to);
   rejected=new CheckBox{Content="Include rejected light frames",Margin=new Thickness(0,25,18,0),VerticalAlignment=VerticalAlignment.Center};AccessibleName(rejected,"Include rejected light frames");filters.Children.Add(rejected);
   UiHelp.Hint(from,"Start acquisition date, inclusive. Undated captures are excluded when a date range is set.");UiHelp.Hint(to,"End acquisition date, inclusive. Clear both dates to include undated captures.");
   UiHelp.Hint(rejected,"Off excludes rejected light frames and stacks from charts and cards. Headline chart figures always show the whole-repository integration, including rejected and undated images. Stacks with matching light subframes are counted only once.");
   var reset=new Button{Content="Reset scope",Margin=new Thickness(0,24,0,0),VerticalAlignment=VerticalAlignment.Center};filters.Children.Add(reset);
   reset.Click+=(s,e)=>{telescope.SelectedIndex=0;from.SelectedDate=null;to.SelectedDate=null;from.Text="";to.Text="";rejected.IsChecked=false;Schedule();};
   // Three columns: what to show, the preview, and how it looks and exports.
   body=new Grid();Grid.SetRow(body,2);root.Children.Add(body);body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(220)});body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(300)});
   charts=new ListBox{BorderThickness=new Thickness(0),Margin=new Thickness(0,0,18,0),Style=owner.TryFindResource("TargetNavigationListStyle") as Style};AccessibleName(charts,"Analytics charts");ScrollViewer.SetHorizontalScrollBarVisibility(charts,ScrollBarVisibility.Disabled);body.Children.Add(charts);
   for(int i=0;i<8;i++){
    string name=i==CardsIndex?"Target cards":i==CombinedIndex?"Combined document":ArchiveAnalytics.Titles[i];
    var card=new StackPanel{Margin=new Thickness(8,9,8,9)};card.Children.Add(Label(i<6?(i+1).ToString("00")+"  "+name:name,14,true));var detail=Label(i<6?ChartDetails[i]:i==CardsIndex?"Share your imaged targets":"",12);detail.Margin=new Thickness(0,5,0,0);Theme.Bind(detail,TextBlock.ForegroundProperty,"Muted");card.Children.Add(detail);chartDetails.Add(detail);
    var item=new ListBoxItem{Content=card};if(i==CombinedIndex)item.Margin=new Thickness(0,10,0,0);AccessibleName(item,name);TextSearch.SetText(item,name);charts.Items.Add(item);
   }
   var pane=new Grid();pane.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});pane.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(pane,1);body.Children.Add(pane);
   var previewHeader=new DockPanel{Margin=new Thickness(0,0,0,10)};pane.Children.Add(previewHeader);
   optionsToggle=new Button{Margin=new Thickness(12,0,0,0)};AccessibleName(optionsToggle,"Show or hide options");UiHelp.Hint(optionsToggle,"Give the preview the whole width, or bring back the chart, document and export options.");DockPanel.SetDock(optionsToggle,Dock.Right);previewHeader.Children.Add(optionsToggle);
   previewPage=new ComboBox{Width=150,Margin=new Thickness(12,0,0,0),Visibility=Visibility.Collapsed};AccessibleName(previewPage,"Preview page");UiHelp.Hint(previewPage,"Inspect an individual page or card, or the combined sheet. Exports include every page in the chosen chart.");DockPanel.SetDock(previewPage,Dock.Right);previewHeader.Children.Add(previewPage);
   InitializeCardHeader(previewHeader);
   // The title gives way to the page picker and options button on one line, so it never wraps into a tall column.
   previewTitle=Label("Preview",16,true);previewTitle.VerticalAlignment=VerticalAlignment.Center;previewTitle.TextWrapping=TextWrapping.NoWrap;previewTitle.TextTrimming=TextTrimming.CharacterEllipsis;previewHeader.Children.Add(previewTitle);
   preview=new Image{Stretch=Stretch.Uniform,Margin=new Thickness(12),HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center};AccessibleName(preview,"Branded analytics export preview");RenderOptions.SetBitmapScalingMode(preview,BitmapScalingMode.HighQuality);
   paper=new Border{Background=Brushes.White,BorderThickness=new Thickness(1),Child=preview};Theme.Bind(paper,Border.BorderBrushProperty,"Border");Grid.SetRow(paper,1);pane.Children.Add(paper);
   optionsPanel=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Margin=new Thickness(18,0,0,0)};Theme.Bind(optionsPanel,Border.BackgroundProperty,"Surface");Theme.Bind(optionsPanel,Border.BorderBrushProperty,"Border");Grid.SetColumn(optionsPanel,2);body.Children.Add(optionsPanel);
   var options=new StackPanel{Margin=new Thickness(16,14,16,16)};var optionsScroll=new ScrollViewer{Content=options,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};AccessibleName(optionsScroll,"Analytics options");optionsPanel.Child=optionsScroll;
   // What is being made comes first, since it decides the formats and controls below.
   var output=Section(options,"Output",true);
   imagesOutput=new RadioButton{Content=OutputLabel("Images & documents","PNG · JPEG · PDF · SVG"),GroupName="AnalyticsOutput",IsChecked=!prefs.Video,Margin=new Thickness(0,0,0,8)};AccessibleName(imagesOutput,"Images & documents");
   videoOutput=new RadioButton{Content=OutputLabel("Video & GIF","Animated MP4 · GIF"),GroupName="AnalyticsOutput",IsChecked=prefs.Video,Margin=new Thickness(0,0,0,6)};AccessibleName(videoOutput,"Video & GIF");
   UiHelp.Hint(imagesOutput,"Still images and print-ready documents.");UiHelp.Hint(videoOutput,"Animated chart reveals or a card slideshow, ready for social posts.");
   foreach(var choice in new[]{imagesOutput,videoOutput}){choice.SetResourceReference(Control.FontSizeProperty,"UiFontControl");output.Children.Add(choice);var current=choice;Action mark=()=>{Theme.Bind(current,Control.ForegroundProperty,current.IsChecked==true?"Accent":"Text");current.FontWeight=current.IsChecked==true?FontWeights.SemiBold:FontWeights.Normal;};mark();choice.Checked+=(s,e)=>mark();choice.Unchecked+=(s,e)=>mark();}
   // The highlighted chart's own choices. Blank titles use the standard wording.
   chartSection=Section(options,"This chart");
   chartStyle=new ComboBox();chartStyleField=Field(chartSection,"Chart type",chartStyle);UiHelp.Hint(chartStyle,"Show this chart as a donut, bars or columns. Every group is still included; the timeline stays in date order.");
   chartOrder=new ComboBox{ItemsSource=AnalyticsPreferences.Orders};chartOrderField=Field(chartSection,"Order",chartOrder);UiHelp.Hint(chartOrder,"Rank groups by size or alphabetically. Donuts and columns keep the largest groups and gather the rest into Other.");
   chartTitle=new TextBox{MaxLength=AnalyticsPreferences.TitleLength};Field(chartSection,"Chart title",chartTitle);UiHelp.Hint(chartTitle,"Leave blank to use the standard title.");
   chartSubtitle=new TextBox{MaxLength=AnalyticsPreferences.SubtitleLength,TextWrapping=TextWrapping.Wrap};Field(chartSection,"Chart subtitle",chartSubtitle);UiHelp.Hint(chartSubtitle,"Leave blank to use the standard description.");
   resetChart=new Button{Content="Reset this chart",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,10)};UiHelp.Hint(resetChart,"Return this chart's type, order, title and subtitle to their defaults.");chartSection.Children.Add(resetChart);
   combinedSection=Section(options,"Charts in the document");
   for(int i=0;i<6;i++){var include=new CheckBox{Content=ArchiveAnalytics.Titles[i],IsChecked=prefs.Included(i),Margin=new Thickness(0,0,0,7)};AccessibleName(include,"Include "+ArchiveAnalytics.Titles[i]);include.SetResourceReference(Control.FontSizeProperty,"UiFontControl");combinedSection.Children.Add(include);combinedCharts.Add(include);}
   UiHelp.Hint(combinedSection,"Choose the charts that go into the combined document and Export all. At least one chart stays included.");
   cardSection=Section(options,"Target cards");InitializeCardChoices(cardSection);
   var document=Section(options,"Document");
   documentLayout=new ComboBox{ItemsSource=new[]{"Landscape · Current","Vertical · 9:16","Portrait · 4:5","Square · 1:1","Widescreen · 16:9","Pinterest · 2:3"},SelectedIndex=prefs.Layout};layoutField=Field(document,"Document layout",documentLayout);
   documentTheme=new ComboBox{ItemsSource=new[]{"Dark","Light"},SelectedIndex=prefs.LightDocument?1:0};LocalizedInterface.SetContext(documentTheme,"Theme");Field(document,"Document theme",documentTheme);
   palette=new ComboBox{ItemsSource=AnalyticsPreferences.Palettes,SelectedIndex=Math.Max(0,Array.IndexOf(AnalyticsPreferences.Palettes,prefs.Palette??""))};paletteField=Field(document,"Colour palette",palette);
   caption=new TextBox{Text=repositoryName,MaxLength=160};captionField=Field(document,"Document label",caption);UiHelp.Hint(caption,"Shown in each chart's footer and in exported file details. It starts as the repository folder name.");
   UiHelp.Hint(documentLayout,"Landscape keeps the current document layout. Social layouts cover TikTok/Reels/Stories (9:16), Instagram feed (4:5), square posts (1:1), video (16:9) and Pinterest (2:3), in every export format.");
   UiHelp.Hint(documentTheme,"Dark uses AstroArchive's midnight, blue and cyan palette. Choose Light for white-paper documents. Preview and all export formats use this choice.");
   UiHelp.Hint(palette,"Colours for chart series in the preview and every export. Colour-blind safe uses the Okabe–Ito hues. Lettering keeps the document theme's colours.");
   var exportSection=Section(options,"Export");
   format=new ComboBox();Field(exportSection,"Export format",format);
   resolution=new ComboBox{ItemsSource=new[]{"300 dpi · Print","150 dpi · Screen"},SelectedIndex=0};
   // Finer settings stay folded away until wanted.
   exportOptions=new Expander{Header="Export options",Margin=new Thickness(0,0,0,10)};AccessibleName(exportOptions,"Export options");var advanced=new StackPanel{Margin=new Thickness(0,10,0,0)};exportOptions.Content=advanced;exportSection.Children.Add(exportOptions);
   Field(advanced,"Image resolution",resolution);UiHelp.Hint(resolution,"Social exports each page at the pixel dimensions shown. Tall pages stack vertically; square and wide pages use a grid. Print and Screen use the selected dpi.");
   motionControls=new StackPanel{Visibility=Visibility.Collapsed};advanced.Children.Add(motionControls);
   pace=new ComboBox{ItemsSource=new[]{"4 s · Brisk","6 s · Balanced","8 s · Relaxed"},SelectedIndex=prefs.Pace};transition=new ComboBox{ItemsSource=new[]{"Glide","Zoom","Dissolve"},SelectedIndex=prefs.Transition};videoSize=new ComboBox{ItemsSource=new[]{"Original pixels","1280 px · Longest edge","720 px · Longest edge"},SelectedIndex=0};
   Field(motionControls,"Time per scene",pace);Field(motionControls,"Transition",transition);Field(motionControls,"Video size",videoSize);
   UiHelp.Hint(pace,"Includes a smooth reveal and a readable hold. Every continuation page or card receives the same time.");UiHelp.Hint(videoSize,"Original pixels follows the selected layout. Smaller sizes preserve its ratio. MP4 uses H.264 at 24 fps; GIF loops at 12 fps with a shared palette.");
   InitializeCardOptions(advanced);
   motionSummary=Label("",12);motionSummary.Margin=new Thickness(0,0,0,6);Theme.Bind(motionSummary,TextBlock.ForegroundProperty,"Muted");motionSummary.Visibility=Visibility.Collapsed;exportSection.Children.Add(motionSummary);
   exportHint=Label("",12);Theme.Bind(exportHint,TextBlock.ForegroundProperty,"Muted");exportSection.Children.Add(exportHint);
   var finish=Section(options,"Your choices");
   var remembered=Label("Chart types, titles, order, palette, document and export choices are remembered for next time. Ctrl+E exports; Ctrl+Shift+E exports the combined document.",12);remembered.Margin=new Thickness(0,0,0,10);Theme.Bind(remembered,TextBlock.ForegroundProperty,"Muted");finish.Children.Add(remembered);
   resetAll=new Button{Content="Reset all options…",HorizontalAlignment=HorizontalAlignment.Left};UiHelp.Hint(resetAll,"Return every chart, document and export choice to its default. The telescope and date scope are not changed.");finish.Children.Add(resetAll);
   // The footer keeps the outcome on the left and the actions on the right,
   // always in view however short the window is.
   var footer=new Grid{Margin=new Thickness(0,14,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Grid.SetRow(footer,3);root.Children.Add(footer);
   var outcome=new StackPanel{Margin=new Thickness(0,0,16,0),VerticalAlignment=VerticalAlignment.Center};footer.Children.Add(outcome);
   progress=new ProgressBar{Minimum=0,Maximum=100,Height=4,Margin=new Thickness(0,0,0,8),Visibility=Visibility.Collapsed};AccessibleName(progress,"Video export progress");outcome.Children.Add(progress);
   status=Label("Integration includes standalone stacks; stacks with matching subframes, videos and calibrations are excluded.",12);Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");AccessibleName(status,"Analytics status");AutomationProperties.SetLiveSetting(status,AutomationLiveSetting.Polite);var statusScroll=new ScrollViewer{Content=status,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};outcome.Children.Add(statusScroll);
   var open=new Button{Content="Open export folder",HorizontalAlignment=HorizontalAlignment.Left,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,0)};outcome.Children.Add(open);
   open.Click+=(s,e)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(lastOutput)){UseShellExecute=true});}catch(Exception error){status.Text=error.Message;}};
   export=new Button{Content="Export chart…"};Theme.Bind(export,Control.BackgroundProperty,"Accent");Theme.Bind(export,Control.BorderBrushProperty,"Accent");Theme.Bind(export,Control.ForegroundProperty,"AccentText");
   exportAll=new Button{Content="Export all…"};close=new Button{Content="Close",IsCancel=true};
   play=new Button{Content="▶ Play preview",Visibility=Visibility.Collapsed};AccessibleName(play,"Play animation preview");
   var exportActions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom};foreach(var button in new[]{play,export,exportAll,close}){button.Margin=new Thickness(8,0,0,0);exportActions.Children.Add(button);}Grid.SetColumn(exportActions,1);footer.Children.Add(exportActions);
   UiHelp.Hint(export,"Save the preview in the chosen format. Long rankings include continuation pages.");UiHelp.Hint(exportAll,"Save the charts chosen for the combined document in one file, using the same telescope and date scope.");
   // Short windows keep the preview usable: the scope and a long status scroll
   // within a few lines rather than pushing the preview out of the window.
   Action arrange=()=>{bool compact=ActualHeight<740;scopeScroll.MaxHeight=compact?80:190;statusScroll.MaxHeight=compact?72:double.PositiveInfinity;};SizeChanged+=(s,e)=>arrange();
   debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};debounce.Tick+=(s,e)=>{debounce.Stop();RefreshData();};
   designDebounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};designDebounce.Tick+=(s,e)=>{designDebounce.Stop();Redraw();};
   playback=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1000.0/24)};playback.Tick+=(s,e)=>{if(animation==null||playbackClock==null)return;double seconds=playbackClock.Elapsed.TotalSeconds%animation.Duration;preview.Source=animation.Preview(seconds);};
   play.Click+=(s,e)=>{if(playback.IsEnabled){playback.Stop();playbackClock.Stop();play.Content="▶ Play preview";previewPage.IsEnabled=true;}else{var scenes=SelectedPages();if(scenes==null||scenes.Count==0)return;if(animation==null){animation=new AnalyticsAnimation(scenes,VideoOptions());playbackClock=new System.Diagnostics.Stopwatch();}playbackClock.Start();playback.Start();play.Content="Ⅱ Pause preview";previewPage.IsEnabled=false;}};
   pace.SelectionChanged+=(s,e)=>{StopPlayback();Hints();};transition.SelectionChanged+=(s,e)=>{StopPlayback();Hints();};videoSize.SelectionChanged+=(s,e)=>Hints();
   telescope.SelectionChanged+=(s,e)=>Schedule();from.SelectedDateChanged+=(s,e)=>Schedule();to.SelectedDateChanged+=(s,e)=>Schedule();caption.TextChanged+=(s,e)=>Schedule();rejected.Checked+=(s,e)=>Schedule();rejected.Unchecked+=(s,e)=>Schedule();
   // Invalid typed dates must also invalidate the current export, rather than
   // silently retaining the last successfully parsed date.
   from.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>Schedule()));to.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>Schedule()));
   from.DateValidationError+=(s,e)=>{e.ThrowException=false;Schedule();};to.DateValidationError+=(s,e)=>{e.ThrowException=false;Schedule();};
   charts.SelectionChanged+=(s,e)=>{if(!ready)return;UpdateItemOptions();RefreshFormats();RenderPreview();};
   chartStyle.SelectionChanged+=(s,e)=>{int index=charts.SelectedIndex;if(choosingStyle||index<0||index>=6||chartStyle.SelectedIndex<0)return;var styles=AnalyticsGraphics.StylesFor(index);prefs.Chart(index).Style=chartStyle.SelectedIndex==0?null:styles[chartStyle.SelectedIndex];Redraw();};
   chartOrder.SelectionChanged+=(s,e)=>{int index=charts.SelectedIndex;if(choosingStyle||index<0||index>=6||chartOrder.SelectedIndex<0)return;prefs.Chart(index).Order=chartOrder.SelectedIndex==0?null:AnalyticsPreferences.Orders[chartOrder.SelectedIndex];Redraw();};
   chartTitle.TextChanged+=(s,e)=>{int index=charts.SelectedIndex;if(choosingStyle||index<0||index>=6)return;prefs.Chart(index).Title=chartTitle.Text;DesignChanged();};
   chartSubtitle.TextChanged+=(s,e)=>{int index=charts.SelectedIndex;if(choosingStyle||index<0||index>=6)return;prefs.Chart(index).Subtitle=chartSubtitle.Text;DesignChanged();};
   resetChart.Click+=(s,e)=>{int index=charts.SelectedIndex;if(index<0||index>=6||exporting)return;prefs.Charts.Remove(AnalyticsPreferences.Items[index]);UpdateItemOptions();Redraw();};
   for(int i=0;i<combinedCharts.Count;i++){
    int index=i;var include=combinedCharts[i];
    RoutedEventHandler changed=(s,e)=>{if(applying)return;if(!prefs.Include(index,include.IsChecked==true)){applying=true;include.IsChecked=true;applying=false;status.Text="Keep at least one chart in the combined document.";return;}UpdateChartDetails();Redraw();};
    include.Checked+=changed;include.Unchecked+=changed;
   }
   previewPage.SelectionChanged+=(s,e)=>DrawPreview();format.SelectionChanged+=(s,e)=>{if(choosingOutput)return;StopPlayback();videoSize.SelectedIndex=Convert.ToString(format.SelectedItem)=="GIF"?2:0;Hints();RenderPreview();};
   documentTheme.SelectionChanged+=(s,e)=>Redraw();palette.SelectionChanged+=(s,e)=>{prefs.Palette=palette.SelectedIndex>0?AnalyticsPreferences.Palettes[palette.SelectedIndex]:null;Redraw();};
   imagesOutput.Checked+=(s,e)=>ChooseOutput(false);videoOutput.Checked+=(s,e)=>ChooseOutput(true);
   documentLayout.SelectionChanged+=(s,e)=>{if(exporting)return;UpdateResolutionChoices();Redraw();};
   optionsToggle.Click+=(s,e)=>{prefs.OptionsHidden=optionsPanel.Visibility==Visibility.Visible;ShowOptions(!prefs.OptionsHidden);};
   resetAll.Click+=(s,e)=>{
    if(exporting||LocalizedMessage.Show(this,"Return every chart, document and export choice to its default? The telescope and date scope are not changed.","Reset all options",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
    prefs.Reset();caption.Text=repositoryName;ApplyPreferences();Redraw();status.Text="Options reset to their defaults.";
   };
   export.Click+=async(s,e)=>{if(await Export(false)){open.Visibility=Visibility.Visible;}};exportAll.Click+=async(s,e)=>{if(await Export(true)){open.Visibility=Visibility.Visible;}};close.Click+=(s,e)=>{if(exportCancellation!=null){exportCancellation.Cancel();close.IsEnabled=false;status.Text="Stopping export…";}else Close();};
   // Ctrl+E exports what is shown; Ctrl+Shift+E exports the combined document.
   PreviewKeyDown+=(s,e)=>{
    if(e.Key!=Key.E||(Keyboard.Modifiers&ModifierKeys.Control)==0)return;e.Handled=true;
    var target=(Keyboard.Modifiers&ModifierKeys.Shift)!=0&&exportAll.Visibility==Visibility.Visible?exportAll:export;if(target.IsEnabled&&target.Visibility==Visibility.Visible)target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   };
   Closing+=(s,e)=>{if(exporting){e.Cancel=true;if(exportCancellation!=null){exportCancellation.Cancel();status.Text="Stopping export…";}else status.Text="Finishing your export…";}};
   Closed+=(s,e)=>{closed=true;generation++;debounce.Stop();designDebounce.Stop();StopPlayback();CancelCards();RememberPreferences();};
   ApplyPreferences();UpdateChartDetails();
   ready=true;charts.SelectedIndex=Math.Max(0,Math.Min(cards==null?CombinedIndex:CardsIndex,initial<0?prefs.ItemIndex():initial));UpdateItemOptions();RefreshFormats();RestoreVideoSize();RefreshData();
  }
  // A labelled control in a WrapPanel row, as used by the scope filters.
  static FrameworkElement AddField(Panel parent,string label,Control control){var field=new StackPanel{Margin=new Thickness(0,0,16,0)};var text=Label(label,12);text.Margin=new Thickness(0,0,0,5);field.Children.Add(text);field.Children.Add(control);AccessibleName(control,label);parent.Children.Add(field);return field;}
  // A labelled control filling the options panel's width.
  static FrameworkElement Field(Panel parent,string label,Control control){
   var field=new StackPanel{Margin=new Thickness(0,0,0,12)};var text=Label(label,12);text.Margin=new Thickness(0,0,0,5);field.Children.Add(text);
   control.Width=double.NaN;control.HorizontalAlignment=HorizontalAlignment.Stretch;field.Children.Add(control);AccessibleName(control,label);parent.Children.Add(field);return field;
  }
  void ShowOptions(bool show){
   optionsPanel.Visibility=show?Visibility.Visible:Visibility.Collapsed;body.ColumnDefinitions[2].Width=show?new GridLength(300):new GridLength(0);optionsToggle.Content=show?"Hide options":"Show options";
  }
  // Puts every control in line with the saved preferences without redrawing on each change.
  void ApplyPreferences(){
   applying=true;choosingOutput=true;
   try{
    documentTheme.SelectedIndex=prefs.LightDocument?1:0;documentLayout.SelectedIndex=prefs.Layout;UpdateResolutionChoices();if(prefs.Layout==0&&prefs.ScreenResolution)resolution.SelectedIndex=1;
    palette.SelectedIndex=Math.Max(0,Array.IndexOf(AnalyticsPreferences.Palettes,prefs.Palette??""));pace.SelectedIndex=prefs.Pace;transition.SelectedIndex=prefs.Transition;cardResolution.SelectedIndex=prefs.CardResolution;
    lastImageFormat=prefs.ImageFormat;lastCardFormat=prefs.CardFormat;lastVideoFormat=prefs.VideoFormat;imagesOutput.IsChecked=!prefs.Video;videoOutput.IsChecked=prefs.Video;format.ItemsSource=null;
    for(int i=0;i<combinedCharts.Count;i++)combinedCharts[i].IsChecked=prefs.Included(i);
    ShowOptions(!prefs.OptionsHidden);
   }finally{applying=false;choosingOutput=false;}
   if(ready){UpdateItemOptions();RefreshFormats();RestoreVideoSize();UpdateChartDetails();}
  }
  void RestoreVideoSize(){if(videoOutput.IsChecked==true)videoSize.SelectedIndex=prefs.VideoSize;}
  void RememberPreferences(){
   if(charts.SelectedIndex>=0)prefs.Item=AnalyticsPreferences.Items[charts.SelectedIndex];
   prefs.LightDocument=documentTheme.SelectedIndex==1;prefs.Layout=Math.Max(0,documentLayout.SelectedIndex);if(documentLayout.SelectedIndex==0)prefs.ScreenResolution=resolution.SelectedIndex==1;
   prefs.Palette=palette.SelectedIndex>0?AnalyticsPreferences.Palettes[palette.SelectedIndex]:null;RememberFormat();prefs.Video=videoOutput.IsChecked==true;prefs.ImageFormat=lastImageFormat;prefs.CardFormat=lastCardFormat;prefs.VideoFormat=lastVideoFormat;
   prefs.Pace=pace.SelectedIndex;prefs.Transition=transition.SelectedIndex;if(prefs.Video)prefs.VideoSize=videoSize.SelectedIndex;prefs.CardResolution=cardResolution.SelectedIndex;
   cards.Layout=Math.Max(0,cardLayout.SelectedIndex);cards.Credit=cardCredit.Text.Trim();prefs.Normalise();
   if(savePreferences!=null)savePreferences();
  }
  // Rebuilds every chart for a design change and shows the result.
  void Redraw(){if(applying||!ready||snapshot==null||exporting)return;designDebounce.Stop();BuildPages();RenderPreview();UpdateChartDetails();}
  void DesignChanged(){if(applying||!ready)return;designDebounce.Stop();designDebounce.Start();}
  void UpdateResolutionChoices(){
   double w,h;AnalyticsGraphics.LayoutSize((AnalyticsLayout)Math.Max(0,documentLayout.SelectedIndex),out w,out h);bool social=documentLayout.SelectedIndex>0;
   resolution.ItemsSource=social?new[]{"300 dpi · Print","150 dpi · Screen",w.ToString("0")+" × "+h.ToString("0")+" · Social"}:new[]{"300 dpi · Print","150 dpi · Screen"};resolution.SelectedIndex=social?2:0;
  }
  // Each chart's detail line notes when it no longer uses the standard design.
  void UpdateChartDetails(){
   for(int i=0;i<6;i++){AnalyticsChartOptions options;bool custom=prefs.Charts!=null&&prefs.Charts.TryGetValue(AnalyticsPreferences.Items[i],out options)&&!options.Unchanged();chartDetails[i].Text=ChartDetails[i]+(custom?" · Customised":"");}
   int count=prefs.IncludedCharts().Count;chartDetails[CombinedIndex].Text=count==6?"All six charts in one document":count+" of 6 charts in one document";
  }
  void StopPlayback(){if(playback!=null)playback.Stop();if(play!=null)play.Content="▶ Play preview";if(previewPage!=null)previewPage.IsEnabled=true;animation=null;playbackClock=null;}
  // Each section of the options panel appears only when it applies to the highlighted item.
  void UpdateItemOptions(){
   int index=charts.SelectedIndex;bool chart=index>=0&&index<6;
   choosingStyle=true;try{
    chartSection.Visibility=chart?Visibility.Visible:Visibility.Collapsed;combinedSection.Visibility=index==CombinedIndex?Visibility.Visible:Visibility.Collapsed;
    if(chart){
     var options=prefs.Chart(index);var styles=AnalyticsGraphics.StylesFor(index);chartStyle.ItemsSource=styles.Select((style,i)=>AnalyticsGraphics.StyleLabel(style)+(i==0?" · Recommended":"")).ToList();chartStyle.SelectedIndex=Math.Max(0,Array.IndexOf(styles,options.Style??styles[0]));chartStyleField.Visibility=styles.Length>1?Visibility.Visible:Visibility.Collapsed;
     chartOrder.SelectedIndex=Math.Max(0,Array.IndexOf(AnalyticsPreferences.Orders,options.Order??AnalyticsPreferences.Orders[0]));chartOrderField.Visibility=AnalyticsPreferences.Orderable(index)?Visibility.Visible:Visibility.Collapsed;
     chartTitle.Text=options.Title??"";chartSubtitle.Text=options.Subtitle??"";
    }
    else chartStyleField.Visibility=Visibility.Collapsed;
   }finally{choosingStyle=false;}
   bool cardsShown=index==CardsIndex;paletteField.Visibility=captionField.Visibility=cardsShown?Visibility.Collapsed:Visibility.Visible;
   UpdateCardControls();
  }
  void ChooseOutput(bool video){
   if(choosingOutput||exporting)return;RefreshFormats(video);Hints();RenderPreview();
  }
  // Charts, cards and video each remember their own last format.
  void RememberFormat(){string current=Convert.ToString(format.SelectedItem);if(string.IsNullOrEmpty(current))return;if(current=="MP4"||current=="GIF")lastVideoFormat=current;else if(formatsForCards)lastCardFormat=current;else lastImageFormat=current;}
  void RefreshFormats(){RefreshFormats(videoOutput.IsChecked==true);}
  void RefreshFormats(bool video){
   string[] choices=video?new[]{"MP4","GIF"}:IsCards?new[]{"PNG","JPEG"}:new[]{"PNG","JPEG","PDF","SVG"};var currentItems=format.ItemsSource as string[];
   if(currentItems!=null&&currentItems.SequenceEqual(choices))return;
   RememberFormat();formatsForCards=!video&&IsCards;
   choosingOutput=true;try{StopPlayback();format.ItemsSource=choices;format.SelectedItem=video?lastVideoFormat:IsCards?lastCardFormat:lastImageFormat;if(format.SelectedIndex<0)format.SelectedIndex=0;videoSize.SelectedIndex=Convert.ToString(format.SelectedItem)=="GIF"?2:0;}finally{choosingOutput=false;}
   Hints();
  }
  bool IsMotion {get{return Convert.ToString(format.SelectedItem)=="MP4"||Convert.ToString(format.SelectedItem)=="GIF";}}
  AnalyticsVideoOptions VideoOptions(){return new AnalyticsVideoOptions{SecondsPerChart=pace.SelectedIndex==0?4:pace.SelectedIndex==1?6:8,FramesPerSecond=Convert.ToString(format.SelectedItem)=="GIF"?12:24,MaximumEdge=videoSize.SelectedIndex==1?1280:videoSize.SelectedIndex==2?720:0,Transition=Convert.ToString(transition.SelectedItem)};}
  void Schedule(){if(!ready||exporting)return;StopPlayback();CancelCards();play.IsEnabled=false;generation++;snapshot=null;export.IsEnabled=false;exportAll.IsEnabled=false;status.Text="Updating analytics…";debounce.Stop();debounce.Start();}
  bool ValidDate(DatePicker picker){DateTime parsed;return string.IsNullOrWhiteSpace(picker.Text)||(picker.SelectedDate.HasValue&&DateTime.TryParse(picker.Text,out parsed)&&parsed.Date==picker.SelectedDate.Value.Date);}
  async void RefreshData(){
   if(!ValidDate(from)||!ValidDate(to)){status.Text="Enter a valid date or clear the date field.";return;}
   int request=++generation;snapshot=null;export.IsEnabled=false;exportAll.IsEnabled=false;play.IsEnabled=false;status.Text="Updating analytics…";
   var selected=telescope.SelectedItem as ComboBoxItem;var options=new AnalyticsOptions{Telescope=selected==null?null:Convert.ToString(selected.Tag),From=from.SelectedDate,To=to.SelectedDate,IncludeRejected=rejected.IsChecked==true,Caption=caption.Text.Trim()};
   // An unknown telescope is represented by empty metadata; normalise just this
   // snapshot so it remains selectable independently of All telescopes.
   IEnumerable<Frame> source=frames;if(selected!=null&&string.IsNullOrWhiteSpace(options.Telescope)){options.UnknownTelescopeOnly=true;options.Telescope=null;}
   if(selected!=null)options.Caption=(string.IsNullOrWhiteSpace(options.Caption)?"Repository":options.Caption)+" · "+Convert.ToString(selected.Content);
   try{
    var result=await Task.Run(()=>ArchiveAnalytics.Build(source,options));if(closed||request!=generation)return;snapshot=result;BuildPages();
    summary.Text=result.Captures.ToString("N0")+" light frames · "+ArchiveAnalytics.Number(result.Seconds/3600)+" h integration · "+result.Targets+" targets";
    scope.Header="Scope · "+(selected==null?"All telescopes":Convert.ToString(selected.Content))+(options.From.HasValue||options.To.HasValue?" · Date range":" · All dates");
    status.Text=result.Captures==0?(result.Seconds>0?"No light frames match this scope; standalone stack integration is included.":"No light frames match this scope. Adjust the telescope or date range."):result.UnknownExposure+" images with unknown exposure · "+result.UnknownDate+" undated light frames · "+result.ExcludedRejected+" rejected light frames excluded"+(result.ExcludedUndated>0?" · "+result.ExcludedUndated+" undated light frames excluded by date range":"")+". Integration includes standalone stacks; matching stacks, videos and calibrations are excluded.";
    RenderPreview();
   }catch(Exception error){if(closed||request!=generation)return;status.Text=error.Message;}
  }
  void BuildPages(){
   bool dark=documentTheme.SelectedIndex!=1;var layout=(AnalyticsLayout)Math.Max(0,documentLayout.SelectedIndex);
   reportPages=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Pages(snapshot,i,dark,layout,prefs.Chart(i),prefs.Palette)).ToArray();pages=prefs.IncludedCharts().SelectMany(i=>reportPages[i]).ToList();
   if(!IsCards)paper.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(pages[0].Background));
  }
  List<AnalyticsPage> SelectedPages(){if(IsCards)return cardPages;if(snapshot==null||charts.SelectedIndex<0)return null;return charts.SelectedIndex==CombinedIndex?pages:reportPages[charts.SelectedIndex];}
  string CombinedName(){int count=prefs.IncludedCharts().Count;return count==6?"All six charts":count+" of 6 charts";}
  void RenderPreview(){
   if(!ready||snapshot==null||charts.SelectedIndex<0)return;StopPlayback();
   if(IsCards){RenderCards();return;}
   CancelCards();previewHoldsCards=false;bool all=charts.SelectedIndex==CombinedIndex;paper.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(pages[0].Background));
   var selected=all?pages:reportPages[charts.SelectedIndex];previewPage.Items.Clear();previewPage.Items.Add("All pages");for(int i=0;i<selected.Count;i++)previewPage.Items.Add(new ComboBoxItem{Content="Page "+(i+1)+" of "+selected.Count,ToolTip=selected[i].Title});previewPage.Visibility=selected.Count>1?Visibility.Visible:Visibility.Collapsed;previewPage.SelectedIndex=all&&documentLayout.SelectedIndex==0&&!IsMotion?0:1;
   previewTitle.Text=all?CombinedName()+(documentLayout.SelectedIndex>0?" · Social pages":" · Combined preview"):selected[0].Title+" · Export preview";
   export.Content=all?"Export all…":"Export chart…";exportAll.Visibility=all?Visibility.Collapsed:Visibility.Visible;export.IsEnabled=exportAll.IsEnabled=play.IsEnabled=(snapshot.Captures>0||snapshot.RepositorySeconds>0)&&!exporting;Hints();
  }
  void DrawPreview(){
   if(!ready||snapshot==null||charts.SelectedIndex<0||previewPage.SelectedIndex<0)return;
   StopPlayback();
   var selected=SelectedPages();if(selected==null||selected.Count==0)return;int page=previewPage.SelectedIndex-1;if(page>=selected.Count)return;preview.Source=AnalyticsExport.Preview(page<0?selected:new List<AnalyticsPage>{selected[page]});
  }
  void Hints(){
   if(!ready)return;string value=Convert.ToString(format.SelectedItem);bool still=value=="PNG"||value=="JPEG";
   resolution.IsEnabled=still&&!exporting;((FrameworkElement)resolution.Parent).Visibility=still&&!IsCards?Visibility.Visible:Visibility.Collapsed;
   motionControls.Visibility=play.Visibility=motionSummary.Visibility=IsMotion?Visibility.Visible:Visibility.Collapsed;UpdateCardControls();
   exportOptions.Header=IsMotion?"Export options · Timing and video size":IsCards?"Export options · Resolution":still?"Export options · Resolution":"Export options";
   var chosen=SelectedPages();
   if(IsMotion){var settings=VideoOptions();int count=chosen==null?0:chosen.Count;int w=0,h=0;if(count>0)AnalyticsAnimation.Dimensions(chosen[0],settings.MaximumEdge,out w,out h);motionSummary.Text=(count*settings.SecondsPerChart).ToString("0")+" s · "+count+" scene"+(count==1?"":"s")+(count>0?" · "+w+" × "+h:"");exportHint.Text=(IsCards?"A slideshow of your cards with ":"Animated chart reveals and ")+settings.Transition.ToLowerInvariant()+" transitions · "+(value=="MP4"?"H.264 MP4 · 24 fps":"Looping GIF · 12 fps")+".";return;}
   if(IsCards){exportHint.Text=CardHint();return;}
   string orientation=Convert.ToString(documentLayout.SelectedItem).ToLowerInvariant();bool tall=documentLayout.SelectedIndex==1||documentLayout.SelectedIndex==2||documentLayout.SelectedIndex==5;string sheet=tall?"Multiple pages stack vertically in one sheet.":"Multiple pages combine into one sheet.";
   exportHint.Text=value=="PDF"?"Vector charts and lettering on "+orientation+" pages. Long rankings continue onto extra pages; every target and telescope is included.":value=="SVG"?"Scalable "+orientation+" graphics with an embedded logo. "+sheet:"High-resolution "+orientation+" images in your chosen document theme. "+sheet;
  }
  async Task<bool> Export(bool all){
   if(IsCards)return await ExportCards();
   if(snapshot==null||(snapshot.Captures==0&&snapshot.RepositorySeconds<=0)||exporting)return false;all=all||charts.SelectedIndex==CombinedIndex;string selectedFormat=Convert.ToString(format.SelectedItem),extension=selectedFormat=="JPEG"?"jpg":selectedFormat.ToLowerInvariant();
   var chosen=all?pages:reportPages[charts.SelectedIndex];string combined=CombinedName();
   var dialog=new SaveFileDialog{Title=all?"Export all analytics":"Export analytics chart",FileName="AstroArchive_"+(all?"Analytics":snapshot.Reports[charts.SelectedIndex].Id)+(documentLayout.SelectedIndex>0?"_"+((AnalyticsLayout)documentLayout.SelectedIndex).ToString():"")+"."+extension,Filter=selectedFormat+" document|*."+extension,DefaultExt="."+extension,AddExtension=true,OverwritePrompt=true};
   if(dialog.ShowDialog(this)!=true)return false;
   StopPlayback();bool motion=IsMotion;var videoOptions=VideoOptions();exportCancellation=motion?new CancellationTokenSource():null;
   SetExporting(true,motion);status.Text="Preparing "+selectedFormat+" export…";
   int dpi=resolution.SelectedIndex==0?300:resolution.SelectedIndex==1?150:96;
   try{
    // WPF's image encoder needs an STA. Keep large raster and PDF work off the UI
    // dispatcher while retaining the immutable scene shown in the preview.
    var token=exportCancellation==null?CancellationToken.None:exportCancellation.Token;
    await RunExport(()=>{if(motion)AnalyticsAnimation.Save(dialog.FileName,chosen,selectedFormat,videoOptions,Reporter(),token);else AnalyticsExport.Save(dialog.FileName,chosen,selectedFormat,dpi);});
    lastOutput=dialog.FileName;status.Text="Exported "+(all?combined.ToLowerInvariant():"chart")+" · "+Path.GetFileName(lastOutput);return true;
   }catch(OperationCanceledException){status.Text="Export stopped. Your previous file is preserved.";return false;}catch(Exception error){status.Text="Export could not be saved: "+error.Message;return false;}
   finally{SetExporting(false,motion);RenderPreview();}
  }
  Action<int,string> Reporter(){
   int lastProgress=-1;var timer=System.Diagnostics.Stopwatch.StartNew();
   return (percent,message)=>{if(percent==lastProgress)return;lastProgress=percent;Dispatcher.BeginInvoke(new Action(()=>{progress.Value=percent;double remaining=percent>0&&percent<100?timer.Elapsed.TotalSeconds*(100-percent)/percent:0;status.Text=message+" · "+percent+"%"+(remaining>0?" · about "+TimeSpan.FromSeconds(remaining).ToString(@"m\:ss")+" remaining":"");}));};
  }
  static Task RunExport(Action work){
   var completion=new TaskCompletionSource<bool>();var thread=new Thread(()=>{try{work();completion.SetResult(true);}catch(Exception error){completion.SetException(error);}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
  }
  void SetExporting(bool active,bool cancellable){
   exporting=active;foreach(var control in new Control[]{imagesOutput,videoOutput,scope,charts,chartStyle,chartOrder,chartTitle,chartSubtitle,resetChart,documentTheme,documentLayout,palette,caption,format,exportOptions,export,exportAll,resetAll})control.IsEnabled=!active;foreach(var include in combinedCharts)include.IsEnabled=!active;SetCardControlsEnabled(!active);
   if(active){resolution.IsEnabled=false;close.IsEnabled=cancellable;close.IsCancel=!cancellable;close.Content=cancellable?"Cancel export":"Close";progress.Visibility=cancellable?Visibility.Visible:Visibility.Collapsed;progress.Value=0;}
   else{if(exportCancellation!=null){exportCancellation.Dispose();exportCancellation=null;}close.IsEnabled=true;close.IsCancel=true;close.Content="Close";progress.Visibility=Visibility.Collapsed;}
  }
 }
}
