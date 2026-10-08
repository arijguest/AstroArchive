using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace AstroArchive {
 public partial class MainUi {
  bool dumpStartupChecked,operationBusy;
  Window dumpProgressWindow;TextBlock dumpProgressStatus,dumpProgressRate;ProgressBar dumpProgressBar;Button dumpProgressCancel;
  Window operationProgressWindow;
  void InitializeProgressVisibility(){Window.StateChanged+=(s,e)=>UpdateProgressVisibility();Window.IsVisibleChanged+=(s,e)=>UpdateProgressVisibility();}
  void EnsureOperationProgress(){
   if(operationProgressWindow!=null)return;
   var popup=(Popup)Window.FindName("OperationPopup");popup.IsOpen=false;var content=popup.Child;popup.Child=null;
   var window=new Window{Owner=Window,Icon=ApplicationIcon.Image,ShowInTaskbar=false,Title="AstroArchive — file progress",SizeToContent=SizeToContent.WidthAndHeight,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=Window.FontFamily};
   window.Resources.MergedDictionaries.Add(Window.Resources);window.SetResourceReference(Control.FontSizeProperty,"UiFontControl");Theme.Bind(window,Control.BackgroundProperty,"Surface");Theme.Bind(window,Control.ForegroundProperty,"Text");window.Content=content;
   window.Closing+=(s,e)=>{if(cancel!=null){e.Cancel=true;B("CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}};
   operationProgressWindow=window;
  }
  void UpdateProgressVisibility(){
   bool visible=Window.IsVisible&&Window.WindowState!=WindowState.Minimized;
   // A WPF Popup is an independent native window; use an owned window instead.
   ((Popup)Window.FindName("OperationPopup")).IsOpen=false;
   if(operationBusy&&dumpProgressWindow==null&&visible)EnsureOperationProgress();
   if(operationProgressWindow!=null){
    if(!visible||!operationBusy||dumpProgressWindow!=null)operationProgressWindow.Hide();
    else{if(operationProgressWindow.WindowState==WindowState.Minimized)operationProgressWindow.WindowState=WindowState.Normal;if(!operationProgressWindow.IsVisible)operationProgressWindow.Show();}
   }
   if(dumpProgressWindow!=null){
    if(!visible)dumpProgressWindow.Hide();
    else if(operationBusy){if(dumpProgressWindow.WindowState==WindowState.Minimized)dumpProgressWindow.WindowState=WindowState.Normal;if(!dumpProgressWindow.IsVisible)dumpProgressWindow.Show();}
   }
   if(!visible){if(filtersPopup!=null)filtersPopup.IsOpen=false;ClosePreviewDetails("");ClosePreviewDetails("Edited");}
  }
  void OpenDumpProgress(){
   var window=new Window{Owner=Window,Icon=ApplicationIcon.Image,ShowInTaskbar=false,Title="Processing Dump folder",Width=580,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=Window.FontFamily,FontSize=Window.FontSize};window.Resources.MergedDictionaries.Add(Window.Resources);Theme.Bind(window,Control.BackgroundProperty,"Canvas");Theme.Bind(window,Control.ForegroundProperty,"Text");
   var content=new StackPanel{Margin=new Thickness(24)};content.Children.Add(new TextBlock{Text="Processing Dump folder",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
   dumpProgressStatus=new TextBlock{Text="Checking files…",TextWrapping=TextWrapping.Wrap};content.Children.Add(dumpProgressStatus);
   dumpProgressRate=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};content.Children.Add(dumpProgressRate);
   dumpProgressBar=new ProgressBar{Minimum=0,Maximum=1,Height=8,Margin=new Thickness(0,16,0,0)};content.Children.Add(dumpProgressBar);
   dumpProgressCancel=new Button{Content="Cancel",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};dumpProgressCancel.Click+=(s,e)=>{B("CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));dumpProgressStatus.Text="Canceling after the current operation…";dumpProgressCancel.IsEnabled=false;};content.Children.Add(dumpProgressCancel);
   window.Closing+=(s,e)=>{if(dumpProgressWindow==window&&cancel!=null){e.Cancel=true;dumpProgressCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}};
   window.Content=content;dumpProgressWindow=window;if(Window.WindowState!=WindowState.Minimized)window.Show();
  }
  void CloseDumpProgress(){var window=dumpProgressWindow;dumpProgressWindow=null;dumpProgressStatus=dumpProgressRate=null;dumpProgressBar=null;dumpProgressCancel=null;if(window!=null)window.Close();}
  void UpdateDumpProgress(ProgressInfo progress){if(dumpProgressWindow==null)return;dumpProgressStatus.Text=L("StatusLabel").Text;dumpProgressRate.Text=L("RateLabel").Text;dumpProgressBar.IsIndeterminate=!settings.ReducedMotion&&!progress.TotalKnown&&!progress.Finished;dumpProgressBar.Value=progress.ProgressFraction;}
  void AddDumpSettings(FormWindow dialog){
   dialog.Text("Dump folder",true);
   dialog.Text("Drop FITS files or telescope folders into Dump inside your archive. On opening the archive, AstroArchive sorts them and removes successfully verified inputs, including duplicates. Failed or unsupported files stay. Edit metadata to assign each physical telescope ID.");
   if(repo==null)return;
   dialog.Text(repo.DumpFolder);
   dialog.Button("Open dump folder",()=>{try{repo.EnsureDumpFolder();Process.Start(new ProcessStartInfo(repo.DumpFolder){UseShellExecute=true});}catch(Exception e){MessageBox.Show(dialog.Window,e.Message,"Dump folder unavailable");}});
   dialog.Button("Process dump folder now",()=>{dialog.Window.Close();ProcessDumpUi();});
  }
  void ProcessDumpUi(){
   if(repo==null||cancel!=null||closing)return;
   try{repo.EnsureDumpFolder();if(!Directory.EnumerateFileSystemEntries(repo.DumpFolder).Any())return;}catch(Exception error){MessageBox.Show(Window,error.Message,"Dump folder unavailable",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
   OpenDumpProgress();
   ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked=false;plan=null;BeginLive(true);DumpResult result=null;bool ignoreFailed=settings.IgnoreFailed,ignoreRaster=settings.IgnoreRasterImports;
   Run(ct=>{result=repo.ProcessDump(ct,Progress,settings.CopyWorkers,LiveFrame,ignoreFailed,ignoreRaster);return result.Summary;},summary=>{
    plan=result.Plan;FilterImports();
    L("StatusLabel").Text=summary;
    if(result.NeedsReview)ShowReport("Dump folder: files retained for review",repo.LastReport);
   });
  }
 }
}
