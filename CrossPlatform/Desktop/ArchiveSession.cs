using System.Collections.ObjectModel;
using System.Text.Json;
using AstroArchive;

namespace AstroArchive.Desktop;

public sealed class DesktopSettings
{
    public string Archive { get; set; } = "";
    public string Source { get; set; } = "";
    public string Telescope { get; set; } = "My telescope";
    public string Model { get; set; } = "Auto";
    public string Theme { get; set; } = "Dark";
    public string ExternalEditor { get; set; } = "";
    public static string DefaultPath => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { } xdg && Path.IsPathRooted(xdg)
            ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "astroarchive", "settings.json");
}

// One foreground operation owns this session. The view disables conflicting
// actions while work runs, and cancellation waits for the engine to unwind.
public sealed class ArchiveSession : IDisposable
{
    public Repository? Repository { get; private set; }
    public DesktopSettings Settings { get; private set; }
    public ObservableCollection<Frame> Captures { get; } = [];
    public ObservableCollection<Frame> Candidates { get; } = [];
    public ObservableCollection<EditedImage> Edited { get; } = [];
    public AnalyticsSnapshot Analytics { get; private set; } = ArchiveAnalytics.Build([], new AnalyticsOptions());
    public string LastReport { get; private set; } = "";
    public string LastOutput { get; private set; } = "";
    public string SettingsWarning { get; private set; } = "";
    public string ConfigPath { get; }
    private ImportPlan? plan;

    public ArchiveSession(string? configPath = null)
    {
        ConfigPath = configPath ?? DesktopSettings.DefaultPath;
        Settings = new DesktopSettings();
        try
        {
            if (File.Exists(ConfigPath))
            {
                Exporter.CheckDestinationPath(ConfigPath);
                Settings = JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(ConfigPath))
                    ?? throw new IOException("Empty settings document.");
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        { SettingsWarning = "Settings could not be loaded: " + e.Message; }
        Settings.Archive ??= ""; Settings.Source ??= ""; Settings.Telescope ??= "My telescope";
        Settings.Model ??= "Auto"; Settings.ExternalEditor ??= "";
        if(Settings.Theme is not ("Dark" or "Light" or "System")) Settings.Theme="Dark";
    }

    public void SaveSettings()
    {
        Exporter.CheckDestinationPath(ConfigPath);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        string temp = ConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                JsonSerializer.Serialize(output, Settings, new JsonSerializerOptions { WriteIndented = true });
                output.Flush(true);
            }
            File.Move(temp, ConfigPath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public void Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            throw new ArgumentException("Enter an absolute archive folder path.");
        string root = Path.GetFullPath(path);
        if (Repository != null && root == Repository.Root) { Refresh(); return; }
        var next = new Repository(root);
        Repository?.Dispose(); Repository = next;
        Settings.Archive = root; plan = null; Candidates.Clear();
        Refresh(); SaveSettings();
    }

    public Repository RequireArchive() => Repository ?? throw new InvalidOperationException("Open an archive first.");
    public void Refresh()
    {
        Replace(Captures, RequireArchive().All());
        var projects = RequireArchive().EditedProjects(out var errors);
        Replace(Edited, projects.SelectMany(p => RequireArchive().EditedImages(p).Select(image => { image.Project = p; return image; })));
        Analytics = ArchiveAnalytics.Build(Captures, new AnalyticsOptions());
        if (errors.Count > 0) LastReport = string.Join("\n", errors);
    }

    public ImportPlan Scan(string source, string telescope, string model, CancellationToken ct, Action<ProgressInfo> progress)
    {
        if (!Path.IsPathRooted(source) || !Directory.Exists(source)) throw new IOException("Choose an existing source folder.");
        if (string.IsNullOrWhiteSpace(telescope)) throw new ArgumentException("Enter a physical telescope name.");
        plan = RequireArchive().Scan(source, telescope.Trim(), model, ct, progress, deferHash: false);
        Settings.Source = source; Settings.Telescope = telescope.Trim(); Settings.Model = model;
        LastReport = string.Join("\n", plan.Errors);
        return plan;
    }
    public void ShowCandidates(ImportPlan scanned) => Replace(Candidates, scanned.Frames);
    public ImportResult Import(IEnumerable<Frame> selected, CancellationToken ct, Action<ProgressInfo> progress)
    {
        if (plan == null) throw new InvalidOperationException("Scan a source before importing.");
        var frames = selected.ToList();
        if (frames.Count == 0) throw new InvalidOperationException("Select files to import.");
        // Source cleanup intentionally stays off on Linux.
        var result = RequireArchive().Import(frames, ct, progress,
            new ImportOptions { SourceRoot = Settings.Source, DeleteOriginals = false, ScanMetrics = plan.Metrics });
        LastReport = RequireArchive().LastReport;
        return result;
    }
    public string Export(IEnumerable<Frame> selection, string destination, string name, bool stacking, CancellationToken ct, Action<ProgressInfo> progress)
    {
        LastOutput = Exporter.Create(RequireArchive(), selection.ToList(), new ExportOptions {
            Parent = destination, Name = name, CreateNewFolder = true, AddMetadata = true,
            Mode = stacking ? "Subs" : "Files", IncludeCalibration = stacking, ConvertToFits = stacking
        }, ct, progress);
        return LastOutput;
    }
    public EditedProject WorkingCopies(IEnumerable<Frame> selection, string name, CancellationToken ct, Action<ProgressInfo> progress)
        => RequireArchive().CreateEditedWorkingCopies(selection, name, "Linux editor", ct, progress);
    public void AddEdited(IEnumerable<string> paths, string name, CancellationToken ct, Action<ProgressInfo> progress)
    {
        RequireArchive().AddEditedImages(paths, null, name, ct, progress);
    }
    public string ExportEdited(IEnumerable<EditedImage> selected, string destination, string name, CancellationToken ct, Action<ProgressInfo> progress)
    {
        LastOutput = Exporter.CreateEdited(RequireArchive(), selected, new ExportOptions { Parent = destination,
            Name = name, CreateNewFolder = true }, ct, progress);
        return LastOutput;
    }
    public void ApplyMetadata(IEnumerable<Frame> selection, string target, string filter, CancellationToken ct)
    {
        var changes = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(target)) changes["Target"] = target;
        if (!string.IsNullOrWhiteSpace(filter)) changes["Filter"] = filter;
        if (changes.Count == 0) throw new ArgumentException("Enter a target or filter to apply.");
        var patch = new MetadataPatch(changes); patch.Validate();
        var frames = selection.ToList();
        if (frames.Count == 0) throw new ArgumentException("Select archived files.");
        foreach (var frame in frames) { ct.ThrowIfCancellationRequested(); RequireArchive().Refile(patch.Apply(frame), ct); }
    }
    public string Backup(string parent, bool compressed, CancellationToken ct, Action<ProgressInfo> progress)
    {
        LastOutput = RequireArchive().CreateBackup(parent, compressed, ct, progress).Path;
        return LastOutput;
    }
    public void Verify(CancellationToken ct, Action<ProgressInfo> progress)
    {
        var all = RequireArchive().All(); var errors = new List<string>(); int done = 0;
        foreach (var frame in all)
        {
            ct.ThrowIfCancellationRequested();
            try { RequireArchive().ValidateCapture(frame, ct); }
            catch (Exception e) when (e is IOException or InvalidDataException) { errors.Add(frame.OriginalName + ": " + e.Message); }
            progress(new ProgressInfo { Done = ++done, Total = all.Count, Text = frame.OriginalName, Stage = "Verifying archive" });
        }
        LastReport = errors.Count == 0 ? $"Verified {all.Count} archived files. SHA-256 matches the index." : string.Join("\n", errors);
        if (errors.Count > 0) throw new IOException(LastReport);
    }
    public string ExportAnalytics(string parent, string name, CancellationToken ct)
    {
        var dest = Exporter.Destination(RequireArchive(), new ExportOptions { Parent = parent, Name = name, CreateNewFolder = true });
        ct.ThrowIfCancellationRequested(); Directory.CreateDirectory(dest);
        var pages = Enumerable.Range(0, Analytics.Reports.Count).SelectMany(i => AnalyticsGraphics.Pages(Analytics, i, false)).ToList();
        using var logoStream = typeof(Repository).Assembly.GetManifestResourceStream("AstroArchive_Logo.png")!;
        using var logoBytes = new MemoryStream(); logoStream.CopyTo(logoBytes);
        Exporter.WriteMetadataText(Path.Combine(dest, "analytics.svg"), AnalyticsGraphics.Svg(pages, Convert.ToBase64String(logoBytes.ToArray())), ct);
        Exporter.WriteMetadataText(Path.Combine(dest, "analytics.json"), Util.Serialize(Analytics), ct);
        LastOutput = dest; return dest;
    }
    public static void LaunchEditor(string executable, string path)
    {
        if (!Path.IsPathRooted(executable) || !File.Exists(executable)) throw new IOException("Choose the editor's absolute executable path in Settings.");
        var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false };
        start.ArgumentList.Add(path);
        System.Diagnostics.Process.Start(start)?.Dispose();
    }
    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    { var items = values.ToList(); collection.Clear(); foreach (var item in items) collection.Add(item); }
    public void Dispose() => Repository?.Dispose();
}
