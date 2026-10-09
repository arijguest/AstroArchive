using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AstroArchive {
    public sealed class WalkthroughStep {
        public string Title, Body, Target, Action;
        public int Page;
    }
    public sealed class WalkthroughWindow : Window {
        public readonly TextBlock Heading = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        public readonly HelpArticle Body = new HelpArticle { Padding = new Thickness(0) };
        public readonly TextBlock Count = new TextBlock();
        public readonly Button Back = new Button { Content = "Back" };
        public readonly Button Next = new Button { Content = "Next", IsDefault = true };
        public readonly Button Try = new Button();
        public int StepIndex { get; private set; }
        readonly WalkthroughStep[] steps;
        readonly Action<WalkthroughStep> select, action;
        readonly Action<bool> complete;
        bool finished;
        public WalkthroughWindow(Window owner, WalkthroughStep[] steps, Action<WalkthroughStep> select, Action<WalkthroughStep> action, Action<bool> complete) {
            Owner = owner; Icon = ApplicationIcon.Image; this.steps = steps; this.select = select; this.action = action; this.complete = complete;
            Title = steps.Length==4?"AstroArchive: first import":"AstroArchive walkthrough";
            Width = Math.Min(570, SystemParameters.WorkArea.Width - 24); MinWidth = Math.Min(460, Width);
            Height = Math.Min(500, SystemParameters.WorkArea.Height - 24); MinHeight = Math.Min(360, Height);
            WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = owner.FontFamily; FontSize = owner.FontSize;
            Resources.MergedDictionaries.Add(owner.Resources); SetResourceReference(Control.FontSizeProperty, "UiFontControl"); Theme.Bind(this, Control.BackgroundProperty, "Canvas"); Theme.Bind(this, Control.ForegroundProperty, "Text");
            var layout = new Grid { Margin = new Thickness(22) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = layout;
            var title = new StackPanel { Margin = new Thickness(0, 0, 0, 14) }; title.Children.Add(Count); title.Children.Add(Heading); layout.Children.Add(title);
            Heading.SetResourceReference(TextBlock.FontSizeProperty, "UiFontTitle"); Theme.Bind(Count, TextBlock.ForegroundProperty, "Muted");
            Count.SetResourceReference(TextBlock.FontSizeProperty, "UiFontCaption"); Count.FontWeight = FontWeights.SemiBold; Count.Margin = new Thickness(0, 0, 0, 6);
            Theme.Bind(Body, Control.BackgroundProperty, "Canvas"); AutomationProperties.SetName(Body, "Walkthrough instructions");
            Grid.SetRow(Body, 1); layout.Children.Add(Body);
            Try.HorizontalAlignment = HorizontalAlignment.Left; Try.Margin = new Thickness(0, 14, 0, 8); Grid.SetRow(Try, 2); layout.Children.Add(Try);
            Try.Click += (s,e) => this.action(steps[StepIndex]);
            var buttons = new DockPanel { Margin = new Thickness(0, 8, 0, 0) }; Grid.SetRow(buttons, 3); layout.Children.Add(buttons);
            var close = new Button { Content = "Close", IsCancel = true }; close.Click += (s,e) => Close(); buttons.Children.Add(close);
            var navigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(navigation, Dock.Right); buttons.Children.Add(navigation); navigation.Children.Add(Back); navigation.Children.Add(Next);
            Theme.Bind(Next, Control.BackgroundProperty, "Accent"); Theme.Bind(Next, Control.ForegroundProperty, "AccentText"); Theme.Bind(Next, Control.BorderBrushProperty, "Accent");
            Back.Click += (s,e) => SetStep(StepIndex - 1);
            Next.Click += (s,e) => { if (StepIndex == steps.Length - 1) { finished = true; Close(); } else SetStep(StepIndex + 1); };
            UiHelp.Describe(Back, "Previous step."); UiHelp.Describe(Next, "Next step (Enter).");
            UiHelp.Describe(Try, "Open this step’s menu or settings.");
            UiHelp.Describe(close, "Close walkthrough.");
            Loaded += (s,e) => SetStep(0);
            PreviewKeyDown += (s,e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); } };
            Closed += (s,e) => this.complete(finished);
        }
        public void SetStep(int index) {
            StepIndex = Math.Max(0, Math.Min(steps.Length - 1, index));
            var step = steps[StepIndex]; Heading.Text = step.Title; Body.ShowText(step.Body); Try.Content = step.Action;
            Count.Text = "STEP " + (StepIndex + 1) + " OF " + steps.Length;
            Back.IsEnabled = StepIndex > 0; Next.Content = StepIndex == steps.Length - 1 ? "Finish" : "Next";
            AutomationProperties.SetName(Heading, Count.Text + ": " + step.Title);
            AutomationProperties.SetHelpText(Next, HelpCatalog.PlainText(step.Body));
            select(step); Next.Focus();
        }
    }
    public sealed class WalkthroughHighlight : Adorner {
        public WalkthroughHighlight(UIElement element) : base(element) { IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext context) {
            var brush = AdornedElement is FrameworkElement ? ((FrameworkElement)AdornedElement).TryFindResource("Focus") as Brush : Brushes.DodgerBlue;
            context.DrawRoundedRectangle(null, new Pen(brush ?? Brushes.DodgerBlue, 3), new Rect(new Point(0, 0), AdornedElement.RenderSize), 6, 6);
        }
    }
    public partial class MainUi {
        WalkthroughWindow walkthrough;
        WalkthroughHighlight walkthroughHighlight;
        AdornerLayer walkthroughHighlightLayer;
        int walkthroughOriginalPage;
        static WalkthroughStep[] WalkthroughSteps() {
            return new[] {
                new WalkthroughStep { Title = "Switch pages from the toolbar", Page = 0, Target = "PageSelector", Action = "Try the page selector", Body = "Use the purple **PAGE** selector to move around AstroArchive. It sits beside the toolbar, or below it in smaller windows.\n\n- Choose **Repository**, **Edited** or **Import**.\n- Search, filters, selections and import progress stay in place.\n- Use `Ctrl+1–3` to select a page or `Ctrl+Tab` to cycle pages." },
                new WalkthroughStep { Title = "Choose your repository", Page = 0, Target = "OpenRepositoryFolderButton", Action = "Open Preferences", Body = "Your repository holds verified capture copies and the archive index.\n\n1. Open **Settings → General**.\n2. Choose your archive folder.\n\nClick the repository path on the main page to open it in Explorer." },
                new WalkthroughStep { Title = "Select a telescope or source folder", Page = 1, Target = "ImportSourceCard", Action = "Open Import menu", Body = "Select a saved telescope or **Browse** its capture folder. Give each physical telescope its own device ID.\n\n- **Connected telescopes** finds local storage; **Saved telescopes** manages profiles.\n- **Scan folder** skips matching filenames and known DWARF sessions; Seestar scans still find new filenames.\n- Enable **Robust file matching** in Import options to revisit edits and additions inside skipped sessions.\n- **Review and repair → Full rescan** reads and hashes every file for a complete check." },
                new WalkthroughStep { Title = "Review before importing", Page = 1, Target = "ImportGrid", Action = "Open review and import options", Body = "Check each file’s **status** and **review reason** in the scan table.\n\n- **Review and repair** opens flagged captures and transfer retries.\n- **Import options** controls analysis and original removal.\n- The policy summary stays visible. **Originals are kept by default.**" },
                new WalkthroughStep { Title = "Import verified copies", Page = 1, Target = "ImportButton", Action = "Show import actions", Body = "Import copies ready files from the **filtered scan** and verifies their checksums.\n\n- Already archived content is skipped.\n- Progress and timing appear during the job.\n- **Cancel** stops safely after the current operation.\n\nThis walkthrough never starts an import for you." },
                new WalkthroughStep { Title = "Browse, preview and sort", Page = 0, Target = "FramesGrid", Action = "Open Repository view options", Body = "Browse targets and sessions, or narrow the view with **Search** and **Filters**.\n\n## Select and organise\n- `Ctrl/Shift` selects files; right-click opens file actions.\n- **Group subs by session** switches between compact sessions and files.\n\n## Sort and choose columns\n- Click a heading to sort; click again to reverse. `Shift-click` adds a heading.\n- ▲ / ▼ show direction; numbers show sort priority.\n- Right-click a heading to choose columns." },
                new WalkthroughStep { Title = "Explore the capture sky", Page = 0, Target = "PreviewSky", Action = "Reset the sky view", Body = "Repository’s sky globe appears below the image when there is room. It uses the recorded capture time and location.\n\n- **Drag** to rotate; **scroll or pinch** to zoom.\n- With the globe focused, **arrow keys** rotate and **plus/minus** zoom.\n- Use **Reset**, double-click or `Home` to restore the capture view.\n\nExploring changes the viewing angle; capture coordinates, time and horizon stay fixed." },
                new WalkthroughStep { Title = "Export a stacking project", Page = 0, Target = "ExportButton", Action = "Open Export menu", Body = "Select captures, then choose **Export** for original copies or a ready-to-stack folder.\n\n- Stacks copy to the destination; subs keep compatible input folders.\n- Matching calibrations include reasons; scientific conversion is explicit.\n- **Add Metadata** and **Create new folder** start off. Existing files are kept.\n\nStacking takes place in your chosen processing software." },
                new WalkthroughStep { Title = "Make AstroArchive comfortable", Page = 0, Target = "SettingsMenu", Action = "Open accessibility preferences", Body = "Open **Settings** for Preferences.\n\n- **General** sets appearance and the repository.\n- **Accessibility** adjusts text, rows, contrast and motion.\n- **Guide** opens searchable help, this walkthrough and About AstroArchive.\n- Press `F1` for help on the current page." }
            };
        }
        void StartWalkthrough(){StartWalkthrough(false);}
        void StartWalkthrough(bool firstImport) {
            if (cancel != null) return;
            if (walkthrough != null) { walkthrough.Activate(); return; }
            settings.GuideSeen = true; SaveSettings();
            walkthroughOriginalPage = ((TabControl)Window.FindName("MainTabs")).SelectedIndex;
            var steps=WalkthroughSteps();if(firstImport)steps=new[]{steps[1],steps[2],steps[3],steps[4]};
            walkthrough = new WalkthroughWindow(Window, steps, SelectWalkthroughStep, step => {
                if (cancel != null) return;
                Window.Activate();
                if (step.Target == "PageSelector") { C("PageSelector").Focus(); C("PageSelector").IsDropDownOpen = true; }
                else if (step.Target == "PreviewSky") { ((SkyGlobeView)Window.FindName("PreviewSky")).ResetView(); ((SkyGlobeView)Window.FindName("PreviewSky")).Focus(); }
                else if (step.Title == "Choose your repository") Configure();
                else if (step.Title == "Make AstroArchive comfortable") Configure(4);
                else if (step.Page == 1) OpenTopMenu("ImportMenu");
                else if (step.Title == "Export a stacking project") OpenTopMenu("ExportMenu");
                else OpenTopMenu("RepositoryMenu");
            }, finished => {
                RemoveWalkthroughHighlight(); walkthrough = null;
                C("PageSelector").IsDropDownOpen = false;
                foreach (var item in TopMenus()) item.IsSubmenuOpen = false;
                if (finished) settings.GuideCompleted = true;
                SaveSettings(); GoToPage(walkthroughOriginalPage);
            });
            walkthrough.Show();
            // Keep the card clear of the top navigation and source inputs.
            Rect area = SystemParameters.WorkArea;
            walkthrough.Left = Math.Max(area.Left + 12, Math.Min(Window.Left + Window.ActualWidth - walkthrough.Width - 24, area.Right - walkthrough.Width - 12));
            walkthrough.Top = Math.Max(area.Top + 12, Math.Min(Window.Top + Window.ActualHeight - walkthrough.Height - 40, area.Bottom - walkthrough.Height - 12));
        }
        void SelectWalkthroughStep(WalkthroughStep step) {
            C("PageSelector").IsDropDownOpen = false;
            RemoveWalkthroughHighlight(); GoToPage(step.Target == "PageSelector" ? walkthroughOriginalPage : step.Page); Window.UpdateLayout();
            var target = Window.FindName(step.Target) as UIElement;
            if (target == null) return;
            var layer = AdornerLayer.GetAdornerLayer(target);
            if (layer == null) return;
            walkthroughHighlight = new WalkthroughHighlight(target); walkthroughHighlightLayer = layer; layer.Add(walkthroughHighlight);
        }
        void RemoveWalkthroughHighlight() {
            if (walkthroughHighlight == null) return;
            if (walkthroughHighlightLayer != null) walkthroughHighlightLayer.Remove(walkthroughHighlight);
            walkthroughHighlight = null;
            walkthroughHighlightLayer = null;
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
