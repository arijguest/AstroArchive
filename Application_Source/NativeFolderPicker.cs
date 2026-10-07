// Windows 10/11, .NET Framework 4.8 and C# 5. Uses the Windows Common Item Dialog.
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AstroArchive {
 internal static class NativeFolderPicker {
  const uint PickFolders=0x20,ForceFileSystem=0x40,PathMustExist=0x800,NoChangeDirectory=0x8;
  const uint HidePinnedPlaces=0x40000,HideRecentPlaces=0x20000;
  const uint PlaceTop=1,PlaceBottom=0;
  const uint FileSystemPath=0x80058000;
  const int Canceled=unchecked((int)0x800704C7);

  internal static string Select(IntPtr owner,string title,string current) {
   IFileDialog dialog=null;IShellItem initial=null,result=null;IntPtr path=IntPtr.Zero;
   try {
    dialog=(IFileDialog)new FileOpenDialog();
    uint options;dialog.GetOptions(out options);
    // Let Explorer cloud namespaces be browsed; validate the returned Windows path below.
    options&=~(ForceFileSystem|HidePinnedPlaces|HideRecentPlaces);
    dialog.SetOptions(options|PickFolders|PathMustExist|NoChangeDirectory);
    dialog.SetTitle(title);dialog.SetOkButtonLabel("Select folder");
    AddNavigation(dialog);
    string folder=ExistingFolder(current);
    if(folder!=null) {
     Guid itemId=typeof(IShellItem).GUID;
     int hr=SHCreateItemFromParsingName(folder,IntPtr.Zero,ref itemId,out initial);
     // A disconnected drive or deleted folder should not prevent browsing elsewhere.
     if(hr>=0&&initial!=null)try{dialog.SetFolder(initial);}catch(COMException){}
    }
    int status=dialog.Show(owner);
    if(status==Canceled)return null;
    Marshal.ThrowExceptionForHR(status);
    dialog.GetResult(out result);
    try{result.GetDisplayName(FileSystemPath,out path);}
    catch(COMException e){throw new IOException("Choose a folder inside the mounted Drive location. This Explorer shortcut has no Windows folder path. You can copy the folder's full path from Explorer and paste it into the selector's address bar (Alt+D).",e);}
    string selected=Marshal.PtrToStringUni(path);
    if(string.IsNullOrWhiteSpace(selected)||!Path.IsPathRooted(selected))throw new IOException("This location has no Windows folder path. Choose a folder inside a mounted drive.");
    return selected;
   } finally {
    if(path!=IntPtr.Zero)Marshal.FreeCoTaskMem(path);
    Release(result);Release(initial);Release(dialog);
   }
  }

  static void AddNavigation(IFileDialog dialog) {
   IShellItem computer=ShellItem("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}");
   try {
    if(computer!=null) {
     try{dialog.AddPlace(computer,PlaceTop);dialog.SetDefaultFolder(computer);}catch(COMException){}
    }
   }finally{Release(computer);}
   // Enumerate drive letters without probing media or assuming Google Drive uses G:.
   string[] drives;
   try{drives=Environment.GetLogicalDrives();}catch(IOException){return;}
   catch(System.Security.SecurityException){return;}
   Array.Sort(drives,StringComparer.OrdinalIgnoreCase);
   foreach(string drive in drives) {
    IShellItem item=ShellItem(drive);
    try{if(item!=null)try{dialog.AddPlace(item,PlaceBottom);}catch(COMException){}}
    finally{Release(item);}
   }
  }
  static IShellItem ShellItem(string path) {
   IShellItem item;Guid id=typeof(IShellItem).GUID;
   int status=SHCreateItemFromParsingName(path,IntPtr.Zero,ref id,out item);
   if(status>=0)return item;
   Release(item);return null;
  }

  static string ExistingFolder(string value) {
   if(string.IsNullOrWhiteSpace(value))return null;
   try {
    string path=Path.GetFullPath(value.Trim().Trim('"'));
    while(!Directory.Exists(path)) {
     string parent=Path.GetDirectoryName(path);
     if(string.IsNullOrEmpty(parent)||parent==path)return null;
     path=parent;
    }
    return path;
   } catch(ArgumentException){return null;}catch(NotSupportedException){return null;}
     catch(PathTooLongException){return null;}catch(System.Security.SecurityException){return null;}
  }
  static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}

  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]
  static extern int SHCreateItemFromParsingName(string path,IntPtr binding,ref Guid interfaceId,out IShellItem item);

  [ComImport,Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
  class FileOpenDialog {}

  // Method order matches the native IModalWindow/IFileDialog vtable.
  [ComImport,Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IFileDialog {
   [PreserveSig]int Show(IntPtr owner);
   void SetFileTypes(uint count,IntPtr types);
   void SetFileTypeIndex(uint index);
   void GetFileTypeIndex(out uint index);
   void Advise(IntPtr events,out uint cookie);
   void Unadvise(uint cookie);
   void SetOptions(uint options);
   void GetOptions(out uint options);
   void SetDefaultFolder(IShellItem item);
   void SetFolder(IShellItem item);
   void GetFolder(out IShellItem item);
   void GetCurrentSelection(out IShellItem item);
   void SetFileName([MarshalAs(UnmanagedType.LPWStr)]string name);
   void GetFileName(out IntPtr name);
   void SetTitle([MarshalAs(UnmanagedType.LPWStr)]string title);
   void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)]string text);
   void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)]string text);
   void GetResult(out IShellItem item);
   void AddPlace(IShellItem item,uint alignment);
   void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)]string extension);
   void Close(int result);
   void SetClientGuid(ref Guid id);
   void ClearClientData();
   void SetFilter(IntPtr filter);
  }

  [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IShellItem {
   void BindToHandler(IntPtr binding,ref Guid handlerId,ref Guid interfaceId,out IntPtr result);
   void GetParent(out IShellItem parent);
   void GetDisplayName(uint name,out IntPtr value);
   void GetAttributes(uint mask,out uint attributes);
   void Compare(IShellItem other,uint hint,out int order);
  }
 }
}
