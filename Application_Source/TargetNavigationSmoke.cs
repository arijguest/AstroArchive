using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  static Color TargetSurfacePixel(FrameworkElement root,ListBox list){
   root.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);
   var origin=list.TranslatePoint(new Point(2,10),root);var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect((int)origin.X,(int)origin.Y,1,1),pixel,4,0);return Color.FromArgb(pixel[3],pixel[2],pixel[1],pixel[0]);
  }
  void SmokeTargetListBackground(string output){
   var fixture=new Window{Owner=Window,Title="Target list refresh",Width=640,Height=360,ShowInTaskbar=false};fixture.Resources.MergedDictionaries.Add(Window.Resources);
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());fixture.Content=grid;
   var lists=new System.Collections.Generic.List<ListBox>();
   var targets=TargetNavigation.Build(Enumerable.Range(1,60).Select(i=>new Frame{Target="M"+i,Kind="Light",Exposure=60}));
   foreach(var prototype in new[]{Targets,EditedTargets}){
    var list=new ListBox{Style=prototype.Style,ItemTemplate=prototype.ItemTemplate,ItemContainerStyle=prototype.ItemContainerStyle,Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
    foreach(var group in prototype.GroupStyle)list.GroupStyle.Add(group);var view=new ListCollectionView(targets);view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));list.ItemsSource=view;
    ScrollViewer.SetCanContentScroll(list,true);ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);VirtualizingPanel.SetIsVirtualizingWhenGrouping(list,true);VirtualizingPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
    var host=new Border{Child=list,Margin=new Thickness(8)};Theme.Bind(host,Border.BackgroundProperty,"Surface");Grid.SetColumn(host,lists.Count);grid.Children.Add(host);lists.Add(list);
   }
   try{fixture.Show();foreach(string mode in new[]{"Dark","Light"}){
    Theme.Apply(Window,mode);PumpPopupLayout();Color expected=((SolidColorBrush)Window.FindResource("Surface")).Color;
    foreach(bool enabled in new[]{true,false,true}){
     foreach(var list in lists)list.IsEnabled=enabled;fixture.UpdateLayout();
     for(int i=0;i<lists.Count;i++)if(TargetSurfacePixel(grid,lists[i])!=expected)throw new Exception(mode+" target background changed while "+(enabled?"enabled":"disabled")+" on "+(i==0?"Repository":"Edited")+".");
    }
    foreach(var list in lists){var scroll=PopupChildren<ScrollViewer>(list).First();if(!scroll.CanContentScroll||!PopupChildren<VirtualizingStackPanel>(list).Any())throw new Exception("The target template lost virtualized scrolling.");list.ScrollIntoView(targets.Last());fixture.UpdateLayout();PumpPopupLayout();if(scroll.VerticalOffset<=0)throw new Exception("Grouped targets no longer scroll to the last item.");list.ScrollIntoView(targets.First());}
    CapturePopup(fixture,System.IO.Path.Combine(output,"AstroArchive_Target_Background_"+mode+".png"));
   }System.IO.File.WriteAllText(System.IO.Path.Combine(output,"target-background-smoke.txt"),"PASS: rendered Repository and Edited target surfaces retain light/dark colours during enabled/disabled/enabled refresh states; grouped lists retain virtualized scrolling.");
   }finally{fixture.Close();Theme.Apply(Window,settings.ThemeMode);}
  }
  void VerifyTargetListAppearance(ListBox list,string context){
   list.ApplyTemplate();var surface=list.Template.FindName("TargetSurface",list) as Border;var scroll=list.Template.FindName("PART_ScrollViewer",list) as ScrollViewer;
   if(surface==null||!object.Equals(surface.Background,list.Background)||surface.Opacity!=1||scroll==null||!scroll.CanContentScroll||scroll.HorizontalScrollBarVisibility!=ScrollBarVisibility.Disabled)throw new Exception(context+" changed its target surface or scrolling layout.");
   var allTargets=list.Items.Cast<TargetSummary>().Single(t=>t.Name=="All targets");list.ScrollIntoView(allTargets);PumpPopupLayout();
   var row=list.ItemContainerGenerator.ContainerFromItem(allTargets) as ListBoxItem;var label=row==null?null:PopupChildren<TextBlock>(row).FirstOrDefault(t=>t.Text=="All Targets");
   if(label==null||label.ActualWidth<=0||label.ActualHeight<=0||row.ActualWidth>list.ActualWidth+1)throw new Exception(context+" hid target entries or expanded them outside the panel.");
   var background=Window.TryFindResource("Surface") as System.Windows.Media.Brush;Readable(label.Foreground,background,context+" target label");
  }
  void SmokeTargetNavigation(string output){
   SmokeResponsiveTargetTitles();SmokeTargetKeyboard();
   var previousRows=all;string previousSearch=T("SearchBox").Text;var previousFilters=libraryFilters.Values.ToList();string previousTarget=(Targets.SelectedItem as TargetSummary).Name;
   try{
    libraryFilters.Values.Clear();T("SearchBox").Text="";WaitForSearches();
    all=new System.Collections.Generic.List<Frame>{
     new Frame{Target="M42",Kind="Light",Exposure=60,OriginalName="orion.fit"},new Frame{Target="M42",Kind="Stack",OriginalName="orion-stack.fit"},new Frame{Target="NGC6888",Kind="Light",Exposure=120,OriginalName="crescent.fit"},
     new Frame{Target="M31",Kind="Light",Exposure=60,OriginalName="andromeda.fit"},new Frame{Target="M45",Kind="Light",Exposure=60,OriginalName="pleiades.fit"},new Frame{Target="Moon",Kind="Stack",OriginalName="moon.fit"},
     new Frame{Target="C/2023 A3 (Tsuchinshan-ATLAS)",Kind="Light",Exposure=30,OriginalName="comet.fit"},new Frame{Target="12P/Pons-Brooks",Kind="Stack",OriginalName="comet-stack.fit"},new Frame{Target="Unknown",Kind="Light",OriginalName="unknown.fit"}
    };Filter(true);PumpPopupLayout();
    var view=Targets.ItemsSource as ListCollectionView;if(view==null||view.GroupDescriptions.Count!=1||view.Groups==null)throw new Exception("Target type grouping is missing.");
    if(!view.Groups.Cast<CollectionViewGroup>().Any(g=>Convert.ToString(g.Name)=="Solar system"&&g.ItemCount==3)||!view.Groups.Cast<CollectionViewGroup>().Any(g=>Convert.ToString(g.Name)=="Nebulae"&&g.ItemCount==2))throw new Exception("Comets or nebulae do not share a section.");
    if(Targets.Items.Cast<TargetSummary>().Count()!=9||Targets.Items.Cast<TargetSummary>().First().Name!="All targets"||PopupChildren<Expander>(Targets).Any())throw new Exception("Target sections require expanding or include selectable header rows.");
    Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M42");if(displayed.Count!=2)throw new Exception("Grouped target selection did not filter files.");
    T("SearchBox").Text="Orion";WaitForSearches();if((Targets.SelectedItem as TargetSummary).Name!="M42"||displayed.Count!=2)throw new Exception("Rebuilding type groups lost target selection.");
    T("SearchBox").Text="C/2023";WaitForSearches();if((Targets.SelectedItem as TargetSummary).Name!="All targets"||displayed.Count!=1)throw new Exception("Missing target did not return to filtered All targets.");
    T("SearchBox").Text="";WaitForSearches();PumpPopupLayout();
    foreach(string theme in new[]{"Dark","Light"}){Targets.ScrollIntoView(Targets.Items[0]);Theme.Apply(Window,theme);PumpPopupLayout();
     if(!PopupChildren<TextBlock>(Targets).Any(t=>t.Text=="Solar system")||!PopupChildren<TextBlock>(Targets).Any(t=>t.Text=="Nebulae"))throw new Exception("Inline type headings did not render.");
     var target=Targets.Items.Cast<TargetSummary>().Single(t=>t.Name=="M42");Targets.ScrollIntoView(target);PumpPopupLayout();var container=Targets.ItemContainerGenerator.ContainerFromItem(target) as ListBoxItem;
     if(container==null||!Convert.ToString(container.ToolTip).StartsWith("M42\n1 sub · 1 stack")||Convert.ToString(container.ToolTip).Contains("files")||!PopupChildren<TargetTitle>(container).Any(t=>t.FullTitle=="M42 - Orion Nebula"&&t.TextWrapping==TextWrapping.Wrap))throw new Exception("Concise row or ID/counts tooltip did not render.");
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
     var editedView=EditedTargets.ItemsSource as ListCollectionView;if(editedView==null||!editedView.Groups.Cast<CollectionViewGroup>().Any(g=>Convert.ToString(g.Name)=="Solar system"&&g.ItemCount==2))throw new Exception("Edited comet section is missing");
     EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().Single(t=>t.Name=="12P/Pons-Brooks");WaitForSearches();if(G("EditedGrid").Items.Count!=1)throw new Exception("Edited comet selection did not filter images");
    }finally{editedImages=previousEdited;C("EditedClassFilter").SelectedItem=editedClass;T("EditedSearchBox").Text=editedSearch;FilterEditedImages();WaitForSearches();EditedTargets.SelectedItem=EditedTargets.Items.Cast<TargetSummary>().FirstOrDefault(t=>t.Name==editedTarget)??EditedTargets.Items.Cast<TargetSummary>().First();}
   }finally{all=previousRows;libraryFilters.Values.Clear();foreach(var filter in previousFilters)libraryFilters.Values[filter.Key]=filter.Value;T("SearchBox").Text=previousSearch;WaitForSearches();Filter(true);Targets.SelectedItem=Targets.Items.Cast<TargetSummary>().FirstOrDefault(t=>t.Name==previousTarget)??Targets.Items.Cast<TargetSummary>().First();Theme.Apply(Window,settings.ThemeMode);}
  }
  void SmokeResponsiveTargetTitles(){
   var fixture=new Window{Owner=Window,Width=840,Height=460,ShowInTaskbar=false,FontFamily=Window.FontFamily};fixture.Resources.MergedDictionaries.Add(Window.Resources);
   var panel=new StackPanel{Orientation=Orientation.Horizontal};fixture.Content=panel;
   var lists=new System.Collections.Generic.List<ListBox>();
   foreach(var prototype in new[]{Targets,EditedTargets}){
    var list=new ListBox{Width=380,ItemTemplate=prototype.ItemTemplate,ItemContainerStyle=prototype.ItemContainerStyle};ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
    list.ItemsSource=new[]{"NGC6960","NGC6992"}.Select(name=>prototype==Targets?(TargetSummary)new TargetSummary{Name=name,Files=2,Subs=2,ExposureSeconds=120}:new EditedTargetSummary{Name=name,Files=2}).ToList();panel.Children.Add(list);lists.Add(list);
   }
   try{
    fixture.Show();foreach(int size in new[]{14,21}){
     fixture.FontSize=size;
     foreach(double width in new[]{380.0,180.0,380.0}){
      foreach(var list in lists)list.Width=width;PumpPopupLayout();
      foreach(var list in lists)foreach(var target in list.Items.Cast<TargetSummary>()){
       var row=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(target);var title=PopupChildren<TargetTitle>(row).Single();
       string expected=width==180?TargetNavigation.WithoutObjectType(target.DisplayName):target.DisplayName;
       if(title.Text!=expected)throw new Exception("Target title did not adapt at "+size+"px / "+width+": "+title.Text+" instead of "+expected);
       if(!Convert.ToString(row.ToolTip).StartsWith(TargetNavigation.TargetId(target.Name))||Convert.ToString(row.ToolTip).Contains("Nebula")||Convert.ToString(row.ToolTip).Contains("files"))throw new Exception("Target tooltip kept a full name, type or file count.");
      }
     }
    }
   }finally{fixture.Close();}
  }
  void SmokeTargetKeyboard(){
   WaitForSearches();var previousRows=all;var previousEdited=editedImages;var previousRepo=repo;var previousKeyboard=keyboardTargets;var captures=librarySelection.Items;var images=editedSelection.Items;var filters=libraryFilters.Values.ToList();string query=T("SearchBox").Text,editedQuery=T("EditedSearchBox").Text;object imageClass=C("EditedClassFilter").SelectedItem;int page=((TabControl)Window.FindName("MainTabs")).SelectedIndex;
   try{
    repo=null;libraryFilters.Values.Clear();T("SearchBox").Clear();T("EditedSearchBox").Clear();C("EditedClassFilter").SelectedItem="All images";WaitForSearches();
    foreach(bool background in new[]{false,true})foreach(int index in new[]{0,2}){
     int count=background?2103:3;string[] names={"M31","M42","M45"};
     all=Enumerable.Range(0,count).Select(i=>new Frame{Hash="target-key-"+i,Target=names[i%3],Kind="Light",Exposure=60,OriginalName=i+".fit"}).ToList();
     var project=new EditedProject{Id="target-keyboard",Name="Keyboard fixture"};editedImages=Enumerable.Range(0,count).Select(i=>new EditedImage{Project=project,Filename=i+".png",RelativePath=i+".png",Metadata=new EditedMetadata{Object=names[i%3],ImageClass="Edited image"}}).ToList();
     keyboardTargets=null;GoToPage(index);if(index==0)Filter(true);else FilterEditedImages();WaitForSearches();var list=index==0?Targets:EditedTargets;
     list.SelectedIndex=0;WaitForSearches();list.Focus();PumpPopupLayout();
     int selected=0;foreach(var key in new[]{System.Windows.Input.Key.Down,System.Windows.Input.Key.Down,System.Windows.Input.Key.Up}){
      selected+=key==System.Windows.Input.Key.Down?1:-1;string expected=((TargetSummary)list.Items[selected]).Name;var source=System.Windows.Input.Keyboard.FocusedElement as UIElement??list;
      source.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(source),0,key){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});WaitForSearches();PumpPopupLayout();
      if(((TargetSummary)list.SelectedItem).Name!=expected||!list.IsKeyboardFocusWithin)throw new Exception("Target arrow did not move selection/retain focus on page "+index+", background="+background);
      if(index==0?displayed.Count!=count/3||displayed.Any(f=>f.Target!=expected):G("EditedGrid").Items.Count!=count/3||G("EditedGrid").Items.Cast<EditedImage>().Any(i=>i.Metadata.Object!=expected))throw new Exception("Target arrow moved focus without filtering the view.");
     }
     // Clicking a search field ends target navigation; its arrows keep editing text.
     var search=T(index==0?"SearchBox":"EditedSearchBox");search.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseDownEvent});search.Focus();int before=list.SelectedIndex;
     search.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(search),0,System.Windows.Input.Key.Down){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});if(list.SelectedIndex!=before)throw new Exception("Target navigation hijacked a search-field arrow.");
    }
   }finally{
    keyboardTargets=null;repo=previousRepo;all=previousRows;editedImages=previousEdited;librarySelection.Clear();foreach(var frame in captures)librarySelection.Add(frame);editedSelection.Clear();foreach(var image in images)editedSelection.Add(image);libraryFilters.Values.Clear();foreach(var filter in filters)libraryFilters.Values[filter.Key]=filter.Value;T("SearchBox").Text=query;T("EditedSearchBox").Text=editedQuery;C("EditedClassFilter").SelectedItem=imageClass;WaitForSearches();Filter(true);FilterEditedImages();WaitForSearches();GoToPage(page);keyboardTargets=previousKeyboard;
   }
  }
 }
}
