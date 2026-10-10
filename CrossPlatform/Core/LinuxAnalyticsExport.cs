using SkiaSharp;
namespace AstroArchive;
public static class LinuxAnalyticsExport
{
    public static void Write(string folder,IList<AnalyticsPage> pages,byte[] logo,CancellationToken ct)
    {
        using var image=SKBitmap.Decode(logo)??throw new InvalidDataException("Analytics logo cannot be decoded.");
        using var output=new FileStream(Path.Combine(folder,"analytics.pdf"),FileMode.CreateNew,FileAccess.Write);
        using var document=SKDocument.CreatePdf(output)??throw new IOException("PDF renderer is unavailable.");
        for(int i=0;i<pages.Count;i++) {
            ct.ThrowIfCancellationRequested(); using(var canvas=document.BeginPage((float)pages[i].CanvasWidth,(float)pages[i].CanvasHeight))Draw(canvas,pages[i],image);
            document.EndPage();
            using var bitmap=new SKBitmap((int)pages[i].CanvasWidth,(int)pages[i].CanvasHeight); using(var canvas=new SKCanvas(bitmap))Draw(canvas,pages[i],image);
            using var png=bitmap.Encode(SKEncodedImageFormat.Png,100); ct.ThrowIfCancellationRequested();
            using var file=new FileStream(Path.Combine(folder,"analytics-page-"+(i+1)+".png"),FileMode.CreateNew,FileAccess.Write); png.SaveTo(file);
            using var jpeg=bitmap.Encode(SKEncodedImageFormat.Jpeg,95);using var jpegFile=new FileStream(Path.Combine(folder,"analytics-page-"+(i+1)+".jpg"),FileMode.CreateNew,FileAccess.Write);jpeg.SaveTo(jpegFile);
        }
        document.Close(); output.Flush(true);
    }
    internal static void Draw(SKCanvas canvas,AnalyticsPage page,SKBitmap logo,double reveal=1,bool clear=true) {
        if(clear)canvas.Clear(SKColor.Parse(page.Background));
        foreach(var mark in page.Marks) {
            using var paint=new SKPaint { Color=SKColor.Parse(mark.Fill??"#000000"),IsAntialias=true };
            using var shader=mark.FillEnd==null?null:SKShader.CreateLinearGradient(new SKPoint((float)mark.X,(float)mark.Y),new SKPoint((float)(mark.X+(mark.VerticalGradient?0:mark.Width)),(float)(mark.Y+(mark.VerticalGradient?mark.Height:0))),[SKColor.Parse(mark.Fill),SKColor.Parse(mark.FillEnd)],[0,1],SKShaderTileMode.Clamp);paint.Shader=shader;
            canvas.Save();
            if(mark.Role=="chart") {
                if(mark.Kind=="rect"&&mark.FillEnd!=null) { canvas.Translate((float)mark.X,(float)(mark.Y+mark.Height));canvas.Scale((float)(mark.VerticalGradient?1:reveal),(float)(mark.VerticalGradient?reveal:1));canvas.Translate((float)-mark.X,(float)(-mark.Y-mark.Height)); }
                else paint.Color=paint.Color.WithAlpha((byte)(255*reveal));
            }
            if(mark.Kind=="rect")canvas.DrawRoundRect(SKRect.Create((float)mark.X,(float)mark.Y,(float)mark.Width,(float)mark.Height),(float)mark.Radius,(float)mark.Radius,paint);
            else if(mark.Kind=="logo")canvas.DrawBitmap(logo,SKRect.Create((float)mark.X,(float)mark.Y,(float)mark.Width,(float)mark.Height));
            else if(mark.Kind=="polygon") { using var path=new SKPath();path.MoveTo((float)mark.Points[0],(float)mark.Points[1]);for(int i=2;i<mark.Points.Length;i+=2)path.LineTo((float)mark.Points[i],(float)mark.Points[i+1]);path.Close();canvas.DrawPath(path,paint); }
            else if(mark.Kind=="text") { using var face=SKTypeface.FromFamilyName("DejaVu Sans",mark.Bold?SKFontStyle.Bold:SKFontStyle.Normal);using var font=new SKFont(face,(float)mark.Size); using var path=font.GetTextPath(mark.Text,new SKPoint((float)mark.X,(float)mark.Y-font.Metrics.Ascent)); canvas.DrawPath(path,paint); }
            canvas.Restore();
        }
    }
}
