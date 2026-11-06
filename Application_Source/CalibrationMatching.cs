using System;
using System.Collections.Generic;
using System.Linq;
namespace AstroArchive {
    public sealed class CalibrationDecision {
        public string Status {
            get;
            set;
        }
        public List<string> Reasons {
            get;
            set;
        }
        public bool Accepted {
            get {
                return Status=="Accepted";
            }
        }
        // A partial match agrees on everything both files record but one side lacks descriptive metadata
        // (typical of master files). It is used only when no fully matching calibration exists.
        public bool Usable {
            get {
                return Status=="Accepted"||Status=="Partial match";
            }
        }
    }
    public static class CalibrationMatching {
        public const int FlatNightWindow=30;
        public const double PreferredTemperatureDifference=3;
        static bool TemperatureSensitive(Frame frame) {
            return frame.Kind=="Dark"||frame.Kind=="Master dark"||frame.Kind=="Dark flat"||frame.Kind=="Master dark flat";
        }
        static double TemperatureDistance(IEnumerable<Frame> inputs,Frame calibration) {
            return inputs.Max(input=>Math.Abs(input.Temperature.Value-calibration.Temperature.Value));
        }
        // Share the application's exact/partial ranking and one-set-per-kind policy
        // between automatic stacking, exports and flat preparation.
        public static List<Frame> Select(IEnumerable<Frame> inputs,IEnumerable<Frame> candidates) {
            var targets=inputs.ToList();var all=candidates.ToList();var result=new List<Frame>();
            foreach(string kind in new[]{"Dark","Flat","Bias","Dark flat"})result.AddRange(Choose(targets,all,kind));
            return result;
        }
        public static List<string> Warnings(IEnumerable<Frame> inputs,IEnumerable<Frame> calibrations) {
            var targets=inputs.ToList();var warnings=new List<string>();
            foreach(var cal in calibrations.Where(TemperatureSensitive)) {
                var applicable=targets.Where(target=>Evaluate(target,cal).Usable).ToList();if(applicable.Count==0)continue;
                double difference=TemperatureDistance(applicable,cal);
                if(difference>PreferredTemperatureDifference)warnings.Add(cal.Kind+" "+(cal.OriginalName??cal.Hash)+": sensor temperature differs by up to "+Util.Num(difference)+" C. No closer compatible temperature set was available at this match quality; continuing with this calibration.");
            }
            return warnings.Distinct().ToList();
        }
        static bool Missing(string value) {
            return string.IsNullOrWhiteSpace(value)||value.Trim().Equals("Unknown",StringComparison.OrdinalIgnoreCase);
        }
        static bool Same(string a,string b) {
            return string.Equals((a??"").Trim(),(b??"").Trim(),StringComparison.OrdinalIgnoreCase);
        }
        // Lights of an identified target (other than the Milky Way) count as telephoto via CameraDetection.DefaultForTarget.
        static string DetectedCamera(Frame f) {
            return Missing(f.Camera)?null:f.Camera.Trim();
        }
        static bool FixedOptics(Frame f) {
            return f.MakeText=="Seestar"||f.MakeText=="DWARFLAB"||f.MakeText==CelestronOrigin.Make;
        }
        public static int? NightDays(string a,string b) {
            DateTime x,y;
            if(!DateTime.TryParseExact((a??"").Trim(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out x)||!DateTime.TryParseExact((b??"").Trim(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out y))return null;
            return (int)Math.Abs((x-y).TotalDays);
        }
        static bool Unresolved(Frame f) {
            if(f.MetadataConflicts==null)return false;
            var relevant=new[] {
                "CameraId","CameraModel","ReadoutMode","Roi","Gain","Offset","Exposure","Temperature","Model","Filter","BinX","BinY","Kind","Calibration","Camera"
            };
            foreach(string conflict in f.MetadataConflicts)foreach(string field in relevant)if(conflict.StartsWith(field+":",StringComparison.Ordinal)) {
                MetadataFact fact;
                if(f.Facts==null||!f.Facts.TryGetValue(field,out fact)||fact.Source!="User")return true;
            }
            return false;
        }
        // Values recorded on both inputs must agree. Values that decide the correction itself (telescope, binning,
        // gain, dark exposure/temperature) must be known. Descriptive values missing on one input only lower the
        // match to Partial, so masters that drop such headers can still be chosen when nothing better exists.
        public static CalibrationDecision Evaluate(Frame light,Frame cal) {
            var result=new CalibrationDecision {
                Status="Accepted",Reasons=new List<string>()
            };
            Action<string> reject=reason=> {
                result.Status="Rejected";
                result.Reasons.Add(reason);
            };
            Action<string> review=reason=> {
                if(result.Status!="Rejected")result.Status="Needs review";
                result.Reasons.Add(reason);
            };
            Action<string> gap=reason=> {
                if(result.Status=="Accepted")result.Status="Partial match";
                result.Reasons.Add(reason);
            };
            if(Unresolved(light)||Unresolved(cal))review("Conflicting metadata requires review before automatic matching.");
            if(!Assets.IsCalibration(cal.Kind))reject("Not a calibration frame.");
            if(cal.Rejected||CaptureScreening.FileProblem(cal)||cal.Status=="Failed")reject("Rejected or changed calibration.");
            if(light.Calibration=="Calibrated"||light.Calibration=="Registered")reject("Input already calibrated or registered.");
            if(!Assets.CanStack(light)||!Assets.CanStack(cal))reject("Linear, decodable scientific data are required.");
            if(Missing(light.Telescope)||Missing(cal.Telescope))review("Telescope is unknown.");
            else if(!Same(light.Telescope,cal.Telescope))reject("Telescope differs.");
            if(!Missing(light.MakeText)&&!Missing(cal.MakeText)&&!Same(light.MakeText,cal.MakeText))reject("Telescope make differs.");
            else if(Missing(light.MakeText)!=Missing(cal.MakeText))gap("Telescope make is unrecorded on one input.");
            string lightCamera=DetectedCamera(light),calCamera=DetectedCamera(cal);
            if(lightCamera!=null&&calCamera!=null&&!Same(lightCamera,calCamera))reject("Camera channel differs.");
            else if((lightCamera==null)!=(calCamera==null))gap("Camera channel is unrecorded on one input.");
            if(light.Width!=cal.Width||light.Height!=cal.Height||light.Channels!=cal.Channels)reject("Image dimensions or channels differ.");
            if(!string.IsNullOrWhiteSpace(light.Bayer)&&!string.IsNullOrWhiteSpace(cal.Bayer)&&!Same(light.Bayer,cal.Bayer))reject("Bayer pattern differs.");
            else if(string.IsNullOrWhiteSpace(light.Bayer)!=string.IsNullOrWhiteSpace(cal.Bayer))gap("Bayer pattern is unrecorded on one input.");
            if(light.BinX<=0||light.BinY<=0||cal.BinX<=0||cal.BinY<=0)review("Binning is unknown.");
            else if(light.BinX!=cal.BinX||light.BinY!=cal.BinY)reject("Binning differs.");
            foreach(string field in new[] {
                "CameraId","CameraModel","ReadoutMode","Roi","GainUnit"
            }) {
                var property=typeof(Frame).GetProperty(field);
                string a=(string)property.GetValue(light,null),b=(string)property.GetValue(cal,null);
                if(Missing(a)&&Missing(b))continue;
                if(Missing(a)||Missing(b))gap(field+" is unrecorded on one input.");
                else if(!Same(a,b))reject(field+" differs.");
            }
            if(light.Offset.HasValue!=cal.Offset.HasValue)gap("Offset is unrecorded on one input.");
            else if(light.Offset.HasValue&&Math.Abs(light.Offset.Value-cal.Offset.Value)>0.001)reject("Offset differs.");
            if(!light.Gain.HasValue||!cal.Gain.HasValue)review("Camera gain setting is unknown (e-/ADU is a separate quantity).");
            else if(Math.Abs(light.Gain.Value-cal.Gain.Value)>0.001)reject("Camera gain setting differs.");
            bool dark=cal.Kind=="Dark"||cal.Kind=="Master dark"||cal.Kind=="Dark flat"||cal.Kind=="Master dark flat",flat=cal.Kind=="Flat"||cal.Kind=="Master flat",darkFlat=cal.Kind=="Dark flat"||cal.Kind=="Master dark flat";
            if(darkFlat&&light.Kind!="Flat"&&light.Kind!="Master flat")reject("Dark flats calibrate raw flats, rather than lights.");
            if(dark) {
                if(!light.Exposure.HasValue||!cal.Exposure.HasValue)review("Exposure is unknown.");
                else if(Math.Abs(light.Exposure.Value-cal.Exposure.Value)>Math.Max(0.001,light.Exposure.Value*0.001))reject("Exposure differs; dark scaling is not assumed.");
                if(!light.Temperature.HasValue||!cal.Temperature.HasValue)review("Sensor temperature is unknown.");
                else if(Math.Abs(light.Temperature.Value-cal.Temperature.Value)>PreferredTemperatureDifference)result.Reasons.Add("Sensor temperature differs by "+Util.Num(Math.Abs(light.Temperature.Value-cal.Temperature.Value))+" C; usable as a fallback when no closer compatible calibration is available.");
            }
            if(flat) {
                if(!Missing(light.Filter)&&!Missing(cal.Filter)&&!Same(light.Filter,cal.Filter))reject("Filter differs.");
                else if(Missing(light.Filter)||Missing(cal.Filter))gap("Filter is unrecorded on one input.");
                bool configured=!Missing(light.OpticalConfiguration)&&!Missing(cal.OpticalConfiguration);
                if(configured&&!Same(light.OpticalConfiguration,cal.OpticalConfiguration))reject("Optical configuration differs.");
                else if(Missing(light.OpticalConfiguration)!=Missing(cal.OpticalConfiguration))gap("Optical configuration is unrecorded on one input.");
                // Fixed-optics telescopes (or a recorded, identical optical configuration) keep flats valid across nearby nights.
                bool stable=FixedOptics(light)||configured&&Same(light.OpticalConfiguration,cal.OpticalConfiguration);
                int? days=NightDays(light.Night,cal.Night);
                if(days==null) {
                    if(stable)gap("Observing night is unknown.");
                    else review("Observing night is unknown.");
                } else if(days.Value>0) {
                    if(!stable)reject("Flat belongs to a different observing night.");
                    else if(days.Value>FlatNightWindow)reject("Flat is more than "+FlatNightWindow+" days from the lights.");
                    else gap("Flat is from another night ("+days.Value+" day"+(days.Value==1?"":"s")+" away).");
                }
            }
            if(result.Reasons.Count==0)result.Reasons.Add("Known identity, geometry and applicable acquisition settings match.");
            return result;
        }
        // Picks one calibration set of a kind for these targets: fully matching candidates before partial ones,
        // then the closest sensor temperature (darks) and nearest night, preferring raw frames over a master on a tie.
        // Raw frames from one night form a set; each master stands alone.
        public static List<Frame> Choose(IList<Frame> targets,IEnumerable<Frame> all,string kind) {
            if(targets.Count==0)return new List<Frame>();
            string master="Master "+kind.ToLowerInvariant();
            var candidates=new List<Frame>();var exact=new HashSet<Frame>();
            foreach(var cal in all) {
                if(cal==null||cal.Rejected||cal.Kind!=kind&&cal.Kind!=master)continue;
                bool usable=true,accepted=true;
                foreach(var target in targets) {
                    var decision=Evaluate(target,cal);
                    usable&=decision.Usable;
                    accepted&=decision.Accepted;
                }
                if(!usable)continue;
                candidates.Add(cal);
                if(accepted)exact.Add(cal);
            }
            if(exact.Count>0)candidates.RemoveAll(c=>!exact.Contains(c));
            if(candidates.Count==0)return new List<Frame>();
            bool dark=kind=="Dark"||kind=="Dark flat";
            // Do not combine an entire night's raw darks across distant temperatures.
            // Keep nearby candidates, otherwise only the nearest available cohort.
            if(dark) {
                var nearby=candidates.Where(cal=>TemperatureDistance(targets,cal)<=PreferredTemperatureDifference).ToList();
                if(nearby.Count>0)candidates=nearby;
                else {double nearest=candidates.Min(cal=>TemperatureDistance(targets,cal));candidates.RemoveAll(cal=>Math.Abs(TemperatureDistance(targets,cal)-nearest)>0.001);}
            }
            var targetTemperatures=targets.Where(t=>t.Temperature.HasValue).Select(t=>t.Temperature.Value).ToList();
            double targetTemperature=targetTemperatures.Count==0?0:targetTemperatures.Average();
            Func<List<Frame>,double> temperatureGap=set=> {
                if(!dark)return 0;
                var known=set.Where(c=>c.Temperature.HasValue).Select(c=>c.Temperature.Value).ToList();
                return known.Count==0?double.MaxValue:Math.Round(Math.Abs(known.Average()-targetTemperature),1);
            };
            Func<List<Frame>,int> nightGap=set=> {
                int best=int.MaxValue;
                foreach(var target in targets) {
                    int? days=NightDays(target.Night,set[0].Night);
                    if(days.HasValue&&days.Value<best)best=days.Value;
                }
                return best;
            };
            var sets=candidates.GroupBy(c=>c.Kind==master?"master|"+c.Hash:"raw|"+(c.Night??"")).Select(g=>g.ToList()).ToList();
            return sets.OrderBy(temperatureGap).ThenBy(nightGap).ThenBy(set=>set[0].Kind==master?1:0).ThenByDescending(set=>set[0].Kind==master?set[0].StackCount:set.Count).ThenBy(set=>set[0].Hash??"",StringComparer.Ordinal).First();
        }
    }
}
