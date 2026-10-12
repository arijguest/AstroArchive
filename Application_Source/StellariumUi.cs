using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace AstroArchive {
 public partial class MainUi {
  // One click: show the previewed capture's position, and its time and place when known, in Stellarium.
  void InitializeStellarium(){B("StellariumButton").Click+=(s,e)=>OpenInStellarium();UpdateStellariumAction();}
  StellariumView StellariumViewFor(Frame frame){return frame==null?null:Stellarium.From(CaptureSky.Resolve(frame,settings),frame);}
  void UpdateStellariumAction(){
   var button=B("StellariumButton");if(button==null)return;
   var view=repo==null?null:StellariumViewFor(previewFrame);button.IsEnabled=view!=null;button.ToolTip=Localization.T(Stellarium.Tooltip(view));
  }
  void OpenInStellarium(){
   var view=StellariumViewFor(previewFrame);if(view==null)return;
   string script=Stellarium.Script(view);
   if(Stellarium.TrySend(script,Stellarium.RemotePort,700))return;
   string executable=Stellarium.Resolve(settings);
   if(executable==null){
    var picker=new OpenFileDialog{Title="Locate Stellarium — application could not be found automatically",Filter="Stellarium application|stellarium.exe",CheckFileExists=true};
    if(picker.ShowDialog(Window)!=true)return;
    if(!Stellarium.IsExecutable(picker.FileName)){LocalizedMessage.Show("Choose stellarium.exe from your Stellarium folder.","Open in Stellarium",MessageBoxButton.OK,MessageBoxImage.Information);return;}
    Stellarium.Configure(settings,picker.FileName);SaveSettings();executable=picker.FileName;
   }
   try{Stellarium.Launch(executable,script);}
   catch(Exception error){if(!(error is IOException||error is Win32Exception||error is UnauthorizedAccessException))throw;LocalizedMessage.Show("Could not start Stellarium.\n\n"+error.Message,"Open in Stellarium",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
 }
}
