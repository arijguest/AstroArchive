#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics;
namespace AstroArchive;

// Settings contain opaque references. Session credentials never reach disk;
// persistent credentials require the desktop's unlocked Secret Service keyring.
public static class LinuxSecrets
{
    private static readonly ConcurrentDictionary<string,string> session = new();
    public static string Temporary(string value) { string id="session:"+Guid.NewGuid().ToString("N"); session[id]=value; return id; }
    public static void Forget(string id) => session.TryRemove(id,out _);
    public static string Store(string value)
    {
        if(string.IsNullOrWhiteSpace(value)) return "";
        string id=Guid.NewGuid().ToString("N");
        Invoke(["store","--label=AstroArchive credential","application","astroarchive","credential",id],value);
        return "secret-service:"+id;
    }
    public static string Read(string reference)
    {
        if(session.TryGetValue(reference,out var value)) return value;
        if(!reference.StartsWith("secret-service:",StringComparison.Ordinal)) return "";
        return Invoke(["lookup","application","astroarchive","credential",reference[15..]],null).TrimEnd('\r','\n');
    }
    public static void Remove(string reference) { if(reference.StartsWith("session:",StringComparison.Ordinal))Forget(reference);else if(reference.StartsWith("secret-service:",StringComparison.Ordinal))Invoke(["clear","application","astroarchive","credential",reference[15..]],null); }
    private static string Invoke(string[] args,string? input)
    {
        const string executable="/usr/bin/secret-tool";
        if(!File.Exists(executable)) throw new PlatformNotSupportedException("Install libsecret-tools and unlock your desktop keyring to save credentials. You can use a credential for this session without saving it.");
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true };
        foreach(string arg in args) start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
        if(input!=null) process.StandardInput.Write(input); process.StandardInput.Close();
        if(!process.WaitForExit(15000)) { process.Kill(true); throw new IOException("Desktop keyring did not respond within 15 seconds."); }
        if(process.ExitCode!=0) throw new PlatformNotSupportedException("The desktop keyring is unavailable or locked. Unlock it before saving credentials.");
        return output.GetAwaiter().GetResult();
    }
}
