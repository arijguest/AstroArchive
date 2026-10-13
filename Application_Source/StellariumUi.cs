using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AstroArchive {
 public partial class MainUi {
  // One click: show the previewed capture's position, and its time and place when known, in Stellarium.
  void InitializeStellarium(){B("StellariumButton").Click+=(s,e)=>OpenInStellarium();UpdateStellariumAction();}
  StellariumView StellariumViewFor(Frame frame){return frame==null?null:Stellarium.From(CaptureSky.Resolve(frame,settings),frame);}
  void UpdateStellariumAction(){
   var button=B("StellariumButton");if(button==null)return;
   var view=repo==null?null:StellariumViewFor(previewFrame);button.IsEnabled=view!=null;button.ToolTip=Localization.T(Stellarium.Tooltip(view));
  }
  void OpenInStellarium(){OpenStellariumView(StellariumViewFor(previewFrame));}
  // Also used by the target information pane, which opens a catalogue position or a Solar System body by name.
  void OpenStellariumView(StellariumView view){
   if(view==null)return;
   string script=Stellarium.Script(view);
   if(Stellarium.TrySend(script,Stellarium.RemotePort,700))return;
   string executable=Stellarium.Resolve(settings);
   if(executable==null){OpenStellariumWeb(view);return;}
   try{Stellarium.Launch(executable,script);}
   catch(Exception error){if(!(error is IOException||error is Win32Exception||error is UnauthorizedAccessException))throw;LocalizedMessage.Show("Could not start Stellarium.\n\n"+error.Message,"Open in Stellarium",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
  // Stellarium is not installed, so show the same view in Stellarium Web in the default browser.
  void OpenStellariumWeb(StellariumView view){
   if(!InternetAction())return;string url=Stellarium.WebUrl(view);
   try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
   catch(Exception error){if(!(error is Win32Exception||error is InvalidOperationException))throw;LocalizedMessage.Show("Stellarium is not installed and your browser could not open Stellarium Web. Visit "+url+"\n\n"+error.Message,"Open in Stellarium",MessageBoxButton.OK,MessageBoxImage.Information);}
  }
 }
}
