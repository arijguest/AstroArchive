// Windows-only checks for actual WPF menu, focus, sorting and accessibility behavior.
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace AstroArchive {
    public partial class MainUi {
        void SmokeNavigation(string output) {
            var tabs = (TabControl)Window.FindName("MainTabs");
            int page = tabs.SelectedIndex;
            var saved = Util.Serialize(settings);
            try {
                GoToPage(0); Window.UpdateLayout();
                var menu = (Menu)Window.FindName("MainMenu");
                string[] expected = { "Import", "Export", "Repository", "Settings", "Guide", "Buy Me a Coffee" };
                if (!menu.Items.Cast<MenuItem>().Select(item => Convert.ToString(item.Header).Replace("_", "")).SequenceEqual(expected)) throw new Exception("Top-level menu order changed.");
                if (Convert.ToString(((TabItem)tabs.Items[0]).Header) != "Repository") throw new Exception("Repository tab retains the Library label.");
                if (B("ThemeButton").IsVisible || B("MoreButton").IsVisible || B("PerformanceButton").IsVisible || B("RotationButton").IsVisible) throw new Exception("Secondary action buttons remain visible.");
                if (!B("ExportButton").IsVisible || !B("RepositoryImportButton").IsVisible) throw new Exception("Primary repository actions are missing.");
                var brand = (FrameworkElement)Window.FindName("BrandPanel");
                var path = B("OpenRepositoryFolderButton");
                var position = brand.TranslatePoint(new Point(), Window);
                if (position.X < Window.ActualWidth / 2 || path.TranslatePoint(new Point(), Window).Y >= menu.TranslatePoint(new Point(), Window).Y) throw new Exception("Brand/path layout did not move to the right and above navigation.");
                foreach (string mode in new[] { "Light", "Dark" }) {
                    settings.ThemeMode = mode; ApplyAppearance();
                    OpenTopMenu("RepositoryMenu"); PumpPopupLayout();
                    var root = TopMenu("RepositoryMenu");
                    if (!root.IsSubmenuOpen || root.ActualWidth == 0) throw new Exception("Repository menu failed to open.");
                    foreach (var item in root.Items.OfType<MenuItem>()) {
                        item.ApplyTemplate();
                        Readable(item.Foreground, (Brush)Window.FindResource("Surface"), mode + " navigation menu");
                    }
                    var view = root.Items.OfType<MenuItem>().Single(item => Convert.ToString(item.Header) == "View");
                    var filters = view.Items.OfType<MenuItem>().Single(item => Convert.ToString(item.Header) == "Filters");
                    view.IsSubmenuOpen = true; filters.IsSubmenuOpen = true; PumpPopupLayout();
                    if (!filters.IsSubmenuOpen || filters.Items.OfType<MenuItem>().Count() < 10) throw new Exception("Repository filter tree is missing.");
                    filters.IsSubmenuOpen = false; view.IsSubmenuOpen = false;
                    root.IsSubmenuOpen = false;
                    Capture(Path.Combine(output, "AstroArchive_Navigation_" + mode + ".png"));
                }
                var grid = G("FramesGrid"); var exposure = grid.Columns.First(column => column.SortMemberPath == "Exposure");
                var before = tableSorts["FramesGrid"].ToList();
                try {
                    SortTable("FramesGrid", exposure, false);
                    var kind = grid.Columns.First(column => column.SortMemberPath == "Kind"); SortTable("FramesGrid", kind, true);
                    if(grid.Items.Count>0)grid.ScrollIntoView(grid.Items[0],exposure);Window.UpdateLayout();PumpPopupLayout();
                    if (TableSortIndicators.GetMark(exposure) != "▲ 1" || TableSortIndicators.GetMark(kind) != "▲ 2") throw new Exception("Sort arrows or priorities are missing.");
                    var rendered = PopupChildren<DataGridColumnHeader>(grid).FirstOrDefault(header => header.Column == exposure);
                    if (rendered == null || !PopupChildren<TextBlock>(rendered).Any(text => text.Text == "▲ 1")) throw new Exception("Sort indicator did not render in the header.");
                    if (!AutomationProperties.GetName(rendered).Contains("ascending") || !AutomationProperties.GetName(rendered).Contains("priority 1")) throw new Exception("Sorted header lacks an accessible description.");
                    Capture(Path.Combine(output, "AstroArchive_Sorted_UI.png"));
                    SortTable("FramesGrid", exposure, true); if (TableSortIndicators.GetMark(exposure) != "▼ 1") throw new Exception("Descending sort marker did not reverse.");
                } finally { tableSorts["FramesGrid"] = before; RestoreTableSort("FramesGrid");if(grid.Items.Count>0)grid.ScrollIntoView(grid.Items[0],grid.Columns.First()); }
                settings.TextScalePercent = 130; settings.ComfortableRows = true; settings.HighContrast = true; ApplyAppearance(); Window.UpdateLayout();
                if (Math.Abs(Window.FontSize - 16.9) > 0.05 || grid.RowHeight < 57) throw new Exception("Accessible text or row sizing did not apply.");
                Readable(Window.Foreground, Window.Background, "High contrast workspace");
                if ((Brush)Window.FindResource("Focus") == null) throw new Exception("Focus highlight is missing.");
                Capture(Path.Combine(output, "AstroArchive_Accessibility_UI.png"));
                settings.TextScalePercent = 150; ApplyAppearance(); PumpPopupLayout();
                foreach (var item in menu.Items.Cast<MenuItem>()) {
                    var bounds = PopupBounds(item, menu);
                    if (bounds.Right > menu.ActualWidth + 1 || bounds.Bottom > menu.ActualHeight + 1 || item.FontSize < 19) throw new Exception("Large-text navigation is clipped or does not scale.");
                }
                var tableViewer = PopupChildren<ScrollViewer>(grid).First();
                if (tableViewer.ExtentWidth > tableViewer.ViewportWidth + 1 && !PopupChildren<ScrollBar>(tableViewer).Any(bar => bar.Orientation == Orientation.Horizontal && bar.IsVisible && bar.ActualWidth > 40)) throw new Exception("Wide table has no usable horizontal scrollbar.");
                Capture(Path.Combine(output, "AstroArchive_Large_Text_UI.png"));
                settings.ReducedMotion=true;var previousProgress=latestProgress;latestProgress=new ProgressInfo{TotalKnown=false};LiveTick(true);if(((ProgressBar)Window.FindName("ProgressBar")).IsIndeterminate)throw new Exception("Reduced motion still animates unknown progress.");latestProgress=previousProgress;
                var preferences = new FormWindow(Window, "Accessibility smoke", 640, 620);
                preferences.Tabs("Preferences", "Processing", "Plate solving", "Repository", "Accessibility");
                var controls = AddAccessibilityPreferences(preferences); preferences.SelectTab(4); preferences.CloseOnly();
                try { preferences.Window.Show(); PumpPopupLayout(); controls.Scale.SelectedItem = "150%"; var snapshot = new Settings(); controls.Save(snapshot);
                    if (snapshot.TextScalePercent != 150 || !snapshot.HighContrast || !snapshot.ComfortableRows) throw new Exception("Accessibility preferences were not preserved.");
                } finally { preferences.Window.Close(); }
                var about = AboutPage();
                try { about.Window.Show(); PumpPopupLayout(); if (!PopupChildren<TextBlock>(about.Window).Any(text => text.Text == "Ari J. Guest")) throw new Exception("About page lacks author attribution."); CapturePopup(about.Window, Path.Combine(output, "AstroArchive_About_UI.png")); }
                finally { about.Window.Close(); }
                var steps = WalkthroughSteps(); var originalSource = T("SourceBox").Text;
                var tour = new WalkthroughWindow(Window, steps, SelectWalkthroughStep, step => { }, finished => RemoveWalkthroughHighlight());
                try {
                    tour.Show(); PumpPopupLayout();
                    for (int index = 0; index < steps.Length; index++) { tour.SetStep(index); PumpPopupLayout(); if (tour.StepIndex != index || tour.Heading.Text != steps[index].Title) throw new Exception("Walkthrough lost a step."); }
                    tour.SetStep(1); tour.Back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); if (tour.StepIndex != 0) throw new Exception("Walkthrough Back did not return.");
                    if (T("SourceBox").Text != originalSource || cancel != null) throw new Exception("Walkthrough performed an import or changed the source.");
                    foreach (var button in new[] { tour.Next, tour.Back, tour.Try }) {
                        var bounds = PopupBounds(button, tour);
                        if (!button.IsVisible || bounds.Right > tour.ActualWidth || bounds.Bottom > tour.ActualHeight) throw new Exception("Large-text walkthrough actions are clipped.");
                    }
                    CapturePopup(tour, Path.Combine(output, "AstroArchive_Walkthrough_UI.png"));
                } finally { tour.Close(); RemoveWalkthroughHighlight(); }
                if (L("RateLabel").IsVisible || ((ProgressBar)Window.FindName("ProgressBar")).IsVisible) throw new Exception("Idle status bar still displays processing indicators.");
            } finally { settings = Util.Deserialize<Settings>(saved); ApplyAppearance(); GoToPage(page); UpdateNavigationState(); }
        }
    }
}
