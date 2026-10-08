using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
namespace AstroArchive {
 public partial class MainUi {
  void Guide(){
   var menu=new ContextMenu();Action<string,string> add=(title,key)=>{var item=new MenuItem{Header=title,ToolTip="Open this topic in the searchable guide."};item.Click+=(s,e)=>OpenGuide(key);menu.Items.Add(item);};
   add("Search the guide…",null);add("Help for this page (F1)",CurrentHelpTopic());menu.Items.Add(new Separator());
   add("Getting started","START HERE");add("USB and saved telescopes","USB TRANSFER AND SAVED TELESCOPES");add("Image preview and tables","IMAGE PREVIEW AND TABLES");add("Stacking projects","STACKING PROJECTS AND SESSIONS");add("Troubleshooting","TROUBLESHOOTING");add("Keyboard shortcuts","KEYBOARD SHORTCUTS");add("Frame types and glossary","FRAME TYPES AND GLOSSARY");
   menu.PlacementTarget=TopMenu("GuideMenu");menu.IsOpen=true;
  }
  string CurrentHelpTopic(){return ((TabControl)Window.FindName("MainTabs")).SelectedIndex==2?"EDITED IMAGES AND WORKING COPIES":((TabControl)Window.FindName("MainTabs")).SelectedIndex==1?"IMPORT WORKFLOW":"LIBRARY WORKFLOW";}
  void OpenGuide(string key){new HelpWindow(Window,HelpCatalog.Load(),key).ShowDialog();}
  void InitializeHelp(){InitializeTooltips();Window.PreviewKeyDown+=(s,e)=>{if(e.Key!=Key.F1)return;e.Handled=true;OpenGuide(CurrentHelpTopic());};}
  void SmokeHelp(){
   foreach(string name in ControlTips.Keys){var control=Window.FindName(name) as FrameworkElement;if(control==null||control.ToolTip==null||!ToolTipService.GetShowOnDisabled(control))throw new InvalidOperationException("Missing tooltip: "+name);}
   if(Convert.ToString(((TabItem)((TabControl)Window.FindName("MainTabs")).Items[1]).Header)!="Import")throw new InvalidOperationException("Import tab label was not updated.");
   new WindowInteropHelper(Window).EnsureHandle();var help=new HelpWindow(Window,HelpCatalog.Load(),"IMPORT WORKFLOW");if(help.SelectedTopic==null||help.SelectedTopic.Key!="IMPORT WORKFLOW")throw new InvalidOperationException("Contextual help did not select Import.");
   help.SearchBox.Text="pinch";if(help.TopicList.Items.Count<1||help.SelectedTopic==null||help.SelectedTopic.Key!="IMAGE PREVIEW AND TABLES")throw new InvalidOperationException("Help search did not find the preview topic.");help.SearchBox.Text="no-such-help-topic-123456";if(help.TopicList.Items.Count!=0||help.SelectedTopic!=null||!help.Article.Text.Contains("No topics match"))throw new InvalidOperationException("Help search did not explain empty results.");help.SearchBox.Clear();if(help.TopicList.Items.Count<15)throw new InvalidOperationException("Clearing search did not restore guide topics.");help.Close();
  }
 }
 public class HelpWindow:Window {
  public readonly TextBox SearchBox=new TextBox();public readonly ListBox TopicList=new ListBox();public readonly TextBox Article=new TextBox();
  readonly List<HelpTopic> topics;readonly TextBlock title=new TextBlock{FontSize=21,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};readonly TextBlock count=new TextBlock{Foreground=Brushes.SlateGray,Margin=new Thickness(0,8,0,0)};
  public HelpTopic SelectedTopic{get{return TopicList.SelectedItem as HelpTopic;}}
  public HelpWindow(Window owner,List<HelpTopic> sections,string selected=null){
   topics=sections;Owner=owner;Icon=ApplicationIcon.Image;Title="AstroArchive Guide / Help";Width=Math.Min(1050,SystemParameters.WorkArea.Width-24);Height=Math.Min(780,SystemParameters.WorkArea.Height-24);MinWidth=600;MinHeight=420;WindowStartupLocation=WindowStartupLocation.CenterOwner;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,Control.BackgroundProperty,"Canvas");Theme.Bind(this,Control.ForegroundProperty,"Text");FontFamily=owner.FontFamily;FontSize=owner.FontSize;SetResourceReference(Control.FontSizeProperty,"UiFontControl");
   var layout=new Grid{Margin=new Thickness(20)};layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=layout;
   var search=new Grid{Margin=new Thickness(0,0,0,14)};search.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});search.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});search.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});layout.Children.Add(search);
   var label=new Label{Content="Search help",Target=SearchBox,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};search.Children.Add(label);Grid.SetColumn(SearchBox,1);search.Children.Add(SearchBox);UiHelp.Tip(SearchBox,"Search topic titles and full guide text. Every word must match. Ctrl+F focuses this field.");var clear=Button("Clear",()=>SearchBox.Clear(),"Show all guide topics again.");Grid.SetColumn(clear,2);clear.Margin=new Thickness(10,0,0,0);search.Children.Add(clear);
   var content=new Grid();content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(255)});content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(content,1);layout.Children.Add(content);
   TopicList.BorderThickness=new Thickness(0);Theme.Bind(TopicList,Control.BackgroundProperty,"Surface");TopicList.HorizontalContentAlignment=HorizontalAlignment.Stretch;ScrollViewer.SetHorizontalScrollBarVisibility(TopicList,ScrollBarVisibility.Disabled);TopicList.ItemTemplate=new DataTemplate{VisualTree=TopicLabel()};TopicList.Margin=new Thickness(0,0,16,0);UiHelp.Tip(TopicList,"Choose a topic. Use Up and Down to browse the filtered results.");content.Children.Add(TopicList);
   var reading=new Grid();reading.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});reading.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});Grid.SetColumn(reading,1);content.Children.Add(reading);Theme.Bind(title,TextBlock.ForegroundProperty,"Text");Theme.Bind(count,TextBlock.ForegroundProperty,"Muted");reading.Children.Add(title);Article.IsReadOnly=true;Article.VerticalContentAlignment=VerticalAlignment.Top;Article.TextWrapping=TextWrapping.Wrap;Article.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;Article.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;Article.BorderThickness=new Thickness(0);Article.Padding=new Thickness(14);Theme.Bind(Article,Control.BackgroundProperty,"Surface");Grid.SetRow(Article,1);reading.Children.Add(Article);UiHelp.Tip(Article,"Read and select guide text. Ctrl+C copies selected text.");
   var footer=new DockPanel{Margin=new Thickness(0,14,0,0)};Grid.SetRow(footer,2);layout.Children.Add(footer);var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);actions.Children.Add(Button("Save guide…",SaveGuide,"Save the complete guide as a text file."));var close=Button("Close",Close,"Close help and return to AstroArchive. Escape also closes help.");close.IsCancel=true;close.Margin=new Thickness(0);actions.Children.Add(close);footer.Children.Add(count);
   TopicList.SelectionChanged+=(s,e)=>ShowTopic();SearchBox.TextChanged+=(s,e)=>FilterTopics();FilterTopics();if(selected!=null)TopicList.SelectedItem=topics.FirstOrDefault(t=>t.Key==selected)??TopicList.SelectedItem;
   PreviewKeyDown+=(s,e)=>{if((e.Key==Key.F&&(Keyboard.Modifiers&ModifierKeys.Control)!=0)||e.Key==Key.F1){SearchBox.Focus();SearchBox.SelectAll();e.Handled=true;}else if(e.Key==Key.Escape){Close();e.Handled=true;}};
   Loaded+=(s,e)=>{if(selected==null)SearchBox.Focus();else TopicList.Focus();};
  }
  static FrameworkElementFactory TopicLabel(){var label=new FrameworkElementFactory(typeof(TextBlock));label.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Title"));label.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);return label;}
  Button Button(string text,Action action,string tip){var button=new Button{Content=text};UiHelp.Tip(button,tip);button.Click+=(s,e)=>action();return button;}
  void FilterTopics(){var previous=SelectedTopic;var results=HelpCatalog.Search(topics,SearchBox.Text);TopicList.ItemsSource=results;TopicList.SelectedItem=results.Contains(previous)?previous:results.FirstOrDefault();count.Text=results.Count+" of "+topics.Count+" topics · Ctrl+F to search";ShowTopic();}
  void ShowTopic(){var topic=SelectedTopic;title.Text=topic==null?"No matching topics":topic.Title;Article.Text=topic==null?"No topics match your search. Try fewer words, or Clear to browse the guide.":topic.Body;Article.ScrollToHome();}
  void SaveGuide(){var file=new SaveFileDialog{FileName="AstroArchive_Guide.txt",Filter="Text files|*.txt"};if(file.ShowDialog(this)!=true)return;try{System.IO.File.WriteAllText(file.FileName,string.Join("\r\n\r\n",topics.Select(t=>t.Title+"\r\n"+t.Body)));}catch(Exception ex){MessageBox.Show(this,ex.Message,"Guide could not be saved",MessageBoxButton.OK,MessageBoxImage.Warning);}}
 }
}
