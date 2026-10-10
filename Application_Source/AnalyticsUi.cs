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
   var menu=MenuAction("Analytics…",()=>ShowAnalytics(0),repo!=null&&!NetworkImportBlocked,false);
   UiHelp.Hint(menu,"Explore six branded charts; export PNG, JPEG, PDF, SVG or animated MP4/GIF stories.");return menu;
  }
  void ShowAnalytics(int index){
   if(repo==null||NetworkImportBlocked)return;
   // Freeze only the metadata needed by analytics. No files are opened or decoded.
   var frames=all.Where(f=>f.Kind=="Light"&&f.Status!="Deleted").Select(f=>new Frame{Target=f.Target,Telescope=f.Telescope,Filter=f.Filter,Kind=f.Kind,Exposure=f.Exposure,Rejected=f.Rejected,AcquisitionDate=f.AcquisitionDate,AcquisitionDateSource=f.AcquisitionDateSource,Observed=f.Observed,TimeSource=f.TimeSource,OriginalName=f.OriginalName}).ToList();
   string caption=new DirectoryInfo(repo.Root).Name;
   if(settings.AnalyticsCharts==null)settings.AnalyticsCharts=new Dictionary<string,AnalyticsChartOptions>();
   new AnalyticsWindow(Window,frames,caption,index,settings.AnalyticsCharts,SaveSettings).ShowDialog();
  }
 }
 public sealed partial class AnalyticsWindow:Window {
  readonly List<Frame> frames;readonly ListBox charts;readonly Image preview;readonly TextBlock summary,status,previewTitle,exportHint;
  readonly ComboBox telescope,format,resolution,previewPage,documentTheme,documentLayout;readonly DatePicker from,to;readonly CheckBox rejected;readonly TextBox caption;readonly Border paper;
  readonly Button export,exportAll,close,customize;readonly Expander scope;readonly DispatcherTimer debounce;readonly Grid body;
  readonly WrapPanel motionControls;readonly ComboBox pace,transition,videoSize,outputKind;readonly TextBox duration;readonly Button play;readonly TextBlock motionSummary;readonly ProgressBar progress;readonly DispatcherTimer playback;
  AnalyticsAnimation animation;System.Diagnostics.Stopwatch playbackClock;CancellationTokenSource exportCancellation;
  AnalyticsSnapshot snapshot;List<AnalyticsPage> pages;List<AnalyticsPage>[] reportPages;int generation;bool exporting,closed,ready;string lastOutput;string lastDocumentFormat="PDF",lastMotionFormat="MP4";
  readonly Dictionary<string,AnalyticsChartOptions> configurations;readonly Action saveConfigurations;
  static TextBlock Label(string text,double size=13,bool bold=false){var label=new TextBlock{Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};label.SetResourceReference(TextBlock.FontSizeProperty,size>=16?"UiFontTitle":size<=12?"UiFontSmall":"UiFontControl");return label;}
  static void AccessibleName(DependencyObject control,string name){AutomationProperties.SetName(control,name);}
  public AnalyticsWindow(Window owner,List<Frame> source,string repositoryName,int initial,Dictionary<string,AnalyticsChartOptions> configurations=null,Action saveConfigurations=null){
   this.configurations=configurations??new Dictionary<string,AnalyticsChartOptions>();this.saveConfigurations=saveConfigurations;
   frames=source;Owner=owner;Icon=ApplicationIcon.Image;Title="Analytics · AstroArchive";WindowStartupLocation=WindowStartupLocation.CenterOwner;
   Width=Math.Min(1280,SystemParameters.WorkArea.Width-24);Height=Math.Min(900,SystemParameters.WorkArea.Height-24);MinWidth=Math.Min(820,Width);MinHeight=Math.Min(600,Height);
   FontFamily=owner.FontFamily;FontSize=owner.FontSize;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,BackgroundProperty,"Canvas");Theme.Bind(this,ForegroundProperty,"Text");
   var root=new Grid{Margin=new Thickness(20)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var header=new DockPanel{Margin=new Thickness(0,0,0,14)};root.Children.Add(header);
   var brand=new Image{Source=((Image)owner.FindName("BrandLogo")).Source,Width=48,Height=48,Margin=new Thickness(0,0,14,0)};DockPanel.SetDock(brand,Dock.Left);header.Children.Add(brand);
   outputKind=new ComboBox{ItemsSource=new[]{"Document","Video / GIF"},SelectedIndex=0,Width=155,VerticalAlignment=VerticalAlignment.Center};AccessibleName(outputKind,"Export type");DockPanel.SetDock(outputKind,Dock.Right);header.Children.Add(outputKind);
   var heading=new StackPanel();heading.Children.Add(Label("Archive analytics",25,true));summary=Label("Preparing your observatory notes…");Theme.Bind(summary,TextBlock.ForegroundProperty,"Muted");heading.Children.Add(summary);header.Children.Add(heading);
   scope=new Expander{Header="Scope · Entire repository",Margin=new Thickness(0,0,0,14)};Grid.SetRow(scope,1);root.Children.Add(scope);
   var filters=new WrapPanel{Margin=new Thickness(0,12,0,6)};scope.Content=filters;
   telescope=new ComboBox{MinWidth=180,MaxWidth=260};telescope.Items.Add("All telescopes");foreach(string value in frames.Select(f=>string.IsNullOrWhiteSpace(f.Telescope)?"":f.Telescope).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t=>t)){telescope.Items.Add(new ComboBoxItem{Content=string.IsNullOrWhiteSpace(value)?"Unknown telescope":value,Tag=value});}telescope.SelectedIndex=0;
   from=new DatePicker{Width=145};to=new DatePicker{Width=145};caption=new TextBox{Text=repositoryName,Width=220,MaxLength=160};
   AddField(filters,"Telescope",telescope);AddField(filters,"From (inclusive)",from);AddField(filters,"To (inclusive)",to);AddField(filters,"Document label",caption);
   rejected=new CheckBox{Content="Include rejected light frames",Margin=new Thickness(0,25,18,0),VerticalAlignment=VerticalAlignment.Center};AccessibleName(rejected,"Include rejected light frames");filters.Children.Add(rejected);
   UiHelp.Hint(from,"Start acquisition date, inclusive. Undated captures are excluded when a date range is set.");UiHelp.Hint(to,"End acquisition date, inclusive. Clear both dates to include undated captures.");
   UiHelp.Hint(rejected,"Off excludes rejected frames from charts. Headline cards always show all repository light frames, including rejected and undated captures. Unknown exposures are never guessed.");
   var reset=new Button{Content="Reset scope",Margin=new Thickness(0,24,0,0),VerticalAlignment=VerticalAlignment.Center};filters.Children.Add(reset);
   reset.Click+=(s,e)=>{telescope.SelectedIndex=0;from.SelectedDate=null;to.SelectedDate=null;from.Text="";to.Text="";rejected.IsChecked=false;caption.Text=repositoryName;Schedule();};
   body=new Grid();Grid.SetRow(body,2);root.Children.Add(body);body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(240)});body.ColumnDefinitions.Add(new ColumnDefinition());
   charts=new ListBox{BorderThickness=new Thickness(0),Margin=new Thickness(0,0,18,0),Style=owner.TryFindResource("TargetNavigationListStyle") as Style};AccessibleName(charts,"Analytics charts");ScrollViewer.SetHorizontalScrollBarVisibility(charts,ScrollBarVisibility.Disabled);body.Children.Add(charts);
   string[] details={"Share of light frames","Integration through time","Your most imaged objects","Physical instrument totals","Integration by filter","Distribution of sub lengths","One document · six graphics"};
   for(int i=0;i<7;i++){
    var card=new StackPanel{Margin=new Thickness(8,9,8,9)};card.Children.Add(Label(i==6?"All six charts":(i+1).ToString("00")+"  "+ArchiveAnalytics.Titles[i],14,true));var detail=Label(details[i],12);detail.Margin=new Thickness(0,5,0,0);Theme.Bind(detail,TextBlock.ForegroundProperty,"Muted");card.Children.Add(detail);
    var item=new ListBoxItem{Content=card};AccessibleName(item,i==6?"All six charts":ArchiveAnalytics.Titles[i]);TextSearch.SetText(item,i==6?"All six charts":ArchiveAnalytics.Titles[i]);charts.Items.Add(item);
   }
   var pane=new Grid();pane.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});pane.RowDefinitions.Add(new RowDefinition());pane.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetColumn(pane,1);body.Children.Add(pane);
   var previewHeader=new DockPanel{Margin=new Thickness(0,0,0,8)};pane.Children.Add(previewHeader);
   customize=new Button{Content="Customize…",Margin=new Thickness(8,0,0,0)};AccessibleName(customize,"Customize chart");DockPanel.SetDock(customize,Dock.Right);previewHeader.Children.Add(customize);customize.Click+=(s,e)=>{if(!exporting&&snapshot!=null)ConfigurationDialog().Show();};
   previewPage=new ComboBox{Width=132,Margin=new Thickness(12,0,0,0),Visibility=Visibility.Collapsed};AccessibleName(previewPage,"Preview page");UiHelp.Hint(previewPage,"Inspect an individual print page or the combined sheet. Exports include every page in the chosen chart.");DockPanel.SetDock(previewPage,Dock.Right);previewHeader.Children.Add(previewPage);
   previewTitle=Label("Preview",16,true);previewHeader.Children.Add(previewTitle);
   preview=new Image{Stretch=Stretch.Uniform,Margin=new Thickness(4),HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center};AccessibleName(preview,"Branded analytics export preview");
   paper=new Border{Background=Brushes.White,BorderThickness=new Thickness(1),Child=preview};Theme.Bind(paper,Border.BorderBrushProperty,"Border");Grid.SetRow(paper,1);pane.Children.Add(paper);
   exportHint=Label("");exportHint.Margin=new Thickness(0,10,0,0);Theme.Bind(exportHint,TextBlock.ForegroundProperty,"Muted");Grid.SetRow(exportHint,2);pane.Children.Add(exportHint);
   var footer=new StackPanel{Margin=new Thickness(0,12,0,0)};Grid.SetRow(footer,3);root.Children.Add(footer);
   var actions=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};footer.Children.Add(actions);
   format=new ComboBox{ItemsSource=new[]{"PNG","JPEG","PDF","SVG"},SelectedIndex=2,Width=105};resolution=new ComboBox{ItemsSource=new[]{"300 dpi · Print","150 dpi · Screen"},SelectedIndex=0,Width=190};
   documentTheme=new ComboBox{ItemsSource=new[]{"Dark","Light"},SelectedIndex=0,Width=100};
   documentLayout=new ComboBox{ItemsSource=new[]{"Landscape · Current","Vertical · 9:16","Portrait · 4:5","Square · 1:1","Widescreen · 16:9","Pinterest · 2:3"},SelectedIndex=0,Width=200};
   AddField(actions,"Document layout",documentLayout);AddField(actions,"Document theme",documentTheme);AddField(actions,"Export format",format);AddField(actions,"Image resolution",resolution);
   UiHelp.Hint(documentLayout,"Landscape keeps the current document layout. Social layouts cover TikTok/Reels/Stories (9:16), Instagram feed (4:5), square posts (1:1), video (16:9) and Pinterest (2:3), in every export format.");
   UiHelp.Hint(resolution,"Social exports each page at the pixel dimensions shown. Tall pages stack vertically; square and wide pages use a grid. Print and Screen use the selected dpi.");
   UiHelp.Hint(documentTheme,"Dark uses AstroArchive's midnight, blue and cyan palette. Choose Light for white-paper documents. Preview and all export formats use this choice.");
   export=new Button{Content="Export chart…",Margin=new Thickness(0,8,8,0),VerticalAlignment=VerticalAlignment.Bottom};Theme.Bind(export,Control.BackgroundProperty,"Accent");Theme.Bind(export,Control.BorderBrushProperty,"Accent");Theme.Bind(export,Control.ForegroundProperty,"AccentText");
   exportAll=new Button{Content="Export all…",Margin=new Thickness(0,8,8,0),VerticalAlignment=VerticalAlignment.Bottom};close=new Button{Content="Close",IsCancel=true,Margin=new Thickness(0,8,0,0),VerticalAlignment=VerticalAlignment.Bottom};var exportActions=new WrapPanel();exportActions.Children.Add(export);exportActions.Children.Add(exportAll);exportActions.Children.Add(close);
   UiHelp.Hint(export,"Save the previewed chart in the chosen format. Long rankings include continuation pages.");UiHelp.Hint(exportAll,"Save all six charts in one document, using the same telescope and date scope.");
   motionControls=new WrapPanel{Margin=new Thickness(0,12,0,0),Visibility=Visibility.Collapsed};footer.Children.Add(motionControls);
   pace=new ComboBox{ItemsSource=new[]{"4 s · Brisk","6 s · Balanced","8 s · Relaxed","Custom length"},SelectedIndex=0,Width=155};transition=new ComboBox{ItemsSource=new[]{"Glide","Zoom","Dissolve"},SelectedIndex=0,Width=130};videoSize=new ComboBox{ItemsSource=new[]{"Original pixels","1280 px · Longest edge","720 px · Longest edge"},SelectedIndex=0,Width=190};
   duration=new TextBox{Text="24",Width=90,MaxLength=6};
   AddField(motionControls,"Pace",pace);AddField(motionControls,"Length · seconds",duration);AddField(motionControls,"Transition",transition);AddField(motionControls,"Video size",videoSize);
   play=new Button{Content="▶ Play preview",Margin=new Thickness(0,22,12,0),VerticalAlignment=VerticalAlignment.Bottom};AccessibleName(play,"Play animation preview");motionControls.Children.Add(play);motionSummary=Label("",12);motionSummary.Margin=new Thickness(0,26,0,0);Theme.Bind(motionSummary,TextBlock.ForegroundProperty,"Muted");motionControls.Children.Add(motionSummary);footer.Children.Add(exportActions);
   UiHelp.Hint(pace,"Includes a smooth chart reveal and a readable hold. Every continuation page receives the same time.");UiHelp.Hint(videoSize,"Original pixels follows the selected layout. Smaller sizes preserve its ratio. MP4 uses H.264 at 24 fps; GIF loops at 12 fps with a shared palette.");
   progress=new ProgressBar{Minimum=0,Maximum=100,Height=4,Margin=new Thickness(0,10,0,0),Visibility=Visibility.Collapsed};AccessibleName(progress,"Video export progress");footer.Children.Add(progress);
   status=Label("Time totals use individual light-frame exposures. Stacks, videos and calibrations are excluded.",12);status.Margin=new Thickness(0,10,0,0);Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");AccessibleName(status,"Analytics status");AutomationProperties.SetLiveSetting(status,AutomationLiveSetting.Polite);footer.Children.Add(status);
   var open=new Button{Content="Open export folder",HorizontalAlignment=HorizontalAlignment.Left,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,0)};footer.Children.Add(open);
   open.Click+=(s,e)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(lastOutput)){UseShellExecute=true});}catch(Exception error){status.Text=error.Message;}};
   debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};debounce.Tick+=(s,e)=>{debounce.Stop();RefreshData();};
   playback=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1000.0/24)};playback.Tick+=(s,e)=>{if(animation==null||playbackClock==null)return;double seconds=playbackClock.Elapsed.TotalSeconds%animation.Duration;preview.Source=animation.Preview(seconds);};
   play.Click+=(s,e)=>{if(playback.IsEnabled){playback.Stop();playbackClock.Stop();play.Content="▶ Play preview";previewPage.IsEnabled=true;}else{if(animation==null){animation=new AnalyticsAnimation(charts.SelectedIndex==6?pages:reportPages[charts.SelectedIndex],VideoOptions());playbackClock=new System.Diagnostics.Stopwatch();}playbackClock.Start();playback.Start();play.Content="Ⅱ Pause preview";previewPage.IsEnabled=false;}};
   outputKind.SelectionChanged+=(s,e)=>{if(!ready||exporting)return;StopPlayback();format.ItemsSource=outputKind.SelectedIndex==1?new[]{"MP4","GIF"}:new[]{"PNG","JPEG","PDF","SVG"};format.SelectedItem=outputKind.SelectedIndex==1?lastMotionFormat:lastDocumentFormat;Hints();RenderPreview();};
   duration.TextChanged+=(s,e)=>{if(!ready)return;StopPlayback();Hints();};
   pace.SelectionChanged+=(s,e)=>{StopPlayback();Hints();};transition.SelectionChanged+=(s,e)=>{StopPlayback();Hints();};videoSize.SelectionChanged+=(s,e)=>Hints();
   telescope.SelectionChanged+=(s,e)=>Schedule();from.SelectedDateChanged+=(s,e)=>Schedule();to.SelectedDateChanged+=(s,e)=>Schedule();caption.TextChanged+=(s,e)=>Schedule();rejected.Checked+=(s,e)=>Schedule();rejected.Unchecked+=(s,e)=>Schedule();
   // Invalid typed dates must also invalidate the current export, rather than
   // silently retaining the last successfully parsed date.
   from.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>Schedule()));to.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>Schedule()));
   from.DateValidationError+=(s,e)=>{e.ThrowException=false;Schedule();};to.DateValidationError+=(s,e)=>{e.ThrowException=false;Schedule();};
   charts.SelectionChanged+=(s,e)=>RenderPreview();previewPage.SelectionChanged+=(s,e)=>DrawPreview();format.SelectionChanged+=(s,e)=>{if(format.SelectedItem!=null){if(IsMotion)lastMotionFormat=Convert.ToString(format.SelectedItem);else lastDocumentFormat=Convert.ToString(format.SelectedItem);}StopPlayback();videoSize.SelectedIndex=Convert.ToString(format.SelectedItem)=="GIF"?2:0;Hints();RenderPreview();};documentTheme.SelectionChanged+=(s,e)=>{if(snapshot==null||exporting)return;BuildPages();RenderPreview();};
   documentLayout.SelectionChanged+=(s,e)=>{if(exporting)return;double w,h;AnalyticsGraphics.LayoutSize((AnalyticsLayout)documentLayout.SelectedIndex,out w,out h);resolution.ItemsSource=documentLayout.SelectedIndex>0?new[]{"300 dpi · Print","150 dpi · Screen",w.ToString("0")+" × "+h.ToString("0")+" · Social"}:new[]{"300 dpi · Print","150 dpi · Screen"};resolution.SelectedIndex=documentLayout.SelectedIndex>0?2:0;if(snapshot!=null){BuildPages();RenderPreview();}};
   export.Click+=async(s,e)=>{if(await Export(false)){open.Visibility=Visibility.Visible;}};exportAll.Click+=async(s,e)=>{if(await Export(true)){open.Visibility=Visibility.Visible;}};close.Click+=(s,e)=>{if(exportCancellation!=null){exportCancellation.Cancel();close.IsEnabled=false;status.Text="Stopping export…";}else Close();};
   Closing+=(s,e)=>{if(exporting){e.Cancel=true;if(exportCancellation!=null){exportCancellation.Cancel();status.Text="Stopping export…";}else status.Text="Finishing your export…";}};
   Closed+=(s,e)=>{closed=true;generation++;debounce.Stop();StopPlayback();};
   ready=true;charts.SelectedIndex=Math.Max(0,Math.Min(6,initial));RefreshData();
  }
  static void AddField(Panel parent,string label,Control control){var field=new StackPanel{Margin=new Thickness(0,0,16,0)};var text=Label(label,12);text.Margin=new Thickness(0,0,0,5);field.Children.Add(text);field.Children.Add(control);AccessibleName(control,label);parent.Children.Add(field);}
  void StopPlayback(){if(playback!=null)playback.Stop();if(play!=null)play.Content="▶ Play preview";if(previewPage!=null)previewPage.IsEnabled=true;animation=null;playbackClock=null;}
  bool IsMotion {get{return Convert.ToString(format.SelectedItem)=="MP4"||Convert.ToString(format.SelectedItem)=="GIF";}}
  AnalyticsVideoOptions VideoOptions(){return new AnalyticsVideoOptions{SecondsPerChart=pace.SelectedIndex==0?4:pace.SelectedIndex==1?6:8,TotalSeconds=pace.SelectedIndex==3?CustomDuration():0,Loop=Convert.ToString(format.SelectedItem)=="GIF",FramesPerSecond=Convert.ToString(format.SelectedItem)=="GIF"?12:24,MaximumEdge=videoSize.SelectedIndex==1?1280:videoSize.SelectedIndex==2?720:0,Transition=Convert.ToString(transition.SelectedItem)};}
  void Schedule(){if(!ready||exporting)return;StopPlayback();play.IsEnabled=false;generation++;snapshot=null;export.IsEnabled=false;exportAll.IsEnabled=false;status.Text="Updating analytics…";debounce.Stop();debounce.Start();}
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
    status.Text=result.Captures==0?"No light frames match this scope. Adjust the telescope or date range.":result.UnknownExposure+" unknown exposures · "+result.UnknownDate+" undated frames · "+result.ExcludedRejected+" rejected frames excluded"+(result.ExcludedUndated>0?" · "+result.ExcludedUndated+" undated frames excluded by date range":"")+". Integration excludes stacks, videos and calibrations.";
    RenderPreview();
   }catch(Exception error){if(closed||request!=generation)return;status.Text=error.Message;}
  }
  void BuildPages(){reportPages=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Pages(snapshot,i,documentTheme.SelectedIndex==0,(AnalyticsLayout)documentLayout.SelectedIndex,ChartConfiguration(i))).ToArray();pages=reportPages.SelectMany(p=>p).ToList();paper.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(pages[0].Background));}
  void RenderPreview(){
   if(!ready||snapshot==null||charts.SelectedIndex<0)return;StopPlayback();bool all=charts.SelectedIndex==6;customize.IsEnabled=!exporting;
   var selected=all?pages:reportPages[charts.SelectedIndex];previewPage.Items.Clear();previewPage.Items.Add("All pages");for(int i=0;i<selected.Count;i++)previewPage.Items.Add(new ComboBoxItem{Content="Page "+(i+1)+" of "+selected.Count,ToolTip=selected[i].Title});previewPage.Visibility=selected.Count>1?Visibility.Visible:Visibility.Collapsed;previewPage.SelectedIndex=all&&documentLayout.SelectedIndex==0&&!IsMotion?0:1;
   previewTitle.Text=all?(documentLayout.SelectedIndex>0?"All six charts · Social pages":"All six charts · Combined preview"):ArchiveAnalytics.Titles[charts.SelectedIndex]+" · Export preview";
   export.Content=all?"Export all…":"Export chart…";exportAll.Visibility=all?Visibility.Collapsed:Visibility.Visible;export.IsEnabled=exportAll.IsEnabled=play.IsEnabled=snapshot.Captures>0&&!exporting;Hints();
  }
  void DrawPreview(){
   if(!ready||snapshot==null||charts.SelectedIndex<0||previewPage.SelectedIndex<0)return;
   StopPlayback();
   var selected=charts.SelectedIndex==6?pages:reportPages[charts.SelectedIndex];int page=previewPage.SelectedIndex-1;preview.Source=AnalyticsExport.Preview(page<0?selected:new List<AnalyticsPage>{selected[page]});
  }
  void Hints(){
   if(!ready)return;string value=Convert.ToString(format.SelectedItem);resolution.IsEnabled=(value=="PNG"||value=="JPEG")&&!exporting;
   motionControls.Visibility=IsMotion?Visibility.Visible:Visibility.Collapsed;
   ((FrameworkElement)resolution.Parent).Visibility=IsMotion?Visibility.Collapsed:Visibility.Visible;
   ((FrameworkElement)duration.Parent).Visibility=pace.SelectedIndex==3?Visibility.Visible:Visibility.Collapsed;
   bool valid=!IsMotion||pace.SelectedIndex!=3||CustomDuration()>0;if(snapshot!=null)export.IsEnabled=exportAll.IsEnabled=play.IsEnabled=snapshot.Captures>0&&!exporting&&valid;
   if(!valid){motionSummary.Text="Enter a length from 1 to 300 seconds.";exportHint.Text="The chosen length covers every chart and continuation page.";return;}
   if(IsMotion){var settings=VideoOptions();var chosen=snapshot==null?null:charts.SelectedIndex==6?pages:charts.SelectedIndex>=0?reportPages[charts.SelectedIndex]:null;int count=chosen==null?0:chosen.Count;int w=0,h=0;if(chosen!=null)AnalyticsAnimation.Dimensions(chosen[0],settings.MaximumEdge,out w,out h);motionSummary.Text=(settings.TotalSeconds>0?settings.TotalSeconds:count*settings.SecondsPerChart).ToString("0.##")+" s · "+count+" scene"+(count==1?"":"s")+" · "+w+" × "+h;exportHint.Text=(value=="GIF"?"Looping animation":"Video")+" · "+settings.Transition+" transitions. Export all includes every chart and continuation page.";return;}
   string orientation=Convert.ToString(documentLayout.SelectedItem).ToLowerInvariant();bool tall=documentLayout.SelectedIndex==1||documentLayout.SelectedIndex==2||documentLayout.SelectedIndex==5;string sheet=tall?"Multiple pages stack vertically in one sheet.":"Multiple pages combine into one sheet.";
   exportHint.Text=value=="PDF"?"Vector charts and lettering on "+orientation+" pages. Long rankings continue onto extra pages; every target and telescope is included.":value=="SVG"?"Scalable "+orientation+" graphics with an embedded logo. "+sheet:"High-resolution "+orientation+" images in your chosen document theme. "+sheet;
  }
  async Task<bool> Export(bool all){
   if(snapshot==null||snapshot.Captures==0||exporting||IsMotion&&pace.SelectedIndex==3&&CustomDuration()<=0)return false;all=all||charts.SelectedIndex==6;string selectedFormat=Convert.ToString(format.SelectedItem),extension=selectedFormat=="JPEG"?"jpg":selectedFormat.ToLowerInvariant();
   var chosen=all?pages:reportPages[charts.SelectedIndex];
   var dialog=new SaveFileDialog{Title=all?"Export all analytics":"Export analytics chart",FileName="AstroArchive_"+(all?"Analytics":snapshot.Reports[charts.SelectedIndex].Id)+(documentLayout.SelectedIndex>0?"_"+((AnalyticsLayout)documentLayout.SelectedIndex).ToString():"")+"."+extension,Filter=selectedFormat+" document|*."+extension,DefaultExt="."+extension,AddExtension=true,OverwritePrompt=true};
   if(dialog.ShowDialog(this)!=true)return false;
   StopPlayback();bool motion=IsMotion;var videoOptions=VideoOptions();exportCancellation=motion?new CancellationTokenSource():null;
   exporting=true;customize.IsEnabled=false;outputKind.IsEnabled=false;scope.IsEnabled=false;charts.IsEnabled=false;documentTheme.IsEnabled=false;documentLayout.IsEnabled=false;format.IsEnabled=false;resolution.IsEnabled=false;motionControls.IsEnabled=false;export.IsEnabled=false;exportAll.IsEnabled=false;close.IsEnabled=motion;close.IsCancel=!motion;close.Content=motion?"Cancel export":"Close";progress.Visibility=motion?Visibility.Visible:Visibility.Collapsed;progress.Value=0;status.Text="Preparing "+selectedFormat+" export…";
   int dpi=resolution.SelectedIndex==0?300:resolution.SelectedIndex==1?150:96;
   try{
    // WPF's image encoder needs an STA. Keep large raster and PDF work off the UI
    // dispatcher while retaining the immutable scene shown in the preview.
    var completion=new TaskCompletionSource<bool>();var token=exportCancellation==null?CancellationToken.None:exportCancellation.Token;int lastProgress=-1;var timer=System.Diagnostics.Stopwatch.StartNew();
    Action<int,string> report=(percent,message)=>{if(percent==lastProgress)return;lastProgress=percent;Dispatcher.BeginInvoke(new Action(()=>{progress.Value=percent;double remaining=percent>0&&percent<100?timer.Elapsed.TotalSeconds*(100-percent)/percent:0;status.Text=message+" · "+percent+"%"+(remaining>0?" · about "+TimeSpan.FromSeconds(remaining).ToString(@"m\:ss")+" remaining":"");}));};
    var thread=new Thread(()=>{try{if(motion)AnalyticsAnimation.Save(dialog.FileName,chosen,selectedFormat,videoOptions,report,token);else AnalyticsExport.Save(dialog.FileName,chosen,selectedFormat,dpi);completion.SetResult(true);}catch(Exception error){completion.SetException(error);}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();await completion.Task;
    lastOutput=dialog.FileName;status.Text="Exported "+(all?"all six charts":"chart")+" · "+Path.GetFileName(lastOutput);return true;
   }catch(OperationCanceledException){status.Text="Export stopped. Your previous file is preserved.";return false;}catch(Exception error){status.Text="Export could not be saved: "+error.Message;return false;}
   finally{exporting=false;customize.IsEnabled=true;outputKind.IsEnabled=true;if(exportCancellation!=null){exportCancellation.Dispose();exportCancellation=null;}scope.IsEnabled=true;charts.IsEnabled=true;documentTheme.IsEnabled=true;documentLayout.IsEnabled=true;format.IsEnabled=true;motionControls.IsEnabled=true;close.IsEnabled=true;close.IsCancel=true;close.Content="Close";progress.Visibility=Visibility.Collapsed;RenderPreview();}
  }
 }
}
