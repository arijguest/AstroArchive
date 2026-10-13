using System;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  double? editedPreviewManualWidth;bool editedPreviewDragging;double editedPreviewAspect=0.75;
  // Automatic sizing never squeezes the table below this; the divider can still go to the column minimum.
  const double EditedTableComfortWidth=440,EditedPreviewMinimumWidth=240;
  void InitializeEditedPreviewLayout(){
   var body=(Grid)Window.FindName("EditedBody");var host=(Grid)Window.FindName("EditedPreviewHost");var divider=(GridSplitter)Window.FindName("EditedPreviewDivider");
   body.SizeChanged+=(s,e)=>SizeEditedPreview();host.SizeChanged+=(s,e)=>{if(e.HeightChanged)SizeEditedPreview();};
   divider.DragStarted+=(s,e)=>editedPreviewDragging=true;
   divider.DragCompleted+=(s,e)=>{editedPreviewDragging=false;editedPreviewManualWidth=body.ColumnDefinitions[3].ActualWidth;SizeEditedPreview();};
   editedPreviewViewport.FrameChanged+=SizeEditedPreview;
  }
  // Room the preview column may take automatically, leaving the table its comfortable width.
  double EditedPreviewAvailableWidth(){
   var body=(Grid)Window.FindName("EditedBody");double room=body.ActualWidth-body.ColumnDefinitions[0].ActualWidth-body.ColumnDefinitions[2].ActualWidth;
   double table=Math.Max(body.ColumnDefinitions[1].MinWidth,EditedTableComfortWidth);
   return Math.Max(0,Math.Max(room-table,Math.Min(EditedPreviewMinimumWidth,room-body.ColumnDefinitions[1].MinWidth)));
  }
  void SizeEditedPreview(){
   var body=(Grid)Window.FindName("EditedBody");var host=(Grid)Window.FindName("EditedPreviewHost");if(editedPreviewDragging||body.ActualWidth<=0||host.ActualHeight<=0)return;
   // Target information sizes this column while it is shown; two owners made it jump.
   if(((FrameworkElement)Window.FindName("EditedTargetInfoPane")).Visibility==Visibility.Visible)return;
   var pane=(Border)Window.FindName("EditedPreviewPane");double chrome=pane.Padding.Left+pane.Padding.Right+pane.BorderThickness.Left+pane.BorderThickness.Right;
   // Fit the column to the image being shown at full height. Between images
   // (loading or none selected) keep the last shape so the table does not jump.
   var aspect=editedPreviewViewport==null?null:editedPreviewViewport.FrameAspect;if(aspect.HasValue&&aspect.Value>0)editedPreviewAspect=aspect.Value;
   double ideal=Math.Max(EditedPreviewMinimumWidth,Math.Max(0,host.ActualHeight-PreviewViewport.ToolbarSpace)*editedPreviewAspect+chrome);
   double available=editedPreviewManualWidth.HasValue?Math.Max(0,body.ActualWidth-body.ColumnDefinitions[0].ActualWidth-body.ColumnDefinitions[2].ActualWidth-body.ColumnDefinitions[1].MinWidth):EditedPreviewAvailableWidth();
   double width=Math.Min(available,editedPreviewManualWidth??ideal);var column=body.ColumnDefinitions[3];
   if(Math.Abs(column.Width.Value-width)>0.5||!column.Width.IsAbsolute)column.Width=new GridLength(width);
  }
 }
}
