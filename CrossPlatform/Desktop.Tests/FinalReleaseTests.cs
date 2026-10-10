using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AstroArchive.Desktop;
using Xunit;

namespace AstroArchive.Desktop.Tests;

public sealed class FinalReleaseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "astroarchive-final-" + Guid.NewGuid().ToString("N"));
    private string Config => Path.Combine(root, "config", "settings.json");
    private static void Put(MainWindow window, string id, string value) => ((TextBox)window.Controls[id]).Text = value;
    private static async Task Click(MainWindow window, string id)
    {
        ((Button)window.Controls[id]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await window.LastOperation;
    }

    [AvaloniaFact]
    public async Task ImportPreferencesSurviveRestartWithoutSavingUnrelatedSettings()
    {
        var window = new MainWindow(new ArchiveSession(Config)); window.Show();
        try
        {
            Put(window, "ArchivePath", Path.Combine(root, "archive")); await Click(window, "OpenArchive");
            Put(window, "SourcePath", ReleaseFixture.Prepare(root)); await Click(window, "Scan");
            ((CheckBox)window.Controls["IgnoreFailed"]).IsChecked = true;
            ((CheckBox)window.Controls["IgnoreRaster"]).IsChecked = true;
            Put(window, "CopyWorkers", "3");
            Put(window, "Latitude", "invalid unrelated input");
            await Click(window, "ImportAll"); Assert.Null(window.LastError);
            using var reopened = new ArchiveSession(Config);
            Assert.True(reopened.Settings.IgnoreFailed); Assert.True(reopened.Settings.IgnoreRaster);
            Assert.Equal(3, reopened.Settings.CopyWorkers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task InvalidSettingsLeaveTheActiveSolverAndSavedPreferencesUnchanged()
    {
        var window = new MainWindow(new ArchiveSession(Config)); window.Show();
        try
        {
            Put(window, "AstapPath", "/original/solver"); Put(window, "Latitude", "51.5");
            await Click(window, "SaveAdvanced"); Assert.Null(window.LastError);
            string before = File.ReadAllText(Config);
            Put(window, "AstapPath", "/unintended/solver"); Put(window, "Latitude", "900");
            await Click(window, "SaveAdvanced"); Assert.Contains("Latitude", window.LastError);
            Assert.Equal("/original/solver", window.Session.Settings.Astap);
            Assert.Equal(51.5, window.Session.Settings.Latitude);
            Assert.Equal(before, File.ReadAllText(Config));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NullProfilesAndOutOfRangeSavedPreferencesDoNotPreventStartup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
        File.WriteAllText(Config, """
            {"Astap":null,"StarDatabase":null,"ApiKeyReference":null,"StackingExecutable":null,
             "Telescopes":[null,{"Id":null},{"Id":"Scope","Model":null,"LastSource":null}],
             "CopyWorkers":1000,"TextScale":-4,"AutoSolve":"Unexpected","PreviewStretch":null}
            """);
        var window = new MainWindow(new ArchiveSession(Config)); window.Show();
        try
        {
            Assert.Contains("settings", window.Session.SettingsWarning, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, window.Session.Settings.TextScale);
            Assert.InRange(window.Session.Settings.CopyWorkers, 1, 8);
            Assert.Equal("Off", window.Session.Settings.AutoSolve);
            Assert.Single(window.Session.Settings.Telescopes);
            Assert.NotNull(window.Session.SolverSettings().Astap);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ArchiveOpenAndCancelControlsFitAtMaximumTextScale()
    {
        var session = new ArchiveSession(Config); session.Settings.TextScale = 2;
        var window = new MainWindow(session) { Width = 1100, Height = 720 }; window.Show();
        try
        {
            Put(window, "ArchivePath", Path.Combine(root, "large-text-archive")); await Click(window, "OpenArchive");
            Put(window, "SourcePath", ReleaseFixture.Prepare(root)); await Click(window, "Scan");
            await Click(window, "ImportAll"); Assert.Null(window.LastError);
            ((TabControl)window.Controls["Pages"]).SelectedIndex=0;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); window.UpdateLayout();
            foreach (string name in new[] { "OpenArchive", "BrowseArchive", "Cancel" })
            {
                var control = window.Controls[name]; var origin = control.TranslatePoint(default, window)!.Value;
                Assert.InRange(origin.X + control.Bounds.Width, 1, window.Width);
                Assert.InRange(origin.Y + control.Bounds.Height, 1, window.Height);
            }
            var grid=(DataGrid)window.Controls["Captures"];
            var filename=grid.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text=="Light_M31.fit");
            Assert.Equal(28,filename.FontSize);
            Put(window,"TextScale","1"); await Click(window,"SaveAdvanced"); Assert.Null(window.LastError);
            window.UpdateLayout(); Assert.Equal(14,grid.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text=="Light_M31.fit").FontSize);
        }
        finally { window.Close(); }
    }

    [Fact]
    public async Task EditorLaunchPassesUnicodeAndShellMetacharactersAsOneLiteralArgument()
    {
        Directory.CreateDirectory(root);
        string executable = Path.Combine(root, "editor with spaces Ω"), received = Path.Combine(root, "received.txt");
        File.WriteAllText(executable, "#!/bin/sh\nprintf '%s\\n' \"$#\" \"$@\" > '" + received + "'\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string input = Path.Combine(root, "image Ω $(touch INJECTED) `touch INJECTED` 'quoted'.fit");
        ArchiveSession.LaunchEditor(executable, input);
        for (int i = 0; i < 100 && !File.Exists(received); i++) await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "1", input }, File.ReadAllLines(received));
        Assert.False(File.Exists(Path.Combine(Environment.CurrentDirectory, "INJECTED")));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
