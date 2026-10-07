using System;
using System.Collections.Generic;
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
    }
    public static class CalibrationMatching {
        static bool Missing(string value) {
            return string.IsNullOrWhiteSpace(value)||value=="Unknown";
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
            if(Unresolved(light)||Unresolved(cal))review("Conflicting metadata requires review before automatic matching.");
            if(!Assets.IsCalibration(cal.Kind))reject("Not a calibration frame.");
            if(cal.Rejected||CaptureScreening.FileProblem(cal)||cal.Status=="Failed")reject("Rejected or changed calibration.");
            if(light.Calibration=="Calibrated"||light.Calibration=="Registered")reject("Input already calibrated or registered.");
            if(!Assets.CanStack(light)||!Assets.CanStack(cal))reject("Linear, decodable scientific data are required.");
            if(Missing(light.Telescope)||Missing(light.Camera)||Missing(cal.Telescope)||Missing(cal.Camera))review("Physical device or camera channel is unknown.");
            if(light.Telescope!=cal.Telescope||light.MakeText!=cal.MakeText||light.Camera!=cal.Camera)reject("Physical device, make or camera channel differs.");
            if(light.Width!=cal.Width||light.Height!=cal.Height||light.Channels!=cal.Channels||light.Bayer!=cal.Bayer)reject("Image geometry or Bayer pattern differs.");
            if(light.BinX<=0||light.BinY<=0||cal.BinX<=0||cal.BinY<=0)review("Binning is unknown.");
            else if(light.BinX!=cal.BinX||light.BinY!=cal.BinY)reject("Binning differs.");
            foreach(string field in new[] {
                "CameraId","CameraModel","ReadoutMode","Roi","GainUnit"
            }) {
                var property=typeof(Frame).GetProperty(field);
                string a=(string)property.GetValue(light,null),b=(string)property.GetValue(cal,null);
                if(Missing(a)&&Missing(b))continue;
                if(Missing(a)||Missing(b))review(field+" is missing on one input.");
                else if(!string.Equals(a,b,StringComparison.OrdinalIgnoreCase))reject(field+" differs.");
            }
            if(light.Offset.HasValue!=cal.Offset.HasValue)review("Offset is missing on one input.");
            else if(light.Offset.HasValue&&Math.Abs(light.Offset.Value-cal.Offset.Value)>0.001)reject("Offset differs.");
            if(!light.Gain.HasValue||!cal.Gain.HasValue)review("Camera gain setting is unknown (e-/ADU is a separate quantity).");
            else if(Math.Abs(light.Gain.Value-cal.Gain.Value)>0.001)reject("Camera gain setting differs.");
            bool dark=cal.Kind=="Dark"||cal.Kind=="Master dark"||cal.Kind=="Dark flat"||cal.Kind=="Master dark flat",flat=cal.Kind=="Flat"||cal.Kind=="Master flat",darkFlat=cal.Kind=="Dark flat"||cal.Kind=="Master dark flat";
            if(darkFlat&&light.Kind!="Flat")reject("Dark flats calibrate raw flats, rather than lights.");
            if(dark) {
                if(!light.Exposure.HasValue||!cal.Exposure.HasValue)review("Exposure is unknown.");
                else if(Math.Abs(light.Exposure.Value-cal.Exposure.Value)>=0.001)reject("Exposure differs; dark scaling is not assumed.");
                if(!light.Temperature.HasValue||!cal.Temperature.HasValue)review("Sensor temperature is unknown.");
                else if(Math.Abs(light.Temperature.Value-cal.Temperature.Value)>3)reject("Sensor temperature differs by more than 3 C.");
            }
            if(flat) {
                if(Missing(light.Filter)||Missing(cal.Filter))review("Filter is unknown.");
                else if(light.Filter!=cal.Filter)reject("Filter differs.");
                if(Missing(light.Night)||light.Night=="Unknown date"||cal.Night=="Unknown date")review("Observing night is unknown.");
                else if(light.Night!=cal.Night)reject("Flat belongs to a different observing night.");
                if(Missing(light.OpticalConfiguration)!=Missing(cal.OpticalConfiguration))review("Optical configuration is missing on one input.");
                else if(!string.Equals(light.OpticalConfiguration,cal.OpticalConfiguration,StringComparison.OrdinalIgnoreCase))reject("Optical configuration differs.");
            }
            if(result.Reasons.Count==0)result.Reasons.Add("Known identity, geometry and applicable acquisition settings match.");
            return result;
        }
    }
}
