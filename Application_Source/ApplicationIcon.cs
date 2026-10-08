using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public static class ApplicationIcon {
  static readonly Lazy<BitmapFrame> image=new Lazy<BitmapFrame>(Load);
  public static BitmapFrame Image{get{return image.Value;}}
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
