AstroArchive's first Linux x64 desktop preview, based on Windows 3.1.3. Download the Debian package for Ubuntu/Debian, or the portable tarball. Both include .NET 10; no separately installed .NET runtime is needed.

The desktop includes Repository search and FITS/XISF/SER numeric previews, verified folder/card imports, original-file and stacking exports, Edited working copies and finished images, six analytics reports, and verified archive backups.

### Moving the same archive between Windows and Linux

Keep the entire archive folder, including `.astroarchive`, on the removable drive. Windows 3.1.1–3.1.3 and this Linux preview use the same capture files, index schema, sidecars and Edited records. Close AstroArchive and eject the drive before moving it. Use one writer at a time; exFAT is recommended for straightforward write access on both platforms.

Linux preserves Windows deletion-protection settings. Disable protection on Windows before importing or moving captures on Linux; browsing, exports, working copies and backups remain available. Complete interrupted protection changes on Windows before using the archive on Linux.

### Installation

Verify the downloaded package against `SHA256SUMS`.

```sh
sudo apt install ./astroarchive_3.1.3-preview.1_amd64.deb
astroarchive
```

Alternatively, extract `AstroArchive-3.1.3-preview.1-linux-x64.tar.gz` and launch `AstroArchive` inside its folder. The tarball requires the distribution's ICU, OpenSSL 3, fontconfig and X11 libraries. Optional libcfitsio/libzstd enable their scientific codecs.

### Validation and preview scope

See the attached `VALIDATION.md` for test evidence, operating-system coverage and remaining limits. The release checks exercise all six pages, installed packages, interrupted-import recovery, a removable exFAT filesystem and archive exchanges with the Windows Framework engine in both directions.

This is a preview. Source originals are retained. Direct telescope/live-network UI, plate-solving/rotation UI, native Linux deletion protection and some Windows dialogs are not included. Wayland uses XWayland. There is no automatic Linux updater. Read the attached Linux README for workflows and filesystem guidance.

Required Notice: Copyright 2026 Ari J. Guest (https://arijguest.com)
Licensed under PolyForm Noncommercial 1.0.0; package includes LICENSE and third-party notices.
