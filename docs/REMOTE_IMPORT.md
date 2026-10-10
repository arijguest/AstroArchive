# Network and live telescope imports

Network discovery, selected imports and live imports are available in
[AstroArchive 3.0.1](https://github.com/arijguest/AstroArchive/releases/tag/v3.0.1.1)
and later. [3.1.1](https://github.com/arijguest/AstroArchive/releases/tag/v3.1.1.1)
adds independent, simultaneous telescope sessions and selected downloads while live
import runs. Seestar uses read-only SMB access; DWARF uses read-only FTP.

On Import, **Connect over network…** sits beside Saved telescope. The dialog
searches all active Wi-Fi/Ethernet IPv4 networks automatically, combines device
replies, and connects after the user selects a telescope. Its usual flow has no
address, port, credential or bandwidth fields. **Advanced…** opens those options.
Imports use the selected repository and saved physical telescope; a discovered
device identity helps select the right profile. Passwords are never saved.

## Connect and import

1. Install [the latest Windows release](https://github.com/arijguest/AstroArchive/releases/latest)
   and choose a repository under **Settings → General**. If using a portable build,
   extract the entire folder and keep `SMBLibrary.dll`, `ThirdParty` and the licence
   notices with `AstroArchive.exe`.
2. Keep the PC and telescope awake and on the same Wi-Fi/LAN, or connect the PC
   to the telescope hotspot. The telescope app can continue managing captures.
3. On **Import**, click **Connect over network…**, wait for discovery, select your
   telescope and click **Connect**. A discovery reply is followed by a file-access
   check; it does not itself guarantee compatible storage access.
4. Choose the saved physical telescope or name a new one. Use the same profile
   for the same physical device across USB and network imports. A profile already
   bound to a different discovered device cannot be selected for this telescope.
5. Choose files to import now or start live import as described below.

After connecting, **Select files** opens capture folders. **Search all** lists
eligible captures recursively across the configured storage root and selects them
for review. Double-click folders or press Enter to open them; tick individual files
or press Space to toggle the current row. Selected files remain selected across
folders. **Up** moves to the parent and **All captures** returns to the storage root.
**Import preferences…** opens the existing import settings without losing the selection.
Import preferences filter files before transfer. A selection of 100 files or 1 GiB displays an inline
network/USB notice and a confirmation before starting the batch.

## Live imports

**Start live import** watches the displayed folder and subfolders. By default,
files present at the first successful scan form an excluded baseline. A new file
must have unchanged size and write time over two polls before being downloaded.
This baseline also excludes a file already visible but still being written when
live import starts. Use **Advanced… → Also import existing captures when live import
starts**, or **Search all**, to include it. Open a session/target folder first to
watch only that folder, or use **All captures** to watch the full storage.

Growing or incomplete new FITS files remain retryable. Advanced can include existing
files, change the five-second polling interval and change the 2 MiB/s transfer
limit. A **Live import underway** indicator beside **Connect over network…** opens
Activity. Reconnect at any time to select additional files, or connect another
telescope and start a separate live watcher or selected download. Use a distinct
saved telescope name for each physical device. A device can have one live watcher;
additional selected downloads use the same saved telescope. Completed imports
and verified downloads survive cancellation; brief disconnects retry automatically.
**Stop live import** or **Cancel download** in Activity stops only that session.
**Stop all telescope imports** on Import stops every network session. Closing the
app cancels sessions and waits for them to finish before closing the archive.
Keep AstroArchive open and the PC awake. Capture settings and exposure control
remain in the telescope app.
Polling is followed by downloading, checks and archiving, so five seconds is not
a delivery guarantee. Lower the transfer limit if downloads affect capture/app
responsiveness. Captures created while live import is stopped are existing files
on its next start; use **Search all** or include existing captures to catch up.

## Advanced access and troubleshooting

Ordinary discovery needs no IP entry. **Advanced…** allows a manual telescope
IPv4 address, storage path and discovery adapter. For DWARF it also offers FTP
port (default 21), username (default anonymous) and a password held only for the
active connection. Live polling supports 2–120 seconds; transfer limits are
1, 2 or 5 MB/s, or Unlimited. These dialog settings are not saved as credentials.

To find a manual address, check the telescope app's network settings or the
router's connected-device/DHCP list and identify your telescope. Use that device's
IPv4 address. Windows **Settings → Network & internet → Wi-Fi/Ethernet → Properties**
shows the PC's IPv4 address and gateway, which help identify its network; the PC
address is not the telescope address. Hotspot addressing varies by model/firmware.

Leave **Storage path** blank initially: Seestar defaults to
`\\<telescope IP>\EMMC Images`; DWARF defaults to `/`. A custom Seestar path must
include the host and share, for example `\\<telescope IP>\EMMC Images\MyWorks` if
that folder exists. Use a folder exposed by the current firmware. Apply the
manual address/path to connect and check access.

- **Nothing discovered:** check both devices are awake and on the same network,
  then **Search again**. Guest Wi-Fi or client isolation can prevent local replies.
  Check any Windows Firewall prompt for AstroArchive on your trusted local network;
  a VPN or an unrelated adapter may interfere. Advanced can select an adapter or
  connect directly by IP.
- **Discovered but connection/listing fails:** verify the telescope IP and storage
  path, firmware file access, and DWARF FTP settings. Direct Seestar access uses
  its own SMB client; File Explorer access and changes to Windows SMB guest/signing
  settings are not prerequisites. Older tester Windows-share errors do not require
  weakening those settings for this client.
- **Captures missing:** enable Seestar **Save Each Frame** for individual exposures,
  check the displayed storage scope and **Import preferences…**, then **Search all**.
  Failed/raster exclusions still apply. Live mode excludes existing files by default.
- **Repeated retry or incomplete imports:** keep the connection and PC awake,
  allow captures to finish writing, and check free space for staging and repository
  copies. Read **Repository → Diagnostics** for the latest report. Completed
  imports remain available when stopping or retrying.

For a support report, include app/package version, telescope model/firmware, the
exact error and whether it occurred in discovery, connection, listing, downloading
or archiving. For live issues include folder scope, polling/transfer limit and
whether existing captures were included. Do not include passwords.

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
Different telescope caches transfer concurrently. Requests sharing a download
cache wait for its current batch instead of failing with a file-lock error; the
wait can be canceled independently. Archive scans and commits are serialized
against the shared repository, so simultaneous selections and live captures use
the latest hash-based duplicate checks. An overlapping capture is stored once,
reported as already present, and marked complete by the live watcher. Verified
cached downloads are reused. Repository switching and unrelated archive mutations
remain unavailable until all network sessions finish.
The cache is under `%LOCALAPPDATA%\AstroArchive\RemoteDownloads` and remains after
an import, stop or uninstall. Stop imports and close AstroArchive before deleting
it manually. Deleting cached downloads does not delete completed archive imports;
later retries need to download those files again.

Seestar uses a separate SMB2/3 client rather than Windows mounting. Windows SMB
guest/signing policies are unchanged. The guest telescope session is unsigned.
DWARF uses passive, read-only FTP. The firmware must expose compatible file access;
discovery alone does not prove it does. Enable Save Each Frame in Seestar for subs.

## Development checks

```powershell
.\Application_Source\build.ps1
python -m pip install impacket==0.13.1 pyftpdlib==2.2.0
.\Application_Source\test.ps1 -RemoteOnly
.\Application_Source\test-remote-transports.ps1
.\Application_Source\dist\AstroArchive.exe --ui-test .\remote-ui --remote-only --no-updates
```

The transport checks need Python 3.12 on PATH with impacket 0.13.1 and pyftpdlib
2.2.0 for loopback-only, read-only fixtures; runtime users need neither. Run
`test.ps1` first to build the test executable used by the transport script.
Loopback ports 24445 and 24421 must be free. The regular full
application suite also runs the remote integration checks. The
[Windows build and release workflow](../.github/workflows/windows.yml) validates
`main` and pull requests and publishes eligible release packages. The separate
[v3 development workflow](../.github/workflows/v3.yml)
builds and validates the application, installer/updater, SMB/FTP archive transfers
and WPF dialog on Windows for `v3` and `temp` without publishing a release.

SMBLibrary is a replaceable DLL with matching source and licence material under
`Application_Source/Remote/lib`. Application and installer builds include it and
the notices. Real telescope discovery, model/firmware differences and capture
performance under network load still need hardware testing.

For a hardware smoke test, use a separate test repository. Connect by discovery,
import a few selected captures with metadata, and repeat the import to check for
duplicates. Start live import before a new exposure appears and confirm its
completed capture reaches the archive. Briefly disconnect/reconnect Wi-Fi, stop
live import, then catch up with Search all. Compare a sample archived file's
SHA-256 with a separate copy of the telescope original. Check both themes and
keyboard navigation. Record the model/firmware and results; simulated/loopback
checks cannot substitute for this device test.

### Pause, recovery and simultaneous transfers

Connect again to select additional files while live import runs, or add another
telescope. Each active transfer appears in Activity with independent Pause and
Cancel/Stop controls. Pause all imports affects every active telescope. Settings,
filters, grouping and Analytics remain available; new preference defaults do not
change existing queues. Repository switching waits until jobs finish or pause.

Downloads appear in Import before archiving, with downloading/downloaded/error
status. ETAs use measured bytes and transfer speed (plus the configured bandwidth
limit), and watchers show “Watching for captures” when no files are pending.

Closing the app preserves queues. After a crash or update, choose Resume in
Activity and reconnect the same telescope/source. Recovery verifies completed
archive and cached downloads, restarts incomplete files, and checks duplicates
again. A resumed live watcher keeps its original baseline, including eligibility
for captures made while paused. Telescope passwords needed for recovery use
Windows user encryption. Cancel/Stop discards the queue while keeping completed
archive copies and telescope originals.
