// Content-addressed, portable mosaic collections. Image paths and kinds stay intact.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace AstroArchive {
 public sealed class MosaicPanel {
  public string Id,Key,Evidence;public string Name{get;set;}public SkyGeometry Sky;
 }
 public sealed class MosaicProject {
  public string Id,Key,CreatedUtc;public string Name{get;set;}public int? ExpectedPanels;public bool IgnoreDiscovery;
  public List<MosaicPanel> Panels=new List<MosaicPanel>();
  [ScriptIgnore]public string Label {get{return Name+" · "+Panels.Count+" panels"+(ExpectedPanels.HasValue?" / "+ExpectedPanels.Value+" planned":"");}}
 }
 public sealed class MosaicMember {
  public string ProjectId,PanelId,Hash,Role,State,Evidence;
 }
 public sealed class MosaicLabel {
  public string ProjectId,PanelId,Name,Panel,State,Role;
  public MosaicLabel Clone(){return (MosaicLabel)MemberwiseClone();}
 }
 public sealed class MosaicManifestMember {public MosaicMember Membership;public string RelativePath;public bool Missing;}
 public sealed class MosaicManifest {
  public int Schema=1;public List<MosaicProject> Projects=new List<MosaicProject>();public List<MosaicManifestMember> Members=new List<MosaicManifestMember>();
 }
 public sealed class MosaicDetectionResult {
  public int Collections,Assigned,Unresolved;public List<string> Warnings=new List<string>();
  public override string ToString(){return Collections+" collections found; "+Assigned+" panel assignments; "+Unresolved+" unresolved.";}
 }
 public sealed partial class Repository {
  long mosaicLabelGeneration=-1,mosaicManifestGeneration=-1;Dictionary<string,List<MosaicLabel>> mosaicLabelCache;HashSet<string> ignoredMosaicHashes;
  bool mosaicManifestBlocked;public string MosaicWarning{get;private set;}
  void MosaicSchema(){
   db.Exec("CREATE TABLE IF NOT EXISTS mosaics(id TEXT PRIMARY KEY, source_key TEXT UNIQUE, data TEXT NOT NULL)");
   db.Exec("CREATE TABLE IF NOT EXISTS mosaic_members(project TEXT,hash TEXT,data TEXT NOT NULL,PRIMARY KEY(project,hash))");
   db.Exec("CREATE INDEX IF NOT EXISTS mosaic_hash ON mosaic_members(hash)");
   string path=Path.Combine(Meta,"mosaics","manifest.json");
   if(Mosaics().Count==0&&File.Exists(path)){
    try{if(new FileInfo(path).Length>64*1024*1024)throw new InvalidDataException("Mosaic manifest is too large.");RestoreMosaicManifest(Util.Deserialize<MosaicManifest>(File.ReadAllText(path)));}
    catch(Exception e){MosaicWarning="Mosaic manifest could not be restored: "+e.Message;mosaicManifestBlocked=true;}
   }
  }
  public List<MosaicProject> Mosaics(){return db.Query("SELECT data FROM mosaics").Select(Util.Deserialize<MosaicProject>).OrderBy(m=>m.Name,StringComparer.OrdinalIgnoreCase).ToList();}
  public List<MosaicMember> MosaicMembers(string project=null){return db.Query(project==null?"SELECT data FROM mosaic_members":"SELECT data FROM mosaic_members WHERE project=?",project==null?new string[0]:new[]{project}).Select(Util.Deserialize<MosaicMember>).ToList();}
  static string Name(string name){name=(name??"").Trim();if(name.Length==0||name.Length>160)throw new ArgumentException("Enter a name with 1–160 characters.");return name;}
  static bool Id(string id){return Regex.IsMatch(id??"",@"^[a-f0-9]{32}$");}
  static bool CaptureHash(string hash){return Regex.IsMatch(hash??"",@"^[a-f0-9]{64}$");}
  static void ValidateMosaicGeometry(SkyGeometry sky){if(sky==null)return;if(!MosaicGeometry.ValidPosition(sky.RA,sky.Dec)||double.IsNaN(sky.WidthDegrees)||double.IsInfinity(sky.WidthDegrees)||double.IsNaN(sky.HeightDegrees)||double.IsInfinity(sky.HeightDegrees)||sky.WidthDegrees<0||sky.WidthDegrees>180||sky.HeightDegrees<0||sky.HeightDegrees>180||sky.Corners==null||(sky.Corners.Count!=0&&sky.Corners.Count!=4)||sky.Corners.Any(c=>c==null||!MosaicGeometry.ValidPosition(c.RA,c.Dec)))throw new InvalidDataException("Invalid mosaic sky geometry.");}
  public static bool MosaicScience(Frame f){return f!=null&&(f.Kind=="Light"||f.Kind=="Stack");}
  void SaveMosaic(MosaicProject p){db.Exec("INSERT OR REPLACE INTO mosaics(id,source_key,data) VALUES(?,?,?)",p.Id,p.Key??p.Id,Util.Serialize(p));}
  void SaveMosaicMember(MosaicMember m){db.Exec("INSERT OR REPLACE INTO mosaic_members(project,hash,data) VALUES(?,?,?)",m.ProjectId,m.Hash,Util.Serialize(m));}
  MosaicProject Project(string id){var p=Mosaics().FirstOrDefault(m=>m.Id==id);if(p==null)throw new ArgumentException("The mosaic collection no longer exists.");return p;}
  public MosaicProject CreateMosaic(string name){
   var p=new MosaicProject{Id=Guid.NewGuid().ToString("N"),Name=Name(name),CreatedUtc=DateTime.UtcNow.ToString("o")};p.Key="manual:"+p.Id;SaveMosaic(p);return p;
  }
  public MosaicPanel CreateMosaicPanel(string project,string name){
   var p=Project(project);var panel=new MosaicPanel{Id=Guid.NewGuid().ToString("N"),Name=Name(name),Evidence="User-created panel"};p.Panels.Add(panel);SaveMosaic(p);return panel;
  }
  public void RenameMosaic(string project,string name,int? expectedPanels=null){
   var p=Project(project);p.Name=Name(name);if(expectedPanels.HasValue&&(expectedPanels<1||expectedPanels>10000))throw new ArgumentException("Planned panel count must be 1–10000.");p.ExpectedPanels=expectedPanels;SaveMosaic(p);
  }
  public void RenameMosaicPanel(string project,string panel,string name){var p=Project(project);var item=p.Panels.FirstOrDefault(x=>x.Id==panel);if(item==null)throw new ArgumentException("The panel no longer exists.");item.Name=Name(name);SaveMosaic(p);}
  static bool SamePointing(SkyGeometry a,SkyGeometry b){return a!=null&&b!=null&&a.HasFootprint&&b.HasFootprint&&Catalog.Distance(a.RA,a.Dec,b.RA,b.Dec)<=.08*Math.Min(Math.Min(a.WidthDegrees,a.HeightDegrees),Math.Min(b.WidthDegrees,b.HeightDegrees));}
  public void RecordMosaicGeometry(Frame frame,SkyGeometry geometry){
   frame.Sky=geometry;db.Transaction(()=>{Save(frame);if(geometry==null)return;
    foreach(var m in db.Query("SELECT data FROM mosaic_members WHERE hash=?",frame.Hash).Select(Util.Deserialize<MosaicMember>).Where(m=>m.State!="Ignored"&&m.PanelId!=null)){
     var p=Project(m.ProjectId);var panel=p.Panels.FirstOrDefault(x=>x.Id==m.PanelId);if(panel!=null&&(panel.Sky==null||panel.Sky.Approximate&&!geometry.Approximate)){panel.Sky=geometry;SaveMosaic(p);}
    }
   });
  }
  public void AssignMosaic(string project,string panel,IEnumerable<Frame> frames,bool output=false){
   var p=Project(project);if(!output&&p.Panels.All(x=>x.Id!=panel))throw new ArgumentException("Choose a panel for these captures.");
   var rows=frames.GroupBy(f=>f.Hash).Select(g=>g.First()).ToList();if(rows.Count==0)throw new ArgumentException("Select captures to assign.");
   foreach(var f in rows)if(!MosaicScience(f)||!CaptureHash(f.Hash)||Find(f.Hash)==null)throw new ArgumentException("Mosaics accept indexed light captures and stacks.");
   db.Transaction(()=>{foreach(var f in rows)SaveMosaicMember(new MosaicMember{ProjectId=p.Id,PanelId=output?null:panel,Hash=f.Hash,Role=output?"Output":"Input",State="Confirmed",Evidence="User assignment"});});
  }
  public void ConfirmMosaic(string project,IEnumerable<string> hashes){
   var p=Project(project);var keys=new HashSet<string>(hashes);var rows=MosaicMembers(project).Where(m=>keys.Contains(m.Hash)).ToList();
   if(rows.Any(m=>m.State=="Ignored"||(m.Role!="Output"&&p.Panels.All(x=>x.Id!=m.PanelId))))throw new ArgumentException("Assign unresolved captures to a panel before confirming.");
   db.Transaction(()=>{foreach(var m in rows){m.State="Confirmed";m.Evidence+="; user confirmed";SaveMosaicMember(m);}});
  }
  public void RemoveMosaicMembers(string project,IEnumerable<string> hashes){var keys=new HashSet<string>(hashes);db.Transaction(()=>{foreach(var m in MosaicMembers(project).Where(m=>keys.Contains(m.Hash))){m.State="Ignored";m.Evidence="Removed by user; automatic detection will retain this choice";SaveMosaicMember(m);}});}
  public void IgnoreMosaic(string project){var p=Project(project);p.IgnoreDiscovery=true;db.Transaction(()=>{SaveMosaic(p);foreach(var m in MosaicMembers(project)){m.State="Ignored";SaveMosaicMember(m);}});}
  void AttachMosaicLabels(List<Frame> frames){
   if(mosaicLabelCache==null||mosaicLabelGeneration!=db.Generation){
    var projects=Mosaics().ToDictionary(p=>p.Id);var cache=new Dictionary<string,List<MosaicLabel>>();
    var memberships=MosaicMembers();ignoredMosaicHashes=new HashSet<string>(memberships.Where(m=>m.State=="Ignored").Select(m=>m.Hash));
    foreach(var m in memberships.Where(m=>m.State!="Ignored")){MosaicProject p;if(!projects.TryGetValue(m.ProjectId,out p))continue;var panel=p.Panels.FirstOrDefault(x=>x.Id==m.PanelId);List<MosaicLabel> labels;if(!cache.TryGetValue(m.Hash,out labels))cache[m.Hash]=labels=new List<MosaicLabel>();
     labels.Add(new MosaicLabel{ProjectId=p.Id,PanelId=m.PanelId,Name=p.Name,Panel=m.Role=="Output"?"Completed output":panel==null?"Unassigned":panel.Name,State=m.State,Role=m.Role});
    }mosaicLabelCache=cache;mosaicLabelGeneration=db.Generation;
   }
   foreach(var f in frames){List<MosaicLabel> labels;f.MosaicLabels=mosaicLabelCache.TryGetValue(f.Hash??"",out labels)?labels.Select(m=>m.Clone()).ToList():null;f.MosaicDismissed=f.MosaicLabels==null&&ignoredMosaicHashes.Contains(f.Hash??"");}
  }
  public MosaicManifest MosaicSnapshot(){
   var frames=db.Query("SELECT data FROM files").Select(Util.Deserialize<Frame>).ToDictionary(f=>f.Hash);
   var result=new MosaicManifest{Projects=Mosaics()};
   foreach(var member in MosaicMembers()){Frame f;bool exists=frames.TryGetValue(member.Hash,out f);result.Members.Add(new MosaicManifestMember{Membership=member,RelativePath=exists?f.RelativePath:null,Missing=!exists});}
   return result;
  }
  public void RestoreMosaicManifest(MosaicManifest manifest){
   if(manifest==null||manifest.Schema!=1||manifest.Projects==null||manifest.Members==null)throw new InvalidDataException("Unsupported mosaic manifest.");
   var ids=new HashSet<string>();var keys=new HashSet<string>();var members=new HashSet<string>();
   foreach(var p in manifest.Projects){
    if(p==null||!Id(p.Id)||!ids.Add(p.Id)||!keys.Add(p.Key??p.Id)||p.Panels==null||p.Panels.Count>10000)throw new InvalidDataException("Invalid or duplicate mosaic collection.");
    Name(p.Name);if(p.ExpectedPanels.HasValue&&(p.ExpectedPanels<1||p.ExpectedPanels>10000))throw new InvalidDataException("Invalid panel count.");
    var panels=new HashSet<string>();foreach(var panel in p.Panels){if(panel==null||!Id(panel.Id)||!panels.Add(panel.Id))throw new InvalidDataException("Invalid or duplicate panel.");Name(panel.Name);ValidateMosaicGeometry(panel.Sky);}
   }
   foreach(var entry in manifest.Members){var m=entry==null?null:entry.Membership;var p=m==null?null:manifest.Projects.FirstOrDefault(x=>x.Id==m.ProjectId);
    if(m==null||p==null||!CaptureHash(m.Hash)||!members.Add(m.ProjectId+"|"+m.Hash)||!new[]{"Declared","Suggested","Confirmed","Ignored"}.Contains(m.State)||!new[]{"Input","Output"}.Contains(m.Role)||(m.PanelId!=null&&p.Panels.All(x=>x.Id!=m.PanelId))||(m.Role=="Output"&&m.PanelId!=null))throw new InvalidDataException("Invalid mosaic membership.");
    if(entry.RelativePath!=null&&(Path.IsPathRooted(entry.RelativePath)||!Util.Within(Path.Combine(Root,entry.RelativePath),Root)))throw new InvalidDataException("Mosaic manifest path escapes the archive.");
   }
   db.Transaction(()=>{foreach(var p in manifest.Projects)SaveMosaic(p);foreach(var m in manifest.Members)SaveMosaicMember(m.Membership);});
  }
  void CheckpointMosaics(CancellationToken ct){
   if(mosaicManifestBlocked||mosaicManifestGeneration==db.Generation)return;
   var snapshot=MosaicSnapshot();if(snapshot.Projects.Count==0)return;
   string directory=Path.Combine(Meta,"mosaics");Directory.CreateDirectory(directory);string target=Path.Combine(directory,"manifest.json"),temp=target+"."+Guid.NewGuid().ToString("N")+".partial";long generation=db.Generation;
   try{ct.ThrowIfCancellationRequested();File.WriteAllText(temp,Util.Serialize(snapshot),new UTF8Encoding(false));CommitTemporary(temp,target,ct);mosaicManifestGeneration=generation;}finally{TryRemove(temp);}
  }
  public MosaicDetectionResult DetectMosaics(IEnumerable<Frame> frames,CancellationToken ct,bool geometrySuggestions=false){
   var result=new MosaicDetectionResult();var input=frames.ToList();if(!geometrySuggestions&&!input.Any(f=>MosaicScience(f)&&f.Mosaic!=null))return result;ct.ThrowIfCancellationRequested();db.Transaction(()=>{var projects=Mosaics().ToDictionary(p=>p.Key??p.Id);var existing=MosaicMembers().ToDictionary(m=>m.ProjectId+"|"+m.Hash);
   var indexedHashes=new HashSet<string>(db.Query("SELECT hash FROM files"));var rows=input.Where(f=>MosaicScience(f)&&CaptureHash(f.Hash)&&indexedHashes.Contains(f.Hash)).GroupBy(f=>f.Hash).Select(g=>g.First()).OrderBy(f=>f.Hash).ToList();
   if(geometrySuggestions)foreach(var f in rows.Where(f=>f.Mosaic==null)){
    var marker=MosaicMetadata.Read(f,new FitsHeader(),f.SourcePath??FilePath(f),f.SourceRoot??Root,null);if(marker!=null){f.Mosaic=marker;Save(f);}
   }
   foreach(var group in rows.Where(f=>f.Mosaic!=null).GroupBy(f=>f.Mosaic.ProjectKey)){
    ct.ThrowIfCancellationRequested();if(string.IsNullOrEmpty(group.Key))continue;var hinted=group.ToList();MosaicProject project;
    if(!projects.TryGetValue(group.Key,out project)){var h=hinted[0].Mosaic;project=new MosaicProject{Id=Guid.NewGuid().ToString("N"),Key=group.Key,Name=h.Name,ExpectedPanels=h.ExpectedPanels,CreatedUtc=DateTime.UtcNow.ToString("o")};projects.Add(group.Key,project);}
    if(project.IgnoreDiscovery)continue;result.Collections++;
    var positions=geometrySuggestions?MosaicGeometry.PointingGroups(hinted.Where(f=>f.Mosaic.PanelKey==null)):new List<List<Frame>>();
    foreach(var f in hinted){
     ct.ThrowIfCancellationRequested();MosaicMember prior;if(existing.TryGetValue(project.Id+"|"+f.Hash,out prior)&&new[]{"Confirmed","Ignored"}.Contains(prior.State))continue;
     var h=f.Mosaic;string key=h.Conflict==null?h.PanelKey:null;string evidence=h.Evidence;bool declared=h.Declared&&key!=null;
     if(key==null&&h.Conflict==null&&positions.Count>1){var pointing=positions.FirstOrDefault(g=>g.Any(x=>x.Hash==f.Hash));if(pointing!=null){key="sky:"+pointing[0].Hash;evidence+="; distinct WCS pointing (review required)";}}
     MosaicPanel panel=null;if(key!=null){panel=project.Panels.FirstOrDefault(p=>p.Key==key||key.StartsWith("sky:")&&SamePointing(p.Sky,f.Sky));if(panel==null){panel=new MosaicPanel{Id=Guid.NewGuid().ToString("N"),Key=key,Name=key.StartsWith("sky:")?"Panel "+(project.Panels.Count+1).ToString("00"):"Panel "+key,Sky=f.Sky,Evidence=evidence};project.Panels.Add(panel);}else if(panel.Sky==null&&f.Sky!=null)panel.Sky=f.Sky;}
     var member=new MosaicMember{ProjectId=project.Id,PanelId=h.Output?null:panel==null?null:panel.Id,Hash=f.Hash,Role=h.Output?"Output":"Input",State=h.Conflict==null&&(declared||h.Declared&&h.Output)?"Declared":"Suggested",Evidence=h.Conflict??evidence};
     SaveMosaicMember(member);existing[project.Id+"|"+f.Hash]=member;if(member.PanelId!=null||member.Role=="Output")result.Assigned++;else result.Unresolved++;if(h.Conflict!=null)result.Warnings.Add(f.OriginalName+": "+h.Conflict);
    }SaveMosaic(project);
   }
   // Geometry alone suggests within a capture session; it never joins a whole sky survey.
   if(geometrySuggestions)foreach(var session in rows.Where(f=>f.Mosaic==null&&f.Sky!=null).GroupBy(f=>(f.TelescopeIdentity??f.Telescope)+"|"+f.Session+"|"+f.Target+"|"+f.Camera)){
    ct.ThrowIfCancellationRequested();var pointings=MosaicGeometry.PointingGroups(session);if(pointings.Count<2)continue;if(pointings.Count>128){result.Warnings.Add("Skipped a session with more than 128 distinct fields; select a smaller capture set for review.");continue;}
    var neighbors=new Dictionary<int,List<int>>();for(int i=0;i<pointings.Count;i++)neighbors[i]=new List<int>();
    for(int i=0;i<pointings.Count;i++)for(int j=i+1;j<pointings.Count;j++){double overlap=MosaicGeometry.Overlap(pointings[i][0].Sky,pointings[j][0].Sky);if(overlap>=.05&&overlap<=.85){neighbors[i].Add(j);neighbors[j].Add(i);}}
    var visited=new HashSet<int>();for(int i=0;i<pointings.Count;i++){if(!visited.Add(i))continue;var component=new List<int>();var queue=new Queue<int>();queue.Enqueue(i);while(queue.Count>0){int n=queue.Dequeue();component.Add(n);foreach(int adjacent in neighbors[n])if(visited.Add(adjacent))queue.Enqueue(adjacent);}if(component.Count<2)continue;
     string prefix="geometry:"+Util.HashText(session.Key)+":";var candidates=projects.Values.Where(p=>(p.Key??"").StartsWith(prefix)&&component.Any(n=>p.Panels.Any(panel=>SamePointing(panel.Sky,pointings[n][0].Sky)))).ToList();string key=candidates.Count==1?candidates[0].Key:prefix+Guid.NewGuid().ToString("N");MosaicProject project;
     if(!projects.TryGetValue(key,out project)){project=new MosaicProject{Id=Guid.NewGuid().ToString("N"),Key=key,Name=(Catalog.IsAmbiguous(session.First().Target)?"Sky field":session.First().Target)+" mosaic candidate",CreatedUtc=DateTime.UtcNow.ToString("o")};projects.Add(key,project);}if(project.IgnoreDiscovery)continue;result.Collections++;
     foreach(int n in component){var pointing=pointings[n];string panelKey="sky:"+pointing[0].Hash;var panel=project.Panels.FirstOrDefault(p=>p.Key==panelKey||SamePointing(p.Sky,pointing[0].Sky));if(panel==null){panel=new MosaicPanel{Id=Guid.NewGuid().ToString("N"),Key=panelKey,Name="Panel "+(project.Panels.Count+1).ToString("00"),Sky=pointing[0].Sky,Evidence="Distinct pointing with neighboring footprint overlap; user review required"};project.Panels.Add(panel);}
      foreach(var f in pointing){MosaicMember prior;if(existing.TryGetValue(project.Id+"|"+f.Hash,out prior)&&new[]{"Confirmed","Ignored"}.Contains(prior.State))continue;var member=new MosaicMember{ProjectId=project.Id,PanelId=panel.Id,Hash=f.Hash,Role="Input",State="Suggested",Evidence=panel.Evidence+(f.Sky.Approximate?"; approximate footprint":"")};SaveMosaicMember(member);existing[project.Id+"|"+f.Hash]=member;result.Assigned++;}
     }SaveMosaic(project);
    }
   }
   });return result;
  }
  public void ReadMosaicMetadata(IEnumerable<Frame> frames,CancellationToken ct,Action<ProgressInfo> progress){
   var rows=frames.Where(MosaicScience).ToList();var sessions=new Dictionary<string,Classifier.ShotsMetadata>(StringComparer.OrdinalIgnoreCase);int done=0;
   foreach(var f in rows){ct.ThrowIfCancellationRequested();ValidateCapture(f,ct);string path=FilePath(f);var info=Assets.Inspect(path,n=>ct.ThrowIfCancellationRequested());var selected=Assets.Selected(f);var image=selected==null?null:info.Images.FirstOrDefault(i=>i.Key==selected.Key);var header=image==null?info.Header:new FitsHeader{Values=image.Headers??new Dictionary<string,string>(),Width=image.Width,Height=image.Height,Channels=image.Channels};MosaicHint session=null;
    string sidecar=string.IsNullOrEmpty(f.SidecarRelativePath)?null:Path.GetFullPath(Path.Combine(Root,f.SidecarRelativePath));
    if(sidecar!=null&&Util.Within(sidecar,Root)&&File.Exists(sidecar)){Classifier.ShotsMetadata cached;if(!sessions.TryGetValue(sidecar,out cached)){cached=new Classifier.ShotsMetadata();if(new FileInfo(sidecar).Length<=4*1024*1024)cached.Raw=Util.Deserialize<Dictionary<string,object>>(File.ReadAllText(sidecar));sessions[sidecar]=cached;}session=MosaicMetadata.Session(cached.Raw,f.OriginalName);}
    f.Mosaic=MosaicMetadata.Read(f,header,f.SourcePath??path,f.SourceRoot??Root,session);var geometry=MosaicGeometry.FromHeader(header,header.Width,header.Height);if(geometry!=null)f.Sky=geometry;RecordMosaicGeometry(f,f.Sky);if(progress!=null)progress(new ProgressInfo{Done=++done,Total=rows.Count,Stage="Mosaic metadata",Text=f.OriginalName});
   }
  }
 }
}
