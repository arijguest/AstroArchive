// One Export window for verified copies, stacking folders, app handoffs, AstroBin and catalogue CSVs. Menus open it with a category chosen.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AstroArchive {
 public enum ExportCategory {Files,Stacking,Application,AstroBin,Catalogue}
 public partial class MainUi {
  static readonly string[] ExportCategoryNames={"Files","Stacking folder","Open in app","AstroBin CSV","Catalogue CSV"};
  static string ExportCategoryName(ExportCategory category){return ExportCategoryNames[(int)category];}
  sealed class ExportScope {public List<Frame> Files=new List<Frame>();public List<EditedImage> Edited;public bool Selected,WholeRepository;}
  sealed class ExportPage {
   public ExportCategory Category;public string Action="Export";public Func<string> Problem=()=>null;public Func<bool> NeedsDestination=()=>false;public Func<string> FolderName=()=>"";public Func<bool> Accept=()=>true;public Action Activated,PreferencesChanged;
  }
  sealed class AstroBinChoice {public AstroBinSheet Sheet;public bool Video;public string Label{get;set;}public string Csv{get{return Video?Sheet.VideoCsv:Sheet.Csv;}}}
  sealed class ExportWindowFields {
   public FormWindow Dialog;public ExportScope Scope;public readonly List<ExportPage> Pages=new List<ExportPage>();public Button Primary;public TextBlock Problem;public FrameworkElement Destination;public TextBox Parent,FolderName;public CheckBox NewFolder;public bool FolderNameEdited;public Action Refresh,Run;
   public CheckBox FilesMetadata,StackCalibration,StackUnknown,StackConvert,StackAstroBin,StackSessions,StackMetadata,StackRejected,AppRemember,AppCalibration,AppUnknown,AstroCalibration,AstroUnknown;
   public ComboBox FilesAfter,StackInputs,StackAfter,Application,AstroSheet,CatalogueScope;public TextBlock StackAvailability,AstroSummary,AstroNotes,AstroStatus;public TextBox AstroPreview;public AstroBinReport AstroBin;
   public ExportPage Page{get{var list=Dialog.SectionList;return Pages[list==null?0:Math.Max(0,Math.Min(Pages.Count-1,list.SelectedIndex))];}}
  }
  static string ExportStamp(){return DateTime.Now.ToString("yyyyMMdd_HHmmss");}
  TextBlock ExportNote(FormWindow d,string text=""){var note=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};note.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");Theme.Bind(note,TextBlock.ForegroundProperty,"Muted");d.Add(note);return note;}
  ExportOptions ExportDestinationOptions(ExportWindowFields f){return new ExportOptions{Parent=f.Parent.Text.Trim(),Name=f.FolderName.Text.Trim(),CreateNewFolder=f.NewFolder.IsChecked==true};}
  bool ValidExportDestination(ExportWindowFields f){try{Exporter.Destination(repo,ExportDestinationOptions(f));return true;}catch(Exception e){LocalizedMessage.Show(f.Dialog.Window,e.Message,"Export");return false;}}

  // Entry points used by menus, buttons and right-click actions.
  void OpenExport(ExportCategory? category=null,List<Frame> files=null,bool wholeRepository=false){
   if(repo==null||RepositoryOperationBlocked)return;
   var scope=new ExportScope{Files=files??Context(),Selected=SelectedFiles().Count>0,WholeRepository=wholeRepository};
   var chosen=category??LastExportCategory();if(category==null&&!ExportCategoryAvailable(chosen,scope.Files))chosen=ExportCategory.Files;
   ExportWindowFields fields;var dialog=ExportWindow(scope,chosen,out fields);if(dialog.Show()&&fields.Run!=null)fields.Run();
  }
  void OpenEditedExport(ExportCategory category){
   if(repo==null||RepositoryOperationBlocked||SearchBlocked("EditedSearchBox"))return;var images=SelectedEditedImages();if(images.Count==0)return;
   ExportWindowFields fields;var dialog=ExportWindow(new ExportScope{Edited=images.ToList(),Selected=true},category,out fields);if(dialog.Show()&&fields.Run!=null)fields.Run();
  }
  ExportCategory LastExportCategory(){for(int i=0;i<ExportCategoryNames.Length;i++)if(ExportCategoryNames[i]==settings.LastExportCategory)return (ExportCategory)i;return ExportCategory.Files;}
  void ExportCatalogue(){OpenExport(ExportCategory.Catalogue,null,true);}
  void ExportEditedFiles(){OpenEditedExport(ExportCategory.Files);}
  void ExportEditedTo(){OpenEditedExport(ExportCategory.Application);}
  MenuItem ExportMenu(List<Frame> selected){
   var menu=new MenuItem{Header="Export",IsEnabled=selected.Count>0};UiHelp.Tip(menu,"Open Export with an option chosen.");
   foreach(var category in new[]{ExportCategory.Files,ExportCategory.Stacking,ExportCategory.Application,ExportCategory.AstroBin,ExportCategory.Catalogue}){var chosen=category;menu.Items.Add(FileAction(ExportCategoryName(chosen)+"…",()=>OpenExport(chosen,selected),ExportCategoryAvailable(chosen,selected)));}
   return menu;
  }
  static bool ExportCategoryAvailable(ExportCategory category,List<Frame> files){
   if(files.Count==0)return false;if(category==ExportCategory.Stacking)return files.Any(f=>f.Kind=="Light"||f.Kind=="Stack");
   if(category==ExportCategory.AstroBin)return files.Any(f=>f.Kind=="Light"||f.Kind=="Stack"||f.Kind=="Video");return true;
  }
  void ShowExportMenu(){OpenExport();}

  FormWindow ExportWindow(ExportScope scope,ExportCategory category,out ExportWindowFields fields){
   var d=new FormWindow(Window,"Export",880,740);d.Window.MinWidth=Math.Min(640,d.Window.Width);var f=fields=new ExportWindowFields{Dialog=d,Scope=scope};bool edited=scope.Edited!=null;
   var categories=edited?new[]{ExportCategory.Files,ExportCategory.Application}:new[]{ExportCategory.Files,ExportCategory.Stacking,ExportCategory.Application,ExportCategory.AstroBin,ExportCategory.Catalogue};
   d.SectionsName="Export options";d.Sections(categories.Select(ExportCategoryName).ToArray());
   // Summary of what is being exported, and the destination shared by the folder-based options.
   var header=new StackPanel{Margin=new Thickness(0,0,0,16)};var summary=edited?EditedExportSummary(scope.Edited):ExportSummaries.Describe(scope.Files,scope.Selected);
   var title=new TextBlock{Text=summary.Title+(summary.Targets.Length>0?" · "+summary.Targets:""),TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold};title.SetResourceReference(TextBlock.FontSizeProperty,"UiFontTitle");Theme.Bind(title,TextBlock.ForegroundProperty,"Text");header.Children.Add(title);
   if(summary.TargetList.Contains("\n"))UiHelp.Tip(title,summary.TargetList);System.Windows.Automation.AutomationProperties.SetName(title,"Export selection: "+title.Text);
   if(summary.Detail.Length>0){var detail=new TextBlock{Text=summary.Detail,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0)};detail.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");Theme.Bind(detail,TextBlock.ForegroundProperty,"Muted");header.Children.Add(detail);}
   var bottom=new StackPanel{Margin=new Thickness(0,12,0,0)};f.Problem=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8),Visibility=Visibility.Collapsed};f.Problem.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");Theme.Bind(f.Problem,TextBlock.ForegroundProperty,"Muted");bottom.Children.Add(f.Problem);
   var destination=new StackPanel();f.Destination=destination;bottom.Children.Add(destination);
   var caption=new TextBlock{Text="Destination folder",Margin=new Thickness(0,0,0,5)};caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");destination.Children.Add(caption);
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   f.Parent=new TextBox{Text=settings.ExportWorkingDirectory??"",VerticalContentAlignment=VerticalAlignment.Center};System.Windows.Automation.AutomationProperties.SetName(f.Parent,"Destination folder");UiHelp.For(f.Parent,"Destination folder");row.Children.Add(f.Parent);
   var choose=new Button{Content="Choose folder…",Margin=new Thickness(8,0,0,0)};UiHelp.For(choose,"Choose folder");choose.Click+=(s,e)=>{string path=Folder("Choose an export destination",f.Parent.Text,d.Window);if(path!=null)f.Parent.Text=path;};Grid.SetColumn(choose,1);row.Children.Add(choose);destination.Children.Add(row);
   var nameRow=new Grid{Margin=new Thickness(0,8,0,0)};nameRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});nameRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
   f.NewFolder=new CheckBox{Content=new TextBlock{Text="Create new folder"},IsChecked=false,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};System.Windows.Automation.AutomationProperties.SetName(f.NewFolder,"Create new folder");nameRow.Children.Add(f.NewFolder);
   f.FolderName=new TextBox{IsEnabled=false,Visibility=Visibility.Hidden,VerticalContentAlignment=VerticalAlignment.Center};System.Windows.Automation.AutomationProperties.SetName(f.FolderName,"New folder name");UiHelp.For(f.FolderName,"New folder name");Grid.SetColumn(f.FolderName,1);nameRow.Children.Add(f.FolderName);destination.Children.Add(nameRow);
   Action folderName=()=>{f.FolderName.IsEnabled=f.NewFolder.IsChecked==true;f.FolderName.Visibility=f.FolderName.IsEnabled?Visibility.Visible:Visibility.Hidden;};f.NewFolder.Checked+=(s,e)=>folderName();f.NewFolder.Unchecked+=(s,e)=>folderName();
   f.FolderName.TextChanged+=(s,e)=>{if(f.FolderName.IsKeyboardFocusWithin)f.FolderNameEdited=true;};
   d.Pin(header,bottom);
   for(int i=0;i<categories.Length;i++){d.Tab(i);ExportPage page;
    switch(categories[i]){
     case ExportCategory.Files:page=edited?EditedFilesPage(d,f):FilesPage(d,f);break;
     case ExportCategory.Stacking:page=StackingPage(d,f);break;
     case ExportCategory.Application:page=ApplicationPage(d,f);break;
     case ExportCategory.AstroBin:page=AstroBinPage(d,f);break;
     default:page=CataloguePage(d,f);break;
    }
    page.Category=categories[i];f.Pages.Add(page);
   }
   f.Refresh=()=>{
    var page=f.Page;string problem=page.Problem();f.Primary.Content=page.Action;f.Primary.IsEnabled=problem==null;UiHelp.For(f.Primary,page.Action);
    f.Problem.Text=problem??"";f.Problem.Visibility=problem==null?Visibility.Collapsed:Visibility.Visible;
    f.Destination.Visibility=page.NeedsDestination()?Visibility.Visible:Visibility.Collapsed;if(!f.FolderNameEdited)f.FolderName.Text=page.FolderName();
   };
   var preferences=d.FooterButton("Export preferences…",()=>{string previous=settings.ExportWorkingDirectory??"";Configure(2,d.Window);if(string.IsNullOrWhiteSpace(f.Parent.Text)||f.Parent.Text==previous)f.Parent.Text=settings.ExportWorkingDirectory??"";foreach(var p in f.Pages)if(p.PreferencesChanged!=null)p.PreferencesChanged();f.Refresh();});preferences.Margin=new Thickness(0,0,8,0);
   f.Primary=d.Accept("Export",()=>{
    var page=f.Page;if(page.Problem()!=null)return false;if(page.NeedsDestination()&&!ValidExportDestination(f))return false;if(!page.Accept())return false;
    settings.LastExportCategory=ExportCategoryName(page.Category);try{SaveSettings();}catch(Exception){}return true;
   });
   d.SectionList.SelectionChanged+=(s,e)=>{if(e.OriginalSource!=d.SectionList)return;var page=f.Page;if(page.Activated!=null)page.Activated();f.Refresh();};
   int index=Array.IndexOf(categories,category);d.SelectTab(index<0?0:index);var initial=f.Page;if(initial.Activated!=null)initial.Activated();f.Refresh();
   return d;
  }
  static ExportSummary EditedExportSummary(List<EditedImage> images){
   var summary=new ExportSummary{Title=images.Count==1?"1 selected Edited file":images.Count.ToString("N0")+" selected Edited files"};
   ExportSummaries.Targets(images.Select(i=>i.Metadata!=null&&!string.IsNullOrEmpty(i.Metadata.Object)?i.Metadata.ObjectLabel:i.Project!=null&&!string.IsNullOrEmpty(i.Project.Target)?Catalog.Label(i.Project.Target):""),summary);
   summary.Detail=string.Join(" · ",images.GroupBy(i=>i.FileType).OrderByDescending(g=>g.Count()).Select(g=>g.Count()+" "+g.Key));return summary;
  }

  // Original-file copies.
  ExportPage FilesPage(FormWindow d,ExportWindowFields f){
   var files=f.Scope.Files;var page=new ExportPage{Action="Export files",NeedsDestination=()=>files.Count>0,FolderName=()=>"AstroArchive_export_"+ExportStamp(),Problem=()=>files.Count==0?"Select files to export.":null};
   d.Text("Files",true);d.Text("Copies the original files byte for byte and verifies every copy. Existing files are kept; duplicate names receive a numbered suffix.");
   f.FilesMetadata=d.Check("Add metadata",false);UiHelp.Tip(f.FilesMetadata,"Include a manifest, companion metadata and workflow notes.");
   Func<ExportOptions> options=()=>{var choice=ExportDestinationOptions(f);choice.Mode="Files";choice.IncludeCalibration=false;choice.IncludeRejected=true;choice.AddMetadata=f.FilesMetadata.IsChecked==true;return choice;};
   f.FilesAfter=AfterExportChoice(d,files);Action refresh=()=>RefreshAfterExport(f.FilesAfter,files,options());page.PreferencesChanged=refresh;refresh();
   page.Accept=()=>{if(!ValidSiril(d,f.FilesAfter))return false;var choice=options();bool siril=Convert.ToString(f.FilesAfter.SelectedItem)=="Siril";f.Run=()=>RunExport(files,choice,siril,false);return true;};
   return page;
  }
  ComboBox AfterExportChoice(FormWindow d,List<Frame> files){
   var combo=files.Any(f=>f.Kind=="Stack")?d.Select("Open with… after export",new[]{"None","Siril"},"None"):new ComboBox{ItemsSource=new[]{"None","Siril"},SelectedItem="None"};
   UiHelp.Tip(combo,"Open one exported FITS stack in Siril. Multiple stacks and subs-only exports cannot open a single image.");return combo;
  }
  static void RefreshAfterExport(ComboBox combo,List<Frame> files,ExportOptions options){bool eligible=SirilHandoff.StackAfterExport(files,options)!=null;combo.IsEnabled=eligible;if(!eligible)combo.SelectedItem="None";}
  bool ValidSiril(FormWindow d,ComboBox combo){if(Convert.ToString(combo.SelectedItem)!="Siril")return true;try{SirilHandoff.ValidateExecutable(settings.SirilExecutable);return true;}catch(Exception e){LocalizedMessage.Show(d.Window,e.Message,"Siril");return false;}}

  // Ready-to-stack folders with optional calibrations and an AstroBin acquisition CSV.
  ExportPage StackingPage(FormWindow d,ExportWindowFields f){
   var items=f.Scope.Files.Where(x=>x.Kind=="Light"||x.Kind=="Stack").ToList();string target=items.Select(x=>x.Target).Distinct().Count()==1?items[0].Target:"Multiple targets";
   var page=new ExportPage{Action="Export stacking folder",NeedsDestination=()=>true,FolderName=()=>Util.Safe(target)+"_"+ExportStamp()};
   d.Text("Stacking folder",true);d.Text("Groups lights by compatible capture settings, adds matching calibration files when chosen, and verifies every copy. Stacking happens in your processing software.");
   if(items.Count==0){page.Problem=()=>"Select lights or stacks to prepare a stacking folder.";page.NeedsDestination=()=>false;return page;}
   bool mixed=items.Any(x=>x.Kind=="Light")&&items.Any(x=>x.Kind=="Stack"),lights=items.Any(x=>x.Kind=="Light");
   f.StackInputs=mixed?d.Select("Inputs",new[]{"Subs","Stacks","Both"},"Both"):new ComboBox{ItemsSource=new[]{"Subs","Stacks","Both"},SelectedItem=lights?"Subs":"Stacks"};if(mixed)UiHelp.Tip(f.StackInputs,"Stacks and subs are exported separately. Do not combine stacks with their constituent subs or overlapping live-stack snapshots.");
   // Calibration matching runs when this option is first shown, not whenever Export opens.
   List<Frame> availableCalibrations=new List<Frame>();bool active=false;
   f.StackCalibration=d.Check("Include matching calibrations",false);f.StackCalibration.Visibility=lights?Visibility.Visible:Visibility.Collapsed;
   f.StackAvailability=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};Theme.Bind(f.StackAvailability,TextBlock.ForegroundProperty,"Muted");if(lights)d.Add(f.StackAvailability);
   f.StackUnknown=d.Check("Confirm unknown-state subs are uncalibrated",false);f.StackUnknown.Visibility=items.Any(x=>x.Kind=="Light"&&x.Calibration=="Unknown")?Visibility.Visible:Visibility.Collapsed;f.StackUnknown.IsEnabled=false;UiHelp.Tip(f.StackUnknown,"Enable only if these subs have not already been calibrated.");
   f.StackConvert=d.Check("Convert supported images to FITS",false);f.StackConvert.Visibility=items.Any(Exporter.RequiresConversion)?Visibility.Visible:Visibility.Collapsed;UiHelp.Tip(f.StackConvert,"Creates derived FITS from linear, decodable inputs. Archived originals stay unchanged.");
   f.StackAstroBin=d.Check("Add AstroBin acquisition CSV",true);UiHelp.Tip(f.StackAstroBin,"Writes astrobin_acquisition.csv and notes from the original metadata of the exported lights, stacks and calibration frames.");
   Func<ExportOptions> options=()=>{var choice=ExportDestinationOptions(f);choice.Mode=Convert.ToString(f.StackInputs.SelectedItem);choice.IncludeCalibration=f.StackCalibration.IsEnabled&&f.StackCalibration.IsChecked==true;choice.IncludeUnknownCalibration=f.StackUnknown.IsChecked==true;choice.IncludeRejected=f.StackRejected!=null&&f.StackRejected.IsChecked==true;choice.SeparateSessions=f.StackSessions!=null&&f.StackSessions.IsChecked==true;choice.ConvertToFits=f.StackConvert.IsChecked==true;choice.AddMetadata=f.StackMetadata!=null&&f.StackMetadata.IsChecked==true;choice.IncludeAstroBinCsv=f.StackAstroBin.IsChecked==true;choice.AstroBinFilterIds=settings.AstroBinFilterIds;return choice;};
   f.StackAfter=AfterExportChoice(d,items);Button review=null;
   d.Advanced("More options",()=>{f.StackSessions=d.Check("Separate sessions into folders",false);f.StackMetadata=d.Check("Add metadata",false);UiHelp.Tip(f.StackMetadata,"Include a manifest, companion metadata and workflow notes.");f.StackRejected=d.Check("Include rejected/reference files",false);review=d.Button("Review calibration matches…",()=>ShowReport("Calibration matching",CalibrationReport(items.Where(x=>x.Kind=="Light").ToList(),availableCalibrations)));review.Visibility=f.StackCalibration.Visibility;});
   Action summary=()=>{if(!active)return;try{bool subs=Convert.ToString(f.StackInputs.SelectedItem)!="Stacks";f.StackCalibration.IsEnabled=subs&&availableCalibrations.Count>0;f.StackUnknown.IsEnabled=f.StackCalibration.IsEnabled&&f.StackCalibration.IsChecked==true;review.IsEnabled=subs;var selected=items.Where(x=>x.Kind=="Light"&&(f.StackRejected.IsChecked==true||!x.Rejected)).ToList();int count=f.StackCalibration.IsEnabled&&f.StackCalibration.IsChecked==true?Exporter.AvailableCalibrations(selected,availableCalibrations,f.StackSessions.IsChecked==true,f.StackUnknown.IsChecked==true).Count:0;int unknownCount=selected.Count(x=>x.Calibration=="Unknown");f.StackAvailability.Text=!subs?"Stacks receive no additional calibration.":availableCalibrations.Count==0?"No calibration files available.":f.StackCalibration.IsChecked!=true?"Calibration files omitted.":count>0?count+" matching calibration file"+(count==1?"":"s")+". Inputs with different calibration sets use separate folders.":"No matching calibration files. Review calibration matches for the reason.";if(subs&&f.StackCalibration.IsChecked==true&&availableCalibrations.Count>0&&unknownCount>0&&f.StackUnknown.IsChecked!=true)f.StackAvailability.Text=(count>0?f.StackAvailability.Text+" ":"")+unknownCount+" subs have unknown processing state. Confirm they are uncalibrated to include their calibration files.";}catch(Exception error){f.StackAvailability.Text=error.Message;}};
   Action after=()=>RefreshAfterExport(f.StackAfter,items,options());page.PreferencesChanged=after;
   f.StackInputs.SelectionChanged+=(s,e)=>{after();summary();};foreach(var check in new[]{f.StackConvert,f.StackRejected}){check.Checked+=(s,e)=>after();check.Unchecked+=(s,e)=>after();}foreach(var check in new[]{f.StackSessions,f.StackCalibration,f.StackUnknown,f.StackRejected}){check.Checked+=(s,e)=>summary();check.Unchecked+=(s,e)=>summary();}after();
   page.Activated=()=>{if(active)return;active=true;availableCalibrations=repo==null?new List<Frame>():Exporter.ExistingCalibrations(repo,all);summary();};
   page.Accept=()=>{var choice=options();if(!items.Any(x=>(choice.IncludeRejected||!x.Rejected)&&(choice.Mode=="Both"||choice.Mode=="Subs"&&x.Kind=="Light"||choice.Mode=="Stacks"&&x.Kind=="Stack"))){LocalizedMessage.Show(d.Window,"This input choice has no eligible files.","Export");return false;}if(!ValidSiril(d,f.StackAfter))return false;bool siril=Convert.ToString(f.StackAfter.SelectedItem)=="Siril";f.Run=()=>RunExport(items,choice,siril,true);return true;};
   return page;
  }
  void RunExport(List<Frame> selected,ExportOptions options,bool openSiril,bool stacking){
   string executable=settings.SirilExecutable;var result=openSiril?new ExportResult():null;
   Run(ct=>Exporter.Create(repo,selected,options,ct,Progress,result),path=>{
    if(openSiril){try{using(var process=Process.Start(SirilHandoff.ExportLaunchInfo(executable,result))){if(process==null)throw new IOException("Siril did not start.");}L("StatusLabel").Text="Exported to "+path+" · opened stack in Siril";if(completionActivity!=null)completionActivity.OutputPath=path;return;}
     catch(Exception error){if(completionActivity!=null){completionActivity.NeedsReview=true;completionActivity.ReportTitle="Siril handoff";completionActivity.Report="Your export is complete: "+path+"\nSiril could not open the stack: "+error.Message;}}}
    ExportComplete(path,stacking);
    if(options.AstroBinFiles.Count>0&&completionActivity!=null){completionActivity.Status+=" AstroBin CSV added.";if(completionActivity.Report==null){completionActivity.ReportTitle="AstroBin acquisition notes";completionActivity.Report=options.AstroBin.NotesText();}}
   });
  }

  // Processor handoffs: verified working copies or stacking inputs, opened in a compatible app.
  ExportPage ApplicationPage(FormWindow d,ExportWindowFields f){
   bool edited=f.Scope.Edited!=null;var page=new ExportPage{Action="Open in app"};ExternalSelection selection=null;string error=null;
   if(edited)selection=new ExternalSelection{Paths=f.Scope.Edited.Select(i=>repo.EditedPath(i.Project,i.RelativePath)).ToList()};else try{selection=ExternalApps.Selection(f.Scope.Files);}catch(Exception e){error=e.Message;}
   d.Text("Open in app",true);
   if(selection==null){d.Text("Opens verified copies in PixInsight, Siril, DSS and other processing apps.");page.Problem=()=>error;return page;}
   d.Text(selection.Folder?"Copies and verifies the original files, then opens your chosen app.":edited?"Opens the selected Edited images in your chosen app. Save results in their image folders.":"Creates verified working copies in Edited, then opens them in your chosen app. Save processed results alongside the copies.");
   var combo=new ComboBox();f.Application=combo;combo.Items.Add(new ComboBoxItem{Content="Choose an application…"});System.Windows.Automation.AutomationProperties.SetName(combo,"Application");
   string preferred=ExternalApps.Default(settings,selection);foreach(var app in ExternalApps.All){string problem=ExternalApps.Problem(app,selection);var item=new ComboBoxItem{Content=app.Name,Tag=app,IsEnabled=problem==null,ToolTip=problem};ToolTipService.SetShowOnDisabled(item,true);combo.Items.Add(item);if(preferred==app.Id)combo.SelectedItem=item;}
   if(combo.SelectedItem==null)combo.SelectedIndex=0;d.Add(combo);
   var help=ExportNote(d);Action describe=()=>{var app=SelectedApplication(combo);help.Text=app==null?(ExternalApps.All.Any(a=>ExternalApps.Problem(a,selection)==null)?"Choose a compatible destination. Unavailable destinations below explain their restrictions.":"No destination supports this selection. Files can copy the originals; convert to a supported format or select fewer images to open them in an app."):app.Help;};
   combo.SelectionChanged+=(s,e)=>{describe();if(f.Refresh!=null)f.Refresh();};describe();
   var unavailable=ExternalApps.All.Where(a=>ExternalApps.Problem(a,selection)!=null).ToList();if(unavailable.Count>0)d.Options("Unavailable destinations ("+unavailable.Count+")",()=>{foreach(var app in unavailable){d.Text(app.Name,true);d.Text(ExternalApps.Problem(app,selection));}});
   f.AppRemember=d.Check(selection.Subframes?"Use this as my default for subframe folders":"Use this as my default for these file types",false);
   if(selection.Folder){
    f.AppCalibration=d.Check("Include matching calibration files",false);f.AppCalibration.IsEnabled=selection.Paths.All(p=>ExternalApps.FileType(p)!="SER"&&ExternalApps.FileType(p)!="AVI");
    d.Advanced("More options",()=>{f.AppUnknown=d.Check("Include calibrations when the subframe calibration state is unknown",false);});
    f.AppUnknown.IsEnabled=false;f.AppCalibration.Checked+=(s,e)=>f.AppUnknown.IsEnabled=true;f.AppCalibration.Unchecked+=(s,e)=>f.AppUnknown.IsEnabled=false;
   }
   d.Button("Application settings…",()=>{ExportDestinationSettings(owner:d.Window);page.PreferencesChanged();f.Refresh();});
   page.PreferencesChanged=()=>{string updated=ExternalApps.Default(settings,selection);if(updated!=null)combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i=>i.Tag is ExportApplication&&((ExportApplication)i.Tag).Id==updated);describe();};
   page.NeedsDestination=()=>selection.Folder;page.FolderName=()=>"AstroArchive_inputs_"+ExportStamp();
   page.Problem=()=>{var app=SelectedApplication(combo);return app==null?"Choose an application.":ExternalApps.Problem(app,selection);};
   page.Accept=()=>{
    var app=SelectedApplication(combo);string executable=LocateExportApplication(app,d.Window);if(executable==null)return false;bool remember=f.AppRemember.IsChecked==true;
    if(edited){f.Run=()=>{RememberExport(selection,app,remember);RunEditedOpen(selection,app,executable);};return true;}
    bool cals=f.AppCalibration!=null&&f.AppCalibration.IsChecked==true,unknown=cals&&f.AppUnknown.IsChecked==true;var options=selection.Folder?ExportDestinationOptions(f):null;var files=f.Scope.Files;
    f.Run=()=>{RememberExport(selection,app,remember);RunExportTo(files,selection,app,executable,options,cals,unknown);};return true;
   };
   return page;
  }
  void RunExportTo(List<Frame> selected,ExternalSelection selection,ExportApplication app,string executable,ExportOptions options,bool cals,bool unknown){
   if(!selection.Folder){
    EditedProject project=null;string launchError=null;var paths=new List<string>();
    Run(ct=>{project=repo.CreateEditedWorkingCopies(selected,selected[0].TargetLabel+" · "+app.Name+" · "+ExportStamp(),app.Name,ct,Progress);paths=project.Sources.Select(f=>repo.EditedPath(project,f.RelativePath)).ToList();ct.ThrowIfCancellationRequested();try{ExternalApps.Launch(app,executable,paths,null,ct);}catch(OperationCanceledException){throw;}catch(Exception error){launchError=error.Message;}return "";},done=>{RefreshEdited();ExportLaunchResult(app,Path.GetDirectoryName(paths[0]),launchError,false);});return;
   }
   options.Mode="Files";options.IncludeCalibration=false;options.IncludeRejected=false;List<DssJob> jobs=null;string launchFailure=null;
   Run(ct=>{
    var inputs=ExternalApps.FolderInputs(repo,selected,cals,unknown);
    // Every calibration must be readable by the receiver too, before copying.
    string problem=ExternalApps.Problem(app,new ExternalSelection{Folder=true,Paths=inputs.Select(f=>f.OriginalName??f.RelativePath).ToList()});if(problem!=null)throw new IOException(problem);
    string folder=Exporter.Create(repo,inputs,options,ct,Progress);if(app.Id=="dss")jobs=ExternalApps.WriteDssJobs(folder,selected,options.ExportedFiles,cals,unknown,ct);
    ct.ThrowIfCancellationRequested();if(jobs==null||jobs.Count==1)try{ExternalApps.Launch(app,executable,jobs==null?new string[0]:new[]{jobs[0].Path},folder,ct);}catch(OperationCanceledException){throw;}catch(Exception error){launchFailure=error.Message;}return folder;
   },folder=>{if(jobs!=null&&jobs.Count>1){QueueActivityReview("DSS capture groups are ready",()=>ChooseDssJob(folder,jobs,app,executable));if(completionActivity!=null)completionActivity.OutputPath=folder;return;}ExportLaunchResult(app,folder,launchFailure,app.Id!="dss"&&app.Id!="siril");});
  }
  void RunEditedOpen(ExternalSelection selection,ExportApplication app,string executable){
   string error=null;Run(ct=>{try{ExternalApps.Launch(app,executable,selection.Paths,null,ct);}catch(OperationCanceledException){throw;}catch(Exception e){error=e.Message;}return "";},done=>ExportLaunchResult(app,Path.GetDirectoryName(selection.Paths[0]),error,false));
  }

  // Edited working copies, copied in their saved format.
  ExportPage EditedFilesPage(FormWindow d,ExportWindowFields f){
   var images=f.Scope.Edited;var page=new ExportPage{Action="Export files",NeedsDestination=()=>true,FolderName=()=>"AstroArchive_Edited_"+ExportStamp(),Problem=()=>images.Count==0?"Select Edited files to export.":null};
   d.Text("Files",true);d.Text("Copies the selected images in their original format and verifies every copy. Existing files are kept; duplicate names receive a numbered suffix.");
   page.Accept=()=>{var options=ExportDestinationOptions(f);f.Run=()=>RunEditedExport(images,options);return true;};return page;
  }
  void RunEditedExport(List<EditedImage> images,ExportOptions options){Run(ct=>Exporter.CreateEdited(repo,images,options,ct,Progress),path=>ExportComplete(path,false));}

  // AstroBin acquisition CSV: preview, copy and save without exporting images.
  ExportPage AstroBinPage(FormWindow d,ExportWindowFields f){
   var files=f.Scope.Files;var page=new ExportPage{Action="Save CSV…"};
   d.Text("AstroBin CSV",true);d.Text("Builds AstroBin's acquisition table from the original archive metadata, keeping details that processing can lose. No image files are exported.");
   if(!files.Any(x=>x.Kind=="Light"||x.Kind=="Stack"||x.Kind=="Video")){page.Problem=()=>"Select lights, stacks or recordings to describe their acquisition.";return page;}
   bool lights=files.Any(x=>x.Kind=="Light");List<Frame> pool=null;
   f.AstroCalibration=d.Check("Count matching calibration frames",true);f.AstroCalibration.Visibility=lights?Visibility.Visible:Visibility.Collapsed;UiHelp.Tip(f.AstroCalibration,"Counts archived darks, flats, flat darks and bias frames that match the selected subs. Each frame is counted once.");
   f.AstroUnknown=d.Check("Confirm unknown-state subs are uncalibrated",false);f.AstroUnknown.Visibility=files.Any(x=>x.Kind=="Light"&&x.Calibration=="Unknown")?Visibility.Visible:Visibility.Collapsed;UiHelp.Tip(f.AstroUnknown,"Enable only if these subs have not already been calibrated.");
   var sheetCaption=new TextBlock{Text="CSV",Margin=new Thickness(0,12,0,5)};sheetCaption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");d.Add(sheetCaption);
   f.AstroSheet=new ComboBox{DisplayMemberPath="Label"};System.Windows.Automation.AutomationProperties.SetName(f.AstroSheet,"AstroBin CSV for");UiHelp.Tip(f.AstroSheet,"Each target needs its own AstroBin image. Planetary recordings use AstroBin's video columns.");d.Add(f.AstroSheet);
   f.AstroSummary=new TextBlock{TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,6)};f.AstroSummary.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");Theme.Bind(f.AstroSummary,TextBlock.ForegroundProperty,"Text");d.Add(f.AstroSummary);
   f.AstroPreview=new TextBox{IsReadOnly=true,Height=150,FontFamily=new FontFamily("Consolas"),TextWrapping=TextWrapping.NoWrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};System.Windows.Automation.AutomationProperties.SetName(f.AstroPreview,"AstroBin CSV preview");d.Add(f.AstroPreview);
   var actions=new WrapPanel{Margin=new Thickness(0,8,0,0)};var copy=new Button{Content="Copy CSV",Margin=new Thickness(0,0,8,8)};UiHelp.Hint(copy,"Copy the CSV to paste into AstroBin's acquisition import.");var copyNotes=new Button{Content="Copy notes",Margin=new Thickness(0,0,8,8)};UiHelp.Hint(copyNotes,"Copy the notes about blank or uncertain values.");actions.Children.Add(copy);actions.Children.Add(copyNotes);d.Add(actions);
   f.AstroStatus=ExportNote(d);f.AstroStatus.Margin=new Thickness(0,0,0,4);
   f.AstroNotes=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8)};f.AstroNotes.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");Theme.Bind(f.AstroNotes,TextBlock.ForegroundProperty,"Text");d.Add(f.AstroNotes);
   Func<AstroBinChoice> current=()=>f.AstroSheet.SelectedItem as AstroBinChoice;
   Action show=()=>{
    var choice=current();var report=f.AstroBin;f.AstroStatus.Text="";if(report==null)return;
    f.AstroPreview.Text=choice==null?"":choice.Csv;
    f.AstroSummary.Text=choice==null?"No acquisition rows.":choice.Video?choice.Sheet.Label+" · "+choice.Sheet.Videos.Count+" recording"+(choice.Sheet.Videos.Count==1?"":"s"):choice.Sheet.Label+" · "+choice.Sheet.Rows.Count+" row"+(choice.Sheet.Rows.Count==1?"":"s")+" · "+choice.Sheet.Frames.ToString("N0")+" frames · "+CaptureGroups.ExposureLabel(choice.Sheet.IntegrationSeconds,0);
    var notes=report.Notes.Concat(choice==null?report.Sheets.SelectMany(s=>s.Notes):choice.Sheet.Notes).ToList();if(choice!=null&&choice.Video)notes.AddRange(choice.Sheet.Videos.Where(v=>v.UtcStart.Length>0).Select(v=>v.Name+" started "+v.UtcStart+"."));
    f.AstroNotes.Text=notes.Count==0?"No blank or uncertain values.":"Check before uploading:\n"+string.Join("\n",notes.Select(n=>"• "+n));
    copy.IsEnabled=choice!=null;if(f.Refresh!=null)f.Refresh();
   };
   Action build=()=>{
    if(pool==null)pool=lights&&repo!=null?Exporter.ExistingCalibrations(repo,all):new List<Frame>();
    f.AstroUnknown.IsEnabled=f.AstroCalibration.IsChecked==true;
    f.AstroBin=AstroBinCsv.Build(files,new AstroBinOptions{CalibrationPool=pool,CountCalibrations=lights&&f.AstroCalibration.IsChecked==true,IncludeUnknownCalibration=f.AstroUnknown.IsChecked==true,FilterIds=settings.AstroBinFilterIds});
    var previous=current();var choices=new List<AstroBinChoice>();foreach(var sheet in f.AstroBin.Sheets){if(sheet.Rows.Count>0)choices.Add(new AstroBinChoice{Sheet=sheet,Label=sheet.Label+(sheet.Videos.Count>0?" · long exposure":"")});if(sheet.Videos.Count>0)choices.Add(new AstroBinChoice{Sheet=sheet,Video=true,Label=sheet.Label+" · planetary video"});}
    f.AstroSheet.ItemsSource=choices;f.AstroSheet.SelectedItem=previous==null?choices.FirstOrDefault():choices.FirstOrDefault(c=>c.Label==previous.Label)??choices.FirstOrDefault();
    sheetCaption.Visibility=f.AstroSheet.Visibility=choices.Count>1?Visibility.Visible:Visibility.Collapsed;show();
   };
   f.AstroSheet.SelectionChanged+=(s,e)=>show();foreach(var check in new[]{f.AstroCalibration,f.AstroUnknown}){check.Checked+=(s,e)=>build();check.Unchecked+=(s,e)=>build();}
   copy.Click+=(s,e)=>{var choice=current();if(choice==null)return;try{Clipboard.SetText(choice.Csv);f.AstroStatus.Text="CSV copied. Paste it into AstroBin's acquisition CSV import.";}catch(Exception error){f.AstroStatus.Text="The clipboard is unavailable: "+error.Message;}};
   copyNotes.Click+=(s,e)=>{if(f.AstroBin==null)return;var choice=current();try{Clipboard.SetText(f.AstroBin.NotesText(choice==null?null:choice.Sheet));f.AstroStatus.Text="Notes copied.";}catch(Exception error){f.AstroStatus.Text="The clipboard is unavailable: "+error.Message;}};
   var filters=AstroBinCsv.FilterNames(files);
   if(filters.Count>0)d.Advanced("AstroBin filter IDs",()=>{
    d.Text("AstroBin identifies filters by number. Paste the filter's AstroBin page address or its number. IDs are remembered.");
    foreach(string filter in filters){string name=filter;string saved=null;if(settings.AstroBinFilterIds!=null)settings.AstroBinFilterIds.TryGetValue(name,out saved);var input=d.Input(name,saved??"");
     input.LostKeyboardFocus+=(s,e)=>{string text=input.Text.Trim(),id=AstroBinCsv.FilterId(text);if(text.Length>0&&id==null){f.AstroStatus.Text=name+": enter a number or an AstroBin filter page address.";return;}
      var ids=settings.AstroBinFilterIds==null?new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase):new Dictionary<string,string>(settings.AstroBinFilterIds,StringComparer.OrdinalIgnoreCase);string old;ids.TryGetValue(name,out old);if((old??"")==(id??""))return;
      if(id==null)ids.Remove(name);else{ids[name]=id;input.Text=id;}settings.AstroBinFilterIds=ids;try{SaveSettings();}catch(Exception){}build();};
    }
   });
   page.Activated=()=>{if(f.AstroBin==null)build();};
   page.Problem=()=>f.AstroBin!=null&&f.AstroBin.Empty?"No acquisition rows can be written. See the notes above.":null;
   page.Accept=()=>{
    var choice=current();if(choice==null)return false;var picker=new SaveFileDialog{FileName=Util.Safe(choice.Sheet.Target)+(choice.Video?"_astrobin_video":"_astrobin")+".csv",Filter="AstroBin CSV|*.csv"};if(picker.ShowDialog(d.Window)!=true)return false;
    // UTF-8 without a byte-order mark keeps AstroBin's first column name exact.
    try{File.WriteAllText(picker.FileName,choice.Csv,new UTF8Encoding(false));}catch(Exception error){LocalizedMessage.Show(d.Window,error.Message,"AstroBin CSV");return false;}
    string saved=picker.FileName;if(f.AstroSheet.Items.Count>1){f.AstroStatus.Text="Saved "+choice.Label+" to "+saved+". Choose another CSV to save it too.";return false;}
    f.Run=()=>{L("StatusLabel").Text="AstroBin CSV saved: "+saved;};return true;
   };
   return page;
  }

  // Searchable metadata spreadsheet of the selection or whole repository.
  ExportPage CataloguePage(FormWindow d,ExportWindowFields f){
   var files=f.Scope.Files;var page=new ExportPage{Action="Save CSV…"};
   d.Text("Catalogue CSV",true);d.Text("A spreadsheet of archive metadata: targets, equipment, exposures, dates, checksums and paths, for searching or record keeping. No image files are exported.");
   var choices=new List<string>();if(files.Count>0)choices.Add((f.Scope.Selected?"Selected files":"Files in view")+" ("+files.Count.ToString("N0")+")");choices.Add("Entire repository ("+all.Count.ToString("N0")+")");
   f.CatalogueScope=d.Select("Rows",choices.ToArray(),f.Scope.WholeRepository||files.Count==0?choices.Last():choices[0]);
   page.Accept=()=>{
    bool whole=f.CatalogueScope.SelectedIndex==choices.Count-1;var picker=new SaveFileDialog{FileName=whole?"AstroArchive_catalog.csv":"AstroArchive_selection.csv",Filter="CSV catalogue|*.csv"};if(picker.ShowDialog(d.Window)!=true)return false;
    string destination=picker.FileName;var snapshot=whole?null:files.Select(x=>x.Clone()).ToList();f.Run=()=>RunCatalogue(destination,snapshot);return true;
   };
   return page;
  }
  void RunCatalogue(string destination,List<Frame> snapshot){
   Run(ct=>{ct.ThrowIfCancellationRequested();repo.ExportIndex(destination,snapshot);return snapshot==null?"Catalogue CSV exported.":snapshot.Count+" catalogue rows exported.";},message=>{L("StatusLabel").Text=message;if(completionActivity!=null)completionActivity.OutputPath=Path.GetDirectoryName(destination);},"Exporting catalogue CSV");
  }
 }
}
