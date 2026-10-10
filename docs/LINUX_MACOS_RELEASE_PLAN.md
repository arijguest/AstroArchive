# AstroArchive Linux and macOS release plan

Prepared 10 October 2026. Repository inspected at commit `9a20dc2709ab69fe04c7c9e2dcf12f095a2e0ba7`, package version `2.1.15.1`.

This is a planning deliverable. It records observed source constraints, recommended decisions, implementation work, and release evidence that must be produced. It does not claim that a Linux or macOS build exists, that platform tests have passed, or that accounts, certificates, hardware, personnel, and budget have been secured. No release version or launch date is reserved by this document. Platform policies must be checked again when implementation starts and at release freeze.

## 1. Release strategy and main conclusion

Linux and macOS releases require an application port. The current product cannot become a supported native application on either platform by changing installer settings. The UI uses WPF; the runtime is .NET Framework 4.8; database loading, file identity, source deletion, archive protection, processor detection, credential storage, and updates contain Windows assumptions.

The recommended direction is a shared, independently testable C# archive/scientific core, explicit platform services, and an Avalonia desktop application on .NET 10 LTS. Retain the Windows WPF application during the transition, sharing core behavior where practical. Use the new desktop shell on Windows as an additional comparison target before deciding whether to replace WPF. Give Windows users normal maintenance releases while preventing unfinished port work from entering its publication path.

Ship a dependable archive manager first. Archive/import/export integrity, repository portability, metadata fidelity, verified backups, responsive browsing, scientific still previews, and supported processor handoffs are the first-release contract. Automatic source deletion and Windows-style filesystem deletion protection are excluded from the first Unix release unless their safety can be independently demonstrated. Video, RAW decoding, sandboxed distribution, and self-updating can be staged without weakening the archive contract.

The critical path is:

1. Prove the UI/runtime choice with real packaged applications on Mac and Linux.
2. Define portable archive, JSON, filesystem, and locking contracts.
3. Extract the core and preserve existing Windows behavior.
4. Implement platform services and complete repository round-trip tests.
5. Complete end-to-end desktop workflows and scientific previews.
6. Package, sign, notarize, and verify actual downloadable artifacts.
7. Run a controlled beta on disposable/copied archives, then release candidates.
8. Promote independently qualified platform artifacts using one coordinated release process.

The product's success criterion is not merely that a window opens. A Windows-created archive must retain its images, identities, corrections, sidecars, Edited projects, and deletion history after legitimate use on Linux and macOS, including failures and later reopening on an explicitly supported Windows version.

## 2. What the source inspection establishes

| Observed constraint | Evidence | Release implication |
| --- | --- | --- |
| Windows x64, C# 5, .NET Framework 4.8, direct `csc.exe` build | `Application_Source/build.ps1`, `Application_Source/README.md` | Introduce SDK-style projects and a modern runtime; the current build is not portable. |
| WPF loads `MainWindow.xaml` at runtime and uses a large partial `MainUi` | `Application_Source/App.cs`, UI source files | XAML, controls, event handlers, dialogs, dispatcher use, bindings, styling, and tests need deliberate migration. |
| 220 top-level application C# files; 38 end in `Tests.cs`; 95 contain `System.Windows` references | Simple source inventory at the inspected commit | The UI dependency is substantial. These counts describe files, not effort or a validated component boundary. |
| SQLite is P/Invoked from Windows `winsqlite3.dll` with System32-only loading | `Application_Source/Repository.cs` | Supply a supported cross-platform provider/native SQLite distribution and verify its native binaries. |
| Local working database plus portable `.astroarchive/index.sqlite` snapshots | `Repository.cs`, `ArchiveBackup.cs` | Moving an archive and reopening old local caches can create stale metadata; define cache freshness and recovery explicitly. |
| Repository mutex and local cache keys derive from lowercased absolute paths | `Repository.cs` | Case-sensitive paths, mount aliases, and separate hosts cannot use the current identity/locking model unchanged. |
| `Util.Within` uses case-insensitive string comparisons; generated relative paths use `Path.Combine` | `Model.cs`, `Repository.cs`, `ImportEngine.cs`, `EditedWorkspace.cs` | Windows path separators and Unix case sensitivity are direct correctness and containment risks. |
| Source deletion hashes protected open handles and uses Windows disposition APIs | `SourceCleanup.cs` | Unix pathname deletion is not equivalent; a naive port would weaken the existing guarantee. |
| NTFS protection uses SIDs, deny ACLs, and recovery logs; active protection prevents Unix opening | `ArchiveProtection.cs` | Handle existing protected archives before promising migration; ordinary read-only mode bits do not replace deny-delete ACLs. |
| WIC raster readers and WPF `MediaElement` | `RasterReader.cs`, `RasterHeaders.cs`, `MotionPreviewUi.cs` | Raster scientific decoding and media playback need new implementations; a UI toolkit alone does not supply them. |
| Optional codecs use Windows library names/loading | `FitsAssets.cs`, `XisfReader.cs` | Build/package/load CFITSIO and Zstandard for each RID; validate ABI, architecture, and library search rules. |
| External detection uses registry and Windows paths; Photoshop uses COM | `ExternalApps.cs` | Provide native platform adapters and a platform-specific capability matrix. |
| Astrometry.net key is protected with user-scoped DPAPI | `PlateSolve.cs`, `Model.cs` | Use Keychain/Secret Service or a session-only fallback; copied Windows secrets cannot be decrypted on Unix. |
| Timezone validation explicitly expects Windows IDs | `MetadataEditing.cs` | Store interoperable timezone evidence and correctly map Windows/IANA identifiers. |
| USB discovery uses Windows volume/device-control APIs | `TelescopeConnections.cs` | Discover mounted volumes with platform APIs; avoid treating every fixed disk as a telescope. |
| Windows updater validates schema 1 and specific `.exe` names | `Installer/Updates.cs`, `ReleaseMonitor.cs`, `scripts/build-release.ps1` | Keep its compatibility feed intact; introduce platform-specific metadata separately. |
| `main` pushes can publish Windows releases and mark them latest | `.github/workflows/windows.yml` | Port branches and cross-platform publishing need coordination; a documentation or refactor push can otherwise touch publication. |
| Windows engine, UI, raster, installer, updater, and release-report checks exist | `Application_Source/test.ps1`, `Installer/test.ps1`, `scripts/verify-release-report.py` | Reuse useful fixtures/semantics, but Windows-only test success is not evidence for Mac/Linux. |
| Source-available PolyForm Noncommercial licensing and separately licensed catalogue data | `LICENSE`, `LICENSING.md`, catalogue notices | Distribution channels and bundled dependencies need correct notices and compatible terms; do not advertise AstroArchive as OSI open source. |

Important positive starting points: content hashes already identify files; verified copying is separate from previews; FITS/XISF/SER processing contains reusable C#; `IAssetReader`, `PixelImage`, `PreviewData`, and WPF-independent preview geometry provide useful seams; many engine regressions already have generated fixtures. Preserve these strengths during extraction.

## 3. Proposed supported platforms and distribution scope

These are proposed AstroArchive commitments, not declarations that the application currently runs on them. Dependency support and AstroArchive qualification are separate.

| Target | Proposed first release | Qualification required |
| --- | --- | --- |
| Linux x64 | Ubuntu 24.04 and 26.04 LTS; Debian 13; Fedora 44 if still supported at freeze | Clean install, real GNOME/KDE sessions, relevant package dependencies, native library loading, workflow and removable-drive checks. Reduce the public list if capacity is insufficient. |
| Other glibc Linux distributions | Compatibility/community support initially | Publish ABI/dependency requirements and tested combinations; do not promise all distributions from one AppImage test. |
| Linux ARM64 | Later milestone unless funded and equipped from the start | Real ARM64 desktop tests, decoder builds, external applications, and package parity. |
| macOS Apple Silicon | Native `osx-arm64`; proposed macOS 14, 15, and 26 where still supported by dependencies at freeze | Signed/notarized downloads on real Macs, APFS and removable drives, permission denial, Retina, sleep/wake, and native processor integration. |
| macOS Intel | Separate `osx-x64` download only if native Intel hardware and compatible supported OS are available | Intel execution and native dependencies; Rosetta-only testing cannot establish an Intel support claim. |
| Older macOS | Excluded initially | No unsupported-runtime workaround as the default product path. Revisit only with an explicit maintenance commitment. |
| musl Linux, 32-bit, WSL, headless/server use | Excluded from the first desktop support contract | Separate packaging and qualification would be needed. A useful future headless verification tool can be considered independently. |
| Windows | Maintain existing Windows release behavior | Existing checks continue; extracted shared behavior must be tested by the Windows application as well. |

As of inspection, Microsoft lists .NET 10 as LTS through 14 November 2028, while .NET 8 and 9 end support on 10 November 2026. Starting this port on .NET 8 would create an immediate servicing problem. Use a pinned .NET 10 SDK and tested current servicing patch, and own runtime patching for self-contained downloads. [Microsoft lifecycle policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

Current .NET 10 supported-OS data includes macOS 14/15/26 and Ubuntu 22.04/24.04/26.04. This is a dependency boundary, not a substitute for app testing. [Official OS matrix](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

Recommended initial artifacts:

- Linux: self-contained x64 AppImage plus a self-contained `.tar.gz` fallback. The AppImage reduces installation friction; the archive provides a route when FUSE or mount policy prevents AppImage execution.
- macOS: separate signed/notarized ARM64 and x64 `.dmg` downloads, each containing `AstroArchive.app`. Do not call two architecture-specific downloads a universal application.
- Windows: retain the current installer and compatibility alias.
- Later: `.deb`/`.rpm`, a Homebrew cask, and Flatpak after their update ownership, host handoff, permissions, and maintenance requirements are proven.

The first release should support manual installation/replacement and safe update notifications. Automatic installation is a subsequent milestone unless a maintained updater can be integrated and fully qualified without delaying integrity work.

## 4. Architecture decision and migration boundaries

### 4.1 UI/runtime decision

| Option | Benefit | Principal cost/risk | Recommendation |
| --- | --- | --- | --- |
| Avalonia on modern .NET | Preserves C#, desktop deployment, and some XAML concepts; supports Mac/Linux | WPF controls/resources/events are not drop-in; media and OS services still require work | Preferred, contingent on a short proof with AstroArchive's actual workflows. |
| Avalonia XPF | Could preserve more WPF code | Commercial terms, compatibility differences, native interop and deployment still need verification | Time-box a comparison only if licensing budget and vendor support are acceptable. Do not assume it solves Windows APIs. |
| .NET MAUI | C# and Microsoft tooling | Linux backend support/maintenance does not match the desired baseline | Do not choose it solely to preserve C#; demonstrate the Linux support contract first. |
| Electron/Tauri with web UI | Good web UI ecosystem | Substantial UI rewrite, managed/native bridge, large-file operations and packaging complexity | Consider only if a web product direction independently justifies it. |
| Qt/native UI rewrite | Mature desktop capability | New UI language/bindings and more application rewrite | Higher-cost fallback if the desktop spike fails. |
| Wine/VM/Mono compatibility distribution | Shorter demonstration path | Does not resolve native UX, libraries, codecs, integrity assumptions, or support burden | An optional user workaround is separate from a supported native release. |

Avalonia documents native macOS integration and Linux backends. Its current documentation says native Wayland is opt-in from 12.1.0; X11 remains an available path. Choose and pin a tested stable version rather than freezing an outdated assumption that native Wayland is unavailable. [Avalonia platform support](https://docs.avaloniaui.net/docs/supported-platforms). Microsoft's Linux MAUI backends are described as experimental and not officially supported; reassess if that changes. [MAUI platform backends](https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/platform-backends/?view=net-maui-10.0).

### 4.2 Proposed component layout

```text
AstroArchive.Core
  Archive models, metadata, target/session rules, calibration/export policy
  Import coordination, hashes, repository operations, recovery protocols
  FITS/XISF/SER parsing, scientific arrays, preview math
  No desktop controls, OS-specific P/Invoke, shell launching, or credential API

AstroArchive.Storage
  SQLite abstraction, migrations, portable snapshots, schema/version checks
  Stable serialization and archive-path encoding

AstroArchive.Platform.Abstractions
  Filesystem identity/containment/commit/locks, volumes, user directories
  Credentials, desktop reveal/open/dialogs, external app launch, updates
  Explicit capability results and useful failure descriptions

AstroArchive.Platform.Windows / MacOS / Linux
  Narrow implementations of platform services

AstroArchive.Imaging
  Scientific raster reader, optional native codecs, bounded display decoding
  Platform presentation adapters outside numeric conversion

AstroArchive.Desktop
  Avalonia views/viewmodels, commands, selection and progress state
  UI-thread adaptation of domain progress; no archive mutations in views

AstroArchive.Legacy.Windows
  Existing WPF application during transition; consumes compatible shared logic

AstroArchive.Tests / PlatformTests / DesktopTests / PackagingTests
  Shared fixtures, real filesystem checks, UI workflows, installed artifacts
```

This is a proposed structure, not a demand to create every assembly before working software exists. Start with a few useful project boundaries and split further when a dependency or testability problem warrants it.

### 4.3 Windows sharing decision

Do not fork domain logic into two permanent implementations. There are two workable transition routes:

1. Extract a .NET Standard 2.0-compatible core consumed by .NET Framework 4.8 and .NET 10; put modern-only APIs behind adapters.
2. Multi-target appropriate libraries for `net48` and `net10.0`, keeping one source implementation and narrowly scoped conditional adapters.

Select after compiling a representative slice. .NET Framework 4.8 supports .NET Standard 2.0, but not .NET Standard 2.1. [Microsoft compatibility guidance](https://learn.microsoft.com/dotnet/standard/net-standard?tabs=net-standard-2-0).

The current direct compiler scripts do not automatically consume SDK-built libraries. Update their assembly/resource references or introduce an SDK build for the legacy app, then prove the same installer still embeds and verifies the correct binaries. This change also invalidates the existing developer claim that no SDK or NuGet is required; update development documentation when it becomes true, rather than leaving a misleading build contract.

### 4.4 Refactoring rules

- Extract and test one workflow at a time. Preserve existing SHA-256 checks, metadata evidence priority, unknown values, calibration requirements, and original-byte export behavior.
- Separate behavior changes from broad formatting changes; compressed source is harder to review, but a wholesale reformat alongside a safety port obscures regressions.
- Replace static platform dependencies through constructor/service injection at real seams. Avoid an interface for every trivial utility.
- Make capabilities structured results, not scattered `OperatingSystem` checks in menus.
- Keep scientific arrays and display images separate. A bitmap rendered for the screen must never become an implicit scientific export input.
- Specify thread ownership, cancellation, and resource disposal for every service that crosses UI, native, or process boundaries.

## 5. First ten working days: resolve expensive uncertainty early

The spike should use generated fixtures and disposable archive copies. Its output is reviewable evidence and decision records, not a production release.

| Day range | Work | Evidence/decision |
| --- | --- | --- |
| 1–2 | Create a source dependency inventory, portable format inventory, build baseline, and SDK prototype | Existing Windows behavior is reproducible; all Windows API categories are assigned an adapter/removal decision. |
| 2–4 | Prototype one virtualized file table, target navigation, selection, sampled FITS preview, and progress/cancellation | Real rendering on Mac and Linux; no unacceptable table/preview or accessibility limitation. |
| 3–5 | Open a copied Windows SQLite index with the selected provider; decode legacy JSON | Golden records, user edits, fields, and unknown values survive without normalization writes. |
| 4–6 | Package a self-contained Mac app and Linux artifact, including SQLite and rendering dependencies | Finder and desktop-menu launches work on machines without the .NET SDK; library paths and permissions are correct. |
| 5–7 | Sign/notarize a minimal Mac package using the intended account/process | Account/certificate readiness and nested binary signing work before UI rewrite absorbs months. |
| 6–8 | Probe case-sensitive paths, legacy separators, file identity, locks, symlinks, USB mounts, and source cleanup requirements | Archive format and Unix feature exclusions are documented with concrete fixtures. |
| 8–10 | Run a thin import → browse → preview → verified export workflow on both platforms | Architecture ADR, support matrix, dependency/license inventory, revised estimates, and a go/no-go decision. |

If the desktop framework fails this slice, make the architecture decision immediately. If notarization is blocked by account setup, continue extraction but do not describe the Mac launch as scheduled. If portable archives need a schema change, complete the compatible Windows-reader plan before allowing production writes on Unix.

## 6. Archive portability and filesystem correctness

### 6.1 Define paths as archive data, not OS strings

Current relative paths are generated with `Path.Combine`, so Windows databases can contain backslashes. On Unix those backslashes may be literal filename characters. A native join does not safely interpret the historical path format.

Create a versioned archive-path codec with these rules:

1. Canonical new archive-relative paths use `/` between components.
2. Legacy Windows backslashes are decoded as separators only for records known to use the legacy Windows representation. Do not replace all backslashes in arbitrary Unix source paths; a backslash can be a legitimate character there.
3. Validate components before resolving them: reject rooted paths, drive/UNC syntax, traversal, NUL, and encoded or ambiguous separators. Define whether `.` components are rejected or normalized; never silently normalize an escape into an allowed path.
4. Join validated components under the repository root, then apply physical containment checks.
5. Absolute source/history/application paths are OS-specific evidence. Preserve historical strings as provenance, but mark unavailable paths and ask for explicit rebinding before accessing them.
6. Use a common portable filename policy for newly managed files, retaining current Windows reserved-name protection. Include UTF-8 byte-length and total-path budgets, not just UTF-16 character counts.
7. Detect case and Unicode normalization collisions before import, export, rename, migration, and ZIP extraction. Preserve both originals through deterministic disambiguation or refuse the conflicting operation with an actionable report.

Do not assume every Mac volume is case-insensitive or every Windows-accessible share is case-insensitive. Distinguish exact persisted names from the comparison behavior of the physical volume. Case-insensitive metadata comparison can remain appropriate for extensions, target identifiers, and certain catalogues; that does not justify case-insensitive path containment.

Required fixtures include `M31.fit` versus `m31.fit`, directories `Archive` versus `archive`, NFC/NFD forms of the same visible name, non-Latin and emoji names, leading dashes, spaces, tabs, quotes, trailing dots, reserved Windows names, very long names, literal Unix backslashes, and path prefixes such as `/data/Archive-other`.

### 6.2 Physical containment and symlink policy

Keep the current principle that managed paths cannot escape the archive. Current managed-path checks traverse parent/leaf attributes; Unix mutation safety needs more than a lexical check and a later pathname operation.

- Resolve a user-selected repository root once and record its physical identity. Consider that macOS has system path aliases/symlinks such as `/tmp`; rejecting every symlink in every ancestor indiscriminately can reject legitimate roots. Distinguish canonical root selection from links inside the managed tree.
- Reject symlinks, dangling symlinks, and mount escapes inside managed captures/metadata unless explicitly supported by a separately reviewed contract.
- Validate leaves and intermediate components; include bind mounts, directory aliases, and hard-link relationships in the threat/correctness model.
- For mutations, use platform primitives capable of anchoring traversal to an opened directory, checking identity, and refusing link following. Linux `openat`/`openat2` availability differs from macOS; adapters must account for that.
- Test a link substitution between validation and mutation. If the implementation cannot preserve containment under that race, retain safe copies and refuse the operation.
- Do not follow arbitrary source links by default. Skipped entries must appear in scan/backup results, with the reason.
- Backups and exports also need containment and link checks; fixing import alone is insufficient.

### 6.3 File identity and caching

`FileStamp.VerifiedUnchanged` currently trusts appropriate NTFS/ReFS identities and change information. Its existing non-Windows fallback is conservative: it does not establish a reliable identity. Preserve that conservative behavior until Unix evidence is qualified.

Introduce a platform-qualified stamp containing platform/provider, filesystem/volume identity, file identifier, byte length, timestamp resolution, modification/change evidence, and a reliability classification. Linux device/inode and macOS filesystem file IDs are useful evidence, but inode reuse and coarse/remote timestamps mean they cannot replace hashes universally.

- Never use creation time as an interoperable content-generation guarantee.
- Do not accept old Windows identities as reliable Unix identities.
- Require unchanged open-handle evidence around reads where available. Do not assume Unix `FileShare.Read` prevents independent programs from writing.
- Disable identity shortcuts for cloud placeholders, unsupported filesystems, network providers, and migrated stamps.
- On exFAT/FAT or uncertain providers, rehash when content identity matters. Explain any slower first scan after migration.
- Distinguish file inventory shortcuts from checksum-confirmed duplicates. Keep filename matching explicit and never let it authorize source deletion.
- Cache keys need a decoder/classification version and platform-qualified identity so changes in decoders or metadata rules invalidate the right entries.

### 6.4 One writer: local process and cross-host boundaries

The current lowercased path-based named mutex protects only part of the problem. `/Volumes/Captures`, `/media/user/Captures`, a bind mount, and a network alias may refer to the same archive. Different hosts do not share a named mutex at all.

Recommended local-write contract:

1. Open a lock anchored in repository metadata on the actual volume, using qualified OS locking primitives; canonical aliases must contend for the same physical lock.
2. Also prevent conflicting local-index access. Include archive identity and selected physical root, and define intentional copies/restores as separate archive instances.
3. Hold locks throughout repository mutation and portable snapshot publication. Persist diagnostic lock-owner details separately; a stale text file alone must not mean the archive is locked.
4. Test second app instances, separate login sessions, abandoned locks, crash recovery, alternate mount paths, and lock-file deletion attempts.
5. Test read-only browsing alongside a writer only if snapshot consistency is guaranteed. Otherwise show a useful repository-in-use result.

For the first release, promise write support on qualified local volumes. Synced cloud folders and multi-host network shares cannot be made safe by declaring a local mutex or a lease file sufficient. Offer verified import from such sources and backups to such destinations when tested. For archives stored there, either provide genuine read-only support or an explicitly qualified single-host/offline workflow. Do not imply safe concurrent access from two computers.

Read-only support must actually open the database read-only, suppress schema creation, normalization/repair, metadata writes, source scans that persist state, and checkpoint-on-dispose. The current `Repository` constructor and `Database` constructor perform writes, so an added disabled button is not a read-only implementation.

### 6.5 Working index and portable snapshot coherence

Currently an existing local index can be selected without importing a newer portable index. Moving an archive between hosts and then returning to an earlier host is therefore a concrete scenario to test.

Introduce explicit archive instance identity, format version, and snapshot generation/fingerprint. Preserve a verified portable recovery snapshot while distinguishing it from a local performance cache.

- At open, compare the portable snapshot state with local working-index provenance. If portable state advanced, rebuild or import it rather than silently favoring stale local metadata.
- If local uncheckpointed changes exist and portable state also changed, stop for a recoverable conflict. Never overwrite one side silently.
- Record a durable operation journal or clearly specified recovery boundary for changes acknowledged to the user before portable checkpoint completion.
- Surface checkpoint failures as actionable archive state. Current disposal catches checkpoint exceptions; metadata portability must not be claimed after an unnoticed failed snapshot.
- Generate a fresh instance identity for an intentional independent restore/clone while retaining provenance, so caches cannot cross-contaminate it. Existing backup-origin behavior must be preserved or migrated deliberately.
- Hashes and metadata remain authoritative; OS paths are locations, not archive identity.
- Define what happens when removable media disappears: halt writes, retain recoverable local state, report the last durable portable generation, and reconcile on remount.

### 6.6 Atomicity, durability, and recovery

Audit `CommitTemporary`, `AtomicText`, file refiling, telescope rename, deletion staging, Edited version creation, backups, and exports. A rename can be atomic without being durable, and two renames with an intermediate backup are not one atomic replacement.

For each operation specify: preconditions, private temporary files, verification, durable flush policy, atomic commit point, database state transition, portable checkpoint, cancellation boundary, and restart recovery.

- Stage files on the same filesystem as the final destination when relying on rename semantics.
- Verify a copied file independently before calling it complete.
- Qualify file and parent-directory flush requirements for the promised crash/power-loss behavior; document limits of filesystem/device guarantees.
- Use a transaction or durable intent record for file-plus-database changes. SQLite transactions do not roll back a previously moved image by themselves.
- Recovery must be idempotent: reopening twice cannot double-import, double-delete, or destroy a last good snapshot.
- Cancellation may retain verified copies but must not leave them reported as a completed batch or trigger an external launch.
- Never clean an unknown `.partial` file merely by extension. Delete only owned temporary artifacts after checking the operation record, identity, and containment.

### 6.7 Source deletion and Dump handling

Windows source cleanup deliberately prevents writer/rename/delete races through an open handle and deletes the verified object. Unix `unlink(path)` removes a directory entry; checking an inode and then unlinking the path leaves a race if the name is replaced. Advisory locks do not force unrelated software to cooperate.

Initial Unix contract: **source originals are retained**. Hide or disable Delete originals with a concrete capability reason. The Dump inbox may also rely on source cleanup, so it must retain files or explicitly offer a copy/import workflow; do not present it as a successful move when cleanup did not occur.

A future source-cleanup project must specify the achievable Unix guarantee, test replacement/write/hard-link races, and preserve the verified destination across interruption. Quarantining a source name may improve recoverability but does not automatically reproduce Windows handle deletion. Do not enable this feature simply because a happy-path hash-and-delete test passes.

Managed archive deletion and Edited deletion can still be supported after their own identity, containment, staging, journal, and recovery tests pass. Their user contract is distinct from deleting external source originals.

### 6.8 Existing NTFS protection

An archive with active Windows protection currently throws on non-Windows open. Test real protected repositories as well as ordinary backups.

Recommended migration paths:

- Preferred: create a verified backup on Windows. Its ordinary-permission recovery copy can be opened after portability validation; original Windows protection remains on the source archive.
- Alternative: disable protection on Windows using existing recovery logic before moving the whole repository.
- Read-only inspection on Unix can be implemented without replaying or rewriting Windows ACL recovery records, but needs an explicit non-mutating storage path.

Keep Windows protection records as Windows-specific evidence. Do not translate deny-delete ACLs into `chmod 444`: on Unix deletion is governed largely by permissions on the parent directory. Do not require `sudo`, filesystem immutable flags, or recursive permission changes for the first release. Explain that verified copies and backups remain available even when OS-level deletion protection is not.

## 7. SQLite, JSON, schema, and Windows round-trip compatibility

### 7.1 Provider replacement

Choose a maintained managed provider with an explicit native SQLite supply chain, such as `Microsoft.Data.Sqlite` with the appropriate native bundle, or a carefully audited provider abstraction. Confirm compatibility with the chosen legacy-target strategy. Avoid an uncontrolled system SQLite version unless that is an intentional, tested packaging policy.

Preserve:

- Existing tables and primary keys: files, sessions, source manifests, deletion history, and import-name history.
- Busy-timeout, transaction, synchronization, snapshot-backup, disposal, and error-report semantics.
- UTF-8 names and metadata. Do not accidentally replace UTF-8 decoding with an ANSI string helper.
- Local working databases and portable snapshots as distinct concepts.

Verify actual packaged SQLite binaries on all RIDs. Inspect version and compile options, use an SQLite integrity check in test/verification workflows, and run migration/backup fixtures. Test lock contention, disk-full, read-only directories, damaged databases, and opening after interrupted checkpoint publication. Do not silently create an empty archive when opening the expected index fails.

### 7.2 Serialization compatibility

`JavaScriptSerializer` is not the serializer provided by modern .NET. Changing to `System.Text.Json` without a compatibility layer can discard public fields and reinterpret ignored/computed properties. Settings, manifests, import options, and models mix fields and properties, and use `ScriptIgnore`.

Build explicit persisted DTOs and legacy readers. Freeze a golden corpus before extraction, including SQLite JSON rows and standalone JSON files.

Verify missing fields versus explicit nulls, public fields, nullable booleans/numbers, dictionaries, computed getters, defaults, unknown future properties, enum/string conventions, date/time encodings where present, floating-point values, Unicode escaping, and user/saved evidence priority. Preserve unknown fields when older and newer versions may both write a record, or explicitly prohibit older writers after a format upgrade.

Do not trust identical JSON text as the only success criterion. Compare persisted meaning, hashes, and metadata evidence; deterministic serialization is useful for manifests/signatures but not a reason to erase legacy fields. Test unknown-field preservation independently. Migration must not invoke domain setters that silently rewrite valid historical values without recording that change.

### 7.3 Archive-format version and compatible Windows release

Application version, platform package revision, archive format, database schema, JSON model, and update-feed schema are separate version concepts.

Before Unix writes a new representation:

1. Define the archive-format version, minimum reader, and minimum writer.
2. Determine whether current Windows code can safely read/write it. Even additive JSON fields may be lost by an older deserializer followed by reserialization.
3. Release a compatible Windows reader/writer first if needed, or explicitly make Unix work on a converted copy that older Windows versions cannot write.
4. Stage migration with verified pre-migration database/metadata snapshots and an operation journal. Image bytes must remain unchanged.
5. Complete migration interruption tests before allowing a production archive.
6. Enforce version gates on open. A newer archive must not be partially repaired by an older writer.

The release gate is a real round trip: supported Windows version → Linux import/edit/export/backup → macOS browse/edit/Edited operations → supported Windows version. Compare image/sidecar hashes, file counts, selected pages/HDU/frame, target/telescope identities, metadata corrections, unknown/conflicting facts, deletion history, import inventory, and Edited project/version references. Revisit the first machine to exercise stale-cache reconciliation.

### 7.4 Timezones and scientific metadata

Replace Windows-only timezone guidance with Windows/IANA interoperability. Store observed UTC separately from local observed time, timezone identifier, and the evidence used. Do not convert filename times into UTC without a chosen timezone.

Test Windows IDs, IANA IDs, UTC, half-hour and quarter-hour offsets, southern-hemisphere DST, ambiguous autumn times, nonexistent spring times, and historical dates. Modern runtime mapping may help, but depends on OS/globalization data; verify the shipped configuration. Use invariant culture for scientific numbers and machine records, while formatting UI dates/numbers appropriately.

User-facing scheduling in this plan uses Europe/London. Acquisition timestamps in archives follow their recorded evidence; they must not inherit the operator's desktop timezone by default.

## 8. Scientific decoding, preview, and media

### 8.1 Preserve the precision boundary

The portable UI must display images without weakening scientific conversion rules. A display decoder that reduces a 16-bit or floating-point image to 8-bit is acceptable only for a clearly separate display preview, never as evidence of scientific export eligibility.

Preserve documented bounds and refusals, including the current `32 * 1024 * 1024` channel-sample allocation bound, at most four channels, bounded TIFF/XISF images and FITS HDUs, and refusal of precision-losing 64-bit integer conversion. Keep sampled sidebar buffers independent of full-resolution popup buffers. Do not silently raise all limits to make a problematic fixture work.

| Format/workflow | First-release commitment | Main verification |
| --- | --- | --- |
| Uncompressed FITS | Archive, metadata, sampled/full preview within bounds, supported numeric export | BITPIX, BSCALE/BZERO, RGB/Bayer axes, multiple HDUs/cubes, orientation, NaN/Infinity, invalid dimensions. |
| Gzip FITS | Original-byte archive/export and supported decoding | Decompression limits, truncated input, cancellation, representative slice selection. |
| Tile-compressed FITS | Decoder capability if qualified CFITSIO is bundled; otherwise explicit original-only support | Decompressed values and selected HDU; missing codec must not break opening or copying the archive. |
| XISF | Existing supported layouts/compression with truthful optional features | Planar/interleaved, integer/float, attached/base64, byte order, zlib/LZ4/shuffle/Zstandard, unsupported subblocks. |
| SER | Existing frame interpretation/playback and original recording export | Bayer/RGB/BGR, 8/16 bit, timestamps, frame selection, seek/cancellation, recordings larger than memory. |
| TIFF | Scientific layouts only with a qualified full-precision reader | Mono/RGB, signed/unsigned/float layouts, pages, metadata, compressed variants, orientation. |
| PNG | Display plus eligible numeric conversion only where decoded values are preserved | 8/16-bit, channels, text metadata, alpha policy, colour/gamma separation. |
| JPEG/GIF | Original-byte archive/export and qualified display/animation | EXIF orientation, GIF disposal/transparency/timing, frame/canvas bounds, no scientific linearity inference. |
| Camera RAW | Original-byte archive/export initially; preview optional | No promise based on Windows-installed codecs; document camera/decoder coverage before adding RAW previews. |
| AVI/MP4/MOV/M4V/WMV/MKV | Original-byte archive/export; playback only for qualified backend/codec combinations | Backend absent, corrupt containers, codec absence, non-local files, large recordings, hardware/software fallback. |

### 8.2 Raster backend selection

Evaluate full-precision TIFF and PNG libraries independently from Avalonia's display-image APIs. Consider maintained managed or native libraries, but prove the exact supported layouts and metadata before choosing. Assess dependency maintenance, licensing, precision, metadata query support, cancellation, and memory bounds.

`RasterHeaders` already contains a fallback that cannot reproduce full WIC inspection. Verify that replacing WIC does not lose target/exposure/filter/stack facts extracted from TIFF descriptions, PNG text chunks, and EXIF fields. Register readers centrally at composition time rather than relying on a Windows-only startup registration.

Preserve `IAssetReader` semantics or refine them into inspect/numeric/sample interfaces. Capability results should identify whether a file is archivable, previewable, numeric, and scientifically exportable. A failed preview is not an import failure if original-byte verification succeeds.

### 8.3 CFITSIO and Zstandard

The first-release preference is vetted, pinned, architecture-matched bundled libraries for consistent offline behavior, provided their terms, build provenance, and Mac signatures are handled. Optional installation remains a viable reduced-scope choice if the UI documents it correctly.

- Replace hardcoded Windows names and `LoadLibraryExW` through a platform resolver. Use known application-owned locations, not the current directory or arbitrary library search paths.
- Check C ABI types, `size_t`, long/long-long widths, calling conventions, structure layout, UTF-8 filenames, and ownership/freeing on each target.
- Validate ELF/Mach-O architecture and all transitive dependencies. An ARM64 application cannot load an x64 codec.
- On Mac, sign bundled native code with the distribution identity. Do not broadly disable library validation just to load untrusted optional dylibs.
- Test codecs absent, wrong architecture, broken transitive dependencies, truncated compressed streams, oversized declared output, and decompression bombs.
- Preserve import/export even when an optional decoder fails. Report the failing decoder/version clearly and avoid native process crashes where practical.

### 8.4 Video backend

Run a dedicated short comparison of an FFmpeg/libav pipeline, libVLC integration, and OS-specific playback. Decide based on required codec coverage, UI composition, Wayland/Mac support, footprint, crash isolation, redistribution terms, and maintenance ownership. Do not assume Windows `MediaElement` has an equivalent built into Avalonia.

Keep planetary SER behavior in the scientific pipeline. Mute ordinary video preview by default if maintaining present behavior. Bound frame queues; do not decode an entire recording into RAM. Stop playback and free native surfaces/handles when changing selection, closing a popup, ejecting media, or quitting. Validate software rendering and overlay behavior separately.

If playback is not ready, show an explicit unavailable preview with original-file export/open options. Marketing must not claim the Windows playback matrix applies to all platforms.

### 8.5 Memory and performance budgets

One maximum-sized `double[]` at the current sample bound is approximately 256 MiB, before source bytes, temporary arrays, RGB display buffers, caches, and native decoder allocations. Eight independent decodes could exceed several gigabytes. Existing copy worker limits do not constitute an imaging memory budget.

Set a process-wide decode budget, a cap on simultaneous full-resolution decodes, bounded caches, and promptly canceled stale preview jobs. Use source dimensions/codec output bounds before allocating. Measure managed and native memory during repeated browsing, not only managed GC totals. Prefer lazy pages/frames and streaming samples where possible.

Use fixed reference datasets for throughput and memory comparisons: 10,000 small captures, 100 large FITS images, high-bit-depth TIFF/XISF, long SER/video recordings, 100,000 metadata rows, and a mixed archive. Synthetic fixtures supplement permission-cleared real-world files; benchmarks do not require distributing private images.

## 9. Platform services, storage locations, permissions, and credentials

### 9.1 User directories and settings

| Data | Linux policy | macOS policy |
| --- | --- | --- |
| Settings | `$XDG_CONFIG_HOME/astroarchive`, default `~/.config/astroarchive` | `~/Library/Application Support/AstroArchive` |
| Durable local index/recovery state | `$XDG_DATA_HOME/astroarchive` or appropriate durable application data | Application Support, outside the `.app` bundle |
| Reconstructible previews/header caches | `$XDG_CACHE_HOME/astroarchive` | `~/Library/Caches/AstroArchive` |
| Logs and operation state | `$XDG_STATE_HOME/astroarchive`, with a documented fallback | `~/Library/Logs/AstroArchive` and durable state in Application Support |
| Active local IPC/runtime files | `$XDG_RUNTIME_DIR` if valid; define secure fallback | Private per-user runtime location |
| Portable archive metadata | `.astroarchive` within the selected repository | Same portable structure |
| API keys | Secret Service when available, otherwise session-only unless user opts into an explicit alternative | Keychain |

Respect absolute, non-default XDG variables. Do not put durable working databases in a cache directory that the OS/user may clear. Never write settings, codecs, databases, or logs inside a signed Mac bundle, an AppImage mount, or a root-owned package directory. [XDG directory specification](https://specifications.freedesktop.org/basedir/latest/).

Separate migrated global preferences from OS-specific paths, credentials, window bounds, and application detection. When importing Windows settings, retain semantic preferences but require rebinding unavailable repository/source/processor paths. Clamp restored window bounds to connected displays.

### 9.2 Mac permissions

Test Desktop, Documents, Downloads, removable volumes, network volumes, and selected application paths under allowed, denied, and later-revoked access. macOS privacy controls apply even to direct-distribution applications; notarization does not grant arbitrary file access.

- Use a normal native folder-selection flow and useful permission-specific errors.
- Request only capabilities needed for the operation. Do not instruct every user to grant Full Disk Access for an ordinary archive workflow.
- Direct Developer ID distribution and Mac App Store sandboxing are different models. App Store distribution is deferred.
- Security-scoped bookmark requirements become explicit work if sandboxing is adopted; do not assume an ordinary persisted path is sufficient there.
- Never respond to a missing volume by creating an empty repository at the abandoned mount path.
- Keychain denied/locked results need a session-only key entry path and a meaningful message, without repeated automatic prompts.

### 9.3 Linux permissions and desktop services

Run as the logged-in user. Respect mount ownership, read-only volumes, SELinux/AppArmor constraints, no-exec locations, and absent desktop/keyring services. Avoid `sudo` as the default remedy.

Use native/portal file dialogs where supported and a tested fallback. Reveal files through a qualified file-manager/D-Bus adapter, with opening the containing folder as a fallback. Open web URLs through desktop services. No shell-command string should be constructed from user paths.

Secret Service can be absent or locked, especially on minimal desktops. Report credential storage availability honestly; do not silently persist an API key in plaintext. When a Windows DPAPI blob is encountered, preserve it as foreign-platform data if needed but request re-entry for actual use.

### 9.4 Removable volumes and telescope discovery

Replace Windows drive-letter and `DeviceIoControl` detection with mounted-volume services: macOS volume/Disk Arbitration facilities and Linux UDisks2 or a bounded mount-information fallback. Device node access is not necessary for importing ordinary files from an already mounted telescope/card.

- Identify a volume through stable provider evidence where available, plus user binding and relative capture root. Labels and mount paths are display/location data, not unique IDs.
- Treat cloneable volume UUIDs cautiously. Do not equate volume identity with physical telescope identity.
- Revalidate an existing telescope binding on reconnect. Prompt before binding ambiguous volumes with identical names/layouts.
- Handle mount/unmount and sleep/wake events; a polling fallback must be bounded and cancellable.
- Retain the current bounded layout inspection. Ignore irrelevant hidden metadata such as `.DS_Store`, `._*`, `.Spotlight-V100`, and `.Trashes` according to explicit rules while preserving genuine acquisition sidecars.
- Test capture devices presented as removable, fixed USB disks, mounted network mirrors, cards, and manually selected folders.
- Handle ejection during scan, copy, verification, metadata save, and checkpoint. Stop safely and report what was committed.

## 10. External processing applications and plate solving

Do not copy the eight-application Windows menu verbatim and assume all entries work. Expose supported native adapters, useful availability reasons, and manual verified-folder export everywhere.

| Application | Linux release stance | macOS release stance | Adapter/verification work |
| --- | --- | --- | --- |
| Siril | Native handoff after version/argument tests | Native `.app`/CLI handoff after tests | Single image versus folder semantics, GUI executable selection, Unicode paths, running instance behavior. |
| PixInsight | Native handoff if installed and protocol validated | Native app bundle handoff if validated | PJSR bridge escaping, script lifetime, documents, folder/WBPP workflow, installed version differences. |
| GIMP | Native/packaged variants individually qualified | Native app bundle individually qualified | Multiple image open, plugin-dependent FITS, conservative RAW claims, launcher identity. |
| Photoshop | Unavailable as a native supported adapter | Native open-document workflow if qualified | Windows COM is unusable; use supported Mac opening mechanisms first. Automation/Apple Events requires its own permission/entitlement contract. |
| DeepSkyStacker | Manual folder/file-list export; no native availability claim | Same | Optional Wine integration can be a separate experimental adapter, never inferred from an `.exe` on disk. |
| AutoStakkert! | Manual original/sequence export; no native claim | Same | Preserve current conservative handling; no invented command-line import. |
| AstroWizard | Conditional on an available native release | Conditional on an available native release | Verify the actual product/version/protocol; retain manual export when unsupported. |
| Stacking Wizard | Conditional on an available native release | Conditional on an available native release | Verify GUI/folder handoff; do not assume Windows executable names or parameters transfer. |
| ASTAP | Local solver after native CLI tests | Local solver after native app/CLI tests | Executable discovery, star catalogue location, output parsing, timeout/cancellation, Unicode/space paths. |
| Astrometry.net | Existing opt-in online solve | Existing opt-in online solve | Portable HTTP, credential service, cancellation, timeouts, TLS/proxy behavior, redacted errors. |

External application support must be rechecked against official application documentation and tested versions during adapter implementation. This matrix makes no claim about future release availability or every format the receiving tool can decode.

Adapter requirements:

1. User-configured paths take priority; automatic discovery is bounded.
2. Mac `.app` bundles need bundle identification and Launch Services or an explicitly supported internal executable. A path to `Foo.app` is not generally an executable filename.
3. Linux discovery includes executable PATH entries and known desktop application registrations; distinguish native, Flatpak, Snap, and AppImage installations. PATH availability in a terminal may differ from desktop launch.
4. Use argument arrays (`ProcessStartInfo.ArgumentList` on modern targets), not Windows quoting copied into Unix command strings. The net48 adapter can keep appropriate Windows quoting separately.
5. Pass paths as arguments and validate scheme/executable choices; do not invoke a shell with arbitrary metadata. Test leading-dash filenames and use `--` only when supported by the receiver.
6. Escape generated scripts according to their language. Argument-list safety does not prevent injection into a generated PJSR script or application file list.
7. Do not launch until required copies and manifests are verified. Cancellation or partial batches prevent automatic handoff.
8. A successful process start is not proof the processor accepted the documents. Preserve outputs and report launch/import distinction.
9. Handoff paths must remain accessible to the receiver. Flatpak document-portal paths and Mac sandbox scopes cannot be assumed accessible to an unrelated host process.
10. Handle one already-running app instance, slow startup, unavailable plugin, executable relocation, permissions, and command-line length limits.

For ASTAP, keep an app-owned temporary workspace and deterministic cleanup. Validate process-tree cancellation and stale output rejection. Star databases may be large; never silently bundle/download them as part of the first installer. Solving should not block browsing or mutate original images.

## 11. Desktop UX parity and platform conventions

Recreate workflows using commands and persistent state rather than translating WPF event handlers one by one. Table virtualization, selection, and cancellation behavior need early qualification.

| Workflow | Required behavior | High-risk regression |
| --- | --- | --- |
| Repository selection/startup | Explicit root selection, existing archive open, clear unavailable/read-only result | Creating a new empty archive at an unmounted old path; unnoticed normalization writes. |
| Import | Telescope binding, scan, metadata review, eligibility, verification, progress, cancel | UI freezes, provisional rows treated as committed, source retained but message says removed. |
| Repository browsing | Target/session grouping, search, sort, column persistence, batch selection | Losing selected files when navigation/filtering changes; loading the whole image set. |
| Target navigation | Stable selected/active row semantics, keyboard traversal, counters | Mouse-only behavior, focus jumping, incorrect selection after asynchronous refresh. |
| Preview | Sampled sidebar, separate native-resolution popup, zoom/pan/Fit/stretch | Full image decoded during every row change; wrong coordinate/orientation or stale preview. |
| Edited | Working copies, duplicate/version detection, metadata-only edits, refresh/export/delete | Reimporting an editor's incomplete write; overwriting a changed working file. |
| Export/stacking | Original bytes, collision preservation, optional calibration/manifest/conversion | Preview pixels exported; calibration roles lost; incompatible groups merged. |
| Backup/restore | Whole archive, verified snapshot, complete manifest, useful progress | Missing `.astroarchive`/Edited; success before verification; late source changes ignored. |
| Deletion/reset | Explicit confirmation, managed membership, staging/recovery, deletion history | External original removed; stale selection deletes a different record. |
| Preferences/help | Semantically compatible settings, accurate platform content, offline guide | Windows-only paths/instructions displayed as native behavior. |
| Activity/update notice | Durable outcomes, cancellation, useful errors, unobtrusive checks | Failed checkpoint or partial export disguised as success. |

Mac conventions: Command-based shortcuts where appropriate, native app menu/About/Preferences/Quit, Finder open behavior, Dock identity, Retina scaling, trackpad gestures, focus rings, and VoiceOver semantics. Preserve documented Windows shortcuts where useful, but provide clear platform labels. Window close and application quit are distinct lifecycle events; archive locks/background jobs must follow the intended policy.

Linux conventions: desktop-entry identity and icon, Ctrl shortcuts, GNOME/KDE themes and dialogs, fractional scaling, keyboard focus, AT-SPI screen-reader behavior where available, portal failure handling, and both X11/native-Wayland or XWayland paths for the chosen framework version.

Use a software-rendering fallback when GPU initialization fails, and test it. Screenshots alone cannot prove accessibility, focus, or virtualized selection behavior. Test screen readers, high contrast, large text, reduced motion, mixed-DPI monitors, and multilingual input. No platform-specific unavailable feature should leave an unexplained enabled control.

## 12. macOS packaging, signing, notarization, and first launch

### 12.1 Bundle and architecture policy

Use a stable reverse-DNS bundle identifier controlled by the project, a matching executable name, proper `.icns` assets, version/minimum-system metadata, and consistent display/product identity. Keep mutable data outside the bundle.

Package every RID-specific native dependency: .NET runtime, Avalonia native backend, rendering libraries, SQLite, optional scientific codecs, and any media library. Inspect dependencies with `otool` and architectures with `file`/`lipo`. Reject references to a developer's Homebrew cellar, build directory, or libraries available only on the CI machine. Use appropriate bundle-relative install names/rpaths.

Self-contained publishing still requires correct native dependencies and a supported OS. Do not enable trimming, single-file extraction, or Native AOT in the first packaging pass. They can interfere with reflection, dynamic resources, native library discovery, and startup; introduce them only with dedicated evidence.

Separate ARM64 and x64 bundles reduce ambiguity. A future universal bundle must include compatible architectures for all native pieces and a coherent managed runtime layout, not just a `lipo`-combined launcher. Rosetta launching is a compatibility test, not a replacement for native ARM64 or Intel qualification.

### 12.2 Signing pipeline

Developer ID signing plus notarization is the planned direct-distribution path. Establish the developer account, team identity, Developer ID Application certificate, secure credential handling, renewal ownership, and recovery before beta. A `.pkg` would additionally need the appropriate installer signing identity; the initial DMG route avoids unnecessary installer machinery.

Recommended sequence:

1. Build and assemble the complete architecture-specific `.app`.
2. Verify resources, native dependencies, versions, and notices.
3. Sign nested executables/libraries/helpers explicitly from the inside out, then sign the outer bundle with secure timestamps and hardened runtime.
4. Validate signatures and the intended Team ID. Do not use `codesign --deep` as a substitute for correct nested signing.
5. Submit a suitable archive for app notarization with `notarytool`; retain submission IDs and logs. Staple and validate the app ticket.
6. Create the final DMG containing the stapled app, sign/notarize the DMG as appropriate, then staple and validate its ticket.
7. Compute final downloadable hashes after stapling/container changes.
8. Download that exact artifact through a normal browser to a clean test account; inspect Gatekeeper assessment, launch it, copy to Applications, and repeat offline where supported.

Alternatively a qualified pipeline may notarize the final container and retrieve the contained app ticket without a separate app submission. The gate is validated tickets for the shipped objects and the real distribution path, not an arbitrary number of submissions.

Use the least entitlements required by the tested .NET/runtime integration. Qualify JIT requirements explicitly. Do not add disabled library validation, unrestricted executable memory, DYLD environment allowances, Apple Events, or sandbox permissions just because an old sample includes them. Add a specific entitlement only when an implemented feature and a test demonstrate the need.

Apple documents `notarytool`/`stapler` and supported containers. Notarization identifies an accepted distribution artifact; it is not an archive integrity test. [Apple notarization workflow](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow). Avalonia also documents bundle packaging and JIT-related signing considerations. [Avalonia Mac deployment](https://docs.avaloniaui.net/docs/deployment/macos).

### 12.3 Mac failures and remedies

| Symptom | Likely cause | Remedy and release gate |
| --- | --- | --- |
| Works in Terminal, fails in Finder | CWD/PATH/resource assumptions, launch environment | Resolve resources from application paths, use bundle/native launch services, test Finder from a clean account. |
| “Damaged”/blocked launch | Signature changed, nested binary missed, missing notarization, quarantine assessment | Inspect signature/notary/Gatekeeper logs; rebuild and sign correctly. Do not make quarantine removal the installation procedure. |
| ARM Mac fails loading a codec | x64-only dylib or incompatible dependency | Ship the ARM64 build and inspect the full dependency graph. |
| JIT startup crash under hardened runtime | Missing/incompatible entitlement or runtime integration | Verify current runtime requirements on the actual supported OS and keep the narrow working configuration. |
| Offline first launch fails | Missing stapled ticket or unresolved dependencies | Validate app/container tickets and offline clean-machine launch. |
| Permission denied on Documents/external drive | macOS privacy denial, ownership, read-only volume | Offer explicit folder selection and targeted diagnostics; verify denied/revoked behavior preserves the archive. |
| Keychain access repeatedly prompts | Wrong access policy, changing identity, locked keychain | Stable signing/bundle identity, correct credential adapter, session-only fallback. |
| Notary job stays pending | Service/account/setup delay | Retain submission identity, inspect service/account status and logs, retry boundedly; keep release draft unpublished. |
| Updates break signature or permissions | In-place writes to signed bundle or incorrect replacement | Manual verified replacement first; later updater must verify and replace the entire bundle safely. |

## 13. Linux packaging, ABI, desktop sessions, and distribution

### 13.1 ABI and dependency policy

Build against a documented oldest supported ABI environment, provisionally Ubuntu 22.04 for a glibc x64 compatibility build, then qualify on the public target distributions. An older build environment is not a promise of support for every older distro. Check the requirements of the whole artifact: runtime, rendering, SQLite, codecs, C++ ABI, OpenSSL/globalization libraries, and app launcher.

Self-contained .NET does not mean every system dependency disappears. Generate package/dependency requirements from the actual shipped versions and clean-machine tests. Avoid copying a documentation example's `libssl` or `libicu` alternatives without checking that they are valid for the chosen runtime/distribution.

Inspect native dependencies with ELF tooling; ensure no accidental link to build-machine paths, unnecessarily new glibc symbols, or unavailable GPU/media libraries. Provide a bounded startup diagnostic that reports a missing dependency and architecture clearly without dumping private environment variables.

### 13.2 AppImage and archive fallback

- Produce a launchable AppImage with correct AppDir metadata, executable permissions, desktop entry, icons, product ID, and licenses.
- Test a download with executable permission absent, FUSE unavailable, no-exec Downloads, spaces/Unicode in the path, read-only AppImage mount, and renamed/moved file.
- Ship a verified `.tar.gz` fallback whose extracted launcher works from a writable allowed location without FUSE. Preserve executable bits and avoid unsafe archive entries.
- Do not require users to run the app as root or install permissive device rules to import already mounted files.
- Download metadata and checksums must identify the exact artifact; an AppImage and tarball have different bytes and hashes.

FUSE support is a real AppImage compatibility condition. Document the archive fallback or qualified extract-and-run behavior with the actual AppImage runtime being shipped. [AppImage FUSE troubleshooting](https://docs.appimage.org/user-guide/troubleshooting/fuse.html).

### 13.3 X11, Wayland, GPU, and portals

Pin the UI/backend version and separately qualify X11, native Wayland if adopted, and XWayland where advertised. Test GNOME and KDE because focus, dialogs, scaling, file-manager integration, clipboard, and decorations can differ.

Native Wayland is a candidate with current Avalonia, but it remains a release decision based on actual AstroArchive tests. If native Wayland fails a blocker, use the qualified XWayland path with accurate requirements, or reduce support scope. Do not label an untested backend as supported merely because a framework package exists.

Test Intel/AMD/NVIDIA where available, software rendering, Mesa version differences, remote sessions if advertised, fractional scaling, and mixed-DPI monitors. A headless Xvfb run is useful for automation but cannot qualify compositor, GPU, portal, accessibility, or removable-media behavior.

### 13.4 Native packages and Flatpak later

For `.deb`/`.rpm`, define installation ownership and remove only package-owned files. Post-install/uninstall scripts must not enumerate or remove user archives. Build dependencies, architecture mappings, signing/repository metadata, and package revisions need their own tests. Package-managed installs must not self-update app binaries behind the package manager.

Flatpak is a separate workstream. It restricts host files, processes, D-Bus services, and native libraries; AstroArchive's archive selection, USB volumes, host processors, secret storage, and solver handoffs all interact with these boundaries. Use portals and narrow permissions, prove persistent folder grants and host-visible working copies, and document unavailable integrations. Do not grant broad host execution/access simply to reproduce the Windows menu. [Flatpak sandbox permissions](https://docs.flatpak.org/en/latest/sandbox-permissions.html).

Check source-available/proprietary application eligibility and channel-specific terms before promising Flathub or a distribution repository. Do not assume PolyForm Noncommercial is accepted by every community packaging policy. A first-party downloadable AppImage/tarball does not require community repository acceptance.

## 14. Versions, update feeds, and safe release coordination

### 14.1 Preserve existing Windows update compatibility

The schema-1 Windows feed is loaded from GitHub's latest release and validates specific Windows installer names. Preserve `update.json`, the current Windows asset names, checksums, and package semantics for existing clients until a separately validated migration is complete.

Introduce a new feed such as `updates-v2.json` or distinct platform/channel endpoints. Its design must not cause an old Windows client to choose a DMG/AppImage or encounter an absent latest-release feed.

During Unix alpha/beta, publish GitHub prereleases or other explicitly isolated preview assets and do not mark them latest stable. Every release that becomes latest for Windows must retain a complete compatible Windows feed/artifact set. Conversely, the Windows automain workflow must not republish, overwrite, or retag a draft intended for coordinated cross-platform promotion.

### 14.2 Proposed manifest data

Version release metadata independently from platform revisions. Include:

- Application version and immutable source commit/tag.
- Channel, platform/RID, artifact type, installation owner, and minimum supported OS.
- Platform package revision, monotonically comparable for that channel/RID/type.
- Download URL, final SHA-256, byte size, and relevant signature/notarization identity.
- Archive minimum reader/writer/format compatibility.
- Release notes and known limitations for that platform.
- Manifest expiry/sequence/signature-key metadata if an authenticated update protocol is used.

Illustrative shape, not a ready-to-ship security protocol:

```json
{
  "schema": 2,
  "applicationVersion": "<next-version>",
  "sourceCommit": "<full-commit>",
  "channel": "stable",
  "artifacts": [
    {
      "rid": "osx-arm64",
      "type": "dmg",
      "packageRevision": 1,
      "installOwner": "user",
      "minimumOsVersion": "<qualified-version>",
      "url": "<immutable-https-release-url>",
      "sha256": "<final-artifact-hash>",
      "size": 0,
      "archiveFormat": "<supported-format>"
    }
  ]
}
```

Do not use placeholder `size: 0` in a real feed; generation must supply actual validated values. Keep the existing four-part Windows package version while mapping common application versions to SemVer/platform packaging conventions explicitly. Do not overload Windows installer revision as a universal database version.

### 14.3 Update ownership and trust

Initial behavior: unobtrusive platform/channel-specific release notices, notes, verified download selection, and manual replacement instructions. Offline startup and disabled-check preferences must continue to work. A failed check must not block opening an archive.

Later automatic updates require a maintained, tested mechanism per installation type. Mac may use a suitably integrated maintained updater; AppImage may use a qualified replacement mechanism. Native package and Flatpak updates belong to their package systems.

The future automated install gate verifies authenticated update metadata and final payload identity; checks OS/RID/channel/type/version; rejects unwanted downgrade/replay/malformed feed; waits for archive jobs, durable checkpoints, and locks; then launches a platform-qualified replacement process. A checksum delivered through the same compromised channel is integrity evidence, not independent publisher authentication.

Require clear signing-key ownership, rotation and revocation handling, and retained known-good installers. Automatic application rollback is safe only when archive writer compatibility permits it. Never restore an old database silently on top of new images/metadata.

### 14.4 Single release coordinator

Adopt one protected coordinator for stable version promotion. Platform jobs build/test independently and produce immutable artifacts/reports. The coordinator validates all mandatory artifacts, version mappings, source commit, signatures, notices, and compatibility feeds, stages the draft, then promotes it only after the declared release gates.

Port commits should use a feature branch without publication. Coordinate changes to the existing main-push Windows publisher deliberately; simply adding a second workflow with `contents: write` is not sufficient. Use concurrency across the release tag/version, not merely one OS workflow. Retrying a draft may be supported, but published bytes must never be replaced under the same version.

A missing Mac notarization job must keep the intended combined release in draft or trigger an explicitly scoped Windows/Linux-only release decision. It must never publish an unsigned Mac artifact as an automatic fallback.

## 15. CI, supply chain, and artifact validation

### 15.1 Job structure

| Job class | Trigger | Responsibilities |
| --- | --- | --- |
| Shared engine | PR and protected branches | Build portable libraries; run parser, metadata, import/export, recovery, compatibility tests on Linux, Mac, and Windows. |
| Legacy Windows | PR and Windows maintenance path | Existing engine/raster/UI/installer/updater checks; verify shared-core changes and package payload assumptions. |
| Desktop build/UI | PR | Compile target projects; bounded synthetic UI workflows, selection, progress, preview, accessibility-tree checks. |
| Native platform integration | Scheduled/RC and relevant PR | Real filesystems, locks, symlink races, removable volumes, credentials, process launch, permissions. |
| Packaging | Release candidate/tag | Self-contained RID publish; native dependency scan; AppImage/tar/DMG assembly; install/launch/replace/uninstall tests. |
| Signing/notarization | Protected release context only | Short-lived keychain/credential setup, sign, notarize, validate, capture reports, cleanup. |
| Promotion | Declared stable release | Validate immutable artifacts and feeds; stage draft; publish with required evidence. |

Actual runner labels and architecture availability must be verified when creating workflows. Do not assume `macos-latest` remains Intel or that an ARM runner is available to the repository. Pin a tested image or record the image and requalify changes. Use real Intel/ARM machines where hosted runners cannot cover the support promise.

No signing credentials or write tokens in untrusted PR execution. Avoid using a privileged workflow to execute arbitrary PR code. Pin actions/dependencies, review updates, use locked restore, and record SDK/runtime/UI/native versions.

### 15.2 Build and publication evidence

Each artifact report should include source commit, application/package/archive versions, RID, OS image/toolchain, dependency versions, test results, native dependency inventory, notices, signature identities, notarization IDs/status, final hash/size, and known exclusions. A report must be bound to the final bytes; a pre-sign/pre-staple hash is insufficient.

Produce a dependency/SBOM inventory and relevant provenance. Do not promise bit-for-bit reproducible signed DMGs: timestamps, signing, and notarization affect bytes. Aim for repeatable source/dependency resolution and explain nondeterministic distribution steps.

Retain failure logs and screenshots without exposing keys, API payloads, personal paths, or images. Clean signing keychains and secret files on both success and failure. Cache package restores safely, but never rely on a cache to provide an undeclared native dependency.

### 15.3 Required installed-artifact checks

- Download and launch without developer tools/runtime installed.
- Validate the actual packaged app, not just `dotnet run` or unpacked build output.
- Verify final artifact hash/signature and missing/wrong native dependency behavior.
- Run import, browse, preview, edit metadata, Edited working copy, export, backup, close/reopen, and supported external handoff.
- Replace the same app version, upgrade, attempt unsupported downgrade, and uninstall/remove the app while preserving archives/settings/local recovery state.
- Launch offline, under permission denial, from a changed location, and with a relocated archive.
- Confirm installation/removal never deletes archive data even if a user chose an unfortunate nearby directory. Reject an install destination that overlaps a managed archive where necessary.
- Verify notices/resources/offline help are present and target-specific.

## 16. Test programme and acceptance evidence

### 16.1 Reuse existing checks deliberately

Port applicable cases from Security, ArchiveSafety, FastImport, ImportSelection/Policy, Export, Edited/EditedDeletion, MetadataEditing, Telescope, ExternalApps, Compatibility/Raster, Preview/Resolution, TargetNavigation/Workflow, SessionScan, Performance, and release-report suites. Preserve behavior assertions and fixture generators; replace Windows-specific harness mechanics.

Mark tests as portable, adapter-specific, UI-specific, or physical/manual. Skipped tests must be counted with reasons, and a skip must not satisfy a launch gate. Legacy Mono tests or mock process launches are useful for selected engine evidence, not native desktop qualification.

### 16.2 Core matrix

| Dimension | Mandatory cases |
| --- | --- |
| OS/architecture | Qualified Linux x64 combinations; native Mac ARM64; native Mac Intel if advertised; maintained Windows target. |
| Filesystem | ext4; APFS case-insensitive and case-sensitive; exFAT removable media; Windows NTFS round trip; additional advertised providers. |
| Paths | Spaces, quotes, Unicode, normalization collisions, case collisions, long paths, leading dashes, literal backslashes, mount aliases, missing root. |
| Archive state | New, Windows legacy, migrated, protected Windows source, verified restore, stale local cache, corrupt index, unsupported future schema. |
| Operations | Scan, import, duplicate/restore, metadata/refile, Edited version, export, backup, archive deletion/reset, reopen/recovery. |
| Failures | Cancel, process kill, disk-full, permission revoke, media eject, changed source, stale selection, SQLite busy, partial checkpoint, absent codec/app/keyring. |
| UI | Light/dark/high contrast, large text, reduced motion, keyboard, screen reader, mixed DPI, repeated navigation/preview. |
| Sessions | X11, chosen Wayland path, GNOME/KDE, Finder/Dock, offline, sleep/wake, separate accounts. |

Network/cloud archives have a separate qualification matrix if write support is proposed. Source import and backup destination tests do not establish archive-writing safety on that provider.

### 16.3 Integrity and failure scenarios

| ID | Scenario | Required invariant |
| --- | --- | --- |
| A01 | Import known files on every target | Original and committed destination SHA-256 match; sidecars and metadata evidence remain associated. |
| A02 | Modify a source after scan and during copy | Detect or conservatively refuse commitment; original is retained; no false success. |
| A03 | Tamper with duplicate destination | Reverify/restore/refuse appropriately; no cache shortcut certifies changed bytes. |
| A04 | Kill during file commit, DB commit, and portable checkpoint | Reopen yields a valid previous/new state or explicit recoverable operation; no silent orphan/data loss. |
| A05 | Disk-full in copy, journal, DB, snapshot, and backup | Last good archive remains readable; partial result is reported; no external handoff. |
| A06 | Mount path disappears and is reused for another disk | Do not open/create/write the wrong archive; confirm identity before reconnecting. |
| A07 | Symlink/dangling link/bind alias substitution | No mutation outside the intended managed tree. |
| A08 | Two instances, accounts, and mount aliases | Exactly one authorized writer; explicit read-only/in-use result for others. |
| A09 | Windows → Linux → Mac → Windows round trip | Hashes, metadata facts, histories, projects, selections, and supported path resolution remain correct. |
| A10 | Return to a host with a stale local index | New portable generation wins or a recoverable conflict is presented; no overwrite of newer metadata. |
| A11 | Restore/clone an archive | Independent instance/cache identity with retained provenance; repeated open is idempotent. |
| A12 | Open Windows-protected archive on Unix | Safe refusal/read-only/verified-backup route; Windows ACL recovery evidence is not rewritten. |
| A13 | Delete/reset after the selected file changes | Correct identity validation; retain source originals and unrelated managed data. |
| A14 | Cancel a verified multi-file export | Completed files remain verifiable; incomplete temp files are owned/removed safely; no subsequent launch. |
| A15 | Change an Edited file while refreshing | No incorrect duplicate/version or stale metadata application; report/retry unstable files. |
| A16 | Back up while files change or volume disconnects | No incomplete backup advertised as verified; previous backup remains usable. |
| A17 | Open newer unsupported archive schema | Refuse writing before any repair/migration/checkpoint occurs. |
| A18 | Missing/locked credential store | Offline/local workflows remain usable; no key leaked to logs/plaintext settings. |
| A19 | API/solver timeout, cancellation, stale output | No stale plate solution applied; cleanup preserves archived originals. |
| A20 | Repeated recovery from the same interrupted operation | Idempotent results, stable histories/counts, no duplicate deletion or import. |

Use deterministic fault hooks to exercise each commit boundary, plus real process kills on actual filesystems. Process-kill testing cannot prove all power-loss/device-flush guarantees; document the durability contract and supplement with controlled storage-failure testing if such a guarantee is advertised.

### 16.4 Preview/conversion reference corpus

Generate analytic images with known physical sample values, then compare numeric decoding across platforms. Conversion needs exact agreement where the representation allows it, or documented numeric tolerance where it does not. Separate that from display rendering, where small colour/font/GPU differences may be acceptable.

Include FITS signed/unsigned conventions, floats/NaN, non-unit BSCALE/BZERO, multi-HDU/cubes, RGB axes and Bayer offsets; XISF byte order/compression/planar cases; TIFF 16-bit/float/multipage; PNG 16-bit/text metadata; JPEG orientation; GIF disposal; SER Bayer/RGB/16-bit frames. Unsupported layouts must fail with a useful capability result, without truncating or silently changing scientific values.

Verify export checksums and optional manifests independently. Calibration acceptance/review/rejection rules, stack versus constituent subs, and unknown linearity must retain current semantics.

### 16.5 Proposed performance gates

Establish reference machines/data during discovery before freezing numeric targets. Starting targets for tuning, not measured claims:

- Offline cold launch on a reference local SSD: usable shell within 5 seconds.
- Cancellation acknowledged in UI within 1 second; worker shutdown within 5 seconds for interruptible local work. Native/network hangs require explicit timeout/isolation design rather than pretending cancellation guarantees a library returns.
- Warm metadata search/filter over 100,000 records: target under 500 ms without loss of selection semantics.
- Sampled preview of a representative 50 MiB uncompressed FITS on SSD: target under 1 second after metadata selection; unrelated browsing remains interactive.
- On comparable hardware/storage, no unexplained verified-copy throughput regression greater than 20% against the appropriate existing/core baseline. Required safety rehashing on weaker filesystems is an explained tradeoff, not a reason to weaken verification.
- Repeated selection/playback/full-resolution cycles return near steady-state memory; retained growth and native handle leaks are blockers.

Record distributions/percentiles, cold/warm caches, storage, CPU/RAM, dataset, and decoder versions. Do not compare an ARM laptop to a different Windows workstation and call the difference a port regression.

## 17. Delivery work packages and ownership

Assign people to the following roles before calendar commitments. One person can hold several roles, but explicit review coverage is required for integrity changes.

| Work package | Primary owner role | Dependencies | Completion evidence |
| --- | --- | --- | --- |
| WP01 Architecture/build spike | Technical lead | None | Native packaged thin workflow, architecture ADR, dependency choice. |
| WP02 Legacy compatibility corpus | Core engineer + QA | Baseline source | Golden archives/JSON/bytes, current Windows tests reproducible. |
| WP03 Core extraction | Core engineer | WP01–02 | UI-free projects, legacy app consumes shared behavior, CI green. |
| WP04 Portable path/serialization format | Core/storage engineer | WP02 | Versioned readers/writers, collisions and migration tests, compatible Windows decision. |
| WP05 SQLite/snapshots/recovery | Storage engineer | WP03–04 | Provider parity, generation reconciliation, fault recovery. |
| WP06 Filesystem/locks/volumes | Platform engineer | WP01, path contract | Qualified OS adapters and local-volume support evidence. |
| WP07 Imaging/native codecs | Imaging engineer | Core interfaces | Numeric parity corpus, bounded decode/memory, packaged RID dependencies. |
| WP08 Desktop workflows | Desktop engineer | Thin slice, service contracts | Import/Repository/Edited/export/backup/navigation parity. |
| WP09 Secrets/timezones/permissions | Platform engineer | Model/platform contracts | Keychain/Secret Service, DPAPI migration behavior, permission/timezone tests. |
| WP10 Processor/solver adapters | Platform engineer | Export, desktop, filesystem | Versioned real-app handoffs, shell/script escaping and cancellation tests. |
| WP11 Mac distribution | Release engineer | Early account proof; native builds | Signed/notarized downloaded app, native architecture and privacy tests. |
| WP12 Linux distribution | Release engineer | Native build/backend decision | AppImage/tar clean-machine launch and desktop/ABI tests. |
| WP13 Feeds/release coordinator | Release engineer | Version/support decisions | Windows schema-1 compatibility, channel isolation, final-byte validation. |
| WP14 Cross-platform qualification | QA + core/platform reviewers | WP04–13 | Round trips, fault matrix, UI/performance/package acceptance. |
| WP15 Documentation/beta/support | Product/documentation owner | Actual capability matrix | Accurate download page, guides, limitations, issue template, beta protocol. |
| WP16 RC/stable promotion | Release owner | All mandatory gates | Signed-off evidence, immutable release, first-week response coverage. |

A reviewer other than the implementer should assess path containment, source/managed deletion decisions, schema compatibility, recovery, and update trust. This is an engineering review requirement, not a request to run another AI agent.

## 18. Indicative schedule, effort, and dependencies

Use relative weeks until discovery is complete. An illustrative schedule with roughly two to three experienced engineers and part-time QA/release help is **24–34 calendar weeks**, including overlap and a **25–35% contingency allowance** against a roughly 20–25-week execution baseline for native packaging, migration, and physical-platform problems. This is an initial planning range, not a source-derived estimate or guaranteed delivery date. The low end requires shortening overlapping phases; the high end includes the full contingency.

| Phase | Relative window | Deliverables | Exit gate |
| --- | --- | --- | --- |
| Discovery/spike | Weeks 1–2 | Platform thin slice, archive contract, signing proof, scope | Architecture and support decisions justified. |
| Core/build extraction | Weeks 3–6 | SDK projects, adapters, shared engine CI, compatible Windows route | Engine behavior preserved, no UI dependencies in core. |
| Archive/platform safety | Weeks 5–10 | Paths, serialization, SQLite, locks, snapshots, volumes, credentials | Round trip on disposable archives and fault cases pass. |
| Desktop/imaging workflows | Weeks 7–15 | Main pages, previews, Edited, export/backup, core handoffs | Complete everyday workflow on both target platforms. |
| Packaging/integration alpha | Weeks 12–17 | Signed Mac artifacts, Linux artifacts, installer/removal checks | Clean machines and qualified formats/handoffs work. |
| Controlled beta | Weeks 17–22 | Real user/device/filesystem evidence, fixed blockers, final docs | Stable beta evidence and no unresolved integrity issue. |
| RC/stable preparation | Weeks 23–25 | Final artifact qualification, release rehearsal, coordinator | All gates pass for every advertised artifact. |
| Contingency | Through week 34 or replan | Native/permission/migration failures | Scope/date change made explicitly. |

The ranges overlap intentionally and cannot be added as independent serial estimates. If starting on Monday 12 October 2026, this suggests investigation toward a spring to early-summer 2027 launch; do not announce a date from this calculation. Recheck OS/runtime/framework support at that future freeze.

Indicative implementation effort after discovery: approximately **180–280 engineer-days**, plus **40–70 QA/release/documentation days**, including test harness and packaging work. Replace these with work-package estimates after the ten-day spike. A solo maintainer with interrupted availability should plan roughly **9–15 months or longer**, and consider ARM64 Mac + one Linux x64 distro first rather than preserving the full matrix.

External dependencies that can dominate the calendar:

- Developer account/certificate readiness and notarization service behavior.
- Native Mac Intel/ARM hardware, representative Linux GPU/desktops, and removable media.
- Legacy Windows writer changes needed for archive round trips.
- UI virtualized table/selection performance and accessibility.
- Full-precision TIFF/PNG and native media/library availability.
- Actual supported external processor versions and permission-cleared sample data.
- Beta participants with representative filesystems/devices and time for diagnosis.

Budget by category: engineering time, QA/release time, Mac hardware/hosted runners, CI minutes/artifact retention, developer-account/signing renewal, any commercial UI/vendor support, and support capacity. Obtain current prices and account terms before approving budget; this plan deliberately does not invent a fixed cash estimate.

## 19. Prioritized risk register

Likelihood is an engineering assessment based on observed coupling, not a measured incident frequency. P0 means data/security integrity or wrong-artifact publication; P1 blocks an advertised core workflow/target; P2 permits a documented reduced-scope release.

| ID | Priority / likelihood | Failure | Prevention/remedy | Proof/owner |
| --- | --- | --- | --- | --- |
| R01 | P1 / certain | WPF/.NET Framework binaries cannot supply native UI | New desktop/runtime and controlled extraction | Native thin slice; technical lead. |
| R02 | P0 / high | Legacy separators/case assumptions address the wrong file | Versioned path codec, physical containment, collision policy | Path corpus and round trip; core/platform. |
| R03 | P0 / high | New JSON serializer loses fields/evidence | Persisted DTOs, legacy reader, unknown-field policy | Golden rows/settings and writer tests; storage. |
| R04 | P0 / high | Older Windows rewrites a newer archive incorrectly | Minimum writer gates; compatible Windows release before Unix writes | Supported Windows round trip; core/release. |
| R05 | P0 / medium-high | Stale local cache overwrites newer portable metadata | Generation/provenance reconciliation and conflict recovery | Return-to-first-host case; storage. |
| R06 | P0 / medium-high | Two instances/hosts write divergent local indexes | Qualified repository locking, scope restrictions | Alias/account/multi-host cases; platform. |
| R07 | P0 / high if naively ported | Unix source deletion removes a replacement/original incorrectly | Retain sources initially; separate cleanup project | Race tests and explicit capability; platform. |
| R08 | P0 / medium | Symlink/raced path escapes managed tree | Handle/directory-anchored mutation, conservative link policy | Substitution tests; platform/security reviewer. |
| R09 | P0 / medium | Crash between image/DB/snapshot commits loses acknowledged data | Durable intent/recovery, checkpoint status, idempotence | Fault injection/process kills; storage. |
| R10 | P1 / certain for affected archives | Active Windows protection prevents Unix open | Verified backup or Windows disable route; true read-only if implemented | Protected real archive tests; core/docs. |
| R11 | P0/P1 / high | Display decoder changes scientific precision | Numeric/display separation and reference corpus | Physical value conversion checks; imaging. |
| R12 | P1 / high | Native dependency absent/wrong architecture | Pinned RID builds and dependency inspection | Clean-machine packaged launch; release. |
| R13 | P1 / medium-high | Mac signature/notarization/JIT failure | Early signing proof, inside-out signing, narrow entitlements | Quarantined browser download/first launch; Mac release. |
| R14 | P1 / high | Linux ABI/FUSE/backend incompatibility | Oldest ABI build, target matrix, tar fallback, renderer choice | Actual distro/desktop launches; Linux release. |
| R15 | P1 / medium-high | Huge images exhaust RAM or leave native leaks | Global budget, decode concurrency, bounded caches/frames | Repeated mixed-format/memory tests; imaging/desktop. |
| R16 | P1 / high | Desktop loses selection or freezes during background work | Viewmodel state, virtualization, thread/cancel contract | Real workflow UI checks; desktop. |
| R17 | P1 / medium | Permission denial/ejection corrupts state or opens wrong mount | Volume identity checks, targeted errors, recovery | Denial/eject/reconnect cases; platform. |
| R18 | P1 / certain for copied secrets | DPAPI keys cannot be used on Unix | Keychain/Secret Service and re-entry/session fallback | Migrated settings plus store failures; platform. |
| R19 | P1/P2 / high | Processor detection/arguments/COM fail | Per-platform adapters and manual export fallback | Real receivers/escaping; platform. |
| R20 | P0 / medium | Cross-platform release breaks Windows latest feed | Preserve schema 1, isolated beta, one coordinator | Old launcher + staged release tests; release. |
| R21 | P0 / medium | Wrong/unsigned/replayed update payload accepted | Artifact selection/trust validation; manual install first | Feed negative tests and final-byte report; release. |
| R22 | P1 / medium | Windows maintenance regresses during port | Shared checks, stable WPF release path, small PRs | Windows installer/workflow regression evidence; core/release. |
| R23 | P2 / high | Full video/RAW or sandbox integration delays launch | Truthful original-only/partial capability scope | Format matrix/docs/unsupported UI; product/imaging. |
| R24 | P1/P2 / medium | Packaging channel rejects terms/dependencies | Dependency notices/channel review; first-party downloads first | License inventory and accepted channel policy; release/product. |
| R25 | P1 / medium-high | Support matrix grows beyond test capacity | Support only combinations with current evidence/owners | Release checklist and hardware matrix; release owner. |

## 20. Alpha, beta, release candidate, and launch gates

### 20.1 Alpha gate

An alpha may have known UI/format limitations, but must clearly identify itself and use copied/disposable archives. Required before wider distribution:

- Core import/export hashes verified on both platforms.
- Safe source retention and unsupported-feature UI.
- Portable schema/path decisions and Windows compatibility boundary documented.
- No known escape from the selected archive and no silent empty-index fallback.
- Linux packages launch on at least one clean supported system.
- Mac public alpha packages use the intended signing/notarization route.
- Crash/permission errors produce actionable redacted diagnostic information.

### 20.2 Beta programme

Recruit approximately 10–20 technically comfortable testers if feasible, chosen for coverage rather than download counts: Apple Silicon/Intel where promised, GNOME/KDE, APFS/ext4/exFAT, telescope/card/network source layouts, large archives, and Siril/PixInsight/GIMP users.

Ask testers to use verified copies/backups initially, perform prescribed workflows, and record versions/platform/filesystem/operation. Do not require uploading entire private archives. Provide a small diagnostic export with opt-in file examples and redacted paths. No automatic telemetry or image upload is assumed; any future telemetry needs its own product decision.

Track unique completed end-to-end workflows, recovery cases, portable archive round trips, and package installations. A week with no reported bugs is not strong evidence if no one exercised the relevant path. Require at least two beta iterations when platform fixes affect archive behavior or distribution.

### 20.3 Release-candidate gate

Every advertised platform/artifact must have:

- No unresolved P0; no P1 in core workflows on a supported target.
- Completed legacy/current archive round trips and recovery matrix with independent review.
- Clean-machine installed-artifact results, supported minimum OS/ABI, and qualified native dependencies.
- Valid final hashes/signatures and Mac notarization/tickets, with downloaded-artifact checks.
- Install/replace/remove behavior that retains archives/settings and recoverable state.
- Accurate format/processor/permission/update documentation.
- A known-good previous installer plus archive compatibility information for recovery.
- Measured reference performance/memory and no unresolved retained leak.
- Release evidence bound to the immutable source commit and shipped bytes.
- An assigned first-week release/support owner and stop-ship procedure.

For a declared support cell, missing evidence means reduce the support claim or block that artifact. A waived P2 must have a documented user impact, workaround, owner, and follow-up; an integrity issue cannot be waived through a release-note paragraph.

### 20.4 Staged stable launch

1. Freeze source/version/dependency inputs; recheck support policies.
2. Build and qualify final artifacts; retain all reports.
3. Stage a draft release with legacy Windows assets/feed and Unix artifacts/new metadata as applicable.
4. Download the staged bytes through the intended channel and repeat final package checks.
5. Update platform download documentation from actual qualified artifacts. Website work occurs in its existing separate Sites project when explicitly scheduled; this document does not publish it.
6. Promote the release, preserving old releases and immutable versioned downloads.
7. Begin with beta/early adopters and expand visibility after 48–72 hours of meaningful clean use if the channel permits. Distinguish actual rollout controls from merely waiting before a broader announcement.
8. Review incidents daily during the first week and after a short stabilization period before adding new distribution channels/features.

Do not send announcements or tester invitations automatically from this planning task. Prepare copy and target links during implementation, then use the project's authorized communication workflow.

## 21. Incident response, rollback, and support

### 21.1 Stop-ship triggers

Immediately halt affected promotion/update delivery for verified unexpected deletion, changed original bytes, silent metadata/history loss, wrong archive/mount writes, containment failure, incompatible writer acceptance, wrong-architecture/wrong-platform update selection, or artifact trust mismatch. Ordinary missing optional codec support is handled according to the published capability contract.

### 21.2 Response sequence

1. Identify affected version, RID, filesystem, operation, and archive format. Preserve the immutable release/artifacts and diagnostic evidence.
2. Pause the affected channel/promotion through the release coordinator. Do not delete the only recoverable installer or overwrite published bytes.
3. Publish a precise known-issue message with affected conditions, immediate safe action, and recovery route through authorized channels.
4. Preserve user recovery journals, local index, portable snapshots, originals, and verified copies. Advise copies before repair when diagnosis depends on state.
5. Reproduce on a minimal fixture; isolate whether the issue is parser, serialization, file commit, snapshot, desktop, package, or processor behavior.
6. Build a new patch/package revision, run targeted checks plus affected release gates, then promote with explicit compatibility notes.

### 21.3 Application rollback versus archive rollback

An older app may be safe to reinstall only if it supports the current archive format/writer contract. Feed anti-downgrade behavior and intentional emergency rollback are separate mechanisms; use an explicit qualified procedure, not a silent version comparison bypass.

Database rollback must not be automatic. Images, sidecars, deletion history, and Edited versions may have changed after the snapshot. A repair/migration recovery tool must account for them and operate on a verified copy when necessary. Restore a full verified archive backup when that is the chosen recovery route, retaining the damaged/current state for reconciliation.

### 21.4 Support evidence

Issue templates should collect app/package version, RID/OS, install type, filesystem/provider, operation stage, exact redacted error, codec/processor version, and whether the archive was migrated/restored. Provide a diagnostic command/UI action that outputs dependency/capability/state information without secrets, images, personal coordinates, or unredacted paths by default.

Documentation needs separate Mac Gatekeeper/permissions/Keychain guidance and Linux executable/FUSE/native dependency/desktop/keyring guidance, plus common archive recovery. Keep the complete offline guide usable in the app.

## 22. Minimum launch scope versus follow-up roadmap

| Launch-blocking work | Safely staged follow-up |
| --- | --- |
| Modern portable build and native desktop shell | Replacing Windows WPF entirely |
| Legacy JSON/path/schema compatibility and writer gates | Universal Mac bundle |
| Verified imports/exports, metadata, Edited, backup/recovery | Linux ARM64 or additional architectures |
| Local-volume containment, identity, locking, snapshots | Safe multi-host writable network/cloud archives |
| Supported FITS/XISF/SER and qualified scientific TIFF/PNG | Broader proprietary RAW decoding |
| Truthful optional-codec/media capabilities | Complete media playback matrix |
| Core native Siril/PixInsight/GIMP handoffs where advertised | Wine-assisted Windows processor adapters |
| Mac signed/notarized DMG; Linux AppImage/archive | Mac App Store, Flatpak, Snap, native repositories |
| Stable channel/legacy Windows update compatibility | Automatic install/update mechanisms |
| Cross-platform numeric/integrity/package tests and docs | Unix source deletion and OS-level delete protection |

Scope reductions must preserve complete everyday workflows. For example, dropping automatic update installation is acceptable with clear download/replacement guidance; dropping verified backup or correct archive reopen behavior is not.

## 23. Concrete implementation backlog

Create repository issues/work items during implementation, using the following titles, dependencies, and definitions of done. This planning task does not create external issues or change application code.

| Backlog item | Depends on | Definition of done |
| --- | --- | --- |
| Establish SDK solution and legacy build comparison | Baseline | Same Windows behavior/assets from a documented build; portable thin project runs. |
| Capture golden legacy archive and JSON corpus | Baseline | Versioned synthetic fixtures cover settings, files, manifests, histories, Edited, and protected/backup variants. |
| Decide Avalonia/backend/version and preview/table viability | Thin build | Recorded real Mac/Linux slice, accessibility/performance findings, architecture decision. |
| Define archive-path codec and collision policy | Golden corpus | Legacy/new paths and normalization/case corpus pass; traversal/absolute ambiguity rejected. |
| Add archive version/minimum writer and migration journal | Path/DTO decision | Old/new writer behavior, interrupted migration, pre-migration recovery verified. |
| Ship compatible Windows archive reader/writer if required | Format definition | Real supported Windows round trip; no field loss on rewrite. |
| Replace serializer through persisted DTOs | Golden corpus | Legacy semantic equality, fields/ignores/defaults, unknown-property policy verified. |
| Introduce SQLite provider and backup parity | SDK/DTOs | Actual RIDs run CRUD/transactions/snapshot/integrity/recovery checks. |
| Implement portable/local snapshot coherence | Archive/provider | Stale-host and uncheckpointed-conflict cases cannot overwrite newer state. |
| Implement Unix containment and commit adapters | Path contract | Real link/race/permission/disk-full/cancellation tests; no out-of-root writes. |
| Implement repository identity/locks/read-only behavior | Storage/filesystem | Alias/account/crash tests; genuine read-only open performs no metadata writes. |
| Implement mounted-volume/telescope binding | Platform adapters | Reconnect/eject/ambiguous-volume tests on real devices. |
| Expose source-retention and protection limitations | Capability model | Unix menus/status/guide match implemented guarantees; Dump does not falsely report removal. |
| Implement Keychain/Secret Service/session credentials | Platform model | No plaintext fallback or key in logs; migrated DPAPI handled explicitly. |
| Add timezone mapping/evidence tests | DTOs | Windows/IANA/DST/unknown-time cases preserve correct UTC/provenance. |
| Port main desktop pages and persistent selection | Core/viewmodels | Keyboard/mouse/async navigation, tables/search/columns and activity workflows pass. |
| Implement scientific raster reader and metadata | Imaging seams | Full-precision TIFF/PNG corpus and evidence extraction pass. |
| Package and resolve native codecs per RID | Native policy | Signed Mac code; ELF/Mach-O dependencies and absent/wrong-library cases validated. |
| Implement preview lifecycle and global memory budget | Desktop/imaging | No stale frames, resource leaks, or unbounded full-resolution concurrency. |
| Qualify media playback or document original-only support | Backend spike | Capability matrix and actual package tests align. |
| Implement native processor/solver adapters | Export/platform | Real receivers, argv/script escaping, timeouts, cancellation, launch failures covered. |
| Build/sign/notarize Mac DMGs | Early signing proof/native build | Browser-downloaded ARM64/x64 artifacts pass Gatekeeper and offline tests where promised. |
| Build Linux AppImage/tar and desktop integration | Native Linux build | Clean declared distros and chosen display backends pass. |
| Add platform feed/schema-1 compatibility/coordinator | Version/package contract | Old Windows clients and new target selection; draft/retry/immutable promotion tests. |
| Complete cross-platform recovery/round-trip qualification | All core work | Test matrix evidence, no P0/P1 in advertised workflows, independent review. |
| Update offline docs/download page/support templates | Final capabilities | Every instruction/link corresponds to a qualified artifact/feature. |
| Run beta/RC rehearsal and release decision | All mandatory gates | Completed tester coverage, final-byte evidence, rollback/support ownership. |

## 24. Planning completion and remaining decisions

This plan is complete as a repository-based risk and delivery proposal. The next action is the ten-day discovery spike, not public publication. Before committing the implementation schedule, decide: staffed capacity, Mac Intel launch requirement, initial Linux distro list, UI/framework version, precise archive compatibility route, numeric raster/media dependencies, signing readiness, and whether automatic update installation is intentionally deferred.

Use the conservative initial assumptions in this document when decisions remain open: preserve Windows maintenance; native ARM64 Mac and Linux x64 first; Intel only with real qualification; local writable archives; original sources retained; direct downloads; full archive integrity and verified recovery required. Update this document with spike results and actual assigned owners rather than presenting assumptions as established product capability.

## 25. Source references and revalidation checklist

Repository evidence is tied to the inspected commit. Re-open these files when implementation changes them:

- Architecture/build: `README.md`, `Application_Source/build.ps1`, `App.cs`, `MainWindow.xaml`, `docs/DEVELOPMENT.md`.
- Data/path/storage: `Model.cs`, `Repository.cs`, `ImportEngine.cs`, `FileState.cs`, `ArchiveReset.cs`, `ArchiveBackup.cs`, `EditedWorkspace.cs`, `AssociatedMetadata.cs`.
- Deletion/protection: `SourceCleanup.cs`, `FileDeletion.cs`, `EditedDeletion.cs`, `ArchiveProtection.cs`.
- Imaging: `Compatibility.cs`, `FitsAssets.cs`, `XisfReader.cs`, `RasterReader.cs`, `RasterHeaders.cs`, `PreviewData.cs`, `Preview.cs`, `MotionPreviewUi.cs`.
- Platform/integrations: `TelescopeConnections.cs`, `ExternalApps.cs`, `PlateSolve.cs`, `MetadataEditing.cs`, `Theme.cs`, `NativeFolderPicker.cs`.
- Release: `Installer/release.json`, `Installer/Updates.cs`, `Installer/ReleaseMonitor.cs`, `Installer/build.ps1`, `.github/workflows/windows.yml`, `scripts/build-release.ps1`, `scripts/verify-release-report.py`, `docs/RELEASING.md`, `docs/UPDATES.md`.
- Licensing/support: `LICENSE`, `LICENSING.md`, all catalogue notices, `docs/COMPATIBILITY.md`, `docs/PROCESSOR_HANDOFFS.md`, `docs/SECURITY.md`.

External primary sources checked during planning:

- [.NET support lifecycle](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- [.NET 10 supported operating systems](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).
- [.NET 10 Linux dependencies](https://github.com/dotnet/core/blob/main/release-notes/10.0/dotnet-dependencies.md).
- [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms).
- [Avalonia macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos).
- [Avalonia desktop Linux deployment](https://docs.avaloniaui.net/docs/deployment/linux).
- [MAUI experimental platform backends](https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/platform-backends/?view=net-maui-10.0).
- [.NET Standard compatibility](https://learn.microsoft.com/dotnet/standard/net-standard?tabs=net-standard-2-0).
- [Apple notarization workflow](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow).
- [AppImage FUSE troubleshooting](https://docs.appimage.org/user-guide/troubleshooting/fuse.html).
- [Flatpak sandbox permissions](https://docs.flatpak.org/en/latest/sandbox-permissions.html).
- [XDG base directories](https://specifications.freedesktop.org/basedir/latest/).

Revalidate runtime patch/support, exact framework version/backends, OS lifecycle, hosted-runner architectures, signing/notarization requirements, library redistribution terms, and each advertised processor protocol at implementation and release freeze. Those facts can change over the proposed delivery period.
