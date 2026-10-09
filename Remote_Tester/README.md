# AstroArchive Remote tester 0.1.1

Portable Windows 10/11 x64 tester. Requires .NET Framework 4.8. Extract the ZIP
and double-click **AstroArchive.RemoteTester.exe**. No installer, Python, ASCOM,
Alpaca driver, telescope control or AstroArchive installation is needed.

The **Remote** page guides you through hotspot/home-network setup, locating the
telescope IP, inspecting folders and choosing a local download destination.
Use **Start local simulator**, then **Start watching**, to try the entire download
flow with twelve unique slowly written FITS files before connecting hardware.

For DWARF 3/mini, enable Wi-Fi through the phone app, join the hotspot on the PC,
and try FTP at `192.168.88.1`, port 21, anonymous login, folder `/`. Test connection
and enter the appropriate Astronomy / DWARF_RAW session folder. Home-network
addresses come from the device app or your router's connected-device list.
DWARF II FTP support and all model/firmware combinations require hardware testing.

For Seestar, enable **Save Each Frame**, join its hotspot or home LAN, and inspect
`\\<telescope-IP>\` in File Explorer. The documented hotspot IP is `10.0.0.1`;
the documented capture path is `\\10.0.0.1\EMMC Images\MyWorks`. Use the share names
actually exposed by the device. Windows handles SMB credentials; authenticate in
Explorer if needed. The tester does not enable SMB1, guest access or change firewall
or Windows security policies. No file access port needs to be opened on your router.

See the on-page **Manufacturer guide** for current device instructions. **Show PC
addresses** labels this computer's addresses and gateway; it does not claim to
discover the telescope automatically.

Downloads retain original bytes and telescope originals. A separate worker process
keeps the page responsive during blocked network I/O; **Stop** terminates that
worker, leaving completed downloads and an atomically saved journal. Interrupted
files restart from the beginning. A dedicated destination can have only one writer.
Start again with **Include exposures already in the selected folder** enabled to
catch up. Uncheck it to exclude unchanged files present at the start of that run.

Each selected source has a separate numbered/hash folder inside the destination,
preserving the source's relative session paths. Temporary files and receipts live
in `.remote-tester`. Existing files with different bytes are never overwritten.
**Verify downloads** checks saved local hashes; **Export test report** saves transfer
states, hashes and diagnostics. Reports contain addresses and paths, not passwords.
Passwords are not saved in settings; an ephemeral worker configuration holds them
while that worker runs. Normal completion/Stop removes it; a forced UI termination
can leave it in that run's private user-settings folder.

## Limits and validation

Individual uncompressed `.fit`, `.fits`, `.fts` files are supported. Files whose
names begin with `stack` are excluded. JPEGs, videos, compressed FITS and session
sidecars are excluded. No metadata is invented or imported into an archive.

Two stable listings, exact transfer length, before/after remote size/time and FITS
header/data-block structure are checked. This is not a FITS checksum validator or
a guarantee that arbitrary remote writes have finished: same-size rewrites with
unchanged/coarse timestamps cannot be reliably detected. Use immutable individual
exposures and independently compare originals after shooting. Local verification
proves equality with the download receipt, not equality with an independent source.

The tester bounds recursive listing to 50,000 entries; choose a small session folder.
It defaults to 5-second polling, one transfer at a time and 2 MB/s. Stop watching
before verification or changing the connection. It refreshes subfolders during
watching and retries network errors. A new folder selected during a later run is a
separate download scope. Source sidecars are deliberately absent in this prototype.

Settings and reports use `%LOCALAPPDATA%\AstroArchive.RemoteTester`, separately from
AstroArchive. Completed files go only to the local destination you choose. No update
checks or telemetry are made. Manufacturer-guide buttons open a browser only when
clicked. This is an unsigned test build; native Windows WPF UI and telescope model/
firmware interoperability still require testing on Windows and your hardware.

## Hardware test

1. Test the simulator and **Verify downloads** first.
2. Test file listing on the actual telescope; choose a session folder.
3. Shoot comparable sessions with downloads off/on, recording capture cadence and
   phone-app responsiveness. Check downloads appear and backlog remains bounded.
4. Disconnect the PC Wi-Fi temporarily, reconnect and confirm automatic recovery.
5. Stop/restart watching; confirm completed files stay and missing files catch up.
6. Compare every downloaded SHA-256 to originals independently copied after capture.
7. Export the report. Record telescope model/firmware, network mode and any errors.

## Build

From a Windows PowerShell terminal in this directory:

```powershell
.\build.ps1
```

Output: `dist\AstroArchive.RemoteTester.exe`. Only the built-in .NET Framework C#
compiler and reference libraries are needed. The shared palette is compiled from
`Application_Source\Theme.cs`; the main application is not modified.

## If Windows blocks the tester or it fails to open

This test executable is unsigned. Smart App Control can block an unsigned, unknown
application. A startup fix does not remove that policy block; a trusted publisher
signature is needed. Do not disable protection or install a self-signed root
certificate for this tester. Recent Windows updates may allow Smart App Control to
be re-enabled in Windows Security → App & browser control → Smart App Control.

Version 0.1.1 saves caught startup errors to
`%LOCALAPPDATA%\AstroArchive.RemoteTester\startup-error.txt` and shows a selectable
error window with **Copy details**. The log includes the full exception, runtime
version and architecture. If the CLR or Windows policy prevents the process from
starting, app-level logging cannot run. Use **Collect-diagnostics.ps1** (right-click
→ Run with PowerShell, if permitted by your existing policy) to save a read-only
report to the Desktop. It checks framework version, executable signature and recent
matching Windows events without running the tester or changing security settings.
It opens the report in Notepad so you can copy the text.

If scripts are also blocked, do not change execution policy. Open Event Viewer →
Windows Logs → Application and Application and Services Logs → Microsoft → Windows
→ CodeIntegrity → Operational; look for AstroArchive.RemoteTester entries near the
attempted launch time. Event details can be copied with the **Copy** action.
