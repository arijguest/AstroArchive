// Portable content-based exclusions survive source renames and telescope remounts.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class DeletionEvent {public string Utc,Action;}
 public sealed class DeletedCapture {public string Hash;public string DeletedUtc;public string AllowedUtc;public Frame Metadata;public List<DeletionEvent> Events=new List<DeletionEvent>();
  public bool Excluded {get{return string.IsNullOrEmpty(AllowedUtc);}}
  public string State {get{return Excluded?"Excluded":"Reimport allowed";}} public string TargetLabel {get{return Metadata==null?"Unknown":Metadata.TargetLabel;}}
  public string OriginalName {get{return Metadata==null?"":Metadata.OriginalName;}}
  public string Audit {get{return string.Join("\n",Events.Select(e=>e.Utc+" · "+e.Action));}}}
 public sealed partial class Repository {
  static void EnsureAudit(DeletedCapture record){if(record.Events==null)record.Events=new List<DeletionEvent>();if(record.Events.Count==0){record.Events.Add(new DeletionEvent{Utc=record.DeletedUtc,Action="Deleted from archive; import excluded"});if(!string.IsNullOrEmpty(record.AllowedUtc))record.Events.Add(new DeletionEvent{Utc=record.AllowedUtc,Action="Reimport explicitly allowed"});}}
  public List<DeletedCapture> Deletions(){var records=db.Query("SELECT data FROM deleted_files").Select(Util.Deserialize<DeletedCapture>).ToList();foreach(var record in records)EnsureAudit(record);return records;}
  HashSet<string> DeletedHashes(){return new HashSet<string>(Deletions().Where(d=>d.Excluded).Select(d=>d.Hash));}
  public int AllowReimport(IEnumerable<string> hashes,CancellationToken ct){
   ct.ThrowIfCancellationRequested();var requested=new HashSet<string>(hashes);var records=Deletions().Where(d=>d.Excluded&&requested.Contains(d.Hash)).ToList();
   db.Transaction(()=>{foreach(var record in records){record.AllowedUtc=DateTime.UtcNow.ToString("o");record.Events.Add(new DeletionEvent{Utc=record.AllowedUtc,Action="Reimport explicitly allowed"});db.Exec("UPDATE deleted_files SET data=? WHERE hash=?",Util.Serialize(record),record.Hash);foreach(var manifest in db.Query("SELECT data FROM source_manifest WHERE hash=? AND status=?",record.Hash,"Deleted").Select(Util.Deserialize<SourceManifest>)){manifest.Status="Reimport allowed";Manifest(manifest);}}});
   // Preserve the deletion log, including when its exclusion was explicitly lifted.
   Checkpoint(CancellationToken.None);return records.Count;
  }
  public Frame RecheckAllowedSource(Frame frame,CancellationToken ct){
   ct.ThrowIfCancellationRequested();if(frame.Status!="Deleted"||DeletedHashes().Contains(frame.Hash))return frame;
   try{var updated=Classifier.Read(frame.SourcePath,frame.SourceRoot,frame.Telescope,frame.Model);updated.SourceRoot=frame.SourceRoot;updated.Status="New";updated.SourceDisposition="Deletion exclusion lifted; ready to import.";return updated;}
   catch(Exception e){var updated=frame.Clone();updated.Status="Unreadable";updated.IntegrityIssue=updated.ScreeningIssue=FileRetry.Detail(frame.SourcePath,e);return updated;}
  }
  void RememberDeletion(Frame frame){
   var previous=db.Query("SELECT data FROM deleted_files WHERE hash=?",frame.Hash).Select(Util.Deserialize<DeletedCapture>).FirstOrDefault();if(previous!=null)EnsureAudit(previous);
   var record=new DeletedCapture{Hash=frame.Hash,DeletedUtc=DateTime.UtcNow.ToString("o"),Metadata=frame.Clone(),Events=previous==null?new List<DeletionEvent>():previous.Events};record.Events.Add(new DeletionEvent{Utc=record.DeletedUtc,Action="Deleted from archive; import excluded"});
   db.Exec("INSERT OR REPLACE INTO deleted_files(hash,data) VALUES(?,?)",frame.Hash,Util.Serialize(record));
   foreach(var entry in db.Query("SELECT data FROM source_manifest WHERE hash=?",frame.Hash).Select(Util.Deserialize<SourceManifest>)){entry.Status="Deleted";Manifest(entry);}
  }
  static void MarkDeleted(Frame frame){frame.Status="Deleted";frame.SourceDisposition="Previously deleted from archive; skipped on import. Original retained.";}
 }
}
