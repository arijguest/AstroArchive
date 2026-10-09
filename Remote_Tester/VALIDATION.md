# Remote tester validation — 9 October 2026

- Version 0.1.1 compiled with the native Windows .NET Framework compiler on
  GitHub Actions windows-2022. Embedded WPF page, AstroArchive logo/icon and a
  separate RemoteTester application manifest/version.
- Native WPF startup and rendering passed in both Light and Dark themes on
  Windows. This reproduces and fixes the original startup failure: the Muted
  text-style key collided with the Muted brush key.
- All 19 transport-independent core checks passed on Windows.
- Windows build and screenshots: https://github.com/arijguest/AstroArchive/actions/runs/37992017129
- Twenty-five focused core checks passed under Mono, including generated FITS
  validation, path guards, stable-file waiting, incomplete-file recovery,
  byte-for-byte transfers, duplicates, restart, session filename collisions,
  changed-source conflict handling and local hash verification.
- FTP checks used a real read-only pyftpdlib server over loopback, with directory
  listing, MDTM, binary transfers and exact source/destination hash comparisons.
  A failed FTP endpoint and subsequent successful reconnection were also checked.
- XAML XML structure and named UI control references validated; text styles use
  keys separate from colour resources.

Windows SMB access, live telescope transfers and hardware capture cadence still
require hardware testing. Native startup/rendering were tested on Windows Server
2022; Windows 10/11 device configuration may differ. This build is unsigned and may
be blocked by Smart App Control. It includes saved startup reports and copyable
errors; the read-only collection script can diagnose failures before CLR startup.
No actual telescope was accessed and no main-app files were changed.

On Windows, `test.ps1` builds/runs the 19 transport-independent checks. The extra
six FTP checks can be run by serving the test source folder on a read-only loopback
FTP server and passing that port as the second argument to CoreTests.exe.
