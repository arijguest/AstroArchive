using System;
using System.Diagnostics;
using System.IO;
namespace AstroArchive {
 public enum StackingApplication { Siril, StackingWizard, Other }
 public static class StackingApps {
  public static string Name(StackingApplication app){switch(app){case StackingApplication.Siril:return "Siril";case StackingApplication.StackingWizard:return "StackingWizard";case StackingApplication.Other:return "Other…";default:throw new ArgumentOutOfRangeException("app");}}
  public static void ValidateExecutable(string executable,StackingApplication app){
   Name(app);
   if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable)||!Path.GetExtension(executable).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Choose an installed application executable (.exe).");
   if(app==StackingApplication.Siril)SirilHandoff.ValidateExecutable(executable);
  }
  public static ProcessStartInfo LaunchInfo(string executable,string folder,StackingApplication app){
   ValidateExecutable(executable,app);folder=Path.GetFullPath(folder);
   if(!Directory.Exists(folder)||!File.Exists(Path.Combine(folder,"manifest.json"))||File.Exists(Path.Combine(folder,"INCOMPLETE.txt")))throw new IOException("The completed exported folder is unavailable. Your application has not been started.");
   // Siril documents --directory. Other receivers have no verified folder-open
   // contract: launch the selected app without guessing file/folder arguments.
   return new ProcessStartInfo(Path.GetFullPath(executable),app==StackingApplication.Siril?"--directory "+SirilHandoff.QuoteArgument(folder):""){UseShellExecute=false,WorkingDirectory=folder};
  }
 }
}
