using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void CheckToolbarRow(string searchName,string[] buttons){
   var search=PopupBounds(T(searchName),Window);double right=search.Right;
   if(search.Width<200)throw new Exception("Search field became too narrow: "+searchName);
   foreach(string name in buttons){
    var button=B(name);var bounds=PopupBounds(button,Window);
    if(Math.Abs(bounds.Top-search.Top)>1||Math.Abs(bounds.Bottom-search.Bottom)>1||bounds.Left<right+5||bounds.Right>Window.ActualWidth-18)
     throw new Exception("Toolbar buttons wrap, overlap or leave their row: "+name+" "+bounds+", search "+search);
    button.ApplyTemplate();var label=(FrameworkElement)button.Template.FindName("ButtonLabel",button);
    if(label==null||label.ActualWidth+1<label.DesiredSize.Width)throw new Exception("Toolbar button label is clipped: "+name);
    right=bounds.Right;
   }
  }
  void SmokeToolbarLayout(string output){
   double width=Window.Width,height=Window.Height;int scale=settings.TextScalePercent,page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string theme=settings.ThemeMode;bool preview=settings.ShowPreview;
   try{
    foreach(string mode in new[]{"Dark","Light"})foreach(int percent in new[]{100,150})foreach(double size in new[]{1060.0,1380.0}){
     settings.ThemeMode=mode;settings.TextScalePercent=percent;ApplyAppearance();Window.Width=size;Window.Height=850;
     GoToPage(0);
     foreach(bool show in new[]{true,false}){
      settings.ShowPreview=show;SetPreviewVisibility();PumpPopupLayout();
      CheckToolbarRow("SearchBox",new[]{"LibraryFiltersButton","ClearButton","RepositoryImportButton","ExportButton"});
      if(show&&percent==100&&size==1380)Capture(Path.Combine(output,"AstroArchive_Restored_Repository_Toolbar_"+mode+".png"));
     }
     settings.ShowPreview=preview;SetPreviewVisibility();GoToPage(2);PumpPopupLayout();
     CheckToolbarRow("EditedSearchBox",new[]{"EditedFiltersButton","EditedClearButton"});
     var action=PopupBounds(B("EditedEditorButton"),Window);var table=PopupBounds(G("EditedGrid"),Window);
     if(Math.Abs(action.Left-table.Left)>2||action.Bottom>table.Top+1)throw new Exception("Edited image actions are not aligned above their file list.");
     var add=PopupBounds(B("EditedAddButton"),Window);var import=PopupBounds(B("EditedImportFolderButton"),Window);var refresh=PopupBounds(B("EditedRefreshButton"),Window);
     if(Math.Abs(add.Top-import.Top)>1||Math.Abs(import.Top-refresh.Top)>1||add.Right+7>import.Left||import.Right+7>refresh.Left||refresh.Right>Window.ActualWidth-18)throw new Exception("Edited image buttons wrap, overlap or clip.");
     if(percent==100&&size==1380)Capture(Path.Combine(output,"AstroArchive_Restored_Edited_Toolbar_"+mode+".png"));
    }
    File.WriteAllText(Path.Combine(output,"toolbar-layout-smoke.txt"),"PASS compact single-row Repository and Edited buttons; complete labels; search remains usable; preview shown/hidden; image actions aligned; light/dark, 100/150% text and minimum/default window widths.");
   }finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;settings.ShowPreview=preview;ApplyAppearance();SetPreviewVisibility();Window.Width=width;Window.Height=height;GoToPage(page);PumpPopupLayout();}
  }
 }
}
