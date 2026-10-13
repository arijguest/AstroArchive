using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AstroArchive {
 // Prepares a GitHub issue: the user's description plus the diagnostics they choose to include,
 // previewed exactly as they will be sent. Nothing leaves the PC until the user submits on GitHub.
 public sealed class ProblemReportWindow:Window {
  public readonly TextBox Summary=new TextBox(),Description=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinLines=4,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Preview=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas")};
  public readonly List<CheckBox> Choices=new List<CheckBox>();public readonly Button Open=new Button{Content="Open GitHub issue",IsDefault=true};
  public readonly IList<ReportSection> Sections;
  public string LastUrl{get;private set;}
  public Func<string,bool> Launch=url=>{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});return true;};
  public ProblemReportWindow(Window owner,Exception error,string context,IList<ReportSection> sections){
   Sections=sections;Owner=owner;Title="Report a problem";Icon=ApplicationIcon.Image;Width=Math.Min(780,SystemParameters.WorkArea.Width-24);Height=Math.Min(760,SystemParameters.WorkArea.Height-24);MinWidth=Math.Min(520,Width);MinHeight=Math.Min(460,Height);ShowInTaskbar=owner==null;
   WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;
   if(owner!=null){Resources.MergedDictionaries.Add(owner.Resources);FontFamily=owner.FontFamily;FontSize=owner.FontSize;SetResourceReference(Control.FontSizeProperty,"UiFontControl");Theme.Bind(this,Control.BackgroundProperty,"Canvas");Theme.Bind(this,Control.ForegroundProperty,"Text");}
   else{FontFamily=new FontFamily("Segoe UI");FontSize=13;Background=Brushes.White;Foreground=Brushes.Black;}
   var layout=new Grid{Margin=new Thickness(22)};foreach(var height in new[]{GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto})layout.RowDefinitions.Add(new RowDefinition{Height=height});Content=layout;
   var header=new StackPanel{Margin=new Thickness(0,0,0,12)};layout.Children.Add(header);
   var heading=new TextBlock{Text="Report a problem",FontWeight=FontWeights.SemiBold};heading.SetResourceReference(TextBlock.FontSizeProperty,"UiFontTitle");header.Children.Add(heading);
   var intro=new TextBlock{Text="Describe what went wrong. AstroArchive adds the details you tick below so the problem can be reproduced. Nothing is sent until you submit the issue on GitHub.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};Muted(intro);header.Children.Add(intro);
   if(error!=null){var notice=new Border{Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,12,0,0),BorderThickness=new Thickness(4,0,0,0),Child=new TextBlock{Text=Localization.T("AstroArchive hit an error:")+" "+error.Message,TextWrapping=TextWrapping.Wrap}};notice.SetResourceReference(Border.BackgroundProperty,"Starred");notice.SetResourceReference(Border.BorderBrushProperty,"StarAccent");header.Children.Add(notice);}
   var form=new StackPanel();Grid.SetRow(form,1);layout.Children.Add(form);
   form.Children.Add(Label("Title (optional)",Summary));Summary.Padding=new Thickness(6,4,6,4);AutomationProperties.SetName(Summary,"Issue title");UiHelp.Describe(Summary,"For example: Import stops at 40% over USB.");form.Children.Add(Summary);
   form.Children.Add(Label("What happened?",Description));Description.Padding=new Thickness(6,4,6,4);Description.Height=96;AutomationProperties.SetName(Description,"What happened");UiHelp.Describe(Description,"What were you doing, what did you expect, and what happened instead?");form.Children.Add(Description);
   form.Children.Add(Label("Include",null));var include=new WrapPanel();form.Children.Add(include);
   foreach(var section in sections){var part=section;var choice=new CheckBox{Content=part.Title,IsChecked=part.Include,Margin=new Thickness(0,4,18,4),Tag=part};UiHelp.Hint(choice,"About "+part.Text.Length+" characters.");choice.Checked+=(s,e)=>{part.Include=true;Update();};choice.Unchecked+=(s,e)=>{part.Include=false;Update();};Choices.Add(choice);include.Children.Add(choice);}
   var previewPanel=new DockPanel{Margin=new Thickness(0,10,0,0)};Grid.SetRow(previewPanel,2);layout.Children.Add(previewPanel);
   var previewLabel=Label("Preview of the diagnostics",Preview);DockPanel.SetDock(previewLabel,Dock.Top);previewPanel.Children.Add(previewLabel);
   var note=new TextBlock{Text="Folder paths are replaced with [local path]. Images and archive files are never attached. Remove anything else you'd rather not share in GitHub before submitting.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};Muted(note);note.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");DockPanel.SetDock(note,Dock.Bottom);previewPanel.Children.Add(note);
   AutomationProperties.SetName(Preview,"Diagnostic details that will be sent");previewPanel.Children.Add(Preview);
   var buttons=new DockPanel{Margin=new Thickness(0,14,0,0)};Grid.SetRow(buttons,3);layout.Children.Add(buttons);
   var right=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(right,Dock.Right);buttons.Children.Add(right);
   var cancel=new Button{Content="Cancel",IsCancel=true};cancel.Click+=(s,e)=>Close();right.Children.Add(cancel);
   if(owner!=null){Theme.Bind(Open,Control.BackgroundProperty,"Accent");Theme.Bind(Open,Control.ForegroundProperty,"AccentText");Theme.Bind(Open,Control.BorderBrushProperty,"Accent");}
   Open.Margin=new Thickness(0);AutomationProperties.SetName(Open,"Open a prefilled GitHub issue with the displayed details");UiHelp.Describe(Open,"Opens a prefilled issue in your browser to review and submit.");Open.Click+=(s,e)=>OpenIssue();right.Children.Add(Open);
   var left=new StackPanel{Orientation=Orientation.Horizontal};buttons.Children.Add(left);
   var copy=new Button{Content="Copy report"};UiHelp.Describe(copy,"Copy the description and diagnostics to the clipboard.");copy.Click+=(s,e)=>CopyReport();left.Children.Add(copy);
   var save=new Button{Content="Save report…"};UiHelp.Describe(save,"Save the description and diagnostics as a text file.");save.Click+=(s,e)=>SaveReport();left.Children.Add(save);
   Update();Loaded+=(s,e)=>Description.Focus();
  }
  static void Muted(TextBlock text){text.SetResourceReference(TextBlock.ForegroundProperty,"Muted");}
  static TextBlock Label(string text,Control target){return new TextBlock{Text=text,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,4)};}
  void Update(){Preview.Text=AppErrorReporting.Compose(Sections);}
  public string FullReport(){return (Summary.Text.Trim().Length>0?Summary.Text.Trim()+"\r\n\r\n":"")+(Description.Text.Trim().Length>0?AppErrorReporting.Sanitize(Description.Text.Trim())+"\r\n\r\n":"")+AppErrorReporting.Compose(Sections).Replace("\n","\r\n");}
  bool CopyReport(){try{Clipboard.SetText(FullReport());return true;}catch(Exception error){LocalizedMessage.Show(this,error.Message,"The report could not be copied",MessageBoxButton.OK,MessageBoxImage.Warning);return false;}}
  void SaveReport(){var file=new SaveFileDialog{FileName="AstroArchive_problem_report.txt",Filter="Text files|*.txt"};if(file.ShowDialog(this)!=true)return;try{System.IO.File.WriteAllText(file.FileName,FullReport());}catch(Exception error){LocalizedMessage.Show(this,error.Message,"The report could not be saved",MessageBoxButton.OK,MessageBoxImage.Warning);}}
  void OpenIssue(){
   bool truncated;LastUrl=AppErrorReporting.IssueUrl(Summary.Text,Description.Text,Sections,out truncated);
   // The full report always goes to the clipboard; when the link had to be shortened, say so first.
   try{Clipboard.SetText(FullReport());}catch{}
   if(truncated)LocalizedMessage.Show(this,"The report is too long for a web link, so the issue draft has a shortened copy. The full report is on your clipboard: paste it into the issue before submitting.","Report a problem",MessageBoxButton.OK,MessageBoxImage.Information);
   try{if(Launch(LastUrl)){AppLog.Info("Problem report opened on GitHub"+(truncated?" (shortened)":""));Close();}}
   catch(Exception launchError){LocalizedMessage.Show(this,"Could not open GitHub. The report has not been sent. Use Copy report or Save report… and attach it to an issue at github.com/arijguest/AstroArchive/issues.\n\n"+launchError.Message,"Report a problem",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
 }
 internal static class AppErrorReportingUi {
  // Used before the main window exists, for example after a startup error.
  internal static void Report(Exception error,string context,Window owner=null){new ProblemReportWindow(owner,error,context,AppErrorReporting.Sections(error,context,null)).ShowDialog();}
 }
 public partial class MainUi {
  void ReportProblem(){ReportProblem(null,"User-submitted problem");}
  internal void ReportProblem(Exception error,string context){var report=ProblemReport(error,context);report.Owner=OwnerForReport();report.ShowDialog();}
  Window OwnerForReport(){return Window.OwnedWindows.OfType<HelpWindow>().FirstOrDefault(w=>w.IsActive)??(Window)Window;}
  // Adds what only the main window knows: page, display settings, recent activity and the last import report.
  ProblemReportWindow ProblemReport(Exception error,string context){
   var tabs=(TabControl)Window.FindName("MainTabs");
   var details=new Dictionary<string,string>{
    {"Page",tabs.SelectedIndex==2?"Edited":tabs.SelectedIndex==1?"Import":"Repository"},
    {"Language",Localization.Current},
    {"Theme",(settings.ThemeMode??"System")+(settings.HighContrast?", high contrast":"")+", text "+settings.TextScalePercent+"%"},
    {"Screen",SystemParameters.PrimaryScreenWidth.ToString("0",CultureInfo.InvariantCulture)+" x "+SystemParameters.PrimaryScreenHeight.ToString("0",CultureInfo.InvariantCulture)},
    {"Archive",repo==null?"No repository open":all.Count+" captures"},
    {"Saved telescopes",(settings.Telescopes==null?0:settings.Telescopes.Count).ToString(CultureInfo.InvariantCulture)},
    {"Running work",activities.Count(a=>a.Running).ToString(CultureInfo.InvariantCulture)},
    {"Automatic update checks",settings.DisableAutomaticReleaseChecks?"Off":"On"}
   };
   var sections=AppErrorReporting.Sections(error,context,details);
   var recent=activities.Take(15).Select(a=>a.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+" "+a.Title+(a.Running?" (running)":a.Failed?" (failed)":a.Canceled?" (cancelled)":"")+(string.IsNullOrEmpty(a.Status)?"":": "+a.Status)).ToList();
   if(recent.Count>0)sections.Insert(Math.Min(2,sections.Count),new ReportSection{Title="Recent activity",Text=string.Join("\n",recent),Cap=2000});
   if(repo!=null&&!string.IsNullOrWhiteSpace(repo.LastReport))sections.Add(new ReportSection{Title="Last scan or import report",Text=repo.LastReport.Trim(),Cap=2000});
   return new ProblemReportWindow(Window,error,context,sections);
  }
 }
}
