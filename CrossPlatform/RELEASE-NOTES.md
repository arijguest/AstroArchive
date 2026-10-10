Final audit fixes import preference persistence, rejects invalid preferences before changing
the active solver, recovers invalid saved profiles without a startup crash, and corrects
Debian preview upgrade ordering. Validation now includes 33 native page screenshots.

Linux 3.1.4.1 preview adds workflows missing from the first preview while retaining the
Windows 3.1.4.1 archive format. Both packages include .NET 10.

Added: ASTAP/Astrometry.net target solving, reviewed assignments, rotation and optional
post-import analysis; native telescope discovery, Direct SMB/FTP and live imports;
restart recovery; detailed metadata/filter/review controls; exact supported PNG/TIFF
samples and raster previews; sky context navigation; richer stacking exports; Edited
folder imports/overrides/deletion; capture deletion/audit/reimport; desktop keyring
credentials; capture guard; branded PDF/PNG/JPEG analytics, six layout/theme choices, scoped charts, animated GIF/MP4 stories and saved text scale.

### Moving the same archive between Windows and Linux

Keep the entire archive folder, including `.astroarchive`, on the removable drive. Windows 3.1.1–3.1.4.1 and this Linux preview use the same capture files, index schema, sidecars and Edited records. Close AstroArchive and eject the drive before moving it. Use one writer at a time; exFAT is recommended for straightforward write access on both platforms.

Linux preserves Windows deletion-protection settings. Disable protection on Windows before importing or moving captures on Linux; browsing, exports, working copies and backups remain available. Complete interrupted protection changes on Windows before using the archive on Linux.

### Installation

Verify the downloaded package against `SHA256SUMS`.

```sh
sudo apt install ./astroarchive_3.1.4.1-preview.1_amd64.deb
astroarchive
```

Alternatively, extract `AstroArchive-3.1.4.1-preview.1-linux-x64.tar.gz` and launch `AstroArchive` inside its folder. The tarball requires the distribution's ICU, OpenSSL 3, fontconfig and X11 libraries. Optional libcfitsio/libzstd enable their scientific codecs.

### Validation and preview scope

See the attached `VALIDATION.md` for test evidence, operating-system coverage and remaining limits. The release checks exercise all six pages, installed packages, interrupted-import recovery, a removable exFAT filesystem and archive exchanges with the Windows Framework engine in both directions.

This is a preview. Linux retains source/Dump originals. Its capture guard applies
inside AstroArchive; Windows NTFS protection remains separate. In-app raster previews
are stills; movie/animation playback uses the system viewer. Some Windows viewport,
layout and accessibility dialogs remain different. Wayland uses XWayland. Install
updates manually. Hardware/firmware and external application limits are documented
in the attached README, FEATURE-PARITY.md and VALIDATION.md.

Validation includes real Linux FTP/SMB servers and UDP replies, online/ASTAP protocol
fixtures, known synthetic rotation, exact raster samples, real page and pointer/keyboard
controls, persisted recovery, an actual CI Secret Service keyring, 33 native screenshots,
a six-page PDF, independent decoding of GIF/H.264 stories in all six layouts and both themes, installed packages, exFAT remounts and Windows archive exchanges.

Required Notice: Copyright 2026 Ari J. Guest (https://arijguest.com)
Licensed under PolyForm Noncommercial 1.0.0; package includes LICENSE and third-party notices.
