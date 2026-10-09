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
  List<EditedImage> SelectedEditedImages(){return G("EditedGrid").SelectedItems.Cast<EditedImage>().ToList();}
  FormWindow EditedMetadataDialog(List<EditedImage> selected,out EditedMetadataFields fields){
   var dialog=new FormWindow(Window,"Edit metadata",610,730);dialog.Text(selected.Count+" selected edited image"+(selected.Count==1?"":"s"),true);dialog.Text("Empty fields keep existing values. Assignments are saved with Edited; image files remain unchanged.");
   Func<Func<EditedMetadata,string>,string> shared=get=>{var values=selected.Select(i=>get(i.Metadata)??"").Distinct().ToList();return values.Count==1?values[0]:"";};fields=new EditedMetadataFields();
   fields.Object=dialog.Input("Target (object ID or common name)",shared(m=>m.Object));fields.Filters=dialog.Input("Filters / channels",shared(m=>m.Filters));
   fields.ImageClass=dialog.Select("Image class",new[]{"Keep existing","Edited image","Starless","Stars only","GIF","Meteor","Unknown (conflicting labels)"},selected.Select(i=>i.Metadata.ImageClass).Distinct().Count()==1?selected[0].Metadata.ImageClass:"Keep existing");
   fields.TotalExposure=dialog.Input("Total exposure seconds (optional)",shared(m=>m.TotalExposure.HasValue?m.TotalExposure.Value.ToString("G",CultureInfo.InvariantCulture):""));fields.Subs=dialog.Input("Sub count (optional)",shared(m=>m.Subs.HasValue?m.Subs.Value.ToString(CultureInfo.InvariantCulture):""));fields.SubExposure=dialog.Input("Exposure per sub in seconds (optional)",shared(m=>m.SubExposure.HasValue?m.SubExposure.Value.ToString("G",CultureInfo.InvariantCulture):""));
   fields.RA=dialog.Input("RA (as recorded, optional)",shared(m=>m.RA));fields.Dec=dialog.Input("Dec (as recorded, optional)",shared(m=>m.Dec));fields.Remember();var controls=fields;
   dialog.Accept("Save metadata",()=>{try{controls.Values();return true;}catch(ArgumentException e){MessageBox.Show(dialog.Window,e.Message);return false;}});return dialog;
  }
  void EditEditedMetadata(){
   if(repo==null||RepositoryOperationBlocked||SearchBlocked("EditedSearchBox"))return;var selected=SelectedEditedImages();if(selected.Count==0)return;EditedMetadataFields fields;var dialog=EditedMetadataDialog(selected,out fields);if(!dialog.Show())return;var changes=fields.Values();var first=selected[0];
   Run(ct=>{foreach(var group in selected.GroupBy(i=>i.Project.Id))repo.SaveEditedMetadata(group.First().Project,group,changes,ct);return "Edited metadata saved.";},message=>{RefreshEdited(first.Project.Id,first.RelativePath);L("StatusLabel").Text=message;});
  }
  void InitializeEditedFileMenu(){
   var grid=G("EditedGrid");grid.PreviewMouseRightButtonDown+=(sender,args)=>{DependencyObject node=args.OriginalSource as DependencyObject;while(node!=null&&!(node is DataGridRow))node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);var row=node as DataGridRow;if(row!=null&&!row.IsSelected)grid.SelectedItem=row.Item;};
   grid.ContextMenu=ThemedMenu();grid.ContextMenuOpening+=(sender,args)=>{var menu=grid.ContextMenu;menu.Items.Clear();menu.Items.Add(FileAction("Export to…",ExportEditedTo,SelectedEditedImages().Count>0));menu.Items.Add(FileAction("Edit metadata…",EditEditedMetadata,SelectedEditedImages().Count>0));menu.Items.Add(FileAction("Preview image…",PreviewEditedImage,SelectedEditedImages().Count==1));menu.Items.Add(FileAction("Image details…",ShowEditedDetails,ActiveEditedImage!=null));menu.Items.Add(FileAction("Open image folder",OpenEditedFolder,ActiveEditedImage!=null));};
  }
 }
}
