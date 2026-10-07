// Display-only image samples. No changes to the source pixels or metadata.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AstroArchive {
 public sealed class PreviewData {
  public int Width,Height,SourceWidth,SourceHeight,Channels;
  public double[] Pixels;
  public double Minimum=0,Maximum=1;
  public bool FlipY;
  public string Description;
  public static readonly string[] StretchModes={"Linear","Auto","Strong","Auto per channel"};
  public byte[] Render(string mode,CancellationToken ct) {
   int count=checked(Width*Height);if(Pixels==null||Pixels.Length!=count*Channels)throw new ArgumentException("Invalid preview samples.");
   bool linear=mode=="Linear",separate=mode=="Auto per channel";double[] black=new double[Channels],white=new double[Channels],mid=new double[Channels];
   for(int c=0;c<Channels;c++){
    ct.ThrowIfCancellationRequested();var sample=new List<double>();int stride=Math.Max(1,count/60000);
    for(int i=0;i<count;i+=stride){double value=separate?Pixels[i*Channels+c]:Enumerable.Range(0,Channels).Average(k=>Pixels[i*Channels+k]);if(!double.IsNaN(value)&&!double.IsInfinity(value))sample.Add(value);}
    sample.Sort();if(sample.Count==0){black[c]=0;white[c]=1;mid[c]=0.5;continue;}
    black[c]=linear?Minimum:sample[(int)((sample.Count-1)*0.001)];white[c]=linear?Maximum:sample[(int)((sample.Count-1)*0.9995)];
    if(!(white[c]>black[c]))white[c]=black[c]+Math.Max(1,Math.Abs(black[c])*0.001);
    double median=(sample[sample.Count/2]-black[c])/(white[c]-black[c]);double target=mode=="Strong"?0.35:0.22;
    mid[c]=linear?0.5:Math.Max(0.00001,Math.Min(0.99999,Midtone(target,Math.Max(0.000001,Math.Min(0.999999,median)))));
   }
   byte[] output=new byte[checked(count*3)];
   for(int y=0;y<Height;y++){ct.ThrowIfCancellationRequested();for(int x=0;x<Width;x++){int src=((FlipY?Height-1-y:y)*Width+x)*Channels,dst=(y*Width+x)*3;
    for(int k=0;k<3;k++){int c=Channels==1?0:k;double value=(Pixels[src+c]-black[c])/(white[c]-black[c]);if(double.IsNaN(value)||double.IsInfinity(value))value=0;value=Math.Max(0,Math.Min(1,value));output[dst+k]=(byte)Math.Round(255*(linear?value:Midtone(mid[c],value)));}
   }}return output;
  }
  static double Midtone(double m,double x){if(x<=0)return 0;if(x>=1)return 1;return (m-1)*x/((2*m-1)*x-m);}
 }
}
