#nullable enable
namespace AstroArchive;

public sealed class ProtectionState { public string Mode = "Off", Sid = "", Root = ""; }
public sealed class ProtectionRule { public string Path = ""; public bool Directory; public int Bits; }
public sealed class ProtectionRecord { public string Kind = "", Id = ""; public ProtectionRule? Rule; public string[] Paths = [], Affected = []; public bool Capture; }

public sealed class ArchiveProtection
{
    private readonly bool windowsProtected;
    private readonly string guardPath;
    private bool guarded;
    public bool Enabled => guarded;
    public string Availability => "Linux protection prevents capture deletion and relocation in AstroArchive. Filesystem tools and Windows do not enforce this application guard; Windows NTFS protection settings remain separate.";
    public ArchiveProtection(string root, string meta)
    {
        guardPath = Path.Combine(meta, "linux-protection.json");
        if(File.Exists(guardPath)) {
            var guard=Util.Deserialize<ProtectionState>(File.ReadAllText(guardPath));
            if(guard==null||guard.Mode is not ("Off" or "Enabled"))throw new IOException("Linux capture guard settings are invalid. Restore this archive's guard settings before opening it.");
            guarded=guard.Mode=="Enabled";
        }
        string path = Path.Combine(meta, "protection.json");
        if (!File.Exists(path)) return;
        var state = Util.Deserialize<ProtectionState>(File.ReadAllText(path));
        if (state == null || (state.Mode != "Off" && state.Mode != "Enabled"))
            throw new IOException("This archive has an interrupted Windows protection change. Open it on Windows to complete recovery before using it on Linux.");
        windowsProtected = state.Mode == "Enabled";
    }
    public void CheckCaptureWrite()
    {
        if (windowsProtected) throw new PlatformNotSupportedException("This archive has Windows deletion protection. Disable it in AstroArchive on Windows before importing or moving captures on Linux. Browsing, export, working copies and backups remain available.");
    }
    public void ProtectCapture(string path) { }
    public void Enable(IEnumerable<string> files, CancellationToken ct, Action<ProgressInfo>? progress) { ct.ThrowIfCancellationRequested(); Util.AtomicText(guardPath,Util.Serialize(new ProtectionState { Mode="Enabled" })); guarded=true; }
    public void Disable(CancellationToken ct, Action<ProgressInfo>? progress) { ct.ThrowIfCancellationRequested(); Util.AtomicText(guardPath,Util.Serialize(new ProtectionState { Mode="Off" })); guarded=false; }
    public IDisposable Unlock(string source, string? destination, bool capture = true) { if(capture) CheckRelocation(); return new Scope(); }
    public void CheckRelocation() { CheckCaptureWrite(); if(guarded) throw new IOException("Capture protection is enabled. Disable the Linux application guard in Settings before deleting or relocating captures."); }
    private sealed class Scope : IDisposable { public void Dispose() { } }
}

public sealed partial class Repository
{
    private ArchiveProtection protection = null!;
    public bool OriginalsProtected => protection.Enabled;
    public string ProtectionAvailability => protection.Availability;
    public void SetOriginalsProtection(bool enabled, CancellationToken ct, Action<ProgressInfo> progress)
    { if (enabled) protection.Enable(All().Select(FilePath), ct, progress); else protection.Disable(ct,progress); }
    private void ProtectCapture(string path) => protection.ProtectCapture(path);
    private void MoveCapture(string source, string destination)
    {
        protection.CheckRelocation();
        CheckManagedPath(source, Root); CheckManagedPath(destination, Root);
        File.Move(source, destination);
    }
    private void DeleteEmptyCaptureFolder(string path) { CheckManagedPath(path, Root); Directory.Delete(path); }
}
