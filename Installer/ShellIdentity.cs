using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
namespace AstroArchive.Installation {
 // A stable shell identity joins the versioned launcher, shortcuts and WPF window.
 public static class ShellIdentity {
  public const string AppId="AriJGuest.AstroArchive";
  static readonly Guid propertyStoreId=new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
  static readonly Guid appProperties=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
  [StructLayout(LayoutKind.Sequential)]struct PropertyKey{public Guid Format;public uint Id;public PropertyKey(uint id){Format=appProperties;Id=id;}}
  [StructLayout(LayoutKind.Sequential)]struct Variant{public ushort Type,Reserved1,Reserved2,Reserved3;public IntPtr Value,Padding;}
  [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IPropertyStore {
   [PreserveSig]int GetCount(out uint count);
   [PreserveSig]int GetAt(uint index,out PropertyKey key);
   [PreserveSig]int GetValue(ref PropertyKey key,out Variant value);
   [PreserveSig]int SetValue(ref PropertyKey key,ref Variant value);
   [PreserveSig]int Commit();
  }
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SetCurrentProcessExplicitAppUserModelID(string id);
  [DllImport("shell32.dll")]static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr id);
  [DllImport("shell32.dll")]static extern int SHGetPropertyStoreForWindow(IntPtr window,ref Guid iid,out IPropertyStore store);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHGetPropertyStoreFromParsingName(string path,IntPtr binding,uint flags,ref Guid iid,out IPropertyStore store);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern void SHChangeNotify(uint change,uint flags,string path,IntPtr other);
  [DllImport("ole32.dll")]static extern int PropVariantClear(ref Variant value);
  static void Set(IPropertyStore store,uint id,string text){var key=new PropertyKey(id);var value=new Variant{Type=31,Value=Marshal.StringToCoTaskMemUni(text)};try{Marshal.ThrowExceptionForHR(store.SetValue(ref key,ref value));}finally{PropVariantClear(ref value);}}
  static string Read(IPropertyStore store,uint id){var key=new PropertyKey(id);Variant value;Marshal.ThrowExceptionForHR(store.GetValue(ref key,out value));try{return value.Type==31?Marshal.PtrToStringUni(value.Value):null;}finally{PropVariantClear(ref value);}}
  public static void InitializeProcess(){Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppId));}
  public static string ProcessId(){IntPtr value;Marshal.ThrowExceptionForHR(GetCurrentProcessExplicitAppUserModelID(out value));try{return Marshal.PtrToStringUni(value);}finally{Marshal.FreeCoTaskMem(value);}}
  public static string IconPath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstroArchive","Shell","AstroArchive.ico");}}
  public static string EnsureIcon(){
   string path=IconPath;byte[] bytes;
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AstroArchive.ico")){
    if(stream==null)throw new InvalidDataException("The bundled shell icon is missing.");using(var output=new MemoryStream()){stream.CopyTo(output);bytes=output.ToArray();}
   }
   if(File.Exists(path)&&InstallCore.HashFile(path)==InstallCore.Hash(bytes))return path;
   Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllBytes(temporary,bytes);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}
   Notify(path);return path;
  }
  public static void ConfigureWindow(IntPtr window,string executable){
   var iid=propertyStoreId;IPropertyStore store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window,ref iid,out store));
   try{Set(store,5,AppId);Set(store,2,WindowsIntegration.Quote(executable));Set(store,3,EnsureIcon()+",0");Set(store,4,"AstroArchive");}finally{Marshal.FinalReleaseComObject(store);}
  }
  public static string WindowProperty(IntPtr window,uint id){var iid=propertyStoreId;IPropertyStore store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window,ref iid,out store));try{return Read(store,id);}finally{Marshal.FinalReleaseComObject(store);}}
  public static void ConfigureShortcut(string path){var iid=propertyStoreId;IPropertyStore store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreFromParsingName(path,IntPtr.Zero,2,ref iid,out store));try{Set(store,5,AppId);Marshal.ThrowExceptionForHR(store.Commit());}finally{Marshal.FinalReleaseComObject(store);}Notify(path);}
  public static string ShortcutId(string path){var iid=propertyStoreId;IPropertyStore store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreFromParsingName(path,IntPtr.Zero,0,ref iid,out store));try{return Read(store,5);}finally{Marshal.FinalReleaseComObject(store);}}
  public static void Notify(string path){SHChangeNotify(0x2000,5,path,IntPtr.Zero);}
 }
}
