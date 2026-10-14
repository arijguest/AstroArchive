using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
namespace AstroArchive {
 // After a process removes or moves the selected rows (delete, move, merge, re-identify), the window stays on the same page and target
 // and selects the row that followed them, or the one before when they were last, instead of dropping back to All Targets.
 public partial class MainUi {
  sealed class ViewHint {
   public int Page;public string Target;public bool Armed;public DateTime At;
   public HashSet<string> Selected=new HashSet<string>();public List<string> Next=new List<string>();
  }
  ViewHint viewHint;
  static string HintKey(object item){
   var frame=item as Frame;if(frame!=null)return string.IsNullOrEmpty(frame.Hash)?"p:"+frame.RelativePath:"h:"+frame.Hash;
   var image=item as EditedImage;return image==null||image.Project==null?null:"e:"+image.Project.Id+"/"+image.RelativePath;
  }
  void CaptureViewHint(){
   viewHint=null;if(repo==null)return;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;if(page!=0&&page!=2)return;
   var grid=G(page==0?"FramesGrid":"EditedGrid");var items=grid.Items.Cast<object>().Where(o=>HintKey(o)!=null).ToList();
   var chosen=page==0?librarySelection.Items.Cast<object>():editedSelection.Items.Cast<object>();
   var selected=new HashSet<string>(chosen.Select(HintKey).Where(k=>k!=null));
   int first=items.FindIndex(o=>selected.Contains(HintKey(o))),last=items.FindLastIndex(o=>selected.Contains(HintKey(o)));
   var target=(page==0?Targets:EditedTargets).SelectedItem as TargetSummary;
   // The target is kept for every process, even with nothing selected; the next row is only chosen when rows were selected.
   var hint=new ViewHint{Page=page,Target=target==null?null:target.Name,Selected=selected};if(first<0){viewHint=hint;return;}
   hint.Next.AddRange(items.Skip(last+1).Take(200).Select(HintKey).Where(k=>!selected.Contains(k)));
   hint.Next.AddRange(items.Take(first).Reverse().Take(200).Select(HintKey).Where(k=>!selected.Contains(k)));
   viewHint=hint;
  }
  // Called once the process has finished, just before the lists are rebuilt.
  void ArmViewHint(){if(viewHint!=null){viewHint.Armed=true;viewHint.At=DateTime.UtcNow;}}
  // While armed, a rebuilt target list keeps the target the user was in.
  string HintTarget(int page,string current){var hint=viewHint;return hint!=null&&hint.Armed&&hint.Page==page&&hint.Target!=null?hint.Target:current;}
  void ApplyViewHint(int page){
   var hint=viewHint;if(hint==null||!hint.Armed||hint.Page!=page)return;
   if((DateTime.UtcNow-hint.At).TotalSeconds>30){viewHint=null;return;}
   string name=page==0?"FramesGrid":"EditedGrid";var grid=G(name);var items=grid.Items.Cast<object>().ToList();
   // Rows still listed means the lists have not been rebuilt yet; wait for the next rebuild.
   if(items.Any(o=>{string key=HintKey(o);return key!=null&&hint.Selected.Contains(key);}))return;
   viewHint=null;var index=new Dictionary<string,object>();foreach(var item in items){string key=HintKey(item);if(key!=null&&!index.ContainsKey(key))index[key]=item;}
   object next=null;foreach(string key in hint.Next)if(index.TryGetValue(key,out next))break;else next=null;
   if(next==null)return;
   if(page==0){var frame=(Frame)next;var session=subframeSessions.FirstOrDefault(g=>g.Frames.Contains(frame));if(session!=null)SelectSession(session,ModifierKeys.None);else{SelectOnlyTargetRow(name,frame);grid.ScrollIntoView(frame);}}
   else{SelectOnlyTargetRow(name,next);grid.ScrollIntoView(next);}
  }
 }
}
