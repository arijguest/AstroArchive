using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace AstroArchive {
 public partial class MainUi {
  void FillMissingMetadata(){
   if(repo==null||RepositoryOperationBlocked)return;
   var selection=SelectedFiles().Select(f=>f.Clone()).ToList();
   var d=new FormWindow(Window,"Fill missing metadata",650,390);d.Text("Fill missing metadata",true);
   d.Text("Find missing values in original file headers, recognised filenames and preserved session metadata. Known values and user edits stay in place; conflicts remain unknown. DSO and solar-system targets default to Tele when their camera is unknown.");
   var scope=d.Select("Files to check",selection.Count>0?new[]{"Selected files ("+selection.Count+")","Entire repository"}:new[]{"Entire repository"},selection.Count>0?"Selected files ("+selection.Count+")":"Entire repository");
   d.Text("Preview the proposed additions before saving. This updates the catalogue and leaves the original files in place.");d.Accept("Preview missing metadata",()=>true);if(!d.Show())return;
   var frames=scope.SelectedIndex==0&&selection.Count>0?selection:repo.All().Select(f=>f.Clone()).ToList();var proposals=new List<MetadataCompletion>();var errors=new List<string>();
   Run(ct=>{for(int i=0;i<frames.Count;i++){ct.ThrowIfCancellationRequested();Progress(new ProgressInfo{Stage="Reading missing metadata",Done=i,Total=frames.Count,Text=frames[i].OriginalName});try{var proposal=repo.DetectMissingMetadata(frames[i],ct);if(proposal.Fields.Count>0)proposals.Add(proposal);}catch(OperationCanceledException){throw;}catch(Exception error){errors.Add(frames[i].OriginalName+": "+error.Message);}}return "";},ignored=>ReviewMissingMetadata(proposals,errors),"Finding missing metadata");
  }
  void ReviewMissingMetadata(List<MetadataCompletion> proposals,List<string> errors){
   var changes=proposals.SelectMany(p=>p.Fields.Select(key=>new MetadataDetail{Field=key,Value=Convert.ToString(typeof(Frame).GetProperty(key).GetValue(p.Updated,null)),Source=p.Updated.Facts.ContainsKey(key)?p.Updated.Facts[key].Source:""}).Select(row=>new{File=p.Previous.OriginalName,row.Field,row.Value,row.Source})).ToList();
   var d=new FormWindow(Window,"Review missing metadata",920,650);d.Text(changes.Count+" missing values found in "+proposals.Count+" files",true);d.Text("Only these additions will be saved. Existing values and original files are preserved.");
   var grid=new DataGrid{ItemsSource=changes,AutoGenerateColumns=false,IsReadOnly=true,Height=330};foreach(string key in new[]{"File","Field","Value","Source"})grid.Columns.Add(new DataGridTextColumn{Header=key,Binding=new Binding(key),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});d.Add(grid);
   if(errors.Count>0)d.Text(errors.Count+" files could not be inspected:\n"+string.Join("\n",errors.Take(5)));
   if(proposals.Count==0){d.CloseOnly();d.Show();return;}
   d.Accept("Fill missing metadata",()=>true);if(!d.Show())return;
   Run(ct=>{int fields=0,files=0;for(int i=0;i<proposals.Count;i++){ct.ThrowIfCancellationRequested();Progress(new ProgressInfo{Stage="Saving missing metadata",Done=i,Total=proposals.Count,Text=proposals[i].Previous.OriginalName});try{int count=repo.ApplyMissingMetadata(proposals[i],ct);fields+=count;if(count>0)files++;}catch(OperationCanceledException){throw;}catch(Exception error){errors.Add(proposals[i].Previous.OriginalName+": "+error.Message);}}repo.Checkpoint(ct);return fields+" missing values filled in "+files+" files.";},message=>{L("StatusLabel").Text=message;if(errors.Count>0)ShowReport("Missing metadata report",message+"\n\n"+string.Join("\n",errors));},"Filling missing metadata");
  }
 }
}
