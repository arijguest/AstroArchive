using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
namespace AstroArchive {
    public class FitsAssetReader:IAssetReader {
        public string Name {
            get {
                return "FITS";
            }
        }
        public AssetInfo Inspect(string path,Action<int> counted) {
            return Fits.Inspect(path,counted);
        }
        public PixelImage Read(string path,ImageDescriptor image,int index,CancellationToken ct) {
            return Fits.ReadPixels(path,image,index,ct);
        }
    }
    public static class NativeCodecs {
        // User-provisioned codecs live outside versioned installer folders, so repair stays safe.
        static readonly List<IntPtr> loaded=new List<IntPtr>();
        public static string Folder {
            get {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","codecs");
            }
        }
        [DllImport("kernel32.dll",EntryPoint="LoadLibraryExW",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibrary(string path,IntPtr reserved,uint flags);
        internal static void Preload(string name) {
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;
            string path=Path.Combine(Folder,name);
            if(!File.Exists(path))return;
            lock(loaded) {
                IntPtr library=LoadLibrary(path,IntPtr.Zero,0x100|0x800);
                if(library!=IntPtr.Zero)loaded.Add(library);
            }
        }
    }
    public static class NativeFits {
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)][DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl)]static extern float ffvers(out float version);
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)][DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)]static extern int ffdkopn(out IntPtr file,string name,int mode,ref int status);
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)][DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl)]static extern int ffmahd(IntPtr file,int hdu,out int type,ref int status);
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)][DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl)]static extern int ffgpv(IntPtr file,int datatype,long first,long count,ref double nil,[Out] double[] pixels,out int anynil,ref int status);
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)][DllImport("cfitsio.dll",CallingConvention=CallingConvention.Cdecl)]static extern int ffclos(IntPtr file,ref int status);
        static readonly Lazy<bool> codecAvailable=new Lazy<bool>(ProbeCodec);
        public static bool Available {
            get {
                return codecAvailable.Value;
            }
        }
        static bool ProbeCodec() {
            try {
                NativeCodecs.Preload("cfitsio.dll");
                float version;
                ffvers(out version);
                return version>=3;
            }
            catch(DllNotFoundException) {
                return false;
            }
            catch(EntryPointNotFoundException) {
                return false;
            }
            catch(BadImageFormatException) {
                return false;
            }
        }
        public static PixelImage Read(string path,ImageDescriptor image,int index,CancellationToken ct) {
            if(index<0||index>=image.Count)throw new ArgumentOutOfRangeException("index");
            if(!Available)throw new NotSupportedException("The optional 64-bit CFITSIO codec is not installed. Export the original compressed FITS or decompress it externally.");
            Assets.Dimensions(image.Width,image.Height,image.Channels);
            ct.ThrowIfCancellationRequested();
            IntPtr file;
            int status=0;
            if(ffdkopn(out file,path,0,ref status)!=0)throw new IOException("CFITSIO could not open this file ("+status+").");
            try {
                int type;
                ffmahd(file,image.Hdu+1,out type,ref status);
                int n=checked(image.Width*image.Height*image.Channels);
                var pixels=new double[n];
                double nil=double.NaN;
                int anynil;
                var block=new double[Math.Min(n,65536)];
                for(int first=0;first<n;first+=block.Length) {
                    ct.ThrowIfCancellationRequested();
                    int count=Math.Min(block.Length,n-first);
                    if(status!=0||ffgpv(file,82,1+(long)index*n+first,count,ref nil,block,out anynil,ref status)!=0)throw new IOException("CFITSIO could not decode this image ("+status+").");
                    Array.Copy(block,0,pixels,first,count);
                }
                ct.ThrowIfCancellationRequested();
                return new PixelImage {
                    Width=image.Width,Height=image.Height,Channels=image.Channels,Pixels=pixels
                };
            }
            finally {
                int close=0;
                ffclos(file,ref close);
            }
        }
    }
    public static class ScientificFits {
        static string Card(string key,string value) {
            return (key.PadRight(8)+"= "+value).PadRight(80).Substring(0,80);
        }
        public static void Write(string destination,PixelImage image,Frame source,CancellationToken ct) {
            Assets.Dimensions(image.Width,image.Height,image.Channels);
            if(image.Pixels==null||image.Pixels.Length!=checked(image.Width*image.Height*image.Channels))throw new InvalidDataException("Pixel buffer disagrees with image geometry.");
            if(Assets.Selected(source)!=null&&Assets.Selected(source).Bitpix==64)throw new NotSupportedException("64-bit integer data require an exact-integer converter; export the original instead.");
            var cards=new List<string> {
                Card("SIMPLE","T"),Card("BITPIX","-64"),Card("NAXIS",image.Channels==1?"2":"3"),Card("NAXIS1",image.Width.ToString()),Card("NAXIS2",image.Height.ToString())
            };
            if(image.Channels>1) {
                cards.Add(Card("NAXIS3",image.Channels.ToString()));
                cards.Add(Card("CTYPE3","'RGB'"));
            }
            var canonical=new Dictionary<string,string>();
            Action<string,string> text=(key,value)=> {
                if(!string.IsNullOrEmpty(value)&&value!="Unknown")canonical[key]="'"+value.Replace("'","''")+"'";
            };
            Action<string,double?> number=(key,value)=> {
                if(value.HasValue)canonical[key]=value.Value.ToString("R",CultureInfo.InvariantCulture);
            };
            text("OBJECT",source.Target);
            text("IMAGETYP",source.Kind);
            text("FILTER",source.Filter);
            text("BAYERPAT",source.Bayer);
            text("CAMMODEL",source.CameraModel);
            text("CAM-SER",source.CameraId);
            text("TELESCOP",source.TelescopeModel);
            text("READMODE",source.ReadoutMode);
            text("OPTCONF",source.OpticalConfiguration);
            text("ROI",source.Roi);
            text("DATE-OBS",source.ObservedUtc);
            if(source.Calibration=="Calibrated")canonical["CALIBRAT"]="T";
            if(source.Calibration=="Uncalibrated")canonical["CALIBRAT"]="F";
            if(source.Calibration=="Registered")canonical["REGISTER"]="T";
            if(source.Calibration!="Uncalibrated")text("CALSTAT",source.CalibrationSteps);
            if(source.ObservedUtc!=null)text("TIMESYS","UTC");
            number("EXPTIME",source.Exposure);
            number("GAIN",source.Gain);
            number("EGAIN",source.ElectronsPerAdu);
            number("OFFSET",source.Offset);
            number("CCD-TEMP",source.Temperature);
            if(source.BinX>0)number("XBINNING",source.BinX);
            if(source.BinY>0)number("YBINNING",source.BinY);
            if(source.LinearData.HasValue)canonical["LINEAR"]=source.LinearData==true?"T":"F";
            var normalized=new HashSet<string>(new[] {
                "OBJECT","OBJNAME","TARGET","TARGNAME","OBSTARG","IMAGETYP","IMAGETYPE","FRAME","FRAMETYP","EXPTIME","EXPOSURE","EXP_TIME","EXPOS","GAIN","CCDGAIN","EGAIN","OFFSET","BLKLEVEL","CCD-TEMP","SENSOR_T","SENSORT","CAMTEMP","TEMPERAT","FILTER","FILTERID","FILTNAME","BAYERPAT","BAYERPATTERN","CAMMODEL","CAMERA","INSTRUME","CAM-SER","CAMSN","CAMSER","SERIAL","TELESCOP","TELMODEL","READMODE","READOUTM","OPTCONF","OPTICAL","ROI","XBINNING","YBINNING","CCDXBIN","CCDYBIN","BINNING","CALSTAT","CALIBRAT","CALIBRED","REGISTER","REGISTRD","DEROTATE","LINEAR","DATE-OBS","DATEOBS","DATE_OBS","TIMESYS"
            });
            var selected=Assets.Selected(source);
            if(selected!=null&&selected.Headers!=null)foreach(var pair in selected.Headers) {
                string key=pair.Key;
                if(normalized.Contains(key)||selected.Count>1&&System.Text.RegularExpressions.Regex.IsMatch(key,@"^(CTYPE|CRVAL|CRPIX|CDELT|CROTA|CD\d|PC\d|PV\d|WCSAXES|LONPOLE|LATPOLE)")||key.Length>8||key.StartsWith("NAXIS")||key.StartsWith("Z")||new[] {
                    "SIMPLE","XTENSION","BITPIX","BSCALE","BZERO","BLANK","PCOUNT","GCOUNT","CHECKSUM","DATASUM","END","CTYPE3"
                }
                .Contains(key)||key.StartsWith("TFORM")||key.StartsWith("TTYPE"))continue;
                double parsedNumber;
                string value=pair.Value;
                if(value!="T"&&value!="F"&&!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out parsedNumber))value="'"+value.Replace("'","''")+"'";
                if(key.Length+12+value.Length<=80)cards.Add(Card(key,value));
            }
            foreach(var pair in canonical)if(pair.Value.Length<=68)cards.Add(Card(pair.Key,pair.Value));
            if(selected!=null&&selected.Count>1)cards.Add("HISTORY Container slice extracted; multidimensional WCS omitted.".PadRight(80));
            if(!string.IsNullOrEmpty(source.Hash))cards.Add(Card("ORIGSHA","'"+source.Hash+"'"));
            cards.Add("HISTORY AstroArchive derived export; archived source bytes retained.".PadRight(80));
            cards.Add("END".PadRight(80));
            string header=string.Concat(cards);
            header=header.PadRight((header.Length+2879)/2880*2880);
            using(var stream=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                byte[] bytes=Encoding.ASCII.GetBytes(header);
                stream.Write(bytes,0,bytes.Length);
                byte[] block=new byte[65536];
                for(int first=0;first<image.Pixels.Length;first+=block.Length/8) {
                    ct.ThrowIfCancellationRequested();
                    int count=Math.Min(block.Length/8,image.Pixels.Length-first);
                    for(int k=0;k<count;k++) {
                        long value=BitConverter.DoubleToInt64Bits(image.Pixels[first+k]);
                        for(int b=0;b<8;b++)block[k*8+b]=(byte)((ulong)value>>(56-b*8));
                    }
                    stream.Write(block,0,count*8);
                }
                while(stream.Length%2880!=0)stream.WriteByte(0);
                stream.Flush(true);
            }
        }
    }
}
