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
        IEnumerable<MenuItem> TopMenus() { return new[] { "ImportMenu", "ExportMenu", "RepositoryMenu", "EditedMenu", "SettingsMenu", "GuideMenu", "CoffeeMenu" }.Select(TopMenu); }
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
            menu.Measure(natural); brand.Measure(natural); pages.Measure(natural);
            double sideSpace = (width - pages.DesiredSize.Width) / 2 - 12;
            bool sameRow = menu.DesiredSize.Width <= sideSpace && brand.DesiredSize.Width <= sideSpace;
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
            var item = new MenuItem { Header = label, IsEnabled = available && (!requiresIdle || cancel == null&&!ActiveSearchBlocked) };
            UiHelp.For(item, label);
            item.Click += (s,e) => { if (item.IsEnabled && (!requiresIdle || cancel == null&&!ActiveSearchBlocked)) action(); };
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
            var tabs=(TabControl)Window.FindName("MainTabs");SelectInitialPage();tabs.SelectionChanged+=(s,e)=>{if(e.OriginalSource!=tabs)return;SyncPageSelector();if(tabs.SelectedIndex!=0&&previewMotion!=null)previewMotion.Pause();if(tabs.SelectedIndex!=2&&editedMotion!=null)editedMotion.Pause();};
            var selector = C("PageSelector");
            selector.SelectionChanged += (s,e) => { if (!updatingPageSelector && selector.SelectedItem != null) GoToPage(Convert.ToInt32(((ComboBoxItem)selector.SelectedItem).Tag)); };
            SyncPageSelector();
            UiHelp.Tip(selector, "Switch page (Ctrl+1–3).");
            ((FrameworkElement)Window.FindName("HeaderBar")).SizeChanged += (s,e) => UpdateCompactHeader();
            UpdateCompactHeader();
            foreach (string name in new[] { "ImportMenu", "ExportMenu", "RepositoryMenu", "EditedMenu", "SettingsMenu", "GuideMenu" }) {
                string captured = name;
                var menu = TopMenu(name);
                menu.GotKeyboardFocus += (s,e) => { if (!preparingNavigation && ReferenceEquals(e.NewFocus, menu) && !menu.IsSubmenuOpen) PopulateNavigation(captured); };
                menu.PreviewMouseLeftButtonDown += (s,e) => { if (!menu.IsSubmenuOpen) PopulateNavigation(captured); };
                PopulateNavigation(name);
            }
            TopMenu("CoffeeMenu").Click += (s,e) => OpenWebsite("https://ko-fi.com/arijguest");
            UiHelp.Tip(TopMenu("CoffeeMenu"), "Support AstroArchive on Ko-fi.");
            B("OpenRepositoryFolderButton").Click += (s,e) => OpenRepositoryFolder();
            B("RepositoryImportButton").Click += (s,e) => GoToPage(1);
            B("ImportExportButton").Click += (s,e) => OpenTopMenu("ExportMenu");
            B("ImportOptionsButton").Click += (s,e) => ImportPreferences();
            B("AssignUnknownTargetButton").Click += (s,e) => AssignUnknownImportTargets();
            UiHelp.Tip(B("OpenRepositoryFolderButton"), "Open repository folder.");
            UiHelp.Tip(B("RepositoryImportButton"), "Choose a source and review imports.");
            UiHelp.Tip(B("ImportExportButton"), "Export archived files; import scanned files first.");
            Window.PreviewKeyDown += NavigationKeys;
            navigationReady = true;
            UpdateNavigationState();
            if (firstRun) Window.ContentRendered += (s,e) => {
                Window.Dispatcher.BeginInvoke(new Action(() => { if (!settings.GuideSeen && cancel == null && !closing) StartWalkthrough(); }));
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
            else if (e.Key == Key.E && cancel == null && repo != null) { OpenTopMenu("ExportMenu"); e.Handled = true; }
        }
        void UpdateNavigationState() {
            if (!navigationReady) return;
            TopMenu("ImportMenu").IsEnabled = cancel == null;
            TopMenu("ExportMenu").IsEnabled = cancel == null && repo != null&&!ActiveSearchBlocked&&!SearchBlocked("SearchBox");
            TopMenu("SettingsMenu").IsEnabled = cancel == null;
            TopMenu("EditedMenu").IsEnabled = cancel == null;
            B("ImportExportButton").IsEnabled = TopMenu("ExportMenu").IsEnabled;
            B("OpenRepositoryFolderButton").IsEnabled = repo != null;
            if (repo != null) {
                B("OpenRepositoryFolderButton").ToolTip = "Open repository folder\n" + repo.Root;
                AutomationProperties.SetName(B("OpenRepositoryFolderButton"), "Open active repository: " + repo.Root);
            }
            var analysis = Convert.ToString(C("ImportSolveMode").SelectedItem) != "Off" || Convert.ToString(C("ImportRotationMode").SelectedItem) != "Off";
            var cleanup = ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked == true;
            L("ImportPolicyLabel").Text = (cleanup ? "Originals will be deleted after verified import" : "Originals kept") +
                " · " + (analysis ? "Optional analysis enabled" : "Analysis off") + " · " + (SkipFlagged ? "Flagged captures excluded" : "Flagged captures included") +
                (settings.IgnoreFailed ? " · Failed filenames ignored" : " · Failed filenames included") +
                (settings.IgnoreRasterImports ? " · PNG/JPG ignored" : "") + (unknownImportTarget.Length>0?" · Unknown → "+Catalog.Label(unknownImportTarget):"") + (importFilters.ActiveCount>0?" · "+importFilters.ActiveCount+" active filters":"");
            L("ImportPolicyLabel").FontWeight = cleanup ? FontWeights.SemiBold : FontWeights.Normal;
            L("RateLabel").Visibility = cancel != null ? Visibility.Visible : Visibility.Collapsed;
            ((ProgressBar)Window.FindName("ProgressBar")).Visibility = cancel != null ? Visibility.Visible : Visibility.Collapsed;

        }
        void PopulateNavigation(string name) {
            var menu = TopMenu(name);
            menu.Items.Clear();
            if (name == "ImportMenu") BuildImportNavigation(menu);
            else if (name == "ExportMenu") BuildExportNavigation(menu);
            else if (name == "RepositoryMenu") BuildRepositoryNavigation(menu);
            else if (name == "EditedMenu") BuildEditedNavigation(menu);
            else if (name == "SettingsMenu") BuildSettingsNavigation(menu);
            else if (name == "GuideMenu") BuildGuideNavigation(menu);
        }
        void BuildImportNavigation(MenuItem menu) {
            menu.Items.Add(MenuAction("Go to Import", () => GoToPage(1), true, false));
            menu.Items.Add(ButtonAction("Choose source folder…", "SourceButton", 1));
            menu.Items.Add(ButtonAction("Scan source folder", "ScanButton", 1));
            menu.Items.Add(MenuAction("Full rescan of source", () => { GoToPage(1); Scan(true); }, repo != null && cancel == null));
            menu.Items.Add(ButtonAction("Import ready files", "ImportButton", 1));
            var usb = Branch("USB telescopes", ButtonAction("Refresh connected devices", "RefreshUsbButton"));
            foreach (var telescope in usbTelescopes) {
                var device = telescope;
                usb.Items.Add(MenuAction((device.ProfileId ?? device.Make) + " · " + device.Source, () => { GoToPage(1); UploadUsb(device); }, repo != null));
            }
            if (usbTelescopes.Count == 0) usb.Items.Add(new MenuItem { Header = "No telescope storage detected", IsEnabled = false });
            menu.Items.Add(usb);
            menu.Items.Add(Branch("Saved telescopes", ButtonAction("Save current telescope…", "SaveTelescopeButton", 1),
                ButtonAction("Rename saved telescope…", "RenameTelescopeButton", 1), ButtonAction("Recover profiles from repository", "RebuildTelescopesButton", 1)));
            menu.Items.Add(Branch("Review and recovery", ButtonAction("Review flagged captures…", "ReviewImportsButton", 1),
                ButtonAction("Screen visible captures", "ScreenImportsButton", 1), ButtonAction("Retry failed imports", "RetryImportsButton", 1),
                MenuAction("Scan report…", () => ShowReport("Scan report", plan == null ? "Scan a folder first." : plan.ScanReport))));
            var tools = Branch("Selected files");tools.IsEnabled=!SearchBlocked("ImportSearchBox"); MoveMenuItems(tools, BuildImportTools(), item => item is MenuItem && Convert.ToString(((MenuItem)item).Header) != "Scan report…"); menu.Items.Add(tools);
            menu.Items.Add(Branch("Table", FiltersNavigation(true),
                ColumnsNavigation("ImportGrid"), MenuAction("Clear search and filters", () => B("ImportClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), repo != null)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction("Import options…", ImportPreferences));
            menu.Items.Add(Branch("Dump folder", MenuAction("Open Dump in Explorer", () => { repo.EnsureDumpFolder(); OpenFolder(repo.DumpFolder); }, repo != null),
                MenuAction("Process Dump now", ProcessDumpUi, repo != null)));
        }
        MenuItem ColumnsNavigation(string table) {
            var item = Branch("Columns"); MoveMenuItems(item, BuildColumnsMenu(table, null)); return item;
        }
        MenuItem FiltersNavigation(bool imports) {
            var item = Branch("Filters"); MoveMenuItems(item, BuildFiltersMenu(imports)); return item;
        }
        void BuildExportNavigation(MenuItem menu) {
            var selected = Context();
            menu.Items.Add(new MenuItem { Header = selected.Count + " repository files" + (SelectedFiles().Count == 0 ? " in view" : " selected"), IsEnabled = false });
            var choices = ExportMenu(selected);
            foreach (var child in choices.Items.Cast<object>().ToList()) { choices.Items.Remove(child); menu.Items.Add(child); }
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction("Selection catalogue CSV…", () => ExportSelectionCsv(selected), selected.Count > 0));
            menu.Items.Add(MenuAction("Complete repository catalogue CSV…", ExportCatalogue, repo != null));
        }
        void BuildRepositoryNavigation(MenuItem menu) {
            menu.Items.Add(MenuAction("Browse repository", () => GoToPage(0), true, false));
            menu.Items.Add(MenuAction("Choose repository folder…", ChooseRepository));
            menu.Items.Add(MenuAction("Open repository in Explorer", OpenRepositoryFolder, repo != null, false));
            var view = Branch("View", FiltersNavigation(false), ColumnsNavigation("FramesGrid"));
            foreach (string label in new[] { "Session summaries", "Show all files", "By target", "By target and session" }) {
                string mode = label; var choice = MenuAction(mode, () => { GoToPage(0); C("LibraryViewBox").SelectedItem = mode; }, repo != null);
                choice.IsCheckable = true; choice.IsChecked = Convert.ToString(C("LibraryViewBox").SelectedItem) == mode; view.Items.Add(choice);
            }
            var preview = MenuAction("Image preview pane", () => B("PreviewToggle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), true, false);
            preview.IsCheckable = true; preview.IsChecked = settings.ShowPreview; view.Items.Add(preview);
            view.Items.Add(MenuAction("Open an external image…", OpenPreviewFile, true, false));
            view.Items.Add(MenuAction("Clear search and filters", () => B("ClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)))); menu.Items.Add(view);
            var selection = Branch("Selected files");selection.IsEnabled=cancel==null&&!SearchBlocked("SearchBox");
            var files = SelectedFiles();
            if (files.Count > 0) { var context = ThemedMenu(); BuildFileMenu(context, files); MoveMenuItems(selection, context); }
            else selection.Items.Add(new MenuItem { Header = "Select files in the repository table", IsEnabled = false });
            menu.Items.Add(selection);
            menu.Items.Add(Branch("Review and analysis", ButtonAction("Review flagged captures…", "ReviewLibraryButton", 0),
                ButtonAction("Screen selected or visible captures", "ScreenLibraryButton", 0), ButtonAction("Rotation analysis…", "RotationButton", 0),
                ButtonAction("Identify targets…", "SolveButton", 0), ButtonAction("Edit metadata…", "EditButton", 0)));
            var maintenance = Branch("Maintenance");maintenance.IsEnabled=cancel==null&&repo!=null;
            if (repo != null) MoveMenuItems(maintenance, BuildRepositoryTools(), item => !(item is MenuItem) || Convert.ToString(((MenuItem)item).Header) != "Export searchable catalogue CSV");
            maintenance.Items.Add(new Separator()); maintenance.Items.Add(MenuAction("Delete all archive data…", ResetArchive, repo != null)); menu.Items.Add(maintenance);
            var performance = MenuAction("Show performance table…", ShowPerformanceTable, true, false);
            menu.Items.Add(Branch("Diagnostics", performance, MenuAction("Last import report…", () => ShowReport("Import performance and errors", repo == null ? "Choose a repository first." : repo.LastReport), true, false)));
        }
        void BuildSettingsNavigation(MenuItem menu) {
            menu.Items.Add(MenuAction("Preferences…", () => Configure(0)));
            menu.Items.Add(MenuAction("Accessibility…", () => Configure(4)));
            menu.Items.Add(Branch("Import and processing", MenuAction("Copy workers and observing site…", () => Configure(1)), MenuAction("Import options…", ImportPreferences)));
            menu.Items.Add(MenuAction("Plate solving…", () => Configure(2)));
            menu.Items.Add(MenuAction("Repository settings…", () => Configure(3)));
            menu.Items.Add(Branch("Image compatibility", MenuAction("Supported formats and conversion…", () => ShowReport("Image compatibility", FormatGuide)),
                MenuAction("Open optional codec folder", () => { Directory.CreateDirectory(NativeCodecs.Folder); OpenFolder(NativeCodecs.Folder); })));
            menu.Items.Add(new Separator()); menu.Items.Add(MenuAction("Check for and install releases…", () => Releases(Window)));
        }
        void BuildGuideNavigation(MenuItem menu) {
            menu.Items.Add(MenuAction("Interactive walkthrough…", StartWalkthrough, cancel == null));
            menu.Items.Add(MenuAction("Search the guide…", () => OpenGuide(null), true, false));
            menu.Items.Add(MenuAction("Help for this page (F1)", () => OpenGuide(CurrentHelpTopic()), true, false));
            var topics = Branch("Topics");
            foreach (var entry in new[] {
                new[] { "Getting started", "START HERE" }, new[] { "Importing captures", "IMPORT WORKFLOW" },
                new[] { "Preview and tables", "IMAGE PREVIEW AND TABLES" }, new[] { "Stacking projects", "STACKING PROJECTS AND SESSIONS" },
                 new[] { "Troubleshooting", "TROUBLESHOOTING" },
                new[] { "Keyboard shortcuts", "KEYBOARD SHORTCUTS" }
            }) { string key = entry[1]; topics.Items.Add(MenuAction(entry[0], () => OpenGuide(key), true, false)); }
            menu.Items.Add(topics); menu.Items.Add(new Separator()); menu.Items.Add(MenuAction("About AstroArchive…", About, true, false));
        }
        void ChooseRepository() {
            string selected = Folder("Choose your repository folder", repo == null ? settings.Repository : repo.Root);
            if (selected == null) return;
            try { OpenRepository(selected); UpdateNavigationState(); }
            catch (Exception ex) { MessageBox.Show(Window, ex.Message, "Repository could not be opened", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        void OpenRepositoryFolder() { if (repo != null) OpenFolder(repo.Root); }
        void OpenFolder(string path) {
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(Window, ex.Message, "Folder could not be opened", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        void OpenWebsite(string url) {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(Window, "Visit " + url + "\n\n" + ex.Message, "Browser could not be opened", MessageBoxButton.OK, MessageBoxImage.Information); }
        }
        void ExportCatalogue() {
            var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "AstroArchive_catalog.csv", Filter = "CSV catalogue|*.csv" };
            if (dialog.ShowDialog(Window) == true) { repo.ExportIndex(dialog.FileName); L("StatusLabel").Text = "Catalogue CSV exported."; }
        }
        void ImportPreferences() {
            var dialog = new FormWindow(Window, "Import options", 640, 690);
            dialog.Tabs("Files", "Capture", "Analysis");
            dialog.Text("Options for the next import", true);
            dialog.Text("Review the policy summary before copying. Filename and format exclusions are saved for folder, USB and Dump imports; changing them requires a new scan.");
            CheckBox flagged,failed,raster,originals;AddImportPolicyControls(dialog,out flagged,out failed,out raster,out originals);
            dialog.Text("Original removal applies only to newly imported, verified files. Scanning another source resets it. Cloud-synced source deletions propagate.");
            dialog.Tab(1);
            var model = dialog.Select("Instrument model", TelescopeProfiles.Models.ToArray(), Convert.ToString(C("ModelBox").SelectedItem));
            var camera = dialog.Select("Camera channel", new[] { "Auto", "Telephoto", "Wide" }, Convert.ToString(C("CameraBox").SelectedItem));
            var target=ImportTargetChoice(dialog,unknownImportTarget);
            dialog.Text("This target fills Unknown lights/stacks in the current folder scan and next manual import. Known targets, meteor captures and calibration labels stay intact. For mixed targets, use Set Unknown targets on selected scan rows instead.");
            var targetError=new TextBlock{TextWrapping=TextWrapping.Wrap};dialog.Add(targetError);
            dialog.Tab(2);
            var solve = dialog.Select("Target analysis", new[] { "Off", "Ambiguous only", "All light/stack files" }, Convert.ToString(C("ImportSolveMode").SelectedItem));
            var rotation = dialog.Select("Rotation analysis", new[] { "Off", "Ambiguous mounts", "All light sessions" }, Convert.ToString(C("ImportRotationMode").SelectedItem));
            dialog.Text("Analysis is optional and off by default. Plate solving needs a configured solver; rotation analysis needs suitable capture times and location.");
            dialog.SelectTab(0);
            dialog.Accept("Apply import options", () => {bool valid=ValidImportTarget(target.Text,targetError,true);if(!valid)dialog.SelectTab(1);return valid;});
            if (!dialog.Show()) return;
            bool rescan = !Equals(model.SelectedItem, C("ModelBox").SelectedItem) || !Equals(camera.SelectedItem, C("CameraBox").SelectedItem) || failed.IsChecked != ((CheckBox)Window.FindName("IgnoreFailedCheck")).IsChecked || settings.IgnoreRasterImports != (raster.IsChecked==true);
            unknownImportTarget=string.IsNullOrWhiteSpace(target.Text)?"":ImportPolicy.Target(target.Text);
            settings.IgnoreRasterImports=raster.IsChecked==true;SaveSettings();
            C("ModelBox").SelectedItem = model.SelectedItem; C("CameraBox").SelectedItem = camera.SelectedItem;
            C("ImportSolveMode").SelectedItem = solve.SelectedItem; C("ImportRotationMode").SelectedItem = rotation.SelectedItem;
            ((CheckBox)Window.FindName("SkipFlaggedCheck")).IsChecked = flagged.IsChecked;
            ((CheckBox)Window.FindName("IgnoreFailedCheck")).IsChecked = failed.IsChecked;
            if (rescan) InvalidateImportPlan();
            else if(plan!=null&&unknownImportTarget.Length>0)ImportPolicy.AssignUnknown(plan.Frames,unknownImportTarget);
            ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked = !rescan && originals.IsEnabled && originals.IsChecked == true;
            FilterImports();UpdateNavigationState();
        }
    }
}
