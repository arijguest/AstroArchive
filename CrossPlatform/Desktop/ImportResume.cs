using System.Collections.ObjectModel;
using AstroArchive.Remote;
namespace AstroArchive.Desktop;
public sealed partial class ArchiveSession
{
    public ImportPlan ResumeCandidates => plan??new ImportPlan();
    public ObservableCollection<ImportResumeRecord> RecoverableImports { get; } = [];
    private ImportResumeStore Recovery => new(Path.Combine(Path.GetDirectoryName(ConfigPath)!,"import-jobs"));
    private ImportResumeRecord? resuming;
    private ImportResumeRecord Record(string kind,Connection? connection,TelescopeProfile profile,IEnumerable<Frame>? frames=null,IEnumerable<Entry>? files=null)
    {
        var copy=connection==null?null:CloneConnection(connection); if(copy!=null)copy.Password="";
        return new ImportResumeRecord { Kind=kind,Repository=RequireArchive().Root,Source=Settings.Source,Profile=profile,Frames=frames?.Select(f=>f.Clone()).ToList(),Files=files?.ToList(),Connection=copy,
            Title=profile.Id+" · "+kind,Workers=Settings.CopyWorkers,IgnoreFailed=Settings.IgnoreFailed,IgnoreRaster=Settings.IgnoreRaster,Solve=Settings.AutoSolve,Rotation=Settings.AutoRotation,
            ProtectedPassword=connection!=null&&Settings.RemoteCredentials.TryGetValue(CredentialId(connection),out var reference)?reference:"" };
    }
    private T Recoverable<T>(ImportResumeRecord record,Func<ImportResumeStore,T> operation)
    {
        var store=Recovery; record.State="Running"; store.Save(record);
        try { var result=operation(store); if(record.State=="Interrupted")store.Save(record);else store.Remove(record); return result; }
        catch(OperationCanceledException) { record.State="Paused"; store.Save(record); throw; }
        catch { record.State="Interrupted"; store.Save(record); throw; }
    }
    public void RefreshRecovery()
    {
        var store=Recovery; Replace(RecoverableImports,store.Load().Where(r=>r.Repository==RequireArchive().Root));
        if(store.Warnings.Count>0)LastReport=string.Join("\n",store.Warnings);
    }
    public void ResumeImport(ImportResumeRecord record,string password,CancellationToken ct,Action<ProgressInfo> progress)
    {
        if(!RecoverableImports.Contains(record)||record.Repository!=RequireArchive().Root)throw new ArgumentException("Select an interrupted import for this archive.");
        var store=Recovery; Settings.CopyWorkers=record.Workers; Settings.IgnoreFailed=record.IgnoreFailed; Settings.IgnoreRaster=record.IgnoreRaster; Settings.AutoSolve=record.Solve??"Off"; Settings.AutoRotation=record.Rotation??"Off";
        resuming=record;
        try {
            if(record.Kind=="Files") {
                var remaining=store.Remaining(record,RequireArchive(),ct); Settings.Source=record.Source;
                if(remaining.Count==0) { AnalyzeImported(record.Frames??[],ct,progress); store.Remove(record); LastReport="All selected archive copies verified. Nothing remains to import."; return; }
                plan=new ImportPlan { Source=record.Source,Frames=remaining }; Import(remaining,ct,progress);
            } else if(record.Kind is "RemoteFiles" or "RemoteLive") {
                var connection=CloneConnection(record.Connection); connection.Password=string.IsNullOrEmpty(password)?PlateSolve.Unprotect(record.ProtectedPassword):password;
                ImportRemote(connection,record.Files??[],record.Profile,record.Kind=="RemoteLive",ct,progress);
            } else throw new ArgumentException("This import type is not supported by the Linux recovery screen.");
        } finally { resuming=null; }
    }
    public void ForgetImport(ImportResumeRecord record) { if(!RecoverableImports.Contains(record))throw new ArgumentException("Select an import recovery record.");Recovery.Remove(record);RefreshRecovery(); }
}
