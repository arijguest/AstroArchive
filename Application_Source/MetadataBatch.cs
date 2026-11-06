// Batch edits use detached snapshots; only committed rows are published to the UI.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class MetadataBatchResult {
  public readonly List<Tuple<Frame,Frame>> Updated=new List<Tuple<Frame,Frame>>();
  public readonly List<string> Errors=new List<string>();
 }
 public sealed partial class Repository {
  public static bool PendingMetadata(Frame frame,bool imports){return imports&&(CaptureScreening.Importable(frame)||frame.Status=="Duplicate in source"||frame.Status=="Unreadable");}
  public void EditMetadata(IEnumerable<Frame> selection,MetadataPatch patch,bool imports,MetadataBatchResult result,CancellationToken ct,Action<ProgressInfo> progress){
   var rows=selection.ToList();using(var claim=Background.Claim("Saving metadata",rows.Where(f=>!PendingMetadata(f,imports)).Select(f=>f.Hash)))EditMetadataCore(rows,patch,imports,result,ct,progress);
  }
  void EditMetadataCore(IEnumerable<Frame> selection,MetadataPatch patch,bool imports,MetadataBatchResult result,CancellationToken ct,Action<ProgressInfo> progress){
   var work=new List<Tuple<Frame,Frame,bool>>();
   // Validate every conversion before the first write. Resolve archived rows afresh,
   // so a cached import row cannot move an outdated path or replace newer metadata.
   foreach(var original in selection){ct.ThrowIfCancellationRequested();bool pending=PendingMetadata(original,imports);Frame current=pending?original:Find(original.Hash);if(current==null)throw new InvalidOperationException("A selected capture is no longer archived. Scan again.");work.Add(Tuple.Create(original,patch.Apply(current),pending));}
   bool saved=false;
   try{for(int i=0;i<work.Count;i++){
    ct.ThrowIfCancellationRequested();var item=work[i];if(progress!=null)progress(new ProgressInfo{Done=i,Total=work.Count,Stage="Saving metadata",Text=item.Item1.OriginalName});
    ct.ThrowIfCancellationRequested();try{var updated=item.Item3?item.Item2:patch.Apply(Find(item.Item1.Hash));if(!item.Item3){Refile(updated,ct);saved=true;}if(imports){updated=updated.Clone();updated.SourcePath=item.Item1.SourcePath;updated.SourceRoot=item.Item1.SourceRoot;updated.SourceStamp=item.Item1.SourceStamp;updated.Status=item.Item1.Status;}result.Updated.Add(Tuple.Create(item.Item1,updated));}
    catch(OperationCanceledException){throw;}catch(Exception error){result.Errors.Add(FileRetry.Detail(item.Item1.OriginalName,error));}
   }}finally{if(saved)try{Checkpoint(CancellationToken.None);}catch(Exception error){result.Errors.Add("Edits are saved locally; portable index checkpoint failed: "+error.Message);}}
   if(progress!=null)progress(new ProgressInfo{Done=work.Count,Total=work.Count,Stage="Saving metadata",Text=result.Updated.Count+" captures updated"});
  }
 }
}
