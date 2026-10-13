using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  readonly TargetInfoCatalog targetInformation=new TargetInfoCatalog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TargetInfo.zip"));
  sealed class TargetInfoState {public TargetInfoView View;public CancellationTokenSource Cancel;public int Generation;public TargetInfoCard LocalCard,Card;public string Context,Back,CacheKey;public IList<TargetInfoMember> Members;public string Target,Member;public double? PreferredWidth;public bool Dragging,Constraining,Refreshing;}
  readonly TargetInfoState libraryInfo=new TargetInfoState(),editedInfo=new TargetInfoState();
  void InitializeTargetInformation(string prefix){
   // The orbit view eases its camera and plays its dates only as far as Reduce motion allows; Windows' own animation setting also counts.
   OrbitView.ReducedMotion=()=>settings.ReducedMotion||!SystemParameters.ClientAreaAnimation;
   var state=prefix.Length==0?libraryInfo:editedInfo;state.View=new TargetInfoView{Focusable=true};((Border)Window.FindName(prefix+"TargetInfoPane")).Child=state.View;
   state.View.ChooseObject=id=>LoadTargetInformation(prefix,state.Target,id,state.Member);
   state.View.ChooseMember=member=>LoadTargetInformation(prefix,state.Target,null,member);
   state.View.GoBack=()=>LoadTargetInformation(prefix,state.Target,null);
   state.View.OpenInStellarium=OpenStellariumView;state.View.InternetAvailable=AstroArchive.Installation.ConnectivityPolicy.AllowsInternet;state.View.RefreshInformation=()=>EnrichTargetInformation(state,true);
   var body=(Grid)Window.FindName(prefix.Length==0?"LibraryBody":"EditedBody");body.SizeChanged+=(s,e)=>ConstrainTargetInformationWidth(prefix);
   // A Grid may retain its minimum desired size while an ancestor clips it.
   // Recheck its allotted slot after layout, including preview divider changes.
   body.LayoutUpdated+=(s,e)=>ConstrainTargetInformationWidth(prefix);
   // While the divider is dragged the user owns the width; reapplying the saved
   // width on every layout pass made the divider snap back.
   var divider=(GridSplitter)Window.FindName(prefix+"PreviewDivider");divider.DragStarted+=(s,e)=>state.Dragging=true;
   divider.DragCompleted+=(s,e)=>{state.Dragging=false;state.PreferredWidth=body.ColumnDefinitions[3].ActualWidth;ConstrainTargetInformationWidth(prefix);};
  }
  void ConstrainTargetInformationWidth(string prefix){
   var pane=(FrameworkElement)Window.FindName(prefix+"TargetInfoPane");if(pane.Visibility!=Visibility.Visible)return;
   var state=prefix.Length==0?libraryInfo:editedInfo;var body=(Grid)Window.FindName(prefix.Length==0?"LibraryBody":"EditedBody");if(body.ActualWidth<=0||state.Dragging||state.Constraining)return;
   double allotted=System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(body).Width;
   double bodyWidth=allotted>0?Math.Min(body.ActualWidth,allotted):body.ActualWidth;
   var column=body.ColumnDefinitions[3];double available=Math.Max(220,bodyWidth-body.ColumnDefinitions[0].ActualWidth-body.ColumnDefinitions[1].MinWidth-body.ColumnDefinitions[2].ActualWidth);
   double width=Math.Min(available,state.PreferredWidth??column.Width.Value);if(column.Width.IsAbsolute&&Math.Abs(column.Width.Value-width)<=0.5)return;
   state.Constraining=true;try{column.Width=new GridLength(width);}finally{state.Constraining=false;}
  }
  void SetTargetInformationVisible(string prefix,bool visible){
   var state=prefix.Length==0?libraryInfo:editedInfo;var column=prefix.Length==0?(ColumnDefinition)Window.FindName("PreviewColumn"):((Grid)Window.FindName("EditedBody")).ColumnDefinitions[3];if(visible&&!state.PreferredWidth.HasValue)state.PreferredWidth=Math.Max(220,column.Width.Value);if(!visible)state.PreferredWidth=null;column.MinWidth=visible?220:0;
   ((FrameworkElement)Window.FindName(prefix+"TargetInfoPane")).Visibility=visible?Visibility.Visible:Visibility.Collapsed;
   foreach(string name in prefix.Length==0?new[]{"PreviewHeader","PreviewName","PreviewHost"}:new[]{"EditedPreviewHeader","EditedPreviewHost"})((FrameworkElement)Window.FindName(name)).Visibility=visible?Visibility.Collapsed:Visibility.Visible;
   if(visible)ConstrainTargetInformationWidth(prefix);else if(prefix.Length>0)SizeEditedPreview();
  }
  void HideTargetInformation(string prefix){
   var state=prefix.Length==0?libraryInfo:editedInfo;state.Generation++;if(state.Cancel!=null){state.Cancel.Cancel();state.Cancel.Dispose();state.Cancel=null;}if(informationCache!=null&&state.CacheKey!=null)informationCache.Protect(state.CacheKey,false);state.Refreshing=false;state.CacheKey=null;state.LocalCard=state.Card=null;state.Target=null;state.Member=null;if(state.View!=null){SetTargetInformationVisible(prefix,false);state.View.ToolTip=null;state.View.ShowMessage("Target information","");}
  }
  void ShowTargetInformation(string prefix){
   var state=prefix.Length==0?libraryInfo:editedInfo;if(state.View==null)return;
   var target=(prefix.Length==0?Targets:EditedTargets).SelectedItem as TargetSummary;string name=target==null?"All targets":target.Name;
   SetTargetInformationVisible(prefix,true);if(state.Target==name&&state.Member==null)return;
   if(name=="All targets"||name=="Calibration"||name=="Unknown"){
    HideTargetInformation(prefix);state.Target=name;SetTargetInformationVisible(prefix,true);state.View.ShowMessage(name=="All targets"?"Explore your targets":name,name=="All targets"?"Choose a target on the left to see its overview and sky location. Select a file to preview your image.":name=="Calibration"?"Calibration frames do not have a celestial target. Select a file to preview it.":"Identify this target to see its catalogue information. Select a file to preview it.");return;
   }
   LoadTargetInformation(prefix,name,null);
  }
  // The user's targets inside a section (each comet under Comets), with how many files each has.
  List<TargetInfoMember> SectionMembers(string prefix,string section){
   IEnumerable<KeyValuePair<string,int>> counts=prefix.Length==0?all.GroupBy(f=>f.Target).Select(g=>new KeyValuePair<string,int>(g.Key,g.Count())):editedImages.GroupBy(EditedTarget).Select(g=>new KeyValuePair<string,int>(g.Key,g.Count()));
   counts=counts.Where(c=>c.Key!=null&&TargetNavigation.Section(c.Key)==section);
   string unit=prefix.Length==0?"{0} files":"{0} images";
   return counts.Select(c=>new TargetInfoMember{Target=c.Key,Label=TargetNavigation.ShortName(c.Key),Detail=c.Value==1?Localization.T(prefix.Length==0?"1 file":"1 image"):Localization.F(unit,c.Value)}).OrderBy(m=>m.Label,StringComparer.OrdinalIgnoreCase).ToList();
  }
  // member reads one target inside a section (a comet under Comets) and offers the way back to the section.
  async void LoadTargetInformation(string prefix,string target,string id,string member=null){
   var state=prefix.Length==0?libraryInfo:editedInfo;HideTargetInformation(prefix);state.Target=target;state.Member=member;SetTargetInformationVisible(prefix,true);state.View.ShowMessage(Catalog.Label(member??target),"Loading target information…");
   state.Cancel=new CancellationTokenSource();var token=state.Cancel.Token;int generation=++state.Generation;
   try{
    // The selected canonical target carries saved names; don't use a display label
    // or fuzzy catalogue search to select a scientific identity.
    // Eclipse sections are groups of Sun or Moon captures and have their own pages. Comets lists the user's comets.
    bool comets=target==TargetNavigation.CometsSection;
    string lookup=member??(target==Eclipses.SolarSection?Eclipses.SolarSection:target==Eclipses.LunarSection?Eclipses.LunarSection:target);
    var members=comets&&member==null?SectionMembers(prefix,target):null;
    string note=member!=null?null:target==Eclipses.SolarSection?"Your captures taken while the Moon covered part of the Sun. Select one to see the covered fraction in Capture details and the sky map.":target==Eclipses.LunarSection?"Your captures taken while the Moon was in Earth's shadow. Select one to see how deep it was in Capture details and the sky map.":null;
    var result=await Task.Run(()=>{
     var found=id==null?targetInformation.Find(lookup,token):targetInformation.ById(id,token);
     // An older information pack has no eclipse pages; fall back to the eclipsed body.
     if(found.Status=="not_found"&&id==null&&Eclipses.IsSection(lookup))found=targetInformation.Find(lookup==Eclipses.SolarSection?"Sun":"Moon",token);
     return found;
    },token);
    if(token.IsCancellationRequested||generation!=state.Generation||Window.Dispatcher.HasShutdownStarted)return;
    string back=member!=null&&comets?"All comets":null;
    if(member!=null&&id==null)note=Localization.F("One of your comets · {0}",TargetNavigation.ShortName(member));
    if(result.Status=="matched"){
     state.LocalCard=result.Cards[0];state.Context=id==null?note:"Reading a candidate for "+Catalog.Label(member??target);state.Members=members;state.Back=back;
     state.Card=InformationCoordinator.Clone(state.LocalCard);state.View.ShowCard(state.Card,state.Context,members,"YOUR COMETS",back);
     string provider=InformationCoordinator.Provider(state.Card);if(provider!=null){state.CacheKey=InformationCache.Lookup(provider,state.Card.id,Localization.Current);informationCache.Protect(state.CacheKey,true);}
     var local=state.LocalCard;var saved=await Task.Run(()=>informationDownloads.Saved(local,Localization.Current),token);if(token.IsCancellationRequested||generation!=state.Generation)return;
     if(saved!=null){state.Card=InformationCoordinator.Merge(state.LocalCard,saved);state.View.UpdateCard(state.Card,state.Context,members,"YOUR COMETS",back);}
     EnrichTargetInformation(state,false);
    }
    else if(result.Status=="ambiguous")state.View.ShowChoices(Catalog.Label(member??target),result);
    else if(members!=null)state.View.ShowCard(CometInfo.GroupCard(),null,members,"YOUR COMETS",null);
    else state.View.ShowMessage(Catalog.Label(member??target),"No information is available for this target.");
   }catch(OperationCanceledException){}catch(Exception error){if(generation!=state.Generation||Window.Dispatcher.HasShutdownStarted)return;state.View.ShowMessage(Catalog.Label(target),"Target information is unavailable. You can still select and preview your files.");state.View.ToolTip=error.Message;}
  }
  async void EnrichTargetInformation(TargetInfoState state,bool refresh){
   if(state.LocalCard==null||!AstroArchive.Installation.ConnectivityPolicy.AllowsInternet||state.Refreshing&&!refresh)return;if(refresh){state.Refreshing=true;state.View.InformationStatus("Updating information…",true);}int generation=state.Generation;var local=state.LocalCard;
   DownloadedInformation record;try{record=await informationDownloads.Enrich(local,Localization.Current,refresh);}catch(Exception){record=null;}
   if(generation!=state.Generation||Window.Dispatcher.HasShutdownStarted||!AstroArchive.Installation.ConnectivityPolicy.AllowsInternet)return;if(!refresh&&state.Refreshing)return;if(refresh)state.Refreshing=false;
   if(record==null||refresh&&state.Card!=null&&record.retrieved_at==state.Card.downloaded_at){if(refresh)state.View.InformationStatus("No additional information was downloaded. Essential and saved information remain available.",false);return;}
   if(!refresh&&state.Card!=null&&record.retrieved_at==state.Card.downloaded_at)return;state.Card=InformationCoordinator.Merge(local,record);state.View.UpdateCard(state.Card,state.Context,state.Members,"YOUR COMETS",state.Back);if(refresh)state.View.InformationStatus("Information updated.",false);
  }
  void OpenTargetInformation(ListBox list){
   bool edited=object.ReferenceEquals(list,EditedTargets);
   if(edited){openingEditedSelection=false;ClearTargetSelection("EditedGrid");ShowTargetInformation("Edited");}
   else{openingRepositorySelection=false;ClearTargetSelection("FramesGrid");if(!settings.ShowPreview){settings.ShowPreview=true;SetPreviewVisibility();SaveSettings();}ShowTargetInformation("");}
   keyboardTargets=null;var state=edited?editedInfo:libraryInfo;state.View.Focus();
  }
 }
}
