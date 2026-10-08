// Display-only image samples. No changes to the source pixels or metadata.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AstroArchive {
 public sealed class PreviewData {
  public int Width,Height,SourceWidth,SourceHeight,Channels;
  public double[] Pixels;
  public double Minimum=0,Maximum=1;
  public bool FlipY;
  public string Description;
  public string Target,ObservationMode,Filter,DisplayMode;
  public bool SkipStretch {get{return ObservationTargets.Unstretched(Target,ObservationMode);}}
  public void ApplyContext(Frame frame,string path){
   if(frame!=null){if(!Catalog.IsAmbiguous(frame.Target))Target=frame.Target;if(!string.IsNullOrEmpty(frame.ObservationMode))ObservationMode=frame.ObservationMode;if(!string.IsNullOrEmpty(frame.Filter)&&frame.Filter!="Unknown")Filter=frame.Filter;}
   if(string.IsNullOrWhiteSpace(Filter)||Filter=="Unknown"){string hint=string.Join("/",(path??"").Replace('\\','/').Split('/').Reverse().Take(3).Reverse());var match=Regex.Match(hint,FilterPattern,RegexOptions.IgnoreCase);if(match.Success)Filter=match.Groups[1].Value;}
   // File/folder hints apply only when an explicit astronomical target/mode is absent.
   if(Catalog.IsAmbiguous(Target)&&(string.IsNullOrWhiteSpace(ObservationMode)||new[]{"Light","Science","Stack","Stacked"}.Any(value=>string.Equals(value,ObservationMode,StringComparison.OrdinalIgnoreCase)))){string hint=ObservationTargets.ModeFromPath(path);if(hint.Length>0)ObservationMode=hint;}
  }
  const string FilterPattern=@"(?:^|[^a-z0-9])(ir[ _-]*cut|lp|lpf|light[ _-]*pollution|dual[ _-]*band|duo[ _-]*band|l[ _-]*(enhance|extreme|ultimate)|uhc)(?:[^a-z0-9]|$)";
  public static bool FilterColourCompensation(string filter){return Regex.IsMatch(filter??"",FilterPattern,RegexOptions.IgnoreCase);}
  public static readonly string[] StretchModes={"Linear","Auto","Strong","Auto per channel"};
  // Decoded samples are read-only in the viewer. Each display gets its own
  // metadata/stretch state while sharing the bounded sample buffer.
  public PreviewData Copy(){return (PreviewData)MemberwiseClone();}
  public byte[] Render(string mode,CancellationToken ct) {
   int count=checked(Width*Height);if(Pixels==null||Pixels.Length!=count*Channels)throw new ArgumentException("Invalid preview samples.");
   if(Width<=0||Height<=0||(Channels!=1&&Channels!=3))throw new ArgumentException("Invalid preview geometry or channels.");
   bool linear=mode=="Linear"||SkipStretch,separate=mode=="Auto per channel";double[] black=new double[Channels],white=new double[Channels],mid=new double[Channels];
   if(linear){
    DisplayMode="Linear"+(SkipStretch?" · solar / planetary":"");
    for(int c=0;c<Channels;c++){black[c]=Minimum;white[c]=Maximum;mid[c]=0.5;if(!(white[c]>black[c]))white[c]=black[c]+Math.Max(1,Math.Abs(black[c])*0.001);}
    return RenderPixels(black,white,mid,true,ct);
   }
   // Sample each colour independently; linked statistics must not conceal a filter cast.
   var colours=Enumerable.Range(0,Channels).Select(c=>new List<double>()).ToArray();var linked=new List<double>();int stride=Math.Max(1,count/60000);
   for(int i=0;i<count;i+=stride){if(i%4096==0)ct.ThrowIfCancellationRequested();double sum=0;int valid=0;for(int c=0;c<Channels;c++){double value=Pixels[i*Channels+c];if(double.IsNaN(value)||double.IsInfinity(value))continue;colours[c].Add(value);sum+=value;valid++;}if(valid==Channels)linked.Add(sum/Channels);}
   foreach(var sample in colours){ct.ThrowIfCancellationRequested();sample.Sort();}linked.Sort();
   bool automatic=mode=="Auto"||mode=="Strong";
   if(!linear&&Channels==3&&automatic){
    var medians=colours.Select(sample=>sample.Count==0?0:sample[sample.Count/2]).ToArray();
    bool measurable=colours.All(sample=>sample.Count>1&&sample[(int)((sample.Count-1)*0.9995)]>sample[(int)((sample.Count-1)*0.001)]);
    bool severeCast=measurable&&medians.Min()>0&&medians.Max()/medians.Min()>3;
    if(measurable&&linked.Count>1){double low=linked[(int)((linked.Count-1)*0.001)],high=linked[(int)((linked.Count-1)*0.9995)];if(high>low)severeCast|=(medians.Count(value=>value<=low)>=2&&medians.Any(value=>value>=high))||(medians.Count(value=>value>=high)>=2&&medians.Any(value=>value<=low));}
    separate=FilterColourCompensation(Filter)||severeCast;
   }
   DisplayMode=linear?"Linear"+(SkipStretch?" · solar / planetary":""):mode+(separate&&automatic?" · colour compensated":"");
   for(int c=0;c<Channels;c++){
    ct.ThrowIfCancellationRequested();var sample=separate?colours[c]:linked;if(sample.Count==0){black[c]=0;white[c]=1;mid[c]=0.5;continue;}
    black[c]=linear?Minimum:sample[(int)((sample.Count-1)*0.001)];white[c]=linear?Maximum:sample[(int)((sample.Count-1)*0.9995)];
    if(!(white[c]>black[c]))white[c]=black[c]+Math.Max(1,Math.Abs(black[c])*0.001);
    double median=(sample[sample.Count/2]-black[c])/(white[c]-black[c]);double target=mode=="Strong"?0.35:0.22;
    mid[c]=linear?0.5:Math.Max(0.00001,Math.Min(0.99999,Midtone(target,Math.Max(0.000001,Math.Min(0.999999,median)))));
   }
   return RenderPixels(black,white,mid,false,ct);
  }
  byte[] RenderPixels(double[] black,double[] white,double[] mid,bool linear,CancellationToken ct){
   byte[] output=new byte[checked(Width*Height*3)];
   for(int y=0;y<Height;y++){ct.ThrowIfCancellationRequested();for(int x=0;x<Width;x++){int src=((FlipY?Height-1-y:y)*Width+x)*Channels,dst=(y*Width+x)*3;
    for(int k=0;k<3;k++){int c=Channels==1?0:k;double value=(Pixels[src+c]-black[c])/(white[c]-black[c]);if(double.IsNaN(value)||double.IsInfinity(value))value=0;value=Math.Max(0,Math.Min(1,value));output[dst+k]=(byte)Math.Round(255*(linear?value:Midtone(mid[c],value)));}
   }}return output;
  }
  static double Midtone(double m,double x){if(x<=0)return 0;if(x>=1)return 1;return (m-1)*x/((2*m-1)*x-m);}
 }
}
