using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AstroArchive.Desktop;

public static class ReleaseFixture
{
    public static string Prepare(string root)
    {
        var source = Path.Combine(root, "source with spaces Ω"); Directory.CreateDirectory(source);
        Fits(Path.Combine(source, "Light_M31.fit"), "M31", 12);
        Fits(Path.Combine(source, "Light_M45.fit"), "M45", 29);
        File.WriteAllText(Path.Combine(source, "broken.fit"), "invalid FITS, must remain visible");
        return source;
    }
    public static void Fits(string path, string target, int seed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string Card(string key, string value) => (key.PadRight(8) + "= " + value).PadRight(80);
        string header = (Card("SIMPLE", "T") + Card("BITPIX", "16") + Card("NAXIS", "2") +
            Card("NAXIS1", "64") + Card("NAXIS2", "48") + Card("OBJECT", "'"+target+"'") +
            Card("IMAGETYP", "'LIGHT'") + Card("DATE-OBS", "'2026-10-06T21:00:00'") +
            Card("EXPTIME", "60") + Card("GAIN", "80") + Card("FILTER", "'L'") + "END".PadRight(80)).PadRight(2880);
        byte[] bytes = new byte[2880+8640]; Encoding.ASCII.GetBytes(header).CopyTo(bytes, 0);
        for (int y=0; y<48; y++) for(int x=0; x<64; x++) {
            int value = 1800+seed+x*10+y*20; int at=2880+(y*64+x)*2;
            bytes[at]=(byte)(value>>8); bytes[at+1]=(byte)value;
        }
        File.WriteAllBytes(path, bytes);
    }
    public static void Check(bool condition, string message)
    { if(!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
}

// Runs from the self-contained installed executable. No SDK or network required.
public static class PackageSelfTest
{
    public static int Run(string? directory)
    {
        string root = Path.GetFullPath(directory ?? Path.Combine(Path.GetTempPath(), "astroarchive-check-"+Guid.NewGuid().ToString("N")));
        try {
            string source = ReleaseFixture.Prepare(root); Directory.CreateDirectory(Path.Combine(root,"exports"));
            string archive = Path.Combine(root,"archive");
            using (var session = new ArchiveSession(Path.Combine(root,"config","settings.json"))) {
                session.Open(archive);
                var scan=session.Scan(source,"Release scope","Auto",CancellationToken.None,_=>{});
                ReleaseFixture.Check(scan.Frames.Count==3 && scan.Errors.Count==1,"scan preserves unreadable rows");
                var result=session.Import(scan.Frames,CancellationToken.None,_=>{}); session.Refresh();
                ReleaseFixture.Check(result.Imported==2 && session.Captures.Count==2,"verified imports commit exactly two captures");
                ReleaseFixture.Check(Directory.GetFiles(source,"*.fit").Length==3,"source originals remain untouched");
                session.Verify(CancellationToken.None,_=>{});
                string export=session.Export(session.Captures,Path.Combine(root,"exports"),"originals",false,CancellationToken.None,_=>{});
                var hashes=Directory.GetFiles(export,"*.fit",SearchOption.AllDirectories).Select(p=>Util.Hash(p,CancellationToken.None)).Order().ToArray();
                ReleaseFixture.Check(hashes.SequenceEqual(session.Captures.Select(f=>f.Hash).Order()),"export bytes match independently hashed imports");
                session.WorkingCopies(session.Captures,"working",CancellationToken.None,_=>{}); session.Refresh();
                ReleaseFixture.Check(session.Edited.Count==2,"working copies populate Edited");
                session.Backup(Path.Combine(root,"exports"),true,CancellationToken.None,_=>{});
                ReleaseFixture.Check(File.Exists(session.LastOutput),"lossless verified backup created");
            }
            using(var reopened=new ArchiveSession(Path.Combine(root,"config","settings.json"))) {
                reopened.Open(archive); ReleaseFixture.Check(reopened.Captures.Count==2 && reopened.Edited.Count==2,"installed app reopens durable index and Edited records");
                reopened.Verify(CancellationToken.None,_=>{});
            }
            File.WriteAllText(Path.Combine(root,"package-check.txt"),"PASS: installed executable import/export/backup/reopen");
            Console.WriteLine("Package checks passed at " + root); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

public static class NativeSmoke
{
    public static string? Root { get; private set; }
    public static int Run(string? directory)
    {
        Root=Path.GetFullPath(directory ?? Path.Combine(Path.GetTempPath(),"astroarchive-ui-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
        return Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime([]);
    }
    public static async Task Check(MainWindow window, string root, bool screenshots)
    {
        string source=ReleaseFixture.Prepare(root), parent=Path.Combine(root,"exports"); Directory.CreateDirectory(parent);
        void Text(string id,string value) => ((TextBox)window.Controls[id]).Text=value;
        void Page(int index) { ((TabControl)window.Controls["Pages"]).SelectedIndex=index; window.UpdateLayout(); }
        async Task Click(string id) {
            ((Button)window.Controls[id]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await window.LastOperation;
            await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);
            ReleaseFixture.Check(window.LastError==null,"page command " + id + " completes: " + window.LastError);
        }
        Text("ArchivePath",Path.Combine(root,"archive")); await Click("OpenArchive");
        Page(1); Text("SourcePath",source); Text("Telescope","Smoke scope"); await Click("Scan");
        ReleaseFixture.Check(window.Session.Candidates.Count==3 && window.Session.Candidates.Any(f=>f.Status=="Unreadable"),"Import page shows good and unreadable files");
        await Click("ImportAll"); ReleaseFixture.Check(window.Session.Captures.Count==2,"Import page populates Repository");
        Page(0); var table=(DataGrid)window.Controls["Captures"]; table.SelectedItem=window.Session.Captures[0]; await Click("Preview");
        await Click("Verify");
        Text("Search","M31"); await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background); ReleaseFixture.Check(((IEnumerable<Frame>)table.ItemsSource).Count()==1,"Repository search filters visible rows"); Text("Search","");
        table.SelectedItem=window.Session.Captures.Single(f=>f.Target=="M31"); Text("CaptureDestination",parent); Text("CaptureName","original export"); await Click("Export");
        ReleaseFixture.Check(Directory.GetFiles(window.Session.LastOutput,"*.fit",SearchOption.AllDirectories).Length==1,"Repository exports only the selected capture");
        table.SelectedItem=window.Session.Captures.Single(f=>f.Target=="M31"); Text("CaptureName","stacking"); await Click("StackingExport");
        table.SelectedItem=window.Session.Captures.Single(f=>f.Target=="M31"); Text("CaptureName","working copies"); await Click("WorkingCopy");
        ReleaseFixture.Check(window.Session.Edited.Count==1,"Repository working-copy action adds Edited image");
        table.SelectedItem=window.Session.Captures.Single(f=>f.Target=="M31"); Text("Target","M33"); await Click("ApplyMetadata");
        ReleaseFixture.Check(window.Session.Captures.Any(f=>f.Target=="M33"),"metadata refiling updates the Repository");
        Page(2); Text("EditedPaths",Path.Combine(source,"Light_M45.fit")); await Click("AddEdited");
        ReleaseFixture.Check(window.Session.Edited.Count==2,"Edited page adds a finished image");
        ((DataGrid)window.Controls["EditedImages"]).SelectedItem=window.Session.Edited[0]; Text("EditedDestination",parent); Text("EditedName","edited export"); await Click("ExportEdited");
        ReleaseFixture.Check(Directory.GetFiles(window.Session.LastOutput,"*.fit").Length==1,"Edited page exports selected image");
        Page(3); Text("AnalyticsDestination",parent); Text("AnalyticsName","reports"); await Click("ExportAnalytics");
        ReleaseFixture.Check(File.ReadAllText(Path.Combine(window.Session.LastOutput,"analytics.svg")).Contains("<svg"),"Analytics exports a vector report");
        ReleaseFixture.Check(window.Session.Analytics.Reports.Count==6 && window.Session.Analytics.Seconds==120,"Analytics shows all six reports with exact integration");
        Page(4); Text("BackupDestination",parent); await Click("Backup"); await Click("SaveSettings");
        if(screenshots) {
            for(int i=0;i<6;i++) {
                Page(i); window.UpdateLayout(); await Task.Delay(100); window.UpdateLayout();
                using(var bitmap=new RenderTargetBitmap(new PixelSize((int)window.Width,(int)window.Height),new Vector(96,96))) {
                    bitmap.Render(window); bitmap.Save(Path.Combine(root,$"page-{i}.png"),new PngBitmapEncoderOptions());
                }
            }
        }
        ReleaseFixture.Check(Directory.GetFiles(source,"*.fit").Length==3,"all page workflows preserve source originals");
        File.WriteAllText(Path.Combine(root,"ui-check.txt"),"PASS: six pages; import, preview, search, export, stacking, metadata, Edited, analytics, backup, settings");
    }
    public static async void Start(MainWindow window,IClassicDesktopStyleApplicationLifetime desktop)
    {
        try { await Check(window,Root!,true); desktop.Shutdown(0); }
        catch(Exception e) { Console.Error.WriteLine(e); desktop.Shutdown(1); }
    }
}
