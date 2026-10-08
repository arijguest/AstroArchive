// Executed separately on Windows because WIC/WPF are unavailable under Mono.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AstroArchive {
    public static class RasterTests {
        static void Check(bool passed,string message) {
            if(!passed)throw new Exception(message);
        }
        static string Write(string root,string name,BitmapEncoder encoder,BitmapSource source) {
            encoder.Frames.Add(BitmapFrame.Create(source));
            string path=Path.Combine(root,name);
            using(var stream=File.Create(path))encoder.Save(stream);
            return path;
        }
        // Insert standard PNG tEXt chunks with independent CRCs, as real editors write them.
        static void AddPngText(string path,string[] fields){
            byte[] png=File.ReadAllBytes(path);using(var output=new MemoryStream()){output.Write(png,0,33);
                foreach(string field in fields){int split=field.IndexOf('=');byte[] chunk=Encoding.UTF8.GetBytes("tEXt"+field.Substring(0,split)+"\0"+field.Substring(split+1));int length=chunk.Length-4;
                    output.WriteByte((byte)(length>>24));output.WriteByte((byte)(length>>16));output.WriteByte((byte)(length>>8));output.WriteByte((byte)length);output.Write(chunk,0,chunk.Length);uint crc=0xffffffff;
                    foreach(byte value in chunk){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}crc^=0xffffffff;
                    output.WriteByte((byte)(crc>>24));output.WriteByte((byte)(crc>>16));output.WriteByte((byte)(crc>>8));output.WriteByte((byte)crc);
                }output.Write(png,33,png.Length-33);File.WriteAllBytes(path,output.ToArray());}
        }
        [STAThread]public static int Main(string[] args) {
            try {
                string root=args[0];
                Directory.CreateDirectory(root);
                Assets.Register(new RasterReader(),".tif",".tiff",".png",".jpg",".jpeg");
                byte[] samples= {
                    12,0,44,1,0,125,255,255
                };
                var gray=BitmapSource.Create(2,2,96,96,PixelFormats.Gray16,null,samples,4);
                string editedPath=Write(root,"Whirlpool_starless.png",new PngBitmapEncoder(),gray);AddPngText(editedPath,new[]{"OBJECT=M51","FILTER=Ha","NCOMBINE=12","TOTEXP=7200"});
                var recovered=EditedMetadata.Read(Path.GetFileName(editedPath),Assets.Inspect(editedPath).Header);Check(recovered.ImageClass=="Starless"&&recovered.Object=="M51"&&recovered.Filters=="Ha"&&recovered.Subs==12&&recovered.TotalExposure==7200,"Edited PNG acquisition metadata not recovered: "+recovered.Details);Console.WriteLine("PASS WIC edited PNG object/filter/sub-count/total-exposure metadata");
                string tiff=Write(root,"gray16.tiff",new TiffBitmapEncoder {
                    Compression=TiffCompressOption.Zip
                },gray);
                var frame=Classifier.Read(tiff,root,"Rig","Auto");
                Check(frame.Format=="TIFF"&&frame.LinearData==null&&!Assets.CanStack(frame),"Raster linearity was assumed");
                Check(Assets.Read(frame,tiff,0,CancellationToken.None).Pixels.SequenceEqual(new double[] {
                    12,300,32000,65535
                }),"TIFF lost 16-bit values");
                Console.WriteLine("PASS WIC 16-bit TIFF values and conservative linearity");
                string png=Write(root,"gray16.png",new PngBitmapEncoder(),gray);
                var pngFrame=Classifier.Read(png,root,"Rig","Auto");
                Check(Assets.Read(pngFrame,png,0,CancellationToken.None).Pixels.SequenceEqual(new double[] {
                    12,300,32000,65535
                }),"PNG lost 16-bit values");
                pngFrame.LinearData=true;
                Check(Assets.CanStack(pngFrame),"Confirmed linear PNG ineligible");
                Console.WriteLine("PASS WIC 16-bit PNG values and explicit eligibility");
                var pages=new TiffBitmapEncoder();
                pages.Frames.Add(BitmapFrame.Create(gray));
                var second=BitmapSource.Create(1,1,96,96,PixelFormats.Gray16,null,new byte[] {
                    42,0
                },2);
                string multi=Write(root,"pages.tiff",pages,second);
                var info=Assets.Inspect(multi);
                Check(info.Images.Count==2,"TIFF pages missing");
                frame.Images=info.Images;
                frame.ImageKey="page:1";
                Check(Assets.Read(frame,multi,0,CancellationToken.None).Pixels.Single()==42,"Wrong TIFF page");
                Console.WriteLine("PASS WIC multi-page TIFF selection");
                byte[] rgb= {
                    10,20,30,40,50,60,70,80,90,100,110,120
                };
                var color=BitmapSource.Create(2,2,96,96,PixelFormats.Rgb24,null,rgb,6);
                string rgbPng=Write(root,"color.png",new PngBitmapEncoder(),color);
                var colorFrame=Classifier.Read(rgbPng,root,"Rig","Auto");
                var pixels=Assets.Read(colorFrame,rgbPng,0,CancellationToken.None);
                Check(pixels.Pixels.SequenceEqual(new double[] {
                    10,40,70,100,20,50,80,110,30,60,90,120
                }),"RGB channel values changed");
                string converted=Path.Combine(root,"rgb-derived.fits");
                ScientificFits.Write(converted,pixels,colorFrame,CancellationToken.None);
                var convertedInfo=Assets.Inspect(converted);
                Check(Fits.ReadPixels(converted,convertedInfo.Images[0],0,CancellationToken.None).Pixels.SequenceEqual(pixels.Pixels),"Raster-derived FITS pixels changed");
                Console.WriteLine("PASS WIC RGB channel order and scientific FITS conversion");
                string jpeg=Write(root,"preview.jpg",new JpegBitmapEncoder(),color);
                var jpegFrame=Classifier.Read(jpeg,root,"Rig","Auto");
                jpegFrame.LinearData=true;
                Check(Assets.CanDecode(jpegFrame)&&!Assets.CanStack(jpegFrame),"Lossy JPEG offered as scientific data");
                Console.WriteLine("PASS JPEG preview without scientific export");
                return 0;
            }
            catch(Exception ex) {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
