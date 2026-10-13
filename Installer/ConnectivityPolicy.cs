// Shared per-user policy; installation preferences never replace settings.json.
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace AstroArchive.Installation {
 public enum ConnectivityMode { Pending=0, Hybrid=1, Offline=2 }
 public sealed class ConnectivityChoice {public int Schema=1;public ConnectivityMode Mode;}
 public static class ConnectivityPolicy {
  public static string ChoicePath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","connectivity.json");}}
  static readonly object gate=new object();static ConnectivityMode mode=ConnectivityMode.Pending;
  static CancellationTokenSource online=new CancellationTokenSource();
  public static ConnectivityMode Mode {get{lock(gate)return mode;}}
  public static bool AllowsInternet {get{return Mode==ConnectivityMode.Hybrid;}}
  public static CancellationToken Token {get{lock(gate)return online.Token;}}
  public static ConnectivityMode Read(string path=null){
   try{path=path??ChoicePath;InstallCore.NoLinks(path);if(!File.Exists(path)||new FileInfo(path).Length>4096)return ConnectivityMode.Pending;
    var choice=new JavaScriptSerializer{MaxJsonLength=4096,RecursionLimit=4}.Deserialize<ConnectivityChoice>(File.ReadAllText(path));
    return choice!=null&&choice.Schema==1&&(choice.Mode==ConnectivityMode.Hybrid||choice.Mode==ConnectivityMode.Offline)?choice.Mode:ConnectivityMode.Pending;
   }catch{return ConnectivityMode.Pending;}
  }
  public static void Save(ConnectivityMode selected,string path=null){
   if(selected!=ConnectivityMode.Hybrid&&selected!=ConnectivityMode.Offline)throw new ArgumentException("Choose Hybrid or Offline.");
   path=path??ChoicePath;InstallCore.NoLinks(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(new ConnectivityChoice{Mode=selected}),new UTF8Encoding(false));
    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public static void Apply(ConnectivityMode selected){
   CancellationTokenSource old=null;lock(gate){if(!Enum.IsDefined(typeof(ConnectivityMode),selected))selected=ConnectivityMode.Pending;if(mode==selected)return;
    mode=selected;old=online;online=new CancellationTokenSource();if(mode!=ConnectivityMode.Hybrid)online.Cancel();}
   // Abort callbacks must run outside the lock. Tokens can remain held by retiring tasks.
   old.Cancel();
  }
  public static void RequireInternet(){if(!AllowsInternet)throw new InvalidOperationException("Online access is paused. Choose Hybrid in Preferences > General > Online access to reconnect.");}
 }
}
