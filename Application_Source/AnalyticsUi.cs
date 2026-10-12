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
   var menu=MenuAction("Analytics",()=>ShowAnalytics(0),repo!=null&&!NetworkImportBlocked,false);
   UiHelp.Hint(menu,"Explore six branded charts and target cards; export images, PDF, SVG or animated MP4/GIF stories.");return menu;
  }
  void ShowAnalytics(int index){ShowAnalytics(index,null);}
  void ShowAnalytics(int index,string cardTarget){
   if(repo==null||NetworkImportBlocked)return;
   // Freeze only the metadata needed by analytics. No files are opened or decoded.
   var frames=all.Where(f=>(f.Kind=="Light"||f.Kind=="Stack")&&f.Status!="Deleted").Select(f=>new Frame{Target=f.Target,Telescope=f.Telescope,Filter=f.Filter,Kind=f.Kind,Exposure=f.Exposure,Rejected=f.Rejected,Session=f.Session,AcquisitionDate=f.AcquisitionDate,AcquisitionDateSource=f.AcquisitionDateSource,Observed=f.Observed,TimeSource=f.TimeSource,OriginalName=f.OriginalName}).ToList();
   string caption=new DirectoryInfo(repo.Root).Name;
   new AnalyticsWindow(Window,frames,caption,index,CardSource(cardTarget)).ShowDialog();
  }
 }
 public sealed partial class AnalyticsWindow:Window {
  public const int CardsIndex=7;
  readonly List<Frame> frames;readonly ListBox charts;readonly Image preview;readonly TextBlock summary,status,previewTitle,exportHint;
  readonly RadioButton imagesOutput,videoOutput;readonly ComboBox telescope,format,resolution,previewPage,documentTheme,documentLayout,chartStyle;readonly DatePicker from,to;readonly CheckBox rejected;readonly TextBox caption;readonly Border paper;
  readonly Button export,exportAll,close;readonly Expander scope,exportOptions;readonly DispatcherTimer debounce;readonly Grid body;readonly FrameworkElement chartStyleField,layoutField;
  readonly WrapPanel motionControls;readonly ComboBox pace,transition,videoSize;readonly Button play;readonly TextBlock motionSummary;readonly ProgressBar progress;readonly DispatcherTimer playback;
  readonly string[] chartStyles=new string[6];
  AnalyticsAnimation animation;System.Diagnostics.Stopwatch playbackClock;CancellationTokenSource exportCancellation;
  AnalyticsSnapshot snapshot;List<AnalyticsPage> pages;List<AnalyticsPage>[] reportPages;int generation;bool exporting,closed,ready,choosingOutput,choosingStyle,formatsForCards;string lastOutput,lastImageFormat="PDF",lastCardFormat="PNG",lastVideoFormat="MP4";
  static TextBlock Label(string text,double size=13,bool bold=false){var label=new TextBlock{Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};label.SetResourceReference(TextBlock.FontSizeProperty,size>=16?"UiFontTitle":size<=12?"UiFontSmall":"UiFontControl");return label;}
  static void AccessibleName(DependencyObject control,string name){AutomationProperties.SetName(control,name);}
  // An output choice reads as its purpose first, then the formats it offers.
  readonly List<TextBlock> outputDetails=new List<TextBlock>();
  StackPanel OutputLabel(string title,string formats){var panel=new StackPanel{Orientation=Orientation.Horizontal};panel.Children.Add(new TextBlock{Text=title});var detail=new TextBlock{Text=formats,Margin=new Thickness(8,0,0,0),FontWeight=FontWeights.Normal};Theme.Bind(detail,TextBlock.ForegroundProperty,"Muted");panel.Children.Add(detail);outputDetails.Add(detail);return panel;}
  public AnalyticsWindow(Window owner,List<Frame> source,string repositoryName,int initial):this(owner,source,repositoryName,initial,null){}
  public AnalyticsWindow(Window owner,List<Frame> source,string repositoryName,int initial,AnalyticsCardSource cardSource){
   frames=source;cards=cardSource;Owner=owner;Icon=ApplicationIcon.Image;Title="Analytics · AstroArchive";WindowStartupLocation=WindowStartupLocation.CenterOwner;
   Width=Math.Min(1280,SystemParameters.WorkArea.Width-24);Height=Math.Min(900,SystemParameters.WorkArea.Height-24);MinWidth=Math.Min(820,Width);MinHeight=Math.Min(600,Height);
   FontFamily=owner.FontFamily;FontSize=owner.FontSize;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,BackgroundProperty,"Canvas");Theme.Bind(this,ForegroundProperty,"Text");
   var root=new Grid{Margin=new Thickness(24)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var header=new DockPanel{Margin=new Thickness(0,0,0,14)};root.Children.Add(header);
   var brand=new Image{Source=((Image)owner.FindName("BrandLogo")).Source,Width=48,Height=48,Margin=new Thickness(0,0,14,0)};DockPanel.SetDock(brand,Dock.Left);header.Children.Add(brand);
   var heading=new StackPanel();heading.Children.Add(Label("Archive analytics",25,true));summary=Label("Preparing your observatory notes…");Theme.Bind(summary,TextBlock.ForegroundProperty,"Muted");heading.Children.Add(summary);header.Children.Add(heading);
   scope=new Expander{Header="Scope · Entire repository",Margin=new Thickness(0,0,0,14)};AccessibleName(scope,"Analytics scope");Grid.SetRow(scope,1);root.Children.Add(scope);
   var filters=new WrapPanel{Margin=new Thickness(0,12,0,6)};var scopeScroll=new ScrollViewer{Content=filters,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=190};scope.Content=scopeScroll;
   telescope=new ComboBox{MinWidth=180,MaxWidth=260};telescope.Items.Add("All telescopes");foreach(string value in frames.Select(f=>string.IsNullOrWhiteSpace(f.Telescope)?"":f.Telescope).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t=>t)){telescope.Items.Add(new ComboBoxItem{Content=string.IsNullOrWhiteSpace(value)?"Unknown telescope":value,Tag=value});}telescope.SelectedIndex=0;
   from=new DatePicker{Width=145};to=new DatePicker{Width=145};caption=new TextBox{Text=repositoryName,Width=220,MaxLength=160};
   AddField(filters,"Telescope",telescope);AddField(filters,"From (inclusive)",from);AddField(filters,"To (inclusive)",to);AddField(filters,"Document label",caption);
   rejected=new CheckBox{Content="Include rejected light frames",Margin=new Thickness(0,25,18,0),VerticalAlignment=VerticalAlignment.Center};AccessibleName(rejected,"Include rejected light frames");filters.Children.Add(rejected);
   UiHelp.Hint(from,"Start acquisition date, inclusive. Undated captures are excluded when a date range is set.");UiHelp.Hint(to,"End acquisition date, inclusive. Clear both dates to include undated captures.");
   UiHelp.Hint(rejected,"Off excludes rejected light frames and stacks from charts and cards. Headline chart figures always show the whole-repository integration, including rejected and undated images. Stacks with matching light subframes are counted only once.");
   var reset=new Button{Content="Reset scope",Margin=new Thickness(0,24,0,0),VerticalAlignment=VerticalAlignment.Center};filters.Children.Add(reset);
   reset.Click+=(s,e)=>{telescope.SelectedIndex=0;from.SelectedDate=null;to.SelectedDate=null;from.Text="";to.Text="";rejected.IsChecked=false;caption.Text=repositoryName;Schedule();};
   body=new Grid();Grid.SetRow(body,2);root.Children.Add(body);body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(240)});body.ColumnDefinitions.Add(new ColumnDefinition());
   // The chart list fills the column; the one choice that belongs to the
   // highlighted item (chart type, or which targets get cards) sits beneath it.
   var navigation=new DockPanel{Margin=new Thickness(0,0,18,0)};body.Children.Add(navigation);
   var itemOptions=new StackPanel{Margin=new Thickness(8,10,8,0)};DockPanel.SetDock(itemOptions,Dock.Bottom);navigation.Children.Add(itemOptions);
   chartStyle=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};chartStyleField=AddField(itemOptions,"Chart type",chartStyle);((FrameworkElement)chartStyleField).Margin=new Thickness(0);
   UiHelp.Hint(chartStyle,"Show this chart as a donut, bars or columns. Every group is still included; the timeline stays in date order.");
   InitializeCardChoices(itemOptions);
   charts=new ListBox{BorderThickness=new Thickness(0),Style=owner.TryFindResource("TargetNavigationListStyle") as Style};AccessibleName(charts,"Analytics charts");ScrollViewer.SetHorizontalScrollBarVisibility(charts,ScrollBarVisibility.Disabled);navigation.Children.Add(charts);
   string[] details={"Share of light frames","Integration through time","Your most imaged objects","Physical instrument totals","Integration by filter","Distribution of sub lengths","One document · six graphics","Share your imaged targets"};
   for(int i=0;i<8;i++){
    string name=i==CardsIndex?"Target cards":i==6?"All six charts":ArchiveAnalytics.Titles[i];
    var card=new StackPanel{Margin=new Thickness(8,9,8,9)};card.Children.Add(Label(i<6?(i+1).ToString("00")+"  "+name:name,14,true));var detail=Label(details[i],12);detail.Margin=new Thickness(0,5,0,0);Theme.Bind(detail,TextBlock.ForegroundProperty,"Muted");card.Children.Add(detail);
    var item=new ListBoxItem{Content=card};if(i==CardsIndex)item.Margin=new Thickness(0,10,0,0);AccessibleName(item,name);TextSearch.SetText(item,name);charts.Items.Add(item);
   }
   var pane=new Grid();pane.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});pane.RowDefinitions.Add(new RowDefinition());pane.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetColumn(pane,1);body.Children.Add(pane);
   var previewHeader=new DockPanel{Margin=new Thickness(0,0,0,10)};pane.Children.Add(previewHeader);
   previewPage=new ComboBox{Width=150,Margin=new Thickness(12,0,0,0),Visibility=Visibility.Collapsed};AccessibleName(previewPage,"Preview page");UiHelp.Hint(previewPage,"Inspect an individual page or card, or the combined sheet. Exports include every page in the chosen chart.");DockPanel.SetDock(previewPage,Dock.Right);previewHeader.Children.Add(previewPage);
   InitializeCardHeader(previewHeader);
   previewTitle=Label("Preview",16,true);previewTitle.VerticalAlignment=VerticalAlignment.Center;previewHeader.Children.Add(previewTitle);
   preview=new Image{Stretch=Stretch.Uniform,Margin=new Thickness(12),HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center};AccessibleName(preview,"Branded analytics export preview");RenderOptions.SetBitmapScalingMode(preview,BitmapScalingMode.HighQuality);
   paper=new Border{Background=Brushes.White,BorderThickness=new Thickness(1),Child=preview};Theme.Bind(paper,Border.BorderBrushProperty,"Border");Grid.SetRow(paper,1);pane.Children.Add(paper);
   exportHint=Label("");exportHint.Margin=new Thickness(0,10,0,0);Theme.Bind(exportHint,TextBlock.ForegroundProperty,"Muted");Grid.SetRow(exportHint,2);pane.Children.Add(exportHint);
   var footerFrame=new Grid{Margin=new Thickness(0,12,0,0)};Grid.SetRow(footerFrame,3);root.Children.Add(footerFrame);
   for(int i=0;i<3;i++)footerFrame.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   // Images and video are equal, always-visible choices beside the export settings.
   var outputChoices=new WrapPanel{Margin=new Thickness(0,0,0,10)};footerFrame.Children.Add(outputChoices);
   var outputLabel=Label("Export as",12,true);outputLabel.Margin=new Thickness(0,3,16,0);outputChoices.Children.Add(outputLabel);
   imagesOutput=new RadioButton{Content=OutputLabel("Images & documents","PNG · JPEG · PDF · SVG"),GroupName="AnalyticsOutput",IsChecked=true,Margin=new Thickness(0,0,22,0),VerticalAlignment=VerticalAlignment.Center};AccessibleName(imagesOutput,"Images & documents");
   videoOutput=new RadioButton{Content=OutputLabel("Video & GIF","Animated MP4 · GIF"),GroupName="AnalyticsOutput",VerticalAlignment=VerticalAlignment.Center};AccessibleName(videoOutput,"Video & GIF");
   UiHelp.Hint(imagesOutput,"Still images and print-ready documents.");UiHelp.Hint(videoOutput,"Animated chart reveals or a card slideshow, ready for social posts.");
   foreach(var choice in new[]{imagesOutput,videoOutput}){choice.SetResourceReference(Control.FontSizeProperty,"UiFontControl");outputChoices.Children.Add(choice);var current=choice;Action mark=()=>{Theme.Bind(current,Control.ForegroundProperty,current.IsChecked==true?"Accent":"Text");current.FontWeight=current.IsChecked==true?FontWeights.SemiBold:FontWeights.Normal;};mark();choice.Checked+=(s,e)=>mark();choice.Unchecked+=(s,e)=>mark();}
   var footer=new StackPanel();var optionsScroll=new ScrollViewer{Content=footer,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=230};Grid.SetRow(optionsScroll,1);footerFrame.Children.Add(optionsScroll);
   var actions=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};footer.Children.Add(actions);
   format=new ComboBox{ItemsSource=new[]{"PNG","JPEG","PDF","SVG"},SelectedIndex=2,Width=105};resolution=new ComboBox{ItemsSource=new[]{"300 dpi · Print","150 dpi · Screen"},SelectedIndex=0,Width=190};
   documentTheme=new ComboBox{ItemsSource=new[]{"Dark","Light"},SelectedIndex=0,Width=100};LocalizedInterface.SetContext(documentTheme,"Theme");
   documentLayout=new ComboBox{ItemsSource=new[]{"Landscape · Current","Vertical · 9:16","Portrait · 4:5","Square · 1:1","Widescreen · 16:9","Pinterest · 2:3"},SelectedIndex=0,Width=200};
   layoutField=AddField(actions,"Document layout",documentLayout);InitializeCardLayout(actions);AddField(actions,"Document theme",documentTheme);AddField(actions,"Export format",format);
   UiHelp.Hint(documentLayout,"Landscape keeps the current document layout. Social layouts cover TikTok/Reels/Stories (9:16), Instagram feed (4:5), square posts (1:1), video (16:9) and Pinterest (2:3), in every export format.");
   UiHelp.Hint(documentTheme,"Dark uses AstroArchive's midnight, blue and cyan palette. Choose Light for white-paper documents. Preview and all export formats use this choice.");
   // Finer settings stay folded away until wanted.
   exportOptions=new Expander{Header="Export options",Margin=new Thickness(0,10,0,0)};AccessibleName(exportOptions,"Export options");var advanced=new WrapPanel{Margin=new Thickness(0,8,0,0)};exportOptions.Content=advanced;footer.Children.Add(exportOptions);
   AddField(advanced,"Image resolution",resolution);UiHelp.Hint(resolution,"Social exports each page at the pixel dimensions shown. Tall pages stack vertically; square and wide pages use a grid. Print and Screen use the selected dpi.");
   motionControls=new WrapPanel{Visibility=Visibility.Collapsed};advanced.Children.Add(motionControls);
   pace=new ComboBox{ItemsSource=new[]{"4 s · Brisk","6 s · Balanced","8 s · Relaxed"},SelectedIndex=0,Width=155};transition=new ComboBox{ItemsSource=new[]{"Glide","Zoom","Dissolve"},SelectedIndex=0,Width=130};videoSize=new ComboBox{ItemsSource=new[]{"Original pixels","1280 px · Longest edge","720 px · Longest edge"},SelectedIndex=0,Width=190};
   AddField(motionControls,"Time per scene",pace);AddField(motionControls,"Transition",transition);AddField(motionControls,"Video size",videoSize);
   UiHelp.Hint(pace,"Includes a smooth reveal and a readable hold. Every continuation page or card receives the same time.");UiHelp.Hint(videoSize,"Original pixels follows the selected layout. Smaller sizes preserve its ratio. MP4 uses H.264 at 24 fps; GIF loops at 12 fps with a shared palette.");
   InitializeCardOptions(advanced);
   motionSummary=Label("",12);motionSummary.Margin=new Thickness(0,6,0,0);Theme.Bind(motionSummary,TextBlock.ForegroundProperty,"Muted");motionSummary.Visibility=Visibility.Collapsed;footer.Children.Add(motionSummary);
   progress=new ProgressBar{Minimum=0,Maximum=100,Height=4,Margin=new Thickness(0,10,0,0),Visibility=Visibility.Collapsed};AccessibleName(progress,"Video export progress");footer.Children.Add(progress);
   status=Label("Integration includes standalone stacks; stacks with matching subframes, videos and calibrations are excluded.",12);status.Margin=new Thickness(0,10,0,0);Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");AccessibleName(status,"Analytics status");AutomationProperties.SetLiveSetting(status,AutomationLiveSetting.Polite);footer.Children.Add(status);
   var open=new Button{Content="Open export folder",HorizontalAlignment=HorizontalAlignment.Left,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,0)};footer.Children.Add(open);
   open.Click+=(s,e)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(lastOutput)){UseShellExecute=true});}catch(Exception error){status.Text=error.Message;}};
   // Short or narrow windows keep the preview usable: the format hints beside the
   // output choices give way first so those choices stay on one line.
   Action arrangeOptions=()=>{bool compact=ActualHeight<740;foreach(var detail in outputDetails)detail.Visibility=compact||ActualWidth<1100?Visibility.Collapsed:Visibility.Visible;optionsScroll.MaxHeight=compact?(scope.IsExpanded?72:104):230;scopeScroll.MaxHeight=compact?80:190;exportHint.Visibility=compact?Visibility.Collapsed:Visibility.Visible;};
   SizeChanged+=(s,e)=>arrangeOptions();scope.Expanded+=(s,e)=>arrangeOptions();scope.Collapsed+=(s,e)=>arrangeOptions();
   export=new Button{Content="Export chart…",VerticalAlignment=VerticalAlignment.Bottom};Theme.Bind(export,Control.BackgroundProperty,"Accent");Theme.Bind(export,Control.BorderBrushProperty,"Accent");Theme.Bind(export,Control.ForegroundProperty,"AccentText");
   exportAll=new Button{Content="Export all…",VerticalAlignment=VerticalAlignment.Bottom};close=new Button{Content="Close",IsCancel=true,VerticalAlignment=VerticalAlignment.Bottom};
   play=new Button{Content="▶ Play preview",VerticalAlignment=VerticalAlignment.Bottom,Visibility=Visibility.Collapsed};AccessibleName(play,"Play animation preview");
   var exportActions=new WrapPanel{Margin=new Thickness(0,10,0,0)};foreach(var button in new[]{play,export,exportAll,close}){button.Margin=new Thickness(0,0,8,0);exportActions.Children.Add(button);}Grid.SetRow(exportActions,2);footerFrame.Children.Add(exportActions);
   UiHelp.Hint(export,"Save the preview in the chosen format. Long rankings include continuation pages.");UiHelp.Hint(exportAll,"Save all six charts in one document, using the same telescope and date scope.");
   debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};debounce.Tick+=(s,e)=>{debounce.Stop();RefreshData();};
   playback=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1000.0/24)};playback.Tick+=(s,e)=>{if(animation==null||playbackClock==null)return;double seconds=playbackClock.Elapsed.TotalSeconds%animation.Duration;preview.Source=animation.Preview(seconds);};
   play.Click+=(s,e)=>{if(playback.IsEnabled){playback.Stop();playbackClock.Stop();play.Content="▶ Play preview";previewPage.IsEnabled=true;}else{var scenes=SelectedPages();if(scenes==null||scenes.Count==0)return;if(animation==null){animation=new AnalyticsAnimation(scenes,VideoOptions());playbackClock=new System.Diagnostics.Stopwatch();}playbackClock.Start();playback.Start();play.Content="Ⅱ Pause preview";previewPage.IsEnabled=false;}};
   pace.SelectionChanged+=(s,e)=>{StopPlayback();Hints();};transition.SelectionChanged+=(s,e)=>{StopPlayback();Hints();};videoSize.SelectionChanged+=(s,e)=>Hints();
   telescope.SelectionChanged+=(s,e)=>Schedule();from.SelectedDateChanged+=(s,e)=>Schedule();to.SelectedDateChanged+=(s,e)=>Schedule();caption.TextChanged+=(s,e)=>Schedule();rejected.Checked+=(s,e)=>Schedule();rejected.Unchecked+=(s,e)=>Schedule();
   // Invalid typed dates must also invalidate the current export, rather than
   // silently retaining the last successfully parsed date.
   from.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>Schedule()));to.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>Schedule()));
   from.DateValidationError+=(s,e)=>{e.ThrowException=false;Schedule();};to.DateValidationError+=(s,e)=>{e.ThrowException=false;Schedule();};
   charts.SelectionChanged+=(s,e)=>{if(!ready)return;UpdateItemOptions();RefreshFormats();RenderPreview();};
   chartStyle.SelectionChanged+=(s,e)=>{int index=charts.SelectedIndex;if(choosingStyle||index<0||index>=6||chartStyle.SelectedIndex<0)return;chartStyles[index]=AnalyticsGraphics.StylesFor(index)[chartStyle.SelectedIndex];if(snapshot==null||exporting)return;BuildPages();RenderPreview();};
   previewPage.SelectionChanged+=(s,e)=>DrawPreview();format.SelectionChanged+=(s,e)=>{if(choosingOutput)return;StopPlayback();videoSize.SelectedIndex=Convert.ToString(format.SelectedItem)=="GIF"?2:0;Hints();RenderPreview();};documentTheme.SelectionChanged+=(s,e)=>{if(snapshot==null||exporting)return;BuildPages();RenderPreview();};
   imagesOutput.Checked+=(s,e)=>ChooseOutput(false);videoOutput.Checked+=(s,e)=>ChooseOutput(true);
   documentLayout.SelectionChanged+=(s,e)=>{if(exporting)return;double w,h;AnalyticsGraphics.LayoutSize((AnalyticsLayout)documentLayout.SelectedIndex,out w,out h);resolution.ItemsSource=documentLayout.SelectedIndex>0?new[]{"300 dpi · Print","150 dpi · Screen",w.ToString("0")+" × "+h.ToString("0")+" · Social"}:new[]{"300 dpi · Print","150 dpi · Screen"};resolution.SelectedIndex=documentLayout.SelectedIndex>0?2:0;if(snapshot!=null){BuildPages();RenderPreview();}};
   export.Click+=async(s,e)=>{if(await Export(false)){open.Visibility=Visibility.Visible;}};exportAll.Click+=async(s,e)=>{if(await Export(true)){open.Visibility=Visibility.Visible;}};close.Click+=(s,e)=>{if(exportCancellation!=null){exportCancellation.Cancel();close.IsEnabled=false;status.Text="Stopping export…";}else Close();};
   Closing+=(s,e)=>{if(exporting){e.Cancel=true;if(exportCancellation!=null){exportCancellation.Cancel();status.Text="Stopping export…";}else status.Text="Finishing your export…";}};
   Closed+=(s,e)=>{closed=true;generation++;debounce.Stop();StopPlayback();CancelCards();};
   ready=true;charts.SelectedIndex=Math.Max(0,Math.Min(cards==null?6:CardsIndex,initial));UpdateItemOptions();RefreshFormats();RefreshData();
  }
  static FrameworkElement AddField(Panel parent,string label,Control control){var field=new StackPanel{Margin=new Thickness(0,0,16,0)};var text=Label(label,12);text.Margin=new Thickness(0,0,0,5);field.Children.Add(text);field.Children.Add(control);AccessibleName(control,label);parent.Children.Add(field);return field;}
  void StopPlayback(){if(playback!=null)playback.Stop();if(play!=null)play.Content="▶ Play preview";if(previewPage!=null)previewPage.IsEnabled=true;animation=null;playbackClock=null;}
  // Chart type appears only for charts that can change; card choices only for cards.
  void UpdateItemOptions(){
   int index=charts.SelectedIndex;bool chart=index>=0&&index<6;
   choosingStyle=true;try{
    if(chart){var styles=AnalyticsGraphics.StylesFor(index);chartStyle.ItemsSource=styles.Select((style,i)=>AnalyticsGraphics.StyleLabel(style)+(i==0?" · Recommended":"")).ToList();chartStyle.SelectedIndex=Math.Max(0,Array.IndexOf(styles,chartStyles[index]??styles[0]));chartStyleField.Visibility=styles.Length>1?Visibility.Visible:Visibility.Collapsed;}
    else chartStyleField.Visibility=Visibility.Collapsed;
   }finally{choosingStyle=false;}
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
  void BuildPages(){reportPages=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Pages(snapshot,i,documentTheme.SelectedIndex==0,(AnalyticsLayout)documentLayout.SelectedIndex,chartStyles[i])).ToArray();pages=reportPages.SelectMany(p=>p).ToList();if(!IsCards)paper.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(pages[0].Background));}
  List<AnalyticsPage> SelectedPages(){if(IsCards)return cardPages;if(snapshot==null||charts.SelectedIndex<0)return null;return charts.SelectedIndex==6?pages:reportPages[charts.SelectedIndex];}
  void RenderPreview(){
   if(!ready||snapshot==null||charts.SelectedIndex<0)return;StopPlayback();
   if(IsCards){RenderCards();return;}
   CancelCards();previewHoldsCards=false;bool all=charts.SelectedIndex==6;paper.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(pages[0].Background));
   var selected=all?pages:reportPages[charts.SelectedIndex];previewPage.Items.Clear();previewPage.Items.Add("All pages");for(int i=0;i<selected.Count;i++)previewPage.Items.Add(new ComboBoxItem{Content="Page "+(i+1)+" of "+selected.Count,ToolTip=selected[i].Title});previewPage.Visibility=selected.Count>1?Visibility.Visible:Visibility.Collapsed;previewPage.SelectedIndex=all&&documentLayout.SelectedIndex==0&&!IsMotion?0:1;
   previewTitle.Text=all?(documentLayout.SelectedIndex>0?"All six charts · Social pages":"All six charts · Combined preview"):ArchiveAnalytics.Titles[charts.SelectedIndex]+" · Export preview";
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
   exportOptions.Header=IsMotion?"Export options · Timing and video size":IsCards?"Export options · Resolution and credit":still?"Export options · Resolution":"Export options";
   var chosen=SelectedPages();
   if(IsMotion){var settings=VideoOptions();int count=chosen==null?0:chosen.Count;int w=0,h=0;if(count>0)AnalyticsAnimation.Dimensions(chosen[0],settings.MaximumEdge,out w,out h);motionSummary.Text=(count*settings.SecondsPerChart).ToString("0")+" s · "+count+" scene"+(count==1?"":"s")+(count>0?" · "+w+" × "+h:"");exportHint.Text=(IsCards?"A slideshow of your cards with ":"Animated chart reveals and ")+settings.Transition.ToLowerInvariant()+" transitions · "+(value=="MP4"?"H.264 MP4 · 24 fps":"Looping GIF · 12 fps")+".";return;}
   if(IsCards){exportHint.Text=CardHint();return;}
   string orientation=Convert.ToString(documentLayout.SelectedItem).ToLowerInvariant();bool tall=documentLayout.SelectedIndex==1||documentLayout.SelectedIndex==2||documentLayout.SelectedIndex==5;string sheet=tall?"Multiple pages stack vertically in one sheet.":"Multiple pages combine into one sheet.";
   exportHint.Text=value=="PDF"?"Vector charts and lettering on "+orientation+" pages. Long rankings continue onto extra pages; every target and telescope is included.":value=="SVG"?"Scalable "+orientation+" graphics with an embedded logo. "+sheet:"High-resolution "+orientation+" images in your chosen document theme. "+sheet;
  }
  async Task<bool> Export(bool all){
   if(IsCards)return await ExportCards();
   if(snapshot==null||(snapshot.Captures==0&&snapshot.RepositorySeconds<=0)||exporting)return false;all=all||charts.SelectedIndex==6;string selectedFormat=Convert.ToString(format.SelectedItem),extension=selectedFormat=="JPEG"?"jpg":selectedFormat.ToLowerInvariant();
   var chosen=all?pages:reportPages[charts.SelectedIndex];
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
    lastOutput=dialog.FileName;status.Text="Exported "+(all?"all six charts":"chart")+" · "+Path.GetFileName(lastOutput);return true;
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
   exporting=active;foreach(var control in new Control[]{imagesOutput,videoOutput,scope,charts,chartStyle,documentTheme,documentLayout,format,exportOptions,export,exportAll})control.IsEnabled=!active;SetCardControlsEnabled(!active);
   if(active){resolution.IsEnabled=false;close.IsEnabled=cancellable;close.IsCancel=!cancellable;close.Content=cancellable?"Cancel export":"Close";progress.Visibility=cancellable?Visibility.Visible:Visibility.Collapsed;progress.Value=0;}
   else{if(exportCancellation!=null){exportCancellation.Dispose();exportCancellation=null;}close.IsEnabled=true;close.IsCancel=true;close.Content="Close";progress.Visibility=Visibility.Collapsed;}
  }
 }
}
