using System;
using System.Linq;
using System.Threading;
namespace AstroArchive {
 public interface ISampledAssetReader {
  PreviewData Preview(Frame frame,string path,ImageDescriptor image,int index,CancellationToken ct);
 }
 // Accumulate display bins directly from decoded rows/bytes, without a full double buffer.
 internal sealed class SampledPreview {
  readonly PreviewData data;readonly int[] counts;readonly int step,offsetX,offsetY;readonly bool cfa;readonly string bayer;
  public SampledPreview(Frame frame,ImageDescriptor image){
   Assets.Dimensions(image.Width,image.Height,image.Channels);bayer=(frame.Bayer??"").ToUpperInvariant();cfa=image.Channels==1&&new[]{"RGGB","BGGR","GRBG","GBRG"}.Contains(bayer);
   step=Math.Max(cfa?2:1,(int)Math.Ceiling(Math.Max(image.Width,image.Height)/1400.0));if(cfa&&step%2!=0)step++;
   int width=(image.Width+step-1)/step,height=(image.Height+step-1)/step,channels=cfa||image.Channels>=3?3:1;
   data=new PreviewData{Width=width,Height=height,SourceWidth=image.Width,SourceHeight=image.Height,Channels=channels,Pixels=new double[checked(width*height*channels)],Description=frame.Format+" · "+(cfa?bayer+" colour":channels==3?"RGB":"mono")+" · "+image.Label};counts=new int[data.Pixels.Length];
   var header=new FitsHeader{Values=image.Headers??new System.Collections.Generic.Dictionary<string,string>()};offsetX=(int)(header.Number("XBAYROFF")??0);offsetY=(int)(header.Number("YBAYROFF")??0);
   if(image.Bitpix>0){data.Minimum=0;data.Maximum=Math.Pow(2,image.Bitpix)-1;}
  }
  public void Add(int x,int y,int channel,double value){
   if(channel>=data.Channels||double.IsNaN(value)||double.IsInfinity(value))return;
   int c=cfa?"RGB".IndexOf(bayer[((y+offsetY)&1)*2+((x+offsetX)&1)]):data.Channels==1?0:channel;
   int pixel=((y/step)*data.Width+x/step)*data.Channels+c;data.Pixels[pixel]+=value;counts[pixel]++;
  }
  public PreviewData Finish(Frame frame,string path,CancellationToken ct){
   for(int i=0;i<data.Pixels.Length;i++){if(i%65536==0)ct.ThrowIfCancellationRequested();data.Pixels[i]=counts[i]>0?data.Pixels[i]/counts[i]:double.NaN;}data.ApplyContext(frame,path);return data;
  }
 }
}
