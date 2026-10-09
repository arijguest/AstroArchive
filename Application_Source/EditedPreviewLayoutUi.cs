using System;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  double? editedPreviewManualWidth;bool editedPreviewDragging;
  void InitializeEditedPreviewLayout(){
   var body=(Grid)Window.FindName("EditedBody");var host=(Grid)Window.FindName("EditedPreviewHost");var divider=(GridSplitter)Window.FindName("EditedPreviewDivider");
   body.SizeChanged+=(s,e)=>SizeEditedPreview();host.SizeChanged+=(s,e)=>{if(e.HeightChanged)SizeEditedPreview();};
   divider.DragStarted+=(s,e)=>editedPreviewDragging=true;
   divider.DragCompleted+=(s,e)=>{editedPreviewDragging=false;editedPreviewManualWidth=body.ColumnDefinitions[3].ActualWidth;SizeEditedPreview();};
  }
  void SizeEditedPreview(){
   var body=(Grid)Window.FindName("EditedBody");var host=(Grid)Window.FindName("EditedPreviewHost");if(editedPreviewDragging||body.ActualWidth<=0||host.ActualHeight<=0)return;
   var pane=(Border)Window.FindName("EditedPreviewPane");double chrome=pane.Padding.Left+pane.Padding.Right+pane.BorderThickness.Left+pane.BorderThickness.Right;
   // Allocate enough width for a full-height 3:4 portrait, leaving the table
   // the space it can actually use. File selection never changes the column.
   double ideal=Math.Max(240,Math.Max(0,host.ActualHeight-PreviewViewport.ToolbarSpace)*0.75+chrome);
   double available=Math.Max(0,body.ActualWidth-body.ColumnDefinitions[0].ActualWidth-body.ColumnDefinitions[2].ActualWidth-body.ColumnDefinitions[1].MinWidth);
   double width=Math.Min(available,editedPreviewManualWidth??ideal);var column=body.ColumnDefinitions[3];
   if(Math.Abs(column.Width.Value-width)>0.5||!column.Width.IsAbsolute)column.Width=new GridLength(width);
  }
 }
}
