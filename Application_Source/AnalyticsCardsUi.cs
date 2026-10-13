using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AstroArchive {
 // The image on one target's card and where it sits in the frame.
 public sealed class TargetCardComposition {
  public string Path,Label,Description;public BitmapSource Image;public bool Fill;public TargetCardPlacement Placement=new TargetCardPlacement();
  public TargetCardComposition Copy(){var copy=(TargetCardComposition)MemberwiseClone();copy.Placement=Placement.Copy();return copy;}
 }
 public sealed class TargetCardEdit {
  public Window Owner;public TargetCardData Data;public TargetCardLayout Layout;public bool Dark;public string Credit;public TargetCardComposition Current;
 }
 // What Analytics needs from the main window to make cards. Without image
 // callbacks every card uses the star field.
 public sealed class AnalyticsCardSource {
  public List<Frame> Frames;public List<string> InitialTargets;public bool Fill;public string Credit;public int Layout;
  public Func<string,CancellationToken,Task<TargetCardComposition>> DefaultImage;public Func<TargetCardEdit,TargetCardComposition> Adjust;
 }
 public static class TargetCardExport {
  public const int MaximumCards=25;
  // One card saves as an image; several save as a ZIP of individual images. The
  // destination is replaced only once every card has been written.
  public static void Save(string destination,int count,Func<int,BitmapSource> card,Func<int,string> name,bool jpeg,Action<int,string> progress,CancellationToken cancellation){
   if(count<1)throw new ArgumentException("Choose at least one card.");
   if(count==1){cancellation.ThrowIfCancellationRequested();TargetCardRenderer.Save(card(0),destination,jpeg);if(progress!=null)progress(100,"Export complete");return;}
   string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
    using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var zip=new ZipArchive(output,ZipArchiveMode.Create)){
     var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
     for(int i=0;i<count;i++){
      cancellation.ThrowIfCancellationRequested();string file=(i+1).ToString("00")+"_"+Util.Safe(name(i)).Replace(' ','_');while(!used.Add(file))file+="_";
      var entry=zip.CreateEntry(file+(jpeg?".jpg":".png"),CompressionLevel.NoCompression);using(var stream=entry.Open())TargetCardRenderer.Encode(stream,card(i),jpeg);
      if(progress!=null)progress((i+1)*98/count,"Saving card "+(i+1)+" of "+count);
     }
    }
    cancellation.ThrowIfCancellationRequested();if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);if(progress!=null)progress(100,"Export complete");
   }finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
 public sealed partial class AnalyticsWindow {
  sealed class CardJob {public string Target;public TargetCardData Data;public TargetCardComposition Composition;}
  AnalyticsCardSource cards;List<AnalyticsPage> cardPages=new List<AnalyticsPage>();List<CardJob> cardJobs=new List<CardJob>();List<TargetCardChoice> ranked;
  readonly Dictionary<string,TargetCardComposition> compositions=new Dictionary<string,TargetCardComposition>(StringComparer.Ordinal);
  Button cardTargets,adjustCard;ComboBox cardLayout,cardResolution;TextBox cardCredit;FrameworkElement cardLayoutField,cardResolutionField,cardCreditField;DispatcherTimer cardDebounce;
  List<string> chosenTargets;int topCount=10;bool rankByFrames,previewHoldsCards;CancellationTokenSource cardLoading;int cardGeneration;
  bool IsCards {get{return charts!=null&&charts.SelectedIndex==CardsIndex;}}
  void InitializeCardChoices(Panel parent){
   if(cards==null)cards=new AnalyticsCardSource{Frames=frames};
   if(cards.InitialTargets!=null&&cards.InitialTargets.Count>0)chosenTargets=cards.InitialTargets.ToList();
   cardTargets=new Button{HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,0,12)};AccessibleName(cardTargets,"Choose card targets");
   UiHelp.Hint(cardTargets,"Make cards for your top imaged targets, or choose the targets yourself.");cardTargets.Click+=(s,e)=>ChooseCardTargets();parent.Children.Add(cardTargets);UpdateTargetsLabel();
   cardLayout=new ComboBox{ItemsSource=TargetCards.LayoutLabels,SelectedIndex=Math.Max(0,Math.Min(TargetCards.LayoutLabels.Length-1,cards.Layout))};cardLayoutField=Field(parent,"Card layout",cardLayout);
   cardLayout.SelectionChanged+=(s,e)=>{if(IsCards&&!exporting)RenderCards();};
   cardCredit=new TextBox{Text=cards.Credit??"",MaxLength=80};cardCreditField=Field(parent,"Card credit (optional)",cardCredit);UiHelp.Hint(cardCredit,"For example, your name or handle. Leave blank for “Made with AstroArchive”.");
  }
  void InitializeCardHeader(DockPanel header){
   adjustCard=new Button{Content="Adjust card…",Margin=new Thickness(12,0,0,0),Visibility=Visibility.Collapsed};AccessibleName(adjustCard,"Adjust card image");DockPanel.SetDock(adjustCard,Dock.Right);header.Children.Add(adjustCard);
   UiHelp.Hint(adjustCard,"Choose another image of this target, including any Edited image, adjust its zoom and position, and add a short description.");adjustCard.Click+=(s,e)=>AdjustCurrentCard();
  }
  void InitializeCardOptions(Panel advanced){
   cardResolution=new ComboBox{ItemsSource=TargetCardRenderer.ResolutionLabels,SelectedIndex=0};cardResolutionField=Field(advanced,"Card resolution",cardResolution);cardResolutionField.Visibility=Visibility.Collapsed;
   UiHelp.Hint(cardResolution,"Standard matches the layout's social size. High doubles it for print or large screens. Sharing makes smaller files for messages.");cardResolution.SelectionChanged+=(s,e)=>Hints();
   cardDebounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};cardDebounce.Tick+=(s,e)=>{cardDebounce.Stop();if(IsCards&&!exporting)RenderCards();};cardCredit.TextChanged+=(s,e)=>{if(!IsCards)return;cardDebounce.Stop();cardDebounce.Start();};
  }
  void UpdateCardControls(){
   if(cardTargets==null||layoutField==null)return;bool active=IsCards;
   cardSection.Visibility=active?Visibility.Visible:Visibility.Collapsed;layoutField.Visibility=active?Visibility.Collapsed:Visibility.Visible;
   cardResolutionField.Visibility=active&&!IsMotion?Visibility.Visible:Visibility.Collapsed;adjustCard.Visibility=active&&cards.Adjust!=null?Visibility.Visible:Visibility.Collapsed;
  }
  void SetCardControlsEnabled(bool enabled){foreach(var control in new Control[]{cardTargets,adjustCard,cardLayout,cardResolution,cardCredit})control.IsEnabled=enabled;}
  void UpdateTargetsLabel(){cardTargets.Content=chosenTargets==null?"Top "+topCount+" targets · by "+(rankByFrames?"light frames":"integration")+"  ▾":chosenTargets.Count+" chosen target"+(chosenTargets.Count==1?"":"s")+"  ▾";}
  void CancelCards(){cardGeneration++;if(cardLoading!=null){cardLoading.Cancel();cardLoading=null;}if(cardDebounce!=null)cardDebounce.Stop();}
  TargetCardLayout CurrentCardLayout(){return (TargetCardLayout)Math.Max(0,cardLayout.SelectedIndex);}
  // Cards follow the same telescope and date scope as the charts.
  List<Frame> CardScope(){
   var selected=telescope.SelectedItem as ComboBoxItem;string instrument=selected==null?null:Convert.ToString(selected.Tag);DateTime? start=from.SelectedDate,end=to.SelectedDate;
   return cards.Frames.Where(f=>selected==null||string.Equals(string.IsNullOrWhiteSpace(f.Telescope)?"":f.Telescope,instrument,StringComparison.OrdinalIgnoreCase)).Where(f=>{
    if(!start.HasValue&&!end.HasValue)return true;var date=CaptureSessions.Date(f);return date!=null&&(!start.HasValue||date.Date.Date>=start.Value.Date)&&(!end.HasValue||date.Date.Date<=end.Value.Date);
   }).ToList();
  }
  static AnalyticsPage CardPage(TargetCardData data,BitmapSource card,TargetCardLayout layout,bool dark){
   int width,height;TargetCards.Size(layout,out width,out height);
   var page=new AnalyticsPage{Title=data.Title,CanvasWidth=width,CanvasHeight=height,Background=dark?"#0C1220":"#FFFFFF",Role="card"};page.Marks.Add(new AnalyticsMark{Kind="image",Width=width,Height=height,Image=card,Role="card"});return page;
  }
  async void RenderCards(){
   if(!ready||snapshot==null||exporting||!IsCards)return;CancelCards();var loading=cardLoading=new CancellationTokenSource();int request=cardGeneration;
   export.IsEnabled=exportAll.IsEnabled=play.IsEnabled=adjustCard.IsEnabled=false;exportAll.Visibility=Visibility.Collapsed;export.Content=IsMotion?"Export card video…":"Export cards…";previewTitle.Text="Target cards";status.Text="Preparing target cards…";if(!previewHoldsCards)ShowLoading("Preparing target cards…");
   var scoped=CardScope();bool includeRejected=rejected.IsChecked==true,byFrames=rankByFrames;var layout=CurrentCardLayout();bool dark=documentTheme.SelectedIndex==0;string credit=cardCredit.Text;
   try{
    var rank=await Task.Run(()=>TargetCards.Rank(scoped,byFrames,includeRejected),loading.Token);if(closed||request!=cardGeneration)return;ranked=rank;
    var targets=chosenTargets==null?rank.Take(topCount).Select(c=>c.Target).ToList():chosenTargets.Where(t=>rank.Any(c=>c.Target==t)).Take(TargetCardExport.MaximumCards).ToList();
    var jobs=new List<CardJob>();var result=new List<AnalyticsPage>();
    for(int i=0;i<targets.Count;i++){
     status.Text="Preparing card "+(i+1)+" of "+targets.Count+"…";TargetCardComposition composition;
     if(!compositions.TryGetValue(targets[i],out composition)){composition=cards.DefaultImage==null?new TargetCardComposition{Fill=cards.Fill}:await cards.DefaultImage(targets[i],loading.Token);if(closed||request!=cardGeneration)return;compositions[targets[i]]=composition;}
     var data=TargetCards.Build(scoped,targets[i],includeRejected);var bitmap=TargetCardRenderer.Render(data,composition.Image,layout,dark,composition.Fill,credit,composition.Placement,1,composition.Description);
     jobs.Add(new CardJob{Target=targets[i],Data=data,Composition=composition});result.Add(CardPage(data,bitmap,layout,dark));
     // Let the window repaint between cards rather than freezing for the whole set.
     await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);if(closed||request!=cardGeneration)return;
    }
    cardJobs=jobs;cardPages=result;int previous=previewHoldsCards?Math.Max(1,previewPage.SelectedIndex):1;previewHoldsCards=true;
    previewPage.Items.Clear();previewPage.Items.Add("All cards");foreach(var page in result)previewPage.Items.Add(new ComboBoxItem{Content=page.Title,ToolTip=page.Title});previewPage.Visibility=result.Count>1?Visibility.Visible:Visibility.Collapsed;
    paper.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?"#0C1220":"#FFFFFF"));
    if(result.Count==0){preview.Source=null;HideLoading();previewTitle.Text="Target cards";status.Text=chosenTargets==null?"No imaged targets match this scope. Widen the telescope or date range.":"None of the chosen targets were imaged in this scope. Choose other targets or widen the scope.";}
    else{previewPage.SelectedIndex=Math.Min(previous,result.Count);previewTitle.Text="Target cards · "+result.Count+" target"+(result.Count==1?"":"s");status.Text=result.Count+" card"+(result.Count==1?"":"s")+" ready"+(chosenTargets==null?" · Your top targets by "+(byFrames?"light frames":"integration"):"")+". Cards include light frames plus standalone stacks without their subs.";}
    export.IsEnabled=play.IsEnabled=result.Count>0;adjustCard.IsEnabled=result.Count>0&&cards.Adjust!=null;Hints();if(Loading&&result.Count>0)DrawPreview();
   }catch(OperationCanceledException){}
   catch(Exception error){if(closed||request!=cardGeneration)return;status.Text="Cards could not be prepared: "+error.Message;HideLoading();}
   finally{if(cardLoading==loading)cardLoading=null;}
  }
  string CardHint(){
   var layout=CurrentCardLayout();double factor=TargetCardRenderer.ResolutionScale(Math.Max(0,cardResolution.SelectedIndex),layout);int w,h;TargetCards.Size(layout,out w,out h);
   return "Each card is a "+Math.Round(w*factor)+" × "+Math.Round(h*factor)+" pixel image. "+(cardJobs.Count>1?"Several cards save together as a ZIP of individual images.":"Choose targets in the options panel; adjust any card's image above the preview.");
  }
  int CurrentCardIndex(){int index=previewPage.SelectedIndex-1;return Math.Max(0,Math.Min(cardJobs.Count-1,index));}
  void AdjustCurrentCard(){
   if(cards.Adjust==null||cardJobs.Count==0||exporting)return;int index=CurrentCardIndex();var job=cardJobs[index];
   var result=cards.Adjust(new TargetCardEdit{Owner=this,Data=job.Data,Layout=CurrentCardLayout(),Dark=documentTheme.SelectedIndex==0,Credit=cardCredit.Text,Current=job.Composition});
   if(result==null)return;compositions[job.Target]=result;RenderCards();
  }
  void ChooseCardTargets(){
   if(exporting)return;var choices=ranked??new List<TargetCardChoice>();
   var d=new FormWindow(this,"Choose target cards",640,720);d.Text("Target cards",true);
   var mode=d.Select("Make cards for",new[]{"My top targets","Targets I choose"},chosenTargets==null?"My top targets":"Targets I choose");
   ComboBox count=null,order=null;var top=d.Group(()=>{count=d.Select("How many",new[]{"3","5","10","15","20","25"},topCount.ToString());order=d.Select("Rank by",new[]{"Integration time","Light frames"},rankByFrames?"Light frames":"Integration time");});
   TextBox search=null;var list=new ListBox{SelectionMode=SelectionMode.Multiple,Height=300,Margin=new Thickness(0,8,0,4)};AccessibleName(list,"Targets to make cards for");ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
   var picked=new HashSet<string>(chosenTargets??new List<string>(),StringComparer.Ordinal);
   var pick=d.Group(()=>{search=d.Input("Find a target","");d.Add(list);d.Text("Up to "+TargetCardExport.MaximumCards+" targets. Cards keep this order, most integration first.");});
   bool filling=false;
   Action fill=()=>{filling=true;try{list.Items.Clear();string query=search.Text.Trim();foreach(var choice in choices.Where(c=>query.Length==0||c.Label.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0)){var item=new ListBoxItem{Content=choice.Label+"  ·  "+TargetCards.Duration(choice.Seconds)+" · "+choice.Lights.ToString("N0")+" light frames",Tag=choice.Target};AccessibleName(item,choice.Label);list.Items.Add(item);if(picked.Contains(choice.Target))item.IsSelected=true;}}finally{filling=false;}};
   list.SelectionChanged+=(s,e)=>{if(filling)return;foreach(ListBoxItem item in e.AddedItems)picked.Add((string)item.Tag);foreach(ListBoxItem item in e.RemovedItems)picked.Remove((string)item.Tag);};
   search.TextChanged+=(s,e)=>fill();
   Action show=()=>{bool chooseOwn=mode.SelectedIndex==1;top.Visibility=chooseOwn?Visibility.Collapsed:Visibility.Visible;pick.Visibility=chooseOwn?Visibility.Visible:Visibility.Collapsed;};mode.SelectionChanged+=(s,e)=>show();fill();show();
   if(choices.Count==0)d.Text("No imaged targets match the current scope.");
   d.Accept("Use these targets",()=>{
    if(mode.SelectedIndex==1){if(picked.Count==0){LocalizedMessage.Show(d.Window,"Choose at least one target.","Target cards",MessageBoxButton.OK,MessageBoxImage.Information);return false;}if(picked.Count>TargetCardExport.MaximumCards){LocalizedMessage.Show(d.Window,"Choose up to "+TargetCardExport.MaximumCards+" targets.","Target cards",MessageBoxButton.OK,MessageBoxImage.Information);return false;}}
    return true;
   });
   if(!d.Show())return;
   if(mode.SelectedIndex==1)chosenTargets=choices.Where(c=>picked.Contains(c.Target)).Select(c=>c.Target).Concat(picked.Where(t=>!choices.Any(c=>c.Target==t))).ToList();
   else{chosenTargets=null;topCount=int.Parse(Convert.ToString(count.SelectedItem));rankByFrames=order.SelectedIndex==1;}
   UpdateTargetsLabel();RenderCards();
  }
  async Task<bool> ExportCards(){
   if(exporting||cardJobs.Count==0||cardPages.Count!=cardJobs.Count)return false;string kind=Convert.ToString(format.SelectedItem);bool motion=IsMotion,jpeg=kind=="JPEG",collection=!motion&&cardJobs.Count>1;
   string extension=collection?"zip":jpeg?"jpg":kind.ToLowerInvariant();string name=cardJobs.Count==1?"AstroArchive_"+Util.Safe(cardJobs[0].Data.Title).Replace(' ','_')+"_card":"AstroArchive_Target_Cards";
   var dialog=new SaveFileDialog{Title="Export target cards",FileName=name+"."+extension,Filter=(collection?"ZIP of "+kind+" cards":kind)+"|*."+extension,DefaultExt="."+extension,AddExtension=true,OverwritePrompt=true};if(dialog.ShowDialog(this)!=true)return false;
   StopPlayback();var jobs=cardJobs.ToList();var scenes=cardPages.ToList();var layout=CurrentCardLayout();bool dark=documentTheme.SelectedIndex==0;string credit=cardCredit.Text;
   double factor=TargetCardRenderer.ResolutionScale(Math.Max(0,cardResolution.SelectedIndex),layout);var videoOptions=VideoOptions();
   exportCancellation=new CancellationTokenSource();var token=exportCancellation.Token;SetExporting(true,true);status.Text="Preparing "+kind+" export…";var report=Reporter();
   try{
    await RunExport(()=>{
     if(motion)AnalyticsAnimation.Save(dialog.FileName,scenes,kind,videoOptions,report,token);
     else TargetCardExport.Save(dialog.FileName,jobs.Count,i=>factor==1?(BitmapSource)scenes[i].Marks[0].Image:TargetCardRenderer.Render(jobs[i].Data,jobs[i].Composition.Image,layout,dark,jobs[i].Composition.Fill,credit,jobs[i].Composition.Placement,factor,jobs[i].Composition.Description),i=>jobs[i].Data.Title,jpeg,report,token);
    });
    lastOutput=dialog.FileName;status.Text="Exported "+jobs.Count+" target card"+(jobs.Count==1?"":"s")+" · "+Path.GetFileName(lastOutput);return true;
   }catch(OperationCanceledException){status.Text="Export stopped. Your previous file is preserved.";return false;}catch(Exception error){status.Text="Cards could not be saved: "+error.Message;return false;}
   finally{SetExporting(false,true);export.IsEnabled=play.IsEnabled=cardJobs.Count>0;adjustCard.IsEnabled=cardJobs.Count>0&&cards.Adjust!=null;Hints();}
  }
 }
 public partial class MainUi {
  AnalyticsCardSource CardSource(string target){
   return new AnalyticsCardSource{Frames=all.Where(f=>(f.Kind=="Light"||f.Kind=="Stack")&&f.Status!="Deleted").ToList(),InitialTargets=TargetCards.Eligible(target)?new List<string>{target}:null,Fill=settings.CardFill,Credit=settings.CardCredit,Layout=settings.CardLayout,DefaultImage=DefaultCardImage,Adjust=AdjustCardImage};
  }
  // Keeps memory bounded when many cards are open: the card frame is at most
  // about 1900 pixels wide even at High resolution.
  static BitmapSource CompactCardImage(BitmapSource image){
   double scale=Math.Min(1,1920.0/Math.Max(image.PixelWidth,image.PixelHeight));BitmapSource source=scale<1?(BitmapSource)new TransformedBitmap(image,new ScaleTransform(scale,scale)):image;
   var converted=new FormatConvertedBitmap(source,PixelFormats.Bgr24,null,0);int stride=(converted.PixelWidth*3+3)&~3;var pixels=new byte[stride*converted.PixelHeight];converted.CopyPixels(pixels,stride,0);
   var result=BitmapSource.Create(converted.PixelWidth,converted.PixelHeight,96,96,PixelFormats.Bgr24,null,pixels,stride);result.Freeze();return result;
  }
  // The first readable image in card order: linked or starred Edited images, then stacks.
  // The target's remembered image and framing when it can still be read,
  // otherwise its first readable image.
  async Task<TargetCardComposition> DefaultCardImage(string target,CancellationToken ct){
   var memory=CardMemory(target);var images=CardImages(target);
   Func<TargetCardComposition,TargetCardComposition> restore=c=>{if(memory!=null){c.Description=memory.Description;if(memory.Path!=null&&(memory.Path.Length==0||string.Equals(memory.Path,c.Path,StringComparison.OrdinalIgnoreCase))){c.Fill=memory.Fill;c.Placement=memory.Placement();}}return c;};
   if(memory!=null&&memory.Path!=null){
    if(memory.Path.Length==0)return restore(new TargetCardComposition{Fill=settings.CardFill});
    var saved=images.FirstOrDefault(c=>c.Path!=null&&string.Equals(c.Path,memory.Path,StringComparison.OrdinalIgnoreCase));
    if(saved!=null){try{var image=await Task.Run(()=>CompactCardImage(DecodeCardImage(saved,ct)),ct);return restore(new TargetCardComposition{Path=saved.Path,Label=saved.Label,Image=image,Fill=settings.CardFill});}catch(OperationCanceledException){throw;}catch(Exception){ct.ThrowIfCancellationRequested();}}
   }
   foreach(var choice in images.Where(c=>c.Path!=null).Take(3).ToList()){
    try{var image=await Task.Run(()=>CompactCardImage(DecodeCardImage(choice,ct)),ct);return restore(new TargetCardComposition{Path=choice.Path,Label=choice.Label,Image=image,Fill=settings.CardFill});}
    catch(OperationCanceledException){throw;}catch(Exception){ct.ThrowIfCancellationRequested();}
   }
   return restore(new TargetCardComposition{Fill=settings.CardFill});
  }
  TargetCardMemory CardMemory(string target){
   TargetCardMemory memory;return settings.CardChoices!=null&&settings.CardChoices.TryGetValue(Catalog.CanonicalTarget(target),out memory)?memory:null;
  }
  // Path is the chosen image, "" for the star field, or null while nothing was chosen.
  void RememberCard(string target,string path,TargetCardComposition card){
   if(settings.CardChoices==null)settings.CardChoices=new Dictionary<string,TargetCardMemory>(StringComparer.Ordinal);var p=card.Placement??new TargetCardPlacement();
   settings.CardChoices[Catalog.CanonicalTarget(target)]=new TargetCardMemory{Path=path,Fill=card.Fill,Zoom=p.Zoom,PanX=p.PanX,PanY=p.PanY,Rotation=TargetCardRenderer.QuarterTurn(p.Rotation),Description=string.IsNullOrWhiteSpace(card.Description)?null:card.Description.Trim()};SaveSettings();
  }
  static Expander CardPlacementControls(TargetCardPlacement placement,Action changed){
   var panel=new StackPanel{Margin=new Thickness(0,6,0,0)};var expander=new Expander{Header="Adjust image position",Content=panel,Margin=new Thickness(0,0,0,12)};AutomationProperties.SetName(expander,"Adjust image position");
   Func<string,double,double,double,Action<double>,Slider> slider=(label,minimum,maximum,value,apply)=>{
    var caption=new TextBlock{Text=label,Margin=new Thickness(0,4,0,2)};caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");panel.Children.Add(caption);
    var control=new Slider{Minimum=minimum,Maximum=maximum,Value=value,SmallChange=(maximum-minimum)/40,LargeChange=(maximum-minimum)/8,Margin=new Thickness(0,0,0,6)};AutomationProperties.SetName(control,label);panel.Children.Add(control);
    control.ValueChanged+=(s,e)=>{apply(control.Value);changed();};return control;
   };
   var sliders=new[]{slider("Zoom",1,4,placement.Zoom,v=>placement.Zoom=v),slider("Horizontal position",-1,1,placement.PanX,v=>placement.PanX=v),slider("Vertical position",-1,1,placement.PanY,v=>placement.PanY=v)};
   var reset=new Button{Content="Reset position",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,2,0,0)};UiHelp.For(reset,"Reset position");reset.Click+=(s,e)=>ResetCardPlacement(expander,false);panel.Children.Add(reset);
   UiHelp.Hint(sliders[0],"Enlarge the image within its frame, then move it into place.");expander.Tag=new CardPlacementUi{Sliders=sliders,Placement=placement,Changed=changed};return expander;
  }
  sealed class CardPlacementUi {public Slider[] Sliders;public TargetCardPlacement Placement;public Action Changed;}
  static Slider[] CardSliders(Expander controls){var ui=controls.Tag as CardPlacementUi;return ui==null?null:ui.Sliders;}
  // A new image starts upright and centred; Reset position keeps the rotation.
  static void ResetCardPlacement(Expander controls,bool rotation=true){
   var ui=controls.Tag as CardPlacementUi;if(ui==null)return;bool turned=rotation&&ui.Placement.Rotation!=0;if(rotation)ui.Placement.Rotation=0;
   ui.Sliders[0].Value=1;ui.Sliders[1].Value=0;ui.Sliders[2].Value=0;if(turned)ui.Changed();
  }
  static void ApplyCardPlacement(Expander controls,TargetCardPlacement saved){
   var ui=controls.Tag as CardPlacementUi;if(ui==null||saved==null)return;ui.Placement.Rotation=saved.Rotation;ui.Sliders[0].Value=saved.Zoom;ui.Sliders[1].Value=saved.PanX;ui.Sliders[2].Value=saved.PanY;ui.Changed();
  }
  // Quarter turns sit under the preview, where the image is, rather than in a menu.
  static Panel CardRotation(Expander controls){
   var bar=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,8,0,0)};
   foreach(var turn in new[]{-90,90}){
    int delta=turn;var button=new Button{Content=delta<0?"↺ Rotate left":"Rotate right ↻",Margin=new Thickness(4,0,4,0),Padding=new Thickness(10,3,10,3)};UiHelp.For(button,delta<0?"Rotate left":"Rotate right");
    UiHelp.Hint(button,delta<0?"Turn the image a quarter turn anticlockwise.":"Turn the image a quarter turn clockwise.");
    button.Click+=(s,e)=>{var ui=controls.Tag as CardPlacementUi;if(ui==null)return;ui.Placement.Rotation=TargetCardRenderer.QuarterTurn(ui.Placement.Rotation+delta);ui.Sliders[1].Value=0;ui.Sliders[2].Value=0;ui.Changed();};bar.Children.Add(button);
   }
   return bar;
  }
  // Dragging the preview moves the image and Ctrl+scroll zooms it. Both go
  // through the sliders, so keyboard users keep the same control.
  static void CardGestures(Image preview,Expander controls,TargetCardPlacement placement,Func<BitmapSource> hero,Func<TargetCardLayout> layout,Func<bool> fill){
   var sliders=CardSliders(controls);if(sliders==null)return;Point? last=null;
   Action cursor=()=>preview.Cursor=hero()==null?null:last!=null?Cursors.SizeAll:Cursors.Hand;preview.MouseEnter+=(s,e)=>cursor();
   preview.MouseLeftButtonDown+=(s,e)=>{if(hero()==null)return;last=e.GetPosition(preview);preview.CaptureMouse();cursor();e.Handled=true;};
   preview.MouseMove+=(s,e)=>{
    var image=hero();if(last==null||image==null||preview.ActualWidth<=0)return;var now=e.GetPosition(preview);int width,height;TargetCards.Size(layout(),out width,out height);
    double toCard=width/preview.ActualWidth;var range=TargetCardRenderer.PanRange(image,layout(),fill(),placement);
    if(range.Width>.5)sliders[1].Value=placement.PanX-(now.X-last.Value.X)*toCard/range.Width;if(range.Height>.5)sliders[2].Value=placement.PanY-(now.Y-last.Value.Y)*toCard/range.Height;last=now;
   };
   preview.MouseLeftButtonUp+=(s,e)=>{if(last!=null)preview.ReleaseMouseCapture();};preview.LostMouseCapture+=(s,e)=>{last=null;cursor();};
   // The window sees the wheel before the dialog's scroller does, so Ctrl+scroll zooms instead of scrolling.
   MouseWheelEventHandler wheel=(s,e)=>{if((Keyboard.Modifiers&ModifierKeys.Control)==0||hero()==null||!preview.IsMouseOver)return;sliders[0].Value=placement.Zoom*Math.Pow(1.1,e.Delta/120.0);e.Handled=true;};
   Window host=null;preview.Loaded+=(s,e)=>{if(host!=null)return;host=Window.GetWindow(preview);if(host!=null)host.PreviewMouseWheel+=wheel;};
  }
  static TextBox CardDescription(string text){
   var box=new TextBox{Text=text??"",MaxLength=TargetCardRenderer.DescriptionLength,TextWrapping=TextWrapping.Wrap,MinHeight=54,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};AutomationProperties.SetName(box,"Short description (optional)");
   UiHelp.Hint(box,"A line or two under the target name, in card images and videos. Leave blank for none.");return box;
  }
  // Any image of the target qualifies: Edited images need no archive link,
  // though linked images are listed first.
  TargetCardComposition AdjustCardImage(TargetCardEdit edit){
   var work=(edit.Current??new TargetCardComposition{Fill=settings.CardFill}).Copy();var images=CardImages(edit.Data.Target);
   var d=new FormWindow(edit.Owner,"Adjust target card",1240,920);d.Text(Catalog.Label(edit.Data.Target),true);d.Text("Drag the preview to move the image, and hold Ctrl while scrolling to zoom.");
   // The preview takes every pixel the options column leaves, and grows with the window.
   var layoutGrid=new Grid();layoutGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});layoutGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(320)});
   var previewFrame=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(8),Margin=new Thickness(0,0,18,0)};Theme.Bind(previewFrame,Border.BackgroundProperty,"SurfaceAlt");Theme.Bind(previewFrame,Border.BorderBrushProperty,"Border");
   var preview=new Image{Stretch=Stretch.Uniform};Action fitPreview=()=>preview.MaxHeight=Math.Max(260,d.Window.ActualHeight-260);d.Window.SizeChanged+=(s,e)=>fitPreview();d.Window.Loaded+=(s,e)=>fitPreview();AutomationProperties.SetName(preview,"Target card preview");RenderOptions.SetBitmapScalingMode(preview,BitmapScalingMode.HighQuality);
   var status=new TextBlock{TextWrapping=TextWrapping.Wrap,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,8,0,0)};status.SetResourceReference(TextBlock.FontSizeProperty,"UiFontSmall");Theme.Bind(status,TextBlock.ForegroundProperty,"Muted");
   var previewStack=new StackPanel();previewStack.Children.Add(preview);previewStack.Children.Add(status);previewFrame.Child=previewStack;layoutGrid.Children.Add(previewFrame);
   var options=new StackPanel();Grid.SetColumn(options,1);layoutGrid.Children.Add(options);d.Add(layoutGrid);
   var choices=new List<CardImage>{new CardImage{Label="No image (star field)"}};if(work.Image!=null&&!images.Any(i=>string.Equals(i.Path,work.Path,StringComparison.OrdinalIgnoreCase)))choices.Add(new CardImage{Label=work.Label??"Current image",Path=work.Path});choices.AddRange(images);
   int selected=work.Image==null?0:Math.Max(1,choices.FindIndex(c=>c.Path!=null&&string.Equals(c.Path,work.Path,StringComparison.OrdinalIgnoreCase)));
   var caption=new TextBlock{Text="Image",Margin=new Thickness(0,0,0,5)};caption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");options.Children.Add(caption);
   var imageBox=new ComboBox{ItemsSource=choices,SelectedIndex=selected,MaxDropDownHeight=320,Margin=new Thickness(0,0,0,10)};AutomationProperties.SetName(imageBox,"Image");UiHelp.Hint(imageBox,"Linked Edited images come first, then starred and newer images, then archive stacks and lights.");options.Children.Add(imageBox);
   var fill=new CheckBox{Content=new TextBlock{Text="Fill the frame (crop edges)",TextWrapping=TextWrapping.Wrap},IsChecked=work.Fill,Margin=new Thickness(0,2,0,12)};AutomationProperties.SetName(fill,"Fill the frame");options.Children.Add(fill);
   var noteCaption=new TextBlock{Text="Short description (optional)",Margin=new Thickness(0,0,0,5)};noteCaption.SetResourceReference(TextBlock.FontSizeProperty,"UiFontBody");options.Children.Add(noteCaption);
   var note=CardDescription(work.Description);note.Margin=new Thickness(0,0,0,12);options.Children.Add(note);
   bool loading=false;int generation=0;var cancel=new CancellationTokenSource();
   Action draw=()=>{try{preview.Source=TargetCardRenderer.Render(edit.Data,work.Image,edit.Layout,edit.Dark,work.Fill,edit.Credit,work.Placement,.6,work.Description);}catch(Exception e){if(!(e is ArgumentException||e is InvalidOperationException||e is OutOfMemoryException))throw;status.Text="The card could not be drawn: "+e.Message;}};
   var placement=CardPlacementControls(work.Placement,draw);options.Children.Add(placement);
   CardGestures(preview,placement,work.Placement,()=>work.Image,()=>edit.Layout,()=>work.Fill);previewStack.Children.Insert(1,CardRotation(placement));
   note.TextChanged+=(s,e)=>{work.Description=string.IsNullOrWhiteSpace(note.Text)?null:note.Text.Trim();draw();};
   fill.Checked+=(s,e)=>{work.Fill=true;draw();};fill.Unchecked+=(s,e)=>{work.Fill=false;draw();};
   imageBox.SelectionChanged+=async(s,e)=>{
    int current=++generation;var choice=imageBox.SelectedItem as CardImage;ResetCardPlacement(placement);
    if(choice==null||choice.Path==null){work.Image=null;work.Path=null;work.Label=null;loading=false;status.Text="";draw();return;}
    if(choice.Path==(edit.Current==null?null:edit.Current.Path)&&edit.Current.Image!=null){work.Image=edit.Current.Image;work.Path=choice.Path;work.Label=choice.Label;loading=false;status.Text="";draw();return;}
    loading=true;status.Text="Loading image…";
    try{var image=await Task.Run(()=>CompactCardImage(DecodeCardImage(choice,cancel.Token)),cancel.Token);if(current!=generation)return;work.Image=image;work.Path=choice.Path;work.Label=choice.Label;loading=false;status.Text="";draw();}
    catch(OperationCanceledException){}
    catch(Exception error){if(current!=generation)return;loading=false;status.Text="This image could not be read ("+error.Message+"). Choose another image.";}
   };
   d.Accept("Use this image",()=>{if(loading)status.Text="Wait for the image to finish loading.";return !loading;});d.Window.Closed+=(s,e)=>cancel.Cancel();
   draw();if(!d.Show())return null;RememberCard(edit.Data.Target,work.Image==null?"":work.Path,work);return work;
  }
 }
}
