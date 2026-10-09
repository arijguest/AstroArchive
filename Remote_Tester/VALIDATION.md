# Remote tester validation — 9 October 2026

- Windows x64 GUI executable compiled in C# 5 against Microsoft's .NET Framework
  4.8 reference assemblies, with embedded WPF page, AstroArchive logo/icon and a
  separate RemoteTester application manifest/version.
- Twenty-five focused core checks passed under Mono, including generated FITS
  validation, path guards, stable-file waiting, incomplete-file recovery,
  byte-for-byte transfers, duplicates, restart, session filename collisions,
  changed-source conflict handling and local hash verification.
- FTP checks used a real read-only pyftpdlib server over loopback, with directory
  listing, MDTM, binary transfers and exact source/destination hash comparisons.
  A failed FTP endpoint and subsequent successful reconnection were also checked.
- XAML XML structure and all 37 named UI control references validated.

Native WPF rendering, interaction, Windows SMB access and hardware capture cadence
have not been run in this Linux environment. They require the supplied Windows
executable and the on-page simulator/hardware test instructions. This build is
unsigned. No actual telescope was accessed and no main-app files were changed.

On Windows, `test.ps1` builds/runs the 19 transport-independent checks. The extra
six FTP checks can be run by serving the test source folder on a read-only loopback
FTP server and passing that port as the second argument to CoreTests.exe.
