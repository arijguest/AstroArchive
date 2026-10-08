// Windows Imaging Component decodes raster pages without converting the archived original.
using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
    public sealed class RasterReader:IAssetReader {
        static object Query(BitmapMetadata metadata,string query){try{return metadata.GetQuery(query);}catch(NotSupportedException){return null;}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}catch(IOException){return null;}}
        static void AcquisitionMetadata(BitmapFrame frame,FitsHeader header){
            var metadata=frame.Metadata as BitmapMetadata;if(metadata==null)return;
            for(int blockIndex=0;blockIndex<64;blockIndex++){
             string blockPath="/["+blockIndex+"]tEXt";if(Query(metadata,blockPath)==null)break;
             foreach(string key in new[]{"OBJECT","OBJNAME","TARGET","FILTER","FILTERID","NCOMBINE","STACKCNT","NSTACK","NSUBS","SUBEXP","SUBEXPT","TOTEXP","TOTALEXP","EXPTOTAL","EXPTIME","IMAGETYP","OBJCTRA","OBJCTDEC"}){
                var value=Query(metadata,blockPath+"/{str="+key+"}") as string;if(!string.IsNullOrWhiteSpace(value)&&value.Length<8192)header.Values[key]=value.Trim();
            }
            }
            foreach(string query in new[]{"/tEXt/{str=Description}","/tEXt/{str=Comment}","/ifd/{ushort=270}","/app1/ifd/{ushort=270}"}){
                string text=Query(metadata,query) as string;if(string.IsNullOrEmpty(text)||text.Length>65536)continue;
                foreach(Match match in Regex.Matches(text,@"(?:^|[\r\n;])\s*(OBJECT|OBJNAME|TARGET|FILTER|NCOMBINE|STACKCNT|NSUBS|SUBEXP|SUBEXPT|TOTEXP|TOTALEXP|EXPTOTAL|EXPTIME|IMAGETYP|OBJCTRA|OBJCTDEC)\s*[:=]\s*([^\r\n;]+)",RegexOptions.IgnoreCase)){
                    string key=match.Groups[1].Value.ToUpperInvariant();if(!header.Values.ContainsKey(key))header.Values[key]=match.Groups[2].Value.Trim().Trim('\'', '"');
                }
            }
            if(!header.Values.ContainsKey("EXPTIME"))foreach(string query in new[]{"/ifd/exif/{ushort=33434}","/app1/ifd/exif/{ushort=33434}"}){
                object value=Query(metadata,query);if(!(value is ulong))continue;ulong rational=(ulong)value;uint numerator=(uint)rational,denominator=(uint)(rational>>32);if(denominator>0)header.Values["EXPTIME"]=((double)numerator/denominator).ToString("R",CultureInfo.InvariantCulture);
            }
        }
        public string Name {
            get {
                return "Raster";
            }
        }
        static BitmapDecoder Open(Stream stream) {
            return BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand);
        }
        static bool Gray(PixelFormat format) {
            return format==PixelFormats.Gray8||format==PixelFormats.Gray16||format==PixelFormats.Gray32Float||format==PixelFormats.BlackWhite;
        }
        static bool Scientific(PixelFormat format) {
            return Gray(format)&&format!=PixelFormats.BlackWhite||format==PixelFormats.Rgb24||format==PixelFormats.Bgr24||format==PixelFormats.Rgb48||format==PixelFormats.Rgb128Float;
        }
        public AssetInfo Inspect(string path,Action<int> counted) {
            using(var stream=File.OpenRead(path)) {
                var decoder=Open(stream);
                var result=new AssetInfo {
                    Format=Assets.Extension(path)==".tif"||Assets.Extension(path)==".tiff"?"TIFF":Assets.Extension(path)==".png"?"PNG":"JPEG",Note="Raster linearity is unknown; confirm acquisition data before scientific export."
                };
                if(decoder.Frames.Count>256)throw new NotSupportedException("Raster has more than 256 pages.");
                for(int page=0;page<decoder.Frames.Count;page++) {
                    var frame=decoder.Frames[page];
                    int channels=Gray(frame.Format)?1:3;
                    result.Images.Add(new ImageDescriptor {
                        Key="page:"+page,Label="Page "+(page+1)+" · "+frame.Format,Width=frame.PixelWidth,Height=frame.PixelHeight,Channels=channels,Count=1,Hdu=page,Bitpix=frame.Format==PixelFormats.Gray32Float||frame.Format==PixelFormats.Rgb128Float?-32:frame.Format.BitsPerPixel/channels,Encoding=Scientific(frame.Format)?"raster":"preview only",Numeric=true
                    });
                }
                if(result.Images.Count>0) {
                    var first=result.Images[0];
                    result.Header.Width=first.Width;
                    result.Header.Height=first.Height;
                    result.Header.Channels=first.Channels;
                    AcquisitionMetadata(decoder.Frames[0],result.Header);
                }
                if(counted!=null)counted((int)Math.Min(int.MaxValue,stream.Position));
                return result;
            }
        }
        public PixelImage Read(string path,ImageDescriptor image,int index,CancellationToken ct) {
            if(index!=0)throw new ArgumentOutOfRangeException("index");
            Assets.Dimensions(image.Width,image.Height,image.Channels);
            ct.ThrowIfCancellationRequested();
            using(var stream=File.OpenRead(path)) {
                var decoder=Open(stream);
                var source=decoder.Frames[image.Hdu];
                bool gray=image.Channels==1,floating=source.Format==PixelFormats.Gray32Float||source.Format==PixelFormats.Rgb128Float;
                PixelFormat target=gray?(floating?PixelFormats.Gray32Float:PixelFormats.Gray16):(floating?PixelFormats.Rgb128Float:PixelFormats.Rgb48);
                BitmapSource converted=source.Format==target?(BitmapSource)source:new FormatConvertedBitmap(source,target,null,0);
                int bytes=target.BitsPerPixel/8,stride=checked(image.Width*bytes);
                byte[] row=new byte[stride];
                int plane=checked(image.Width*image.Height);
                var pixels=new double[checked(plane*image.Channels)];
                bool scaled=source.Format==PixelFormats.Gray8||source.Format==PixelFormats.Rgb24||source.Format==PixelFormats.Bgr24||image.Encoding=="preview only";
                for(int y=0;y<image.Height;y++) {
                    ct.ThrowIfCancellationRequested();
                    converted.CopyPixels(new System.Windows.Int32Rect(0,y,image.Width,1),row,stride,0);
                    for(int x=0;x<image.Width;x++)for(int c=0;c<image.Channels;c++) {
                        int offset=x*bytes+c*(floating?4:2);
                        double value=floating?BitConverter.ToSingle(row,offset):(row[offset]|row[offset+1]<<8);
                        pixels[c*plane+y*image.Width+x]=scaled?value/257.0:value;
                    }
                }
                return new PixelImage {
                    Width=image.Width,Height=image.Height,Channels=image.Channels,Pixels=pixels
                };
            }
        }
    }
}
