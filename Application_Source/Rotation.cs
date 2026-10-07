// Distributed star detection, triangle matching and robust similarity fitting.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace AstroArchive {
 public struct Star {public double X,Y,Flux;public Star(double x,double y,double f){X=x;Y=y;Flux=f;}}
 public class Triangle {public double A,B;public int I,J,K;public int Sign;}
 public class Transform {public double C,S,Tx,Ty;public Star Apply(Star p){return new Star(C*p.X-S*p.Y+Tx,S*p.X+C*p.Y+Ty,0);} }
 public static class Rotation {
  public static double Median(IEnumerable<double> a){double[] s=a.OrderBy(x=>x).ToArray();if(s.Length==0)return 0;return s.Length%2==0?(s[s.Length/2]+s[s.Length/2-1])/2:s[s.Length/2];}
  public static List<Star> Detect(FitsImage image,int maximum=100){
   int w=image.Width,h=image.Height;double[] p=image.Pixels;var found=new List<Star>();
   for(int by=0;by<h;by+=64)for(int bx=0;bx<w;bx+=64){var sample=new List<double>();for(int y=by;y<Math.Min(h,by+64);y+=2)for(int x=bx;x<Math.Min(w,bx+64);x+=2)if(!double.IsNaN(p[y*w+x]))sample.Add(p[y*w+x]);if(sample.Count<12)continue;
    double bg=Median(sample),noise=1.4826*Median(sample.Select(v=>Math.Abs(v-bg)));if(noise<1e-10)noise=Math.Max(1e-8,Math.Abs(bg)*1e-8);double threshold=bg+6*noise;
    for(int y=Math.Max(4,by);y<Math.Min(h-4,by+64);y++)for(int x=Math.Max(4,bx);x<Math.Min(w-4,bx+64);x++){
     double peak=p[y*w+x];if(double.IsNaN(peak)||peak<threshold)continue;bool local=true;for(int dy=-2;dy<=2&&local;dy++)for(int dx=-2;dx<=2;dx++){if(dx==0&&dy==0)continue;double v=p[(y+dy)*w+x+dx];if(v>peak||(v==peak&&(dy<0||(dy==0&&dx<0)))){local=false;break;}}
     if(!local)continue;double flux=0,sx=0,sy=0;int support=0;
     for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++){double v=Math.Max(0,p[(y+dy)*w+x+dx]-bg-1.5*noise);if(double.IsNaN(v))continue;flux+=v;sx+=v*(x+dx);sy+=v*(y+dy);if(v>(peak-bg)*0.15)support++;}
     if(flux<=0||support<3)continue;double cx=sx/flux,cy=sy/flux,mxx=0,myy=0,mxy=0;
     for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++){double v=Math.Max(0,p[(y+dy)*w+x+dx]-bg-1.5*noise);if(double.IsNaN(v))continue;double xx=x+dx-cx,yy=y+dy-cy;mxx+=v*xx*xx;myy+=v*yy*yy;mxy+=v*xx*yy;}
     mxx/=flux;myy/=flux;mxy/=flux;double tr=mxx+myy,disc=Math.Sqrt((mxx-myy)*(mxx-myy)+4*mxy*mxy);if(tr<0.4||tr>12||(tr-disc)<0.08||(tr+disc)/(tr-disc)>5.5)continue;
     found.Add(new Star(cx,cy,flux));
    }
   }
   var selected=new List<Star>();int[] cells=new int[16];
   foreach(var s in found.OrderByDescending(s=>s.Flux)){int cell=Math.Min(3,(int)(s.Y/h*4))*4+Math.Min(3,(int)(s.X/w*4));if(cells[cell]>=Math.Max(4,maximum/8))continue;if(selected.Any(t=>Dist(s,t)<36))continue;selected.Add(s);cells[cell]++;if(selected.Count>=maximum)break;}
   return selected;
  }
  static double Dist(Star a,Star b){return (a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y);}
  static List<Triangle> Triangles(List<Star> p){var ts=new List<Triangle>();var seen=new HashSet<string>();for(int i=0;i<p.Count;i++){
   var nn=Enumerable.Range(0,p.Count).Where(j=>j!=i).OrderBy(j=>Dist(p[i],p[j])).Take(6).ToArray();for(int a=0;a<nn.Length;a++)for(int b=a+1;b<nn.Length;b++){
    int[] ids={i,nn[a],nn[b]};Array.Sort(ids);string key=string.Join(",",ids);if(!seen.Add(key))continue;
    double[] opp={Dist(p[ids[1]],p[ids[2]]),Dist(p[ids[0]],p[ids[2]]),Dist(p[ids[0]],p[ids[1]])};var order=Enumerable.Range(0,3).OrderBy(k=>opp[k]).ToArray();int v0=ids[order[0]],v1=ids[order[1]],v2=ids[order[2]];
    double longest=opp[order[2]],smallest=opp[order[0]];double cross=(p[v1].X-p[v0].X)*(p[v2].Y-p[v0].Y)-(p[v1].Y-p[v0].Y)*(p[v2].X-p[v0].X);
    if(smallest<100||Math.Abs(cross)/longest<0.06)continue;double ar=Math.Sqrt(smallest/longest),br=Math.Sqrt(opp[order[1]]/longest);if(br-ar<0.012||1-br<0.012)continue;
    ts.Add(new Triangle{I=v0,J=v1,K=v2,A=ar,B=br,Sign=cross>0?1:-1});
   }}return ts;}
  public static Transform Fit(IList<Star> source,IList<Star> dest){double ax=source.Average(s=>s.X),ay=source.Average(s=>s.Y),bx=dest.Average(s=>s.X),by=dest.Average(s=>s.Y),dot=0,cross=0,den=0;for(int i=0;i<source.Count;i++){double x=source[i].X-ax,y=source[i].Y-ay,u=dest[i].X-bx,v=dest[i].Y-by;dot+=x*u+y*v;cross+=x*v-y*u;den+=x*x+y*y;}if(den<1)throw new InvalidDataException("Star geometry is degenerate.");double c=dot/den,sine=cross/den;return new Transform{C=c,S=sine,Tx=bx-c*ax+sine*ay,Ty=by-sine*ax-c*ay};}
  static List<Tuple<int,int,double>> Matches(List<Star> a,List<Star> b,Transform t,double tolerance){var pairs=new List<Tuple<int,int,double>>();for(int i=0;i<a.Count;i++){Star v=t.Apply(a[i]);double best=tolerance*tolerance;int idx=-1;for(int j=0;j<b.Count;j++){double d=Dist(v,b[j]);if(d<best){best=d;idx=j;}}if(idx>=0)pairs.Add(Tuple.Create(i,idx,best));}var used=new HashSet<int>();return pairs.OrderBy(p=>p.Item3).Where(p=>used.Add(p.Item2)).ToList();}
  public static PairResult Match(List<Star> a,List<Star> b,int w,int h,CancellationToken ct){
   if(a.Count<12||b.Count<12)throw new InvalidDataException("Fewer than 12 usable stars.");var ta=Triangles(a);var tb=Triangles(b);var buckets=new Dictionary<string,List<Triangle>>();Func<int,int,int,string> key=(x,y,s)=>x+","+y+","+s;
   foreach(var t in tb){string k=key((int)(t.A/0.01),(int)(t.B/0.01),t.Sign);if(!buckets.ContainsKey(k))buckets[k]=new List<Triangle>();buckets[k].Add(t);}
   var hypotheses=new List<Tuple<Triangle,Triangle,double>>();foreach(var t in ta){int x=(int)(t.A/0.01),y=(int)(t.B/0.01);for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++){List<Triangle> q;if(!buckets.TryGetValue(key(x+dx,y+dy,t.Sign),out q))continue;foreach(var u in q){double d=(t.A-u.A)*(t.A-u.A)+(t.B-u.B)*(t.B-u.B);if(d<0.00012)hypotheses.Add(Tuple.Create(t,u,d));}}}
   List<Tuple<int,int,double>> best=null;Transform transform=null;double bestError=double.MaxValue;int tried=0;
   foreach(var pair in hypotheses.OrderBy(p=>p.Item3).Take(1400)){if((++tried%20)==0)ct.ThrowIfCancellationRequested();Triangle t=pair.Item1,u=pair.Item2;var tf=Fit(new[]{a[t.I],a[t.J],a[t.K]},new[]{b[u.I],b[u.J],b[u.K]});double scale=Math.Sqrt(tf.C*tf.C+tf.S*tf.S);if(scale<0.97||scale>1.03)continue;
    var m=Matches(a,b,tf,2.6);if(m.Count<12)continue;double err=m.Average(p=>p.Item3);if(best==null||m.Count>best.Count||(m.Count==best.Count&&err<bestError)){best=m;transform=tf;bestError=err;}if(m.Count>Math.Min(a.Count,b.Count)*0.8&&err<0.09)break;
   }
   if(best==null)throw new InvalidDataException("No reliable common star pattern; frames may be different fields.");
   for(int n=0;n<4;n++){transform=Fit(best.Select(p=>a[p.Item1]).ToArray(),best.Select(p=>b[p.Item2]).ToArray());double rms=Math.Sqrt(best.Average(p=>Dist(transform.Apply(a[p.Item1]),b[p.Item2])));best=Matches(a,b,transform,Math.Max(0.5,Math.Min(2.0,rms*3)));if(best.Count<12)throw new InvalidDataException("Insufficient matches after outlier rejection.");}
   transform=Fit(best.Select(p=>a[p.Item1]).ToArray(),best.Select(p=>b[p.Item2]).ToArray());double rmsFinal=Math.Sqrt(best.Average(p=>Dist(transform.Apply(a[p.Item1]),b[p.Item2])));double fraction=(double)best.Count/Math.Min(a.Count,b.Count);
   var pts=best.Select(p=>a[p.Item1]).ToList();double coverage=(pts.Max(p=>p.X)-pts.Min(p=>p.X))/w*(pts.Max(p=>p.Y)-pts.Min(p=>p.Y))/h;double sc=Math.Sqrt(transform.C*transform.C+transform.S*transform.S);
   if(rmsFinal>1.2||fraction<0.22||coverage<0.12||Math.Abs(sc-1)>0.01)throw new InvalidDataException("Match failed residual, scale or spatial-coverage checks.");
   double cx=pts.Average(p=>p.X),cy=pts.Average(p=>p.Y),lever=Math.Sqrt(pts.Sum(p=>(p.X-cx)*(p.X-cx)+(p.Y-cy)*(p.Y-cy)));
   return new PairResult{Angle=Math.Atan2(transform.S,transform.C)*180/Math.PI,Rms=rmsFinal,Scale=sc,Matches=best.Count,Coverage=coverage,Uncertainty=Math.Max(0.003,rmsFinal/lever*180/Math.PI)};
  }
  static double[] Unwrap(double[] angles){double[] q=new double[angles.Length];if(angles.Length==0)return q;q[0]=angles[0];for(int i=1;i<angles.Length;i++){double d=angles[i]-angles[i-1];while(d>180)d-=360;while(d<-180)d+=360;q[i]=q[i-1]+d;}return q;}
  public static double Parallactic(DateTime utc,double ra,double dec,double lat,double lon){double days=(utc-new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc)).TotalDays;double gst=280.46061837+360.98564736629*days;double hour=(gst+lon-ra)%360;if(hour>180)hour-=360;double k=Math.PI/180;return Math.Atan2(Math.Sin(hour*k),Math.Tan(lat*k)*Math.Cos(dec*k)-Math.Sin(dec*k)*Math.Cos(hour*k))/k;}
  public static RotationResult Analyze(Repository repo,List<Frame> frames,double? lat,double? lon,CancellationToken ct,Action<ProgressInfo> progress,Action<int> counted=null){
   var result=new RotationResult{Session=frames.First().Session,Mount="Unknown",Evidence="",AnalyzedAt=DateTime.UtcNow.ToString("o")};
   var eligible=frames.Where(f=>f.Kind=="Light"&&!f.Rejected&&f.Calibration!="Registered"&&!string.IsNullOrEmpty(f.Observed)&&(string.IsNullOrEmpty(f.ClassificationVersion)||!string.IsNullOrEmpty(f.ObservedUtc))&&Assets.CanDecode(f)&&f.Format!="SER"&&(Assets.Selected(f)==null||Assets.Selected(f).Count<=1)).OrderBy(f=>f.ObservedUtc??f.Observed,StringComparer.Ordinal).ToList();
   // Never bridge a long interruption or combine mixed exposure/calibration groups.
   var segments=new List<List<Frame>>();foreach(var f in eligible){DateTime? d=Util.Time(f.ObservedUtc??f.Observed);if(!d.HasValue)continue;if(segments.Count==0||((d.Value-Util.Time(segments.Last().Last().ObservedUtc??segments.Last().Last().Observed).Value).TotalMinutes>45))segments.Add(new List<Frame>());segments.Last().Add(f);}
   var group=segments.OrderByDescending(s=>(Util.Time(s.Last().ObservedUtc??s.Last().Observed).Value-Util.Time(s.First().ObservedUtc??s.First().Observed).Value).TotalMinutes).FirstOrDefault();
   if(group==null||group.Count<5){result.Evidence="At least five timestamped individual light frames in one uninterrupted session are required.";return result;}
   int count=Math.Min(18,group.Count);var sampled=Enumerable.Range(0,count).Select(i=>group[(int)Math.Round(i*(group.Count-1.0)/(count-1))]).Distinct().ToList();
   List<Star> reference=null;FitsImage refImage=null;DateTime start=DateTime.MinValue;var accepted=new List<Frame>();
   for(int i=0;i<sampled.Count;i++){ct.ThrowIfCancellationRequested();Frame f=sampled[i];progress(new ProgressInfo{Done=i,Total=sampled.Count,Text="Matching stars: "+f.OriginalName});try{FitsImage image=Assets.Analysis(f,repo.FilePath(f),ct,counted);var stars=Detect(image);if(stars.Count<12)throw new InvalidDataException("Fewer than 12 usable stars.");
    DateTime time=Util.Time(f.ObservedUtc??f.Observed).Value;
    if(reference==null){reference=stars;refImage=image;start=time;result.Points.Add(new RotationPoint{File=f.OriginalName,Time=f.Observed,Minutes=0,Angle=0,Stars=stars.Count,Error=0.003});accepted.Add(f);continue;}
    if(image.Width!=refImage.Width||image.Height!=refImage.Height)throw new InvalidDataException("Different image dimensions.");
    var m=Match(reference,stars,image.Width,image.Height,ct);result.Points.Add(new RotationPoint{File=f.OriginalName,Time=f.Observed,Minutes=(time-start).TotalMinutes,Angle=m.Angle,Rms=m.Rms,Stars=m.Matches,Error=m.Uncertainty});accepted.Add(f);
   }catch(OperationCanceledException){throw;}catch(Exception e){result.Rejected.Add(f.OriginalName+": "+e.Message);}}
   if(result.Points.Count<5){result.Evidence="Too few reliable star matches ("+result.Points.Count+"). "+string.Join("; ",result.Rejected.Take(2));return result;}
   double[] y=Unwrap(result.Points.Select(p=>p.Angle).ToArray());double[] x=result.Points.Select(p=>p.Minutes).ToArray();for(int i=0;i<y.Length;i++)result.Points[i].Angle=y[i];
   result.SpanMinutes=x.Last()-x.First();result.DriftDegrees=y.Max()-y.Min();
   if(result.SpanMinutes<10){result.Evidence="Only "+result.SpanMinutes.ToString("0.0")+" minutes available; mount classification needs a longer span.";return result;}
   var slopes=new List<double>();for(int i=0;i<x.Length;i++)for(int j=i+1;j<x.Length;j++)if(x[j]-x[i]>1)slopes.Add((y[j]-y[i])/(x[j]-x[i]));double slope=Median(slopes),offset=Median(y.Select((v,i)=>v-slope*x[i]));double res=1.4826*Median(y.Select((v,i)=>Math.Abs(v-offset-slope*x[i])));double error=Median(result.Points.Select(p=>p.Error));result.RateDegreesMinute=slope;result.ResidualDegrees=res;
   Frame baseFrame=accepted.First();double? latitude=lat??baseFrame.Latitude,longitude=lon??baseFrame.Longitude;
   bool geometry=latitude.HasValue&&longitude.HasValue&&baseFrame.RA.HasValue&&baseFrame.Dec.HasValue&&accepted.All(f=>f.TimeSource=="FITS UTC");
   string context="Matched "+result.Points.Count+"/"+sampled.Count+" frames over "+result.SpanMinutes.ToString("0.0")+" min; angular range "+result.DriftDegrees.ToString("0.000")+"°, median fit error "+Median(result.Points.Skip(1).Select(p=>p.Rms)).ToString("0.00")+" binned pixels. ";
   if(geometry){
    double[] q=Unwrap(accepted.Select(f=>Parallactic(Util.Time(f.ObservedUtc??f.Observed).Value,baseFrame.RA.Value,baseFrame.Dec.Value,latitude.Value,longitude.Value)).ToArray());double predicted=q.Max()-q.Min();
    double fitPlus=Median(y.Select((v,i)=>v-q[i])),fitMinus=Median(y.Select((v,i)=>v+q[i]));double rPlus=Math.Sqrt(y.Select((v,i)=>Math.Pow(v-q[i]-fitPlus,2)).Average()),rMinus=Math.Sqrt(y.Select((v,i)=>Math.Pow(v+q[i]-fitMinus,2)).Average());double modelError=Math.Min(rPlus,rMinus);
    if(predicted>0.5&&result.DriftDegrees>0.35&&modelError<Math.Max(0.08,Math.Min(0.5,predicted*0.05))&&result.DriftDegrees>error*8){result.Mount="Alt-Az?";result.Evidence=context+"Measured rotation agrees with the Alt-Az parallactic-angle model (RMS "+modelError.ToString("0.000")+"°). This remains an inference.";}
    else if(predicted>2&&result.SpanMinutes>=15&&result.DriftDegrees<Math.Max(0.08,error*6)&&res<0.03){result.Mount="EQ?";result.Evidence=context+"Stable orientation where Alt-Az predicts "+predicted.ToString("0.00")+"° rotation. Registered or derotated files can mimic EQ; only analyze acquisition subs.";}
    else result.Evidence=context+"Neither mount model is sufficiently supported. Alt-Az expected angular range: "+predicted.ToString("0.00")+"°.";
   }else if(result.DriftDegrees>0.6&&Math.Abs(slope)*result.SpanMinutes>0.5&&res<Math.Max(0.05,result.DriftDegrees*0.08)&&result.DriftDegrees>error*10){result.Mount="Alt-Az?";result.Evidence=context+"Coherent rotation suggests Alt-Az. Without site and pointing data, poor EQ polar alignment or a rotating camera cannot be excluded.";}
   else result.Evidence=context+"Low or inconsistent rotation cannot establish EQ. Choose an observing town/city in Settings and add solved pointing to test the Alt-Az model.";
   return result;
  }
 }
}
