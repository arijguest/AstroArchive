using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  // Uses the 720 × 1280 sidebar fixture with an Orion solution: off by default, one click on, legible labels in and beyond the frame, a plate-solve offer for unsolved frames, and one shared switch.
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
    CheckTargetLabels(preview,stage,"fitted "+label);
    CaptureSidebar(host,Path.Combine(output,"AstroArchive_Target_Overlay_"+label+".png"));
    // Zoomed in, M42's circle runs past the frame: it stays drawn and its label moves to a visible break.
    preview.SmokeZoom(6);PumpPopupLayout();if(preview.TargetCircles==0)throw new Exception("Zoomed overlay lost circles that cross the frame edge");CheckTargetLabels(preview,stage,"zoomed "+label);
    if(label=="Dark100")CaptureSidebar(host,Path.Combine(output,"AstroArchive_Target_Overlay_Zoomed.png"));preview.Fit();PumpPopupLayout();
    if(label=="Dark100"){
     // A wide field (about 3° × 5°), so IC 1396's ring crosses the view rather than enclosing it.
     header.Values["CRVAL1"]="324.2083";header.Values["CRVAL2"]="57.5";header.Values["CDELT1"]="-0.004";header.Values["CDELT2"]="0.004";previewFrame=new Frame{Target="Elephant's Trunk Nebula",Kind="Light",Width=720,Height=1280,Sky=SkyWcs.FromHeader(header,720,1280)};UpdateTargetOverlay();PumpPopupLayout();
     if(preview.TargetCircles<2)throw new Exception("Elephant's Trunk field lacks IC 1396 and its trunk");CheckTargetLabels(preview,stage,"Elephant's Trunk");CaptureSidebar(host,Path.Combine(output,"AstroArchive_Target_Overlay_Elephants_Trunk.png"));
    }
    if(label=="Dark100"){settings.HighContrast=true;ApplyAppearance();PumpPopupLayout();if(preview.TargetCircles==0)throw new Exception("High contrast hid the target overlay");CaptureSidebar(host,Path.Combine(output,"AstroArchive_Target_Overlay_HighContrast.png"));settings.HighContrast=contrast;ApplyAppearance();PumpPopupLayout();}
    previewFrame=new Frame{Target="M42",Kind="Light",Width=720,Height=1280};UpdateTargetOverlay();PumpPopupLayout();
    if(preview.TargetCircles!=0||preview.TargetNoteText==null||!preview.TargetNoteText.Contains(Localization.T("This capture has no plate solution"))||preview.TargetNoteButton==null||!preview.TargetNoteButton.IsVisible)throw new Exception("Unsolved capture lacks a note offering to plate solve");
    // The Edited pane's button changes the same setting, so switching off there clears the repository preview too.
    B("EditedTargetOverlayButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpPopupLayout();if(settings.ShowTargetOverlay||preview.TargetCircles!=0||preview.TargetNoteText!=null||System.Windows.Automation.AutomationProperties.GetItemStatus(B("TargetOverlayButton"))!=Localization.T("Off"))throw new Exception("Target overlay did not switch off everywhere");
   }finally{settings.ShowTargetOverlay=saved;settings.HighContrast=contrast;previewFrame=savedFrame;previewData=savedData;UpdateTargetOverlay();PumpPopupLayout();}
  }
  static void CheckTargetLabels(PreviewViewport preview,Grid stage,string label){
   var labels=preview.TargetLabelBounds.ToList();if(labels.Count==0)throw new Exception("Target overlay has no labels: "+label);
   foreach(var bounds in labels){if(bounds.Left< -0.5||bounds.Top< -0.5||bounds.Right>stage.ActualWidth+0.5||bounds.Bottom>stage.ActualHeight+0.5)throw new Exception("Target label clipped ("+label+"): "+bounds+" in "+stage.RenderSize);if(labels.Count(other=>other.IntersectsWith(bounds))>1)throw new Exception("Target labels overlap ("+label+"): "+bounds);}
  }
 }
}
