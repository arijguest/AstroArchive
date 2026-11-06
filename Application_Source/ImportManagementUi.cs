using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace AstroArchive {
 public partial class MainUi {
  bool importContextOnRow;
  List<Frame> SelectedImportRows(){var rows=G("ImportGrid").SelectedItems.Cast<Frame>().Where(f=>f.Status!="Deleted").ToList();return plan==null?new List<Frame>():rows.Select(f=>plan.Frames.FirstOrDefault(p=>ReferenceEquals(p,f)||string.Equals(p.SourcePath,f.SourcePath,StringComparison.OrdinalIgnoreCase))??f).Distinct().ToList();}
  void SelectImportContextRow(Frame frame){if(frame==null)return;var grid=G("ImportGrid");if(!grid.SelectedItems.Contains(frame)){grid.SelectedItems.Clear();grid.SelectedItems.Add(frame);}}
  void InitializeImportManagement(){
   var grid=G("ImportGrid");grid.ContextMenu=ThemedMenu();
   grid.PreviewMouseRightButtonDown+=(s,e)=>{var row=ItemsControl.ContainerFromElement(grid,e.OriginalSource as DependencyObject) as DataGridRow;importContextOnRow=row!=null;if(row==null){e.Handled=true;return;}SelectImportContextRow(row.Item as Frame);row.Focus();e.Handled=true;};
   grid.ContextMenuOpening+=(s,e)=>{if((e.CursorLeft>=0&&!importContextOnRow)||SelectedImportRows().Count==0){e.Handled=true;return;}BuildImportContextMenu(grid.ContextMenu);};
   grid.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Delete&&!RepositoryOperationBlocked&&!SearchBlocked("ImportSearchBox")&&SelectedImportRows().Count>0){e.Handled=true;RemoveSelectedImports();}};
  }
  void BuildImportContextMenu(ContextMenu menu){
   menu.Items.Clear();var selected=SelectedImportRows();menu.Items.Add(new MenuItem{Header=selected.Count+" selected imports",IsEnabled=false});
   var source=BuildImportTools();foreach(var item in source.Items.Cast<object>().ToList()){source.Items.Remove(item);menu.Items.Add(item);}
  }
  void ReconcileImportRows(){
   if(plan==null){FilterImports();return;}var indexed=all.Where(f=>!string.IsNullOrEmpty(f.Hash)).GroupBy(f=>f.Hash).ToDictionary(g=>g.Key,g=>g.First());
   for(int i=0;i<plan.Frames.Count;i++){var row=plan.Frames[i];if(Repository.PendingMetadata(row,true)||row.Status=="Deleted")continue;Frame current;
    if(!string.IsNullOrEmpty(row.Hash)&&indexed.TryGetValue(row.Hash,out current)){var updated=current.Clone();updated.SourcePath=row.SourcePath;updated.SourceRoot=row.SourceRoot;updated.SourceStamp=row.SourceStamp;updated.Status=row.Status;plan.Frames[i]=updated;}
    else if(!string.IsNullOrEmpty(row.Hash)){var updated=row.Clone();updated.Status="Deleted";updated.SourceDisposition="Archived copy deleted; source retained. Review deletion history before reimporting.";plan.Frames[i]=updated;}
   }FilterImports();
  }
  void RemoveSelectedImports(){
   if(RepositoryOperationBlocked||plan==null||SearchBlocked("ImportSearchBox"))return;var selected=SelectedImportRows();if(selected.Count==0)return;
   var dialog=new FormWindow(Window,"Remove selected imports",620,460);dialog.Text("Remove "+selected.Count+" selected import"+(selected.Count==1?"":"s")+" from this list?",true);
   dialog.Text("Source files and archived copies are retained. Removed rows will not be included in Import ready files or Stack all imports. Scanning the source folder again restores eligible rows.");
   dialog.Add(new TextBox{Text=string.Join("\r\n",selected.Select(f=>f.OriginalName)),IsReadOnly=true,Height=160,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});dialog.Accept("Remove from list",()=>true);if(!dialog.Show())return;
   var remove=new HashSet<Frame>(selected);plan.Frames.RemoveAll(remove.Contains);FilterImports();L("StatusLabel").Text=selected.Count+" imports removed from the list. Source files retained.";
  }
  void DeleteSelectedImportCopies(){
   if(repo==null||RepositoryOperationBlocked)return;var hashes=new HashSet<string>(SelectedImportRows().Where(f=>!Repository.PendingMetadata(f,true)).Select(f=>f.Hash));var selected=all.Where(f=>hashes.Contains(f.Hash)).Select(f=>f.Clone()).ToList();if(selected.Count>0)DeleteFiles(selected);
  }
 }
}
