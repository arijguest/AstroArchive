# AstroArchive Linux 3.1.4 preview — release validation

Source: branch `codex/linux-release`, commit `@SOURCE_SHA@`.
Date: 10 October 2026. Version: `3.1.4-preview.2`, Linux x64.
Build: self-contained Ubuntu 22.04 artifacts from the release tag, published only after all workflow gates pass.
Review: https://github.com/arijguest/AstroArchive/pull/41

## Automated checks

| Area | Coverage |
| --- | --- |
| Shared engine | @ENGINE_TESTS@ Linux engine checks pass; @ENGINE_SKIPS@ Windows-specific/optional-codec checks skip. Existing Windows application, installer and analytics pipelines run separately. |
| Desktop | All @DESKTOP_TESTS@ Linux tests pass: actual pointer input, six page workflows, cancellation and cleanup, archive locking, source case distinctions, symlinks/dangling links, modified source/archive rejection, settings permissions/corruption, corrupt Edited project isolation, SQLite rollback, casing compatibility and backup restoration. |
| Native rendering | The packaged executable drives all six real pages under X11/Xvfb, checks resulting files/data and saves 21 screenshots at 1220×850 and 1100×720. Advanced workflow screenshots also cover metadata, solving, telescope transfers, recovery, Edited tools, sky context, solver settings and branded analytics options. Native rendering is checked in addition to headless events; a native font failure found during development was corrected with the bundled Inter font. |
| Package independence | Self-contained tarball and Debian payload run import/export/working-copy/backup/reopen checks without invoking an SDK. CI installs the Debian package with apt and checks its executable, desktop entry and icon. |
| Abrupt interruption | The installed app starts an importing child process, waits for one durable commit, kills it, reopens the archive and imports the remaining capture without losing or duplicating the committed file. |
| Removable filesystem | An ephemeral exFAT loopback drive is formatted, mounted, imported, unmounted/remounted, updated and verified. A read-only remount rejects opening for writes with the expected filesystem error. Every pipeline propagates failures. Both CI runners used the FUSE exFAT driver because their kernels omit the exFAT module. No user drive is used. |
| Shared archive | Real fixtures travel Linux → Windows Framework → Linux and Windows Framework → Linux → the same Windows path with its old working-index cache retained. Verify hashes, fresh index, sidecars, pointing, detailed metadata, Windows timezone IDs, capture metadata refiling, nested Edited paths/overrides, exact millisecond timestamps and duplicate detection. Archive/source folders contain spaces and Unicode. |
| Telescope protocols | Actual read-only local FTP and SMB servers exercise list/download, sidecars, verified retry and source retention. UDP tests exercise actual Seestar discovery replies, cancellation and socket reuse. Live-import tests preserve baseline/completed state across pause and restart. |
| Solving and rotation | Independent ASTAP and local Astrometry.net protocol fixtures check Linux arguments, descendant cancellation, private star-coordinate upload, target matching and persisted pointing. Rotating synthetic star fields recover a known rotation rate; explicit mount metadata remains authoritative. |
| Credentials | An actual unlocked Secret Service keyring stores, reloads and removes credentials; private settings hold opaque references. Session-only keys remain in memory. |
| Scientific raster and analytics | Exact 16-bit PNG samples survive all five filters; corrupt CRCs are rejected. Little/big endian floating TIFF pages retain exact values. Installed packages independently decode exported FITS samples. PDF exports are inspected by pdfinfo; six PNG report pages are decoded. Independent ffmpeg/Pillow decode checks MP4/GIF across six layouts and both themes, exact aspect ratios, color/orientation, frame counts, timing, full native resolutions and complete six-chart stories. |
| Integrity and packaging | SHA-256 manifests, complete archive backups, runtime/NuGet licence notices, stable locked dependencies and original source retention. |

CI evidence:
- Linux packages, Ubuntu 22.04/24.04, exFAT and both Windows exchanges: @CI_URL@
- Existing Windows application/installer regressions: https://github.com/arijguest/AstroArchive/pull/41/checks
- Existing Windows analytics checks: https://github.com/arijguest/AstroArchive/pull/41/checks

## What the Windows/Linux compatibility checks protect

The current Windows 3.1.4 Framework engine is used in CI; the shared archive format remains compatible with Windows 3.1.1–3.1.4. The release keeps the existing SQLite schema and Windows JSON separator/date convention. Linux converts backslashes at the serialization boundary. Shared paths resolve established Windows folder casing while refusing ambiguous folders and links outside the archive. Linux writes its durable index directly to the drive and rotates the existing cache identity so released Windows 3.1.1–3.1.4 reloads the current drive index rather than a stale local copy.

An actual Windows reader caught a date encoding problem during development: Microsoft JavaScriptSerializer requires escaped JSON slashes around Microsoft date values. The corrected writer emits the established encoding, with a golden regression for it and an exact millisecond check in each exchange. ISO date strings in untyped metadata remain strings. A corrupt Edited record is reported separately while healthy projects and archived captures remain visible.

Close the application on each machine and eject/unmount the drive before moving it. One writer at a time is required. Keep `.astroarchive` with the capture/Edited files. Windows protection settings are preserved; protected capture imports/refiling require disabling protection on Windows first. The app does not alter NTFS permissions automatically.

## Coverage limits

CI exercises Ubuntu 22.04 and 24.04 x64; local engine and packaged native checks also run on Debian 13.6 x64. The same application source also passes installed-executable import/export/backup/reopen and forced-crash recovery checks on Debian 13.6. Debian 12 is an intended target supported by the dependency range, but has not had a dedicated CI desktop run. Wayland is supported through XWayland; compositor-specific behaviour, graphics hardware and real physical drive disconnection were not exercised. The removable-drive test uses genuine exFAT through FUSE and real unmount/remount operations on a loopback block device. The native kernel exFAT driver was not directly exercised.

Generated FITS and the existing engine fixtures cover common archive behaviours and scientific codecs, not every telescope/media file or very large real collection. Native file-chooser dialogs and third-party editors were not exhaustively exercised. The native smoke drives actual page commands; the pointer test separately checks real mouse dispatch. There is no claim of full Windows UI/feature parity or complete hardware certification.

See README.md and FEATURE-PARITY.md for workflows, remaining platform differences, dependencies and shared-drive guidance. This is a Linux preview, not a replacement for the Windows stable release.

`AstroArchive-Linux-page-screenshots.zip` contains the 21 native CI screenshots. `Linux-validation-logs.zip` contains engine, desktop TRX, installed package, exFAT, native network, keyring, UI and independent PDF inspection logs. Package hashes match the CI-generated SHA256SUMS; the published manifest additionally covers the evidence attachments.
