// Portable content-based exclusions survive source renames and telescope remounts.
using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public sealed class DeletedCapture {public string Hash;public string DeletedUtc;public Frame Metadata;}
 public sealed partial class Repository {
  public List<DeletedCapture> Deletions(){return db.Query("SELECT data FROM deleted_files").Select(Util.Deserialize<DeletedCapture>).ToList();}
  HashSet<string> DeletedHashes(){return new HashSet<string>(db.Query("SELECT hash FROM deleted_files"));}
  void RememberDeletion(Frame frame){
   db.Exec("INSERT OR REPLACE INTO deleted_files(hash,data) VALUES(?,?)",frame.Hash,Util.Serialize(new DeletedCapture{Hash=frame.Hash,DeletedUtc=DateTime.UtcNow.ToString("o"),Metadata=frame.Clone()}));
   foreach(var entry in db.Query("SELECT data FROM source_manifest WHERE hash=?",frame.Hash).Select(Util.Deserialize<SourceManifest>)){entry.Status="Deleted";Manifest(entry);}
  }
  static void MarkDeleted(Frame frame){frame.Status="Deleted";frame.SourceDisposition="Previously deleted from archive; skipped on import. Original retained.";}
 }
}
