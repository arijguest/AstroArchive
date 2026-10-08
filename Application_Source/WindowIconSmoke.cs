using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
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
   VerifyWindowIcon(Window);var dialog=new FormWindow(Window,"Application icon smoke",480,360);dialog.CloseOnly();
   try{dialog.Window.Show();PumpPopupLayout();VerifyWindowIcon(dialog.Window);}finally{dialog.Window.Close();}
  }
 }
}
