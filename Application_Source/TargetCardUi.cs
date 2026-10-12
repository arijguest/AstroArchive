using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroArchive {
 // Shareable target cards: the best image of a target with its integration,
 // filters and nights, in the Analytics palette and social sizes.
 public static class TargetCardRenderer {
  sealed class Palette {public Color Top,Bottom,Ink,Muted,Accent,Second,Panel,PanelEnd,Border,Matte;}
  static Palette Colours(bool dark){
   Func<string,Color> c=hex=>(Color)ColorConverter.ConvertFromString(hex);
   return dark?new Palette{Top=c("#0C1220"),Bottom=c("#111B30"),Ink=c("#EDF2FC"),Muted=c("#94A5C0"),Accent=c("#90B8FF"),Second=c("#55DFEA"),Panel=c("#151E2E"),PanelEnd=c("#1D2944"),Border=c("#2A3851"),Matte=c("#05080F")}
    :new Palette{Top=c("#FFFFFF"),Bottom=c("#F4F3FF"),Ink=c("#202B43"),Muted=c("#5F6D86"),Accent=c("#5951D6"),Second=c("#008477"),Panel=c("#F5F7FC"),PanelEnd=c("#ECEEFB"),Border=c("#DCE2EF"),Matte=c("#0B1020")};
  }
  // Conventional colours for common astronomy filters; others use the Analytics palette.
  public static Color FilterColour(string filter,int index,bool dark){
   string f=(filter??"").Trim().ToUpperInvariant().Replace("-","").Replace(" ","");Func<string,Color> c=hex=>(Color)ColorConverter.ConvertFromString(hex);
   if(f=="HA"||f=="HALPHA"||f=="H")return c(dark?"#FF6B6B":"#D6404E");if(f=="OIII"||f=="O3"||f=="O")return c(dark?"#55DFEA":"#0E8F9A");if(f=="SII"||f=="S2"||f=="S")return c(dark?"#F0C36A":"#B37608");
   if(f=="L"||f=="LUM"||f=="LUMINANCE"||f=="CLEAR")return c(dark?"#C7D2E6":"#7B8AA3");if(f=="R"||f=="RED")return c(dark?"#F07A8F":"#CB5078");if(f=="G"||f=="GREEN")return c(dark?"#7DD8A0":"#2E8B57");if(f=="B"||f=="BLUE")return c(dark?"#7FA8FF":"#427AB5");
   string[] palette=dark?new[]{"#90B8FF","#C19AFF","#F0C36A","#ED9ACB","#9DD7B0","#A6B4CC"}:new[]{"#5951D6","#8756A5","#B37608","#CB5078","#6B813A","#64748B"};return c(palette[index%palette.Length]);
  }
  static Brush Solid(Color colour,double opacity=1){var brush=new SolidColorBrush(colour){Opacity=opacity};brush.Freeze();return brush;}
  static Brush Vertical(Color top,Color bottom){var brush=new LinearGradientBrush(top,bottom,90);brush.Freeze();return brush;}
  static FormattedText Text(string text,double size,Color colour,bool bold=false,bool semibold=false){
   return new FormattedText(text??"",CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.Bold:semibold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,Solid(colour),1.0);
  }
  static FormattedText Fit(string text,double size,Color colour,double width,bool bold=false,double minimum=0){
   var t=Text(text,size,colour,bold);while(t.Width>width&&size>Math.Max(10,minimum)){size*=0.94;t=Text(text,size,colour,bold);}
   if(t.Width>width){t.MaxTextWidth=Math.Max(1,width);t.Trimming=TextTrimming.CharacterEllipsis;t.MaxLineCount=1;}return t;
  }
  static void Image(DrawingContext dc,BitmapSource hero,Rect frame,bool fill,Palette p,double radius){
   dc.PushClip(new RectangleGeometry(frame,radius,radius));dc.DrawRectangle(Solid(p.Matte),null,frame);
   if(hero!=null){
    double scale=fill?Math.Max(frame.Width/hero.PixelWidth,frame.Height/hero.PixelHeight):Math.Min(frame.Width/hero.PixelWidth,frame.Height/hero.PixelHeight);
    double w=hero.PixelWidth*scale,h=hero.PixelHeight*scale;dc.DrawImage(hero,new Rect(frame.X+(frame.Width-w)/2,frame.Y+(frame.Height-h)/2,w,h));
   }else{
    // No image: a quiet field of stars keeps the card on brand.
    var rng=new Random(7);for(int i=0;i<160;i++){double x=frame.X+rng.NextDouble()*frame.Width,y=frame.Y+rng.NextDouble()*frame.Height,r=rng.NextDouble()<0.08?2.2:rng.NextDouble()*1.2+0.4;dc.DrawEllipse(Solid(Colors.White,0.25+rng.NextDouble()*0.6),null,new Point(x,y),r,r);}
   }
   dc.Pop();dc.DrawRoundedRectangle(null,new Pen(Solid(p.Border,0.9),2),frame,radius,radius);
  }
  static void Metric(DrawingContext dc,Rect box,string value,string label,Palette p,Color stripe,double scale){
   dc.DrawRoundedRectangle(Vertical(p.Panel,p.PanelEnd),new Pen(Solid(p.Border,0.6),1.5),box,18*scale,18*scale);
   dc.DrawRoundedRectangle(Solid(stripe),null,new Rect(box.X+1,box.Y+16*scale,4*scale,box.Height-32*scale),2,2);
   var v=Fit(value,46*scale,p.Ink,box.Width-40*scale,true,24);dc.DrawText(v,new Point(box.X+24*scale,box.Y+box.Height/2-v.Height+6*scale));
   var l=Fit(label,24*scale,p.Muted,box.Width-40*scale);dc.DrawText(l,new Point(box.X+24*scale,box.Y+box.Height/2+8*scale));
  }
  static double Filters(DrawingContext dc,TargetCardData data,Rect area,Palette p,bool dark,double scale){
   if(data.Filters.Count==0||data.Seconds<=0)return 0;
   var label=Text("FILTERS",18*scale,p.Muted,false,true);dc.DrawText(label,new Point(area.X,area.Y));double y=area.Y+label.Height+10*scale,x=area.X,total=data.Filters.Sum(f=>f.Value);
   var bar=new Rect(area.X,y,area.Width,16*scale);dc.PushClip(new RectangleGeometry(bar,bar.Height/2,bar.Height/2));dc.DrawRectangle(Solid(p.Border),null,bar);
   for(int i=0;i<data.Filters.Count;i++){double w=bar.Width*data.Filters[i].Value/total;dc.DrawRectangle(Solid(FilterColour(data.Filters[i].Label,i,dark)),null,new Rect(x,bar.Y,Math.Max(0,w-(i<data.Filters.Count-1?2:0)),bar.Height));x+=w;}
   dc.Pop();y=bar.Bottom+14*scale;x=area.X;
   for(int i=0;i<data.Filters.Count;i++){
    var chip=Text(data.Filters[i].Label+"  "+TargetCards.Duration(data.Filters[i].Value),24*scale,p.Ink);double width=chip.Width+28*scale;if(x+width>area.Right&&x>area.X){x=area.X;y+=chip.Height+10*scale;}
    if(y+chip.Height>area.Bottom)break;dc.DrawEllipse(Solid(FilterColour(data.Filters[i].Label,i,dark)),null,new Point(x+7*scale,y+chip.Height/2),6*scale,6*scale);dc.DrawText(chip,new Point(x+20*scale,y));x+=width+14*scale;
   }
   return y-area.Y+30*scale;
  }
  public static BitmapSource Render(TargetCardData data,BitmapSource hero,TargetCardLayout layout,bool dark,bool fill,string credit){
   int width,height;TargetCards.Size(layout,out width,out height);var p=Colours(dark);var visual=new DrawingVisual();
   using(var dc=visual.RenderOpen()){
    var page=new Rect(0,0,width,height);dc.DrawRectangle(Vertical(p.Top,p.Bottom),null,page);
    // The Analytics accent strip and a restrained constellation identify the brand.
    var strip=new LinearGradientBrush(p.Accent,p.Second,0);strip.Freeze();dc.DrawRectangle(strip,null,new Rect(0,0,width,6));
    for(int i=0;i<18;i++){double sx=width*(.62+((i*37)%100)/290.0),sy=24+((i*53)%90);dc.DrawEllipse(Solid(p.Accent,i%6==0?.45:.2),null,new Point(sx,sy),i%6==0?2.2:1.2,i%6==0?2.2:1.2);}
    bool wide=layout==TargetCardLayout.Widescreen;double scale=wide?1:width/1080.0,margin=64*scale;
    var logo=AnalyticsExport.BrandLogo();dc.DrawImage(logo,new Rect(margin,40*scale,44*scale,44*scale));var brand=Text("AstroArchive",28*scale,p.Ink,true);dc.DrawText(brand,new Point(margin+56*scale,40*scale+(44*scale-brand.Height)/2));
    if(!string.IsNullOrEmpty(data.DateRange)){var dates=Text(data.DateRange,24*scale,p.Muted);dc.DrawText(dates,new Point(width-margin-dates.Width,40*scale+(44*scale-dates.Height)/2));}
    double top=118*scale;Rect hero1;Rect info;
    if(wide){hero1=new Rect(margin,top,1040,height-top-margin);info=new Rect(hero1.Right+56,top,width-hero1.Right-56-margin,height-top-margin);}
    else{double share=layout==TargetCardLayout.Vertical?0.56:layout==TargetCardLayout.Portrait?0.48:0.38;hero1=new Rect(margin,top,width-2*margin,height*share);info=new Rect(margin,hero1.Bottom+36*scale,width-2*margin,height-hero1.Bottom-36*scale-margin);}
    Image(dc,hero,hero1,fill,p,28*scale);
    double y=info.Y;var title=Fit(data.Title,(wide?96:86)*scale,p.Ink,info.Width,true,40);dc.DrawText(title,new Point(info.X,y-10*scale));y+=title.Height-6*scale;
    if(!string.IsNullOrEmpty(data.Subtitle)){var subtitle=Fit(data.Subtitle,38*scale,p.Muted,info.Width);dc.DrawText(subtitle,new Point(info.X,y));y+=subtitle.Height;}
    y+=26*scale;double gap=16*scale,tileHeight=(layout==TargetCardLayout.Square?112:128)*scale;
    string countLabel=data.Lights>0?"Light frames":"Stacks";int count=data.Lights>0?data.Lights:data.Stacks;
    var tiles=new[]{new{V=TargetCards.Duration(data.Seconds),L="Integration",C=p.Second},new{V=count.ToString("N0",CultureInfo.CurrentCulture),L=countLabel,C=p.Accent},new{V=data.Nights.ToString("N0",CultureInfo.CurrentCulture),L=data.Nights==1?"Night":"Nights",C=FilterColour("",2,dark)}};
    if(wide){for(int i=0;i<3;i++)Metric(dc,new Rect(info.X,y+i*(tileHeight+gap),info.Width,tileHeight),tiles[i].V,tiles[i].L,p,tiles[i].C,scale);y+=3*(tileHeight+gap)+12;}
    else{double tileWidth=(info.Width-2*gap)/3;for(int i=0;i<3;i++)Metric(dc,new Rect(info.X+i*(tileWidth+gap),y,tileWidth,tileHeight),tiles[i].V,tiles[i].L,p,tiles[i].C,scale);y+=tileHeight+30*scale;}
    var footer=Text(string.IsNullOrWhiteSpace(credit)?"Made with AstroArchive":credit.Trim(),24*scale,p.Muted,false,true);double footerTop=height-margin*0.75-footer.Height;
    if(!string.IsNullOrEmpty(data.Equipment)){var equipment=Fit(data.Equipment,24*scale,p.Muted,info.Width-(wide?0:footer.Width+24*scale));double equipmentTop=wide?footerTop-equipment.Height-10:footerTop;if(equipmentTop>y+40*scale||wide)dc.DrawText(equipment,new Point(info.X,equipmentTop));}
    Filters(dc,data,new Rect(info.X,y,info.Width,Math.Max(0,footerTop-y-(wide&&!string.IsNullOrEmpty(data.Equipment)?50:16)*scale)),p,dark,scale);
    var credited=Fit(footer.Text,24*scale,p.Muted,wide?info.Width:info.Width*0.6,false);dc.DrawText(credited,new Point(wide?info.X:width-margin-credited.Width,footerTop+(wide?18:0)));
   }
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  }
  public static void Save(BitmapSource card,string destination,bool jpeg){
   string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
    using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){BitmapEncoder encoder=jpeg?(BitmapEncoder)new JpegBitmapEncoder{QualityLevel=95}:new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(jpeg?Flatten(card):card));encoder.Save(output);}
    if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);
   }finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
  static BitmapSource Flatten(BitmapSource card){var converted=new FormatConvertedBitmap(card,PixelFormats.Bgr24,null,0);converted.Freeze();return converted;}
 }
 public partial class MainUi {
  sealed class CardImage {public string Label,Path;public Frame Frame;public bool Scientific;public override string ToString(){return Label;}}
  // Starred finished images first, then other Edited images, then stacks and lights.
  List<CardImage> CardImages(string target){
   string key=Catalog.CanonicalTarget(target);var result=new List<CardImage>();
   foreach(var image in editedImages.Where(i=>i.Metadata!=null&&Catalog.CanonicalTarget(i.Metadata.Object)==key&&CardStill(i.Filename)).OrderByDescending(i=>i.IsStarred).ThenByDescending(i=>i.Modified)){
    string path;try{path=repo.EditedPath(image.Project,image.RelativePath);}catch(Exception e){if(e is IOException||e is InvalidDataException||e is ArgumentException)continue;throw;}
    result.Add(new CardImage{Label=(image.IsStarred?"★ ":"")+image.Filename+" · Edited",Path=path,Scientific=ScientificPreview(path)});
   }
   foreach(var frame in all.Where(f=>f.Target==key&&f.Status!="Deleted"&&(f.Kind=="Stack"||f.Kind=="Light")&&CardStill(f.OriginalName??f.RelativePath)).OrderByDescending(f=>f.IsStarred).ThenBy(f=>f.Kind=="Stack"?0:1).ThenByDescending(f=>f.Exposure??0).ThenByDescending(f=>f.AcquisitionDate).Take(40)){
    string path=repo.FilePath(frame);result.Add(new CardImage{Label=(frame.IsStarred?"★ ":"")+frame.OriginalName+" · "+frame.KindLabel+(frame.Exposure.HasValue?" · "+TargetCards.Duration(frame.Exposure.Value):""),Path=path,Frame=frame,Scientific=ScientificPreview(path)});
   }
   return result;
  }
  BitmapSource DecodeCardImage(CardImage choice,CancellationToken ct){
   var data=DecodePreview(choice.Path,ct,choice.Frame);data.ApplyContext(choice.Frame,choice.Path);
   string mode=choice.Scientific&&!(choice.Frame!=null&&ObservationTargets.Unstretched(choice.Frame.Target,choice.Frame.ObservationMode))?settings.PreviewStretch??"Auto per channel":"Linear";
   var rgb=data.Render(mode,ct);var bitmap=BitmapSource.Create(data.Width,data.Height,96,96,PixelFormats.Rgb24,null,rgb,data.Width*3);
   if(data.FlipY){var flipped=new TransformedBitmap(bitmap,new ScaleTransform(1,-1));flipped.Freeze();return flipped;}bitmap.Freeze();return bitmap;
  }
  void ShowTargetCard(string target){
   if(repo==null||!IntegrationTarget(target))return;var data=TargetCards.Build(all,target);var images=CardImages(target);
   var d=new FormWindow(Window,"Target card",1100,800);d.Text("Share "+Catalog.Label(data.Target),true);
   d.Text("A card for social posts and messages with your best image of this target and its integration, frames, nights and filters. Integration counts light frames, plus standalone stacks without their subs.");
   var layoutGrid=new Grid();layoutGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.35,GridUnitType.Star)});layoutGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
   var previewFrame=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(10),Margin=new Thickness(0,0,18,0),MinHeight=420};Theme.Bind(previewFrame,Border.BackgroundProperty,"SurfaceAlt");Theme.Bind(previewFrame,Border.BorderBrushProperty,"Border");
   var preview=new Image{Stretch=Stretch.Uniform,MaxHeight=560};AutomationProperties.SetName(preview,"Target card preview");RenderOptions.SetBitmapScalingMode(preview,BitmapScalingMode.HighQuality);
   var status=new TextBlock{TextWrapping=TextWrapping.Wrap,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,8,0,0)};status.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");
   var previewStack=new StackPanel();previewStack.Children.Add(preview);previewStack.Children.Add(status);previewFrame.Child=previewStack;layoutGrid.Children.Add(previewFrame);
   var options=new StackPanel();Grid.SetColumn(options,1);layoutGrid.Children.Add(options);d.Add(layoutGrid);
   Func<string,FrameworkElement,FrameworkElement> labelled=(text,control)=>{var panel=new StackPanel{Margin=new Thickness(0,0,0,10)};var caption=new TextBlock{Text=text,Margin=new Thickness(0,0,0,5)};caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");panel.Children.Add(caption);panel.Children.Add(control);AutomationProperties.SetName(control,text);options.Children.Add(panel);return control;};
   var choices=new List<CardImage>{new CardImage{Label="No image (star field)"}};choices.AddRange(images);
   var imageBox=(ComboBox)labelled("Image",new ComboBox{ItemsSource=choices,SelectedIndex=images.Count>0?1:0,MaxDropDownHeight=320});
   var layoutBox=(ComboBox)labelled("Layout",new ComboBox{ItemsSource=TargetCards.LayoutLabels,SelectedIndex=Math.Max(0,Math.Min(TargetCards.LayoutLabels.Length-1,settings.CardLayout))});
   var themeBox=(ComboBox)labelled("Theme",new ComboBox{ItemsSource=new[]{"Dark","Light"},SelectedItem=settings.CardTheme=="Light"?"Light":"Dark"});
   var fill=new CheckBox{Content=new TextBlock{Text="Fill the frame (crop edges)",TextWrapping=TextWrapping.Wrap},IsChecked=settings.CardFill,Margin=new Thickness(0,2,0,12)};AutomationProperties.SetName(fill,"Fill the frame");options.Children.Add(fill);
   var credit=(TextBox)labelled("Credit line (optional)",new TextBox{Text=settings.CardCredit??"",MaxLength=80});UiHelp.Hint(credit,"For example, your name or handle. Leave blank for “Made with AstroArchive”.");
   var format=(ComboBox)labelled("Format",new ComboBox{ItemsSource=new[]{"PNG","JPEG"},SelectedItem=settings.CardFormat=="JPEG"?"JPEG":"PNG"});
   var buttons=new WrapPanel{Margin=new Thickness(0,6,0,0)};options.Children.Add(buttons);
   BitmapSource hero=null,card=null;CancellationTokenSource loading=null;int generation=0;
   Action draw=()=>{
    try{card=TargetCardRenderer.Render(data,hero,(TargetCardLayout)Math.Max(0,layoutBox.SelectedIndex),Convert.ToString(themeBox.SelectedItem)!="Light",fill.IsChecked==true,credit.Text);preview.Source=card;int w,h;TargetCards.Size((TargetCardLayout)Math.Max(0,layoutBox.SelectedIndex),out w,out h);if(loading==null)status.Text=w+" × "+h+" pixels";}
    catch(Exception e){if(!(e is ArgumentException||e is InvalidOperationException||e is OutOfMemoryException))throw;status.Text="The card could not be drawn: "+e.Message;}
   };
   Action loadImage=async()=>{
    if(loading!=null)loading.Cancel();var choice=imageBox.SelectedItem as CardImage;int current=++generation;
    if(choice==null||choice.Path==null){hero=null;loading=null;draw();return;}
    var cancelLoad=loading=new CancellationTokenSource();status.Text="Loading image…";
    try{var bitmap=await Task.Run(()=>DecodeCardImage(choice,cancelLoad.Token),cancelLoad.Token);if(current!=generation)return;hero=bitmap;loading=null;draw();}
    catch(OperationCanceledException){}
    catch(Exception e){if(current!=generation)return;loading=null;hero=null;draw();status.Text="This image could not be read ("+e.Message+"). The card uses a star field instead.";}
   };
   imageBox.SelectionChanged+=(s,e)=>loadImage();foreach(var box in new[]{layoutBox,themeBox})box.SelectionChanged+=(s,e)=>draw();fill.Checked+=(s,e)=>draw();fill.Unchecked+=(s,e)=>draw();credit.TextChanged+=(s,e)=>draw();
   Action remember=()=>{settings.CardLayout=layoutBox.SelectedIndex;settings.CardTheme=Convert.ToString(themeBox.SelectedItem);settings.CardFill=fill.IsChecked==true;settings.CardCredit=credit.Text.Trim();settings.CardFormat=Convert.ToString(format.SelectedItem);SaveSettings();};
   var save=new Button{Content="Save card…",Margin=new Thickness(0,0,8,8)};Theme.Bind(save,Control.BackgroundProperty,"Accent");Theme.Bind(save,Control.ForegroundProperty,"AccentText");UiHelp.For(save,"Save card");buttons.Children.Add(save);
   save.Click+=(s,e)=>{
    if(card==null)return;bool jpeg=Convert.ToString(format.SelectedItem)=="JPEG";
    var picker=new Microsoft.Win32.SaveFileDialog{FileName="AstroArchive_"+Util.Safe(data.Title).Replace(' ','_')+"_card"+(jpeg?".jpg":".png"),Filter=jpeg?"JPEG image|*.jpg":"PNG image|*.png",InitialDirectory=settings.ExportWorkingDirectory!=null&&Directory.Exists(settings.ExportWorkingDirectory)?settings.ExportWorkingDirectory:null};
    if(picker.ShowDialog(d.Window)!=true)return;
    try{TargetCardRenderer.Save(card,picker.FileName,jpeg);remember();status.Text="Saved "+Path.GetFileName(picker.FileName)+".";L("StatusLabel").Text="Target card saved: "+picker.FileName;}
    catch(Exception error){if(!(error is IOException||error is UnauthorizedAccessException||error is NotSupportedException))throw;MessageBox.Show(d.Window,"The card could not be saved: "+error.Message,"Target card",MessageBoxButton.OK,MessageBoxImage.Warning);}
   };
   var copy=new Button{Content="Copy image",Margin=new Thickness(0,0,8,8)};UiHelp.For(copy,"Copy image");buttons.Children.Add(copy);
   copy.Click+=(s,e)=>{if(card==null)return;try{Clipboard.SetImage(card);remember();status.Text="Copied. Paste it into a post or message.";}catch(Exception error){if(!(error is System.Runtime.InteropServices.ExternalException||error is InvalidOperationException))throw;status.Text="The clipboard is busy. Try again.";}};
   d.CloseOnly();d.Window.Closed+=(s,e)=>{if(loading!=null)loading.Cancel();};
   draw();loadImage();d.Show();
  }
  static bool CardStill(string name){return !string.IsNullOrEmpty(name)&&Util.IsImageAsset(name)&&!MediaFiles.Motion(name);}
  static bool IntegrationTarget(string target){return !string.IsNullOrWhiteSpace(target)&&target!="All targets"&&target!="Calibration"&&!Catalog.IsAmbiguous(target);}
 }
}
