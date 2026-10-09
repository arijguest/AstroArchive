using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocumentList=System.Windows.Documents.List;
namespace AstroArchive {
 // One selectable, theme-aware reading style for help and walkthrough steps.
 public sealed class HelpArticle:RichTextBox {
  public string PlainText{get{return new TextRange(Document.ContentStart,Document.ContentEnd).Text;}}
  public HelpArticle(){
   IsReadOnly=true;IsUndoEnabled=false;IsDocumentEnabled=false;BorderThickness=new Thickness(0);Padding=new Thickness(16);VerticalScrollBarVisibility=ScrollBarVisibility.Auto;HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;
   SetResourceReference(Control.FontSizeProperty,"UiFontBody");Theme.Bind(this,Control.BackgroundProperty,"Surface");Theme.Bind(this,Control.ForegroundProperty,"Text");MenuScrolling.SetEnabled(this,true);AutomationProperties.SetName(this,"Help article");
  }
  public void ShowText(string text){
   var document=new FlowDocument{PagePadding=new Thickness(0),TextAlignment=TextAlignment.Left};document.SetResourceReference(TextElement.FontSizeProperty,"UiFontBody");document.SetResourceReference(TextElement.ForegroundProperty,"Text");Document=document;
   DocumentList list=null;HelpBlockKind listKind=HelpBlockKind.Paragraph;
   foreach(var block in HelpCatalog.Blocks(text)){
    var paragraph=new Paragraph{Margin=new Thickness(0,0,0,12)};AddInlines(paragraph,block.Text);
    if(block.Kind==HelpBlockKind.Heading){paragraph.FontWeight=FontWeights.SemiBold;paragraph.SetResourceReference(TextElement.FontSizeProperty,"UiFontHeading");paragraph.Margin=new Thickness(0,8,0,8);}
    if(block.Kind==HelpBlockKind.Bullet||block.Kind==HelpBlockKind.Numbered){
     if(list==null||listKind!=block.Kind||block.Kind==HelpBlockKind.Numbered&&block.Number!=list.StartIndex+list.ListItems.Count){list=new DocumentList{MarkerStyle=block.Kind==HelpBlockKind.Bullet?TextMarkerStyle.Disc:TextMarkerStyle.Decimal,StartIndex=block.Kind==HelpBlockKind.Numbered?block.Number:1,Margin=new Thickness(24,0,0,12),MarkerOffset=6};document.Blocks.Add(list);listKind=block.Kind;}
     paragraph.Margin=new Thickness(0,0,0,5);list.ListItems.Add(new ListItem(paragraph));
    }else{list=null;document.Blocks.Add(paragraph);}
   }ScrollToHome();
  }
  static void AddInlines(Paragraph paragraph,string text){
   int position=0;foreach(Match match in Regex.Matches(text,@"\*\*(.+?)\*\*|`([^`]+)`")){
    if(match.Index>position)paragraph.Inlines.Add(new Run(text.Substring(position,match.Index-position)));
    var run=new Run(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value);if(match.Groups[1].Success)run.FontWeight=FontWeights.SemiBold;else run.FontFamily=new FontFamily("Consolas");paragraph.Inlines.Add(run);position=match.Index+match.Length;
   }if(position<text.Length)paragraph.Inlines.Add(new Run(text.Substring(position)));
  }
 }
}
