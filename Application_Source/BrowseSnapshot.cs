using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroArchive {
 internal sealed class BrowseSnapshot {
  public long Revision;
  public bool CompareTarget;
  public List<Frame> Rows;
  public Dictionary<string,QualityMeasurement> Measurements;
 }

 public sealed partial class Repository {
  bool browseTracking;

  // Connection-local journal. SQLite rolls back the notifications with their writes,
  // including deletes and saves made by the import, recovery and background workers.
  void TrackBrowseChanges(){
   if(browseTracking)return;
   db.Transaction(()=>{
    db.Exec("CREATE TEMP TABLE browse_changes(hash TEXT PRIMARY KEY,revision INTEGER NOT NULL)");
    db.Exec("CREATE INDEX temp.browse_changes_revision ON browse_changes(revision)");
    db.Exec("CREATE TEMP TRIGGER browse_insert AFTER INSERT ON main.files BEGIN INSERT OR REPLACE INTO browse_changes SELECT NEW.hash,COALESCE(MAX(revision),0)+1 FROM browse_changes; END");
    db.Exec("CREATE TEMP TRIGGER browse_update AFTER UPDATE ON main.files BEGIN INSERT OR REPLACE INTO browse_changes SELECT NEW.hash,COALESCE(MAX(revision),0)+1 FROM browse_changes; END");
    db.Exec("CREATE TEMP TRIGGER browse_delete AFTER DELETE ON main.files BEGIN INSERT OR REPLACE INTO browse_changes SELECT OLD.hash,COALESCE(MAX(revision),0)+1 FROM browse_changes; END");
   });
   browseTracking=true;
  }

  internal List<Frame> OpeningRows(string target){
   const string recent="SELECT data FROM files ORDER BY rowid DESC LIMIT 160";
   if(string.IsNullOrWhiteSpace(target)||target=="All targets")return db.Query(recent).Select(Util.Deserialize<Frame>).ToList();
   // This is a bounded first view. The normal view builds its exact target sections
   // and uses the entire index, including legacy rows with different metadata.
   var rows=db.Query("SELECT data FROM files WHERE instr(data,?)>0 ORDER BY rowid DESC LIMIT 160","\"Target\":"+Util.Serialize(target))
    .Select(Util.Deserialize<Frame>).Where(f=>f.Target==target).ToList();
   return rows.Count>0?rows:db.Query(recent).Select(Util.Deserialize<Frame>).ToList();
  }

  internal BrowseSnapshot ReadBrowseSnapshot(BrowseSnapshot previous){
   List<string> records,changed=null;
   Dictionary<string,QualityMeasurement> measurements;
   long revision;
   bool compare=QualityCompareTarget;
   lock(db.Gate){
    TrackBrowseChanges();
    revision=long.Parse(db.Query("SELECT COALESCE(MAX(revision),0) FROM browse_changes").Single(),CultureInfo.InvariantCulture);
    measurements=QualityMeasurements();
    if(previous!=null&&revision==previous.Revision&&compare==previous.CompareTarget&&ReferenceEquals(previous.Measurements,measurements))return previous;
    if(previous==null)records=db.Query("SELECT data FROM files");
    else{
     string after=previous.Revision.ToString(CultureInfo.InvariantCulture);
     changed=db.Query("SELECT hash FROM browse_changes WHERE revision>CAST(? AS INTEGER)",after);
     records=db.Query("SELECT files.data FROM browse_changes JOIN files ON files.hash=browse_changes.hash WHERE revision>CAST(? AS INTEGER)",after);
    }
   }

   var updated=records.Select(Util.Deserialize<Frame>).ToList();
   if(previous==null){
    QualityAnnotator.Apply(updated,measurements,compare);
    return new BrowseSnapshot{Revision=revision,CompareTarget=compare,Rows=updated,Measurements=measurements};
   }

   var affected=new HashSet<string>(changed,StringComparer.Ordinal);
   var cohorts=new HashSet<string>(StringComparer.Ordinal);
   bool scoringChanged=compare!=previous.CompareTarget||!ReferenceEquals(measurements,previous.Measurements);
   if(!scoringChanged&&measurements.Count>0){
    foreach(var frame in previous.Rows.Where(f=>affected.Contains(f.Hash)).Concat(updated)){
     QualityMeasurement measurement;
     if(measurements.TryGetValue(frame.Hash,out measurement))cohorts.Add(QualityAnnotator.Cohort(frame,measurement.Kind,compare));
    }
   }

   var scoring=new List<Frame>(updated);
   var rows=new List<Frame>(previous.Rows.Count+updated.Count);
   foreach(var frame in previous.Rows){
    if(affected.Contains(frame.Hash))continue;
    QualityMeasurement measurement;
    bool measured=measurements.TryGetValue(frame.Hash,out measurement);
    bool recalculate=scoringChanged&&(measured||previous.Measurements.ContainsKey(frame.Hash))||
     measured&&cohorts.Count>0&&cohorts.Contains(QualityAnnotator.Cohort(frame,measurement.Kind,compare));
    if(!recalculate){rows.Add(frame);continue;}
    var copy=frame.Clone();
    rows.Add(copy);scoring.Add(copy);
   }
   rows.AddRange(updated);
   QualityAnnotator.Apply(scoring,measurements,compare);
   return new BrowseSnapshot{Revision=revision,CompareTarget=compare,Rows=rows,Measurements=measurements};
  }
 }
}
