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
        public readonly TextBlock Body = new TextBlock { TextWrapping = TextWrapping.Wrap };
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
            Title = "AstroArchive walkthrough";
            Width = Math.Min(530, SystemParameters.WorkArea.Width - 24); MinWidth = Math.Min(460, Width);
            Height = Math.Min(430, SystemParameters.WorkArea.Height - 24); MinHeight = Math.Min(330, Height);
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
            Count.Margin = new Thickness(0, 0, 0, 8);
            var reading = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(reading, 1); layout.Children.Add(reading);
            Try.HorizontalAlignment = HorizontalAlignment.Left; Try.Margin = new Thickness(0, 14, 0, 8); Grid.SetRow(Try, 2); layout.Children.Add(Try);
            Try.Click += (s,e) => this.action(steps[StepIndex]);
            var buttons = new DockPanel { Margin = new Thickness(0, 8, 0, 0) }; Grid.SetRow(buttons, 3); layout.Children.Add(buttons);
            var close = new Button { Content = "Skip / close", IsCancel = true }; close.Click += (s,e) => Close(); buttons.Children.Add(close);
            var navigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(navigation, Dock.Right); buttons.Children.Add(navigation); navigation.Children.Add(Back); navigation.Children.Add(Next);
            Back.Click += (s,e) => SetStep(StepIndex - 1);
            Next.Click += (s,e) => { if (StepIndex == steps.Length - 1) { finished = true; Close(); } else SetStep(StepIndex + 1); };
            UiHelp.Tip(Back, "Return to the previous step."); UiHelp.Tip(Next, "Advance through the walkthrough. Enter activates this button.");
            UiHelp.Tip(Try, "Open the relevant menu or settings. Steps never import or delete files automatically.");
            UiHelp.Tip(close, "Close the walkthrough. Run it again from Guide whenever you need it.");
            Loaded += (s,e) => SetStep(0);
            PreviewKeyDown += (s,e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); } };
            Closed += (s,e) => this.complete(finished);
        }
        public void SetStep(int index) {
            StepIndex = Math.Max(0, Math.Min(steps.Length - 1, index));
            var step = steps[StepIndex]; Heading.Text = step.Title; Body.Text = step.Body; Try.Content = step.Action;
            Count.Text = "Step " + (StepIndex + 1) + " of " + steps.Length;
            Back.IsEnabled = StepIndex > 0; Next.Content = StepIndex == steps.Length - 1 ? "Finish" : "Next";
            AutomationProperties.SetName(Heading, Count.Text + ": " + step.Title);
            AutomationProperties.SetHelpText(Next, step.Body);
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
                new WalkthroughStep { Title = "Switch pages from the toolbar", Page = 0, Target = "PageSelector", Action = "Try the page selector", Body = "The centred purple dropdown labelled PAGE shows your current page. It sits beside the toolbar actions when there is room, or just below them in smaller windows. Open it to choose Repository, Edited, Import. Search, filters, selections and import progress stay in place when you switch. Ctrl+1–3 selects those pages; Ctrl+Tab cycles them." },
                new WalkthroughStep { Title = "Choose your repository", Page = 0, Target = "OpenRepositoryFolderButton", Action = "Open Repository menu", Body = "Your repository is the folder where AstroArchive keeps verified capture copies and its portable index. Choose it from Repository → Choose repository folder. The path in the grey strip below the repository table opens that folder in Explorer." },
                new WalkthroughStep { Title = "Select a telescope or source folder", Page = 1, Target = "ImportSourceCard", Action = "Open Import menu", Body = "Select a saved physical telescope or Browse its capture folder. Each telescope gets a distinct device ID. Import → USB telescopes finds connected storage; Saved telescopes manages profiles. Scan folder reads metadata before any copies are made." },
                new WalkthroughStep { Title = "Review before importing", Page = 1, Target = "ImportGrid", Action = "Open review and import options", Body = "The scan table shows status and review reasons. Use Import → Review and recovery to inspect flagged files or retry transfers. Import options controls analysis and original removal. The policy summary stays visible; originals are kept by default." },
                new WalkthroughStep { Title = "Import verified copies", Page = 1, Target = "ImportButton", Action = "Show import actions", Body = "Import copies only ready files in the filtered scan and verifies their checksums. Already archived content is skipped. During a job, timing and progress appear in a temporary panel; Cancel stops safely after the current operation. This walkthrough never starts an import for you." },
                new WalkthroughStep { Title = "Browse, preview and sort", Page = 0, Target = "FramesGrid", Action = "Open Repository view options", Body = "Search your captures or browse targets and sessions. Click a table heading to sort; click again to reverse. Shift-click adds another heading. ▲ and ▼ show direction, and numbers show sort priority. Ctrl/Shift selects files; right-click opens file actions. View contains filters, columns and the preview pane." },
                new WalkthroughStep { Title = "Explore the capture sky", Page = 0, Target = "PreviewSky", Action = "Reset the sky view", Body = "Select a capture to show its sky globe below the preview. It appears when the image leaves room and uses the recorded capture time and location. Drag to rotate it; scroll or pinch to zoom. Arrow keys rotate and plus/minus zoom when the globe has focus. The small reset button, double-click or Home restores the capture view. Exploring changes only the viewing angle; capture coordinates, time and horizon stay fixed." },
                new WalkthroughStep { Title = "Export a stacking project", Page = 0, Target = "ExportButton", Action = "Open Export menu", Body = "Select repository files and choose Export for original copies or a ready-to-stack folder. Matching calibrations are offered with reasons; scientific conversion is explicit. Stacking happens in your chosen processing software. Existing export folders are never replaced." },
                new WalkthroughStep { Title = "Make AstroArchive comfortable", Page = 0, Target = "SettingsMenu", Action = "Open accessibility preferences", Body = "Settings → Preferences contains appearance; Settings → Accessibility adjusts larger text, comfortable rows, high contrast and reduced progress animation. Guide holds searchable help, this walkthrough and About AstroArchive. F1 opens help for the current page." }
            };
        }
        void StartWalkthrough() {
            if (cancel != null) return;
            if (walkthrough != null) { walkthrough.Activate(); return; }
            settings.GuideSeen = true; SaveSettings();
            walkthroughOriginalPage = ((TabControl)Window.FindName("MainTabs")).SelectedIndex;
            walkthrough = new WalkthroughWindow(Window, WalkthroughSteps(), SelectWalkthroughStep, step => {
                if (cancel != null) return;
                Window.Activate();
                if (step.Target == "PageSelector") { C("PageSelector").Focus(); C("PageSelector").IsDropDownOpen = true; }
                else if (step.Target == "PreviewSky") { ((SkyGlobeView)Window.FindName("PreviewSky")).ResetView(); ((SkyGlobeView)Window.FindName("PreviewSky")).Focus(); }
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
            dialog.Text("Version " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            dialog.Text("A desktop archive for astronomical images: verified imports, searchable capture metadata, image previews and export projects for your processing tools.");
            dialog.Text("Ari J. Guest", true);
            dialog.Text("Created by Ari J. Guest. Learn more about the author and their work at arijguest.com.");
            dialog.Button("Visit arijguest.com", () => OpenWebsite("https://arijguest.com"));
            dialog.Button("Source code and releases", () => OpenWebsite("https://github.com/arijguest/AstroArchive"));
            dialog.Button("Buy Me a Coffee", () => OpenWebsite("https://ko-fi.com/arijguest"));
            dialog.Text("Catalogue acknowledgements", true);
            dialog.Text("Target names and positions use OpenNGC; observing towns and cities use GeoNames. Their notices are included with the installation. Original image data remains in your repository; derived exports are identified separately.");
            dialog.CloseOnly(); return dialog;
        }
        void About() { AboutPage().Show(); }
    }
}
