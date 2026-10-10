using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeEditedPreviewLayout(string output){
   var body=(Grid)Window.FindName("EditedBody");var host=(Grid)Window.FindName("EditedPreviewHost");var stage=(Grid)Window.FindName("EditedPreviewStage");var header=(Grid)Window.FindName("EditedPreviewHeader");var pane=(Border)Window.FindName("EditedPreviewPane");var column=body.ColumnDefinitions[3];
   double width=Window.Width,height=Window.Height;int scale=settings.TextScalePercent,page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string theme=settings.ThemeMode;var manual=editedPreviewManualWidth;var original=((Image)Window.FindName("EditedPreviewImage")).Source as BitmapSource;var message=L("EditedPreviewMessage").Visibility;int cases=0;
   var colours=new[]{new byte[]{220,40,40},new byte[]{40,220,40},new byte[]{40,40,220},new byte[]{220,220,40}};var rgb=new byte[720*960*3];
   for(int y=0;y<960;y++)for(int x=0;x<720;x++)Array.Copy(colours[(y<480?0:2)+(x<360?0:1)],0,rgb,(y*720+x)*3,3);
   var bitmap=BitmapSource.Create(720,960,96,96,PixelFormats.Rgb24,null,rgb,720*3);bitmap.Freeze();
   try{
    CancelEditedPreview();GoToPage(2);CancelAutomaticEditedRefresh();editedPreviewManualWidth=null;L("EditedPreviewMessage").Visibility=Visibility.Collapsed;
    foreach(string removed in new[]{"EditedPreviewSkyPanel","EditedPreviewSky","EditedPreviewSkyResetButton","EditedStretchMode","EditedPreviewDetailsButton","EditedPreviewDetailsPopup"})if(Window.FindName(removed)!=null)throw new Exception("Edited preview retains removed control: "+removed);
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150})foreach(var size in new[]{new[]{1180.0,850},new[]{1060.0,650},new[]{1180.0,1000}}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();Window.Width=size[0];Window.Height=size[1];editedPreviewViewport.SetImage(bitmap,true);PumpPopupLayout();SizeEditedPreview();PumpPopupLayout();editedPreviewViewport.Resize();PumpPopupLayout();
     double availableHeight=Math.Max(0,host.ActualHeight-PreviewViewport.ToolbarSpace),chrome=pane.Padding.Left+pane.Padding.Right+pane.BorderThickness.Left+pane.BorderThickness.Right;
     double availableWidth=body.ActualWidth-body.ColumnDefinitions[0].ActualWidth-body.ColumnDefinitions[2].ActualWidth-body.ColumnDefinitions[1].MinWidth;
     if(availableHeight*0.75+chrome<=availableWidth&&Math.Abs(stage.ActualHeight-availableHeight)>2)throw new Exception("3:4 Edited portrait is width-limited: "+stage.RenderSize+", available height "+availableHeight+", column "+column.ActualWidth);
     if(Math.Abs(stage.ActualWidth/stage.ActualHeight-0.75)>0.002||body.ColumnDefinitions[1].ActualWidth<body.ColumnDefinitions[1].MinWidth-1||pane.TranslatePoint(new Point(pane.ActualWidth,0),body).X>body.ActualWidth+1)throw new Exception("Edited preview distorted the image or overflowed the table/window: "+mode+textScale+", requested "+size[0]+"x"+size[1]+", body "+body.RenderSize+", stage "+stage.RenderSize+", table "+body.ColumnDefinitions[1].ActualWidth+", pane right "+pane.TranslatePoint(new Point(pane.ActualWidth,0),body).X);
     var enlarge=B("EditedOpenPreviewButton");var buttonBounds=enlarge.TransformToAncestor(header).TransformBounds(new Rect(enlarge.RenderSize));if(buttonBounds.Right>header.ActualWidth+1||buttonBounds.Left<0||header.ActualHeight>enlarge.ActualHeight+1)throw new Exception("Edited filename/enlarge header clips or adds an extra row");
     var imageArea=stage.TransformToAncestor(host).TransformBounds(new Rect(stage.RenderSize));var capture=CaptureSidebar(pane,Path.Combine(output,"AstroArchive_Edited_Preview_"+mode+textScale+"_"+size[0]+"x"+size[1]+".png"));var hostOrigin=host.TranslatePoint(new Point(),pane);var pixels=new byte[capture.PixelWidth*capture.PixelHeight*4];capture.CopyPixels(pixels,capture.PixelWidth*4,0);
     for(int quadrant=0;quadrant<4;quadrant++){int x=(int)(hostOrigin.X+imageArea.X+imageArea.Width*(quadrant%2==0?0.2:0.8)),y=(int)(hostOrigin.Y+imageArea.Y+imageArea.Height*(quadrant<2?0.2:0.8)),offset=(y*capture.PixelWidth+x)*4;var expected=colours[quadrant];if(Math.Abs(pixels[offset+2]-expected[0])>12||Math.Abs(pixels[offset+1]-expected[1])>12||Math.Abs(pixels[offset]-expected[2])>12||pixels[offset+3]<250)throw new Exception("Edited portrait pixels are clipped or blank: quadrant "+quadrant);}
     editedPreviewViewport.SmokeGestures();double fittedWidth=column.ActualWidth;editedPreviewViewport.BeginLoading();PumpPopupLayout();if(Math.Abs(column.ActualWidth-fittedWidth)>1||stage.RenderSize.Width<1||((Image)Window.FindName("EditedPreviewImage")).Source!=null)throw new Exception("Edited loading changed its panel or retained old pixels");cases++;
    }
    settings.TextScalePercent=100;ApplyAppearance();Window.Width=1180;Window.Height=850;editedPreviewViewport.SetImage(bitmap,true);PumpPopupLayout();SizeEditedPreview();PumpPopupLayout();
    var divider=(GridSplitter)Window.FindName("EditedPreviewDivider");double before=column.ActualWidth;
    divider.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});divider.RaiseEvent(new DragDeltaEventArgs(-40,0){RoutedEvent=Thumb.DragDeltaEvent});PumpPopupLayout();divider.RaiseEvent(new DragCompletedEventArgs(-40,0,false){RoutedEvent=Thumb.DragCompletedEvent});PumpPopupLayout();
    if(column.ActualWidth<=before+20||!editedPreviewManualWidth.HasValue)throw new Exception("Edited divider cannot widen the preview manually");double chosen=column.ActualWidth;Window.Height=700;PumpPopupLayout();if(Math.Abs(column.ActualWidth-chosen)>1)throw new Exception("Window resize discarded the manually chosen preview width");
    File.WriteAllText(Path.Combine(output,"edited-preview-layout-smoke.txt"),"PASS "+cases+" native Edited layouts: no sky/stretch/details controls; full-height 3:4 portrait fit; all four rendered image quadrants; compact filename/enlarge header; below-image zoom/pan/Fit; light/dark and 100/150% text; small/tall windows; stable loading frame; adjustable divider with retained manual width.");
   }finally{editedPreviewDragging=false;editedPreviewManualWidth=manual;settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();Window.Width=width;Window.Height=height;editedPreviewViewport.SetImage(original,true);L("EditedPreviewMessage").Visibility=message;GoToPage(page);PumpPopupLayout();SizeEditedPreview();}
  }
 }
}
