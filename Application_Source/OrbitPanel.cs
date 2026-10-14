using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AstroArchive {
 // The orbit card: the canvas with its icon toolbar over it and one line saying what the picture is.
 // It shows where the target is now (or at its orbit epoch when no dated position exists) and where it was when imaged.
 public sealed class OrbitPanel:StackPanel {
  const double Compact=280,Tall=440;
  // Dated positions are limited to 1972-2026 (known leap seconds).
  static readonly DateTime Earliest=new DateTime(1972,1,1,0,0,0,DateTimeKind.Utc),Latest=new DateTime(2026,12,31,23,0,0,DateTimeKind.Utc);
  public readonly OrbitView View=new OrbitView{Height=Compact};
  public DateTime? MarkerUtc;
  // Dates this target was imaged in the repository, set before SetCard.
  public List<DateTime> CaptureDates;
  readonly TextBlock caption,dateText;readonly TextBox dateEdit;readonly Grid dateHost;
  readonly Border toolbar;readonly StackPanel tools=new StackPanel{Orientation=Orientation.Horizontal};
  readonly Button expand;readonly Path expandIcon;
  CancellationTokenSource work;int generation;OrbitRecord record;bool overview,supportsDate;
  const string Reset="M 3.5,6 A 5.5,5.5 0 1 1 3.5,10.5 M 3.5,2.5 L 3.5,6 L 7,6",ZoomOut="M 3,8 L 13,8",ZoomIn="M 3,8 L 13,8 M 8,3 L 8,13",
   TopView="M 2.5,8 A 5.5,5.5 0 1 0 13.5,8 A 5.5,5.5 0 1 0 2.5,8 M 8,8 L 8.01,8",PlaneView="M 1.5,8 A 6.5,3 0 1 0 14.5,8 A 6.5,3 0 1 0 1.5,8 M 8,8 L 8.01,8",
   Larger="M 3,6 L 3,3 L 6,3 M 10,3 L 13,3 L 13,6 M 13,10 L 13,13 L 10,13 M 6,13 L 3,13 L 3,10",Smaller="M 6,3 L 6,6 L 3,6 M 10,3 L 10,6 L 13,6 M 13,10 L 10,10 L 10,13 M 3,10 L 6,10 L 6,13";
  public OrbitPanel(){
   var stage=new Grid();stage.Children.Add(View);
   toolbar=new Border{CornerRadius=new CornerRadius(15),BorderThickness=new Thickness(1),Padding=new Thickness(3),Margin=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Child=tools};
   toolbar.SetResourceReference(Border.BackgroundProperty,"Surface");toolbar.SetResourceReference(Border.BorderBrushProperty,"Border");
   Tool("Reset orbit",Reset,()=>View.GoReset());Tool("Zoom out",ZoomOut,()=>View.Zoom(0.8));Tool("Zoom in",ZoomIn,()=>View.Zoom(1.25));
   Tool("Top view",TopView,()=>View.GoTop());Tool("Orbit plane",PlaneView,()=>View.GoPlane());
   expand=Tool("Expand orbit",Larger,()=>{View.Height=View.Height==Compact?Tall:Compact;Sync();});expandIcon=(Path)expand.Content;
   stage.Children.Add(toolbar);Children.Add(stage);
   caption=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,2)};caption.SetResourceReference(TextBlock.ForegroundProperty,"Muted");caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontCaption");Children.Add(caption);
   // The date sits opposite the ORBIT heading in the same small caps style; clicking it opens a box to type another date.
   dateText=new TextBlock{FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Focusable=true};KeyboardNavigation.SetIsTabStop(dateText,true);
   dateText.SetResourceReference(TextBlock.ForegroundProperty,"Muted");dateText.SetResourceReference(TextBlock.FontSizeProperty,"UiFontCaption");
   dateEdit=new TextBox{Visibility=Visibility.Collapsed,HorizontalAlignment=HorizontalAlignment.Right,MinWidth=96,Padding=new Thickness(4,1,4,1),MaxLength=12};dateEdit.SetResourceReference(TextBox.FontSizeProperty,"UiFontCaption");
   dateHost=new Grid();dateHost.Children.Add(dateText);dateHost.Children.Add(dateEdit);
   dateText.MouseLeftButtonUp+=(s,e)=>{BeginDateEdit();e.Handled=true;};dateText.KeyDown+=(s,e)=>{if(e.Key==Key.Enter||e.Key==Key.Space){BeginDateEdit();e.Handled=true;}};
   dateEdit.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){CommitDateEdit();e.Handled=true;}else if(e.Key==Key.Escape){EndDateEdit();e.Handled=true;}};dateEdit.LostKeyboardFocus+=(s,e)=>{if(dateEdit.Visibility==Visibility.Visible)CommitDateEdit();};
   View.Changed=Sync;Unloaded+=(s,e)=>Cancel();Loaded+=(s,e)=>{if(record!=null&&View.Scene==null)LoadScene();};
  }
  // Line icons on a 16 by 16 grid, stroked in the button's own text colour so they follow the theme and the disabled state.
  static Path Icon(string data,Button owner){
   var icon=new Path{Data=Geometry.Parse(data),Width=16,Height=16,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};
   icon.SetBinding(Shape.StrokeProperty,new Binding("Foreground"){Source=owner});icon.SetBinding(Shape.FillProperty,new Binding("Foreground"){Source=owner});return icon;
  }
  Button Tool(string name,string icon,Action action){
   var button=new Button{Width=26,Height=26,MinHeight=0,Padding=new Thickness(0),Margin=new Thickness(1,0,1,0),BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Background=Brushes.Transparent};
   var shape=Icon(icon,button);shape.Fill=null;button.Content=shape;AutomationProperties.SetName(button,Localization.T(name));UiHelp.Tip(button,name);button.Click+=(s,e)=>action();tools.Children.Add(button);return button;
  }
  // The expand icon follows the height. Runs on every camera move, so it acts only on a change.
  bool shownTall;
  void Sync(){
   bool tall=View.Height!=Compact;if(tall!=shownTall||expand.ToolTip==null){shownTall=tall;expandIcon.Data=Geometry.Parse(tall?Smaller:Larger);AutomationProperties.SetName(expand,Localization.T(tall?"Collapse orbit":"Expand orbit"));UiHelp.Tip(expand,tall?"Collapse orbit":"Expand orbit");}
  }
  // Placed beside the heading by the card; shows where the target is on the chosen date.
  public FrameworkElement HeaderDate{get{return dateHost;}}
  void ShowDate(string text){
   dateText.Text=text;bool editable=supportsDate&&record!=null;dateText.Cursor=editable?Cursors.Hand:Cursors.Arrow;dateText.Focusable=editable;
   dateText.TextDecorations=editable?TextDecorations.Underline:null;
   string tip=editable?Localization.F("Click to change the date ({0}).",DateFormats.Hint):null;dateText.ToolTip=tip;AutomationProperties.SetName(dateText,Localization.T("Orbit date")+": "+text);AutomationProperties.SetHelpText(dateText,tip??"");
  }
  void BeginDateEdit(){
   if(!supportsDate||record==null)return;
   dateEdit.Text=DateFormats.Format((MarkerUtc??DateTime.UtcNow).Date);dateEdit.ToolTip=Localization.F("Type a date as {0}, then press Enter. Esc cancels.",DateFormats.Hint);AutomationProperties.SetName(dateEdit,Localization.T("Orbit date"));
   dateEdit.Visibility=Visibility.Visible;dateText.Visibility=Visibility.Hidden;dateEdit.Focus();dateEdit.SelectAll();
  }
  void EndDateEdit(){dateEdit.Visibility=Visibility.Collapsed;dateText.Visibility=Visibility.Visible;dateEdit.ClearValue(Control.BorderBrushProperty);}
  void CommitDateEdit(){
   DateTime day;
   if(!DateFormats.TryParse(dateEdit.Text,out day)||day<Earliest.Date||day>Latest.Date||!OrbitalModel.Dynamical(DateTime.SpecifyKind(day,DateTimeKind.Utc)).HasValue){
    dateEdit.BorderBrush=Brushes.IndianRed;dateEdit.ToolTip=Localization.F("Enter a date as {0} between {1} and {2}.",DateFormats.Hint,DateFormats.Format(Earliest),DateFormats.Format(Latest));return;
   }
   EndDateEdit();var utc=DateTime.SpecifyKind(day,DateTimeKind.Utc);
   // Today keeps the live clock time, so "now" stays now.
   SetMarker(utc.Date==DateTime.UtcNow.Date&&OrbitalModel.Dynamical(DateTime.UtcNow).HasValue?DateTime.UtcNow:utc);
  }
  public void SetCard(TargetInfoCard card){
   overview=card.id=="SUN";record=overview?PlanetEphemeris.Orbit("EARTH",new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)):OrbitalCatalog.For(card);View.Overview=overview;supportsDate=false;
   if(record!=null&&record.valid_from.HasValue&&record.valid_to.HasValue){
    var from=OrbitalModel.Utc(record.valid_from.Value);var to=OrbitalModel.Utc(record.valid_to.Value);
    if(from.HasValue&&to.HasValue)supportsDate=(from.Value>Earliest?from.Value:Earliest)<(to.Value<Latest?to.Value:Latest);
   }
   // A dated body shows where it is today; anything else shows its position at the orbit epoch.
   if(supportsDate&&!MarkerUtc.HasValue&&OrbitalModel.Dynamical(DateTime.UtcNow).HasValue)MarkerUtc=DateTime.UtcNow;
   LoadScene();
  }
  async void LoadScene(){Cancel();int current=++generation;
   if(record==null){View.SetScene(null);toolbar.Visibility=Visibility.Collapsed;dateHost.Visibility=Visibility.Collapsed;caption.Text=Localization.T("Complete orbital elements are unavailable. No orientation has been assumed.");return;}
   toolbar.Visibility=Visibility.Visible;dateHost.Visibility=Visibility.Visible;caption.Text=Localization.T("Preparing 3D orbit…");Sync();work=new CancellationTokenSource();var token=work.Token;
   try{var value=await Task.Run(()=>OrbitalCatalog.Scene(record,token),token);if(current!=generation||token.IsCancellationRequested)return;if(overview){value.Full.Extent=35;value.Inner.Extent=6;}Action apply=()=>{if(current!=generation||token.IsCancellationRequested)return;View.SetScene(value);View.Imaged=ImagedMarks();SetMarker(MarkerUtc);Sync();};if(Dispatcher.CheckAccess())apply();else await Dispatcher.InvokeAsync(apply);}catch(OperationCanceledException){}catch(Exception){Action failed=()=>{if(current==generation)caption.Text=Localization.T("The orbit could not be drawn.");};if(Dispatcher.CheckAccess())failed();else Dispatcher.BeginInvoke(failed);}
  }
  public void Cancel(){generation++;if(work!=null){work.Cancel();work.Dispose();work=null;}}
  // The planets on the same date, so a comet or the Moon is shown among them; the target itself is the marker.
  void Place(DateTime utc){
   View.Bodies=null;if(record.central_body!="Sun")return;var list=new List<KeyValuePair<string,OrbitPoint>>();
   foreach(string id in OrbitalCatalog.Planets){OrbitPoint point;if(!overview&&id==record.object_id)continue;if(PlanetEphemeris.Position(id,utc,out point))list.Add(new KeyValuePair<string,OrbitPoint>(OrbitalCatalog.PlanetName(id),point));}
   View.Bodies=list.Count==0?null:list;
  }
  // Where the target was on each date it was imaged. Capture dates carry no clock time, so the position is for 00:00 UTC.
  List<ImagedMark> ImagedMarks(){
   var marks=new List<ImagedMark>();if(overview||record==null||CaptureDates==null||CaptureDates.Count==0)return marks;
   foreach(var day in CaptureDates.Select(d=>d.Date).Distinct().OrderBy(d=>d)){
    var utc=DateTime.SpecifyKind(day,DateTimeKind.Utc);double? dynamical=OrbitalModel.Dynamical(utc);if(!dynamical.HasValue)continue;OrbitPoint point;
    var planet=record.rights.dataset=="planetary-approximation"?PlanetEphemeris.Orbit(record.object_id,utc):null;
    if(OrbitalModel.Position(planet??record,dynamical.Value,out point))marks.Add(new ImagedMark{Utc=utc,Point=point});
   }
   return marks;
  }
  void SetMarker(DateTime? utc){
   MarkerUtc=utc;if(record==null)return;OrbitPoint point=new OrbitPoint();bool available;DateTime shown;
   if(utc.HasValue){
    shown=utc.Value;double? day=OrbitalModel.Dynamical(shown);var planet=record.rights.dataset=="planetary-approximation"?PlanetEphemeris.Orbit(record.object_id,shown):null;
    available=day.HasValue&&OrbitalModel.Position(planet??record,day.Value,out point);ShowDate(DateFormats.Format(shown)+" UTC");
   }else{
    available=OrbitalModel.Position(record,record.epoch,out point,true);shown=OrbitalModel.Utc(record.epoch)??DateTime.UtcNow;ShowDate(Localization.F("Epoch {0}",DateFormats.Format(shown)));
   }
   View.Marker=available&&!overview?(OrbitPoint?)point:null;View.MarkerIsNow=utc.HasValue&&Math.Abs((shown-DateTime.UtcNow).TotalDays)<1.5;Place(shown);
   string text=Localization.T("Approximate orbit · linear distances; body markers enlarged.");
   if(View.Imaged!=null&&View.Imaged.Count>0)text+=" "+Localization.T("Small rings: where it was when you imaged it.");
   caption.Text=available||overview?text:Localization.T("A current position is unavailable for this solution.");
   View.Redraw();
  }
 }
}
