using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DocumentList=System.Windows.Documents.List;
namespace AstroArchive {
 public partial class MainUi {
  static IEnumerable<Paragraph> HelpParagraphs(IEnumerable<Block> blocks){
   foreach(var block in blocks){var paragraph=block as Paragraph;if(paragraph!=null)yield return paragraph;var list=block as DocumentList;if(list!=null)foreach(var item in list.ListItems)foreach(var child in HelpParagraphs(item.Blocks))yield return child;}
  }
  static void CheckHelpArticle(HelpArticle article){
   double body=(double)article.FindResource("UiFontBody"),heading=(double)article.FindResource("UiFontHeading");
   if(!article.IsReadOnly||article.Document.FontSize!=body||article.ActualWidth<=0||article.ActualHeight<=0)throw new Exception("Formatted article has no readable layout or ignores text scaling");
   foreach(var paragraph in HelpParagraphs(article.Document.Blocks)){
    if(Math.Abs(paragraph.FontSize-(paragraph.FontWeight==FontWeights.SemiBold?heading:body))>0.01)throw new Exception("Article heading/body font hierarchy differs from shared styles");Readable(paragraph.Foreground,article.Background,"formatted article paragraph");
   }
   if(article.PlainText.Contains("**")||article.PlainText.Contains("## "))throw new Exception("Formatting markers appear in rendered/copyable text");
  }
  void SmokeHelpFormatting(string output){
   string theme=settings.ThemeMode;int scale=settings.TextScalePercent;int cases=0;
   try{foreach(string mode in new[]{"Dark","Light"})foreach(int textScale in new[]{100,150}){
    settings.ThemeMode=mode;settings.TextScalePercent=textScale;ApplyAppearance();var help=new HelpWindow(Window,HelpCatalog.Load(),"START HERE");
    try{help.Show();PumpPopupLayout();foreach(var topic in HelpCatalog.Load()){
      help.TopicList.SelectedItem=help.TopicList.Items.Cast<HelpTopic>().Single(t=>t.Key==topic.Key);PumpPopupLayout();CheckHelpArticle(help.Article);if(!help.Article.PlainText.Contains(HelpCatalog.Blocks(topic.Body).First().Text.Replace("**","").Replace("`","")))throw new Exception("Formatted topic lost introductory text: "+topic.Key);cases++;
     }
     help.TopicList.SelectedItem=help.TopicList.Items.Cast<HelpTopic>().Single(t=>t.Key=="START HERE");PumpPopupLayout();var ordered=help.Article.Document.Blocks.OfType<DocumentList>().Single();if(ordered.MarkerStyle!=TextMarkerStyle.Decimal||ordered.ListItems.Count!=5||help.Article.Document.Blocks.OfType<Paragraph>().Count(p=>p.FontWeight==FontWeights.SemiBold)<2)throw new Exception("Getting started lost numbered steps or subheadings");
     help.Article.SelectAll();if(!help.Article.Selection.Text.Contains("Choose repository")||!ApplicationCommands.Copy.CanExecute(null,help.Article))throw new Exception("Formatted guide text cannot be selected/copied");help.Article.Selection.Select(help.Article.Document.ContentStart,help.Article.Document.ContentStart);help.Article.ScrollToHome();PumpPopupLayout();CapturePopup(help,Path.Combine(output,"AstroArchive_Help_Formatted_"+mode+textScale+".png"));
     var steps=WalkthroughSteps();var tour=new WalkthroughWindow(Window,steps,step=>{},step=>{},finished=>{});try{tour.Show();PumpPopupLayout();foreach(int index in new[]{2,5,8}){tour.SetStep(index);PumpPopupLayout();CheckHelpArticle(tour.Body);if(!tour.Body.Document.Blocks.OfType<DocumentList>().Any())throw new Exception("Walkthrough instructions are still one prose block");foreach(var button in new[]{tour.Try,tour.Back,tour.Next}){var bounds=PopupBounds(button,tour);if(!button.IsVisible||bounds.Right>tour.ActualWidth||bounds.Bottom>tour.ActualHeight)throw new Exception("Formatted walkthrough actions are clipped");}CapturePopup(tour,Path.Combine(output,"AstroArchive_Walkthrough_Formatted_"+mode+textScale+"_"+index+".png"));}}finally{tour.Close();}
    }finally{help.Close();}
   }
   File.WriteAllText(Path.Combine(output,"help-formatting-smoke.txt"),"PASS "+cases+" formatted Help topic layouts; semantic headings and native ordered/bullet lists; selection/copy; themed contrast and font hierarchy; short structured walkthrough instructions with accessible actions; dark/light and 100/150% text.");
   }finally{settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();}
  }
 }
}
