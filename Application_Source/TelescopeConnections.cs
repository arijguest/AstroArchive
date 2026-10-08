// Saved physical telescopes and local USB sources. C# 5 / .NET Framework 4.8.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
namespace AstroArchive {
 public class TelescopeProfile {
  public string Id{get;set;} public string SessionIdentity{get;set;} public string Model{get;set;} public string Camera{get;set;}
  public string LastSource{get;set;} public string VolumeId{get;set;} public string SourceRelativePath{get;set;}
  public string DisplayText{get{return string.IsNullOrEmpty(Id)?"New telescope...":Id+(Model=="Auto"||string.IsNullOrEmpty(Model)?"":" · "+Model);}}
 }
 public static class TelescopeProfiles {
  public static readonly string[] Models={"Auto","Seestar S50 Pro","Seestar S50","Seestar S30 Pro","Seestar S30","Dwarf 3","Dwarf II","Dwarf mini","Other"};
  public static string Model(string value){return Models.Contains(value)?value:"Auto";}
  public static void Initialize(Settings settings){
   if(settings.Telescopes==null)settings.Telescopes=new List<TelescopeProfile>();
   settings.Telescopes=settings.Telescopes.Where(p=>p!=null&&!string.IsNullOrWhiteSpace(p.Id)).GroupBy(p=>p.Id.Trim(),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
   foreach(var p in settings.Telescopes){p.Id=p.Id.Trim();if(string.IsNullOrEmpty(p.SessionIdentity))p.SessionIdentity=p.Id;p.Model=Model(p.Model);p.Camera=p.Camera=="Telephoto"||p.Camera=="Wide"?p.Camera:"Auto";}
   if(settings.Telescopes.Count==0&&!string.IsNullOrWhiteSpace(settings.Telescope)&&!string.IsNullOrEmpty(settings.LastSource))settings.Telescopes.Add(new TelescopeProfile{Id=settings.Telescope.Trim(),SessionIdentity=settings.Telescope.Trim(),Model=Model(settings.Model),Camera="Auto",LastSource=settings.LastSource});
  }
  public static TelescopeProfile Find(Settings settings,string id){return settings.Telescopes.FirstOrDefault(p=>string.Equals(p.Id,id,StringComparison.OrdinalIgnoreCase));}
  public static int MergeRepository(Settings settings,IEnumerable<Frame> frames){
   Initialize(settings);int added=0;
   foreach(var group in frames.Where(f=>!string.IsNullOrWhiteSpace(f.Telescope)&&!string.Equals(f.Telescope.Trim(),"Unknown",StringComparison.OrdinalIgnoreCase)).GroupBy(f=>f.Telescope.Trim(),StringComparer.OrdinalIgnoreCase)){
    if(Find(settings,group.Key)!=null)continue;
    var models=group.Select(f=>Model(f.Model)).Where(m=>m!="Auto").Distinct().ToList();var makes=group.Select(f=>f.MakeText).Where(m=>m=="Seestar"||m=="DWARFLAB").Distinct().ToList();
    var identities=group.Select(f=>f.TelescopeIdentity??f.Telescope).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    settings.Telescopes.Add(new TelescopeProfile{Id=group.Key,SessionIdentity=identities.Count==1?identities[0]:group.Key,Model=models.Count==1&&makes.Count<=1?models[0]:"Auto",Camera="Auto"});added++;
   }
   return added;
  }
  public static void Rename(Settings settings,string oldName,string newName){
   Initialize(settings);newName=(newName??"").Trim();if(newName.Length==0)throw new ArgumentException("Enter a telescope name.");var profile=Find(settings,oldName);if(profile==null)throw new ArgumentException("Select a saved telescope first.");var existing=Find(settings,newName);if(existing!=null&&existing!=profile)throw new ArgumentException("That telescope name is already saved.");
   profile.Id=newName;if(string.Equals(settings.SelectedTelescope,oldName,StringComparison.OrdinalIgnoreCase))settings.SelectedTelescope=newName;if(string.Equals(settings.Telescope,oldName,StringComparison.OrdinalIgnoreCase))settings.Telescope=newName;
  }
  public static TelescopeProfile Save(Settings settings,string id,string model,string camera,string source,IEnumerable<UsbVolume> volumes){
   Initialize(settings);id=(id??"").Trim();if(id.Length==0)throw new ArgumentException("Give this physical telescope a unique ID.");
   var previous=Find(settings,id);var profile=previous==null?new TelescopeProfile{Id=id,SessionIdentity=id}:Util.Deserialize<TelescopeProfile>(Util.Serialize(previous));profile.Model=Model(model);profile.Camera=camera=="Telephoto"||camera=="Wide"?camera:"Auto";
   if(!string.IsNullOrWhiteSpace(source)){
    source=Path.GetFullPath(source.Trim());var volume=volumes.FirstOrDefault(v=>!string.IsNullOrEmpty(v.Id)&&Util.Within(source,v.Root));
    if(volume!=null){profile.VolumeId=volume.Id;profile.SourceRelativePath=source.Substring(Path.GetFullPath(volume.Root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}
    else if(!string.Equals(source,profile.LastSource,StringComparison.OrdinalIgnoreCase)){profile.VolumeId=null;profile.SourceRelativePath=null;}
    profile.LastSource=source;
   }
   if(previous!=null)settings.Telescopes.Remove(previous);settings.Telescopes.Add(profile);settings.SelectedTelescope=profile.Id;return profile;
  }
 }
 public class UsbVolume {public string Root;public string Id;}
 public class UsbTelescope {public UsbVolume Volume;public string Source;public string Make;public string ProfileId;}
 public static class UsbStorage {
  public static List<UsbVolume> Volumes(){
   var result=new List<UsbVolume>();if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return result;
   foreach(var drive in DriveInfo.GetDrives())try{
    if(drive.DriveType!=DriveType.Removable&&drive.DriveType!=DriveType.Fixed)continue;if(!drive.IsReady)continue;if(!IsUsb(drive.Name)&&drive.DriveType!=DriveType.Removable)continue;
    string id=Identity(drive.Name);if(id!=null)result.Add(new UsbVolume{Root=drive.Name,Id=id});
   }catch(IOException){}catch(UnauthorizedAccessException){}
   return result;
  }
  public static string Identity(string root){if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return null;var name=new StringBuilder(128);return GetVolumeNameForVolumeMountPoint(root.TrimEnd('\\')+"\\",name,name.Capacity)?name.ToString():null;}
  static bool IsUsb(string root){using(var handle=CreateFile(@"\\.\"+root.TrimEnd('\\'),0,3,IntPtr.Zero,3,0,IntPtr.Zero)){if(handle.IsInvalid)return false;byte[] query=new byte[12],descriptor=new byte[1024];int returned;return DeviceIoControl(handle,0x002D1400,query,query.Length,descriptor,descriptor.Length,out returned,IntPtr.Zero)&&returned>=32&&BitConverter.ToInt32(descriptor,28)==7;}}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumeNameForVolumeMountPoint(string root,StringBuilder volume,int length);
  [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(SafeFileHandle handle,uint code,byte[] input,int inputLength,byte[] output,int outputLength,out int returned,IntPtr overlapped);
 }
 public static class UsbTelescopeDiscovery {
  public static TelescopeProfile MatchProfile(UsbTelescope device,IEnumerable<TelescopeProfile> profiles,string preferred=null){
   var saved=profiles.Where(p=>!string.IsNullOrEmpty(p.Id)).ToList();var bound=saved.FirstOrDefault(p=>string.Equals(p.Id,device.ProfileId,StringComparison.OrdinalIgnoreCase));if(bound!=null)return bound;
   var candidates=saved.Where(p=>InstrumentDetection.MakeOf(p.Model)==device.Make).ToList();
   var selected=candidates.FirstOrDefault(p=>string.Equals(p.Id,preferred,StringComparison.OrdinalIgnoreCase));return selected??(candidates.Count==1?candidates[0]:null);
  }
  static bool Allowed(string source,string archive){return string.IsNullOrEmpty(archive)||(!Util.Within(source,archive)&&!Util.Within(archive,source));}
  static bool Readable(string path){try{if(!Directory.Exists(path)||!FileStamp.CanTraverse(new DirectoryInfo(path)))return false;using(var entries=Directory.EnumerateFileSystemEntries(path).GetEnumerator())entries.MoveNext();return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
  public static string Resolve(UsbVolume volume,TelescopeProfile profile){
   if(!string.Equals(volume.Id,profile.VolumeId,StringComparison.OrdinalIgnoreCase)||string.IsNullOrEmpty(volume.Id))return null;
   try{string relative=profile.SourceRelativePath??"";if(Path.IsPathRooted(relative))return null;string path=Path.GetFullPath(Path.Combine(volume.Root,relative));return Util.Within(path,volume.Root)?path:null;}catch(ArgumentException){return null;}catch(NotSupportedException){return null;}
  }
  public static List<UsbTelescope> Discover(IEnumerable<UsbVolume> volumes,IEnumerable<TelescopeProfile> profiles,string archive,CancellationToken ct){
   var result=new List<UsbTelescope>();foreach(var volume in volumes){ct.ThrowIfCancellationRequested();if(!Readable(volume.Root))continue;bool bound=false;
    foreach(var profile in profiles){string path=Resolve(volume,profile);if(path==null||!Allowed(path,archive)||!Readable(path))continue;result.Add(new UsbTelescope{Volume=volume,Source=path,ProfileId=profile.Id,Make=InstrumentDetection.MakeOf(profile.Model)});bound=true;}
    if(bound||!Allowed(volume.Root,archive))continue;string make=DetectLayout(volume.Root,ct);if(make=="Seestar"||make=="DWARFLAB")result.Add(new UsbTelescope{Volume=volume,Source=volume.Root,Make=make});
   }return result;
  }
  public static string DetectLayout(string root,CancellationToken ct){
   var makes=new HashSet<string>();var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(root,0));int inspected=0;
   while(queue.Count>0&&inspected++<256){ct.ThrowIfCancellationRequested();var item=queue.Dequeue();try{
    string relative=Path.GetFullPath(item.Item1).Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Length).Replace('\\','/');string make=InstrumentDetection.LayoutMake(relative,false);if(make!="Unknown")makes.Add(make);if(!FileStamp.CanTraverse(new DirectoryInfo(item.Item1)))continue;
    int entries=0;foreach(var entry in new DirectoryInfo(item.Item1).EnumerateFileSystemInfos()){
     ct.ThrowIfCancellationRequested();if(++entries>512)break;
     if((entry.Attributes&FileAttributes.Directory)!=0){if(item.Item2<3&&entry.Name!=".astroarchive"&&entry.Name!="System Volume Information"&&entry.Name!="$RECYCLE.BIN"&&FileStamp.CanTraverse((DirectoryInfo)entry))queue.Enqueue(Tuple.Create(entry.FullName,item.Item2+1));}
     else if(entry.Name.Equals("shotsInfo.json",StringComparison.OrdinalIgnoreCase))makes.Add("DWARFLAB");
     else if(Util.IsFits(entry.Name)&&entry.Name.StartsWith("Light_",StringComparison.OrdinalIgnoreCase)&&(item.Item1.EndsWith("_sub",StringComparison.OrdinalIgnoreCase)||item.Item1.EndsWith("-sub",StringComparison.OrdinalIgnoreCase)))makes.Add("Seestar");
    }
   }catch(IOException){}catch(UnauthorizedAccessException){}}
   return makes.Count==1?makes.First():"Unknown";
  }
 }
}
