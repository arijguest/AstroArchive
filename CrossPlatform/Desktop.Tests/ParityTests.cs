using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.Interactivity;
using AstroArchive.Desktop;
using AstroArchive.Remote;
using BitMiracle.LibTiff.Classic;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Xunit;
namespace AstroArchive.Desktop.Tests;

public sealed class ParityTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"astroarchive-parity-"+Guid.NewGuid().ToString("N"));
    private CancellationToken Ct=>TestContext.Current.CancellationToken;
    private ArchiveSession Session() { var session=new ArchiveSession(Path.Combine(root,"config","settings.json")); session.Open(Path.Combine(root,"archive")); return session; }
    private void Import(ArchiveSession session) { var source=ReleaseFixture.Prepare(root); var plan=session.Scan(source,"Scope","Auto",Ct,_=>{}); session.Import(plan.Frames,Ct,_=>{}); session.Refresh(); }
    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
    private static void Put(MainWindow window,string id,string value)=>((TextBox)window.Controls[id]).Text=value;
    private static async Task Click(MainWindow window,string id) { ((Button)window.Controls[id]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await window.LastOperation; Assert.Null(window.LastError); }
    [AvaloniaFact]
    public async Task NewRepositoryAndSettingsControlsExecuteAndPreserveOriginals()
    {
        var window=new MainWindow(Session()); window.Show();
        try {
            Import(window.Session); var grid=(DataGrid)window.Controls["Captures"]; grid.SelectedItem=window.Session.Captures.Single(f=>f.Target=="M31");
            await Click(window,"Details"); Assert.Contains("Original",((TextBox)window.Controls["Report"]).Text);
            await Click(window,"ScreenArchive"); Assert.Contains("Screened 1",window.Session.LastReport);
            grid.SelectedItem=window.Session.Captures.Single(f=>f.Target=="M31");
            Put(window,"Meta:Exposure","120.5"); Put(window,"Meta:Gain","42"); Put(window,"Meta:Binning","2x2");
            await Click(window,"Meta:Apply"); var capture=window.Session.Captures.Single(f=>f.Target=="M31");
            Assert.Equal(120.5,capture.Exposure); Assert.Equal(42,capture.Gain); Assert.Equal(2,capture.BinX);
            Assert.Equal(capture.Hash,Util.Hash(window.Session.Repository!.FilePath(capture),Ct));
            ((ComboBox)window.Controls["Filter:Frame type"]).SelectedItem="Light"; Put(window,"MinExposure","100"); await Click(window,"ApplyFilters");
            Assert.Single(((IEnumerable<Frame>)grid.ItemsSource)); Put(window,"MinExposure","");
            Put(window,"Latitude","51.5"); Put(window,"Longitude","-0.12"); Put(window,"TextScale","1.25"); await Click(window,"SaveAdvanced");
            Assert.Equal(51.5,window.Session.Settings.Latitude); Assert.Equal(17.5,window.FontSize);
            grid.SelectedItem=window.Session.Captures[0]; await Click(window,"Rotation"); Assert.Contains("five timestamped",window.Session.LastReport);
            await Click(window,"EnableProtection"); Assert.True(window.Session.Repository.OriginalsProtected);
            grid.SelectedItem=window.Session.Captures[0]; Put(window,"DeleteCapturesConfirm","DELETE");
            ((Button)window.Controls["DeleteCaptures"]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await window.LastOperation;
            Assert.Contains("protection",window.LastError); Assert.Equal(2,window.Session.Captures.Count);
            await Click(window,"DisableProtection"); grid.SelectedItem=window.Session.Captures[0]; Put(window,"DeleteCapturesConfirm","DELETE"); await Click(window,"DeleteCaptures");
            Assert.Single(window.Session.Captures); Assert.Equal(3,Directory.GetFiles(Path.Combine(root,"source with spaces Ω"),"*.fit").Length);
        } finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task NewImportEditedAndSolverControlsUseRealEngine()
    {
        var window=new MainWindow(Session()); window.Show();
        try {
            string source=ReleaseFixture.Prepare(root); Put(window,"SourcePath",source); Put(window,"Telescope","Scope"); await Click(window,"SaveProfile");
            ((ComboBox)window.Controls["Profiles"]).SelectedItem="Scope"; Put(window,"SourcePath",""); await Click(window,"LoadProfile"); Assert.Equal(source,window.Text("SourcePath"));
            await Click(window,"Scan"); var candidates=(DataGrid)window.Controls["Candidates"]; candidates.SelectedItem=window.Session.Candidates.First(f=>f.Status=="New");
            Put(window,"ImportMeta:Filter","Ha"); await Click(window,"ImportMeta:Apply"); await Click(window,"ScreenImports"); await Click(window,"ImportSelected"); Assert.Single(window.Session.Captures);
            var captures=(DataGrid)window.Controls["Captures"]; captures.SelectedItem=window.Session.Captures[0];
            string solver=ReleaseFixture.FakeAstap(Path.Combine(root,"ASTAP directory with spaces Ω")); Put(window,"AstapPath",solver); await Click(window,"SaveAdvanced");
            await Click(window,"Solve"); Assert.Single(window.Session.Solutions); Assert.True(window.Session.Solutions[0].Solved);
            ((DataGrid)window.Controls["Solutions"]).SelectedItem=window.Session.Solutions[0]; Put(window,"SolvedTarget","M33"); await Click(window,"ApplySolutions");
            Assert.Equal("M33",window.Session.Captures.Single().Target); Assert.Equal(10.6847,window.Session.Captures.Single().RA);
            captures.SelectedItem=window.Session.Captures[0]; Put(window,"CaptureName","working"); await Click(window,"WorkingCopy");
            var edited=(DataGrid)window.Controls["EditedImages"]; edited.SelectedItem=window.Session.Edited.Single(); Put(window,"EditedObject","M45"); Put(window,"EditedSubs","20"); Put(window,"EditedSeconds","1200");
            await Click(window,"EditFinished"); Assert.Equal(20,window.Session.Edited.Single().Metadata.Subs); Assert.Equal("M45",window.Session.Edited.Single().Metadata.Object);
            edited.SelectedItem=window.Session.Edited.Single(); await Click(window,"EditedDetails"); Assert.Contains("1200",window.Session.LastReport); await Click(window,"EditedPreview");
            edited.SelectedItem=window.Session.Edited.Single(); Put(window,"DeleteEditedConfirm","DELETE"); await Click(window,"DeleteEdited"); Assert.Empty(window.Session.Edited); Assert.Single(window.Session.Captures);
        } finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task NetworkControlsImportThroughSimulatorAndRejectStaleConnection()
    {
        var window=new MainWindow(Session()); window.Show();
        try {
            string source=ReleaseFixture.Prepare(root); ((ComboBox)window.Controls["RemoteKind"]).SelectedItem="Local simulator"; Put(window,"RemoteFolder",source); Put(window,"RemoteLimit","0");
            await Click(window,"ListRemote"); Assert.Equal(3,window.Session.RemoteFiles.Count);
            var grid=(DataGrid)window.Controls["RemoteFiles"]; foreach(var entry in window.Session.RemoteFiles.Where(e=>e.Name!="broken.fit"))grid.SelectedItems.Add(entry);
            await Click(window,"ImportRemote"); Assert.Equal(2,window.Session.Captures.Count); window.Session.Verify(Ct,_=>{});
            Put(window,"RemoteHost","127.0.0.2"); ((Button)window.Controls["ImportRemote"]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await window.LastOperation; Assert.Contains("changed since listing",window.LastError);
            Assert.Equal(3,Directory.GetFiles(source,"*.fit").Length);
        } finally { window.Close(); }
    }
    [Fact]
    public async Task LiveImportsWaitForStableFilesAndCancelWithoutDuplicates()
    {
        using var session=Session(); string source=Path.Combine(root,"telescope"); Directory.CreateDirectory(source);
        var connection=new Connection { Kind="Local simulator",Folder=source,PollSeconds=2,LimitMB=0,IncludeExisting=true };
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(Ct); cancel.CancelAfter(TimeSpan.FromSeconds(18));
        var imported=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task=Task.Run(()=>session.ImportRemote(connection,[],new TelescopeProfile { Id="Live scope",Model="Auto" },true,cancel.Token,p=> { if(p.Stage=="Live import"&&p.Text.Contains("1 imported"))imported.TrySetResult(); }),Ct);
        ReleaseFixture.Fits(Path.Combine(source,"live.fit"),"M31",17);
        await imported.Task.WaitAsync(TimeSpan.FromSeconds(14),Ct); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);
        session.Refresh(); Assert.Single(session.Captures); Assert.True(File.Exists(Path.Combine(source,"live.fit"))); session.Verify(Ct,_=>{});
        var list=session.ListRemote(connection,Ct,_=>{}); session.ShowRemoteFiles(list); session.ImportRemote(connection,list,new TelescopeProfile { Id="Live scope",Model="Auto" },false,Ct,_=>{}); session.Refresh(); Assert.Single(session.Captures);
    }
    [Fact]
    public void ProtectionPersistsAndRejectsMissingCaptureDeletion()
    {
        string archive; using(var session=Session()) { Import(session); archive=session.Repository!.Root; session.Repository.SetOriginalsProtection(true,Ct,_=>{}); File.Delete(session.Repository.FilePath(session.Captures[0])); Assert.Throws<IOException>(()=>session.Repository.DeleteFrames(session.Captures,Ct,_=>{})); }
        using var reopened=Session(); Assert.True(reopened.Repository!.OriginalsProtected); Assert.Equal(2,reopened.Captures.Count);
    }
    [Fact]
    public void SessionCredentialsNeverReachSettingsAndAreForgotten()
    {
        string reference=LinuxSecrets.Temporary("test-api-secret"); Assert.Equal("test-api-secret",PlateSolve.Unprotect(reference)); LinuxSecrets.Forget(reference); Assert.Equal("",PlateSolve.Unprotect(reference));
        using var session=Session(); session.UseSessionKey("never-write-this-key"); session.SaveSettings(); Assert.DoesNotContain("never-write",File.ReadAllText(session.ConfigPath));
    }
    [Fact]
    public void AstapAcceptsLinuxQuotesInCataloguePathAndLeavesSourceBytesIntact()
    {
        using var session=Session(); Import(session); var capture=session.Captures[0]; string path=session.Repository!.FilePath(capture),before=Util.Hash(path,Ct);
        session.Settings.Astap=ReleaseFixture.FakeAstap(Path.Combine(root,"solver Ω")); session.Settings.StarDatabase=Path.Combine(root,"catalogue \"quoted\" $() Ω"); Directory.CreateDirectory(session.Settings.StarDatabase);
        var jobs=session.Solve([capture],Ct,_=>{}); Assert.True(jobs.Single().Solved,jobs.Single().Error); Assert.Equal(41.269,jobs.Single().Result.Dec); Assert.Equal(before,Util.Hash(path,Ct));
        Assert.Contains(session.Settings.StarDatabase,File.ReadAllText(Path.Combine(Path.GetDirectoryName(session.Settings.Astap)!,"arguments.txt")));
    }
    [Fact]
    public async Task SolverCancellationKillsItsChildProcessAndKeepsArchiveUsable()
    {
        using var session=Session(); Import(session); string directory=Path.Combine(root,"sleep solver"); Directory.CreateDirectory(directory); string executable=Path.Combine(directory,"astap");
        File.WriteAllText(executable,"#!/bin/sh\nsleep 120 &\necho $! > child.pid\nwait\n"); File.SetUnixFileMode(executable,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute); session.Settings.Astap=executable;
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(Ct); cancel.CancelAfter(500);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Task.Run(()=>session.Solve(session.Captures,cancel.Token,_=>{}),Ct));
        int pid=int.Parse(File.ReadAllText(Path.Combine(directory,"child.pid"))); Assert.True(SpinWait.SpinUntil(()=> { try { string stat=File.Exists("/proc/"+pid+"/stat")?File.ReadAllText("/proc/"+pid+"/stat"):"";return stat.Length==0||stat.Split(' ')[2]=="Z"; }catch(IOException){return true;} },TimeSpan.FromSeconds(2))); session.Verify(Ct,_=>{}); Assert.Equal(2,session.Captures.Count);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Png16BitFiltersPreserveExactScientificSamples(int filter)
    {
        Directory.CreateDirectory(root); string path=Path.Combine(root,"M31.png"); WritePng(path,filter);
        var reader=new LinuxRasterReader(); var asset=reader.Inspect(path,_=>{}); var image=reader.Read(path,asset.Images[0],0,Ct);
        Assert.Equal(16,asset.Images[0].Bitpix); Assert.Equal("raster",asset.Images[0].Encoding); Assert.Equal(new double[]{0,257,32768,65535,13,60000},image.Pixels); Assert.Equal("M31",asset.Header.Get("OBJECT"));
        byte[] corrupted=File.ReadAllBytes(path); corrupted[^5]^=1; File.WriteAllBytes(path,corrupted); Assert.Throws<InvalidDataException>(()=>reader.Inspect(path,_=>{}));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FloatingTiffPagesPreserveValuesAndRejectInvalidPage(bool bigEndian)
    {
        Directory.CreateDirectory(root); string path=Path.Combine(root,"floating.tif");
        using(var tiff=Tiff.Open(path,bigEndian?"wb":"wl")) {
            for(int page=0;page<2;page++) {
                tiff.SetField(TiffTag.IMAGEWIDTH,3);tiff.SetField(TiffTag.IMAGELENGTH,2);tiff.SetField(TiffTag.BITSPERSAMPLE,32);tiff.SetField(TiffTag.SAMPLESPERPIXEL,1);tiff.SetField(TiffTag.SAMPLEFORMAT,SampleFormat.IEEEFP);tiff.SetField(TiffTag.PHOTOMETRIC,Photometric.MINISBLACK);tiff.SetField(TiffTag.PLANARCONFIG,PlanarConfig.CONTIG);tiff.SetField(TiffTag.ROWSPERSTRIP,2);tiff.SetField(TiffTag.ORIENTATION,Orientation.TOPLEFT);
                foreach(int y in new[]{0,1}) { var bytes=new[]{-0.125f+page+y,0.5f+page+y,1234.75f+page+y}.SelectMany(BitConverter.GetBytes).ToArray(); Assert.True(tiff.WriteScanline(bytes,y)); } tiff.WriteDirectory();
            }
        }
        var reader=new LinuxRasterReader();var asset=reader.Inspect(path,_=>{});Assert.Equal(2,asset.Images.Count);var image=reader.Read(path,asset.Images[1],0,Ct);
        Assert.Equal(new double[]{0.875,1.5,1235.75,1.875,2.5,1236.75},image.Pixels);Assert.Equal(-32,asset.Images[1].Bitpix);
    }
    [Fact]
    public void RotationRecoversKnownMotionAndPreservesUserMountAssignment()
    {
        using var session=Session(); string source=Path.Combine(root,"stars");
        for(int i=0;i<6;i++)StarFits(Path.Combine(source,"Light_M31_"+i+".fit"),i*.15,i*3);
        var plan=session.Scan(source,"Rotation scope","Auto",Ct,_=>{}); Assert.Equal(6,session.Import(plan.Frames,Ct,_=>{}).Imported);session.Refresh();
        var first=session.Captures.First(); session.Metadata([first],new Dictionary<string,string>{{"Mount","EQ"}},false,Ct);session.Refresh();
        var results=session.AnalyzeRotation(session.Captures,Ct,_=>{}); Assert.Single(results); var result=results[0];
        Assert.Equal(6,result.Points.Count); Assert.InRange(Math.Abs(result.RateDegreesMinute),.045,.055);Assert.InRange(result.DriftDegrees,.70,.80);Assert.Equal("Alt-Az?",result.Mount);
        session.Refresh();Assert.All(session.Captures,frame=>Assert.Contains("Matched 6/6",frame.RotationReport));Assert.Equal("EQ",session.Captures.Single(f=>f.Hash==first.Hash).Mount);session.Verify(Ct,_=>{});
    }
    [Fact]
    public async Task OnlineSolverProtocolSendsCoordinatesAndReadsCalibrationWithoutUploadingPixels()
    {
        Directory.CreateDirectory(root);string path=Path.Combine(root,"stars.fit");StarFits(path,0,0);
        using var reserve=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);reserve.Start();int port=((System.Net.IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        using var listener=new System.Net.HttpListener();listener.Prefixes.Add("http://127.0.0.1:"+port+"/");listener.Start();
        var bodies=new List<string>();using var stop=CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var server=Task.Run(async()=> { while(!stop.IsCancellationRequested) {
            System.Net.HttpListenerContext context;try{context=await listener.GetContextAsync().WaitAsync(stop.Token);}catch(Exception)when(stop.IsCancellationRequested){break;}
            using var reader=new StreamReader(context.Request.InputStream);string body=await reader.ReadToEndAsync(Ct);bodies.Add(body);
            string route=context.Request.Url!.AbsolutePath;
            string reply=route=="/api/login"?"{\"session\":\"fixture-session\"}":route=="/api/upload"?"{\"subid\":123}":route=="/api/submissions/123"?"{\"jobs\":[456]}":route=="/api/jobs/456"?"{\"status\":\"success\"}":route.EndsWith("calibration/")?"{\"ra\":10.6847,\"dec\":41.269,\"radius\":1}":"";
            if(reply.Length==0)context.Response.StatusCode=404;byte[] bytes=Encoding.UTF8.GetBytes(reply);await context.Response.OutputStream.WriteAsync(bytes,Ct);context.Response.Close();
        } },Ct);
        string previous=PlateSolve.ApiRoot,reference=LinuxSecrets.Temporary("synthetic-api-key");PlateSolve.ApiRoot="http://127.0.0.1:"+port;
        try {
            var settings=new Settings { UseOnline=true,ApiKeyProtected=reference };var solved=await Task.Run(()=>PlateSolve.Solve(path,settings,Ct,_=>{}),Ct);
            Assert.Equal(10.6847,solved.RA);Assert.Equal(41.269,solved.Dec);Assert.Equal("M31",solved.Suggested);
            Assert.Contains("synthetic-api-key",Uri.UnescapeDataString(bodies[0]));Assert.Contains("stars.xyls",bodies[1]);Assert.Contains("BINTABLE",bodies[1]);Assert.DoesNotContain("OBJECT",bodies[1]);Assert.True(bodies[1].Length<20000);Assert.Contains("publicly_visible",bodies[1]);Assert.Contains("\"n\"",bodies[1]);
        } finally { PlateSolve.ApiRoot=previous;LinuxSecrets.Forget(reference);stop.Cancel();listener.Stop();await server; }
    }
    [Fact]
    public void RecoveryKeepsCaseDistinctSelectionsAndResumesAfterProcessInterruption()
    {
        using var session=Session();string source=Path.Combine(root,"resume-source");ReleaseFixture.Fits(Path.Combine(source,"LIGHT.fit"),"M31",1);ReleaseFixture.Fits(Path.Combine(source,"light.fit"),"M45",2);
        var scan=session.Scan(source,"Resume scope","Auto",Ct,_=>{});var record=new ImportResumeRecord { Kind="Files",Repository=session.Repository!.Root,Source=source,Frames=scan.Frames,Profile=new TelescopeProfile { Id="Resume scope",Model="Auto" },Workers=1 };
        var store=new ImportResumeStore(Path.Combine(Path.GetDirectoryName(session.ConfigPath)!,"import-jobs"));store.Save(record);
        session.Repository.Import([scan.Frames[0]],Ct,_=>{},new ImportOptions { SourceRoot=source,Workers=1,OnFrame=f=>store.Completed(record,f) });
        session.Refresh();Assert.Single(session.RecoverableImports);Assert.Equal("Interrupted",session.RecoverableImports[0].State);
        session.ResumeImport(session.RecoverableImports[0],"",Ct,_=>{});session.Refresh();Assert.Equal(2,session.Captures.Count);Assert.Empty(session.RecoverableImports);session.Verify(Ct,_=>{});
    }
    [Fact]
    public void FailedImportRetainsRecoveryAndRepairAllowsRetry()
    {
        using var session=Session();Import(session);string source=Path.Combine(root,"retry-source");string path=Path.Combine(source,"Light_M42.fit");ReleaseFixture.Fits(path,"M42",5);
        var scan=session.Scan(source,"Scope","Auto",Ct,_=>{});byte[] original=File.ReadAllBytes(path);File.AppendAllText(path,"changed");Assert.Equal(1,session.Import(scan.Frames,Ct,_=>{}).Failed);session.Refresh();Assert.Single(session.RecoverableImports);
        File.WriteAllBytes(path,original);File.SetLastWriteTimeUtc(path,new DateTime(scan.Frames[0].SourceStamp.Modified,DateTimeKind.Utc));session.ResumeImport(session.RecoverableImports[0],"",Ct,_=>{});session.Refresh();Assert.Equal(3,session.Captures.Count);Assert.Empty(session.RecoverableImports);
    }
    [Fact]
    public void AnalyticsExportsPdfAndEveryRasterPageAndStackingUsesArgumentVector()
    {
        using var session=Session();Import(session);string destination=Path.Combine(root,"exports");Directory.CreateDirectory(destination);string output=session.ExportAnalytics(destination,"reports",Ct);
        Assert.Equal("%PDF",Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(output,"analytics.pdf")),0,4));Assert.Equal(6,Directory.GetFiles(output,"*.png").Length);
        foreach(string image in Directory.GetFiles(output,"*.png")){using var bitmap=SkiaSharp.SKBitmap.Decode(image);Assert.Equal(1200,bitmap.Width);Assert.Equal(800,bitmap.Height);}
        string executable=Path.Combine(root,"siril");File.WriteAllText(executable,"placeholder");var info=ArchiveSession.StackingLaunchInfo(executable,output);Assert.False(info.UseShellExecute);Assert.Equal(new[]{"--directory",output},info.ArgumentList);
    }
    [AvaloniaFact]
    public async Task PointerClicksOnExpandedSolverControlsRunTheCommands()
    {
        var window=new MainWindow(Session());window.Show();
        try { Import(window.Session);((TabControl)window.Controls["Pages"]).SelectedIndex=0; await Click(window,"Refresh");
            window.Session.Settings.Astap=ReleaseFixture.FakeAstap(Path.Combine(root,"pointer-solver"));
            await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);window.UpdateLayout();
            var row=((DataGrid)window.Controls["Captures"]).GetVisualDescendants().OfType<DataGridRow>().First(r=>r.DataContext is Frame);
            var rowPoint=row.TranslatePoint(new Point(80,row.Bounds.Height/2),window)!.Value;
            window.MouseDown(rowPoint,Avalonia.Input.MouseButton.Left);window.MouseUp(rowPoint,Avalonia.Input.MouseButton.Left);
            ((Expander)window.Controls["Section:Target identification and rotation"]).IsExpanded=true;
            foreach(string name in new[]{"Solve","ApplySolutions"}) {
                if(name=="ApplySolutions")((DataGrid)window.Controls["Solutions"]).SelectedItem=window.Session.Solutions[0];
                Assert.Single(((DataGrid)window.Controls[name=="Solve"?"Captures":"Solutions"]).SelectedItems);
                var button=(Button)window.Controls[name];window.UpdateLayout();button.BringIntoView();window.UpdateLayout();
                Assert.Single(((DataGrid)window.Controls[name=="Solve"?"Captures":"Solutions"]).SelectedItems);
                var point=button.TranslatePoint(new Avalonia.Point(button.Bounds.Width/2,button.Bounds.Height/2),window)!.Value;
                Assert.InRange(point.Y,0,window.Height);window.MouseDown(point,Avalonia.Input.MouseButton.Left); window.MouseUp(point,Avalonia.Input.MouseButton.Left);await window.LastOperation;Assert.Null(window.LastError);
                Assert.NotEmpty(window.Session.Solutions);
            }
            Assert.Contains(window.Session.Captures,f=>f.RA==10.6847);
        } finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task SkyContextSupportsKeyboardAndPointerNavigation()
    {
        var window=new MainWindow(Session());window.Show();
        try { Import(window.Session);await Click(window,"Refresh");((TabControl)window.Controls["Pages"]).SelectedIndex=0;((DataGrid)window.Controls["Captures"]).SelectedItem=window.Session.Captures.Single(f=>f.Target=="M31");await Click(window,"SkyContext");
            var sky=(SkyGlobe)window.Controls["SkyGlobe"];Assert.True(sky.Context.HasPosition);Assert.True(sky.Context.ApproximatePosition);double initial=sky.Camera.Yaw;
            window.UpdateLayout();sky.BringIntoView();window.UpdateLayout();sky.Focus();window.KeyPress(Avalonia.Input.Key.Right,Avalonia.Input.RawInputModifiers.None,Avalonia.Input.PhysicalKey.ArrowRight,null);window.KeyRelease(Avalonia.Input.Key.Right,Avalonia.Input.RawInputModifiers.None,Avalonia.Input.PhysicalKey.ArrowRight,null);Assert.NotEqual(initial,sky.Camera.Yaw);
            var point=sky.TranslatePoint(new Point(160,160),window)!.Value;window.MouseDown(point,Avalonia.Input.MouseButton.Left);window.MouseMove(point+new Vector(30,20));window.MouseUp(point+new Vector(30,20),Avalonia.Input.MouseButton.Left);Assert.NotEqual(initial-10,sky.Camera.Yaw);
            await Click(window,"ResetSky");Assert.Equal(initial,sky.Camera.Yaw);
        } finally { window.Close(); }
    }
    [Fact]
    public void AutomaticAnalysisIsOptInAndKeepsVerifiedCaptureBytes()
    {
        using var session=Session();session.Settings.Astap=ReleaseFixture.FakeAstap(Path.Combine(root,"automatic-solver"));session.Settings.AutoSolve="All";session.Settings.AutoRotation="Unknown mounts";Import(session);
        Assert.All(session.Captures,frame=>{Assert.Equal("M31",frame.Target);Assert.Equal(10.6847,frame.RA);Assert.NotEmpty(frame.RotationReport);});Assert.Contains("Automatic analysis",session.LastReport);session.Verify(Ct,_=>{});
    }
    [Fact]
    public void BackupRestoresWithCaptureGuardDisabledAndInvalidMetadataChangesNothing()
    {
        using var session=Session();Import(session);session.Repository!.SetOriginalsProtection(true,Ct,_=>{});string parent=Path.Combine(root,"backups");Directory.CreateDirectory(parent);
        string output=session.Backup(parent,false,Ct,_=>{});using var restored=new Repository(Path.Combine(output,"Repository"));Assert.False(restored.OriginalsProtected);Assert.Equal(2,restored.All().Count);Assert.Equal(0,restored.Verify(Ct,_=>{}));
        Assert.Throws<ArgumentException>(()=>session.Metadata(session.Captures,new Dictionary<string,string>{{"Gain","NaN"}},false,Ct));Assert.All(session.Repository.All(),f=>Assert.Equal(80,f.Gain));
        Assert.Throws<ArgumentException>(()=>session.Delete(session.Captures,[],"",Ct,_=>{}));Assert.Equal(2,session.Repository.All().Count);
    }
    [AvaloniaFact]
    public async Task DeletionHistoryDumpAndFinishedFolderButtonsUseSharedRules()
    {
        var window=new MainWindow(Session());window.Show();
        try { Import(window.Session);await Click(window,"Refresh");var original=window.Session.Captures[0];((DataGrid)window.Controls["Captures"]).SelectedItem=original;Put(window,"DeleteCapturesConfirm","DELETE");await Click(window,"DeleteCaptures");
            Assert.Single(window.Session.Deleted);((DataGrid)window.Controls["DeletedCaptures"]).SelectedItem=window.Session.Deleted[0];await Click(window,"DeletionAudit");Assert.Contains("import excluded",window.Session.LastReport);
            ((DataGrid)window.Controls["DeletedCaptures"]).SelectedItem=window.Session.Deleted[0];await Click(window,"AllowReimport");Assert.False(window.Session.Deleted[0].Excluded);
            Put(window,"SourcePath",Path.Combine(root,"source with spaces Ω"));await Click(window,"Scan");await Click(window,"ImportAll");Assert.Equal(2,window.Session.Captures.Count);
            string dump=window.Session.RequireArchive().DumpFolder;window.Session.RequireArchive().EnsureDumpFolder();ReleaseFixture.Fits(Path.Combine(dump,"Light_M42.fit"),"M42",47);await Click(window,"ImportDump");Assert.Equal(3,window.Session.Captures.Count);Assert.True(File.Exists(Path.Combine(dump,"Light_M42.fit")));
            string finished=Path.Combine(root,"finished","nested","edited_M45.fit");ReleaseFixture.Fits(finished,"M45",51);Put(window,"EditedFolder",Path.Combine(root,"finished"));await Click(window,"ImportEditedFolder");Assert.Single(window.Session.Edited);Assert.Contains("nested",window.Session.Edited[0].RelativePath);window.Session.Verify(Ct,_=>{});
        } finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task AnalyticsScopeLayoutsAndAnimationControlsProduceRealFiles()
    {
        var window=new MainWindow(Session());window.Show();
        try {
            Import(window.Session);await Click(window,"Refresh");((TabControl)window.Controls["Pages"]).SelectedIndex=3;
            Put(window,"AnalyticsTelescope","absent scope");await Click(window,"ApplyAnalyticsScope");Assert.Equal(0,window.Session.Analytics.Captures);Assert.Equal(2,window.Session.Analytics.RepositoryCaptures);
            Put(window,"AnalyticsTelescope","");Put(window,"AnalyticsFrom","2026-10-06");Put(window,"AnalyticsTo","2026-10-06");await Click(window,"ApplyAnalyticsScope");Assert.Equal(2,window.Session.Analytics.Captures);
            Put(window,"AnalyticsFrom","2026-10-07");((Button)window.Controls["ApplyAnalyticsScope"]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await window.LastOperation;Assert.Contains("start date",window.LastError);Assert.Equal(2,window.Session.Analytics.Captures);Put(window,"AnalyticsFrom","2026-10-06");
            ((ComboBox)window.Controls["AnalyticsLayout"]).SelectedItem="Portrait";((ComboBox)window.Controls["AnalyticsTheme"]).SelectedItem="Dark";((ComboBox)window.Controls["AnalyticsFormat"]).SelectedItem="GIF";
            Put(window,"AnalyticsSeconds",".5");Put(window,"AnalyticsFps","4");Put(window,"AnalyticsResolution","160");string parent=Path.Combine(root,"analytics");Directory.CreateDirectory(parent);Put(window,"AnalyticsDestination",parent);Put(window,"AnalyticsName","portrait story");await Click(window,"ExportAnalytics");
            string output=window.Session.LastOutput;Assert.Equal("GIF89a",Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(output,"analytics-story.gif")),0,6));
            using var png=SkiaSharp.SKBitmap.Decode(Path.Combine(output,"analytics-page-1.png"));using var jpeg=SkiaSharp.SKBitmap.Decode(Path.Combine(output,"analytics-page-1.jpg"));Assert.Equal((1080,1350),(png.Width,png.Height));Assert.Equal((png.Width,png.Height),(jpeg.Width,jpeg.Height));Assert.Equal(6,Directory.GetFiles(output,"*.png").Length);Assert.Contains("1080",File.ReadAllText(Path.Combine(output,"analytics.svg")));
        } finally { window.Close(); }
    }
    [Fact]
    public void CancelledAnimatedExportRemovesItsPartialFile()
    {
        using var session=Session();Import(session);using var stream=typeof(Repository).Assembly.GetManifestResourceStream("AstroArchive_Logo.png")!;using var logo=new MemoryStream();stream.CopyTo(logo);Directory.CreateDirectory(root);string destination=Path.Combine(root,"cancelled.gif");using var cancel=new CancellationTokenSource();
        var pages=new[]{AnalyticsGraphics.Page(session.Analytics,0,true)};
        Assert.ThrowsAny<OperationCanceledException>(()=>LinuxAnalyticsAnimation.Save(destination,pages,logo.ToArray(),"GIF",new LinuxAnimationOptions { SecondsPerChart=2,FramesPerSecond=4,MaximumEdge=160 },cancel.Token,(percent,_)=>cancel.Cancel()));
        Assert.False(File.Exists(destination));Assert.Empty(Directory.GetFiles(root,"*.tmp.*"));
    }
    [AvaloniaFact]
    public async Task RemoteAndRecoveryTablesRenderPublicFieldsAndSortNumericValues()
    {
        var window=new MainWindow(Session());window.Show();
        try {
            ReleaseFixture.Prepare(root);((TabControl)window.Controls["Pages"]).SelectedIndex=1;((ComboBox)window.Controls["RemoteKind"]).SelectedItem="Local simulator";Put(window,"RemoteFolder",Path.Combine(root,"source with spaces Ω"));await Click(window,"ListRemote");
            ((Expander)window.Controls["Section:Telescope network imports"]).IsExpanded=true;window.UpdateLayout();await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);window.UpdateLayout();
            var grid=(DataGrid)window.Controls["RemoteFiles"];Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(),t=>t.Text=="Light_M31.fit");Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(),t=>t.Text==new FileInfo(Path.Combine(root,"source with spaces Ω","Light_M31.fit")).Length.ToString());
            var comparer=grid.Columns[1].CustomSortComparer;Assert.True(comparer.Compare(new Entry{Size=2},new Entry{Size=10})<0);
            var record=new ImportResumeRecord { Kind="Files",Title="Visible paused import",State="Paused",Repository=window.Session.RequireArchive().Root,Frames=[] };new ImportResumeStore(Path.Combine(Path.GetDirectoryName(window.Session.ConfigPath)!,"import-jobs")).Save(record);await Click(window,"Refresh");
            ((Expander)window.Controls["Section:Paused and interrupted imports"]).IsExpanded=true;window.UpdateLayout();await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);window.UpdateLayout();
            var recovery=(DataGrid)window.Controls["RecoverableImports"];Assert.Contains(recovery.GetVisualDescendants().OfType<TextBlock>(),t=>t.Text=="Visible paused import");Assert.Contains(recovery.GetVisualDescendants().OfType<TextBlock>(),t=>t.Text=="Paused");
        } finally { window.Close(); }
    }
    [Fact]
    public async Task DiscoveryUsesActualUdpRepliesAndReleasesSocketsAfterCancellation()
    {
        using var responder=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.2"),4720));
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var task=Task.Run(async()=> { while(!stop.IsCancellationRequested) { try { var packet=await responder.ReceiveAsync(stop.Token);byte[] reply=Encoding.UTF8.GetBytes("{\"method\":\"scan_iscope\",\"code\":0,\"result\":{\"product_model\":\"Seestar S50\",\"sn\":\"fixture\"}}");await responder.SendAsync(reply,packet.RemoteEndPoint,stop.Token); } catch(OperationCanceledException){break;} } },Ct);
        try { var network=new DiscoveryNetwork { Address="127.0.0.1",Mask="255.0.0.0",Name="loopback" };
            var result=TelescopeDiscovery.Find(network,Ct,500,"127.0.0.2");Assert.Contains(result.Devices,d=>d.Kind=="Seestar SMB"&&d.Host=="127.0.0.2");
            using var cancel=CancellationTokenSource.CreateLinkedTokenSource(Ct);cancel.CancelAfter(100);Assert.Throws<OperationCanceledException>(()=>TelescopeDiscovery.Find(network,cancel.Token,8000,"127.0.0.2"));
            Assert.Contains(TelescopeDiscovery.Find(network,Ct,300,"127.0.0.2").Devices,d=>d.Host=="127.0.0.2");
        } finally { stop.Cancel();await task; }
    }
    [Fact]
    public async Task PausedLiveImportRetainsBaselineAndFindsCapturesWrittenWhilePaused()
    {
        using var session=Session();string source=Path.Combine(root,"live-resume");ReleaseFixture.Fits(Path.Combine(source,"baseline.fit"),"M31",1);
        var connection=new Connection { Kind="Local simulator",Folder=source,IncludeExisting=false,PollSeconds=2,LimitMB=0 };
        using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(Ct)) {
            cancel.CancelAfter(TimeSpan.FromSeconds(10));await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Task.Run(()=>session.ImportRemote(connection,[],new TelescopeProfile { Id="Live scope",Model="Auto" },true,cancel.Token,p=>{if(p.Stage=="Live import")cancel.Cancel();}),Ct));
        }
        session.Refresh();Assert.Empty(session.Captures);Assert.Single(session.RecoverableImports);Assert.Single(session.RecoverableImports[0].LiveState.Baseline);
        ReleaseFixture.Fits(Path.Combine(source,"while-paused.fit"),"M45",2);
        using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(Ct)) {
            cancel.CancelAfter(TimeSpan.FromSeconds(12));await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Task.Run(()=>session.ResumeImport(session.RecoverableImports[0],"",cancel.Token,p=>{if(p.Stage=="Live import"&&p.Text.Contains("1 imported"))cancel.Cancel();}),Ct));
        }
        session.Refresh();Assert.Single(session.Captures);Assert.Equal("while-paused.fit",session.Captures[0].OriginalName);Assert.Single(session.RecoverableImports[0].LiveState.Complete);session.Verify(Ct,_=>{});
    }
    private static void StarFits(string path,double degrees,int minutes)
    {
        const int size=256;Directory.CreateDirectory(Path.GetDirectoryName(path)!);var random=new Random(73);var stars=Enumerable.Range(0,45).Select(_=>(x:random.Next(20,236),y:random.Next(20,236))).ToArray();double angle=degrees*Math.PI/180;
        string Card(string key,string value)=>(key.PadRight(8)+"= "+value).PadRight(80);string time=new DateTime(2026,10,6,21,0,0).AddMinutes(minutes).ToString("s");
        string header=(Card("SIMPLE","T")+Card("BITPIX","16")+Card("NAXIS","2")+Card("NAXIS1",size.ToString())+Card("NAXIS2",size.ToString())+Card("OBJECT","'M31'")+Card("IMAGETYP","'LIGHT'")+Card("DATE-OBS","'"+time+"'")+Card("EXPTIME","60")+"END".PadRight(80)).PadRight(2880);
        byte[] bytes=new byte[2880+((size*size*2+2879)/2880)*2880];Encoding.ASCII.GetBytes(header).CopyTo(bytes,0);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++) { double value=1000+20*Math.Sin(x*1.7+y*.6);foreach(var star in stars) { double cx=128+Math.Cos(angle)*(star.x-128)-Math.Sin(angle)*(star.y-128),cy=128+Math.Sin(angle)*(star.x-128)+Math.Cos(angle)*(star.y-128);double distance=(x-cx)*(x-cx)+(y-cy)*(y-cy);if(distance<36)value+=10000*Math.Exp(-distance/2.42); }short sample=(short)Math.Round(value);BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(2880+(y*size+x)*2,2),sample); }
        File.WriteAllBytes(path,bytes);
    }
    private static void WritePng(string path,int filter) {
        using var output=File.Create(path);output.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string kind,byte[] data) { Span<byte> length=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(length,data.Length);output.Write(length);byte[] type=Encoding.ASCII.GetBytes(kind);output.Write(type);output.Write(data);uint crc=0xffffffff;foreach(byte b in type.Concat(data)) { crc^=b;for(int n=0;n<8;n++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1; } BinaryPrimitives.WriteUInt32BigEndian(length,~crc);output.Write(length); }
        byte[] header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,3);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),2);header[8]=16;Chunk("IHDR",header);Chunk("tEXt",Encoding.ASCII.GetBytes("OBJECT\0M31"));
        using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionMode.Compress,true)) {
            byte[] prior=new byte[6];foreach(ushort[] values in new[]{new ushort[]{0,257,32768},new ushort[]{65535,13,60000}}) {
                byte[] raw=values.SelectMany(v=>new byte[]{(byte)(v>>8),(byte)v}).ToArray();z.WriteByte((byte)filter);
                for(int i=0;i<6;i++) { int a=i>=2?raw[i-2]:0,b=prior[i],c=i>=2?prior[i-2]:0,p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);int predictor=filter==1?a:filter==2?b:filter==3?(a+b)/2:filter==4?pa<=pb&&pa<=pc?a:pb<=pc?b:c:0;z.WriteByte(unchecked((byte)(raw[i]-predictor))); }prior=raw;
            }
        }Chunk("IDAT",compressed.ToArray());Chunk("IEND",[]);
    }
}
