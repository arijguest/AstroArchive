using System.Text.Json;
using System.Collections.ObjectModel;
using AstroArchive.Remote;
namespace AstroArchive.Desktop;

public sealed partial class ArchiveSession
{
    public ObservableCollection<TargetSolveJob> Solutions { get; } = [];
    public ObservableCollection<Entry> RemoteFiles { get; } = [];
    private List<TargetSolveJob> solved = [];
    private Connection? listedConnection;
    private string sessionKey = "";
    public Func<Connection,ISource> RemoteSource { get; set; } = Downloader.Source;
    public string CacheRoot => Path.Combine(Path.GetDirectoryName(ConfigPath)!,"downloads");
    public void UseSessionKey(string key) { if(sessionKey.Length>0) LinuxSecrets.Forget(sessionKey); sessionKey=string.IsNullOrWhiteSpace(key)?"":LinuxSecrets.Temporary(key); }
    public Settings SolverSettings() => new() { Astap=Settings.Astap,StarDatabase=Settings.StarDatabase,
        UseOnline=Settings.UseOnline,ApiKeyProtected=sessionKey.Length>0?sessionKey:Settings.ApiKeyReference,FieldHeight=Settings.FieldHeight };
    public List<TargetSolveJob> Solve(IEnumerable<Frame> selection,CancellationToken ct,Action<ProgressInfo> progress)
    {
        var frames=selection.ToList(); if(frames.Count==0) throw new ArgumentException("Select captures to identify.");
        var repository=RequireArchive(); var settings=SolverSettings();
        solved=TargetSolving.Plan(frames);
        TargetSolving.Solve(solved,(frame,token,message)=>{ repository.ValidateCapture(frame,token); return PlateSolve.Solve(frame,repository.FilePath(frame),settings,token,message); },ct,
            p=>progress(new ProgressInfo { Stage=p.Stage,Text=p.Detail,Done=p.Completed,Total=p.Total }));
        LastReport=string.Join("\n",solved.Select(j=>j.Filename+": "+j.Match+" · "+j.Centre));
        return solved;
    }
    public string ShowDetails(Frame frame) => LastReport=string.Join("\n",MetadataDetail.For(frame).Select(d=>d.Field+": "+d.Value+(d.Source.Length>0?" · "+d.Source:"")));
    public void SetReport(string value) => LastReport=value;
    private static string CredentialId(Connection c) => Util.HashText(c.Kind+"|"+c.Host+"|"+c.User);
    public void SaveRemotePassword(Connection c,string value) { Settings.RemoteCredentials[CredentialId(c)]=PlateSolve.Protect(value); SaveSettings(); }
    public string RemotePassword(Connection c) => Settings.RemoteCredentials.TryGetValue(CredentialId(c),out var reference)?PlateSolve.Unprotect(reference):"";
    public void ShowSolutions(IEnumerable<TargetSolveJob> jobs) => Replace(Solutions,jobs);
    public void ApplySolutions(IEnumerable<TargetSolveJob> selection,string overrideTarget,CancellationToken ct)
    {
        var jobs=selection.ToList(); if(jobs.Count==0) throw new ArgumentException("Select solved groups to apply.");
        var patches=new List<Frame>();
        foreach(var job in jobs) {
            if(!solved.Contains(job)||!job.Solved) throw new ArgumentException("The selected group has no solution. Solve it first.");
            string target=string.IsNullOrWhiteSpace(overrideTarget)?job.Target:overrideTarget;
            var current=job.Frames.Select(f=>RequireArchive().Find(f.Hash)??throw new IOException("A solved capture was removed. Solve the current selection again.")).ToList();
            var fresh=new TargetSolveJob { Result=job.Result,Representative=current.Single(f=>f.Hash==job.Representative.Hash),Frames=current };
            foreach(var frame in current) { RequireArchive().ValidateCapture(frame,ct); patches.Add(TargetSolving.Apply(fresh,frame,target)); }
        }
        // Validate the whole patch before saving any group.
        foreach(var frame in patches) { ct.ThrowIfCancellationRequested(); RequireArchive().Refile(frame,ct); }
        LastReport=$"Saved pointing and target assignments for {patches.Count} captures. Original bytes unchanged.";
    }
    public List<RotationResult> AnalyzeRotation(IEnumerable<Frame> selection,CancellationToken ct,Action<ProgressInfo> progress)
    {
        var frames=selection.ToList(); if(frames.Count==0) throw new ArgumentException("Select acquisition light frames for rotation analysis.");
        var results=new List<RotationResult>();
        foreach(var group in frames.GroupBy(f=>SubframeSessions.Key(f)??f.Hash)) {
            ct.ThrowIfCancellationRequested(); var rows=group.ToList(); foreach(var frame in rows) RequireArchive().ValidateCapture(frame,ct);
            var result=Rotation.Analyze(RequireArchive(),rows,Settings.Latitude,Settings.Longitude,ct,progress);
            result.Mount=MountLabels.Normalize(result.Mount); result.Session=rows[0].Session+"_"+Util.HashText(rows[0].Group).Substring(0,8);
            RequireArchive().SaveRotation(result);
            foreach(var original in rows) { var frame=original.Clone(); frame.RotationReport=Util.Serialize(result);
                if(!(frame.MountEvidence??"").StartsWith("User")&&!(frame.MountEvidence??"").StartsWith("Explicit")) { frame.Mount=result.Mount; frame.MountEvidence=result.Evidence; }
                RequireArchive().Refile(frame,ct);
            }
            results.Add(result);
        }
        LastReport=string.Join("\n\n",results.Select(r=>r.Mount+": "+r.Evidence+"\n"+string.Join("\n",r.Rejected)));
        return results;
    }
    public void Metadata(IEnumerable<Frame> selection,IDictionary<string,string> values,bool imports,CancellationToken ct)
    {
        var frames=selection.ToList(); if(frames.Count==0) throw new ArgumentException("Select files to edit.");
        var patch=new MetadataPatch(values); patch.Validate(); if(patch.Count==0) throw new ArgumentException("Enter at least one metadata value.");
        var updated=frames.Select(patch.Apply).ToList();
        for(int i=0;i<frames.Count;i++) { ct.ThrowIfCancellationRequested(); if(imports) {
            foreach(var property in typeof(Frame).GetProperties().Where(p=>p.CanWrite)) property.SetValue(frames[i],property.GetValue(updated[i]));
        } else RequireArchive().Refile(updated[i],ct); }
    }
    public void Screen(IEnumerable<Frame> selection,bool imports,CancellationToken ct,Action<ProgressInfo> progress)
    {
        var frames=selection.ToList(); if(frames.Count==0) throw new ArgumentException("Select files to screen.");
        var result=RequireArchive().Screen(frames,imports,ct,progress);
        LastReport=$"Screened {result.Checked}; {result.Problems} need review.\n"+string.Join("\n",result.Errors);
    }
    public string Export(IEnumerable<Frame> selection,ExportOptions options,CancellationToken ct,Action<ProgressInfo> progress)
        => LastOutput=Exporter.Create(RequireArchive(),selection.ToList(),options,ct,progress);
    public List<Entry> ListRemote(Connection connection,CancellationToken ct,Action<ProgressInfo> progress)
    {
        connection.Validate(false); ValidateRemoteLocation(connection);
        using var source=RemoteSource(connection);
        var entries=RemoteCaptureCatalog.Search(source,connection,connection.Folder,ct,n=>progress(new ProgressInfo { Stage="Listing telescope",Text=n+" captures",TotalKnown=false }));
        listedConnection=CloneConnection(connection); return entries;
    }
    public void ShowRemoteFiles(IEnumerable<Entry> files) => Replace(RemoteFiles,files);
    private static Connection CloneConnection(Connection connection) => Util.Deserialize<Connection>(Util.Serialize(connection));
    private void ValidateRemoteLocation(Connection connection) {
        if(connection.Kind=="Seestar SMB"&&connection.SmbMode!="Direct") throw new ArgumentException("Use Direct SMB on Linux.");
        if(connection.Kind=="Local simulator"&&(Util.Within(connection.Folder,RequireArchive().Root)||Util.Within(RequireArchive().Root,connection.Folder)||Util.Within(CacheRoot,connection.Folder)||Util.Within(connection.Folder,CacheRoot))) throw new ArgumentException("Source, archive and download cache must be separate folders.");
    }
    public void ImportRemote(Connection connection,IEnumerable<Entry> selection,TelescopeProfile profile,bool live,CancellationToken ct,Action<ProgressInfo> progress)
    {
        connection.Validate(false); ValidateRemoteLocation(connection);
        if(string.IsNullOrWhiteSpace(profile.Id)) throw new ArgumentException("Enter a physical telescope name.");
        var requested=selection.ToList();
        if(!live&&resuming==null) {
            if(listedConnection==null||listedConnection.Kind!=connection.Kind||listedConnection.Host!=connection.Host||listedConnection.Port!=connection.Port||listedConnection.SmbPort!=connection.SmbPort||listedConnection.Folder!=connection.Folder||listedConnection.User!=connection.User)
                throw new ArgumentException("Connection changed since listing. List captures again before importing.");
            if(requested.Count==0||requested.Any(e=>!RemoteFiles.Contains(e)))throw new ArgumentException("Select captures from the current telescope listing.");
        }
        var record=resuming??Record(live?"RemoteLive":"RemoteFiles",connection,profile,files:requested);
        Recoverable(record,store=> {
            if(live) RemoteLiveImport.Run(RequireArchive(),profile,connection,CacheRoot,Settings.CopyWorkers,ct,progress,null,null,Settings.IgnoreFailed,Settings.IgnoreRaster,RemoteSource,result=>LastReport=result.Report,record.LiveState,state=>{record.LiveState=state;store.Save(record);});
            else {
                var result=RemoteArchiveImport.Run(RequireArchive(),profile,connection,requested,CacheRoot,Settings.CopyWorkers,ct,progress,ignoreFailed:Settings.IgnoreFailed,ignoreRaster:Settings.IgnoreRaster,sourceFactory:RemoteSource);
                LastReport=result.Report;
                if(result.Downloads.Errors.Count>0||result.Archive.Import.Failed>0) throw new IOException(result.Summary+" See the activity report; list and retry failed files.");
            }
            return true;
        });
    }
    public void ImportDump(CancellationToken ct,Action<ProgressInfo> progress)
    {
        var result=RequireArchive().ProcessDump(ct,progress,Settings.CopyWorkers,ignoreFailed:Settings.IgnoreFailed,ignoreRaster:Settings.IgnoreRaster);
        LastReport=RequireArchive().LastReport;
        AnalyzeImported(result.Plan.Frames.Where(f=>f.Status=="Imported"),ct,progress);
        if(result.Import.Failed>0)throw new IOException(result.Summary);
    }
    public void ImportFinishedFolder(string folder,string name,bool recursive,CancellationToken ct,Action<ProgressInfo> progress)
    {
        var plan=RequireArchive().ScanEditedFolder(folder,recursive,ct,progress);var result=new EditedImportResult();
        RequireArchive().ImportEditedFolder(plan,name,ct,progress,result);
        LastReport=$"Added {result.Imported} finished images; {result.SkippedDuplicates} duplicates; {result.SkippedArchived} original captures skipped.\n"+string.Join("\n",plan.Errors.Concat(plan.Images.Where(i=>i.Problem!=null).Select(i=>i.Filename+": "+i.Problem)).Concat(result.Warnings));
    }
    public void EditFinished(IEnumerable<EditedImage> selection,EditedMetadata metadata,CancellationToken ct)
    {
        var images=selection.ToList(); if(images.Count==0) throw new ArgumentException("Select Edited images.");
        foreach(var group in images.GroupBy(i=>i.Project.Id)) RequireArchive().SaveEditedMetadata(group.First().Project,group,metadata,ct);
    }
    public void Delete(IEnumerable<Frame> frames,IEnumerable<EditedImage> images,string confirmation,CancellationToken ct,Action<ProgressInfo> progress)
    {
        if(confirmation!="DELETE") throw new ArgumentException("Type DELETE to confirm permanent deletion of the selected archive copies.");
        var captures=frames.ToList(); if(captures.Count>0&&RequireArchive().OriginalsProtected) throw new IOException("Disable capture protection in Settings before deletion.");
        var result=captures.Count>0?RequireArchive().DeleteFrames(captures,ct,progress):RequireArchive().DeleteEditedImages(images,ct,progress);
        LastReport=$"Deleted {result.Deleted} archive copies. Source originals retained.\n"+string.Join("\n",result.Errors);
        if(result.Errors.Count>0) throw new IOException(LastReport);
    }
    public static void OpenSystemViewer(string path)
    {
        const string executable="/usr/bin/xdg-open";if(!File.Exists(executable))throw new IOException("Install xdg-utils and a default image/video viewer to open this file.");
        var info=new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute=false };info.ArgumentList.Add(Path.GetFullPath(path));System.Diagnostics.Process.Start(info)?.Dispose();
    }
    public void RecoverProfiles()
    {
        var engine=new Settings { Telescopes=Settings.Telescopes,Telescope="",Model="Auto" };
        TelescopeProfiles.Initialize(engine); int count=TelescopeProfiles.MergeRepository(engine,RequireArchive().All());
        Settings.Telescopes=engine.Telescopes; SaveSettings(); LastReport=$"Recovered {count} telescope profiles.";
    }
    public void RenameProfile(string oldName,string newName,CancellationToken ct,Action<ProgressInfo> progress)
    {
        var renamed=JsonSerializer.Deserialize<DesktopSettings>(JsonSerializer.Serialize(Settings))!;
        var engine=new Settings { Telescopes=renamed.Telescopes,Telescope=oldName,Model="Auto" }; TelescopeProfiles.Rename(engine,oldName,newName);
        if(renamed.Telescope==oldName)renamed.Telescope=newName;
        RequireArchive().RenameTelescope(oldName,newName,ct,progress,settingsPath:ConfigPath,originalSettings:JsonSerializer.Serialize(Settings),renamedSettings:JsonSerializer.Serialize(renamed));
        Settings=renamed; LastReport=$"Renamed telescope {oldName} to {newName}; capture hashes and session identities retained.";
    }
    public static System.Diagnostics.ProcessStartInfo StackingLaunchInfo(string executable,string folder)
    {
        if(!Path.IsPathRooted(executable)||!File.Exists(executable)) throw new IOException("Choose an absolute stacking application executable in Settings.");
        if(!Directory.Exists(folder)||File.Exists(Path.Combine(folder,"INCOMPLETE.txt"))) throw new IOException("Complete a stacking export before opening the processing application.");
        var info=new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute=false,WorkingDirectory=folder };
        if(Path.GetFileName(executable) is "siril" or "siril.AppImage") { info.ArgumentList.Add("--directory"); info.ArgumentList.Add(folder); }
        return info;
    }
    public void SaveProfile(string name,string model,string source)
    {
        if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a telescope name.");
        Settings.Telescopes.RemoveAll(p=>string.Equals(p.Id,name,StringComparison.OrdinalIgnoreCase));
        Settings.Telescopes.Add(new TelescopeProfile { Id=name,SessionIdentity=name,Model=TelescopeProfiles.Model(model),LastSource=source }); SaveSettings();
    }
}
