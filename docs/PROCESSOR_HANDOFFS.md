# Direct processing handoffs

Verified on 7 October 2026. A handoff must open the supplied inputs in the
receiving application's workspace. Merely launching an application, copying a
folder, or writing a script that the user still has to load does not qualify.

## Implemented routes

| Receiver | Inputs | Method | Reliability boundary |
| --- | --- | --- | --- |
| AstroWizard (priority for pre-stacked data) | One non-rejected `.fit` or `.fits` stack | Launch the verified Windows executable with the working copy's absolute path as its one positional argument. | Official signed **Clear Eyes, 7 October 2026** build only; checked by SHA-256 before export and again before launch. |
| Siril | One non-rejected uncompressed FITS stack | Launch `siril.exe` with the working copy's absolute path as its one positional argument. | Documented GUI image-open contract. `siril-cli.exe`, multiple inputs and gzip are excluded from this route. |

Select a stack in the library, right-click **Export → Send stack to AstroWizard…**
or **Send stack to Siril…**, locate the executable and choose a working-copy
destination. The selected capture is copied and SHA-256 verified using the
existing exporter. The receiving program opens that copy; processing never
targets the archive copy. Exported metadata and checksums remain in the new
project. Launch failures retain the copy and report its path.

AstroArchive remembers each executable location. Shell execution is disabled;
paths containing spaces or Unicode remain one argument. It opens a new process,
rather than injecting files into an existing session. A successful process
launch is reported as a launch, not as confirmation that a GUI finished loading.

### AstroWizard evidence

The official StackingWizard Windows **Beta 4.7.1** build calls AstroWizard with
the executable followed by stack paths. The official AstroWizard Windows
**Clear Eyes** build consumes existing image paths from its startup arguments.
It schedules its image-loading method for a single path and its stack-set method
for multiple paths. The single-image receiver was exercised directly from the
official build's startup dispatch with recording UI methods; spaces and Unicode
were preserved. `.fts` and gzip paths were ignored by that receiver, so those
extensions are excluded from the AstroArchive action.

Verified vendor downloads and published checksums:

- [AstroWizard Clear Eyes release](https://github.com/lukomaticoYT/astrowizard-releases/releases/tag/2026-10-07)
  — signed `AstroWizard.exe` SHA-256
  `200f8eb21079cba4d4de0482e52265d18ca69425bcb49f513e622c7bc504f803`.
- [StackingWizard Beta 4.7.1 release](https://github.com/lukomaticoYT/astrowizard-releases/releases/tag/stackingwizard-beta-2026-10-07)
  — signed `StackingWizard.exe` SHA-256
  `bd1bd470b9946e9155bfa77a5e58ad3b282b05f5250647871fe88dddcaaad501`.
- [Official Wizard workflow and guide](https://astrowizard.lukomatico.com/stackingwizard.html).
- [Siril's GUI/CLI launch contract](https://siril.org/docs/man/).

Vendor executables are downloaded separately from their official sources;
AstroArchive neither bundles nor redistributes them. The AstroWizard hash gate
is deliberate because this startup mechanism is verified implementation
behavior rather than a promised public API. A newer or older build is refused
before creating the export until its receiving behavior is verified and added.
Windows GUI execution has not been exercised in this Linux workspace.

## Remaining plan, in priority order

1. **StackingWizard for non-stacked captures.** Its own startup entry point in
   the verified Windows build does not consume command-line files or folders.
   Its outgoing AstroWizard handoff proves the AstroWizard receiver, not a
   StackingWizard receiver. Enable a direct action only after an official
   incoming file/folder/manifest contract is available and verified. Export one
   target and compatible camera/filter group per job, preserve raw/master
   calibration distinctions, and omit rejected inputs by default. Keep the
   existing ready-to-stack folder export as the manual workflow meanwhile.
2. **Siril for non-stacked captures.** Use the official CLI to prepare a real
   sequence in an isolated working folder, then launch the GUI with the resulting
   `.seq` file. Verify conversion output, exit status and actual sequence loading
   against supported Windows versions before enabling this route. Do not pass
   many unrelated FITS paths as though they were a sequence. Script launch with
   `-s` runs headlessly and does not itself satisfy the GUI handoff requirement.
3. **AstroWizard filter sets.** Its multi-file receiver exists, but combining
   stacks needs validated filter labels, a common target/canvas and one compatible
   filter family. Verify those conditions and test its stack-set receiving path
   before adding multiple-selection handoff. Do not mix live-stack snapshots or
   constituent subs with independent stacks.
4. **Other processors, including PixInsight, DeepSkyStacker and AstroPixelProcessor.**
   Verify each program's documented incoming image/project contract and GUI
   behavior first. Supporting FITS alone does not establish direct import.
   Native project/file-list formats may be useful, but are not enabled until
   their receiver and required metadata are tested.

For every new route: verify installation/version before exporting; preserve
selection, camera, target and calibration boundaries; use verified copies;
test whitespace/Unicode, missing files, cancellation and launch failure; and
confirm that the receiving workspace actually has the requested image/sequence.
Avoid UI automation, clipboard pasting, guessed flags and undocumented watched
folders as substitutes for a verified receiver.

## Dump inbox

Every opened archive gets a **`Dump`** folder directly inside its selected root.
Startup, or switching to another repository, scans it recursively and imports
supported FITS through the existing classifier and verified copy pipeline.
**Settings → Open dump folder** shows it; **Process dump folder now** retries
without restarting. There is no continuous watcher: finish copying into the
folder before starting the program or choosing the retry action.

Only successfully processed FITS are removed. Removal requires a committed index,
a successful portable checkpoint, unchanged source state, and native Windows
handle checks of both physical paths, identities and SHA-256 hashes. Verified
duplicates are also removed, while archive metadata edits are preserved. A
missing archive copy is restored from the drop. Locked, changed, unreadable or
failed inputs remain for the next attempt; cancellation retains pending inputs.
Non-FITS files, session JSON and subfolders remain. Session JSON needed by new
imports is copied into archive metadata before their FITS source is removed.

Drop recognition is per file in Auto mode. Physical telescope IDs cannot be
deduced reliably from a mixed drop, so they start as **Unknown**; use **Edit
metadata** to assign the device before automatic calibration matching. Model
and camera overrides from the regular Import page are not applied to the inbox.
Dump work runs in the background with progress and cancellation. Errors and
cleanup failures are shown for review and saved in
`.astroarchive/reports/last-dump.txt`.

The inbox is the only permitted nested import source. Ordinary imports retain
their separate-source requirement, physical cleanup cannot leave `Dump`, linked
inbox roots are refused, and repository reindexing excludes pending inbox files.

## Validation for this source change

- WPF application compiled against Microsoft .NET Framework 4.8 reference
  assemblies with C# 5 compatibility.
- **71 regression tests passed** on Linux/Mono, including the optional test
  against the official AstroWizard executable. This checks the version gate,
  working-copy hashes and launch arguments; it does not launch the Windows GUI.
- **7 Windows-only tests skipped** here. They cover native cleanup/locking,
  hard-link guards and trusted file identity; `Application_Source/test.ps1`
  includes them for Windows execution.
- The official AstroWizard startup dispatch passed four receiver-contract
  checks: single FITS with spaces/Unicode, a two-file set, ignored `.fts`, and
  ignored gzip. The recording UI substitutes verify dispatch, not image rendering.
- XAML syntax, source inclusion and whitespace checks passed. Full Windows
  GUI smoke tests and interactive receiver loading remain unverified locally.

Repository handoffs now create and register an Edited project automatically. Both editors open its verified working image. Project folders and acquisition metadata remain portable with the repository. Editor outputs saved in the same folder appear on refresh.
