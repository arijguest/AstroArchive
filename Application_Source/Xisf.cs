// XISF 1.0: first image, inline/embedded/attached samples, zlib/LZ4 and byte shuffle.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

namespace AstroArchive {
 public static class Xisf {
  const int Limit=256*1024*1024;
  static string Attr(XmlElement e,string name,string fallback=""){return e.HasAttribute(name)?e.GetAttribute(name):fallback;}
  public static PreviewData Read(string path,CancellationToken ct){
   ct.ThrowIfCancellationRequested();using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new BinaryReader(stream)){
    if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="XISF0100")throw new InvalidDataException("Not an XISF 1.0 file.");
    int length=reader.ReadInt32();reader.ReadInt32();if(length<1||length>4*1024*1024||length>stream.Length-16)throw new InvalidDataException("Invalid XISF header length.");
    var doc=new XmlDocument{XmlResolver=null};using(var xml=XmlReader.Create(new MemoryStream(reader.ReadBytes(length)),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))doc.Load(xml);
    var node=doc.SelectSingleNode("/*[local-name()='xisf']/*[local-name()='Image']") as XmlElement;if(node==null)throw new InvalidDataException("No XISF image found.");
    string[] geometry=Attr(node,"geometry").Split(':');int width,height,channels;
    if(geometry.Length!=3||!int.TryParse(geometry[0],out width)||!int.TryParse(geometry[1],out height)||!int.TryParse(geometry[2],out channels)||width<1||height<1||width>100000||height>100000||!(channels==1||channels==3))throw new NotSupportedException("Preview supports two-dimensional mono and RGB XISF images.");
    string format=Attr(node,"sampleFormat");int size;switch(format){case "UInt8":size=1;break;case "UInt16":size=2;break;case "UInt32":case "Float32":size=4;break;case "UInt64":case "Float64":size=8;break;default:throw new NotSupportedException("Unsupported XISF sample format: "+format);}
    long expected=checked((long)width*height*channels*size);if(expected>Limit)throw new NotSupportedException("XISF preview currently supports up to 256 MB of decoded samples.");
    string location=Attr(node,"location");byte[] bytes;
    if(location.StartsWith("attachment:")){
     string[] parts=location.Split(':');long offset,n;if(parts.Length!=3||!long.TryParse(parts[1],out offset)||!long.TryParse(parts[2],out n)||offset<16+length||n<0||n>Limit||offset>stream.Length-n)throw new InvalidDataException("Invalid XISF attachment.");
     stream.Position=offset;bytes=reader.ReadBytes((int)n);if(bytes.Length!=n)throw new InvalidDataException("Truncated XISF attachment.");
    }else{
     XmlElement data=node;string encoding="";if(location.StartsWith("inline:"))encoding=location.Substring(7);else if(location=="embedded"){data=node.SelectSingleNode("*[local-name()='Data']") as XmlElement;if(data!=null)encoding=Attr(data,"encoding");}
     if(data==null||encoding!="base64")throw new NotSupportedException("XISF preview requires attached or base64 inline/embedded image data.");if(data.InnerText.Length>Limit*2)throw new InvalidDataException("XISF inline data exceeds the preview limit.");bytes=Convert.FromBase64String(data.InnerText);
    }
    ct.ThrowIfCancellationRequested();string compression=Attr(node,"compression");if(compression.Length>0){
     string[] parts=compression.Split(':');int decoded;if(parts.Length<2||!int.TryParse(parts[1],out decoded)||decoded!=expected)throw new InvalidDataException("Invalid XISF decoded size.");string codec=parts[0];bool shuffle=codec.EndsWith("+sh");if(shuffle)codec=codec.Substring(0,codec.Length-3);
     if(codec=="zlib")bytes=Inflate(bytes,decoded,ct);else if(codec=="lz4"||codec=="lz4hc")bytes=Lz4(bytes,decoded,ct);else throw new NotSupportedException("Unsupported XISF compression: "+codec+". Save with zlib or LZ4 to preview.");
     if(shuffle){int itemSize;if(parts.Length!=3||!int.TryParse(parts[2],out itemSize)||itemSize!=size)throw new InvalidDataException("Invalid XISF byte shuffle.");byte[] unshuffled=new byte[decoded];int items=decoded/itemSize;for(int b=0;b<itemSize;b++){ct.ThrowIfCancellationRequested();for(int n=0;n<items;n++)unshuffled[n*itemSize+b]=bytes[b*items+n];}bytes=unshuffled;}
    }
    if(bytes.LongLength!=expected)throw new InvalidDataException("XISF sample size does not match its geometry.");
    string order=Attr(node,"byteOrder","little"),storage=Attr(node,"pixelStorage","planar");if(order!="little"&&order!="big")throw new InvalidDataException("Invalid XISF byte order.");if(storage!="planar"&&storage!="normal")throw new NotSupportedException("Unsupported XISF pixel storage.");
    if((order=="little")!=BitConverter.IsLittleEndian)for(int n=0;n<bytes.Length;n+=size){if(n%65536==0)ct.ThrowIfCancellationRequested();Array.Reverse(bytes,n,size);}
    var cfaProperty=node.SelectSingleNode("*[local-name()='Property' and (@id='PCL:CFASourcePattern' or @id='PCL:CFAPattern')]") as XmlElement;
    string pattern=cfaProperty==null?"":Attr(cfaProperty,"value",cfaProperty.InnerText).Trim().ToUpperInvariant();bool cfa=channels==1&&new[]{"RGGB","BGGR","GRBG","GBRG"}.Contains(pattern);int displayChannels=cfa?3:channels;
    int step=Math.Max(cfa?2:1,(int)Math.Ceiling(Math.Max(width,height)/1400.0));if(cfa&&step%2!=0)step++;int w=(width+step-1)/step,h=(height+step-1)/step;
    var image=new PreviewData{Width=w,Height=h,SourceWidth=width,SourceHeight=height,Channels=displayChannels,Pixels=new double[w*h*displayChannels],Description="XISF · "+format+" · "+(cfa?pattern+" colour":channels==3?"RGB":"mono"),Maximum=format.StartsWith("UInt")?Math.Pow(2,size*8)-1:1};var counts=new int[image.Pixels.Length];
    for(int y=0;y<height;y++){ct.ThrowIfCancellationRequested();for(int x=0;x<width;x++)for(int c=0;c<channels;c++){
     int offset=(storage=="planar"?(c*width*height+y*width+x):((y*width+x)*channels+c))*size;double v;
     switch(format){case "UInt8":v=bytes[offset];break;case "UInt16":v=BitConverter.ToUInt16(bytes,offset);break;case "UInt32":v=BitConverter.ToUInt32(bytes,offset);break;case "UInt64":v=BitConverter.ToUInt64(bytes,offset);break;case "Float32":v=BitConverter.ToSingle(bytes,offset);break;default:v=BitConverter.ToDouble(bytes,offset);break;}
     if(double.IsNaN(v)||double.IsInfinity(v))continue;int displayChannel=cfa?"RGB".IndexOf(pattern[(y&1)*2+(x&1)]):c;int index=((y/step)*w+x/step)*displayChannels+displayChannel;image.Pixels[index]+=v;counts[index]++;
    }}for(int n=0;n<counts.Length;n++)image.Pixels[n]=counts[n]>0?image.Pixels[n]/counts[n]:double.NaN;return image;
   }
  }
  public static byte[] Inflate(byte[] input,int size,CancellationToken ct){
   if(input.Length<6||(input[0]&15)!=8||((input[0]<<8)+input[1])%31!=0||(input[1]&32)!=0)throw new InvalidDataException("Invalid zlib header.");
   byte[] output=new byte[size];using(var compressed=new MemoryStream(input,2,input.Length-6))using(var inflater=new DeflateStream(compressed,CompressionMode.Decompress)){int n=0;while(n<size){ct.ThrowIfCancellationRequested();int read=inflater.Read(output,n,Math.Min(65536,size-n));if(read==0)throw new InvalidDataException("Truncated zlib samples.");n+=read;}if(inflater.ReadByte()!=-1)throw new InvalidDataException("Oversized zlib samples.");}
   uint a=1,b=0;for(int n=0;n<output.Length;n++){if(n%65536==0)ct.ThrowIfCancellationRequested();a=(a+output[n])%65521;b=(b+a)%65521;}uint expected=((uint)input[input.Length-4]<<24)|((uint)input[input.Length-3]<<16)|((uint)input[input.Length-2]<<8)|input[input.Length-1];if((b<<16|a)!=expected)throw new InvalidDataException("XISF zlib checksum mismatch.");return output;
  }
  public static byte[] Lz4(byte[] input,int size,CancellationToken ct){
   byte[] output=new byte[size];int p=0,n=0;
   while(p<input.Length){ct.ThrowIfCancellationRequested();int token=input[p++],literal=token>>4,match=token&15;if(literal==15)literal=Extended(input,ref p,literal);
    if(literal>input.Length-p||literal>size-n)throw new InvalidDataException("Invalid LZ4 literals.");Buffer.BlockCopy(input,p,output,n,literal);p+=literal;n+=literal;if(p==input.Length)break;
    if(p+2>input.Length)throw new InvalidDataException("Truncated LZ4 offset.");int offset=input[p]|input[p+1]<<8;p+=2;if(offset<1||offset>n)throw new InvalidDataException("Invalid LZ4 offset.");if(match==15)match=Extended(input,ref p,match);match=checked(match+4);if(match>size-n)throw new InvalidDataException("Oversized LZ4 output.");for(int k=0;k<match;k++){if(k%65536==0)ct.ThrowIfCancellationRequested();output[n]=output[n-offset];n++;}
   }if(n!=size)throw new InvalidDataException("Truncated LZ4 samples.");return output;
  }
  static int Extended(byte[] input,ref int p,int value){int next;do{if(p>=input.Length)throw new InvalidDataException("Truncated LZ4 length.");next=input[p++];value=checked(value+next);}while(next==255);return value;}
 }
}
