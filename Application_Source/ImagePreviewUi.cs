using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  CancellationTokenSource detachedPreviewCancel;
  ImagePreviewWindow comparisonWindow;

  void CancelDetachedPreview(){
   if(detachedPreviewCancel!=null){detachedPreviewCancel.Cancel();detachedPreviewCancel=null;}
   if(comparisonWindow!=null)comparisonWindow.Close();
  }

  async void PreviewImage(Frame frame){
   if(repo==null||closing||metadataPreviewSuspended||frame==null)return;
   var repository=repo;
   if(!MediaFiles.Motion(frame.RelativePath??frame.OriginalName??"")){
    var rows=G("FramesGrid").Items.OfType<Frame>().Where(f=>!MediaFiles.Motion(f.RelativePath??f.OriginalName??"")).ToList();
    int current=rows.FindIndex(f=>f==frame||!string.IsNullOrEmpty(frame.Hash)&&f.Hash==frame.Hash);
    if(current<0){rows=new List<Frame>{frame};current=0;}
    ComparePreviews(rows,current,f=>new ComparisonCapture{
     Name=f.OriginalName,Path=repository.FilePath(f),ArchiveFrame=f.Clone(),Target=f.Target,Width=f.Width,Height=f.Height
    });
    return;
   }

   CancelDetachedPreview();
   var cancellation=detachedPreviewCancel=new CancellationTokenSource();
   var token=cancellation.Token;
   try{
    string path=repository.FilePath(frame);
    await Task.Run(()=>CheckPreviewFile(path,frame,token),token);
    if(!closing&&repo==repository&&!token.IsCancellationRequested)new ImagePreviewWindow(Window,frame.OriginalName,path).Show();
   }catch(OperationCanceledException){}
   catch(Exception error){if(!closing)PreviewFailure(frame.OriginalName,error);}
   finally{
    if(ReferenceEquals(detachedPreviewCancel,cancellation))detachedPreviewCancel=null;
    cancellation.Dispose();
   }
  }

  void PreviewFailure(string name,Exception error){
   var entry=AddActivity("Preview unavailable",false);entry.Status=name;entry.Report=error.Message;
   entry.Failed=entry.NeedsReview=true;NotifyActivity(entry);
  }

  sealed class ComparisonCapture {
   public string Name,Path,Target;
   public Frame ArchiveFrame;
   public int Width,Height;
  }

  sealed class ComparisonImage {
   public PreviewData Data;
   public FileStamp Stamp;
   public SkyGeometry Sky;
   public bool Native;
  }

  static FileStamp CheckPreviewFile(string path,Frame capture,CancellationToken token){
   token.ThrowIfCancellationRequested();
   var before=FileStamp.Read(path);
   if(capture!=null&&!before.VerifiedUnchanged(capture.RepositoryStamp)&&Util.Hash(path,token)!=capture.Hash)
    throw new InvalidDataException("The archived file changed. Refresh before previewing it.");
   return before;
  }

  void ComparePreviews<T>(List<T> items,int first,Func<T,ComparisonCapture> captureAt){
   CancelDetachedPreview();
   var repository=repo;
   ComparisonCapture current;
   try{current=captureAt(items[first]);}catch(Exception error){PreviewFailure("",error);return;}
   var popup=comparisonWindow=new ImagePreviewWindow(Window,current.Name,current.Width,current.Height,null);
   int index=first,loadedIndex=-1;
   var stretch=ScientificPreview(current.Path)?settings.PreviewStretch??"Auto per channel":"Linear";
   CancellationTokenSource pending=null;
   ComparisonImage loaded=null;
   bool closed=false,updatingControls=false;
   popup.Closed+=(s,e)=>{closed=true;if(pending!=null)pending.Cancel();if(comparisonWindow==popup)comparisonWindow=null;};
   AttachTargets(popup,()=>{
    if(loaded==null)return null;
    string format=current.ArchiveFrame!=null?current.ArchiveFrame.Format:current.Path.EndsWith(".xisf",StringComparison.OrdinalIgnoreCase)?"XISF":null;
    bool flip=TargetOverlay.DisplayFlip(loaded.Data.FlipY,loaded.Sky,format);
    if(current.ArchiveFrame!=null)return TargetOverlay.For(current.ArchiveFrame,flip);
    var overlay=TargetOverlay.For(loaded.Sky,current.Target,flip);
    if(loaded.Sky==null)overlay.Note="This image has no plate solution in its header";
    return overlay;
   });

   var controls=popup.ComparisonControls(stretch);
   Func<int,bool,Task> load=async(next,native)=>{
    if(closed||repo!=repository||metadataPreviewSuspended||next<0||next>=items.Count)return;
    if(pending!=null)pending.Cancel();
    var cancellation=pending=new CancellationTokenSource();
    var token=cancellation.Token;
    bool same=next==loadedIndex;
    var previous=same?loaded:null;
    if(!same){loaded=null;popup.RefreshTargets();}
    try{
     index=next;current=captureAt(items[next]);var capture=current;
     string mode=ScientificPreview(capture.Path)?Convert.ToString(controls.Stretch.SelectedItem):"Linear";
     popup.Title="Preview - "+capture.Name;
     controls.Previous.IsEnabled=index>0;controls.Next.IsEnabled=index+1<items.Count;
     controls.Stretch.IsEnabled=ScientificPreview(capture.Path);
     updatingControls=true;controls.Native.IsChecked=native;updatingControls=false;
     controls.Status.Text=Localization.T(native?"Loading native detail…":"Loading sampled preview…");
     ComparisonImage image=null;BitmapSource bitmap=null;
     await previewDecodeGate.WaitAsync(token);
     try{
      await Task.Run(()=>{
       var before=CheckPreviewFile(capture.Path,capture.ArchiveFrame,token);
       if(previous!=null&&previous.Native==native&&before.VerifiedUnchanged(previous.Stamp))image=previous;
       else{
        var data=native?DecodeFullPreview(capture.Path,token,capture.ArchiveFrame):
         previewCache.Get(capture.Path,capture.ArchiveFrame,()=>DecodePreview(capture.Path,token,capture.ArchiveFrame),token);
        data.ApplyContext(capture.ArchiveFrame,capture.Path);
        SkyGeometry sky=capture.ArchiveFrame==null?null:capture.ArchiveFrame.Sky;
        if(capture.ArchiveFrame==null)try{sky=HeaderSky(capture.Path);}catch(IOException){}catch(InvalidDataException){}catch(NotSupportedException){}
        image=new ComparisonImage{Data=data,Native=native,Stamp=before,Sky=sky};
       }
       bitmap=RenderPreviewBitmap(image.Data,mode,token);
       if(!before.ContentSame(FileStamp.Read(capture.Path)))throw new IOException("The image changed while its preview was loading.");
      },token);
     }finally{previewDecodeGate.Release();}
     if(closed||repo!=repository||token.IsCancellationRequested)return;
     loaded=image;loadedIndex=index;
     bool keep=same||controls.KeepView.IsChecked==true;
     popup.Viewport.SetImage(bitmap,!keep,keep);
     popup.RefreshTargets();
     controls.Status.Text=(index+1)+" / "+items.Count+" · "+Localization.T(native?"Native detail":"Sampled preview");
     if(!native&&items.Count>1){
      int neighbour=index+1<items.Count?index+1:index-1;
      await Task.Delay(220,token);
      if(!await previewDecodeGate.WaitAsync(0,token))return;
      try{
       await Task.Run(()=>{
        var adjacent=captureAt(items[neighbour]);string path=adjacent.Path;
        if(new DriveInfo(System.IO.Path.GetPathRoot(path)).DriveType!=DriveType.Fixed||FileStamp.Read(path).Cloud)return;
        var stamp=CheckPreviewFile(path,adjacent.ArchiveFrame,token);
        if(!stamp.Reliable)return;
        previewCache.Get(path,adjacent.ArchiveFrame,()=>{
         var data=DecodePreview(path,token,adjacent.ArchiveFrame);
         if(!stamp.ContentSame(FileStamp.Read(path)))throw new IOException("Adjacent image changed.");
         return data;
        },token);
       },token);
      }catch(IOException){}catch(UnauthorizedAccessException){}catch(InvalidDataException){}catch(NotSupportedException){}catch(ArgumentException){}
      finally{previewDecodeGate.Release();}
     }
    }catch(OperationCanceledException){}
    catch(Exception error){if(!closed&&!token.IsCancellationRequested){controls.Status.Text=error.Message;popup.Viewport.SetImage(null,true);}}
    finally{
     if(ReferenceEquals(pending,cancellation))pending=null;
     cancellation.Dispose();
    }
   };

   controls.Previous.Click+=async(s,e)=>await load(index-1,false);
   controls.Next.Click+=async(s,e)=>await load(index+1,false);
   RoutedEventHandler detail=async(s,e)=>{if(!updatingControls)await load(index,controls.Native.IsChecked==true);};
   controls.Native.Checked+=detail;controls.Native.Unchecked+=detail;
   controls.Stretch.SelectionChanged+=async(s,e)=>await load(index,controls.Native.IsChecked==true);
   popup.Loaded+=async(s,e)=>await load(first,false);
   popup.Show();
  }

 }
 public class ImagePreviewWindow:Window {
  readonly PreviewViewport preview;MotionPreview motion;
  public ImagePreviewWindow(Window owner,string filename,string path):this(owner,filename,480,640,new byte[480*640*3]){
   var host=(Grid)Content;var message=new TextBlock{Text="Loading preview…",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12)};host.Children.Add(message);
   Loaded+=(s,e)=>{motion=new MotionPreview(preview,path,info=>message.Visibility=Visibility.Collapsed,error=>{message.Text=error;message.Visibility=Visibility.Visible;},true);motion.Start();};Closed+=(s,e)=>{if(motion!=null)motion.Dispose();};
  }
  public ImagePreviewWindow(Window owner,string filename,int width,int height,byte[] pixels){
   Owner=owner;Icon=ApplicationIcon.Image;Title="Preview - "+filename;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;FontFamily=owner.FontFamily;FontSize=13;Resources.MergedDictionaries.Add(owner.Resources);Theme.Bind(this,Control.BackgroundProperty,"Canvas");Theme.Bind(this,Control.ForegroundProperty,"Text");
   // Reserve the native window chrome, then size the client area to the portrait image.
   var geometry=new PreviewGeometry(width>0?width:640,height>0?height:480,0);var work=SystemParameters.WorkArea;double clientWidth,clientHeight;geometry.Frame(Math.Min(1100,work.Width-64),Math.Max(1,Math.Min(760,work.Height-100)-PreviewViewport.ToolbarSpace),out clientWidth,out clientHeight);
   Width=clientWidth+2*SystemParameters.ResizeFrameVerticalBorderWidth+16;Height=clientHeight+PreviewViewport.ToolbarSpace+SystemParameters.CaptionHeight+2*SystemParameters.ResizeFrameHorizontalBorderHeight+16;MinWidth=Math.Min(Width,260);MinHeight=Math.Min(Height,360);
   var host=new Grid{Margin=new Thickness(8)};var viewport=new Grid();var image=new Image();viewport.Children.Add(image);host.Children.Add(viewport);Content=host;preview=new PreviewViewport(host,viewport,image,null,false,true);
   if(pixels==null)preview.BeginLoading(width,height);
   else{var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,pixels,width*3);bitmap.Freeze();preview.SetImage(bitmap,true);}
   Loaded+=(s,e)=>preview.Resize();PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.F){preview.Fit();e.Handled=true;}};
  }
  internal sealed class CompareControls {
   public Button Previous,Next;
   public CheckBox Native,KeepView;
   public ComboBox Stretch;
   public TextBlock Status;
  }
  internal CompareControls ComparisonControls(string stretch){
   Width=Math.Max(Width,Math.Min(620,SystemParameters.WorkArea.Width-40));
   var host=(Grid)Content;Content=null;
   var layout=new DockPanel();
   var bar=new WrapPanel{Margin=new Thickness(8,8,8,0)};
   DockPanel.SetDock(bar,Dock.Top);layout.Children.Add(bar);layout.Children.Add(host);Content=layout;
   var previous=new Button{Content=Localization.T("Previous"),Margin=new Thickness(0,0,6,0)};
   var next=new Button{Content=Localization.T("Next"),Margin=new Thickness(0,0,10,0)};
   var native=new CheckBox{Content=Localization.T("Native detail"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};
   var keep=new CheckBox{Content=Localization.T("Keep zoom"),IsChecked=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};
   var modes=new ComboBox{ItemsSource=PreviewData.StretchModes,SelectedItem=PreviewData.StretchModes.Contains(stretch)?stretch:"Auto per channel",MinWidth=90};
   UiHelp.Hint(modes,"Display stretch");
   var status=new TextBlock{Margin=new Thickness(8,4,0,2),TextWrapping=TextWrapping.Wrap};
   foreach(var control in new FrameworkElement[]{previous,next,native,keep,modes,status})bar.Children.Add(control);
   return new CompareControls{Previous=previous,Next=next,Native=native,KeepView=keep,Stretch=modes,Status=status};
  }
  Func<TargetOverlayResult> targetOverlay;Func<bool> targetsOn;
  // The overlay is computed by the owner, which also owns the shared on/off setting.
  public void EnableTargets(Func<TargetOverlayResult> overlay,Func<bool> on,Action toggle){targetOverlay=overlay;targetsOn=on;preview.ShowTargetToggle(()=>{toggle();RefreshTargets();});RefreshTargets();}
  public void RefreshTargets(){if(targetOverlay==null)return;bool on=targetsOn();preview.SetTargetToggle(on);var result=on?targetOverlay():null;if(result==null)preview.SetTargets(null,null);else preview.SetTargets(result.Marks,result.Note);}
  public PreviewViewport Viewport{get{return preview;}}
  public void SmokeGestures(){preview.SmokeGestures();preview.SmokeRotation();}
 }
}
