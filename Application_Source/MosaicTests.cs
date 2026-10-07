// Generated captures verify provenance, portable relationships and export boundaries.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public partial class Tests {
  static FitsHeader Wcs(double ra=56,double dec=24,double rotation=0){
   var h=new FitsHeader{Width=64,Height=48};double t=rotation*Math.PI/180;
   foreach(var p in new Dictionary<string,string>{{"CTYPE1","RA---TAN"},{"CTYPE2","DEC--TAN"},{"CRVAL1",ra.ToString(CultureInfo.InvariantCulture)},{"CRVAL2",dec.ToString(CultureInfo.InvariantCulture)},{"CRPIX1","32.5"},{"CRPIX2","24.5"},{"CD1_1",(-.04*Math.Cos(t)).ToString(CultureInfo.InvariantCulture)},{"CD1_2",(.04*Math.Sin(t)).ToString(CultureInfo.InvariantCulture)},{"CD2_1",(.04*Math.Sin(t)).ToString(CultureInfo.InvariantCulture)},{"CD2_2",(.04*Math.Cos(t)).ToString(CultureInfo.InvariantCulture)}})h.Values[p.Key]=p.Value;return h;
  }
  static List<Frame> MosaicFixture(Repository repo,string name,bool hints=true,int count=4){
   string source=Path.Combine(root,"captures-"+Util.HashText(name).Substring(0,12));Directory.CreateDirectory(source);
   for(int i=0;i<count;i++){var h=LightHeaders(new DateTime(2026,10,6+i%2,21,0,i),"M45");if(hints){h["MOSAICID"]="'capture-project'";h["MOSNAME"]="'Pleiades mosaic'";h["PANELID"]="'"+(i%2+1)+"'";h["NPANELS"]="4";}
    foreach(var field in Wcs(56+(i%2)*1.8).Values)h[field.Key]=field.Key.StartsWith("CTYPE")?"'"+field.Value+"'":field.Value;
    Write(Path.Combine(source,"Light_M45_"+i.ToString("000")+".fit"),64,48,(x,y)=>1000+x+y+i*10,h);
   }
   var plan=repo.Scan(source,"Seestar-01","Seestar S50",ct,NoProgress);repo.Import(plan.Frames,ct,NoProgress);return repo.All();
  }
  static void MosaicTests(){
   Test("Mosaic classification reuses an already-read header without touching image pixels",()=>{
    string p=Path.Combine(root,"metadata-only-mosaic.fit");var headers=LightHeaders(new DateTime(2026,10,6,21,0,0),"M45");headers["MOSAICID"]="'project-1'";headers["PANELID"]="'1'";Write(p,64,48,(x,y)=>1000,headers);var h=Fits.Header(p);var stamp=FileStamp.Read(p);File.Delete(p);
    var f=Classifier.Read(p,root,"Seestar-01","Auto",stamp.Size,null,null,h,stamp);
    Check(f.Mosaic!=null&&f.Mosaic.Declared&&f.Mosaic.PanelKey=="1"&&f.Kind=="Light","Mosaic metadata was lost or pixels were required.");
   });
   Test("DWARF MOSAIC markers retain uncertainty and do not invent panels",()=>{
    string dir=Path.Combine(root,"DWARF_RAW_TELE_MOSAIC_M45_EXP_60_GAIN_60_2026-10-06");Directory.CreateDirectory(dir);string p=Path.Combine(dir,"raw_M45_001.fit");Write(p,64,48,(x,y)=>1000,new Dictionary<string,string>());var f=Classifier.Read(p,root,"Dwarf-03","Dwarf 3");
    Check(f.Target=="M45"&&f.Mosaic!=null&&!f.Mosaic.Declared&&f.Mosaic.PanelKey==null,"Marker became a confirmed panel.");
   });
   Test("Nested mosaic session arrays associate panels only with exact filenames",()=>{
    var json=Util.Deserialize<Dictionary<string,object>>("{\"mosaic\":{\"id\":\"project\",\"name\":\"Wide sky\",\"panels\":[{\"id\":\"A\",\"files\":[\"one.fit\"]},{\"id\":\"B\",\"files\":[\"two.fit\"]}]}}");
    Check(MosaicMetadata.Session(json,"one.fit").PanelKey=="A"&&MosaicMetadata.Session(json,"two.fit").PanelKey=="B","Panel array association lost.");
    Check(MosaicMetadata.Session(json,"other.fit").PanelKey==null,"An unrelated panel leaked into a capture.");
   });
   Test("Conflicting header session and filename panel evidence remains unresolved",()=>{
    var f=new Frame{Target="M45",Telescope="T",Session="S"};var h=new FitsHeader();h.Values["MOSAICID"]="project";h.Values["PANELID"]="2";var hint=MosaicMetadata.Read(f,h,Path.Combine(root,"MOSAIC","panel_1","one.fit"),root,null);
    Check(hint.Conflict!=null,"Conflicting panel assignments were accepted.");
   });
   Test("FITS footprints support rotated fields right-ascension wrap and reference pixels",()=>{
    var a=MosaicGeometry.FromHeader(Wcs(359.8,24,25),64,48);var b=MosaicGeometry.FromHeader(Wcs(.8,24,25),64,48);
    Check(a!=null&&b!=null&&a.Corners.All(p=>p.RA>=0&&p.RA<360),"RA wrap failed.");Check(MosaicGeometry.Overlap(a,b)>.1,"Rotated neighboring fields did not overlap.");
    var shifted=Wcs();shifted.Values["CRPIX1"]="12.5";var centre=MosaicGeometry.FromHeader(shifted,64,48);Check(Catalog.Distance(centre.RA,centre.Dec,56,24)>.5,"CRVAL was assumed to be the image centre.");
   });
   Test("Unsupported distorted non-J2000 and invalid WCS does not create trusted geometry",()=>{
    foreach(var change in new[]{new KeyValuePair<string,string>("CTYPE1","RA---TAN-SIP"),new KeyValuePair<string,string>("EQUINOX","1950"),new KeyValuePair<string,string>("CUNIT1","hour"),new KeyValuePair<string,string>("CRVAL2","95"),new KeyValuePair<string,string>("A_ORDER","2")}){var h=Wcs();h.Values[change.Key]=change.Value;Check(MosaicGeometry.FromHeader(h,64,48)==null,"Unsupported WCS accepted: "+change.Key);}
   });
   Test("Declared imports form stable panels across nights while preserving canonical captures",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-declared-repo"))){var rows=MosaicFixture(repo,"mosaic-declared-source");var project=repo.Mosaics().Single();Check(project.Panels.Count==2&&project.ExpectedPanels==4,"Declared panels or planned count missing.");Check(repo.MosaicMembers().All(m=>m.State=="Declared")&&rows.All(f=>f.MosaicLabels.Count==1),"Declared membership missing.");Check(rows.Select(f=>f.Night).Distinct().Count()==2&&rows.All(f=>f.Kind=="Light"&&f.RelativePath.StartsWith("Targets")),"Mosaic changed capture category.");var ids=project.Panels.Select(p=>p.Id).ToArray();repo.RenameMosaic(project.Id,"Renamed collection",4);repo.RenameMosaicPanel(project.Id,ids[0],"Northern panel");Check(repo.Mosaics().Single().Panels.Select(p=>p.Id).SequenceEqual(ids),"Rename changed panel identity.");foreach(var f in repo.All())Check(Util.Hash(repo.FilePath(f),ct)==f.Hash,"Capture bytes changed.");}
   });
   Test("Ordinary dithering and target-centre coordinates do not create mosaic candidates",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-dither-repo"))){var rows=MosaicFixture(repo,"mosaic-dither-source",false);foreach(var f in rows){f.Session="same";f.Sky=MosaicGeometry.FromHeader(Wcs(56+Array.IndexOf(rows.ToArray(),f)*.01),64,48);repo.Save(f);}repo.DetectMosaics(rows,ct,true);Check(repo.Mosaics().Count==0,"Dithering became a mosaic.");foreach(var f in rows){f.Sky=null;f.RA=56;f.Dec=24;}repo.DetectMosaics(rows,ct,true);Check(repo.Mosaics().Count==0,"Target coordinates became panel geometry.");}
   });
   Test("Neighboring WCS fields are suggestions and require confirmation before stacking",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-geometry-repo"))){var rows=MosaicFixture(repo,"mosaic-geometry-source",false);foreach(var f in rows)f.Session="same";var result=repo.DetectMosaics(rows,ct,true);Check(result.Assigned==4&&repo.Mosaics().Single().Panels.Count==2,"Distinct pointings were not suggested.");Expect(()=>Exporter.Create(repo,rows,new ExportOptions{Parent=root,Name="unconfirmed-mosaic"},ct,NoProgress),"Unconfirmed panels were stacked.");Check(!Directory.Exists(Path.Combine(root,"unconfirmed-mosaic")),"Failed export left a project.");repo.ConfirmMosaic(repo.Mosaics().Single().Id,rows.Select(f=>f.Hash));Check(repo.MosaicMembers().All(m=>m.State=="Confirmed"),"Confirmation lost.");}
   });
   Test("Manual mosaic assignments can span targets nights filters and several collections",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-manual-repo"))){var rows=MosaicFixture(repo,"mosaic-manual-source",false);var a=repo.CreateMosaic("Heart and Soul");var pa=repo.CreateMosaicPanel(a.Id,"North");var b=repo.CreateMosaic("Other project");var pb=repo.CreateMosaicPanel(b.Id,"One");repo.AssignMosaic(a.Id,pa.Id,rows);repo.AssignMosaic(b.Id,pb.Id,rows.Take(1));Expect(()=>Exporter.Create(repo,rows,new ExportOptions{Parent=root,Name="ambiguous-mosaic"},ct,NoProgress),"Multi-collection export was guessed.");string result=Exporter.Create(repo,rows,new ExportOptions{Parent=root,Name="selected-mosaic",MosaicId=a.Id},ct,NoProgress);Check(Directory.GetFiles(result,"*.fit",SearchOption.AllDirectories).Length==4,"Selected collection export lost captures.");}
   });
   Test("User reassignment and removal survive repeated automatic mosaic detection",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-override-repo"))){var rows=MosaicFixture(repo,"mosaic-override-source");var p=repo.Mosaics().Single();var manual=repo.CreateMosaicPanel(p.Id,"Custom");repo.AssignMosaic(p.Id,manual.Id,rows.Take(1));repo.RemoveMosaicMembers(p.Id,rows.Skip(1).Take(1).Select(f=>f.Hash));repo.DetectMosaics(rows,ct,true);Check(repo.MosaicMembers(p.Id).Single(m=>m.Hash==rows[0].Hash).PanelId==manual.Id,"Manual assignment was overwritten.");Check(repo.MosaicMembers(p.Id).Single(m=>m.Hash==rows[1].Hash).State=="Ignored","Removed capture was re-added.");}
   });
   Test("Mosaic stacking exports keep equal-setting panels and completed outputs separate",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-export-repo"))){var rows=MosaicFixture(repo,"mosaic-export-source");var p=repo.Mosaics().Single();var output=rows[3];repo.AssignMosaic(p.Id,null,new[]{output},true);var layout=new MosaicExportLayout(repo,p.Id);Check(layout.Includes(output,"Stacks",false)&&!layout.Includes(output,"Subs",false),"Output eligibility depended on its capture kind.");string result=Exporter.Create(repo,repo.All(),new ExportOptions{Parent=root,Name="mosaic-panel-project",Mode="Both"},ct,NoProgress);var files=Directory.GetFiles(result,"*.fit",SearchOption.AllDirectories);Check(files.Length==4&&files.Count(f=>f.Contains(Path.Combine("outputs","completed")))==1,"Completed mosaic pooled with inputs.");Check(Directory.GetDirectories(Path.Combine(result,"mosaics")).SelectMany(d=>Directory.GetDirectories(Path.Combine(d,"panels"))).Count()==2,"Panels pooled despite identical capture settings.");Check(File.ReadAllText(Path.Combine(result,"manifest.json")).Contains("MosaicCollections"),"Mosaic provenance missing.");foreach(string path in files)Check(rows.Any(f=>f.Hash==Util.Hash(path,ct)),"Export modified a capture.");string outputs=Exporter.Create(repo,new List<Frame>{output},new ExportOptions{Parent=root,Name="mosaic-output-only",Mode="Stacks"},ct,NoProgress);Check(Directory.GetFiles(outputs,"*.fit",SearchOption.AllDirectories).Length==1,"Completed output alone could not be exported.");}
   });
   Test("Portable mosaic manifests restore stable memberships after index loss and reindex",()=>{
    string path=Path.Combine(root,"mosaic-portable-repo"),working,id,panel;List<string> hashes;
    using(var repo=new Repository(path)){var rows=MosaicFixture(repo,"mosaic-portable-source");var p=repo.Mosaics().Single();id=p.Id;panel=p.Panels[0].Id;hashes=rows.Select(f=>f.Hash).ToList();repo.ConfirmMosaic(id,hashes);repo.Checkpoint(ct);working=repo.WorkingIndex;Check(File.Exists(Path.Combine(repo.Meta,"mosaics","manifest.json")),"Manifest missing.");}
    File.Delete(working);File.Delete(Path.Combine(path,".astroarchive","index.sqlite"));
    using(var repo=new Repository(path)){Check(repo.Mosaics().Single().Id==id&&repo.Mosaics().Single().Panels[0].Id==panel,"Manifest changed identities.");repo.Scan(path,"Seestar-01","Seestar S50",ct,NoProgress,true);Check(repo.All().Count==4&&repo.All().All(f=>f.MosaicLabels.Any(m=>m.ProjectId==id&&m.State=="Confirmed")),"Reindex lost content-based membership.");}
   });
   Test("Invalid mosaic manifests are rejected before any collection mutation",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-invalid-repo"))){var p=repo.CreateMosaic("Keep");repo.CreateMosaicPanel(p.Id,"One");var manifest=repo.MosaicSnapshot();manifest.Members.Add(new MosaicManifestMember{Membership=new MosaicMember{ProjectId=p.Id,Hash=new string('a',64),Role="Input",State="Suggested"},RelativePath="../escape.fit"});Expect(()=>repo.RestoreMosaicManifest(manifest),"Path traversal accepted.");Check(repo.Mosaics().Count==1,"Invalid restore partially changed collections.");manifest.Members.Clear();manifest.Projects[0].Panels[0].Sky=new SkyGeometry{RA=56,Dec=95};Expect(()=>repo.RestoreMosaicManifest(manifest),"Invalid sky geometry accepted.");Check(repo.Mosaics().Single().Panels.Single().Sky==null,"Invalid geometry mutated the collection.");manifest.Schema=99;Expect(()=>repo.RestoreMosaicManifest(manifest),"Unknown manifest schema accepted.");}
   });
   Test("Canceled mosaic discovery commits no partial panel membership",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-cancel-repo"))){var rows=MosaicFixture(repo,"mosaic-cancel-source",false);using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect(()=>repo.DetectMosaics(rows,cancel.Token,true),"Canceled discovery continued.");}Check(repo.Mosaics().Count==0,"Canceled discovery created collections.");}
   });
   Test("Named mosaic folders group different panel targets without changing target labels",()=>{
    Frame a=new Frame{Telescope="T",Target="M31",Session="A",OriginalName="one.fit"},b=new Frame{Telescope="T",Target="M32",Session="B",OriginalName="two.fit"};
    a.Mosaic=MosaicMetadata.Read(a,new FitsHeader(),Path.Combine(root,"MOSAIC_Andromeda","panel_01","one.fit"),root,null);b.Mosaic=MosaicMetadata.Read(b,new FitsHeader(),Path.Combine(root,"MOSAIC_Andromeda","panel_02","two.fit"),root,null);
    Check(a.Mosaic.ProjectKey==b.Mosaic.ProjectKey&&a.Mosaic.PanelKey=="1"&&b.Mosaic.PanelKey=="2"&&a.Target=="M31"&&b.Target=="M32","Mosaic folder was split by target labels.");
   });
   Test("Geometric collection and panel identities survive added captures and repeated discovery",()=>{
    using(var repo=new Repository(Path.Combine(root,"geo-stability-repo"))){var rows=MosaicFixture(repo,"geo-stability-source",false);foreach(var f in rows)f.Session="same";repo.DetectMosaics(rows,ct,true);var original=repo.Mosaics().Single();var ids=original.Panels.Select(p=>p.Id).OrderBy(x=>x).ToArray();var extra=rows[0].Clone();extra.Hash=new string('0',64);extra.OriginalName="extra.fit";repo.Save(extra);rows.Add(extra);repo.DetectMosaics(rows,ct,true);Check(repo.Mosaics().Count==1&&repo.Mosaics().Single().Id==original.Id&&repo.Mosaics().Single().Panels.Select(p=>p.Id).OrderBy(x=>x).SequenceEqual(ids),"New exposures changed stable identities.");}
   });
   Test("Mosaic filters match individual collections and panels for shared captures",()=>{
    var f=new Frame{MosaicLabels=new List<MosaicLabel>{new MosaicLabel{Name="One",Panel="North",State="Confirmed"},new MosaicLabel{Name="Two",Panel="South",State="Declared"}}};var filters=new CaptureFilters();filters.Values["Mosaic"]="Two";filters.Values["Panel"]="South";Check(filters.Apply(new[]{f},"").Count==1,"Shared capture filtering used combined text instead of membership.");
   });
   Test("Rejected manifest files stay intact when a repository opens and checkpoints",()=>{
    string path=Path.Combine(root,"broken-mosaic-manifest"),dir=Path.Combine(path,".astroarchive","mosaics");Directory.CreateDirectory(dir);string manifest=Path.Combine(dir,"manifest.json");File.WriteAllText(manifest,"{\"Schema\":99}");
    using(var repo=new Repository(path)){Check(!string.IsNullOrEmpty(repo.MosaicWarning),"Bad manifest was silently ignored.");repo.CreateMosaic("New local collection");repo.Checkpoint(ct);}
    Check(File.ReadAllText(manifest)=="{\"Schema\":99}","Bad manifest was overwritten during recovery.");
   });
   Test("Standalone solver WCS cards preserve a complete footprint without decoding pixels",()=>{
    var h=Wcs(56,24,15);var cards=h.Values.Select(p=>Card(p.Key,p.Key.StartsWith("CTYPE")?"'"+p.Value+"'":p.Value)).ToList();cards.Add("END".PadRight(80));using(var stream=new MemoryStream(System.Text.Encoding.ASCII.GetBytes(string.Concat(cards)))){var parsed=MosaicGeometry.WcsCards(stream);Check(MosaicGeometry.FromHeader(parsed,64,48)!=null,"Solver WCS lost geometry.");}
   });
   Test("CROTA geometry remains rotated when an IMAGE HDU includes PCOUNT",()=>{
    var h=Wcs();foreach(string key in new[]{"CD1_1","CD1_2","CD2_1","CD2_2"})h.Values.Remove(key);h.Values["CDELT1"]="-0.04";h.Values["CDELT2"]="0.04";h.Values["CROTA2"]="90";h.Values["PCOUNT"]="0";var sky=MosaicGeometry.FromHeader(h,64,48);Check(Math.Abs(sky.Corners[0].Dec-sky.Corners[1].Dec)>2,"PCOUNT hid CROTA rotation.");
   });
   Test("Dismissed metadata memberships stop categorising captures in Library",()=>{
    using(var repo=new Repository(Path.Combine(root,"mosaic-dismiss-repo"))){var rows=MosaicFixture(repo,"mosaic-dismiss-source");repo.IgnoreMosaic(repo.Mosaics().Single().Id);Check(repo.All().All(f=>f.MosaicText=="-"&&f.MosaicDismissed),"Dismissed mosaic still appears as a library category.");repo.DetectMosaics(rows,ct);Check(repo.MosaicMembers().All(m=>m.State=="Ignored"),"Dismissed collection was revived.");}
   });
  }
 }
}
