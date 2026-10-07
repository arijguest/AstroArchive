// .NET Framework 4.8. One source read supplies both the copy and SHA-256 digest.
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
namespace AstroArchive {
 public static class FileTransfer {
  static readonly ConcurrentBag<byte[]> buffers=new ConcurrentBag<byte[]>();static int retained;
  public sealed class BufferLease:IDisposable {public readonly byte[] Buffer;bool returned;internal BufferLease(byte[] buffer){Buffer=buffer;}public void Dispose(){if(returned)return;returned=true;if(Interlocked.Increment(ref retained)<=8)buffers.Add(Buffer);else Interlocked.Decrement(ref retained);}}
  public static BufferLease Rent(){byte[] buffer;if(buffers.TryTake(out buffer))Interlocked.Decrement(ref retained);else buffer=new byte[1048576];return new BufferLease(buffer);}

  public static string CopyHash(string from,string to,CancellationToken ct,PipelineMetrics metrics,bool cloud,PipelineMetrics.Transfer transfer=null){
   ct.ThrowIfCancellationRequested();if(transfer!=null)transfer.BeginCopy();
   PipelineMetrics.Scope availability=cloud&&metrics!=null?metrics.Begin("Cloud availability",Path.GetFileName(from)):null;PipelineMetrics.Scope copy=!cloud&&metrics!=null?metrics.Begin("Copy + source hash",Path.GetFileName(from)):null;
   try{using(var lease=Rent())using(var sha=SHA256.Create())using(var input=new FileStream(from,FileMode.Open,FileAccess.Read,FileShare.Read,1048576,FileOptions.SequentialScan))using(var output=new FileStream(to,FileMode.CreateNew,FileAccess.Write,FileShare.None,1048576,FileOptions.SequentialScan)){byte[] buffer=lease.Buffer;while(true){ct.ThrowIfCancellationRequested();if(cloud&&metrics!=null&&availability==null)availability=metrics.Begin("Cloud availability",Path.GetFileName(from));int n=input.Read(buffer,0,buffer.Length);if(availability!=null){availability.Bytes(n);if(n==0)availability.Complete();availability.Dispose();availability=null;}if(n==0)break;if(copy==null&&metrics!=null)copy=metrics.Begin("Copy + source hash",Path.GetFileName(from));sha.TransformBlock(buffer,0,n,buffer,0);output.Write(buffer,0,n);if(transfer!=null)transfer.Copied(n);if(copy!=null)copy.Bytes(n);if(cloud&&copy!=null){copy.Dispose();copy=null;}}sha.TransformFinalBlock(new byte[0],0,0);output.Flush(true);if(transfer!=null)transfer.EndCopy();if(copy==null&&metrics!=null)copy=metrics.Begin("Copy + source hash",Path.GetFileName(from));if(copy!=null)copy.Complete();return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();}}finally{if(availability!=null)availability.Dispose();if(copy!=null)copy.Dispose();}
  }
 }
}
