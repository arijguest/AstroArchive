using SkiaSharp;
namespace AstroArchive.Desktop;
public static class AnalyticsMediaCheck
{
    public static int Run(string? directory)
    {
        string root=Path.GetFullPath(directory??Path.Combine(Path.GetTempPath(),"astroarchive-media-"+Guid.NewGuid().ToString("N")));
        try {
            if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("Choose a new empty media test directory.");
            using var session=new ArchiveSession(Path.Combine(root,"config","settings.json"));session.Open(Path.Combine(root,"archive"));
            var plan=session.Scan(ReleaseFixture.Prepare(root),"Media scope","Auto",CancellationToken.None,_=>{});session.Import(plan.Frames,CancellationToken.None,_=>{});session.Refresh();
            using var stream=typeof(Repository).Assembly.GetManifestResourceStream("AstroArchive_Logo.png")!;using var bytes=new MemoryStream();stream.CopyTo(bytes);byte[] logo=bytes.ToArray();
            foreach(var layout in Enum.GetValues<AnalyticsLayout>()) {
                foreach(bool dark in new[]{false,true}) {
                    var page=AnalyticsGraphics.Page(session.Analytics,0,dark,layout);string prefix=Path.Combine(root,"media-"+layout+"-"+(dark?"Dark":"Light"));
                    var options=new LinuxAnimationOptions { SecondsPerChart=2,FramesPerSecond=4,MaximumEdge=320 };
                    using(var reference=LinuxAnalyticsAnimation.Frame([page],logo,options,1.75))using(var png=reference.Encode(SKEncodedImageFormat.Png,100))using(var output=File.Create(prefix+"-reference.png"))png.SaveTo(output);
                    foreach(string format in new[]{"GIF","MP4"})LinuxAnalyticsAnimation.Save(prefix+"."+format.ToLowerInvariant(),[page],logo,format,options,CancellationToken.None);
                }
                LinuxAnalyticsAnimation.Save(Path.Combine(root,"media-native-"+layout+".mp4"),[AnalyticsGraphics.Page(session.Analytics,0,true,layout)],logo,"MP4",new LinuxAnimationOptions { SecondsPerChart=.125,FramesPerSecond=24,MaximumEdge=3840 },CancellationToken.None);
            }
            var pages=Enumerable.Range(0,6).Select(i=>AnalyticsGraphics.Page(session.Analytics,i,true,AnalyticsLayout.Vertical)).ToList();
            foreach(string format in new[]{"GIF","MP4"})LinuxAnalyticsAnimation.Save(Path.Combine(root,"media-story."+format.ToLowerInvariant()),pages,logo,format,new LinuxAnimationOptions { SecondsPerChart=1.5,FramesPerSecond=4,MaximumEdge=320 },CancellationToken.None);
            Console.WriteLine("PASS: Linux media fixtures created for independent decoding");return 0;
        } catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
