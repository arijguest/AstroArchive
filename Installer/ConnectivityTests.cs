using System;
using System.IO;
using AstroArchive.Installation;

static class ConnectivityTests {
 public static void Run(Action<string,Action> test,Action<bool,string> check,string scratch){
  test("Pending and Offline block update requests before the transport",()=>{
   int requests=0;var client=new UpdateClient{Fetch=(uri,limit)=>{requests++;return new byte[0];}};
   foreach(var mode in new[]{ConnectivityMode.Pending,ConnectivityMode.Offline}){ConnectivityPolicy.Apply(mode);bool denied=false;try{client.Check(new InstallRecord{Version="3.2.8",PackageVersion="3.2.8.1"});}catch(InvalidOperationException){denied=true;}check(denied&&requests==0,"An offline request reached the transport");}
  });
  test("Atomic per-user choices survive updates, repairs and invalid handoffs",()=>{
   string path=Path.Combine(scratch,"connectivity.json");check(ConnectivityPolicy.Read(path)==ConnectivityMode.Pending,"Missing choice guessed Hybrid");
   ConnectivityPolicy.Save(ConnectivityMode.Offline,path);check(ConnectivityPolicy.Read(path)==ConnectivityMode.Offline,"Offline choice lost");ConnectivityPolicy.Save(ConnectivityMode.Hybrid,path);check(ConnectivityPolicy.Read(path)==ConnectivityMode.Hybrid,"Hybrid replacement failed");
   check(Directory.GetFiles(scratch,"connectivity.json.*.tmp").Length==0,"Handoff left staging files");File.WriteAllText(path,"{\"Schema\":1,\"Mode\":77}");check(ConnectivityPolicy.Read(path)==ConnectivityMode.Pending,"Unknown choice allowed internet");
   File.WriteAllText(path,"damaged");check(ConnectivityPolicy.Read(path)==ConnectivityMode.Pending,"Corrupt handoff allowed internet");
  });
  test("Offline cancels active policy tokens and reconnect creates a new token",()=>{
   ConnectivityPolicy.Apply(ConnectivityMode.Hybrid);var token=ConnectivityPolicy.Token;ConnectivityPolicy.Apply(ConnectivityMode.Offline);check(token.IsCancellationRequested&&!ConnectivityPolicy.AllowsInternet,"Offline did not abort active work");ConnectivityPolicy.Apply(ConnectivityMode.Hybrid);check(!ConnectivityPolicy.Token.IsCancellationRequested,"Reconnect reused an aborted token");
  });
 }
}
