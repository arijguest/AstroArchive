using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.IO;
using System.Reflection;
using AstroArchive.Installation;
namespace AstroArchive {
 public partial class MainUi {
  [DllImport("user32.dll",CharSet=CharSet.Auto)]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr parameter,IntPtr data);
  static void VerifyWindowIcon(Window window){
   if(!ReferenceEquals(window.Icon,ApplicationIcon.Image)||!ApplicationIcon.Image.IsFrozen)throw new Exception(window.Title+" does not use the shared application ICO.");
   var decoder=ApplicationIcon.Image.Decoder as IconBitmapDecoder;if(decoder==null||!decoder.Frames.Any(f=>f.PixelWidth==16)||!decoder.Frames.Any(f=>f.PixelWidth==32))throw new Exception("Application ICO lost its native small/large frames.");
   IntPtr handle=new WindowInteropHelper(window).Handle;
   // WM_GETICON queries the icons supplied to the native title bar / taskbar.
   if(handle==IntPtr.Zero||SendMessage(handle,0x007F,IntPtr.Zero,IntPtr.Zero)==IntPtr.Zero||SendMessage(handle,0x007F,new IntPtr(1),IntPtr.Zero)==IntPtr.Zero)throw new Exception(window.Title+" has no native small/large window icon.");
  }
  void SmokeWindowIcons(){
   if(ShellIdentity.ProcessId()!=ShellIdentity.AppId)throw new Exception("The process lacks a stable Windows taskbar identity.");
   var mainHandle=new WindowInteropHelper(Window).Handle;
   if(ShellIdentity.WindowProperty(mainHandle,5)!=ShellIdentity.AppId||ShellIdentity.WindowProperty(mainHandle,3)!=ShellIdentity.IconPath+",0")throw new Exception("Windows taskbar relaunch metadata does not use the branded stable icon.");
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AstroArchive.ico"))using(var output=new MemoryStream()){stream.CopyTo(output);if(InstallCore.Hash(output.ToArray())!=InstallCore.HashFile(ShellIdentity.IconPath))throw new Exception("Cached shell icon does not match the bundled logo.");}
   string root=Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));var record=InstallCore.Read(root);
   if(record!=null)foreach(string path in WindowsIntegration.ShortcutPaths(root).Where(p=>Path.GetFileName(p)!="Uninstall AstroArchive.lnk")){
    if(ShellIdentity.ShortcutId(path)!=ShellIdentity.AppId||WindowsIntegration.ShortcutValue(path,"IconLocation")!=ShellIdentity.IconPath+",0"||WindowsIntegration.ShortcutValue(path,"TargetPath")!=Path.Combine(root,record.ActiveDirectory,"Start.exe"))throw new Exception("Installed shortcut lacks the stable branded icon/identity: "+path);
   }
   VerifyWindowIcon(Window);var dialog=new FormWindow(Window,"Application icon smoke",480,360);dialog.CloseOnly();
   try{dialog.Window.Show();PumpPopupLayout();VerifyWindowIcon(dialog.Window);}finally{dialog.Window.Close();}
  }
 }
}
