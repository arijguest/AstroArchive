// Windows 10/11, .NET Framework 4.8. Delete the verified open file, never a raced pathname.
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace AstroArchive {
 public static class SourceCleanup {
  const uint ReadAndDelete=0x80010000,ShareRead=1,OpenExisting=3,BackupSemantics=0x02000000;
  public static void ValidateRoots(string source,string repository) {
   if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("A source folder is required when deleting originals.");
   if(Util.Within(source,repository)||Util.Within(repository,source))throw new IOException("Deletion requires separate source and repository folders.");
  }
  public static void DeleteVerified(string source,string destination,string expected,string sourceRoot,string repository,CancellationToken ct) {
   ValidateRoots(sourceRoot,repository);
   if(!Util.Within(source,sourceRoot)||Util.Within(source,repository)||!Util.Within(destination,repository))throw new IOException("Source cleanup refused a path outside the import boundaries.");
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new PlatformNotSupportedException("Verified source removal requires Windows file handles.");
   ct.ThrowIfCancellationRequested();
   // ShareRead prevents concurrent writers, renames and deletes until this handle closes.
   using(var handle=CreateFile(LongPath(source),ReadAndDelete,ShareRead,IntPtr.Zero,OpenExisting,0,IntPtr.Zero)) {
    if(handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"Original was kept because it could not be locked for removal.");
    using(var original=new FileStream(handle,FileAccess.Read,1048576,false))
    using(var copy=new FileStream(destination,FileMode.Open,FileAccess.Read,FileShare.Read,1048576)) {
     string realSource=FinalPath(handle),realMirror=DirectoryPath(sourceRoot),realRepository=DirectoryPath(repository),realCopy=FinalPath(copy.SafeFileHandle);
     if(!Util.Within(realSource,realMirror)||Util.Within(realSource,realRepository)||!Util.Within(realCopy,realRepository)||Util.Within(realMirror,realRepository)||Util.Within(realRepository,realMirror))throw new IOException("Original was kept: resolved source/repository locations overlap or leave the selected folders.");
     FileIdentity a,b;
     if(!GetFileInformationByHandle(handle,out a)||!GetFileInformationByHandle(copy.SafeFileHandle,out b))throw new Win32Exception(Marshal.GetLastWin32Error(),"Original was kept because file identity could not be verified.");
     if(a.VolumeSerial==b.VolumeSerial&&a.IndexHigh==b.IndexHigh&&a.IndexLow==b.IndexLow)throw new IOException("Original was kept: source and destination have the same file identity.");
     if(Util.Hash(original,ct)!=expected||Util.Hash(copy,ct)!=expected)throw new IOException("Original was kept because the source or repository checksum changed.");
     ct.ThrowIfCancellationRequested();
     var disposition=new FileDisposition{DeleteFile=1};
     if(!SetFileInformationByHandle(handle,4,ref disposition,1))throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows could not remove this original; the repository copy is retained.");
    }
   }
  }
  static string LongPath(string path) {path=Path.GetFullPath(path);return path.StartsWith(@"\\?\")?path:path.StartsWith(@"\\")?@"\\?\UNC\"+path.Substring(2):@"\\?\"+path;}
  static string DirectoryPath(string path) {
   using(var handle=CreateFile(LongPath(path),0,7,IntPtr.Zero,OpenExisting,BackupSemantics,IntPtr.Zero)) {
    if(handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot resolve an import folder for safe source removal.");
    return FinalPath(handle);
   }
  }
  static string FinalPath(SafeFileHandle handle) {
   var text=new StringBuilder(512);uint length=GetFinalPathNameByHandle(handle,text,(uint)text.Capacity,0);
   if(length>=text.Capacity){text.Capacity=checked((int)length+1);length=GetFinalPathNameByHandle(handle,text,(uint)text.Capacity,0);}
   if(length==0||length>=text.Capacity)throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot verify the physical file path; original was kept.");
   return text.ToString();
  }
  [StructLayout(LayoutKind.Sequential)]struct FileDisposition {public byte DeleteFile;}
  [StructLayout(LayoutKind.Sequential)]struct FileIdentity {public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;public uint VolumeSerial,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
  [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileIdentity identity);
  [DllImport("kernel32.dll",EntryPoint="GetFinalPathNameByHandleW",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetFileInformationByHandle(SafeFileHandle handle,int information,ref FileDisposition disposition,uint size);
 }
}
