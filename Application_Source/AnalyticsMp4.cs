using System;
using System.Runtime.InteropServices;

namespace AstroArchive {
 // Windows' H.264 sink writer keeps video export self-contained. Native objects
 // are released per frame; no encoder executable or network access is required.
 internal sealed class AnalyticsMp4:IDisposable {
  IntPtr writer;bool started,finished;uint stream;readonly int width,height,fps;long frame;
  [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFStartup(int version,int flags);
  [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFShutdown();
  [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFCreateMediaType(out IntPtr type);
  [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFCreateSample(out IntPtr sample);
  [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFCreateMemoryBuffer(int size,out IntPtr buffer);
  [DllImport("mfreadwrite.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]static extern int MFCreateSinkWriterFromURL(string url,IntPtr byteStream,IntPtr attributes,out IntPtr sink);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int UInt32Method(IntPtr self,ref Guid key,uint value);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int UInt64Method(IntPtr self,ref Guid key,ulong value);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GuidMethod(IntPtr self,ref Guid key,ref Guid value);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int AddStreamMethod(IntPtr self,IntPtr type,out uint index);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int InputMethod(IntPtr self,uint index,IntPtr type,IntPtr attributes);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SimpleMethod(IntPtr self);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SampleMethod(IntPtr self,uint index,IntPtr sample);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int TimeMethod(IntPtr self,long time);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int PointerMethod(IntPtr self,IntPtr pointer);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int LockMethod(IntPtr self,out IntPtr data,out int maximum,out int current);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int LengthMethod(IntPtr self,int length);
  static T Method<T>(IntPtr self,int slot)where T:class{return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self),slot*IntPtr.Size),typeof(T)) as T;}
  static void Check(int result){if(result<0)Marshal.ThrowExceptionForHR(result);}
  static void Release(ref IntPtr pointer){if(pointer!=IntPtr.Zero){Marshal.Release(pointer);pointer=IntPtr.Zero;}}
  static void U32(IntPtr type,string key,uint value){var id=new Guid(key);Check(Method<UInt32Method>(type,21)(type,ref id,value));}
  static void U64(IntPtr type,string key,ulong value){var id=new Guid(key);Check(Method<UInt64Method>(type,22)(type,ref id,value));}
  static void G(IntPtr type,string key,string value){var id=new Guid(key);var guid=new Guid(value);Check(Method<GuidMethod>(type,24)(type,ref id,ref guid));}
  void Describe(IntPtr type,bool output){
   G(type,"48eba18e-f8c9-4687-bf11-0a74c9f96a8f","73646976-0000-0010-8000-00aa00389b71"); // major: video
   G(type,"f7e34c9a-42e8-4714-b74b-cb29d72c35e5",output?"34363248-0000-0010-8000-00aa00389b71":"00000016-0000-0010-8000-00aa00389b71");
   U64(type,"1652c33d-d6b2-4012-b834-72030849a37d",((ulong)width<<32)|(uint)height);
   U64(type,"c459a2e8-3d2c-4e44-b132-fee5156c7bb0",((ulong)fps<<32)|1);
   U64(type,"c6376a1e-8d0a-4027-be45-6d9a0ad39bb6",((ulong)1<<32)|1);
   U32(type,"e2724bb8-e676-4806-b4b2-a8d6efb44ccd",2); // progressive
   if(output){U32(type,"20332624-fb0d-4d9e-bd0d-cbf6786c102e",(uint)Math.Max(4000000,width*height*fps/5));U32(type,"ad76a80b-2d5c-4e0b-b375-64e520137036",100);}
   else U32(type,"644b4e48-1e02-4516-b0eb-c01ca9d49ac6",(uint)(width*4));
  }
  public AnalyticsMp4(string path,int width,int height,int fps){
   this.width=width;this.height=height;this.fps=fps;IntPtr output=IntPtr.Zero,input=IntPtr.Zero;
   try{
    Check(MFStartup(0x20070,0));started=true;Check(MFCreateSinkWriterFromURL(path,IntPtr.Zero,IntPtr.Zero,out writer));
    Check(MFCreateMediaType(out output));Describe(output,true);Check(Method<AddStreamMethod>(writer,3)(writer,output,out stream));
    Check(MFCreateMediaType(out input));Describe(input,false);Check(Method<InputMethod>(writer,4)(writer,stream,input,IntPtr.Zero));Check(Method<SimpleMethod>(writer,5)(writer));
   }catch{Dispose();throw;}finally{Release(ref output);Release(ref input);}
  }
  public void Add(byte[] bgra){
   if(finished)throw new InvalidOperationException("Video is already complete.");if(bgra.Length!=width*height*4)throw new ArgumentException("Incorrect video frame size.");
   IntPtr sample=IntPtr.Zero,buffer=IntPtr.Zero;bool locked=false;
   try{
    Check(MFCreateMemoryBuffer(bgra.Length,out buffer));IntPtr data;int maximum,current;Check(Method<LockMethod>(buffer,3)(buffer,out data,out maximum,out current));locked=true;Marshal.Copy(bgra,0,data,bgra.Length);Check(Method<SimpleMethod>(buffer,4)(buffer));locked=false;Check(Method<LengthMethod>(buffer,6)(buffer,bgra.Length));
    Check(MFCreateSample(out sample));Check(Method<PointerMethod>(sample,42)(sample,buffer));long time=frame*10000000/fps,next=(frame+1)*10000000/fps;
    Check(Method<TimeMethod>(sample,36)(sample,time));Check(Method<TimeMethod>(sample,38)(sample,next-time));Check(Method<SampleMethod>(writer,6)(writer,stream,sample));frame++;
   }finally{if(locked)Method<SimpleMethod>(buffer,4)(buffer);Release(ref sample);Release(ref buffer);}
  }
  public void Complete(){if(finished)return;Check(Method<SimpleMethod>(writer,11)(writer));finished=true;}
  public void Dispose(){Release(ref writer);if(started){MFShutdown();started=false;}}
 }
}
