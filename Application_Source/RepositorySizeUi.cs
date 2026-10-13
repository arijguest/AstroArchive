using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AstroArchive {
 // A quiet size readout beside the repository path, refreshed in the background
 // after the archive changes; it never delays browsing or imports.
 public partial class MainUi {
  RepositorySizeInfo repositorySize;CancellationTokenSource sizeCancel;DispatcherTimer sizeDelay;int sizeGeneration;
  void ScheduleRepositorySize(double seconds=2){
   if(sizeDelay==null){sizeDelay=new DispatcherTimer();sizeDelay.Tick+=(s,e)=>{sizeDelay.Stop();MeasureRepositorySize();};}
   sizeDelay.Stop();sizeDelay.Interval=TimeSpan.FromSeconds(seconds);sizeDelay.Start();
  }
  async void MeasureRepositorySize(){
   if(sizeCancel!=null)sizeCancel.Cancel();var label=L("RepositorySizeLabel");
   if(repo==null||closing){repositorySize=null;label.Visibility=Visibility.Collapsed;return;}
   var cancel=sizeCancel=new CancellationTokenSource();int generation=++sizeGeneration;var measured=repo;string root=repo.Root,meta=repo.Meta,edited=repo.EditedFolder,dump=repo.DumpFolder;
   try{
    var size=await Task.Run(()=>RepositorySize.Measure(root,meta,edited,dump,cancel.Token),cancel.Token);
    if(generation!=sizeGeneration||repo!=measured||closing)return;repositorySize=size;
    label.Text=size.Summary;label.ToolTip=size.Breakdown;label.Visibility=Visibility.Visible;System.Windows.Automation.AutomationProperties.SetHelpText(label,size.Breakdown);
   }catch(OperationCanceledException){}
   catch(Exception e){if(!(e is System.IO.IOException||e is UnauthorizedAccessException))throw;if(generation==sizeGeneration)label.Visibility=Visibility.Collapsed;}
   finally{if(ReferenceEquals(sizeCancel,cancel)){sizeCancel=null;}cancel.Dispose();}
  }
  void DisposeRepositorySize(){if(sizeDelay!=null)sizeDelay.Stop();if(sizeCancel!=null)sizeCancel.Cancel();}
 }
}
