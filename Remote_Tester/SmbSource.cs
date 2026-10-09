using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using SMBLibrary;
using SMBLibrary.Client;
using SmbAttributes = SMBLibrary.FileAttributes;

namespace AstroArchive.Remote
{
    public sealed class SmbPath
    {
        public string Host, Share, Relative;
        public string Root { get { return @"\\" + Host + "\\" + Share; } }
        public string Full { get { return Root + (Relative.Length == 0 ? "" : "\\" + Relative); } }

        // UI fields may be empty or partially edited. Strict validation stays in Parse.
        public static bool TrySplitAddress(string path, out string host, out string suffix)
        {
            host = suffix = "";
            if (path == null || !path.StartsWith(@"\\")) return false;
            int end = path.IndexOf('\\', 2);
            if (end <= 2 || end >= path.Length - 1 || path[end + 1] == '\\') return false;
            host = path.Substring(2, end - 2);
            suffix = path.Substring(end);
            return true;
        }

        public static SmbPath Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(@"\\") || path.Contains("/"))
                throw new ArgumentException(@"Choose a telescope address, storage share and capture folder.");
            string[] parts = path.Substring(2).TrimEnd('\\').Split('\\');
            IPAddress ip;
            if (parts.Length < 2 || !IPAddress.TryParse(parts[0], out ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new ArgumentException("Enter the telescope's IPv4 address and a storage share.");
            Paths.SafeRelative(parts[1]);
            string relative = string.Join("\\", parts.Skip(2));
            if (relative.Length > 0) Paths.SafeRelative(relative);
            return new SmbPath { Host = ip.ToString(), Share = parts[1], Relative = relative };
        }
    }

    // Independent SMB2/3 transport. No Windows UNC mounting or security-policy calls.
    // Guest access is unsigned and is limited to this client's telescope session.
    public sealed class DirectSmbSource : ISource
    {
        sealed class Client : SMB2Client
        {
            public Client() : base(5000, false, false) { }
            public bool ConnectTo(IPAddress host, int port) { return base.Connect(host, SMBTransportType.DirectTCPTransport, port); }
        }

        readonly SmbPath selected;
        readonly int port;
        Client client;
        ISMBFileStore store;

        public DirectSmbSource(Connection connection)
        {
            selected = SmbPath.Parse(connection.Folder);
            if (selected.Host != connection.Host) throw new ArgumentException("The storage path and telescope address must match.");
            port = connection.SmbPort;
        }

        void Connect()
        {
            if (store != null) return;
            client = new Client();
            try
            {
                if (!client.ConnectTo(IPAddress.Parse(selected.Host), port))
                    throw new IOException("The telescope did not accept an SMB2/3 file connection. Check its address, network and firmware.");
                Check(client.Login("", "guest", ""), "Guest sign-in");
                NTStatus status;
                store = client.TreeConnect(selected.Share, out status);
                Check(status, "Opening storage '" + selected.Share + "'");
                if (store == null) throw new IOException("The telescope did not return its storage share.");
            }
            catch { Reset(); throw; }
        }

        string Relative(string path)
        {
            SmbPath parsed = SmbPath.Parse(path);
            if (!parsed.Host.Equals(selected.Host, StringComparison.OrdinalIgnoreCase) || !parsed.Share.Equals(selected.Share, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The requested path is outside this telescope's selected storage.");
            return parsed.Relative;
        }

        static void Check(NTStatus status, string operation)
        {
            if (status != NTStatus.STATUS_SUCCESS)
                throw new IOException(operation + " failed: " + status + " (0x" + ((uint)status).ToString("X8") + ").", unchecked((int)status));
        }

        object OpenHandle(string path, bool directory)
        {
            Connect();
            object handle;
            SMBLibrary.FileStatus fileStatus;
            Check(store.CreateFile(out handle, out fileStatus, Relative(path), AccessMask.GENERIC_READ,
                directory ? SmbAttributes.Directory : SmbAttributes.Normal,
                ShareAccess.Read | ShareAccess.Write | ShareAccess.Delete, CreateDisposition.FILE_OPEN,
                directory ? CreateOptions.FILE_DIRECTORY_FILE : CreateOptions.FILE_NON_DIRECTORY_FILE, null), "Opening " + (directory ? "folder" : "exposure"));
            return handle;
        }

        void Close(object handle) { if (store != null && handle != null) { try { store.CloseFile(handle); } catch { } } }
        T Request<T>(Func<T> request) { try { return request(); } catch { Reset(); throw; } }

        public List<Entry> List(string folder)
        {
            return Request(() =>
            {
                object handle = OpenHandle(folder, true);
                try
                {
                    var entries = new List<Entry>();
                    int seen = 0;
                    List<QueryDirectoryFileInformation> page;
                    // SMBLibrary aggregates all pages and returns NO_MORE_FILES
                    // with the completed list; it must not be queried again here.
                    NTStatus status = store.QueryDirectory(out page, handle, "*", FileInformationClass.FileDirectoryInformation);
                    if (status != NTStatus.STATUS_NO_MORE_FILES && !(status == NTStatus.STATUS_NO_SUCH_FILE && (page == null || page.Count == 0)))
                        Check(status, "Listing folder");
                    foreach (var item in page ?? new List<QueryDirectoryFileInformation>())
                    {
                        if (++seen > 50000) throw new IOException("More than 50,000 entries. Choose a smaller capture folder.");
                        var file = item as FileDirectoryInformation;
                        if (file == null) throw new IOException("The telescope returned an unsupported directory entry.");
                        if (file.FileName == "." || file.FileName == ".." || (file.FileAttributes & SmbAttributes.ReparsePoint) != 0) continue;
                        Paths.SafeRelative(file.FileName);
                        if (file.FileName.IndexOfAny(new[] { '/', '\\' }) >= 0) throw new IOException("The telescope returned an invalid filename.");
                        entries.Add(new Entry { Name = file.FileName, Path = folder.TrimEnd('\\') + "\\" + file.FileName,
                            Directory = (file.FileAttributes & SmbAttributes.Directory) != 0, Size = file.EndOfFile,
                            Modified = DateTime.SpecifyKind(file.LastWriteTime, DateTimeKind.Utc) });
                    }
                    return entries;
                }
                finally { Close(handle); }
            });
        }

        Entry Information(string path, object handle)
        {
            FileInformation information;
            Check(store.GetFileInformation(out information, handle, FileInformationClass.FileNetworkOpenInformation), "Reading exposure details");
            var info = information as FileNetworkOpenInformation;
            if (info == null || !info.LastWriteTime.HasValue || info.EndOfFile < 0) throw new IOException("The telescope did not return usable exposure details.");
            if ((info.FileAttributes & (SmbAttributes.Directory | SmbAttributes.ReparsePoint)) != 0) throw new IOException("The selected exposure is a folder or linked file.");
            return new Entry { Path = path, Name = path.Split('\\').Last(), Size = info.EndOfFile, Modified = DateTime.SpecifyKind(info.LastWriteTime.Value, DateTimeKind.Utc) };
        }

        public Entry Stat(string path)
        {
            return Request(() => { object handle = OpenHandle(path, false); try { return Information(path, handle); } finally { Close(handle); } });
        }

        public Stream Open(string path)
        {
            return Request<Stream>(() =>
            {
                object handle = OpenHandle(path, false);
                try { return new ReadStream(this, store, handle, Information(path, handle).Size); }
                catch { Close(handle); throw; }
            });
        }

        sealed class ReadStream : Stream
        {
            readonly DirectSmbSource owner;
            readonly ISMBFileStore files;
            object handle;
            readonly long length;
            long offset;
            public ReadStream(DirectSmbSource owner, ISMBFileStore files, object handle, long length) { this.owner = owner; this.files = files; this.handle = handle; this.length = length; }
            public override int Read(byte[] buffer, int start, int count)
            {
                if (handle == null) throw new ObjectDisposedException("Seestar exposure");
                if (buffer == null) throw new ArgumentNullException("buffer");
                if (start < 0 || count < 0 || start > buffer.Length - count) throw new ArgumentOutOfRangeException();
                if (count == 0) return 0;
                try
                {
                    byte[] bytes;
                    NTStatus status = files.ReadFile(out bytes, handle, offset, (int)Math.Min(count, owner.client.MaxReadSize));
                    if (status == NTStatus.STATUS_END_OF_FILE) return 0;
                    Check(status, "Reading exposure");
                    if (bytes == null || bytes.Length > count) throw new IOException("The telescope returned an invalid read response.");
                    Buffer.BlockCopy(bytes, 0, buffer, start, bytes.Length);
                    offset += bytes.Length;
                    return bytes.Length;
                }
                catch { owner.Reset(); throw; }
            }
            public override bool CanRead { get { return handle != null; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return length; } }
            public override long Position { get { return offset; } set { throw new NotSupportedException(); } }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long length) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException("Telescope access is read-only."); }
            protected override void Dispose(bool disposing) { if (disposing && handle != null) { if (owner.store == files) owner.Close(handle); handle = null; } base.Dispose(disposing); }
        }

        void Reset()
        {
            if (store != null) { try { store.Disconnect(); } catch { } store = null; }
            if (client != null) { try { client.Disconnect(); } catch { } client = null; }
        }
        public void Dispose() { Reset(); }
    }
}
