using System;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 // Native-size display pixels are decoded separately and never enter the sidebar cache.
 public static class FullResolutionPreview {
  public static PreviewData Create(Frame frame,PixelImage image,ImageDescriptor descriptor,string path,CancellationToken ct){
   ct.ThrowIfCancellationRequested();string bayer=(frame.Bayer??"").ToUpperInvariant();
   var header=new FitsHeader{Values=descriptor.Headers??new System.Collections.Generic.Dictionary<string,string>()};
   if(bayer.Length==0)bayer=(string.IsNullOrEmpty(descriptor.Color)?header.Get("BAYERPAT"):descriptor.Color).ToUpperInvariant();
   bool cfa=image.Channels==1&&new[]{"RGGB","BGGR","GRBG","GBRG"}.Contains(bayer);int channels=cfa||image.Channels>=3?3:1;
   var result=new PreviewData{Width=image.Width,Height=image.Height,SourceWidth=image.Width,SourceHeight=image.Height,Channels=channels,FlipY=frame.Format=="FITS",Description=frame.Format+" · full resolution · "+(cfa?bayer+" colour":channels==3?"RGB":"mono")};
   if(descriptor.Bitpix>0){double low=0,high=Math.Pow(2,descriptor.Bitpix)-1;if(frame.Format=="FITS"){low=descriptor.Bitpix==8?0:-Math.Pow(2,descriptor.Bitpix-1);high=descriptor.Bitpix==8?255:Math.Pow(2,descriptor.Bitpix-1)-1;}double scale=frame.Format=="FITS"?header.Number("BSCALE")??1:1,zero=frame.Format=="FITS"?header.Number("BZERO")??0:0;result.Minimum=Math.Min(low*scale+zero,high*scale+zero);result.Maximum=Math.Max(low*scale+zero,high*scale+zero);}
   int plane=checked(image.Width*image.Height);int offsetX=(int)(header.Number("XBAYROFF")??0),offsetY=(int)(header.Number("YBAYROFF")??0);
   if(!cfa){result.Pixels=image.Pixels;result.Planar=true;result.StoredChannels=image.Channels;}
   else{result.Pixels=new double[checked(plane*channels)];for(int y=0;y<image.Height;y++){ct.ThrowIfCancellationRequested();for(int x=0;x<image.Width;x++)for(int c=0;c<channels;c++)result.Pixels[(y*image.Width+x)*channels+c]=Colour(image,x,y,c,bayer,offsetX,offsetY);}}
   result.Target=header.Get("OBJECT","OBJNAME","TARGET","TARGNAME");result.Filter=header.Get("FILTER","FILTERID","FILTNAME");result.ObservationMode=header.Get("OBSMODE","CAPMODE","SHOOTMOD","MODE","IMAGETYP");result.ApplyContext(frame,path);return result;
  }
  static double Colour(PixelImage image,int x,int y,int channel,string bayer,int offsetX,int offsetY){
   char colour="RGB"[channel];double own=image.Pixels[y*image.Width+x];if(bayer[((y+offsetY)&1)*2+((x+offsetX)&1)]==colour&&!double.IsNaN(own)&&!double.IsInfinity(own))return own;
   double sum=0;int count=0;for(int row=Math.Max(0,y-1);row<=Math.Min(image.Height-1,y+1);row++)for(int col=Math.Max(0,x-1);col<=Math.Min(image.Width-1,x+1);col++){
    if(bayer[((row+offsetY)&1)*2+((col+offsetX)&1)]!=colour)continue;double value=image.Pixels[row*image.Width+col];if(double.IsNaN(value)||double.IsInfinity(value))continue;sum+=value;count++;
   }return count>0?sum/count:double.NaN;
  }
 }
}
