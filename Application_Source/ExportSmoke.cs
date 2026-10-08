// Exercise the controls used by both original-file and stacking export dialogs.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeExportOptions(string output){
   SmokeGroupedCalibrationOptions(output);
   foreach(string mode in new[]{"Dark","Light"}){
    Theme.Apply(Window,mode);var dialog=new FormWindow(Window,"Export files",610,560);dialog.Text("1 selected file",true);dialog.Text("Stacks copy directly to the destination; subs retain compatible input folders.");var fields=ExportDestination(dialog,"Test export");var stack=new Frame{Kind="Stack",OriginalName="Stack.fit",Format="FITS"};var launchOptions=new ExportOptions{Mode="Files"};ExportAfterChoice(dialog,fields,new System.Collections.Generic.List<Frame>{stack},()=>launchOptions);var advanced=dialog.Advanced("More options",()=>dialog.Add(fields.Metadata));dialog.CloseOnly();
    try{dialog.Window.Show();PumpPopupLayout();var options=fields.Options();if(advanced.IsExpanded||fields.Name.IsVisible)throw new Exception("Optional export fields are visible by default.");if(options.AddMetadata||options.CreateNewFolder||fields.Name.IsEnabled)throw new Exception("Export extras are enabled by default.");
     if(fields.Parent.Text!=(settings.ExportWorkingDirectory??"")||Convert.ToString(fields.AfterExport.SelectedItem)!="None"||!fields.AfterExport.IsEnabled||fields.AfterExport.Items.Count!=2)throw new Exception("Export working directory or Siril choices are incorrect.");fields.AfterExport.SelectedItem="Siril";launchOptions.Mode="Subs";fields.RefreshAfterExport();if(fields.AfterExport.IsEnabled||Convert.ToString(fields.AfterExport.SelectedItem)!="None")throw new Exception("Ineligible export retained a Siril launch.");launchOptions.Mode="Files";fields.RefreshAfterExport();
     fields.NewFolder.IsChecked=true;fields.Metadata.IsChecked=true;if(!fields.Name.IsEnabled||!fields.Options().CreateNewFolder||!fields.Options().AddMetadata)throw new Exception("Optional export controls did not update.");fields.NewFolder.IsChecked=false;if(fields.Name.IsEnabled||fields.Options().CreateNewFolder||!fields.Options().AddMetadata)throw new Exception("Export options are not independent.");fields.Metadata.IsChecked=false;
     foreach(var label in PopupChildren<TextBlock>(dialog.Window))if(!string.IsNullOrWhiteSpace(label.Text))Readable(label.Foreground,dialog.Window.Background,mode+" export label");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Export_"+mode+".png"));
    }finally{dialog.Window.Close();}
   }
   Theme.Apply(Window,"Dark");File.WriteAllText(Path.Combine(output,"export-ui-smoke.txt"),"PASS: metadata and new-folder export options default off, independent toggles, folder name state, default export directory, None/Siril after-export choices, eligibility updates and light/dark labels.");
  }
  void SmokeGroupedCalibrationOptions(string output){
   var previousRepo=repo;var previousRows=all;var previousGroups=subframeSessions;var previousSelection=G("FramesGrid").SelectedItems.Cast<object>().ToList();string theme=settings.ThemeMode;int scale=settings.TextScalePercent;string root=Path.Combine(output,"grouped-calibration-dialog-fixture");Repository temporary=null;
   try{
    temporary=new Repository(root);repo=temporary;var light=new Frame{Hash="light-a",RelativePath="light-a.fit",OriginalName="Light_a.fit",Kind="Light",Target="M45",Session="same-session",Night="2026-10-06",Telescope="Rig",Camera="Primary",BinX=1,BinY=1,Width=64,Height=48,Channels=1,Exposure=60,Gain=80,Temperature=10,Filter="L",Calibration="Unknown"};var second=light.Clone();second.Hash="light-b";second.RelativePath="light-b.fit";second.OriginalName="Light_b.fit";var dark=light.Clone();dark.Hash="dark";dark.RelativePath="dark.fit";dark.OriginalName="Dark.fit";dark.Kind="Dark";dark.Calibration="Calibration frame";all=new List<Frame>{light,second,dark};foreach(var frame in all){File.WriteAllText(repo.FilePath(frame),"dialog fixture");repo.Save(frame);}
    foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();changingSessionSelection=true;try{G("FramesGrid").SelectedItems.Clear();subframeSessions=SubframeSessions.Build(all);subframeSessions.Single().IsSelected=true;}finally{changingSessionSelection=false;}var selected=SelectedFiles();if(selected.Count!=2||selected.Any(f=>f.Kind!="Light"))throw new Exception("Group selection did not expand to its actual subs.");
     Exception failure=null;bool checkedDialog=false;Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{
      Window dialog=null;try{dialog=Window.OwnedWindows.Cast<Window>().Single(w=>w.Title=="Export stacking folder");PumpPopupLayout();var calibration=PopupChildren<CheckBox>(dialog).Single(c=>Convert.ToString(c.Content)=="Include matching calibrations");var unknown=PopupChildren<CheckBox>(dialog).Single(c=>Convert.ToString(c.Content)=="Confirm unknown-state subs are uncalibrated");if(!unknown.IsVisible||unknown.IsEnabled||unknown.IsChecked==true||PopupChildren<Expander>(dialog).Any(e=>e.IsExpanded))throw new Exception("Unknown-state confirmation is hidden or enabled without opting in.");calibration.IsChecked=true;if(!unknown.IsEnabled||!PopupChildren<TextBlock>(dialog).Any(t=>t.Text.Contains("2 subs have unknown processing state")))throw new Exception("Grouped export does not explain why unknown-state subs omit calibration.");unknown.IsChecked=true;PumpPopupLayout();if(!PopupChildren<TextBlock>(dialog).Any(t=>t.Text.StartsWith("1 matching calibration file.")))throw new Exception("Confirming raw group did not find its matching dark.");calibration.IsChecked=false;if(unknown.IsEnabled||!PopupChildren<TextBlock>(dialog).Any(t=>t.Text=="Calibration files omitted."))throw new Exception("Calibration toggle ignored after confirmation.");CapturePopup(dialog,Path.Combine(output,"AstroArchive_GroupedCalibration_"+mode+textScale+".png"));checkedDialog=true;}catch(Exception error){failure=error;}finally{if(dialog!=null)dialog.DialogResult=false;}
     }));ExportProject(selected,false);if(failure!=null)throw failure;if(!checkedDialog)throw new Exception("Grouped calibration export dialogue did not open.");
    }
   }finally{repo=previousRepo;all=previousRows;subframeSessions=previousGroups;changingSessionSelection=true;try{G("FramesGrid").SelectedItems.Clear();foreach(var item in previousSelection)G("FramesGrid").SelectedItems.Add(item);}finally{changingSessionSelection=false;}settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();if(temporary!=null)temporary.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
   File.WriteAllText(Path.Combine(output,"grouped-calibration-export-smoke.txt"),"PASS: group selection expands actual subs; unknown-state confirmation stays visible outside advanced options; calibration count updates with confirmation/toggle; cancellation exports nothing in both themes and text sizes.");
  }
 }
}
