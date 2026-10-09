# Remote tester validation — 9 October 2026

- Version 0.1.2 compiled with the native Windows .NET Framework compiler on
  GitHub Actions windows-2022. Embedded WPF page, AstroArchive logo/icon and a
  separate RemoteTester application manifest/version.
- Native WPF startup and rendering passed in both Light and Dark themes on
  Windows. This reproduces and fixes the original startup failure: the Muted
  text-style key collided with the Muted brush key.
- All 19 transport-independent core checks and 26 discovery checks passed on
  Windows. Discovery checks include independent protocol byte fixtures, malformed
  and unrelated responses, subnet scope, duplicate replies, real UDP loopback
  responders for both families, cancellation and socket reuse after cancellation.
- Native UI checks also exercised discovered-device selection, preserving a
  Seestar share/session path while replacing its host, and switching to DWARF FTP.
- Discovery uses DWARF protobuf ping/echo on UDP 9900 and Seestar scan_iscope on
  UDP 4720. It is explicit, limited to a selected active IPv4 Wi-Fi/Ethernet subnet,
  and does not change telescope settings or start downloads. Discovery alone does
  not verify FTP/SMB availability or credentials.
- The Windows SMB settings helper is read-only. Failed connection tests include
  the original Windows error code and Seestar guidance.
- Windows build and screenshots: https://github.com/arijguest/AstroArchive/actions/runs/37994071556
- Twenty-five focused core checks passed under Mono, including generated FITS
  validation, path guards, stable-file waiting, incomplete-file recovery,
  byte-for-byte transfers, duplicates, restart, session filename collisions,
  changed-source conflict handling and local hash verification.
- FTP checks used a real read-only pyftpdlib server over loopback, with directory
  listing, MDTM, binary transfers and exact source/destination hash comparisons.
  A failed FTP endpoint and subsequent successful reconnection were also checked.
- XAML XML structure and named UI control references validated; text styles use
  keys separate from colour resources.

Actual telescope discovery across Wi-Fi/router configurations, Windows SMB access,
live telescope transfers and hardware capture cadence still
require hardware testing. Native startup/rendering were tested on Windows Server
2022; Windows 10/11 device configuration may differ. This build is unsigned and may
be blocked by Smart App Control. It includes saved startup reports and copyable
errors; the read-only collection script can diagnose failures before CLR startup.
No actual telescope was accessed and no main-app files were changed.

On Windows, `test.ps1` builds/runs the 19 transport-independent checks and 26
discovery checks. The extra
six FTP checks can be run by serving the test source folder on a read-only loopback
FTP server and passing that port as the second argument to CoreTests.exe.

Discovery protocol references (used for wire format, not copied implementations):
- https://github.com/alikh31/dwarflab-sdk/blob/main/proto/ble.proto
- https://github.com/alikh31/dwarflab-viewer/blob/main/src/main/services/discovery-service.ts
- https://github.com/irjudson/seestar-api/blob/main/seestar/discovery.py

The attached portable EXE is compiled against Microsoft's .NET Framework 4.8
reference assemblies using Mono. The same application source passed native Windows
compilation and startup validation in the run above. A Windows-compiled package is
also available as that run's remote-tester-windows artifact. Neither is signed.
