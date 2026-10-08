// A collection's panels are independent stacking inputs; outputs have their own role.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public sealed class MosaicExportLayout {
  readonly Dictionary<string,List<MosaicMember>> members;readonly Dictionary<string,MosaicProject> projects;readonly string selected;
  public MosaicExportLayout(Repository repo,string project){selected=project;projects=repo.Mosaics().ToDictionary(p=>p.Id);members=repo.MosaicMembers().GroupBy(m=>m.Hash).ToDictionary(g=>g.Key,g=>g.ToList());if(project!=null&&!projects.ContainsKey(project))throw new InvalidOperationException("The selected mosaic no longer exists.");}
  public MosaicMember Member(Frame f){
   List<MosaicMember> all;if(!members.TryGetValue(f.Hash??"",out all))all=new List<MosaicMember>();
   var active=all.Where(m=>m.State!="Ignored"&&(selected==null||m.ProjectId==selected)).ToList();
   if(active.Count>1)throw new InvalidOperationException(f.OriginalName+" belongs to several mosaics. Choose a mosaic collection for this export.");
   if(active.Count==0){if(selected!=null)throw new InvalidOperationException(f.OriginalName+" is not assigned to the selected mosaic.");if(f.Mosaic!=null&&!all.Any(m=>m.State=="Ignored"))throw new InvalidOperationException("Review and assign mosaic panels before exporting "+f.OriginalName+" as stacking inputs.");return null;}
   var member=active[0];MosaicProject p;if(!projects.TryGetValue(member.ProjectId,out p))throw new InvalidOperationException("Mosaic collection metadata is missing.");
   if(member.State=="Suggested"||(member.Role!="Output"&&p.Panels.All(x=>x.Id!=member.PanelId)))throw new InvalidOperationException("Confirm the mosaic panel assignment for "+f.OriginalName+" before exporting stacking inputs.");
   return member;
  }
  public string Key(Frame f){var m=Member(f);return m==null?"regular":m.ProjectId+"|"+(m.Role=="Output"?"output":m.PanelId);}
  public bool Output(Frame f){var m=Member(f);return m!=null&&m.Role=="Output";}
  public bool Includes(Frame f,string mode,bool includeRejected){if(!Repository.MosaicScience(f)||(!includeRejected&&f.Rejected))return false;bool output=Output(f);return mode=="Both"||mode=="Subs"&&f.Kind=="Light"&&!output||mode=="Stacks"&&(f.Kind=="Stack"||output);}
  public string ProjectFolder(MosaicProject p){return Path.Combine("mosaics",Util.Safe(p.Name)+"_"+p.Id.Substring(0,8));}
  public string Prefix(Frame f){var m=Member(f);if(m==null)return "";var p=projects[m.ProjectId];if(m.Role=="Output")return Path.Combine(ProjectFolder(p),"outputs","completed");var panel=p.Panels.First(x=>x.Id==m.PanelId);return Path.Combine(ProjectFolder(p),"panels",Util.Safe(panel.Name)+"_"+panel.Id.Substring(0,8));}
  public List<MosaicProject> Collections(IEnumerable<Frame> frames){return frames.Select(Member).Where(m=>m!=null).Select(m=>m.ProjectId).Distinct().Select(id=>projects[id]).ToList();}
  public void WriteNotes(string destination,IEnumerable<Frame> frames,string manifestName="manifest.json",CancellationToken ct=default(CancellationToken)){
   foreach(var project in Collections(frames)){string folder=Path.Combine(destination,ProjectFolder(project));Exporter.CheckDestinationPath(Path.Combine(folder,"outputs","panel-results"));Exporter.CheckDestinationPath(Path.Combine(folder,"outputs","stitched"));Directory.CreateDirectory(Path.Combine(folder,"outputs","panel-results"));Directory.CreateDirectory(Path.Combine(folder,"outputs","stitched"));
    Exporter.WriteMetadataText(Path.Combine(folder,"MOSAIC_WORKFLOW.txt"),project.Name+"\r\n"+project.Panels.Count+" known panels"+(project.ExpectedPanels.HasValue?" / "+project.ExpectedPanels.Value+" planned":"")+"\r\n\r\nStack each panel's compatible input groups separately. Keep cameras, filters and calibration states separate. Place the resulting panel images in outputs/panel-results, then align and stitch them in your chosen mosaic software into outputs/stitched.\r\nCompleted imported mosaics are in outputs/completed. Do not combine them with their contributing captures as independent exposures.\r\nPanel membership and source hashes are recorded in "+manifestName+". AstroArchive prepares the inputs; it does not stitch images.\r\n",ct);
   }
  }
 }
}
