using System;
using System.IO;
using System.Text;
using Microsoft.Win32;
namespace AstroArchive.Remote {
 public static class StartupDiagnostics {
  public static string ReportPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive.RemoteTester","startup-error.txt");}}
  public static string Details(Exception error){string release="unknown";try{using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))if(key!=null)release=Convert.ToString(key.GetValue("Release","unknown"));}catch{}return "AstroArchive Remote tester 0.1.1"+Environment.NewLine+"Time: "+DateTimeOffset.Now.ToString("o")+Environment.NewLine+"Windows: "+Environment.OSVersion+Environment.NewLine+"Process: "+(Environment.Is64BitProcess?"64-bit":"32-bit")+Environment.NewLine+"CLR: "+Environment.Version+Environment.NewLine+".NET Framework release: "+release+Environment.NewLine+Environment.NewLine+error;}
  public static string Save(Exception error){string details=Details(error);foreach(string path in new[]{ReportPath,Path.Combine(Path.GetTempPath(),"AstroArchive.RemoteTester-startup-error.txt")})try{Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,details,new UTF8Encoding(false));return path;}catch{}return null;}
 }
}
