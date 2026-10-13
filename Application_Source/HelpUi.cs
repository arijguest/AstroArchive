using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
namespace AstroArchive {
 public partial class MainUi {
  HelpWindow helpWindow;
  void Guide(){
   var menu=new ContextMenu();BuildGuideItems(menu.Items);menu.PlacementTarget=TopMenu("GuideMenu");menu.IsOpen=true;
  }
  // Shared by the Guide menu and the legacy Guide button: help, guided fixes, the tour, reporting and About.
  void BuildGuideItems(ItemCollection items){
   items.Add(MenuAction("Help…",()=>OpenGuide(null),true,false));
   items.Add(MenuAction("Help for this page (F1)",()=>OpenGuide(CurrentHelpTopic()),true,false));
   var fixes=Branch("Guided fixes");foreach(var guide in WalkthroughCatalog.Fixes()){var chosen=guide;var item=MenuAction(Localization.T(guide.Title)+"…",()=>StartWalkthrough(chosen.Id),true,false);UiHelp.Hint(item,guide.Summary);fixes.Items.Add(item);}items.Add(fixes);
   var tours=Branch("Guided tours");foreach(var guide in WalkthroughCatalog.Tours()){var chosen=guide;var item=MenuAction(chosen.Id==WalkthroughCatalog.Tour?"Interactive walkthrough…":Localization.T(guide.Title)+"…",()=>StartWalkthrough(chosen.Id),true,false);UiHelp.Hint(item,guide.Summary);tours.Items.Add(item);}items.Add(tours);
   items.Add(new Separator());
   var report=MenuAction("Report a problem…",ReportProblem,true,false);UiHelp.Hint(report,"Open a GitHub bug report with your system details and recent log filled in.");items.Add(report);
   items.Add(new Separator());items.Add(MenuAction("About AstroArchive…",About,true,false));
  }
  string CurrentHelpTopic(){return ((TabControl)Window.FindName("MainTabs")).SelectedIndex==2?"EDITED IMAGES AND WORKING COPIES":((TabControl)Window.FindName("MainTabs")).SelectedIndex==1?"IMPORT WORKFLOW":"LIBRARY WORKFLOW";}
  // Help stays open beside the main window so instructions can be followed while working.
  void OpenGuide(string key){
   if(helpWindow!=null){helpWindow.ShowTopic(key);helpWindow.Activate();return;}
   helpWindow=new HelpWindow(Window,HelpCatalog.Load(),key){StartGuide=id=>StartWalkthrough(id),Report=ReportProblem};
   helpWindow.Closed+=(s,e)=>helpWindow=null;helpWindow.Show();
  }
  void InitializeHelp(){InitializeTooltips();Window.PreviewKeyDown+=(s,e)=>{if(e.Key!=Key.F1)return;e.Handled=true;OpenGuide(CurrentHelpTopic());};}
  void SmokeHelp(string output){
   foreach(string name in ControlTips.Keys){var control=Window.FindName(name) as FrameworkElement;if(control==null||control.ToolTip==null)throw new InvalidOperationException("Missing tooltip: "+name);}
   if(Convert.ToString(((TabItem)((TabControl)Window.FindName("MainTabs")).Items[1]).Header)!="Import")throw new InvalidOperationException("Import tab label was not updated.");
   new WindowInteropHelper(Window).EnsureHandle();var help=new HelpWindow(Window,HelpCatalog.Load(),"IMPORT WORKFLOW");if(help.SelectedTopic==null||help.SelectedTopic.Key!="IMPORT WORKFLOW"||!help.ShowingArticle)throw new InvalidOperationException("Contextual help did not select Import.");
   help.SearchBox.Text="pinch";if(help.TopicList.Items.Count<1||help.SelectedTopic==null||help.SelectedTopic.Key!="IMAGE PREVIEW AND TABLES")throw new InvalidOperationException("Help search did not find the preview topic.");
   help.SearchBox.Text="greyed out";if(!help.GuideMatches.Any(g=>g.Id=="import-disabled"))throw new InvalidOperationException("Help search did not offer the guided fix for a disabled Import.");
   help.SearchBox.Text="no-such-help-topic-123456";if(help.TopicList.Items.Count!=0||help.SelectedTopic!=null||!help.Article.PlainText.Contains("No topics match"))throw new InvalidOperationException("Help search did not explain empty results.");help.SearchBox.Clear();if(help.TopicList.Items.Count<30)throw new InvalidOperationException("Clearing search did not restore guide topics.");
   help.ShowHome();if(help.ShowingArticle||help.TopicList.SelectedItem!=null||help.HomeCards.Count(c=>c.Tag is WalkthroughGuide&&((WalkthroughGuide)c.Tag).Fix)!=WalkthroughCatalog.Fixes().Count())throw new InvalidOperationException("Help home does not list every guided fix.");
   string started=null;help.StartGuide=id=>started=id;help.ShowTopic("TROUBLESHOOTING");help.Article.RaiseLink("walkthrough:missing-files");if(started!="missing-files"||help.IsVisible)throw new InvalidOperationException("A guided-fix link did not start its walkthrough.");
   help=new HelpWindow(Window,HelpCatalog.Load(),"TROUBLESHOOTING");help.Article.RaiseLink("topic:REPORTING A PROBLEM");if(help.SelectedTopic==null||help.SelectedTopic.Key!="REPORTING A PROBLEM")throw new InvalidOperationException("A topic link did not open its topic.");help.Close();
   SmokeHelpFormatting(output);
  }
 }
 // The guide: a home page of guided fixes, tours and categories; grouped topics with search;
 // and readable articles with a "Show me" walkthrough, related topics and problem reporting.
 public class HelpWindow:Window {
  public readonly TextBox SearchBox=new TextBox();public readonly ListBox TopicList=new ListBox();public readonly HelpArticle Article=new HelpArticle();
  public Action<string> StartGuide;public Action Report;
  public readonly List<Button> HomeCards=new List<Button>();public List<WalkthroughGuide> GuideMatches=new List<WalkthroughGuide>();
  readonly List<HelpTopic> topics;
  readonly TextBlock title=new TextBlock{FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap},category=new TextBlock{FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,4)},summary=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)},count=new TextBlock{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0)},placeholder=new TextBlock{Text="Search help, for example USB, backup or greyed out",IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0)};
  readonly Button showMe=new Button{HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,0)},home=new Button{Content="Home",HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,10)};
  readonly WrapPanel related=new WrapPanel{Margin=new Thickness(0,12,0,0)},matches=new WrapPanel{Margin=new Thickness(0,0,0,10)};
  readonly FrameworkElement homeView,articleView;readonly StackPanel relatedPanel=new StackPanel();
  bool showingHome,filtering;
  public HelpTopic SelectedTopic{get{return TopicList.SelectedItem as HelpTopic;}}
  public bool ShowingArticle{get{return !showingHome;}}
  public HelpWindow(Window owner,List<HelpTopic> sections,string selected=null){
   topics=sections;Owner=owner;Icon=ApplicationIcon.Image;Title="AstroArchive Guide / Help";Width=Math.Min(1120,SystemParameters.WorkArea.Width-24);Height=Math.Min(800,SystemParameters.WorkArea.Height-24);MinWidth=Math.Min(640,Width);MinHeight=Math.Min(440,Height);WindowStartupLocation=WindowStartupLocation.CenterOwner;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,Control.BackgroundProperty,"Canvas");Theme.Bind(this,Control.ForegroundProperty,"Text");FontFamily=owner.FontFamily;FontSize=owner.FontSize;SetResourceReference(Control.FontSizeProperty,"UiFontControl");
   var layout=new Grid{Margin=new Thickness(20)};layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=layout;
   // Search: one wide box with a hint, so the first thing to try is typing the problem.
   var search=new Grid{Margin=new Thickness(0,0,0,16)};search.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});search.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});layout.Children.Add(search);
   SearchBox.Padding=new Thickness(6,6,6,6);AutomationProperties.SetName(SearchBox,"Search help");UiHelp.Describe(SearchBox,"Search the guide and guided fixes (Ctrl+F).");search.Children.Add(SearchBox);Theme.Bind(placeholder,TextBlock.ForegroundProperty,"Muted");search.Children.Add(placeholder);
   var clear=Button("Clear",()=>{SearchBox.Clear();SearchBox.Focus();},"Show all topics.");Grid.SetColumn(clear,1);clear.Margin=new Thickness(10,0,0,0);search.Children.Add(clear);
   var content=new Grid();content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(270)});content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(content,1);layout.Children.Add(content);
   // Navigation: Home, then topics grouped under their categories.
   var navigation=new DockPanel{Margin=new Thickness(0,0,18,0)};content.Children.Add(navigation);
   home.Click+=(s,e)=>ShowHome();UiHelp.Describe(home,"Guided fixes, tours and guide sections (Alt+Home).");DockPanel.SetDock(home,Dock.Top);navigation.Children.Add(home);
   TopicList.BorderThickness=new Thickness(0);Theme.Bind(TopicList,Control.BackgroundProperty,"Surface");TopicList.HorizontalContentAlignment=HorizontalAlignment.Stretch;ScrollViewer.SetHorizontalScrollBarVisibility(TopicList,ScrollBarVisibility.Disabled);TopicList.ItemTemplate=new DataTemplate{VisualTree=TopicLabel()};UiHelp.Describe(TopicList,"Choose a topic.");AutomationProperties.SetName(TopicList,"Guide topics");
   TopicList.GroupStyle.Add(new GroupStyle{HeaderTemplate=new DataTemplate{VisualTree=GroupLabel()}});navigation.Children.Add(TopicList);
   var reading=new Grid();Grid.SetColumn(reading,1);content.Children.Add(reading);
   homeView=HomeView();reading.Children.Add(homeView);articleView=ArticleView();reading.Children.Add(articleView);
   var footer=new DockPanel{Margin=new Thickness(0,14,0,0)};Grid.SetRow(footer,2);layout.Children.Add(footer);
   var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);actions.Children.Add(Button("Save guide…",SaveGuide,"Save the complete guide as text."));var close=Button("Close",Close,"Close help (Esc).");close.IsCancel=true;close.Margin=new Thickness(0);actions.Children.Add(close);
   var report=Button("Report a problem…",()=>{if(Report!=null)Report();},"Open a GitHub bug report with your system details and recent log filled in.");DockPanel.SetDock(report,Dock.Left);footer.Children.Add(report);
   Theme.Bind(count,TextBlock.ForegroundProperty,"Muted");count.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");footer.Children.Add(count);
   Article.LinkClicked+=FollowLink;
   TopicList.SelectionChanged+=(s,e)=>{if(filtering)return;if(SelectedTopic!=null)ShowArticle();else if(SearchBox.Text.Trim().Length==0)ShowHome();};
   SearchBox.TextChanged+=(s,e)=>FilterTopics();FilterTopics();ShowTopic(selected);
   PreviewKeyDown+=(s,e)=>{
    if((e.Key==Key.F&&(Keyboard.Modifiers&ModifierKeys.Control)!=0)||e.Key==Key.F1){SearchBox.Focus();SearchBox.SelectAll();e.Handled=true;}
    else if(e.SystemKey==Key.Home&&(Keyboard.Modifiers&ModifierKeys.Alt)!=0){ShowHome();e.Handled=true;}
    else if(e.Key==Key.Escape){if(SearchBox.IsKeyboardFocusWithin&&SearchBox.Text.Length>0)SearchBox.Clear();else Close();e.Handled=true;}
    else if(e.Key==Key.Down&&SearchBox.IsKeyboardFocusWithin&&TopicList.Items.Count>0){TopicList.Focus();if(TopicList.SelectedItem==null)TopicList.SelectedIndex=0;var item=TopicList.ItemContainerGenerator.ContainerFromItem(TopicList.SelectedItem) as ListBoxItem;if(item!=null)item.Focus();e.Handled=true;}
   };
   Loaded+=(s,e)=>{if(showingHome)SearchBox.Focus();else TopicList.Focus();};
  }
  // Selects a topic by key, or the home page for null or an unknown key.
  public void ShowTopic(string key){
   var topic=topics.FirstOrDefault(t=>t.Key==key);if(topic==null){ShowHome();return;}
   if(!TopicList.Items.Cast<HelpTopic>().Contains(topic)){SearchBox.Clear();}
   filtering=true;try{TopicList.SelectedItem=topic;}finally{filtering=false;}TopicList.ScrollIntoView(topic);ShowArticle();
  }
  public void ShowHome(){showingHome=true;filtering=true;try{TopicList.SelectedItem=null;}finally{filtering=false;}homeView.Visibility=Visibility.Visible;articleView.Visibility=Visibility.Collapsed;}
  FrameworkElement HomeView(){
   var page=new StackPanel{Margin=new Thickness(4,0,4,12)};
   var heading=new TextBlock{Text="How can we help?",FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};heading.SetResourceReference(TextBlock.FontSizeProperty,"UiFontTitle");page.Children.Add(heading);
   var intro=new TextBlock{Text="Guided fixes and tours open on AstroArchive's own screens. They highlight each control, check your setup as you go, and never import or delete anything for you.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};Theme.Bind(intro,TextBlock.ForegroundProperty,"Muted");page.Children.Add(intro);
   page.Children.Add(Section("Fix a problem",WalkthroughCatalog.Fixes().Select(g=>Card(g.Title,g.Summary,"Start guided fix ›",g,()=>Start(g.Id)))));
   page.Children.Add(Section("Learn by doing",WalkthroughCatalog.Tours().Select(g=>Card(g.Title,g.Summary,"Start tour ›",g,()=>Start(g.Id)))));
   page.Children.Add(Section("Browse the guide",HelpCatalog.Categories.Where(c=>topics.Any(t=>t.Category==c)).Select(c=>{var first=topics.Where(t=>t.Category==c).OrderBy(t=>topics.IndexOf(t)).First();int n=topics.Count(t=>t.Category==c);return Card(c,string.Join(" · ",topics.Where(t=>t.Category==c).Take(3).Select(t=>t.Title)),n+(n==1?" topic ›":" topics ›"),c,()=>ShowTopic(first.Key));})));
   var scroller=new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};MenuScrolling.SetEnabled(scroller,true);AutomationProperties.SetName(scroller,"Help home");return scroller;
  }
  FrameworkElement Section(string heading,IEnumerable<Button> cards){
   var panel=new StackPanel{Margin=new Thickness(0,18,0,0)};var label=new TextBlock{Text=heading,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)};label.SetResourceReference(TextBlock.FontSizeProperty,"UiFontHeading");panel.Children.Add(label);
   var wrap=new WrapPanel();foreach(var card in cards)wrap.Children.Add(card);panel.Children.Add(wrap);return panel;
  }
  Button Card(string heading,string text,string action,object tag,Action click){
   var body=new StackPanel();var name=new TextBlock{Text=heading,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold};body.Children.Add(name);
   var detail=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.Normal,Margin=new Thickness(0,4,0,0)};Theme.Bind(detail,TextBlock.ForegroundProperty,"Muted");detail.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");body.Children.Add(detail);
   var go=new TextBlock{Text=action,Margin=new Thickness(0,8,0,0)};Theme.Bind(go,TextBlock.ForegroundProperty,"Accent");go.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");body.Children.Add(go);
   var card=new Button{Content=body,Tag=tag,Width=272,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,Padding=new Thickness(14,12,14,12),Margin=new Thickness(0,0,12,12)};Theme.Bind(card,Control.BackgroundProperty,"Surface");
   AutomationProperties.SetName(card,heading);AutomationProperties.SetHelpText(card,text);card.Click+=(s,e)=>click();HomeCards.Add(card);return card;
  }
  FrameworkElement ArticleView(){
   var view=new Grid();foreach(var height in new[]{GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto})view.RowDefinitions.Add(new RowDefinition{Height=height});
   var header=new StackPanel{Margin=new Thickness(0,0,0,12)};Theme.Bind(category,TextBlock.ForegroundProperty,"Accent");category.SetResourceReference(TextBlock.FontSizeProperty,"UiFontCaption");header.Children.Add(category);
   title.SetResourceReference(TextBlock.FontSizeProperty,"UiFontTitle");Theme.Bind(title,TextBlock.ForegroundProperty,"Text");header.Children.Add(title);
   Theme.Bind(summary,TextBlock.ForegroundProperty,"Muted");header.Children.Add(summary);
   Theme.Bind(showMe,Control.BackgroundProperty,"Accent");Theme.Bind(showMe,Control.ForegroundProperty,"AccentText");Theme.Bind(showMe,Control.BorderBrushProperty,"Accent");showMe.Click+=(s,e)=>{var topic=SelectedTopic;if(topic!=null&&topic.Walkthrough!=null)Start(topic.Walkthrough);};UiHelp.Describe(showMe,"Walk through this on AstroArchive's own screens.");header.Children.Add(showMe);
   view.Children.Add(header);Grid.SetRow(matches,1);view.Children.Add(matches);Grid.SetRow(Article,2);view.Children.Add(Article);UiHelp.Describe(Article,"Copy selected text with Ctrl+C.");
   var relatedLabel=new TextBlock{Text="Related topics",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,0)};relatedPanel.Children.Add(relatedLabel);relatedPanel.Children.Add(related);Grid.SetRow(relatedPanel,3);view.Children.Add(relatedPanel);
   return view;
  }
  static FrameworkElementFactory TopicLabel(){var label=new FrameworkElementFactory(typeof(TextBlock));label.SetBinding(TextBlock.TextProperty,new Binding("Title"));label.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);label.SetValue(FrameworkElement.MarginProperty,new Thickness(6,0,0,0));return label;}
  static FrameworkElementFactory GroupLabel(){var label=new FrameworkElementFactory(typeof(TextBlock));label.SetBinding(TextBlock.TextProperty,new Binding("Name"));label.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);label.SetValue(FrameworkElement.MarginProperty,new Thickness(4,12,0,4));label.SetResourceReference(TextBlock.ForegroundProperty,"Muted");label.SetResourceReference(TextBlock.FontSizeProperty,"UiFontCaption");return label;}
  Button Button(string text,Action action,string tip){var button=new Button{Content=text};UiHelp.Describe(button,tip);button.Click+=(s,e)=>action();return button;}
  void Start(string id){var start=StartGuide;Close();if(start!=null)start(id);}
  void FollowLink(string target){
   if(target.StartsWith("walkthrough:"))Start(target.Substring("walkthrough:".Length));
   else if(target.StartsWith("topic:"))ShowTopic(target.Substring("topic:".Length));
  }
  void FilterTopics(){
   string query=SearchBox.Text.Trim();placeholder.Visibility=SearchBox.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
   var previous=SelectedTopic;bool home=showingHome;var results=HelpCatalog.Search(topics,query);filtering=true;try{
   // Browsing groups topics by category; searching lists the best matches first.
   var view=new ListCollectionView(query.Length==0?results.OrderBy(t=>HelpCatalog.CategoryOrder(t.Category)).ThenBy(t=>topics.IndexOf(t)).ToList():results);
   if(query.Length==0)view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
   TopicList.ItemsSource=view;TopicList.SelectedItem=query.Length>0?results.FirstOrDefault():home?null:previous;}finally{filtering=false;}
   GuideMatches=WalkthroughCatalog.Search(query);matches.Children.Clear();
   foreach(var guide in GuideMatches){var chosen=guide;var chip=Button(Localization.T(guide.Fix?"Guided fix:":"Tour:")+" "+Localization.T(guide.Title)+" ›",()=>Start(chosen.Id),guide.Summary);chip.Margin=new Thickness(0,0,8,8);Theme.Bind(chip,Control.BackgroundProperty,"SupportSurface");matches.Children.Add(chip);}
   matches.Visibility=GuideMatches.Count==0?Visibility.Collapsed:Visibility.Visible;
   count.Text=query.Length==0?topics.Count+" topics · F1 for page help · Ctrl+F to search":results.Count+" of "+topics.Count+" topics match";
   if(query.Length==0&&(home||previous==null))ShowHome();else ShowArticle();
  }
  void ShowArticle(){
   showingHome=false;homeView.Visibility=Visibility.Collapsed;articleView.Visibility=Visibility.Visible;var topic=SelectedTopic;
   category.Text=topic==null?"":topic.Category.ToUpperInvariant();title.Text=topic==null?"No matching topics":topic.Title;
   summary.Text=topic==null?"":topic.Summary??"";summary.Visibility=summary.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
   var guide=topic==null||topic.Walkthrough==null?null:WalkthroughCatalog.Find(topic.Walkthrough);showMe.Visibility=guide==null?Visibility.Collapsed:Visibility.Visible;
   if(guide!=null){showMe.Content="▶  "+Localization.T(guide.Fix?"Start guided fix:":"Show me in AstroArchive:")+" "+Localization.T(guide.Title);AutomationProperties.SetName(showMe,Convert.ToString(showMe.Content));}
   Article.ShowText(topic==null?Localization.T("No topics match your search. Try fewer words, or **Clear** to browse the guide."):topic.Body);
   related.Children.Clear();if(topic!=null)foreach(string key in topic.Related){var target=topics.FirstOrDefault(t=>t.Key==key);if(target==null)continue;var link=Button(target.Title+" ›",()=>ShowTopic(target.Key),target.Summary??target.Title);link.Margin=new Thickness(0,8,8,0);related.Children.Add(link);}
   relatedPanel.Visibility=related.Children.Count==0?Visibility.Collapsed:Visibility.Visible;
  }
  void SaveGuide(){var file=new SaveFileDialog{FileName="AstroArchive_Guide.txt",Filter="Text files|*.txt"};if(file.ShowDialog(this)!=true)return;try{System.IO.File.WriteAllText(file.FileName,string.Join("\r\n\r\n",HelpCatalog.Categories.SelectMany(c=>topics.Where(t=>t.Category==c)).Select(HelpCatalog.PlainText)));}catch(Exception ex){LocalizedMessage.Show(this,ex.Message,"Guide could not be saved",MessageBoxButton.OK,MessageBoxImage.Warning);}}
 }
}
