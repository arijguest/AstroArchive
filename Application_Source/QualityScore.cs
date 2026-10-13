// Cohort-relative quality scoring. Pure: takes stored measurements, returns scores, ranks and flags.
// Scores are computed on demand from the stored measurements, so changing the comparison group or weights never needs a re-read.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AstroArchive {
 public sealed class QualityItem {public string Cohort;public QualityMeasurement M;}
 public sealed class QualityOutcome {
  public double? Score;public double Percentile=-1;public string Flags="";public string Detail="";public double? ExposureFit;public string ExposureLabel="";public bool Reference;public int CohortSize;
 }
 public static class QualityScorer {
  public const int MinimumCohort=8;
  public const double WeightFwhm=0.35,WeightSnr=0.25,WeightStars=0.20,WeightEcc=0.20;
  public const double WeightObstruction=0.30;
  public const double WeightDetail=0.40,WeightLimb=0.30,WeightDiscSnr=0.30;
  public const double SoftFraction=0.02,SoftRatio=1.25,ClipCap=40,ClipShare=0.02;
  static readonly string[] Ceilings={"1","255","1023","4095","16383","65535"};

  struct Stat {public double Median,Sigma;public int Count;}
  sealed class Group {public List<int> Index=new List<int>();}

  public static QualityOutcome[] Compute(IList<QualityItem> items){
   var outcomes=new QualityOutcome[items.Count];for(int i=0;i<outcomes.Length;i++)outcomes[i]=new QualityOutcome();
   var groups=new Dictionary<string,Group>();
   for(int i=0;i<items.Count;i++){var m=items[i].M;if(m==null||m.Status==QualityStatus.Failed||m.Status==QualityStatus.Unsuitable||m.Status==QualityStatus.NoObject)continue;string key=m.Kind+"|"+items[i].Cohort;Group g;if(!groups.TryGetValue(key,out g)){g=new Group();groups[key]=g;}g.Index.Add(i);}
   foreach(var g in groups.Values){if(items[g.Index[0]].M.Kind==QualityKind.Disc)ScoreDiscs(items,g.Index,outcomes);else ScoreStars(items,g.Index,outcomes);Rank(g.Index,outcomes);}
   return outcomes;
  }

  static double Logistic(double zGood){return 100.0/(1.0+Math.Exp(-1.2*(zGood+0.6)));}
  static double Sigma(double mad,double floor){return Math.Max(1.4826*mad,floor);}
  static Stat Robust(List<double> values,double floor){
   var s=new Stat();s.Count=values.Count;if(values.Count==0)return s;s.Median=QualityMeter.Median(values);var dev=new List<double>(values.Count);foreach(double v in values)dev.Add(Math.Abs(v-s.Median));s.Sigma=Sigma(QualityMeter.Median(dev),floor);return s;
  }
  static double Percentile(List<double> values,double p){if(values.Count==0)return double.NaN;var s=values.ToArray();Array.Sort(s);double pos=p*(s.Length-1);int lo=(int)Math.Floor(pos),hi=(int)Math.Ceiling(pos);return s[lo]+(s[hi]-s[lo])*(pos-lo);}
  static bool Finite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
  static double Z(double x,Stat s,bool lowerIsBetter){double z=(x-s.Median)/s.Sigma;return lowerIsBetter?-z:z;}

  // ----- star fields -----
  static double Mp(QualityMeasurement m){return Math.Max(0.01,m.Width*(double)m.Height/1e6);}
  static void ScoreStars(IList<QualityItem> items,List<int> index,QualityOutcome[] outcomes){
   var fw=new List<double>();var ec=new List<double>();var st=new List<double>();var sn=new List<double>();var bg=new List<double>();var rawFwhm=new List<double>();var rawStars=new List<double>();
   foreach(int i in index){var m=items[i].M;
    if(m.Status==QualityStatus.Ok&&Finite(m.Fwhm)&&m.Fwhm>0){fw.Add(Math.Log(m.Fwhm));rawFwhm.Add(m.Fwhm);}
    if(m.Status==QualityStatus.Ok&&Finite(m.Ecc))ec.Add(m.Ecc);
    st.Add(Math.Log(1+m.Stars));rawStars.Add(m.Stars);
    if(Finite(m.Snr)&&m.Snr>0)sn.Add(Math.Log(Math.Max(m.Snr,0.1)));
    if(Finite(m.Bg))bg.Add(m.Bg);}
   var fwS=Robust(fw,0.04);var ecS=Robust(ec,0.04);var stS=Robust(st,0.10);var snS=Robust(sn,0.08);var bgS=new Stat();if(bg.Count>0){bgS=Robust(bg,0.02*Math.Max(1e-6,Math.Abs(QualityMeter.Median(bg))));}
   bool cohort=index.Count>=MinimumCohort;
   double p10=Percentile(rawFwhm,0.10),p90Stars=Percentile(rawStars,0.90);
   foreach(int i in index){
    var m=items[i].M;var o=outcomes[i];o.CohortSize=index.Count;o.Reference=!cohort;var terms=new List<double[]>();
    bool haveShape=m.Status==QualityStatus.Ok&&Finite(m.Fwhm)&&Finite(m.Ecc);double mp=Mp(m);
    if(haveShape){
     double zf=cohort&&fwS.Count>=MinimumCohort?Z(Math.Log(m.Fwhm),fwS,true):-(Math.Log(m.Fwhm/m.Width)-Math.Log(0.004))/0.5;
     double ze=cohort&&ecS.Count>=MinimumCohort?Z(m.Ecc,ecS,true):-(m.Ecc-0.40)/0.15;
     terms.Add(new[]{WeightFwhm,Logistic(zf)});terms.Add(new[]{WeightEcc,Logistic(ze)});
    }
    {double zs=cohort&&stS.Count>=MinimumCohort?Z(Math.Log(1+m.Stars),stS,false):(Math.Log(1+m.Stars/mp)-Math.Log(1+150))/0.8;terms.Add(new[]{WeightStars,Logistic(zs)});}
    if(Finite(m.Snr)&&m.Snr>0){double zn=cohort&&snS.Count>=MinimumCohort?Z(Math.Log(Math.Max(m.Snr,0.1)),snS,false):(Math.Log(Math.Max(m.Snr,0.1))-Math.Log(25))/0.8;terms.Add(new[]{WeightSnr,Logistic(zn)});}
    double obstruction=Finite(m.Obstruction)?Math.Max(0,Math.Min(1,m.Obstruction)):0;
    if(Finite(m.Obstruction))terms.Add(new[]{WeightObstruction,100*Math.Pow(1-obstruction,3)});
    double score=Combine(terms);
    // Absolute loss of usable field remains a penalty even if every comparison
    // frame has the same obstruction; sharp surviving stars cannot compensate.
    if(obstruction>=0.25)score=Math.Min(score,35);else if(obstruction>=0.10)score=Math.Min(score,60);
    if(!haveShape)score=Math.Min(score,30);
    o.Score=score;
    // Flags
    var flags=new List<string>();string hint="";
    bool clouded=obstruction<0.03&&(m.Status==QualityStatus.TooFewStars||(cohort&&p90Stars>0&&m.Stars<0.5*p90Stars));
    bool soft=haveShape&&(m.Fwhm/m.Width>SoftFraction||(cohort&&Finite(p10)&&m.Fwhm>SoftRatio*p10));
    bool trailed=haveShape&&(m.Ecc>0.65||(cohort&&ecS.Count>=MinimumCohort&&Z(m.Ecc,ecS,false)>3&&m.Ecc>0.5));
    bool bright=Finite(m.Gradient)&&m.Gradient>0.6||(cohort&&bg.Count>=MinimumCohort&&Finite(m.Bg)&&Z(m.Bg,bgS,false)>3);
    if(obstruction>=0.03)flags.Add("Suspected obstruction");
    if(clouded)flags.Add("Clouded");
    if(soft){flags.Add("Soft");hint=FocusHint(m);}
    if(trailed){flags.Add("Trailed");hint="likely tracking or guiding";}
    if(bright)flags.Add("Bright sky");
    o.Flags=string.Join(", ",flags.ToArray());
    // Tooltip text
    var d=new StringBuilder();
    d.Append("Quality ").Append(Math.Round(score).ToString(CultureInfo.InvariantCulture)).Append(cohort?" · compared with "+index.Count+" similar frames":" · fixed reference (fewer than "+MinimumCohort+" similar frames)");
    if(haveShape){d.Append("\nStar width (FWHM) ").Append(m.Fwhm.ToString("0.0",CultureInfo.InvariantCulture)).Append(" px · ").Append((100*m.Fwhm/m.Width).ToString("0.00",CultureInfo.InvariantCulture)).Append("% of width · HFD ").Append(m.Hfd.ToString("0.0",CultureInfo.InvariantCulture)).Append(" px");
     d.Append("\nRoundness (eccentricity) ").Append(m.Ecc.ToString("0.00",CultureInfo.InvariantCulture));
     if(Finite(m.FwhmCentre)&&Finite(m.FwhmEdge))d.Append(" · FWHM centre ").Append(m.FwhmCentre.ToString("0.0",CultureInfo.InvariantCulture)).Append(" / edge ").Append(m.FwhmEdge.ToString("0.0",CultureInfo.InvariantCulture)).Append(" px");}
    else d.Append("\nToo few usable stars to measure star shape (").Append(m.StarsUsed).Append(" found).");
    d.Append("\nStars ").Append(m.Stars).Append(" detected · ").Append(m.StarsUsed).Append(" measured");
    if(Finite(m.Snr))d.Append(" · SNR ").Append(m.Snr.ToString("0",CultureInfo.InvariantCulture));
    if(Finite(m.Bg))d.Append("\nSky level ").Append(m.Bg.ToString("0",CultureInfo.InvariantCulture)).Append(Finite(m.Gradient)?" · gradient "+(100*m.Gradient).ToString("0",CultureInfo.InvariantCulture)+"%":"");
    if(Finite(m.Obstruction))d.Append("\nSuspected obstructed field ").Append((100*obstruction).ToString("0",CultureInfo.InvariantCulture)).Append("% · star-poor regions with sharp background boundaries");
    if(Finite(m.StarCoverage))d.Append("\nSpatial star coverage ").Append((100*m.StarCoverage).ToString("0",CultureInfo.InvariantCulture)).Append("% of expected occupied regions");
    if(flags.Count>0)d.Append("\nFlags: ").Append(o.Flags).Append(hint.Length>0?" ("+hint+")":"");
    o.Detail=d.ToString();
   }
  }
  static string FocusHint(QualityMeasurement m){
   if(m.Ecc>0.55)return "likely tracking or guiding";
   if(Finite(m.FwhmCentre)&&Finite(m.FwhmEdge)&&m.FwhmCentre>0&&m.FwhmEdge/m.FwhmCentre>1.3)return "likely tilt or field curvature";
   return "likely focus or seeing";
  }
  static double Combine(List<double[]> terms){
   double w=0,s=0;foreach(var t in terms){w+=t[0];s+=t[0]*Math.Log(Math.Max(1.0,t[1]));}return w>0?Math.Exp(s/w):0;
  }

  // ----- discs -----
  public static double Ceiling(double max){
   if(!Finite(max)||max<=1.0001)return 1.0;foreach(string c in Ceilings){double v=double.Parse(c,CultureInfo.InvariantCulture);if(max<=v)return v;}return 4294967295.0;
  }
  public static double ExposureScore(double peakFraction,double saturatedShare){
   double fit;
   if(peakFraction<0.6)fit=100*Math.Pow(Math.Max(0,peakFraction)/0.6,1.5);
   else if(peakFraction<=0.9)fit=100;
   else fit=100-60*Math.Min(1.0,(peakFraction-0.9)/0.1);
   if(saturatedShare>ClipShare)fit=Math.Min(fit,ClipCap);
   return Math.Max(0,Math.Min(100,fit));
  }
  public static string ExposureLabel(double peakFraction,double saturatedShare){
   if(saturatedShare>ClipShare||peakFraction>0.92)return "Over";if(peakFraction<0.5)return "Under";return "Good";
  }
  static void ScoreDiscs(IList<QualityItem> items,List<int> index,QualityOutcome[] outcomes){
   var de=new List<double>();var li=new List<double>();var sn=new List<double>();double maxOfMax=0;
   foreach(int i in index){var m=items[i].M;if(Finite(m.Detail)&&m.Detail>0)de.Add(Math.Log(m.Detail));if(Finite(m.Limb)&&m.Limb>0)li.Add(Math.Log(m.Limb));if(Finite(m.Snr)&&m.Snr>0)sn.Add(Math.Log(m.Snr));if(Finite(m.Max)&&m.Max>maxOfMax)maxOfMax=m.Max;}
   var deS=Robust(de,0.10);var liS=Robust(li,0.10);var snS=Robust(sn,0.10);bool cohort=index.Count>=MinimumCohort;double ceiling=Ceiling(maxOfMax);
   foreach(int i in index){
    var m=items[i].M;var o=outcomes[i];o.CohortSize=index.Count;o.Reference=!cohort;var terms=new List<double[]>();
    if(Finite(m.Detail)&&m.Detail>0&&cohort&&deS.Count>=MinimumCohort)terms.Add(new[]{WeightDetail,Logistic(Z(Math.Log(m.Detail),deS,false))});
    if(Finite(m.Limb)&&m.Limb>0){double z=cohort&&liS.Count>=MinimumCohort?Z(Math.Log(m.Limb),liS,true):-(Math.Log(m.Limb)-Math.Log(0.02))/0.6;terms.Add(new[]{WeightLimb,Logistic(z)});}
    if(Finite(m.Snr)&&m.Snr>0){double z=cohort&&snS.Count>=MinimumCohort?Z(Math.Log(m.Snr),snS,false):(Math.Log(m.Snr)-Math.Log(40))/0.8;terms.Add(new[]{WeightDiscSnr,Logistic(z)});}
    var flags=new List<string>();
    if(terms.Count>0){double score=Combine(terms);if(m.Sat>ClipShare){score=Math.Min(score,ClipCap);}o.Score=score;}
    if(m.Sat>ClipShare)flags.Add("Clipped");
    if(m.BlackClip>0.3)flags.Add("Black clipped");
    if(Finite(m.Peak)&&ceiling>0){double fraction=m.Peak/ceiling;o.ExposureFit=ExposureScore(fraction,m.Sat);o.ExposureLabel=ExposureLabel(fraction,m.Sat);if(o.ExposureLabel=="Under")flags.Add("Under-exposed");else if(o.ExposureLabel=="Over"&&!flags.Contains("Clipped"))flags.Add("Over-exposed");}
    o.Flags=string.Join(", ",flags.ToArray());
    var d=new StringBuilder();
    if(o.Score.HasValue)d.Append("Sharpness ").Append(Math.Round(o.Score.Value).ToString(CultureInfo.InvariantCulture)).Append(cohort?" · compared with "+index.Count+" similar frames":" · fixed reference (fewer than "+MinimumCohort+" similar frames)");else d.Append("Sharpness not available for this frame");
    if(Finite(m.Limb))d.Append("\nLimb edge width ").Append((100*m.Limb).ToString("0.00",CultureInfo.InvariantCulture)).Append("% of the object's diameter");else d.Append(m.FillsFrame?"\nObject fills the frame: no complete limb to measure":"\nLimb edge could not be measured");
    if(Finite(m.Detail))d.Append("\nDetail ratio ").Append(m.Detail.ToString("0.00",CultureInfo.InvariantCulture));
    if(Finite(m.Snr))d.Append("\nSNR ").Append(m.Snr.ToString("0",CultureInfo.InvariantCulture));
    if(o.ExposureFit.HasValue&&Finite(m.Peak))d.Append("\nExposure ").Append(o.ExposureLabel).Append(" · brightest pixels at ").Append((100*m.Peak/ceiling).ToString("0",CultureInfo.InvariantCulture)).Append("% of the sensor ceiling · ").Append((100*m.Sat).ToString("0.0",CultureInfo.InvariantCulture)).Append("% clipped");
    if(flags.Count>0)d.Append("\nFlags: ").Append(o.Flags);
    o.Detail=d.ToString();
   }
  }

  static void Rank(List<int> index,QualityOutcome[] outcomes){
   var scored=index.Where(i=>outcomes[i].Score.HasValue).ToList();int n=scored.Count;if(n==0)return;
   var sorted=scored.Select(i=>outcomes[i].Score.Value).OrderBy(v=>v).ToArray();
   foreach(int i in scored){double s=outcomes[i].Score.Value;int below=0,equal=0;int lo=Array.BinarySearch(sorted,s);if(lo<0)lo=~lo;while(lo>0&&sorted[lo-1]==s)lo--;below=lo;int hi=lo;while(hi<n&&sorted[hi]==s)hi++;equal=hi-lo;
    outcomes[i].Percentile=n<=1?0.5:(below+0.5*(equal-1))/(n-1);}
  }
 }
}
