// Signature checks keep original-file archiving available without loading WPF.
using System;
using System.IO;
namespace AstroArchive {
    public static class RasterHeaders {
        public static AssetInfo Inspect(string path) {
            string extension=Assets.Extension(path),format=extension==".png"?"PNG":extension==".jpg"||extension==".jpeg"?"JPEG":"TIFF";
            using(var stream=File.OpenRead(path)) {
                byte[] header=new byte[33];
                int read=stream.Read(header,0,header.Length);
                if(format=="PNG") {
                    byte[] magic= {
                        137,80,78,71,13,10,26,10
                    };
                    if(read<33)throw new InvalidDataException("Truncated PNG header.");
                    for(int i=0;i<8;i++)if(header[i]!=magic[i])throw new InvalidDataException("Not a PNG image.");
                    if(header[12]!=73||header[13]!=72||header[14]!=68||header[15]!=82)throw new InvalidDataException("PNG has no IHDR.");
                    long width=Big(header,16),height=Big(header,20);
                    if(width<=0||height<=0||width>int.MaxValue||height>int.MaxValue)throw new InvalidDataException("Invalid PNG dimensions.");
                    int type=header[25],channels=type==0||type==4?1:3;
                    return new AssetInfo {
                        Format=format,Header=new FitsHeader {
                            Width=(int)width,Height=(int)height,Channels=channels
                        },Images= {
                            new ImageDescriptor {
                                Key="page:0",Label="Image 1",Width=(int)width,Height=(int)height,Channels=channels,Bitpix=header[24],Count=1,Encoding=type==0||type==2?"raster":"preview only",Numeric=true
                            }
                        },Note="Windows pixel decoding is required for raster preview/conversion."
                    };
                }
                if(format=="JPEG") {
                    if(read<3||header[0]!=255||header[1]!=216||header[2]!=255)throw new InvalidDataException("Not a JPEG image.");
                }
                else if(read<8||!((header[0]==73&&header[1]==73&&(header[2]==42||header[2]==43)&&header[3]==0)||(header[0]==77&&header[1]==77&&header[2]==0&&(header[3]==42||header[3]==43))))throw new InvalidDataException("Not a TIFF image.");
                return new AssetInfo {
                    Format=format,Note="Windows pixel decoding is required for raster inspection and preview."
                };
            }
        }
        static uint Big(byte[] value,int p) {
            return (uint)value[p]<<24|(uint)value[p+1]<<16|(uint)value[p+2]<<8|value[p+3];
        }
    }
}
