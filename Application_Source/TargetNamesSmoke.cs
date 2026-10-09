using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeTargetNames(string output){
   WaitForSearches();
   var previousRules=settings.TargetNames;var previousRows=all;var previousEdited=editedImages;var previousRepo=repo;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex,scale=settings.TextScalePercent;string theme=settings.ThemeMode;
   string title="Silver Sliver Galaxy with an extended observing name";
   try{
    repo=null;all=new List<Frame>{new Frame{Target="C23",Kind="Stack"},new Frame{Target="Personal Sliver View",Kind="Stack"}};
    TargetNameFields fields;var dialog=TargetNameDialog("C23",out fields);
    try{
     dialog.Window.Show();PumpPopupLayout();if(fields.Id.Text!="NGC891"||!fields.Id.IsReadOnly||fields.Common.Text!="Silver Sliver Galaxy")throw new Exception("Name editor lost the canonical ID or built-in common name");
     fields.Common.Text=title;fields.Aliases.Text="Personal Sliver View\nSliver Field";ApplyTargetNames(Catalog.ChangeNames(settings.TargetNames,fields.Values()),false);WaitForSearches();PumpPopupLayout();
     if(all.Count(f=>f.Target=="NGC891")!=2||Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="NGC891").Files!=2)throw new Exception("Saving a nickname did not regroup existing entries immediately");
     CapturePopup(dialog.Window,Path.Combine(output,"AstroArchive_Target_Names_Dialog.png"));
    }finally{dialog.Window.Close();}
    var project=new EditedProject{Id="name-smoke",Name="Name fixture"};editedImages=new List<EditedImage>{new EditedImage{Project=project,Filename="sliver.png",RelativePath="sliver.png",Metadata=new EditedMetadata{Object="C23",ImageClass="Edited image"}},new EditedImage{Project=project,Filename="personal.png",RelativePath="personal.png",Metadata=new EditedMetadata{Object="Personal Sliver View",ImageClass="Edited image"}}};FilterEditedImages();WaitForSearches();
    foreach(int index in new[]{0,2})foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
     settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();GoToPage(index);WaitForSearches();var list=index==0?Targets:EditedTargets;var target=list.Items.Cast<TargetSummary>().SingleOrDefault(t=>t.Name=="NGC891");if(target==null)throw new Exception("Target-name fixture disappeared on page "+index+" at "+mode+textScale+": "+string.Join(", ",list.Items.Cast<TargetSummary>().Select(t=>t.Name)));list.ScrollIntoView(target);PumpPopupLayout();var container=list.ItemContainerGenerator.ContainerFromItem(target) as ListBoxItem;
     if(list.ActualWidth<235||container==null||target.Files!=2)throw new Exception("Targets panel is still narrow or saved aliases split its entries");
     var name=PopupChildren<TextBlock>(container).SingleOrDefault(t=>t.Text=="NGC891 - "+title);var ids=PopupChildren<TextBlock>(container).SingleOrDefault(t=>t.Text==target.Subline);if(name==null||ids==null)throw new Exception("Target-name labels did not render on page "+index+" at "+mode+textScale+": "+string.Join(" | ",PopupChildren<TextBlock>(container).Select(t=>t.Text)));
     foreach(var text in new[]{name,ids}){var measured=new FormattedText(text.Text,CultureInfo.CurrentUICulture,text.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,text.Foreground,VisualTreeHelper.GetDpi(text).PixelsPerDip);measured.MaxTextWidth=text.ActualWidth;if(text.TextWrapping!=TextWrapping.Wrap||text.TextTrimming!=TextTrimming.None||measured.Height>text.ActualHeight+2||text.ActualWidth>container.ActualWidth)throw new Exception("Full target name or alternate IDs were clipped at "+mode+textScale);}
     if(!ids.Text.StartsWith("C23 · ")||!ids.Text.Contains("UGC1831")||ids.Text.Contains("NGC891")||list.ContextMenu==null||!TargetNamesAction("C23").IsEnabled)throw new Exception("Alternate catalogue IDs or name editor missing from a Targets page");
     Capture(Path.Combine(output,"AstroArchive_Wide_Targets_"+index+mode+textScale+".png"));
    }
    File.WriteAllText(Path.Combine(output,"target-names-smoke.txt"),"PASS saved nickname immediately regroups existing Repository/Edited entries; both Targets menus expose name editing; canonical ID protected; expanded panel width, full wrapped names and alternate catalogue IDs in light/dark at 100/150% text.");
   }finally{settings.TargetNames=previousRules;Catalog.ConfigureNames(previousRules);repo=previousRepo;all=previousRows;editedImages=previousEdited;settings.TextScalePercent=scale;settings.ThemeMode=theme;ApplyAppearance();Filter(true);FilterEditedImages();WaitForSearches();GoToPage(page);}
  }
 }
}
