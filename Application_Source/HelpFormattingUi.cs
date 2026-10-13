using System;
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
  // Raised with "topic:KEY" or "walkthrough:id" when a link in the text is clicked.
  public event Action<string> LinkClicked;
  // Walkthrough steps are translated, so their callout labels are too; the guide itself stays in English.
  public bool Localized;
  public void RaiseLink(string target){var handler=LinkClicked;if(handler!=null)handler(target);}
  public string PlainText{get{return new TextRange(Document.ContentStart,Document.ContentEnd).Text;}}
  public HelpArticle(){
   IsReadOnly=true;IsUndoEnabled=false;IsDocumentEnabled=true;BorderThickness=new Thickness(0);Padding=new Thickness(16);VerticalScrollBarVisibility=ScrollBarVisibility.Auto;HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;
   SetResourceReference(Control.FontSizeProperty,"UiFontBody");Theme.Bind(this,Control.BackgroundProperty,"Surface");Theme.Bind(this,Control.ForegroundProperty,"Text");MenuScrolling.SetEnabled(this,true);AutomationProperties.SetName(this,"Help article");
  }
  public void ShowText(string text){
   var document=new FlowDocument{PagePadding=new Thickness(0),TextAlignment=TextAlignment.Left,LineHeight=double.NaN};document.SetResourceReference(TextElement.FontSizeProperty,"UiFontBody");document.SetResourceReference(TextElement.ForegroundProperty,"Text");Document=document;
   DocumentList list=null;HelpBlockKind listKind=HelpBlockKind.Paragraph;
   foreach(var block in HelpCatalog.Blocks(text)){
    var paragraph=new Paragraph{Margin=new Thickness(0,0,0,12)};
    if(block.Kind==HelpBlockKind.Callout){
     // Tips and notes sit in a tinted panel with an accent rule; "Important" uses the warm warning tint.
     bool important=block.Label=="Important";var label=new Run((Localized?Localization.T(block.Label):block.Label)+"  "){FontWeight=FontWeights.SemiBold};label.SetResourceReference(TextElement.ForegroundProperty,important?"StarAccent":"Link");paragraph.Inlines.Add(label);AddInlines(paragraph,block.Text);paragraph.Margin=new Thickness(0);
     var panel=new Section(paragraph){BorderThickness=new Thickness(4,0,0,0),Padding=new Thickness(14,10,14,10),Margin=new Thickness(0,2,0,14)};panel.SetResourceReference(TextElement.BackgroundProperty,important?"Starred":"SupportSurface");panel.SetResourceReference(Block.BorderBrushProperty,important?"StarAccent":"Accent");
     list=null;document.Blocks.Add(panel);continue;
    }
    AddInlines(paragraph,block.Text);
    if(block.Kind==HelpBlockKind.Heading){paragraph.FontWeight=FontWeights.SemiBold;paragraph.SetResourceReference(TextElement.FontSizeProperty,"UiFontHeading");paragraph.Margin=new Thickness(0,10,0,6);}
    if(block.Kind==HelpBlockKind.Bullet||block.Kind==HelpBlockKind.Numbered){
     if(list==null||listKind!=block.Kind||block.Kind==HelpBlockKind.Numbered&&block.Number!=list.StartIndex+list.ListItems.Count){list=new DocumentList{MarkerStyle=block.Kind==HelpBlockKind.Bullet?TextMarkerStyle.Disc:TextMarkerStyle.Decimal,StartIndex=block.Kind==HelpBlockKind.Numbered?block.Number:1,Margin=new Thickness(24,0,0,12),MarkerOffset=6};document.Blocks.Add(list);listKind=block.Kind;}
     paragraph.Margin=new Thickness(0,0,0,6);list.ListItems.Add(new ListItem(paragraph));
    }else{list=null;document.Blocks.Add(paragraph);}
   }ScrollToHome();
  }
  void AddInlines(Paragraph paragraph,string text){
   int position=0;foreach(Match match in HelpCatalog.Inlines(text)){
    if(match.Index>position)paragraph.Inlines.Add(new Run(text.Substring(position,match.Index-position)));
    if(match.Groups[3].Success){
     string target=match.Groups[4].Value;var link=new Hyperlink(new Run(match.Groups[3].Value)){Cursor=System.Windows.Input.Cursors.Hand,ToolTip=target.StartsWith("walkthrough:")?Localization.T("Start this guided walkthrough."):Localization.T("Open this help topic.")};link.SetResourceReference(TextElement.ForegroundProperty,"Link");
     link.Click+=(s,e)=>RaiseLink(target);AutomationProperties.SetName(link,match.Groups[3].Value);paragraph.Inlines.Add(link);
    }else{
     var run=new Run(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value);
     if(match.Groups[1].Success)run.FontWeight=FontWeights.SemiBold;else{run.FontFamily=new FontFamily("Consolas");run.SetResourceReference(TextElement.BackgroundProperty,"SurfaceAlt");}
     paragraph.Inlines.Add(run);
    }
    position=match.Index+match.Length;
   }if(position<text.Length)paragraph.Inlines.Add(new Run(text.Substring(position)));
  }
 }
}
