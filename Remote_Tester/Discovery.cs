using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace AstroArchive.Remote {
 public sealed class DiscoveryNetwork {
  public string Name {get;set;}
  public string Address {get;set;}
  public string Mask {get;set;}
  public string Display {get{return Name+" · "+Address;}}
  public string Broadcast {get{byte[] ip=IPAddress.Parse(Address).GetAddressBytes(),mask=IPAddress.Parse(Mask).GetAddressBytes();return new IPAddress(ip.Select((b,i)=>(byte)(b|~mask[i])).ToArray()).ToString();}}
  public bool Contains(string host){IPAddress ip;if(!IPAddress.TryParse(host,out ip)||ip.AddressFamily!=AddressFamily.InterNetwork)return false;byte[] remote=ip.GetAddressBytes(),local=IPAddress.Parse(Address).GetAddressBytes(),mask=IPAddress.Parse(Mask).GetAddressBytes();if(remote[0]==0||remote[0]>=224||host==Address||host==Broadcast)return false;return remote.Select((b,i)=>(b&mask[i])==(local[i]&mask[i])).All(b=>b);}
  public void Validate(){IPAddress address,mask;if(!IPAddress.TryParse(Address,out address)||address.AddressFamily!=AddressFamily.InterNetwork||!IPAddress.TryParse(Mask,out mask)||mask.AddressFamily!=AddressFamily.InterNetwork)throw new ArgumentException("Choose an active IPv4 network.");byte[] bytes=mask.GetAddressBytes();bool zero=false;int bits=0;foreach(byte b in bytes)for(int i=7;i>=0;i--){bool one=(b&(1<<i))!=0;if(one&&zero)throw new ArgumentException("The network mask is invalid.");if(one)bits++;else zero=true;}if(bits<1||bits>30)throw new ArgumentException("Discovery needs an IPv4 subnet with a broadcast address.");}
  public static List<DiscoveryNetwork> Available(){var networks=new List<DiscoveryNetwork>();foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(a=>a.OperationalStatus==OperationalStatus.Up&&(a.NetworkInterfaceType==NetworkInterfaceType.Wireless80211||a.NetworkInterfaceType==NetworkInterfaceType.Ethernet)).OrderBy(a=>a.NetworkInterfaceType==NetworkInterfaceType.Wireless80211?0:1)){foreach(var ip in adapter.GetIPProperties().UnicastAddresses.Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address))){var network=new DiscoveryNetwork{Name=adapter.Name,Address=ip.Address.ToString(),Mask=ip.IPv4Mask.ToString()};try{network.Validate();networks.Add(network);}catch(ArgumentException){}}}return networks;}
 }
 public sealed class DiscoveredTelescope {
  public string Kind {get;set;}
  public string Name {get;set;}
  public string Host {get;set;}
  public string Identity {get;set;}
  public string Firmware {get;set;}
 }
 public sealed class DiscoveryResult {
  public readonly List<DiscoveredTelescope> Devices=new List<DiscoveredTelescope>();
  public readonly List<string> Warnings=new List<string>();
 }
 public static class TelescopeDiscovery {
  // Read-only native discovery, independent of Alpaca bridges and file services.
  // Wire formats: dwarflab-sdk/proto/ble.proto and seestar-api/seestar/discovery.py.
  static readonly byte[] Magic=Encoding.ASCII.GetBytes("txtl");
  public static byte[] DwarfPing(long timestamp){using(var stream=new MemoryStream()){Varint(stream,8);Varint(stream,1);Varint(stream,16);Varint(stream,(ulong)timestamp);Varint(stream,26);Varint(stream,4);stream.Write(Magic,0,4);return stream.ToArray();}}
  public static byte[] SeestarPing(int id){return Encoding.UTF8.GetBytes("{\"method\":\"scan_iscope\",\"params\":\"\",\"id\":"+id+"}\r\n");}
  static void Varint(Stream stream,ulong value){while(value>=128){stream.WriteByte((byte)((value&127)|128));value>>=7;}stream.WriteByte((byte)value);}
  sealed class Field {public int Number;public ulong Value;public byte[] Bytes;}
  static ulong ReadVarint(byte[] data,ref int offset){ulong value=0;for(int shift=0;shift<70;shift+=7){if(offset>=data.Length)throw new InvalidDataException();byte b=data[offset++];if(shift==63&&b>1)throw new InvalidDataException();value|=(ulong)(b&127)<<shift;if((b&128)==0)return value;}throw new InvalidDataException();}
  static List<Field> Fields(byte[] data){var fields=new List<Field>();int offset=0;while(offset<data.Length){ulong key=ReadVarint(data,ref offset);if(key<8||key>>3>536870911)throw new InvalidDataException();var field=new Field{Number=(int)(key>>3)};int wire=(int)(key&7);if(wire==0)field.Value=ReadVarint(data,ref offset);else if(wire==2){ulong count=ReadVarint(data,ref offset);if(count>(ulong)(data.Length-offset))throw new InvalidDataException();field.Bytes=new byte[(int)count];Buffer.BlockCopy(data,offset,field.Bytes,0,(int)count);offset+=(int)count;}else if(wire==1||wire==5){int count=wire==1?8:4;if(count>data.Length-offset)throw new InvalidDataException();offset+=count;}else throw new InvalidDataException();fields.Add(field);if(fields.Count>256)throw new InvalidDataException();}return fields;}
  static string Text(List<Field> fields,int number){var field=fields.LastOrDefault(f=>f.Number==number);return field==null||field.Bytes==null?"":Clean(new UTF8Encoding(false,true).GetString(field.Bytes));}
  static string Clean(string value){return new string((value??"").Where(c=>!char.IsControl(c)).Take(80).ToArray()).Trim();}
  public static DiscoveredTelescope ParseDwarf(byte[] data,string host){try{if(data==null||data.Length>16384)return null;var fields=Fields(data);var type=fields.LastOrDefault(f=>f.Number==1);var magic=fields.LastOrDefault(f=>f.Number==3);if(type==null||type.Value!=2||magic==null||magic.Bytes==null||!magic.Bytes.SequenceEqual(Magic))return null;string name=Text(fields,8);return new DiscoveredTelescope{Kind="DWARF FTP",Name=name.Length==0?"DWARF":name,Host=host,Identity=Text(fields,7),Firmware=Text(fields,10)};}catch(InvalidDataException){return null;}catch(DecoderFallbackException){return null;}}
  static string Value(Dictionary<string,object> data,string key){object value;return data.TryGetValue(key,out value)&&value is string?Clean((string)value):"";}
  public static DiscoveredTelescope ParseSeestar(byte[] data,string host){try{if(data==null||data.Length>16384)return null;var payload=new JavaScriptSerializer{MaxJsonLength=16384,RecursionLimit=8}.Deserialize<Dictionary<string,object>>(new UTF8Encoding(false,true).GetString(data));object result,code;if(payload==null||Value(payload,"method")!="scan_iscope"||!payload.TryGetValue("code",out code)||!(code is int)||(int)code!=0||!payload.TryGetValue("result",out result))return null;var info=result as Dictionary<string,object>;if(info==null)return null;string model=Value(info,"product_model"),ssid=Value(info,"ssid"),family=Value(info,"model");if(!new[]{model,ssid,family}.Any(s=>s.StartsWith("Seestar",StringComparison.OrdinalIgnoreCase)))return null;return new DiscoveredTelescope{Kind="Seestar SMB",Name=model.Length>0?model:ssid.Length>0?ssid:"Seestar",Host=host,Identity=Value(info,"sn"),Firmware=""};}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}}
  public static bool Add(DiscoveryResult result,DiscoveryNetwork network,DiscoveredTelescope device){if(device==null||!network.Contains(device.Host)||result.Devices.Count>=64||result.Devices.Any(d=>d.Kind==device.Kind&&d.Host==device.Host))return false;result.Devices.Add(device);return true;}
  sealed class Probe:IDisposable {
   public Socket Socket;public int Port;public bool Dwarf;
   public Probe(DiscoveryNetwork network,bool dwarf){Dwarf=dwarf;Port=dwarf?9900:4720;Socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);try{Socket.EnableBroadcast=true;Socket.ExclusiveAddressUse=true;Socket.Bind(new IPEndPoint(IPAddress.Parse(network.Address),dwarf?9900:0));Socket.ReceiveTimeout=100;Socket.SendTimeout=250;}catch{Socket.Dispose();throw;}}
   public void Dispose(){Socket.Dispose();}
  }
  public static DiscoveryResult Find(DiscoveryNetwork network,CancellationToken cancel){return Find(network,cancel,8000,null);}
  // Custom duration/destination are used only by loopback protocol tests.
  public static DiscoveryResult Find(DiscoveryNetwork network,CancellationToken cancel,int milliseconds,string destination){network.Validate();if(milliseconds<1||milliseconds>10000)throw new ArgumentException("Invalid discovery duration.");var result=new DiscoveryResult();var probes=new List<Probe>();try{foreach(bool dwarf in new[]{true,false}){cancel.ThrowIfCancellationRequested();try{probes.Add(new Probe(network,dwarf));}catch(SocketException e){result.Warnings.Add((dwarf?"DWARF":"Seestar")+" discovery could not start: "+e.Message+(dwarf?" Close other telescope discovery apps using UDP 9900 and retry.":""));}}if(probes.Count==0)return result;var watch=Stopwatch.StartNew();long next=0;int sequence=0;byte[] buffer=new byte[16384];while(watch.ElapsedMilliseconds<milliseconds){cancel.ThrowIfCancellationRequested();if(watch.ElapsedMilliseconds>=next){sequence++;next=watch.ElapsedMilliseconds+1000;foreach(var probe in probes){byte[] ping=probe.Dwarf?DwarfPing(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()):SeestarPing(sequence);try{probe.Socket.SendTo(ping,new IPEndPoint(IPAddress.Parse(destination??network.Broadcast),probe.Port));}catch(SocketException e){string warning=(probe.Dwarf?"DWARF":"Seestar")+" broadcast failed: "+e.Message;if(!result.Warnings.Contains(warning))result.Warnings.Add(warning);}}}foreach(var probe in probes){for(int i=0;i<32&&probe.Socket.Poll(0,SelectMode.SelectRead);i++){cancel.ThrowIfCancellationRequested();try{EndPoint sender=new IPEndPoint(IPAddress.Any,0);int length=probe.Socket.ReceiveFrom(buffer,ref sender);string host=((IPEndPoint)sender).Address.ToString();if(!network.Contains(host))continue;byte[] packet=new byte[length];Buffer.BlockCopy(buffer,0,packet,0,length);Add(result,network,probe.Dwarf?ParseDwarf(packet,host):ParseSeestar(packet,host));}catch(SocketException e){if(e.SocketErrorCode!=SocketError.MessageSize&&e.SocketErrorCode!=SocketError.TimedOut){string warning="Discovery response could not be read: "+e.Message;if(!result.Warnings.Contains(warning))result.Warnings.Add(warning);}}}}if(cancel.WaitHandle.WaitOne(50))cancel.ThrowIfCancellationRequested();}return result;}finally{foreach(var probe in probes)probe.Dispose();}}
 }
}
