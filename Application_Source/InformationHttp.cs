using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using AstroArchive.Installation;

namespace AstroArchive {
 public sealed class InformationRateLimit:IOException {public DateTime RetryUtc;public InformationRateLimit(DateTime retry):base("The information provider asked for a pause."){RetryUtc=retry;}}
 public static class InformationHttp {
  public const string UserAgent="AstroArchive/3.3.3 (https://astroarchive-companion-test.arijguest.chatgpt.site; target information)";
  static readonly SemaphoreSlim transport=new SemaphoreSlim(2,2);
  public static Func<Uri,int,CancellationToken,byte[]> Fetch=Download;
  public static bool WikipediaHost(string host){return host=="en.wikipedia.org"||host=="fr.wikipedia.org"||host=="de.wikipedia.org"||host=="es.wikipedia.org"||host=="it.wikipedia.org"||host=="ja.wikipedia.org"||host=="zh.wikipedia.org";}
  public static bool Trusted(Uri uri){return uri!=null&&uri.Scheme=="https"&&uri.Port==443&&uri.UserInfo.Length==0&&
   (uri.Host=="ssd-api.jpl.nasa.gov"||uri.Host=="www.wikidata.org"||uri.Host=="query.wikidata.org"||WikipediaHost(uri.Host)||uri.Host=="commons.wikimedia.org"||uri.Host=="upload.wikimedia.org"||uri.Host=="thumb.wikimedia.org");}
  public static object Json(Uri uri,CancellationToken token){
   ConnectivityPolicy.RequireInternet();token.ThrowIfCancellationRequested();if(!Trusted(uri))throw new InvalidDataException("Unapproved information host.");var bytes=Fetch(uri,1024*1024,token);
   if(bytes==null||bytes.Length>1024*1024)throw new InvalidDataException("Information response is too large.");
   return new JavaScriptSerializer{MaxJsonLength=1024*1024,RecursionLimit=24}.DeserializeObject(new UTF8Encoding(false,true).GetString(bytes));
  }
  public static DateTime RetryAfter(string header,DateTime now){
   int seconds;DateTime date;if(int.TryParse(header,out seconds)&&seconds>=0)return now.AddSeconds(Math.Max(60,Math.Min(86400,seconds)));
   if(DateTime.TryParse(header,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out date))return date>now.AddDays(1)?now.AddDays(1):date<now.AddMinutes(1)?now.AddMinutes(1):date;
   return now.AddMinutes(15);
  }
  static byte[] Download(Uri uri,int limit,CancellationToken token){transport.Wait(token);try{return DownloadCore(uri,limit,token);}finally{transport.Release();}}
  static byte[] DownloadCore(Uri uri,int limit,CancellationToken token){
   if(limit<1||limit>2*1024*1024)throw new ArgumentException("Invalid information download limit.");ConnectivityPolicy.RequireInternet();ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var clock=System.Diagnostics.Stopwatch.StartNew();for(int hop=0;hop<4;hop++){
    token.ThrowIfCancellationRequested();ConnectivityPolicy.RequireInternet();if(!Trusted(uri))throw new InvalidDataException("Unapproved information destination.");
    // Reject private/local DNS answers as well as unapproved hosts. This gate is
    // deliberately separate from LAN telescope and Stellarium connections.
    foreach(var address in Dns.GetHostAddresses(uri.DnsSafeHost))if(Private(address))throw new InvalidDataException("Private information destination.");
    var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=10000;request.ReadWriteTimeout=10000;request.UserAgent=UserAgent;request.AutomaticDecompression=DecompressionMethods.None;
    try{using(token.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse()){
     int status=(int)response.StatusCode;if(status==301||status==302||status==303||status==307||status==308){string location=response.Headers["Location"];if(string.IsNullOrEmpty(location))throw new InvalidDataException("Missing information redirect.");uri=new Uri(uri,location);continue;}
     if(status!=200||response.ContentLength>limit||!string.IsNullOrEmpty(response.Headers["Content-Encoding"]))throw new InvalidDataException("Unsupported information response.");
     using(var stream=response.GetResponseStream())using(var result=new MemoryStream()){byte[] buffer=new byte[8192];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();ConnectivityPolicy.RequireInternet();if(result.Length+n>limit||clock.Elapsed.TotalSeconds>30)throw new InvalidDataException("Information download exceeded its limit.");result.Write(buffer,0,n);}return result.ToArray();}
    }}catch(WebException error){token.ThrowIfCancellationRequested();using(var response=error.Response as HttpWebResponse){if(response!=null&&((int)response.StatusCode==429||(int)response.StatusCode==503))throw new InformationRateLimit(RetryAfter(response.Headers["Retry-After"],DateTime.UtcNow));}throw;}
   }throw new InvalidDataException("Too many information redirects.");
  }
  public static bool Private(IPAddress address){
   if(IPAddress.IsLoopback(address))return true;if(address.IsIPv4MappedToIPv6)return Private(address.MapToIPv4());byte[] b=address.GetAddressBytes();
   if(b.Length==16)return address.IsIPv6LinkLocal||address.IsIPv6SiteLocal||address.IsIPv6Multicast||(b[0]&0xfe)==0xfc||address.Equals(IPAddress.IPv6Any);
   return b[0]==0||b[0]==10||b[0]==127||b[0]>=224||b[0]==169&&b[1]==254||b[0]==172&&b[1]>=16&&b[1]<=31||b[0]==192&&(b[1]==168||b[1]==0)||b[0]==100&&b[1]>=64&&b[1]<=127||b[0]==198&&(b[1]==18||b[1]==19);
  }
  // Only baseline raster formats, with bounded dimensions before WIC sees bytes.
  public static bool Raster(byte[] data){
   if(data==null||data.Length<24||data.Length>2*1024*1024)return false;int width=0,height=0;
   if(data[0]==137&&data[1]==80&&data[2]==78&&data[3]==71&&data[4]==13&&data[5]==10&&data[6]==26&&data[7]==10){
    if(Encoding.ASCII.GetString(data,12,4)!="IHDR")return false;width=Big(data,16);height=Big(data,20);
   }else if(data[0]==255&&data[1]==216){int n=2;while(n+4<data.Length){if(data[n++]!=255)return false;while(n<data.Length&&data[n]==255)n++;if(n>=data.Length)return false;int marker=data[n++];if(marker==217||marker==218)break;if(marker==1||marker>=208&&marker<=215)continue;if(n+2>data.Length)return false;int size=data[n]*256+data[n+1];if(size<2||n+size>data.Length)return false;
     if(marker==192||marker==193||marker==194){if(size<8)return false;height=data[n+3]*256+data[n+4];width=data[n+5]*256+data[n+6];break;}n+=size;}
   }
   return width>0&&height>0&&width<=2048&&height<=2048&&(long)width*height<=4*1024*1024;
  }
  static int Big(byte[] b,int n){return (b[n]<<24)|(b[n+1]<<16)|(b[n+2]<<8)|b[n+3];}
 }
}
