// FITS primary/image HDUs: BITPIX 8/16/32/64/-32/-64, RGB cubes and gzip.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AstroArchive {
 public class FitsHeader {
  public Dictionary<string,string> Values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string,string> Comments=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  public long Offset; public long DataBytes; public int Bitpix; public int Width; public int Height; public int Channels;
  public string Get(params string[] keys) {foreach(var k in keys) {string v; if(Values.TryGetValue(k,out v)&&!string.IsNullOrWhiteSpace(v)) return v.Trim();} return "";}
  public double? Number(params string[] keys) {double n; string v=Get(keys).Replace('D','E'); return double.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?(double?)n:null;}
 }
 public class FitsImage {public double[] Pixels; public int Width; public int Height; }
 public static class Fits {
  static Stream Open(string path,bool preview=false) {Stream s=new FileStream(path,FileMode.Open,FileAccess.Read,preview?FileShare.ReadWrite|FileShare.Delete:FileShare.Read,65536); return path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)?(Stream)new GZipStream(s,CompressionMode.Decompress):s;}
  static void ReadFull(Stream s,byte[] b,int n) {int k=0,r; while(k<n&&(r=s.Read(b,k,n-k))>0) k+=r; if(k!=n) throw new InvalidDataException("Truncated FITS file.");}
  static void Skip(Stream s,long n) {if(s.CanSeek) {if(n>s.Length-s.Position)throw new InvalidDataException("Truncated FITS data.");s.Seek(n,SeekOrigin.Current);} else {byte[] b=new byte[65536];while(n>0){int r=s.Read(b,0,(int)Math.Min(n,b.Length));if(r==0)throw new InvalidDataException("Truncated FITS data.");n-=r;}}}
  static FitsHeader ReadHeader(Stream s,ref long pos){
   var h=new FitsHeader();bool end=false;int blocks=0;do{byte[] block=new byte[2880];int got=s.Read(block,0,block.Length);if(got==0&&blocks==0)return null;while(got<block.Length){int read=s.Read(block,got,block.Length-got);if(read==0)throw new InvalidDataException("Truncated FITS header.");got+=read;}pos+=2880;for(int i=0;i<36;i++){string card=Encoding.ASCII.GetString(block,i*80,80),key=card.Substring(0,8).Trim();if(key=="END"){end=true;break;}if(card[8]!='='||key.Length==0)continue;string body=card.Substring(10);bool quote=false;int slash=-1;for(int j=0;j<body.Length;j++){if(body[j]=='\''){if(quote&&j+1<body.Length&&body[j+1]=='\''){j++;continue;}quote=!quote;}else if(body[j]=='/'&&!quote){slash=j;break;}}string value=(slash<0?body:body.Substring(0,slash)).Trim();if(value.StartsWith("'")&&value.EndsWith("'"))value=value.Substring(1,value.Length-2).Replace("''","'").TrimEnd();h.Values[key]=value;h.Comments[key]=slash<0?"":body.Substring(slash+1).Trim();}if(++blocks>1024)throw new InvalidDataException("FITS header exceeds safety limit.");}while(!end);
   int dims=(int)(h.Number("NAXIS")??0),bits=(int)(h.Number("BITPIX")??0);if(dims<0||dims>9||!new[]{8,16,32,64,-32,-64}.Contains(bits))throw new InvalidDataException("Unsupported FITS dimensions or BITPIX.");long count=dims==0?0:1;for(int k=1;k<=dims;k++){long n=(long)(h.Number("NAXIS"+k)??0);if(n<0)throw new InvalidDataException("Invalid FITS axis.");count=checked(count*n);}h.DataBytes=checked((count+(long)(h.Number("PCOUNT")??0))*(long)(h.Number("GCOUNT")??1)*(Math.Abs(bits)/8));h.Offset=pos;h.Bitpix=bits;h.Width=(int)(h.Number("NAXIS1")??0);h.Height=(int)(h.Number("NAXIS2")??0);h.Channels=dims==3?(int)(h.Number("NAXIS3")??1):1;return h;
  }
  static FitsHeader Merge(FitsHeader primary,FitsHeader image){var result=new FitsHeader{Offset=image.Offset,DataBytes=image.DataBytes,Bitpix=image.Bitpix,Width=image.Width,Height=image.Height,Channels=image.Channels};foreach(var pair in primary.Values)result.Values[pair.Key]=pair.Value;foreach(var pair in primary.Comments)result.Comments[pair.Key]=pair.Value;foreach(var pair in image.Values)result.Values[pair.Key]=pair.Value;foreach(var pair in image.Comments)result.Comments[pair.Key]=pair.Value;foreach(string key in new[]{"BSCALE","BZERO","BLANK"})if(!image.Values.ContainsKey(key))result.Values.Remove(key);return result;}
  static FitsHeader FindImage(Stream s){long pos=0;FitsHeader primary=null;for(int hdu=0;hdu<64;hdu++){var h=ReadHeader(s,ref pos);if(h==null)break;if(hdu==0){if(h.Get("SIMPLE")!="T")throw new InvalidDataException("Not a standard FITS file.");primary=h;}if(h.Get("ZIMAGE")=="T")throw new NotSupportedException("Compressed FITS requires the optional CFITSIO codec.");int dims=(int)(h.Number("NAXIS")??0);if(dims>=2&&(h.Get("XTENSION")==""||h.Get("XTENSION")=="IMAGE")&&h.DataBytes>0){if(dims>3||(dims==3&&h.Channels>4))throw new NotSupportedException("Choose a sequence plane through the image browser.");if(h.Width<=0||h.Height<=0||h.Width>100000||h.Height>100000)throw new InvalidDataException("Invalid image size.");if(s.CanSeek&&s.Length-s.Position<h.DataBytes)throw new InvalidDataException("Truncated FITS image.");return Merge(primary,h);}long skip=checked((h.DataBytes+2879)/2880*2880);Skip(s,skip);pos+=skip;}throw new InvalidDataException("No image HDU found.");}
  public static AssetInfo Inspect(string path,Action<int> counted=null){
   var result=new AssetInfo{Format="FITS"};using(var input=Open(path)){Stream s=counted==null?input:(Stream)new ReadCounter(input,counted);long pos=0;FitsHeader primary=null;
    for(int hdu=0;hdu<64;hdu++){if(s.CanSeek&&s.Position==s.Length)break;FitsHeader h;try{h=ReadHeader(s,ref pos);}catch(InvalidDataException){if(!s.CanSeek&&result.Images.Count>0)throw;throw;}if(h==null)break;if(hdu==0){if(h.Get("SIMPLE")!="T")throw new InvalidDataException("Not a standard FITS file.");primary=h;result.Header=h;}
     bool compressed=h.Get("ZIMAGE")=="T";int dims=(int)(h.Number(compressed?"ZNAXIS":"NAXIS")??0);if(dims>=2&&(compressed||h.Get("XTENSION")==""||h.Get("XTENSION")=="IMAGE")&&h.DataBytes>0){var merged=Merge(primary,h);int w=(int)(h.Number(compressed?"ZNAXIS1":"NAXIS1")??0),height=(int)(h.Number(compressed?"ZNAXIS2":"NAXIS2")??0);if(w<=0||height<=0||w>100000||height>100000)throw new InvalidDataException("Invalid FITS dimensions.");
      string axis=h.Get("CTYPE3");bool color=dims==3&&(axis.Equals("RGB",StringComparison.OrdinalIgnoreCase)||h.Get("COLORSPC").Equals("RGB",StringComparison.OrdinalIgnoreCase));int channels=color?(int)(h.Number(compressed?"ZNAXIS3":"NAXIS3")??1):1;long slices=1;for(int k=color?4:3;k<=dims;k++)slices=checked(slices*(long)(h.Number((compressed?"ZNAXIS":"NAXIS")+k)??1));if(slices<1||slices>int.MaxValue||channels<1||channels>4)throw new NotSupportedException("Unsupported FITS image axes.");
      var descriptor=new ImageDescriptor{Key="hdu:"+hdu,Label=(h.Get("EXTNAME")==""?"HDU "+hdu:h.Get("EXTNAME"))+(slices>1?" · "+slices+" planes":""),Hdu=hdu,Width=w,Height=height,Channels=channels,Count=(int)slices,Bitpix=compressed?(int)(h.Number("ZBITPIX")??0):h.Bitpix,Offset=h.Offset,Length=h.DataBytes,Encoding="FITS",Compression=compressed?"cfitsio":null,Color=color?"RGB":h.Get("BAYERPAT","BAYERPATTERN"),Numeric=new[]{8,16,32,64,-32,-64}.Contains(compressed?(int)(h.Number("ZBITPIX")??0):h.Bitpix),Headers=merged.Values,Comments=merged.Comments};result.Images.Add(descriptor);if(result.Images.Count==1){merged.Width=w;merged.Height=height;merged.Channels=channels;result.Header=merged;}
     }
     long skip=checked((h.DataBytes+2879)/2880*2880);if(!s.CanSeek){Skip(s,h.DataBytes);pos+=h.DataBytes;long padding=skip-h.DataBytes;Skip(s,padding);pos+=padding;}else{Skip(s,skip);pos+=skip;}
    }
   }if(result.Images.Count==0)result.Note="No supported image was found in the first 64 HDUs; export original files.";return result;
  }
  public static PixelImage ReadPixels(string path,ImageDescriptor image,int index,System.Threading.CancellationToken ct){if(index<0||index>=Math.Max(1,image.Count))throw new ArgumentOutOfRangeException("index");Assets.Dimensions(image.Width,image.Height,image.Channels);if(image.Compression=="cfitsio")return NativeFits.Read(path,image,index,ct);using(var input=Open(path)){Stream stream=new ReadCounter(input,n=>ct.ThrowIfCancellationRequested());long start=checked(image.Offset+(long)index*image.Width*image.Height*image.Channels*(Math.Abs(image.Bitpix)/8));Skip(stream,start);int plane=checked(image.Width*image.Height),bpp=Math.Abs(image.Bitpix)/8;var pixels=new double[checked(plane*image.Channels)];byte[] row=new byte[checked(image.Width*bpp)];var h=new FitsHeader{Values=image.Headers??new Dictionary<string,string>()};double scale=h.Number("BSCALE")??1,zero=h.Number("BZERO")??0;double? blank=h.Number("BLANK");for(int c=0;c<image.Channels;c++)for(int y=0;y<image.Height;y++){ct.ThrowIfCancellationRequested();ReadFull(stream,row,row.Length);for(int x=0;x<image.Width;x++){double raw=Decode(row,x*bpp,image.Bitpix);pixels[c*plane+y*image.Width+x]=blank.HasValue&&raw==blank.Value?double.NaN:raw*scale+zero;}}return new PixelImage{Width=image.Width,Height=image.Height,Channels=image.Channels,Pixels=pixels};}}
  sealed class ReadCounter:Stream {readonly Stream input;readonly Action<int> count;public ReadCounter(Stream stream,Action<int> callback){input=stream;count=callback;}public override int Read(byte[] buffer,int offset,int length){int n=input.Read(buffer,offset,length);count(n);return n;}public override bool CanRead{get{return input.CanRead;}}public override bool CanSeek{get{return input.CanSeek;}}public override bool CanWrite{get{return false;}}public override long Length{get{return input.Length;}}public override long Position{get{return input.Position;}set{input.Position=value;}}public override long Seek(long offset,SeekOrigin origin){return input.Seek(offset,origin);}public override void Flush(){}public override void SetLength(long length){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int length){throw new NotSupportedException();}}
  public static FitsHeader Header(string path,Action<int> count=null,Action<FileStamp> lockedStamp=null,System.Threading.CancellationToken ct=default(System.Threading.CancellationToken)){ct.ThrowIfCancellationRequested();using(var input=Open(path)){var header=FindImage(new ReadCounter(input,n=>{ct.ThrowIfCancellationRequested();if(count!=null)count(n);}));if(lockedStamp!=null)lockedStamp(FileStamp.Read(path));return header;}}
  public static FitsHeader Validate(string path,System.Threading.CancellationToken ct){
   using(var input=Open(path)){
    ct.ThrowIfCancellationRequested();var header=FindImage(new ReadCounter(input,n=>ct.ThrowIfCancellationRequested()));long remaining=header.DataBytes;var buffer=new byte[65536];
    while(remaining>0){ct.ThrowIfCancellationRequested();int read=input.Read(buffer,0,(int)Math.Min(remaining,buffer.Length));if(read==0)throw new InvalidDataException("Truncated FITS image.");remaining-=read;}
    // Read gzip trailers as well as the image; header-only parsing cannot validate compressed payloads.
    if(path.EndsWith(".gz",StringComparison.OrdinalIgnoreCase))while(true){ct.ThrowIfCancellationRequested();if(input.Read(buffer,0,buffer.Length)==0)break;}
    return header;
   }
  }
  [StructLayout(LayoutKind.Explicit)]struct Bits { [FieldOffset(0)]public uint Integer;[FieldOffset(0)]public float Single;[FieldOffset(0)]public ulong Long;[FieldOffset(0)]public double Double; }
  static double Decode(byte[] b,int p,int bits) {
   if(bits==8)return b[p];if(bits==16)return unchecked((short)((b[p]<<8)|b[p+1]));
   if(bits==32)return unchecked((int)(((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3]));
   if(bits==64){ulong v=0;for(int k=0;k<8;k++)v=(v<<8)|b[p+k];return unchecked((long)v);}
   if(bits==-32){var q=new Bits{Integer=((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3]};return q.Single;}
   ulong u=0;for(int k=0;k<8;k++)u=(u<<8)|b[p+k];return new Bits{Long=u}.Double;
  }
  static FitsHeader SelectedHeader(Stream source,ImageDescriptor selected,int index){if(index<0||index>=Math.Max(1,selected.Count))throw new ArgumentOutOfRangeException("index");if(!selected.Numeric||selected.Compression=="cfitsio")throw new NotSupportedException("A supported uncompressed FITS image is required.");Skip(source,checked(selected.Offset+(long)index*selected.Width*selected.Height*selected.Channels*(Math.Abs(selected.Bitpix)/8)));return new FitsHeader{Values=selected.Headers,Comments=selected.Comments,Width=selected.Width,Height=selected.Height,Channels=selected.Channels,Bitpix=selected.Bitpix};}
  public static PreviewData Preview(string path,System.Threading.CancellationToken ct,ImageDescriptor selected=null,int index=0) {
   ct.ThrowIfCancellationRequested();using(var input=Open(path,true)){
    Stream source=new ReadCounter(input,n=>ct.ThrowIfCancellationRequested());FitsHeader h=selected==null?FindImage(source):SelectedHeader(source,selected,index);string bayer=h.Get("BAYERPAT").ToUpperInvariant();bool cfa=h.Channels==1&&new[]{"RGGB","BGGR","GRBG","GBRG"}.Contains(bayer);
    int step=Math.Max(cfa?2:1,(int)Math.Ceiling(Math.Max(h.Width,h.Height)/1400.0));if(cfa&&step%2!=0)step++;
    int width=(h.Width+step-1)/step,height=(h.Height+step-1)/step,channels=cfa||h.Channels>=3?3:1;
    var image=new PreviewData{Width=width,Height=height,SourceWidth=h.Width,SourceHeight=h.Height,Channels=channels,Target=h.Get("OBJECT","OBJNAME","TARGET","TARGNAME","OBSTARG"),Filter=h.Get("FILTER","FILTERID","FILTNAME"),ObservationMode=h.Get("OBSMODE","CAPMODE","SHOOTMOD","MODE","IMAGETYP"),FlipY=true,Description="FITS · "+(cfa?bayer+" colour":channels==3?"RGB":"mono")+" · "+h.Bitpix+" bit"};
    image.Pixels=new double[checked(width*height*channels)];var counts=new int[image.Pixels.Length];
    int bpp=Math.Abs(h.Bitpix)/8;byte[] row=new byte[checked(h.Width*bpp)];double scale=h.Number("BSCALE")??1,zero=h.Number("BZERO")??0;double? blank=h.Number("BLANK");
    if(h.Bitpix>0){double low=h.Bitpix==8?0:-Math.Pow(2,h.Bitpix-1),high=h.Bitpix==8?255:Math.Pow(2,h.Bitpix-1)-1;image.Minimum=Math.Min(low*scale+zero,high*scale+zero);image.Maximum=Math.Max(low*scale+zero,high*scale+zero);}
    int offsetX=(int)(h.Number("XBAYROFF")??0),offsetY=(int)(h.Number("YBAYROFF")??0);
    for(int c=0;c<h.Channels;c++)for(int y=0;y<h.Height;y++){
     ct.ThrowIfCancellationRequested();ReadFull(source,row,row.Length);if(c>=channels)continue;
     for(int x=0;x<h.Width;x++){
      double raw=Decode(row,x*bpp,h.Bitpix);if((blank.HasValue&&h.Bitpix>0&&raw==blank.Value)||double.IsNaN(raw)||double.IsInfinity(raw))continue;
      int channel=cfa?"RGB".IndexOf(bayer[((y+offsetY)&1)*2+((x+offsetX)&1)]):channels==1?0:c;
      int pixel=((y/step)*width+x/step)*channels+channel;image.Pixels[pixel]+=raw*scale+zero;counts[pixel]++;
     }
    }
    for(int n=0;n<image.Pixels.Length;n++)image.Pixels[n]=counts[n]>0?image.Pixels[n]/counts[n]:double.NaN;
    return image;
   }
  }
  public static FitsImage Image(string path,System.Threading.CancellationToken ct,Action<int> counted=null,ImageDescriptor selected=null,int index=0) {
   using(var input=Open(path)){Stream s=new ReadCounter(input,n=>{ct.ThrowIfCancellationRequested();if(counted!=null)counted(n);});
    FitsHeader h=selected==null?FindImage(s):SelectedHeader(s,selected,index);int step=Math.Max(2,(int)Math.Ceiling(Math.Max(h.Width,h.Height)/1000.0)); if(step%2!=0)step++;
    int w=(h.Width+step-1)/step,z=(h.Height+step-1)/step;double[] pix=new double[w*z];int[] counts=new int[pix.Length];
    int bpp=Math.Abs(h.Bitpix)/8;byte[] row=new byte[checked(h.Width*bpp)];double scale=h.Number("BSCALE")??1,zero=h.Number("BZERO")??0;double? blank=h.Number("BLANK");
    for(int c=0;c<h.Channels;c++)for(int y=0;y<h.Height;y++) {ct.ThrowIfCancellationRequested();ReadFull(s,row,row.Length);int dy=y/step;
     for(int x=0;x<h.Width;x++){double raw=Decode(row,x*bpp,h.Bitpix);if((blank.HasValue&&h.Bitpix>0&&raw==blank.Value)||double.IsNaN(raw)||double.IsInfinity(raw))continue;
      double v=raw*scale+zero;int idx=dy*w+x/step;pix[idx]+=v;counts[idx]++;
     }
    }
    for(int i=0;i<pix.Length;i++)pix[i]=counts[i]>0?pix[i]/counts[i]:double.NaN;
    return new FitsImage{Pixels=pix,Width=w,Height=z};
   }
  }
 }
}
