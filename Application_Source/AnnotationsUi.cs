using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace AstroArchive {
 // Stars, tags and notes for archived captures (Repository) and working copies
 // (Edited). Nothing is added to the default view: starred rows are tinted and
 // marked, and everything else lives in menus, details and optional columns.
 public partial class MainUi {
  Dictionary<string,CaptureAnnotation> annotations=new Dictionary<string,CaptureAnnotation>(StringComparer.OrdinalIgnoreCase);string annotationsRoot;
  Dictionary<string,CaptureAnnotation> Annotations{get{
   if(repo==null)return new Dictionary<string,CaptureAnnotation>(StringComparer.OrdinalIgnoreCase);
   if(!string.Equals(annotationsRoot,repo.Root,StringComparison.OrdinalIgnoreCase)){
    annotationsRoot=repo.Root;
    try{annotations=CaptureAnnotations.Load(repo.Meta);}
    catch(Exception e){if(!(e is System.IO.IOException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException))throw;annotations=new Dictionary<string,CaptureAnnotation>(StringComparer.OrdinalIgnoreCase);L("StatusLabel").Text="Stars, tags and notes could not be read: "+e.Message;}
   }return annotations;
  }}
  void ApplyAnnotations(IEnumerable<IAnnotated> items){CaptureAnnotations.Apply(items,Annotations);}
  void InitializeAnnotations(){
   foreach(string name in new[]{"FramesGrid","EditedGrid"}){
    string grid=name;
    // S stars or unstars the selection, as in mail and photo apps.
    G(grid).PreviewKeyDown+=(s,e)=>{
     if(e.Key!=Key.S||Keyboard.Modifiers!=ModifierKeys.None||e.OriginalSource is TextBox||repo==null||RepositoryOperationBlocked)return;
     var items=AnnotationSelection(grid);if(items.Count==0)return;e.Handled=true;ToggleStars(items);
    };
   }
  }
  List<IAnnotated> AnnotationSelection(string grid){return grid=="EditedGrid"?SelectedEditedImages().Cast<IAnnotated>().ToList():SelectedFiles().Cast<IAnnotated>().ToList();}
  static string StarLabel(IList<IAnnotated> items){return items.Count>0&&items.All(i=>i.IsStarred)?"Unstar":"Star";}
  MenuItem StarAction(List<IAnnotated> items){var item=FileAction(StarLabel(items),()=>ToggleStars(items),items.Count>0&&repo!=null);item.InputGestureText="S";UiHelp.Tip(item,"Starred files are tinted and marked with ★. Search “starred” or use Filters to find them.");return item;}
  MenuItem NotesAction(List<IAnnotated> items){return FileAction(items.Count==1?"Tags and note…":"Tags and notes…",()=>EditAnnotations(items),items.Count>0&&repo!=null);}
  bool SaveAnnotations(IEnumerable<IAnnotated> items,AnnotationEdit edit,out int changed){
   changed=0;var store=Annotations;var before=store.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);var list=items.ToList();
   try{changed=CaptureAnnotations.Edit(store,list,edit);if(changed>0)CaptureAnnotations.Save(repo.Meta,store);}
   catch(Exception e){
    if(!(e is System.IO.IOException||e is UnauthorizedAccessException))throw;
    // Keep the screen honest: restore what is actually saved.
    annotations=before;CaptureAnnotations.Apply(list,annotations);LocalizedMessage.Show(Window,"Stars, tags and notes could not be saved: "+e.Message,"AstroArchive",MessageBoxButton.OK,MessageBoxImage.Warning);return false;
   }
   if(changed>0)AnnotationsChanged(list);return true;
  }
  // Frames shown in several places (rows, previews, sessions) are distinct objects; sync them all.
  void AnnotationsChanged(List<IAnnotated> changed){
   var keys=new HashSet<string>(changed.Select(i=>i.AnnotationKey).Where(k=>k!=null),StringComparer.OrdinalIgnoreCase);
   ApplyAnnotations(all.Where(f=>f.Hash!=null&&keys.Contains(f.Hash)));ApplyAnnotations(editedImages.Where(i=>i.AnnotationKey!=null&&keys.Contains(i.AnnotationKey)));
   if(previewFrame!=null&&previewFrame.Hash!=null&&keys.Contains(previewFrame.Hash))CaptureAnnotations.Apply(previewFrame,Annotations);
   var view=G("FramesGrid").ItemsSource as ICollectionView;if(view!=null&&view.Groups!=null)foreach(var session in SessionGroups(view.Groups))session.StarsChanged();
   // Search and filters may now include or exclude these rows.
   if(T("SearchBox").Text.Trim().Length>0||libraryFilters.ActiveCount>0)Filter(false);
   if(T("EditedSearchBox").Text.Trim().Length>0)FilterEditedImages(false);
   Details();
  }
  static IEnumerable<SubframeSession> SessionGroups(IEnumerable<object> groups){
   foreach(var group in groups.OfType<CollectionViewGroup>()){var session=group.Name as SubframeSession;if(session!=null)yield return session;if(!group.IsBottomLevel)foreach(var nested in SessionGroups(group.Items.Cast<object>()))yield return nested;}
  }
  void ToggleStars(List<IAnnotated> items){
   if(repo==null||items.Count==0)return;bool star=!items.All(i=>i.IsStarred);int changed;
   if(SaveAnnotations(items,new AnnotationEdit{Starred=star},out changed))L("StatusLabel").Text=(star?"Starred ":"Unstarred ")+items.Count.ToString("N0")+" file"+(items.Count==1?"":"s")+".";
  }
  void EditAnnotations(List<IAnnotated> items){
   if(repo==null||items.Count==0)return;
   var common=CaptureAnnotations.CommonTags(items);var notes=items.Select(i=>i.UserNote??"").Distinct().ToList();bool mixedNotes=notes.Count>1;
   var d=new FormWindow(Window,items.Count==1?"Tags and note":"Tags and notes",620,620);
   d.Text(items.Count==1?"Tags and note":items.Count.ToString("N0")+" selected files",true);
   d.Text("Saved with this repository; the files themselves never change.");
   int starred=items.Count(i=>i.IsStarred);
   var star=new CheckBox{Content=new TextBlock{Text="Starred",TextWrapping=TextWrapping.Wrap},IsThreeState=starred>0&&starred<items.Count,IsChecked=starred==items.Count?true:starred==0?(bool?)false:null,Margin=new Thickness(0,4,0,3)};AutomationProperties.SetName(star,"Starred");
   if(star.IsThreeState)star.Checked+=(s,e)=>star.IsThreeState=false;d.Add(star);
   if(starred>0&&starred<items.Count){var mixed=new TextBlock{Text=starred.ToString("N0")+" of "+items.Count.ToString("N0")+" are starred; leave the box mixed to keep each.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24,0,0,4)};mixed.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(mixed,TextBlock.ForegroundProperty,"Muted");d.Add(mixed);}
   var tags=d.Input("Tags (separate with commas)",string.Join(", ",common));
   if(items.Count>1&&items.Any(i=>i.Tags!=null&&i.Tags.Count>common.Count)){var other=new TextBlock{Text="Only tags shared by every selected file are shown. Others stay unless you remove them here.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)};other.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(other,TextBlock.ForegroundProperty,"Muted");d.Add(other);}
   var known=CaptureAnnotations.KnownTags(Annotations).Take(24).ToList();
   if(known.Count>0){
    var chips=new WrapPanel{Margin=new Thickness(0,2,0,6)};
    foreach(string tag in known){var name=tag;var chip=new Button{Content=name,Padding=new Thickness(8,2,8,2),Margin=new Thickness(0,0,6,6),MinHeight=0};chip.SetResourceReference(Control.FontSizeProperty,"UiFontSmall");AutomationProperties.SetName(chip,"Add tag "+name);UiHelp.Tip(chip,"Add this tag");
     chip.Click+=(s,e)=>{var current=CaptureAnnotations.Tags(tags.Text);if(!current.Any(t=>string.Equals(t,name,StringComparison.OrdinalIgnoreCase)))current.Add(name);tags.Text=string.Join(", ",current);tags.Focus();tags.CaretIndex=tags.Text.Length;};chips.Children.Add(chip);}
    d.Add(chips);
   }
   var note=d.Input(items.Count==1?"Note":"Note for every selected file",mixedNotes?"":notes[0]);note.AcceptsReturn=true;note.TextWrapping=TextWrapping.Wrap;note.MinHeight=96;note.MaxHeight=180;note.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;note.MaxLength=CaptureAnnotations.MaxNoteLength;
   if(mixedNotes){var hint=new TextBlock{Text="These files have different notes. Leave this blank to keep each note, or type to replace them all.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)};hint.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(hint,TextBlock.ForegroundProperty,"Muted");d.Add(hint);}
   AnnotationEdit edit=null;
   d.Accept("Save",()=>{
    edit=new AnnotationEdit{Starred=star.IsChecked};var entered=CaptureAnnotations.Tags(tags.Text);edit.AddTags.AddRange(entered);
    edit.RemoveTags.AddRange(common.Where(t=>!entered.Any(n=>string.Equals(n,t,StringComparison.OrdinalIgnoreCase))));
    string text=note.Text;if(!mixedNotes||text.Trim().Length>0)edit.Note=text;return true;
   });
   if(!d.Show()||edit==null)return;int changed;
   if(SaveAnnotations(items,edit,out changed))L("StatusLabel").Text=changed==0?"No changes to tags or notes.":"Tags and notes saved for "+changed.ToString("N0")+" file"+(changed==1?"":"s")+".";
  }
  // One read-only summary line per kind, for Capture details and Edited image details.
  static void AnnotationFacts(IList<IAnnotated> items,Action<string,string> fact){
   if(items.Count==0)return;int starred=items.Count(i=>i.IsStarred);
   if(starred>0)fact("Starred",items.Count==1||starred==items.Count?"★ Starred":"★ "+starred.ToString("N0")+" of "+items.Count.ToString("N0")+" starred");
   var tags=items.Where(i=>i.Tags!=null).SelectMany(i=>i.Tags).GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();if(tags.Count>0)fact("Tags",string.Join(", ",tags));
   var notes=items.Select(i=>i.UserNote).Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct().ToList();if(notes.Count==1)fact("Note",notes[0]);else if(notes.Count>1)fact("Notes",notes.Count+" different notes · open Tags and notes to review");
  }
 }
}
