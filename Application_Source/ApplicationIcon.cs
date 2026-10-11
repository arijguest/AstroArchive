using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public static class ApplicationIcon {
  // Frozen frames still retain a decoder whose size variants are dispatcher-bound.
  // WPF reads that decoder when it creates native icons, so each UI thread needs
  // its own ICO decoder (the splash and main window run on different threads).
  [ThreadStatic]static BitmapFrame image;
  public static BitmapFrame Image{get{if(image==null)image=Load();return image;}}
  static BitmapFrame Load(){
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AstroArchive.ico")){
    if(stream==null)throw new InvalidDataException("The application icon is missing.");
    // Keep the ICO decoder's size variants so WPF can choose the native small/large icons.
    var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
    foreach(var frame in decoder.Frames)frame.Freeze();return decoder.Frames[0];
   }
  }
 }
}
