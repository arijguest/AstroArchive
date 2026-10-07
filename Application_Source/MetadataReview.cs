using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace AstroArchive {
    public sealed class MetadataChange {
        bool apply;
        public bool Touched {
            get;
            private set;
        }
        public bool Apply {
            get {
                return apply;
            }
            set {
                if(value!=apply&&CanRetain) {
                    apply=value;
                    Touched=true;
                }
            }
        }
        public string File {
            get;
            private set;
        }
        public string Field {
            get;
            private set;
        }
        public string Before {
            get;
            private set;
        }
        public string After {
            get;
            private set;
        }
        public bool CanRetain {
            get {
                return !new[] {
                    "Width","Height","Channels","Format","ClassificationVersion","ImageKey","ImageIndex"
                }
                .Contains(Field);
            }
        }
        internal Frame Previous,Candidate;
        internal MetadataChange(Frame previous,Frame candidate,string field,bool detected) {
            Previous=previous;
            Candidate=candidate;
            File=previous.OriginalName;
            Field=field;
            var property=typeof(Frame).GetProperty(field);
            Before=Convert.ToString(property.GetValue(previous,null),CultureInfo.InvariantCulture);
            After=Convert.ToString(property.GetValue(candidate,null),CultureInfo.InvariantCulture);
            apply=detected;
        }
    }
    public static class MetadataReview {
        static readonly string[] untracked= {
            "Exposure","Gain","Temperature","BinX","BinY","Filter","Calibration","Camera","Model","Kind"
        };
        public static List<MetadataChange> Compare(Frame previous,Frame candidate) {
            var changes=new List<MetadataChange>();
            foreach(var property in typeof(Frame).GetProperties().Where(p=>p.CanWrite&&(p.PropertyType==typeof(string)||p.PropertyType==typeof(double?)||p.PropertyType==typeof(bool?)||p.PropertyType==typeof(int)||p.PropertyType==typeof(int?)))) {
                object before=property.GetValue(previous,null),after=property.GetValue(candidate,null);
                if(object.Equals(before,after))continue;
                string value=Convert.ToString(before,CultureInfo.InvariantCulture);
                bool known=value.Length>0&&value!="Unknown"&&value!="Other / unknown"&&value!="Auto"&&!((property.Name=="BinX"||property.Name=="BinY")&&value=="0");
                bool keep=previous.Facts==null&&untracked.Contains(property.Name)&&known;
                changes.Add(new MetadataChange(previous,candidate,property.Name,!keep));
            }
            return changes;
        }
        public static void Apply(IEnumerable<MetadataChange> changes) {
            foreach(var change in changes.Where(c=>!c.Apply)) {
                var property=typeof(Frame).GetProperty(change.Field);
                property.SetValue(change.Candidate,property.GetValue(change.Previous,null),null);
                if(change.Touched) {
                    Assets.UserFact(change.Candidate,change.Field);
                    continue;
                }
                if(change.Candidate.Facts==null)change.Candidate.Facts=new Dictionary<string,MetadataFact>();
                change.Candidate.Facts[change.Field]=new MetadataFact {
                    Value=change.Before,Raw=change.Before,Source="Legacy indexed value (unattributed)"
                };
                if(change.Candidate.MetadataConflicts==null)change.Candidate.MetadataConflicts=new List<string>();
                change.Candidate.MetadataConflicts.Add(change.Field+": retained legacy value differs from detected metadata. Assign it explicitly in Edit metadata to resolve.");
            }
        }
    }
}
