using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DocumentList=System.Windows.Documents.List;
namespace AstroArchive {
 public partial class MainUi {
  static IEnumerable<Paragraph> HelpParagraphs(IEnumerable<Block> blocks){
   foreach(var block in blocks){var paragraph=block as Paragraph;if(paragraph!=null)yield return paragraph;var list=block as DocumentList;if(list!=null)foreach(var item in list.ListItems)foreach(var child in HelpParagraphs(item.Blocks))yield return child;var section=block as Section;if(section!=null)foreach(var child in HelpParagraphs(section.Blocks))yield return child;}
  }
  static void CheckHelpArticle(HelpArticle article){
   double body=(double)article.FindResource("UiFontBody"),heading=(double)article.FindResource("UiFontHeading");
   if(!article.IsReadOnly||article.Document.FontSize!=body||article.ActualWidth<=0||article.ActualHeight<=0)throw new Exception("Formatted article has no readable layout or ignores text scaling");
   foreach(var paragraph in HelpParagraphs(article.Document.Blocks)){
    if(Math.Abs(paragraph.FontSize-(paragraph.FontWeight==FontWeights.SemiBold?heading:body))>0.01)throw new Exception("Article heading/body font hierarchy differs from shared styles");Readable(paragraph.Foreground,article.Background,"formatted article paragraph");
   }
   foreach(var callout in article.Document.Blocks.OfType<Section>())foreach(var paragraph in HelpParagraphs(callout.Blocks))Readable(paragraph.Foreground,callout.Background,"help callout");
   if(article.PlainText.Contains("**")||article.PlainText.Contains("## ")||article.PlainText.Contains("](")||article.PlainText.Contains("> Tip:"))throw new Exception("Formatting markers appear in rendered/copyable text");
  }
  void SmokeHelpFormatting(string output){
   string theme=settings.ThemeMode;int scale=settings.TextScalePercent;int cases=0;
   try{foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
    settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();var help=new HelpWindow(Window,HelpCatalog.Load(),"START HERE");
    try{help.Show();PumpPopupLayout();foreach(var topic in HelpCatalog.Load()){
      help.ShowTopic(topic.Key);PumpPopupLayout();CheckHelpArticle(help.Article);if(!help.Article.PlainText.Contains(HelpCatalog.StripInline(HelpCatalog.Blocks(topic.Body).First().Text)))throw new Exception("Formatted topic lost introductory text: "+topic.Key);cases++;
     }
     help.ShowTopic("START HERE");PumpPopupLayout();var ordered=help.Article.Document.Blocks.OfType<DocumentList>().Single();if(ordered.MarkerStyle!=TextMarkerStyle.Decimal||ordered.ListItems.Count!=5||help.Article.Document.Blocks.OfType<Paragraph>().Any(p=>p.FontWeight==FontWeights.SemiBold)||!help.Article.Document.Blocks.OfType<Section>().Any())throw new Exception("Getting started lost its five numbered steps or tip, or gained unrelated subheadings");
     help.Article.SelectAll();if(!help.Article.Selection.Text.Contains("Choose repository")||!ApplicationCommands.Copy.CanExecute(null,help.Article))throw new Exception("Formatted guide text cannot be selected/copied");help.Article.Selection.Select(help.Article.Document.ContentStart,help.Article.Document.ContentStart);help.Article.ScrollToHome();PumpPopupLayout();CapturePopup(help,Path.Combine(output,"AstroArchive_Help_Formatted_"+mode+textScale+".png"));
     help.ShowTopic("TROUBLESHOOTING");PumpPopupLayout();if(!HelpParagraphs(help.Article.Document.Blocks).SelectMany(p=>p.Inlines.OfType<Hyperlink>()).Any())throw new Exception("Troubleshooting lost its guided-fix links");CapturePopup(help,Path.Combine(output,"AstroArchive_Help_Troubleshooting_"+mode+textScale+".png"));
     help.ShowHome();PumpPopupLayout();foreach(var card in help.HomeCards.Where(c=>c.IsVisible)){if(card.ActualWidth<=0||card.ActualHeight<=0)throw new Exception("Help home card has no layout");foreach(var text in PopupChildren<TextBlock>(card).Where(t=>!string.IsNullOrWhiteSpace(t.Text)))Readable(text.Foreground,card.Background,"help home card");}
     CapturePopup(help,Path.Combine(output,"AstroArchive_Help_Home_"+mode+textScale+".png"));
     var tour=new WalkthroughWindow(Window,WalkthroughCatalog.Find("import-disabled"),step=>{},step=>{},finished=>{},step=>step.Check==null?null:new WalkthroughCheck{Ok=step.Check=="repository",Text=step.Check=="repository"?"Repository: D:\\Astro":"This source hasn't been scanned yet."},step=>true);try{tour.Show();PumpPopupLayout();foreach(int index in new[]{0,3,5}){tour.SetStep(index);PumpPopupLayout();CheckHelpArticle(tour.Body);
       if(tour.CurrentStep.Check!=null&&(tour.StatusPanel.Visibility!=Visibility.Visible||tour.Status.Text.Length==0||tour.StatusMark.Text!=(tour.CurrentStep.Check=="repository"?"✓":"!")))throw new Exception("Walkthrough setup check is not shown");
       if(tour.CurrentStep.Check==null&&tour.StatusPanel.Visibility==Visibility.Visible)throw new Exception("Walkthrough shows a check for a step without one");
       if(tour.StatusPanel.IsVisible)Readable(tour.Status.Foreground,tour.StatusPanel.Background,"walkthrough setup check");
       foreach(var button in new[]{tour.Try,tour.Back,tour.Next}.Where(b=>b.Visibility==Visibility.Visible)){var bounds=PopupBounds(button,tour);if(!button.IsVisible||bounds.Right>tour.ActualWidth||bounds.Bottom>tour.ActualHeight)throw new Exception("Formatted walkthrough actions are clipped");}CapturePopup(tour,Path.Combine(output,"AstroArchive_Guided_Fix_"+mode+textScale+"_"+index+".png"));}}finally{tour.Close();}
     var steps=WalkthroughSteps();tour=new WalkthroughWindow(Window,steps,step=>{},step=>{},finished=>{});try{tour.Show();PumpPopupLayout();foreach(int index in new[]{2,5,7}){tour.SetStep(index);PumpPopupLayout();CheckHelpArticle(tour.Body);if(!tour.Body.Document.Blocks.OfType<DocumentList>().Any())throw new Exception("Walkthrough instructions are still one prose block");foreach(var button in new[]{tour.Try,tour.Back,tour.Next}){var bounds=PopupBounds(button,tour);if(!button.IsVisible||bounds.Right>tour.ActualWidth||bounds.Bottom>tour.ActualHeight)throw new Exception("Formatted walkthrough actions are clipped");}CapturePopup(tour,Path.Combine(output,"AstroArchive_Walkthrough_Formatted_"+mode+textScale+"_"+index+".png"));}}finally{tour.Close();}
     SmokeProblemReport(output,mode+textScale);
    }finally{help.Close();}
   }
   File.WriteAllText(Path.Combine(output,"help-formatting-smoke.txt"),"PASS "+cases+" formatted Help topic layouts; help home cards; semantic headings, callouts, links and native ordered/bullet lists; selection/copy; themed contrast and font hierarchy; guided fixes with setup checks; problem report preview and the AstroArchive website link; dark/light and 100/150% text.");
   }finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();}
  }
  // The nearest painted surface behind an element: button text is read against the button, not the window.
  // Check boxes and text boxes paint only their box, so their labels sit on the surface around them.
  static Brush BackgroundBehind(DependencyObject element){
   for(var node=VisualTreeHelper.GetParent(element);node!=null;node=VisualTreeHelper.GetParent(node)){Brush brush=null;var control=node as Control;var border=node as Border;var panel=node as Panel;if(control is Button||control is Window)brush=control.Background;else if(border!=null)brush=border.Background;else if(panel!=null)brush=panel.Background;var solid=brush as SolidColorBrush;if(solid!=null&&solid.Color.A==255)return solid;}
   return null;
  }
  void SmokeProblemReport(string output,string label){
   var report=ProblemReport(new InvalidOperationException("Smoke failure at C:\\Users\\sample\\image.fits"),"Smoke report");string opened=null;bool closed=false;report.Launch=url=>{opened=url;return true;};report.Closed+=(s,e)=>closed=true;
   try{
    report.Show();PumpPopupLayout();
    if(!report.Sections.Any(s=>s.Title=="System details"&&s.Include)||!report.Sections.Any(s=>s.Title=="Recent app log"&&s.Include)||report.Choices.Count!=report.Sections.Count)throw new Exception("Problem report does not offer the system details and app log");
    if(!report.Preview.Text.Contains("### System details")||report.Preview.Text.Contains("sample"))throw new Exception("Problem report preview is missing details or exposes a local path");
    foreach(var choice in report.Choices.Where(c=>((ReportSection)c.Tag).Title!="System details"))choice.IsChecked=false;PumpPopupLayout();
    if(report.Preview.Text.Contains("### Recent app log")||!report.Preview.Text.Contains("### System details"))throw new Exception("Problem report ignores unticked sections");
    foreach(var text in PopupChildren<TextBlock>(report).Where(t=>t.IsVisible&&!string.IsNullOrWhiteSpace(t.Text)))Readable(text.Foreground,BackgroundBehind(text),"problem report text");
    CapturePopup(report,Path.Combine(output,"AstroArchive_Report_Problem_"+label+".png"));
    report.Summary.Text="Smoke title";report.Description.Text="Smoke description";report.Open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(opened==null||!opened.StartsWith("https://astroarchive-companion-test.arijguest.chatgpt.site/support#title=Smoke%20title")||!Uri.UnescapeDataString(opened).Contains("Smoke description")||!closed)throw new Exception("Problem report did not open a prefilled the AstroArchive website issue");
   }finally{if(!closed)report.Close();}
  }
 }
}
