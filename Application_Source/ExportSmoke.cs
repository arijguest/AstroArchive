// Exercise the controls used by both original-file and stacking export dialogs.
using System;
using System.IO;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeExportOptions(string output){
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
 }
}
