using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace AstroArchive {
 public partial class MainUi {
  Frame targetOverlayFrame;bool targetOverlayFlip;TargetOverlayResult targetOverlay;
  void InitializeTargetOverlay(){
   B("TargetOverlayButton").Click+=(s,e)=>{settings.ShowTargetOverlay=!settings.ShowTargetOverlay;SaveSettings();UpdateTargetOverlay();};
   UpdateTargetOverlay();
  }
  // Off by default. The button keeps its place in every state; the overlay itself explains frames it cannot mark.
  void UpdateTargetOverlay(){
   var button=B("TargetOverlayButton");if(button==null||previewViewport==null)return;bool on=settings.ShowTargetOverlay;
   string tip=Localization.T(TargetOverlay.Tooltip(on));button.ToolTip=tip;AutomationProperties.SetName(button,Localization.T("Show targets in frame"));AutomationProperties.SetItemStatus(button,Localization.T(on?"On":"Off"));
   var icon=(Path)Window.FindName("TargetOverlayIcon");if(icon!=null)icon.SetResourceReference(Shape.StrokeProperty,on?"Accent":"Text");
   if(on)button.SetResourceReference(Control.BackgroundProperty,"Selection");else button.ClearValue(Control.BackgroundProperty);
   if(!on||previewFrame==null||previewData==null){previewViewport.SetTargets(null,null);return;}
   if(targetOverlay==null||!ReferenceEquals(targetOverlayFrame,previewFrame)||targetOverlayFlip!=previewData.FlipY){targetOverlayFrame=previewFrame;targetOverlayFlip=previewData.FlipY;targetOverlay=TargetOverlay.For(previewFrame,previewData.FlipY);}
   previewViewport.SetTargets(targetOverlay.Marks,targetOverlay.Note);
  }
 }
}
