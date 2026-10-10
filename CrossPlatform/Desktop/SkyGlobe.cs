using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Globalization;
namespace AstroArchive.Desktop;
public sealed class SkyGlobe : Control
{
    public SkyGlobeCamera Camera { get; } = new();
    public CaptureSky Context { get; private set; } = CaptureSky.Resolve(null,null);
    public bool TargetMarkerDrawn { get; private set; }
    private Point drag;
    private bool dragging;
    public SkyGlobe() { Width=330;Height=330;Focusable=true;ClipToBounds=true;Cursor=new Cursor(StandardCursorType.Hand); }
    public void SetContext(CaptureSky context) {
        Context=context;var target=context.Orientation.Map(SkyVector.Equatorial(context.RA,context.Dec));
        Camera.SetHome(context.HasPosition?Math.Atan2(target.X,target.Y)*180/Math.PI:180,context.HasHorizon&&context.Altitude<0?-25:25);InvalidateVisual();
    }
    public void ResetView() { Camera.Reset();InvalidateVisual(); }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { Focus();drag=e.GetPosition(this);dragging=true;e.Pointer.Capture(this);e.Handled=true; } }
    protected override void OnPointerMoved(PointerEventArgs e) { if(!dragging)return;var point=e.GetPosition(this);Camera.Orbit((drag.X-point.X)*.6,(point.Y-drag.Y)*.6);drag=point;InvalidateVisual();e.Handled=true; }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { dragging=false;e.Pointer.Capture(null);e.Handled=true; }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { dragging=false; }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { Camera.Magnify(Math.Pow(1.2,e.Delta.Y));InvalidateVisual();e.Handled=true; }
    protected override void OnKeyDown(KeyEventArgs e) {
        switch(e.Key) { case Key.Left:Camera.Orbit(10,0);break;case Key.Right:Camera.Orbit(-10,0);break;case Key.Up:Camera.Orbit(0,-10);break;case Key.Down:Camera.Orbit(0,10);break;case Key.Add:case Key.OemPlus:Camera.Magnify(1.2);break;case Key.Subtract:case Key.OemMinus:Camera.Magnify(1/1.2);break;case Key.Home:Camera.Reset();break;default:return; }InvalidateVisual();e.Handled=true;
    }
    public override void Render(DrawingContext dc) {
        base.Render(dc);TargetMarkerDrawn=false;var centre=new Point(Bounds.Width/2,Bounds.Height/2);double radius=(Math.Min(Bounds.Width,Bounds.Height)/2-18)*Camera.Zoom;
        dc.DrawRectangle(Brushes.Transparent,null,new Rect(Bounds.Size));if(radius<=0||Context.BelowHorizon)return;
        double yaw=Camera.Yaw*Math.PI/180,tilt=Camera.Tilt*Math.PI/180;
        var right=new SkyVector(Math.Cos(yaw),-Math.Sin(yaw),0);var up=new SkyVector(-Math.Sin(yaw)*Math.Sin(tilt),-Math.Cos(yaw)*Math.Sin(tilt),Math.Cos(tilt));var front=new SkyVector(Math.Sin(yaw)*Math.Cos(tilt),Math.Cos(yaw)*Math.Cos(tilt),Math.Sin(tilt));
        Point Project(SkyVector v)=>new(centre.X+radius*v.Dot(right),centre.Y-radius*v.Dot(up));
        var visible=new Pen(new SolidColorBrush(Color.Parse("#7D98BD")),1);var faint=new Pen(new SolidColorBrush(Color.Parse("#253348")),.7);
        dc.DrawEllipse(new SolidColorBrush(Color.Parse("#101D30")),visible,centre,radius,radius);
        foreach(double altitude in new[]{-45.0,0,45}) { var prior=SkyVector.Horizontal(0,altitude);for(int az=5;az<=360;az+=5) { var next=SkyVector.Horizontal(az,altitude);dc.DrawLine((prior.Dot(front)+next.Dot(front))/2>=0?visible:faint,Project(prior),Project(next));prior=next; } }
        var stars=SkyFigures.Stars.Select(Context.Orientation.Map).ToArray();
        foreach(var figure in SkyFigures.Figures)foreach(var path in figure.Paths)for(int i=1;i<path.Length;i++) { var a=stars[path[i-1]];var b=stars[path[i]];dc.DrawLine((a.Dot(front)+b.Dot(front))/2>=0?visible:faint,Project(a),Project(b)); }
        foreach(var star in stars.Where(s=>s.Dot(front)>=0))dc.DrawEllipse(Brushes.LightGray,null,Project(star),1,1);
        if(Context.HasHorizon)for(int i=0;i<4;i++) { var vector=SkyVector.Horizontal(i*90,0);if(vector.Dot(front)<0)continue;var point=Project(vector);var label=new FormattedText(new[]{"N","E","S","W"}[i],CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("fonts:Inter#Inter"),12,Brushes.White);dc.DrawText(label,new Point(point.X-6,point.Y-6)); }
        if(Context.HasPosition) { var vector=Context.Orientation.Map(SkyVector.Equatorial(Context.RA,Context.Dec));if(vector.Dot(front)>=0) { dc.DrawEllipse(null,new Pen(Context.ApproximatePosition?Brushes.Orange:Brushes.DeepSkyBlue,2),Project(vector),7,7);TargetMarkerDrawn=true; } }
    }
}
