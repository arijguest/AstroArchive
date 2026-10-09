# Remote tester 0.2.1 validation

0.2.1 local compilation and 19 SMB configuration/path checks passed. Native Windows
validation of the expanded discovery-selection regression checks is pending.

The following records the preceding 0.2.0 validation:

Native Windows run: https://github.com/arijguest/AstroArchive/actions/runs/37997859442
Source commit: 7b4aa09.

- Compiled with the native Windows .NET Framework C# 5 compiler on windows-2022.
- Nineteen transfer-engine checks, 26 discovery checks and 24 direct-SMB checks
  passed (69 distinct checks; the nine SMB configuration checks also run separately).
- Direct SMB used a real loopback-only, read-only SMB2 guest server. Checks cover
  metadata and folder listing, exact FITS bytes and independent source hashes,
  read-only streams, source scope, reconnect, stable/incomplete files, restart,
  unchanged originals, corrupted local copies and offline receipt verification.
- Native WPF startup, discovered-device selection and source-change/start guards
  passed. Connection, Downloads and Help pages rendered in Light and Dark themes
  (six screenshots). Busy operations expose Cancel; downloads expose Stop.
- The one-click demo ran through the actual EXE's worker process on Windows. It
  copied completed generated exposures, compared bytes against independent originals,
  stopped its worker and locally verified its receipts.
- XAML XML, named UI controls and style/brush resource-key separation were checked.
  The UI and transfer worker contain no PowerShell calls or policy modifications.
- SMBLibrary 1.5.8 is the unmodified NuGet .NET 4.0 DLL. It is a separate replaceable
  dependency; matching source, notices and LGPL/GPL texts are included in ThirdParty.
- The Windows fixture corrects an impacket 0.13.1 read-only binary-mode issue in its
  own opened handles; the production client is unchanged by that test adaptation.

Actual telescope discovery, Seestar guest-share availability, SMB/FTP interoperability
across model/firmware combinations and capture cadence under download load still
require hardware testing. No actual telescope was accessed. Native checks ran on
Windows Server 2022; Windows 10/11 configuration can differ. This build is unsigned.
Guest telescope SMB access is unsigned for that session, with Windows policies left
unchanged. Verification proves equality with the saved local receipt, not an
independent hardware original. The main AstroArchive application was not modified.

The attached portable EXE is compiled against Microsoft's .NET Framework 4.8
reference assemblies using Mono. The same application source passed native Windows
compilation, functional worker checks and UI validation in the run above. A native
Windows-compiled package is also available as that run's remote-tester-windows
artifact. Neither build is signed.
