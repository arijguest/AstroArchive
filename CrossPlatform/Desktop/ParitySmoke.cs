using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
namespace AstroArchive.Desktop;
public static class ParitySmoke
{
    public static async Task Run(MainWindow window,string root,bool screenshots)
    {
        void Put(string id,string value)=>((TextBox)window.Controls[id]).Text=value;
        async Task Click(string id) { ((Button)window.Controls[id]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await window.LastOperation; await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background); ReleaseFixture.Check(window.LastError==null,"parity command "+id+": "+window.LastError); }
        var captures=(DataGrid)window.Controls["Captures"]; captures.SelectedItem=window.Session.Captures.First();
        await Click("Details"); await Click("ScreenArchive"); await Click("SkyContext"); await Click("ResetSky");
        Put("Meta:Gain","73"); Put("Meta:Binning","1x1"); await Click("Meta:Apply");
        ReleaseFixture.Check(window.Session.Captures.Any(f=>f.Gain==73),"detailed metadata is persisted");
        Put("MinExposure","60"); Put("MaxExposure","60"); await Click("ApplyFilters");
        ReleaseFixture.Check(((IEnumerable<Frame>)captures.ItemsSource).Count()==2,"numeric filter includes exact boundaries"); Put("MinExposure",""); Put("MaxExposure","");
        Put("AstapPath",ReleaseFixture.FakeAstap(Path.Combine(root,"solver fixture with spaces Ω"))); Put("Latitude","51.5"); Put("Longitude","-0.12"); await Click("SaveAdvanced");
        captures.SelectedItem=window.Session.Captures.First(); await Click("Solve");
        ((DataGrid)window.Controls["Solutions"]).SelectedItem=window.Session.Solutions.First(); Put("SolvedTarget","M33"); await Click("ApplySolutions");
        ReleaseFixture.Check(window.Session.Captures.Any(f=>f.Target=="M33"&&f.RA==10.6847),"solver pointing and reviewed target are saved");
        await Click("Rotation"); ReleaseFixture.Check(window.Session.LastReport.Contains("five timestamped"),"rotation reports insufficient evidence without inventing a mount result");
        Put("Telescope","Smoke scope"); await Click("SaveProfile"); ((ComboBox)window.Controls["Profiles"]).SelectedItem="Smoke scope"; await Click("LoadProfile"); await Click("RecoverProfiles");
        ((ComboBox)window.Controls["RemoteKind"]).SelectedItem="Local simulator"; Put("RemoteFolder",Path.Combine(root,"source with spaces Ω")); Put("RemoteLimit","0");
        await Click("ListRemote"); var remote=(DataGrid)window.Controls["RemoteFiles"]; foreach(var file in window.Session.RemoteFiles.Where(e=>e.Name!="broken.fit")) remote.SelectedItems.Add(file);
        await Click("ImportRemote"); ReleaseFixture.Check(window.Session.Captures.Count==2,"remote imports deduplicate existing captures");
        var edited=(DataGrid)window.Controls["EditedImages"]; edited.SelectedItem=window.Session.Edited.First(); Put("EditedObject","M45"); Put("EditedSubs","20"); Put("EditedSeconds","1200"); await Click("EditFinished"); await Click("EditedDetails"); await Click("EditedPreview");
        ReleaseFixture.Check(window.Session.Edited.Any(i=>i.Metadata.Subs==20&&i.Metadata.TotalExposure==1200),"Edited overrides are saved without changing image bytes");
        await Click("EnableProtection"); ReleaseFixture.Check(window.Session.RequireArchive().OriginalsProtected,"Linux capture guard enables"); await Click("DisableProtection");
        window.Session.Verify(CancellationToken.None,_=>{});
        string recoverySource=Path.Combine(root,"recovery source"); ReleaseFixture.Fits(Path.Combine(recoverySource,"Light_M42.fit"),"M42",31);
        var plan=window.Session.Scan(recoverySource,"Recovery scope","Auto",CancellationToken.None,_=>{});
        var recovery=new ImportResumeRecord { Kind="Files",State="Paused",Title="Native recovery fixture",Repository=window.Session.RequireArchive().Root,Source=recoverySource,Frames=plan.Frames,Profile=new TelescopeProfile { Id="Recovery scope",Model="Auto" },Workers=1 };
        var store=new ImportResumeStore(Path.Combine(Path.GetDirectoryName(window.Session.ConfigPath)!,"import-jobs"));store.Save(recovery);window.Session.Refresh();
        ((DataGrid)window.Controls["RecoverableImports"]).SelectedItem=window.Session.RecoverableImports.Single();await Click("ResumeImport");
        ReleaseFixture.Check(window.Session.Captures.Count==3&&window.Session.RecoverableImports.Count==0,"recovery page resumes a persisted selection and removes its completed record");
        recovery.Id=Guid.NewGuid().ToString("N");store.Save(recovery);window.Session.Refresh();((DataGrid)window.Controls["RecoverableImports"]).SelectedItem=window.Session.RecoverableImports.Single();await Click("ForgetImport");
        ReleaseFixture.Check(window.Session.RecoverableImports.Count==0,"forgetting a recovery record leaves archive and source files intact");
        captures.SelectedItem=window.Session.Captures.Single(f=>f.Target=="M42");Put("DeleteCapturesConfirm","DELETE");await Click("DeleteCaptures");
        var history=(DataGrid)window.Controls["DeletedCaptures"];history.SelectedItem=window.Session.Deleted.Single();await Click("DeletionAudit");
        ReleaseFixture.Check(window.Session.LastReport.Contains("import excluded"),"deletion audit explains the reimport exclusion");
        history.SelectedItem=window.Session.Deleted.Single();await Click("AllowReimport");
        Put("SourcePath",recoverySource);await Click("Scan");await Click("ImportAll");ReleaseFixture.Check(window.Session.Captures.Count==3,"explicit reimport permission restores a deleted archive copy");
        string dump=window.Session.RequireArchive().DumpFolder;window.Session.RequireArchive().EnsureDumpFolder();string dumpFile=Path.Combine(dump,"Light_M81.fit");ReleaseFixture.Fits(dumpFile,"M81",47);await Click("ImportDump");
        ReleaseFixture.Check(window.Session.Captures.Count==4&&File.Exists(dumpFile),"Dump imports retain the Linux source original");
        string finished=Path.Combine(root,"finished","nested","edited_M45.fit");ReleaseFixture.Fits(finished,"M45",51);Put("EditedFolder",Path.Combine(root,"finished"));await Click("ImportEditedFolder");
        ReleaseFixture.Check(window.Session.Edited.Count==3&&window.Session.Edited.Any(i=>i.RelativePath.Contains("nested")),"recursive finished-image import retains relative paths");
        window.Session.Verify(CancellationToken.None,_=>{});
        if(screenshots) {
            foreach(var section in window.Controls.Values.OfType<Expander>()) section.IsExpanded=true;
            var tabs=(TabControl)window.Controls["Pages"];
            foreach(var item in new[]{(0,"sky-context","SkyGlobe"),(0,"repository-metadata","Section:Detailed metadata editing"),(0,"repository-solving","ApplySolutions"),(1,"import-review","ScreenImports"),(1,"network-import","ListRemote"),(1,"import-recovery","ResumeImport"),(2,"edited-tools","EditFinished"),(4,"solver-settings","SaveAdvanced")}) {
                tabs.SelectedIndex=item.Item1; window.Width=1100; window.Height=720; window.UpdateLayout();
                await Task.Delay(100); window.UpdateLayout();
                var scroller=(ScrollViewer)((TabItem)tabs.Items[item.Item1]!).Content!;
                var position=window.Controls[item.Item3].TranslatePoint(default,(Visual)scroller.Content!);
                if(position.HasValue)scroller.Offset=new Vector(0,Math.Max(0,position.Value.Y-(item.Item2 is "sky-context" or "repository-metadata"?8:scroller.Viewport.Height*.55)));
                await Task.Delay(100);window.UpdateLayout();
                using var bitmap=new RenderTargetBitmap(new PixelSize((int)window.Width,(int)window.Height),new Vector(96,96)); bitmap.Render(window); bitmap.Save(Path.Combine(root,"parity-"+item.Item2+".png"));
            }
            foreach(var section in window.Controls.Values.OfType<Expander>()) section.IsExpanded=false;
        }
        Console.WriteLine("PARITY BUTTONS PASS: metadata, filters, screening, ASTAP protocol, reviewed target, rotation, profiles, remote import, Edited overrides/preview, capture guard, recovery, deletion audit/reimport, Dump and finished folders");
    }
}
