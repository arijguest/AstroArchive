using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Runtime.InteropServices;

namespace AstroArchive.Desktop;

public sealed class MainWindow : Window
{
    public ArchiveSession Session { get; }
    public Task LastOperation { get; private set; } = Task.CompletedTask;
    public bool Busy { get; private set; }
    public string? LastError { get; private set; }
    public Dictionary<string, Control> Controls { get; } = new();
    private CancellationTokenSource? cancellation;
    private readonly TextBlock status = new() { Text = "Choose an archive folder to begin.", TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 1, Height = 5 };
    private readonly TextBox report = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
    private readonly TabControl tabs = new();
    private readonly StackPanel archiveBar = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly Image preview = new() { Stretch = Stretch.Uniform, MinHeight = 180, MaxHeight = 280 };
    private readonly TextBlock previewText = new() { Text = "Select an archived capture and choose Preview.", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock summary = new() { FontSize = 16, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel charts = new() { Spacing = 18 };
    private WriteableBitmap? previewBitmap;
    private readonly Button cancel;

    public MainWindow(ArchiveSession? session = null)
    {
        Session = session ?? new ArchiveSession();
        Title = "AstroArchive · Linux preview"; Width = 1220; Height = 850; MinWidth = 1100; MinHeight = 720;
        if (Application.Current != null) Application.Current.RequestedThemeVariant = Session.Settings.Theme == "Light" ? ThemeVariant.Light : Session.Settings.Theme == "System" ? ThemeVariant.Default : ThemeVariant.Dark;
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(22) };
        var title = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0,0,0,16) };
        var brand = new StackPanel { Spacing = 3 };
        brand.Children.Add(new TextBlock { Text = "AstroArchive", FontSize = 28, FontWeight = FontWeight.SemiBold });
        brand.Children.Add(new TextBlock { Text = "Your observations, preserved and ready to process", Opacity = 0.8 });
        title.Children.Add(brand);
        var badge = new TextBlock { Text = "LINUX PREVIEW 1", Foreground = Brushes.LightSkyBlue, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(badge, 1); title.Children.Add(badge); body.Children.Add(title);

        archiveBar.Children.Add(Label("Archive"));
        var archive = Field("ArchivePath", Session.Settings.Archive, "Absolute folder path", 540); archiveBar.Children.Add(archive);
        archiveBar.Children.Add(Button("BrowseArchive", "Browse…", () => PickFolder(archive)));
        archiveBar.Children.Add(Button("OpenArchive", "Open / create", () => Run("Opening archive", _ => { Session.Open(archive.Text ?? ""); return Task.CompletedTask; })));
        Grid.SetRow(archiveBar, 1); body.Children.Add(archiveBar);
        tabs.Margin = new Thickness(0,18,0,12);
        tabs.ItemsSource = new[] {
            Page("Repository", RepositoryPage()), Page("Import", ImportPage()),
            Page("Edited", EditedPage()), Page("Analytics", AnalyticsPage()),
            Page("Settings", SettingsPage()), Page("Guide", GuidePage())
        };
        Controls["Pages"] = tabs; Grid.SetRow(tabs, 2); body.Children.Add(tabs);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        footer.Children.Add(status); Controls["Status"] = status;
        cancel = Button("Cancel", "Cancel operation", () => { cancellation?.Cancel(); status.Text = "Canceling safely…"; return Task.CompletedTask; });
        cancel.IsEnabled = false; Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
        progress.Margin = new Thickness(0,10,0,0); Grid.SetRow(progress, 1); Grid.SetColumnSpan(progress, 2); footer.Children.Add(progress);
        Grid.SetRow(footer, 3); body.Children.Add(footer); Content = body;
        Closing += (_, e) => { if (Busy) { e.Cancel = true; cancellation?.Cancel(); status.Text = "Canceling safely. Close again after the operation finishes."; } };
        Closed += (_, _) => { Session.Dispose(); previewBitmap?.Dispose(); cancellation?.Dispose(); };
        if (!string.IsNullOrEmpty(Session.SettingsWarning)) status.Text = Session.SettingsWarning;
        else if (Session.Settings.Archive.Length == 0) tabs.SelectedIndex = 5;
    }

    private Control RepositoryPage()
    {
        var grid = Table("Captures", Session.Captures,
            ("Target", "TargetLabel"), ("File", "OriginalName"), ("Type", "KindLabel"), ("Night", "Night"),
            ("Telescope", "Telescope"), ("Filter", "Filter"), ("Exposure", "ExposureText"), ("Review", "ReviewText"));
        var search = Field("Search", "", "Search target, filename, telescope or filter", 420);
        search.TextChanged += (_, _) => grid.ItemsSource = new CaptureFilters().Apply(Session.Captures, search.Text ?? "");
        var bar = Row(search, Button("Refresh", "Refresh", () => Run("Refreshing", _ => { Session.Refresh(); return Task.CompletedTask; })),
            Button("Preview", "Preview selected", () => Run("Loading preview", PreviewSelected)),
            Button("Verify", "Verify archive", () => Run("Verifying", ct => Task.Run(() => Session.Verify(ct, ReportProgress), ct))));
        var lower = new Grid { ColumnDefinitions = new ColumnDefinitions("*,360"), Margin = new Thickness(0,14,0,0) };
        var operations = new StackPanel { Spacing = 10 };
        operations.Children.Add(Heading("Export selected captures"));
        operations.Children.Add(DestinationFields("Capture"));
        operations.Children.Add(Row(Button("Export", "Export original files", () => ExportCaptures(false)), Button("StackingExport", "Prepare stacking project", () => ExportCaptures(true))));
        operations.Children.Add(Heading("Metadata and working copies"));
        operations.Children.Add(Row(Field("Target", "", "New target (blank keeps existing)", 255), Field("Filter", "", "New filter", 180)));
        operations.Children.Add(Row(Button("ApplyMetadata", "Apply to selected", () => Run("Saving metadata", ct => { var frames = Selected<Frame>("Captures"); var target = Text("Target"); var filter = Text("Filter"); return Task.Run(() => Session.ApplyMetadata(frames, target, filter, ct), ct); })),
            Button("WorkingCopy", "Create Edited working copies", () => Run("Creating working copies", ct => { var frames = Selected<Frame>("Captures"); var name = Text("CaptureName"); return Task.Run(() => Session.WorkingCopies(frames, name, ct, ReportProgress), ct); }))));
        var imagePanel = new StackPanel { Spacing = 6 }; imagePanel.Children.Add(preview); imagePanel.Children.Add(previewText);
        lower.Children.Add(operations); Grid.SetColumn(imagePanel, 1); lower.Children.Add(imagePanel);
        return Layout(bar, grid, lower);
    }
    private Control ImportPage()
    {
        var source = Field("SourcePath", Session.Settings.Source, "Source folder, mounted card or network share", 520);
        var top = new StackPanel { Spacing = 10 };
        top.Children.Add(Notice("Import copies and verifies original bytes. Source files are retained on Linux. Inspect the scan before importing."));
        top.Children.Add(Row(source, Button("BrowseSource", "Browse…", () => PickFolder(source))));
        top.Children.Add(Row(Field("Telescope", Session.Settings.Telescope, "Physical telescope name", 260), Field("Model", Session.Settings.Model, "Model (Auto, Dwarf 3, Seestar S50…)", 300),
            Button("Scan", "Scan source", () => Run("Scanning source", async ct => {
                string sourcePath = Text("SourcePath"), scope = Text("Telescope"), model = Text("Model");
                var plan = await Task.Run(() => Session.Scan(sourcePath, scope, model, ct, ReportProgress), ct);
                Session.ShowCandidates(plan); Session.SaveSettings();
                status.Text = $"Scan found {plan.Frames.Count} files; {plan.Errors.Count} errors. Select files or import all eligible rows.";
            }))));
        top.Children.Add(Row(Button("ImportSelected", "Import selected", () => Import(false)), Button("ImportAll", "Import all eligible", () => Import(true))));
        var table = Table("Candidates", Session.Candidates, ("File", "OriginalName"), ("Target", "TargetLabel"), ("Type", "KindLabel"), ("Status", "Status"), ("Review", "ReviewReason"));
        return Layout(top, table, Notice("Duplicates are verified by content. Unreadable files remain visible. Cancel retains committed copies; rescan and import to resume."));
    }
    private Control EditedPage()
    {
        var paths = Field("EditedPaths", "", "Absolute image paths, one per line", 650); paths.AcceptsReturn = true; paths.Height = 65;
        var top = new StackPanel { Spacing = 10 };
        top.Children.Add(Notice("Work on copies in Edited. Archive captures keep their original bytes."));
        top.Children.Add(Row(paths, Button("BrowseEdited", "Choose images…", async () => {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Add Edited images", AllowMultiple = true });
            paths.Text = string.Join("\n", files.Select(f => f.TryGetLocalPath()).Where(p => p != null));
        })));
        top.Children.Add(Row(Field("EditedProject", "Edited images", "Project name", 300),
            Button("AddEdited", "Add images", () => Run("Adding Edited images", ct => { var paths = Text("EditedPaths").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); var name = Text("EditedProject"); return Task.Run(() => Session.AddEdited(paths, name, ct, ReportProgress), ct); })),
            Button("LaunchEditor", "Open selected in editor", () => Run("Opening editor", _ => {
                var image = Selected<EditedImage>("EditedImages").Single();
                ArchiveSession.LaunchEditor(Session.Settings.ExternalEditor, Session.RequireArchive().EditedPath(image.Project, image.RelativePath)); return Task.CompletedTask;
            }))));
        var table = Table("EditedImages", Session.Edited, ("Project", "Project.Name"), ("Image", "Filename"), ("Type", "FileType"), ("Kind", "Kind"), ("Bytes", "Bytes"));
        return Layout(top, table, Row(DestinationFields("Edited"), Button("ExportEdited", "Export selected", () => Run("Exporting Edited images", ct => { var images = Selected<EditedImage>("EditedImages"); var parent = Text("EditedDestination"); var name = Text("EditedName"); return Task.Run(() => Session.ExportEdited(images, parent, name, ct, ReportProgress), ct); }))));
    }
    private Control AnalyticsPage()
    {
        var top = new StackPanel { Spacing = 10 }; top.Children.Add(summary);
        top.Children.Add(Notice("Integration totals use individual light frames. Stacks, videos, rejected frames and calibration files are excluded."));
        top.Children.Add(Row(DestinationFields("Analytics"), Button("ExportAnalytics", "Export SVG + data", () => Run("Exporting analytics", ct => { var parent = Text("AnalyticsDestination"); var name = Text("AnalyticsName"); return Task.Run(() => Session.ExportAnalytics(parent, name, ct), ct); }))));
        var panel = new StackPanel { Spacing = 18 }; panel.Children.Add(top); panel.Children.Add(charts);
        return new ScrollViewer { Content = panel };
    }
    private Control SettingsPage()
    {
        var panel = new StackPanel { Spacing = 14, MaxWidth = 800, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Heading("Appearance and editor"));
        var theme = new ComboBox { ItemsSource = new[] { "Dark", "Light", "System" }, SelectedItem = Session.Settings.Theme, Width = 180 }; Controls["Theme"] = theme;
        panel.Children.Add(Row(Label("Theme"), theme));
        panel.Children.Add(Field("EditorPath", Session.Settings.ExternalEditor, "Absolute path to Siril, PixInsight, GIMP or another editor", 670));
        panel.Children.Add(Button("SaveSettings", "Save settings", () => Run("Saving settings", _ => {
            Session.Settings.Theme = theme.SelectedItem?.ToString() ?? "Dark"; Session.Settings.ExternalEditor = Text("EditorPath"); Session.SaveSettings();
            if (Application.Current != null) Application.Current.RequestedThemeVariant = Session.Settings.Theme == "Light" ? ThemeVariant.Light : Session.Settings.Theme == "System" ? ThemeVariant.Default : ThemeVariant.Dark;
            return Task.CompletedTask;
        })));
        panel.Children.Add(Heading("Verified archive backup"));
        panel.Children.Add(Field("BackupDestination", "", "Existing destination outside the archive", 670));
        var zip = new CheckBox { Content = "Lossless ZIP backup", IsChecked = true }; Controls["ZipBackup"] = zip; panel.Children.Add(zip);
        panel.Children.Add(Button("Backup", "Create and verify backup", () => Run("Backing up archive", ct => { var parent = Text("BackupDestination"); var compressed = zip.IsChecked == true; return Task.Run(() => Session.Backup(parent, compressed, ct, ReportProgress), ct); })));
        panel.Children.Add(Notice("Archives can move between Windows and Linux. Close AstroArchive before disconnecting the drive. Direct telescope discovery, online solving and native deletion protection are not available on Linux. Windows protection settings are retained; source originals are retained."));
        panel.Children.Add(Heading("Activity report")); panel.Children.Add(report); Controls["Report"] = report;
        return new ScrollViewer { Content = panel };
    }
    private static Control GuidePage()
    {
        var panel = new StackPanel { Spacing = 20, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Heading("Welcome to AstroArchive on Linux"));
        panel.Children.Add(Notice("1. Open or create an archive\nChoose an absolute folder path on a local filesystem. Keep enough free space for copies, exports and backups."));
        panel.Children.Add(Notice("2. Scan your observations\nIn Import, choose a folder or mounted telescope card, enter a physical telescope name, then Scan source. Review unreadable files and metadata before importing."));
        panel.Children.Add(Notice("3. Preserve and organize\nImport selected files or all eligible rows. SHA-256 verification protects transfers; every source original is retained. Repository search and metadata edits organize your archive without changing capture pixels."));
        panel.Children.Add(Notice("4. Process copies\nExport original files or a stacking project. Create Edited working copies for an external editor, or add finished images to Edited."));
        panel.Children.Add(Notice("5. Verify and back up\nVerify archive checks captured hashes. Settings creates a verified folder or lossless ZIP backup. Analytics exports six reports as SVG and JSON."));
        panel.Children.Add(Notice("FITS, XISF and SER numeric previews are available where the reader supports the codec. Other formats can be archived and exported unchanged. Unsupported previews report the reason."));
        return new ScrollViewer { Content = panel };
    }

    private Task Import(bool all) => Run("Importing verified copies", async ct => {
        var selected = all ? Session.Candidates.ToList() : Selected<Frame>("Candidates");
        var result = await Task.Run(() => Session.Import(selected, ct, ReportProgress), ct);
        status.Text = $"Imported {result.Imported}; duplicates {result.Duplicates}; failed {result.Failed}. Source originals retained.";
        if (result.Failed > 0) LastError = $"{result.Failed} files failed. See the activity report.";
    });
    private Task ExportCaptures(bool stacking) => Run("Exporting verified copies", ct => { var frames = Selected<Frame>("Captures"); var parent = Text("CaptureDestination"); var name = Text("CaptureName"); return Task.Run(() => Session.Export(frames, parent, name, stacking, ct, ReportProgress), ct); });
    private async Task PreviewSelected(CancellationToken ct)
    {
        var frame = Selected<Frame>("Captures").Single(); string path = Session.RequireArchive().FilePath(frame);
        preview.Source=null; previewBitmap?.Dispose(); previewBitmap=null; previewText.Text="Preview: "+frame.OriginalName;

        var data = await Task.Run(() => Assets.Display(frame, path, frame.ImageIndex ?? 0, ct), ct);
        byte[] pixels = await Task.Run(() => data.Render("Auto per channel", ct), ct);
        var bitmap = new WriteableBitmap(new PixelSize(data.Width, data.Height), new Vector(96,96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        byte[] bgra = new byte[checked(data.Width*data.Height*4)];
        for (int i=0;i<data.Width*data.Height;i++) { bgra[i*4]=pixels[i*3+2]; bgra[i*4+1]=pixels[i*3+1]; bgra[i*4+2]=pixels[i*3]; bgra[i*4+3]=255; }
        using (var locked = bitmap.Lock())
            for (int y=0; y<data.Height; y++) Marshal.Copy(bgra, y*data.Width*4, locked.Address+y*locked.RowBytes, data.Width*4);
        preview.Source = bitmap; previewBitmap?.Dispose(); previewBitmap = bitmap;
        previewText.Text = frame.OriginalName + " · " + data.Description + " · display stretch only";
    }
    public Task Run(string title, Func<CancellationToken, Task> operation)
    {
        if (Busy) return LastOperation;
        LastOperation = Execute(title, operation); return LastOperation;
    }
    private async Task Execute(string title, Func<CancellationToken, Task> operation)
    {
        Busy = true; LastError = null; cancellation?.Dispose(); cancellation = new CancellationTokenSource();
        archiveBar.IsEnabled = tabs.IsEnabled = false; cancel.IsEnabled = true; progress.IsIndeterminate = true; status.Text = title + "…";
        try { await operation(cancellation.Token); if (status.Text == title + "…") status.Text = title + " complete."; }
        catch (OperationCanceledException) { status.Text = "Canceled safely. Committed copies are retained; rescan to continue."; }
        catch (Exception e) { LastError = e.Message; status.Text = "Unable to complete: " + e.Message; }
        finally {
            try { if (Session.Repository != null) { Session.Refresh(); ((DataGrid)Controls["Captures"]).ItemsSource = new CaptureFilters().Apply(Session.Captures, Text("Search")); UpdateAnalytics(); } }
            catch (Exception e) { LastError = e.Message; status.Text = "Archive refresh failed: " + e.Message; }
            report.Text = Session.LastReport + (Session.LastOutput.Length > 0 ? "\nOutput: " + Session.LastOutput : "");
            Busy = false; archiveBar.IsEnabled = tabs.IsEnabled = true; cancel.IsEnabled = false; progress.IsIndeterminate = false; progress.Value = 0;
        }
    }
    private void ReportProgress(ProgressInfo info) => Dispatcher.UIThread.Post(() => {
        if (!Busy) return;
        progress.IsIndeterminate = info.Total <= 0; progress.Value = info.Total > 0 ? Math.Clamp((double)info.Done/info.Total, 0, 1) : 0;
        status.Text = info.Stage + " · " + info.Done + "/" + info.Total + " · " + info.Text;
    });
    private void UpdateAnalytics()
    {
        var data = Session.Analytics;
        summary.Text = $"{data.Captures:N0} light frames · {data.Targets:N0} targets · {data.Seconds/3600:0.##} hours recorded integration\n{data.DateRange}";
        charts.Children.Clear();
        foreach (var report in data.Reports) {
            var card = new StackPanel { Spacing = 6 }; card.Children.Add(Heading(report.Title)); card.Children.Add(Notice(report.Description));
            double max = report.Values.Count > 0 ? report.Values.Max(v => v.Value) : 0;
            foreach (var item in report.Values.Take(30)) {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("280,*,100") };
                row.Children.Add(new TextBlock { Text = item.Label, TextWrapping = TextWrapping.Wrap });
                var bar = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, max), Value = item.Value, Height = 12, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(bar, 1); row.Children.Add(bar); var value = new TextBlock { Text = ArchiveAnalytics.Number(item.Value) + " " + report.Unit }; Grid.SetColumn(value, 2); row.Children.Add(value); card.Children.Add(row);
            }
            if (report.Values.Count == 0) card.Children.Add(Notice("No matching observations yet."));
            if (report.Values.Count > 30) card.Children.Add(Notice("Showing the first 30 groups. Export includes every group."));
            card.Children.Add(Notice(report.Note)); charts.Children.Add(card);
        }
    }
    public string Text(string name) => ((TextBox)Controls[name]).Text?.Trim() ?? "";
    private List<T> Selected<T>(string name) => ((DataGrid)Controls[name]).SelectedItems.Cast<T>().ToList();
    private TextBox Field(string name, string value, string hint, double width) { var control = new TextBox { Name = name, Text = value, PlaceholderText = hint, Width = width }; Controls[name] = control; return control; }
    private Button Button(string name, string text, Func<Task> action) {
        var button = new Button { Name = name, Content = text, VerticalAlignment = VerticalAlignment.Center }; Controls[name] = button;
        button.Click += (_, _) => { var task = action(); if (name != "Cancel") LastOperation = task; }; return button;
    }
    private async Task PickFolder(TextBox input) {
        var chosen = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose folder", AllowMultiple = false });
        if (chosen.FirstOrDefault()?.TryGetLocalPath() is { } path) input.Text = path;
    }
    private Control DestinationFields(string prefix) => Row(Field(prefix+"Destination", "", "Existing destination folder", 310), Field(prefix+"Name", prefix+" project", "New folder / project name", 210));
    private DataGrid Table<T>(string name, IEnumerable<T> items, params (string title, string path)[] columns) {
        var grid = new DataGrid { Name = name, ItemsSource = items, AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Extended, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, MinHeight = 140 };
        foreach (var column in columns) grid.Columns.Add(new DataGridTextColumn { Header = column.title, Binding = new Binding(column.path), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Controls[name] = grid; return grid;
    }
    private static TabItem Page(string title, Control content) => new() { Header = title, Content = content, Padding = new Thickness(14,10,14,10) };
    private static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 18, FontWeight = FontWeight.SemiBold };
    private static TextBlock Notice(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
    private static StackPanel Row(params Control[] children) { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; foreach (var child in children) row.Children.Add(child); return row; }
    private static Control Layout(Control top, Control center, Control bottom) { center.Height = 260; var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto") }; grid.Children.Add(top); center.Margin = new Thickness(0,12,0,0); Grid.SetRow(center, 1); grid.Children.Add(center); Grid.SetRow(bottom, 2); bottom.Margin = new Thickness(0,12,0,0); grid.Children.Add(bottom); return new ScrollViewer { Content = grid }; }
}
