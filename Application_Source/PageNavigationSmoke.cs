using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AstroArchive {
 public partial class MainUi {
  void SmokePageNavigation(string output){
   var tabs=(TabControl)Window.FindName("MainTabs");var selector=C("PageSelector");var header=(FrameworkElement)Window.FindName("HeaderBar");
   int page=tabs.SelectedIndex;double originalWidth=Window.Width;int textScale=settings.TextScalePercent;string theme=settings.ThemeMode;bool contrast=settings.HighContrast;
   string search=T("SearchBox").Text,source=T("SourceBox").Text;var importPlan=plan;var activeCancel=cancel;string filters=Util.Serialize(libraryFilters.Values);
   var selected=G("FramesGrid").SelectedItems.Cast<object>().ToArray();
   var repositoryRows=all;
   try{
    all=new System.Collections.Generic.List<Frame>();SelectInitialPage();if(tabs.SelectedIndex!=1||selector.SelectedIndex!=3)throw new Exception("Empty repository does not start on Import.");
    all.Add(new Frame{Target="M45"});SelectInitialPage();if(tabs.SelectedIndex!=0||selector.SelectedIndex!=0)throw new Exception("Populated repository does not start on Repository.");all=repositoryRows;
    var choices=selector.Items.Cast<ComboBoxItem>().ToArray();
    if(!choices.Select(item=>Convert.ToString(item.Content)).SequenceEqual(new[]{"Repository","Edited","Mosaic","Import"})||!choices.Select(item=>Convert.ToInt32(item.Tag)).SequenceEqual(new[]{0,3,2,1}))throw new Exception("Page choices do not match their existing workspaces.");
    foreach(var choice in choices){
     selector.SelectedItem=choice;PumpPopupLayout();int index=Convert.ToInt32(choice.Tag);
     if(tabs.SelectedIndex!=index||!ReferenceEquals(tabs.SelectedContent,((TabItem)tabs.Items[index]).Content))throw new Exception("Page dropdown replaced or opened the wrong workspace.");
     GoToPage((index+1)%4);if(Convert.ToInt32(((ComboBoxItem)selector.SelectedItem).Tag)!=tabs.SelectedIndex)throw new Exception("Programmatic navigation did not synchronize the dropdown.");
    }
    selector.SelectedIndex=3;CyclePage(1);if(selector.SelectedIndex!=0||tabs.SelectedIndex!=0)throw new Exception("Ctrl+Tab does not wrap forward.");CyclePage(-1);if(selector.SelectedIndex!=3||tabs.SelectedIndex!=1)throw new Exception("Ctrl+Shift+Tab does not wrap backward.");
    GoToPage(0);PumpPopupLayout();
    if(T("SearchBox").Text!=search||T("SourceBox").Text!=source||plan!=importPlan||cancel!=activeCancel||Util.Serialize(libraryFilters.Values)!=filters||!G("FramesGrid").SelectedItems.Cast<object>().SequenceEqual(selected))throw new Exception("Page switching reset search, selection, filters or import state.");
    if(PopupChildren<TabPanel>(tabs).Any())throw new Exception("Hidden page navigation still reserves a tab row.");
    foreach(int scale in new[]{100,150})foreach(double width in new[]{1060.0,1380.0}){
     settings.TextScalePercent=scale;Window.Width=width;settings.HighContrast=false;
     foreach(string mode in new[]{"Light","Dark"}){
      settings.ThemeMode=mode;ApplyAppearance();PumpPopupLayout();
      var bounds=PopupBounds(selector,Window);var settingsBounds=PopupBounds(TopMenu("SettingsMenu"),Window);
      if(bounds.Width<149||bounds.Right>settingsBounds.Left||bounds.Left<0||bounds.Right>Window.ActualWidth||bounds.Bottom>header.TranslatePoint(new Point(0,header.ActualHeight),Window).Y+1)throw new Exception("Page selector is clipped or no longer beside Settings.");
      Readable(selector.Foreground,selector.Background,mode+" page selector");
      double contentTop=tabs.TranslatePoint(new Point(),Window).Y,headerBottom=header.TranslatePoint(new Point(0,header.ActualHeight),Window).Y;
      if(Math.Abs(contentTop-headerBottom-6)>1)throw new Exception("Pages do not start six pixels below the toolbar.");
      foreach(var item in TopMenus())if(Math.Abs(PopupBounds(item,Window).Top-bounds.Top)>8)throw new Exception("Navigation wraps into a second row.");
      foreach(var tab in tabs.Items.Cast<TabItem>())if(((FrameworkElement)tab.Content).Margin.Top!=0)throw new Exception("A page retains its old selector spacing.");
      selector.IsDropDownOpen=true;PumpPopupLayout();if(!selector.IsDropDownOpen)throw new Exception("Page selector popup did not open.");
      foreach(var item in choices){item.ApplyTemplate();Readable(item.Foreground,(System.Windows.Media.Brush)Window.FindResource("Surface"),mode+" page choice");}
      if(scale==100&&width==1380)Capture(Path.Combine(output,"AstroArchive_Page_Selector_"+mode+".png"));selector.IsDropDownOpen=false;
     }
    }
    settings.HighContrast=true;ApplyAppearance();Readable(selector.Foreground,selector.Background,"High contrast page selector");
   }finally{all=repositoryRows;selector.IsDropDownOpen=false;Window.Width=originalWidth;settings.TextScalePercent=textScale;settings.ThemeMode=theme;settings.HighContrast=contrast;ApplyAppearance();GoToPage(page);PumpPopupLayout();}
   File.WriteAllText(Path.Combine(output,"page-navigation-smoke.txt"),"PASS: empty/populated repository startup, toolbar page selection, programmatic/keyboard cycling, existing workspace/state retention, no tab row, six-pixel gaps, both themes at 100/150% and 1060/1380 widths, high contrast and popup text contrast.");
  }
 }
}
