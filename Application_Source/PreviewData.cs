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
  // Full-resolution readers can retain their planar buffer, including an ignored alpha plane.
  public bool Planar;public int StoredChannels;
  sealed class Statistics {public double[] Pixels;public int Width,Height,Channels,StoredChannels;public bool Planar;public SampleSummary[] Colours;public SampleSummary Linked;public bool SevereCast;}
  sealed class SampleSummary {public int Count;public double Low,High,Median;}
  sealed class StatisticsCache {public readonly object Gate=new object();public Statistics Value;public int Builds;}
  readonly StatisticsCache statistics=new StatisticsCache();
  internal int StatisticsBuilds{get{lock(statistics.Gate)return statistics.Builds;}}
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
  public double Sample(int pixel,int channel){return Pixels[Planar?channel*checked(Width*Height)+pixel:pixel*Channels+channel];}
  static SampleSummary Summarize(List<double> values){return values.Count==0?new SampleSummary():new SampleSummary{Count=values.Count,Low=values[(int)((values.Count-1)*0.001)],High=values[(int)((values.Count-1)*0.9995)],Median=values[values.Count/2]};}
  Statistics GetStatistics(CancellationToken ct){
   lock(statistics.Gate){
    ct.ThrowIfCancellationRequested();var cached=statistics.Value;
    if(cached!=null&&ReferenceEquals(cached.Pixels,Pixels)&&cached.Width==Width&&cached.Height==Height&&cached.Channels==Channels&&cached.StoredChannels==StoredChannels&&cached.Planar==Planar)return cached;
    var colours=Enumerable.Range(0,Channels).Select(c=>new List<double>()).ToArray();var linked=new List<double>();int count=checked(Width*Height),stride=Math.Max(1,count/60000);
    for(int i=0;i<count;i+=stride){ct.ThrowIfCancellationRequested();double sum=0;int valid=0;for(int c=0;c<Channels;c++){double value=Sample(i,c);if(double.IsNaN(value)||double.IsInfinity(value))continue;colours[c].Add(value);sum+=value;valid++;}if(valid==Channels)linked.Add(sum/Channels);}
    foreach(var sample in colours){ct.ThrowIfCancellationRequested();sample.Sort();}linked.Sort();
    var result=new Statistics{Pixels=Pixels,Width=Width,Height=Height,Channels=Channels,StoredChannels=StoredChannels,Planar=Planar,Colours=colours.Select(Summarize).ToArray(),Linked=Summarize(linked)};
    if(Channels==3){var medians=result.Colours.Select(s=>s.Median).ToArray();bool measurable=result.Colours.All(s=>s.Count>1&&s.High>s.Low);result.SevereCast=measurable&&medians.Min()>0&&medians.Max()/medians.Min()>3;
     if(measurable&&result.Linked.Count>1&&result.Linked.High>result.Linked.Low){double low=result.Linked.Low,high=result.Linked.High;result.SevereCast|=(medians.Count(value=>value<=low)>=2&&medians.Any(value=>value>=high))||(medians.Count(value=>value>=high)>=2&&medians.Any(value=>value<=low));}
    }
    ct.ThrowIfCancellationRequested();statistics.Value=result;statistics.Builds++;return result;
   }
  }
  public byte[] Render(string mode,CancellationToken ct) {
   ct.ThrowIfCancellationRequested();int count=checked(Width*Height);int stored=Planar&&StoredChannels>0?StoredChannels:Channels;if(Pixels==null||stored<Channels||Pixels.Length!=checked(count*stored))throw new ArgumentException("Invalid preview samples.");
   if(Width<=0||Height<=0||(Channels!=1&&Channels!=3))throw new ArgumentException("Invalid preview geometry or channels.");
   bool linear=mode=="Linear"||SkipStretch,separate=mode=="Auto per channel";double[] black=new double[Channels],white=new double[Channels],mid=new double[Channels];
   if(linear){
    DisplayMode="Linear"+(SkipStretch?" · solar / planetary":"");
    for(int c=0;c<Channels;c++){black[c]=Minimum;white[c]=Maximum;mid[c]=0.5;if(!(white[c]>black[c]))white[c]=black[c]+Math.Max(1,Math.Abs(black[c])*0.001);}
    return RenderPixels(black,white,mid,true,ct);
   }
   // Cache sample statistics only; current display context still chooses the stretch.
   var stats=GetStatistics(ct);
   bool automatic=mode=="Auto"||mode=="Strong";
   if(!linear&&Channels==3&&automatic){
    separate=FilterColourCompensation(Filter)||stats.SevereCast;
   }
   DisplayMode=linear?"Linear"+(SkipStretch?" · solar / planetary":""):mode+(separate&&automatic?" · colour compensated":"");
   for(int c=0;c<Channels;c++){
    ct.ThrowIfCancellationRequested();var sample=separate?stats.Colours[c]:stats.Linked;if(sample.Count==0){black[c]=0;white[c]=1;mid[c]=0.5;continue;}
    black[c]=sample.Low;white[c]=sample.High;
    if(!(white[c]>black[c]))white[c]=black[c]+Math.Max(1,Math.Abs(black[c])*0.001);
    double median=(sample.Median-black[c])/(white[c]-black[c]);double target=mode=="Strong"?0.35:0.22;
    mid[c]=linear?0.5:Math.Max(0.00001,Math.Min(0.99999,Midtone(target,Math.Max(0.000001,Math.Min(0.999999,median)))));
   }
   return RenderPixels(black,white,mid,false,ct);
  }
  byte[] RenderPixels(double[] black,double[] white,double[] mid,bool linear,CancellationToken ct){
   byte[] output=new byte[checked(Width*Height*3)];
   int plane=checked(Width*Height);
   for(int y=0;y<Height;y++){ct.ThrowIfCancellationRequested();for(int x=0;x<Width;x++){int pixel=(FlipY?Height-1-y:y)*Width+x,dst=(y*Width+x)*3;
    if(Channels==1){byte value=DisplayByte(Pixels[pixel],black[0],white[0],mid[0],linear);output[dst]=value;output[dst+1]=value;output[dst+2]=value;}
    else for(int c=0;c<3;c++)output[dst+c]=DisplayByte(Pixels[Planar?c*plane+pixel:pixel*3+c],black[c],white[c],mid[c],linear);
   }}return output;
  }
  static byte DisplayByte(double sample,double black,double white,double mid,bool linear){double value=(sample-black)/(white-black);if(double.IsNaN(value)||double.IsInfinity(value))value=0;value=Math.Max(0,Math.Min(1,value));return (byte)Math.Round(255*(linear?value:Midtone(mid,value)));}
  static double Midtone(double m,double x){if(x<=0)return 0;if(x>=1)return 1;return (m-1)*x/((2*m-1)*x-m);}
 }
}
