// Image-quality engine tests on synthetic images with known star width, noise and edge blur. Pure: no files, UI or database.
using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
 public static class QualityEngineTests {
  static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
  static void Near(double actual,double expected,double tolerance,string what){if(!(Math.Abs(actual/expected-1)<=tolerance))throw new Exception(what+": got "+actual.ToString("0.###")+", expected "+expected.ToString("0.###")+" within "+(100*tolerance).ToString("0")+"%");}

  static double Gauss(Random random){double u=1-random.NextDouble(),v=random.NextDouble();return Math.Sqrt(-2*Math.Log(u))*Math.Cos(2*Math.PI*v);}
  // Star field: sky 1000 with optional linear gradient, Gaussian stars of the given FWHM (and y stretch), white noise.
  public static float[] Field(int w,int h,double fwhm,int stars,double noise,int seed,double stretchY=1,double gradient=0,double scale=1,double offset=0,double amplitudeScale=1){
   var random=new Random(seed);var p=new double[w*h];double sigma=fwhm/2.3548200450309493;
   for(int y=0;y<h;y++)for(int x=0;x<w;x++)p[y*w+x]=1000*(1+gradient*x/(double)w);
   var placed=new List<double[]>();int attempts=0;
   while(placed.Count<stars&&attempts++<stars*200){
    double x=40+random.NextDouble()*(w-80),y=40+random.NextDouble()*(h-80);bool near=false;foreach(var o in placed)if((o[0]-x)*(o[0]-x)+(o[1]-y)*(o[1]-y)<45*45){near=true;break;}if(near)continue;
    placed.Add(new[]{x,y,Math.Exp(Math.Log(500)+random.NextDouble()*Math.Log(10))*amplitudeScale});
   }
   double sy=sigma*stretchY;int reach=(int)Math.Ceiling(6*Math.Max(sigma,sy));
   foreach(var s in placed){
    for(int y=(int)s[1]-reach;y<=(int)s[1]+reach;y++){if(y<0||y>=h)continue;for(int x=(int)s[0]-reach;x<=(int)s[0]+reach;x++){if(x<0||x>=w)continue;
     double sum=0;for(int j=0;j<3;j++)for(int i=0;i<3;i++){double dx=x+(i-1)/3.0-s[0],dy=y+(j-1)/3.0-s[1];sum+=Math.Exp(-0.5*(dx*dx/(sigma*sigma)+dy*dy/(sy*sy)));}
     p[y*w+x]+=s[2]*sum/9.0;}}
   }
   var plane=new float[w*h];for(int i=0;i<plane.Length;i++)plane[i]=(float)((p[i]+noise*Gauss(random))*scale+offset);return plane;
  }
  // Bright disc with faint surface texture, blurred by a Gaussian of the given sigma, on a dark sky.
  public static float[] Disc(int size,double radius,double blur,double noise,int seed,double peak=30000,bool texture=true){
   var random=new Random(seed);var raw=new double[size*size];double cx=size/2.0,cy=size/2.0;
   var cells=new double[16*16];for(int i=0;i<cells.Length;i++)cells[i]=Gauss(random);
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){double r=Math.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy));if(r>=radius){raw[y*size+x]=200;continue;}
    double detail=0;if(texture){double fx=x/(double)size*15,fy=y/(double)size*15;int ix=(int)fx,iy=(int)fy;double tx=fx-ix,ty=fy-iy;double a=cells[iy*16+ix],b=cells[iy*16+ix+1],c=cells[(iy+1)*16+ix],d=cells[(iy+1)*16+ix+1];detail=(a*(1-tx)+b*tx)*(1-ty)+(c*(1-tx)+d*tx)*ty;}
    raw[y*size+x]=200+(peak-200)*(0.8+0.07*detail)*(1-0.25*(r/radius)*(r/radius));}
   var blurred=blur>0.05?Blur(raw,size,size,blur):raw;var plane=new float[size*size];for(int i=0;i<plane.Length;i++)plane[i]=(float)(blurred[i]+noise*Gauss(random));return plane;
  }
  static double[] Blur(double[] src,int w,int h,double sigma){
   int r=(int)Math.Ceiling(4*sigma);var k=new double[2*r+1];double sum=0;for(int i=-r;i<=r;i++){k[i+r]=Math.Exp(-0.5*i*i/(sigma*sigma));sum+=k[i+r];}for(int i=0;i<k.Length;i++)k[i]/=sum;
   var tmp=new double[src.Length];var dst=new double[src.Length];
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){double a=0;for(int i=-r;i<=r;i++){int xx=Math.Min(w-1,Math.Max(0,x+i));a+=k[i+r]*src[y*w+xx];}tmp[y*w+x]=a;}
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){double a=0;for(int i=-r;i<=r;i++){int yy=Math.Min(h-1,Math.Max(0,y+i));a+=k[i+r]*tmp[yy*w+x];}dst[y*w+x]=a;}
   return dst;
  }
  static QualityMeasurement StarShot(double fwhm,double ecc,int stars,double snr,double bg=1000){return new QualityMeasurement{Kind=QualityKind.Stars,Version=QualityMeter.Version,Status=QualityStatus.Ok,Fwhm=fwhm,Hfd=fwhm*0.9,Ecc=ecc,Snr=snr,Stars=stars,StarsUsed=80,Bg=bg,Gradient=0.02,Noise=10,Width=3000,Height=2000,Peak=40000,Max=60000};}
  static List<QualityItem> Items(string cohort,IEnumerable<QualityMeasurement> list){return list.Select(m=>new QualityItem{M=m,Cohort=cohort}).ToList();}

  public static void Run(Action<string,Action> test){
   test("Quality: roofs and broad branches lower scores despite sharp surviving stars",()=>{
    int w=1200,h=900;var clear=Field(w,h,2.4,180,12,511);var roof=(float[])clear.Clone();var branches=(float[])clear.Clone();
    var random=new Random(512);
    for(int y=0;y<h;y++)for(int x=0;x<w;x++){
     if(x<w/3)roof[y*w+x]=(float)(100+12*Gauss(random));
     if(x>w*0.24&&x<w*0.36||y>h*0.48&&y<h*0.62)branches[y*w+x]=(float)(100+12*Gauss(random));
    }
    var clean=QualityMeter.MeasureStars(clear,w,h,false);var blocked=QualityMeter.MeasureStars(roof,w,h,false);var tree=QualityMeter.MeasureStars(branches,w,h,false);
    Check(clean.Obstruction<0.03&&blocked.Obstruction>=0.20&&tree.Obstruction>=0.08,"Foreground obstruction evidence missed or clear sky flagged");
    var group=Enumerable.Repeat(clean,10).Concat(new[]{blocked,tree}).ToList();var scored=QualityScorer.Compute(Items("obstruction",group));
    Check(scored[10].Score<scored[0].Score&&scored[11].Score<scored[0].Score&&scored[10].Flags.Contains("Suspected obstruction"),"Sharp stars compensated for a blocked field");
    var restored=QualityMeasurement.Unpack("roof",blocked.Version,blocked.Kind,blocked.Status,blocked.Pack());Check(restored.Obstruction==blocked.Obstruction&&restored.StarCoverage==blocked.StarCoverage,"Stored spatial evidence was lost");
   });
   test("Quality: smooth gradients and diffuse nebula backgrounds are not foreground obstructions",()=>{
    int w=1200,h=900;var p=Field(w,h,2.4,180,12,513,gradient:0.8);
    for(int y=0;y<h;y++)for(int x=0;x<w;x++)p[y*w+x]+=(float)(1500*Math.Exp(-((x-600.0)*(x-600)/(300*300.0)+(y-450.0)*(y-450)/(220*220.0))));
    var m=QualityMeter.MeasureStars(p,w,h,false);Check(m.Obstruction<0.03,"Smooth sky/nebula structure was classified as foreground");
   });
   test("Quality: identical obstructions across a cohort still reduce the absolute score",()=>{
    var clean=StarShot(2.5,0.3,200,50);clean.Obstruction=0;
    var blocked=StarShot(2.5,0.3,200,50);blocked.Obstruction=0.40;
    var clearScore=QualityScorer.Compute(Items("clear",Enumerable.Repeat(clean,10)))[0];var blockedScore=QualityScorer.Compute(Items("blocked",Enumerable.Repeat(blocked,10)))[0];
    Check(blockedScore.Score<=35&&blockedScore.Score<clearScore.Score,"A uniformly blocked cohort escaped the obstruction penalty");
    Check(double.IsNaN(QualityMeasurement.Unpack("old",1,0,0,"1;2").Obstruction),"Legacy records invented spatial evidence");
   });
   test("Quality: star width follows the true FWHM within 10%",()=>{
    foreach(double fwhm in new[]{1.8,2.2,4.0}){var m=QualityMeter.MeasureStars(Field(1200,900,fwhm,150,12,3),1200,900,false);Check(m.Status==QualityStatus.Ok,"Stars not found at FWHM "+fwhm+": "+m.Stars+" stars, status "+m.Status);Near(m.Fwhm,fwhm,0.10,"FWHM "+fwhm);Check(m.Hfd>0&&m.Hfd<m.Fwhm*1.6,"Half-flux diameter implausible at FWHM "+fwhm+": "+m.Hfd);}
   });
   test("Quality: noise estimate scales with the noise and ignores a sky gradient",()=>{
    var low=QualityMeter.MeasureStars(Field(1200,900,2.5,150,10,5),1200,900,false);var high=QualityMeter.MeasureStars(Field(1200,900,2.5,150,20,5),1200,900,false);var slope=QualityMeter.MeasureStars(Field(1200,900,2.5,150,10,5,1,0.3),1200,900,false);
    Near(high.Noise/low.Noise,2.0,0.10,"Noise ratio");Near(slope.Noise,low.Noise,0.10,"Noise on a gradient");Check(slope.Gradient>low.Gradient+0.1,"Gradient not reported");
   });
   test("Quality: star count rises with the number of stars and falls under cloud",()=>{
    int a=QualityMeter.MeasureStars(Field(1200,900,2.5,40,12,7),1200,900,false).Stars,b=QualityMeter.MeasureStars(Field(1200,900,2.5,80,12,7),1200,900,false).Stars,c=QualityMeter.MeasureStars(Field(1200,900,2.5,160,12,7),1200,900,false).Stars;
    Check(a<b&&b<c,"Star counts not increasing: "+a+", "+b+", "+c);
    int clear=QualityMeter.MeasureStars(Field(1200,900,2.5,120,12,9),1200,900,false).Stars,cloudy=QualityMeter.MeasureStars(Field(1200,900,2.5,120,12,9,1,0,1,0,0.05),1200,900,false).Stars;Check(cloudy<clear*0.6,"Cloud did not reduce the star count: "+clear+" to "+cloudy);
   });
   test("Quality: elongated stars read as eccentric",()=>{
    var round=QualityMeter.MeasureStars(Field(1200,900,3.0,150,12,11),1200,900,false);var trailed=QualityMeter.MeasureStars(Field(1200,900,3.0,150,12,11,1.8),1200,900,false);
    Check(round.Status==QualityStatus.Ok&&trailed.Status==QualityStatus.Ok,"Fields not measured");Check(round.Ecc<0.3,"Round stars eccentricity "+round.Ecc);Check(trailed.Ecc>0.6,"Trailed stars eccentricity "+trailed.Ecc);
   });
   test("Quality: measurements ignore brightness scale and offset",()=>{
    var plain=QualityMeter.MeasureStars(Field(1200,900,2.6,150,12,13),1200,900,false);var scaled=QualityMeter.MeasureStars(Field(1200,900,2.6,150,12,13,1,0,40,2000),1200,900,false);
    Near(scaled.Fwhm,plain.Fwhm,0.03,"FWHM after scaling");Near(scaled.Stars,plain.Stars,0.05,"Stars after scaling");
   });
   test("Quality: a one-shot-colour mosaic does not change star width",()=>{
    var plane=Field(1200,900,3.0,150,12,15);var gains=new[]{0.6f,1.0f,1.0f,0.8f};for(int y=0;y<900;y++)for(int x=0;x<1200;x++)plane[y*1200+x]*=gains[(y&1)*2+(x&1)];
    var m=QualityMeter.MeasureStars(plane,1200,900,true);Check(m.Status==QualityStatus.Ok,"Mosaic not measured");Near(m.Fwhm,3.0,0.20,"Mosaic FWHM");
   });
   test("Quality: empty or tiny frames fail cleanly",()=>{
    var flat=new float[400*400];for(int i=0;i<flat.Length;i++)flat[i]=500;Check(QualityMeter.MeasureStars(flat,400,400,false).Status==QualityStatus.TooFewStars,"A flat frame was scored");
    Check(QualityMeter.MeasureStars(new float[50*50],50,50,false).Status==QualityStatus.Failed,"A tiny frame was accepted");Check(QualityMeter.MeasureDisc(flat,400,400).Status==QualityStatus.NoObject,"A flat frame has no object");
   });
   test("Quality: limb width follows the edge blur and detail falls as blur grows",()=>{
    double radius=170;var widths=new List<double>();var details=new List<double>();
    foreach(double blur in new[]{1.0,2.0,3.0,5.0}){var m=QualityMeter.MeasureDisc(Disc(640,radius,blur,40,21+(int)blur),640,640);Check(m.Status==QualityStatus.Ok,"Disc not measured at blur "+blur+": "+m.Status);widths.Add(m.Limb);details.Add(m.Detail);if(blur>=2.0)Near(m.Limb,2.563*blur/(2*radius),0.15,"Limb at blur "+blur);Near(m.Diameter,2*radius,0.05,"Diameter");}
    for(int i=1;i<widths.Count;i++){Check(widths[i]>widths[i-1],"Limb width not increasing with blur");Check(details[i]<details[i-1],"Detail ratio not falling with blur: "+string.Join(", ",details.Select(d=>d.ToString("0.00")).ToArray()));}
   });
   test("Quality: clipped discs report saturation and the brightest pixel level",()=>{
    var bright=Disc(640,170,2.0,40,31,90000);for(int i=0;i<bright.Length;i++)if(bright[i]>65535)bright[i]=65535;var m=QualityMeter.MeasureDisc(bright,640,640);Check(m.Sat>0.2,"Saturation not detected: "+m.Sat);
    var fine=QualityMeter.MeasureDisc(Disc(640,170,2.0,40,31,30000),640,640);Check(fine.Sat<0.01,"False saturation: "+fine.Sat);Near(fine.Peak/65535.0,0.5,0.25,"Peak level");
   });
   test("Quality: measurements survive being stored",()=>{
    var m=QualityMeter.MeasureStars(Field(1200,900,2.4,100,12,17),1200,900,false);var back=QualityMeasurement.Unpack("abc",m.Version,m.Kind,m.Status,m.Pack());
    Check(back.Hash=="abc"&&back.Fwhm==m.Fwhm&&back.Ecc==m.Ecc&&back.Stars==m.Stars&&back.Width==1200&&back.Status==m.Status,"Stored copy differs");
    var gap=QualityMeasurement.Unpack("x",1,0,1,"");Check(double.IsNaN(gap.Fwhm)&&gap.Stars==0,"Empty data not read as missing");
   });
   test("Quality: sharper, rounder, richer frames score higher in a cohort",()=>{
    var list=new List<QualityMeasurement>();for(int i=0;i<12;i++)list.Add(StarShot(2.0+0.3*i,0.30,200,50));
    var outcomes=QualityScorer.Compute(Items("g",list));
    for(int i=1;i<12;i++)Check(outcomes[i].Score.Value<outcomes[i-1].Score.Value,"Score did not fall as FWHM grew at "+i);
    Check(!outcomes[0].Reference&&outcomes[0].CohortSize==12,"A cohort of 12 used the fixed reference");Check(outcomes[0].Percentile>outcomes[11].Percentile&&outcomes[0].Percentile<=1&&outcomes[11].Percentile>=0,"Percentiles out of order");
    var shuffled=Items("g",list.AsEnumerable().Reverse());var back=QualityScorer.Compute(shuffled);Check(Math.Abs(back[11].Score.Value-outcomes[0].Score.Value)<1e-9,"Order of the cohort changed a score");
    var tidy=new List<QualityMeasurement>();for(int i=0;i<12;i++)tidy.Add(StarShot(2.5,0.30,200,50));var rounder=StarShot(2.5,0.10,200,50);var richer=StarShot(2.5,0.30,400,50);var cleaner=StarShot(2.5,0.30,200,90);
    foreach(var better in new[]{rounder,richer,cleaner}){var group=new List<QualityMeasurement>(tidy);group.Add(better);var o=QualityScorer.Compute(Items("g",group));Check(o[12].Score.Value>o[0].Score.Value,"A better frame did not score higher");}
   });
   test("Quality: weak frames are flagged in plain words",()=>{
    var list=new List<QualityMeasurement>();for(int i=0;i<10;i++)list.Add(StarShot(2.5,0.30,200,50));list.Add(StarShot(2.5,0.30,40,50));list.Add(StarShot(2.5,0.85,200,50));list.Add(StarShot(95,0.30,200,50));
    var o=QualityScorer.Compute(Items("g",list));Check(o[0].Flags==""&&o[0].Score.Value>40,"A normal frame was flagged: "+o[0].Flags);
    Check(o[10].Flags.Contains("Clouded"),"Clouded frame not flagged");Check(o[11].Flags.Contains("Trailed")&&o[11].Detail.Contains("tracking"),"Trailed frame not flagged");Check(o[12].Flags.Contains("Soft")&&o[12].Detail.Contains("focus"),"Soft frame not flagged");
    Check(o[10].Score.Value<o[0].Score.Value&&o[11].Score.Value<o[0].Score.Value&&o[12].Score.Value<o[0].Score.Value,"Flagged frames did not score lower");
   });
   test("Quality: small cohorts use fixed references and uniformly bad nights still show",()=>{
    var good=Items("a",new[]{StarShot(8,0.25,300,60),StarShot(8,0.25,300,60),StarShot(8,0.25,300,60)});var bad=Items("b",new[]{StarShot(70,0.8,10,4),StarShot(70,0.8,10,4),StarShot(70,0.8,10,4)});
    var g=QualityScorer.Compute(good);var b=QualityScorer.Compute(bad);Check(g[0].Reference&&b[0].Reference,"Small cohorts were ranked against each other");Check(g[0].Score.Value>b[0].Score.Value+25,"A uniformly bad set matched a good one: "+g[0].Score.Value+" vs "+b[0].Score.Value);Check(b[0].Flags.Length>0,"A uniformly bad set was not flagged");
    var mixed=QualityScorer.Compute(new List<QualityItem>{new QualityItem{M=new QualityMeasurement{Kind=QualityKind.Stars,Version=1,Status=QualityStatus.Failed},Cohort="x"},new QualityItem{M=null,Cohort="x"}});Check(!mixed[0].Score.HasValue&&!mixed[1].Score.HasValue,"Unreadable measurements were scored");
   });
   test("Quality: too few stars give a low score and a clear flag",()=>{
    var m=new QualityMeasurement{Kind=QualityKind.Stars,Version=1,Status=QualityStatus.TooFewStars,Stars=3,StarsUsed=2,Snr=8,Width=3000,Height=2000};var o=QualityScorer.Compute(Items("g",new[]{m}))[0];
    Check(o.Score.HasValue&&o.Score.Value<=30&&o.Flags.Contains("Clouded"),"Starless frame not capped and flagged: "+o.Score+" "+o.Flags);
   });
   test("Quality: disc sharpness and exposure are separate",()=>{
    Func<double,double,double,QualityMeasurement> disc=(limb,detail,peak)=>new QualityMeasurement{Kind=QualityKind.Disc,Version=1,Status=QualityStatus.Ok,Limb=limb,Detail=detail,Snr=40,Sat=0,BlackClip=0,Peak=peak,Max=peak,Width=1000,Height=1000,Diameter=400};
    var list=new List<QualityMeasurement>();for(int i=0;i<10;i++)list.Add(disc(0.01+0.004*i,3.0-0.2*i,45000));var o=QualityScorer.Compute(Items("d",list));
    for(int i=1;i<10;i++)Check(o[i].Score.Value<o[i-1].Score.Value,"Disc score did not fall with a wider limb at "+i);Check(o[0].ExposureLabel=="Good"&&o[0].ExposureFit.Value>=99,"Well exposed disc not Good: "+o[0].ExposureLabel);
    var dark=QualityScorer.Compute(Items("d",new[]{disc(0.02,2.0,45000),disc(0.02,2.0,12000)}))[1];Check(dark.ExposureLabel=="Under"&&dark.ExposureFit.Value<60&&dark.Flags.Contains("Under"),"Dark disc not Under: "+dark.ExposureLabel);
    var clipped=disc(0.02,2.0,65535);clipped.Sat=0.2;var c=QualityScorer.Compute(Items("d",new[]{clipped}))[0];Check(c.ExposureLabel=="Over"&&c.Score.Value<=QualityScorer.ClipCap&&c.Flags.Contains("Clipped")&&c.ExposureFit.Value<=QualityScorer.ClipCap,"Clipped disc not capped: "+c.Score+" "+c.Flags);
    Check(QualityScorer.ExposureScore(0.75,0)==100&&QualityScorer.ExposureLabel(0.3,0)=="Under"&&QualityScorer.ExposureLabel(0.95,0)=="Over"&&QualityScorer.ExposureLabel(0.7,0)=="Good","Exposure fit bands wrong");
    Check(QualityScorer.Ceiling(0.8)==1.0&&QualityScorer.Ceiling(200)==255&&QualityScorer.Ceiling(3500)==4095&&QualityScorer.Ceiling(50000)==65535,"Sensor ceiling guess wrong");
   });
   test("Quality: cohorts never mix star fields and discs",()=>{
    var items=new List<QualityItem>();for(int i=0;i<9;i++)items.Add(new QualityItem{M=StarShot(2+0.1*i,0.3,200,50),Cohort="same"});
    items.Add(new QualityItem{M=new QualityMeasurement{Kind=QualityKind.Disc,Version=1,Status=QualityStatus.Ok,Limb=0.02,Detail=2,Snr=40,Peak=40000,Max=40000,Width=1000,Height=1000},Cohort="same"});
    var o=QualityScorer.Compute(items);Check(o.Take(9).All(x=>x.CohortSize==9)&&o[9].CohortSize==1,"A disc was ranked with star fields");
   });
  }
 }
}
