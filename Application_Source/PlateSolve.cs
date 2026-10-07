// ASTAP CLI protocol and Astrometry.net API. Sources are listed in README.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AstroArchive {
 public class SolveResult {public SkyGeometry Sky{get;set;}public double RA{get;set;}public double Dec{get;set;}public double Radius{get;set;}public string Solver{get;set;}public string Suggested{get;set;}public List<Candidate> Candidates{get;set;}}
 public static class PlateSolve {
  public static string Protect(string key){return string.IsNullOrWhiteSpace(key)?"":Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()),null,DataProtectionScope.CurrentUser));}
  public static string Unprotect(string data){if(string.IsNullOrEmpty(data))return "";try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(data),null,DataProtectionScope.CurrentUser));}catch{return "";}}
  public static string FindAstap(){string[] p={Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"astap.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"astap","astap.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"ASTAP","astap_cli.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"astap","astap.exe")};return p.FirstOrDefault(File.Exists)??"";}
  public static bool Configured(Settings s){return (!s.UseOnline&&File.Exists(s.Astap))||(s.UseOnline&&!string.IsNullOrEmpty(Unprotect(s.ApiKeyProtected)));}
  public static SolveResult Solve(string path,Settings s,CancellationToken ct,Action<string> progress,Action<int> counted=null){
   SolveResult r;if(s.UseOnline){if(string.IsNullOrEmpty(Unprotect(s.ApiKeyProtected)))throw new InvalidOperationException("Add your Astrometry.net API key in Settings.");r=Online(path,s,ct,progress,counted);}else{if(!File.Exists(s.Astap))throw new InvalidOperationException("Choose ASTAP and its installed star catalogue in Settings, or configure Astrometry.net.");r=Local(path,s,ct,progress,counted);}
   r.Candidates=Catalog.Nearby(r.RA,r.Dec,Math.Max(0.25,Math.Min(25,r.Radius)));
   double threshold=Math.Max(0.025,Math.Min(0.15,r.Radius*0.12));var list=r.Candidates;
   if(list.Count>0&&list[0].Separation<threshold&&(list.Count==1||list[1].Separation>Math.Max(list[0].Separation*3,threshold*2)))r.Suggested=list[0].Name;
   return r;
  }
  static string Quote(string s){if(s.Contains("\""))throw new ArgumentException("Quotes are not valid in a file path.");return "\""+s+"\"";}
  static SolveResult Local(string source,Settings settings,CancellationToken ct,Action<string> progress,Action<int> counted){
   string temp=Path.Combine(Path.GetTempPath(),"AstroArchiveSolve_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
   try{string input=Path.Combine(temp,"frame.fits");if(source.EndsWith(".gz",StringComparison.OrdinalIgnoreCase)){using(var f=File.OpenRead(source))using(var g=new GZipStream(f,CompressionMode.Decompress))using(var o=File.Create(input)){byte[] b=new byte[65536];int n;while((n=g.Read(b,0,b.Length))>0){ct.ThrowIfCancellationRequested();o.Write(b,0,n);if(counted!=null)counted(n);}}}else{using(var from=File.OpenRead(source))using(var to=File.Create(input)){byte[] buffer=new byte[65536];int n;while((n=from.Read(buffer,0,buffer.Length))>0){ct.ThrowIfCancellationRequested();to.Write(buffer,0,n);if(counted!=null)counted(n);}}}
    FitsHeader header=Fits.Header(input);double fov=settings.FieldHeight??0;double? focal=header.Number("FOCALLEN","FOCLEN"),pixel=header.Number("YPIXSZ","PIXSIZE");if(fov<=0&&focal.HasValue&&pixel.HasValue&&focal>0&&pixel>0)fov=2*Math.Atan(header.Height*pixel.Value/1000/(2*focal.Value))*180/Math.PI;
    string args="-f "+Quote(input)+" -r 180 -fov "+fov.ToString("0.#####",CultureInfo.InvariantCulture)+" -z 0 -wcs -o "+Quote(Path.Combine(temp,"solution"));if(!string.IsNullOrWhiteSpace(settings.StarDatabase)){if(!Directory.Exists(settings.StarDatabase))throw new DirectoryNotFoundException("ASTAP star database folder not found.");args+=" -d "+Quote(settings.StarDatabase);}
    progress("ASTAP: solving star field...");var psi=new ProcessStartInfo(settings.Astap,args){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(settings.Astap)};
    using(var p=Process.Start(psi)){DateTime deadline=DateTime.UtcNow.AddMinutes(5);try{while(!p.WaitForExit(250)){ct.ThrowIfCancellationRequested();if(DateTime.UtcNow>deadline)throw new TimeoutException("ASTAP solve exceeded five minutes.");}}catch{try{if(!p.HasExited)p.Kill();}catch{}throw;}
     string ini=Path.Combine(temp,"solution.ini");if(!File.Exists(ini))throw new IOException("ASTAP returned no solution file (exit "+p.ExitCode+"). Check the star database installation.");
     var h=ParseIni(File.ReadAllText(ini));if(h.Get("PLTSOLVD")!="T")throw new InvalidDataException("ASTAP could not solve this frame. "+h.Get("ERROR","WARNING"));double? ra=h.Number("CRVAL1"),dec=h.Number("CRVAL2");if(!ra.HasValue||!dec.HasValue)throw new InvalidDataException("ASTAP solution lacks valid coordinates.");
     double sx=Math.Abs(h.Number("CDELT1")??Math.Sqrt(Math.Pow(h.Number("CD1_1")??0,2)+Math.Pow(h.Number("CD2_1")??0,2))),sy=Math.Abs(h.Number("CDELT2")??Math.Sqrt(Math.Pow(h.Number("CD1_2")??0,2)+Math.Pow(h.Number("CD2_2")??0,2)));
     double radius=Math.Sqrt(Math.Pow(sx*header.Width,2)+Math.Pow(sy*header.Height,2))/2;if(radius<=0)radius=1;
     return new SolveResult{RA=ra.Value,Dec=dec.Value,Radius=radius,Solver="ASTAP",Sky=LocalGeometry(h,temp,input,header.Width,header.Height)};}
   }finally{foreach(string f in Directory.GetFiles(temp)){try{File.Delete(f);}catch{}}try{Directory.Delete(temp);}catch{}}
  }
  static SkyGeometry LocalGeometry(FitsHeader ini,string directory,string input,int width,int height){
   var sky=MosaicGeometry.FromHeader(ini,width,height,"ASTAP WCS");if(sky!=null)return sky;
   foreach(string path in new[]{Path.Combine(directory,"solution.wcs"),Path.Combine(directory,"frame.wcs")})try{if(File.Exists(path))using(var stream=File.OpenRead(path)){sky=MosaicGeometry.FromHeader(MosaicGeometry.WcsCards(stream),width,height,"ASTAP WCS");if(sky!=null)return sky;}}catch{}
   try{return MosaicGeometry.FromHeader(Fits.Header(input),width,height,"ASTAP temporary FITS WCS");}catch{return null;}
  }
  static SkyGeometry OnlineGeometry(long job,int width,int height,CancellationToken ct){
   try{var request=(HttpWebRequest)WebRequest.Create("https://nova.astrometry.net/wcs_file/"+job);request.Timeout=30000;request.ReadWriteTimeout=30000;using(ct.Register(()=>request.Abort()))using(var response=request.GetResponse())using(var stream=response.GetResponseStream())return MosaicGeometry.FromHeader(MosaicGeometry.WcsCards(stream),width,height,"Astrometry.net WCS");}catch{ct.ThrowIfCancellationRequested();return null;}
  }
  public static FitsHeader ParseIni(string text){var h=new FitsHeader();foreach(string line in text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){int i=line.IndexOf('=');if(i<1)continue;string k=line.Substring(0,i).Trim(),v=line.Substring(i+1).Trim();int comment=v.IndexOf("//",StringComparison.Ordinal);if(comment>=0)v=v.Substring(0,comment).Trim();h.Values[k]=v;}return h;}
  static Dictionary<string,object> Request(string url,string form,byte[] multipart,string boundary,CancellationToken ct){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var r=(HttpWebRequest)WebRequest.Create(url);r.Timeout=30000;r.ReadWriteTimeout=30000;r.Method=form==null&&multipart==null?"GET":"POST";r.UserAgent="AstroArchive/1.0";
   using(ct.Register(()=>r.Abort())){try{if(r.Method=="POST"){byte[] bytes=multipart??Encoding.UTF8.GetBytes("request-json="+Uri.EscapeDataString(form));r.ContentType=multipart==null?"application/x-www-form-urlencoded":"multipart/form-data; boundary="+boundary;r.ContentLength=bytes.Length;using(var s=r.GetRequestStream())s.Write(bytes,0,bytes.Length);}using(var reply=r.GetResponse())using(var s=reply.GetResponseStream())using(var reader=new StreamReader(s)){var data=Util.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());object status;if(data.TryGetValue("status",out status)&&Convert.ToString(status)=="error")throw new InvalidOperationException("Astrometry.net rejected the request. Check the API key and solver settings.");return data;}}catch(WebException){ct.ThrowIfCancellationRequested();throw;}}
  }
  static SolveResult Online(string path,Settings settings,CancellationToken ct,Action<string> progress,Action<int> counted){
   progress("Detecting stars for Astrometry.net...");FitsImage image=Fits.Image(path,ct,counted);var stars=Rotation.Detect(image,250);if(stars.Count<12)throw new InvalidDataException("Not enough stars for plate solving.");
   var login=Request("https://nova.astrometry.net/api/login",Util.Serialize(new{apikey=Unprotect(settings.ApiKeyProtected)}),null,null,ct);object sess;if(!login.TryGetValue("session",out sess))throw new InvalidDataException("Astrometry.net login did not return a session.");
   var options=new Dictionary<string,object>{{"session",sess},{"publicly_visible","n"},{"allow_commercial_use","n"},{"allow_modifications","n"},{"image_width",image.Width},{"image_height",image.Height},{"scale_units","degwidth"},{"scale_type","ul"},{"scale_lower",0.15},{"scale_upper",90.0},{"crpix_center",true},{"positional_error",1.5}};
   byte[] xy=XYList(stars,image.Width,image.Height);string boundary="AstroArchive"+Guid.NewGuid().ToString("N");byte[] body;
   using(var ms=new MemoryStream()){byte[] first=Encoding.UTF8.GetBytes("--"+boundary+"\r\nContent-Disposition: form-data; name=\"request-json\"\r\nContent-Type: text/plain\r\n\r\n"+Util.Serialize(options)+"\r\n--"+boundary+"\r\nContent-Disposition: form-data; name=\"file\"; filename=\"stars.xyls\"\r\nContent-Type: application/octet-stream\r\n\r\n");ms.Write(first,0,first.Length);ms.Write(xy,0,xy.Length);byte[] last=Encoding.ASCII.GetBytes("\r\n--"+boundary+"--\r\n");ms.Write(last,0,last.Length);body=ms.ToArray();}
   progress("Submitting star coordinates to Astrometry.net...");var upload=Request("https://nova.astrometry.net/api/upload",null,body,boundary,ct);object sub;if(!upload.TryGetValue("subid",out sub))throw new InvalidDataException("Astrometry.net returned no submission ID.");
   DateTime deadline=DateTime.UtcNow.AddMinutes(10);long job=0;
   while(DateTime.UtcNow<deadline){ct.ThrowIfCancellationRequested();if(job==0){var status=Request("https://nova.astrometry.net/api/submissions/"+sub,null,null,null,ct);object jobs;if(status.TryGetValue("jobs",out jobs)){foreach(object j in (IEnumerable)jobs)if(j!=null){job=Convert.ToInt64(j);break;}}progress("Astrometry.net: waiting for solve job...");}else{
     var status=Request("https://nova.astrometry.net/api/jobs/"+job,null,null,null,ct);object state;status.TryGetValue("status",out state);if(Convert.ToString(state)=="failure")throw new InvalidDataException("Astrometry.net could not solve this field.");if(Convert.ToString(state)=="success"){var cal=Request("https://nova.astrometry.net/api/jobs/"+job+"/calibration/",null,null,null,ct);return new SolveResult{RA=Convert.ToDouble(cal["ra"],CultureInfo.InvariantCulture),Dec=Convert.ToDouble(cal["dec"],CultureInfo.InvariantCulture),Radius=Convert.ToDouble(cal["radius"],CultureInfo.InvariantCulture),Solver="Astrometry.net job "+job,Sky=OnlineGeometry(job,image.Width,image.Height,ct)};}progress("Astrometry.net: solving job "+job+"...");}
    if(ct.WaitHandle.WaitOne(2000))ct.ThrowIfCancellationRequested();
   }throw new TimeoutException("Astrometry.net has not completed the solve after ten minutes. Try again later or use local ASTAP.");
  }
  static string Card(string key,string val){return (key.PadRight(8)+"= "+val).PadRight(80).Substring(0,80);}
  static void Header(Stream s,IEnumerable<string> cards){string v=string.Concat(cards)+"END".PadRight(80);v=v.PadRight(((v.Length+2879)/2880)*2880);byte[] b=Encoding.ASCII.GetBytes(v);s.Write(b,0,b.Length);}
  public static byte[] XYList(List<Star> stars,int w,int h){using(var s=new MemoryStream()){
   Header(s,new[]{Card("SIMPLE","T".PadLeft(20)),Card("BITPIX","8".PadLeft(20)),Card("NAXIS","0".PadLeft(20)),Card("EXTEND","T".PadLeft(20))});
   Header(s,new[]{Card("XTENSION","'BINTABLE'"),Card("BITPIX","8".PadLeft(20)),Card("NAXIS","2".PadLeft(20)),Card("NAXIS1","16".PadLeft(20)),Card("NAXIS2",stars.Count.ToString().PadLeft(20)),Card("PCOUNT","0".PadLeft(20)),Card("GCOUNT","1".PadLeft(20)),Card("TFIELDS","2".PadLeft(20)),Card("TTYPE1","'X'"),Card("TFORM1","'D'"),Card("TTYPE2","'Y'"),Card("TFORM2","'D'"),Card("IMAGEW",w.ToString().PadLeft(20)),Card("IMAGEH",h.ToString().PadLeft(20))});
   foreach(var p in stars){byte[] x=BitConverter.GetBytes(p.X+1),y=BitConverter.GetBytes(p.Y+1);Array.Reverse(x);Array.Reverse(y);s.Write(x,0,8);s.Write(y,0,8);}while(s.Length%2880!=0)s.WriteByte(0);return s.ToArray();}}
 }
}
