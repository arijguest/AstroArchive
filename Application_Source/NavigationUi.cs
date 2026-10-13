using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroArchive {
    public partial class MainUi {
        bool navigationReady;
        bool updatingPageSelector;
        bool preparingNavigation;
        MenuItem TopMenu(string name) { return (MenuItem)Window.FindName(name); }
        IEnumerable<MenuItem> TopMenus() { return new[] { "ImportMenu", "ExportMenu", "RepositoryMenu", "SettingsMenu", "GuideMenu", "CoffeeMenu" }.Select(TopMenu); }
        void SyncPageSelector() {
            var selector = C("PageSelector"); var tabs = (TabControl)Window.FindName("MainTabs");
            updatingPageSelector = true;
            try { selector.SelectedItem = selector.Items.Cast<ComboBoxItem>().First(item => Convert.ToInt32(item.Tag) == tabs.SelectedIndex); }
            finally { updatingPageSelector = false; }
        }
        void UpdateCompactHeader() {
            var header = (FrameworkElement)Window.FindName("HeaderBar");
            double width = header.ActualWidth > 0 ? header.ActualWidth : Window.Width - 36;
            bool compact = width < 1200 || settings.TextScalePercent > 100;
            ((FrameworkElement)Window.FindName("BrandTitle")).Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            TopMenu("CoffeeMenu").Header = compact ? "_Support" : "_Buy Me a Coffee";
            foreach (var item in TopMenus()) item.Padding = new Thickness(compact ? 6 : 12, 7, compact ? 6 : 12, 7);
            var menu = (Menu)Window.FindName("MainMenu");
            var brand = (FrameworkElement)Window.FindName("BrandPanel");
            var pages = (FrameworkElement)Window.FindName("PageNavigation");
            C("PageSelector").Width = 150 * Math.Max(1, settings.TextScalePercent / 100.0);
            // Measure the unwrapped actions so the centred selector never covers a button.
            var natural = new Size(double.PositiveInfinity, double.PositiveInfinity);
            menu.Measure(natural);if(activityBell!=null)activityBell.Measure(natural);brand.Measure(natural);pages.Measure(natural);
            double sideSpace = (width - pages.DesiredSize.Width) / 2 - 12;
            bool sameRow = menu.DesiredSize.Width+(activityBell==null?0:activityBell.DesiredSize.Width) <= sideSpace && brand.DesiredSize.Width <= sideSpace;
            Grid.SetRow(pages, sameRow ? 0 : 1);
            pages.Margin = new Thickness(0, sameRow ? 0 : 2, 0, 4);
        }
        void CyclePage(int direction) {
            var selector = C("PageSelector"); selector.IsDropDownOpen = false;
            selector.SelectedIndex = (selector.SelectedIndex + direction + selector.Items.Count) % selector.Items.Count;
        }
        void SelectInitialPage() { GoToPage(all.Count == 0 ? 1 : 0); }
        void GoToPage(int index) { ((TabControl)Window.FindName("MainTabs")).SelectedIndex = index; }
        void OpenTopMenu(string name) {
            var menu = TopMenu(name);
            if (!menu.IsEnabled) return;
            preparingNavigation = true;
            try { PopulateNavigation(name); menu.Focus(); menu.IsSubmenuOpen = true; }
            finally { preparingNavigation = false; }
        }
        MenuItem MenuAction(string label, Action action, bool available = true, bool requiresIdle = true) {
            var item = new MenuItem { Header = label, IsEnabled = available && (!requiresIdle || !RepositoryOperationBlocked&&!ActiveSearchBlocked) };
            UiHelp.For(item, label);
            item.Click += (s,e) => { if (item.IsEnabled && (!requiresIdle || !RepositoryOperationBlocked&&!ActiveSearchBlocked)) action(); };
            return item;
        }
        MenuItem ButtonAction(string label, string control, int page = -1) {
            return MenuAction(label, () => {
                if (page >= 0) GoToPage(page);
                var button = B(control);
                if (button.IsEnabled) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }, B(control).IsEnabled);
        }
        static MenuItem Branch(string label, params object[] children) {
            var item = new MenuItem { Header = label };
            UiHelp.For(item, label);
            foreach (var child in children) item.Items.Add(child);
            return item;
        }
        static void MoveMenuItems(MenuItem destination, ContextMenu source, Func<object,bool> include = null) {
            foreach (var item in source.Items.Cast<object>().ToList()) {
                source.Items.Remove(item);
                if (include == null || include(item)) destination.Items.Add(item);
            }
        }
        void InitializeNavigation(bool firstRun) {
            InitializeSearch();
            T("SourceBox").TextChanged+=(s,e)=>{unknownImportTarget="";if(navigationReady)UpdateNavigationState();};
            var tabs=(TabControl)Window.FindName("MainTabs");SelectInitialPage();tabs.SelectionChanged+=(s,e)=>{if(e.OriginalSource!=tabs)return;BeginOpeningSelection(tabs.SelectedIndex);ScheduleEditedRefresh();SyncPageSelector();if(tabs.SelectedIndex!=0&&previewMotion!=null)previewMotion.Pause();if(tabs.SelectedIndex!=2&&editedMotion!=null)editedMotion.Pause();UpdateNavigationState();};
            var selector = C("PageSelector");
            selector.SelectionChanged += (s,e) => { if (!updatingPageSelector && selector.SelectedItem != null) GoToPage(Convert.ToInt32(((ComboBoxItem)selector.SelectedItem).Tag)); };
            SyncPageSelector();
            UiHelp.Hint(selector, "Switch page (Ctrl+1–3).");
            ((FrameworkElement)Window.FindName("HeaderBar")).SizeChanged += (s,e) => UpdateCompactHeader();
            UpdateCompactHeader();
            foreach (string name in new[] { "ImportMenu", "ExportMenu", "RepositoryMenu", "GuideMenu" }) {
                string captured = name;
                var menu = TopMenu(name);
                menu.GotKeyboardFocus += (s,e) => { if (!preparingNavigation && ReferenceEquals(e.NewFocus, menu) && !menu.IsSubmenuOpen) PopulateNavigation(captured); };
                menu.PreviewMouseLeftButtonDown += (s,e) => { if (!menu.IsSubmenuOpen) PopulateNavigation(captured); };
                PopulateNavigation(name);
            }
            TopMenu("SettingsMenu").Click += (sender,args) => Configure();
            UiHelp.Describe(TopMenu("SettingsMenu"), "Open Preferences.");
            TopMenu("CoffeeMenu").Click += (s,e) => OpenWebsite("https://ko-fi.com/arijguest");
            UiHelp.Hint(TopMenu("CoffeeMenu"), "Support AstroArchive on Ko-fi.");
            B("OpenRepositoryFolderButton").Click += (s,e) => OpenRepositoryFolder();
            B("OpenImportSourceButton").Click += (s,e) => { string source = T("SourceBox").Text.Trim(); if (Directory.Exists(source)) OpenFolder(source); };
            B("OpenEditedRootButton").Click += (s,e) => { if (repo != null) { Directory.CreateDirectory(repo.EditedFolder); OpenFolder(repo.EditedFolder); } };
            B("RepositoryImportButton").Click += (s,e) => GoToPage(1);
            B("ImportExportButton").Click += (s,e) => OpenExport();
            B("ImportOptionsButton").Click += (s,e) => ImportPreferences();
            B("AssignUnknownTargetButton").Click += (s,e) => AssignUnknownImportTargets();
            UiHelp.Hint(B("OpenRepositoryFolderButton"), "Open repository folder.");
            UiHelp.Describe(B("RepositoryImportButton"), "Choose a source and review imports.");
            UiHelp.Hint(B("ImportExportButton"), "Export archived files; import scanned files first.");
            Window.PreviewKeyDown += NavigationKeys;
            DescribePaneNavigation();navigationReady = true;BeginOpeningSelection(tabs.SelectedIndex);
            var originals=(CheckBox)Window.FindName("DeleteOriginalsCheck");originals.Checked+=(s,e)=>UpdateNavigationState();originals.Unchecked+=(s,e)=>UpdateNavigationState();
            UpdateNavigationState();
            if (firstRun) Window.ContentRendered += (s,e) => {
                Window.Dispatcher.BeginInvoke(new Action(() => { if (!settings.GuideSeen && !RepositoryOperationBlocked && !closing) StartWalkthrough(true); }));
            };
        }
        void NavigationKeys(object sender, KeyEventArgs e) {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (e.Key == Key.Tab) { CyclePage((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1); e.Handled = true; }
            else if (e.Key >= Key.D1 && e.Key <= Key.D3) { C("PageSelector").SelectedIndex = e.Key - Key.D1; e.Handled = true; }
            else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad3) { C("PageSelector").SelectedIndex = e.Key - Key.NumPad1; e.Handled = true; }
            else if (e.Key == Key.F) {
                GoToPage(((TabControl)Window.FindName("MainTabs")).SelectedIndex == 2 ? 2 : ((TabControl)Window.FindName("MainTabs")).SelectedIndex == 1 ? 1 : 0);
                var search = T(((TabControl)Window.FindName("MainTabs")).SelectedIndex == 2 ? "EditedSearchBox" : ((TabControl)Window.FindName("MainTabs")).SelectedIndex == 1 ? "ImportSearchBox" : "SearchBox");
                search.Focus(); search.SelectAll(); e.Handled = true;
            } else if (e.Key == Key.I) { GoToPage(1); e.Handled = true; }
            else if (e.Key == Key.E && !RepositoryOperationBlocked && repo != null) { if (((TabControl)Window.FindName("MainTabs")).SelectedIndex == 2) ExportEditedFiles(); else OpenExport(); e.Handled = true; }
        }
        void UpdateNavigationState() {
            if (!navigationReady) return;
            string scanTip=settings.RobustImportMatching?"Robust scan checks file changes and missing archive copies.":"Quick scan skips archived filenames and known DWARF sessions. Use Full rescan to check changes.";
            UiHelp.Hint(B("ScanButton"),B("ScanButton").IsEnabled?scanTip:RepositoryOperationBlocked?"Wait for the current operation to finish.":"Choose a repository first.",true);
            TopMenu("ImportMenu").IsEnabled = !NetworkImportBlocked;
            TopMenu("ExportMenu").IsEnabled = !RepositoryOperationBlocked && repo != null&&!ActiveSearchBlocked&&!SearchBlocked(((TabControl)Window.FindName("MainTabs")).SelectedIndex==2?"EditedSearchBox":"SearchBox");
            TopMenu("SettingsMenu").IsEnabled = !NetworkImportBlocked;
            B("ImportExportButton").IsEnabled = TopMenu("ExportMenu").IsEnabled;
            B("OpenRepositoryFolderButton").IsEnabled = repo != null;
            UpdatePathStrips();
            if (repo != null) {
                B("OpenRepositoryFolderButton").ToolTip = "Open repository folder\n" + repo.Root;
                AutomationProperties.SetName(B("OpenRepositoryFolderButton"), "Open active repository: " + repo.Root);
            }
            var analysis = Convert.ToString(C("ImportSolveMode").SelectedItem) != "Off";
            var cleanup = ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked == true;
            L("ImportPolicyLabel").Text=(cleanup?"Delete after import ON · Calibration kept":"Originals kept")+(analysis?" · Analysis enabled":"")+(!SkipFlagged?" · Flagged files included":"")+(unknownImportTarget.Length>0?" · Unknown → "+Catalog.Label(unknownImportTarget):"");
            UiHelp.Tip(L("ImportPolicyLabel"),(SkipFlagged?"Flagged captures excluded":"Flagged captures included")+" · "+(settings.IgnoreFailed?"Failed filenames ignored":"Failed filenames included")+" · "+(settings.RobustImportMatching?"Robust matching":"Filename matching")+(settings.IgnoreRasterImports?" · PNG/JPG/JPEG/MP4 ignored":" · PNG/JPG/JPEG/MP4 included"));
            L("ImportPolicyLabel").FontWeight = cleanup ? FontWeights.SemiBold : FontWeights.Normal;
            L("RateLabel").Visibility = RepositoryOperationBlocked ? Visibility.Visible : Visibility.Collapsed;
            ((ProgressBar)Window.FindName("ProgressBar")).Visibility = RepositoryOperationBlocked ? Visibility.Visible : Visibility.Collapsed;

        }
        void PopulateNavigation(string name) {
            var menu = TopMenu(name);
            menu.Items.Clear();
            if (name == "ImportMenu") BuildImportNavigation(menu);
            else if (name == "ExportMenu") BuildExportNavigation(menu);
            else if (name == "RepositoryMenu") BuildRepositoryNavigation(menu);
            else if (name == "GuideMenu") BuildGuideNavigation(menu);
        }
        // Import: live status, telescope sources, the scan-and-import flow, grouped tools, then preferences.
        void BuildImportNavigation(MenuItem menu) {
            if(remoteSessions.Count>0)menu.Items.Add(MenuAction("Live import underway…",OpenActivity,true,false));
            foreach(var telescope in usbTelescopes){var device=telescope;menu.Items.Add(MenuAction(UsbImportLabel(device),()=>{GoToPage(1);UploadUsb(device);},repo!=null&&!RepositoryOperationBlocked&&!usbImportPicking));}
            menu.Items.Add(MenuAction("Connect over network…",()=>{GoToPage(1);OpenRemoteImport();},repo!=null&&!NetworkImportBlocked,false));
            menu.Items.Add(MenuAction("Watch a capture folder…",()=>{GoToPage(1);OpenLocalWatch();},repo!=null&&!NetworkImportBlocked,false));
            menu.Items.Add(new Separator());
            menu.Items.Add(ButtonAction("Scan source", "ScanButton", 1));
            menu.Items.Add(MenuAction("Full rescan",()=>{GoToPage(1);Scan(true);},repo!=null));
            menu.Items.Add(ButtonAction("Import ready files", "ImportButton", 1));
            menu.Items.Add(new Separator());
            var tools=Branch("Review and repair", ButtonAction("Review flagged files…", "ReviewImportsButton", 1),ButtonAction("Screen for failures", "ScreenImportsButton", 1),ButtonAction("Retry failed imports", "RetryImportsButton", 1));
            tools.Items.Add(new Separator());MoveMenuItems(tools,BuildImportTools(),item=>item is MenuItem&&Convert.ToString(((MenuItem)item).Header)!="Scan report…");tools.IsEnabled=!SearchBlocked("ImportSearchBox");menu.Items.Add(tools);
            var telescopes=Branch("Telescopes", ButtonAction("Save current telescope…", "SaveTelescopeButton", 1), ButtonAction("Rename telescope…", "RenameTelescopeButton", 1), ButtonAction("Recover profiles", "RebuildTelescopesButton", 1));
            telescopes.Items.Add(new Separator());telescopes.Items.Add(ButtonAction("Refresh USB devices", "RefreshUsbButton"));menu.Items.Add(telescopes);
            menu.Items.Add(Branch("Dump folder",MenuAction("Open folder",()=>{repo.EnsureDumpFolder();OpenFolder(repo.DumpFolder);},repo!=null),MenuAction("Process files",ProcessDumpUi,repo!=null)));
            menu.Items.Add(Branch("Reports",MenuAction("Scan report…",()=>ShowReport("Scan report",plan==null?"Scan a folder first.":plan.ScanReport),true,false),MenuAction("Last import report…",()=>ShowReport("Import report",repo==null?"Choose a repository first.":repo.LastReport),true,false)));
            menu.Items.Add(new Separator());menu.Items.Add(MenuAction("Import preferences…",ImportPreferences,!NetworkImportBlocked,false));
        }
        void BuildExportNavigation(MenuItem menu) {
            if(((TabControl)Window.FindName("MainTabs")).SelectedIndex==2){
                var images=SelectedEditedImages();menu.Items.Add(new MenuItem{Header=EditedExportSummary(images).Title,IsEnabled=false});
                menu.Items.Add(MenuAction("Files…",ExportEditedFiles,images.Count>0));menu.Items.Add(MenuAction("Open in app…",ExportEditedTo,images.Count>0));return;
            }
            var selected=Context();var summary=ExportSummaries.Describe(selected,SelectedFiles().Count>0);var header=new MenuItem{Header=summary.Title+(summary.Targets.Length>0?" · "+summary.Targets:""),IsEnabled=false};if(summary.TargetList.Contains("\n"))UiHelp.Tip(header,summary.TargetList);menu.Items.Add(header);
            foreach(var category in new[]{ExportCategory.Files,ExportCategory.Stacking,ExportCategory.Application}){var chosen=category;menu.Items.Add(MenuAction(ExportCategoryName(chosen)+"…",()=>OpenExport(chosen,selected),ExportCategoryAvailable(chosen,selected)));}
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction(ExportCategoryName(ExportCategory.AstroBin)+"…",()=>OpenExport(ExportCategory.AstroBin,selected),ExportCategoryAvailable(ExportCategory.AstroBin,selected)));
            // The Export window's Rows choice covers the selection or the entire repository; with nothing in view it starts on the entire repository.
            menu.Items.Add(MenuAction(ExportCategoryName(ExportCategory.Catalogue)+"…",()=>OpenExport(ExportCategory.Catalogue,selected,selected.Count==0),repo!=null));
        }
        // Repository: view options, analysis, then safety and upkeep with permanent deletions kept apart.
        void BuildRepositoryNavigation(MenuItem menu) {
            menu.Items.Add(MenuAction("Filters…",()=>{GoToPage(0);ShowFilters(false);},repo!=null,false));
            var grouped=MenuAction("Group subs by session",()=>{GoToPage(0);C("LibraryViewBox").SelectedItem=Convert.ToString(C("LibraryViewBox").SelectedItem)=="Session summaries"?"Show all files":"Session summaries";},repo!=null,false);grouped.IsCheckable=true;grouped.IsChecked=Convert.ToString(C("LibraryViewBox").SelectedItem)=="Session summaries";menu.Items.Add(grouped);
            menu.Items.Add(new Separator());
            menu.Items.Add(AnalyticsMenu());
            var review=Branch("Review and analysis",ButtonAction("Review flagged files…","ReviewLibraryButton",0),ButtonAction("Screen for failures","ScreenLibraryButton",0));
            review.Items.Add(new Separator());foreach(var item in new[]{ButtonAction("Identify targets…","SolveButton",0),ButtonAction("Analyse image quality…","AnalyzeQualityButton",0),MenuAction("Re-measure image quality…",()=>{GoToPage(0);AnalyzeQuality(true);},B("AnalyzeQualityButton").IsEnabled)})review.Items.Add(item);menu.Items.Add(review);
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction("Back up archive…",BackUpArchive,repo!=null));
            var tools=BuildRepositoryTools();
            Func<string,MenuItem> tool=header=>{var item=tools.Items.OfType<MenuItem>().FirstOrDefault(m=>Convert.ToString(m.Header)==header);if(item!=null)tools.Items.Remove(item);return item;};
            var maintenance=Branch("Maintenance");var cleanUp=Branch("Clean up");
            foreach(string header in new[]{"Fill missing metadata…","Verify repository checksums","Index an existing repository","Deletion history…"}){var item=tool(header);if(item!=null)maintenance.Items.Add(item);}
            foreach(string header in new[]{"Delete failed…","Purge non-raw files…"}){var item=tool(header);if(item!=null)cleanUp.Items.Add(item);}
            if(cleanUp.Items.Count>0)cleanUp.Items.Add(new Separator());cleanUp.Items.Add(MenuAction("Delete archive data…",ResetArchive,repo!=null));
            maintenance.IsEnabled=cleanUp.IsEnabled=!RepositoryOperationBlocked&&repo!=null;menu.Items.Add(maintenance);menu.Items.Add(cleanUp);
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction("Last operation…",ShowPerformanceTable,true,false));
        }
        void BuildGuideNavigation(MenuItem menu) {
            BuildGuideItems(menu.Items);
        }
        // Each table shows the folder behind it in a centred bar; clicking opens that folder.
        void UpdatePathStrips() {
            string source = T("SourceBox").Text.Trim();
            bool sourceExists = source.Length > 0 && Directory.Exists(source);
            L("ImportPathLabel").Text = source.Length == 0 ? "Choose a source folder to scan" : source;
            B("OpenImportSourceButton").IsEnabled = sourceExists;
            B("OpenImportSourceButton").ToolTip = sourceExists ? "Open source folder\n" + source : source.Length == 0 ? "Choose a source folder to scan." : "This source folder is not currently available.";
            AutomationProperties.SetName(B("OpenImportSourceButton"), sourceExists ? "Open source folder: " + source : "Source folder unavailable");
            string edited = repo == null ? null : repo.EditedFolder;
            L("EditedPathLabel").Text = edited ?? "Choose a repository to keep edited images";
            B("OpenEditedRootButton").IsEnabled = edited != null;
            B("OpenEditedRootButton").ToolTip = edited == null ? "Choose a repository first." : "Open Edited folder\n" + edited;
            AutomationProperties.SetName(B("OpenEditedRootButton"), edited == null ? "Edited folder unavailable" : "Open Edited folder: " + edited);
        }
        void OpenRepositoryFolder() { if (repo != null) OpenFolder(repo.Root); }
        void OpenFolder(string path) {
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true }); }
            catch (Exception ex) { LocalizedMessage.Show(Window, ex.Message, "Folder could not be opened", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        void OpenWebsite(string url) {
            if(!InternetAction())return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { LocalizedMessage.Show(Window, "Visit " + url + "\n\n" + ex.Message, "Browser could not be opened", MessageBoxButton.OK, MessageBoxImage.Information); }
        }
        void ImportPreferences() { Configure(1); }
    }
}
