namespace AstroArchive.Desktop;
public sealed partial class ArchiveSession
{
    private void AnalyzeImported(IEnumerable<Frame> selection,CancellationToken ct,Action<ProgressInfo> progress)
    {
        var frames=selection.Select(f=>RequireArchive().Find(f.Hash)).OfType<Frame>().ToList();string importReport=LastReport;var reports=new List<string>();
        try {
            var solve=frames.Where(f=>Assets.CanDecode(f)&&!f.Rejected&&(Settings.AutoSolve=="All"||Settings.AutoSolve=="Unknown targets"&&Catalog.IsAmbiguous(f.Target))).ToList();
            if(solve.Count>0) { var jobs=Solve(solve,ct,progress);var accepted=jobs.Where(j=>j.Solved&&j.Include).ToList();if(accepted.Count>0)ApplySolutions(accepted,"",ct);reports.Add(string.Join("\n",jobs.Select(j=>j.Filename+": "+j.Match))); }
            var rotation=frames.Where(f=>f.Kind=="Light"&&!f.Rejected&&(Settings.AutoRotation=="All"||Settings.AutoRotation=="Unknown mounts"&&MountLabels.Type(f.Mount).Length==0)).Select(f=>RequireArchive().Find(f.Hash)).OfType<Frame>().ToList();
            if(rotation.Count>0) { AnalyzeRotation(rotation,ct,progress);reports.Add(LastReport); }
        } finally { LastReport=importReport+(reports.Count>0?"\n\nAutomatic analysis\n"+string.Join("\n\n",reports):""); }
    }
}
