namespace AstroArchive;

public sealed class ProtectionState { public string Mode = "Off", Sid = "", Root = ""; }
public sealed class ProtectionRule { public string Path = ""; public bool Directory; public int Bits; }
public sealed class ProtectionRecord { public string Kind = "", Id = ""; public ProtectionRule? Rule; public string[] Paths = [], Affected = []; public bool Capture; }

public sealed class ArchiveProtection
{
    public bool Enabled => false;
    public string Availability => "Filesystem deletion protection is available in the Windows application. Mac imports retain source originals.";
    public ArchiveProtection(string root, string meta)
    {
        string path = Path.Combine(meta, "protection.json");
        if (!File.Exists(path)) return;
        var state = Util.Deserialize<ProtectionState>(File.ReadAllText(path));
        if (state == null || state.Mode != "Off")
            throw new IOException("This archive has Windows deletion protection. Open a verified backup, or disable protection in AstroArchive on Windows before moving it.");
    }
    public void ProtectCapture(string path) { }
    public void Enable(IEnumerable<string> files, CancellationToken ct, Action<ProgressInfo>? progress) => throw new PlatformNotSupportedException(Availability);
    public void Disable(CancellationToken ct, Action<ProgressInfo>? progress) { ct.ThrowIfCancellationRequested(); }
    public IDisposable Unlock(string source, string? destination, bool capture = true) => new Scope();
    private sealed class Scope : IDisposable { public void Dispose() { } }
}

public sealed partial class Repository
{
    private ArchiveProtection protection = null!;
    public bool OriginalsProtected => false;
    public string ProtectionAvailability => protection.Availability;
    public void SetOriginalsProtection(bool enabled, CancellationToken ct, Action<ProgressInfo> progress)
    { if (enabled) protection.Enable(All().Select(FilePath), ct, progress); }
    private void ProtectCapture(string path) => protection.ProtectCapture(path);
    private void MoveCapture(string source, string destination)
    {
        CheckManagedPath(source, Root); CheckManagedPath(destination, Root);
        File.Move(source, destination);
    }
    private void DeleteEmptyCaptureFolder(string path) { CheckManagedPath(path, Root); Directory.Delete(path); }
}
