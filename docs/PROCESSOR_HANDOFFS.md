# Direct processing handoffs

A handoff must open the supplied inputs in the
receiving application's workspace. Merely launching an application, copying a
folder, or writing a script that the user still has to load does not qualify.

## Implemented routes

| Receiver | Inputs | Method | Reliability boundary |
| --- | --- | --- | --- |
| Siril | One non-rejected uncompressed FITS stack | Launch `siril.exe` with the working copy's absolute path as its one positional argument. | Documented GUI image-open contract. `siril-cli.exe`, multiple inputs and gzip are excluded from this route. |

Select a stack in the repository, right-click **Export → Send stack to Siril…**,
locate the executable and name an Edited project. The selected capture is copied
and SHA-256 verified into the repository's managed Edited workspace. The receiving
program opens that copy; processing never targets the archive copy. Acquisition
metadata stays with the project, and outputs saved alongside the copy appear on
return or refresh. Launch failures retain the copy and report its path.

AstroArchive remembers the Siril executable location. Shell execution is disabled;
paths containing spaces or Unicode remain one argument. It opens a new process,
rather than injecting files into an existing session. A successful process
launch is reported as a launch, not as confirmation that a GUI finished loading.

### Completed stacking-folder app launchers

After **Export → Ready-to-stack folder…** (with or without calibrations), the
completion popup offers **Siril**, **StackingWizard** and **Other…**, alongside
**Open exported folder**. These start installed applications for manual input
loading. Siril uses its documented `--directory` option to select the export as
its working directory; this does not convert subs or create/load a sequence.
StackingWizard and other apps start without file/folder arguments, with the
export as their process working directory. Their own folder pickers may choose
a different initial location.

The first click asks for an installed executable. Siril shares the saved GUI
location with its single-stack route; StackingWizard saves its own location.
Other… always opens an executable chooser seeded with the previous choice.
The popup remains open, missing applications can be relocated, and launch
failures show the retained export path. No installation/download or changes to
exported inputs are involved. This launcher feature does not establish new
direct processing handoffs in the table above.

### AstroWizard: export and open manually

Direct sending to AstroWizard was removed in package **1.10.4.1**. The previous
route accepted a single exact executable checksum. That check rejected other
official releases, and its startup file-loading behavior was verified for only
one build rather than a supported contract across builds. Removing the checksum
check alone would not establish compatibility.

Use **Export → Create Edited working copies…**, name a project, then open the
verified copies from within AstroWizard or another editor. The project is registered
in Edited and its folder opens for manual loading. Default application uses the
Windows file association to open a selected Edited image. Ordinary file exports
remain available for destinations outside the repository.

- [Official AstroWizard downloads](https://github.com/lukomaticoYT/astrowizard-releases)
- [Siril's GUI/CLI launch contract](https://siril.org/docs/man/).

Vendor executables are downloaded separately from their official sources;
AstroArchive neither bundles nor redistributes them.

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
3. **Other processors, including PixInsight, DeepSkyStacker and AstroPixelProcessor.**
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

## Validation

Application and release checks compile the WPF application against .NET Framework
4.8, run the engine and installer/updater suites, and exercise the Windows UI.
The Siril regression verifies single-stack eligibility, working-copy hashes,
launch arguments with spaces/Unicode, rejected inputs and invalid executables.
It does not launch a separately installed Siril GUI.
