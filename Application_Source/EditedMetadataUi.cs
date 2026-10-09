using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  sealed class EditedMetadataFields {
   public TextBox Object,Filters,Subs,SubExposure,TotalExposure,RA,Dec;public ComboBox ImageClass;
   readonly Dictionary<TextBox,string> initial=new Dictionary<TextBox,string>();public string InitialClass;
   public void Remember(){foreach(var box in new[]{Object,Filters,Subs,SubExposure,TotalExposure,RA,Dec})initial[box]=box.Text;InitialClass=Convert.ToString(ImageClass.SelectedItem);}
   string Changed(TextBox box){string value=box.Text.Trim();return value.Length==0||value==initial[box].Trim()?null:value;}
   double? Seconds(TextBox box){string value=Changed(box);if(value==null)return null;double result;if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out result)||result<=0||double.IsNaN(result)||double.IsInfinity(result))throw new ArgumentException("Enter positive exposure seconds using a decimal point.");return result;}
   public EditedMetadata Values(){int count;string subs=Changed(Subs);if(subs!=null&&(!int.TryParse(subs,NumberStyles.Integer,CultureInfo.InvariantCulture,out count)||count<=0))throw new ArgumentException("Enter a positive whole-number sub count.");string imageClass=Convert.ToString(ImageClass.SelectedItem);return new EditedMetadata{Object=Changed(Object),Filters=Changed(Filters),Subs=subs==null?null:(int?)int.Parse(subs,CultureInfo.InvariantCulture),SubExposure=Seconds(SubExposure),TotalExposure=Seconds(TotalExposure),RA=Changed(RA),Dec=Changed(Dec),ImageClass=imageClass==InitialClass||imageClass=="Keep existing"?null:imageClass};}
  }
  List<EditedImage> SelectedEditedImages(){return editedSelection.Items;}
  FormWindow EditedMetadataDialog(List<EditedImage> selected,out EditedMetadataFields fields){
   var dialog=new FormWindow(Window,"Edit metadata",610,730);dialog.Text(selected.Count+" selected edited image"+(selected.Count==1?"":"s"),true);dialog.Text("Changes apply to all "+selected.Count+" selected image"+(selected.Count==1?"":"s")+" by default. Empty fields keep existing values. Assignments are saved with Edited; image files remain unchanged.");
   Func<Func<EditedMetadata,string>,string> shared=get=>{var values=selected.Select(i=>get(i.Metadata)??"").Distinct().ToList();return values.Count==1?values[0]:"";};fields=new EditedMetadataFields();
   fields.Object=dialog.Input("Target (object ID or common name)",shared(m=>m.Object));fields.Filters=dialog.Input("Filters / channels",shared(m=>m.Filters));
   fields.ImageClass=dialog.Select("Image class",new[]{"Keep existing","Edited image","Starless","Stars only","GIF","Meteor","Unknown (conflicting labels)"},selected.Select(i=>i.Metadata.ImageClass).Distinct().Count()==1?selected[0].Metadata.ImageClass:"Keep existing");
   fields.TotalExposure=dialog.Input("Total exposure seconds (optional)",shared(m=>m.TotalExposure.HasValue?m.TotalExposure.Value.ToString("G",CultureInfo.InvariantCulture):""));fields.Subs=dialog.Input("Sub count (optional)",shared(m=>m.Subs.HasValue?m.Subs.Value.ToString(CultureInfo.InvariantCulture):""));fields.SubExposure=dialog.Input("Exposure per sub in seconds (optional)",shared(m=>m.SubExposure.HasValue?m.SubExposure.Value.ToString("G",CultureInfo.InvariantCulture):""));
   fields.RA=dialog.Input("RA (as recorded, optional)",shared(m=>m.RA));fields.Dec=dialog.Input("Dec (as recorded, optional)",shared(m=>m.Dec));fields.Remember();var controls=fields;
   dialog.Accept("Save metadata",()=>{try{controls.Values();return true;}catch(ArgumentException e){MessageBox.Show(dialog.Window,e.Message);return false;}});return dialog;
  }
  void EditEditedMetadata(){EditEditedMetadata(SelectedEditedImages());}
  async void EditEditedMetadata(List<EditedImage> selection){
   if(repo==null||RepositoryOperationBlocked||SearchBlocked("EditedSearchBox")||metadataPreviewSuspended)return;var selected=selection.ToList();if(selected.Count==0)return;var repository=repo;
   try{
    await SuspendMetadataPreviews();if(closing||repo!=repository||RepositoryOperationBlocked)return;
    EditedMetadataFields fields;var dialog=EditedMetadataDialog(selected,out fields);if(!dialog.Show())return;var changes=fields.Values();var first=selected[0];
    await RunOperation(ct=>{foreach(var group in selected.GroupBy(i=>i.Project.Id))repo.SaveEditedMetadata(group.First().Project,group,changes,ct);return "Edited metadata saved.";},message=>{RefreshEdited(first.Project.Id,first.RelativePath);L("StatusLabel").Text=message;});
   }finally{ResumeMetadataPreviews();}
  }
  bool contextOnEditedFile;
  void InitializeEditedFileMenu(){
   var grid=G("EditedGrid");grid.ContextMenu=ThemedMenu();
   grid.PreviewMouseRightButtonDown+=(sender,args)=>{
    if(args.Handled)return;var row=ItemsControl.ContainerFromElement(grid,args.OriginalSource as DependencyObject) as DataGridRow;contextOnEditedFile=row!=null;
    if(row==null){grid.ContextMenu.IsOpen=false;args.Handled=true;return;}
    if(!row.IsSelected)grid.SelectedItem=row.Item;row.Focus();args.Handled=true;
   };
   grid.ContextMenuOpening+=(sender,args)=>{var selected=SelectedEditedImages();if((args.CursorLeft>=0&&!contextOnEditedFile)||repo==null||RepositoryOperationBlocked||SearchBlocked("EditedSearchBox")||selected.Count==0){args.Handled=true;return;}BuildEditedFileMenu(grid.ContextMenu,selected);};
   grid.PreviewKeyDown+=(sender,args)=>{if(args.Key==Key.Delete&&repo!=null&&!RepositoryOperationBlocked&&!SearchBlocked("EditedSearchBox")&&SelectedEditedImages().Count>0){args.Handled=true;DeleteEditedFiles(SelectedEditedImages());}};
  }
  void BuildEditedFileMenu(ContextMenu menu,List<EditedImage> selected){
   menu.Items.Clear();menu.Items.Add(new MenuItem{Header=selected.Count+" selected file"+(selected.Count==1?"":"s"),IsEnabled=false});menu.Items.Add(FileAction("Export files…",ExportEditedFiles,selected.Count>0));menu.Items.Add(FileAction("Export to…",ExportEditedTo,selected.Count>0));menu.Items.Add(FileAction("Edit metadata…",()=>EditEditedMetadata(selected),selected.Count>0));menu.Items.Add(FileAction("Preview image…",PreviewEditedImage,selected.Count==1));menu.Items.Add(FileAction("Image details…",ShowEditedDetails,selected.Count==1));menu.Items.Add(FileAction("Open image folder",OpenEditedFolder,selected.Count==1));
   menu.Items.Add(new Separator());var delete=FileAction("Delete files…",()=>DeleteEditedFiles(selected),selected.Count>0);delete.Foreground=new SolidColorBrush(Color.FromRgb(183,40,51));UiHelp.Tip(delete,"Delete selected Edited copies; keep source originals and archived captures.");menu.Items.Add(delete);
  }
  void ExportEditedFiles(){
   if(repo==null||RepositoryOperationBlocked||SearchBlocked("EditedSearchBox"))return;var selected=SelectedEditedImages();if(selected.Count==0)return;
   var dialog=new FormWindow(Window,"Export Edited files",610,480);dialog.Text(selected.Count+" selected file"+(selected.Count==1?"":"s"),true);
   dialog.Text("Copies the selected images in their original format. Existing files are kept; duplicate names receive a numbered suffix.");
   var destination=ExportDestination(dialog,"AstroArchive_Edited_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
   dialog.Accept("Export files",()=>ValidExportDestination(dialog,destination));if(!dialog.Show())return;var options=destination.Options();
   Run(ct=>Exporter.CreateEdited(repo,selected,options,ct,Progress),path=>ExportComplete(path,false));
  }
  FormWindow EditedDeletionDialog(List<EditedImage> selected){
   var dialog=new FormWindow(Window,"Delete selected Edited files",640,520);dialog.Text("Delete "+selected.Count+" selected file"+(selected.Count==1?"":"s")+"?",true);dialog.Text(repo.Root);
   dialog.Text("This permanently removes the selected Edited copies. Source copies, archived originals, other Edited files and project source metadata stay. Cloud-synced deletions propagate to the cloud.");
   dialog.Add(new TextBox{Text=string.Join("\r\n",selected.Select(i=>i.Project.Name+" / "+i.RelativePath)),IsReadOnly=true,Height=170,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});dialog.Accept("Delete selected files",()=>true,true);return dialog;
  }
  void DeleteEditedFiles(List<EditedImage> selected){
   if(repo==null||RepositoryOperationBlocked||SearchBlocked("EditedSearchBox")||selected.Count==0)return;if(!EditedDeletionDialog(selected).Show())return;CancelEditedPreview();
   Run(ct=>{var result=repo.DeleteEditedImages(selected,ct,Progress);return result.Deleted+" selected Edited files deleted."+(result.Errors.Count==0?"":"\r\n\r\n"+string.Join("\r\n",result.Errors));},message=>{L("StatusLabel").Text=message.Split('\n')[0];if(message.Contains("\n"))ShowReport("Edited file deletion report",message);},"Deleting Edited files");
  }
 }
}
