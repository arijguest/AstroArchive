// Explicit header aliases and small, independently versioned recognition profiles.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
namespace AstroArchive {
    public sealed class RecognitionProfile {
        public string Id,Pattern,Make;
        public bool Software;
        public bool Matches(string text) {
            return Regex.IsMatch(text??"",Pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        }
    }
    public static class MetadataProfiles {
        public static string ExposureEvidence(Frame frame) {
            if(frame.Kind=="Video")return "Video length: "+frame.ExposureText+"\n"+(frame.VideoDurationSeconds.HasValue?"Source: "+frame.VideoDurationSource:"No readable recording duration is available.");
            var lines=new List<string>{"Reported exposure: "+frame.ExposureText};
            MetadataFact fact;
            if(frame.Facts!=null&&frame.Facts.TryGetValue("Exposure",out fact)&&fact!=null&&!string.IsNullOrEmpty(fact.Source))lines.Add("Source: "+fact.Source);
            var image=frame.Images==null?null:frame.Images.FirstOrDefault(i=>i.Key==frame.ImageKey)??frame.Images.FirstOrDefault();
            if(image!=null&&image.Headers!=null)foreach(string key in new[]{"EXPTIME","EXPOSURE","EXP_TIME","EXPOS","TOTALEXP","TOTEXP","EXPTOTAL","INTTIME","SUBEXP","SUBEXPT","EXPOSUB","EXP_SUB","NCOMBINE","STACKCNT","NSTACK","STACKNUM","NSUBS","SUBCOUNT"}) {
                string value;
                if(!image.Headers.TryGetValue(key,out value)||string.IsNullOrWhiteSpace(value))continue;
                lines.Add(key+" = "+value);
            }
            if(frame.StackCount>0)lines.Add("Combined frames: "+frame.StackCount);
            double? sub,gain;if(Classifier.FilenameExposureGain(frame.OriginalName,out sub,out gain))lines.Add("Filename: "+Util.Num(sub)+" s per sub; gain "+Util.Num(gain));
            int seestarCount;double seestarSeconds;
            if(frame.Kind=="Stack"&&frame.MakeText=="Seestar"&&Classifier.SeestarStackFilename(frame.OriginalName,out seestarCount,out seestarSeconds))lines.Add("Seestar filename: "+seestarCount+" subs × "+Util.Num(seestarSeconds)+" s = "+Util.Num(seestarCount*seestarSeconds)+" s total.");
            else if(frame.Kind=="Stack"&&frame.Facts!=null&&frame.Facts.TryGetValue("StackCount",out fact)&&fact!=null&&(fact.Source??"").StartsWith("DWARF stack integration:"))lines.Add(fact.Source);
            else if(frame.Kind=="Stack")lines.Add("Stack exposure may be per-sub or total.");
            if(lines.Count==1)lines.Add("Exposure source unknown.");
            return string.Join("\n",lines);
        }
        static readonly RecognitionProfile[] profiles= {
            new RecognitionProfile {
                Id="vaonis-1",Pattern=@"\b(Vaonis|Vespera(?:\s*(?:II|Pro))?|Stellina)\b",Make="Vaonis"
            },    new RecognitionProfile {
                Id="unistellar-1",Pattern=@"\b(Unistellar|eVscope(?:\s*2)?|eQuinox(?:\s*2)?|Odyssey(?:\s*Pro)?)\b",Make="Unistellar"
            },    new RecognitionProfile {
                Id="nina-1",Pattern=@"\bN\.?I\.?N\.?A\.?\b",Software=true
            },    new RecognitionProfile {
                Id="asiair-1",Pattern=@"\bASIAIR\b",Software=true
            },    new RecognitionProfile {
                Id="ekos-1",Pattern=@"\b(Ekos|KStars|INDI)\b",Software=true
            },    new RecognitionProfile {
                Id="sharpcap-1",Pattern=@"\bSharpCap\b",Software=true
            }
        };
        static void Fact(Frame f,string field,string value,string raw,string source,string unit=null) {
            if(string.IsNullOrEmpty(value))return;
            f.Facts[field]=new MetadataFact {
                Value=value,Raw=raw,Source=source,Unit=unit
            };
        }
        static string Alias(Frame f,FitsHeader h,string field,params string[] keys) {
            string first="",firstKey="";
            foreach(string key in keys) {
                string value=h.Get(key);
                if(value.Length==0)continue;
                if(first.Length==0) {
                    first=value;
                    firstKey=key;
                }
                else if(!string.Equals(first,value,StringComparison.OrdinalIgnoreCase))f.MetadataConflicts.Add(field+": "+firstKey+"="+first+"; "+key+"="+value);
            }
            Fact(f,field,first,first,"Header: "+firstKey);
            return first;
        }
        static double? Number(Frame f,FitsHeader h,string field,string unit,params string[] keys) {
            string raw=Alias(f,h,field,keys);
            double value;
            if(!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||double.IsNaN(value)||double.IsInfinity(value))return null;
            Fact(f,field,value.ToString("R",CultureInfo.InvariantCulture),raw,"Header: "+keys.First(k=>h.Get(k).Length>0),unit);
            return value;
        }
        static string Comment(FitsHeader h,params string[] keys) {
            foreach(string key in keys) {
                string comment;
                if(h.Get(key).Length>0&&h.Comments.TryGetValue(key,out comment))return comment;
            }
            return "";
        }
        public static void Apply(Frame f,FitsHeader h,AssetInfo asset) {
            f.Format=asset.Format;
            f.Images=asset.Images;
            f.ImageKey=asset.Images.Count==0?null:asset.Images[0].Key;
            f.ClassificationVersion=Assets.ClassificationVersion;
            f.Facts=new Dictionary<string,MetadataFact>();
            f.MetadataConflicts=new List<string>();
            f.TelescopeModel=Alias(f,h,"TelescopeModel","TELESCOP","TELMODEL");
            f.CameraModel=Alias(f,h,"CameraModel","CAMMODEL","CAMERA","INSTRUME");
            f.CameraId=Alias(f,h,"CameraId","CAM-SER","CAMSN","CAMSER","SERIAL");
            f.AcquisitionSoftware=Alias(f,h,"AcquisitionSoftware","SWCREATE","CREATOR","PROGRAM");
            if(f.Make=="Seestar")f.DeviceProfile="seestar-1";
            if(f.Make=="DWARFLAB")f.DeviceProfile="dwarf-1";
            foreach(var profile in profiles) {
                if(!profile.Matches(profile.Software?f.AcquisitionSoftware:f.TelescopeModel+" "+f.CameraModel))continue;
                if(profile.Software)f.AcquisitionProfile=profile.Id;
                else {
                    if(f.Make!="Unknown"&&f.Make!=profile.Make) {
                        f.MetadataConflicts.Add("Instrument profile conflicts with existing make: "+f.Make);
                        continue;
                    }
                    f.Make=profile.Make;
                    if(!string.IsNullOrEmpty(f.TelescopeModel))f.Model=f.TelescopeModel;
                    f.DeviceProfile=profile.Id;
                    f.MakeEvidence="Explicit instrument metadata; "+profile.Id;
                }
            }
            if(f.Camera=="Unknown"&&(!string.IsNullOrEmpty(f.CameraModel)||!string.IsNullOrEmpty(f.CameraId))&&f.Make!="DWARFLAB") {
                f.Camera="Primary";
                f.CameraEvidence="Explicit camera metadata";
            }
            f.OpticalConfiguration=Alias(f,h,"OpticalConfiguration","OPTCONF","OPTICAL");
            f.ReadoutMode=Alias(f,h,"ReadoutMode","READMODE","READOUTM");
            f.Roi=Alias(f,h,"Roi","ROI");
            if(string.IsNullOrEmpty(f.Roi)&&h.Number("XORGSUBF").HasValue&&h.Number("YORGSUBF").HasValue)f.Roi=h.Get("XORGSUBF")+","+h.Get("YORGSUBF")+","+f.Width+","+f.Height;
            f.Offset=Number(f,h,"Offset","camera units","OFFSET","BLKLEVEL");
            f.ElectronsPerAdu=Number(f,h,"ElectronsPerAdu","e-/ADU","EGAIN","CCDGAIN");
            double? gain=Number(f,h,"Gain","camera units","GAIN");
            if(gain.HasValue)f.Gain=gain;
            f.GainUnit=f.Gain.HasValue?"camera units":null;
            double? exposure=Number(f,h,"Exposure","s","EXPTIME","EXPOSURE","EXP_TIME","EXPOS"),temperature=Number(f,h,"Temperature","C","CCD-TEMP","SENSOR_T","SENSORT","CAMTEMP","TEMPERAT");
            if(exposure.HasValue) {
                string comment=Comment(h,"EXPTIME","EXPOSURE","EXP_TIME","EXPOS");
                f.Exposure=Regex.IsMatch(comment,@"\b(ms|milliseconds?)\b",RegexOptions.IgnoreCase)?exposure/1000:exposure;
                f.Facts["Exposure"].Value=f.Exposure.Value.ToString("R",CultureInfo.InvariantCulture);
            }
            if(temperature.HasValue) {
                string comment=Comment(h,"CCD-TEMP","SENSOR_T","SENSORT","CAMTEMP","TEMPERAT");
                f.Temperature=Regex.IsMatch(comment,@"\bKelvin\b|\[K\]",RegexOptions.IgnoreCase)?temperature-273.15:Regex.IsMatch(comment,@"\bFahrenheit\b|\[F\]",RegexOptions.IgnoreCase)?(temperature-32)*5/9:temperature;
                f.Facts["Temperature"].Value=f.Temperature.Value.ToString("R",CultureInfo.InvariantCulture);
            }
            string gainComment=Comment(h,"GAIN");
            if(Regex.IsMatch(gainComment,@"e-?\s*/\s*ADU|electrons?\s*/\s*ADU",RegexOptions.IgnoreCase)) {
                f.ElectronsPerAdu=h.Number("GAIN");
                f.Gain=null;
                f.GainUnit=null;
                f.Facts.Remove("Gain");
                Fact(f,"ElectronsPerAdu",f.ElectronsPerAdu.HasValue?f.ElectronsPerAdu.Value.ToString("R",CultureInfo.InvariantCulture):null,h.Get("GAIN"),"Header: GAIN","e-/ADU");
            }
            f.LinearData=(f.Format=="FITS"||f.Format=="SER")?(bool?)true:null;
            string linear=h.Get("LINEAR");
            if(linear=="T"||linear=="true"||linear=="1")f.LinearData=true;
            else if(linear=="F"||linear=="false"||linear=="0")f.LinearData=false;
            if(f.Format=="JPEG"||f.Format=="JPG")f.LinearData=false;
            string stretch=h.Get("STRETCHED","NONLINEAR");
            if(stretch=="T")f.LinearData=false;
            f.RegistrationState=f.Calibration=="Registered"?"Registered":"Unknown";
            f.CalibrationSteps=h.Get("CALSTAT");
            string obs=h.Get("DATE-OBS","DATEOBS","DATE_OBS"),timeSystem=h.Get("TIMESYS");
            DateTimeOffset utc;
            bool eligible=obs.Length>10&&(timeSystem.Length==0||timeSystem.Equals("UTC",StringComparison.OrdinalIgnoreCase))&&(f.Format=="FITS"||Regex.IsMatch(obs,@"(Z|[+-]\d\d:\d\d)$"));
            if(eligible&&DateTimeOffset.TryParse(obs,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out utc)) {
                f.ObservedUtc=utc.UtcDateTime.ToString("o");
                f.TimeZoneId="UTC";
                Fact(f,"ObservedUtc",f.ObservedUtc,obs,"Header: DATE-OBS","UTC");
            }
            else {
                f.ObservedUtc=null;
                if(!string.IsNullOrEmpty(f.Observed))f.TimeSource="Capture time (timezone unknown)";
            }
            Fact(f,"Kind",f.Kind,h.Get("IMAGETYP","IMAGETYPE","FRAME","FRAMETYP"),h.Get("IMAGETYP","IMAGETYPE","FRAME","FRAMETYP").Length>0?"Header":"Filename / folder rules");
            Fact(f,"Target",f.Target,h.Get("OBJECT","OBJNAME","TARGET"),f.TargetEvidence);
            Fact(f,"Model",f.Model,h.Get("TELESCOP","INSTRUME"),f.MakeEvidence);
            Fact(f,"Camera",f.Camera,h.Get("CAMERA"),f.CameraEvidence);
            if(!string.IsNullOrEmpty(asset.Note))f.Notes+=asset.Note+" ";
            if(f.MetadataConflicts.Count>0)f.Notes+="Conflicting metadata aliases require review. ";
        }
    }
}
