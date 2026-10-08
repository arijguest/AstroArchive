using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace AstroArchive {
 public partial class MainUi {
  void VerifyTargetListAppearance(ListBox list,string context){
   list.ApplyTemplate();var surface=list.Template.FindName("TargetSurface",list) as Border;var scroll=list.Template.FindName("PART_ScrollViewer",list) as ScrollViewer;
   if(surface==null||!object.Equals(surface.Background,list.Background)||surface.Opacity!=1||scroll==null||!scroll.CanContentScroll||scroll.HorizontalScrollBarVisibility!=ScrollBarVisibility.Disabled)throw new Exception(context+" changed its target surface or scrolling layout.");
   var allTargets=list.Items.Cast<TargetSummary>().Single(t=>t.Name=="All targets");list.ScrollIntoView(allTargets);PumpPopupLayout();
   var row=list.ItemContainerGenerator.ContainerFromItem(allTargets) as ListBoxItem;var label=row==null?null:PopupChildren<TextBlock>(row).FirstOrDefault(t=>t.Text=="All targets");
   if(label==null||label.ActualWidth<=0||label.ActualHeight<=0||row.ActualWidth>list.ActualWidth+1)throw new Exception(context+" hid target entries or expanded them outside the panel.");
   var background=Window.TryFindResource("Surface") as System.Windows.Media.Brush;Readable(label.Foreground,background,context+" target label");
  }
  void SmokeTargetNavigation(string output){
   var previousRows=all;string previousSearch=T("SearchBox").Text;var previousFilters=libraryFilters.Values.ToList();string previousTarget=(Targets.SelectedItem as TargetSummary).Name;
   try{
    libraryFilters.Values.Clear();T("SearchBox").Text="";WaitForSearches();
    all=new System.Collections.Generic.List<Frame>{
     new Frame{Target="M42",Kind="Light",Exposure=60,OriginalName="orion.fit"},new Frame{Target="M42",Kind="Stack",OriginalName="orion-stack.fit"},new Frame{Target="NGC6888",Kind="Light",Exposure=120,OriginalName="crescent.fit"},
     new Frame{Target="M31",Kind="Light",Exposure=60,OriginalName="andromeda.fit"},new Frame{Target="M45",Kind="Light",Exposure=60,OriginalName="pleiades.fit"},new Frame{Target="Moon",Kind="Stack",OriginalName="moon.fit"},
     new Frame{Target="C/2023 A3 (Tsuchinshan-ATLAS)",Kind="Light",Exposure=30,OriginalName="comet.fit"},new Frame{Target="12P/Pons-Brooks",Kind="Stack",OriginalName="comet-stack.fit"},new Frame{Target="Unknown",Kind="Light",OriginalName="unknown.fit"}
    };Filter(true);PumpPopupLayout();
    var view=Targets.ItemsSource as ListCollectionView;if(view==null||view.GroupDescriptions.Count!=1||view.Groups==null)throw new Exception("Target type grouping is missing.");
    if(!view.Groups.Cast<CollectionViewGroup>().Any(g=>Convert.ToString(g.Name)=="Comets"&&g.ItemCount==2)||!view.Groups.Cast<CollectionViewGroup>().Any(g=>Convert.ToString(g.Name)=="Nebulae"&&g.ItemCount==2))throw new Exception("Comets or nebulae do not share a section.");
    if(Targets.Items.Cast<TargetSummary>().Count()!=9||Targets.Items.Cast<TargetSummary>().First().Name!="All targets"||PopupChildren<Expander>(Targets).Any())throw new Exception("Target sections require expanding or include selectable header rows.");
    Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M42");if(displayed.Count!=2)throw new Exception("Grouped target selection did not filter files.");
    T("SearchBox").Text="Orion";WaitForSearches();if((Targets.SelectedItem as TargetSummary).Name!="M42"||displayed.Count!=2)throw new Exception("Rebuilding type groups lost target selection.");
    T("SearchBox").Text="C/2023";WaitForSearches();if((Targets.SelectedItem as TargetSummary).Name!="All targets"||displayed.Count!=1)throw new Exception("Missing target did not return to filtered All targets.");
    T("SearchBox").Text="";WaitForSearches();PumpPopupLayout();
    foreach(string theme in new[]{"Dark","Light"}){Targets.ScrollIntoView(Targets.Items[0]);Theme.Apply(Window,theme);PumpPopupLayout();
     if(!PopupChildren<TextBlock>(Targets).Any(t=>t.Text=="Comets")||!PopupChildren<TextBlock>(Targets).Any(t=>t.Text=="Nebulae"))throw new Exception("Inline type headings did not render.");
     var target=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M42");Targets.ScrollIntoView(target);PumpPopupLayout();var container=Targets.ItemContainerGenerator.ContainerFromItem(target) as ListBoxItem;
     if(container==null||!Convert.ToString(container.ToolTip).Contains("2 files")||!PopupChildren<TextBlock>(container).Any(t=>t.Text=="Orion Nebula"&&t.TextWrapping==TextWrapping.Wrap))throw new Exception("Concise row or full-detail tooltip did not render.");
     Capture(System.IO.Path.Combine(output,"AstroArchive_Targets_"+theme+".png"));
     var source=Targets.ItemsSource;var selection=Targets.SelectedItem;Targets.IsEnabled=false;
     try{VerifyTargetListAppearance(Targets,theme+" pending targets");if(Targets.ItemsSource!=source||Targets.SelectedItem!=selection)throw new Exception("Pending targets lost entries or selection.");Capture(System.IO.Path.Combine(output,"AstroArchive_Targets_Pending_"+theme+".png"));}
     finally{Targets.IsEnabled=true;}VerifyTargetListAppearance(Targets,theme+" restored targets");
    }
    var previousEdited=editedImages;string editedSearch=T("EditedSearchBox").Text;object editedClass=C("EditedClassFilter").SelectedItem;string editedTarget=(EditedTargets.SelectedItem as TargetSummary).Name;
    try{
     var project=new EditedProject{Id=Guid.NewGuid().ToString("N"),Name="Comet navigation fixture"};
     editedImages=new System.Collections.Generic.List<EditedImage>{
      new EditedImage{Project=project,Filename="comet.png",RelativePath="comet.png",Metadata=new EditedMetadata{Object="C/2023 A3 (Tsuchinshan-ATLAS)",ImageClass="Starless"}},
      new EditedImage{Project=project,Filename="comet-stack.png",RelativePath="comet-stack.png",Metadata=new EditedMetadata{Object="12P/Pons-Brooks",ImageClass="Starless"}},
      new EditedImage{Project=project,Filename="nebula.png",RelativePath="nebula.png",Metadata=new EditedMetadata{Object="M42",ImageClass="Starless"}}
     };C("EditedClassFilter").SelectedItem="All images";T("EditedSearchBox").Text="";FilterEditedImages();WaitForSearches();
     var editedView=EditedTargets.ItemsSource as ListCollectionView;if(editedView==null||!editedView.Groups.Cast<CollectionViewGroup>().Any(g=>Convert.ToString(g.Name)=="Comets"&&g.ItemCount==2))throw new Exception("Edited comet section is missing");
     EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="12P/Pons-Brooks");WaitForSearches();if(G("EditedGrid").Items.Count!=1)throw new Exception("Edited comet selection did not filter images");
    }finally{editedImages=previousEdited;C("EditedClassFilter").SelectedItem=editedClass;T("EditedSearchBox").Text=editedSearch;FilterEditedImages();WaitForSearches();EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().FirstOrDefault(t=>t.Name==editedTarget)??EditedTargets.Items.Cast<TargetSummary>().First();}
   }finally{all=previousRows;libraryFilters.Values.Clear();foreach(var filter in previousFilters)libraryFilters.Values[filter.Key]=filter.Value;T("SearchBox").Text=previousSearch;WaitForSearches();Filter(true);Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().FirstOrDefault(t=>t.Name==previousTarget)??Targets.Items.Cast<TargetSummary>().First();Theme.Apply(Window,settings.ThemeMode);}
  }
 }
}
