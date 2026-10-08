// Rename a physical telescope without rewriting capture bytes or acquisition sessions.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public class TelescopeRenameResult {public int Files;public string Warning;}
 public class TelescopeRenamePath {public string Before;public string After;public string Hash;}
 public class TelescopeRenameJournal {public string Id;public List<TelescopeRenamePath> Paths;public string SettingsPath;public string OriginalSettings;public string RenamedSettings;}
 public sealed partial class Repository {
  public string RecoveredTelescopeSettings{get;private set;}
  string RenameJournalPath{get{return Path.Combine(Path.GetDirectoryName(WorkingIndex),"telescope-rename.json");}}
  void RecoverTelescopeRename(){
   if(!File.Exists(RenameJournalPath))return;var journal=Util.Deserialize<TelescopeRenameJournal>(File.ReadAllText(RenameJournalPath));if(journal==null||journal.Paths==null||string.IsNullOrEmpty(journal.Id))throw new InvalidDataException("The telescope rename recovery record is invalid: "+RenameJournalPath);bool committed=CacheGet("rename:"+journal.Id)!=null;
   foreach(var path in journal.Paths){CheckManagedPath(path.Before,Root);CheckManagedPath(path.After,Root);string wanted=committed?path.After:path.Before,other=committed?path.Before:path.After;
    if(File.Exists(wanted)){if(Util.Hash(wanted,CancellationToken.None)!=path.Hash)throw new IOException("Interrupted telescope rename needs recovery: "+wanted);continue;}
    if(!File.Exists(other)||Util.Hash(other,CancellationToken.None)!=path.Hash)throw new IOException("Interrupted telescope rename needs recovery: "+other);
    Directory.CreateDirectory(Path.GetDirectoryName(wanted));MoveCapture(other,wanted);
   }
   if(!string.IsNullOrEmpty(journal.SettingsPath)){string settings=committed?journal.RenamedSettings:journal.OriginalSettings;Util.AtomicText(journal.SettingsPath,settings);RecoveredTelescopeSettings=settings;}
   File.Delete(RenameJournalPath);db.Exec("DELETE FROM sessions WHERE id=?","rename:"+journal.Id);
  }
  public TelescopeRenameResult RenameTelescope(string oldName,string newName,CancellationToken ct,Action<ProgressInfo> progress,Action persistProfile=null,string settingsPath=null,string originalSettings=null,string renamedSettings=null){
   if(File.Exists(RenameJournalPath))throw new IOException("An interrupted telescope rename needs recovery. Reopen the repository before renaming again.");
   oldName=(oldName??"").Trim();newName=(newName??"").Trim();if(oldName.Length==0||newName.Length==0)throw new ArgumentException("A telescope name is required.");
   var all=All();if(all.Any(f=>string.Equals(f.Telescope,newName,StringComparison.OrdinalIgnoreCase)&&!string.Equals(f.Telescope,oldName,StringComparison.OrdinalIgnoreCase)))throw new IOException("That telescope name is already in use in the repository. Choose a unique name.");
   var originals=all.Where(f=>string.Equals(f.Telescope,oldName,StringComparison.OrdinalIgnoreCase)).ToList();var changed=new List<Frame>();var moves=new List<Tuple<string,string>>();
   foreach(var original in originals){ct.ThrowIfCancellationRequested();var frame=original.Clone();frame.TelescopeIdentity=frame.TelescopeIdentity??original.Telescope;frame.Telescope=newName;string before=FilePath(original);frame.RelativePath=Destination(frame);string after=FilePath(frame);CheckManagedPath(before,Root);CheckManagedPath(after,Root);
    if(File.Exists(before)&&Util.Hash(before,ct)!=frame.Hash)throw new IOException("Repository contents changed; telescope was not renamed: "+before);
    if(!string.Equals(before,after,StringComparison.OrdinalIgnoreCase)&&File.Exists(after))throw new IOException("Rename destination already exists: "+after);changed.Add(frame);
   }
   var byHash=changed.ToDictionary(f=>f.Hash);var manifests=db.Query("SELECT data FROM source_manifest").Select(Util.Deserialize<SourceManifest>).Where(m=>m.Metadata!=null&&string.Equals(m.Metadata.Telescope,oldName,StringComparison.OrdinalIgnoreCase)).ToList();
   var deletions=Deletions().Where(d=>d.Metadata!=null&&string.Equals(d.Metadata.Telescope,oldName,StringComparison.OrdinalIgnoreCase)).ToList();
   var journal=new TelescopeRenameJournal{Id=Guid.NewGuid().ToString("N"),SettingsPath=settingsPath,OriginalSettings=originalSettings,RenamedSettings=renamedSettings,Paths=new List<TelescopeRenamePath>()};
   for(int i=0;i<changed.Count;i++){string before=FilePath(originals[i]),after=FilePath(changed[i]);if(File.Exists(before)&&!string.Equals(before,after,StringComparison.OrdinalIgnoreCase))journal.Paths.Add(new TelescopeRenamePath{Before=before,After=after,Hash=changed[i].Hash});}
   string pending=RenameJournalPath+".partial";try{using(var stream=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)){byte[] bytes=System.Text.Encoding.UTF8.GetBytes(Util.Serialize(journal));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Move(pending,RenameJournalPath);}finally{TryRemove(pending);}
   try{db.Transaction(()=>{
    int done=0;foreach(var frame in changed){ct.ThrowIfCancellationRequested();var original=originals[done];string before=FilePath(original),after=FilePath(frame);if(progress!=null)progress(new ProgressInfo{Stage="Renaming telescope",Text=frame.OriginalName,Done=done,Total=changed.Count});
     if(File.Exists(before)&&!string.Equals(before,after,StringComparison.OrdinalIgnoreCase)){Directory.CreateDirectory(Path.GetDirectoryName(after));MoveCapture(before,after);moves.Add(Tuple.Create(before,after));}
     frame.RepositoryStamp=File.Exists(after)?FileStamp.Read(after):null;Save(frame);done++;
    }
    foreach(var manifest in manifests){ct.ThrowIfCancellationRequested();manifest.Metadata.TelescopeIdentity=manifest.Metadata.TelescopeIdentity??manifest.Metadata.Telescope;manifest.Metadata.Telescope=newName;Frame frame;if(byHash.TryGetValue(manifest.Hash??"",out frame)){manifest.Destination=frame.RelativePath;manifest.Copy=frame.RepositoryStamp;manifest.Metadata.RelativePath=frame.RelativePath;manifest.Metadata.RepositoryStamp=frame.RepositoryStamp;}Manifest(manifest);}
    foreach(var deletion in deletions){ct.ThrowIfCancellationRequested();deletion.Metadata.TelescopeIdentity=deletion.Metadata.TelescopeIdentity??deletion.Metadata.Telescope;deletion.Metadata.Telescope=newName;if(!string.IsNullOrEmpty(deletion.Metadata.RelativePath))deletion.Metadata.RelativePath=Destination(deletion.Metadata);db.Exec("INSERT OR REPLACE INTO deleted_files(hash,data) VALUES(?,?)",deletion.Hash,Util.Serialize(deletion));}
    ct.ThrowIfCancellationRequested();if(persistProfile!=null)persistProfile();if(settingsPath!=null)Util.AtomicText(settingsPath,renamedSettings);CachePut("rename:"+journal.Id,"committed");
   });}catch(Exception failure){var errors=new List<Exception>{failure};foreach(var move in moves.AsEnumerable().Reverse())try{Directory.CreateDirectory(Path.GetDirectoryName(move.Item1));MoveCapture(move.Item2,move.Item1);}catch(Exception rollback){errors.Add(rollback);}try{if(settingsPath!=null)Util.AtomicText(settingsPath,originalSettings);if(errors.Count==1)File.Delete(RenameJournalPath);}catch(Exception rollback){errors.Add(rollback);}if(errors.Count>1)throw new AggregateException("Telescope rename failed and some changes need recovery when the repository is reopened.",errors);throw;}
   var result=new TelescopeRenameResult{Files=changed.Count};
   foreach(var move in moves)try{string parent=Path.GetDirectoryName(move.Item1);while(parent!=null&&!parent.Equals(Root,StringComparison.OrdinalIgnoreCase)&&Util.Within(parent,Root)&&Directory.Exists(parent)){CheckManagedPath(parent,Root);if(!FileStamp.CanTraverse(new DirectoryInfo(parent))||Directory.EnumerateFileSystemEntries(parent).Any())break;DeleteEmptyCaptureFolder(parent);parent=Path.GetDirectoryName(parent);}}catch(Exception e){result.Warning="The name was updated; an empty old archive folder could not be removed: "+e.Message;}
   try{File.Delete(RenameJournalPath);db.Exec("DELETE FROM sessions WHERE id=?","rename:"+journal.Id);}catch(Exception e){result.Warning=(result.Warning??"")+" The completed rename recovery record could not be cleared: "+e.Message;}
   try{Checkpoint(CancellationToken.None);}catch(Exception e){result.Warning=(result.Warning??"")+" The name was updated in the local index, but the portable snapshot could not be saved: "+e.Message;}return result;
  }
 }
}
