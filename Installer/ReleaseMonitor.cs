// Shared by the launcher and the WPF app; network work always runs off the UI thread.
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AstroArchive.Installation {
 public sealed class ReleaseCheckState {
  public int Schema=1,Failures;
  public string ComparisonVersion,ComparisonPackage,CheckedUtc,NextCheckUtc,Error,NotifiedPackage;
  public bool AutomaticDisabled;
  public UpdateManifest Available;
 }
 public sealed class ReleaseMonitor {
  readonly object gate=new object();readonly string path;readonly InstallRecord comparison;
  readonly Func<UpdateManifest> check;readonly Func<DateTime> utc;
  Task<UpdateManifest> pending;
  public ReleaseCheckState State {get;private set;}
  public bool Busy {get{lock(gate)return pending!=null;}}
  public ReleaseMonitor(string root,InstallRecord comparison,Func<UpdateManifest> check,Func<DateTime> utc=null,string cachePath=null){
   this.comparison=comparison;this.check=check;this.utc=utc??(()=>DateTime.UtcNow);
   path=cachePath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","updates","release-"+InstallCore.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToLowerInvariant()))+".json");
   State=Read();
  }
  ReleaseCheckState Read(){
   try{InstallCore.NoLinks(path);if(!File.Exists(path)||new FileInfo(path).Length>128*1024)return NewState();var state=InstallCore.Json().Deserialize<ReleaseCheckState>(File.ReadAllText(path));if(state==null||state.Schema!=1)return NewState();
    if(state.ComparisonVersion!=comparison.Version||state.ComparisonPackage!=(comparison.PackageVersion??comparison.Version)){var replacement=NewState();replacement.AutomaticDisabled=state.AutomaticDisabled;return replacement;}
    if(state.Available!=null){UpdateClient.Validate(state.Available);if(InstallCore.Parse(state.Available.package_version)<=InstallCore.Parse(comparison.PackageVersion??comparison.Version)||InstallCore.Parse(state.Available.application_version)<InstallCore.Parse(comparison.Version))state.Available=null;}
    DateTime checkedUtc;if(state.CheckedUtc!=null&&(!TryUtc(state.CheckedUtc,out checkedUtc)||checkedUtc>this.utc().AddMinutes(5)))return NewState();return state;
   }catch{return NewState();}
  }
  ReleaseCheckState NewState(){return new ReleaseCheckState{ComparisonVersion=comparison.Version,ComparisonPackage=comparison.PackageVersion??comparison.Version};}
  static bool TryUtc(string value,out DateTime date){return DateTime.TryParse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out date)&&date.Kind==DateTimeKind.Utc;}
  public bool Due {get{lock(gate){DateTime next;return pending==null&&!State.AutomaticDisabled&&(!TryUtc(State.NextCheckUtc,out next)||next<=utc()||next>utc().AddHours(6.1));}}}
  void Save(){
   string temporary=null;try{InstallCore.NoLinks(path);Directory.CreateDirectory(Path.GetDirectoryName(path));temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temporary,InstallCore.Json().Serialize(State),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}catch{/* Caching failure never prevents checking or opening the app. */}finally{if(temporary!=null&&File.Exists(temporary))try{File.Delete(temporary);}catch{}}
  }
  public void SetAutomatic(bool enabled){lock(gate){State.AutomaticDisabled=!enabled;Save();}}
  public bool MarkNotified(){lock(gate){if(State.Available==null||State.NotifiedPackage==State.Available.package_version)return false;State.NotifiedPackage=State.Available.package_version;Save();return true;}}
  public Task<UpdateManifest> CheckAsync(){
   lock(gate){if(pending!=null)return pending;var completion=new TaskCompletionSource<UpdateManifest>();pending=completion.Task;
    Task.Run(()=>{UpdateManifest release=null;Exception failure=null;try{release=check();if(release!=null){UpdateClient.Validate(release);if(InstallCore.Parse(release.package_version)<=InstallCore.Parse(comparison.PackageVersion??comparison.Version)||InstallCore.Parse(release.application_version)<InstallCore.Parse(comparison.Version))release=null;}}catch(Exception error){failure=error;}
     lock(gate){var now=utc();if(failure==null){State.Available=release;State.CheckedUtc=now.ToString("o");State.NextCheckUtc=now.AddHours(6).ToString("o");State.Failures=0;State.Error=null;}else{State.Failures=Math.Min(3,State.Failures+1);State.Error=failure.Message.Substring(0,Math.Min(2000,failure.Message.Length));State.NextCheckUtc=now.AddMinutes(State.Failures==1?15:State.Failures==2?60:360).ToString("o");}Save();pending=null;}
     if(failure==null)completion.SetResult(release);else completion.SetException(failure);
    });return completion.Task;
   }
  }
 }
}
