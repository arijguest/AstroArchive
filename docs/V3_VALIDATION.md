# V3 development build validation

Base: `temp` at `41b1a02`. Validated application source: `80cdaaa`.
Application/package version: 3.0.0 / 3.0.0.1.

[Native Windows validation](https://github.com/arijguest/AstroArchive/actions/runs/38004244038)
completed successfully on Windows Server 2022:

- Application compilation with the built-in Windows compiler.
- 454 archive engine tests passed; two optional/platform codec tests were skipped.
- 36 remote integration checks passed, including original-byte equality, cache
  reuse/corruption, duplicate detection, incomplete/changing exposures, metadata
  freshness, custom folder context, live baseline, disconnect recovery and stop.
- 19 transport safeguards, 26 discovery checks and 34 SMB checks passed.
- Real read-only loopback SMB and FTP transfers entered the archive, preserved
  adjacent metadata and independent original hashes, and created no duplicates
  on retry. Source files remained unchanged.
- 58 installer/updater checks passed. The installer was built with SMBLibrary,
  its matching source and licence notices; install, repair and uninstall coverage
  includes these files.
- WPF discovery/connect/import gating, device/profile identity, folder browsing,
  search-all selection and live action checks passed. The dialog rendered in Light
  and Dark themes; existing connected-import and control-state UI checks also passed.

The portable ZIP attached to the chat is compiled using Mono against Microsoft's
.NET Framework 4.8 reference assemblies. The same application source passed the
native Windows build and checks above. That run also offers a Windows-compiled
portable ZIP and installer in its `astroarchive-v3-windows` artifact. Both builds
are unsigned development builds. No public release or update feed was published.

No telescope was accessed by these checks. Model/firmware file access, real
discovery, Wi-Fi behaviour and capture performance under download load still need
hardware testing. The earlier tester successfully listed the user's S50 Pro files.
Verification checks received bytes against local receipts; it does not establish
an independent checksum supplied by the telescope. Windows 10/11 configurations
can differ from the Windows Server runner.
