using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using AstroArchive.Remote;
using System.Globalization;
namespace AstroArchive.Desktop;

public sealed partial class MainWindow
{
    private ComboBox Select(string id,IEnumerable<string> values,string value="") {
        var combo=new ComboBox { ItemsSource=values.ToArray(),SelectedItem=value,Width=220 }; Controls[id]=combo; return combo;
    }
    private static Control WithLabel(string label,Control child) => new StackPanel { Spacing=4,Children={Label(label),child} };
    private CheckBox Check(string id,string label,bool value=false) { var check=new CheckBox { Content=label,IsChecked=value }; Controls[id]=check; return check; }
    private string Choice(string id) => ((ComboBox)Controls[id]).SelectedItem?.ToString()??"";
    private bool Checked(string id) => ((CheckBox)Controls[id]).IsChecked==true;
    private Expander Section(string heading,Control content) { var section=new Expander { Header=heading,Content=content,HorizontalAlignment=HorizontalAlignment.Stretch }; Controls["Section:"+heading]=section; return section; }
    private static WrapPanel Wrap(params Control[] controls) { var panel=new WrapPanel(); foreach(var control in controls) { control.Margin=new Thickness(0,0,10,10); panel.Children.Add(control); } return panel; }
    private Control ExportOptionsPanel() => Section("Export options",new StackPanel { Spacing=8,Children={
        Wrap(Label("Stacking layout"),Select("ExportMode",["Subs","Stacks","Both"],"Subs"),Button("OpenStacking","Open last export in processing app",()=>Run("Opening stacking application",_=>{ System.Diagnostics.Process.Start(ArchiveSession.StackingLaunchInfo(Session.Settings.StackingExecutable,Session.LastOutput))?.Dispose(); return Task.CompletedTask; }))),
        Wrap(Check("ExportFolder","Create a new folder",true),Check("ExportMetadata","Include metadata",true),Check("ExportCalibration","Include matching calibrations",true)),
        Wrap(Check("ExportConvert","Convert eligible numeric inputs to FITS",true),Check("ExportSessions","Separate sessions"),Check("ExportRejected","Include rejected captures"),Check("ExportUnknown","Allow unknown calibration state"))
    }});
    private ExportOptions ReadExportOptions(string parent,string name,bool stacking) => new() {
        Parent=parent,Name=name,Mode=stacking?Choice("ExportMode"):"Files",CreateNewFolder=Checked("ExportFolder"),AddMetadata=Checked("ExportMetadata"),
        IncludeCalibration=stacking&&Checked("ExportCalibration"),ConvertToFits=stacking&&Checked("ExportConvert"),SeparateSessions=Checked("ExportSessions"),IncludeRejected=Checked("ExportRejected"),IncludeUnknownCalibration=Checked("ExportUnknown") };
    private Control MetadataPanel(string prefix,bool imports) {
        var fields=new WrapPanel();
        foreach(var field in MetadataEditing.Fields) fields.Children.Add(new StackPanel { Width=280,Margin=new Thickness(0,0,12,8),Children={Label(field.Label),Field(prefix+field.Key,"","Blank keeps existing",270)} });
        return Section(imports?"Import candidate metadata editing":"Detailed metadata editing",new StackPanel { Spacing=10,Children={Notice("Apply values to selected rows. Pixels and original headers remain unchanged. Blank fields keep existing values."),fields,
            Button(prefix+"Apply","Apply metadata to selected",()=>Run("Saving detailed metadata",ct=>{ var selection=Selected<Frame>(imports?"Candidates":"Captures"); var values=MetadataEditing.Fields.Where(f=>Text(prefix+f.Key).Length>0).ToDictionary(f=>f.Key,f=>Text(prefix+f.Key)); return Task.Run(()=>Session.Metadata(selection,values,imports,ct),ct); })) }});
    }
    private Control RepositoryTools() {
        var filters=new WrapPanel();
        foreach(string field in CaptureFilters.Fields) {
            var combo=Select("Filter:"+field,["Any"],"Any"); combo.SelectionChanged+=(_,_)=>ApplyCaptureView();
            filters.Children.Add(new StackPanel { Margin=new Thickness(0,0,10,8),Children={Label(field),combo} });
        }
        filters.Children.Add(Field("MinExposure","","Minimum exposure (s)",210)); filters.Children.Add(Field("MaxExposure","","Maximum exposure (s)",210));
        var panel=new StackPanel { Spacing=12 };
        panel.Children.Add(Section("Filters and sessions",new StackPanel { Spacing=8,Children={filters,Button("ApplyFilters","Apply filters",()=>Run("Filtering captures",_=>{ var min=Number("MinExposure",0,null); var max=Number("MaxExposure",0,null); if(min>max)throw new ArgumentException("Minimum exposure exceeds maximum."); ApplyCaptureView(); return Task.CompletedTask; }))} }));
        panel.Children.Add(Wrap(Select("Stretch",PreviewData.StretchModes,Session.Settings.PreviewStretch),Button("SkyContext","Sky context",()=>Run("Showing sky context",_=>{ ShowSky(Selected<Frame>("Captures").Single()); return Task.CompletedTask; })), Button("ResetSky","Reset sky view",()=>{sky.ResetView();return Task.CompletedTask;}),
            Button("SystemViewer","Open in system viewer",()=>Run("Opening system viewer",_=>{ var frame=Selected<Frame>("Captures").Single();Session.RequireArchive().ValidateCapture(frame,CancellationToken.None);ArchiveSession.OpenSystemViewer(Session.RequireArchive().FilePath(frame));return Task.CompletedTask;})),
            Button("Details","Selected capture details",()=>Run("Reading metadata",_=>{ report.Text=Session.ShowDetails(Selected<Frame>("Captures").Single()); return Task.CompletedTask; })),
            Button("ScreenArchive","Screen selected",()=>Run("Screening captures",ct=>{ var frames=Selected<Frame>("Captures"); return Task.Run(()=>Session.Screen(frames,false,ct,ReportProgress),ct); }))));
        panel.Children.Add(MetadataPanel("Meta:",false));
        var solving=new StackPanel { Spacing=10 };
        solving.Children.Add(Notice("Identify selected sessions with ASTAP or Astrometry.net, then review the solution and apply a target. Solver configuration is in Settings."));
        solving.Children.Add(Wrap(Button("Solve","Identify selected targets",()=>Run("Solving selected fields",async ct=>{ var frames=Selected<Frame>("Captures"); var jobs=await Task.Run(()=>Session.Solve(frames,ct,ReportProgress),ct); Session.ShowSolutions(jobs); })),
            Button("Rotation","Analyze selected rotation",()=>Run("Analyzing rotation",ct=>{ var frames=Selected<Frame>("Captures"); return Task.Run(()=>Session.AnalyzeRotation(frames,ct,ReportProgress),ct); }))));
        var solutions=Table("Solutions",Session.Solutions,("Representative","Filename"),("Group","Scope"),("Centre","Centre"),("Suggested target","TargetLabel"),("Result","Match")); solutions.Height=160; solving.Children.Add(solutions);
        solving.Children.Add(Wrap(Field("SolvedTarget","","Target override (blank uses suggestion)",330),Button("ApplySolutions","Apply selected solutions",()=>Run("Saving solved targets",ct=>{ var jobs=Selected<TargetSolveJob>("Solutions"); var target=Text("SolvedTarget"); return Task.Run(()=>Session.ApplySolutions(jobs,target,ct),ct); }))));
        panel.Children.Add(Section("Target identification and rotation",solving));
        panel.Children.Add(Section("Delete selected archive copies",Wrap(Field("DeleteCapturesConfirm","","Type DELETE; source originals remain",340),Button("DeleteCaptures","Delete selected permanently",()=>Run("Deleting captures",ct=>{ var frames=Selected<Frame>("Captures"); var confirmation=Text("DeleteCapturesConfirm"); ((TextBox)Controls["DeleteCapturesConfirm"]).Text=""; return Task.Run(()=>Session.Delete(frames,[],confirmation,ct,ReportProgress),ct); })))));
        var deleted=Table("DeletedCaptures",Session.Deleted,("Original","OriginalName"),("Target","TargetLabel"),("Import policy","State"));deleted.Height=150;
        panel.Children.Add(Section("Deletion history and reimport",new StackPanel { Spacing=10,Children={Notice("Deleted content stays excluded from imports until you allow reimport here. Rescan the source after lifting an exclusion."),deleted,
            Wrap(Button("AllowReimport","Allow selected content to be reimported",()=>Run("Allowing reimport",ct=>{ var hashes=Selected<DeletedCapture>("DeletedCaptures").Select(d=>d.Hash).ToList();if(hashes.Count==0)throw new ArgumentException("Select deletion records.");return Task.Run(()=>Session.RequireArchive().AllowReimport(hashes,ct),ct); })),
                Button("DeletionAudit","Show selected deletion audit",()=>Run("Reading deletion audit",_=>{ Session.SetReport(Selected<DeletedCapture>("DeletedCaptures").Single().Audit);return Task.CompletedTask; }))) }}));
        return panel;
    }
    private List<Frame> CaptureView(string search) {
        var filters=new CaptureFilters();
        foreach(string field in CaptureFilters.Fields) if(Controls.TryGetValue("Filter:"+field,out var control)&&control is ComboBox combo&&combo.SelectedItem is string value&&value!="Any") filters.Values[field]=value;
        if(Controls.ContainsKey("MinExposure")) {
            double? min=double.TryParse(Text("MinExposure"),NumberStyles.Float,CultureInfo.InvariantCulture,out var low)&&double.IsFinite(low)&&low>=0?low:null,max=double.TryParse(Text("MaxExposure"),NumberStyles.Float,CultureInfo.InvariantCulture,out var high)&&double.IsFinite(high)&&high>=0?high:null;
            if(min.HasValue||max.HasValue) { filters.Ranges["Exposure"]=new CaptureRange { Mode=NumericFilterMode.Between,Minimum=min,Maximum=max }; }
        }
        return filters.Apply(Session.Captures,search);
    }
    private void ApplyCaptureView() { if(Controls.ContainsKey("Captures")) ((DataGrid)Controls["Captures"]).ItemsSource=CaptureView(Text("Search")); }
    private void RefreshFilterChoices() {
        foreach(string field in CaptureFilters.Fields) {
            var combo=(ComboBox)Controls["Filter:"+field]; string selected=combo.SelectedItem?.ToString()??"Any";
            var options=new[]{"Any"}.Concat(field=="Review"?CaptureFilters.ReviewChoices:CaptureFilters.Options(Session.Captures,field)).Distinct().ToArray();
            if(!options.Contains(selected)) options=options.Append(selected).ToArray();
            combo.ItemsSource=options; combo.SelectedItem=selected;
        }
    }
    private Control ImportOptionsPanel() {
        return Section("Import preferences and metadata review",new StackPanel { Spacing=10,Children={
            Wrap(Check("IgnoreFailed","Ignore failed filenames",Session.Settings.IgnoreFailed),Check("IgnoreRaster","Ignore PNG/JPEG/movie inputs",Session.Settings.IgnoreRaster),Field("CopyWorkers",Session.Settings.CopyWorkers.ToString(),"Copy workers (1–8)",160)),
            Button("ImportDump","Import archive Dump folder",()=>Run("Importing Dump inbox",ct=>{ReadImportOptions();return Task.Run(()=>Session.ImportDump(ct,ReportProgress),ct);})),
            Wrap(Label("After verified folder import"),Select("AutoSolve",["Off","Unknown targets","All"],Session.Settings.AutoSolve),Select("AutoRotation",["Off","Unknown mounts","All"],Session.Settings.AutoRotation)),
            Button("SaveImportPreferences","Save import preferences",()=>Run("Saving import preferences",_=>{ ReadImportOptions(); return Task.CompletedTask; })),
            Notice("Automatic analysis starts off. All targets allows the solver to replace existing target labels. Configure your solver and observing site in Settings first."),
            Wrap(Button("SaveProfile","Save telescope profile",()=>Run("Saving telescope",_=>{ Session.SaveProfile(Text("Telescope"),Text("Model"),Text("SourcePath")); RefreshProfiles(); return Task.CompletedTask; })),Select("Profiles",Session.Settings.Telescopes.Select(p=>p.Id)),
                Button("LoadProfile","Use saved telescope",()=>Run("Loading telescope",_=>{ var p=Session.Settings.Telescopes.Single(p=>p.Id==Choice("Profiles")); ((TextBox)Controls["Telescope"]).Text=p.Id; ((TextBox)Controls["Model"]).Text=p.Model; ((TextBox)Controls["SourcePath"]).Text=p.LastSource; return Task.CompletedTask; }))),
            Wrap(Field("RenameProfile","","New physical telescope name",310),Button("RenameTelescope","Rename current telescope",()=>Run("Renaming telescope",async ct=>{ string old=Text("Telescope"),name=Text("RenameProfile"); await Task.Run(()=>Session.RenameProfile(old,name,ct,ReportProgress),ct); ((TextBox)Controls["Telescope"]).Text=name; RefreshProfiles(); })),Button("RecoverProfiles","Recover telescope profiles",()=>Run("Recovering profiles",_=>{ Session.RecoverProfiles(); RefreshProfiles(); return Task.CompletedTask; }))),
            Button("ScreenImports","Screen selected candidates",()=>Run("Screening source files",ct=>{ var frames=Selected<Frame>("Candidates"); return Task.Run(()=>Session.Screen(frames,true,ct,ReportProgress),ct); })),MetadataPanel("ImportMeta:",true)
        }});
    }
    private void RefreshProfiles() { var box=(ComboBox)Controls["Profiles"]; box.ItemsSource=Session.Settings.Telescopes.Select(p=>p.Id).ToArray(); }
    private void ReadImportOptions()
    {
        int workers=(int)Number("CopyWorkers",1,8)!.Value;
        Session.Settings.IgnoreFailed=Checked("IgnoreFailed"); Session.Settings.IgnoreRaster=Checked("IgnoreRaster");
        Session.Settings.CopyWorkers=workers; Session.Settings.AutoSolve=Choice("AutoSolve"); Session.Settings.AutoRotation=Choice("AutoRotation");
        Session.SaveSettings();
    }
    private Control RemotePanel() {
        var panel=new StackPanel { Spacing=10 };
        panel.Children.Add(Notice("Browse and import directly from DWARF FTP or Seestar Direct SMB. Telescope originals are retained. Passwords stay in memory unless saved to the desktop keyring. Live import waits for stable captures and retries interrupted connections."));
        panel.Children.Add(Wrap(Select("RemoteKind",["DWARF FTP","Seestar SMB","Local simulator"],"DWARF FTP"),WithLabel("Telescope address",Field("RemoteHost","192.168.88.1","Telescope IPv4 address",220)),WithLabel("FTP / SMB port",Field("RemotePort","21","FTP / SMB port",150))));
        panel.Children.Add(Wrap(Field("RemoteFolder","/","FTP folder / Seestar UNC share / simulator folder",470),WithLabel("Username",Field("RemoteUser","anonymous","Username",220))));
        var password=Field("RemotePassword","","Password (this session)",260); password.PasswordChar='●';
        panel.Children.Add(Wrap(password,Button("SaveRemotePassword","Save password in keyring",()=>Run("Saving credential",ct=>{ var value=SecretText("RemotePassword"); return Task.Run(()=>Session.SaveRemotePassword(RemoteConnection(),value),ct); })),
            WithLabel("Poll interval (seconds)",Field("RemotePoll","5","Poll interval (2–120 s)",175)),WithLabel("Transfer limit (MiB/s)",Field("RemoteLimit","2","Transfer limit (MiB/s; 0 unlimited)",220)),Check("RemoteExisting","Include existing captures",true)));
        panel.Children.Add(Wrap(Button("Discover","Discover telescopes",()=>Run("Discovering telescopes",async ct=>{
            var networks=DiscoveryNetwork.Available(); if(networks.Count==0) throw new IOException("No active IPv4 Ethernet or Wi-Fi network.");
            var devices=new List<DiscoveredTelescope>(); foreach(var network in networks) { var found=await Task.Run(()=>TelescopeDiscovery.Find(network,ct),ct); devices.AddRange(found.Devices); }
            Session.SetReport(devices.Count==0?"No telescope responded. Check your telescope Wi-Fi and firewall, or enter its address manually.":string.Join("\n",devices.Select(d=>d.Name+" · "+d.Kind+" · "+d.Host)));
            var combo=(ComboBox)Controls["Discovered"]; combo.ItemsSource=devices; combo.SelectedIndex=devices.Count>0?0:-1;
        })),new ComboBox { Name="Discovered",Width=280,ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<DiscoveredTelescope>((device,_)=>new TextBlock { Text=device==null?"":device.Name+" · "+device.Host }) },Button("UseDiscovered","Use selected device",()=>Run("Selecting telescope",_=>{ var device=((ComboBox)Controls["Discovered"]).SelectedItem as DiscoveredTelescope??throw new ArgumentException("Select a discovered telescope."); ((ComboBox)Controls["RemoteKind"]).SelectedItem=device.Kind; ((TextBox)Controls["RemoteHost"]).Text=device.Host; ((TextBox)Controls["RemotePort"]).Text=device.Kind=="Seestar SMB"?"445":"21"; ((TextBox)Controls["RemoteFolder"]).Text=device.Kind=="Seestar SMB"?"\\\\"+device.Host+"\\EMMC Images":"/"; return Task.CompletedTask; }))));
        Controls["Discovered"]=((WrapPanel)panel.Children[^1]).Children.OfType<ComboBox>().Single();
        panel.Children.Add(Wrap(Button("ListRemote","List telescope captures",()=>Run("Listing telescope captures",async ct=>{ var connection=RemoteConnection(); var files=await Task.Run(()=>Session.ListRemote(connection,ct,ReportProgress),ct); Session.ShowRemoteFiles(files); })),
            Button("ImportRemote","Import selected telescope captures",()=>RemoteImport(false)),Button("LiveRemote","Start live import",()=>RemoteImport(true))));
        var files=Table("RemoteFiles",Session.RemoteFiles,("Capture","Name"),("Bytes","Size"),("Modified UTC","Modified"),("Folder","Path")); files.Height=200; panel.Children.Add(files);
        ((ComboBox)Controls["RemoteKind"]).SelectionChanged+=(_,_)=> {
            bool smb=Choice("RemoteKind")=="Seestar SMB";
            var host=(TextBox)Controls["RemoteHost"]; if(host.Text is "192.168.88.1" or "192.168.42.1")host.Text=smb?"192.168.42.1":"192.168.88.1";
            var port=(TextBox)Controls["RemotePort"];if(port.Text is "21" or "445")port.Text=smb?"445":"21";
            var folder=(TextBox)Controls["RemoteFolder"];if(folder.Text=="/"||folder.Text?.StartsWith("\\\\192.168.")==true)folder.Text=smb?"\\\\"+host.Text+"\\EMMC Images":"/";
        };
        var bottom=new StackPanel { Spacing=12 }; bottom.Children.Add(Section("Telescope network imports",panel)); bottom.Children.Add(RecoveryPanel()); return bottom;
    }
    private Control RecoveryPanel() {
        var table=Table("RecoverableImports",Session.RecoverableImports,("Import","Title"),("State","State"),("Updated UTC","UpdatedUtc")); table.Height=160;
        return Section("Paused and interrupted imports",new StackPanel { Spacing=10,Children={Notice("Cancel pauses safely. Resume uses the saved selection, verifies completed copies and retains the live-import baseline. Unsaved passwords must be entered again."),table,
            Wrap(SecretField("ResumePassword","Password if not saved in keyring",300),Button("ResumeImport","Resume selected import",()=>Run("Resuming import",async ct=>{ var record=Selected<ImportResumeRecord>("RecoverableImports").Single(); var password=SecretText("ResumePassword"); await Task.Run(()=>Session.ResumeImport(record,password,ct,ReportProgress),ct); if(record.Kind=="Files") { Session.ShowCandidates(Session.ResumeCandidates); ((TextBox)Controls["SourcePath"]).Text=Session.Settings.Source; } })),
                Button("ForgetImport","Forget selected recovery record",()=>Run("Forgetting recovery record",_=>{ Session.ForgetImport(Selected<ImportResumeRecord>("RecoverableImports").Single()); return Task.CompletedTask; }))) }});
    }
    private Connection RemoteConnection() {
        var connection=new Connection { Kind=Choice("RemoteKind"),Host=Text("RemoteHost"),Folder=Text("RemoteFolder"),User=Text("RemoteUser"),Password=SecretText("RemotePassword"),Port=(int)Number("RemotePort",1,65535)!.Value,SmbPort=(int)Number("RemotePort",1,65535)!.Value,SmbMode="Direct",PollSeconds=(int)Number("RemotePoll",2,120)!.Value,LimitMB=Number("RemoteLimit",0,1000)!.Value,IncludeExisting=Checked("RemoteExisting") };
        if(connection.Password.Length==0) connection.Password=Session.RemotePassword(connection); return connection;
    }
    private Task RemoteImport(bool live)=>Run(live?"Watching telescope captures":"Importing telescope captures",ct=>{
        ReadImportOptions(); var connection=RemoteConnection(); var selected=Selected<Entry>("RemoteFiles"); var profile=new TelescopeProfile { Id=Text("Telescope"),Model=Text("Model"),SessionIdentity=Text("Telescope") };
        return Task.Run(()=>Session.ImportRemote(connection,selected,profile,live,ct,ReportProgress),ct);
    });
    private void ShowSky(Frame frame) {
        var context=CaptureSky.Resolve(frame,new Settings { Latitude=Session.Settings.Latitude,Longitude=Session.Settings.Longitude });sky.SetContext(context);
        skyText.Text=context.TargetLabel+" · "+context.TimeLabel+"\n"+context.Summary+"\n"+context.Evidence+"\nDrag to rotate, scroll to zoom; arrow keys, +/- and Home also navigate.";
    }
    private Control EditedTools() => Section("Edited metadata, preview and deletion",new StackPanel { Spacing=10,Children={
        Wrap(Field("EditedFolder","","Absolute folder of finished images",430),Check("EditedRecursive","Include subfolders",true),Button("ImportEditedFolder","Import finished-image folder",()=>Run("Importing Edited folder",ct=>{var folder=Text("EditedFolder");var name=Text("EditedProject");bool recursive=Checked("EditedRecursive");return Task.Run(()=>Session.ImportFinishedFolder(folder,name,recursive,ct,ReportProgress),ct);}))),
        Wrap(Field("EditedObject","","Object / target",240),Field("EditedFilters","","Filters",180),Field("EditedSubs","","Number of subs",170),Field("EditedSeconds","","Total exposure (s)",190)),
        Wrap(Button("EditFinished","Save Edited metadata",()=>Run("Saving Edited metadata",ct=>{ var images=Selected<EditedImage>("EditedImages"); var metadata=new EditedMetadata { Object=Text("EditedObject"),Filters=Text("EditedFilters"),Subs=(int?)Number("EditedSubs",1,int.MaxValue),TotalExposure=Number("EditedSeconds",double.Epsilon,null) }; return Task.Run(()=>Session.EditFinished(images,metadata,ct),ct); })),
            Button("EditedDetails","Edited image details",()=>Run("Reading Edited details",_=>{ Session.SetReport(Selected<EditedImage>("EditedImages").Single().Metadata.Details); return Task.CompletedTask; })),
            Button("EditedPreview","Preview Edited image",()=>Run("Loading Edited preview",ct=>PreviewEdited(ct)))),
        Notice("Edited previews appear in the Repository preview pane."),
        Wrap(Field("DeleteEditedConfirm","","Type DELETE to remove selected Edited copies",390),Button("DeleteEdited","Delete Edited copies permanently",()=>Run("Deleting Edited images",ct=>{ var images=Selected<EditedImage>("EditedImages"); string confirmation=Text("DeleteEditedConfirm"); ((TextBox)Controls["DeleteEditedConfirm"]).Text=""; return Task.Run(()=>Session.Delete([],images,confirmation,ct,ReportProgress),ct); })))
    }});
    private async Task PreviewEdited(CancellationToken ct) {
        var image=Selected<EditedImage>("EditedImages").Single(); string path=Session.RequireArchive().EditedPath(image.Project,image.RelativePath);
        var frame=Classifier.Read(path,Session.RequireArchive().EditedFolder,"Edited","Auto");
        await DisplayFrame(frame,path,ct); tabs.SelectedIndex=0;
    }
    private Control AnalyticsOptionsPanel() => Section("Chart scope, layout and animation",new StackPanel { Spacing=10,Children={
        Wrap(Field("AnalyticsTelescope","","Physical telescope (blank = all)",300),Field("AnalyticsFrom","","From date YYYY-MM-DD",240),Field("AnalyticsTo","","Through date YYYY-MM-DD",240)),
        Wrap(Check("AnalyticsRejected","Include rejected light frames",false),Check("AnalyticsUnknown","Unknown telescope only",false),Button("ApplyAnalyticsScope","Apply chart scope",()=>Run("Filtering analytics",_=>{ Session.SetAnalyticsScope(new AnalyticsOptions { Telescope=Text("AnalyticsTelescope"),From=AnalyticsDate("AnalyticsFrom"),To=AnalyticsDate("AnalyticsTo"),IncludeRejected=Checked("AnalyticsRejected"),UnknownTelescopeOnly=Checked("AnalyticsUnknown"),Caption=Text("AnalyticsTelescope").Length==0?"Repository":Text("AnalyticsTelescope") });return Task.CompletedTask; }))),
        Wrap(WithLabel("Layout",Select("AnalyticsLayout",Enum.GetNames<AnalyticsLayout>(),"Landscape")),WithLabel("Report theme",Select("AnalyticsTheme",["Light","Dark"],"Light")),WithLabel("Export",Select("AnalyticsFormat",["Documents","GIF","MP4"],"Documents"))),
        Notice("Every export includes all charts and continuation pages as PDF, PNG, JPEG, SVG and data. GIF/MP4 adds a six-chart animated story. MP4 requires ffmpeg; GIF needs no external encoder."),
        Wrap(WithLabel("Seconds per chart",Field("AnalyticsSeconds","4","0.25–60",190)),WithLabel("Frames per second",Field("AnalyticsFps","24","1–60",190)),WithLabel("Maximum edge (pixels)",Field("AnalyticsResolution","960","32–3840",210)),WithLabel("Transition",Select("AnalyticsTransition",["Fade","Glide","Zoom"],"Glide")))
    }});
    private DateTime? AnalyticsDate(string id) { string value=Text(id);if(value.Length==0)return null;if(!DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new ArgumentException("Use YYYY-MM-DD for the chart date range.");return date; }
    private Control AdvancedSettings() => Section("Solvers, observing site, accessibility and capture protection",new StackPanel { Spacing=10,Children={
        Notice("ASTAP requires its star catalogue. Online solving sends detected star coordinates and image dimensions to Astrometry.net after you choose Identify; pixels are not uploaded."),
        Field("StackingPath",Session.Settings.StackingExecutable,"Absolute stacking application executable (Siril or another app)",690),
        Wrap(Field("AstapPath",Session.Settings.Astap,"Absolute ASTAP executable",350),Field("StarDatabase",Session.Settings.StarDatabase,"ASTAP catalogue folder (optional)",350)),
        Wrap(Check("UseOnline","Use Astrometry.net",Session.Settings.UseOnline),SecretField("ApiKey","Astrometry.net API key (this session)",340),Button("SaveApiKey","Save API key in keyring",()=>Run("Saving solver credential",ct=>{ string key=Text("ApiKey"); return Task.Run(()=>{ Session.Settings.ApiKeyReference=PlateSolve.Protect(key); Session.SaveSettings(); },ct); }))),
        Wrap(WithLabel("Latitude (degrees)",Field("Latitude",Session.Settings.Latitude?.ToString(CultureInfo.InvariantCulture)??"","Latitude (-90…90)",220)),WithLabel("Longitude (degrees)",Field("Longitude",Session.Settings.Longitude?.ToString(CultureInfo.InvariantCulture)??"","Longitude (-180…180)",220)),WithLabel("Field height (degrees)",Field("FieldHeight",Session.Settings.FieldHeight?.ToString(CultureInfo.InvariantCulture)??"","Field height (degrees; optional)",250))),
        Wrap(WithLabel("Text scale",Field("TextScale",Session.Settings.TextScale.ToString(CultureInfo.InvariantCulture),"Text scale (1…2)",210)),Button("SaveAdvanced","Save solver / site / accessibility settings",()=>Run("Saving preferences",_=>{ SaveAdvancedSettings(); return Task.CompletedTask; }))),
        Notice("Linux capture protection blocks deletion and relocation within AstroArchive. External filesystem tools and Windows do not enforce this Linux guard. Windows NTFS protection remains separate."),
        Wrap(Button("EnableProtection","Enable Linux capture guard",()=>Run("Enabling capture protection",ct=>Task.Run(()=>Session.RequireArchive().SetOriginalsProtection(true,ct,ReportProgress),ct))),Button("DisableProtection","Disable Linux capture guard",()=>Run("Disabling capture protection",ct=>Task.Run(()=>Session.RequireArchive().SetOriginalsProtection(false,ct,ReportProgress),ct))))
    }});
    private void SaveAdvancedSettings()
    {
        // Validate every input before changing the active solver or preferences.
        double? latitude=Number("Latitude",-90,90),longitude=Number("Longitude",-180,180),height=Number("FieldHeight",double.Epsilon,180);
        double scale=Number("TextScale",1,2)!.Value;
        Session.Settings.StackingExecutable=Text("StackingPath"); Session.Settings.Astap=Text("AstapPath");
        Session.Settings.StarDatabase=Text("StarDatabase"); Session.Settings.UseOnline=Checked("UseOnline");
        Session.Settings.Latitude=latitude; Session.Settings.Longitude=longitude; Session.Settings.FieldHeight=height;
        Session.Settings.TextScale=scale; Session.Settings.PreviewStretch=Choice("Stretch");
        Session.SaveSettings(); Session.UseSessionKey(Text("ApiKey")); FontSize=14*scale;
    }
    private string SecretText(string id) => ((TextBox)Controls[id]).Text??"";
    private TextBox SecretField(string id,string hint,double width) { var box=Field(id,"",hint,width); box.PasswordChar='●'; return box; }
    private double? Number(string id,double? minimum,double? maximum) {
        string value=Text(id); if(value.Length==0) { if(id is "CopyWorkers" or "RemotePort" or "RemotePoll" or "RemoteLimit" or "TextScale" or "AnalyticsSeconds" or "AnalyticsFps" or "AnalyticsResolution") throw new ArgumentException("Enter a value for "+id+"."); return null; }
        if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double number)||!double.IsFinite(number)||number<minimum||number>maximum) throw new ArgumentException("Invalid value for "+id+". Use a finite number in the indicated range.");
        if((id is "CopyWorkers" or "RemotePort" or "RemotePoll" or "EditedSubs" or "AnalyticsFps" or "AnalyticsResolution")&&number!=Math.Floor(number)) throw new ArgumentException(id+" needs a whole number.");
        return number;
    }
}
