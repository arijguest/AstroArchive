using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AstroArchive {
 public sealed partial class Repository {
  // The browse root can change between scans. Remember files by their actual
  // source paths; escape DWARF underscores and other SQL LIKE metacharacters.
  static string SourcePattern(string source){return (source.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar).Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%";}
  internal Dictionary<string,SourceManifest> SourceHistory(string source){
   return db.Query("SELECT data FROM source_manifest WHERE path LIKE ? ESCAPE '\\'",SourcePattern(source)).Select(Util.Deserialize<SourceManifest>)
    .Where(m=>!string.IsNullOrEmpty(m.Path)&&Util.Within(m.Path,source))
    .GroupBy(m=>m.Path,StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g=>g.Key,g=>g.OrderByDescending(m=>m.Source==null?0:m.Source.Changed).ThenByDescending(m=>m.Source==null?0:m.Source.Modified).ThenByDescending(m=>m.Status=="Complete").First(),StringComparer.OrdinalIgnoreCase);
  }
  Dictionary<string,Frame> SourceArchive(string source,bool full){
   var rows=full?db.Query("SELECT data FROM files"):db.Query("SELECT data FROM files WHERE hash IN (SELECT hash FROM source_manifest WHERE path LIKE ? ESCAPE '\\')",SourcePattern(source));
   return rows.Select(Util.Deserialize<Frame>).ToDictionary(f=>f.Hash,StringComparer.OrdinalIgnoreCase);
  }
 }
}
