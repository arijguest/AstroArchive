# AstroArchive Remote tester 0.2.0

Portable Windows 10/11 x64 client. Requires .NET Framework 4.8. **Extract the entire
ZIP to one folder**, then run **AstroArchive.RemoteTester.exe**. Keep its companion
SMBLibrary.dll and configuration file beside the EXE. No installer, Python, ASCOM,
Alpaca driver or AstroArchive installation is needed by users.

This version has separate **Connection**, **Downloads**, and **Help & diagnostics**
views using AstroArchive's shared light/dark palette. A persistent action bar keeps
Start/Stop available. Connection failures give a next step, selectable technical
information and **Copy details**. Start becomes available after a successful folder
connection; changing the source requires reconnecting. Appearance follows System,
Light or Dark. Setup instructions and connection options are available when needed.

## Quick start

1. In the telescope's phone app, connect it to your home Wi-Fi using STA mode.
   Connect this PC to the same LAN (Wi-Fi or Ethernet). Alternatively join the
   telescope's hotspot on the PC. Keep shooting in the phone app.
2. Choose Seestar or DWARF, and your network connection. Choose the PC network
   adapter, then **Find telescopes**. Search takes about eight seconds.
3. Select your telescope and choose **Connect to selected telescope**. This fills
   in the address and tests the default capture folder. Manual addresses also work.
4. Double-click a folder, or select it and press Enter, to open the desired session.
   **Storage root** returns to the storage share/FTP root; **Up one folder** goes up.
   The capture path shows the folder that will be watched, including its subfolders.
5. Choose a dedicated download folder on this PC. Decide whether to also copy
   existing exposures. Start with the default 5-second polling and 2 MB/s limit;
   these can be changed under Download options.
6. Choose **Start downloads**. The Downloads view shows downloaded, waiting/copying,
   and attention counts alongside each file's status. Stop retains completed files.

**Try a local demo** creates twelve unique, slowly written FITS exposures and starts
copying them to a separate demo destination automatically. No telescope/server is
needed. The demo files live separately under the tester's private user-data folder.
To test an existing local source, choose Local folder and browse to it.

## Seestar: direct file access without Windows policy changes

**Direct telescope access (recommended)** uses an independent SMB2/3 client to read
Seestar's guest share over TCP 445. It does not use Windows UNC mounting, Credential
Manager or the Windows SMB client, and makes no PowerShell, registry, firewall or
security-policy changes. Windows guest/signing settings can remain as they are.
Guest access remains unsigned for this telescope session; use your trusted home
network or telescope hotspot. The telescope must provide a compatible guest share.
This does not bypass authentication or signing requirements imposed by its firmware.

Enable **Save Each Frame** in the Seestar app. The documented hotspot address is
`10.0.0.1`, storage share `EMMC Images`, and capture folder `MyWorks`. The default path
is `\\10.0.0.1\EMMC Images\MyWorks`; discovery replaces its IP automatically. Use the
share/folder names actually exposed by your firmware. If MyWorks is not found, try
Storage root and enter the capture folder from the list. The Wi-Fi password is
separate from file-share authentication; direct mode uses the guest share.

**Windows file access** remains available as a compatibility option. It uses the
existing Windows file-sharing connection; a source that opens in File Explorer can
be used with that mode. Guest-access/signing restrictions still apply to Windows
mode. An incorrect username/password error in that mode offers switching to Direct
access; no machine-wide security workaround is built into the tester.

## DWARF

For DWARF 3/mini, enable Wi-Fi through the phone app. The documented DWARF hotspot
address is `192.168.88.1`, with FTP port 21 and anonymous login. Start with `/`, then
enter Astronomy / the relevant DWARF_RAW session. FTP options are under the capture
folder's connection options. Verify FTP availability on your model and firmware.
DWARF II and all firmware combinations require hardware testing. Keep capturing
through the manufacturer's app. No telescope control requests are issued.

## Discovery and manual addresses

Discovery sends native DWARF ping/echo probes on UDP 9900 and Seestar scan_iscope
probes on UDP 4720 to the selected adapter's IPv4 subnet broadcast address. It
searches explicitly when you click Find telescopes, not continuously in the
background. It does not scan all IPs or mistake an Alpaca bridge for a file source.
Multiple telescopes are listed separately; repeated replies are collapsed. Replies
outside the selected subnet are ignored. The reply's sender IP is used rather than
an advertised address on a different network. Discovery alone does not verify
FTP/SMB access; Connect to selected telescope performs that separate test.

After changing Wi-Fi, use **Refresh**. Guest Wi-Fi, client isolation, separate VLANs,
unsupported firmware or firewall restrictions can prevent discovery. Allow the app
on your trusted private network if Windows asks; no router port forwarding is
needed. Close other DWARF discovery apps if they occupy UDP 9900. Cancel closes the
search promptly. Manual IP entry still works if broadcasts are unavailable.

Under **Need help finding the address?**, use the known hotspot address or view PC
addresses/gateway. On a home LAN, find the telescope IP in its phone app or the
router's connected-device list; the PC's own IP is not the telescope's. The Help
view includes network/model setup and current manufacturer guidance.

## Download behavior and limits

Files retain original bytes and telescope originals. A worker process keeps the
page responsive during blocked network I/O. Two unchanged listings, exact transfer
length, before/after remote size/time, and FITS header/data blocks are checked.
Local SHA-256 receipts are recorded after successful copies. Verification uses
local receipts and can run with the telescope offline; it is not an independent
comparison with telescope originals or a FITS checksum validator. Same-size rewrites
with unchanged/coarse timestamps cannot be detected reliably. Use immutable
exposures and independently compare originals after shooting.

Individual uncompressed `.fit`, `.fits`, `.fts` files are supported. Names beginning
with `stack`, compressed FITS, JPEGs, videos and sidecars are excluded. No metadata
is invented or imported into an archive. Recursive inventory is limited to 50,000
entries; choose a small session folder. One file transfers at a time. New subfolders
are refreshed while watching; failed network operations retry automatically.

Each source has its own hash-named destination folder preserving relative session
paths. `.remote-tester` contains temporary files and atomic receipts. Interrupted
copies restart from the beginning. Existing different files are never overwritten.
A destination allows only one active download writer. Re-enable copying existing
exposures to catch up after stopping; new-only mode excludes the unchanged baseline.

Settings, runs and demos live under `%LOCALAPPDATA%\AstroArchive.RemoteTester`,
separately from AstroArchive. Reports include addresses, paths, access mode, hashes
and diagnostics, not passwords. FTP passwords are absent from saved settings; an
active worker's temporary configuration contains them and is deleted on normal
completion/Stop. A forced UI termination can leave that configuration behind.
No telemetry or update checks are made. Guide buttons open links only when clicked.

## Hardware testing

Try the demo, then verify its saved files. Test actual discovery and confirm its IP
against the phone app. Test listing/copying on the telescope. Compare sessions with
downloads off/on, recording capture cadence and phone-app responsiveness. Check Wi-Fi
reconnect, cancellation, Stop/restart and newly created session folders. Independently
copy originals after capture and compare every hash. Export a report and record the
model, firmware and network mode. Native tests use simulated devices; real telescope
interoperability and copying while capture continues still need hardware validation.

## Build and developer checks

On Windows, run `build.ps1` to produce `dist\AstroArchive.RemoteTester.exe` with its
companion files. It uses the built-in .NET Framework compiler and the checked-in,
version-pinned SMBLibrary dependency; NuGet/Visual Studio are not needed to build.
The shared palette comes from `Application_Source\Theme.cs`; the main app is unchanged.
`test.ps1` runs core, discovery and SMB configuration checks. Optional `test-smb.ps1`
uses Python/impacket 0.13.1 for a loopback-only, read-only SMB2 guest server and checks
actual transfer bytes, stable/incomplete files, restart and local verification.

SMBLibrary 1.5.8 is dynamically linked, unmodified, under LGPL-3.0-or-later. The ZIP
includes its notices, LGPL/GPL texts, matching source archive and replacement/build
information in ThirdParty. Its DLL can be replaced with a compatible build.

## Startup failures

This test build is unsigned and may be blocked by Smart App Control. Do not disable
protection or install a self-signed root for it. Caught startup errors are saved to
`%LOCALAPPDATA%\AstroArchive.RemoteTester\startup-error.txt` and shown in a selectable
window with Copy details. If Windows prevents the process starting, app logging
cannot run. The included Collect-diagnostics.ps1 gathers framework/signature/recent
Windows events without launching the tester or changing security settings. Run it
only if allowed by your existing policy; otherwise use Event Viewer and copy details.
