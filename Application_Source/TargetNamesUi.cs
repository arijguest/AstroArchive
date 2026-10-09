using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace AstroArchive {
 public partial class MainUi {
  sealed class TargetNameFields {
   public TextBox Id,Common,Aliases;
   public TargetNameRule Values(){return new TargetNameRule{Id=Id.Text.Trim(),CommonName=Common.Text.Trim(),Aliases=Aliases.Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).ToList()};}
  }
  void InitializeTargetNames(){
   foreach(var list in new[]{Targets,EditedTargets}){
    var targetList=list;targetList.ContextMenu=ThemedMenu();
    targetList.PreviewMouseRightButtonDown+=(s,e)=>{var item=ItemsControl.ContainerFromElement(targetList,e.OriginalSource as DependencyObject) as ListBoxItem;if(item!=null)targetList.SelectedItem=item.DataContext;};
    targetList.ContextMenuOpening+=(s,e)=>{
     var target=targetList.SelectedItem as TargetSummary;if(target==null||target.Name=="All targets"||Catalog.IsAmbiguous(target.Name)){e.Handled=true;return;}
     var menu=targetList.ContextMenu;menu.Items.Clear();menu.Items.Add(TargetNamesAction(target.Name));
    };
   }
  }
  MenuItem TargetNamesAction(string target){return FileAction("Edit name and aliases…",()=>EditTargetNames(target),!RepositoryOperationBlocked&&!ActiveSearchBlocked);}
  FormWindow TargetNameDialog(string target,out TargetNameFields fields){
   string id=Catalog.ObjectId(target);var saved=(settings.TargetNames??new List<TargetNameRule>()).FirstOrDefault(r=>Catalog.KnownName(r.Id)==id);
   var dialog=new FormWindow(Window,"Target name and aliases",640,570);dialog.Text("Names for this object",true);dialog.Text("Save a name once to use it across Repository, Import and Edited. Catalogue IDs stay unchanged.");
   fields=new TargetNameFields();fields.Id=dialog.Input("Object ID",id);fields.Id.IsReadOnly=id.Length>0;
   fields.Common=dialog.Input("Common name",saved==null?Catalog.CommonName(target).Length>0?Catalog.CommonName(target):id.Length==0?target:"":saved.CommonName);
   fields.Aliases=dialog.Input("Other names (one per line)",saved==null?"":string.Join(Environment.NewLine,saved.Aliases??new List<string>()));fields.Aliases.AcceptsReturn=true;fields.Aliases.TextWrapping=TextWrapping.Wrap;fields.Aliases.MinHeight=100;fields.Aliases.MaxHeight=160;fields.Aliases.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
   if(id.Length>0)dialog.Text("Catalogue IDs: "+string.Join(" · ",Catalog.CatalogueIds(id)));
   var controls=fields;dialog.Button("Use built-in names",()=>{controls.Common.Text="";controls.Aliases.Text="";});
   dialog.Accept("Save names",()=>{try{Catalog.ChangeNames(settings.TargetNames,controls.Values());return true;}catch(ArgumentException error){MessageBox.Show(dialog.Window,error.Message,"Check target names",MessageBoxButton.OK,MessageBoxImage.Information);return false;}});return dialog;
  }
  void ApplyTargetNames(List<TargetNameRule> rules,bool persist=true){
   Catalog.ValidateNames(rules);var previous=settings.TargetNames;settings.TargetNames=rules;
   try{if(persist)SaveSettings();}catch{settings.TargetNames=previous;throw;}
   foreach(string search in new[]{"SearchBox","ImportSearchBox","EditedSearchBox"})CancelSearch(search);
   Catalog.ConfigureNames(rules);
   foreach(var frame in all.Concat(plan==null?Enumerable.Empty<Frame>():plan.Frames))frame.Target=frame.Target;
   foreach(var image in editedImages)image.Metadata.Object=Catalog.CanonicalTarget(image.Metadata.Object);
   Refresh();FilterImports();RefreshSkyPreviews();
  }
  void EditTargetNames(string target){
   if(RepositoryOperationBlocked||ActiveSearchBlocked)return;TargetNameFields fields;var dialog=TargetNameDialog(target,out fields);if(!dialog.Show())return;
   try{ApplyTargetNames(Catalog.ChangeNames(settings.TargetNames,fields.Values()));L("StatusLabel").Text="Target names saved.";}
   catch(Exception error){MessageBox.Show(Window,error.Message,"Names could not be saved",MessageBoxButton.OK,MessageBoxImage.Error);}
  }
 }
}
