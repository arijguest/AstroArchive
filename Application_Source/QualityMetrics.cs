// Image quality measurement. Pure functions over one luminance plane: no file, database or UI access.
// Stars: noise, star count, FWHM, half-flux diameter and eccentricity of the best isolated stars.
// Discs (planets, Moon, Sun): limb edge width, fine/coarse detail ratio, SNR and clipping.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AstroArchive {
 // Failed: the file could not be read (tried again next run). Unsuitable: set aside, skipped until a fresh measurement is requested.
 public static class QualityStatus {public const int Ok=0,TooFewStars=1,NoObject=2,Failed=3,Unsuitable=4;}
 public static class QualityKind {public const int Stars=0,Disc=1;}
 public sealed class QualityMeasurement {
  public string Hash;public int Version,Kind,Status;
  public double Fwhm=double.NaN,Hfd=double.NaN,FwhmCentre=double.NaN,FwhmEdge=double.NaN,Ecc=double.NaN,Snr=double.NaN,Noise=double.NaN,Bg=double.NaN,Gradient=double.NaN;
  public int Stars,StarsUsed;
  // Spatial evidence of a star-poor foreground region with a sharp background boundary.
  public double Obstruction=double.NaN,StarCoverage=double.NaN;
  public double Detail=double.NaN,Limb=double.NaN,Sat,BlackClip,Peak=double.NaN,Max=double.NaN,Diameter=double.NaN;
  public bool FillsFrame;public int Width,Height;
  public string Pack(){
   var values=new double[]{Fwhm,Hfd,FwhmCentre,FwhmEdge,Ecc,Snr,Noise,Bg,Gradient,Stars,StarsUsed,Detail,Limb,Sat,BlackClip,Peak,Max,Diameter,FillsFrame?1:0,Width,Height,Obstruction,StarCoverage};
   var text=new StringBuilder();for(int i=0;i<values.Length;i++){if(i>0)text.Append(';');text.Append(values[i].ToString("R",CultureInfo.InvariantCulture));}return text.ToString();
  }
  public static QualityMeasurement Unpack(string hash,int version,int kind,int status,string data){
   var parts=(data??"").Split(';');var v=new double[23];for(int i=0;i<v.Length;i++){double x;v[i]=i<parts.Length&&double.TryParse(parts[i],NumberStyles.Float,CultureInfo.InvariantCulture,out x)?x:double.NaN;}
   return new QualityMeasurement{Hash=hash,Version=version,Kind=kind,Status=status,Fwhm=v[0],Hfd=v[1],FwhmCentre=v[2],FwhmEdge=v[3],Ecc=v[4],Snr=v[5],Noise=v[6],Bg=v[7],Gradient=v[8],Stars=(int)Num(v[9]),StarsUsed=(int)Num(v[10]),Detail=v[11],Limb=v[12],Sat=Num(v[13]),BlackClip=Num(v[14]),Peak=v[15],Max=v[16],Diameter=v[17],FillsFrame=v[18]>0.5,Width=(int)Num(v[19]),Height=(int)Num(v[20]),Obstruction=v[21],StarCoverage=v[22]};
  }
  static double Num(double v){return double.IsNaN(v)||double.IsInfinity(v)?0:v;}
 }
 public static class QualityMeter {
  // Bump when any measurement changes; stored rows with an older version are measured again lazily.
  public const int Version=2;
  const double FwhmPerSigma=2.3548200450309493;

  // Collapses channels (planar) to one finite float plane. Non-finite samples become the plane median-free zero.
  public static float[] Plane(double[] pixels,int width,int height,int channels){
   int n=checked(width*height);var plane=new float[n];int c=Math.Max(1,channels);
   if(c==1){for(int i=0;i<n;i++){double v=pixels[i];plane[i]=double.IsNaN(v)||double.IsInfinity(v)?0f:(float)v;}return plane;}
   for(int i=0;i<n;i++){double sum=0;int count=0;for(int k=0;k<c;k++){double v=pixels[k*n+i];if(double.IsNaN(v)||double.IsInfinity(v))continue;sum+=v;count++;}plane[i]=count==0?0f:(float)(sum/count);}
   return plane;
  }

  // ---------- shared helpers ----------
  public static float Select(float[] a,int n,int k){
   int lo=0,hi=n-1;if(n<=0)return 0;k=Math.Max(0,Math.Min(n-1,k));
   while(lo<hi){float pivot=a[(lo+hi)>>1];int i=lo,j=hi;while(i<=j){while(a[i]<pivot)i++;while(a[j]>pivot)j--;if(i<=j){float t=a[i];a[i]=a[j];a[j]=t;i++;j--;}}if(k<=j)hi=j;else if(k>=i)lo=i;else break;}
   return a[k];
  }
  public static float Median(float[] a,int n){if(n<=0)return 0;if((n&1)==1)return Select(a,n,n/2);float hi=Select(a,n,n/2);float lo=Select(a,n/2,n/2-1);return 0.5f*(lo+hi);}
  public static double Median(List<double> values){if(values.Count==0)return double.NaN;var s=values.ToArray();Array.Sort(s);int n=s.Length;return (n&1)==1?s[n/2]:0.5*(s[n/2-1]+s[n/2]);}
  static double MadSigma(float[] a,int n,float median,float[] scratch){for(int i=0;i<n;i++)scratch[i]=Math.Abs(a[i]-median);return 1.4826*Median(scratch,n);}
  public static float[] Bin(float[] src,int w,int h,int f){
   if(f<=1)return src;int ow=w/f,oh=h/f;var o=new float[ow*oh];float norm=1f/(f*f);
   for(int y=0;y<oh;y++){int row=y*f*w;for(int x=0;x<ow;x++){float s=0;int baseIndex=row+x*f;for(int dy=0;dy<f;dy++){int q=baseIndex+dy*w;for(int dx=0;dx<f;dx++)s+=src[q+dx];}o[y*ow+x]=s*norm;}}
   return o;
  }
  // Median and MAD sigma of square tiles taken on a sparse grid.
  // With detrend, sigma comes from differences of neighbouring pixels so a sky gradient inside the tile does not inflate it.
  static void TileStats(float[] a,int w,int h,int tile,int step,List<double> medians,List<double> sigmas,bool detrend=false){
   var buffer=new float[tile*tile];var scratch=new float[tile*tile];var diff=detrend?new float[tile*tile]:null;
   for(int y=0;y+tile<=h;y+=step)for(int x=0;x+tile<=w;x+=step){
    int n=0;for(int dy=0;dy<tile;dy++){int row=(y+dy)*w+x;for(int dx=0;dx<tile;dx++)buffer[n++]=a[row+dx];}
    if(detrend&&sigmas!=null){int k=0;for(int dy=0;dy<tile;dy++){int row=(y+dy)*w+x;for(int dx=0;dx+1<tile;dx++)diff[k++]=a[row+dx+1]-a[row+dx];}float dm=Median(diff,k);sigmas.Add(MadSigma(diff,k,dm,scratch)/Math.Sqrt(2));}
    float median=Median(buffer,n);medians.Add(median);if(sigmas!=null&&!detrend){double sigma=MadSigma(buffer,n,median,scratch);sigmas.Add(sigma);}
   }
  }
  static float[] Gaussian(float[] src,int w,int h,double sigma){
   int radius=Math.Max(1,(int)Math.Ceiling(3*sigma));var kernel=new float[2*radius+1];double sum=0;for(int i=-radius;i<=radius;i++){double v=Math.Exp(-0.5*i*i/(sigma*sigma));kernel[i+radius]=(float)v;sum+=v;}for(int i=0;i<kernel.Length;i++)kernel[i]=(float)(kernel[i]/sum);
   var tmp=new float[w*h];var o=new float[w*h];
   for(int y=0;y<h;y++){int row=y*w;for(int x=0;x<w;x++){float s=0;for(int k=-radius;k<=radius;k++){int xx=x+k;if(xx<0)xx=0;else if(xx>=w)xx=w-1;s+=src[row+xx]*kernel[k+radius];}tmp[row+x]=s;}}
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){float s=0;for(int k=-radius;k<=radius;k++){int yy=y+k;if(yy<0)yy=0;else if(yy>=h)yy=h-1;s+=tmp[yy*w+x]*kernel[k+radius];}o[y*w+x]=s;}
   return o;
  }
  // Background: tile medians interpolated bilinearly.
  static float[] Background(float[] a,int w,int h,int tile){
   int tx=Math.Max(1,w/tile),ty=Math.Max(1,h/tile);var med=new float[tx*ty];var buffer=new float[tile*tile];
   for(int cy=0;cy<ty;cy++)for(int cx=0;cx<tx;cx++){int n=0;int x0=cx*tile,y0=cy*tile,x1=Math.Min(w,x0+tile),y1=Math.Min(h,y0+tile);for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)buffer[n++]=a[y*w+x];med[cy*tx+cx]=Median(buffer,n);}
   var bg=new float[w*h];
   for(int y=0;y<h;y++){double v=(y+0.5)/tile-0.5;int j0=(int)Math.Floor(v);double fy=v-j0;int j1=Math.Min(ty-1,Math.Max(0,j0+1));j0=Math.Min(ty-1,Math.Max(0,j0));
    for(int x=0;x<w;x++){double u=(x+0.5)/tile-0.5;int i0=(int)Math.Floor(u);double fx=u-i0;int i1=Math.Min(tx-1,Math.Max(0,i0+1));i0=Math.Min(tx-1,Math.Max(0,i0));
     double top=med[j0*tx+i0]*(1-fx)+med[j0*tx+i1]*fx,bottom=med[j1*tx+i0]*(1-fx)+med[j1*tx+i1]*fx;bg[y*w+x]=(float)(top*(1-fy)+bottom*fy);}}
   return bg;
  }
  static void PlaneRange(float[] p,out float min,out float max,out int minCount,out int maxCount){
   min=float.MaxValue;max=float.MinValue;minCount=0;maxCount=0;
   for(int i=0;i<p.Length;i++){float v=p[i];if(v>max){max=v;maxCount=1;}else if(v==max)maxCount++;if(v<min){min=v;minCount=1;}else if(v==min)minCount++;}
  }

  // ---------- stars ----------
  struct Candidate {public int X,Y;public float G;}
  struct StarFit {public double Fwhm,Hfd,Ecc,Snr,X,Y;}

  public static QualityMeasurement MeasureStars(float[] p,int w,int h,bool cfa){
   var m=new QualityMeasurement{Kind=QualityKind.Stars,Version=Version,Width=w,Height=h};
   if(p==null||w<96||h<96||p.Length<w*h){m.Status=QualityStatus.Failed;return m;}
   float min,max;int minCount,maxCount;PlaneRange(p,out min,out max,out minCount,out maxCount);m.Max=max;
   if(max<=min){m.Status=QualityStatus.TooFewStars;return m;}
   // Sky level, noise and gradient from sparse tiles of the 2x2 binned plane (this also averages away a Bayer pattern).
   int w2=w/2,h2=h/2;var b2=Bin(p,w,h,2);var medians=new List<double>();var sigmas=new List<double>();
   TileStats(b2,w2,h2,32,96,medians,sigmas,true);
   if(medians.Count>0){m.Bg=Median(medians);m.Noise=Median(sigmas);double lo=double.MaxValue,hi=double.MinValue;foreach(double v in medians){if(v<lo)lo=v;if(v>hi)hi=v;}m.Gradient=Math.Abs(m.Bg)>1e-9?(hi-lo)/Math.Abs(m.Bg):double.NaN;}
   // Detection plane: 4x4 bin for large frames.
   int longest=Math.Max(w,h);int bin=longest>=1400?4:(longest>=700?2:1);
   float[] d;int dw,dh;if(bin==4){d=Bin(b2,w2,h2,2);dw=w2/2;dh=h2/2;}else if(bin==2){d=b2;dw=w2;dh=h2;}else{d=p;dw=w;dh=h;}
   var bg=Background(d,dw,dh,16);var residual=new float[d.Length];for(int i=0;i<d.Length;i++)residual[i]=d[i]-bg[i];
   var g=Gaussian(residual,dw,dh,1.0);
   var gm=new List<double>();var gs=new List<double>();TileStats(g,dw,dh,16,32,gm,gs);double sigmaG=gs.Count>0?Median(gs):0;
   if(!(sigmaG>0)){m.Status=QualityStatus.TooFewStars;return m;}
   double th5=5*sigmaG,th10=10*sigmaG;var candidates=new List<Candidate>();int counted=0;
   for(int y=3;y<dh-3;y++)for(int x=3;x<dw-3;x++){
    float v=g[y*dw+x];if(v<=th5)continue;bool peak=true;
    for(int dy=-2;dy<=2&&peak;dy++)for(int dx=-2;dx<=2;dx++){if(dx==0&&dy==0)continue;float o=g[(y+dy)*dw+x+dx];if(o>v||(o==v&&(dy<0||(dy==0&&dx<0)))){peak=false;break;}}
    if(!peak)continue;candidates.Add(new Candidate{X=x,Y=y,G=v});if(v>=th10)counted++;
   }
   m.Stars=counted;
   MeasureObstruction(d,dw,dh,g,candidates,th10,m);
   candidates.Sort((a,b)=>b.G.CompareTo(a.G));
   float saturation=maxCount>=4?(float)(max-(max-min)*0.01):0f;
   var fits=new List<StarFit>();int examined=0;int margin=30;
   for(int index=0;index<candidates.Count&&fits.Count<100&&examined<400;index++){
    var c=candidates[index];double rx=bin==1?c.X:c.X*bin+bin/2.0,ry=bin==1?c.Y:c.Y*bin+bin/2.0;
    if(rx<margin||ry<margin||rx>=w-margin||ry>=h-margin)continue;
    bool blended=false;double limit=Math.Max(14,2.5*bin*2);
    for(int j=0;j<candidates.Count;j++){if(j==index)continue;var o=candidates[j];if(o.G<0.2f*c.G)break;double ox=bin==1?o.X:o.X*bin+bin/2.0,oy=bin==1?o.Y:o.Y*bin+bin/2.0;if((ox-rx)*(ox-rx)+(oy-ry)*(oy-ry)<limit*limit){blended=true;break;}}
    if(blended)continue;examined++;
    StarFit fit;if(FitStar(p,w,h,rx,ry,cfa,saturation,out fit))fits.Add(fit);
   }
   m.StarsUsed=fits.Count;
   if(fits.Count<12){m.Status=QualityStatus.TooFewStars;if(fits.Count>0)m.Snr=Median(Column(fits,3));return m;}
   m.Fwhm=Median(Column(fits,0));m.Hfd=Median(Column(fits,1));m.Ecc=Median(Column(fits,2));m.Snr=Median(Column(fits,3));
   var centre=new List<double>();var edge=new List<double>();
   foreach(var f in fits){bool inside=Math.Abs(f.X-w/2.0)<w/4.0&&Math.Abs(f.Y-h/2.0)<h/4.0;(inside?centre:edge).Add(f.Fwhm);}
   if(centre.Count>=5)m.FwhmCentre=Median(centre);if(edge.Count>=5)m.FwhmEdge=Median(edge);
   m.Status=QualityStatus.Ok;return m;
  }
  // Obstructions can leave excellent stars in the remaining clear part of the
  // frame. Require both a star deficit and an abrupt, connected background
  // boundary; an empty tile or a smooth sky/vignetting gradient alone is insufficient.
  static void MeasureObstruction(float[] plane,int w,int h,float[] detection,List<Candidate> candidates,double threshold,QualityMeasurement m){
   const int nx=8,ny=8,n=nx*ny;var stars=new int[n];int total=0;
   foreach(var c in candidates){
    if(c.G<threshold||c.X<3||c.Y<3||c.X>=w-3||c.Y>=h-3)continue;
    // Reject ridges/edges: a compact source must fall away along both axes.
    double x=c.G-Math.Max(detection[c.Y*w+c.X-3],detection[c.Y*w+c.X+3]),y=c.G-Math.Max(detection[(c.Y-3)*w+c.X],detection[(c.Y+3)*w+c.X]);
    if(x<0.25*c.G||y<0.25*c.G||Math.Max(x,y)>4*Math.Min(x,y))continue;
    stars[Math.Min(ny-1,c.Y*ny/h)*nx+Math.Min(nx-1,c.X*nx/w)]++;total++;
   }
   m.Obstruction=0;if(total<24)return;
   int occupied=0;foreach(int count in stars)if(count>0)occupied++;
   m.StarCoverage=Math.Min(1,occupied/(n*(1-Math.Exp(-total/(double)n))));
   var levels=new double[n];var noise=new double[n];var skyLevels=new List<double>();var skyNoise=new List<double>();
   for(int cy=0;cy<ny;cy++)for(int cx=0;cx<nx;cx++){
    int k=cy*nx+cx,x0=cx*w/nx,x1=(cx+1)*w/nx,y0=cy*h/ny,y1=(cy+1)*h/ny;var values=new float[81];var diffs=new float[81];int used=0;
    for(int sy=0;sy<9;sy++)for(int sx=0;sx<9;sx++){
     int x=x0+(x1-x0-1)*sx/8,y=y0+(y1-y0-1)*sy/8;values[used]=plane[y*w+x];diffs[used]=plane[y*w+Math.Min(w-1,x+1)]-values[used];used++;
    }
    levels[k]=Median(values,used);float centre=Median(diffs,used);noise[k]=MadSigma(diffs,used,centre,new float[used])/Math.Sqrt(2);
    if(stars[k]>0){skyLevels.Add(levels[k]);skyNoise.Add(noise[k]);}
   }
   if(skyLevels.Count<4)return;double reference=Median(skyLevels),referenceNoise=Math.Max(1e-6,Median(skyNoise));
   var slopes=new List<double>();for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int k=y*nx+x;if(x+1<nx)slopes.Add(Math.Abs(levels[k]-levels[k+1]));if(y+1<ny)slopes.Add(Math.Abs(levels[k]-levels[k+nx]));}
   double jump=Math.Max(8*referenceNoise,4*Median(slopes));
   var suspect=new bool[n];for(int i=0;i<n;i++)suspect[i]=stars[i]==0&&(levels[i]<reference-jump||levels[i]>reference+jump&&noise[i]<0.5*referenceNoise);
   var visited=new bool[n];int blocked=0;
   for(int seed=0;seed<n;seed++){
    if(visited[seed]||!suspect[seed])continue;var queue=new Queue<int>();queue.Enqueue(seed);visited[seed]=true;int area=0;bool sharp=false;
    while(queue.Count>0){int k=queue.Dequeue();area++;int x=k%nx,y=k/nx;
     foreach(int next in new[]{x>0?k-1:-1,x+1<nx?k+1:-1,y>0?k-nx:-1,y+1<ny?k+nx:-1}){
      if(next<0)continue;if(!suspect[next]&&stars[next]>0&&Math.Abs(levels[k]-levels[next])>jump)sharp=true;
      if(suspect[next]&&!visited[next]){visited[next]=true;queue.Enqueue(next);}
     }
    }
    if(sharp&&area>=2)blocked+=area;
   }
   m.Obstruction=blocked/(double)n;
  }
  static List<double> Column(List<StarFit> fits,int which){var o=new List<double>(fits.Count);foreach(var f in fits)o.Add(which==0?f.Fwhm:which==1?f.Hfd:which==2?f.Ecc:f.Snr);return o;}

  // Measures one star on the full-resolution plane. Signed (background-subtracted) intensities keep noise unbiased;
  // a Gaussian window is removed analytically, so the result does not depend on the window size.
  static bool FitStar(float[] p,int w,int h,double x0,double y0,bool cfa,float saturation,out StarFit fit){
   fit=new StarFit();const int Rmax=24;int R=12;double cx=x0,cy=y0;
   for(int attempt=0;attempt<2;attempt++){
    int ix=(int)Math.Round(cx),iy=(int)Math.Round(cy);int half=R+6;
    if(ix-half<0||iy-half<0||ix+half+1>=w||iy+half+1>=h)return false;
    int size=2*half+1;var s=new float[size*size];
    for(int y=0;y<size;y++)for(int x=0;x<size;x++){int px=ix-half+x,py=iy-half+y;float v;
     if(cfa)v=0.25f*(p[py*w+px]+p[py*w+px+1]+p[(py+1)*w+px]+p[(py+1)*w+px+1]);else v=p[py*w+px];s[y*size+x]=v;}
    if(saturation>0){for(int y=half-3;y<=half+3;y++)for(int x=half-3;x<=half+3;x++){int px=ix-half+x,py=iy-half+y;if(p[py*w+px]>=saturation)return false;}}
    // Local background and noise from the ring just outside the measuring aperture.
    var ring=new float[size*size];int rn=0;for(int y=0;y<size;y++)for(int x=0;x<size;x++){int ddx=Math.Abs(x-half),ddy=Math.Abs(y-half);int cheb=Math.Max(ddx,ddy);if(cheb>R&&cheb<=R+5)ring[rn++]=s[y*size+x];}
    if(rn<40)return false;float bg=Median(ring,rn);var scratch=new float[rn];for(int i=0;i<rn;i++)scratch[i]=Math.Abs(ring[i]-bg);double noise=1.4826*Median(scratch,rn);if(noise<=0)noise=1e-6;
    double lx=cx-(ix-half)+(cfa?-0.5:0),ly=cy-(iy-half)+(cfa?-0.5:0);
    // Centroid with a narrow window.
    double sw=2.0;
    for(int it=0;it<4;it++){double sum=0,sx=0,sy=0;int r=(int)Math.Ceiling(3*sw);int xa=Math.Max(0,(int)lx-r),xb=Math.Min(size-1,(int)lx+r),ya=Math.Max(0,(int)ly-r),yb=Math.Min(size-1,(int)ly+r);
     for(int y=ya;y<=yb;y++)for(int x=xa;x<=xb;x++){double dx=x-lx,dy=y-ly;double wt=Math.Exp(-(dx*dx+dy*dy)/(2*sw*sw));double val=(s[y*size+x]-bg)*wt;sum+=val;sx+=val*x;sy+=val*y;}
     if(sum<=0)return false;lx=sx/sum;ly=sy/sum;if(lx<4||ly<4||lx>size-5||ly>size-5)return false;}
    // Peak and half-flux diameter.
    double peak=0;{int ax=(int)Math.Round(lx),ay=(int)Math.Round(ly);double sum9=0;for(int y=ay-1;y<=ay+1;y++)for(int x=ax-1;x<=ax+1;x++)sum9+=s[y*size+x]-bg;peak=sum9/9.0;}
    if(peak<6*noise)return false;
    int bins=2*R+2;var flux=new double[bins];double total=0;
    for(int y=0;y<size;y++)for(int x=0;x<size;x++){double dx=x-lx,dy=y-ly;double r=Math.Sqrt(dx*dx+dy*dy);if(r>R)continue;double val=s[y*size+x]-bg;flux[Math.Min(bins-1,(int)(r*2))]+=val;total+=val;}
    if(total<=0)return false;
    double cumulative=0,r50=-1;for(int b=0;b<bins;b++){double next=cumulative+flux[b];if(next>=total/2){double frac=flux[b]!=0?(total/2-cumulative)/flux[b]:0;r50=(b+Math.Max(0,Math.Min(1,frac)))*0.5;break;}cumulative=next;}
    if(r50<=0)return false;double hfd=2*r50;
    double sigma0=hfd/FwhmPerSigma;if(4.5*sigma0>R&&R<Rmax&&attempt==0){R=Math.Min(Rmax,(int)Math.Ceiling(4.5*sigma0)+1);continue;}
    // Gaussian-windowed moments, window removed analytically.
    double win=Math.Max(1.4,Math.Min(R/3.0,1.5*sigma0));
    for(int it=0;it<2;it++){double sum=0,sx=0,sy=0;int r=(int)Math.Ceiling(3*win);int xa=Math.Max(0,(int)lx-r),xb=Math.Min(size-1,(int)lx+r),ya=Math.Max(0,(int)ly-r),yb=Math.Min(size-1,(int)ly+r);
     for(int y=ya;y<=yb;y++)for(int x=xa;x<=xb;x++){double dx=x-lx,dy=y-ly;double wt=Math.Exp(-(dx*dx+dy*dy)/(2*win*win));double val=(s[y*size+x]-bg)*wt;sum+=val;sx+=val*x;sy+=val*y;}
     if(sum<=0)return false;lx=sx/sum;ly=sy/sum;}
    double wsum=0,mxx=0,myy=0,mxy=0;{int r=(int)Math.Ceiling(3.5*win);int xa=Math.Max(0,(int)lx-r),xb=Math.Min(size-1,(int)lx+r),ya=Math.Max(0,(int)ly-r),yb=Math.Min(size-1,(int)ly+r);
     for(int y=ya;y<=yb;y++)for(int x=xa;x<=xb;x++){double dx=x-lx,dy=y-ly;double wt=Math.Exp(-(dx*dx+dy*dy)/(2*win*win));double val=(s[y*size+x]-bg)*wt;wsum+=val;mxx+=val*dx*dx;myy+=val*dy*dy;mxy+=val*dx*dy;}}
    if(wsum<=0)return false;mxx/=wsum;myy/=wsum;mxy/=wsum;
    double mean=0.5*(mxx+myy),diff=Math.Sqrt(0.25*(mxx-myy)*(mxx-myy)+mxy*mxy);double l1=mean+diff,l2=mean-diff;double w2s=win*win;
    if(l2<=0.04||l1>=0.97*w2s)return false;
    double v1=1.0/(1.0/l1-1.0/w2s),v2=1.0/(1.0/l2-1.0/w2s);
    if(cfa){v1-=0.25;v2-=0.25;}
    if(v2<0.05||v1<v2)return false;
    double s1=Math.Sqrt(v1),s2=Math.Sqrt(v2);double fwhm=FwhmPerSigma*Math.Sqrt(s1*s2);
    double hfdValue=hfd;if(cfa){double sg=hfd/FwhmPerSigma;hfdValue=FwhmPerSigma*Math.Sqrt(Math.Max(0.1,sg*sg-0.25));}
    if(fwhm<0.8||fwhm>40)return false;
    fit.Fwhm=fwhm;fit.Hfd=hfdValue;fit.Ecc=Math.Sqrt(Math.Max(0,1-v2/v1));fit.Snr=peak/noise;fit.X=ix-half+lx;fit.Y=iy-half+ly;return true;
   }
   return false;
  }

  // ---------- discs ----------
  public static QualityMeasurement MeasureDisc(float[] p,int w,int h){
   var m=new QualityMeasurement{Kind=QualityKind.Disc,Version=Version,Width=w,Height=h};
   if(p==null||w<48||h<48||p.Length<w*h){m.Status=QualityStatus.Failed;return m;}
   float min,max;int minCount,maxCount;PlaneRange(p,out min,out max,out minCount,out maxCount);m.Max=max;
   if(max<=min){m.Status=QualityStatus.NoObject;return m;}
   int f=Math.Max(1,(int)Math.Ceiling(Math.Max(w,h)/640.0));int bw=w/f,bh=h/f;var d=Bin(p,w,h,f);
   // Border ring gives a first background estimate; Otsu separates the object.
   int ring=Math.Max(2,Math.Min(bw,bh)/20);var border=new List<float>();
   for(int y=0;y<bh;y++)for(int x=0;x<bw;x++){if(x<ring||y<ring||x>=bw-ring||y>=bh-ring)border.Add(d[y*bw+x]);}
   var borderArray=border.ToArray();float bgLevel=Select(borderArray,borderArray.Length,(int)(borderArray.Length*0.2));
   float lo=float.MaxValue,hi=float.MinValue;for(int i=0;i<d.Length;i++){if(d[i]<lo)lo=d[i];if(d[i]>hi)hi=d[i];}
   float thr=Otsu(d,lo,hi);var mask=new bool[d.Length];for(int i=0;i<d.Length;i++)mask[i]=d[i]>thr;
   int area,x0,y0,x1,y1,touching;var comp=Largest(mask,bw,bh,out area,out x0,out y0,out x1,out y1,out touching);
   if(comp==null||area<40||area<0.0008*bw*bh){m.Status=QualityStatus.NoObject;return m;}
   double objSum=0;for(int i=0;i<comp.Length;i++)if(comp[i])objSum+=d[i];double objMean=objSum/area;
   var below=new List<float>();for(int i=0;i<borderArray.Length;i++)if(borderArray[i]<=bgLevel+(float)(0.5*(objMean-bgLevel)))below.Add(borderArray[i]);
   var belowArray=below.ToArray();double borderNoise=belowArray.Length>8?MadSigma(belowArray,belowArray.Length,Median(belowArray,belowArray.Length),new float[belowArray.Length]):0;
   if(borderNoise<=0)borderNoise=Math.Max(1e-6,0.001*(hi-lo));
   if(objMean-bgLevel<8*borderNoise){m.Status=QualityStatus.NoObject;return m;}
   bool fills=(x1-x0+1)>=0.98*bw||(y1-y0+1)>=0.98*bh||touching>0.15*2*(bw+bh);m.FillsFrame=fills;
   m.Diameter=2*Math.Sqrt(area/Math.PI)*f;
   // Crop (raw resolution, reduced when large) around the object.
   int pad=Math.Max(8,(int)(0.1*Math.Max(x1-x0,y1-y0)));int cx0=Math.Max(0,(x0-pad)*f),cy0=Math.Max(0,(y0-pad)*f),cx1=Math.Min(w,(x1+1+pad)*f),cy1=Math.Min(h,(y1+1+pad)*f);
   int cw=cx1-cx0,ch=cy1-cy0;if(cw<32||ch<32){m.Status=QualityStatus.NoObject;return m;}
   var crop=new float[cw*ch];for(int y=0;y<ch;y++)Array.Copy(p,(cy0+y)*w+cx0,crop,y*cw,cw);
   int k=Math.Max(1,(int)Math.Ceiling(Math.Max(cw,ch)/900.0));int c2w=cw/k,c2h=ch/k;var c2=Bin(crop,cw,ch,k);
   var cmask=new bool[c2.Length];for(int i=0;i<c2.Length;i++)cmask[i]=c2[i]>thr;
   int a2,ax0,ay0,ax1,ay1,t2;var obj=Largest(cmask,c2w,c2h,out a2,out ax0,out ay0,out ax1,out ay1,out t2);
   if(obj==null||a2<40){m.Status=QualityStatus.NoObject;return m;}
   double diameterCrop=2*Math.Sqrt(a2/Math.PI);
   int erodeSteps=Math.Max(2,Math.Min(12,(int)Math.Round(0.04*diameterCrop)));var interior=Erode(obj,c2w,c2h,erodeSteps);
   var grown=Dilate(obj,c2w,c2h,Math.Min(30,erodeSteps*2+4));
   // Background region statistics.
   var bgValues=new List<float>();for(int y=4;y<c2h-4;y++)for(int x=4;x<c2w-4;x++)if(!grown[y*c2w+x])bgValues.Add(c2[y*c2w+x]);
   bool haveBg=bgValues.Count>=400;double bgMean=double.NaN,noise=double.NaN;
   if(haveBg){var arr=bgValues.ToArray();float med=Median(arr,arr.Length);bgMean=med;noise=MadSigma(arr,arr.Length,med,new float[arr.Length]);if(noise<=0)noise=double.NaN;}
   double intSum=0;int intCount=0;for(int i=0;i<interior.Length;i++)if(interior[i]){intSum+=c2[i];intCount++;}
   if(intCount<20){interior=obj;intSum=0;intCount=0;for(int i=0;i<obj.Length;i++)if(obj[i]){intSum+=c2[i];intCount++;}}
   m.Noise=noise;m.Bg=bgMean;
   if(haveBg&&!double.IsNaN(noise))m.Snr=(intSum/intCount-bgMean)/noise;
   // Clipping, peak level and black clipping.
   var objValues=new float[a2];int on=0;for(int i=0;i<obj.Length;i++)if(obj[i])objValues[on++]=c2[i];
   float c2max=float.MinValue;for(int i=0;i<on;i++)if(objValues[i]>c2max)c2max=objValues[i];
   int atMax=0;float clipLevel=c2max-(max-min)*0.002f;for(int i=0;i<on;i++)if(objValues[i]>=clipLevel)atMax++;
   m.Sat=(maxCount>=4&&atMax>=Math.Max(8,0.0005*on)&&c2max>=max-(max-min)*0.01f)?atMax/(double)on:0.0;
   m.Peak=Select(objValues,on,(int)(on*0.999));
   if(haveBg&&minCount>=0.002*p.Length){int black=0;float blackLevel=min+(max-min)*0.0005f;foreach(float v in bgValues)if(v<=blackLevel)black++;m.BlackClip=black/(double)bgValues.Count;}
   // Detail ratio: fine against coarse gradient energy inside the eroded object, noise energy removed.
   var g1=Gaussian(c2,c2w,c2h,1.0);var g3=Gaussian(c2,c2w,c2h,3.0);
   double e1=0,e3=0;int en=0;double b1=0,b3=0;int bn=0;
   for(int y=1;y<c2h-1;y++)for(int x=1;x<c2w-1;x++){int i=y*c2w+x;
    bool inside=interior[i];bool outside=haveBg&&!grown[i]&&x>=4&&y>=4&&x<c2w-4&&y<c2h-4;if(!inside&&!outside)continue;
    double gx1=0.5*(g1[i+1]-g1[i-1]),gy1=0.5*(g1[i+c2w]-g1[i-c2w]),gx3=0.5*(g3[i+1]-g3[i-1]),gy3=0.5*(g3[i+c2w]-g3[i-c2w]);
    if(inside){e1+=gx1*gx1+gy1*gy1;e3+=gx3*gx3+gy3*gy3;en++;}else{b1+=gx1*gx1+gy1*gy1;b3+=gx3*gx3+gy3*gy3;bn++;}}
   if(en>=50){e1/=en;e3/=en;if(bn>=200){b1/=bn;b3/=bn;}else{b1=0;b3=0;}
    double net1=e1-b1,net3=e3-b3;if(net1>0&&net3>3*b3&&net3>1e-12)m.Detail=net1/net3;}
   // Limb edge width from profiles across the boundary normal, on the unbinned crop.
   if(!fills){
    double rawDiameter=diameterCrop*k;double width=LimbWidth(crop,cw,ch,obj,c2w,c2h,k,rawDiameter);if(!double.IsNaN(width))m.Limb=width/rawDiameter;
   }
   m.Status=QualityStatus.Ok;return m;
  }

  static float Otsu(float[] d,float lo,float hi){
   const int bins=256;var hist=new double[bins];double scale=hi>lo?(bins-1)/(double)(hi-lo):0;
   for(int i=0;i<d.Length;i++)hist[(int)((d[i]-lo)*scale)]++;
   double total=d.Length,sumAll=0;for(int i=0;i<bins;i++)sumAll+=i*hist[i];double wB=0,sumB=0,best=-1;int bestBin=0;
   for(int i=0;i<bins;i++){wB+=hist[i];if(wB==0)continue;double wF=total-wB;if(wF==0)break;sumB+=i*hist[i];double mB=sumB/wB,mF=(sumAll-sumB)/wF;double between=wB*wF*(mB-mF)*(mB-mF);if(between>best){best=between;bestBin=i;}}
   return scale>0?lo+(float)((bestBin+0.5)/scale):lo;
  }
  // Largest 4-connected component; also reports its bounding box and how many pixels sit on the frame edge.
  static bool[] Largest(bool[] mask,int w,int h,out int area,out int x0,out int y0,out int x1,out int y1,out int touching){
   area=0;x0=y0=x1=y1=0;touching=0;var label=new int[mask.Length];var queue=new int[mask.Length];int bestLabel=0,bestArea=0,next=0;
   for(int start=0;start<mask.Length;start++){
    if(!mask[start]||label[start]!=0)continue;next++;int head=0,tail=0;queue[tail++]=start;label[start]=next;
    while(head<tail){int q=queue[head++];int x=q%w,y=q/w;
     if(x>0&&mask[q-1]&&label[q-1]==0){label[q-1]=next;queue[tail++]=q-1;}
     if(x<w-1&&mask[q+1]&&label[q+1]==0){label[q+1]=next;queue[tail++]=q+1;}
     if(y>0&&mask[q-w]&&label[q-w]==0){label[q-w]=next;queue[tail++]=q-w;}
     if(y<h-1&&mask[q+w]&&label[q+w]==0){label[q+w]=next;queue[tail++]=q+w;}}
    if(tail>bestArea){bestArea=tail;bestLabel=next;}
   }
   if(bestLabel==0)return null;var o=new bool[mask.Length];x0=w;y0=h;x1=-1;y1=-1;
   for(int i=0;i<mask.Length;i++){if(label[i]!=bestLabel)continue;o[i]=true;area++;int x=i%w,y=i/w;if(x<x0)x0=x;if(x>x1)x1=x;if(y<y0)y0=y;if(y>y1)y1=y;if(x==0||y==0||x==w-1||y==h-1)touching++;}
   return o;
  }
  static bool[] Erode(bool[] src,int w,int h,int steps){
   var cur=src;for(int s=0;s<steps;s++){var next=new bool[cur.Length];for(int y=1;y<h-1;y++)for(int x=1;x<w-1;x++){int i=y*w+x;next[i]=cur[i]&&cur[i-1]&&cur[i+1]&&cur[i-w]&&cur[i+w]&&cur[i-w-1]&&cur[i-w+1]&&cur[i+w-1]&&cur[i+w+1];}cur=next;}return cur;
  }
  static bool[] Dilate(bool[] src,int w,int h,int steps){
   var cur=src;for(int s=0;s<steps;s++){var next=new bool[cur.Length];for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*w+x;bool on=cur[i];if(!on){for(int dy=-1;dy<=1&&!on;dy++){int yy=y+dy;if(yy<0||yy>=h)continue;for(int dx=-1;dx<=1;dx++){int xx=x+dx;if(xx<0||xx>=w)continue;if(cur[yy*w+xx]){on=true;break;}}}}next[i]=on;}cur=next;}return cur;
  }
  static double Sample(float[] a,int w,int h,double x,double y){
   if(x<0||y<0||x>w-1||y>h-1)return double.NaN;int ix=(int)x,iy=(int)y;if(ix>=w-1)ix=w-2;if(iy>=h-1)iy=h-2;double fx=x-ix,fy=y-iy;
   return a[iy*w+ix]*(1-fx)*(1-fy)+a[iy*w+ix+1]*fx*(1-fy)+a[(iy+1)*w+ix]*(1-fx)*fy+a[(iy+1)*w+ix+1]*fx*fy;
  }
  // Median 10-90 percent rise distance, in raw pixels, of intensity profiles across the object boundary.
  static double LimbWidth(float[] crop,int cw,int ch,bool[] obj,int w,int h,int k,double rawDiameter){
   // Smoothed mask gives a stable inward-pointing gradient at each boundary pixel.
   var mf=new float[obj.Length];for(int i=0;i<obj.Length;i++)mf[i]=obj[i]?1f:0f;var sm=Gaussian(mf,w,h,Math.Max(1.5,0.02*Math.Sqrt(obj.Length)/4));
   var boundary=new List<int>();for(int y=1;y<h-1;y++)for(int x=1;x<w-1;x++){int i=y*w+x;if(obj[i]&&(!obj[i-1]||!obj[i+1]||!obj[i-w]||!obj[i+w]))boundary.Add(i);}
   if(boundary.Count<24)return double.NaN;
   int samples=Math.Min(96,boundary.Count);double length=Math.Max(6,Math.Min(40,0.08*rawDiameter));var widths=new List<double>();int steps=(int)(2*length/0.5)+1;var profile=new double[steps];
   for(int s=0;s<samples;s++){
    int i=boundary[(int)((long)s*boundary.Count/samples)];int x=i%w,y=i/w;if(x<2||y<2||x>w-3||y>h-3)continue;
    double gx=0.5*(sm[i+1]-sm[i-1]),gy=0.5*(sm[i+w]-sm[i-w]);double norm=Math.Sqrt(gx*gx+gy*gy);if(norm<1e-4)continue;double nx=-gx/norm,ny=-gy/norm; // outward
    double bx=(x+0.5)*k,by=(y+0.5)*k;bool valid=true;
    for(int t=0;t<steps;t++){double dist=-length+t*0.5;double v=Sample(crop,cw,ch,bx+nx*dist,by+ny*dist);if(double.IsNaN(v)){valid=false;break;}profile[t]=v;}
    if(!valid)continue;
    // profile[0] is inside (negative distance), the last sample is outside.
    int plateau=Math.Max(2,steps/5);double inside=0,outside=0;for(int t=0;t<plateau;t++){inside+=profile[t];outside+=profile[steps-1-t];}inside/=plateau;outside/=plateau;
    double contrast=inside-outside;if(contrast<=0)continue;
    // Edge width as the step height divided by the steepest slope (2.563 sigma for a Gaussian-blurred edge). Slow limb darkening inside is removed as a baseline, so it does not stretch the width.
    var slope=new double[steps];for(int t=1;t<steps;t++)slope[t]=-(profile[t]-profile[t-1])/0.5;slope[0]=slope[1];
    var smooth=new double[steps];for(int t=0;t<steps;t++)smooth[t]=(slope[Math.Max(0,t-1)]+2*slope[t]+slope[Math.Min(steps-1,t+1)])/4;
    double baseline=0;for(int t=0;t<plateau;t++)baseline+=smooth[t];baseline/=plateau;
    int peak=1;for(int t=1;t<steps-1;t++)if(smooth[t]>smooth[peak])peak=t;
    double top=smooth[peak]-baseline;if(top<=0)continue;double area=0;
    for(int t=peak;t<steps;t++){double v=smooth[t]-baseline;if(v<0.08*top)break;area+=v*0.5;}
    for(int t=peak-1;t>=0;t--){double v=smooth[t]-baseline;if(v<0.08*top)break;area+=v*0.5;}
    double width=1.05*area/top;if(width>=0.3&&width<=length)widths.Add(width);
   }
   return widths.Count>=12?Median(widths):double.NaN;
  }
 }
}
