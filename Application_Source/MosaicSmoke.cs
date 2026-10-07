// Native WPF checks use a disposable metadata-only repository.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace AstroArchive {
 public partial class MainUi {
  void SmokeMosaics(string output){
   var originalRepo=repo;var originalAll=all;int originalTab=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string originalTheme=settings.ThemeMode;string directory=Path.Combine(Path.GetTempPath(),"AstroArchive-mosaic-ui-"+Guid.NewGuid().ToString("N"));Repository temporary=null;
   try{
    temporary=new Repository(directory);repo=temporary;
    for(int i=0;i<4;i++){var frame=originalAll[i%originalAll.Count].Clone();frame.Hash=Util.HashText("native mosaic "+i);frame.OriginalName="Panel_"+(i%2+1)+"_capture_"+i+".fit";frame.RelativePath=Path.Combine("Targets",frame.OriginalName);frame.Kind="Light";frame.Mosaic=new MosaicHint{ProjectKey="native-mosaic",Name="Heart and Soul",PanelKey=(i%2+1).ToString(),Declared=true,ExpectedPanels=4,Evidence="Generated FITS mosaic metadata"};frame.Sky=MosaicGeometry.Approximate(40+(i%2)*1.5,24,2,3840,2160);repo.Save(frame);}
    repo.DetectMosaics(repo.All(),CancellationToken.None);var project=repo.Mosaics().Single();repo.AssignMosaic(project.Id,null,repo.All().Take(1),true);Refresh();SelectMosaic(project.Id);PumpPopupLayout();
    if(MosaicCollections.Items.Count!=1||MosaicPanels.Items.Count!=5||G("MosaicGrid").Items.Count!=4)throw new Exception("Mosaic collection/panel hierarchy did not render.");
    if(!PopupChildren<TextBlock>(MosaicCollections).Any(t=>t.Text.Contains("Heart and Soul")))throw new Exception("Mosaic collection name is not visible.");
    var row=G("MosaicGrid").Items.Cast<MosaicRow>().First(r=>r.Member.Role=="Input");G("MosaicGrid").SelectedItem=row;MosaicSelectionChanged();if(!B("MosaicConfirmButton").IsEnabled||!L("MosaicDetailsLabel").Text.Contains("Generated FITS"))throw new Exception("Mosaic selection evidence or confirmation state is missing.");
    foreach(string theme in new[]{"Dark","Light"}){Theme.Apply(Window,theme);PumpPopupLayout();Capture(Path.Combine(output,"AstroArchive_Mosaics_"+theme+".png"));}
    // Check the same object bindings used in assignment/export dropdowns.
    var form=new FormWindow(Window,"Mosaic chooser smoke",520,400);var choices=new ComboBox{ItemsSource=repo.Mosaics(),DisplayMemberPath="Name",SelectedIndex=0};form.Add(choices);form.Window.Show();PumpPopupLayout();if(!PopupChildren<TextBlock>(choices).Any(t=>t.Text=="Heart and Soul"))throw new Exception("Mosaic chooser displays a type name instead of its collection name.");form.Window.Close();
    MosaicPanels.SelectedItem=MosaicPanels.Items.Cast<MosaicPanelChoice>().First(p=>p.Kind=="Output");PumpPopupLayout();if(G("MosaicGrid").Items.Count!=1||G("MosaicGrid").Items.Cast<MosaicRow>().Single().Panel!="Completed output")throw new Exception("Completed mosaic output is mixed with input panels.");
    var filters=new CaptureFilters();filters.Values["Mosaic"]="Heart and Soul";if(filters.Apply(all,"").Count!=4)throw new Exception("Mosaic library filtering failed.");filters.Values["Panel"]="Completed output";if(filters.Apply(all,"").Count!=1)throw new Exception("Mosaic panel/role filtering failed.");
    File.WriteAllText(Path.Combine(output,"mosaic-smoke.txt"),"PASS: named mosaic collections, stable panel selection, metadata evidence, confirmation controls, collection chooser binding, footprint outlines, completed-output separation, light/dark rendering and library filters. No images were solved or decoded.");
   }finally{repo=originalRepo;all=originalAll;if(temporary!=null)temporary.Dispose();Refresh();((TabControl)Window.FindName("MainTabs")).SelectedIndex=originalTab;Theme.Apply(Window,originalTheme);if(Directory.Exists(directory))Directory.Delete(directory,true);}
  }
 }
}
