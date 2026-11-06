using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AstroArchive {
 public partial class MainUi {
  static string CreditInlineText(Inline inline){var run=inline as Run;if(run!=null)return run.Text;var span=inline as Span;if(span!=null)return string.Concat(span.Inlines.Select(CreditInlineText));return inline is LineBreak?"\n":"";}
  static string CreditDisplayText(TextBlock text){return string.Concat(text.Inlines.Select(CreditInlineText));}
  static string ComparableCredit(string text){return System.Text.RegularExpressions.Regex.Replace(text??"",@"\s+"," ").Trim();}
  void CheckCreditDisclosure(TargetInfoView view,TargetInfoImage image){
   var projection=TargetInfoPresentation.Credits(image);var disclosure=PopupChildren<Expander>(view).Single(e=>e.Name=="ImageCredits");
   if(disclosure.IsExpanded)throw new Exception("Image credits did not start collapsed.");
   var panel=(StackPanel)disclosure.Content;
   if(panel.Children.OfType<FrameworkElement>().Any(e=>e.IsVisible))throw new Exception("Credit lines are visible outside the collapsed dropdown.");
   var icons=PopupChildren<Button>(view).Where(b=>b.Name=="OpenInStellarium").ToList();double? iconY=icons.Count==0?(double?)null:icons[0].TranslatePoint(new Point(),disclosure).Y;
   disclosure.IsExpanded=true;PumpPopupLayout();
   var text=PopupChildren<TextBlock>(disclosure).Where(t=>t.Name.StartsWith("ImageCredit",StringComparison.Ordinal)).ToList();
   if(text.Count<1||text.Any(t=>t.TextWrapping!=TextWrapping.Wrap||t.TextAlignment!=TextAlignment.Left))throw new Exception("Credit lines are not consistently left-aligned and wrapped.");
   foreach(string expected in projection.Attributions.Concat(projection.Modifications).Concat(projection.RepresentationCredits).Where(s=>!string.IsNullOrWhiteSpace(s)))if(expected!=projection.Creator&&!text.Any(t=>ComparableCredit(CreditDisplayText(t))==ComparableCredit(expected)))throw new Exception("Required credit information is missing: "+Util.Serialize(expected)+"; displayed: "+Util.Serialize(text.Select(CreditDisplayText).ToArray()));
   if(iconY.HasValue&&Math.Abs(icons[0].TranslatePoint(new Point(),disclosure).Y-iconY.Value)>1)throw new Exception("Image toolbar icons moved into the expanded credit body.");
   if(text.Any(t=>t.ActualWidth>disclosure.ActualWidth+1))throw new Exception("A credit line overflows the dropdown.");
   var hyperlinks=text.SelectMany(t=>t.Inlines.OfType<Hyperlink>()).ToList();
   foreach(string expected in new[]{projection.SourceUrl,projection.LicenceUrl}.Where(s=>!string.IsNullOrWhiteSpace(s))){Uri uri;if(Uri.TryCreate(expected,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https")&&!hyperlinks.Any(h=>h.NavigateUri==uri))throw new Exception("Image source or licence hyperlink was lost.");}
   var caption=PopupChildren<TextBlock>(view).SingleOrDefault(t=>t.Name=="ImageCaption");if(caption!=null&&caption.Text!=projection.Caption)throw new Exception("Credit text leaked into the image caption.");
   var sources=PopupChildren<Expander>(view).SingleOrDefault(e=>e.Name=="Sources");if(sources!=null&&image.rights!=null){sources.IsExpanded=true;PumpPopupLayout();string attribution=TargetInfoPresentation.CreditText(image.rights.full_attribution);if(attribution.Length>0&&PopupChildren<TextBlock>(sources).Any(t=>ComparableCredit(CreditDisplayText(t))==ComparableCredit(attribution)))throw new Exception("Image attribution is duplicated in Sources.");sources.IsExpanded=false;}
   disclosure.IsExpanded=false;PumpPopupLayout();
  }
  public void SmokeInfoCredits(string output){
   Directory.CreateDirectory(output);if(!Window.IsVisible)Window.Show();PumpPopupLayout();
   string previousTheme=settings.ThemeMode;int previousScale=settings.TextScalePercent;Window popup=null;int checkedCards=0;
   try{
    foreach(string theme in new[]{"Light","Dark"})foreach(int scale in new[]{100,150})foreach(int width in new[]{240,420}){
     settings.ThemeMode=theme;settings.TextScalePercent=scale;ApplyAppearance();
     var view=new TargetInfoView{InternetAvailable=true,RefreshInformation=()=>{},OpenInStellarium=position=>{}};
     popup=new Window{Owner=Window,Title="Target information credits",Width=width+24,Height=640,Content=view,WindowStartupLocation=WindowStartupLocation.CenterOwner};popup.Resources.MergedDictionaries.Add(Window.Resources);Theme.Bind(popup,Control.BackgroundProperty,"Surface");popup.Show();PumpPopupLayout();
     foreach(string id in new[]{"M2","M28","M58","M65","M96","NGC 7023","EARTH","NGC 7640"}){
      var found=targetInformation.ById(id,CancellationToken.None);if(found.Cards.Count!=1)throw new Exception("Credit fixture was not found: "+id);var card=found.Cards.Single();view.ShowCard(card);PumpPopupLayout();CheckCreditDisclosure(view,card.image);checkedCards++;
     }
     var basis=targetInformation.Find("M31",CancellationToken.None).Cards.Single();var original=basis.image;byte[] thumbnail=basis.Thumbnail;
     try{
      basis.image=new TargetInfoImage{representation="Visible-light photograph.\nCredit: Example Observatory.",rights=new InformationRights{creator="Example Observatory",full_attribution="Required image attribution: Observatory & contributors.\nArtist: https://example.org/artist",licence_id="CC-BY-SA",licence_version="4.0",licence_url="https://creativecommons.org/licenses/by-sa/4.0/",source_url="https://example.org/image",modifications="Cropped; colour adjusted."}};
      view.ShowCard(basis);PumpPopupLayout();CheckCreditDisclosure(view,basis.image);checkedCards++;
      var credits=PopupChildren<Expander>(view).Single(e=>e.Name=="ImageCredits");credits.IsExpanded=true;PumpPopupLayout();credits.BringIntoView();PumpPopupLayout();CapturePopup(popup,Path.Combine(output,"Credits_"+theme+"_"+scale+"_"+width+".png"));credits.IsExpanded=false;
      basis.Thumbnail=null;basis.DecodedThumbnail=null;view.ShowCard(basis);PumpPopupLayout();CheckCreditDisclosure(view,basis.image);checkedCards++;
      basis.Thumbnail=new byte[]{1,2,3};view.ShowCard(basis);PumpPopupLayout();CheckCreditDisclosure(view,basis.image);checkedCards++;
     }finally{basis.image=original;basis.Thumbnail=thumbnail;basis.DecodedThumbnail=null;}
     popup.Close();popup=null;
    }
    File.WriteAllText(Path.Combine(output,"target-info-credits-smoke.txt"),"PASS: "+checkedCards+" credit presentations; short/long and prefixed catalogue credits, rights-only metadata, missing/invalid thumbnails, collapsed/expanded state, source/licence links, stationary header icons, light/dark themes, 100/150% text and narrow/wide panes.");
   }finally{if(popup!=null)popup.Close();settings.ThemeMode=previousTheme;settings.TextScalePercent=previousScale;ApplyAppearance();}
  }
 }
}
