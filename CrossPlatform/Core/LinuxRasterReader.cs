using BitMiracle.LibTiff.Classic;
using SkiaSharp;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
namespace AstroArchive;

// Preserve integer PNG and numeric TIFF samples. Display fallbacks are explicitly
// ineligible for scientific conversion rather than silently reducing precision.
public sealed class LinuxRasterReader : IAssetReader
{
    public string Name => "Raster";
    public AssetInfo Inspect(string path,Action<int> counted)
    {
        string extension=Assets.Extension(path);
        var result=extension is ".tif" or ".tiff"?InspectTiff(path):RasterHeaders.Inspect(path);
        if(extension==".png") {
            using var stream=File.OpenRead(path); byte[] header=new byte[33]; stream.ReadExactly(header);
            bool numeric=header[28]==0&&header[25] is 0 or 2&&header[24] is 8 or 16;
            result.Images[0].Encoding=numeric?"raster":"preview only"; result.Images[0].Numeric=true;
            Assets.Dimensions(result.Header.Width,result.Header.Height,result.Header.Channels);
            ReadPngMetadata(path,result.Header);
        }
        if(result.Images.Count==0||extension is ".jpg" or ".jpeg" or ".gif") {
            using var stream=File.OpenRead(path); using var codec=SKCodec.Create(stream)??throw new InvalidDataException("The raster image cannot be decoded.");
            Assets.Dimensions(codec.Info.Width,codec.Info.Height,3);
            result.Header.Width=codec.Info.Width; result.Header.Height=codec.Info.Height; result.Header.Channels=3;
            result.Images.Clear(); result.Images.Add(new ImageDescriptor { Key="page:0",Label="Display image",Width=codec.Info.Width,Height=codec.Info.Height,Channels=3,Count=1,Bitpix=8,Encoding="preview only",Numeric=true });
        }
        result.Note="Raster linearity is unknown. Confirm acquisition metadata before scientific export; display-only decodes cannot be converted.";
        if(counted!=null) counted((int)Math.Min(int.MaxValue,new FileInfo(path).Length)); return result;
    }
    private static AssetInfo InspectTiff(string path) {
        using var tiff=Tiff.Open(path,"r")??throw new InvalidDataException("Cannot open TIFF.");
        var result=new AssetInfo { Format="TIFF" };
        do {
            if(result.Images.Count>=256) throw new NotSupportedException("TIFF has more than 256 pages.");
            int width=tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt(),height=tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int bits=tiff.GetFieldDefaulted(TiffTag.BITSPERSAMPLE)[0].ToInt(),samples=tiff.GetFieldDefaulted(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
            var format=(SampleFormat)tiff.GetFieldDefaulted(TiffTag.SAMPLEFORMAT)[0].ToInt();
            var photo=(Photometric)tiff.GetFieldDefaulted(TiffTag.PHOTOMETRIC)[0].ToInt();
            var planar=(PlanarConfig)tiff.GetFieldDefaulted(TiffTag.PLANARCONFIG)[0].ToInt();
            int channels=photo is Photometric.MINISBLACK or Photometric.MINISWHITE?1:3; Assets.Dimensions(width,height,channels);
            bool numeric=!tiff.IsTiled()&&planar==PlanarConfig.CONTIG&&((channels==1&&samples==1)||(photo==Photometric.RGB&&samples==3))&&
                ((format==SampleFormat.UINT&&bits is 8 or 16)||(format==SampleFormat.INT&&bits is 8 or 16 or 32)||(format==SampleFormat.IEEEFP&&bits is 32 or 64))&&
                (Orientation)tiff.GetFieldDefaulted(TiffTag.ORIENTATION)[0].ToInt()==Orientation.TOPLEFT&&photo!=Photometric.MINISWHITE;
            result.Images.Add(new ImageDescriptor { Key="page:"+result.Images.Count,Hdu=result.Images.Count,Label="Page "+(result.Images.Count+1),Width=width,Height=height,Channels=channels,Bitpix=format==SampleFormat.IEEEFP?-bits:bits,Count=1,Encoding=numeric?"raster":"preview only",Numeric=true });
        } while(tiff.ReadDirectory());
        var first=result.Images[0]; result.Header.Width=first.Width; result.Header.Height=first.Height; result.Header.Channels=first.Channels;
        tiff.SetDirectory(0); var description=tiff.GetField(TiffTag.IMAGEDESCRIPTION); if(description!=null) MetadataText(description[0].ToString(),result.Header);
        return result;
    }
    public PixelImage Read(string path,ImageDescriptor image,int index,CancellationToken ct)
    {
        if(index!=0) throw new ArgumentOutOfRangeException(nameof(index)); Assets.Dimensions(image.Width,image.Height,image.Channels); ct.ThrowIfCancellationRequested();
        if(Assets.Extension(path)==".png"&&image.Encoding=="raster") return ReadPng(path,image,ct);
        if(Assets.Extension(path) is ".tif" or ".tiff") return ReadTiff(path,image,ct);
        using var stream=File.OpenRead(path); using var codec=SKCodec.Create(stream)??throw new InvalidDataException("Cannot decode raster image.");
        using var bitmap=new SKBitmap(new SKImageInfo(image.Width,image.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul));
        if(codec.GetPixels(bitmap.Info,bitmap.GetPixels())!=SKCodecResult.Success) throw new InvalidDataException("Raster decode failed or image is incomplete.");
        int plane=checked(image.Width*image.Height); var pixels=new double[plane*3]; var colours=bitmap.Pixels;
        for(int p=0;p<plane;p++) { if(p%image.Width==0)ct.ThrowIfCancellationRequested(); pixels[p]=colours[p].Red; pixels[plane+p]=colours[p].Green; pixels[plane*2+p]=colours[p].Blue; }
        return new PixelImage { Width=image.Width,Height=image.Height,Channels=3,Pixels=pixels };
    }
    private static PixelImage ReadTiff(string path,ImageDescriptor image,CancellationToken ct) {
        using var tiff=Tiff.Open(path,"r")??throw new InvalidDataException("Cannot open TIFF.");
        if(!tiff.SetDirectory((short)image.Hdu)) throw new InvalidDataException("TIFF page is missing.");
        int plane=checked(image.Width*image.Height); var pixels=new double[checked(plane*image.Channels)];
        if(image.Encoding=="preview only") {
            int[] raster=new int[plane]; if(!tiff.ReadRGBAImageOriented(image.Width,image.Height,raster,Orientation.TOPLEFT)) throw new InvalidDataException("TIFF display decode failed.");
            for(int p=0;p<plane;p++) { if(p%image.Width==0)ct.ThrowIfCancellationRequested(); pixels[p]=Tiff.GetR(raster[p]); if(image.Channels==3) { pixels[plane+p]=Tiff.GetG(raster[p]); pixels[2*plane+p]=Tiff.GetB(raster[p]); } }
        } else {
            int bits=Math.Abs(image.Bitpix),bytes=bits/8; byte[] row=new byte[tiff.ScanlineSize()];
            bool signed=(SampleFormat)tiff.GetFieldDefaulted(TiffTag.SAMPLEFORMAT)[0].ToInt()==SampleFormat.INT;
            if(row.Length<checked(image.Width*image.Channels*bytes)) throw new InvalidDataException("TIFF scanline is too short.");
            for(int y=0;y<image.Height;y++) { ct.ThrowIfCancellationRequested(); if(!tiff.ReadScanline(row,y)) throw new InvalidDataException("TIFF scanline decode failed.");
                for(int x=0;x<image.Width;x++)for(int c=0;c<image.Channels;c++) {
                    int at=(x*image.Channels+c)*bytes;
                    double value=image.Bitpix==-32?BitConverter.ToSingle(row,at):image.Bitpix==-64?BitConverter.ToDouble(row,at):bits==8?signed?(sbyte)row[at]:row[at]:bits==16?signed?BitConverter.ToInt16(row,at):BitConverter.ToUInt16(row,at):BitConverter.ToInt32(row,at);
                    pixels[c*plane+y*image.Width+x]=value;
                }
            }
        }
        return new PixelImage { Width=image.Width,Height=image.Height,Channels=image.Channels,Pixels=pixels };
    }
    private static readonly uint[] CrcTable=Enumerable.Range(0,256).Select(n=>{ uint crc=(uint)n; for(int i=0;i<8;i++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1; return crc; }).ToArray();
    private static IEnumerable<(string type,byte[] data)> Chunks(string path) {
        using var stream=File.OpenRead(path); stream.Position=8; int count=0; long total=0;
        while(stream.Position<stream.Length) {
            if(++count>100000) throw new InvalidDataException("Too many PNG chunks.");
            byte[] header=new byte[8]; stream.ReadExactly(header); uint length=BinaryPrimitives.ReadUInt32BigEndian(header);
            if(length>128*1024*1024||(total+=length)>1024L*1024*1024) throw new InvalidDataException("PNG chunk exceeds decode limit.");
            byte[] data=new byte[length]; stream.ReadExactly(data); byte[] checksum=new byte[4]; stream.ReadExactly(checksum);
            uint crc=0xffffffff; foreach(byte b in header.AsSpan(4).ToArray().Concat(data)) crc=CrcTable[(crc^b)&255]^(crc>>8);
            if(~crc!=BinaryPrimitives.ReadUInt32BigEndian(checksum)) throw new InvalidDataException("PNG checksum is invalid.");
            string type=Encoding.ASCII.GetString(header,4,4); yield return (type,data); if(type=="IEND") yield break;
        }
        throw new InvalidDataException("PNG is missing IEND.");
    }
    private static PixelImage ReadPng(string path,ImageDescriptor image,CancellationToken ct) {
        using var compressed=new MemoryStream(); foreach(var chunk in Chunks(path)) { ct.ThrowIfCancellationRequested(); if(chunk.type=="IDAT")compressed.Write(chunk.data); }
        compressed.Position=0; using var input=new ZLibStream(compressed,CompressionMode.Decompress);
        int bytes=image.Bitpix/8,bpp=image.Channels*bytes,stride=checked(image.Width*bpp),plane=checked(image.Width*image.Height);
        byte[] prior=new byte[stride],row=new byte[stride]; double[] pixels=new double[checked(plane*image.Channels)];
        for(int y=0;y<image.Height;y++) {
            ct.ThrowIfCancellationRequested(); int filter=input.ReadByte(); if(filter is <0 or >4) throw new InvalidDataException("Invalid PNG filter."); input.ReadExactly(row);
            for(int i=0;i<stride;i++) { int left=i>=bpp?row[i-bpp]:0,up=prior[i],diagonal=i>=bpp?prior[i-bpp]:0;
                int predict=filter==1?left:filter==2?up:filter==3?(left+up)/2:filter==4?Paeth(left,up,diagonal):0; row[i]=unchecked((byte)(row[i]+predict)); }
            for(int x=0;x<image.Width;x++)for(int c=0;c<image.Channels;c++) { int at=(x*image.Channels+c)*bytes; pixels[c*plane+y*image.Width+x]=bytes==1?row[at]:BinaryPrimitives.ReadUInt16BigEndian(row.AsSpan(at,2)); }
            (row,prior)=(prior,row);
        }
        if(input.ReadByte()!=-1) throw new InvalidDataException("PNG contains excess decoded samples.");
        return new PixelImage { Width=image.Width,Height=image.Height,Channels=image.Channels,Pixels=pixels };
    }
    private static int Paeth(int a,int b,int c) { int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c); return pa<=pb&&pa<=pc?a:pb<=pc?b:c; }
    private static void ReadPngMetadata(string path,FitsHeader header) {
        foreach(var chunk in Chunks(path)) if(chunk.type=="tEXt"&&chunk.data.Length<65536) { int zero=Array.IndexOf(chunk.data,(byte)0); if(zero>0) { string key=Encoding.Latin1.GetString(chunk.data,0,zero).ToUpperInvariant(),value=Encoding.Latin1.GetString(chunk.data,zero+1,chunk.data.Length-zero-1); if(key is "DESCRIPTION" or "COMMENT")MetadataText(value,header); else if(MetadataKeys.Contains(key))header.Values[key]=value; } }
    }
    private static readonly HashSet<string> MetadataKeys=new("OBJECT OBJNAME TARGET FILTER FILTERID NCOMBINE STACKCNT NSUBS SUBEXP SUBEXPT TOTEXP TOTALEXP EXPTOTAL EXPTIME IMAGETYP OBJCTRA OBJCTDEC".Split(' '));
    private static void MetadataText(string text,FitsHeader header) {
        foreach(string line in text.Split(['\r','\n',';'])) { int at=line.IndexOfAny(['=',':']); if(at>0) { string key=line[..at].Trim().ToUpperInvariant(); if(MetadataKeys.Contains(key))header.Values[key]=line[(at+1)..].Trim().Trim('\'','"'); } }
    }
}
