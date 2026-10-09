// Saved telescope selection, USB arrival/removal, and one-click uploads.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
namespace AstroArchive {
 public partial class MainUi {
  bool changingScope,usbChecking,telescopesDisposed,usbImportPicking;int usbGeneration;
  DispatcherTimer usbTimer;HwndSource usbWindow;CancellationTokenSource usbDiscoveryCancel=new CancellationTokenSource();
  List<UsbVolume> usbVolumes=new List<UsbVolume>();List<UsbTelescope> usbTelescopes=new List<UsbTelescope>();UsbTelescope activeUsb,pendingUsb;
  string cancellationMessage;
  TelescopeProfile SelectedScope{get{return C("SavedTelescopeBox").SelectedItem as TelescopeProfile;}}
  void InitializeTelescopes(){
   TelescopeProfiles.Initialize(settings);C("SavedTelescopeBox").DisplayMemberPath="DisplayText";
   C("SavedTelescopeBox").SelectionChanged+=(s,e)=>{if(changingScope||RepositoryOperationBlocked)return;ApplyScope(SelectedScope);};
   B("SaveTelescopeButton").Click+=(s,e)=>SaveScope();B("RenameTelescopeButton").Click+=(s,e)=>RenameScope();
   B("RebuildTelescopesButton").Click+=(s,e)=>RebuildScopes(true);B("RefreshUsbButton").Click+=(s,e)=>RefreshUsb();B("AutoUploadButton").Click+=(s,e)=>UploadUsbMenu();
   T("TelescopeBox").TextChanged+=(s,e)=>InvalidateImportPlan();C("ModelBox").SelectionChanged+=(s,e)=>InvalidateImportPlan();C("CameraBox").SelectionChanged+=(s,e)=>InvalidateImportPlan();
   ReloadScopes(settings.SelectedTelescope??settings.Telescope,true);
   usbTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};usbTimer.Tick+=(s,e)=>RefreshUsb();
   Window.Loaded+=(s,e)=>{if(!telescopesDisposed){usbTimer.Start();RefreshUsb();}};
   Window.SourceInitialized+=(s,e)=>{usbWindow=HwndSource.FromHwnd(new WindowInteropHelper(Window).Handle);if(usbWindow!=null)usbWindow.AddHook(UsbWindowMessage);};
  }
  void QueueUsbRefresh(){Window.Dispatcher.BeginInvoke(new Action(RefreshUsb));}
  IntPtr UsbWindowMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled){if(message==0x0219&&!telescopesDisposed)QueueUsbRefresh();return IntPtr.Zero;}
  void DisposeTelescopes(){telescopesDisposed=true;usbDiscoveryCancel.Cancel();if(usbTimer!=null)usbTimer.Stop();if(usbWindow!=null){usbWindow.RemoveHook(UsbWindowMessage);usbWindow=null;}}
  void InvalidateImportPlan(){if(RepositoryOperationBlocked)return;plan=null;FilterImports();((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked=false;SetBusy(false);}
  void ReloadScopes(string selected,bool apply){
   changingScope=true;try{var profiles=new List<TelescopeProfile>{new TelescopeProfile()};profiles.AddRange(settings.Telescopes.OrderBy(p=>p.Id,StringComparer.OrdinalIgnoreCase));C("SavedTelescopeBox").ItemsSource=profiles;C("SavedTelescopeBox").SelectedItem=profiles.FirstOrDefault(p=>string.Equals(p.Id,selected,StringComparison.OrdinalIgnoreCase))??profiles[0];}finally{changingScope=false;}
   if(apply)ApplyScope(SelectedScope);UpdateTelescopeState(RepositoryOperationBlocked);
  }
  void ApplyScope(TelescopeProfile profile){
   if(profile==null||string.IsNullOrEmpty(profile.Id)){settings.SelectedTelescope=null;T("TelescopeBox").Text="";C("ModelBox").SelectedItem="Auto";C("CameraBox").SelectedItem="Auto";}
   else{settings.SelectedTelescope=profile.Id;T("TelescopeBox").Text=profile.Id;C("ModelBox").SelectedItem=TelescopeProfiles.Model(profile.Model);C("CameraBox").SelectedItem=profile.Camera??"Auto";
    var connected=usbTelescopes.FirstOrDefault(t=>string.Equals(t.ProfileId,profile.Id,StringComparison.OrdinalIgnoreCase));T("SourceBox").Text=pendingUsb!=null?pendingUsb.Source:connected!=null?connected.Source:string.IsNullOrEmpty(profile.VolumeId)?profile.LastSource??"":"";
   }
   InvalidateImportPlan();UpdateTelescopeState(RepositoryOperationBlocked);
  }
  void RebuildScopes(bool report){
   if(RepositoryOperationBlocked||repo==null)return;int added=TelescopeProfiles.MergeRepository(settings,all);ReloadScopes(settings.SelectedTelescope,false);if(added>0)SaveSettings();usbGeneration++;RefreshUsb();
   if(report)L("StatusLabel").Text=added+" telescope profiles recovered from the repository. Existing saved profiles were kept.";
  }
  void SaveScope(){
   if(RepositoryOperationBlocked)return;try{
    var candidate=Util.Deserialize<Settings>(Util.Serialize(settings));var profile=TelescopeProfiles.Save(candidate,T("TelescopeBox").Text,Convert.ToString(C("ModelBox").SelectedItem),Convert.ToString(C("CameraBox").SelectedItem),T("SourceBox").Text,usbVolumes);
    candidate.LastSource=T("SourceBox").Text;candidate.Telescope=profile.Id;candidate.Model=profile.Model;Directory.CreateDirectory(Path.GetDirectoryName(config));Util.AtomicText(config,Util.Serialize(candidate));settings=candidate;pendingUsb=null;ReloadScopes(profile.Id,false);usbGeneration++;RefreshUsb();
    L("StatusLabel").Text="Saved "+profile.Id+". Select it from Saved telescope on your next import.";
   }catch(Exception e){MessageBox.Show(Window,e.Message,"Telescope could not be saved",MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
  void UpdateTelescopeState(bool busy){
   busy=busy||usbImportPicking;
   C("SavedTelescopeBox").IsEnabled=!busy;B("SaveTelescopeButton").IsEnabled=!busy;B("RenameTelescopeButton").IsEnabled=!busy&&SelectedScope!=null&&!string.IsNullOrEmpty(SelectedScope.Id);B("RebuildTelescopesButton").IsEnabled=!busy&&repo!=null;B("RefreshUsbButton").IsEnabled=!usbChecking&&!busy;
   var button=B("AutoUploadButton");button.Visibility=usbTelescopes.Count>0||activeUsb!=null?Visibility.Visible:Visibility.Collapsed;button.IsEnabled=!busy&&repo!=null&&usbTelescopes.Count>0;
   button.Content=usbTelescopes.Count==1?UsbImportLabel(usbTelescopes[0]):"Import connected telescope…";
   button.ToolTip=repo==null?"Choose a repository first.":"Choose folders or files to scan and import. Large selections ask for confirmation; originals stay on the telescope.";
  }
  async void RefreshUsb(){
   if(usbChecking||telescopesDisposed||!Window.IsLoaded)return;usbChecking=true;UpdateTelescopeState(RepositoryOperationBlocked);int generation=usbGeneration;string archive=repo==null?null:repo.Root;
   var profiles=settings.Telescopes.Select(p=>Util.Deserialize<TelescopeProfile>(Util.Serialize(p))).ToList();var token=usbDiscoveryCancel.Token;
   try{var result=await Task.Run(()=>{var volumes=UsbStorage.Volumes();return Tuple.Create(volumes,UsbTelescopeDiscovery.Discover(volumes,profiles,archive,token));});
    if(telescopesDisposed||generation!=usbGeneration)return;usbVolumes=result.Item1;usbTelescopes=result.Item2;
    SelectConnectedTelescope();
    L("UsbStatusLabel").Text=usbTelescopes.Count==0?"Connect a telescope by USB, or browse its capture folder and save the telescope.":usbTelescopes.Count+" USB telescope source"+(usbTelescopes.Count==1?"":"s")+" available.";
    if(activeUsb!=null&&cancel!=null&&!usbVolumes.Any(v=>string.Equals(v.Id,activeUsb.Volume.Id,StringComparison.OrdinalIgnoreCase))){cancellationMessage="USB telescope disconnected. Completed imports are retained; reconnect and resume.";cancel.Cancel();L("StatusLabel").Text="USB telescope disconnected. Canceling; completed imports are retained.";}
    var selected=SelectedScope;if(!RepositoryOperationBlocked&&!usbImportPicking&&selected!=null&&!string.IsNullOrEmpty(selected.VolumeId)&&string.IsNullOrEmpty(T("SourceBox").Text)){var connected=usbTelescopes.FirstOrDefault(t=>string.Equals(t.ProfileId,selected.Id,StringComparison.OrdinalIgnoreCase));if(connected!=null)T("SourceBox").Text=connected.Source;}
   }catch(OperationCanceledException){}catch(Exception e){if(!telescopesDisposed)L("UsbStatusLabel").Text="USB detection could not finish: "+e.Message;}
   finally{usbChecking=false;if(!telescopesDisposed){UpdateTelescopeState(RepositoryOperationBlocked);if(generation!=usbGeneration)QueueUsbRefresh();}}
  }
  void SelectConnectedTelescope(){
   if(RepositoryOperationBlocked||usbImportPicking||usbTelescopes.Count!=1)return;var device=usbTelescopes[0];var matching=UsbTelescopeDiscovery.MatchProfile(device,settings.Telescopes,settings.SelectedTelescope);
   if(matching!=null){device.ProfileId=matching.Id;if(SelectedScope==null||SelectedScope.Id!=matching.Id)ReloadScopes(matching.Id,true);}
  }
  void UploadUsbMenu(){
   if(RepositoryOperationBlocked||usbImportPicking||repo==null)return;if(usbTelescopes.Count==1){UploadUsb(usbTelescopes[0]);return;}
   var menu=ThemedMenu();foreach(var telescope in usbTelescopes){var selected=telescope;menu.Items.Add(FileAction(UsbImportLabel(selected),()=>UploadUsb(selected)));}menu.PlacementTarget=TopMenu("ImportMenu");menu.IsOpen=true;
  }
  string UsbImportLabel(UsbTelescope device){string label="Import from "+ImportSelection.TelescopeBrand(device.Make);if(usbTelescopes.Count(t=>t.Make==device.Make)>1)label+=" · "+(device.ProfileId??device.Volume.Root);return label+"…";}
  bool UsbAvailable(UsbTelescope device){return device.Volume!=null&&!string.IsNullOrEmpty(device.Volume.Id)&&Directory.Exists(device.Volume.Root)&&string.Equals(UsbStorage.Identity(device.Volume.Root),device.Volume.Id,StringComparison.OrdinalIgnoreCase);}
  async void UploadUsb(UsbTelescope telescope){
   if(RepositoryOperationBlocked||usbImportPicking||repo==null)return;((TabControl)Window.FindName("MainTabs")).SelectedIndex=1;
   TelescopeImportPicker picker;usbImportPicking=true;SetBusy(true);
   try{
    if(!UsbAvailable(telescope))throw new IOException("The telescope drive is unavailable. Reconnect it and refresh connected telescopes.");
    var profile=UsbTelescopeDiscovery.MatchProfile(telescope,settings.Telescopes,settings.SelectedTelescope);
    picker=new TelescopeImportPicker(Window,telescope,settings.Telescopes,profile);if(!picker.Show())return;
    var token=usbDiscoveryCancel.Token;int count=await Task.Run(()=>picker.Selection.CaptureCountUpTo(ImportSelection.LargeFileSelection,token));
    if(telescopesDisposed)return;
    if(count>=ImportSelection.LargeFileSelection&&!TelescopeImportConfirmation.Create(Window,picker.Selection,telescope.Make).Show())return;
    if(!UsbAvailable(telescope))throw new IOException("The telescope disconnected before import. Reconnect it and choose the selection again.");
    var candidate=Util.Deserialize<Settings>(Util.Serialize(settings));var selected=TelescopeProfiles.Save(candidate,picker.Profile.Id,picker.Profile.Model,picker.Profile.Camera,picker.Selection.SourceRoot,new[]{telescope.Volume});
    selected.SourceMake=telescope.Make;selected.LastImportSelection=picker.Selection.Paths.Select(p=>p.Substring(picker.Selection.SourceRoot.TrimEnd('\\','/').Length).TrimStart('\\','/')).ToList();
    candidate.LastSource=picker.Selection.SourceRoot;candidate.Telescope=selected.Id;candidate.Model=selected.Model;Directory.CreateDirectory(Path.GetDirectoryName(config));Util.AtomicText(config,Util.Serialize(candidate));settings=candidate;telescope.ProfileId=selected.Id;pendingUsb=null;ReloadScopes(selected.Id,true);T("SourceBox").Text=picker.Selection.SourceRoot;SaveSettings();
   }catch(OperationCanceledException){return;}catch(Exception e){MessageBox.Show(Window,e.Message,"Telescope import could not start",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
   finally{usbImportPicking=false;SetBusy(false);}
   ((CheckBox)Window.FindName("DeleteOriginalsCheck")).IsChecked=false;C("ImportSolveMode").SelectedItem="Off";C("ImportRotationMode").SelectedItem="Off";BeginLive(true);activeUsb=telescope;
   var selectedProfile=Util.Deserialize<TelescopeProfile>(Util.Serialize(picker.Profile));var selection=picker.Selection;var repository=repo;int workers=settings.CopyWorkers;bool ignoreFailed=settings.IgnoreFailed,ignoreRaster=settings.IgnoreRasterImports;AutoUploadResult result=null;
   Run(ct=>{result=UsbAutoUpload.Run(repository,selectedProfile,selection.SourceRoot,workers,ct,Progress,LiveFrame,p=>{plan=p;},()=>UsbAvailable(telescope),ignoreFailed:ignoreFailed,ignoreRaster:ignoreRaster,robustMatching:true,selection:selection);return result.Summary;},r=>{FilterImports();L("ScanLabel").Text=result.Summary;L("StatusLabel").Text=r;if(result.Import.Errors.Count+result.Import.Warnings.Count>0)ShowReport("USB import report",repo.LastReport);});
  }
  void RenameScope(){
   var profile=SelectedScope;if(RepositoryOperationBlocked||profile==null||string.IsNullOrEmpty(profile.Id))return;
   var d=new FormWindow(Window,"Rename telescope",570,370);d.Text("Rename "+profile.Id,true);d.Text("Update this saved telescope and all its indexed captures and archive paths in the selected repository. FITS bytes and acquisition sessions are preserved.");var name=d.Input("New telescope name",profile.Id);string newName=null;
   d.Accept("Rename",()=>{newName=name.Text.Trim();if(newName.Length==0){MessageBox.Show(d.Window,"Enter a telescope name.");return false;}var existing=TelescopeProfiles.Find(settings,newName);if(existing!=null&&!string.Equals(existing.Id,profile.Id,StringComparison.OrdinalIgnoreCase)){MessageBox.Show(d.Window,"That name is already saved. Choose a unique name.");return false;}return true;});if(!d.Show()||newName==profile.Id)return;
   SaveSettings();var candidate=Util.Deserialize<Settings>(Util.Serialize(settings));TelescopeProfiles.Rename(candidate,profile.Id,newName);
   string originalSettings=Util.Serialize(settings),renamedSettings=Util.Serialize(candidate);TelescopeRenameResult result=null;var repository=repo;cancellationMessage="Canceled. The telescope name and repository records are unchanged.";
   Run(ct=>{result=repository==null?new TelescopeRenameResult():repository.RenameTelescope(profile.Id,newName,ct,Progress,null,config,originalSettings,renamedSettings);if(repository==null)Util.AtomicText(config,renamedSettings);return "Renamed telescope to "+newName+"; "+result.Files+" repository files updated.";},r=>{settings=candidate;ReloadScopes(newName,true);usbGeneration++;RefreshUsb();L("StatusLabel").Text=r;if(!string.IsNullOrEmpty(result.Warning))ShowReport("Telescope rename",result.Warning);});
  }
  void SmokeSavedTelescopes(){
   var savedSettings=settings;var savedUsb=usbTelescopes;var savedPending=pendingUsb;var savedPlan=plan;int tab=((TabControl)Window.FindName("MainTabs")).SelectedIndex;string source=T("SourceBox").Text,id=T("TelescopeBox").Text,model=Convert.ToString(C("ModelBox").SelectedItem),camera=Convert.ToString(C("CameraBox").SelectedItem);
   try{
    ((TabControl)Window.FindName("MainTabs")).SelectedIndex=1;PumpPopupLayout();settings=new Settings();TelescopeProfiles.Initialize(settings);TelescopeProfiles.MergeRepository(settings,all);ReloadScopes("Dwarf-03",true);PumpPopupLayout();
    if(!PopupChildren<TextBlock>(C("SavedTelescopeBox")).Any(t=>t.Text=="Dwarf-03 · Dwarf 3"))throw new Exception("Saved telescope selection did not render its display name.");
    if(C("SavedTelescopeBox").Items.Count!=4||T("TelescopeBox").Text!="Dwarf-03"||Convert.ToString(C("ModelBox").SelectedItem)!="Dwarf 3")throw new Exception("Saved telescope dropdown did not recover repository profiles.");
    pendingUsb=new UsbTelescope{Volume=new UsbVolume{Root=@"E:\",Id="smoke-usb"},Source=@"E:\MyWorks",Make="Seestar"};C("SavedTelescopeBox").SelectedItem=TelescopeProfiles.Find(settings,"Seestar-01");
    if(T("SourceBox").Text!=@"E:\MyWorks")throw new Exception("Selecting an existing scope lost the USB setup source.");
    plan=new ImportPlan();T("TelescopeBox").Text="Edited scope";if(plan!=null)throw new Exception("Changed telescope left a stale scan plan.");
    usbTelescopes=new List<UsbTelescope>{pendingUsb};UpdateTelescopeState(true);if(B("AutoUploadButton").Visibility!=Visibility.Visible||B("AutoUploadButton").IsEnabled||C("SavedTelescopeBox").IsEnabled)throw new Exception("USB controls ignore busy state.");
    usbTelescopes.Clear();UpdateTelescopeState(false);if(B("AutoUploadButton").Visibility!=Visibility.Collapsed)throw new Exception("Disconnected USB upload action remains visible.");
   }finally{settings=savedSettings;usbTelescopes=savedUsb;pendingUsb=savedPending;ReloadScopes(settings.SelectedTelescope,false);T("SourceBox").Text=source;T("TelescopeBox").Text=id;C("ModelBox").SelectedItem=model;C("CameraBox").SelectedItem=camera;plan=savedPlan;UpdateTelescopeState(false);((TabControl)Window.FindName("MainTabs")).SelectedIndex=tab;}
  }
 }
}
