using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AstroArchive.Remote;

public static class SmbTests
{
    static int checks;
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    static void Throws(Action action, string message) { try { action(); } catch { Assert(true, message); return; } throw new Exception("Expected rejection: " + message); }
    static void Sample(string path, int seed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string header = ("SIMPLE  =                    T".PadRight(80) + "BITPIX  =                   16".PadRight(80) + "NAXIS   =                    2".PadRight(80) + "NAXIS1  =                   16".PadRight(80) + "NAXIS2  =                   16".PadRight(80) + "END".PadRight(80)).PadRight(2880);
        byte[] bytes = new byte[5760]; Encoding.ASCII.GetBytes(header).CopyTo(bytes, 0); for (int i = 2880; i < 3392; i++) bytes[i] = (byte)(seed + i);
        File.WriteAllBytes(path, bytes);
    }
    static int Main(string[] args)
    {
        try
        {
            var path = SmbPath.Parse(@"\\192.168.1.42\EMMC Images\MyWorks\M31");
            string host, suffix;
            foreach (string draft in new[] { null, "", "/", "x", @"\", @"\\", @"\\host", @"\\host\", @"\\\share" })
                Assert(!SmbPath.TrySplitAddress(draft, out host, out suffix) && host == "" && suffix == "", "incomplete UI path handled safely: " + (draft ?? "<null>"));
            Assert(SmbPath.TrySplitAddress(path.Full, out host, out suffix) && host == path.Host && suffix == @"\EMMC Images\MyWorks\M31", "UI address replacement preserves storage and session path");
            Assert(path.Host == "192.168.1.42" && path.Share == "EMMC Images" && path.Relative == @"MyWorks\M31", "storage path separates address, share and capture folder");
            Assert(SmbPath.Parse(path.Root).Relative == "", "storage root is a valid browse target");
            Throws(() => SmbPath.Parse(@"\\192.168.1.42\EMMC Images\..\escape"), "SMB traversal rejected");
            Throws(() => SmbPath.Parse(@"\\192.168.1.42\EMMC Images\file:stream"), "SMB alternate data stream rejected");
            Throws(() => SmbPath.Parse(@"\\server\EMMC Images"), "direct access requires an explicit IPv4 address");
            var config = new Connection { Kind = "Seestar SMB", Host = "192.168.1.42", Folder = path.Full };
            config.Validate(false); Assert(config.SmbMode == "Direct", "direct telescope access is the default for new settings");
            config.Host = "192.168.1.43"; Throws(() => config.Validate(false), "mismatched path and telescope address rejected"); config.Host = "192.168.1.42";
            using (var source = Downloader.Source(config)) Assert(source is DirectSmbSource, "Seestar direct mode uses the independent SMB transport");
            config.SmbMode = "Windows"; using (var source = Downloader.Source(config)) Assert(source is LocalSource, "Windows compatibility mode remains selectable");
            config.SmbMode = "Direct";
            if (args.Length == 0) { Console.WriteLine(checks + " SMB checks passed."); return 0; }

            string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
            string storage = Path.Combine(root, "storage"), destination = Path.Combine(root, "downloads");
            string original = Path.Combine(storage, "MyWorks", "M31 test session", "raw_0001.fits");
            Sample(original, 1); string second = Path.Combine(storage, "MyWorks", "M42", "raw_0001.fits"); Sample(second, 2);
            config.Host = "127.0.0.1"; config.SmbPort = int.Parse(args[1]); config.Folder = @"\\127.0.0.1\EMMC Images\MyWorks"; config.Destination = destination; config.LimitMB = 0;
            var events = new List<Notice>();
            using (var source = new DirectSmbSource(config))
            {
                var entries = source.List(config.Folder);
                Assert(entries.Count == 2 && entries.All(e => e.Directory), "guest SMB2 session lists capture folders without Windows UNC access");
                string remote = config.Folder + @"\M31 test session\raw_0001.fits";
                Entry details = source.Stat(remote); Assert(details.Size == 5760 && details.Modified.Kind == DateTimeKind.Utc, "direct SMB metadata reports exposure size and UTC write time");
                using (var input = source.Open(remote))
                {
                    Assert(input.CanRead && !input.CanWrite && !input.CanSeek, "direct exposure stream is read-only");
                    Throws(() => input.Write(new byte[] { 1 }, 0, 1), "exposure stream rejects writes");
                    using (var output = new MemoryStream()) { input.CopyTo(output); Assert(output.ToArray().SequenceEqual(File.ReadAllBytes(original)), "direct guest transfer preserves exact original FITS bytes"); }
                }
                Throws(() => source.List(@"\\127.0.0.2\EMMC Images\MyWorks"), "direct source cannot switch to another telescope address");
                Assert(source.List(config.Folder).Count == 2, "direct source reconnects after a failed request");
                var downloader = new Downloader(config, source, events.Add);
                downloader.Poll(); Assert(!Directory.EnumerateFiles(destination, "*.fits", SearchOption.AllDirectories).Any(), "SMB exposure waits for stable observations");
                downloader.Poll(); var copies = Directory.EnumerateFiles(destination, "*.fits", SearchOption.AllDirectories).ToArray();
                Assert(copies.Length == 2 && copies.Select(Downloader.Hash).OrderBy(h => h).SequenceEqual(new[] { Downloader.Hash(original), Downloader.Hash(second) }.OrderBy(h => h)), "SMB downloads retain session paths and exact independent hashes");
                downloader = new Downloader(config, source, events.Add); downloader.Poll(); Assert(Directory.EnumerateFiles(destination, "*.fits", SearchOption.AllDirectories).Count() == 2, "SMB restart creates no duplicate downloads");
                string slow = Path.Combine(storage, "MyWorks", "M42", "raw_0002.fits"); Sample(slow, 3); byte[] complete = File.ReadAllBytes(slow); File.WriteAllBytes(slow, complete.Take(2880).ToArray());
                downloader.Poll(); downloader.Poll(); Assert(events.Last(e => e.File.EndsWith("raw_0002.fits")).State == "Retry", "SMB stable but incomplete FITS is rejected");
                File.WriteAllBytes(slow, complete); downloader.Poll(); downloader.Poll(); Assert(events.Last(e => e.File.EndsWith("raw_0002.fits")).State == "Downloaded", "SMB incomplete exposure recovers after writing finishes");
                Assert(Downloader.Hash(original) == Downloader.Hash(copies.Single(p => p.Contains("M31 test session"))), "telescope original is unchanged after repeated reads");
                File.WriteAllBytes(copies[0], new byte[5760]); downloader.Verify(); Assert(events.Any(e => e.State == "Failed"), "SMB downloaded-file corruption is detected locally");
            }
            // Verification is local, even when the configured SMB endpoint is offline.
            int previousEvents=events.Count;config.SmbPort++; using (var source = new DirectSmbSource(config)) { var downloader = new Downloader(config, source, events.Add); downloader.Verify(); Assert(events.Count>previousEvents && (events.Last().State == "Verified" || events.Last().State == "Failed"), "receipt verification does not connect to an offline telescope"); }
            Console.WriteLine(checks + " SMB checks passed."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
