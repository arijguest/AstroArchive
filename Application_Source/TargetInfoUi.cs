using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
 public partial class MainUi {
  readonly TargetInfoCatalog targetInformation=new TargetInfoCatalog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TargetInfo.zip"));
  sealed class TargetInfoState {public TargetInfoView View;public CancellationTokenSource Cancel;public int Generation;public string Target;public double? PreferredWidth;}
  readonly TargetInfoState libraryInfo=new TargetInfoState(),editedInfo=new TargetInfoState();
  void InitializeTargetInformation(string prefix){
   var state=prefix.Length==0?libraryInfo:editedInfo;state.View=new TargetInfoView{Focusable=true};((Border)Window.FindName(prefix+"TargetInfoPane")).Child=state.View;
   state.View.ChooseObject=id=>LoadTargetInformation(prefix,state.Target,id);
   var body=(Grid)Window.FindName(prefix.Length==0?"LibraryBody":"EditedBody");body.SizeChanged+=(s,e)=>ConstrainTargetInformationWidth(prefix);
   // A Grid may retain its minimum desired size while an ancestor clips it.
   // Recheck its allotted slot after layout, including preview divider changes.
   body.LayoutUpdated+=(s,e)=>ConstrainTargetInformationWidth(prefix);
   var divider=(GridSplitter)Window.FindName(prefix+"PreviewDivider");divider.DragCompleted+=(s,e)=>{state.PreferredWidth=body.ColumnDefinitions[3].ActualWidth;ConstrainTargetInformationWidth(prefix);};
  }
  void ConstrainTargetInformationWidth(string prefix){
   var pane=(FrameworkElement)Window.FindName(prefix+"TargetInfoPane");if(pane.Visibility!=Visibility.Visible)return;
   var state=prefix.Length==0?libraryInfo:editedInfo;var body=(Grid)Window.FindName(prefix.Length==0?"LibraryBody":"EditedBody");if(body.ActualWidth<=0)return;
   double allotted=System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(body).Width;
   double bodyWidth=allotted>0?Math.Min(body.ActualWidth,allotted):body.ActualWidth;
   var column=body.ColumnDefinitions[3];double available=Math.Max(220,bodyWidth-body.ColumnDefinitions[0].ActualWidth-body.ColumnDefinitions[1].MinWidth-body.ColumnDefinitions[2].ActualWidth);
   double width=Math.Min(available,state.PreferredWidth??column.Width.Value);if(!column.Width.IsAbsolute||Math.Abs(column.Width.Value-width)>0.5)column.Width=new GridLength(width);
  }
  void SetTargetInformationVisible(string prefix,bool visible){
   var state=prefix.Length==0?libraryInfo:editedInfo;var column=prefix.Length==0?(ColumnDefinition)Window.FindName("PreviewColumn"):((Grid)Window.FindName("EditedBody")).ColumnDefinitions[3];if(visible&&!state.PreferredWidth.HasValue)state.PreferredWidth=Math.Max(220,column.Width.Value);if(!visible)state.PreferredWidth=null;column.MinWidth=visible?220:0;
   ((FrameworkElement)Window.FindName(prefix+"TargetInfoPane")).Visibility=visible?Visibility.Visible:Visibility.Collapsed;
   foreach(string name in prefix.Length==0?new[]{"PreviewHeader","PreviewName","PreviewHost"}:new[]{"EditedPreviewHeader","EditedPreviewHost"})((FrameworkElement)Window.FindName(name)).Visibility=visible?Visibility.Collapsed:Visibility.Visible;
   if(visible)ConstrainTargetInformationWidth(prefix);
  }
  void HideTargetInformation(string prefix){
   var state=prefix.Length==0?libraryInfo:editedInfo;state.Generation++;if(state.Cancel!=null){state.Cancel.Cancel();state.Cancel.Dispose();state.Cancel=null;}state.Target=null;if(state.View!=null){SetTargetInformationVisible(prefix,false);state.View.ToolTip=null;state.View.ShowMessage("Target information","");}
  }
  void ShowTargetInformation(string prefix){
   var state=prefix.Length==0?libraryInfo:editedInfo;if(state.View==null)return;
   var target=(prefix.Length==0?Targets:EditedTargets).SelectedItem as TargetSummary;string name=target==null?"All targets":target.Name;
   SetTargetInformationVisible(prefix,true);if(state.Target==name)return;
   if(name=="All targets"||name=="Calibration"||name=="Unknown"){
    HideTargetInformation(prefix);state.Target=name;SetTargetInformationVisible(prefix,true);state.View.ShowMessage(name=="All targets"?"Explore your targets":name,name=="All targets"?"Choose a target on the left to see its overview and sky location. Select a file to preview your image.":name=="Calibration"?"Calibration frames do not have a celestial target. Select a file to preview it.":"Identify this target to see its catalogue information. Select a file to preview it.");return;
   }
   LoadTargetInformation(prefix,name,null);
  }
  async void LoadTargetInformation(string prefix,string target,string id){
   var state=prefix.Length==0?libraryInfo:editedInfo;HideTargetInformation(prefix);state.Target=target;SetTargetInformationVisible(prefix,true);state.View.ShowMessage(Catalog.Label(target),"Loading target information…");
   state.Cancel=new CancellationTokenSource();var token=state.Cancel.Token;int generation=++state.Generation;
   try{
    // The selected canonical target carries saved names; don't use a display label
    // or fuzzy catalogue search to select a scientific identity.
    var result=await Task.Run(()=>id==null?targetInformation.Find(target,token):targetInformation.ById(id,token),token);
    if(token.IsCancellationRequested||generation!=state.Generation||Window.Dispatcher.HasShutdownStarted)return;
    if(result.Status=="matched")state.View.ShowCard(result.Cards[0],id==null?null:"Reading a candidate for "+Catalog.Label(target));
    else if(result.Status=="ambiguous")state.View.ShowChoices(Catalog.Label(target),result);
    else state.View.ShowMessage(Catalog.Label(target),result.Message+" Your files and target name are unchanged.");
   }catch(OperationCanceledException){}catch(Exception error){if(generation!=state.Generation||Window.Dispatcher.HasShutdownStarted)return;state.View.ShowMessage(Catalog.Label(target),"Target information is unavailable. You can still select and preview your files.");state.View.ToolTip=error.Message;}
  }
  void OpenTargetInformation(ListBox list){
   bool edited=object.ReferenceEquals(list,EditedTargets);
   if(edited){openingEditedSelection=false;ClearTargetSelection("EditedGrid");ShowTargetInformation("Edited");}
   else{openingRepositorySelection=false;ClearTargetSelection("FramesGrid");if(!settings.ShowPreview){settings.ShowPreview=true;SetPreviewVisibility();SaveSettings();}ShowTargetInformation("");}
   keyboardTargets=null;var state=edited?editedInfo:libraryInfo;state.View.Focus();
  }
 }
}
