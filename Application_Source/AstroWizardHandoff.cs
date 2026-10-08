// Mirrors StackingWizard's positional-file handoff to the verified AstroWizard build.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public static class AstroWizardHandoff {
  public static EditedProject CreateWorkingCopy(Repository repo,List<Frame> frames,string name,string executable,CancellationToken ct,Action<ProgressInfo> progress){
   if(!CanSend(frames))throw new InvalidOperationException("Select one non-rejected FITS stack for AstroWizard.");ValidateExecutable(executable,ct);return repo.CreateEditedWorkingCopy(frames[0],name,"AstroWizard",ct,progress);
  }
  public const string VerifiedBuild="7 October 2026 (Clear Eyes)";
  public const string VerifiedSha256="200f8eb21079cba4d4de0482e52265d18ca69425bcb49f513e622c7bc504f803";
  public static bool CanSend(IList<Frame> frames){return frames.Count==1&&frames[0].Kind=="Stack"&&!frames[0].Rejected&&new[]{".fit",".fits"}.Contains(Path.GetExtension(frames[0].OriginalName??"").ToLowerInvariant());}
  public static void ValidateExecutable(string executable,CancellationToken ct){
   if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable)||!Path.GetExtension(executable).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Locate the official Windows AstroWizard executable.");
   if(Util.Hash(executable,ct)!=VerifiedSha256)throw new IOException("Direct handoff is verified for AstroWizard "+VerifiedBuild+" only. Choose that official build. Other builds require handoff verification before they can be used.");
  }
  public static string ExportStack(Repository repo,List<Frame> frames,ExportOptions options,string executable,CancellationToken ct,Action<ProgressInfo> progress){
   if(!CanSend(frames))throw new InvalidOperationException("Select one non-rejected FITS stack (.fit or .fits) for AstroWizard.");
   ValidateExecutable(executable,ct);
   string project=Exporter.Create(repo,frames,new ExportOptions{Parent=options.Parent,Name=options.Name,Mode="Files",IncludeCalibration=false},ct,progress);
   ct.ThrowIfCancellationRequested();return Directory.GetFiles(Path.Combine(project,"files"),"*",SearchOption.AllDirectories).Single(Util.IsFits);
  }
  public static ProcessStartInfo LaunchInfo(string executable,string image){
   ValidateExecutable(executable,CancellationToken.None);image=Path.GetFullPath(image);
   if(!File.Exists(image)||!new[]{".fit",".fits"}.Contains(Path.GetExtension(image).ToLowerInvariant()))throw new IOException("The exported FITS stack is unavailable.");
   return new ProcessStartInfo(Path.GetFullPath(executable),SirilHandoff.QuoteArgument(image)){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(image)};
  }
 }
}
