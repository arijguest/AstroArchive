// XISF attachment images: bounded XML, uncompressed/zlib/LZ4, optional Zstandard.
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.InteropServices;
namespace AstroArchive {
    public static class PixelCodecs {
        public static void Full(Stream stream,byte[] bytes) {
            int read=0,n;
            while(read<bytes.Length&&(n=stream.Read(bytes,read,bytes.Length-read))>0)read+=n;
            if(read!=bytes.Length)throw new InvalidDataException("Truncated image data.");
        }
        [StructLayout(LayoutKind.Explicit)]struct Sample {
            [FieldOffset(0)]public ulong Bits;
            [FieldOffset(0)]public double Double;
            [FieldOffset(0)]public float Float;
        }
        public static double Number(byte[] bytes,int offset,string format,bool little) {
            int size=Size(format);
            ulong bits=0;
            if(little)for(int i=size-1;i>=0;i--)bits=(bits<<8)|bytes[offset+i];
            else for(int i=0;i<size;i++)bits=(bits<<8)|bytes[offset+i];
            if(format=="Float32")return new Sample {
                Bits=bits
            }
            .Float;
            if(format=="Float64")return new Sample {
                Bits=bits
            }
            .Double;
            return bits;
        }
        public static int Size(string format) {
            switch(format) {
                case "UInt8":return 1;
                case "UInt16":return 2;
                case "UInt32":case "Float32":return 4;
                case "UInt64":case "Float64":return 8;
                default:return 0;
            }
        }
        public static byte[] Inflate(byte[] encoded,int count,CancellationToken ct=default(CancellationToken)) {
            return Xisf.Inflate(encoded,count,ct);
        }
        public static byte[] Lz4(byte[] encoded,int count,CancellationToken ct=default(CancellationToken)) {
            return Xisf.Lz4(encoded,count,ct);
        }
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)][DllImport("libzstd.dll",CallingConvention=CallingConvention.Cdecl)]static extern UIntPtr ZSTD_decompress(byte[] target,UIntPtr size,byte[] source,UIntPtr sourceSize);
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)][DllImport("libzstd.dll",CallingConvention=CallingConvention.Cdecl)]static extern uint ZSTD_isError(UIntPtr size);
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)][DllImport("libzstd.dll",CallingConvention=CallingConvention.Cdecl)]static extern uint ZSTD_versionNumber();
        static readonly Lazy<bool> codecAvailable=new Lazy<bool>(ProbeCodec);
        public static bool ZstdAvailable {
            get {
                return codecAvailable.Value;
            }
        }
        static bool ProbeCodec() {
            try {
                NativeCodecs.Preload("libzstd.dll");
                return ZSTD_versionNumber()>0;
            }
            catch(DllNotFoundException) {
                return false;
            }
            catch(BadImageFormatException) {
                return false;
            }
            catch(EntryPointNotFoundException) {
                return false;
            }
        }
        public static byte[] Zstd(byte[] encoded,int size) {
            if(!ZstdAvailable)throw new NotSupportedException("The optional Zstandard codec is unavailable.");
            var result=new byte[size];
            UIntPtr count=ZSTD_decompress(result,(UIntPtr)size,encoded,(UIntPtr)encoded.Length);
            if(ZSTD_isError(count)!=0||count.ToUInt64()!=(ulong)size)throw new InvalidDataException("Invalid Zstandard image.");
            return result;
        }
    }
    public sealed class XisfReader:IAssetReader {
        public string Name {
            get {
                return "XISF";
            }
        }
        static string A(XElement element,string key,string fallback="") {
            var attribute=element.Attribute(key);
            return attribute==null?fallback:attribute.Value;
        }
        public AssetInfo Inspect(string path,Action<int> counted) {
            using(var stream=File.OpenRead(path)) {
                byte[] signature=new byte[16];
                PixelCodecs.Full(stream,signature);
                if(Encoding.ASCII.GetString(signature,0,8)!="XISF0100")throw new InvalidDataException("Not an XISF 1.0 file.");
                uint length=(uint)(signature[8]|signature[9]<<8|signature[10]<<16|signature[11]<<24);
                if(length==0||length>8*1024*1024||length>stream.Length-16)throw new InvalidDataException("Invalid XISF header size.");
                byte[] xml=new byte[length];
                PixelCodecs.Full(stream,xml);
                if(counted!=null)counted(checked(16+(int)length));
                XDocument document;
                using(var input=new MemoryStream(xml))using(var reader=XmlReader.Create(input,new XmlReaderSettings {
                    DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024
                }))document=XDocument.Load(reader);
                if(document.Root==null||document.Root.Name.LocalName!="xisf")throw new InvalidDataException("Invalid XISF XML root.");
                var result=new AssetInfo {
                    Format="XISF"
                };
                int n=0;
                foreach(var image in document.Descendants().Where(e=>e.Name.LocalName=="Image")) {
                    if(++n>256)throw new InvalidDataException("Too many XISF images.");
                    string[] geometry=A(image,"geometry").Split(':');
                    int w,h,c;
                    if(geometry.Length!=3||!int.TryParse(geometry[0],out w)||!int.TryParse(geometry[1],out h)||!int.TryParse(geometry[2],out c)||w<=0||h<=0||c<1||c>4)throw new InvalidDataException("Invalid XISF image geometry.");
                    string format=A(image,"sampleFormat"),location=A(image,"location"),compression=A(image,"compression"),storage=A(image,"pixelStorage","planar");
                    var header=new FitsHeader {
                        Width=w,Height=h,Channels=c
                    };
                    foreach(var keyword in image.Elements().Where(e=>e.Name.LocalName=="FITSKeyword")) {
                        string value=A(keyword,"value").Trim();
                        if(value.StartsWith("'")&&value.EndsWith("'"))value=value.Substring(1,value.Length-2).Replace("''","'");
                        header.Values[A(keyword,"name")]=value;
                        header.Comments[A(keyword,"name")]=A(keyword,"comment");
                    }
                    foreach(var property in image.Elements().Where(e=>e.Name.LocalName=="Property")) {
                        string id=A(property,"id"),value=A(property,"value",property.Value);
                        if(id=="PCL:CFASourcePattern"||id=="PCL:CFAPattern")header.Values["BAYERPAT"]=value;
                        if(id=="Instrument:Camera:Name")header.Values["INSTRUME"]=value;
                        if(id=="Instrument:Telescope:Name")header.Values["TELESCOP"]=value;
                        if(id=="Observation:Object:Name")header.Values["OBJECT"]=value;
                        if(id=="Observation:Time:Start")header.Values["DATE-OBS"]=value;
                        if(id=="PixInsight:Image:Linear")header.Values["LINEAR"]=value.Equals("true",StringComparison.OrdinalIgnoreCase)?"T":"F";
                    }
                    long offset=0,bytes=0;
                    bool attached=false;
                    string[] attachment=location.Split(':');
                    if(attachment.Length==3&&attachment[0]=="attachment"&&long.TryParse(attachment[1],out offset)&&long.TryParse(attachment[2],out bytes)) {
                        if(offset<16+length||bytes<0||offset>stream.Length||bytes>stream.Length-offset)throw new InvalidDataException("XISF attachment escapes the file.");
                        attached=true;
                    }
                    bool inline=location=="inline:base64"||(location=="embedded"&&image.Elements().Any(e=>e.Name.LocalName=="Data"&&A(e,"encoding")=="base64"));
                    string codec=compression.Split(':')[0].Replace("+sh","");
                    bool known=codec==""||codec=="zlib"||codec=="lz4"||codec=="lz4hc"||codec=="zstd";
                    var descriptor=new ImageDescriptor {
                        Hdu=n-1,Key="image:"+(n-1),Label=A(image,"id","Image "+n),Width=w,Height=h,Channels=c,Count=1,Encoding=format+"|"+A(image,"byteOrder","little")+"|"+storage+(inline?"|base64":""),Bitpix=format=="UInt64"?64:format=="Float32"?-32:format=="Float64"?-64:PixelCodecs.Size(format)*8,Offset=offset,Length=bytes,Compression=compression,Color=A(image,"colorSpace"),Numeric=(attached||inline)&&known&&(A(image,"byteOrder","little")=="little"||A(image,"byteOrder")=="big")&&PixelCodecs.Size(format)>0&&(A(image,"colorSpace")==""||A(image,"colorSpace")=="RGB"||A(image,"colorSpace")=="Gray")&&(storage=="planar"||storage=="normal")&&A(image,"subblocks")=="",Headers=header.Values,Comments=header.Comments
                    };
                    result.Images.Add(descriptor);
                    if(n==1)result.Header=header;
                    if(!descriptor.Numeric)result.Note="An XISF image uses an unsupported attachment, sample format, subblock layout or optional compression codec. Its original file can be archived.";
                }
                if(result.Images.Count==0)throw new InvalidDataException("XISF contains no image.");
                return result;
            }
        }
        public PixelImage Read(string path,ImageDescriptor image,int index,CancellationToken ct) {
            if(index!=0||!image.Numeric)throw new NotSupportedException("This XISF image encoding cannot be decoded.");
            Assets.Dimensions(image.Width,image.Height,image.Channels);
            string[] format=image.Encoding.Split('|');
            int size=PixelCodecs.Size(format[0]),samples=checked(image.Width*image.Height*image.Channels),expected=checked(samples*size);
            if(image.Length<0||image.Length>256L*1024*1024)throw new NotSupportedException("Encoded XISF image exceeds the decoder limit.");
            byte[] encoded;
            if(format.Length>3&&format[3]=="base64") {
                using(var stream=File.OpenRead(path)) {
                    byte[] signature=new byte[16];
                    PixelCodecs.Full(stream,signature);
                    int length=BitConverter.ToInt32(signature,8);
                    if(length<=0||length>8*1024*1024)throw new InvalidDataException("Invalid inline XISF header.");
                    byte[] xml=new byte[length];
                    PixelCodecs.Full(stream,xml);
                    XDocument document;
                    using(var input=new MemoryStream(xml))using(var reader=XmlReader.Create(input,new XmlReaderSettings {
                        DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024
                    }))document=XDocument.Load(reader);
                    var node=document.Descendants().Where(e=>e.Name.LocalName=="Image").ElementAt(image.Hdu);
                    var data=A(node,"location")=="embedded"?node.Elements().First(e=>e.Name.LocalName=="Data"):node;
                    encoded=Convert.FromBase64String(string.Concat(data.Nodes().OfType<XText>().Select(t=>t.Value)));
                }
            }
            else {
                encoded=new byte[(int)image.Length];
                using(var stream=File.OpenRead(path)) {
                    stream.Position=image.Offset;
                    PixelCodecs.Full(stream,encoded);
                }
            }
            ct.ThrowIfCancellationRequested();
            string[] compression=(image.Compression??"").Split(':');
            string codec=compression[0].Replace("+sh","");
            if(codec!="") {
                int declared;
                if(compression.Length<2||!int.TryParse(compression[1],out declared)||declared!=expected)throw new InvalidDataException("XISF decompressed size disagrees with geometry.");
            }
            byte[] raw=codec==""?encoded:codec=="zlib"?PixelCodecs.Inflate(encoded,expected,ct):codec=="lz4"||codec=="lz4hc"?PixelCodecs.Lz4(encoded,expected,ct):codec=="zstd"?PixelCodecs.Zstd(encoded,expected):null;
            if(raw==null||raw.Length!=expected)throw new InvalidDataException("Invalid XISF pixel data length.");
            if(compression[0].EndsWith("+sh")) {
                int item;
                if(compression.Length!=3||!int.TryParse(compression[2],out item)||item!=size)throw new InvalidDataException("Invalid XISF byte shuffle.");
                byte[] unshuffled=new byte[raw.Length];
                for(int b=0;b<size;b++)for(int j=0;j<samples;j++)unshuffled[j*size+b]=raw[b*samples+j];
                raw=unshuffled;
            }
            var pixels=new double[samples];
            int plane=image.Width*image.Height;
            for(int p=0;p<samples;p++) {
                if(p%image.Width==0)ct.ThrowIfCancellationRequested();
                int sample=format[2]=="normal"?(p%plane)*image.Channels+p/plane:p;
                pixels[p]=PixelCodecs.Number(raw,sample*size,format[0],format[1]=="little");
            }
            return new PixelImage {
                Width=image.Width,Height=image.Height,Channels=image.Channels,Pixels=pixels
            };
        }
    }
}
