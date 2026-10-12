// Exercise the Export window: options, contextual fields, shared destination, AstroBin preview and grouped calibration choices.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  static void SelectExportCategory(ExportWindowFields fields,ExportCategory category){int index=fields.Pages.FindIndex(p=>p.Category==category);if(index<0)throw new Exception("Export option is missing: "+category);fields.Dialog.SelectTab(index);PumpPopupLayout();if(fields.Page.Category!=category)throw new Exception("Export option did not open: "+category);}
  void SmokeExportOptions(string output){
   SmokeGroupedCalibrationOptions(output);var filterIds=settings.AstroBinFilterIds;settings.AstroBinFilterIds=null;
   var stack=new Frame{Hash="stack",Kind="Stack",OriginalName="Stack.fit",Format="FITS",Target="M45",Telescope="Rig",Camera="Primary",Calibration="Device stack"};
   var light=new Frame{Hash="light",Kind="Light",OriginalName="Light_M45.fit",Format="FITS",Target="M45",Telescope="Rig",Camera="Primary",Exposure=60,Gain=80,Temperature=-10,BinX=1,BinY=1,Filter="Ha",AcquisitionDate="2026-10-06",AcquisitionDateSource="FITS UTC",Calibration="Calibrated"};
   try{
    foreach(string mode in new[]{"Dark","Light"}){
     Theme.Apply(Window,mode);ExportWindowFields fields;var dialog=ExportWindow(new ExportScope{Files=new List<Frame>{stack,light},Selected=true},ExportCategory.Files,out fields);
     try{
      dialog.Window.Show();PumpPopupLayout();VerifyWindowIcon(dialog.Window);
      if(fields.Pages.Count!=5||fields.Page.Category!=ExportCategory.Files||Convert.ToString(fields.Primary.Content)!="Export files"||!fields.Destination.IsVisible)throw new Exception("Export did not open on the requested option with its destination.");
      if(!PopupChildren<TextBlock>(dialog.Window).Any(t=>t.IsVisible&&t.Text.StartsWith("2 selected files · "+light.TargetLabel))||!PopupChildren<TextBlock>(dialog.Window).Any(t=>t.IsVisible&&t.Text=="1 sub · 1 stack"))throw new Exception("Export header omits the selected file count, target or contents.");
      if(fields.FolderName.IsVisible||fields.NewFolder.IsChecked==true||fields.FilesMetadata.IsChecked==true)throw new Exception("Export extras are enabled by default.");
      if(fields.Parent.Text!=(settings.ExportWorkingDirectory??"")||Convert.ToString(fields.FilesAfter.SelectedItem)!="None"||!fields.FilesAfter.IsEnabled||fields.FilesAfter.Items.Count!=2)throw new Exception("Export working directory or Siril choices are incorrect.");
      fields.NewFolder.IsChecked=true;PumpPopupLayout();if(!fields.FolderName.IsVisible||!fields.FolderName.IsEnabled||!fields.FolderName.Text.StartsWith("AstroArchive_export_"))throw new Exception("New folder name did not appear.");fields.NewFolder.IsChecked=false;PumpPopupLayout();if(fields.FolderName.IsVisible)throw new Exception("New folder name stayed visible.");
      CheckExportLabels(dialog,mode+" files");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Export_"+mode+".png"));
      SelectExportCategory(fields,ExportCategory.Stacking);
      if(Convert.ToString(fields.Primary.Content)!="Export stacking folder"||!fields.Destination.IsVisible||fields.StackAstroBin.IsChecked!=true||Convert.ToString(fields.StackInputs.SelectedItem)!="Both")throw new Exception("Stacking folder defaults are wrong; the AstroBin CSV must be offered by default.");
      fields.StackAfter.SelectedItem="Siril";fields.StackInputs.SelectedItem="Subs";if(fields.StackAfter.IsEnabled||Convert.ToString(fields.StackAfter.SelectedItem)!="None")throw new Exception("Ineligible export retained a Siril launch.");fields.StackInputs.SelectedItem="Both";
      CheckExportLabels(dialog,mode+" stacking");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Export_Stacking_"+mode+".png"));
      SelectExportCategory(fields,ExportCategory.Application);
      if(fields.Primary.IsEnabled||fields.Destination.IsVisible||!fields.Problem.IsVisible||fields.Problem.Text.Length==0)throw new Exception("Mixed subs and stacks did not explain why they cannot open in an app.");
      SelectExportCategory(fields,ExportCategory.AstroBin);
      if(!fields.Primary.IsEnabled||Convert.ToString(fields.Primary.Content)!="Save CSV…"||fields.Destination.IsVisible)throw new Exception("AstroBin CSV is not available without an export folder.");
      if(fields.AstroPreview.Text!="date,number,duration,binning,gain,sensorCooling\r\n2026-10-06,1,60,1,80,-10\r\n")throw new Exception("AstroBin preview is wrong: "+fields.AstroPreview.Text);
      if(!fields.AstroNotes.Text.Contains("No AstroBin filter ID for Ha")||!fields.AstroNotes.Text.Contains("without a recorded sub count")||fields.AstroSheet.IsVisible)throw new Exception("AstroBin notes do not flag missing values.");
      CheckExportLabels(dialog,mode+" AstroBin");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Export_AstroBin_"+mode+".png"));
      SelectExportCategory(fields,ExportCategory.Catalogue);
      if(Convert.ToString(fields.Primary.Content)!="Save CSV…"||fields.Destination.IsVisible||fields.CatalogueScope.Items.Count!=2||fields.CatalogueScope.SelectedIndex!=0)throw new Exception("Catalogue CSV choices are wrong.");
      SelectExportCategory(fields,ExportCategory.Files);if(!fields.Destination.IsVisible)throw new Exception("Returning to Files hid the destination.");
     }finally{dialog.Window.Close();}
    }
   }finally{settings.AstroBinFilterIds=filterIds;Theme.Apply(Window,"Dark");}
   File.WriteAllText(Path.Combine(output,"export-ui-smoke.txt"),"PASS: one Export window opens on the requested option; header counts files and targets; destination shows only for folder exports; new-folder and metadata extras default off; Siril eligibility; AstroBin CSV offered by default with stacking folders; app handoff explains unsupported selections; AstroBin preview and notes without exporting images; catalogue scopes; light/dark labels.");
  }
  void CheckExportLabels(FormWindow dialog,string label){foreach(var text in PopupChildren<TextBlock>(dialog.Window))if(text.IsVisible&&!string.IsNullOrWhiteSpace(text.Text))Readable(text.Foreground,dialog.Window.Background,label+" export label");}
  void SmokeGroupedCalibrationOptions(string output){
   WaitForSearches();var previousRepo=repo;var previousRows=all;var previousGroups=subframeSessions;var previousSelection=G("FramesGrid").SelectedItems.Cast<object>().ToList();var previousLibrarySelection=librarySelection.Items;string theme=settings.ThemeMode;int scale=settings.TextScalePercent;string root=Path.Combine(output,"grouped-calibration-dialog-fixture");Repository temporary=null;
   try{
    temporary=new Repository(root);repo=temporary;var light=new Frame{Hash="light-a",RelativePath="light-a.fit",OriginalName="Light_a.fit",Kind="Light",Target="M45",Session="same-session",Night="2026-10-06",Telescope="Rig",Camera="Primary",BinX=1,BinY=1,Width=64,Height=48,Channels=1,Exposure=60,Gain=80,Temperature=10,Filter="L",Calibration="Unknown"};var second=light.Clone();second.Hash="light-b";second.RelativePath="light-b.fit";second.OriginalName="Light_b.fit";var dark=light.Clone();dark.Hash="dark";dark.RelativePath="dark.fit";dark.OriginalName="Dark.fit";dark.Kind="Dark";dark.Calibration="Calibration frame";all=new List<Frame>{light,second,dark};foreach(var frame in all){File.WriteAllText(repo.FilePath(frame),"dialog fixture");repo.Save(frame);}
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();changingSessionSelection=true;try{G("FramesGrid").SelectedItems.Clear();librarySelection.Clear();subframeSessions=SubframeSessions.Build(all);subframeSessions.Single().IsSelected=true;}finally{changingSessionSelection=false;}CaptureTargetSelection("FramesGrid");var selected=SelectedFiles();if(selected.Count!=2||selected.Any(f=>f.Kind!="Light"))throw new Exception("Group selection did not expand to its actual subs.");
     ExportWindowFields fields;var dialog=ExportWindow(new ExportScope{Files=selected,Selected=true},ExportCategory.Stacking,out fields);
     try{
      dialog.Window.Show();PumpPopupLayout();var calibration=fields.StackCalibration;var unknown=fields.StackUnknown;
      if(!unknown.IsVisible||unknown.IsEnabled||unknown.IsChecked==true||PopupChildren<Expander>(dialog.Window).Any(e=>e.IsExpanded))throw new Exception("Unknown-state confirmation is hidden or enabled without opting in.");
      calibration.IsChecked=true;if(!unknown.IsEnabled||!fields.StackAvailability.Text.Contains("2 subs have unknown processing state"))throw new Exception("Grouped export does not explain why unknown-state subs omit calibration.");
      unknown.IsChecked=true;PumpPopupLayout();if(!fields.StackAvailability.Text.StartsWith("1 matching calibration file."))throw new Exception("Confirming raw group did not find its matching dark.");
      calibration.IsChecked=false;if(unknown.IsEnabled||fields.StackAvailability.Text!="Calibration files omitted.")throw new Exception("Calibration toggle ignored after confirmation.");
      SelectExportCategory(fields,ExportCategory.AstroBin);if(fields.AstroUnknown.IsVisible==false||fields.AstroPreview.Text.Contains("darks"))throw new Exception("AstroBin counted calibrations for unconfirmed unknown-state subs.");
      fields.AstroUnknown.IsChecked=true;PumpPopupLayout();if(!fields.AstroPreview.Text.StartsWith("number,duration,binning,gain,sensorCooling,darks")||!fields.AstroPreview.Text.Contains(",1\r\n"))throw new Exception("AstroBin did not count the confirmed matching dark: "+fields.AstroPreview.Text);
      SelectExportCategory(fields,ExportCategory.Stacking);CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_GroupedCalibration_"+mode+textScale+".png"));
     }finally{dialog.Window.Close();}
    }
   }finally{repo=previousRepo;all=previousRows;subframeSessions=previousGroups;changingSessionSelection=true;try{G("FramesGrid").SelectedItems.Clear();foreach(var item in previousSelection)G("FramesGrid").SelectedItems.Add(item);}finally{changingSessionSelection=false;}librarySelection.Clear();foreach(var frame in previousLibrarySelection)librarySelection.Add(frame);UpdateSelectionCounters();settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();if(temporary!=null)temporary.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
   File.WriteAllText(Path.Combine(output,"grouped-calibration-export-smoke.txt"),"PASS: group selection expands actual subs; unknown-state confirmation stays visible outside advanced options; calibration count updates with confirmation/toggle; AstroBin counts calibrations only after confirmation; closing exports nothing in both themes and text sizes.");
  }
 }
}
