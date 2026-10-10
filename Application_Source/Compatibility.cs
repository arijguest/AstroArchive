// Optional JSON fields keep existing file records readable; a file hash remains its identity.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
namespace AstroArchive {
    public class MetadataFact {
        public string Value {
            get;
            set;
        }
        public string Raw {
            get;
            set;
        }
        public string Source {
            get;
            set;
        }
        public string Unit {
            get;
            set;
        }
    }
    public class AssociatedFile {
        public string SourcePath {
            get;
            set;
        }
        public string RelativePath {
            get;
            set;
        }
        public string Hash {
            get;
            set;
        }
        public FileStamp Stamp {
            get;
            set;
        }
        public string Role {
            get;
            set;
        }
    }
    public class ImageDescriptor {
        public string Key {
            get;
            set;
        }
        public string Label {
            get;
            set;
        }
        public int Width {
            get;
            set;
        }
        public int Height {
            get;
            set;
        }
        public int Channels {
            get;
            set;
        }
        public int Bitpix {
            get;
            set;
        }
        public int Hdu {
            get;
            set;
        }
        public int Count {
            get;
            set;
        }
        public long Offset {
            get;
            set;
        }
        public long Length {
            get;
            set;
        }
        public string Encoding {
            get;
            set;
        }
        public string Compression {
            get;
            set;
        }
        public string Color {
            get;
            set;
        }
        public bool Numeric {
            get;
            set;
        }
        public Dictionary<string,string> Headers {
            get;
            set;
        }
        public Dictionary<string,string> Comments {
            get;
            set;
        }
    }
    public class AssetInfo {
        public double? DurationSeconds;
        public string DurationSource;
        public string Format;
        public FitsHeader Header=new FitsHeader();
        public List<ImageDescriptor> Images=new List<ImageDescriptor>();
        public string Note;
    }
    public class PixelImage {
        public int Width,Height,Channels;
        public double[] Pixels;
    }
    public interface IAssetReader {
        string Name {
            get;
        }
        AssetInfo Inspect(string path,Action<int> counted);
        PixelImage Read(string path,ImageDescriptor image,int index,CancellationToken ct);
    }
    public static class Assets {
        public const long MaxSamples=32L*1024*1024;
        public const string ClassificationVersion="compat-video-2";
        static readonly Dictionary<string,IAssetReader> readers=new Dictionary<string,IAssetReader>(StringComparer.OrdinalIgnoreCase);
        static Assets() {
            Register(new FitsAssetReader(),".fit",".fits",".fts",".fz");
            Register(new XisfReader(),".xisf");
            Register(new SerReader(),".ser");
#if PORTABLE
            Register(new LinuxRasterReader(),".png",".jpg",".jpeg",".tif",".tiff",".gif");
#endif
        }
        public static void Register(IAssetReader reader,params string[] extensions) {
            foreach(string ext in extensions)readers[ext]=reader;
        }
        public static string Extension(string path) {
            string name=path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)?path.Substring(0,path.Length-3):path;
            string ext=Path.GetExtension(name).ToLowerInvariant();
            return path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)&&!new[] {
                ".fit",".fits",".fts"
            }
            .Contains(ext)?".gz":ext;
        }
        public static bool Supported(string path) {
            return readers.ContainsKey(Extension(path))||new[] {
                ".tif",".tiff",".png",".jpg",".jpeg",".gif",".avi",".mp4",".mov",".m4v",".wmv",".mkv",".cr2",".cr3",".nef",".arw",".dng"
            }
            .Contains(Extension(path));
        }
        public static IAssetReader Reader(string path) {
            IAssetReader reader;
            if(readers.TryGetValue(Extension(path),out reader))return reader;
            throw new NotSupportedException("This format can be archived; its pixel decoder is unavailable.");
        }
        public static AssetInfo Inspect(string path,Action<int> counted=null) {
            IAssetReader reader;
            if(readers.TryGetValue(Extension(path),out reader))return reader.Inspect(path,counted);
            if(new[] {
                ".png",".tif",".tiff",".jpg",".jpeg",".gif"
            }
            .Contains(Extension(path)))return RasterHeaders.Inspect(path);
            if(MediaFiles.Video(path))return new AssetInfo{Format=MediaFiles.FileType(path),DurationSeconds=VideoHeaders.Duration(path,counted),DurationSource="Video container duration",Note="Original-file archiving and Windows-codec playback are supported."};
            return new AssetInfo {
                Format=Extension(path).TrimStart('.').ToUpperInvariant(),Note="Original-file archiving is supported. Pixel decoding is unavailable in this environment."
            };
        }
        public static ImageDescriptor Selected(Frame f) {
            return f.Images==null?null:f.Images.FirstOrDefault(i=>i.Key==f.ImageKey)??f.Images.FirstOrDefault();
        }
        public static bool IsCalibration(string kind) {
            return new[] {
                "Dark","Flat","Bias","Dark flat","Master dark","Master flat","Master bias","Master dark flat"
            }
            .Contains(kind);
        }
        public static bool CanDecode(Frame f) {
            var image=Selected(f);
            if(image==null)return f.Images==null&&(string.IsNullOrEmpty(f.Format)||f.Format=="FITS");
            return readers.ContainsKey(Extension(f.OriginalName??f.RelativePath??("image."+f.Format)))&&image.Numeric&&(f.Format=="FITS"&&image.Compression!="cfitsio"||(long)image.Width*image.Height*image.Channels<=MaxSamples)&&(image.Compression!="cfitsio"||NativeFits.Available)&&(!(image.Compression??"").StartsWith("zstd")||PixelCodecs.ZstdAvailable);
        }
        public static bool CanStack(Frame f) {
            var image=Selected(f);
            bool legacy=string.IsNullOrEmpty(f.Format);
            return (legacy||(CanDecode(f)&&f.LinearData==true&&(!Exporter.RequiresConversion(f)||(long)image.Width*image.Height*image.Channels<=MaxSamples)&&(image==null||((image.Bitpix!=64||f.Format=="FITS"&&!Exporter.RequiresConversion(f))&&image.Encoding!="preview only"&&(image.Channels==1||image.Channels==3)&&(image.Count<=1||f.ImageIndex.HasValue&&f.ImageIndex.Value>=0&&f.ImageIndex.Value<image.Count)))))&&!new[] {
                "SER","JPEG","JPG"
            }
            .Contains(f.Format);
        }
        public static string Capabilities(Frame f) {
            return "Archive"+(CanDecode(f)?" · Preview":"")+(CanStack(f)?" · Scientific export":"")+(Selected(f)!=null&&Selected(f).Count>1?" · Sequence":"");
        }
        public static void Dimensions(int width,int height,int channels) {
            if(width<=0||height<=0||channels<1||channels>4||checked((long)width*height*channels)>MaxSamples)throw new NotSupportedException("Image exceeds the bounded decoder limit (32 million samples).");
        }
        public static PixelImage Read(Frame frame,string path,int index,CancellationToken ct) {
            var image=Selected(frame);
            if(image==null) {
                var info=Inspect(path);
                image=info.Images.FirstOrDefault();
            }
            if(image==null)throw new NotSupportedException("No decodable image is available.");
            return Reader(path).Read(path,image,index,ct);
        }
        public static FitsImage Analysis(Frame frame,string path,CancellationToken ct,Action<int> counted=null) {
            if(string.IsNullOrEmpty(frame.Format))return Fits.Image(path,ct,counted);
            if(frame.Format=="FITS"&&Selected(frame)!=null&&Selected(frame).Compression!="cfitsio")return Fits.Image(path,ct,counted,Selected(frame),frame.ImageIndex??0);
            return Downsample(Read(frame,path,frame.ImageIndex??0,ct),ct);
        }
        public static FitsImage Downsample(PixelImage image,CancellationToken ct) {
            int step=Math.Max(2,(int)Math.Ceiling(Math.Max(image.Width,image.Height)/1000.0));
            if(step%2!=0)step++;
            int w=(image.Width+step-1)/step,h=(image.Height+step-1)/step;
            var pixels=new double[w*h];
            var counts=new int[pixels.Length];
            int plane=image.Width*image.Height;
            for(int c=0;c<image.Channels;c++)for(int y=0;y<image.Height;y++) {
                ct.ThrowIfCancellationRequested();
                for(int x=0;x<image.Width;x++) {
                    double v=image.Pixels[c*plane+y*image.Width+x];
                    if(double.IsNaN(v)||double.IsInfinity(v))continue;
                    int p=(y/step)*w+x/step;
                    pixels[p]+=v;
                    counts[p]++;
                }
            }
            for(int p=0;p<pixels.Length;p++)pixels[p]=counts[p]==0?double.NaN:pixels[p]/counts[p];
            return new FitsImage {
                Width=w,Height=h,Pixels=pixels
            };
        }
        public static PreviewData Display(Frame frame,string path,int index,CancellationToken ct,bool fullResolution=false) {
            if(fullResolution){var selected=Selected(frame)??Inspect(path).Images.FirstOrDefault();if(selected==null)throw new NotSupportedException("No decodable image is available.");return FullResolutionPreview.Create(frame,Read(frame,path,index,ct),selected,path,ct);}
            if(string.IsNullOrEmpty(frame.Format))return Fits.Preview(path,ct);
            if(frame.Format=="FITS"&&Selected(frame)!=null&&Selected(frame).Compression!="cfitsio")return Fits.Preview(path,ct,Selected(frame),index);
            var sampled=Reader(path) as ISampledAssetReader;if(sampled!=null)return sampled.Preview(frame,path,Selected(frame)??Inspect(path).Images.First(),index,ct);
            PixelImage image=Read(frame,path,index,ct);
            var descriptor=Selected(frame);
            string bayer=(frame.Bayer??"").ToUpperInvariant();
            bool cfa=image.Channels==1&&new[] {
                "RGGB","BGGR","GRBG","GBRG"
            }
            .Contains(bayer);
            int step=Math.Max(cfa?2:1,(int)Math.Ceiling(Math.Max(image.Width,image.Height)/1400.0));
            if(cfa&&step%2!=0)step++;
            int width=(image.Width+step-1)/step,height=(image.Height+step-1)/step,channels=cfa||image.Channels>=3?3:1;
            var result=new PreviewData {
                Width=width,Height=height,SourceWidth=image.Width,SourceHeight=image.Height,Channels=channels,Pixels=new double[checked(width*height*channels)],FlipY=frame.Format=="FITS",Description=frame.Format+" · "+(cfa?bayer+" colour":channels==3?"RGB":"mono")+" · "+descriptor.Label
            };
            var counts=new int[result.Pixels.Length];
            int plane=image.Width*image.Height;
            var h=new FitsHeader {
                Values=descriptor.Headers??new Dictionary<string,string>()
            };
            int offsetX=(int)(h.Number("XBAYROFF")??0),offsetY=(int)(h.Number("YBAYROFF")??0);
            if(descriptor.Bitpix>0) {
                double low=0,high=Math.Pow(2,descriptor.Bitpix)-1;
                if(frame.Format=="FITS") {
                    low=descriptor.Bitpix==8?0:-Math.Pow(2,descriptor.Bitpix-1);
                    high=descriptor.Bitpix==8?255:Math.Pow(2,descriptor.Bitpix-1)-1;
                    double scale=h.Number("BSCALE")??1,zero=h.Number("BZERO")??0;
                    result.Minimum=Math.Min(low*scale+zero,high*scale+zero);
                    result.Maximum=Math.Max(low*scale+zero,high*scale+zero);
                }
                else {
                    result.Minimum=low;
                    result.Maximum=high;
                }
            }
            for(int c=0;c<image.Channels;c++)for(int y=0;y<image.Height;y++) {
                ct.ThrowIfCancellationRequested();
                if(c>=channels)continue;
                for(int x=0;x<image.Width;x++) {
                    double value=image.Pixels[c*plane+y*image.Width+x];
                    if(double.IsNaN(value)||double.IsInfinity(value))continue;
                    int channel=cfa?"RGB".IndexOf(bayer[((y+offsetY)&1)*2+((x+offsetX)&1)]):channels==1?0:c;
                    int p=((y/step)*width+x/step)*channels+channel;
                    result.Pixels[p]+=value;
                    counts[p]++;
                }
            }
            for(int p=0;p<result.Pixels.Length;p++)result.Pixels[p]=counts[p]>0?result.Pixels[p]/counts[p]:double.NaN;
            result.ApplyContext(frame,path);
            return result;
        }
        public static string CompatibilityKey(Frame f) {
            return string.Join("|",new[] {
                f.CameraId,f.CameraModel,f.OpticalConfiguration,f.ReadoutMode,f.Roi,Util.Num(f.Offset),f.GainUnit,f.ImageKey,Convert.ToString(f.ImageIndex??0),f.LinearData.HasValue?f.LinearData.ToString():"",f.RegistrationState,f.CalibrationSteps
            });
        }
        public static Frame InspectExisting(Frame previous,string path,CancellationToken ct,bool imageChanged=false) {
            ct.ThrowIfCancellationRequested();
            var candidate=Classifier.Read(path,Path.GetDirectoryName(path),previous.Telescope,(previous.MakeEvidence??"").StartsWith("User")?previous.Model:"Auto",asset:null,imageKey:previous.ImageKey,originalName:previous.OriginalName,ct:ct);
            candidate.ImageIndex=previous.ImageIndex;
            candidate.TelescopeIdentity=previous.TelescopeIdentity;
            candidate.Screened=previous.Screened;
            candidate.ScreeningIssue=previous.ScreeningIssue;
            candidate.RejectionReason=previous.RejectionReason;
            candidate.IntegrityIssue=previous.IntegrityIssue;
            candidate.TransferIssue=previous.TransferIssue;
            if(!imageChanged)candidate.Sky=previous.Sky;

            candidate.ObservationMode=previous.ObservationMode;
            candidate.Hash=previous.Hash;
            candidate.RelativePath=previous.RelativePath;
            candidate.SourcePath=previous.SourcePath;
            candidate.SourceRoot=previous.SourceRoot;
            candidate.SidecarRelativePath=previous.SidecarRelativePath;
            candidate.AssociatedFiles=previous.AssociatedFiles;
            candidate.RepositoryStamp=previous.RepositoryStamp;
            candidate.Status=previous.Status;
            candidate.Rejected=previous.Rejected;
            candidate.SourceDisposition=previous.SourceDisposition;
            candidate.Session=previous.Session;
            if(previous.Facts!=null)foreach(var pair in previous.Facts.Where(p=>p.Value.Source=="User")) {
                var property=typeof(Frame).GetProperty(pair.Key);
                if(property!=null&&property.CanWrite)property.SetValue(candidate,property.GetValue(previous,null),null);
                if(candidate.Facts==null)candidate.Facts=new Dictionary<string,MetadataFact>();
                candidate.Facts[pair.Key]=pair.Value;
            }
            foreach(string field in new[] {
                "Target","Mount","Camera","Model","Telescope"
            }) {
                var property=typeof(Frame).GetProperty(field);
                string evidence=field=="Target"?previous.TargetEvidence:field=="Mount"?previous.MountEvidence:field=="Camera"?previous.CameraEvidence:field=="Model"?previous.MakeEvidence:"User";
                if(((evidence??"").StartsWith("User")||(evidence??"").StartsWith("Saved")))property.SetValue(candidate,property.GetValue(previous,null),null);
            }
            return candidate;
        }
        public static void UserFact(Frame frame,string field) {
            if(frame.Facts==null)frame.Facts=new Dictionary<string,MetadataFact>();
            string value=Convert.ToString(typeof(Frame).GetProperty(field).GetValue(frame,null),CultureInfo.InvariantCulture);
            frame.Facts[field]=new MetadataFact {
                Value=value,Raw=value,Source="User",Unit=field=="Exposure"?"s":field=="Temperature"?"C":field=="Gain"||field=="Offset"?"camera units":field=="ElectronsPerAdu"?"e-/ADU":null
            };
        }
    }
}
