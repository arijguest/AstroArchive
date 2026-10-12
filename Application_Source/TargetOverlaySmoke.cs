using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  // Uses the 720 × 1280 sidebar fixture with an Orion solution: off by default, one click on, legible labels, and a clear note for unsolved frames.
  void SmokeTargetOverlay(PreviewViewport preview,string output,string label){
   var stage=(Grid)Window.FindName("PreviewStage");var host=(Grid)Window.FindName("PreviewHost");
   bool saved=settings.ShowTargetOverlay,contrast=settings.HighContrast;var savedFrame=previewFrame;var savedData=previewData;
   try{
    var header=new FitsHeader();double scale=0.0012;
    header.Values["CTYPE1"]="RA---TAN";header.Values["CTYPE2"]="DEC--TAN";header.Values["CRVAL1"]="83.82208";header.Values["CRVAL2"]="-5.39111";header.Values["CRPIX1"]="360.5";header.Values["CRPIX2"]="640.5";
    header.Values["CDELT1"]=(-scale).ToString("R",CultureInfo.InvariantCulture);header.Values["CDELT2"]=scale.ToString("R",CultureInfo.InvariantCulture);
    previewFrame=new Frame{Target="M42",Kind="Light",Width=720,Height=1280,Sky=SkyWcs.FromHeader(header,720,1280)};previewData=new PreviewData{Width=720,Height=1280,SourceWidth=720,SourceHeight=1280,FlipY=true};
    settings.ShowTargetOverlay=false;UpdateTargetOverlay();PumpPopupLayout();if(preview.TargetCircles!=0)throw new Exception("Target overlay drawn while switched off");
    B("TargetOverlayButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();
    if(!settings.ShowTargetOverlay||preview.TargetCircles==0)throw new Exception("Target overlay button did not mark the solved field");
    if(string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(B("TargetOverlayButton")))||System.Windows.Automation.AutomationProperties.GetItemStatus(B("TargetOverlayButton"))!=Localization.T("On"))throw new Exception("Target overlay button does not report its state");
    var labels=preview.TargetLabelBounds.ToList();if(labels.Count==0)throw new Exception("Target overlay has no labels");
    foreach(var bounds in labels){if(bounds.Left< -0.5||bounds.Top< -0.5||bounds.Right>stage.ActualWidth+0.5||bounds.Bottom>stage.ActualHeight+0.5)throw new Exception("Target label clipped: "+bounds+" in "+stage.RenderSize);if(labels.Any(other=>other!=bounds&&other.IntersectsWith(bounds)))throw new Exception("Target labels overlap: "+bounds);}
    CaptureSidebar(host,Path.Combine(output,"AstroArchive_Target_Overlay_"+label+".png"));
    if(label=="Dark100"){settings.HighContrast=true;ApplyAppearance();PumpPopupLayout();if(preview.TargetCircles==0)throw new Exception("High contrast hid the target overlay");CaptureSidebar(host,Path.Combine(output,"AstroArchive_Target_Overlay_HighContrast.png"));settings.HighContrast=contrast;ApplyAppearance();PumpPopupLayout();}
    previewFrame=new Frame{Target="M42",Kind="Light",Width=720,Height=1280};UpdateTargetOverlay();PumpPopupLayout();
    if(preview.TargetCircles!=0||preview.TargetNoteText==null||!preview.TargetNoteText.Contains(Localization.T("Plate solve this capture to mark targets in the frame")))throw new Exception("Unsolved capture lacks a gentle overlay note");
    B("TargetOverlayButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();if(settings.ShowTargetOverlay||preview.TargetCircles!=0||preview.TargetNoteText!=null)throw new Exception("Target overlay did not switch off");
   }finally{settings.ShowTargetOverlay=saved;settings.HighContrast=contrast;previewFrame=savedFrame;previewData=savedData;UpdateTargetOverlay();PumpPopupLayout();}
  }
 }
}
