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
            var tabs=(TabControl)Window.FindName("MainTabs");SelectInitialPage();tabs.SelectionChanged+=(s,e)=>{if(e.OriginalSource!=tabs)return;ScheduleEditedRefresh();SyncPageSelector();if(tabs.SelectedIndex!=0&&previewMotion!=null)previewMotion.Pause();if(tabs.SelectedIndex!=2&&editedMotion!=null)editedMotion.Pause();UpdateNavigationState();};
            var selector = C("PageSelector");
            selector.SelectionChanged += (s,e) => { if (!updatingPageSelector && selector.SelectedItem != null) GoToPage(Convert.ToInt32(((ComboBoxItem)selector.SelectedItem).Tag)); };
            SyncPageSelector();
            UiHelp.Tip(selector, "Switch page (Ctrl+1–3).");
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
            UiHelp.Tip(TopMenu("SettingsMenu"), "Open Preferences.");
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
            TopMenu("ExportMenu").IsEnabled = cancel == null && repo != null&&!ActiveSearchBlocked&&!SearchBlocked(((TabControl)Window.FindName("MainTabs")).SelectedIndex==2?"EditedSearchBox":"SearchBox");
            TopMenu("SettingsMenu").IsEnabled = cancel == null;
            B("ImportExportButton").IsEnabled = TopMenu("ExportMenu").IsEnabled;
            B("OpenRepositoryFolderButton").IsEnabled = repo != null;
            if (repo != null) {
                B("OpenRepositoryFolderButton").ToolTip = "Open repository folder\n" + repo.Root;
                AutomationProperties.SetName(B("OpenRepositoryFolderButton"), "Open active repository: " + repo.Root);
            }
            var analysis = Convert.ToString(C("ImportSolveMode").SelectedItem) != "Off" || Convert.ToString(C("ImportRotationMode").SelectedItem) != "Off";
            var cleanup = ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked == true;
            L("ImportPolicyLabel").Text=(cleanup?"Originals deleted after verification":"Originals kept")+(analysis?" · Analysis enabled":"")+(!SkipFlagged?" · Flagged files included":"")+(unknownImportTarget.Length>0?" · Unknown → "+Catalog.Label(unknownImportTarget):"");
            UiHelp.Tip(L("ImportPolicyLabel"),(SkipFlagged?"Flagged captures excluded":"Flagged captures included")+" · "+(settings.IgnoreFailed?"Failed filenames ignored":"Failed filenames included")+" · "+(settings.RobustImportMatching?"Robust matching":"Filename matching")+(settings.IgnoreRasterImports?" · PNG/JPG/JPEG ignored":" · PNG/JPG/JPEG included"));
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
            else if (name == "GuideMenu") BuildGuideNavigation(menu);
        }
        void BuildImportNavigation(MenuItem menu) {
            menu.Items.Add(ButtonAction("Scan source", "ScanButton", 1));
            menu.Items.Add(ButtonAction("Import ready files", "ImportButton", 1));
            var usb = Branch("Connected telescopes", ButtonAction("Refresh devices", "RefreshUsbButton"));
            foreach (var telescope in usbTelescopes) { var device=telescope;usb.Items.Add(MenuAction((device.ProfileId??device.Make)+" · "+device.Source,()=>{GoToPage(1);UploadUsb(device);},repo!=null)); }
            if(usbTelescopes.Count==0)usb.Items.Add(new MenuItem{Header="No telescope storage detected",IsEnabled=false});menu.Items.Add(usb);
            menu.Items.Add(Branch("Saved telescopes", ButtonAction("Save current telescope…", "SaveTelescopeButton", 1), ButtonAction("Rename telescope…", "RenameTelescopeButton", 1), ButtonAction("Recover profiles", "RebuildTelescopesButton", 1)));
            var tools=Branch("Review and repair", ButtonAction("Review flagged files…", "ReviewImportsButton", 1),ButtonAction("Screen files", "ScreenImportsButton", 1),ButtonAction("Retry failed imports", "RetryImportsButton", 1),MenuAction("Full rescan",()=>{GoToPage(1);Scan(true);},repo!=null),MenuAction("Scan report…",()=>ShowReport("Scan report",plan==null?"Scan a folder first.":plan.ScanReport)));
            tools.Items.Add(new Separator());MoveMenuItems(tools,BuildImportTools(),item=>item is MenuItem&&Convert.ToString(((MenuItem)item).Header)!="Scan report…");tools.IsEnabled=!SearchBlocked("ImportSearchBox");menu.Items.Add(tools);
            menu.Items.Add(Branch("Dump folder",MenuAction("Open folder",()=>{repo.EnsureDumpFolder();OpenFolder(repo.DumpFolder);},repo!=null),MenuAction("Process files",ProcessDumpUi,repo!=null)));
            menu.Items.Add(new Separator());menu.Items.Add(MenuAction("Import preferences…",ImportPreferences));
        }
        void BuildExportNavigation(MenuItem menu) {
            if(((TabControl)Window.FindName("MainTabs")).SelectedIndex==2){menu.Items.Add(MenuAction("Export to…",ExportEditedTo,SelectedEditedImages().Count>0));return;}
            var selected=Context();menu.Items.Add(new MenuItem{Header=selected.Count+" files"+(SelectedFiles().Count==0?" in view":" selected"),IsEnabled=false});
            menu.Items.Add(MenuAction("Export to…",()=>ExportTo(selected),selected.Count>0));menu.Items.Add(MenuAction("Export files…",()=>ExportFiles(selected),selected.Count>0));menu.Items.Add(MenuAction("Stacking folder…",()=>ExportProject(selected,false),selected.Any(f=>f.Kind=="Light"||f.Kind=="Stack")));
            menu.Items.Add(new Separator());menu.Items.Add(Branch("Catalogue CSV",MenuAction("Selected / visible files…",()=>ExportSelectionCsv(selected),selected.Count>0),MenuAction("Entire repository…",ExportCatalogue,repo!=null)));
        }
        void BuildRepositoryNavigation(MenuItem menu) {
            var grouped=MenuAction("Group subs by session",()=>{GoToPage(0);C("LibraryViewBox").SelectedItem=Convert.ToString(C("LibraryViewBox").SelectedItem)=="Session summaries"?"Show all files":"Session summaries";},repo!=null);grouped.IsCheckable=true;grouped.IsChecked=Convert.ToString(C("LibraryViewBox").SelectedItem)=="Session summaries";menu.Items.Add(grouped);
            menu.Items.Add(MenuAction("Filters…",()=>{GoToPage(0);ShowFilters(false);},repo!=null));
            menu.Items.Add(MenuAction("Back up archive…",BackUpArchive,repo!=null));
            menu.Items.Add(new Separator());menu.Items.Add(Branch("Review and analysis",ButtonAction("Review flagged files…","ReviewLibraryButton",0),ButtonAction("Screen files","ScreenLibraryButton",0),ButtonAction("Identify targets…","SolveButton",0),ButtonAction("Analyse rotation…","RotationButton",0)));
            var maintenance=Branch("Maintenance");maintenance.IsEnabled=cancel==null&&repo!=null;if(repo!=null)MoveMenuItems(maintenance,BuildRepositoryTools(),item=>item is MenuItem&&Convert.ToString(((MenuItem)item).Header)!="Export searchable catalogue CSV"&&Convert.ToString(((MenuItem)item).Header)!="Show selected file location");maintenance.Items.Add(new Separator());maintenance.Items.Add(MenuAction("Delete archive data…",ResetArchive,repo!=null));menu.Items.Add(maintenance);
            menu.Items.Add(Branch("Diagnostics",MenuAction("Last operation…",ShowPerformanceTable,true,false),MenuAction("Last import report…",()=>ShowReport("Import report",repo==null?"Choose a repository first.":repo.LastReport),true,false)));
        }
        void BuildGuideNavigation(MenuItem menu) {
            menu.Items.Add(MenuAction("Help…",()=>OpenGuide(CurrentHelpTopic()),true,false));menu.Items.Add(MenuAction("Interactive walkthrough…",StartWalkthrough,cancel==null));menu.Items.Add(new Separator());menu.Items.Add(MenuAction("About AstroArchive…",About,true,false));
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
        void ImportPreferences() { Configure(1); }
    }
}
