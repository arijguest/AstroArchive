using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AstroArchive.Installation;

namespace AstroArchive {
 public partial class MainUi {
  Action connectivityChanged;FileSystemWatcher connectivityWatch;bool connectivityDisposed;InformationCache informationCache;InformationCoordinator informationDownloads;
  const string HybridCopy="Essential information is included. Additional information downloads when you open a target and is saved for later.";
  const string OfflineCopy="Uses essential and saved information. Additional targets may have limited information.";
  void InitializeConnectivity(bool test){
   InformationMedia.Decode=bytes=>{if(!InformationHttp.Raster(bytes))return null;using(var stream=new MemoryStream(bytes)){var image=new System.Windows.Media.Imaging.BitmapImage();image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;image.DecodePixelWidth=640;image.StreamSource=stream;image.EndInit();if(image.PixelWidth>2048||image.PixelHeight>2048||(long)image.PixelWidth*image.PixelHeight>4*1024*1024)return null;image.Freeze();return image;}};
   var mode=test?ConnectivityMode.Offline:ConnectivityPolicy.Read();if(mode==ConnectivityMode.Pending&&!File.Exists(ConnectivityPolicy.ChoicePath))mode=settings.ConnectivityMode;
   if(!Enum.IsDefined(typeof(ConnectivityMode),mode))mode=ConnectivityMode.Pending;settings.ConnectivityMode=mode;ConnectivityPolicy.Apply(mode);
   informationCache=new InformationCache(Path.Combine(Path.GetDirectoryName(config),"information-cache")){LimitMiB=settings.InformationCacheMiB};informationDownloads=new InformationCoordinator(informationCache);
   if(test)return;
   if(mode!=ConnectivityMode.Pending)ConnectivityPolicy.Save(mode);
   connectivityWatch=new FileSystemWatcher(Path.GetDirectoryName(ConnectivityPolicy.ChoicePath),Path.GetFileName(ConnectivityPolicy.ChoicePath)){NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite};
   FileSystemEventHandler changed=(s,e)=>{if(connectivityDisposed||Window.Dispatcher.HasShutdownStarted)return;Window.Dispatcher.BeginInvoke(new Action(()=>{if(connectivityDisposed)return;var choice=ConnectivityPolicy.Read();if(choice==settings.ConnectivityMode)return;settings.ConnectivityMode=choice;ApplyConnectivity();try{SaveSettings();}catch{}}));};
   connectivityWatch.Changed+=changed;connectivityWatch.Created+=changed;connectivityWatch.Deleted+=changed;connectivityWatch.Renamed+=(s,e)=>changed(s,e);connectivityWatch.EnableRaisingEvents=true;
   Window.ContentRendered+=(s,e)=>{if(settings.ConnectivityMode==ConnectivityMode.Pending)Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(ChooseInitialConnectivity));};
  }
  void ChooseInitialConnectivity(){
   if(closing||settings.ConnectivityMode!=ConnectivityMode.Pending)return;var dialog=new FormWindow(Window,"Online access",640,500);
   dialog.Text("Choose how AstroArchive gets information",true);dialog.Text("Hybrid (recommended)",true);dialog.Text(HybridCopy);dialog.Text("Offline",true);dialog.Text(OfflineCopy);
   dialog.Text("You can change this in Preferences > General > Online access. Local telescope connections remain available.");
   Action<ConnectivityMode> choose=mode=>{ConnectivityPolicy.Save(mode);settings.ConnectivityMode=mode;SaveSettings();ApplyConnectivity();dialog.Window.Close();};
   var hybrid=dialog.Button("Use Hybrid (recommended)",()=>choose(ConnectivityMode.Hybrid));hybrid.IsDefault=true;dialog.Button("Use Offline",()=>choose(ConnectivityMode.Offline));dialog.Show();
  }
  void ApplyConnectivity(){
   ConnectivityPolicy.Apply(settings.ConnectivityMode);informationCache.LimitMiB=settings.InformationCacheMiB;
   if(!ConnectivityPolicy.AllowsInternet){informationDownloads.Cancel();if(releaseDownloadCancel!=null)releaseDownloadCancel.Cancel();}
   ApplyReleaseMonitoringPreference();if(connectivityChanged!=null)connectivityChanged();
   foreach(var state in new[]{libraryInfo,editedInfo})if(state.View!=null){state.Refreshing=false;state.View.InternetAvailable=ConnectivityPolicy.AllowsInternet;if(state.Card!=null)state.View.UpdateCard(state.Card,state.Context,state.Members,"YOUR COMETS",state.Back);if(ConnectivityPolicy.AllowsInternet&&state.LocalCard!=null)EnrichTargetInformation(state,false);}
  }
  bool InternetAction(){if(ConnectivityPolicy.AllowsInternet)return true;LocalizedMessage.Show(Window,"Choose Hybrid in Preferences > General > Online access to reconnect.","Online access",MessageBoxButton.OK,MessageBoxImage.Information);return false;}
  sealed class ConnectivityFields {public ComboBox Mode,Limit;public bool Changed,Updating;public ConnectivityMode Original;}
  ConnectivityFields AddConnectivityPreferences(FormWindow dialog){
   dialog.Text("Online access",true);var fields=new ConnectivityFields{Original=settings.ConnectivityMode};fields.Mode=dialog.Select("Connectivity",new[]{"Hybrid (recommended)","Offline"},settings.ConnectivityMode==ConnectivityMode.Hybrid?"Hybrid (recommended)":"Offline");
   fields.Mode.SelectionChanged+=(s,e)=>{if(!fields.Updating)fields.Changed=true;};Action sync=()=>{if(fields.Changed)return;fields.Updating=true;fields.Mode.SelectedIndex=settings.ConnectivityMode==ConnectivityMode.Hybrid?0:1;fields.Original=settings.ConnectivityMode;fields.Updating=false;};connectivityChanged+=sync;dialog.Window.Closed+=(s,e)=>connectivityChanged-=sync;
   dialog.Text(HybridCopy);dialog.Text(OfflineCopy);fields.Limit=dialog.Select("Downloaded information limit (MiB)",new[]{"10","50","100","250","500"},Math.Max(10,Math.Min(500,settings.InformationCacheMiB)).ToString());
   var usage=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8)};Theme.Bind(usage,TextBlock.ForegroundProperty,"Muted");dialog.Add(usage);
   Action show=()=>{try{usage.Text=Localization.F("Downloaded information: {0} MiB",(informationCache.Usage/1048576.0).ToString("0.0"));}catch(IOException){usage.Text=Localization.T("Downloaded information is unavailable.");}};show();
   dialog.Button("Clear downloaded information",()=>{try{informationDownloads.Clear();show();foreach(var state in new[]{libraryInfo,editedInfo})if(state.LocalCard!=null){state.Generation++;state.Card=state.LocalCard;state.View.UpdateCard(state.Card,state.Context,state.Members,"YOUR COMETS",state.Back);}}catch(Exception error){LocalizedMessage.Show(dialog.Window,error.Message,"Downloaded information",MessageBoxButton.OK,MessageBoxImage.Warning);}});
   return fields;
  }
 }
}
