# Network telescope imports in v3

The `v3` branch starts at `temp` commit `41b1a02`. It integrates the read-only
Seestar SMB and DWARF FTP transports from Remote tester 0.2.1 into AstroArchive.

On Import, **Connect over network…** sits beside Saved telescope. The dialog
searches all active Wi-Fi/Ethernet IPv4 networks automatically, combines device
replies, and connects after the user selects a telescope. Its usual flow has no
address, port, credential or bandwidth fields. **Advanced…** opens those options.
Imports use the selected repository and saved physical telescope; a discovered
device identity helps select the right profile. Passwords are never saved.

After connecting, **Select files** opens capture folders. **Search all** lists
eligible captures recursively and selects them for review. Import preferences
filter files before transfer. A selection of 100 files or 1 GiB displays an inline
network/USB notice and a confirmation before starting the batch.

**Start live import** watches the displayed folder and subfolders. By default,
files present at the first successful scan form an excluded baseline. A new file
must have unchanged size and write time over two polls before being downloaded.
Growing or incomplete FITS files remain retryable. Advanced can include existing
files, change the five-second polling interval and change the 2 MiB/s transfer
limit. The main operation button becomes **Stop live import**. Completed imports
and verified downloads survive cancellation; brief disconnects retry automatically.

## Transfer and archive behaviour

`RemoteImport.cs` stages read-only transfers in the isolated user-data
`RemoteDownloads` cache. It preserves capture/session folder context, checks source
size and write time around transfer, validates uncompressed FITS structure, hashes
the received bytes, and verifies the local copy against that hash. FTP LIST can
omit seconds, so transfer checks use fresh MDTM observations. Saved receipts allow
verified cache reuse; corrupt downloads are fetched again. Partial files are removed.

Recognised adjacent sidecars and ancestor DWARF `shotsInfo.json` files are copied
within the selected storage scope. Missing, changing or unavailable metadata cannot
leave an older cached copy as evidence for the next import. Files then enter the
existing scan, screening, duplicate detection and verified archive import pipeline.
Telescope originals are retained. Space is needed for both downloads and archive
copies. These checks do not establish an independent telescope-provided checksum.

Seestar uses a separate SMB2/3 client rather than Windows mounting. Windows SMB
guest/signing policies are unchanged. The guest telescope session is unsigned.
DWARF uses passive, read-only FTP. The firmware must expose compatible file access;
discovery alone does not prove it does. Enable Save Each Frame in Seestar for subs.

## Development checks

```powershell
.\Application_Source\build.ps1
.\Application_Source\test.ps1 -RemoteOnly
.\Application_Source\test-remote-transports.ps1
.\Application_Source\dist\AstroArchive.exe --ui-test .\remote-ui --remote-only --no-updates
```

The transport checks need Python with impacket 0.13.1 and pyftpdlib 2.2.0 for
loopback-only, read-only fixtures; runtime users need neither. The regular full
application suite also runs the remote integration checks. `.github/workflows/v3.yml`
builds and validates the application, installer/updater, SMB/FTP archive transfers
and WPF dialog on Windows without publishing a release.

SMBLibrary is a replaceable DLL with matching source and licence material under
`Application_Source/Remote/lib`. Application and installer builds include it and
the notices. Real telescope discovery, model/firmware differences and capture
performance under network load still need hardware testing.
