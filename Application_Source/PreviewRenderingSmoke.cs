using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
 public partial class MainUi {
  // Check the pixels WPF actually draws: transform-only tests miss layout clips.
  void SmokePreviewRendering(string output){
   var host=new Grid{Width=264,Height=520,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};var stage=new Grid();var image=new Image();stage.Children.Add(image);host.Children.Add(stage);var preview=new PreviewViewport(host,stage,image);
   var window=new Window{Owner=Window,Content=host,Width=600,Height=650,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
   int cases=0;try{window.Show();
    foreach(var dimensions in new[]{new[]{1280,720},new[]{720,1280},new[]{1024,1024},new[]{120,180}}){
     int width=dimensions[0],height=dimensions[1];var rgb=new byte[width*height*3];
     var colors=new[]{new byte[]{220,40,40},new byte[]{40,220,40},new byte[]{40,40,220},new byte[]{220,220,40}};
     for(int y=0;y<height;y++)for(int x=0;x<width;x++){var color=colors[(y<height/2?0:2)+(x<width/2?0:1)];int offset=(y*width+x)*3;Array.Copy(color,0,rgb,offset,3);}
     foreach(double dpi in new[]{96.0,144.0}){
      var source=BitmapSource.Create(width,height,dpi,dpi,PixelFormats.Rgb24,null,rgb,width*3);source.Freeze();preview.SetImage(source,true);PumpPopupLayout();
      foreach(var size in new[]{new[]{264.0,520.0},new[]{460.0,300.0}}){
       host.Width=size[0];host.Height=size[1];PumpPopupLayout();preview.Resize();PumpPopupLayout();preview.Fit();PumpPopupLayout();
       int w=(int)Math.Ceiling(stage.ActualWidth),h=(int)Math.Ceiling(stage.ActualHeight);if(w<1||h<1)throw new InvalidOperationException("Preview render fixture has no viewport.");
       // Capture the root, then crop the centred stage in its host coordinates.
       // Rendering a centred child directly retains its parent layout offset.
       var full=new RenderTargetBitmap((int)Math.Ceiling(host.ActualWidth),(int)Math.Ceiling(host.ActualHeight),96,96,PixelFormats.Pbgra32);full.Render(host);
       var origin=stage.TranslatePoint(new Point(),host);var rendered=new CroppedBitmap(full,new Int32Rect((int)Math.Round(origin.X),(int)Math.Round(origin.Y),w,h));var actual=new byte[w*h*4];rendered.CopyPixels(actual,w*4,0);
       int[] order=width>height?new[]{2,0,3,1}:new[]{0,1,2,3};
       for(int quadrant=0;quadrant<4;quadrant++){
        int x=(int)(w*(quadrant%2==0?0.2:0.8)),y=(int)(h*(quadrant<2?0.2:0.7)),offset=(y*w+x)*4;var expected=colors[order[quadrant]];
        if(Math.Abs(actual[offset+2]-expected[0])>12||Math.Abs(actual[offset+1]-expected[1])>12||Math.Abs(actual[offset]-expected[2])>12||actual[offset+3]<250)throw new InvalidOperationException("Preview image clipped or scaled incorrectly: "+width+"x"+height+", "+dpi+" DPI, viewport "+w+"x"+h+", quadrant "+quadrant+", actual RGBA "+actual[offset+2]+","+actual[offset+1]+","+actual[offset]+","+actual[offset+3]+".");
       }
       if(Math.Abs(image.ActualWidth-width)>0.001||Math.Abs(image.ActualHeight-height)>0.001)throw new InvalidOperationException("Preview layout truncated the untransformed bitmap.");
       preview.SmokeGestures();cases++;
       if(width==1280&&dpi==96&&size[0]==264){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(rendered));using(var stream=File.Create(Path.Combine(output,"AstroArchive_Preview_Render_Regression.png")))encoder.Save(stream);}
      }
     }
    }
   }finally{window.Close();}
   File.WriteAllText(Path.Combine(output,"preview-render-smoke.txt"),"PASS: "+cases+" rendered portrait/landscape/square/upscaled image cases across resize and bitmap DPI; four quadrants fill the viewport; zoom/pan/Fit controls operate on the full image.");
  }
 }
}
