using System;
using System.IO;
using System.Text;
using System.Threading;
namespace AstroArchive {
    public sealed class SerReader:IAssetReader,ISampledAssetReader {
        public string Name {
            get {
                return "SER";
            }
        }
        public AssetInfo Inspect(string path,Action<int> counted) {
            using(var stream=File.OpenRead(path)) {
                byte[] bytes=new byte[178];
                PixelCodecs.Full(stream,bytes);
                if(Encoding.ASCII.GetString(bytes,0,14)!="LUCAM-RECORDER")throw new InvalidDataException("Not a SER recording.");
                int color=Int(bytes,18),endian=Int(bytes,22),w=Int(bytes,26),h=Int(bytes,30),bits=Int(bytes,34),frames=Int(bytes,38),channels=color==100||color==101?3:1;
                if(w<=0||h<=0||frames<=0||(bits!=8&&bits!=16)||(endian!=0&&endian!=1))throw new InvalidDataException("Invalid SER header.");
                long length=checked((long)w*h*channels*(bits/8)*frames);
                if(length>stream.Length-178)throw new InvalidDataException("Truncated SER recording.");
                var header=new FitsHeader {
                    Width=w,Height=h,Channels=channels
                };
                header.Values["OBSERVER"]=Text(bytes,42);
                header.Values["INSTRUME"]=Text(bytes,82);
                header.Values["TELESCOP"]=Text(bytes,122);
                header.Values["IMAGETYP"]="Light";
                long utc=BitConverter.ToInt64(bytes,170);
                if(utc>0&&utc<=DateTime.MaxValue.Ticks)header.Values["DATE-OBS"]=new DateTime(utc,DateTimeKind.Utc).ToString("o");
                string bayer=color==8?"RGGB":color==9?"GRBG":color==10?"GBRG":color==11?"BGGR":"";
                header.Values["BAYERPAT"]=bayer;
                bool supported=color==0||color==100||color==101||bayer!="";
                var descriptor=new ImageDescriptor {
                    Key="sequence:0",Label=frames+" frames",Width=w,Height=h,Channels=channels,Bitpix=bits,Count=frames,Offset=178,Length=length,Encoding=(endian==0?"little":"big")+"|"+color,Color=bayer,Numeric=supported,Headers=header.Values,Comments=header.Comments
                };
                if(counted!=null)counted(178);
                return new AssetInfo {
                    Format="SER",DurationSeconds=VideoHeaders.SerDuration(stream,length,frames,counted),DurationSource="SER capture timestamp span",Header=header,Images= {
                        descriptor
                    },Note=supported?"Planetary recording. Preview frames or export the original sequence.":"This SER colour arrangement is archived without pixel decoding."
                };
            }
        }
        static int Int(byte[] b,int p) {
            return b[p]|b[p+1]<<8|b[p+2]<<16|b[p+3]<<24;
        }
        static string Text(byte[] b,int p) {
            return Encoding.ASCII.GetString(b,p,40).TrimEnd('\0',' ');
        }
        public PixelImage Read(string path,ImageDescriptor image,int index,CancellationToken ct) {
            if(index<0||index>=image.Count||!image.Numeric)throw new ArgumentOutOfRangeException("index");
            Assets.Dimensions(image.Width,image.Height,image.Channels);
            int samples=checked(image.Width*image.Height*image.Channels),size=image.Bitpix/8;
            byte[] raw=new byte[checked(samples*size)];
            using(var stream=File.OpenRead(path)) {
                stream.Position=checked(image.Offset+(long)index*raw.Length);
                PixelCodecs.Full(stream,raw);
            }
            var pixels=new double[samples];
            int plane=image.Width*image.Height;
            string[] encoding=image.Encoding.Split('|');
            int color=int.Parse(encoding[1]);
            for(int p=0;p<plane;p++) {
                if(p%image.Width==0)ct.ThrowIfCancellationRequested();
                for(int c=0;c<image.Channels;c++) {
                    int source=(p*image.Channels+(color==101?2-c:c))*size;
                    pixels[c*plane+p]=PixelCodecs.Number(raw,source,size==1?"UInt8":"UInt16",encoding[0]=="little");
                }
            }
            return new PixelImage {
                Width=image.Width,Height=image.Height,Channels=image.Channels,Pixels=pixels
            };
        }
        public PreviewData Preview(Frame frame,string path,ImageDescriptor image,int index,CancellationToken ct) {
            ct.ThrowIfCancellationRequested();if(index<0||index>=image.Count||!image.Numeric)throw new ArgumentOutOfRangeException("index");
            var result=new SampledPreview(frame,image);int size=image.Bitpix/8;var row=new byte[checked(image.Width*image.Channels*size)];
            string[] encoding=image.Encoding.Split('|');int color=int.Parse(encoding[1]);
            using(var stream=File.OpenRead(path)) {
                stream.Position=checked(image.Offset+(long)index*row.Length*image.Height);
                for(int y=0;y<image.Height;y++) {
                    ct.ThrowIfCancellationRequested();PixelCodecs.Full(stream,row);
                    for(int x=0;x<image.Width;x++)for(int c=0;c<image.Channels;c++)result.Add(x,y,c,PixelCodecs.Number(row,(x*image.Channels+(color==101?2-c:c))*size,size==1?"UInt8":"UInt16",encoding[0]=="little"));
                }
            }
            return result.Finish(frame,path,ct);
        }
    }
}
