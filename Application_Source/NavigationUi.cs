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
        bool preparingNavigation;
        MenuItem TopMenu(string name) { return (MenuItem)Window.FindName(name); }
        void GoToPage(int index) { ((TabControl)Window.FindName("MainTabs")).SelectedIndex = index; }
        void OpenTopMenu(string name) {
            var menu = TopMenu(name);
            if (!menu.IsEnabled) return;
            preparingNavigation = true;
            try { PopulateNavigation(name); menu.Focus(); menu.IsSubmenuOpen = true; }
            finally { preparingNavigation = false; }
        }
        MenuItem MenuAction(string label, Action action, bool available = true, bool requiresIdle = true) {
            var item = new MenuItem { Header = label, IsEnabled = available && (!requiresIdle || cancel == null) };
            UiHelp.For(item, label);
            item.Click += (s,e) => { if (item.IsEnabled && (!requiresIdle || cancel == null)) action(); };
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
            foreach (string name in new[] { "ImportMenu", "ExportMenu", "RepositoryMenu", "SettingsMenu", "GuideMenu" }) {
                string captured = name;
                var menu = TopMenu(name);
                menu.GotKeyboardFocus += (s,e) => { if (!preparingNavigation && ReferenceEquals(e.NewFocus, menu) && !menu.IsSubmenuOpen) PopulateNavigation(captured); };
                menu.PreviewMouseLeftButtonDown += (s,e) => { if (!menu.IsSubmenuOpen) PopulateNavigation(captured); };
                PopulateNavigation(name);
            }
            TopMenu("CoffeeMenu").Click += (s,e) => OpenWebsite("https://ko-fi.com/arijguest");
            UiHelp.Tip(TopMenu("CoffeeMenu"), "Support AstroArchive and Ari J. Guest on Ko-fi. Opens your browser.");
            B("OpenRepositoryFolderButton").Click += (s,e) => OpenRepositoryFolder();
            B("RepositoryImportButton").Click += (s,e) => GoToPage(1);
            B("MosaicImportButton").Click += (s,e) => GoToPage(1);
            B("ImportExportButton").Click += (s,e) => OpenTopMenu("ExportMenu");
            UiHelp.Tip(B("OpenRepositoryFolderButton"), "Open the active repository in File Explorer. The full path is shown in its tooltip.");
            UiHelp.Tip(B("RepositoryImportButton"), "Go to Import to select a source folder and review new captures.");
            UiHelp.Tip(B("MosaicImportButton"), "Go to Import to bring new captures into this repository.");
            UiHelp.Tip(B("ImportExportButton"), "Export archived repository files. Scanned source files must be imported first.");
            foreach (string name in new[] { "ImportMenu", "ExportMenu", "RepositoryMenu", "SettingsMenu", "GuideMenu" })
                UiHelp.Tip(TopMenu(name), name.Replace("Menu", "") + " actions. Press Alt to reveal menu access keys; arrow keys navigate the menu.");
            Window.PreviewKeyDown += NavigationKeys;
            navigationReady = true;
            UpdateNavigationState();
            if (firstRun) Window.ContentRendered += (s,e) => {
                Window.Dispatcher.BeginInvoke(new Action(() => { if (!settings.GuideSeen && cancel == null && !closing) StartWalkthrough(); }));
            };
        }
        void NavigationKeys(object sender, KeyEventArgs e) {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (e.Key == Key.F) {
                GoToPage(((TabControl)Window.FindName("MainTabs")).SelectedIndex == 1 ? 1 : 0);
                var search = T(((TabControl)Window.FindName("MainTabs")).SelectedIndex == 1 ? "ImportSearchBox" : "SearchBox");
                search.Focus(); search.SelectAll(); e.Handled = true;
            } else if (e.Key == Key.I) { GoToPage(1); e.Handled = true; }
            else if (e.Key == Key.E && cancel == null && repo != null) { OpenTopMenu("ExportMenu"); e.Handled = true; }
        }
        void UpdateNavigationState() {
            if (!navigationReady) return;
            TopMenu("ImportMenu").IsEnabled = cancel == null;
            TopMenu("ExportMenu").IsEnabled = cancel == null && repo != null;
            TopMenu("SettingsMenu").IsEnabled = cancel == null;
            B("ImportExportButton").IsEnabled = TopMenu("ExportMenu").IsEnabled;
            B("OpenRepositoryFolderButton").IsEnabled = repo != null;
            if (repo != null) {
                B("OpenRepositoryFolderButton").ToolTip = "Open repository in File Explorer\n" + repo.Root;
                AutomationProperties.SetName(B("OpenRepositoryFolderButton"), "Open active repository: " + repo.Root);
            }
            var analysis = Convert.ToString(C("ImportSolveMode").SelectedItem) != "Off" || Convert.ToString(C("ImportRotationMode").SelectedItem) != "Off";
            var cleanup = ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked == true;
            L("ImportPolicyLabel").Text = (cleanup ? "Originals will be deleted after verified import" : "Originals kept") +
                " · " + (analysis ? "Optional analysis enabled" : "Analysis off") + " · " + (SkipFlagged ? "Flagged captures excluded" : "Flagged captures included") +
                (settings.IgnoreFailed ? " · Failed filenames ignored" : "")+(importFilters.Values.Count>0?" · "+importFilters.Values.Count+" active filters":"");
            L("ImportPolicyLabel").FontWeight = cleanup ? FontWeights.SemiBold : FontWeights.Normal;
            L("RateLabel").Visibility = cancel != null ? Visibility.Visible : Visibility.Collapsed;
            ((ProgressBar)Window.FindName("ProgressBar")).Visibility = cancel != null ? Visibility.Visible : Visibility.Collapsed;
            ((ColumnDefinition)Window.FindName("StatusProgressColumn")).Width = new GridLength(cancel != null ? 240 : 0);
        }
        void PopulateNavigation(string name) {
            var menu = TopMenu(name);
            menu.Items.Clear();
            if (name == "ImportMenu") BuildImportNavigation(menu);
            else if (name == "ExportMenu") BuildExportNavigation(menu);
            else if (name == "RepositoryMenu") BuildRepositoryNavigation(menu);
            else if (name == "SettingsMenu") BuildSettingsNavigation(menu);
            else if (name == "GuideMenu") BuildGuideNavigation(menu);
        }
        void BuildImportNavigation(MenuItem menu) {
            menu.Items.Add(MenuAction("Go to Import", () => GoToPage(1), true, false));
            menu.Items.Add(ButtonAction("Choose source folder…", "SourceButton", 1));
            menu.Items.Add(ButtonAction("Scan source folder", "ScanButton", 1));
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
                MenuAction("Scan report…", () => ShowReport("Scan report", plan == null ? "Scan a folder first." : plan.Errors.Count == 0 ? "All supported files were read successfully." : string.Join("\r\n\r\n", plan.Errors)))));
            var tools = Branch("Selected files"); MoveMenuItems(tools, BuildImportTools(), item => item is MenuItem && Convert.ToString(((MenuItem)item).Header) != "Scan report…"); menu.Items.Add(tools);
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
            menu.Items.Add(ButtonAction("Export active mosaic collection…", "MosaicExportButton"));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction("Selection catalogue CSV…", () => ExportSelectionCsv(selected), selected.Count > 0));
            menu.Items.Add(MenuAction("Complete repository catalogue CSV…", ExportCatalogue, repo != null));
        }
        void BuildRepositoryNavigation(MenuItem menu) {
            menu.Items.Add(MenuAction("Browse repository", () => GoToPage(0), true, false));
            menu.Items.Add(MenuAction("Choose repository folder…", ChooseRepository));
            menu.Items.Add(MenuAction("Open repository in Explorer", OpenRepositoryFolder, repo != null, false));
            var view = Branch("View", FiltersNavigation(false), ColumnsNavigation("FramesGrid"));
            foreach (string label in new[] { "Files", "By target", "By target and session" }) {
                string mode = label; var choice = MenuAction(mode, () => { GoToPage(0); C("LibraryViewBox").SelectedItem = mode; }, repo != null);
                choice.IsCheckable = true; choice.IsChecked = Convert.ToString(C("LibraryViewBox").SelectedItem) == mode; view.Items.Add(choice);
            }
            var preview = MenuAction("Image preview pane", () => B("PreviewToggle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), true, false);
            preview.IsCheckable = true; preview.IsChecked = settings.ShowPreview; view.Items.Add(preview);
            view.Items.Add(MenuAction("Open an external image…", OpenPreviewFile, true, false));
            view.Items.Add(MenuAction("Clear search and filters", () => B("ClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)))); menu.Items.Add(view);
            var selection = Branch("Selected files");selection.IsEnabled=cancel==null;
            var files = SelectedFiles();
            if (files.Count > 0) { var context = ThemedMenu(); BuildFileMenu(context, files); MoveMenuItems(selection, context); }
            else selection.Items.Add(new MenuItem { Header = "Select files in the repository table", IsEnabled = false });
            menu.Items.Add(selection);
            menu.Items.Add(Branch("Review and analysis", ButtonAction("Review flagged captures…", "ReviewLibraryButton", 0),
                ButtonAction("Screen selected or visible captures", "ScreenLibraryButton", 0), ButtonAction("Rotation analysis…", "RotationButton", 0),
                ButtonAction("Identify targets…", "SolveButton", 0), ButtonAction("Edit metadata…", "EditButton", 0)));
            var mosaics = Branch("Mosaic collections", MenuAction("Browse mosaics", () => GoToPage(2), true, false),
                ButtonAction("New collection…", "NewMosaicButton", 2), ButtonAction("Detect from metadata", "DetectMosaicsButton", 2),
                ButtonAction("Assign repository selection…", "AssignMosaicButton"), ButtonAction("Confirm selected memberships", "MosaicConfirmButton", 2),
                ButtonAction("Remove selected memberships", "MosaicRemoveButton", 2));
            if (repo != null) MoveMenuItems(mosaics, BuildMosaicTools()); menu.Items.Add(mosaics);
            var maintenance = Branch("Maintenance");maintenance.IsEnabled=cancel==null&&repo!=null;
            if (repo != null) MoveMenuItems(maintenance, BuildRepositoryTools(), item => !(item is MenuItem) || Convert.ToString(((MenuItem)item).Header) != "Export searchable catalogue CSV");
            maintenance.Items.Add(new Separator()); maintenance.Items.Add(MenuAction("Delete all archive data…", ResetArchive, repo != null)); menu.Items.Add(maintenance);
            var performance = MenuAction("Show performance table", () => {
                var panel = (Expander)Window.FindName("PerformanceDetails");
                panel.Visibility = panel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                panel.IsExpanded = panel.Visibility == Visibility.Visible;
            }, true, false);
            performance.IsCheckable = true; performance.IsChecked = ((Expander)Window.FindName("PerformanceDetails")).Visibility == Visibility.Visible;
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
                new[] { "Mosaic collections", "MOSAIC COLLECTIONS AND PANELS" }, new[] { "Troubleshooting", "TROUBLESHOOTING" },
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
            dialog.Text("Options for the next import", true);
            dialog.Text("Review the policy summary on the Import page before copying. Changing model or camera requires a new scan.");
            var model = dialog.Select("Instrument model", TelescopeProfiles.Models.ToArray(), Convert.ToString(C("ModelBox").SelectedItem));
            var camera = dialog.Select("Camera channel", new[] { "Auto", "Telephoto", "Wide" }, Convert.ToString(C("CameraBox").SelectedItem));
            var solve = dialog.Select("Target analysis", new[] { "Off", "Ambiguous only", "All light/stack files" }, Convert.ToString(C("ImportSolveMode").SelectedItem));
            var rotation = dialog.Select("Rotation analysis", new[] { "Off", "Ambiguous mounts", "All light sessions" }, Convert.ToString(C("ImportRotationMode").SelectedItem));
            var flagged = dialog.Check("Skip flagged captures", SkipFlagged);
            var failed = dialog.Check("Ignore failed filenames", settings.IgnoreFailed);
            var originals = dialog.Check("Delete originals after verified import", ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked == true);
            originals.IsEnabled = ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsEnabled;
            dialog.Text("Original removal applies only to newly imported, verified files. Scanning another source resets it. Cloud-synced source deletions propagate.");
            dialog.Accept("Apply import options", () => true);
            if (!dialog.Show()) return;
            bool rescan = !Equals(model.SelectedItem, C("ModelBox").SelectedItem) || !Equals(camera.SelectedItem, C("CameraBox").SelectedItem);
            C("ModelBox").SelectedItem = model.SelectedItem; C("CameraBox").SelectedItem = camera.SelectedItem;
            C("ImportSolveMode").SelectedItem = solve.SelectedItem; C("ImportRotationMode").SelectedItem = rotation.SelectedItem;
            ((CheckBox)Window.FindName("SkipFlaggedCheck")).IsChecked = flagged.IsChecked;
            ((CheckBox)Window.FindName("IgnoreFailedCheck")).IsChecked = failed.IsChecked;
            if (rescan) InvalidateImportPlan();
            ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked = !rescan && originals.IsEnabled && originals.IsChecked == true;
            UpdateNavigationState();
        }
    }
}
