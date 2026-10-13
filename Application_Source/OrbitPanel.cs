using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AstroArchive {
 // The orbit card: the canvas with its icon toolbar over it, the framing chips, the date controls with a
 // scrubbing timeline and optional playback, and the notes about what the picture is and is not.
 public sealed class OrbitPanel:StackPanel {
  const double Compact=280,Tall=440,SecondsPerRevolution=12;
  // Dated markers are limited to 1972-2026 (known leap seconds); the timeline spans what the solution also supports.
  static readonly DateTime Earliest=new DateTime(1972,1,1,0,0,0,DateTimeKind.Utc),Latest=new DateTime(2026,12,31,23,0,0,DateTimeKind.Utc);
  public readonly OrbitView View=new OrbitView{Height=Compact};
  public DateTime? MarkerUtc;
  readonly TextBlock caption,timeLine,notes,windowStart,windowEnd,playLabel;
  readonly StackPanel controls=new StackPanel(),dated=new StackPanel();
  readonly WrapPanel chips=new WrapPanel(),dateButtons=new WrapPanel();
  readonly Border toolbar;readonly StackPanel tools=new StackPanel{Orientation=Orientation.Horizontal};
  readonly Button fit,inner,play,today,expand;
  readonly Path playIcon,expandIcon;
  readonly TextBox date;readonly Slider timeline;
  CancellationTokenSource work;int generation;OrbitRecord record;bool overview,supportsDate,playing,syncing;
  DateTime windowMin=Earliest,windowMax=Latest;double rate=30;
  DispatcherTimer timer;Stopwatch clock;double previous;
  const string Reset="M 3.5,6 A 5.5,5.5 0 1 1 3.5,10.5 M 3.5,2.5 L 3.5,6 L 7,6",ZoomOut="M 3,8 L 13,8",ZoomIn="M 3,8 L 13,8 M 8,3 L 8,13",
   TopView="M 2.5,8 A 5.5,5.5 0 1 0 13.5,8 A 5.5,5.5 0 1 0 2.5,8 M 8,8 L 8.01,8",PlaneView="M 1.5,8 A 6.5,3 0 1 0 14.5,8 A 6.5,3 0 1 0 1.5,8 M 8,8 L 8.01,8",
   Larger="M 3,6 L 3,3 L 6,3 M 10,3 L 13,3 L 13,6 M 13,10 L 13,13 L 10,13 M 6,13 L 3,13 L 3,10",Smaller="M 6,3 L 6,6 L 3,6 M 10,3 L 10,6 L 13,6 M 13,10 L 10,10 L 10,13 M 3,10 L 6,10 L 6,13",
   PlayShape="M 4.5,2.5 L 13,8 L 4.5,13.5 Z",PauseShape="M 5,3 L 5,13 M 11,3 L 11,13";
  public OrbitPanel(){
   var stage=new Grid();stage.Children.Add(View);
   toolbar=new Border{CornerRadius=new CornerRadius(15),BorderThickness=new Thickness(1),Padding=new Thickness(3),Margin=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Child=tools};
   toolbar.SetResourceReference(Border.BackgroundProperty,"Surface");toolbar.SetResourceReference(Border.BorderBrushProperty,"Border");
   Tool("Reset orbit",Reset,()=>View.GoReset());Tool("Zoom out",ZoomOut,()=>View.Zoom(0.8));Tool("Zoom in",ZoomIn,()=>View.Zoom(1.25));
   Tool("Top view",TopView,()=>View.GoTop());Tool("Orbit plane",PlaneView,()=>View.GoPlane());
   expand=Tool("Expand orbit",Larger,()=>{View.Height=View.Height==Compact?Tall:Compact;Sync();});expandIcon=(Path)expand.Content;
   stage.Children.Add(toolbar);Children.Add(stage);

   fit=Chip("Fit orbit",()=>View.GoFit(false));inner=Chip("Inner solar system",()=>View.GoFit(true));controls.Children.Add(chips);

   play=new Button{Padding=new Thickness(9,3,9,3),Margin=new Thickness(0,4,6,0),MinHeight=28};playIcon=Icon(PlayShape,play);playIcon.Margin=new Thickness(0,0,6,0);
   playLabel=new TextBlock{Text="Play",VerticalAlignment=VerticalAlignment.Center};var face=new StackPanel{Orientation=Orientation.Horizontal};face.Children.Add(playIcon);face.Children.Add(playLabel);play.Content=face;
   UiHelp.Tip(play,"Play or pause the marker date. Space does the same while the orbit has focus.");play.Click+=(s,e)=>TogglePlay();dateButtons.Children.Add(play);
   today=DateButton("Today",()=>{Pause();SetMarker(DateTime.UtcNow);});DateButton("Orbit epoch",()=>{Pause();SetMarker(null);});
   date=new TextBox{Width=110,Text=DateTime.UtcNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),Margin=new Thickness(0,4,6,0),Padding=new Thickness(7,3,7,3),MinHeight=28};date.SetResourceReference(Control.BackgroundProperty,"Surface");date.SetResourceReference(Control.ForegroundProperty,"Text");date.SetResourceReference(Control.BorderBrushProperty,"Border");
   AutomationProperties.SetName(date,Localization.T("Marker date (UTC)"));date.ToolTip=Localization.T("UTC date (YYYY-MM-DD). Press Enter or Set UTC date.");date.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){ApplyDate();e.Handled=true;}};dateButtons.Children.Add(date);
   DateButton("Set UTC date",ApplyDate);dated.Children.Add(dateButtons);

   timeline=new Slider{Minimum=0,Maximum=1,IsMoveToPointEnabled=true,Margin=new Thickness(6,0,6,0),VerticalAlignment=VerticalAlignment.Center};var template=SliderTemplate();if(template!=null)timeline.Template=template;
   timeline.SetResourceReference(FrameworkElement.FocusVisualStyleProperty,"KeyboardFocusOutline");AutomationProperties.SetName(timeline,Localization.T("Marker date slider"));
   timeline.ValueChanged+=(s,e)=>{if(syncing||!supportsDate)return;Pause();SetMarker(windowMin.AddDays(timeline.Value));};
   windowStart=Caption(0,0);windowEnd=Caption(0,0);var scrub=new Grid{Margin=new Thickness(0,2,0,0)};scrub.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});scrub.ColumnDefinitions.Add(new ColumnDefinition());scrub.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   windowStart.VerticalAlignment=windowEnd.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(timeline,1);Grid.SetColumn(windowEnd,2);scrub.Children.Add(windowStart);scrub.Children.Add(timeline);scrub.Children.Add(windowEnd);dated.Children.Add(scrub);
   controls.Children.Add(dated);Children.Add(controls);

   caption=Caption(6,2);caption.Text=Localization.T("Approximate orbit · linear distances; body markers enlarged.");Children.Add(caption);
   timeLine=Caption(6,2);Children.Add(timeLine);notes=Caption(0,2);Children.Add(notes);
   var hint=Caption(6,2);hint.Text=Localization.T("Drag to rotate · Shift-drag to pan · Focus/Ctrl + wheel or pinch to zoom.");Children.Add(hint);
   View.Changed=Sync;View.TogglePlay=TogglePlay;Face();
   Unloaded+=(s,e)=>{Cancel();Pause();};IsVisibleChanged+=(s,e)=>{if(!IsVisible)Pause();};Loaded+=(s,e)=>{if(record!=null&&View.Scene==null)LoadScene();};
  }
  // Muted caption text in the smaller UI size, wrapped.
  static TextBlock Caption(double top,double bottom){var text=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,top,0,bottom)};text.SetResourceReference(TextBlock.ForegroundProperty,"Muted");text.SetResourceReference(TextBlock.FontSizeProperty,"UiFontCaption");return text;}
  // Line icons on a 16 by 16 grid, stroked in the button's own text colour so they follow the theme and the disabled state.
  static Path Icon(string data,Button owner){
   var icon=new Path{Data=Geometry.Parse(data),Width=16,Height=16,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};
   icon.SetBinding(Shape.StrokeProperty,new Binding("Foreground"){Source=owner});icon.SetBinding(Shape.FillProperty,new Binding("Foreground"){Source=owner});return icon;
  }
  Button Tool(string name,string icon,Action action){
   var button=new Button{Width=26,Height=26,MinHeight=0,Padding=new Thickness(0),Margin=new Thickness(1,0,1,0),BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Background=Brushes.Transparent};
   var shape=Icon(icon,button);shape.Fill=null;button.Content=shape;AutomationProperties.SetName(button,Localization.T(name));UiHelp.Tip(button,name);button.Click+=(s,e)=>action();tools.Children.Add(button);return button;
  }
  Button Chip(string label,Action action){var button=new Button{Content=label,Margin=new Thickness(0,4,6,0),Padding=new Thickness(7,3,7,3),MinHeight=28};AutomationProperties.SetName(button,Localization.T(label));button.Click+=(s,e)=>action();chips.Children.Add(button);return button;}
  Button DateButton(string label,Action action){var button=new Button{Content=label,Margin=new Thickness(0,4,6,0),Padding=new Thickness(7,3,7,3),MinHeight=28};AutomationProperties.SetName(button,Localization.T(label));button.Click+=(s,e)=>action();dateButtons.Children.Add(button);return button;}
  // The timeline slider, drawn in the app's own palette: a hairline track, accent for the part before the thumb, and a ringed thumb.
  const string SliderXaml=@"<ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" TargetType=""{x:Type Slider}"">
 <Grid Background=""Transparent"" SnapsToDevicePixels=""True"" MinHeight=""24"">
  <Border Height=""4"" CornerRadius=""2"" Background=""{DynamicResource Border}"" VerticalAlignment=""Center""/>
  <Track x:Name=""PART_Track"" VerticalAlignment=""Center"">
   <Track.DecreaseRepeatButton><RepeatButton Command=""{x:Static Slider.DecreaseLarge}"" Focusable=""False"" IsTabStop=""False""><RepeatButton.Template><ControlTemplate TargetType=""{x:Type RepeatButton}""><Border Height=""4"" CornerRadius=""2"" Background=""{DynamicResource Accent}""/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
   <Track.IncreaseRepeatButton><RepeatButton Command=""{x:Static Slider.IncreaseLarge}"" Focusable=""False"" IsTabStop=""False""><RepeatButton.Template><ControlTemplate TargetType=""{x:Type RepeatButton}""><Border Height=""24"" Background=""Transparent""/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
   <Track.Thumb><Thumb Width=""16"" Height=""16"" Focusable=""False"" Cursor=""Hand""><Thumb.Template><ControlTemplate TargetType=""{x:Type Thumb}""><Ellipse Fill=""{DynamicResource Surface}"" Stroke=""{DynamicResource Accent}"" StrokeThickness=""2""/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
  </Track>
 </Grid>
</ControlTemplate>";
  static ControlTemplate sliderTemplate;static bool sliderTried;
  static ControlTemplate SliderTemplate(){
   // If the template cannot be built, the stock slider still works.
   if(!sliderTried){sliderTried=true;try{sliderTemplate=(ControlTemplate)XamlReader.Parse(SliderXaml);}catch(Exception){sliderTemplate=null;}}
   return sliderTemplate;
  }
  // Fit orbit / Inner solar system show which framing is current; the expand icon follows the height. Runs on every camera move, so it acts only on a change.
  int framing=-1;bool shownTall;
  void Sync(){
   int now=View.Camera.Inner?1:0;if(now!=framing){framing=now;Mark(fit,now==0);Mark(inner,now==1);}
   bool tall=View.Height!=Compact;if(tall!=shownTall||expand.ToolTip==null){shownTall=tall;expandIcon.Data=Geometry.Parse(tall?Smaller:Larger);AutomationProperties.SetName(expand,Localization.T(tall?"Collapse orbit":"Expand orbit"));UiHelp.Tip(expand,tall?"Collapse orbit":"Expand orbit");}
  }
  static void Mark(Button button,bool on){
   if(on){button.SetResourceReference(Control.BorderBrushProperty,"Accent");button.BorderThickness=new Thickness(2);button.Padding=new Thickness(6,2,6,2);}
   else{button.ClearValue(Control.BorderBrushProperty);button.BorderThickness=new Thickness(1);button.Padding=new Thickness(7,3,7,3);}
  }
  public bool Playing {get{return playing;}}
  public void SetCard(TargetInfoCard card){
   overview=card.id=="SUN";record=overview?PlanetEphemeris.Orbit("EARTH",new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)):OrbitalCatalog.For(card);View.Overview=overview;Pause();ConfigureDates();
   // With nothing chosen yet, a dated body starts at today so the picture shows where things are now.
   if(supportsDate&&!MarkerUtc.HasValue&&OrbitalModel.Dynamical(DateTime.UtcNow).HasValue)MarkerUtc=DateTime.UtcNow;
   LoadScene();
  }
  // The timeline spans 1972-2026 where the solution is valid; one revolution takes about twelve seconds of playback.
  void ConfigureDates(){
   supportsDate=false;View.TogglePlay=null;if(record==null)return;
   if(record.valid_from.HasValue&&record.valid_to.HasValue){
    var from=OrbitalModel.Utc(record.valid_from.Value);var to=OrbitalModel.Utc(record.valid_to.Value);
    if(from.HasValue&&to.HasValue){windowMin=from.Value>Earliest?from.Value:Earliest;windowMax=to.Value<Latest?to.Value:Latest;supportsDate=windowMin<windowMax;}
   }
   if(!supportsDate)return;
   double period=OrbitMath.Period(record);rate=double.IsNaN(period)||period<=0?730:Math.Max(0.02,period/SecondsPerRevolution);
   double span=(windowMax-windowMin).TotalDays;syncing=true;timeline.Minimum=0;timeline.Maximum=span;timeline.SmallChange=Math.Max(1.0/24,Math.Min(30,double.IsNaN(period)?1:period/360));timeline.LargeChange=Math.Max(1,Math.Min(span/4,double.IsNaN(period)?30:period/12));syncing=false;
   windowStart.Text=windowMin.Year.ToString(CultureInfo.InvariantCulture);windowEnd.Text=windowMax.Year.ToString(CultureInfo.InvariantCulture);View.TogglePlay=TogglePlay;
  }
  async void LoadScene(){Cancel();int current=++generation;
   if(record==null){View.SetScene(null);toolbar.Visibility=controls.Visibility=Visibility.Collapsed;caption.Text=Localization.T("Complete orbital elements are unavailable. No orientation has been assumed.");timeLine.Text=notes.Text="";return;}
   dated.Visibility=supportsDate?Visibility.Visible:Visibility.Collapsed;today.IsEnabled=supportsDate&&OrbitalModel.Dynamical(DateTime.UtcNow).HasValue;toolbar.Visibility=controls.Visibility=Visibility.Visible;
   caption.Text=Localization.T("Approximate orbit · linear distances; body markers enlarged.");timeLine.Text=Localization.T("Preparing 3D orbit…");Describe();Sync();work=new CancellationTokenSource();var token=work.Token;
   try{var value=await Task.Run(()=>OrbitalCatalog.Scene(record,token),token);if(current!=generation||token.IsCancellationRequested)return;if(overview){value.Full.Extent=35;value.Inner.Extent=6;}Action apply=()=>{if(current!=generation||token.IsCancellationRequested)return;View.SetScene(value);SetMarker(MarkerUtc);Sync();};if(Dispatcher.CheckAccess())apply();else await Dispatcher.InvokeAsync(apply);}catch(OperationCanceledException){}catch(Exception){Action failed=()=>{if(current==generation)timeLine.Text=Localization.T("The orbit could not be drawn.");};if(Dispatcher.CheckAccess())failed();else QueueFailure(failed);}
  }
  void QueueFailure(Action action){Dispatcher.BeginInvoke(action);}
  public void Cancel(){generation++;if(work!=null){work.Cancel();work.Dispose();work=null;}}
  // What does not change with the date: the source solution and the limits of two-body geometry.
  void Describe(){
   var lines=new List<string>{Localization.F("Source epoch: JD {0} {1} · {2}",record.epoch.ToString("0.#####",CultureInfo.InvariantCulture),record.time_standard,record.solution_id),Localization.T("J2000 ecliptic · two-body geometry, not a precision ephemeris. UTC → TT; TDB differs by less than 2 ms.")};
   if(!record.valid_from.HasValue||!record.valid_to.HasValue)lines.Add(Localization.T("Snapshot positions are shown only at the orbit epoch."));
   if(record.object_id=="EARTH")lines.Add(Localization.T("Earth uses the Earth–Moon barycentre approximation."));
   notes.Text=string.Join("\n",lines);
  }
  void ApplyDate(){
   Pause();DateTime value;
   if(!DateTime.TryParseExact(date.Text.Trim(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out value)||value.Year<1972||value.Year>2026){timeLine.Text=Localization.T("Enter a valid UTC date (YYYY-MM-DD) between 1972 and 2026.");date.Focus();return;}
   SetMarker(value);
  }
  // Playback advances the marker by real elapsed time, so a slow frame never changes the pace. With reduced motion it steps four times a second instead.
  public void TogglePlay(){if(playing)Pause();else Play();}
  void Play(){
   if(record==null||!supportsDate||playing||View.Scene==null)return;
   if(!MarkerUtc.HasValue||MarkerUtc.Value<windowMin||MarkerUtc.Value>=windowMax){var now=DateTime.UtcNow;SetMarker(now>=windowMin&&now<windowMax?now:windowMin);}
   playing=true;clock=Stopwatch.StartNew();previous=0;
   if(timer==null){timer=new DispatcherTimer(DispatcherPriority.Input);timer.Tick+=Tick;}
   timer.Interval=TimeSpan.FromMilliseconds(OrbitView.ReducedMotion()?250:33);timer.Start();Face();
  }
  void Pause(){if(!playing)return;playing=false;if(timer!=null)timer.Stop();Face();}
  void Face(){
   playIcon.Data=Geometry.Parse(playing?PauseShape:PlayShape);
   // Play is a filled triangle; Pause is two bars, which read better as heavier strokes.
   if(playing){playIcon.Fill=null;playIcon.StrokeThickness=2.4;}else{playIcon.SetBinding(Shape.FillProperty,new Binding("Foreground"){Source=play});playIcon.StrokeThickness=1.5;}
   playLabel.Text=playing?"Pause":"Play";AutomationProperties.SetName(play,Localization.T(playing?"Pause":"Play"));
  }
  void Tick(object sender,EventArgs e){
   double now=clock.Elapsed.TotalSeconds,elapsed=Math.Min(0.5,now-previous);previous=now;
   var next=(MarkerUtc??windowMin).AddDays(rate*elapsed);if(next>windowMax)next=windowMin;SetMarker(next);
  }
  // The planets on the same date, so a comet or the Moon is shown among them; the target itself is the marker.
  void Place(DateTime utc){
   View.Bodies=null;if(record.central_body!="Sun")return;var list=new List<KeyValuePair<string,OrbitPoint>>();
   foreach(string id in OrbitalCatalog.Planets){OrbitPoint point;if(!overview&&id==record.object_id)continue;if(PlanetEphemeris.Position(id,utc,out point))list.Add(new KeyValuePair<string,OrbitPoint>(OrbitalCatalog.PlanetName(id),point));}
   View.Bodies=list.Count==0?null:list;
  }
  void SetMarker(DateTime? utc){
   MarkerUtc=utc;if(record==null)return;OrbitPoint point=new OrbitPoint();bool available;string time;DateTime shown;
   if(utc.HasValue){
    shown=utc.Value;double? day=OrbitalModel.Dynamical(shown);var planet=record.rights.dataset=="planetary-approximation"?PlanetEphemeris.Orbit(record.object_id,shown):null;
    available=day.HasValue&&OrbitalModel.Position(planet??record,day.Value,out point);time=Localization.F("Marker: {0} UTC",shown.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture));
    View.DateLabel=shown.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+" UTC";
   }else{
    available=OrbitalModel.Position(record,record.epoch,out point,true);time=Localization.F("Marker at orbit epoch: JD {0} {1}",record.epoch.ToString("0.#####",CultureInfo.InvariantCulture),record.time_standard);
    shown=OrbitalModel.Utc(record.epoch)??DateTime.UtcNow;View.DateLabel=Localization.F("Epoch {0}",shown.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
   }
   View.Marker=available&&!overview?(OrbitPoint?)point:null;Place(shown);
   if(!available)time=Localization.T("Dated position unavailable for this solution. Use Orbit epoch.");
   timeLine.Text=(overview?Localization.T("Solar-system overview")+" · ":"")+time;
   if(supportsDate&&shown>=windowMin&&shown<=windowMax){
    syncing=true;timeline.Value=(shown-windowMin).TotalDays;syncing=false;var label=shown.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);timeline.ToolTip=label;AutomationProperties.SetItemStatus(timeline,label);
    if(!date.IsKeyboardFocusWithin)date.Text=label;
   }
   View.Redraw();
  }
 }
}
