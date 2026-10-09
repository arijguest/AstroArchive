using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
namespace AstroArchive {
 public static class UiHelp {
  public static void Describe(FrameworkElement control,string text){AutomationProperties.SetHelpText(control,text??"");}
  public static void ClearTip(FrameworkElement control){control.ClearValue(FrameworkElement.ToolTipProperty);}
  public static readonly DependencyProperty OnlyWhenTruncatedProperty=DependencyProperty.RegisterAttached("OnlyWhenTruncated",typeof(bool),typeof(UiHelp),new PropertyMetadata(false,OverflowChanged));
  public static void SetOnlyWhenTruncated(DependencyObject control,bool value){control.SetValue(OnlyWhenTruncatedProperty,value);}
  public static bool GetOnlyWhenTruncated(DependencyObject control){return (bool)control.GetValue(OnlyWhenTruncatedProperty);}
  static void OverflowChanged(DependencyObject control,DependencyPropertyChangedEventArgs e){var text=control as TextBlock;if(text==null)return;if((bool)e.NewValue)text.ToolTipOpening+=OverflowOpening;else text.ToolTipOpening-=OverflowOpening;}
  static void OverflowOpening(object sender,ToolTipEventArgs e){var text=(TextBlock)sender;var full=new FormattedText(text.Text??"",CultureInfo.CurrentUICulture,text.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,text.Foreground??Brushes.Black,VisualTreeHelper.GetDpi(text).PixelsPerDip);if(full.WidthIncludingTrailingWhitespace<=Math.Max(0,text.ActualWidth-text.Padding.Left-text.Padding.Right)+0.5)e.Handled=true;}
  public static void Tip(FrameworkElement control,string text,bool showOnDisabled=true){if(string.IsNullOrEmpty(text)){ClearTip(control);return;}control.ToolTip=new ToolTip{Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,MaxWidth=360}};ToolTipService.SetShowOnDisabled(control,showOnDisabled);ToolTipService.SetInitialShowDelay(control,800);ToolTipService.SetShowDuration(control,20000);}
  public static void Hint(FrameworkElement control,string text,bool showOnDisabled=true){Tip(control,text,showOnDisabled);Describe(control,text);}
  public static void For(FrameworkElement control,string label){string text;if(DialogTips.TryGetValue(label.TrimEnd('…','.'),out text)){Tip(control,text);Describe(control,text);}}
  static readonly Dictionary<string,string> DialogTips=new Dictionary<string,string>{
   {"Recover profiles","Recover saved telescopes from archive metadata."},
   {"Full rescan","Read and hash every source file again."},
   {"Review and repair","Inspect flagged captures and retry failed transfers."},
   {"Review flagged files","Inspect captures marked for review."},
   {"Screen files","Check rejection labels and file integrity."},
   {"Retry failed imports","Retry failed transfers from unchanged source files."},
   {"Edit selected metadata","Change metadata for selected scan results."},
   {"Identify selected targets","Solve selected lights and stacks to identify objects."},
   {"Set Unknown targets","Assign an object to unidentified lights and stacks."},
   {"Scan report","Show scan results, skipped files and errors."},
   {"Dump folder","Inbox for captures waiting to be imported."},
   {"Process files","Import inbox captures; delete originals after verification."},
   {"Group subs by session","Condense acquisition subs into session rows."},
   {"Back up archive","Create a verified copy of the archive."},
   {"Review and analysis","Review captures, identify objects and assess mount rotation."},
   {"Identify targets","Solve light groups and stacks to identify objects."},
   {"Analyse rotation","Estimate EQ or Alt-Az from acquisition subs."},
   {"Maintenance","Verify, reindex or remove archive files."},
   {"Delete failed","Delete archive files with 'failed' in their filenames."},
   {"Purge non-raw files","Delete indexed PNG/JPG/JPEG copies; keep source originals."},
   {"Deletion history","Review archive deletions and reimport choices."},
   {"Delete archive data","Permanently clear indexed archive data; keep source originals."},
   {"Diagnostics","View operation timings and import reports."},
   {"Last operation","Show timings and diagnostics for the latest operation."},
   {"Last import report","Show the latest import's results and errors."},
   {"Catalogue CSV","Export capture metadata for spreadsheets."},
   {"Selected / visible files","Export metadata for selected or visible captures."},
   {"Entire repository","Export metadata for all archived captures."},
   {"Choose HDU / page / frame","Choose the image or frame used for preview and export."},
   {"Re-detect metadata and review","Review detected values before applying changes."},
   {"Convert supported images to FITS (keeps archived originals)","Export linear images as FITS; keep archived originals."},
   {"Telescope model name (optional)","Model name, separate from the physical telescope ID."},
   {"Camera model (optional)","Used to match calibration files."},
   {"Physical camera ID / serial (optional)","Identify the physical camera for calibration matching."},
   {"Camera offset (optional)","Offset setting. Blank keeps existing values."},
   {"Readout mode (optional)","Sensor readout mode, such as high conversion gain."},
   {"ROI identifier / x,y,width,height (optional)","Sensor crop and origin used for calibration matching."},
   {"Optical configuration ID (optional)","Optical setup used to match flats."},
   {"Capture timezone ID (optional; e.g. UTC or Eastern Standard Time)","Timezone of the recorded capture times."},
   {"Image data","Only confirmed linear data can be exported for stacking."},
   {"Physical telescope ID","Unique name for this physical telescope."},
   {"Frame type","Master calibration frames remain separate."},
   {"Mount mode","Choose EQ or Alt-Az. A ? marks an inference."},
   {"Calibration state","Calibrated or registered lights receive no extra calibration."},
   {"Exposure seconds (optional)","Seconds; use a decimal dot. Blank keeps existing values."},
   {"Gain (optional)","Use a decimal dot. Blank keeps existing values."},
   {"Sensor temperature °C (optional)","Celsius; use a decimal dot. Blank keeps existing values."},
   {"Binning x × y (optional, e.g. 1x1)","Horizontal × vertical binning. Blank keeps existing values."},
   {"ASTAP executable","Local solving also requires an ASTAP star database."},
   {"ASTAP image height in degrees (blank: automatic; useful for wide cameras)","Greater than 0° and at most 180°; blank is automatic."},
   {"Use Astrometry.net instead of ASTAP","Requires internet and your Astrometry.net API key."},
   {"Delete all archive data","Permanently delete indexed archive data; keep sources and unindexed files."},
   {"Destination folder","Use a location outside the repository."},
   {"New folder name","Existing folders are not overwritten."},
   {"Separate sessions into their own folders","Off combines compatible sessions."},
   {"Include matching calibration files","Add compatible darks, flats and biases."},
   {"Include calibrations for subs with unknown calibration state","Review unknown processing states before calibrating."},
   {"Include files marked rejected/reference","Include rejected and reference frames in this export."},
   {"New telescope name","Renames this device and its archived captures."},
   {"Export to","Open verified working copies or stacking inputs in a compatible app."},
   {"Export files","Copy selected files; metadata is optional."},
   {"Stacking folder","Group selected lights or stacks by compatible settings."},
   {"Export destinations","Choose application locations and defaults by file type."},
   {"Ready-to-stack folder","Group selected lights or stacks by compatible settings."},
   {"Ready-to-stack with calibrations","Include available matching calibration files."},
   {"Delete selected files","Delete selected archive copies; keep source originals."},
   {"Identify target","Solve once per Light session/target group; stacks solve individually."},
   {"Verify repository checksums","Report missing or changed archive files."},
   {"Index an existing repository","Index existing images without copying or moving them."},
   {"Search catalogue or enter a custom target","Choose a catalogue target or enter a custom name."},
   {"100%","One sampled preview pixel per screen pixel."},
  };
 }
 public partial class MainUi {
  static readonly Dictionary<string,string> ControlTips=new Dictionary<string,string>{
   {"RetryImportsButton","Retry visible failed transfers; rescan changed sources."},
   {"SkipFlaggedCheck","Exclude rejected or damaged captures."},
   {"IgnoreFailedCheck","Skip filenames containing “failed”; keep originals."},
   {"ImportOptionsButton","Open Import preferences and options for the current source."},
   {"AssignUnknownTargetButton","Assign selected Unknown lights/stacks, or visible ones if none selected."},
   {"ImportClearButton","Clear import search and filters."},
   {"ScreenImportsButton","Check visible captures for rejection or damaged FITS data."},
   {"ScreenLibraryButton","Check selected captures, or visible ones, for rejection or damaged FITS data."},
   {"OpenPreviewButton","Open image."},
   {"StretchMode","Display stretch; image data stays unchanged."},
   {"CoffeeButton","Support AstroArchive on Ko-fi."},
   {"ThemeButton","Switch light/dark theme."},
   {"ImportToolsButton","Metadata, target identification and scan report."},
   {"HelpButton","Open the guide (F1)."},
   {"AutoUploadButton","Import new USB captures; keep originals."},
   {"ClearButton","Clear search, filters and target selection."},
   {"LibraryColumnsButton","Choose columns; drag headers to reorder."},
   {"ImportColumnsButton","Choose columns; drag headers to reorder."},
   {"ExportButton","Export selected files, or all visible files if none selected."},
   {"RotationButton","Assess mount mode from rotation in acquisition subs."},
   {"SolveButton","Solve selected Light groups and stacks; review batch target matches."},
   {"EditButton","Edit selected files, or all visible files if none selected."},
   {"MoreButton","Archive tools."},
   {"SavedTelescopeBox","Reuse a saved telescope and source."},
   {"SaveTelescopeButton","Save this device, model and source folder."},
   {"RenameTelescopeButton","Rename this device and its archived captures."},
   {"RebuildTelescopesButton","Recover missing saved devices from archive metadata."},
   {"TelescopeBox","Use a unique name for each physical telescope."},
   {"ModelBox","Auto detects each capture’s model."},
   {"CameraBox","Overrides apply to every scanned capture."},
   {"DeleteOriginalsCheck","Delete verified new/restored source images. Cloud deletions sync."},
   {"ImportSolveMode","Choose which targets to solve; requires a configured solver."},
   {"ImportRotationMode","Choose which acquisition sessions to analyse."},
   {"MetricsGrid","Measured work by stage; concurrent times can overlap."},
   {"PerformanceButton","Import timings and error diagnostics."},
   {"CancelButton","Stop safely; completed imports remain."}
  };
  void InitializeTooltips(){foreach(var entry in ControlTips){var control=Window.FindName(entry.Key) as FrameworkElement;if(control!=null){UiHelp.Tip(control,entry.Value);UiHelp.Describe(control,entry.Value);}}foreach(string name in new[]{"SearchBox","ImportSearchBox","EditedSearchBox","SourceBox","SettingsButton","LibraryFiltersButton","ImportFiltersButton","ImportGrid","FramesGrid","TargetList","DismissUpdateNotice"}){var control=Window.FindName(name) as FrameworkElement;if(control!=null)UiHelp.ClearTip(control);}UiHelp.Describe(G("FramesGrid"),"Ctrl/Shift selects files; right-click opens file actions.");UiHelp.Describe(G("ImportGrid"),"Review scan results. Row selection does not limit imports; search and filters do.");UiHelp.Describe(T("SourceBox"),"Capture source folder. Scan before importing.");}
  void UpdateSelectionTooltips(int count){string scope=count>0?count+" selected files":displayed.Count+" files in view (nothing selected)";foreach(string name in new[]{"ExportButton","EditButton","ScreenLibraryButton"}){UiHelp.Tip(B(name),scope+". "+(name=="ExportButton"?"Choose an export action.":name=="EditButton"?"Edit metadata for these files.":"Check rejection markers and file integrity."));UiHelp.Describe(B(name),scope);} }
 }
}
