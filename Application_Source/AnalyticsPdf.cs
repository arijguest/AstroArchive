using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AstroArchive {
 // Dependency-free PDF 1.4 writer. Charts and outlined text stay vector; only the
 // bundled logo is raster. Each report receives its own landscape print page.
 public static class AnalyticsPdf {
  static byte[] Bytes(string text){return Encoding.ASCII.GetBytes(text);}
  static byte[] Compress(byte[] source){
   using(var output=new MemoryStream()){
    output.WriteByte(0x78);output.WriteByte(0x9C);using(var deflate=new DeflateStream(output,CompressionMode.Compress,true))deflate.Write(source,0,source.Length);
    uint a=1,b=0;foreach(byte value in source){a=(a+value)%65521;b=(b+a)%65521;}uint checksum=(b<<16)|a;
    output.WriteByte((byte)(checksum>>24));output.WriteByte((byte)(checksum>>16));output.WriteByte((byte)(checksum>>8));output.WriteByte((byte)checksum);return output.ToArray();
   }
  }
  static byte[] StreamObject(string attributes,byte[] data){
   using(var output=new MemoryStream()){byte[] prefix=Bytes("<< "+attributes+" /Length "+data.Length+" >>\nstream\n");output.Write(prefix,0,prefix.Length);output.Write(data,0,data.Length);byte[] end=Bytes("\nendstream");output.Write(end,0,end.Length);return output.ToArray();}
  }
  static string Colour(string hex){int value=int.Parse(hex.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);return AnalyticsGraphics.N((value>>16)/255.0)+" "+AnalyticsGraphics.N(((value>>8)&255)/255.0)+" "+AnalyticsGraphics.N((value&255)/255.0)+" rg\n";}
  public static void Write(Stream destination,IList<AnalyticsPage> pages,byte[] logoRgb,int logoWidth,int logoHeight,Func<AnalyticsMark,string> outlinedText){
   var objects=new List<byte[]>();objects.Add(Bytes("<< /Type /Catalog /Pages 2 0 R >>"));objects.Add(null);
   objects.Add(StreamObject("/Type /XObject /Subtype /Image /Width "+logoWidth+" /Height "+logoHeight+" /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode",Compress(logoRgb)));
   var kids=new StringBuilder();for(int i=0;i<pages.Count;i++){
    int pageId=objects.Count+1,contentId=pageId+1;kids.Append(pageId+" 0 R ");
    // 900 x 600 points = 12.5 x 8.33 inches, with the same aspect as the preview.
    objects.Add(Bytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 900 600] /Resources << /XObject << /Logo 3 0 R >> >> /Contents "+contentId+" 0 R >>"));
    var content=new StringBuilder("q\n0.75 0 0 -0.75 0 600 cm\n");
    foreach(var mark in pages[i].Marks){
     if(mark.Kind=="logo"){content.Append("q "+AnalyticsGraphics.N(mark.Width)+" 0 0 "+AnalyticsGraphics.N(-mark.Height)+" "+AnalyticsGraphics.N(mark.X)+" "+AnalyticsGraphics.N(mark.Y+mark.Height)+" cm /Logo Do Q\n");continue;}
     content.Append(Colour(mark.Fill));
     if(mark.Kind=="rect")content.Append(AnalyticsGraphics.N(mark.X)+" "+AnalyticsGraphics.N(mark.Y)+" "+AnalyticsGraphics.N(mark.Width)+" "+AnalyticsGraphics.N(mark.Height)+" re f\n");
     else if(mark.Kind=="polygon"){
      for(int j=0;j<mark.Points.Length;j+=2)content.Append(AnalyticsGraphics.N(mark.Points[j])+" "+AnalyticsGraphics.N(mark.Points[j+1])+(j==0?" m\n":" l\n"));content.Append("h f\n");
     }else if(mark.Kind=="text")content.Append(outlinedText(mark));
    }content.Append("Q\n");objects.Add(StreamObject("/Filter /FlateDecode",Compress(Bytes(content.ToString()))));
   }
   objects[1]=Bytes("<< /Type /Pages /Count "+pages.Count+" /Kids [ "+kids+" ] >>");
   var offsets=new List<long>();Action<string> write=text=>{var bytes=Bytes(text);destination.Write(bytes,0,bytes.Length);};write("%PDF-1.4\n% AstroArchive Analytics\n");
   for(int i=0;i<objects.Count;i++){offsets.Add(destination.Position);write((i+1)+" 0 obj\n");destination.Write(objects[i],0,objects[i].Length);write("\nendobj\n");}
   long xref=destination.Position;write("xref\n0 "+(objects.Count+1)+"\n0000000000 65535 f \n");foreach(long offset in offsets)write(offset.ToString("0000000000",CultureInfo.InvariantCulture)+" 00000 n \n");
   write("trailer\n<< /Size "+(objects.Count+1)+" /Root 1 0 R >>\nstartxref\n"+xref.ToString(CultureInfo.InvariantCulture)+"\n%%EOF\n");
  }
 }
}
