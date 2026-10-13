using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace AstroArchive {
    // The live result of a step's check, shown under its instructions.
    public sealed class WalkthroughCheck {
        public bool Ok; public string Text;
    }
    public sealed class WalkthroughWindow : Window {
        public readonly TextBlock Heading = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        public readonly HelpArticle Body = new HelpArticle { Padding = new Thickness(0), Localized = true };
        public readonly TextBlock Count = new TextBlock();
        public readonly TextBlock Guide = new TextBlock { TextWrapping = TextWrapping.Wrap };
        public readonly ProgressBar Progress = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 8, 0, 12), IsTabStop = false };
        public readonly Border StatusPanel = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0) };
        public readonly TextBlock StatusMark = new TextBlock { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
        public readonly TextBlock Status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        public readonly Button Back = new Button { Content = "Back" };
        public readonly Button Next = new Button { Content = "Next", IsDefault = true };
        public readonly Button Try = new Button();
        public int StepIndex { get; private set; }
        public WalkthroughGuide Walkthrough { get { return guide; } }
        public WalkthroughStep CurrentStep { get { return steps[StepIndex]; } }
        readonly WalkthroughGuide guide;
        readonly WalkthroughStep[] steps;
        readonly Action<WalkthroughStep> select, action;
        readonly Action<bool> complete;
        readonly Func<WalkthroughStep, WalkthroughCheck> check;
        readonly Func<WalkthroughStep, bool> available;
        readonly DispatcherTimer refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        bool finished;
        public WalkthroughWindow(Window owner, WalkthroughStep[] steps, Action<WalkthroughStep> select, Action<WalkthroughStep> action, Action<bool> complete)
            : this(owner, new WalkthroughGuide { Id = "custom", Title = "AstroArchive walkthrough", Steps = steps }, select, action, complete, null, null) { }
        public WalkthroughWindow(Window owner, WalkthroughGuide guide, Action<WalkthroughStep> select, Action<WalkthroughStep> action, Action<bool> complete, Func<WalkthroughStep, WalkthroughCheck> check, Func<WalkthroughStep, bool> available) {
            Owner = owner; Icon = ApplicationIcon.Image; this.guide = guide; steps = guide.Steps; this.select = select; this.action = action; this.complete = complete; this.check = check; this.available = available;
            Title = guide.Title;
            Width = Math.Min(560, SystemParameters.WorkArea.Width - 24); MinWidth = Math.Min(460, Width);
            Height = Math.Min(560, SystemParameters.WorkArea.Height - 24); MinHeight = Math.Min(380, Height);
            WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = owner.FontFamily; FontSize = owner.FontSize; ShowInTaskbar = false;
            Resources.MergedDictionaries.Add(owner.Resources); SetResourceReference(Control.FontSizeProperty, "UiFontControl"); Theme.Bind(this, Control.BackgroundProperty, "Canvas"); Theme.Bind(this, Control.ForegroundProperty, "Text");
            var layout = new Grid { Margin = new Thickness(22) };
            foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto }) layout.RowDefinitions.Add(new RowDefinition { Height = height });
            Content = layout;
            // Header: which guide this is, where you are in it, and the step's title.
            var title = new StackPanel(); layout.Children.Add(title);
            var caption = new DockPanel(); title.Children.Add(caption);
            DockPanel.SetDock(Count, Dock.Right); caption.Children.Add(Count); caption.Children.Add(Guide);
            Guide.Text = guide.Title; Theme.Bind(Guide, TextBlock.ForegroundProperty, "Accent"); Guide.SetResourceReference(TextBlock.FontSizeProperty, "UiFontCaption"); Guide.FontWeight = FontWeights.SemiBold;
            Theme.Bind(Count, TextBlock.ForegroundProperty, "Muted"); Count.SetResourceReference(TextBlock.FontSizeProperty, "UiFontCaption"); Count.FontWeight = FontWeights.SemiBold; Count.Margin = new Thickness(12, 0, 0, 0);
            Progress.SetResourceReference(Control.ForegroundProperty, "Accent"); Progress.SetResourceReference(Control.BackgroundProperty, "SurfaceAlt"); Progress.BorderThickness = new Thickness(0); AutomationProperties.SetName(Progress, "Walkthrough progress"); title.Children.Add(Progress);
            Heading.SetResourceReference(TextBlock.FontSizeProperty, "UiFontTitle"); Heading.Margin = new Thickness(0, 0, 0, 12); title.Children.Add(Heading);
            Theme.Bind(Body, Control.BackgroundProperty, "Canvas"); AutomationProperties.SetName(Body, "Walkthrough instructions");
            Grid.SetRow(Body, 1); layout.Children.Add(Body);
            // A live check of the user's own setup for this step.
            var status = new DockPanel(); DockPanel.SetDock(StatusMark, Dock.Left); status.Children.Add(StatusMark); status.Children.Add(Status); StatusPanel.Child = status;
            Theme.Bind(StatusPanel, Border.BackgroundProperty, "SurfaceAlt"); Theme.Bind(StatusPanel, Border.BorderBrushProperty, "Border"); AutomationProperties.SetName(StatusPanel, "Setup check");
            Grid.SetRow(StatusPanel, 2); layout.Children.Add(StatusPanel);
            Try.HorizontalAlignment = HorizontalAlignment.Left; Try.Margin = new Thickness(0, 14, 0, 8); Grid.SetRow(Try, 3); layout.Children.Add(Try);
            Try.Click += (s,e) => { this.action(steps[StepIndex]); RefreshCheck(); };
            var buttons = new DockPanel { Margin = new Thickness(0, 8, 0, 0) }; Grid.SetRow(buttons, 4); layout.Children.Add(buttons);
            var close = new Button { Content = "Close", IsCancel = true }; close.Click += (s,e) => Close(); buttons.Children.Add(close);
            var navigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(navigation, Dock.Right); buttons.Children.Add(navigation); navigation.Children.Add(Back); navigation.Children.Add(Next);
            Theme.Bind(Next, Control.BackgroundProperty, "Accent"); Theme.Bind(Next, Control.ForegroundProperty, "AccentText"); Theme.Bind(Next, Control.BorderBrushProperty, "Accent");
            Back.Click += (s,e) => SetStep(StepIndex - 1);
            Next.Click += (s,e) => { if (StepIndex == steps.Length - 1) { finished = true; Close(); } else SetStep(StepIndex + 1); };
            UiHelp.Describe(Back, "Previous step."); UiHelp.Describe(Next, "Next step (Enter).");
            UiHelp.Describe(Try, "Open this step’s menu or settings.");
            UiHelp.Describe(close, "Close walkthrough.");
            Loaded += (s,e) => { SetStep(0); refresh.Start(); };
            Activated += (s,e) => RefreshCheck();
            refresh.Tick += (s,e) => RefreshCheck();
            PreviewKeyDown += (s,e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); } };
            Closed += (s,e) => { refresh.Stop(); this.complete(finished); };
        }
        public void SetStep(int index) {
            StepIndex = Math.Max(0, Math.Min(steps.Length - 1, index));
            var step = steps[StepIndex]; Heading.Text = step.Title; Body.ShowText(Localization.T(step.Body));
            Try.Content = step.Action; Try.Visibility = step.Action == null ? Visibility.Collapsed : Visibility.Visible;
            Count.Text = "Step " + (StepIndex + 1) + " of " + steps.Length; Progress.Value = (StepIndex + 1) / (double)steps.Length;
            Back.IsEnabled = StepIndex > 0; Next.Content = StepIndex == steps.Length - 1 ? "Finish" : "Next";
            AutomationProperties.SetName(Heading, Localization.T(guide.Title) + ", " + Localization.T(Count.Text) + ": " + Localization.T(step.Title));
            AutomationProperties.SetHelpText(Next, HelpCatalog.PlainText(Localization.T(step.Body)));
            select(step); RefreshCheck(); Next.Focus();
        }
        public void RefreshCheck() {
            if (steps == null || StepIndex >= steps.Length) return;
            var step = steps[StepIndex];
            Try.IsEnabled = step.Action != null && (available == null || available(step));
            var result = check == null || step.Check == null ? null : check(step);
            StatusPanel.Visibility = result == null ? Visibility.Collapsed : Visibility.Visible;
            if (result == null) return;
            // Colour supports the mark and words; it never carries the meaning alone.
            StatusMark.Text = result.Ok ? "✓" : "!"; Status.Text = result.Text;
            StatusMark.SetResourceReference(TextBlock.ForegroundProperty, result.Ok ? "QualityHigh" : "QualityMid");
            StatusPanel.SetResourceReference(Border.BorderBrushProperty, result.Ok ? "Border" : "QualityMid");
            AutomationProperties.SetName(StatusPanel, (result.Ok ? Localization.T("Looks good") : Localization.T("Needs attention")) + ": " + result.Text);
        }
    }
    // Spotlights the control a step is about: the rest of the window dims slightly and a focus
    // ring surrounds the control. It never intercepts clicks.
    public sealed class WalkthroughHighlight : Adorner {
        public readonly UIElement Target;
        readonly bool dim;
        Rect drawn = Rect.Empty;
        public WalkthroughHighlight(UIElement root, UIElement target, bool dim) : base(root) {
            Target = target; this.dim = dim; IsHitTestVisible = false;
            target.LayoutUpdated += Moved;
        }
        public void Detach() { Target.LayoutUpdated -= Moved; }
        void Moved(object sender, EventArgs e) { if (Bounds() != drawn) InvalidateVisual(); }
        Rect Bounds() {
            try {
                if (!Target.IsVisible || !AdornedElement.IsAncestorOf(Target)) return Rect.Empty;
                var bounds = Target.TransformToAncestor(AdornedElement).TransformBounds(new Rect(Target.RenderSize)); bounds.Inflate(5, 5); return bounds;
            } catch (InvalidOperationException) { return Rect.Empty; }
        }
        protected override void OnRender(DrawingContext context) {
            drawn = Bounds(); if (drawn.IsEmpty) return;
            var brush = TryFindResource("Focus") as Brush ?? Brushes.DodgerBlue;
            if (dim) {
                var shade = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)); shade.Freeze();
                context.DrawGeometry(shade, null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(AdornedElement.RenderSize)), new RectangleGeometry(drawn, 8, 8)));
            }
            context.DrawRoundedRectangle(null, new Pen(brush, 3), drawn, 8, 8);
        }
    }
    public partial class MainUi {
        WalkthroughWindow walkthrough;
        WalkthroughHighlight walkthroughHighlight;
        AdornerLayer walkthroughHighlightLayer;
        int walkthroughOriginalPage;
        static WalkthroughStep[] WalkthroughSteps() { return WalkthroughCatalog.Find(WalkthroughCatalog.Tour).Steps; }
        void StartWalkthrough() { StartWalkthrough(WalkthroughCatalog.Tour); }
        void StartWalkthrough(bool firstImport) { StartWalkthrough(firstImport ? WalkthroughCatalog.FirstImport : WalkthroughCatalog.Tour); }
        // Walkthroughs only open menus and settings or press harmless buttons; they never import,
        // export or delete. They can start while work runs, since some fixes explain that work.
        void StartWalkthrough(string id) {
            var guide = WalkthroughCatalog.Find(id); if (guide == null || closing) return;
            if (walkthrough != null) { if (walkthrough.Walkthrough.Id == id) { walkthrough.Activate(); return; } walkthrough.Close(); }
            settings.GuideSeen = true; SaveSettings();
            walkthroughOriginalPage = ((TabControl)Window.FindName("MainTabs")).SelectedIndex;
            AppLog.Info("Walkthrough started: " + guide.Title);
            walkthrough = new WalkthroughWindow(Window, guide, SelectWalkthroughStep, DoWalkthroughAction, finished => {
                RemoveWalkthroughHighlight(); walkthrough = null;
                C("PageSelector").IsDropDownOpen = false;
                foreach (var item in TopMenus()) item.IsSubmenuOpen = false;
                if (finished && !guide.Fix) settings.GuideCompleted = true;
                SaveSettings();
                // Tours return to where they started; fixes leave you where the problem was solved.
                if (!guide.Fix) GoToPage(walkthroughOriginalPage);
            }, CheckWalkthroughStep, WalkthroughActionAvailable);
            walkthrough.Show();
            Rect area = SystemParameters.WorkArea;
            walkthrough.Left = Math.Max(area.Left + 12, Math.Min(Window.Left + Window.ActualWidth - walkthrough.Width - 24, area.Right - walkthrough.Width - 12));
            walkthrough.Top = Math.Max(area.Top + 12, Math.Min(Window.Top + Window.ActualHeight - walkthrough.Height - 40, area.Bottom - walkthrough.Height - 12));
        }
        UIElement WalkthroughTarget(string name) { return name == "ActivityBell" ? activityBell : Window.FindName(name ?? "") as UIElement; }
        void SelectWalkthroughStep(WalkthroughStep step) {
            C("PageSelector").IsDropDownOpen = false;
            RemoveWalkthroughHighlight(); GoToPage(step.Page < 0 ? ((TabControl)Window.FindName("MainTabs")).SelectedIndex : step.Page); Window.UpdateLayout();
            var target = WalkthroughTarget(step.Target); var root = Window.Content as UIElement;
            if (target == null || root == null || !target.IsVisible) return;
            var layer = AdornerLayer.GetAdornerLayer(root);
            if (layer == null) return;
            walkthroughHighlight = new WalkthroughHighlight(root, target, !settings.HighContrast); walkthroughHighlightLayer = layer; layer.Add(walkthroughHighlight);
            KeepWalkthroughClear(target);
        }
        // Moves the card to another corner of the main window when it would cover the spotlighted control.
        void KeepWalkthroughClear(UIElement target) {
            if (walkthrough == null || !walkthrough.IsVisible) return;
            var source = PresentationSource.FromVisual(Window); if (source == null || source.CompositionTarget == null) return;
            var toDips = source.CompositionTarget.TransformFromDevice;
            var spot = new Rect(toDips.Transform(target.PointToScreen(new Point(0, 0))), toDips.Transform(target.PointToScreen(new Point(target.RenderSize.Width, target.RenderSize.Height))));
            double width = walkthrough.ActualWidth > 0 ? walkthrough.ActualWidth : walkthrough.Width, height = walkthrough.ActualHeight > 0 ? walkthrough.ActualHeight : walkthrough.Height;
            if (!new Rect(walkthrough.Left, walkthrough.Top, width, height).IntersectsWith(spot)) return;
            Rect area = SystemParameters.WorkArea;
            foreach (var corner in new[] { new Point(1, 1), new Point(0, 1), new Point(1, 0), new Point(0, 0) }) {
                double left = corner.X > 0 ? Window.Left + Window.ActualWidth - width - 24 : Window.Left + 24, top = corner.Y > 0 ? Window.Top + Window.ActualHeight - height - 40 : Window.Top + 90;
                left = Math.Max(area.Left + 12, Math.Min(left, area.Right - width - 12)); top = Math.Max(area.Top + 12, Math.Min(top, area.Bottom - height - 12));
                if (new Rect(left, top, width, height).IntersectsWith(spot)) continue;
                walkthrough.Left = left; walkthrough.Top = top; return;
            }
        }
        void RemoveWalkthroughHighlight() {
            if (walkthroughHighlight == null) return;
            walkthroughHighlight.Detach();
            if (walkthroughHighlightLayer != null) walkthroughHighlightLayer.Remove(walkthroughHighlight);
            walkthroughHighlight = null;
            walkthroughHighlightLayer = null;
        }
        bool WalkthroughActionAvailable(WalkthroughStep step) {
            string act = step.Do ?? "";
            if (act == "report" || act == "activity" || act == "page-selector" || act.StartsWith("page:")) return true;
            if (act.StartsWith("menu:")) return TopMenu(act.Substring(5)).IsEnabled;
            if (act.StartsWith("preferences:")) return !NetworkImportBlocked;
            switch (act) {
                case "browse-source": return B("SourceButton").IsEnabled;
                case "scan": return B("ScanButton").IsEnabled;
                case "clear-search": return B("ClearButton").IsEnabled;
                case "clear-import-search": return B("ImportClearButton").IsEnabled;
                case "refresh-usb": return B("RefreshUsbButton").IsEnabled;
                case "all-targets": return Targets.Items.Count > 0;
                case "connect-network": return repo != null && !NetworkImportBlocked;
                case "export": return TopMenu("ExportMenu").IsEnabled;
                case "backup": return repo != null && !RepositoryOperationBlocked;
            }
            return false;
        }
        void DoWalkthroughAction(WalkthroughStep step) {
            if (!WalkthroughActionAvailable(step)) return;
            string act = step.Do;
            Window.Activate();
            if (act == "page-selector") { C("PageSelector").Focus(); C("PageSelector").IsDropDownOpen = true; }
            else if (act.StartsWith("preferences:")) Configure(int.Parse(act.Substring(12), CultureInfo.InvariantCulture));
            else if (act.StartsWith("menu:")) OpenTopMenu(act.Substring(5));
            else if (act.StartsWith("page:")) GoToPage(int.Parse(act.Substring(5), CultureInfo.InvariantCulture));
            else if (act == "report") ReportProblem();
            else if (act == "activity") OpenActivity();
            else if (act == "all-targets") { Targets.SelectedIndex = 0; Targets.Focus(); }
            else if (act == "connect-network") { GoToPage(1); OpenRemoteImport(); }
            else if (act == "export") OpenExport();
            else if (act == "backup") BackUpArchive();
            else {
                string button = act == "browse-source" ? "SourceButton" : act == "scan" ? "ScanButton" : act == "clear-search" ? "ClearButton" : act == "clear-import-search" ? "ImportClearButton" : act == "refresh-usb" ? "RefreshUsbButton" : null;
                if (button != null) B(button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
        WalkthroughCheck Passed(string text) { return new WalkthroughCheck { Ok = true, Text = text }; }
        WalkthroughCheck Needs(string text) { return new WalkthroughCheck { Ok = false, Text = text }; }
        // Each check reads the current state of the window and settings; none of them changes anything.
        WalkthroughCheck CheckWalkthroughStep(WalkthroughStep step) {
            switch (step.Check) {
                case "repository": return repo != null ? Passed(Localization.T("Repository: " + repo.Root)) : Needs(Localization.T("No repository is chosen yet."));
                case "idle": return !RepositoryOperationBlocked ? Passed(Localization.T("Nothing else is running.")) : Needs(Localization.T("Another task is running. Import waits until it finishes."));
                case "source": { string source = T("SourceBox").Text.Trim(); return source.Length > 0 && Directory.Exists(source) ? Passed(Localization.T("Source: " + source)) : Needs(Localization.T(source.Length == 0 ? "No source folder or telescope is chosen yet." : "The chosen source folder isn't available. Reconnect the drive or choose another folder.")); }
                case "scanned": return plan != null ? Passed(Localization.T("Scanned: " + plan.Frames.Count + " files found.")) : Needs(Localization.T("This source hasn't been scanned yet."));
                case "ready": return B("ImportButton").IsEnabled ? Passed(Localization.T("Files are ready to import.")) : Needs(Localization.T("No files are ready to import in this view."));
                case "unfiltered": return T("SearchBox").Text.Trim().Length == 0 && libraryFilters.ActiveCount == 0 ? Passed(Localization.T("Search and filters are clear.")) : Needs(Localization.T("Search or filters are hiding some files."));
                case "all-targets": return Targets.SelectedIndex <= 0 ? Passed(Localization.T("All Targets is selected.")) : Needs(Localization.T("Only one target is shown."));
                case "has-files": return all.Count > 0 ? Passed(Localization.T(all.Count + " captures in this archive.")) : Needs(Localization.T("This archive has no captures yet."));
                case "preview-on": return settings.ShowPreview ? Passed(Localization.T("The preview pane is on.")) : Needs(Localization.T("The preview pane is turned off."));
                case "one-file": { int count = SelectedFiles().Count; return count == 1 ? Passed(Localization.T("One file is selected.")) : Needs(Localization.T(count == 0 ? "No file is selected." : "Several files are selected. Select just one.")); }
                case "selection": { int count = SelectedFiles().Count; return count > 0 ? Passed(Localization.T(count + " files selected.")) : Needs(Localization.T("No files are selected yet.")); }
                case "location": return settings.Latitude.HasValue || settings.ObservingCityId.HasValue ? Passed(Localization.T("Observing location: " + (string.IsNullOrEmpty(settings.ObservingCity) ? "coordinates set" : settings.ObservingCity))) : Needs(Localization.T("No observing town is set."));
                case "solver": return !string.IsNullOrEmpty(settings.Astap) || settings.UseOnline ? Passed(Localization.T("A plate solver is set up.")) : Needs(Localization.T("No plate solver is set up yet."));
                case "usb": return usbTelescopes.Count > 0 ? Passed(Localization.T("Found: " + string.Join(", ", usbTelescopes.Select(t => ImportSelection.TelescopeBrand(t.Make) + " (" + t.Volume.Root + ")")))) : Needs(Localization.T("No USB telescope is detected right now."));
                case "backup": { DateTime last; return DateTime.TryParse(settings.LastBackupUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out last) ? Passed(Localization.T("Last backup: " + last.ToLocalTime().ToString("d", CultureInfo.CurrentCulture))) : Needs(Localization.T("No backup has been made yet.")); }
            }
            return null;
        }
        FormWindow AboutPage() {
            var dialog = new FormWindow(Window, "About AstroArchive", 650, 640);
            var logo = new Image { Source = ((Image)Window.FindName("BrandLogo")).Source, Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,0,0,12) };
            dialog.Add(logo); dialog.Text("AstroArchive", true);
            var version=ReleaseTarget().Running;dialog.Text("Version " + version.Version + " · Package " + version.PackageVersion);AddAboutReleases(dialog);
            dialog.Text("A desktop archive for astronomical images: verified imports, searchable capture metadata, image previews and export projects for your processing tools.");
            dialog.Text("Ari J. Guest", true);
            dialog.Text("Created by Ari J. Guest. Learn more about the author and their work at arijguest.com.");
            dialog.Button("Visit arijguest.com", () => OpenWebsite("https://arijguest.com"));
            dialog.Button("Source code and releases", () => OpenWebsite("https://github.com/arijguest/AstroArchive"));
            dialog.Button("Report a problem…", ReportProblem);
            dialog.Button("Buy Me a Coffee", () => OpenWebsite("https://ko-fi.com/arijguest"));
            dialog.Text("Software licence", true);
            dialog.Text("Copyright 2026 Ari J. Guest. AstroArchive is source-available under the PolyForm Noncommercial License 1.0.0. Noncommercial use, modification and sharing are permitted under its terms. Commercial use, including resale outside the permitted purposes, requires separate permission.");
            dialog.Button("Read software licence (offline)", () => LicencePage().Show());
            dialog.Button("Commercial licensing enquiries", () => OpenWebsite("https://astroarchive.arijguest.com/#contact"));
            dialog.Text("Catalogue acknowledgements", true);
            dialog.Text("Target names and positions use OpenNGC (CC BY-SA 4.0); observing towns and cities use GeoNames (CC BY 4.0); constellation figures use D3-Celestial (BSD 3-clause). These datasets retain their own licences; their notices are included with the installation. Original image data remains in your repository; derived exports are identified separately.");
            dialog.CloseOnly(); return dialog;
        }
        FormWindow LicencePage() {
            var dialog = new FormWindow(Window, "AstroArchive software licence", 720, 700);
            dialog.Text("PolyForm Noncommercial License 1.0.0", true);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LICENSE")) {
                if (stream == null) throw new InvalidOperationException("The bundled software licence is missing.");
                using (var reader = new StreamReader(stream)) dialog.Text(reader.ReadToEnd());
            }
            dialog.Text("Third-party data keeps its separate licences. Your images and processing outputs are not relicensed by using AstroArchive.");
            dialog.Button("Licensing summary and third-party notices", () => OpenWebsite("https://github.com/arijguest/AstroArchive/blob/main/LICENSING.md"));
            dialog.CloseOnly(); return dialog;
        }
        void About() { AboutPage().Show(); }
    }
}
