# AstroArchive Linux preview 1 — release validation

Source: branch `codex/linux-release`, commit `@SOURCE_SHA@`.
Date: 10 October 2026. Version: `3.1.3-preview.1`, Linux x64.
Build: self-contained Ubuntu 22.04 artifacts from the release tag, published only after all workflow gates pass.
Review: https://github.com/arijguest/AstroArchive/pull/41

## Automated checks

| Area | Coverage |
| --- | --- |
| Shared engine | @ENGINE_TESTS@ Linux engine checks pass; @ENGINE_SKIPS@ Windows-specific/optional-codec checks skip. Existing Windows application, installer and analytics pipelines run separately. |
| Desktop | All @DESKTOP_TESTS@ Linux tests pass: actual pointer input, six page workflows, cancellation and cleanup, archive locking, source case distinctions, symlinks/dangling links, modified source/archive rejection, settings permissions/corruption, corrupt Edited project isolation, SQLite rollback, casing compatibility and backup restoration. |
| Native rendering | The packaged executable drives all six real pages under X11/Xvfb, checks resulting files/data and saves 12 screenshots at 1220×850 and 1100×720. All six compact screenshots were inspected locally. |
| Package independence | Self-contained tarball and Debian payload run import/export/working-copy/backup/reopen checks without invoking an SDK. CI installs the Debian package with apt and checks its executable, desktop entry and icon. |
| Abrupt interruption | The installed app starts an importing child process, waits for one durable commit, kills it, reopens the archive and imports the remaining capture without losing or duplicating the committed file. |
| Removable filesystem | An ephemeral exFAT loopback drive is formatted, mounted, imported, unmounted/remounted, updated and verified. A read-only remount rejects opening for writes with the expected filesystem error. Every pipeline propagates failures. Both CI runners used the FUSE exFAT driver because their kernels omit the exFAT module. No user drive is used. |
| Shared archive | Real fixtures travel Linux → Windows Framework → Linux and Windows Framework → Linux → the same Windows path with its old working-index cache retained. Verify hashes, fresh index, sidecars, capture metadata refiling, nested Edited paths/overrides, exact millisecond timestamps and duplicate detection. Archive/source folders contain spaces and Unicode. |
| Integrity and packaging | SHA-256 manifests, complete archive backups, runtime/NuGet licence notices, stable locked dependencies and original source retention. |

CI evidence:
- Linux packages, Ubuntu 22.04/24.04, exFAT and both Windows exchanges: @CI_URL@
- Existing Windows application/installer regressions: https://github.com/arijguest/AstroArchive/pull/41/checks
- Existing Windows analytics checks: https://github.com/arijguest/AstroArchive/pull/41/checks

## What the Windows/Linux compatibility checks protect

The current Windows 3.1.3 Framework engine is used in CI; the archive engine and format are unchanged from Windows 3.1.1. The release keeps the existing SQLite schema and Windows JSON separator/date convention. Linux converts backslashes at the serialization boundary. Shared paths resolve established Windows folder casing while refusing ambiguous folders and links outside the archive. Linux writes its durable index directly to the drive and rotates the existing cache identity so released Windows 3.1.1–3.1.3 reloads the current drive index rather than a stale local copy.

An actual Windows reader caught a date encoding problem during development: Microsoft JavaScriptSerializer requires escaped JSON slashes around Microsoft date values. The corrected writer emits the established encoding, with a golden regression for it and an exact millisecond check in each exchange. ISO date strings in untyped metadata remain strings. A corrupt Edited record is reported separately while healthy projects and archived captures remain visible.

Close the application on each machine and eject/unmount the drive before moving it. One writer at a time is required. Keep `.astroarchive` with the capture/Edited files. Windows protection settings are preserved; protected capture imports/refiling require disabling protection on Windows first. The app does not alter NTFS permissions automatically.

## Coverage limits

CI exercises Ubuntu 22.04 and 24.04 x64; local engine and packaged native checks also run on Debian 13.6 x64. The same application source also passes installed-executable import/export/backup/reopen and forced-crash recovery checks on Debian 13.6. Debian 12 is an intended target supported by the dependency range, but has not had a dedicated CI desktop run. Wayland is supported through XWayland; compositor-specific behaviour, graphics hardware and real physical drive disconnection were not exercised. The removable-drive test uses genuine exFAT through FUSE and real unmount/remount operations on a loopback block device. The native kernel exFAT driver was not directly exercised.

Generated FITS and the existing engine fixtures cover common archive behaviours and scientific codecs, not every telescope/media file or very large real collection. Native file-chooser dialogs and third-party editors were not exhaustively exercised. The native smoke drives actual page commands; the pointer test separately checks real mouse dispatch. There is no claim of full Windows UI/feature parity or complete hardware certification.

See README.md for the supported workflow subset, dependencies and shared-drive guidance. This is a Linux preview, not a replacement for the Windows stable release.

`AstroArchive-Linux-page-screenshots.zip` contains the 12 native CI screenshots. `Linux-validation-logs.zip` contains engine, desktop TRX, installed package and exFAT logs. Package hashes match the CI-generated SHA256SUMS; the published manifest additionally covers the evidence attachments.
