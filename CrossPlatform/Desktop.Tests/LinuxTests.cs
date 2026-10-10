using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AstroArchive.Desktop;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(AstroArchive.Desktop.Tests.TestApp))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace AstroArchive.Desktop.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

public sealed class LinuxTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "astroarchive-test-" + Guid.NewGuid().ToString("N"));
    private CancellationToken ct => TestContext.Current.CancellationToken;
    private ArchiveSession Session() => new(Path.Combine(root,"settings","settings.json"));
    private void Open(ArchiveSession s) => s.Open(Path.Combine(root,"archive"));
    private void Import(ArchiveSession s) { string source=ReleaseFixture.Prepare(root); var scan=s.Scan(source,"Scope","Auto",ct,_=>{}); s.Import(scan.Frames,ct,_=>{}); s.Refresh(); }

    [AvaloniaFact]
    public async Task AllSixPagesExecuteTheirRealCommands()
    {
        var window=new MainWindow(Session()); window.Show();
        try { await NativeSmoke.Check(window,root,false); Assert.False(window.Busy); }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task PointerClickOpensArchiveAndButtonsRecoverAfterErrors()
    {
        var window=new MainWindow(Session()); window.Show();
        try {
            ((TextBox)window.Controls["ArchivePath"]).Text=Path.Combine(root,"archive");
            var button=(Button)window.Controls["OpenArchive"]; window.UpdateLayout();
            var position=button.TranslatePoint(new Point(button.Bounds.Width/2,button.Bounds.Height/2),window)!.Value;
            window.MouseDown(position,MouseButton.Left); window.MouseUp(position,MouseButton.Left);
            await window.LastOperation; Assert.NotNull(window.Session.Repository); Assert.Null(window.LastError);
            ((TabControl)window.Controls["Pages"]).SelectedIndex=0;
            await window.Run("Invalid export",token=>Task.Run(()=>window.Session.Export([],root,"empty",false,token,_=>{}),token));
            Assert.Contains("Select",window.LastError); Assert.False(window.Busy); Assert.True(window.Controls["OpenArchive"].IsEnabled);
            Assert.Empty(window.Session.Captures);
        } finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task CancellationDisablesConflictingCommandsAndWaitsForCleanup()
    {
        var window=new MainWindow(Session()); window.Show();
        try {
            var work=window.Run("Cancelable operation",token=>Task.Delay(Timeout.Infinite,token));
            Assert.True(window.Busy); Assert.False(window.Controls["OpenArchive"].IsEffectivelyEnabled);
            ((Button)window.Controls["Cancel"]).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await work; Assert.False(window.Busy); Assert.Null(window.LastError);
            Assert.Contains("Canceled safely",((TextBlock)window.Controls["Status"]).Text);
        } finally { window.Close(); }
    }
    [Fact]
    public void CaseDistinctSourcesAreBothImported()
    {
        using var s=Session(); Open(s); string source=Path.Combine(root,"case");
        ReleaseFixture.Fits(Path.Combine(source,"LIGHT.fit"),"M31",1);
        ReleaseFixture.Fits(Path.Combine(source,"light.fit"),"M45",2);
        var scan=s.Scan(source,"Scope","Auto",ct,_=>{}); Assert.Equal(2,scan.Frames.Count);
        Assert.Equal(2,s.Import(scan.Frames,ct,_=>{}).Imported); s.Refresh(); Assert.Equal(2,s.Captures.Count);
        Assert.Equal(2,s.Scan(source,"Scope","Auto",ct,_=>{}).Frames.Count(f=>f.Status.StartsWith("Duplicate")));
    }
    [Fact]
    public void SecondWriterFailsAndReleaseAllowsReopen()
    {
        using(var s=Session()) { Open(s); Assert.Throws<IOException>(()=>new Repository(s.Repository!.Root)); }
        using var reopened=Session(); Open(reopened); Assert.Empty(reopened.Captures);
    }
    [Fact]
    public void CaseDifferentSiblingIsOutsideManagedArchive()
    {
        string upper=Path.Combine(root,"Archive"), lower=Path.Combine(root,"archive");
        Assert.False(Util.Within(Path.Combine(lower,"file.fit"),upper));
        Assert.Throws<IOException>(()=>Repository.CheckManagedPath(Path.Combine(lower,"file.fit"),upper));
    }
    [Theory]
    [InlineData("../escape.fit")]
    [InlineData("/tmp/escape.fit")]
    [InlineData("../../outside.fit")]
    public void IndexedPathsCannotEscapeArchive(string relative)
    {
        using var s=Session(); Open(s);
        Assert.Throws<IOException>(()=>s.Repository!.FilePath(new Frame { RelativePath=relative }));
    }
    [Fact]
    public void SymlinkLeafAndParentCannotRedirectArchiveWrites()
    {
        using var s=Session(); Open(s); string outside=Path.Combine(root,"outside"); Directory.CreateDirectory(outside);
        string link=Path.Combine(s.Repository!.Root,"link"); Directory.CreateSymbolicLink(link,outside);
        Assert.Throws<IOException>(()=>Repository.CheckManagedPath(Path.Combine(link,"escape.fit"),s.Repository.Root));
        File.CreateSymbolicLink(Path.Combine(s.Repository.Root,"dangling.fit"),Path.Combine(outside,"missing.fit"));
        Assert.Throws<IOException>(()=>s.Repository.FilePath(new Frame { RelativePath="dangling.fit" }));
        Assert.Empty(Directory.GetFiles(outside));
    }
    [Fact]
    public void ChangedSourceAfterScanCannotCommit()
    {
        using var s=Session(); Open(s); string source=ReleaseFixture.Prepare(root); var scan=s.Scan(source,"Scope","Auto",ct,_=>{});
        foreach(var frame in scan.Frames.Where(f=>f.Status!="Unreadable")) File.AppendAllText(frame.SourcePath,"changed");
        Assert.Equal(2,s.Import(scan.Frames,ct,_=>{}).Failed); s.Refresh(); Assert.Empty(s.Captures);
        Assert.Equal(3,Directory.GetFiles(source,"*.fit").Length);
    }
    [Fact]
    public void ModifiedArchiveBytesCannotExportOrCreateWorkingCopies()
    {
        using var s=Session(); Open(s); Import(s); var capture=s.Captures[0]; File.AppendAllText(s.Repository!.FilePath(capture),"changed");
        Assert.Throws<IOException>(()=>s.Export([capture],root,"bad",false,ct,_=>{}));
        Assert.Throws<InvalidDataException>(()=>s.WorkingCopies([capture],"bad",ct,_=>{}));
        Assert.Empty(s.Repository.EditedProjects(out _));
        Assert.Throws<IOException>(()=>s.Verify(ct,_=>{})); Assert.Contains("changed",s.LastReport);
    }
    [Fact]
    public void WindowsJsonPathsAndMicrosoftDatesRoundTrip()
    {
        string text="{\"Hash\":\"abc\",\"RelativePath\":\"Targets\\\\M31\\\\a.fit\",\"SourceStamp\":{\"Size\":20,\"Modified\":638000000000000000}}";
        var frame=Util.Deserialize<Frame>(text); Assert.Equal("Targets/M31/a.fit",frame.RelativePath);
        Assert.Contains("Targets\\\\M31\\\\a.fit",Util.Serialize(frame));
        var project=Util.Deserialize<EditedProject>("{\"Schema\":1,\"CreatedUtc\":\"/Date(1760000000123)/\",\"MetadataEdits\":{\"nested\\\\a.fit\":{\"Object\":\"M31\"}}}");
        Assert.True(project.MetadataEdits.ContainsKey("nested/a.fit"));
        Assert.Equal(1760000000123, new DateTimeOffset(project.CreatedUtc).ToUnixTimeMilliseconds());
        Assert.Contains("nested\\\\a.fit",Util.Serialize(project));
        Assert.Contains(@"\/Date(1760000000123)\/",Util.Serialize(project));
        var untyped=(Dictionary<string,object>)Util.Json().DeserializeObject(Util.Serialize(project));
        Assert.IsType<DateTime>(untyped["CreatedUtc"]);
        var iso=Util.Deserialize<Dictionary<string,object>>("{\"date\":\"2026-10-06T21:00:00+01:00\"}");
        Assert.Equal("2026-10-06T21:00:00+01:00",Assert.IsType<string>(iso["date"]));
    }
    [Fact]
    public void LinuxOpenRotatesWindowsCacheIdentityWithoutChangingCaptures()
    {
        string archive=Path.Combine(root,"archive"); string identity;
        using(var s=Session()) { Open(s); Import(s); identity=File.ReadAllText(Path.Combine(s.Repository!.Meta,"backup-origin.txt")); }
        using var reopen=Session(); Open(reopen); Assert.NotEqual(identity,File.ReadAllText(Path.Combine(reopen.Repository!.Meta,"backup-origin.txt")));
        Assert.Equal(2,reopen.Captures.Count); reopen.Verify(ct,_=>{});
    }
    [Fact]
    public void WindowsFolderCasingSurvivesLinuxOpeningExportAndBackup()
    {
        string archive=Path.Combine(root,"archive");
        using(var s=Session()) { Open(s); Import(s); s.WorkingCopies(s.Captures,"copy",ct,_=>{}); }
        Directory.Move(Path.Combine(archive,".astroarchive"),Path.Combine(archive,".ASTROARCHIVE"));
        Directory.Move(Path.Combine(archive,"Targets"),Path.Combine(archive,"targets"));
        using var reopened=Session(); Open(reopened); Assert.Equal(2,reopened.Captures.Count); Assert.Equal(2,reopened.Edited.Count);
        reopened.Verify(ct,_=>{}); reopened.Export(reopened.Captures,root,"case export",false,ct,_=>{});
        string zip=reopened.Backup(root,true,ct,_=>{}), restored=Path.Combine(root,"case restore");
        System.IO.Compression.ZipFile.ExtractToDirectory(zip,restored);
        using var repo=new Repository(Path.Combine(restored,"Repository")); Assert.Equal(0,repo.Verify(ct,_=>{}));
        Assert.Single(Directory.GetDirectories(repo.Root),d=>Path.GetFileName(d).Equals(".astroarchive",StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void AmbiguousWindowsCasingIsRejectedWithoutGuessing()
    {
        using var s=Session(); Open(s); Directory.CreateDirectory(Path.Combine(s.Repository!.Root,"Targets"));
        Directory.CreateDirectory(Path.Combine(s.Repository.Root,"TARGETS"));
        Assert.Throws<IOException>(()=>s.Repository.ResolveArchivePath("targets/M31/image.fit"));
    }
    [Fact]
    public void LinuxKeepsCompletedWindowsProtectionSettingsUnchanged()
    {
        string meta=Path.Combine(root,"archive",".astroarchive"); Directory.CreateDirectory(meta);
        string path=Path.Combine(meta,"protection.json"); File.WriteAllText(path,"{\"Mode\":\"Enabled\",\"Sid\":\"windows-account\",\"Root\":\"E:\\\\archive\"}");
        string before=File.ReadAllText(path); using var s=Session(); Open(s); Assert.Throws<PlatformNotSupportedException>(()=>Import(s));
        Assert.Equal(before,File.ReadAllText(path)); Assert.False(s.Repository!.OriginalsProtected);
    }
    [Fact]
    public void BackupRestoresIndexWorkingCopiesAndMetadata()
    {
        using var s=Session(); Open(s); Import(s); s.WorkingCopies(s.Captures,"copy",ct,_=>{}); s.Refresh();
        string zip=s.Backup(root,true,ct,_=>{}); string restore=Path.Combine(root,"restore");
        System.IO.Compression.ZipFile.ExtractToDirectory(zip,restore);
        using var restored=new ArchiveSession(Path.Combine(root,"restored-settings.json")); restored.Open(Path.Combine(restore,"Repository"));
        Assert.Equal(s.Captures.Select(f=>f.Hash).Order(),restored.Captures.Select(f=>f.Hash).Order());
        Assert.Equal(2,restored.Edited.Count); restored.Verify(ct,_=>{});
    }
    [Fact]
    public void SettingsPersistWithPrivatePermissionsAndCorruptSettingsRemainVisible()
    {
        using(var s=Session()) { s.Settings.Telescope="Scope Ω"; s.SaveSettings(); if(OperatingSystem.IsLinux()) Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite,File.GetUnixFileMode(s.ConfigPath)); }
        using(var reopen=Session()) Assert.Equal("Scope Ω",reopen.Settings.Telescope);
        File.WriteAllText(Path.Combine(root,"settings","settings.json"),"{broken");
        using var broken=Session(); Assert.Contains("could not be loaded",broken.SettingsWarning);
    }
    [Fact]
    public void DatabaseTransactionsRollbackAndBackupPreservesUnicode()
    {
        Directory.CreateDirectory(root); using var db=new Database(Path.Combine(root,"db.sqlite"));
        Assert.Throws<IOException>(()=>db.Transaction(()=>{ db.Exec("INSERT INTO files(hash,data) VALUES(?,?)","Ω","a?b'c"); throw new IOException("stop"); }));
        Assert.Empty(db.Query("SELECT hash FROM files")); db.Exec("INSERT INTO files(hash,data) VALUES(?,?)","Ω","a?b'c");
        string path=Path.Combine(root,"snapshot.sqlite"); db.BackupTo(path,ct);
        using var snapshot=new Database(path); Assert.Equal("a?b'c",snapshot.Query("SELECT data FROM files WHERE hash=?","Ω").Single());
    }
    public void Dispose() { if(Directory.Exists(root)) Directory.Delete(root,true); }
}
