// Exercise the controls used by both original-file and stacking export dialogs.
using System;
using System.IO;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeExportOptions(string output){
   foreach(string mode in new[]{"Dark","Light"}){
    Theme.Apply(Window,mode);var dialog=new FormWindow(Window,"Export selected files",610,560);dialog.Text("1 selected file",true);dialog.Text("Stacks copy directly to the destination; subs retain compatible input folders.");var fields=ExportDestination(dialog,"Test export");dialog.CloseOnly();
    try{dialog.Window.Show();PumpPopupLayout();var options=fields.Options();if(options.AddMetadata||options.CreateNewFolder||fields.Name.IsEnabled)throw new Exception("Export extras are enabled by default.");
     fields.NewFolder.IsChecked=true;fields.Metadata.IsChecked=true;if(!fields.Name.IsEnabled||!fields.Options().CreateNewFolder||!fields.Options().AddMetadata)throw new Exception("Optional export controls did not update.");fields.NewFolder.IsChecked=false;if(fields.Name.IsEnabled||fields.Options().CreateNewFolder||!fields.Options().AddMetadata)throw new Exception("Export options are not independent.");fields.Metadata.IsChecked=false;
     foreach(var label in PopupChildren<TextBlock>(dialog.Window))if(!string.IsNullOrWhiteSpace(label.Text))Readable(label.Foreground,dialog.Window.Background,mode+" export label");CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Export_"+mode+".png"));
    }finally{dialog.Window.Close();}
   }
   Theme.Apply(Window,"Dark");File.WriteAllText(Path.Combine(output,"export-ui-smoke.txt"),"PASS: metadata and new-folder export options default off, independent toggles, folder name state and light/dark labels.");
  }
 }
}
