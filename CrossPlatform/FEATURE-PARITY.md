# Linux feature parity with Windows 3.1.4

3.1.4 preview adds the principal workflows absent from preview 1. The shared C# engine
remains the authority for classification, target matching, calibration matching,
scientific conversion, import policies, deletion history, and archive records.
This is a workflow comparison, not a claim that Avalonia reproduces every WPF dialog.

| Windows workflow | Linux implementation | Automated evidence |
| --- | --- | --- |
| Archive browse, search and sort | Repository table and shared search syntax; sortable columns | Six-page native smoke, real pointer dispatch, engine search/order regressions |
| Session, target, telescope, format, review and calibration filters | All shared categorical filters plus inclusive exposure bounds | Real filter button, exact-boundary checks, shared filter regressions |
| Detailed capture metadata | All shared editable fields, import-candidate edits, provenance/details; index updates preserve pixels | Invalid batch makes no changes; hashes survive edits; real metadata buttons |
| Review and screen failures | Screen selected source/archive files; unreadable rows retain reasons; repair and retry | Damaged/changed file regressions, real screening buttons, failed-job recovery |
| Local/card imports | Verified copies, preferences, workers, deduplication and retained originals | Engine suite, installed packages, forced process death and exFAT remount |
| Saved telescopes, rename, recovery | Machine-local profiles; shared journaled archive rename and profile recovery | Real profile controls and shared rename regressions |
| Dump inbox | Import archive Dump with per-file detection; Linux retains Dump originals | Real Dump button and hash checks |
| Telescope discovery | Native DWARF/Seestar UDP discovery on active IPv4 interfaces; direct address entry | Actual loopback UDP responder, cancellation and socket reuse |
| DWARF FTP / Seestar SMB | Direct list/selected import, verified staging, sidecars, retry and bandwidth controls | Real read-only FTP/SMB servers, checksum/deduplication/source retention |
| Live import | Stable-file polling, reconnect, stop, durable baseline and completed-file state | Files written while paused are imported after resume; baseline files stay excluded |
| Pause/restart recovery | Cancel pauses safely; Import shows persisted jobs with Resume/Forget | Interrupted local job, failed transfer repair, case-distinct sources, live baseline persistence; native Resume/Forget buttons |
| Saved credentials | Secret Service desktop keyring through libsecret; session-only fallback | Actual unlocked CI keyring store/reload/remove; settings never contain plaintext; missing/locked keyring errors reach the page |
| Local plate solving | ASTAP executable/catalogue/FOV configuration, Linux argument vectors, bounded solve and process-tree cancellation | Independent ASTAP protocol fixture with spaces/Unicode/quotes; original hashes; descendant termination |
| Online solving | Astrometry.net with session/keyring API key; star-coordinate upload, private submission, catalogue matching | Local HTTP API fixture checks authentication, XY FITS table, calibration and absence of image metadata/pixels |
| Identify grouped targets | Shared representative selection, reviewed target override and exact/approximate pointing | Real Identify/Apply buttons; latest metadata retained when applying a saved solution; cross-platform fixture |
| Rotation / mount inference | Shared scientific matcher, observing site, evidence, persisted reports and inferred mount labels; explicit mount assignments survive | Independent rotating synthetic star fields recover known drift; insufficient data remains unknown |
| Automatic import analysis | Off/unknown/all policies for newly imported local captures; off by default | Opt-in solver and rotation test preserves capture hashes |
| FITS / XISF / SER preview | Existing scientific readers and display stretches | Codec and preview engine regressions, native preview commands |
| PNG / TIFF / JPEG / GIF preview | Exact non-interlaced 8/16-bit gray/RGB PNG; numeric TIFF scanlines including floating point; display fallbacks for other encodings | Every PNG filter, CRC corruption, little/big endian floating TIFF pages, installed codec check |
| Scientific raster conversion | Eligible numeric data only; display fallbacks remain excluded; acquisition linearity must be confirmed | Exact samples and scientific FITS output checked independently |
| Sky context | Shared pointing/site/time model, interactive globe, approximate-position evidence, keyboard/drag/zoom/reset | Shared astronomy regressions, real keyboard/pointer navigation, native screenshot |
| Original / stacking export | Files/subs/stacks/both, optional metadata/calibration/session split/conversion/rejected inputs; open processing app | Real export controls, shared calibration/conversion regressions, safe Siril argument vector |
| Edited working copies and finished imports | Working copies, selected image import, recursive finished-folder import, metadata overrides, preview, export, editor and deletion | Real Edited commands; folder paths, exact hashes and overrides |
| Capture deletion and reimport history | Explicit DELETE confirmation, shared transactional deletion/audit/exclusions and explicit reimport permission | Invalid confirmation changes nothing; native engine rollback tests; real Delete/Audit/Allow buttons |
| Backup | Verified folder/ZIP snapshot, canonical portable metadata and restore instructions | Hash-verified backup/restore; platform guard settings omitted |
| Analytics | Six scoped reports, six branded layouts/themes, complete SVG/JSON/PDF/PNG/JPEG exports and GIF/H.264 MP4 stories | Real export button, six-page PDF inspected by independent pdfinfo, every raster page decoded; MP4/GIF independently decoded across all layouts/themes, exact ratios, timing and six-chart stories |
| Appearance / accessibility | Dark/light/system, saved text scale, wrap/scroll layout, keyboard controls | Real preference buttons; normal/compact native screenshots |
| Media playback | Open the selected capture with the system image/video viewer | Safe absolute path argument; requires xdg-utils and an installed viewer |

MP4 story export requires distribution `ffmpeg`; GIF and document exports are self-contained.

## Differences that remain

- **Source removal:** Linux keeps source and Dump originals. Windows can mark an
  exclusively opened, verified file for deletion by its handle. Linux has no
  equivalent unlink-by-open-handle operation. Enabling pathname deletion would
  weaken the existing protection against source replacement during cleanup.
  Use the verified archive/backup and manage source retention outside the app.
- **Protection outside AstroArchive:** Linux's persisted guard blocks capture
  deletion/relocation inside AstroArchive. It cannot enforce Windows NTFS ACLs,
  prevent another application deleting files, or impose immutable flags on exFAT.
  Windows protection records remain untouched; Windows-protected archives still
  require Windows to disable protection before Linux capture writes.
- **Preview presentation:** PNG/GIF/movie playback can use the system viewer.
  The Linux in-app raster preview is a still image; the complete Windows motion
  player, full-resolution viewport and column-layout/accessibility dialogs are
  not replicated. Unsupported TIFF layouts use an explicitly display-only
  decode rather than a reduced-precision scientific export.
- **Automatic analysis/recovery scheduling:** Automatic analysis currently follows
  verified local imports. Live/network imports can be solved/analyzed from the
  Repository after stopping. Linux serializes foreground work; Windows's
  simultaneous activity cards and pause buttons have a different interaction.
- **Application updates:** Install the next Debian package or replace the portable
  application folder. The Windows executable updater is not applicable to Linux.
- **Hardware validation:** Native FTP/SMB and UDP tests use local protocol servers;
  ASTAP/API tests use independent protocol fixtures. They do not certify every
  telescope firmware, installed star catalogue or live Astrometry.net service.
  Real telescopes, physical unplugging, Wayland compositors, external editors,
  desktop file choosers and the user's desktop keyring are not available here.

## Shared-drive invariants

No new archive schema or capture representation is introduced. All pointing,
rotation, metadata facts, Edited overrides, tombstones and sidecars use existing
Windows models. Machine-specific executables, credentials and recovery jobs stay
in Linux configuration storage. Shared-drive CI exchanges original captures and
nested Edited metadata in both directions and checks exact dates and hashes.
ExFAT is exercised through a real mounted/unmounted loopback block device.
Close the application and eject the drive before moving it; use one writer.

Required Notice: Copyright 2026 Ari J. Guest (https://arijguest.com)
