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
  readonly TextBlock caption;
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
   if(record==null){View.SetScene(null);toolbar.Visibility=Visibility.Collapsed;caption.Text=Localization.T("Complete orbital elements are unavailable. No orientation has been assumed.");return;}
   toolbar.Visibility=Visibility.Visible;caption.Text=Localization.T("Preparing 3D orbit…");Sync();work=new CancellationTokenSource();var token=work.Token;
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
    available=day.HasValue&&OrbitalModel.Position(planet??record,day.Value,out point);View.DateLabel=shown.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+" UTC";
   }else{
    available=OrbitalModel.Position(record,record.epoch,out point,true);shown=OrbitalModel.Utc(record.epoch)??DateTime.UtcNow;View.DateLabel=Localization.F("Epoch {0}",shown.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
   }
   View.Marker=available&&!overview?(OrbitPoint?)point:null;View.MarkerIsNow=utc.HasValue&&Math.Abs((shown-DateTime.UtcNow).TotalDays)<1.5;Place(shown);
   string text=Localization.T("Approximate orbit · linear distances; body markers enlarged.");
   if(View.Imaged!=null&&View.Imaged.Count>0)text+=" "+Localization.T("Small rings: where it was when you imaged it. Click one for its date.");
   caption.Text=available||overview?text:Localization.T("A current position is unavailable for this solution.");
   View.Redraw();
  }
 }
}
