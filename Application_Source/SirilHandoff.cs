// Siril's documented GUI contract: siril [options] [image-or-sequence-file].
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace AstroArchive {
 public static class SirilHandoff {
  public static EditedProject CreateWorkingCopy(Repository repo,List<Frame> frames,string name,string executable,CancellationToken ct,Action<ProgressInfo> progress){
   ValidateExecutable(executable);if(!CanSend(frames))throw new InvalidOperationException("Select one non-rejected, uncompressed FITS stack to open in Siril.");return repo.CreateEditedWorkingCopy(frames[0],name,"Siril",ct,progress);
  }
  public static bool CanSend(IList<Frame> frames){return frames.Count==1&&frames[0].Kind=="Stack"&&!frames[0].Rejected&&Util.IsFits(frames[0].OriginalName)&&!frames[0].OriginalName.EndsWith(".gz",StringComparison.OrdinalIgnoreCase);}
  public static void ValidateExecutable(string executable){
   if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable)||!Path.GetFileName(executable).Equals("siril.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Choose the installed Siril GUI executable (siril.exe). The command-line executable cannot open the graphical workspace.");
  }
  public static string ExportStack(Repository repo,List<Frame> frames,ExportOptions options,string executable,CancellationToken ct,Action<ProgressInfo> progress){
   ValidateExecutable(executable);if(!CanSend(frames))throw new InvalidOperationException("Select one non-rejected, uncompressed FITS stack to open in Siril.");
   var export=new ExportOptions{Parent=options.Parent,Name=options.Name,Mode="Files",IncludeCalibration=false,CreateNewFolder=true};
   string project=Exporter.Create(repo,frames,export,ct,progress);
   string image=Directory.GetFiles(project,"*",SearchOption.TopDirectoryOnly).Single(Util.IsFits);
   ct.ThrowIfCancellationRequested();return image;
  }
  // .NET Framework has no ArgumentList: quote using the Windows argv rules.
  public static string QuoteArgument(string value){
   var text=new StringBuilder("\"");int slashes=0;
   foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){text.Append('\\',slashes*2+1);text.Append(c);}else{text.Append('\\',slashes);text.Append(c);}slashes=0;}
   text.Append('\\',slashes*2);text.Append('"');return text.ToString();
  }
  public static ProcessStartInfo LaunchInfo(string executable,string image){
   ValidateExecutable(executable);image=Path.GetFullPath(image);
   if(!File.Exists(image)||!Util.IsFits(image)||image.EndsWith(".gz",StringComparison.OrdinalIgnoreCase))throw new IOException("The exported FITS stack is unavailable.");
   return new ProcessStartInfo(Path.GetFullPath(executable),QuoteArgument(image)){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(image)};
  }
 }
}
