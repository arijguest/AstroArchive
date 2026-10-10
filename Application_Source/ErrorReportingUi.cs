using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroArchive {
 internal static class AppErrorReportingUi {
  internal static void Report(Exception error,string context,Window owner=null){
   string diagnostics=AppErrorReporting.Diagnostics(error,context);
   var dialog=new Window{Owner=owner,Title="Report a problem",Width=760,Height=560,MinWidth=520,MinHeight=400,
    WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,
    FontFamily=owner==null?new FontFamily("Segoe UI"):owner.FontFamily,FontSize=owner==null?13:owner.FontSize,
    Background=Brushes.White};
   var layout=new Grid{Margin=new Thickness(20)};
   layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
   layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   layout.Children.Add(new TextBlock{Text="Review the diagnostic report before opening GitHub. No images, repository files, or repository paths are included; exception text may still contain sensitive values. Opening the draft sends the displayed details to GitHub. You can edit the issue before submitting it, or cancel now.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
   var preview=new TextBox{Text=diagnostics,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas"),Margin=new Thickness(0,0,0,12)};
   Grid.SetRow(preview,1);layout.Children.Add(preview);
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
   var cancel=new Button{Content="Cancel",IsCancel=true,MinWidth=96,Margin=new Thickness(0,0,8,0)};
   var open=new Button{Content="Open GitHub draft",IsDefault=true,MinWidth=140};
   AutomationProperties.SetName(open,"Open a prefilled GitHub issue draft with the displayed diagnostic report");
   cancel.Click+=(s,e)=>dialog.Close();
   open.Click+=(s,e)=>{
    try{Process.Start(new ProcessStartInfo(AppErrorReporting.IssueUrl(diagnostics)){UseShellExecute=true});dialog.Close();}
    catch(Exception launchError){MessageBox.Show(dialog,"Could not open GitHub. The report has not been sent.\n\n"+launchError.Message,"Report a problem",MessageBoxButton.OK,MessageBoxImage.Warning);}
   };
   buttons.Children.Add(cancel);buttons.Children.Add(open);Grid.SetRow(buttons,2);layout.Children.Add(buttons);
   dialog.Content=layout;dialog.ShowDialog();
  }
 }
}
