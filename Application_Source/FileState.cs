// Windows 10/11 file identity. Unsupported/cloud filesystems fall back to content reads.
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace AstroArchive {
 public class FileStamp {
  static readonly ConcurrentDictionary<string,string> formats=new ConcurrentDictionary<string,string>();
  public string Identity{get;set;}public long Size{get;set;}public long Modified{get;set;}public long Created{get;set;}public long Changed{get;set;}public int Attributes{get;set;}public bool Reliable{get;set;}
  public bool Cloud{get{return (Attributes&(0x1000|0x40000|0x400000))!=0;}}
  public FileStamp Clone(){return (FileStamp)MemberwiseClone();}
  public bool Same(FileStamp other){return other!=null&&Size==other.Size&&Modified==other.Modified&&Created==other.Created&&Identity==other.Identity&&Changed==other.Changed;}
  public bool ContentSame(FileStamp other){return other!=null&&Size==other.Size&&Modified==other.Modified&&Created==other.Created&&(string.IsNullOrEmpty(Identity)||string.IsNullOrEmpty(other.Identity)||Identity==other.Identity)&&(!Reliable||!other.Reliable||Changed==other.Changed);}
  public bool VerifiedUnchanged(FileStamp other){return Reliable&&other!=null&&other.Reliable&&!Cloud&&!other.Cloud&&!string.IsNullOrEmpty(Identity)&&Same(other);}
  public static FileStamp Read(string path){return Read(new FileInfo(path));}
  public static FileStamp Read(FileInfo file,FileStamp enumerated=null){var stamp=enumerated==null?new FileStamp{Identity="",Size=file.Length,Modified=file.LastWriteTimeUtc.Ticks,Created=file.CreationTimeUtc.Ticks,Attributes=(int)file.Attributes}:enumerated.Clone();if(Environment.OSVersion.Platform!=PlatformID.Win32NT||stamp.Cloud)return stamp;
   try{using(var h=CreateFile(LongPath(file.FullName),0,7,IntPtr.Zero,3,0,IntPtr.Zero)){IdentityInfo id;BasicInfo basic;if(h.IsInvalid||!GetFileInformationByHandle(h,out id)||!GetFileInformationByHandleEx(h,0,out basic,(uint)Marshal.SizeOf(typeof(BasicInfo))))return stamp;stamp.Identity=id.VolumeSerial.ToString("X8")+":"+id.IndexHigh.ToString("X8")+id.IndexLow.ToString("X8");stamp.Changed=basic.Changed;string volume=Path.GetPathRoot(file.FullName);string format=formats.GetOrAdd(volume+":"+id.VolumeSerial,k=>new DriveInfo(volume).DriveFormat);stamp.Reliable=(format=="NTFS"||format=="ReFS")&&stamp.Changed>0;}}catch{}return stamp;
  }
  // Directory handle identifies the physical filesystem even if its drive letter changes.
  public static string VolumeIdentity(string path){if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return null;try{using(var h=CreateFile(LongPath(path),0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero)){IdentityInfo id;return !h.IsInvalid&&GetFileInformationByHandle(h,out id)&&id.VolumeSerial!=0?id.VolumeSerial.ToString("X8"):null;}}catch{return null;}}
  public static bool IsRedirectTag(uint tag){return (tag&0x20000000)!=0;}
  public static bool CanTraverse(DirectoryInfo folder){return CanAccess(folder);}
  public static bool CanAccess(FileSystemInfo entry){if((entry.Attributes&FileAttributes.ReparsePoint)==0)return true;if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return false;try{using(var h=CreateFile(LongPath(entry.FullName),0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)){TagInfo info;return !h.IsInvalid&&GetTagInfo(h,9,out info,8)&&info.Tag!=0&&!IsRedirectTag(info.Tag);}}catch{return false;}}
  [StructLayout(LayoutKind.Sequential)]struct TagInfo{public uint Attributes,Tag;}
  [DllImport("kernel32.dll",EntryPoint="GetFileInformationByHandleEx",SetLastError=true)]static extern bool GetTagInfo(SafeFileHandle handle,int kind,out TagInfo info,uint size);
  public static string LongPath(string path){path=Path.GetFullPath(path);if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return path;return path.StartsWith(@"\\?\")?path:path.StartsWith(@"\\")?@"\\?\UNC\"+path.Substring(2):@"\\?\"+path;}
  [StructLayout(LayoutKind.Sequential)]struct IdentityInfo{public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;public uint VolumeSerial,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
  [StructLayout(LayoutKind.Sequential)]struct BasicInfo{public long Created,Accessed,Written,Changed;public uint Attributes;}
  [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle h,out IdentityInfo info);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandleEx(SafeFileHandle h,int kind,out BasicInfo info,uint size);
 }
 public class SourceManifest {public string Root;public string Path;public string Hash;public string Destination;public string Status;public FileStamp Source;public FileStamp Copy;public Frame Metadata;}
}
